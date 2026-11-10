# Framegen DLL Tester 0.3.9

Choose a game EXE, click **Start game**, then answer **Yes / F1** or **No / F2**. Included DLLs and launch profiles are selected automatically. **Reset tests** starts the sequence over; **Remove test DLL** restores all tracked original files and disables persistent setup.

**Fix: Hookshot ports keep all options.** 0.3.8 showed only the Vulkan option for `hookshot.exe`. Automatic now lists Vulkan first and keeps every DirectX option after it. Other games are unchanged.

**New: persistent setup for native Vulkan.** **Keep framegen setup installed** now also works for Vulkan games that import winmm, version, dinput8 or xinput1_3 (Hookshot uses winmm), so the game can be opened normally without this app. It installs Ultimate ASI Loader, a configuration ASI and the layer files beside the game EXE, with tracked backup / restore. The layer variables are set inside the game process only; no global Vulkan layer or environment is registered. Verified with a normal `hookshot.exe` launch: the layer loaded and created the native Vulkan swapchains. DX9 -> Vulkan still must be started from this app.

**Fix: long Hookshot logs.** Detection now reads the start of `hookshot_log.txt` instead of ignoring logs over 2 MB.

If the tester was already pointed at the game, click **Reset tests**.

177 automated checks cover the current application logic. They do not measure actual generated frames or game latency. See [gameplay feedback](TESTED-GAMES.md).

The experimental Vulkan route still requires the exact verified NVIDIA driver binaries listed in [VULKAN.md](VULKAN.md); a different driver is rejected before launch or install.

**Online / anti-cheat:** Easy Anti-Cheat and other systems that inspect DLLs can block a test, cause startup errors or create account risk. Use offline, unprotected sessions. Stop and restore when an anti-cheat error appears. This app does not bypass protection.

The original app source is MIT. NVSmooth30, DLSSG for SM86, Ultimate ASI Loader, DXVK, Khronos headers and NVIDIA technologies retain their own authorship / terms. Exact origins and license notices accompany the package.
