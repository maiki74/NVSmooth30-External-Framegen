// C# 5 · WizardController.cs · yes/no trial sequence · Windows x64 · .NET Framework 4.8
using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace FramegenLab
{
    [DataContract] public sealed class WizardState
    {
        [DataMember] public string Game = "";
        [DataMember] public string LaunchExecutable;
        [DataMember] public string TargetEvidence;
        [DataMember] public int Index;
        [DataMember] public int Winner = -1;
        [DataMember] public string Phase = "Idle";
        [DataMember] public string TrialId;
        [DataMember] public string CatalogKey;
        [DataMember] public string GraphicsApi = "auto";
        [DataMember] public string SteamTrialId;
    }
    public sealed class WizardController
    {
        public readonly TrialStore Store;
        public readonly TestSession Session;
        public readonly ICandidateCatalog Catalog;
        public WizardState State;
        public bool Busy;
        public bool SteamEnabled { get { return State.Phase == "SteamEnabled" && !String.IsNullOrEmpty(State.SteamTrialId); } }
        public bool CanAnswerYes { get { return !Busy && (State.Phase == "Running" || State.Phase == "AwaitingAnswer" || SteamEnabled); } }
        public bool CanAnswerNo { get { return CanAnswerYes || (!Busy && State.Phase == "Ready"); } }
        readonly string arguments;
        string catalogKey;
        bool catalogChanged;
        string StatePath { get { return Path.Combine(Store.Root, "wizard.json"); } }
        public WizardController(TrialStore store, ICandidateCatalog catalog, string fixtureArguments = "")
        {
            Store = store; Catalog = catalog; Session = new TestSession(store); arguments = fixtureArguments;
            State = File.Exists(StatePath) ? Disk.Read<WizardState>(StatePath) : new WizardState();
            // Old unsupported manual choices return to Automatic without losing trial backups.
            State.GraphicsApi = State.GraphicsApi == "dx9" || State.GraphicsApi == "dx10" ? "auto" : FramegenLab.GraphicsApi.Normalize(State.GraphicsApi);
            Trial[] unfinished = Store.ReadAll().Where(t => t.State != "Restored").ToArray();
            bool pending = unfinished.Length > 0;
            IGameCatalog gameCatalog = Catalog as IGameCatalog;
            if (gameCatalog != null)
            {
                catalogKey = gameCatalog.ConfigureGame(State.Game, State.GraphicsApi); catalogChanged = State.CatalogKey != catalogKey;
                if (catalogChanged && !pending)
                { State.Index = 0; State.Winner = -1; State.TrialId = null; State.Phase = "Idle"; State.CatalogKey = catalogKey; catalogChanged = false; Save(); }
            }
            if (!pending && (State.Index < 0 || State.Index > Catalog.Count || State.Winner >= Catalog.Count)) throw new IOException("The saved test sequence does not match the bundled catalog.");
            if (pending)
            {
                Trial persistent = unfinished.Length == 1 ? unfinished[0] : null;
                if (persistent != null && persistent.PersistentSteam && persistent.State == "Staged" && !catalogChanged && String.Equals(State.Game, persistent.Game, StringComparison.OrdinalIgnoreCase) && Store.InstalledFilesMatch(persistent))
                { State.SteamTrialId = persistent.Id; State.Phase = "SteamEnabled"; }
                else State.Phase = "Recovery";
                if (File.Exists(State.Game) && !String.Equals(GameTarget.Resolve(State.LaunchExecutable ?? State.Game).Renderer, State.Game, StringComparison.OrdinalIgnoreCase))
                    State.Phase = "Recovery"; // Restore the old bootstrap-folder setup before moving files.
            }
            else if (State.Phase == "Running" || State.Phase == "Ready" || State.Phase == "Preparing") State.Phase = "Idle";
            if (!pending && File.Exists(State.LaunchExecutable ?? State.Game)) ApplyTarget(GameTarget.Resolve(State.LaunchExecutable ?? State.Game));
        }
        void Save() { Disk.Write(StatePath, State); }
        void ApplyTarget(GameTarget target)
        {
            bool moved = !String.Equals(State.Game, target.Renderer, StringComparison.OrdinalIgnoreCase);
            State.Game = target.Renderer; State.LaunchExecutable = target.Launcher; State.TargetEvidence = target.Evidence;
            IGameCatalog gameCatalog = Catalog as IGameCatalog;
            string key = gameCatalog == null ? null : gameCatalog.ConfigureGame(State.Game, State.GraphicsApi);
            if (moved || key != State.CatalogKey)
            { State.Index = 0; State.Winner = -1; State.TrialId = null; State.SteamTrialId = null; State.Phase = "Idle"; }
            catalogKey = key; State.CatalogKey = key; catalogChanged = false; Save();
        }
        public void SelectGame(string path)
        {
            if (Busy || Session.Current != null || Store.ReadAll().Any(t => t.State != "Restored")) throw new IOException("Stop and restore the current test before changing games.");
            GameTarget target = GameTarget.Resolve(path);
            if (!String.Equals(target.Renderer, State.Game, StringComparison.OrdinalIgnoreCase)) State = new WizardState();
            ApplyTarget(target);
        }
        public async Task SelectGraphicsApi(string mode)
        {
            string value = FramegenLab.GraphicsApi.Normalize(mode);
            if (Busy) throw new IOException("Wait for the current file operation to finish.");
            if (value == State.GraphicsApi) return;
            IGameCatalog gameCatalog = Catalog as IGameCatalog;
            if (gameCatalog == null) throw new IOException("This catalog does not support graphics API selection.");
            // Do not change the catalog or saved selection until the current files are restored.
            await Stop();
            catalogKey = gameCatalog.ConfigureGame(State.Game, value);
            State.GraphicsApi = value; State.CatalogKey = catalogKey; State.Index = 0; State.Winner = -1;
            State.TrialId = null; State.Phase = "Idle"; catalogChanged = false; Save();
        }
        async Task Prepare()
        {
            if (State.Index >= Catalog.Count) { State.Phase = "Exhausted"; Save(); return; }
            State.Phase = "Preparing"; Save();
            PreparedCandidate candidate = await Task.Run(() => Catalog.Prepare(State.Index));
            if(candidate.Profile==Dx9Runtime.Profile)
            {
                VulkanRuntime.RequireSupportedDriver();
                Session.StartTranslated(State.Game, candidate, State.LaunchExecutable ?? State.Game, arguments);
                State.TrialId=Session.Current.Id; State.Phase="Ready"; Save(); return;
            }
            if(candidate.Profile==VulkanRuntime.Profile)
            {
                VulkanRuntime.RequireSupportedDriver();
                Session.StartExternal(State.Game,candidate.Dll,State.LaunchExecutable ?? VulkanRuntime.Launcher(State.Game),arguments);
                State.TrialId=Session.Current.Id;State.Phase="Ready";Save();return;
            }
            // Only one candidate is staged. Any previous trial must already be restored.
            IConflictCatalog conflicts = Catalog as IConflictCatalog;
            string[] inactive = conflicts == null ? null : conflicts.InactiveKnownProxies(State.Game, candidate.Target);
            Session.Start(State.Game, candidate.Dll, candidate.Target, candidate.Companions, false, arguments, false, Int32.MaxValue, null, inactive);
            Session.SetLauncher(State.LaunchExecutable ?? State.Game);
            State.TrialId = Session.Current.Id; State.Phase = "Ready"; Save();
        }
        public async Task StartGame()
        {
            if (Busy) return; Busy = true;
            try
            {
                if (State.Phase == "Recovery") throw new IOException("Restore the interrupted test first.");
                if (SteamEnabled)
                {
                    Trial installed = Store.ReadAll().SingleOrDefault(t => t.Id == State.SteamTrialId);
                    if (installed == null || !Store.InstalledFilesMatch(installed))
                    { State.Phase = "Recovery"; Save(); throw new IOException("The installed setup changed. Restore it before starting again."); }
                    if (Disk.Hash(installed.Game) != installed.GameHash || (!String.IsNullOrEmpty(installed.LaunchHash) && Disk.Hash(installed.LaunchExecutable) != installed.LaunchHash))
                    { State.Phase = "Recovery"; Save(); throw new IOException("The installed game or launcher changed. Restore this setup and select the updated game before testing again."); }
                    TrialStore.RequireClosed(State.Game);
                    SteamGameContext steam = SteamLaunch.Resolve(State.Game);
                    if (steam != null) ProcessStartSteam(steam.AppId);
                    else
                    {
                        string launcher = State.LaunchExecutable ?? State.Game;
                        using (System.Diagnostics.Process game = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(launcher, arguments) { WorkingDirectory = Path.GetDirectoryName(launcher), UseShellExecute = false })) { }
                    }
                    return;
                }
                IGameCatalog gameCatalog = Catalog as IGameCatalog;
                if (gameCatalog != null && !String.IsNullOrEmpty(gameCatalog.UnavailableReason)) throw new IOException(gameCatalog.UnavailableReason);
                await SteamLaunch.EnsureClient(State.Game);
                if (State.Phase == "Exhausted") throw new IOException("All bundled options were tested. No working option was confirmed.");
                if (State.Phase == "Idle" || State.Phase == "Stopped" || State.Phase == "Saved")
                {
                    if (State.Winner >= 0) State.Index = State.Winner;
                    await Prepare();
                }
                if (State.Phase != "Ready" || Session.Current == null) throw new IOException("Finish answering the current test first.");
                PreparedCandidate candidate = Catalog.Prepare(State.Index);
                var launchEnvironment = candidate.Profile == Dx9Runtime.Profile ? Dx9Runtime.EnvironmentFor(candidate.VulkanLayer, Session.Current.Folder) :
                    candidate.Profile == VulkanRuntime.Profile ? VulkanRuntime.EnvironmentFor(candidate.Dll) : LaunchProfiles.Values(candidate.Profile);
                if(candidate.Profile == Dx9Runtime.Profile) Store.Log(Session.Current, "DX9 compatibility: disabled the conflicting RTSS Vulkan layer for this child process only. RTSS configuration and process are unchanged; overlay availability requires user confirmation.");
                Session.LaunchCurrent(launchEnvironment); State.Phase = "Running"; Save();
            }
            catch { if (Session.Current == null && State.Phase == "Preparing") State.Phase = "Idle"; Save(); throw; }
            finally { Busy = false; }
        }
        static void ProcessStartSteam(string appId)
        { using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("steam://rungameid/" + appId) { UseShellExecute = true })) { } }
        public async Task EnableSteam()
        {
            if (Busy) throw new IOException("Wait for the current file operation to finish.");
            if (SteamEnabled) return;
            BundledCatalog bundled = Catalog as BundledCatalog;
            if (bundled == null || bundled.Count == 0 || State.Index >= bundled.Count) throw new IOException("Choose an available DLL option before enabling persistent setup. Reset tests if the sequence is exhausted.");
            if(bundled.VulkanActive)throw new IOException("Start Vulkan games from this app. This integration applies only to the launched process tree.");
            await Stop(); Busy = true;
            try
            {
                await PreparePersistent(bundled);
            }
            catch { State.Phase = "Recovery"; Save(); throw; }
            finally { Busy = false; }
        }
        async Task PreparePersistent(BundledCatalog bundled)
        {
            PreparedCandidate setup = await Task.Run(() => bundled.PrepareSteam(State.Index, State.Game));
            Trial trial = Store.Prepare(State.Game, setup.Dll, setup.Target, setup.Companions, false, "", false, Int32.MaxValue, bundled.InactiveKnownProxies(State.Game, setup.Target));
            trial.PersistentSteam = true; Store.Save(trial); State.SteamTrialId = trial.Id; Save();
            trial.LaunchExecutable = State.LaunchExecutable ?? State.Game; trial.LaunchHash = Disk.Hash(trial.LaunchExecutable); Store.Save(trial);
            Store.Stage(trial); State.Phase = "SteamEnabled"; State.TrialId = null; Save();
            Store.Log(trial, "Persistent setup prepared for normal launches. Files remain installed until Disable / Remove test DLL. No launcher settings or global environment changed.");
        }
        void Verdict(Trial trial, string value)
        { trial.Outcome = value; Store.Save(trial); Store.Log(trial, "User verdict: " + value); }
        Trial AnsweredTrial()
        {
            if (Session.Current != null) return Session.Current;
            string id = SteamEnabled ? State.SteamTrialId : State.TrialId;
            return Store.ReadAll().SingleOrDefault(t => t.Id == id);
        }
        public string Diagnosis()
        {
            Trial trial = AnsweredTrial();
            return trial == null ? (State.TargetEvidence ?? "Choose a game to inspect its executable and launcher.") : TrialDiagnostics.Report(trial);
        }
        public async Task No()
        {
            if (Busy) return;
            if (!CanAnswerNo) throw new IOException("There is no test to answer.");
            bool persistent = SteamEnabled;
            Busy = true;
            try
            {
                Trial trial = AnsweredTrial(); if (trial == null) throw new IOException("The trial record is missing.");
                Verdict(trial, "No - frame generation did not work (user)");
                // A failed close must stop here; no index advance or file replacement is allowed.
                if (persistent && Session.Current == null) Session.AttachForRecovery(trial);
                if (Session.Current != null) await Session.Finish("User answered No");
                State.Index++; State.Winner = -1; State.TrialId = null; State.SteamTrialId = null;
                if (State.Index >= Catalog.Count) { State.Phase = "Exhausted"; Save(); return; }
                if (persistent) await PreparePersistent((BundledCatalog)Catalog);
                else await Prepare();
            }
            catch
            {
                if (Session.Current != null && Session.Current.State == "Recovery pending") State.Phase = "Recovery";
                else if (persistent) State.Phase = Store.ReadAll().Any(t => t.State != "Restored") ? "Recovery" : "Idle";
                else if (Session.Current == null && State.Phase == "Preparing") State.Phase = "Idle";
                Save(); throw;
            }
            finally { Busy = false; }
        }
        public void Yes()
        {
            if (!CanAnswerYes) throw new IOException("Run a test before answering Yes.");
            Trial trial = AnsweredTrial(); if (trial == null) throw new IOException("The trial record is missing.");
            Verdict(trial, "Yes - frame generation observed (user)"); State.Winner = State.Index;
            State.Phase = SteamEnabled ? "SteamEnabled" : Session.Current == null ? "Saved" : "Success"; Save();
        }
        public async Task Observe()
        {
            if (Busy || Session.Current == null || State.Phase == "Recovery") return;
            string observation = Session.Poll();
            if (observation != "exited") return;
            Busy = true;
            try
            {
                bool success = State.Phase == "Success";
                await Session.Finish("Selected game exited"); State.Phase = success ? "Saved" : "AwaitingAnswer"; Save();
            }
            finally { Busy = false; }
        }
        public async Task Stop()
        {
            if (Busy) throw new IOException("Wait for the current file operation to finish."); Busy = true;
            try
            {
                if (Session.Current != null) await Session.Finish("Stop and restore");
                foreach (Trial t in Store.ReadAll().Where(t => t.State != "Restored"))
                {
                    // Old versions sometimes staged next to a bootstrap. Do not restore
                    // that setup while its separately located renderer is still open.
                    if (File.Exists(t.Game)) TrialStore.RequireClosed(GameTarget.Resolve(t.Game).Renderer);
                    Session.AttachForRecovery(t); await Session.Finish("Recover interrupted test");
                }
                if (catalogChanged) { State.Index = 0; State.Winner = -1; State.CatalogKey = catalogKey; catalogChanged = false; }
                if (File.Exists(State.LaunchExecutable ?? State.Game)) ApplyTarget(GameTarget.Resolve(State.LaunchExecutable ?? State.Game));
                State.Phase = State.Winner >= 0 ? "Saved" : "Stopped"; State.TrialId = null; State.SteamTrialId = null; Save();
            }
            catch { State.Phase = "Recovery"; Save(); throw; }
            finally { Busy = false; }
        }
        public async Task Reset()
        {
            // Reset only after all tracked files have been restored successfully.
            await Stop();
            State.Index = 0; State.Winner = -1; State.TrialId = null; State.Phase = "Idle";
            Save();
        }
    }
}
