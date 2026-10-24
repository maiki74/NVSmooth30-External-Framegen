# Gameplay feedback

| Game | App / route | Reported result | Evidence limits |
| --- | --- | --- | --- |
| Outlast (64-bit) | 0.3.6, Automatic DX9 -> DXVK -> Vulkan | Owner: game opened and felt smoother | DXVK presentation recorded; clean exit and tracked DLL restoration. No independently measured generated FPS, input latency or overlay validation. |
| Buckshot Roulette | 0.3.2, Automatic Vulkan | Owner: game opened and felt smoother | Positive visual feedback; no independent generated-frame count. |
| Dark Souls Remastered | Earlier D3D11 / ASI attempts | Owner reported a launch with MSI Afterburner showing 120 FPS | Logs also recorded a shadow-swapchain error. Counter meaning and generated frames remain unresolved. |
| Stray | Unreal launcher / renderer resolution | Selection and staging repaired | No confirmed successful gameplay result in this app. |

Version 0.3.7 retains the tested 0.3.6 launch logic. Its C# changes are publication labels, version and source links. A successful result on one installation does not establish compatibility with all games or drivers.

## Outlast compatibility fix

Outlast's matching UE3 log identified DirectX 9. Its executable also delay-imports DirectX 11; treating that library as the active renderer selected an ineffective DX11 attempt. Automatic now retains direct/delayed import provenance and valid recent matching backup logs.

The DX9 route stages official DXVK `d3d9.dll`, enables the embedded Vulkan adapter only for the launched process tree and opts out of RTSS's conflicting Vulkan layer for that child. RTSS stays running and its saved configuration is unchanged. Existing DLLs are backed up before staging and restored after exit or removal. No mod FPS cap is added.

The final owner-confirmed run exited normally and restored its staged translator. The working choice is saved locally for **Start saved setup**. Launch this route from the tester; persistent Steam / normal-launch installation is not offered for it.

The app does not bypass anti-cheat. Use unprotected offline sessions; stop and restore on an anti-cheat error.
