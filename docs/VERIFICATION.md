# Local verification

Build: **0.3.7 preview**. Date: 2026-10-24. The executable hash is recorded in `downloads/SHA256SUMS.txt`.

## Current validation

The 170-check suite covers persistent Yes / No controls, F1 / F2 registration and dispatch, safe close / restore / next behavior, PE imports, UE3 backup-log evidence, DX9 translation staging, the per-child RTSS Vulkan workaround and prior recovery / launcher / Godot / overlay checks. It uses disposable windows and inert fixtures, without launching real games or deliberately executing third-party frame-generation DLLs.

Separate real-DX9 presentation fixtures reproduced the Outlast-named RTSS Vulkan device-chain failure. The child-only workaround permitted 180 presents, normal exit and tracked DLL restoration. These runs execute the verified DXVK and Vulkan runtime in the independently owned test process and are separate from the inert suite.

The owner tested Outlast with 0.3.6 and reported that it opened and felt smoother. Its trial recorded DXVK presentation, exit code 0 and restoration of the staged d3d9.dll. This is visual gameplay feedback, with no independently measured generated-frame count or input-latency result. The working choice was saved through the controller's Yes action. Version 0.3.7 retains that tested launch logic; its C# changes are version, publication labels and source links.

## Earlier validation history

Version 0.3.4 removes the running-process selection and restart workflow. The standard EXE selection and prior game/backend checks remain. Obsolete selection-mode and captured-argument fields from 0.3.3 are ignored when reading settings; existing trial journals and original backups remain valid.

Owner gameplay feedback after 0.3.2: Buckshot Roulette opened and felt smoother. This is positive visual feedback from the owner, not an independent generated-frame count or latency measurement.

The new regression fixture reproduces a packed section with VirtualSize=8 and a much larger raw extent preceding normal resource/import sections. Old RVA mapping reads fake resource counts from pack bytes; corrected mapping reaches the actual resource table and OpenGL import. Metadata tests cover an embedded Godot 4.1.1 / PCK v2 `project.binary`, API-log hints, old/future/wrong-version logs, escaping names, explicit rendering methods and malformed native resources. Fixtures are static/inert and are never executed.

After the 150-check run, the Vulkan instructions received a text-only cleanup so unrelated games no longer see a SpartanPort-specific instruction. Both interface modes were rendered again from the final rebuilt EXE. The final hash is in `downloads/SHA256SUMS.txt`; this last copy change did not change trial or detection logic.

Read-only inspection of the installed Buckshot Roulette executable (796325472 bytes, SHA256 `df2f8a31bd469438a1a29f831945ccea7b26963ab735f73b32936792207b1663`) now succeeds. The PCK section maps only eight virtual bytes while spanning 738498656 file bytes. Its project metadata identifies Godot 4.1.1 and the Buckshot Roulette user-data name. The existing project log reports Vulkan; Automatic offers the embedded Vulkan v2 trial instead of DirectX options. No actual Buckshot game was launched by the update, and frame generation remains unverified.

The 0.3.1 update adds static Unreal bootstrap resource resolution, separate launcher/renderer paths, monitored child-process lifetime, legacy install migration, recent exact-path API evidence and fresh per-trial diagnostics. Disposable bootstrap fixtures execute only this app's own test window and inert candidate files. Their checks cover staging in the renderer folder, inherited environment, F2 close/restore/next, migration blocked by an open renderer, reopening, missing/escaping targets, stale/unrelated/DTD XML rejection, exclusion of earlier backend errors and a changed launcher blocked before process creation.

Local Stray inspection found root `Stray.exe` (529920 bytes) embedding `Hk_project\\Binaries\\Win64\\Stray-Win64-Shipping.exe` in RCDATA 201, with the project argument in RCDATA 202. The renderer imports DX11 and DX9 normally, and DX12 through delayed imports. Its recent matching crash context records `RHI.RHIName=D3D11`. This is evidence of a previous run, not proof that the next launch or frame generation works. Old tracked root-folder installation files are restored before selecting the renderer. No actual Stray gameplay test is included in the automated results.

Version 0.3.0 adds the exact tested Vulkan v2 binaries, a no-game-file launch journal, normal SpartanPort launcher resolution, graphics-API selection, and reversible RTSS displayed-FPS setup. Its overlay refresh is 1000 ms and the original application frametime graph is preserved. The transformed layout retains its other visual and hardware-source properties.

**150 automated checks passed** for the 0.3.4 logic, recorded in `private/verification/self-test.txt`, using disposable files, inert native DLL fixtures, the original generated configuration ASI and the app's own fixture windows. The suite does not launch a real game or deliberately load a third-party frame-generation DLL. An earlier external-runtime fixture exposed an early-close race before a window existed; graceful close now retries only until the first close request can be delivered, without repeatedly dismissing a game's confirmation dialog. The bootstrap fixture also exposed a transient Framework `ProcessModule.FileName` null reference. Exact executable matching now uses limited-access `QueryFullProcessImageNameW`; unreadable live processes still block file changes.

Deployment recovery on the inspected Stray installation restored the four tracked root-folder files through the existing trial journal: `version.dll`, `version.ini`, `FramegenLab.SteamBackend.dll` and `FramegenLab.Steam.asi` had not existed before that trial and are now absent. There are zero pending trials. The selected renderer is the shipping executable, the launch path remains root `Stray.exe`, the sequence starts at imported `xinput1_3.dll`, and persistent installation is disabled. The actual Stray game has not been launched by this update; gameplay validation is pending.

The final app's real RTSS helper was also exercised through enable / restore / re-enable. Seven smoke checks confirmed the selected layout, one-second refresh, original graph, PresentMon display source, restoration to `NV.ovl`, unchanged original file hash, and successful re-enabling. The corrected output layout remains selected. No game was started or terminated by this check. Evidence: `private/rtss-output-fps/app-overlay-verification.txt`.

The checks cover:

- Static native x64 PE / import inspection, malformed inputs, path guards and pre-stage hash verification.
- Verified backups, durable journals, partial staging, interrupted recovery, original bytes / attributes / timestamps, corrupt-backup rejection and runtime-change preservation.
- Running-game guards, graceful close, rejected-close handling, bounded launch timeout and temporary file locks after process exit.
- No: close, restore and stage the next option without launching it. Yes: save the working choice and stop searching. Stop: close / restore without changing the choice.
- Reset from running, staged and saved third options, preservation of the selected game / history, persisted reset position and no progress loss after a rejected close.
- Correct Steam identity from the matching installed manifest, actual child-process environment and no Steam API file changes.
- Full 17-attempt catalog reconstruction with SHA-256 / static PE checks and companion configuration.
- D3D11 import detection, exclusion of incompatible D3D12-only attempts, exact original NVSmooth30 ASI hash and joint restoration of loader / ASI / INI.
- No FPS cap or forced VSync in every launch profile.
- Actual Windows F1 / F2 registration, dispatch through messages to the app's own test window, disabling during transitions and release for reuse.
- D3D11 backend failure parsing: initialization alone is never labeled successful frame generation.
- Automatic normal / delayed import detection, ambiguous imports, manual DX12 / DX11 selection and persisted override.
- Removed DX9 / DX10 manual overrides are rejected without changing the selected API or game files. Old saved overrides migrate to Automatic while preserving trial history. Automatic still recognizes unsupported imports; returning to Automatic rebuilds the sequence.
- Windows loads and executes the generated native configuration ASI in a separate disposable process, applies the uncapped / no-forced-VSync profile and initializes only once.
- Persistent setup stages and persists across reopening, while disabling / removal restores the original DLL and removes every tracked helper.
- Changed installed Steam files require recovery on reopen; changing DirectX restores persistent files before changing the sequence.

**Remove test DLL** calls the same verified Stop / restoration path. It keeps the current option, saved choice and trial history; removes newly staged tracked files; and restores prior originals. It does not scan for and delete arbitrary game DLLs. The compiled interface was rendered and visually checked with the new button and F1 / F2 labels. See [interface.png](interface.png).

Persistent setup was exercised in disposable Steam-library and non-Steam fixtures. The non-Steam checks cover installation without an App ID, direct launch with the selected working directory / arguments, retaining the setup after normal game exit and removing it with original-file restoration. The ASI was executed with a Microsoft system DLL as its harmless backend, not NVSmooth30. Actual frame generation from a normal Steam launch with the tester closed has not been validated. The toggle remains off by default. No Steam settings were changed and no persistent setup was installed in a real game by this update.

## Separate Dark Souls Remastered runtime checks

Selected game: native x64 Steam installation, App ID 570940, D3D11 imports detected. The updated sequence presents three ASI loader options.

Observed in the real game:

- Ultimate ASI Loader loads through `dinput8.dll`.
- The original NVSmooth30 ASI loads, initializes NvPresent and installs Present hooks.
- Its D3D11 bridge fails at `CreateSwapChainForHwnd(shadow)` with **0x80070005**, then falls back to native presentation.
- Short tests with WindowMode values 2, 0 and 1 produced the same bridge error.

The owner subsequently reported that the last game launch worked and MSI Afterburner displayed **120 FPS**. That report is retained alongside the backend error, rather than labeling the run a definitive frame-generation failure. The inspected journals do not identify a saved Yes result or independently distinguish source FPS from generated / displayed frames. The loader / ASI configuration remains available unchanged. Further observation in gameplay is needed to reconcile the counter and log.

After testing, the game was closed, all test trials were restored and no pending trial remained. The original DarkSouls.ini was restored byte-for-byte: SHA-256 `62948be7b8bf7ffe545c622b050fc029947e0b87e1e3d61a5e4187f1c859789a`. No test loader / ASI remained in the game directory; its existing Microsoft `xinput1_3.dll` was retained. No FPS cap, VSync or permanent display change was applied.

This verifies loader entry and app recovery and records the owner's 120 FPS observation. Independent confirmation of generated frames, gameplay stability and online compatibility remains pending. Public upload remains on hold.

## SpartanPort probe inspection

The actual `D:\gow3-windows\gow3-windows\bin\spartanport-probe.exe` was first statically inspected without executing it or modifying its files. Native x64 image SHA-256: `4436bc2f6012eddccc182cb569e9c5f2a9b72b77f6bc2375d982b961b6e7209c`. Its imports contain `vulkan-1.dll`. Version 0.2.8 identified Vulkan but had no integration. Version 0.3.0 offers one dedicated, embedded Vulkan v2 option.

The initial separate Vulkan launcher opened the game at a reported 72 FPS. The owner later described the v2 launch as smooth. Bounded, independently owned Vulkan presentation fixtures also exited normally, and RTSS PresentMon later returned valid display intervals for the real game, around 145 FPS in one sample and varying later. These are separate observations: neither initialization nor an overlay value alone establishes interpolation quality or input latency. The combined app uses the same binary hashes, launch settings and normal Play launcher, and awaits user gameplay validation. See [Vulkan details](VULKAN.md).

The earlier RTSS experiment changed both the FPS source and graph source, and refreshed FPS at 100 ms. The owner requested the old graph and refresh speed. The active local layout was updated to a 1000 ms FPS refresh with `Text=<G=Frametime>` restored. The app's FPS overlay helper implements that same policy and retains a reversible original-layout selection. This modifies the RTSS overlay, not MSI Afterburner's internal history.

