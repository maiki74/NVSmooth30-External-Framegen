# Changelog

## 0.3.7 preview — 2026-10-24

- Publish app source, the original native Vulkan adapter, English documentation, credits and a portable package in the owner-selected repository.
- Record the owner's Outlast result: opened and felt smoother, with normal exit and verified restoration. Preserve the saved working choice.
- Retain 0.3.6 launch logic. C# changes are version, publication labels and source links.

## 0.3.6 local private preview — 2026-10-24

- Reproduced Outlast's Vulkan device-chain failure with an OLGame-named disposable DX9 fixture; 180 frames succeeded with only the conflicting RTSS Vulkan layer disabled for that child. Apply this workaround to DX9 launches, preserving global RTSS settings and other Vulkan profiles.
- Retain recent exact-path-matched UE3 backup logs when a new startup log has no renderer record. Keep normal and delayed PE imports separate so delayed DX11 does not displace directly imported DX9.
- Report fatal DXVK device initialization errors explicitly, including failures at process exit.
- Gameplay, generated frames and overlay availability still require user testing.


## 0.3.5 local private preview — 2026-10-24

- Fixed Yes / No and F1 / F2 being disabled for persistent setups; No restores the previous setup before installing the next one.
- Retry unavailable hotkeys and accept F1 / F2 in the focused app.
- Read bounded, recent UE3 logs with exact executable / installation matching instead of choosing DX11 from Outlast auxiliary imports.
- Added official, hash-verified DXVK 3.1.1 x64 for a reversible DX9 -> Vulkan framegen experiment. Sources and license are included; no global configuration changes.
- Separate DX9 presentation fixture completed 180 frames and Vulkan initialization. Outlast gameplay and generated frames still require user confirmation.


## 0.3.4 local private preview — 2026-10-24

- Removed the EXE file / Running process selector at the owner's request. Selection is through Choose game... only.
- Removed the process list, captured command-line settings, pre-existing process restart workflow and System.Management dependency. Old process-selection settings are ignored.
- Retained the graphics API selection, existing backends, normal Start game flow, backup/restore, F1/F2, reset and file removal.
- 150 existing automated checks passed. Both interface modes were rendered; compiled process-picker types and obsolete live settings are absent.

## 0.3.3 local private preview — 2026-10-24

- Added EXE file / Running process selection and a searchable, refreshable visible-window picker with PID and executable path.
- Added Restart with framegen: verify the chosen process identity, request normal exit, wait, then stage and launch the selected setup. A close refusal prevents staging/relaunch. No force termination or injection.
- Preserve accessible process arguments for the same executable. Keep known separate-launcher behavior and use its working directory. Persist the mode and arguments without persisting an instruction to close a PID after restart.
- Added disposable existing-process restart, stale identity, close-refusal, argument parsing and restoration checks. Game binaries, backend DLLs, overlay and FPS profile values are unchanged.
- 164 automated checks passed. The EXE-file interface was rendered and checked; running-process game selection awaits the owner's live UI check.

## 0.3.2 local private preview — 2026-10-24

- Fixed the false `Invalid PE resource entry count` failure on Buckshot Roulette's packed Godot EXE. RVA mapping now respects a section's virtual extent instead of letting its much larger on-disk PCK swallow later sections.
- Added bounded, read-only Godot 4 / PCK v2 project metadata inspection and recent engine-matched project-log API hints. No engine code is run, encrypted data is not decrypted, and game content is not extracted or modified.
- Route the inspected local Buckshot installation to the experimental Vulkan option. Unknown Godot rendering no longer receives unrelated DirectX proxies. Recorded/configured OpenGL gets an explicit unsupported result.
- Added packed-section, import/resource mapping, metadata, malformed-input, log recency/version, path and renderer-hint regression fixtures. Preserve the original Unreal resolver and backup/restore checks.
- The Vulkan backend binaries, driver guards, RTSS layout/refresh and frame-cap settings are unchanged. Buckshot gameplay/frame generation remains pending owner validation; nothing was uploaded.
- 150 automated checks passed. The real Buckshot EXE inspects successfully and retains its original SHA-256; its Vulkan gameplay trial is left for the owner.

## 0.3.1 local private preview — 2026-10-23

- Fixed staging DLLs beside Unreal's bootstrap instead of its renderer, as found in the local Stray installation. Read the launcher's embedded target with bounded static PE resource parsing; preserve its normal launch path and monitor the renderer child.
- Restore old bootstrap-folder trials before migrating the selected path. Do not restore while the separate renderer is running. Keep original backups and reset unverified results.
- Use recent, exact-path-matched Unreal renderer records as labelled last-run API evidence. This selects the DX11 sequence for the inspected Stray installation instead of the unknown/DX12 sequence. Proxy names already imported by the renderer receive priority.
- Added live failure explanations, separate launcher/renderer exit codes, graphics-module samples and per-trial `diagnosis.txt` reports. Direct proxies now receive fresh backend-log diagnostics too; earlier-run errors are excluded.
- Keep the Vulkan v2 payload, overlay timing, existing graph, FPS-cap defaults and no-forced-VSync DirectX profiles unchanged.
- 135 automated checks passed. Fixed the process-startup module-path race exposed by the bootstrap fixture using limited-access native executable-path queries. Verified recovery of the old real Stray setup without starting the game.
- Real Stray frame generation still requires the owner's gameplay test. No compatibility guarantee or public upload.

## 0.3.0 local private preview — 2026-10-23

- Embedded the locally tested Vulkan v2 layer and prepared backend; exact driver/runtime hashes are verified before launch.
- Automatic and manual Vulkan selection use a dedicated option, without staging DirectX proxies or writing game files.
- SpartanPort detection accepts its known Play/main launchers and resolves the actual renderer. Start game uses its normal saved-configuration launcher.
- Added no-file trial journaling, normal renderer-process observation, wrapper startup grace, and graceful close/recovery for Vulkan sessions.
- Fixed an early-close race when a fixture process existed before its main window was ready.
- Added reversible RTSS displayed-FPS setup with one-second FPS updates, original application frametime graph, preserved layout visuals / CPU sources, and original-overlay restoration.
- Kept the existing DirectX DLL sequence, F1/F2 answers, reset, file removal and persistent DirectX setup.
- 111 automated checks passed on the local machine, plus rendered UI checks. Combined-app gameplay still requires the owner's test; public upload remains on hold.

## 0.2.8 local private preview — 2026-10-23

- Automatic now recognizes Vulkan imports and excludes incompatible DirectX-only tests.
- Fixed SpartanPort probe being treated as an unknown API and offered irrelevant D3D12 DLLs.
- Display the unsupported Vulkan backend explicitly; no manual Vulkan option or frame-generation compatibility claim was added.

## 0.2.7 local private preview — 2026-10-23

- Removed the unnecessary Steam-library requirement from persistent installation.
- Renamed the toggle to Keep framegen setup installed for Steam and other compatible game executables.
- Open game uses Steam for recognized Steam installations and directly starts other selected executables from their game folder.
- Removed the unrelated anti-cheat / restore boilerplate from generic error dialogs; pending file recovery gets a specific recovery hint.
- Added disposable checks for non-Steam persistent installation, direct launch, normal exit retention and full removal.

## 0.2.6 local private preview — 2026-10-23

- Removed unsupported DirectX 9 and DirectX 10 manual choices. The selector now offers Automatic, DirectX 12 and DirectX 11 only.
- Older saved DX9 / DX10 overrides migrate to Automatic while preserving trial backups and history.
- Automatic still detects unsupported graphics APIs so incompatible files are not staged.

## 0.2.5 local private preview — 2026-10-23

- Added Automatic / DirectX 12 / 11 / 10 / 9 selection, persisted with the chosen game.
- Automatic reads normal and delayed EXE imports, displays evidence and preserves ambiguity when needed.
- DX9 / DX10 explicitly report no compatible bundled backend instead of staging incompatible files.
- Added opt-in Enable for Steam: retain the current setup when the tester closes and launch through the normal Steam URL.
- Added an original small native ASI generator to apply NVSmooth30 process-local settings before loading its backend. No Steam launch options or global environment changes.
- Toggle off, Remove test DLL, Stop + restore and Reset tests remove every tracked persistent helper and restore original files.
- Detect externally changed installed Steam files on reopening and require recovery.
- Preserve the owner's Dark Souls 120 FPS observation alongside the logged bridge error; live Steam frame generation remains unconfirmed.

## 0.2.4 local private preview — 2026-10-23

- Added Remove test DLL: close / restore the current test without resetting the sequence or deleting unrelated game DLLs.
- Existing originals are restored instead of deleted. Trial backups and history are retained.

## 0.2.3 local private preview — 2026-10-23

- Added global F1 / F2 answer shortcuts with conflict detection and automatic release during operations.
- Filter attempts by detected graphics API instead of trying D3D12-only DLSSG options on confirmed D3D11 games.
- Added official Ultimate ASI Loader v10.0.0, exact MIT notice and hash-verified original NVSmooth30 image for the D3D11 path.
- Added bounded restore retries for temporary file locks after the game exits.
- Report observed D3D11 bridge initialization failures without claiming successful frame generation.
- Live Dark Souls Remastered tests loaded the ASI, but frame generation still failed at the shadow swapchain with 0x80070005. Window-mode experiments were reverted.

## 0.2.2 local private preview — 2026-10-23

- Added Reset tests to close / restore the current trial and return to option 1.
- Reset keeps the selected game and past trial history and clears the saved working choice.
- A failed restoration preserves the previous sequence position until recovery succeeds.

## 0.2.1 local private preview — 2026-10-23

- Removed the old JO.bat 30 FPS cap and forced VSync from every attempt.
- Automatically resolve installed Steam games' App IDs and pass them to the child process.
- Start the installed Steam client if needed, without changing global launch settings or Steam API files.
- Added checks for uncapped profiles, manifest matching and the child process's Steam identity.

## 0.2.0 local private preview — 2026-10-23

- Replaced the advanced setup UI with Choose game, Start game and Yes / No.
- Embedded 13 existing local DLL binaries and 14 automatically sequenced attempts.
- Start applies the exact prior RE4 / JO.bat launch profiles to the child process.
- No gracefully closes the game, restores that attempt and stages the next one without auto-launching it.
- Yes saves the working choice and stops the search; Stop / app close restores originals.
- Included full binary provenance, local package notices and focused workflow verification.
- Public upload remains on hold pending hands-on testing and distribution review.

## 0.1.0 local preview — 2026-10-23

- New English desktop app for frame generation DLL compatibility trials.
- Static PE inspection, verified backups, explicit staging and recovery journals.
- Bounded baseline / candidate runs with graceful game close.
- User-recorded outcomes, notes, FPS measurements and CSV export.
- Online / anti-cheat notice, provenance and recovery documentation.
- Fixture verification completed locally; real-game testing and publication pending.
