// C# 5 · TrialStore.cs · transactional file trials · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FramegenLab
{
    public sealed class TrialStore
    {
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr handle, uint flags, StringBuilder path, ref uint size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        public readonly string Root;
        public string TrialsRoot { get { return Path.Combine(Root, "trials"); } }
        public TrialStore(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(TrialsRoot); Disk.NoLinks(Root); }
        public void Save(Trial t) { ValidateJournal(t); Disk.Write(Path.Combine(t.Folder, "trial.json"), t); }
        public void Log(Trial t, string line)
        { File.AppendAllText(Path.Combine(t.Folder, "events.log"), DateTime.UtcNow.ToString("o") + "  " + line + Environment.NewLine, Encoding.UTF8); }
        public List<Trial> ReadAll()
        {
            List<Trial> result = new List<Trial>();
            foreach (string folder in Directory.GetDirectories(TrialsRoot))
            {
                string journal = Path.Combine(folder, "trial.json");
                if (!File.Exists(journal)) continue;
                try { Trial t = Disk.Read<Trial>(journal); t.Folder = folder; ValidateJournal(t); result.Add(t); }
                catch (Exception e) { throw new IOException("A trial journal could not be read. Keep its backups and inspect it: " + journal, e); }
            }
            return result.OrderByDescending(t => t.StartedUtc).ToList();
        }
        void ValidateJournal(Trial t)
        {
            Disk.PlainName(t.Id);
            string expected = Path.Combine(TrialsRoot, t.Id);
            if (!String.Equals(Path.GetFullPath(t.Folder), expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid trial folder.");
            Disk.NoLinks(t.Folder);
            if (Path.GetFullPath(t.Game) != t.Game || !String.Equals(Path.GetExtension(t.Game), ".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid game path in journal.");
            string gameDir = Path.GetDirectoryName(t.Game); Disk.NoLinks(gameDir);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            if (gameDir.StartsWith(windows, StringComparison.OrdinalIgnoreCase) || String.Equals(gameDir.TrimEnd('\\'), windows.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Testing in the Windows system directory is not supported.");
            if (t.Files == null || t.Files.Count > 128) throw new IOException("Invalid file list.");
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry f in t.Files)
            {
                Disk.PlainName(f.Name);
                if (!seen.Add(f.Name) || !Allowed(f.Name)) throw new IOException("Invalid or duplicate trial file: " + f.Name);
                Disk.NoLinks(Path.Combine(gameDir, f.Name));
            }
        }
        static bool Allowed(string name)
        { return new[] { ".dll", ".asi", ".ini", ".json", ".toml", ".cfg", ".yaml", ".yml" }.Contains(Path.GetExtension(name).ToLowerInvariant()); }
        public static List<Process> FindGame(string game)
        {
            List<Process> matches = new List<Process>();
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(game)))
            {
                try
                {
                    if (p.HasExited) { p.Dispose(); continue; }
                    if (String.Equals(ExecutablePath(p), game, StringComparison.OrdinalIgnoreCase)) matches.Add(p);
                    else p.Dispose();
                }
                catch (InvalidOperationException) { p.Dispose(); }
                catch (System.ComponentModel.Win32Exception)
                { p.Dispose(); throw new IOException("A process with the same game name is protected or inaccessible. Close it before staging or restoring files."); }
                catch (IOException) { p.Dispose(); foreach (Process found in matches) found.Dispose(); throw; }
            }
            return matches;
        }
        static string ExecutablePath(Process process)
        {
            // Query the executable directly, without Framework ProcessModule's
            // startup/exit race. Only request documented limited query access.
            for (int n = 0; n < 12; n++)
            {
                if (process.HasExited) return null;
                IntPtr handle = OpenProcess(0x1000, false, process.Id);
                int error = handle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        StringBuilder path = new StringBuilder(32768); uint length = (uint)path.Capacity;
                        if (QueryFullProcessImageName(handle, 0, path, ref length)) return path.ToString();
                        error = Marshal.GetLastWin32Error();
                    }
                    finally { CloseHandle(handle); }
                }
                if (error == 5) throw new System.ComponentModel.Win32Exception(error);
                process.Refresh(); System.Threading.Thread.Sleep(25);
            }
            if (process.HasExited) return null;
            throw new IOException("The game process is still starting and its executable path is unavailable. Wait a moment, then retry. No files were replaced.");
        }
        public static void RequireClosed(string game)
        {
            List<Process> matches = FindGame(game);
            int count = matches.Count;
            foreach (Process p in matches) p.Dispose();
            if (count > 0) throw new IOException("Close the selected game first. Files are never restored while its process is running.");
        }
        public Trial Prepare(string game, string candidate, string target, string[] companions, bool baseline, string arguments, bool direct, int limit, string[] suspendKnownProxies = null)
        {
            game = Path.GetFullPath(game);
            if (!File.Exists(game) || !String.Equals(Path.GetExtension(game), ".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Select an existing game .exe.");
            Disk.NoLinks(game); RequireClosed(game);
            PeInfo gameInfo = PeInspector.Inspect(game);
            if (gameInfo.IsDll || gameInfo.Architecture != "x64") throw new IOException("This release supports x64 game executables.");
            List<Trial> pending = ReadAll().Where(t => t.State != "Restored").ToList();
            if (pending.Count > 0) throw new IOException("Restore the unfinished trial before starting another one.");
            Trial trial = new Trial { Id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8), Game = game,
                GameHash = gameInfo.Sha256, Baseline = baseline, Candidate = baseline ? "" : Path.GetFullPath(candidate), TargetName = baseline ? "" : target,
                State = "Prepared", StartedUtc = DateTime.UtcNow.ToString("o"), Outcome = "Not assessed", LaunchMode = direct ? "Direct executable" : "Manual / normal launcher",
                Arguments = arguments, LimitSeconds = limit, ModuleObservation = "Not observed" };
            trial.Folder = Path.Combine(TrialsRoot, trial.Id);
            if (!baseline)
            {
                Disk.PlainName(target);
                if (!String.Equals(Path.GetExtension(target), ".dll", StringComparison.OrdinalIgnoreCase)) throw new IOException("The candidate target must end in .dll.");
                PeInfo dll = PeInspector.Inspect(trial.Candidate);
                if (!dll.IsDll || dll.Managed || dll.Architecture != gameInfo.Architecture) throw new IOException("Choose a native x64 DLL matching the game architecture.");
                trial.Files.Add(MakeEntry(trial, trial.Candidate, target));
                foreach (string file in companions) trial.Files.Add(MakeEntry(trial, Path.GetFullPath(file), Path.GetFileName(file)));
                if (suspendKnownProxies != null)
                    foreach (string name in suspendKnownProxies)
                    {
                        Disk.PlainName(name);
                        if (!String.Equals(Path.GetExtension(name), ".dll", StringComparison.OrdinalIgnoreCase) || String.Equals(name, target, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Invalid inactive proxy name.");
                        string path = Path.Combine(Path.GetDirectoryName(game), name); Disk.NoLinks(path);
                        FileAttributes attr = File.GetAttributes(path);
                        if ((attr & (FileAttributes.ReadOnly | FileAttributes.System)) != 0) throw new IOException("An existing known proxy is read-only: " + name);
                        trial.Files.Add(new FileEntry { Name = name, Source = "", CandidateHash = "", OriginalHash = Disk.Hash(path), Existed = true,
                            Attributes = (int)attr, WriteTicks = File.GetLastWriteTimeUtc(path).Ticks, RemovedDuringTrial = true });
                    }
            }
            ValidateJournal(trial);
            Directory.CreateDirectory(trial.Folder);
            // Durable backups and the journal are written before the first game-file change.
            for (int i = 0; i < trial.Files.Count; ++i)
            {
                FileEntry f = trial.Files[i]; string destination = Path.Combine(Path.GetDirectoryName(game), f.Name);
                if (f.Existed)
                {
                    string backup = Path.Combine(trial.Folder, i + ".original");
                    Disk.AtomicCopy(destination, backup);
                    if (Disk.Hash(backup) != f.OriginalHash) throw new IOException("Backup verification failed: " + f.Name);
                }
            }
            Save(trial); Log(trial, "Backups verified. " + (baseline ? "Baseline: no candidate files staged." : "Ready to stage " + trial.Files.Count + " file(s)."));
            return trial;
        }
        FileEntry MakeEntry(Trial trial, string source, string name)
        {
            Disk.PlainName(name); Disk.NoLinks(source);
            if (!Allowed(name) || !File.Exists(source)) throw new IOException("Unsupported or missing companion file: " + source);
            string destination = Path.Combine(Path.GetDirectoryName(trial.Game), name); Disk.NoLinks(destination);
            if (String.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) throw new IOException("Keep candidate files outside the game folder so originals can be restored.");
            if (Directory.Exists(destination)) throw new IOException("A directory already uses the target name: " + name);
            bool exists = File.Exists(destination);
            FileAttributes attributes = exists ? File.GetAttributes(destination) : FileAttributes.Normal;
            if (exists && (attributes & (FileAttributes.ReadOnly | FileAttributes.System)) != 0) throw new IOException("The original file is read-only or a system file: " + name);
            return new FileEntry { Source = source, Name = name, CandidateHash = Disk.Hash(source), Existed = exists,
                OriginalHash = exists ? Disk.Hash(destination) : "", Attributes = (int)attributes, WriteTicks = exists ? File.GetLastWriteTimeUtc(destination).Ticks : 0 };
        }
        public void Stage(Trial t)
        {
            ValidateJournal(t); RequireClosed(t.Game);
            if (t.State != "Prepared") throw new IOException("Only a prepared trial can be staged.");
            // Preflight all sources and destinations before any mutation.
            foreach (FileEntry f in t.Files)
            {
                string destination = Path.Combine(Path.GetDirectoryName(t.Game), f.Name);
                if (!f.RemovedDuringTrial && Disk.Hash(f.Source) != f.CandidateHash) throw new IOException("The candidate changed after inspection: " + f.Name);
                if (File.Exists(destination) != f.Existed || (f.Existed && Disk.Hash(destination) != f.OriginalHash)) throw new IOException("A game file changed after backup: " + f.Name);
            }
            t.State = "Staging"; Save(t);
            try
            {
                foreach (FileEntry f in t.Files)
                {
                    string destination = Path.Combine(Path.GetDirectoryName(t.Game), f.Name);
                    if (f.RemovedDuringTrial)
                    {
                        File.Delete(destination); Log(t, "Temporarily suspended a recognized inactive proxy: " + f.Name); continue;
                    }
                    Disk.AtomicCopy(f.Source, destination);
                    if (Disk.Hash(destination) != f.CandidateHash) throw new IOException("Staged hash mismatch: " + f.Name);
                    Log(t, "Staged " + f.Name + " / SHA-256 " + f.CandidateHash);
                }
                t.State = "Staged"; Save(t);
            }
            catch
            {
                try { Restore(t); } catch (Exception e) { Log(t, "Restore pending: " + e.Message); }
                throw;
            }
        }
        public bool InstalledFilesMatch(Trial trial)
        {
            try
            {
                ValidateJournal(trial);
                foreach (FileEntry file in trial.Files)
                {
                    string path = Path.Combine(Path.GetDirectoryName(trial.Game), file.Name);
                    if (file.RemovedDuringTrial) { if (File.Exists(path)) return false; }
                    else if (!File.Exists(path) || Disk.Hash(path) != file.CandidateHash) return false;
                }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        public void Restore(Trial t)
        {
            ValidateJournal(t); RequireClosed(t.Game);
            if (t.State == "Restored") return;
            // Verify every original before beginning restoration.
            for (int i = 0; i < t.Files.Count; ++i)
            {
                FileEntry f = t.Files[i]; if (f.Restored || !f.Existed) continue;
                string backup = Path.Combine(t.Folder, i + ".original"); Disk.NoLinks(backup);
                if (!File.Exists(backup) || Disk.Hash(backup) != f.OriginalHash) throw new IOException("Original backup is missing or changed: " + backup);
            }
            t.State = "Restoring"; Save(t);
            for (int i = 0; i < t.Files.Count; ++i)
            {
                FileEntry f = t.Files[i]; if (f.Restored) continue;
                for (int attempt = 0; ; ++attempt)
                {
                    try
                    {
                RequireClosed(t.Game);
                string destination = Path.Combine(Path.GetDirectoryName(t.Game), f.Name);
                if (File.Exists(destination))
                {
                    string hash = Disk.Hash(destination);
                    if (hash != f.CandidateHash && (!f.Existed || hash != f.OriginalHash))
                    {
                        string preserved = Path.Combine(t.Folder, i + ".changed-" + Guid.NewGuid().ToString("N"));
                        Disk.AtomicCopy(destination, preserved); Log(t, "Runtime/external change preserved at " + Path.GetFileName(preserved));
                    }
                }
                if (f.Existed)
                {
                    Disk.AtomicCopy(Path.Combine(t.Folder, i + ".original"), destination);
                    File.SetLastWriteTimeUtc(destination, new DateTime(f.WriteTicks, DateTimeKind.Utc));
                    File.SetAttributes(destination, (FileAttributes)f.Attributes);
                    if (Disk.Hash(destination) != f.OriginalHash) throw new IOException("Restored hash mismatch: " + f.Name);
                }
                else if (File.Exists(destination)) File.Delete(destination);
                f.Restored = true; Save(t); break;
                    }
                    catch (Exception e)
                    {
                        int code = e.HResult & 0xffff;
                        bool temporary = (e is IOException || e is UnauthorizedAccessException) && (code == 5 || code == 32 || code == 33);
                        if (!temporary || attempt >= 14) throw;
                        if (attempt == 0) Log(t, "Waiting for Windows to release the file after game exit: " + f.Name);
                        System.Threading.Thread.Sleep(200);
                    }
                }
            }
            t.State = "Restored"; t.EndedUtc = DateTime.UtcNow.ToString("o"); Save(t); Log(t, "Original file bytes verified; trial restored.");
        }
        public void CollectLogs(Trial t)
        {
            Disk.AtomicBytes(Path.Combine(t.Folder, "diagnosis.txt"), Encoding.UTF8.GetBytes(TrialDiagnostics.Report(t)));
            if(t.ExternalLayer)
            {
                string source=Path.Combine(Path.GetDirectoryName(t.RuntimeLayer),"FramegenVulkan.log");
                if(File.Exists(source))try{Disk.NoLinks(source);using(FileStream fs=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){fs.Position=Math.Max(0,fs.Length-65536);using(FileStream output=File.Create(Path.Combine(t.Folder,"FramegenVulkan.log.tail")))fs.CopyTo(output);}}catch(IOException e){Log(t,"Vulkan log unavailable: "+e.Message);}
            }
            string dir = Path.GetDirectoryName(t.Game);
            foreach (string name in new[] { "nvsmooth30.log", "dlssg_to_fsr3.log", "OptiScaler.log", "dinput8.log", "winmm.log", "xinput1_3.log" })
            {
                string path = Path.Combine(dir, name);
                if (!File.Exists(path)) continue;
                try
                {
                    Disk.NoLinks(path);
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fs.Position = Math.Max(0, fs.Length - 65536);
                        byte[] data = new byte[(int)(fs.Length - fs.Position)]; int read = 0;
                        while (read < data.Length) { int n = fs.Read(data, read, data.Length - read); if (n == 0) break; read += n; }
                        File.WriteAllBytes(Path.Combine(t.Folder, name + ".tail"), data.Take(read).ToArray());
                    }
                    Log(t, "Saved last 64 KiB of " + name + "; may contain entries from earlier runs.");
                }
                catch (Exception e) { Log(t, "Log unavailable: " + name + " / " + e.Message); }
            }
        }
        public void Export(string path)
        {
            StringBuilder csv = new StringBuilder("id,started_utc,game,candidate,target,outcome,base_fps_user_reported,displayed_fps_user_reported,module_observation,state,notes\r\n");
            foreach (Trial t in ReadAll())
                csv.AppendLine(String.Join(",", new[] { t.Id, t.StartedUtc, t.Game, t.Candidate, t.TargetName, t.Outcome, t.BaseFps, t.DisplayedFps, t.ModuleObservation, t.State, t.Notes }.Select(Disk.Csv).ToArray()));
            Disk.AtomicBytes(path, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray());
        }
    }
}
