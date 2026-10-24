# Third-party notices

This **local executable includes third-party binaries from the owner's existing installation and the authorized official ASI loader download**. The tester's original code is MIT; that does not relicense any included binary or establish permission for redistribution.

## NVSmooth30

ItsAdeline is the author of [NVSmooth30](https://github.com/ItsAdeline/NVSmooth30). The local prepared DLL's SHA-256 is `0bfdead0c48c00c8732a757703d27e6788c08bc71fe817d6a7630987838aa8dc`. It is a previously prepared, version-specific binary used in earlier testing, not an original algorithm authored by this tester. Nexus download permissions and upstream source licensing are distinct; the original MIT notice is retained in licenses/NVSmooth30-MIT.txt.

The original D3D11 image is recovered by reversing that exact known patch and restoring the original zero PE checksum. Its required SHA-256 is `18ed37a0f333242bb27f649c3ced3dc0cc081023a12e2bdca982b3d31aaa0fbf`. Packaging or recovery does not transfer the author's rights. See [exact provenance](docs/SOURCES.md).

## New Vulkan integration and telemetry

The new `FramegenVulkan.dll` is compiled from this workspace's original C source. It uses declarations from [Khronos Vulkan-Headers v1.4.351](https://github.com/KhronosGroup/Vulkan-Headers/tree/v1.4.351), Apache-2.0 OR MIT; their notices are retained in the local header files. The existing prepared NVSmooth backend retains its separate provenance above. Installed NVIDIA `NvPresent64.dll` and `nvoglv64.dll` are proprietary driver components and are not included in the app. See [exact hashes and source locations](docs/VULKAN.md).

RTSS and its PresentMon provider are existing user-installed software by Unwinder / RTSS and the Intel PresentMon project. Their binaries are not repackaged by this app. The overlay helper follows the installed SDK and bundled framegen example; the new C# helper itself is original app code. [PresentMon source](https://github.com/GameTechDev/PresentMon).

## Ultimate ASI Loader

[ThirteenAG / Ultimate ASI Loader](https://github.com/ThirteenAG/Ultimate-ASI-Loader), v10.0.0, is included as its unmodified official x64 loader image. Copyright (c) 2023 ThirteenAG. MIT license; the full required notice is in [licenses/Ultimate-ASI-Loader.txt](licenses/Ultimate-ASI-Loader.txt) and embedded in the EXE's third-party notices. The official release archive was downloaded with the owner's permission and its SHA-256 checked against the release asset digest.

## DLSSG for SM86

[sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86) is the source of the local 0.3.5 proxy package and its 310.9 / 310.1 runtime variants. Proxy source follows its upstream GPLv3 licensing; its bundled NVIDIA runtime / models / kernel material is third-party material and is not relicensed by that source license or this tester's MIT license.

The local package's full `THIRD_PARTY_NOTICES.txt` is embedded in the EXE and displayed in **Details, logs and sources**, including attribution to Coldwood1026, Microsoft helper libraries, Detours and nlohmann JSON as recorded by the supplied package. Those components are in the third-party binary package, not newly copied source dependencies of the C# tester.

Windows / .NET Framework / Windows Forms are Microsoft technologies. NVIDIA owns its drivers, models, runtime and frame generation technologies. None of those authors endorses this tester.

Exact supplied notices are retained in licenses/Bundled-Backend-Notices.txt and inside the EXE. The original app's MIT license does not relicense third-party binaries or derived kernel material. The bundled older DLSSG snapshot retains its supplied GPLv3-related notice; newer upstream licensing is not applied retroactively.

## DXVK 3.1.1

Official prebuilt x64 d3d9.dll, unmodified. Source and release: https://github.com/doitsujin/dxvk/releases/tag/v3.1.1

Copyright Philip Rebohle, Joshua Ashton, Robin Kertels, Jeffrey Ellison and contributors. zlib/libpng license, reproduced verbatim in licenses/DXVK-Zlib.txt and embedded in the app. The translator supplies no frame-generation algorithm; it enables the experimental Vulkan route for DX9.
