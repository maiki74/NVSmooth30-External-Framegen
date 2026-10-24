# Hands-on testing: start, yes or no

1. Open the local EXE and choose an offline, unprotected game executable.
2. Click **Start game**. Check image quality, fluidity and input response in a repeatable scene. Enable the game's own DLSS Frame Generation setting for DLSSG attempts where supported.
3. If unsuccessful, click **No, try next** or press **F2**. Check that the game closes and the app says the next option is ready. The next game run must wait for your next **Start game** click.
4. If successful, click **Yes, it worked** or press **F1**. Check that the app saves the option and does not continue searching. Keep playing or choose Stop + restore.
5. Close the game and check that tracked original files are restored. Reopen the app and use **Start saved setup** to confirm that your chosen option is remembered.

**DirectX:** leave Automatic selected to read normal / delayed EXE imports and display the evidence. Manually choose 12 / 11 if needed. This selects DLL tests; it does not change the game's rendering API. Automatic DX9 offers the experimental DXVK -> Vulkan route. DX10 remains unsupported. The DX9 route opts out of RTSS's conflicting Vulkan layer only for the child process; global RTSS settings are unchanged. Changing the selection restores the current setup and restarts the sequence. Multiple imports or dynamic loading must remain uncertain rather than being presented as a definite API.

Automatic recognizes `vulkan-1.dll` and offers the dedicated experimental Vulkan v2 option. For SpartanPort, choose its renderer or normal Play launcher and click Start game; the app resolves the normal launcher and applies the embedded runtime only to that process tree. It must not copy a DLL into the game folder or register a system layer. An unsupported driver hash must be rejected before launching. Vulkan has one option: No ends the sequence, and Yes saves it. Its persistent normal-launch toggle is hidden. The v2 path can relax VSync for the launch without modifying saved settings; see [VULKAN.md](VULKAN.md).

**FPS overlay...:** apply displayed FPS and check that the numeric FPS updates once per second, while the original application frametime graph retains its earlier behavior. The main value, average and 1% low use the existing PresentMon display source. Restore original overlay should select the original RTSS layout again. No fixed FPS multiplier is used. This is a change to the RTSS overlay, not MSI Afterburner's internal history. The helper requires the existing default RTSS installation and may request administrator permission.

**Persistent setup toggle:** enable **Keep framegen setup installed** for the current option, close the app and launch the game normally through Steam, another store or its executable. The setup should remain installed and apply its profile without the app running. This is a separate live-game check, not something proven by the fixture suite. Reopen the app and toggle off, or use **Remove test DLL**; check that the game closes, all tracked helper files are removed, prior originals return and the toggle is off. Steam launch options and global environment must remain unchanged. See [persistent setup](STEAM.md).

To restart the DLL sequence, click **Reset tests**. Check that the app closes a running game, restores originals and shows option 1 with the same executable selected. A previously saved working option must be cleared so Start game tries the first option. Past trial journals must remain available. If the game refuses to close, the reset must remain pending and keep the previous position until restoration succeeds.

For a black screen or crash, answer No after closing any error dialog. If the game cannot close, finish the save prompt / close the frozen process yourself, then restore. On an anti-cheat error, stop testing and restore; do not proceed to more proxy options.

**Remove test DLL** uses the same verified close / restoration path. Check that newly staged files disappear, existing originals return, the current option is retained and unrelated DLLs remain untouched. This removes files tracked by the test, not every DLL in the game folder. Backups and history remain available.

F1 / F2 are global while an answer is expected; they are released while stopping, restoring or staging, and on app exit. If a key is already reserved, a status message asks you to use the buttons.

Confirmed D3D11 games receive the original NVSmooth30 image through three Ultimate ASI Loader proxy names. Confirmed D3D12 games receive the 14 existing prepared NVSmooth30 / DLSSG attempts. Unknown APIs retain the full experimental sequence. All profiles explicitly disable mod FPS caps and forced VSync. There is no need to create or open a batch file.

Successful startup and module observations do not prove frame generation. User verdicts are kept in local trial journals. Include the game version, GPU / driver, scene, base FPS, displayed FPS, visual artifacts and stability when reporting your test. Review personal paths before sharing logs.

Automated verification uses inert DLL fixtures and disposable GUI processes. Actual third-party binaries are statically inspected and hash checked, not executed by that suite.

Separate Dark Souls Remastered runtime checks loaded the ASI and initialized NVSmooth30, then recorded a D3D11 shadow swapchain error `0x80070005`. The owner also reported a successful launch with MSI Afterburner showing **120 FPS**. Keep both observations; a counter alone does not establish whether generated frames are included, and an initialization error alone should not erase the owner's observed result. All tested game processes were closed and the original display configuration restored. See [verification](VERIFICATION.md).
