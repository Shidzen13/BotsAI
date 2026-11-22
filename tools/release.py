"""Пакет BotsAI: версія з Plugin.cs -> manifest.json, профіль r2modman Dev (mods.yml, папка плагіна), zip для Thunderstore.

Запуск після dotnet build:  python tools/release.py
"""
import glob
import json
import os
import re
import shutil
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "dist", "pkg")
DLL = os.path.join(ROOT, "src", "RoundsBots", "bin", "Release", "net472", "RoundsBots.dll")
PROFILE = os.path.join(os.environ["APPDATA"], "r2modmanPlus-local", "ROUNDS", "profiles", "Dev")
PACKAGE = "Shidzenreku13-BotsAI"
FILES = ("manifest.json", "README.md", "icon.png", "RoundsBots.dll")


def read_version():
    source = open(os.path.join(ROOT, "src", "RoundsBots", "Plugin.cs"), encoding="utf-8").read()
    return re.search(r'ModVersion = "(\d+)\.(\d+)\.(\d+)"', source).groups()


def update_manifest(version):
    path = os.path.join(PKG, "manifest.json")
    manifest = json.load(open(path, encoding="utf-8"))
    manifest["version_number"] = version
    json.dump(manifest, open(path, "w", encoding="utf-8"), indent=2)


def update_profile(major, minor, patch):
    plugin_dir = os.path.join(PROFILE, "BepInEx", "plugins", PACKAGE)
    for name in FILES:
        shutil.copy(os.path.join(PKG, name), plugin_dir)

    path = os.path.join(PROFILE, "mods.yml")
    text = open(path, encoding="utf-8", newline="").read()
    start = text.index("  name: " + PACKAGE + "\n")
    end = text.find("\n- ", start)
    end = len(text) if end < 0 else end
    block = text[start:end]
    for key, value in (("major", major), ("minor", minor), ("patch", patch)):
        block = re.sub(rf"(\n    {key}: )\d+", rf"\g<1>{value}", block, count=1)
    open(path, "w", encoding="utf-8", newline="").write(text[:start] + block + text[end:])


def build_zip(version):
    for old in glob.glob(os.path.join(ROOT, "dist", "BotsAI-*.zip")):
        os.remove(old)
    path = os.path.join(ROOT, "dist", f"BotsAI-{version}.zip")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
        for name in FILES:
            archive.write(os.path.join(PKG, name), name)
    return path


def main():
    major, minor, patch = read_version()
    version = f"{major}.{minor}.{patch}"
    shutil.copy(DLL, PKG)
    update_manifest(version)
    update_profile(major, minor, patch)
    print("version", version, "->", build_zip(version))


if __name__ == "__main__":
    main()
