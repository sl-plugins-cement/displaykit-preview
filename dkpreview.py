#!/usr/bin/env python3
"""DisplayKit preview loop.

  python dkpreview.py run     <scenario.csproj|dir>            build + run every scenario -> out/<Project>/*.json
  python dkpreview.py watch   <scenario.csproj|dir> [--watch DIR ...] [--port 8765]
                                                               live page; rebuilds on .cs change
  python dkpreview.py png     <scenario.csproj|dir|scene.json> --scenario NAME [--frame N|all] [--viewer ID]
                              [--res 1920x1080] [--scale constant|1920x1080] [--cjk fallback|tofu] [--metrics client|reference]
                              [--badges] [--out FILE]
  python dkpreview.py unity   <same as png>                     UI Toolkit reference render (needs the built player)
  python dkpreview.py unity-build                               build unity/Build/DkRender.exe in Unity batchmode
  python dkpreview.py compare [--wiki DIR] [--unity] [--metrics client|reference]
                                                               wiki examples: wiki screenshot | HTML | Unity

Standard library only, plus Pillow for `compare`. Chrome or Edge renders PNGs headlessly.
"""
from __future__ import annotations

import argparse
import functools
import http.server
import json
import os
import shutil
import subprocess
import sys
import threading
import time
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE / "out"
RUNNER_PROJ = HERE / "src" / "DisplayKit.Preview.Runner" / "DisplayKit.Preview.Runner.csproj"
RUNNER_EXE = HERE / "src" / "DisplayKit.Preview.Runner" / "bin" / "Release" / "net9.0" / "DisplayKit.Preview.Runner.exe"
WIKI_PROJECT = HERE / "scenarios" / "WikiExamples" / "WikiExamples.csproj"
UNITY_EDITOR = Path(os.environ.get("DK_UNITY_EDITOR", r"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe"))
UNITY_PROJECT = HERE / "unity"
UNITY_PLAYER = UNITY_PROJECT / "Build" / "DkRender.exe"
BROWSERS = [
    os.environ.get("DK_CHROME", ""),
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "/usr/bin/google-chrome", "/usr/bin/chromium",
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
]

# Wiki examples: scenario -> (wiki image, frame index or None for last, viewer index into frame players).
WIKI_CASES = [
    ("quick-start", "example1.png", None, 0),
    ("creating-canvas", "example2.png", None, 0),
    ("creating-container", "example3.png", None, 0),
    ("creating-text", "example4.png", None, 0),
    ("nested", "example5.png", None, 0),
    ("centered", "example6.png", None, 0),
    ("full-screen-panel", "example7.png", None, 0),
    ("horizontal-layout", "example8.png", None, 0),
    ("vertical-layout", "example9.png", None, 0),
    ("welcome", "example1-welcome.png", 0, 0),
    ("health-bar", "example2-healthbar-green.png", 1, 0),
    ("health-bar", "example2-healthbar-red.png", 3, 0),
    ("announcement", "example3-announcement.png", 1, 0),
    ("scoreboard", "example4-scoreboard.png", 0, 0),
]


# ---------------------------------------------------------------- build + run

def find_project(arg: str) -> Path:
    p = Path(arg).resolve()
    if p.is_dir():
        projs = sorted(p.glob("*.csproj"))
        if len(projs) != 1:
            sys.exit(f"{p}: expected exactly one .csproj, found {len(projs)}")
        return projs[0]
    if p.suffix == ".csproj" and p.exists():
        return p
    sys.exit(f"not a scenario project: {arg}")


def out_dir_for(project: Path) -> Path:
    return OUT / project.stem


def ensure_runner() -> None:
    if RUNNER_EXE.exists():
        return
    print("building runner…")
    subprocess.run(["dotnet", "build", str(RUNNER_PROJ), "-c", "Release", "-nologo", "-v", "q"], check=True)


@functools.lru_cache(maxsize=None)
def target_path(project: Path) -> Path:
    r = subprocess.run(["dotnet", "msbuild", str(project), "-nologo", "-getProperty:TargetPath", "-p:Configuration=Debug"],
                       capture_output=True, text=True, check=True)
    return Path(r.stdout.strip())


def build_and_run(project: Path, scenario: str | None = None) -> tuple[bool, str, float]:
    """Returns (ok, message, seconds). Writes out/<Project>/*.json and _status.json."""
    ensure_runner()
    out = out_dir_for(project)
    out.mkdir(parents=True, exist_ok=True)
    t0 = time.time()
    write_status(out, "building", "")
    b = subprocess.run(["dotnet", "build", str(project), "-c", "Debug", "-nologo", "-v", "q", "-clp:NoSummary;ErrorsOnly"],
                       capture_output=True, text=True)
    if b.returncode != 0:
        msg = (b.stdout + b.stderr).strip()
        write_status(out, "error", msg, time.time() - t0)
        return False, msg, time.time() - t0
    args = [str(RUNNER_EXE), "--assembly", str(target_path(project)), "--out", str(out)]
    if scenario:
        args += ["--scenario", scenario]
    r = subprocess.run(args, capture_output=True, text=True)
    secs = round(time.time() - t0, 2)
    msg = (r.stdout + r.stderr).strip()
    write_status(out, "ok" if r.returncode == 0 else "failed", msg, secs)
    return r.returncode == 0, msg, secs


def write_status(out: Path, state: str, message: str, seconds: float = 0.0) -> None:
    (out / "_status.json").write_text(json.dumps({"state": state, "message": message, "seconds": round(seconds, 2), "time": time.time()}), encoding="utf-8")


def cmd_run(a) -> int:
    project = find_project(a.project)
    ok, msg, secs = build_and_run(project, a.scenario)
    print(msg)
    print(f"{'ok' if ok else 'FAILED'} in {secs}s -> {out_dir_for(project)}")
    return 0 if ok else 1


# ---------------------------------------------------------------- watch / serve

class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, *args):
        pass


def cmd_watch(a) -> int:
    project = find_project(a.project)
    watch_dirs = [project.parent] + [Path(d).resolve() for d in a.watch]
    out = out_dir_for(project)
    handler = functools.partial(QuietHandler, directory=str(HERE))
    server = http.server.ThreadingHTTPServer(("127.0.0.1", a.port), handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    rel = out.relative_to(HERE).as_posix()
    print(f"preview: http://127.0.0.1:{a.port}/html/index.html?dir={rel}")
    print("watching:", ", ".join(str(d) for d in watch_dirs), "(Ctrl+C to stop)")

    def snapshot():
        stamp = {}
        for d in watch_dirs:
            for p in d.rglob("*"):
                if p.suffix in (".cs", ".csproj", ".props") and "bin" not in p.parts and "obj" not in p.parts:
                    stamp[p] = p.stat().st_mtime
        return stamp

    last = None
    try:
        while True:
            now = snapshot()
            if now != last:
                last = now
                ok, msg, secs = build_and_run(project)
                print(time.strftime("%H:%M:%S"), "ok" if ok else "FAILED", f"{secs}s")
                if not ok:
                    print(msg)
            time.sleep(0.4)
    except KeyboardInterrupt:
        server.shutdown()
    return 0


# ---------------------------------------------------------------- PNG via headless browser

def find_browser() -> str:
    for b in BROWSERS:
        if b and Path(b).exists():
            return b
    for name in ("chrome", "msedge", "chromium", "google-chrome"):
        if shutil.which(name):
            return shutil.which(name)
    sys.exit("no Chrome/Edge found; set DK_CHROME")


def resolve_scene(a) -> Path:
    p = Path(a.source).resolve()
    if p.suffix == ".json":
        return p
    project = find_project(a.source)
    if not a.no_build:
        ok, msg, _ = build_and_run(project, a.scenario)
        if not ok:
            sys.exit(msg)
    if not a.scenario:
        sys.exit("--scenario is required with a project")
    return out_dir_for(project) / f"{a.scenario}.json"


def frame_indices(scene: dict, frame: str | None) -> list[int]:
    n = len(scene["frames"])
    if frame in (None, "last"):
        return [n - 1]
    if frame == "all":
        return list(range(n))
    return [int(frame) % n]


def render_html(scene: dict, frame: int, viewer, res: str, scale: str, cjk: str, badges: bool, png: Path,
                layout: Path | None = None, backdrop: str = "#2b2e2b", metrics: str = "client") -> None:
    w, h = (int(v) for v in res.split("x"))
    work = OUT / "_render" / uuid.uuid4().hex[:10]
    work.mkdir(parents=True)
    for f in ("render.html", "dk-render.js", "dk-render.css"):
        shutil.copy2(HERE / "html" / f, work / f)
    job = {"scene": scene, "frame": frame, "viewer": viewer, "width": w, "height": h, "scale": scale, "cjk": cjk,
           "badges": badges, "backdrop": backdrop, "metrics": metrics, "fontBase": (HERE / "fonts").as_uri() + "/"}
    (work / "job.js").write_text("window.DK_JOB = " + json.dumps(job, ensure_ascii=False) + ";", encoding="utf-8")
    url = (work / "render.html").as_uri()
    profile = OUT / "_chrome-profile"
    base = [find_browser(), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--allow-file-access-from-files",
            "--force-device-scale-factor=1", "--no-first-run", "--no-default-browser-check", f"--user-data-dir={profile}",
            f"--window-size={w},{h}", "--virtual-time-budget=4000", "--run-all-compositor-stages-before-draw"]
    png.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(base + [f"--screenshot={png}", url], capture_output=True, check=True, timeout=120)
    if layout:
        r = subprocess.run(base + ["--dump-dom", url], capture_output=True, check=True, timeout=120, text=True, encoding="utf-8")
        start = r.stdout.find('<pre id="dk-layout">')
        end = r.stdout.find("</pre>", start)
        text = r.stdout[start + len('<pre id="dk-layout">'):end]
        text = text.replace("&lt;", "<").replace("&gt;", ">").replace("&quot;", '"').replace("&amp;", "&")
        layout.write_text(text, encoding="utf-8")
    shutil.rmtree(work, ignore_errors=True)


def cmd_png(a) -> int:
    scene_path = resolve_scene(a)
    scene = json.loads(scene_path.read_text(encoding="utf-8"))
    frames = frame_indices(scene, a.frame)
    for i in frames:
        out = Path(a.out) if a.out and len(frames) == 1 else (Path(a.out).with_suffix("") / f"{scene['scenario']}-f{i}.png" if a.out else
              OUT / "png" / f"{scene['scenario']}-f{i}-{a.res}.png")
        t0 = time.time()
        render_html(scene, i, a.viewer, a.res, a.scale, a.cjk, a.badges, out.resolve(),
                    out.with_suffix(".layout.json").resolve() if a.layout else None, metrics=a.metrics)
        print(f"{out}  ({time.time() - t0:.1f}s)")
    return 0


# ---------------------------------------------------------------- Unity reference renderer

def unity_render(jobs: list[dict]) -> None:
    if not UNITY_PLAYER.exists():
        sys.exit(f"{UNITY_PLAYER} is not built. Run: python dkpreview.py unity-build "
                 "(Unity batchmode needs Unity Hub signed in on this Windows account).")
    path = OUT / "_unity-jobs.json"
    path.write_text(json.dumps({"jobs": jobs}), encoding="utf-8")
    log = OUT / "unity-render.log"
    subprocess.run([str(UNITY_PLAYER), "-batchmode", "-dkJobs", str(path), "-logFile", str(log)], timeout=600)
    failed = [j["out"] for j in jobs if not Path(j["out"]).exists()]
    if failed:
        sys.exit(f"Unity did not write {failed}; see {log}")


def cmd_unity(a) -> int:
    scene_path = resolve_scene(a)
    scene = json.loads(scene_path.read_text(encoding="utf-8"))
    w, h = (int(v) for v in a.res.split("x"))
    jobs = []
    for i in frame_indices(scene, a.frame):
        out = Path(a.out).resolve() if a.out else (OUT / "png" / f"{scene['scenario']}-f{i}-{a.res}.unity.png").resolve()
        jobs.append({"scene": str(scene_path), "frame": i, "viewer": a.viewer, "width": w, "height": h, "scale": a.scale,
                     "cjk": "system" if a.cjk == "fallback" else "none", "out": str(out), "layout": str(out.with_suffix(".layout.json"))})
    unity_render(jobs)
    for j in jobs:
        print(j["out"])
    return 0


def cmd_unity_build(a) -> int:
    if not UNITY_EDITOR.exists():
        sys.exit(f"Unity editor not found at {UNITY_EDITOR}; set DK_UNITY_EDITOR")
    subprocess.run([sys.executable, str(HERE / "fonts.py")], check=True)
    log = OUT / "unity-build.log"
    OUT.mkdir(exist_ok=True)
    r = subprocess.run([str(UNITY_EDITOR), "-batchmode", "-quit", "-projectPath", str(UNITY_PROJECT),
                        "-executeMethod", "DisplayKitPreviewBuild.BuildPlayer", "-logFile", str(log)])
    text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    if "Licensing is initialized" not in text and (
            "com.unity.editor.headless" in text or "Access token is unavailable" in text):
        print("Unity licensing refused batchmode: sign in to Unity Hub on this Windows account, then rerun.")
    for line in text.splitlines():
        if "error" in line.lower() and "Licensing" not in line and "d3d12" not in line:
            print("unity:", line.strip())
    print(f"exit {r.returncode}; log {log}")
    return 0 if UNITY_PLAYER.exists() else 1


# ---------------------------------------------------------------- wiki comparison

def find_wiki(arg: str | None) -> Path:
    if arg:
        return Path(arg).resolve()
    clone = OUT / "labapi-wiki"
    if not clone.exists():
        subprocess.run(["git", "clone", "--depth", "1", "https://github.com/northwood-studios/LabAPI.wiki.git", str(clone)], check=True)
    return clone / "Images"


def cmd_compare(a) -> int:
    from PIL import Image, ImageDraw

    wiki = find_wiki(a.wiki)
    project = WIKI_PROJECT
    ok, msg, _ = build_and_run(project)
    if not ok:
        sys.exit(msg)
    out = OUT / "compare"
    out.mkdir(parents=True, exist_ok=True)
    rows = []
    unity_jobs = []
    for scenario, image, frame, viewer_idx in WIKI_CASES:
        scene_path = out_dir_for(project) / f"{scenario}.json"
        scene = json.loads(scene_path.read_text(encoding="utf-8"))
        fi = frame if frame is not None else len(scene["frames"]) - 1
        viewer = scene["frames"][fi]["players"][viewer_idx]
        stem = Path(image).stem
        # Full-window wiki screenshots are rendered at their own size so positions line up; cropped ones at --res.
        wsize = Image.open(wiki / image).size
        res = f"{wsize[0]}x{wsize[1]}" if wsize[0] > 1000 else a.res
        html_png = out / f"{stem}.html.png"
        render_html(scene, fi, viewer, res, "constant", "fallback", False, html_png, out / f"{stem}.html.layout.json",
                    metrics=a.metrics)
        unity_png = out / f"{stem}.unity.png"
        if a.unity:
            w, h = (int(v) for v in res.split("x"))
            unity_jobs.append({"scene": str(scene_path), "frame": fi, "viewer": viewer, "width": w, "height": h, "scale": "constant",
                               "cjk": "system", "out": str(unity_png), "layout": str(out / f"{stem}.unity.layout.json")})
        rows.append((stem, wiki / image, html_png, unity_png))
    if unity_jobs:
        unity_render(unity_jobs)

    for stem, wiki_png, html_png, unity_png in rows:
        panels = [("wiki (in-game)", wiki_png), ("HTML", html_png)]
        if unity_png.exists():
            panels.append(("Unity UI Toolkit", unity_png))
        imgs = [(label, Image.open(p).convert("RGB")) for label, p in panels if p.exists()]
        if stem.startswith("example2-healthbar"):
            # The wiki crops the bar; crop the renders to the top-centre region at the same size for comparison.
            ww, wh = imgs[0][1].size
            imgs = [imgs[0]] + [(l, im.crop((im.width // 2 - ww // 2, 0, im.width // 2 + ww // 2 + ww % 2, max(wh, 120)))) for l, im in imgs[1:]]
        width = max(im.width for _, im in imgs)
        scale = 960 / width if width > 960 else 1
        tiles = [(l, im.resize((int(im.width * scale), int(im.height * scale)))) for l, im in imgs]
        sheet = Image.new("RGB", (sum(t.width for _, t in tiles) + 10 * (len(tiles) - 1), max(t.height for _, t in tiles) + 22), (20, 20, 22))
        x = 0
        draw = ImageDraw.Draw(sheet)
        for label, t in tiles:
            sheet.paste(t, (x, 22))
            draw.text((x + 4, 4), f"{label}", fill=(220, 220, 220))
            x += t.width + 10
        sheet.save(out / f"{stem}.compare.png")
        print(out / f"{stem}.compare.png")
    return 0


# ----------------------------------------------------------------

def main() -> int:
    sys.stdout.reconfigure(line_buffering=True)
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run"); r.add_argument("project"); r.add_argument("--scenario"); r.set_defaults(fn=cmd_run)
    w = sub.add_parser("watch"); w.add_argument("project"); w.add_argument("--watch", action="append", default=[])
    w.add_argument("--port", type=int, default=8765); w.set_defaults(fn=cmd_watch)

    for name, fn in (("png", cmd_png), ("unity", cmd_unity)):
        s = sub.add_parser(name)
        s.add_argument("source", help="scenario project/dir or a scene .json")
        s.add_argument("--scenario"); s.add_argument("--frame"); s.add_argument("--viewer", type=int)
        s.add_argument("--res", default="1920x1080"); s.add_argument("--scale", default="constant")
        s.add_argument("--cjk", default="fallback", choices=["fallback", "tofu"])
        s.add_argument("--metrics", default="client", choices=["client", "reference"],
                       help="client: the 15.0 client's text metrics (default); reference: plain UI Toolkit, as the Unity renderer")
        s.add_argument("--badges", action="store_true"); s.add_argument("--layout", action="store_true")
        s.add_argument("--no-build", action="store_true"); s.add_argument("--out")
        s.set_defaults(fn=fn)

    ub = sub.add_parser("unity-build"); ub.set_defaults(fn=cmd_unity_build)
    c = sub.add_parser("compare"); c.add_argument("--wiki"); c.add_argument("--unity", action="store_true")
    c.add_argument("--metrics", default="client", choices=["client", "reference"])
    c.add_argument("--res", default="1920x1080"); c.set_defaults(fn=cmd_compare)

    a = p.parse_args()
    return a.fn(a)


if __name__ == "__main__":
    sys.exit(main())
