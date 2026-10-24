// C# 5 · TestSession.cs · bounded local trials · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FramegenLab
{
    public sealed class TestSession
    {
        public readonly TrialStore Store;
        public Trial Current;
        public bool Busy;
        public DateTime ArmedUtc;
        bool observed;
        DateTime nextModuleCheck;
        Process launched;
        Process renderer;
        long backendLogStart;
        string BackendLogPath { get { return Current.ExternalLayer ? Path.Combine(Path.GetDirectoryName(Current.RuntimeLayer), "FramegenVulkan.log") : Path.Combine(Path.GetDirectoryName(Current.Game), "nvsmooth30.log"); } }
        void ArmDiagnostics()
        {
            backendLogStart = 0; nextModuleCheck = DateTime.MinValue;
            try { if (!Current.Baseline && File.Exists(BackendLogPath)) { Disk.NoLinks(BackendLogPath); backendLogStart = new FileInfo(BackendLogPath).Length; } }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        DateTime directExitSeen;
        public static string DescribeBackend(string text)
        {
            System.Text.RegularExpressions.Match failed = System.Text.RegularExpressions.Regex.Match(text, @"CreateSwapChainForHwnd\(shadow\) failed:\s*(0x[0-9A-Fa-f]{8})");
            if (failed.Success) return "DLL loaded; D3D11 bridge failed (" + failed.Groups[1].Value + "). Frame generation unavailable.";
            if (text.Contains("Smooth Motion activated on PRIMARY wrapper.")) return "Presentation wrapper active; visual frame generation still needs your confirmation.";
            if (text.Contains("NVP_Init_D3D=TRUE")) return "NVSmooth30 initialized; frame generation is not yet confirmed.";
            return null;
        }
        void SampleBackend()
        {
            if (Current == null || Current.Baseline) return;
            if (Current.ExternalLayer && Current.Files.Any(f => String.Equals(f.Name, "d3d9.dll", StringComparison.OrdinalIgnoreCase)))
            {
                string translatedLog = Path.Combine(Current.Folder, Path.GetFileNameWithoutExtension(Current.Game) + "_d3d9.log");
                try
                {
                    if (File.Exists(translatedLog) && File.GetLastWriteTimeUtc(translatedLog) >= ArmedUtc)
                    {
                        Disk.NoLinks(translatedLog);
                        using (var stream = new FileStream(translatedLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            stream.Position = Math.Max(0, stream.Length - 8192);
                            using (var reader = new StreamReader(stream))
                            {
                                string failure = DescribeTranslation(reader.ReadToEnd());
                                if (failure != null && failure != Current.BackendObservation)
                                { Current.BackendObservation = failure; Store.Save(Current); Store.Log(Current, failure); }
                            }
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            string path = BackendLogPath;
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < ArmedUtc) return;
            try
            {
                Disk.NoLinks(path);
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < backendLogStart) backendLogStart = 0; // Backend truncated its log for the new run.
                    fs.Position = Math.Max(backendLogStart, fs.Length - 32768);
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        string text=reader.ReadToEnd();
                        string status = Current.ExternalLayer?(text.Contains("vkCreateSwapchainKHR result=0")?"Vulkan presentation initialized. Confirm frame generation visually; initialization is not proof.":null):DescribeBackend(text);
                        if (status != null && status != Current.BackendObservation)
                        { Current.BackendObservation = status; Store.Save(Current); Store.Log(Current, status); }
                    }
                }
            }
            catch (IOException) { } // Diagnostic sampling must not interrupt a running game.
            catch (UnauthorizedAccessException) { }
        }
        public static string DescribeTranslation(string text)
        {
            if (text.Contains("Failed to initialize DXVK device."))
                return "DX9 -> Vulkan device initialization failed. Frame generation unavailable; inspect the trial's DXVK log.";
            if (text.Contains("Presenter: Actual swapchain properties:") && text.Contains("VK_FORMAT_"))
                return "DXVK presentation initialized. Confirm frame generation visually; presentation is not proof.";
            return null;
        }
        public TestSession(TrialStore store) { Store = store; }
        public void StartExternal(string game,string layer,string launcher,string arguments)
        {
            if(Current!=null||Busy)throw new IOException("Finish or restore the current trial first.");
            // A no-file journal uses the same close/recovery/verdict machinery as DLL trials.
            Trial t=Store.Prepare(game,"","",new string[0],true,arguments,false,Int32.MaxValue);
            t.Baseline=false;t.ExternalLayer=true;t.Candidate=layer;t.TargetName="FramegenVulkan.dll";t.LaunchExecutable=launcher;t.LaunchHash=Disk.Hash(launcher);
            Store.Save(t);Store.Stage(t);Current=t;observed=false;directExitSeen=DateTime.MinValue;ArmedUtc=DateTime.UtcNow;
            ArmDiagnostics();
            Store.Log(t,"Process-local Vulkan trial. No DLL or configuration file was copied into the game directory.");
        }
        public void StartTranslated(string game, PreparedCandidate candidate, string launcher, string arguments)
        {
            Start(game, candidate.Dll, candidate.Target, candidate.Companions, false, arguments, false, Int32.MaxValue);
            Current.ExternalLayer = true; Current.VulkanLayer = candidate.VulkanLayer; Store.Save(Current);
            SetLauncher(launcher); ArmDiagnostics();
            Store.Log(Current, "DX9 translation trial: tracked DXVK d3d9.dll plus process-local Vulkan frame-generation layer. Original files will be restored.");
        }
        public void SetLauncher(string launcher)
        {
            if (Current == null || Current.State != "Staged") throw new IOException("Prepare a trial before choosing its launcher.");
            GameTarget target = GameTarget.Resolve(launcher);
            if (!String.Equals(target.Renderer, Current.Game, StringComparison.OrdinalIgnoreCase)) throw new IOException("The launcher does not target the staged renderer.");
            Current.LaunchExecutable = target.Launcher; Current.LaunchHash = Disk.Hash(target.Launcher); Store.Save(Current);
            Store.Log(Current, target.Evidence + " Renderer: " + Current.Game + ". Launcher: " + Current.LaunchExecutable);
        }
        public void Start(string game, string candidate, string target, string[] companions, bool baseline, string arguments, bool direct, int limit, IDictionary<string, string> environment = null, string[] suspendKnownProxies = null)
        {
            if (Current != null || Busy) throw new IOException("Finish or restore the current trial first.");
            Trial t = Store.Prepare(game, candidate, target, companions, baseline, arguments, direct, limit, suspendKnownProxies);
            Current = t; observed = false; directExitSeen = DateTime.MinValue; ArmedUtc = DateTime.UtcNow;
            try
            {
                Store.Stage(t);
                ArmDiagnostics();
                if (direct)
                {
                    LaunchCurrent(environment);
                }
                else Store.Log(t, "Waiting for the user to open the game through its normal launcher.");
            }
            catch
            {
                if (t.State != "Restored") { try { Store.Restore(t); } catch (Exception e) { Store.Log(t, "Recovery required: " + e.Message); } }
                if (t.State == "Restored") Current = null;
                throw;
            }
        }
        public void LaunchCurrent(IDictionary<string, string> environment)
        {
            if (Current == null || Busy || Current.State != "Staged") throw new IOException("Prepare a test before launching the game.");
            TrialStore.RequireClosed(Current.Game);
            if(Current.ExternalLayer)
            {
                VulkanRuntime.RequireSupportedDriver();
                if(Disk.Hash(Current.RuntimeLayer)!=VulkanRuntime.LayerHash||Disk.Hash(Path.Combine(Path.GetDirectoryName(Current.RuntimeLayer),"NVSmoothBackend.dll"))!=VulkanRuntime.BackendHash)throw new IOException("The prepared Vulkan runtime changed. Stop and restore, then prepare it again.");
                if (!Store.InstalledFilesMatch(Current)) throw new IOException("A staged translation file changed. Stop and restore before launching.");
                if(!String.Equals(Current.Game,GameTarget.Resolve(Current.LaunchExecutable).Renderer,StringComparison.OrdinalIgnoreCase))throw new IOException("The normal game launcher changed.");
            }
            string executable=String.IsNullOrEmpty(Current.LaunchExecutable)?Current.Game:Current.LaunchExecutable;
            Disk.NoLinks(executable); TrialStore.RequireClosed(executable);
            if (Disk.Hash(Current.Game) != Current.GameHash || (!String.IsNullOrEmpty(Current.LaunchHash) && Disk.Hash(executable) != Current.LaunchHash))
                throw new IOException("The game or launcher changed after preparation. Stop + restore, then select the game again.");
            ProcessStartInfo start = new ProcessStartInfo(executable, Current.Arguments) { WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false };
            foreach (string key in LaunchProfiles.Keys) start.EnvironmentVariables.Remove(key);
            Current.LaunchEnvironment = new Dictionary<string, string>();
            if (environment != null)
                foreach (KeyValuePair<string, string> item in environment) { start.EnvironmentVariables[item.Key] = item.Value; Current.LaunchEnvironment.Add(item.Key, item.Value); }
            SteamGameContext steam = SteamLaunch.Apply(start);
            if (steam != null)
            {
                Current.LaunchEnvironment["SteamAppId"] = steam.AppId; Current.LaunchEnvironment["SteamGameId"] = steam.AppId;
                Store.Log(Current, "Installed Steam context: App ID " + steam.AppId + " / " + steam.Manifest);
            }
            Current.LaunchMode = "Start game button / child process environment";
            ArmedUtc = DateTime.UtcNow; ArmDiagnostics(); Store.Save(Current);
            Store.Log(Current, "Launch configuration: " + String.Join(", ", Current.LaunchEnvironment.Select(item => item.Key + "=" + item.Value).ToArray()));
            if (launched != null) launched.Dispose();
            launched = Process.Start(start); Bind(launched);
        }
        public void AttachForRecovery(Trial trial)
        {
            if (Current != null || Busy) throw new IOException("A trial is already attached.");
            Current = trial; observed = trial.Pid != 0;
            ArmedUtc = DateTime.Parse(trial.StartedUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
        }
        void Bind(Process p)
        {
            if (p == null) return;
            try
            {
                if (p.HasExited) return;
                ProcessModule module = p.MainModule;
                if (module == null || !String.Equals(module.FileName, Current.Game, StringComparison.OrdinalIgnoreCase)) return;
                Current.Pid = p.Id; Current.ProcessStartTicks = p.StartTime.ToUniversalTime().Ticks;
                if (renderer != null) renderer.Dispose();
                renderer = Process.GetProcessById(p.Id); renderer.EnableRaisingEvents = true;
            }
            catch (InvalidOperationException) { return; }
            catch (System.ComponentModel.Win32Exception) { return; }
            catch (NullReferenceException) { return; } // .NET can lose the native module while an immediately exiting process is sampled.
            Current.State = "Running"; observed = true; Store.Save(Current);
            Store.Log(Current, "Observed selected executable, PID " + p.Id + ". Launch is not proof of frame generation.");
        }
        public string Poll()
        {
            if (Current == null || Busy) return "";
            if (Current.State == "Restored") return "finished";
            List<Process> games = TrialStore.FindGame(Current.Game);
            try
            {
                if (renderer != null && renderer.HasExited) Current.ExitCode = "0x" + unchecked((uint)renderer.ExitCode).ToString("X8");
                if (!observed && games.Count > 0) Bind(games[0]);
                if (games.Count > 0) SampleBackend();
                if (launched != null && launched.HasExited)
                {
                    Current.LauncherExitCode = "0x" + unchecked((uint)launched.ExitCode).ToString("X8");
                    if (String.Equals(Current.LaunchExecutable ?? Current.Game, Current.Game, StringComparison.OrdinalIgnoreCase)) Current.ExitCode = Current.LauncherExitCode;
                    if (directExitSeen == DateTime.MinValue) directExitSeen = DateTime.UtcNow;
                    int grace=!observed&&!String.IsNullOrEmpty(Current.LaunchExecutable)&&!String.Equals(Current.LaunchExecutable,Current.Game,StringComparison.OrdinalIgnoreCase)?90:3;
                    if (games.Count == 0 && (DateTime.UtcNow - directExitSeen).TotalSeconds >= grace) return "exited";
                }
                else if (observed && games.Count == 0) return "exited";
                if (!Current.Baseline && games.Count > 0 && DateTime.UtcNow >= nextModuleCheck && Current.ModuleObservation != "Candidate path observed in module list")
                {
                    nextModuleCheck = DateTime.UtcNow.AddSeconds(10);
                    try
                    {
                        string staged = Current.ExternalLayer && Current.Files.Count == 0 ? Current.RuntimeLayer : Path.Combine(Path.GetDirectoryName(Current.Game), Current.TargetName);
                        bool found = false;
                        List<string> graphics = new List<string>();
                        foreach (ProcessModule module in games[0].Modules)
                        {
                            if (String.Equals(module.FileName, staged, StringComparison.OrdinalIgnoreCase)) found = true;
                            if (new[] { "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll", "vulkan-1.dll" }.Contains(module.ModuleName, StringComparer.OrdinalIgnoreCase)) graphics.Add(module.ModuleName);
                        }
                        Current.GraphicsModules = String.Join(", ", graphics.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
                        Current.ModuleObservation = found ? "Candidate path observed in module list" : "Candidate not observed in this module sample";
                    }
                    catch (Exception e) { Current.ModuleObservation = "Module list unavailable: " + e.GetType().Name; }
                    Store.Save(Current);
                }
            }
            finally { foreach (Process p in games) p.Dispose(); }
            if ((DateTime.UtcNow - ArmedUtc).TotalSeconds >= Current.LimitSeconds) return "timeout";
            return "";
        }
        public async Task<Trial> Finish(string reason)
        {
            if (Current == null || Busy) return null;
            Busy = true; Trial t = Current;
            try
            {
                Store.Log(t, "Finish requested: " + reason);
                if(!observed&&launched!=null&&!launched.HasExited&&!String.Equals(t.LaunchExecutable,t.Game,StringComparison.OrdinalIgnoreCase))
                {
                    bool requested=launched.CloseMainWindow();
                    for(int n=0;n<30&&!launched.HasExited;n++){
                        if(!requested){launched.Refresh();requested=launched.CloseMainWindow();}
                        await Task.Delay(250);
                    }
                    if(!launched.HasExited)throw new IOException("Close the game's launcher before ending this test. It will not be force-terminated.");
                }
                List<Process> games = TrialStore.FindGame(t.Game);
                HashSet<int> closeRequested=new HashSet<int>();
                try
                {
                    foreach (Process p in games)
                    {
                        try
                        {
                            // Require the same executable and a process started during this trial.
                            if (p.StartTime.ToUniversalTime() < ArmedUtc.AddSeconds(-2)) throw new IOException("A pre-existing game process will not be closed.");
                            if (!p.HasExited && p.CloseMainWindow())closeRequested.Add(p.Id);
                        }
                        catch (InvalidOperationException) { }
                    }
                    for (int i = 0; i < 40; ++i)
                    {
                        bool alive = false;
                        foreach (Process p in games) { try { if (!p.HasExited) {alive = true;if(!closeRequested.Contains(p.Id)){p.Refresh();if(p.CloseMainWindow())closeRequested.Add(p.Id);}} else t.ExitCode = "0x" + unchecked((uint)p.ExitCode).ToString("X8"); } catch (InvalidOperationException) { } }
                        if (!alive) break;
                        await Task.Delay(250);
                    }
                }
                finally { foreach (Process p in games) p.Dispose(); }
                TrialStore.RequireClosed(t.Game);
                if (renderer != null && renderer.HasExited) t.ExitCode = "0x" + unchecked((uint)renderer.ExitCode).ToString("X8");
                SampleBackend(); Store.CollectLogs(t); Store.Restore(t); Current = null;
                if (launched != null) { launched.Dispose(); launched = null; }
                if (renderer != null) { renderer.Dispose(); renderer = null; }
                return t;
            }
            catch (Exception e)
            {
                t.State = "Recovery pending";
                try { Store.Save(t); } catch (Exception saveError) { Store.Log(t, "Could not update recovery status: " + saveError.Message); }
                Store.Log(t, "RESTORE PENDING: " + e.Message);
                throw;
            }
            finally { Busy = false; }
        }
    }
}
