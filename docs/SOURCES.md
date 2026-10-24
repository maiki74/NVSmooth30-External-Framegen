# Sources and exact local provenance

## Tester code

- **maiki74:** concept, requested workflow and hands-on game testing.
- **OpenAI Codex:** original C# tester, sequencing, backup / restore, per-process launch configuration, resource packaging and disposable-fixture verification under the owner's direction.
- Original tester source is MIT. There are no NuGet dependencies or downloads performed by the app.


The 0.3.1 launcher fix reads the local game's embedded PE resources as data; no game code or executable is loaded for detection. The inspected Unreal bootstrap's RCDATA IDs 201/202 contain its target path and project argument. The rule is implemented in `src/PeInspector.cs` and `src/GameTarget.cs`, without copying engine source. Recent runtime API evidence comes from this installation's existing Unreal `CrashContext.runtime-xml`, validated against its exact renderer path and timestamp. These local observations are described in [VERIFICATION.md](VERIFICATION.md). Process identity now uses Microsoft's documented [QueryFullProcessImageNameW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew) with limited query access rather than relying on a module object during process startup.

## Included local binaries

The 0.3.2 Godot detection is original, bounded metadata-reader code. Format references: [Microsoft PE section headers](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format#section-table-section-headers), [Godot 4.1 PCK reader](https://github.com/godotengine/godot/blob/4.1/core/io/file_access_pack.cpp), and [Godot binary project settings](https://github.com/godotengine/godot/blob/4.1/core/config/project_settings.cpp). Godot engine source and game assets are not copied into this app. Only metadata is read; no archive is extracted and encrypted packs are not decrypted. Local runtime hints read the first 4 KiB of the matching project's existing Godot log. These are labelled historical project evidence, not exact-process or generated-frame proof.

Version 0.3.0 additionally embeds the new, locally compiled Vulkan v2 layer and a verified duplicate of the prepared NVSmooth backend. Exact native / driver / launcher hashes, C source and compiler/header provenance are in [VULKAN.md](VULKAN.md). Installed NVIDIA driver DLLs, RTSS and PresentMon are not redistributed. The app's displayed-FPS helper uses the existing RTSS provider; its graph remains application-side and its FPS refresh is one second.

See [BUNDLED-FILES.json](BUNDLED-FILES.json) for each reconstructed file's exact length, SHA-256 and original package / proxy identity. Binary blocks are deduplicated and compressed in the EXE; extraction must reproduce the same SHA-256. The packer also reverses one exact, hash-guarded NVSmooth30 patch to recover its original D3D11 image. No candidate is decompiled or recompiled.

1. **ItsAdeline / NVSmooth30:** [upstream source](https://github.com/ItsAdeline/NVSmooth30), [author's Nexus page](https://www.nexusmods.com/site/mods/2344). Prepared input SHA-256: `0bfdead0c48c00c8732a757703d27e6788c08bc71fe817d6a7630987838aa8dc`. The earlier D3D12 repair replaced six bytes at RVA `0x80A0`: original `48 89 5C 24 08 48`, prepared `B8 01 00 00 00 C3`, and recalculated the PE checksum. To recover the exact original for D3D11, this packer restores those bytes and the original zero PE CheckSum, then requires full SHA-256 `18ed37a0f333242bb27f649c3ced3dc0cc081023a12e2bdca982b3d31aaa0fbf`. The resulting `FramegenLab.NVSmooth30.asi` retains the Present hooks required by the D3D11 bridge. This is binary-patch reversal, not decompilation or recompilation; neither image guarantees general game compatibility.
2. **sdli1995 / DLSSG for SM86:** [repository](https://github.com/sdli1995/dlssg_for_sm86). Uses the files already extracted on the owner's PC from the **0.3.5** package: 310.9 and 310.1, six real prebuilt proxies each. This is an explicit local snapshot, not the current upstream 0.4.0 package. No old DLL is paired with the newer package's different layout.
3. **DLSSG INI:** the local package's existing settings, with comments simplified to English. Runtime values are unchanged and are copied alongside each DLSSG proxy.
4. **Launch profiles:** based on the earlier RE4 / diagnostic launchers, with all FPS caps and forced VSync explicitly disabled as requested in 0.2.1. Applied using `ProcessStartInfo.EnvironmentVariables` before Start; no BAT is required.
5. **ThirteenAG / Ultimate ASI Loader:** [official repository](https://github.com/ThirteenAG/Ultimate-ASI-Loader), [v10.0.0 release](https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases/tag/v10.0.0). The owner authorized downloading the official `Ultimate-ASI-Loader-NoPDB_x64.zip`. Its SHA-256 was verified against the release asset digest: `3385950d6af79257d18420b1d6904bc47480170b3dabe345f8c1339d0a49183f`. Extracted `dinput8.dll`: 2,431,208 bytes, SHA-256 `a1249114146c1d004335b34349df90d0714b19bbd61e671232931d930cb9168c`. The same unmodified loader image is tried under the author's supported `dinput8.dll`, `xinput1_3.dll` and `winmm.dll` names. MIT license: [exact notice](../licenses/Ultimate-ASI-Loader.txt), also embedded in the executable. This is a normal ASI loading route, not an anti-cheat bypass.

| Variable | D3D12 profile | Diagnostic profile | D3D11 ASI profile |
| --- | --- | --- | --- |
| SM86_ENABLE_OSD | 0 | 1 | 1 |
| SM86_ENABLE_D3D11_BRIDGE | 0 | unset / upstream default | 1 |
| SM86_DIAGNOSTICS | 1 | 1 | 1 |
| SM86_FORCE_VSYNC | 0 | 0 | 0 |
| SM86_LOW_LATENCY | 0 | 0 | 0 |
| SM86_BASE_FPS_CAP | 0 | 0 | 0 |
| SM86_HALF_REFRESH_CAP | 0 | 0 | 0 |

ASI loader configuration restricts loading to the named test plugin:

```ini
; INI · per-proxy loader config · Windows x64 · Ultimate ASI Loader v10.0.0
[GlobalSets]
LoadPlugins=1
LoadFromScriptsOnly=1
LoadRecursively=0
LoadExtraPlugins=FramegenLab.NVSmooth30.asi
DontLoadFromDllMain=1
DebugLog=1
```

Normal test launches set these overrides on the child process. With Keep framegen setup installed explicitly enabled, the tester's original small ASI applies the same values inside the game process before loading the selected NVSmooth30 image. Known NVSmooth30 overrides are cleared for normal DLSSG attempts so one attempt's settings do not leak into another. No global variables, registry environment or Windows / Steam settings are changed.

The original [SteamBootstrap.cs](../src/SteamBootstrap.cs) emits a minimal Windows x64 PE DLL with an InitializeASI export, standard kernel32 imports, RIP-relative code and unwind / relocation metadata. DllMain only returns TRUE; InitializeASI uses SetEnvironmentVariableW and LoadLibraryW. This is new tester code, not a decompile / recompile or new patch of NVSmooth30. It follows the [Ultimate ASI Loader initialization contract](https://github.com/ThirteenAG/Ultimate-ASI-Loader/blob/master/readme.md). The original backend copy retains its full recorded hash. See [persistent setup](STEAM.md).

Steam App IDs are resolved from the installed library's matching `appmanifest_<id>.acf`, including games with nested executable folders. Only that installed ID is passed to the child process. See Valve's [SteamAPI_Init documentation](https://partner.steamgames.com/doc/api/steam_api#SteamAPI_Init) for normal Steam initialization requirements, including a running client, correct app identity, user context and ownership. No Steam API binaries or ownership checks are altered.

## Primary API references

- [Microsoft PE / COFF format](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format): static architecture / section / export inspection.
- [ProcessStartInfo.EnvironmentVariables](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.environmentvariables?view=netframework-4.8): launch settings inherited by a new process.
- [Process.CloseMainWindow](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.closemainwindow?view=netframework-4.8): graceful close requests; a game may reject the request or show a save prompt.
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey): F1 / F2 global answer keys, released during file operations and on exit.
- [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=netframework-4.8): temporary-file replacement for durable journals and staged files.
- [DataContractJsonSerializer](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.json.datacontractjsonserializer?view=netframework-4.8): local sequence state and recovery journals.

These are documentation references, not copied implementations or endorsements. NVIDIA's algorithms, drivers, models and runtime remain NVIDIA's work. The tester does not measure generated frames or replace a frame generation engine.

The original application source, native adapter and published downloads are provided in the owner-selected repository. Exact binary origins and supplied terms remain separate from the tester's MIT license; see [third-party notices](../THIRD_PARTY_NOTICES.md).

## DX9 translator added in 0.3.5

DXVK 3.1.1 official release: https://github.com/doitsujin/dxvk/releases/tag/v3.1.1

Asset: dxvk-3.1.1.tar.gz, 18,041,512 bytes. Verified archive SHA256: 40565b4a724aadc4433fa4e010b4b23916d9b1f1baeee64e17186db94f54e608. Embedded file: x64/d3d9.dll, SHA256 c126316f7478eaff1ad8087702eda5930f2400187b9c8b3592219c34f6632780. This is the unmodified official PE image, neither decompiled nor recompiled. License: zlib/libpng, licenses/DXVK-Zlib.txt, embedded verbatim in the app. Source tag: https://github.com/doitsujin/dxvk/tree/v3.1.1

Native Windows caveats: https://github.com/doitsujin/dxvk/wiki/Windows . Integration into this app is experimental. Read-only Outlast log analysis found D3D9 in the matched game log; the earlier DirectX 11 attempt could not intercept its presentation. The tester now stages a tracked translator and launches with the existing process-local Vulkan layer. Successful synthetic presentation is separate from user confirmation in the game.
