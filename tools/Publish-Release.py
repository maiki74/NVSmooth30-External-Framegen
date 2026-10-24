# Python 3 · Publish-Release.py · verified preview release assets · GitHub Actions
import argparse
import hashlib
import json
import os
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIRS = ("src", "native", "tests", "tools", "docs", "licenses", ".github")
ROOT_FILES = ("README.md", "START-HERE.txt", "CHANGELOG.md", "THIRD_PARTY_NOTICES.md", "LICENSE", "build.ps1", ".gitignore", ".gitattributes")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_zip(path, files):
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(files):
            entry = zipfile.ZipInfo(name, date_time=(2026, 10, 24, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data)


def packages(version):
    output = ROOT / ".release"
    output.mkdir(exist_ok=True)
    exe = ROOT / "downloads" / "FramegenDllTester.exe"
    checks = (ROOT / "downloads" / "SHA256SUMS.txt").read_text(encoding="utf-8-sig")
    expected = next(line.split()[0] for line in checks.splitlines() if line.split()[-1] == exe.name)
    if sha(exe) != expected:
        raise RuntimeError("Executable SHA256 does not match the verified publication input")
    documentation = [(name, (ROOT / name).read_bytes()) for name in ROOT_FILES if not name.startswith(".") and name != "build.ps1"]
    for folder in ("docs", "licenses"):
        documentation.extend((file.relative_to(ROOT).as_posix(), file.read_bytes()) for file in (ROOT / folder).rglob("*") if file.is_file())
    portable = output / ("FramegenDllTester-Portable-v" + version + ".zip")
    write_zip(portable, [(exe.name, exe.read_bytes())] + documentation)
    with zipfile.ZipFile(portable) as archive:
        if hashlib.sha256(archive.read(exe.name)).hexdigest() != expected:
            raise RuntimeError("Portable archive executable verification failed")
        if "licenses/NVSmooth30-MIT.txt" not in archive.namelist() or "licenses/GPL-3.0.txt" not in archive.namelist():
            raise RuntimeError("Required license notice is missing")
    source_files = [(name, (ROOT / name).read_bytes()) for name in ROOT_FILES]
    for folder in SOURCE_DIRS:
        source_files.extend((file.relative_to(ROOT).as_posix(), file.read_bytes()) for file in (ROOT / folder).rglob("*")
                            if file.is_file() and "__pycache__" not in file.parts)
    source_files.append(("downloads/SHA256SUMS.txt", (ROOT / "downloads" / "SHA256SUMS.txt").read_bytes()))
    source = output / ("FramegenDllTester-Source-v" + version + ".zip")
    write_zip(source, source_files)
    sums = output / "SHA256SUMS.txt"
    assets = [exe, portable, source]
    sums.write_text("".join(sha(asset) + "  " + asset.name + "\n" for asset in assets), encoding="ascii")
    return assets + [sums]


def publish(version, assets):
    repository = os.environ["GITHUB_REPOSITORY"]
    commit = os.environ["GITHUB_SHA"]
    token = os.environ["GITHUB_TOKEN"]
    api = "https://api.github.com/repos/" + repository

    def request(url, method="GET", data=None, content_type="application/json"):
        headers = {"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "FramegenDllTester-release"}
        if data is not None:
            headers["Content-Type"] = content_type
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=120) as response:
            return json.load(response)

    tag = "v" + version
    try:
        release = request(api + "/releases/tags/" + tag)
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        body = (ROOT / "docs" / ("RELEASE-" + version + ".md")).read_text(encoding="utf-8-sig")
        body += "\n\nDownloads: EXE, portable ZIP, source-code ZIP and SHA256SUMS.txt are attached below. The source archive contains the app C#, native C adapter, tests, build scripts and notices. To rebuild from the source-only archive, also download the EXE into downloads/ so Extract-BuildInputs.ps1 can recover the verified embedded inputs.\n"
        release = request(api + "/releases", "POST", json.dumps({"tag_name": tag, "target_commitish": commit, "name": "Framegen DLL Tester " + version + " preview", "body": body, "draft": False, "prerelease": True}).encode())
    if release.get("target_commitish") not in (commit, "main"):
        raise RuntimeError("This release already refers to another commit; refusing to overwrite it")
    existing = {asset["name"]: asset for asset in release["assets"]}
    upload = release["upload_url"].split("{")[0]
    for path in assets:
        digest = "sha256:" + sha(path)
        if path.name in existing:
            if existing[path.name].get("digest") != digest:
                raise RuntimeError("Existing release asset differs: " + path.name)
            continue
        result = request(upload + "?name=" + urllib.parse.quote(path.name), "POST", path.read_bytes(), "application/octet-stream")
        if result["size"] != path.stat().st_size or result.get("digest", digest) != digest:
            raise RuntimeError("Uploaded release asset verification failed: " + path.name)
        print("Published:", result["browser_download_url"])
    print("Release:", release["html_url"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", default="0.3.7")
    parser.add_argument("--prepare-only", action="store_true")
    arguments = parser.parse_args()
    assets = packages(arguments.version)
    for asset in assets:
        print("Verified:", asset.name, sha(asset))
    if not arguments.prepare_only:
        publish(arguments.version, assets)
