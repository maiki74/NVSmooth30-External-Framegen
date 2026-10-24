# What changed in the FPS overlay

| Item | Original | First displayed-FPS experiment | Current app / corrected local setup |
| --- | --- | --- | --- |
| Main FPS value | RTSS `<FR>` presentation-hook count | PresentMon `FramerateDisplayed` | PresentMon `FramerateDisplayed` |
| FPS source refresh | Layout default: 1000 ms | `PM_RefreshPeriod=100` (ten updates per second) | `PM_RefreshPeriod=1000` (one update per second) |
| PresentMon display delay | No override | `PM_DisplayDelay=1000` | `PM_DisplayDelay=0` |
| Frametime graph | `<G=Frametime>`: application frametimes | `<G=OutputFrametime>`: measured display-change intervals | Original `<G=Frametime>` restored |
| Average and 1% low | RTSS cumulative presentation-hook statistics | Display data | Display data: average of sampled output FPS and 99th-percentile recent output frametime |

Changing the graph source made it reflect different events, and the 100 ms source refresh made the FPS number update much faster. Those were overlay changes, not a newly imposed game FPS limit. The current graph again shows application frametimes; the numeric FPS uses the display measurement source. The two intentionally describe different stages of presentation.

This changes the RTSS overlay used with MSI Afterburner. It does not rewrite MSI Afterburner's original internal sensor history. No constant multiplier, fabricated doubling, or monitor-refresh substitution is used. The values come from the installed PresentMon provider; availability and frame classification depend on the driver. In immediate / unsynchronized presentation, software presentation rates can exceed the physical display refresh rate and do not mean that the panel shows that many complete refresh cycles.

The untouched `NV.ovl` source remains available. The earlier local experiment uses `NV.output-fps.ovl`; the combined app uses a separate `FramegenLab.Displayed.ovl` and keeps restoration metadata under `%LOCALAPPDATA%\Framegen DLL Tester\rtss-overlay`. Fonts, positions, colors, layout dimensions and original hardware sources are preserved. The repaired Windows total-CPU source is retained.

Use **FPS overlay...** in the app to apply the corrected policy or restore the original layout. RTSS exits gracefully and restarts hidden to load the selected layout. Windows may request administrator permission for its protected installation folder. No monitoring tool download, GPU tuning, or game-setting write is involved.

Sources: the installed RTSS SDK (`PresentMonInterface.cpp`, `OverlayDataProviderPresentMon.cpp`, `PMDPSharedMemory.h`) and bundled `presentmon_framegen.ovl` example; [PresentMon's official console guide](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md). Implementation: `src/RtssOverlay.cs`.
