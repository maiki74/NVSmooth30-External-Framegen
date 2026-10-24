// C# 5 · SteamLaunch.cs · installed Steam game context · Windows x64 · .NET Framework 4.8
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace FramegenLab
{
    public sealed class SteamGameContext
    {
        public string AppId;
        public string Manifest;
    }
    public static class SteamLaunch
    {
        static string Value(string text, string key)
        {
            Match match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        public static SteamGameContext Resolve(string game)
        {
            string full = Path.GetFullPath(game);
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(full));
            while (directory != null)
            {
                if (String.Equals(directory.Name, "common", StringComparison.OrdinalIgnoreCase) && directory.Parent != null && String.Equals(directory.Parent.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string path in Directory.GetFiles(directory.Parent.FullName, "appmanifest_*.acf"))
                    {
                        Disk.NoLinks(path); string text = File.ReadAllText(path);
                        string id = Value(text, "appid"), installed = Value(text, "installdir"); uint parsed;
                        if (!UInt32.TryParse(id, out parsed) || parsed == 0 || String.IsNullOrWhiteSpace(installed) || installed != Path.GetFileName(installed)) continue;
                        if (!String.Equals(Path.GetFileName(path), "appmanifest_" + id + ".acf", StringComparison.OrdinalIgnoreCase)) continue;
                        string gameRoot = Path.Combine(directory.FullName, installed).TrimEnd('\\') + "\\";
                        if (full.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) return new SteamGameContext { AppId = id, Manifest = path };
                    }
                    return null;
                }
                directory = directory.Parent;
            }
            return null;
        }
        public static SteamGameContext Apply(ProcessStartInfo start)
        {
            // Never inherit a different game's identity from a launcher that started this app.
            start.EnvironmentVariables.Remove("SteamAppId"); start.EnvironmentVariables.Remove("SteamGameId"); start.EnvironmentVariables.Remove("SteamOverlayGameId");
            SteamGameContext context = Resolve(start.FileName);
            if (context != null)
            {
                start.EnvironmentVariables["SteamAppId"] = context.AppId;
                start.EnvironmentVariables["SteamGameId"] = context.AppId;
            }
            return context;
        }
        static bool ClientRunning()
        {
            Process[] clients = Process.GetProcessesByName("steam"); bool running = false;
            foreach (Process p in clients) { try { if (!p.HasExited) running = true; } finally { p.Dispose(); } }
            return running;
        }
        public static async Task EnsureClient(string game)
        {
            if (Resolve(game) == null || ClientRunning()) return;
            string steam = null;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (key != null) steam = key.GetValue("SteamExe") as string;
            if (String.IsNullOrEmpty(steam) || !File.Exists(steam)) throw new IOException("Steam was not found. Open Steam and sign in, then click Start game again.");
            using (Process launched = Process.Start(new ProcessStartInfo(steam, "-silent") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })) { }
            for (int i = 0; i < 20; ++i) { await Task.Delay(500); if (ClientRunning()) return; }
            throw new IOException("Steam is still starting. Finish its login or update, then click Start game again.");
        }
    }
}
