// C# 5 · SelfTests.cs · disposable fixtures only · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace FramegenLab
{
    public static class SelfTests
    {
        static readonly List<string> passed = new List<string>();
        static void Check(bool condition, string text) { if (!condition) throw new Exception("ASSERT: " + text); passed.Add("PASS " + text); }
        static void Reject(Action action, string text)
        {
            bool rejected = false;
            try { action(); } catch (IOException) { rejected = true; } catch (System.IO.InvalidDataException) { rejected = true; }
            Check(rejected, text);
        }
        static byte[] SyntheticDll()
        {
            byte[] data = new byte[1024];
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write((ushort)0x5a4d); stream.Position = 0x3c; w.Write((uint)0x80);
                stream.Position = 0x80; w.Write((uint)0x4550); w.Write((ushort)0x8664); w.Write((ushort)1);
                stream.Position = 0x94; w.Write((ushort)240); w.Write((ushort)0x2022); w.Write((ushort)0x20b);
                stream.Position = 0x188 + 8; w.Write((uint)512); w.Write((uint)0x1000); w.Write((uint)512); w.Write((uint)512);
            }
            return data;
        }
        public static int Run(string report)
        {
            string root = Path.Combine(Path.GetTempPath(), "FramegenDllTester-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string gameDir = Path.Combine(root, "game"), package = Path.Combine(root, "package"); Directory.CreateDirectory(gameDir); Directory.CreateDirectory(package);
            string game = Path.Combine(gameDir, "DisposableFixture.exe"); File.Copy(Assembly.GetExecutingAssembly().Location, game);
            string candidate = Path.Combine(package, "candidate.dll"); File.WriteAllBytes(candidate, SyntheticDll());
            string target = Path.Combine(gameDir, "version.dll"), config = Path.Combine(gameDir, "test.ini"), sourceConfig = Path.Combine(package, "test.ini");
            File.WriteAllText(target, "original bytes - do not lose"); File.WriteAllText(config, "original=true"); File.WriteAllText(sourceConfig, "candidate=true");
            string original = Disk.Hash(target), originalConfig = Disk.Hash(config);
            DateTime stamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc); File.SetLastWriteTimeUtc(target, stamp);
            TrialStore store = new TrialStore(Path.Combine(root, "data"));
            try
            {
                PeInfo image = PeInspector.Inspect(candidate); Check(image.IsDll && !image.Managed && image.Architecture == "x64", "Static PE inspection accepts the native x64 fixture without loading it");
                string bad = Path.Combine(package, "bad.dll"); File.WriteAllText(bad, "not a DLL"); Reject(delegate { PeInspector.Inspect(bad); }, "Malformed DLL is rejected");
                Reject(delegate { store.Prepare(game, candidate, "..\\version.dll", new string[0], false, "", false, 60); }, "Path traversal is rejected before staging");
                Reject(delegate { store.Prepare(game, candidate, "version.exe", new string[0], false, "", false, 60); }, "Executable replacement is rejected");
                Trial t = store.Prepare(game, candidate, "version.dll", new[] { sourceConfig }, false, "", false, 60);
                Check(Disk.Hash(target) == original && File.Exists(Path.Combine(t.Folder, "0.original")), "Verified backup exists before any game-file change");
                store.Stage(t); Check(Disk.Hash(target) == image.Sha256 && Disk.Hash(config) == Disk.Hash(sourceConfig), "DLL and companion config are staged");
                Reject(delegate { store.Prepare(game, candidate, "version.dll", new string[0], false, "", false, 60); }, "A pending trial blocks another test");
                File.AppendAllText(config, "\r\nchanged-at-runtime=true");
                TrialStore recoveredStore = new TrialStore(store.Root); Trial recovered = recoveredStore.ReadAll().Single(); recoveredStore.Restore(recovered);
                Check(Disk.Hash(target) == original && Disk.Hash(config) == originalConfig, "A new store instance recovers interrupted trials byte-for-byte");
                Check(File.GetLastWriteTimeUtc(target) == stamp, "Original modification time is restored");
                Check(Directory.GetFiles(t.Folder, "*.changed-*").Length == 1, "Runtime config changes are preserved separately");
                recoveredStore.Restore(recovered); Check(recovered.State == "Restored", "Restoration is idempotent");
                Trial absent = store.Prepare(game, candidate, "winhttp.dll", new string[0], false, "", false, 60); store.Stage(absent); store.Restore(absent);
                Check(!File.Exists(Path.Combine(gameDir, "winhttp.dll")), "A target absent before the test is absent after restoration");
                Trial baseline = store.Prepare(game, "", "", new string[0], true, "", false, 60); store.Stage(baseline); store.Restore(baseline);
                Check(baseline.Files.Count == 0 && Disk.Hash(target) == original, "Baseline changes no game files");
                Trial failed = store.Prepare(game, candidate, "version.dll", new string[0], false, "", false, 60);
                File.AppendAllText(candidate, "changed"); Reject(delegate { store.Stage(failed); }, "Candidate mutation is caught before staging");
                Check(Disk.Hash(target) == original, "A rejected candidate leaves the original untouched"); store.Restore(failed); File.WriteAllBytes(candidate, SyntheticDll());
                Trial corrupt = store.Prepare(game, candidate, "version.dll", new string[0], false, "", false, 60); store.Stage(corrupt);
                string backup = Path.Combine(corrupt.Folder, "0.original"); byte[] saved = File.ReadAllBytes(backup); File.AppendAllText(backup, "corrupt");
                Reject(delegate { store.Restore(corrupt); }, "A corrupted original backup blocks restoration");
                Check(Disk.Hash(target) == image.Sha256 && corrupt.State != "Restored", "Backup failure never reports successful restoration");
                File.WriteAllBytes(backup, saved); store.Restore(corrupt);
                Trial partial = store.Prepare(game, candidate, "version.dll", new[] { sourceConfig }, false, "", false, 60);
                using (FileStream locked = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Reject(delegate { store.Stage(partial); }, "A locked companion interrupts a multi-file stage");
                    Check(Disk.Hash(target) == original && Disk.Hash(config) == originalConfig, "Partial-stage failure rolls back earlier files and preserves the locked original");
                }
                store.Restore(partial); Check(partial.State == "Restored", "Recovery completes after the external file lock is released");
                TestSession session = new TestSession(store);
                session.Start(game, candidate, "version.dll", new string[0], false, "--fixture", true, 60);
                Process process = null;
                for (int i = 0; i < 60; ++i)
                {
                    List<Process> found = TrialStore.FindGame(game);
                    if (found.Count > 0)
                    {
                        process = found[0]; foreach (Process other in found.Skip(1)) other.Dispose();
                        process.Refresh(); if (process.MainWindowHandle != IntPtr.Zero) break; process.Dispose(); process = null;
                    }
                    Thread.Sleep(100);
                }
                Check(process != null, "Direct launch opens the disposable fixture window");
                if (process != null) process.Dispose();
                session.Poll(); Reject(delegate { store.Restore(session.Current); }, "Running-game guard prevents file restoration while the process is alive");
                Trial finished = session.Finish("Automated fixture test").GetAwaiter().GetResult();
                Check(finished.State == "Restored" && TrialStore.FindGame(game).Count == 0, "Finish gracefully closes the fixture and verifies it exited");
                Check(Disk.Hash(target) == original, "End-to-end session restores the original DLL bytes");
                session.Start(game, candidate, "version.dll", new string[0], false, "", false, 60);
                session.ArmedUtc = DateTime.UtcNow.AddSeconds(-61);
                Check(session.Poll() == "timeout", "A manual launch that never starts reaches the bounded timeout");
                Trial timed = session.Finish("Waiting timeout").GetAwaiter().GetResult();
                Check(timed.State == "Restored" && Disk.Hash(target) == original, "Timeout recovery restores staged files even when no game was launched");
                Trial manual = store.Prepare(game, candidate, "version.dll", new string[0], false, "", false, 60); store.Stage(manual); store.Restore(manual);
                manual.Outcome = "Black screen"; manual.Notes = "comma, quote \" and\r\nnewline"; store.Save(manual);
                string csv = Path.Combine(root, "report.csv"); store.Export(csv); Check(File.ReadAllText(csv).Contains("quote \"\""), "CSV export escapes quoted multiline notes");
                WizardTests.Run(root, game, candidate, Check);
                SteamSetupTests.Run(root, game, candidate, Check);
                LauncherTests.Run(root, game, candidate, Check);
                GodotTests.Run(root, candidate, Check);
                OverlayTests.Run(Check);
                File.WriteAllText(report, "Framegen DLL Tester 0.3.7 - local automated verification\r\n" + DateTime.UtcNow.ToString("o") + "\r\n" + String.Join("\r\n", passed.ToArray()) +
                    "\r\n\r\n" + passed.Count + " checks passed.\r\nNo real game or third-party DLL was executed. Real game compatibility and frame generation remain unverified.\r\nFixture and recovery data retained at: " + root);
                return 0;
            }
            catch
            {
                File.WriteAllText(Path.Combine(gameDir, "fixture-allow-close.txt"), "allow cleanup");
                foreach (Process p in TrialStore.FindGame(game))
                {
                    try
                    {
                        for (int retry = 0; retry < 30 && !p.HasExited; ++retry) { p.CloseMainWindow(); p.WaitForExit(100); }
                    }
                    finally { p.Dispose(); }
                }
                throw;
            }
        }
    }
}
