#!/usr/bin/env python3
"""Fetch the DisplayKit font set once into fonts/ (git-ignored) and copy it into the Unity project.

Both renderers use these exact files, so font metrics are not a difference between them.

  Default          -> Inter Regular (Unity's editor ships Inter; DisplayKit calls its default "NotInter Regular")
  LiberationSans   -> Liberation Sans 2.1.5
  Roboto*          -> static instances of Google Fonts' Roboto variable font
  RobotoMono*      -> static instances of Google Fonts' Roboto Mono variable font

Requires: Python 3.9+, fontTools (pip install fonttools), network on first run.
"""
from __future__ import annotations

import io
import shutil
import sys
import tarfile
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
FONTS = HERE / "fonts"
UNITY_FONTS = HERE / "unity" / "Assets" / "Resources" / "Fonts"
UNITY_EDITOR_FONTS = Path(r"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Data\Resources\Fonts")

GF = "https://raw.githubusercontent.com/google/fonts/main/"
VARIABLE = {
    "Roboto": (GF + "ofl/roboto/Roboto%5Bwdth,wght%5D.ttf", GF + "ofl/roboto/Roboto-Italic%5Bwdth,wght%5D.ttf"),
    "RobotoMono": (GF + "ofl/robotomono/RobotoMono%5Bwght%5D.ttf", GF + "ofl/robotomono/RobotoMono-Italic%5Bwght%5D.ttf"),
}
WEIGHTS = {"Thin": 100, "Light": 300, "Regular": 400, "Medium": 500, "Bold": 700}
LIBERATION = "https://github.com/liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz"
INTER_FALLBACK = GF + "ofl/inter/Inter%5Bopsz,wght%5D.ttf"


def fetch(url: str) -> bytes:
    print("  fetch", url)
    req = urllib.request.Request(url, headers={"User-Agent": "displaykit-preview-fonts"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def instance(var_bytes: bytes, weight: int, out: Path) -> None:
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer

    font = TTFont(io.BytesIO(var_bytes))
    axes = {"wght": weight}
    if any(a.axisTag == "wdth" for a in font["fvar"].axes):
        axes["wdth"] = 100
    if any(a.axisTag == "opsz" for a in font["fvar"].axes):
        axes["opsz"] = 14
    static = instancer.instantiateVariableFont(font, axes, updateFontNames=True)
    static.save(str(out))


def main() -> int:
    FONTS.mkdir(exist_ok=True)

    for family, (upright_url, italic_url) in VARIABLE.items():
        if all((FONTS / f"{family}-{w}.ttf").exists() for w in WEIGHTS):
            continue
        upright, italic = fetch(upright_url), fetch(italic_url)
        for name, weight in WEIGHTS.items():
            instance(upright, weight, FONTS / f"{family}-{name}.ttf")
            italic_name = "Italic" if name == "Regular" else name + "Italic"
            instance(italic, weight, FONTS / f"{family}-{italic_name}.ttf")
            print("  made", family, name)

    if not (FONTS / "LiberationSans-Regular.ttf").exists():
        with tarfile.open(fileobj=io.BytesIO(fetch(LIBERATION)), mode="r:gz") as tar:
            for member in tar.getmembers():
                if member.name.endswith("LiberationSans-Regular.ttf") or member.name.endswith("LICENSE"):
                    data = tar.extractfile(member).read()
                    target = "LiberationSans-Regular.ttf" if member.name.endswith(".ttf") else "LiberationSans-LICENSE.txt"
                    (FONTS / target).write_bytes(data)

    if not (FONTS / "Inter-Regular.ttf").exists():
        src = UNITY_EDITOR_FONTS / "Inter-Regular.ttf"
        if src.exists():
            shutil.copy2(src, FONTS / "Inter-Regular.ttf")
        else:
            instance(fetch(INTER_FALLBACK), 400, FONTS / "Inter-Regular.ttf")

    UNITY_FONTS.mkdir(parents=True, exist_ok=True)
    copied = 0
    for ttf in sorted(FONTS.glob("*.ttf")):
        dest = UNITY_FONTS / ttf.name
        if not dest.exists() or dest.stat().st_size != ttf.stat().st_size:
            shutil.copy2(ttf, dest)
            copied += 1
    print(f"fonts: {len(list(FONTS.glob('*.ttf')))} in {FONTS}, {copied} copied to {UNITY_FONTS}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
