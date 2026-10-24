# Python 3 · Publication-Files.py · explicit source / documentation allowlist · Windows x64
import base64
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
files = [root / name for name in ("README.md", "START-HERE.txt", "CHANGELOG.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "build.ps1", ".gitignore", ".gitattributes", "downloads/SHA256SUMS.txt")]
for name in ("src", "native", "tests", "tools", "docs", "licenses", ".github"):
    files.extend(path for path in (root / name).rglob("*") if path.is_file() and "__pycache__" not in path.parts)
result = []
for path in sorted(set(files)):
    relative = path.relative_to(root).as_posix()
    if relative.startswith(("private/", "dist/", ".release/")):
        raise RuntimeError("Private publication path")
    data = path.read_bytes()
    if path.suffix.lower() == ".png":
        result.append({"path": relative, "encoding": "base64", "content": base64.b64encode(data).decode("ascii")})
    else:
        result.append({"path": relative, "encoding": "utf-8", "content": data.decode("utf-8")})
print(json.dumps(result, ensure_ascii=True))
