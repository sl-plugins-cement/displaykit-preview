"""Render the role intro review set over an in-game backdrop: one sheet per kind showing every family at
1920x1080 and 1280x720, full-intro timelines, the native intro beside ours at the same beats, and the no-CJK
worst cases.

    python scenarios/RoleIntro/render.py [--backdrop <png>] [--native <mp4>] [--no-build]

Run from the repository root. Output: out/RoleIntro/png/. The backdrop should be a real client capture with
the HSM HUD visible, so overlap with the HUD shows up; without one a flat grey is used. The native video is a
14.2 client capture whose role changes show the native intro (see README, "Native reference").
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOL = HERE.parents[1]
sys.path.insert(0, str(TOOL))
import dkpreview  # noqa: E402

from PIL import Image, ImageDraw, ImageFont  # noqa: E402

SCENES = TOOL / "out" / "RoleIntro"
PNG = SCENES / "png"

FULL = [("full-mtf-nu7", "Foundation · MTF Nu-7"), ("full-mtf-omega1", "Foundation · Omega-1"),
        ("full-scp-001a", "SCP · SCP-001-A conversion"), ("full-scp-999", "SCP · SCP-999 (Tutorial)"),
        ("full-chaos", "Chaos Insurgency · Crownfall"), ("full-goc", "GOC"), ("full-serpent", "Serpent's Hand"),
        ("full-mcd", "MC&D"), ("full-event", "Event · City Siege")]
CARD = [("card-mtf-manager", "Foundation · facility manager"), ("card-mtf-delta4", "Foundation · Delta-4 kit"),
        ("card-mtf-o11", "Foundation · O-11 sub-role"), ("card-scp-tier", "SCP · Tiers upgrade"),
        ("card-chaos", "Chaos Insurgency · kit"), ("card-goc", "GOC · sub-role"), ("card-serpent", "Serpent's Hand · sub-role"),
        ("card-mcd", "MC&D · sub-role"), ("card-event", "Event · role upgrade")]
RESOLUTIONS = ["1920x1080", "1280x720"]
CJK_FONT = "C:/Windows/Fonts/msyh.ttc"   # stands in for the client's Noto Sans CJK in the hint mocks
LABEL_FONT = "C:/Windows/Fonts/segoeui.ttf"


def scene(name: str) -> dict:
    return json.loads((SCENES / f"{name}.json").read_text(encoding="utf-8"))


def frame_of(sc: dict, prefix: str) -> int:
    return next(i for i, f in enumerate(sc["frames"]) if str(f.get("label", "")).startswith(prefix))


def render(name: str, frame: int, res: str, out: Path, backdrop: str, cjk: str = "fallback", layout: bool = False,
           scale: str = "constant") -> Path:
    dkpreview.render_html(scene(name), frame, None, res, scale, cjk, False, out.resolve(),
                          out.with_suffix(".layout.json").resolve() if layout else None, backdrop=backdrop)
    return out


def font(path: str, size: int):
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default()


def sheet(cells: list[tuple[Path, str]], cols: int, cell_w: int, title: str, out: Path) -> None:
    imgs = [Image.open(p).convert("RGB") for p, _ in cells]
    cell_h = round(imgs[0].height * cell_w / imgs[0].width)
    rows = (len(imgs) + cols - 1) // cols
    head, pad, cap = 56, 12, 30
    canvas = Image.new("RGB", (cols * (cell_w + pad) + pad, head + rows * (cell_h + cap + pad) + pad), (18, 20, 23))
    d = ImageDraw.Draw(canvas)
    d.text((pad, 14), title, fill=(235, 238, 240), font=font(LABEL_FONT, 26))
    for i, (img, (_, label)) in enumerate(zip(imgs, cells)):
        x = pad + (i % cols) * (cell_w + pad)
        y = head + (i // cols) * (cell_h + cap + pad)
        d.text((x, y + 2), label, fill=(170, 178, 186), font=font(LABEL_FONT, 18))
        canvas.paste(img.resize((cell_w, cell_h), Image.LANCZOS), (x, y + cap))
    out.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out)
    print(out)


def native_frames(video: Path, times: list[float], work: Path) -> list[Path]:
    work.mkdir(parents=True, exist_ok=True)
    out = []
    for i, t in enumerate(times):
        p = work / f"native-{i}.png"
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", f"{t:.2f}", "-i", str(video), "-frames:v", "1", str(p)],
                       check=True)
        out.append(p)
    return out


# Where the hint mock puts each omitted line of a full intro: the native anchors as fractions of the height.
FULL_HINT_Y = {"Lead": 0.304, "Title": 0.428, "Faction": 0.549, "Flavor": 0.585, "Objective": 0.594, "Help": 0.757}
HINT_SIZE = {"Lead": 38, "Title": 88, "Faction": 24, "Flavor": 22, "Objective": 28, "Help": 30, "Badge": 18}
HINT_COLOUR = {"Lead": (255, 255, 255), "Title": (102, 180, 237), "Faction": (150, 190, 235), "Flavor": (184, 196, 206),
               "Objective": (250, 250, 247), "Help": (208, 208, 207), "Badge": (229, 236, 242)}


def mock_full_hint(png: Path, lines: list[tuple[str, str]], res: str, out: Path) -> None:
    """Omit mode, full intro: the plugin's one centred HSM hint carries the Chinese lines at the native anchors.
    HSM scales with screen height, so these hold their place at every resolution."""
    w, h = (int(v) for v in res.split("x"))
    s = h / 1080
    img = Image.open(png).convert("RGB")
    d = ImageDraw.Draw(img)
    for kind, text in lines:
        f = font(CJK_FONT, round(HINT_SIZE[kind] * s))
        plain = text.replace("<color=#C8CB02>", "").replace("</color>", "")
        tw = d.textlength(plain, font=f)
        x, y = (w - tw) / 2, FULL_HINT_Y[kind] * h - HINT_SIZE[kind] * s * 0.6
        d.text((x + 2, y + 2), plain, fill=(0, 0, 0), font=f)
        d.text((x, y), plain, fill=HINT_COLOUR[kind], font=f)
    d.text((round(w * 0.02), round(h * 0.07)), "Chinese lines: HSM hint mock (not DisplayKit)", fill=(255, 200, 60),
           font=font(LABEL_FONT, round(18 * s)))
    img.save(out)
    print(out)


def mock_card_hint(png: Path, lines: list[tuple[str, str]], res: str, out: Path) -> None:
    """Omit mode, card: the Chinese lines as one left-aligned hint ending 14 px (1080p units) above the card."""
    w, h = (int(v) for v in res.split("x"))
    layout = json.loads(png.with_suffix(".layout.json").read_text(encoding="utf-8"))
    card = next(e for e in layout["elements"] if e["path"].count("/") == 1)
    img = Image.open(png).convert("RGB")
    d = ImageDraw.Draw(img)
    s = h / 1080
    sizes = {"Title": 42, "Flavor": 21, "Objective": 23, "Badge": 18, "Faction": 18}
    heights = [round((sizes[k] + 10) * s) for k, _ in lines]
    x, y = round(card["x"] + 25), round(card["y"] - 14 * s - sum(heights))
    for (kind, line), lh in zip(lines, heights):
        f = font(CJK_FONT, round(sizes[kind] * s))
        d.text((x + 2, y + 2), line, fill=(0, 0, 0), font=f)
        d.text((x, y), line, fill=(255, 255, 255) if kind == "Title" else (229, 236, 242), font=f)
        y += lh
    d.text((round(w * 0.42), round(h * 0.07)), "Chinese lines above the card: HSM hint mock (not DisplayKit)",
           fill=(255, 200, 60), font=font(LABEL_FONT, round(18 * s)))
    img.save(out)
    print(out)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--backdrop", type=Path)
    ap.add_argument("--native", type=Path, help="reference clip trimmed to start at the role intro")
    ap.add_argument("--no-build", action="store_true")
    a = ap.parse_args()
    for option, path in [("--backdrop", a.backdrop), ("--native", a.native)]:
        if path is not None and not path.is_file():
            ap.error(f"{option}: file not found: {path}")
    if not a.no_build:
        subprocess.run([sys.executable, str(TOOL / "dkpreview.py"), "run", str(HERE)], check=True)
    backdrop = f"#2b2e2b url('{a.backdrop.resolve().as_uri()}') center/cover no-repeat" if a.backdrop else "#2b2e2b"

    # 1. One sheet per kind, every family, both resolutions. Full intros at their brightest settled frame.
    for kind, items, hold in [("full-intro", FULL, "hold full"), ("upgrade-card", CARD, "hold ")]:
        for res in RESOLUTIONS:
            cells = [(render(n, frame_of(scene(n), hold), res, PNG / "hold" / f"{n}-{res}.png", backdrop), label)
                     for n, label in items]
            what = "full intro at 2.3 s (help line in, native dim starting)" if kind == "full-intro" else "upgrade card (hold)"
            sheet(cells, 3, 620, f"{what} — {res}, constant pixel size, CJK fallback on", PNG / f"{kind}-{res}.png")
        # If DisplayKit panels scale with the screen like the native UI (unknown until the beta).
        cells = [(render(n, frame_of(scene(n), hold), "1280x720", PNG / "hold" / f"{n}-1280x720-scaled.png", backdrop,
                         scale="1920x1080"), label) for n, label in items]
        sheet(cells, 3, 620, f"{kind} — 1280x720 scaled like the native UI (1920x1080 reference)", PNG / f"{kind}-1280x720-scaled.png")

    # 2. Timelines: every recorded frame of a full intro, plus one card.
    for n in ["full-mtf-nu7", "full-serpent", "full-scp-001a", "card-scp-tier"]:
        sc = scene(n)
        frames = [(render(n, i, "1280x720", PNG / "anim" / f"{n}-f{i}.png", backdrop, scale="1920x1080"),
                   f"{sc['frames'][i]['time']:.2f}s · {sc['frames'][i]['label']}") for i in range(len(sc["frames"]))]
        sheet(frames, 3, 460, f"{n} — timeline (1280x720, scaled like the native UI)", PNG / f"timeline-{n}.png")

    # 3. Native intro and ours at the same beats (both 1280x720; ours scaled like the native canvas).
    sc = scene("full-mtf-nu7")
    beats = [i for i, f in enumerate(sc["frames"]) if f["label"] != "destroyed"]
    times = [sc["frames"][i]["time"] for i in beats]
    if a.native:
        nat = native_frames(a.native, times, PNG / "native")
        cells = []
        for i, t, p in zip(beats, times, nat):
            cells.append((p, f"native 14.2.9 zh · +{t:.2f}s"))
            cells.append((render("full-mtf-nu7", i, "1280x720", PNG / "native" / f"ours-{i}.png", backdrop, scale="1920x1080"),
                          f"ours · {t:.2f}s · {sc['frames'][i]['label']}"))
        sheet(cells, 4, 460, "Native intro (NTF Captain, 14.2.9 zh_Hans client) beside ours (Nu-7), same beats", PNG / "native-vs-ours.png")

    # 4. Worst cases: no CJK in DisplayKit.
    worst = PNG / "worst"
    nu7 = [("Lead", "你是"), ("Title", "落锤队长"), ("Faction", "机动特遣队 Nu-7「落锤」"), ("Objective", "指挥推进，夺回站点。"),
           ("Help", "在控制台输入 .info 查看角色详情")]
    tier = [("Title", "三阶 · 回声猎手"), ("Objective", "新技能已解锁。在控制台输入 .info 查看操作。")]
    for res in RESOLUTIONS:
        render("full-nu7-tofu", frame_of(scene("full-nu7-tofu"), "hold full"), res, worst / f"full-draw-on-no-cjk-{res}.png", backdrop, "tofu")
        raw = render("full-nu7-omit", frame_of(scene("full-nu7-omit"), "hold full"), res, worst / f"full-omit-{res}-intro-only.png", backdrop, "tofu")
        mock_full_hint(raw, nu7, res, worst / f"full-omit-{res}.png")
        raw = render("card-tier-omit", frame_of(scene("card-tier-omit"), "hold "), res, worst / f"card-omit-{res}-card-only.png", backdrop, "tofu", layout=True)
        mock_card_hint(raw, tier, res, worst / f"card-omit-{res}.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
