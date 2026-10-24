# Experimental Vulkan v2 integration

The combined app embeds `FramegenVulkan.dll` and the existing prepared `NVSmoothBackend.dll`. It extracts them into its own versioned cache, writes a local explicit-layer manifest, and sets `VK_LAYER_PATH` and `VK_INSTANCE_LAYERS` only for the process tree started by **Start game**. The app never loads those DLLs into its own process. No system layer registration, global environment variable, driver profile, or game-directory DLL is written by this route.

For the verified SpartanPort installation, choosing `spartanport-probe.exe` or one of the known Play / main launcher filenames resolves to the renderer for detection. Actual launch uses the normal `Play SpartanPort.exe`, which supplies the boot image and game configuration. The selected renderer's process is monitored for exit, so the short-lived Play launcher does not prematurely end the session. The app allows up to 90 seconds after wrapper exit for its renderer to appear. Stop requests a graceful close and never force-terminates it.

Vulkan has one dedicated option. F1 / Yes saves the choice; F2 / No closes the session and ends this one-option sequence. The DirectX persistent-install toggle is hidden because this Vulkan integration needs the app's process-local launch settings. A normal game launch outside this app does not enable it.

## Presentation change

The first user test opened but reported 72 FPS rather than the usual 120 FPS. Version 2 sets `SM86_VK_RELAX_VSYNC=1`. The native layer queries supported presentation modes and can replace FIFO / FIFO_RELAXED with IMMEDIATE for ordinary swapchains with no extension chain. Unknown chains and unsupported modes pass through unchanged. This can allow tearing, but does not edit the game's saved VSync or FPS settings. No FPS cap is introduced. See the [Khronos presentation-mode definitions](https://docs.vulkan.org/refpages/latest/refpages/source/VkPresentModeKHR.html).

The owner described v2 gameplay through the separate test launcher as smooth. A later RTSS PresentMon shared-memory read for the game measured approximately 145 displayed FPS, with subsequent samples varying. These observations do not independently establish a generated-frame multiplier or end-to-end input latency. The combined app's gameplay validation is still requested separately.

## Exact local builds

- New native layer SHA256: `6899b3862bd7c9dff129c803867ca3f5988ad5bbc4eef92a54286c6ebe2e4ca8`.
- Prepared NVSmooth backend SHA256: `0bfdead0c48c00c8732a757703d27e6788c08bc71fe817d6a7630987838aa8dc`.
- Installed `NvPresent64.dll`: `08357a3f054aa8c2eea251e63330e80b55006a09240cf573c74d927622eb4a34`.
- Installed `nvoglv64.dll`: `adda6dc8054888d5c52e3f97cf33b742952d16b3f145ced74cfb1012556bd696`.
- SpartanPort renderer: `4436bc2f6012eddccc182cb569e9c5f2a9b72b77f6bc2375d982b961b6e7209c`.
- SpartanPort Play launcher: `f955d03ff134c8b50de0a66f470b2ae00883ecad01c42543eab938235b5d7fb7`.

The app refuses a different driver build before starting the game. The driver's proprietary DLLs are loaded from the existing installation and are not embedded or redistributed.

## Source and build

Native source: `native/vulkan/framegen_vulkan_layer.c`; C# integration: `src/VulkanRuntime.cs`, `src/TestSession.cs` and `src/WizardController.cs`. The native source was compiled with portable Zig 0.17.0 and official Khronos Vulkan-Headers v1.4.351. The exact archive URLs / checksum record and downloaded header notices are retained in `private/vulkan-lab/compiler-download.json` and `headers/sources.json` / header files. The new layer is a source compilation; the pre-existing NVSmooth backend is a separately identified binary-patched image, not a decompiled and recompiled DLL.

```powershell
# PowerShell · framegen_vulkan_layer.c · Windows x64 · Zig cc with Khronos headers
zig cc .\native\vulkan\framegen_vulkan_layer.c -I .\private\vulkan-lab\headers -shared -O2 -std=c11 -Wall -Wextra -o .\private\vulkan-lab\runtime\FramegenVulkan.dll
.\build.ps1 -Test
```

`build.ps1` requires the recorded native hashes. A different compiler build may change the binary hash; native validation and the recorded resource guards must then be reviewed together. The app itself performs no downloads.

The user requested compiler cleanup. Automatic execution review rejected both attempted recursive removals with `blocked by policy`, including an explicit verified literal path. The compiler/archive/cache directory remains in `private/vulkan-lab/toolchain`; no alternative deletion mechanism was used to evade that rejection.

Sources: [ItsAdeline/NVSmooth30](https://github.com/ItsAdeline/NVSmooth30), [sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86), [Khronos Vulkan-Headers](https://github.com/KhronosGroup/Vulkan-Headers/tree/v1.4.351), [SpartanPort](https://github.com/FelipePrado0/SpartanPort), installed NVIDIA driver components, and the original C layer / C# integration in this workspace.
