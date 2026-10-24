// C# 5 · SteamSetupTests.cs · disposable bootstrap and persistence checks · Windows x64 · .NET Framework 4.8
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FramegenLab
{
    public static class BootstrapFixture
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int Initialize();
        public static int Run(string path, string report)
        {
            IntPtr module = LoadLibrary(path);
            if (module == IntPtr.Zero) throw new IOException("Generated fixture ASI did not load: Win32 " + Marshal.GetLastWin32Error());
            try
            {
                IntPtr address = GetProcAddress(module, "InitializeASI");
                if (address == IntPtr.Zero) throw new IOException("The generated ASI has no InitializeASI export.");
                Initialize run = (Initialize)Marshal.GetDelegateForFunctionPointer(address, typeof(Initialize));
                int first = run(), second = run();
                File.WriteAllText(report, "First=" + first + "\r\nSecond=" + second + "\r\n" + String.Join("\r\n", LaunchProfiles.Keys.Select(key => key + "=" + (Environment.GetEnvironmentVariable(key) ?? "<unset>")).ToArray()));
                return first == 1 && second == 2 ? 0 : 3;
            }
            finally { FreeLibrary(module); }
        }
    }
    public static class SteamSetupTests
    {
        public static void Run(string root, string game, string inertDll, Action<bool, string> check)
        {
            string folder = Path.Combine(root, "steam-setup-native"); Directory.CreateDirectory(folder);
            string asi = Path.Combine(folder, "FramegenLab.Steam.asi"), report = Path.Combine(folder, "child-result.txt");
            // The authored ASI loads an existing Microsoft DLL, never a third-party backend.
            File.WriteAllBytes(asi, SteamBootstrap.Build(Path.Combine(Environment.SystemDirectory, "version.dll"), LaunchProfiles.Values("D3D11-ASI")));
            PeInfo native = PeInspector.Inspect(asi);
            check(native.IsDll && !native.Managed && native.Architecture == "x64" && native.Exports.Contains("InitializeASI"), "Steam bootstrap is a valid native x64 ASI with an explicit initialization export");
            ProcessStartInfo start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--bootstrap-fixture \"" + asi + "\" \"" + report + "\"") { UseShellExecute = false, CreateNoWindow = true };
            start.EnvironmentVariables["SM86_BASE_FPS_CAP"] = "30"; start.EnvironmentVariables["SM86_FORCE_VSYNC"] = "1";
            using (Process child = Process.Start(start))
            { check(child.WaitForExit(15000) && child.ExitCode == 0, "Windows loads and executes the generated Steam ASI in a disposable child process"); }
            string values = File.ReadAllText(report);
            check(values.Contains("SM86_BASE_FPS_CAP=0") && values.Contains("SM86_FORCE_VSYNC=0") && values.Contains("SM86_ENABLE_D3D11_BRIDGE=1") && values.Contains("Second=2"), "Steam ASI applies the uncapped profile before backend loading and initializes only once");
            string library = Path.Combine(root, "steam-toggle-library", "steamapps"), gameDir = Path.Combine(library, "common", "Toggle Fixture");
            Directory.CreateDirectory(gameDir); string selected = Path.Combine(gameDir, "ToggleFixture.exe"); File.Copy(game, selected);
            File.WriteAllText(Path.Combine(library, "appmanifest_12345.acf"), "\"AppState\" { \"appid\" \"12345\" \"installdir\" \"Toggle Fixture\" }");
            string originalPath = Path.Combine(gameDir, "version.dll"); File.WriteAllText(originalPath, "original toggle fixture bytes"); string original = Disk.Hash(originalPath);
            TrialStore store = new TrialStore(Path.Combine(root, "steam-toggle-data"));
            BundledCatalog catalog = new BundledCatalog(store.Root); WizardController flow = new WizardController(store, catalog);
            flow.SelectGame(selected); flow.SelectGraphicsApi("dx12").GetAwaiter().GetResult();
            flow.EnableSteam().GetAwaiter().GetResult();
            check(flow.SteamEnabled && store.ReadAll().Single(t => t.State != "Restored").PersistentSteam && File.Exists(Path.Combine(gameDir, "FramegenLab.Steam.asi")), "Steam toggle stages its loader, process configuration ASI and backend with durable original backups");
            WizardController reopened = new WizardController(store, new BundledCatalog(store.Root));
            check(reopened.SteamEnabled && reopened.State.GraphicsApi == "dx12", "Steam setup and explicit DirectX choice survive closing and reopening the app");
            check(reopened.CanAnswerYes && reopened.CanAnswerNo, "Persistent setup enables the same Yes / No actions used by buttons and F1 / F2");
            reopened.Busy = true;
            check(!reopened.CanAnswerYes && !reopened.CanAnswerNo, "Persistent answers are disabled while a file operation is busy");
            reopened.Busy = false;
            reopened.Yes();
            check(reopened.SteamEnabled && reopened.State.Winner == 0 && store.ReadAll().Single(t => t.State != "Restored").Outcome.StartsWith("Yes"), "Yes records the installed persistent trial without disabling its normal launches");
            reopened.No().GetAwaiter().GetResult();
            check(reopened.SteamEnabled && reopened.State.Index == 1 && reopened.State.Winner == -1 && store.ReadAll().Count(t => t.State != "Restored") == 1 && store.ReadAll().Any(t => t.State == "Restored" && t.Outcome.StartsWith("No")), "No restores a persistent attempt, records its verdict and installs exactly the next option");
            reopened.Stop().GetAwaiter().GetResult();
            check(!reopened.SteamEnabled && Disk.Hash(originalPath) == original && !File.Exists(Path.Combine(gameDir, "FramegenLab.Steam.asi")) && !File.Exists(Path.Combine(gameDir, "FramegenLab.SteamBackend.dll")) && !File.Exists(Path.Combine(gameDir, "version.ini")) && store.ReadAll().All(t => t.State == "Restored"), "Disable and Remove test DLL restoration removes every tracked Steam helper and restores the original DLL");
            reopened.Reset().GetAwaiter().GetResult(); reopened.SelectGraphicsApi("dx12").GetAwaiter().GetResult();
            reopened.EnableSteam().GetAwaiter().GetResult();
            File.AppendAllText(Path.Combine(gameDir, "version.ini"), "\r\nexternal-change=true");
            WizardController changed = new WizardController(store, new BundledCatalog(store.Root));
            check(!changed.SteamEnabled && changed.State.Phase == "Recovery", "Changed installed Steam files are detected on reopen and require restoration");
            changed.Stop().GetAwaiter().GetResult();
            changed.EnableSteam().GetAwaiter().GetResult(); changed.SelectGraphicsApi("dx11").GetAwaiter().GetResult();
            check(!changed.SteamEnabled && changed.Catalog.Count == 3 && Disk.Hash(originalPath) == original && !File.Exists(Path.Combine(gameDir, "FramegenLab.Steam.asi")), "Changing DirectX disables Steam setup and restores every tracked file before selecting the new sequence");
            reopened = changed;
            bool rejected9 = false, rejected10 = false;
            try { reopened.SelectGraphicsApi("dx9").GetAwaiter().GetResult(); } catch (IOException) { rejected9 = true; }
            try { reopened.SelectGraphicsApi("dx10").GetAwaiter().GetResult(); } catch (IOException) { rejected10 = true; }
            check(rejected9 && rejected10 && reopened.State.GraphicsApi == "dx11" && Disk.Hash(originalPath) == original, "Removed DirectX 9 and 10 choices are rejected without changing the active selection or files");
            Disk.Write(Path.Combine(store.Root, "wizard.json"), new WizardState { Game = selected, GraphicsApi = "dx9", CatalogKey = "dx9|", Phase = "Idle" });
            reopened = new WizardController(store, new BundledCatalog(store.Root));
            check(reopened.State.GraphicsApi == "auto" && reopened.Catalog.Count == 17 && store.ReadAll().All(t => t.State == "Restored"), "A saved obsolete DirectX choice migrates to Automatic while preserving restored trial history");
            PeInfo multi = new PeInfo(); multi.Imports.AddRange(new[] { "d3d11.dll", "D3D12.dll" });
            check(GraphicsApi.Detect(multi).Api == "unknown" && GraphicsApi.Detect(multi).Evidence.Contains("Multiple"), "Automatic selection keeps multiple DirectX imports ambiguous instead of guessing one with certainty");
            PeInfo legacy = new PeInfo(); legacy.Imports.Add("d3d10_1.dll");
            check(GraphicsApi.Detect(legacy).Api == "dx10" && GraphicsApi.Detect(new PeInfo()).Api == "unknown", "Automatic selection recognizes DirectX 10.1 and reports absent import evidence as unknown");
            byte[] delayed = File.ReadAllBytes(inertDll);
            using (MemoryStream image = new MemoryStream(delayed)) using (BinaryWriter writer = new BinaryWriter(image))
            {
                image.Position = 0x104; writer.Write((uint)14);
                image.Position = 0x170; writer.Write((uint)0x1000); writer.Write((uint)64);
                image.Position = 0x200; writer.Write((uint)1); writer.Write((uint)0x1080);
                image.Position = 0x280; writer.Write(System.Text.Encoding.ASCII.GetBytes("d3d9.dll\0"));
            }
            string delayedPath = Path.Combine(folder, "delayed.dll"); File.WriteAllBytes(delayedPath, delayed);
            check(GraphicsApi.Detect(PeInspector.Inspect(delayedPath)).Api == "dx9", "Automatic detection also reads delayed DirectX imports from the executable bytes");
            check(PeInspector.Inspect(delayedPath).DelayedImports.Contains("d3d9.dll") && !PeInspector.Inspect(delayedPath).DirectImports.Contains("d3d9.dll"), "PE inspection preserves delayed import provenance for reliable mixed-API hints");
            reopened.SelectGraphicsApi("auto").GetAwaiter().GetResult();
            check(reopened.State.GraphicsApi == "auto" && reopened.State.Index == 0 && reopened.Catalog.Count == 17, "Returning to Automatic rebuilds the appropriate sequence and starts at option one");

            string standaloneDir = Path.Combine(root, "standalone-persistent-game"); Directory.CreateDirectory(standaloneDir);
            string standaloneGame = Path.Combine(standaloneDir, "StandaloneFixture.exe"); File.Copy(game, standaloneGame);
            string standaloneTarget = Path.Combine(standaloneDir, "version.dll"); File.WriteAllText(standaloneTarget, "standalone original");
            string standaloneOriginal = Disk.Hash(standaloneTarget);
            TrialStore standaloneStore = new TrialStore(Path.Combine(root, "standalone-persistent-data"));
            WizardController standalone = new WizardController(standaloneStore, new BundledCatalog(standaloneStore.Root), "--fixture");
            standalone.SelectGame(standaloneGame); standalone.EnableSteam().GetAwaiter().GetResult();
            check(SteamLaunch.Resolve(standaloneGame) == null && standalone.SteamEnabled && File.Exists(Path.Combine(standaloneDir, "FramegenLab.Steam.asi")), "Persistent setup installs for a non-Steam executable without requiring a Steam library or App ID");
            standalone.Stop().GetAwaiter().GetResult();
            check(Disk.Hash(standaloneTarget) == standaloneOriginal && !File.Exists(Path.Combine(standaloneDir, "FramegenLab.Steam.asi")), "Removing a non-Steam persistent setup restores originals and removes its helpers");
            // Exercise the direct launch route with an inert tracked DLL, never a third-party backend.
            Trial direct = standaloneStore.Prepare(standaloneGame, inertDll, "version.dll", new string[0], false, "", false, Int32.MaxValue);
            direct.PersistentSteam = true; standaloneStore.Save(direct); standaloneStore.Stage(direct);
            standalone.State.SteamTrialId = direct.Id; standalone.State.Phase = "SteamEnabled";
            Disk.Write(Path.Combine(standaloneStore.Root, "wizard.json"), standalone.State);
            standalone = new WizardController(standaloneStore, new BundledCatalog(standaloneStore.Root), "--fixture");
            try
            {
                standalone.StartGame().GetAwaiter().GetResult(); bool opened = false;
                for (int iteration = 0; iteration < 60; ++iteration)
                {
                    foreach (Process child in TrialStore.FindGame(standaloneGame))
                    { try { if (child.MainWindowHandle != IntPtr.Zero) { opened = true; child.CloseMainWindow(); child.WaitForExit(5000); } } finally { child.Dispose(); } }
                    if (opened) break; System.Threading.Thread.Sleep(100);
                }
                check(opened && File.Exists(Path.Combine(standaloneDir, "fixture-environment.txt")), "Open game directly launches a non-Steam executable with its working directory and arguments");
                standalone.Observe().GetAwaiter().GetResult();
                check(standalone.SteamEnabled && Disk.Hash(standaloneTarget) == Disk.Hash(inertDll), "A normal non-Steam game exit retains the explicitly enabled persistent setup");
                standalone.StartGame().GetAwaiter().GetResult(); bool answerWindow = false;
                for (int iteration = 0; iteration < 60 && !answerWindow; ++iteration)
                {
                    foreach (Process child in TrialStore.FindGame(standaloneGame))
                    { try { child.Refresh(); if (child.MainWindowHandle != IntPtr.Zero) answerWindow = true; } finally { child.Dispose(); } }
                    if (!answerWindow) System.Threading.Thread.Sleep(100);
                }
                check(answerWindow && standalone.CanAnswerYes && standalone.CanAnswerNo, "A running persistent game leaves buttons and shortcut actions available");
                standalone.Yes(); standalone.No().GetAwaiter().GetResult();
                var remaining = TrialStore.FindGame(standaloneGame); bool closed = remaining.Count == 0; foreach (Process child in remaining) child.Dispose();
                check(closed && standalone.SteamEnabled && standalone.State.Index == 1 && standalone.State.Winner == -1 && standaloneStore.ReadAll().Count(t => t.State != "Restored") == 1, "Persistent No gracefully closes the running fixture before preparing the next installed option");
            }
            finally { standalone.Stop().GetAwaiter().GetResult(); }
            check(Disk.Hash(standaloneTarget) == standaloneOriginal && standaloneStore.ReadAll().All(t => t.State == "Restored"), "Stopping the direct-launch fixture restores all files and leaves no pending trial");

            byte[] vulkan = File.ReadAllBytes(inertDll);
            using (MemoryStream image = new MemoryStream(vulkan)) using (BinaryWriter writer = new BinaryWriter(image))
            {
                image.Position = 0x96; writer.Write((ushort)0x22);
                image.Position = 0x104; writer.Write((uint)2);
                image.Position = 0x110; writer.Write((uint)0x1000); writer.Write((uint)40);
                image.Position = 0x20c; writer.Write((uint)0x1080);
                image.Position = 0x280; writer.Write(System.Text.Encoding.ASCII.GetBytes("vulkan-1.dll\0"));
            }
            string vulkanPath = Path.Combine(root, "VulkanStaticFixture.exe"); File.WriteAllBytes(vulkanPath, vulkan);
            WizardController vulkanFlow = new WizardController(new TrialStore(Path.Combine(root, "vulkan-data")), new BundledCatalog(Path.Combine(root, "vulkan-data")));
            vulkanFlow.SelectGame(vulkanPath);
            check(GraphicsApi.Detect(PeInspector.Inspect(vulkanPath)).Api == "vulkan" && vulkanFlow.Catalog.Count == 1, "Automatic recognizes Vulkan and selects its dedicated process-local integration");
            PreparedCandidate vk=vulkanFlow.Catalog.Prepare(0);
            check(vk.Profile==VulkanRuntime.Profile&&Disk.Hash(vk.Dll)==VulkanRuntime.LayerHash&&Disk.Hash(Path.Combine(Path.GetDirectoryName(vk.Dll),"NVSmoothBackend.dll"))==VulkanRuntime.BackendHash,"Both embedded Vulkan binaries extract to the exact locally tested hashes without execution");
            PreparedCandidate dx9 = Dx9Runtime.Prepare(Path.Combine(root, "dx9-runtime"));
            check(dx9.Profile == Dx9Runtime.Profile && Disk.Hash(dx9.Dll) == Dx9Runtime.Hash && dx9.Target == "d3d9.dll" && Disk.Hash(dx9.VulkanLayer) == VulkanRuntime.LayerHash, "The DX9 route extracts the official x64 DXVK DLL and verified Vulkan layer without executing them");
            string parentRtss = Environment.GetEnvironmentVariable("DISABLE_RTSS_LAYER");
            var dx9Environment = Dx9Runtime.EnvironmentFor(dx9.VulkanLayer, root);
            check(dx9Environment["DISABLE_RTSS_LAYER"] == "1" && dx9Environment["DXVK_LOG_PATH"] == root && dx9Environment["SM86_BASE_FPS_CAP"] == "0" && Environment.GetEnvironmentVariable("DISABLE_RTSS_LAYER") == parentRtss, "DX9's verified RTSS Vulkan workaround affects only its launch dictionary and adds no FPS cap");
            check(!VulkanRuntime.EnvironmentFor(dx9.VulkanLayer).ContainsKey("DISABLE_RTSS_LAYER"), "The DX9-specific workaround does not change other Vulkan launch profiles");
            check(TestSession.DescribeTranslation("err: Failed to initialize DXVK device.").Contains("unavailable") && TestSession.DescribeTranslation("Creating device:") == null, "A recorded DXVK device failure is reported explicitly instead of implying backend initialization succeeded");
            check(TestSession.DescribeTranslation("Presenter: Actual swapchain properties:\nFormat: VK_FORMAT_B8G8R8A8_UNORM").Contains("not proof"), "DXVK swapchain creation is distinguished from verified frame generation when another overlay changes function dispatch");
            string original9 = Path.Combine(Path.GetDirectoryName(game), "d3d9.dll"); File.WriteAllText(original9, "original DX9 fixture bytes"); string original9Hash = Disk.Hash(original9);
            TestSession translated = new TestSession(new TrialStore(Path.Combine(root, "dx9-staging")));
            translated.StartTranslated(game, dx9, game, "--fixture");
            check(translated.Current.ExternalLayer && translated.Current.Files.Count == 1 && translated.Current.RuntimeLayer == dx9.VulkanLayer && Disk.Hash(original9) == Dx9Runtime.Hash, "DX9 translation stages only d3d9.dll while retaining a separately verified external Vulkan runtime");
            translated.Finish("DX9 staging fixture cleanup").GetAwaiter().GetResult();
            check(Disk.Hash(original9) == original9Hash && translated.Current == null, "Removing the DX9 attempt restores a pre-existing d3d9.dll byte-for-byte"); File.Delete(original9);
            string vkManifest=File.ReadAllText(Path.Combine(Path.GetDirectoryName(vk.Dll),"FramegenVulkan.json"));
            check(vkManifest.Contains(VulkanRuntime.LayerName)&&vkManifest.Contains("FramegenVulkan.dll"),"The extracted explicit-layer manifest identifies the bundled Vulkan integration");
            bool vkPersistentBlocked=false;try{vulkanFlow.EnableSteam().GetAwaiter().GetResult();}catch(IOException){vkPersistentBlocked=true;}
            check(vkPersistentBlocked&&vulkanFlow.Store.ReadAll().Count==0&&!File.Exists(Path.Combine(root,"version.dll")),"Vulkan cannot silently install a persistent DirectX proxy or modify global launch settings");
            var noFileStore=new TrialStore(Path.Combine(root,"external-layer-data"));var noFileSession=new TestSession(noFileStore);
            noFileSession.StartExternal(game,vk.Dll,game,"--fixture");
            check(noFileSession.Current.ExternalLayer&&!noFileSession.Current.Baseline&&noFileSession.Current.Files.Count==0&&noFileSession.Current.State=="Staged","Vulkan preparation journals an external runtime without staging game DLLs");
            noFileSession.Finish("External-layer fixture recovery").GetAwaiter().GetResult();
            check(noFileSession.Current==null&&noFileStore.ReadAll().All(t=>t.State=="Restored"),"Stopping an unlaunched external-layer trial cleanly restores its no-file journal");
            var vkEnv=VulkanRuntime.EnvironmentFor(vk.Dll);
            check(vkEnv["VK_LAYER_PATH"].StartsWith(Path.GetDirectoryName(vk.Dll),StringComparison.OrdinalIgnoreCase)&&vkEnv["VK_INSTANCE_LAYERS"].Contains(VulkanRuntime.LayerName)&&vkEnv["SM86_VK_RELAX_VSYNC"]=="1"&&vkEnv["SM86_BASE_FPS_CAP"]=="0","Vulkan launches inherit the layer path, verified v2 synchronization option, and no app FPS cap");
            bool supportedDriver=true;try{VulkanRuntime.RequireSupportedDriver();}catch(IOException){supportedDriver=false;}
            if(supportedDriver){
                noFileSession.StartExternal(game,vk.Dll,game,"--fixture");
                try{
                    noFileSession.LaunchCurrent(vkEnv);
                    string captured=Path.Combine(Path.GetDirectoryName(game),"fixture-environment.txt");
                    for(int n=0;n<60;n++){noFileSession.Poll();if(noFileSession.Current.Pid!=0&&File.Exists(captured)&&File.ReadAllText(captured).Contains("SM86_VK_RELAX_VSYNC=1"))break;System.Threading.Thread.Sleep(100);}
                    check(noFileSession.Current.Pid!=0&&File.ReadAllText(captured).Contains("SM86_VK_RELAX_VSYNC=1"),"The app launches its own no-Vulkan fixture with the new external-runtime environment and observes its PID");
                }finally{noFileSession.Finish("External-runtime launch fixture").GetAwaiter().GetResult();}
                check(noFileSession.Current==null&&noFileStore.ReadAll().All(t=>t.State=="Restored"),"The external-runtime session closes its owned fixture and finishes without game-file changes");
            }else check(true,"The installed driver guard rejects the experimental Vulkan runtime; live environment fixture skipped on this machine");
        }
    }
}
