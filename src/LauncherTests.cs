// C# 5 · LauncherTests.cs · disposable bootstrap/renderer fixtures · Windows x64 · .NET Framework 4.8
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FramegenLab
{
    public static class LauncherTests
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr BeginUpdateResource(string path, bool deleteExisting);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint length);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool EndUpdateResource(IntPtr update, bool discard);
        static void Resource(IntPtr update, int id, string text)
        {
            byte[] data = Encoding.Unicode.GetBytes(text + "\0");
            if (!UpdateResource(update, new IntPtr(10), new IntPtr(id), 1033, data, (uint)data.Length)) throw new IOException("Fixture resource update failed: " + Marshal.GetLastWin32Error());
        }
        static void Bootstrap(string path, string relative)
        {
            IntPtr update = BeginUpdateResource(path, false);
            if (update == IntPtr.Zero) throw new IOException("Fixture resource handle failed.");
            bool committed = false;
            try { Resource(update, 201, relative); Resource(update, 202, "FixtureProject"); committed = EndUpdateResource(update, false); if (!committed) throw new IOException("Fixture resource commit failed."); }
            finally { if (!committed) EndUpdateResource(update, true); }
        }
        sealed class InertCatalog : ICandidateCatalog
        {
            readonly string dll;
            public InertCatalog(string value) { dll = value; }
            public int Count { get { return 2; } }
            public string Label(int index) { return "Inert launcher fixture"; }
            public PreparedCandidate Prepare(int index) { return new PreparedCandidate { Dll = dll, Target = index == 0 ? "version.dll" : "winmm.dll", Companions = new string[0], Profile = "RE4-D3D12" }; }
        }
        static void Window(string game)
        {
            for (int n = 0; n < 80; n++)
            {
                bool ready = false;
                foreach (Process p in TrialStore.FindGame(game)) { try { p.Refresh(); ready |= p.MainWindowHandle != IntPtr.Zero; } finally { p.Dispose(); } }
                if (ready) return; Thread.Sleep(100);
            }
            throw new IOException("Fixture renderer did not open.");
        }
        static string Xml(string renderer, string api)
        {
            return "<FGenericCrashContext><RuntimeProperties><BaseDir>" + System.Security.SecurityElement.Escape(Path.GetDirectoryName(renderer)) + "</BaseDir><ExecutableName>" + Path.GetFileNameWithoutExtension(renderer) + "</ExecutableName></RuntimeProperties><EngineData><RHI.RHIName>" + api + "</RHI.RHIName></EngineData></FGenericCrashContext>";
        }
        public static void Run(string root, string fixture, string dll, Action<bool, string> check)
        {
            string folder = Path.Combine(root, "unreal-launcher-fixture"), rendererDir = Path.Combine(folder, @"FixtureProject\Binaries\Win64");
            Directory.CreateDirectory(rendererDir);
            string launcher = Path.Combine(folder, "WrapperFixture.exe"), renderer = Path.Combine(rendererDir, "Fixture-Win64-Shipping.exe");
            File.Copy(fixture, launcher); File.Copy(fixture, renderer);
            Bootstrap(launcher, @"FixtureProject\Binaries\Win64\Fixture-Win64-Shipping.exe");
            GameTarget plan = GameTarget.Resolve(launcher);
            check(plan.Launcher == launcher && plan.Renderer == renderer, "Embedded Unreal bootstrap resources resolve the exact renderer without game-name rules or directory guessing");
            check(GameTarget.Resolve(renderer).Renderer == renderer, "Selecting the actual renderer keeps it as the direct executable");
            TrialStore store = new TrialStore(Path.Combine(root, "launcher-flow-data"));
            WizardController flow = new WizardController(store, new InertCatalog(dll), "--launcher-fixture"); flow.SelectGame(launcher);
            check(flow.State.Game == renderer && flow.State.LaunchExecutable == launcher, "Wizard stores launch and renderer paths separately");
            Trial first = null;
            try
            {
                flow.StartGame().GetAwaiter().GetResult(); Window(renderer);
                for (int n = 0; n < 20 && flow.Session.Current.Pid == 0; n++) { flow.Observe().GetAwaiter().GetResult(); Thread.Sleep(100); }
                first = flow.Session.Current;
                check(first != null && first.Pid != 0 && first.LaunchExecutable == launcher && File.Exists(Path.Combine(rendererDir, "version.dll")) && !File.Exists(Path.Combine(folder, "version.dll")), "Start stages beside the renderer and observes its child process after the bootstrap exits");
                check(File.ReadAllText(Path.Combine(rendererDir, "fixture-environment.txt")).Contains("SM86_BASE_FPS_CAP=0"), "The bootstrap's renderer child inherits the uncapped launch configuration");
                check(TrialDiagnostics.Status(first).Contains("absent from the module sample"), "The app explains an unobserved test DLL rather than claiming frame generation worked");
                flow.No().GetAwaiter().GetResult();
                check(flow.State.Phase == "Ready" && flow.State.Index == 1 && !File.Exists(Path.Combine(rendererDir, "version.dll")) && File.Exists(Path.Combine(rendererDir, "winmm.dll")), "No closes the renderer and restores its first files before staging the next loader in the same correct directory");
                check(File.ReadAllText(Path.Combine(first.Folder, "diagnosis.txt")).Contains(launcher) && first.ExitCode == "0x00000000", "Trial diagnosis records the original launcher and actual renderer exit separately");
            }
            finally { flow.Stop().GetAwaiter().GetResult(); }
            check(TrialStore.FindGame(renderer).Count == 0 && !File.Exists(Path.Combine(rendererDir, "winmm.dll")), "Bootstrap test cleanup leaves no fixture process or staged DLL");

            // Simulate the old version's persistent install in the bootstrap folder.
            Trial legacy = store.Prepare(launcher, dll, "version.dll", new string[0], false, "", false, Int32.MaxValue);
            legacy.PersistentSteam = true; store.Save(legacy); store.Stage(legacy);
            Disk.Write(Path.Combine(store.Root, "wizard.json"), new WizardState { Game = launcher, SteamTrialId = legacy.Id, Phase = "SteamEnabled" });
            WizardController upgraded = new WizardController(store, new InertCatalog(dll));
            check(upgraded.State.Phase == "Recovery" && File.Exists(Path.Combine(folder, "version.dll")), "Legacy bootstrap-folder installations require restoration before the renderer target is migrated");
            using (Process child = Process.Start(new ProcessStartInfo(renderer, "--fixture") { WorkingDirectory = rendererDir, UseShellExecute = false }))
            {
                Window(renderer); bool guarded = false;
                try { upgraded.Stop().GetAwaiter().GetResult(); } catch (IOException) { guarded = true; }
                try { check(guarded && File.Exists(Path.Combine(folder, "version.dll")), "Migration refuses to restore old bootstrap files while the separate renderer is open"); }
                finally { child.Refresh(); child.CloseMainWindow(); if (!child.WaitForExit(10000)) throw new IOException("Fixture renderer refused cleanup."); }
            }
            upgraded.Stop().GetAwaiter().GetResult();
            check(upgraded.State.Game == renderer && upgraded.State.LaunchExecutable == launcher && upgraded.State.Winner == -1 && !File.Exists(Path.Combine(folder, "version.dll")), "Legacy migration restores old files, resolves the renderer and resets the unverified result");
            WizardController reopened = new WizardController(store, new InertCatalog(dll));
            check(reopened.State.Game == renderer && reopened.State.LaunchExecutable == launcher, "Resolved launcher and renderer survive reopening");

            string wrong = Path.Combine(folder, "InvalidFixture.exe"); File.Copy(fixture, wrong);
            Bootstrap(wrong, @"..\Outside\Binaries\Win64\Other.exe");
            bool rejected = false; try { GameTarget.Resolve(wrong); } catch (IOException) { rejected = true; }
            check(rejected, "Embedded bootstrap paths cannot escape the selected game directory");
            Bootstrap(wrong, @"Missing\Binaries\Win64\Missing.exe");
            rejected = false; try { GameTarget.Resolve(wrong); } catch (IOException) { rejected = true; }
            check(rejected, "A missing embedded renderer is reported instead of silently staging into the bootstrap folder");

            string crashes = Path.Combine(root, "recorded-api"), record = Path.Combine(crashes, "UE4CC-fixture", "CrashContext.runtime-xml"); Directory.CreateDirectory(Path.GetDirectoryName(record));
            File.WriteAllText(record, Xml(renderer, "D3D11"));
            check(RecordedGraphics.Read(renderer, crashes).Api == "dx11", "Recent crash context can identify the last-used API when both DirectX 11 and 12 are imported");
            File.WriteAllText(record, Xml(launcher, "D3D12"));
            check(RecordedGraphics.Read(renderer, crashes) == null, "Recorded API evidence for another executable path is ignored");
            File.WriteAllText(record, Xml(renderer, "D3D12")); File.SetLastWriteTimeUtc(record, DateTime.UtcNow.AddDays(-8));
            check(RecordedGraphics.Read(renderer, crashes) == null, "Old recorded API evidence is ignored");
            File.WriteAllText(record, "<!DOCTYPE FGenericCrashContext [<!ENTITY ext SYSTEM 'file:///not-read'>]><FGenericCrashContext>&ext;</FGenericCrashContext>");
            check(RecordedGraphics.Read(renderer, crashes) == null, "Recorded XML rejects DTDs and external entity expansion");
            string legacyRoot = Path.Combine(root, "legacy-api"), legacyLog = Path.Combine(legacyRoot, "OLGame", "Logs", "Launch.log");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyLog));
            string legacyText = "Init: Base directory: " + Path.GetDirectoryName(renderer) + "\\\r\nInit: Executable: " + Path.GetFileNameWithoutExtension(renderer) + "\r\n[0000.88] Log: Using D3D9 device (0): fixture GPU\r\n";
            File.WriteAllText(legacyLog, legacyText);
            check(RecordedGraphics.ReadLegacy(renderer, legacyRoot).Api == "dx9", "Recent UE3 log overrides conflicting import hints using the exact executable and installation directory");
            File.WriteAllText(legacyLog, legacyText.Replace(Path.GetDirectoryName(renderer), root));
            check(RecordedGraphics.ReadLegacy(renderer, legacyRoot) == null, "A UE3 renderer record from a different installation is ignored");
            File.WriteAllText(legacyLog, legacyText); File.SetLastWriteTimeUtc(legacyLog, DateTime.UtcNow.AddDays(-8));
            check(RecordedGraphics.ReadLegacy(renderer, legacyRoot) == null, "Stale UE3 runtime evidence cannot select a current rendering route");
            string backupLog = Path.Combine(Path.GetDirectoryName(legacyLog), "Launch-backup-fixture.log");
            File.WriteAllText(backupLog, legacyText);
            File.WriteAllText(legacyLog, "Init: Aborted before graphics initialization\r\n");
            check(RecordedGraphics.ReadLegacy(renderer, legacyRoot).Api == "dx9", "A startup failure without API data retains matched recent backup-log evidence instead of falling back to DX11");
            File.WriteAllText(backupLog, legacyText.Replace(Path.GetDirectoryName(renderer), root));
            check(RecordedGraphics.ReadLegacy(renderer, legacyRoot) == null, "Fallback backup logs must still match the exact selected installation");

            // Diagnose fresh bytes for a normal proxy too, not just ASI-based trials.
            string backendLog = Path.Combine(rendererDir, "nvsmooth30.log");
            File.WriteAllText(backendLog, "CreateSwapChainForHwnd(shadow) failed: 0x80070005\r\n");
            TestSession plain = new TestSession(store);
            try
            {
                plain.Start(renderer, dll, "version.dll", new string[0], false, "--fixture", true, 60);
                Window(renderer); plain.Poll();
                check(plain.Current.BackendObservation == null, "An old backend failure does not become this trial's diagnosis");
                File.AppendAllText(backendLog, "NVP_Init_D3D=TRUE\r\n"); plain.Poll();
                check(plain.Current.BackendObservation != null && plain.Current.BackendObservation.Contains("initialized") && !plain.Current.BackendObservation.Contains("failed"), "Direct proxy diagnostics read new log bytes and ignore an earlier run's failure");
            }
            finally { plain.Finish("Fresh diagnostic fixture complete").GetAwaiter().GetResult(); }

            TestSession changed = new TestSession(store);
            try
            {
                changed.Start(renderer, dll, "version.dll", new string[0], false, "--launcher-fixture", false, 60);
                changed.SetLauncher(launcher); File.AppendAllText(launcher, "fixture launcher changed");
                bool blocked = false; try { changed.LaunchCurrent(LaunchProfiles.Values("RE4-D3D12")); } catch (IOException) { blocked = true; }
                check(blocked && changed.Current.Pid == 0, "A launcher changed after preparation is rejected before starting a process");
            }
            finally { changed.Finish("Changed launcher fixture cleanup").GetAwaiter().GetResult(); }
            PeInfo mixed = new PeInfo(); mixed.Imports.AddRange(new[] { "d3d9.dll", "d3d11.dll" });
            check(GraphicsApi.Detect(mixed).Api == "dx11" && GraphicsApi.Detect(mixed).Evidence.Contains("hint"), "A sole modern API alongside legacy imports is labelled as a heuristic");
            mixed.DirectImports.Add("d3d9.dll"); mixed.DelayedImports.Add("d3d11.dll");
            check(GraphicsApi.Detect(mixed).Api == "dx9" && GraphicsApi.Detect(mixed).Evidence.Contains("delay-loaded"), "Direct DX9 imports are not displaced by a delay-loaded DX11 library when game logs are unavailable");
            check(TrialDiagnostics.Status(new Trial { Pid = 0, State = "Restored" }).Contains("never observed"), "A missing renderer has a distinct explanation from an unobserved DLL");
        }
    }
}
