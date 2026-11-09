# Framegen DLL Tester 0.3.8

Choose a game EXE, click **Start game**, then answer **Yes / F1** or **No / F2**. Included DLLs and launch profiles are selected automatically. **Reset tests** starts the sequence over; **Remove test DLL** restores all tracked original files and disables persistent setup.

**Fix: Hookshot ports (`hookshot.exe`).** These load their renderer at runtime, so 0.3.7 could not tell which graphics API they use and offered only DirectX DLLs, none of which can attach to a Vulkan game. Automatic now reads the port's recent `hookshot_log.txt` and selects the process-local Vulkan route. Run the game normally once first so that log exists, then choose `bin\hookshot.exe` in the tester. If the tester was already pointed at the game, click **Reset tests**.

The owner reported that the Vulkan layer worked in a Hookshot port using the same layer and launch settings as 0.3.7, started manually. The 0.3.8 **Start game** path for that game still needs its own gameplay confirmation. Launch logic and native binaries are unchanged from 0.3.7.

174 automated checks cover the current application logic. They do not measure actual generated frames or game latency. See [gameplay feedback](TESTED-GAMES.md).

The experimental Vulkan route still requires the exact verified NVIDIA driver binaries listed in [VULKAN.md](VULKAN.md); a different driver is rejected before launch.

**Online / anti-cheat:** Easy Anti-Cheat and other systems that inspect DLLs can block a test, cause startup errors or create account risk. Use offline, unprotected sessions. Stop and restore when an anti-cheat error appears. This app does not bypass protection.

The original app source is MIT. NVSmooth30, DLSSG for SM86, Ultimate ASI Loader, DXVK, Khronos headers and NVIDIA technologies retain their own authorship / terms. Exact origins and license notices accompany the package.
