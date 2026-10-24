# Python 3 · Prepare-Publication.py · documentation-only publication edits · Windows x64
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def update(name, pairs):
    path = ROOT / name
    text = path.read_text(encoding="utf-8-sig")
    for old, new in pairs:
        text = text.replace(old, new)
    path.write_text(text, encoding="utf-8-sig")


update("src/WizardForm.cs", [
    ("FRAMEGEN DLL TESTER 0.3.6 - LOCAL PRIVATE PREVIEW", "FRAMEGEN DLL TESTER 0.3.7 - PREVIEW"),
    ("Third-party rights remain with their authors. No public upload was performed.",
     "Third-party rights remain with their authors. Project: https://github.com/maiki74/NVSmooth30-External-Framegen"),
    ("private/vulkan-lab/framegen_vulkan_layer.c", "native/vulkan/framegen_vulkan_layer.c"),
])
update("src/SelfTests.cs", [("0.3.6", "0.3.7")])
update("README.md", [
    ("**0.3.6 local private preview. Not published to GitHub.**", "**0.3.7 preview.**"),
    ("Open `downloads/FramegenDllTester.exe`.",
     "[**Download EXE**](https://github.com/maiki74/NVSmooth30-External-Framegen/raw/refs/heads/main/downloads/FramegenDllTester.exe) · [Source ZIP](https://github.com/maiki74/NVSmooth30-External-Framegen/archive/refs/heads/main.zip)\n\nOpen `downloads/FramegenDllTester.exe`."),
    ("in-game overlay availability and frame generation still need user confirmation.",
     "the owner subsequently tested Outlast in 0.3.6 and reported that it opened and felt smoother. The game exited normally and the tracked DLL was restored. Generated-frame counts, latency and overlay availability were not independently verified. Version 0.3.7 retains that tested launch logic."),
    ("## Build the private package", "## Build with your local backend package"),
    ("**Public distribution remains on hold.** This local executable includes third-party binaries from the owner's installation. Its local availability is not permission to publish those binaries. The tester's MIT license applies to its original code, not NVIDIA, mod or other third-party material. See [third-party notices](THIRD_PARTY_NOTICES.md).",
     "The tester's MIT license covers its original source. Third-party binaries retain their own terms and exact provenance; see [third-party notices](THIRD_PARTY_NOTICES.md). Build inputs and per-user logs / backups are kept outside version control."),
    ("See [verification](docs/VERIFICATION.md)", "See [tested games](docs/TESTED-GAMES.md), [verification](docs/VERIFICATION.md)"),
])
update("START-HERE.txt", [
    ("FRAMEGEN DLL TESTER 0.3.6 - LOCAL PRIVATE PREVIEW", "FRAMEGEN DLL TESTER 0.3.7 - PREVIEW"),
    ("Open downloads\\FramegenDllTester.exe.", "Open FramegenDllTester.exe from the portable ZIP.\nIn a repository checkout, open downloads\\FramegenDllTester.exe."),
    ("Third-party binaries in this local private build have not been published.", "Third-party components retain their own terms; see THIRD_PARTY_NOTICES.md."),
])
update("docs/TESTING.md", [
    ("DX9 / DX10 are absent from the manual selector; Automatic can still detect them and should stage nothing for an unsupported game.",
     "Automatic DX9 offers the experimental DXVK -> Vulkan route. DX10 remains unsupported. The DX9 route opts out of RTSS's conflicting Vulkan layer only for the child process; global RTSS settings are unchanged."),
])
for name in ["docs/VULKAN.md", "docs/SOURCES.md"]:
    update(name, [("private/vulkan-lab/framegen_vulkan_layer.c", "native/vulkan/framegen_vulkan_layer.c")])
update("docs/VULKAN.md", [(".\\private\\vulkan-lab\\framegen_vulkan_layer.c", ".\\native\\vulkan\\framegen_vulkan_layer.c")])
update("THIRD_PARTY_NOTICES.md", [
    ("# Third-party notices — local private preview", "# Third-party notices"),
    ("It has not been uploaded or released publicly. ", ""),
    ("no public redistribution is performed here.", "the original MIT notice is retained in licenses/NVSmooth30-MIT.txt."),
    ("Public packaging requires a separate distribution review and appropriate source / license materials for any redistributable components. This private test package does not claim that review is complete.",
     "Exact supplied notices are retained in licenses/Bundled-Backend-Notices.txt and inside the EXE. The original app's MIT license does not relicense third-party binaries or derived kernel material. The bundled older DLSSG snapshot retains its supplied GPLv3-related notice; newer upstream licensing is not applied retroactively."),
])
print("Prepared publication labels, documentation and source links; launch logic unchanged.")
