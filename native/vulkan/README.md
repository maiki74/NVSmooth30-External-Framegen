# Vulkan adapter source

`framegen_vulkan_layer.c` is the original C source for the tester's experimental Windows x64 Vulkan adapter. It is separate from the existing NVSmooth30 backend and NVIDIA driver components. It was compiled from source; the backend's earlier six-byte change was a binary patch, not decompilation / recompilation.

The existing build uses Zig 0.17.0 and Khronos Vulkan-Headers v1.4.351. The declarations are obtained from the [official tag](https://github.com/KhronosGroup/Vulkan-Headers/tree/v1.4.351), Apache-2.0 OR MIT. Driver DLLs and the portable compiler are not distributed in this source directory.

```powershell
# PowerShell · project root · Windows x64 · Zig cc and Vulkan-Headers
zig cc .\native\vulkan\framegen_vulkan_layer.c -I .\private\vulkan-lab\headers -shared -O2 -std=c11 -Wall -Wextra -o .\private\vulkan-lab\runtime\FramegenVulkan.dll
```

The expected native SHA256 is `6899b3862bd7c9dff129c803867ca3f5988ad5bbc4eef92a54286c6ebe2e4ca8`. Compiler / build differences can change the output hash; review and validate a new native build before changing the resource guards in `build.ps1` and `src/VulkanRuntime.cs`.

The adapter depends on a particular installed NVIDIA build and private driver interfaces. This is an experimental integration, not a general-purpose Vulkan layer. See [exact builds and limitations](../../docs/VULKAN.md).
