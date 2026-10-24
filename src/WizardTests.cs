// C# 5 · WizardTests.cs · disposable yes/no integration tests · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace FramegenLab
{
    public static class WizardTests
    {
        sealed class FixtureCatalog : ICandidateCatalog
        {
            readonly string source;
            public FixtureCatalog(string path) { source = path; }
            public int Count { get { return 3; } }
            public string Label(int index) { return "Inert fixture " + index; }
            public PreparedCandidate Prepare(int index)
            {
                return new PreparedCandidate { Dll = source, Target = new[] { "version.dll", "winmm.dll", "dxgi.dll" }[index], Companions = new string[0],
                    Profile = new[] { "RE4-D3D12", "DIAGNOSTIC", "DLSSG" }[index], Label = Label(index) };
            }
        }
        static void WaitForWindow(string game)
        {
            for (int i = 0; i < 80; ++i)
            {
                bool ready = false;
                foreach (Process p in TrialStore.FindGame(game)) { try { p.Refresh(); if (p.MainWindowHandle != IntPtr.Zero) ready = true; } finally { p.Dispose(); } }
                if (ready) return;
                Thread.Sleep(100);
            }
            throw new Exception("The disposable fixture did not open its window.");
        }
        static bool NoGame(string game)
        { List<Process> games = TrialStore.FindGame(game); bool none = games.Count == 0; foreach (Process p in games) p.Dispose(); return none; }
        public static void Run(string root, string game, string inertDll, Action<bool, string> check)
        {
            FixtureCatalog fixtures = new FixtureCatalog(inertDll);
            TrialStore store = new TrialStore(Path.Combine(root, "wizard-data"));
            WizardController flow = new WizardController(store, fixtures, "--fixture"); flow.SelectGame(game);
            string target = Path.Combine(Path.GetDirectoryName(game), "version.dll"), original = Disk.Hash(target);
            flow.StartGame().GetAwaiter().GetResult(); WaitForWindow(game);
            string observed = File.ReadAllText(Path.Combine(Path.GetDirectoryName(game), "fixture-environment.txt"));
            check(observed.Contains("SM86_ENABLE_OSD=0") && observed.Contains("SM86_ENABLE_D3D11_BRIDGE=0") && observed.Contains("SM86_DIAGNOSTICS=1") && observed.Contains("SM86_FORCE_VSYNC=0") && observed.Contains("SM86_LOW_LATENCY=0") && observed.Contains("SM86_BASE_FPS_CAP=0") && observed.Contains("SM86_HALF_REFRESH_CAP=0"), "Start game passes every RE4-SmoothMotion.cmd setting to the child process");
            flow.No().GetAwaiter().GetResult();
            check(NoGame(game) && flow.State.Index == 1 && flow.State.Phase == "Ready", "No closes the game and prepares the next option without launching it");
            check(Disk.Hash(target) == original && File.Exists(Path.Combine(Path.GetDirectoryName(game), "winmm.dll")), "No restores the previous DLL before staging the next proxy");
            WizardState recorded = Disk.Read<WizardState>(Path.Combine(store.Root, "wizard.json"));
            check(recorded.Index == 1 && store.ReadAll().Count(t => t.State != "Restored") == 1, "The next option and its single active trial are persisted");
            flow.StartGame().GetAwaiter().GetResult(); WaitForWindow(game);
            observed = File.ReadAllText(Path.Combine(Path.GetDirectoryName(game), "fixture-environment.txt"));
            check(observed.Contains("SM86_ENABLE_OSD=1") && observed.Contains("SM86_DIAGNOSTICS=1") && observed.Contains("SM86_FORCE_VSYNC=0") && observed.Contains("SM86_LOW_LATENCY=0") && observed.Contains("SM86_BASE_FPS_CAP=0") && observed.Contains("SM86_HALF_REFRESH_CAP=0"), "The diagnostic attempt has no 30 FPS cap and does not force VSync");
            check(observed.Contains("SM86_ENABLE_D3D11_BRIDGE=<unset>"), "An absent profile setting does not leak from the prior attempt");
            flow.Yes(); check(flow.State.Winner == 1 && flow.State.Phase == "Success" && !NoGame(game), "Yes saves the working option without advancing or interrupting play");
            flow.Stop().GetAwaiter().GetResult();
            check(NoGame(game) && Disk.Hash(target) == original && !File.Exists(Path.Combine(Path.GetDirectoryName(game), "winmm.dll")), "Stop closes the game and restores the successful test's tracked files");
            WizardController reopened = new WizardController(store, fixtures, "--fixture");
            check(reopened.State.Winner == 1 && reopened.State.Phase == "Saved", "The saved working option survives an app restart");
            reopened.StartGame().GetAwaiter().GetResult(); WaitForWindow(game);
            check(reopened.State.Index == 1, "Start saved setup reuses the successful option");
            reopened.No().GetAwaiter().GetResult(); check(reopened.State.Index == 2 && reopened.State.Phase == "Ready", "Rejecting the saved option resumes the remaining sequence");
            reopened.StartGame().GetAwaiter().GetResult(); WaitForWindow(game);
            observed = File.ReadAllText(Path.Combine(Path.GetDirectoryName(game), "fixture-environment.txt"));
            check(observed.Contains("SM86_ENABLE_OSD=<unset>") && observed.Contains("SM86_BASE_FPS_CAP=0") && observed.Contains("SM86_FORCE_VSYNC=0"), "DLSSG attempts clear unrelated overrides and keep FPS caps and VSync disabled");
            reopened.No().GetAwaiter().GetResult();
            check(reopened.State.Phase == "Exhausted" && NoGame(game) && store.ReadAll().All(t => t.State == "Restored"), "Exhausting the queue restores everything and reports no confirmed success");

            TrialStore vetoStore = new TrialStore(Path.Combine(root, "veto-data"));
            WizardController veto = new WizardController(vetoStore, fixtures, "--fixture-veto"); veto.SelectGame(game);
            string allow = Path.Combine(Path.GetDirectoryName(game), "fixture-allow-close.txt");
            try
            {
                veto.StartGame().GetAwaiter().GetResult(); WaitForWindow(game); bool blocked = false;
                try { veto.No().GetAwaiter().GetResult(); } catch (IOException) { blocked = true; }
                check(blocked && veto.State.Index == 0 && veto.State.Phase == "Recovery" && !File.Exists(Path.Combine(Path.GetDirectoryName(game), "winmm.dll")), "A game that refuses to close blocks No from advancing or replacing its files");
            }
            finally { File.WriteAllText(allow, "allow"); veto.Stop().GetAwaiter().GetResult(); }
            check(NoGame(game) && Disk.Hash(target) == original, "The pending test recovers after the close prompt is resolved");

            TrialStore instantStore = new TrialStore(Path.Combine(root, "instant-data"));
            WizardController instant = new WizardController(instantStore, fixtures, "--fixture-exit"); instant.SelectGame(game);
            instant.StartGame().GetAwaiter().GetResult();
            for (int n = 0; n < 60 && instant.State.Phase != "AwaitingAnswer"; ++n) { instant.Observe().GetAwaiter().GetResult(); Thread.Sleep(100); }
            check(instant.State.Phase == "AwaitingAnswer" && Disk.Hash(target) == original, "An immediate startup failure restores files and waits for the user's answer");

            BundledCatalog real = new BundledCatalog(Path.Combine(root, "bundled-check"));
            check(real.Count == 17 && real.Manifest.Candidates.Select(c => c.Dll).Distinct().Count() == 14, "The full catalog includes 17 attempts and the additional real x64 ASI loader");
            for (int i = 0; i < real.Count; ++i)
            {
                PreparedCandidate candidate = real.Prepare(i);
                BundleFile manifest = real.Manifest.Files.Single(f => f.Key == real.Manifest.Candidates[i].Dll);
                if (Disk.Hash(candidate.Dll) != manifest.Sha256) throw new Exception("Bundle hash mismatch: " + candidate.Label);
            }
            check(true, "Every bundled DLL reconstructs to its recorded SHA-256 and passes static x64 PE inspection");
            PreparedCandidate sdli = real.Prepare(2);
            check(sdli.Companions.Length == 1 && Path.GetFileName(sdli.Companions[0]) == "dlssg_sm86.ini", "DLSSG candidates include their matched configuration file automatically");
            TrialStore conflictStore = new TrialStore(Path.Combine(root, "proxy-conflict"));
            string previousProxy = Path.Combine(Path.GetDirectoryName(game), "winmm.dll"); File.Copy(inertDll, previousProxy);
            Trial isolated = conflictStore.Prepare(game, inertDll, "version.dll", new string[0], false, "", false, 60, new[] { "winmm.dll" });
            conflictStore.Stage(isolated);
            check(!File.Exists(previousProxy) && Disk.Hash(target) == Disk.Hash(inertDll), "Recognized inactive proxies can be suspended so only the tested proxy remains active");
            conflictStore.Restore(isolated);
            check(File.Exists(previousProxy) && Disk.Hash(previousProxy) == Disk.Hash(inertDll) && Disk.Hash(target) == original, "Suspended proxy originals are restored byte-for-byte after the test");
            File.Delete(previousProxy);
            foreach (string profile in new[] { "RE4-D3D12", "DIAGNOSTIC", "DLSSG", "D3D11-ASI" })
            {
                Dictionary<string, string> values = LaunchProfiles.Values(profile);
                check(values["SM86_BASE_FPS_CAP"] == "0" && values["SM86_HALF_REFRESH_CAP"] == "0" && values["SM86_FORCE_VSYNC"] == "0", "No FPS cap or forced VSync in profile " + profile);
            }
            string steamApps = Path.Combine(root, "steam-library", "steamapps"), steamGameRoot = Path.Combine(steamApps, "common", "Owned Fixture");
            string bin = Path.Combine(steamGameRoot, "bin"); Directory.CreateDirectory(bin);
            string steamGame = Path.Combine(bin, "SteamFixture.exe"); File.Copy(game, steamGame);
            File.WriteAllText(Path.Combine(steamApps, "appmanifest_570940.acf"), "\"AppState\" { \"appid\" \"570940\" \"installdir\" \"Owned Fixture\" }");
            SteamGameContext context = SteamLaunch.Resolve(steamGame);
            check(context != null && context.AppId == "570940", "Steam context comes from the matching installed library manifest, including nested EXE folders");
            ProcessStartInfo launch = new ProcessStartInfo(steamGame, "--fixture") { UseShellExecute = false, WorkingDirectory = bin };
            launch.EnvironmentVariables["SteamAppId"] = "999"; SteamLaunch.Apply(launch);
            using (Process child = Process.Start(launch))
            {
                WaitForWindow(steamGame);
                string env = File.ReadAllText(Path.Combine(bin, "fixture-environment.txt"));
                check(env.Contains("SteamAppId=570940") && env.Contains("SteamGameId=570940"), "The launched child receives the correct Steam identity instead of stale inherited values");
                child.CloseMainWindow(); check(child.WaitForExit(5000), "The Steam-context fixture closes cleanly after verification");
            }
            check(!File.Exists(Path.Combine(bin, "steam_appid.txt")), "Steam launch context does not write or patch Steam API files in the game directory");
            ProcessStartInfo plain = new ProcessStartInfo(game) { UseShellExecute = false }; plain.EnvironmentVariables["SteamAppId"] = "999";
            SteamLaunch.Apply(plain); check(!plain.EnvironmentVariables.ContainsKey("SteamAppId"), "A non-Steam game does not inherit an unrelated Steam identity");
            WizardController reset = new WizardController(new TrialStore(Path.Combine(root, "reset-data")), fixtures, "--fixture");
            reset.SelectGame(game); reset.State.Index = 2;
            reset.StartGame().GetAwaiter().GetResult(); WaitForWindow(game); reset.Yes();
            int retainedTrials = reset.Store.ReadAll().Count;
            reset.Reset().GetAwaiter().GetResult();
            check(NoGame(game) && reset.State.Index == 0 && reset.State.Winner == -1 && reset.State.Phase == "Idle", "Reset from a running third option closes the game and returns to option one");
            check(reset.State.Game == game && reset.Store.ReadAll().Count == retainedTrials && reset.Store.ReadAll().All(t => t.State == "Restored"), "Reset preserves the selected executable and trial history while restoring tracked files");
            WizardController afterReset = new WizardController(reset.Store, fixtures, "--fixture");
            check(afterReset.State.Index == 0 && afterReset.State.Winner == -1, "The reset position and cleared working choice survive reopening the app");
            afterReset.StartGame().GetAwaiter().GetResult(); WaitForWindow(game);
            check(afterReset.Session.Current.TargetName == "version.dll", "Start after reset runs the first DLL instead of the previously saved third option");
            afterReset.No().GetAwaiter().GetResult(); afterReset.No().GetAwaiter().GetResult();
            check(afterReset.State.Index == 2 && afterReset.State.Phase == "Ready", "The staged reset fixture reaches option three without launching the prepared option");
            afterReset.Reset().GetAwaiter().GetResult();
            check(afterReset.State.Index == 0 && NoGame(game) && Disk.Hash(target) == original && !File.Exists(Path.Combine(Path.GetDirectoryName(game), "dxgi.dll")), "Reset of a prepared third option removes it and restores originals without launching a game");
            // Reuse the rejecting-close fixture to ensure reset cannot report success before restoration.
            File.Delete(allow);
            WizardController refusedReset = new WizardController(new TrialStore(Path.Combine(root, "reset-refused-data")), fixtures, "--fixture-veto");
            refusedReset.SelectGame(game); refusedReset.State.Index = 2;
            try
            {
                refusedReset.StartGame().GetAwaiter().GetResult(); WaitForWindow(game); refusedReset.Yes();
                bool rejectedReset = false;
                try { refusedReset.Reset().GetAwaiter().GetResult(); } catch (IOException) { rejectedReset = true; }
                check(rejectedReset && refusedReset.State.Index == 2 && refusedReset.State.Winner == 2 && refusedReset.State.Phase == "Recovery", "Reset does not erase progress or the saved choice when the game refuses to close");
            }
            finally { File.WriteAllText(allow, "allow"); refusedReset.Reset().GetAwaiter().GetResult(); }
            check(NoGame(game) && refusedReset.State.Index == 0 && Disk.Hash(target) == original, "Reset completes safely once the game can close");
            byte[] dx11 = File.ReadAllBytes(inertDll);
            using (MemoryStream stream = new MemoryStream(dx11))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                stream.Position = 0x96; writer.Write((ushort)0x22); // native x64 EXE instead of DLL
                stream.Position = 0x104; writer.Write((uint)2); // two data directories
                stream.Position = 0x110; writer.Write((uint)0x1000); writer.Write((uint)40);
                stream.Position = 0x20c; writer.Write((uint)0x1040); // IMAGE_IMPORT_DESCRIPTOR.Name
                stream.Position = 0x240; writer.Write(System.Text.Encoding.ASCII.GetBytes("d3d11.dll\0"));
            }
            string dx11Path = Path.Combine(root, "D3D11StaticFixture.exe"); File.WriteAllBytes(dx11Path, dx11);
            check(PeInspector.Inspect(dx11Path).Imports.Contains("d3d11.dll"), "Static analysis detects the game import that identifies a D3D11 path");
            real.ConfigureGame(dx11Path);
            check(real.Count == 3 && Enumerable.Range(0, real.Count).All(i => real.Prepare(i).Profile == "D3D11-ASI"), "A D3D11 game only receives ASI-loaded NVSmooth30 attempts, not incompatible DLSSG/D3D12 ones");
            PreparedCandidate d3d11 = real.Prepare(0);
            check(d3d11.Companions.Any(f => Path.GetFileName(f) == "FramegenLab.NVSmooth30.asi") && Disk.Hash(d3d11.Companions.First(f => f.EndsWith(".asi"))) == "18ed37a0f333242bb27f649c3ced3dc0cc081023a12e2bdca982b3d31aaa0fbf", "The D3D11 attempt includes the exact pre-RE4-patch image with its Present hooks intact");
            check(LaunchProfiles.Values("D3D11-ASI")["SM86_ENABLE_D3D11_BRIDGE"] == "1", "The D3D11 ASI attempt explicitly enables its bridge");
            TrialStore migratedStore = new TrialStore(Path.Combine(root, "catalog-migration"));
            Disk.Write(Path.Combine(migratedStore.Root, "wizard.json"), new WizardState { Game = dx11Path, Index = 13, Phase = "Exhausted", CatalogKey = "old-unfiltered-list" });
            WizardController migrated = new WizardController(migratedStore, real);
            check(migrated.State.Index == 0 && migrated.State.Phase == "Idle" && migrated.State.Game == dx11Path, "An old exhausted incompatible sequence safely restarts at the new game's first supported attempt");
            Trial asiTrial = migratedStore.Prepare(dx11Path, d3d11.Dll, d3d11.Target, d3d11.Companions, false, "", false, 60);
            migratedStore.Stage(asiTrial); migratedStore.Restore(asiTrial);
            check(asiTrial.State == "Restored" && !File.Exists(Path.Combine(root, "FramegenLab.NVSmooth30.asi")), "The loader, ASI plugin and its INI all participate in verified restoration");
            HotkeyTests.Run(check);
            TrialStore releasedStore = new TrialStore(Path.Combine(root, "release-lock"));
            Trial delayed = releasedStore.Prepare(game, inertDll, "version.dll", new string[0], false, "", false, 60);
            releasedStore.Stage(delayed);
            FileStream held = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            System.Threading.Tasks.Task release = System.Threading.Tasks.Task.Run(delegate { Thread.Sleep(500); held.Dispose(); });
            releasedStore.Restore(delayed); release.GetAwaiter().GetResult();
            check(delayed.State == "Restored" && Disk.Hash(target) == original, "Restoration waits for a short-lived post-exit file lock and then verifies the original bytes");
            check(TestSession.DescribeBackend("[nvs30] NVP_Init_D3D=TRUE.\n[nvs30-bridge] CreateSwapChainForHwnd(shadow) failed: 0x80070005.").Contains("Frame generation unavailable"), "The observed D3D11 failure is reported as unavailable, not successful frame generation");
            check(TestSession.DescribeBackend("[nvs30] NVP_Init_D3D=TRUE.").Contains("not yet confirmed"), "Backend initialization alone is not labeled frame generation success");
        }
    }
}
