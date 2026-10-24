# Framegen DLL Tester 0.3.7 preview

Choose a game EXE, click **Start game**, then answer **Yes / F1** or **No / F2**. Included DLLs and launch profiles are selected automatically. **Reset tests** starts the sequence over; **Remove test DLL** restores all tracked original files and disables persistent setup.

This preview includes the Outlast DX9 -> Vulkan compatibility fix, persistent answer-button / hotkey repairs, packed Godot inspection and Unreal launcher / renderer separation. The owner reported that Outlast opened and felt smoother in 0.3.6; 0.3.7 retains that same launch logic and adds publication packaging / documentation.

170 automated checks cover the current application logic. Separate disposable DX9 presentation tests verify the RTSS Vulkan workaround, successful presentation, normal exit and restoration. They do not measure actual generated frames or game latency. See [gameplay feedback](TESTED-GAMES.md).

**Online / anti-cheat:** Easy Anti-Cheat and other systems that inspect DLLs can block a test, cause startup errors or create account risk. Use offline, unprotected sessions. Stop and restore when an anti-cheat error appears. This app does not bypass protection.

The original app source is MIT. NVSmooth30, DLSSG for SM86, Ultimate ASI Loader, DXVK, Khronos headers and NVIDIA technologies retain their own authorship / terms. Exact origins and license notices accompany the package.
