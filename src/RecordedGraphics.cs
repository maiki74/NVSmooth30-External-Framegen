// C# 5 · RecordedGraphics.cs · read recent Unreal renderer evidence · Windows x64 · .NET Framework 4.8
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;

namespace FramegenLab
{
    public static class RecordedGraphics
    {
        public static GraphicsDetection ForGame(string renderer)
        {
            string bin = Path.GetDirectoryName(renderer);
            if (!String.Equals(Path.GetFileName(bin), "Win64", StringComparison.OrdinalIgnoreCase)) return null;
            string binaries = Path.GetDirectoryName(bin);
            if (!String.Equals(Path.GetFileName(binaries), "Binaries", StringComparison.OrdinalIgnoreCase)) return null;
            string project = Path.GetDirectoryName(binaries);
            GraphicsDetection crash = Read(renderer, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetFileName(project), "Saved", "Crashes"));
            if (crash != null) return crash;
            return ReadLegacy(renderer, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", Path.GetFileName(project)));
        }
        public static GraphicsDetection ReadLegacy(string renderer, string projectLogs)
        {
            try
            {
                if (!Directory.Exists(projectLogs)) return null;
                Disk.NoLinks(projectLogs);
                var logs = new DirectoryInfo(projectLogs).EnumerateDirectories().Take(64)
                    .Select(dir => new DirectoryInfo(Path.Combine(dir.FullName, "Logs"))).Where(dir => dir.Exists)
                    .SelectMany(dir => dir.EnumerateFiles("Launch*.log").Take(64))
                    .OrderByDescending(file => file.LastWriteTimeUtc).Take(8);
                foreach (FileInfo file in logs)
                {
                    if (file.Length > 2097152 || file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7) ||
                        file.LastWriteTimeUtc < File.GetLastWriteTimeUtc(renderer) || file.LastWriteTimeUtc > DateTime.UtcNow.AddMinutes(5)) continue;
                    Disk.NoLinks(file.FullName);
                    string text;
                    using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
                    Match dir = Regex.Match(text, @"(?m)^.*Init: Base directory:\s*([^\r\n]+)");
                    Match exe = Regex.Match(text, @"(?m)^.*Init: Executable:\s*([^\r\n]+)");
                    if (!dir.Success || !exe.Success) continue;
                    string name = exe.Groups[1].Value.Trim();
                    string expected = Path.GetFullPath(Path.Combine(dir.Groups[1].Value.Trim(), name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe"));
                    if (!String.Equals(expected, Path.GetFullPath(renderer), StringComparison.OrdinalIgnoreCase)) continue;
                    Match api = Regex.Match(text, @"Log: Using (D3D9|D3D11|D3D12|Vulkan) device\b", RegexOptions.IgnoreCase);
                    if (!api.Success) continue;
                    string mode = api.Groups[1].Value.ToLowerInvariant().Replace("d3d", "dx");
                    return new GraphicsDetection { Api = mode, Evidence = "Last recorded run: " + GraphicsApi.Label(mode) + " (matched game log, " + file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm 'UTC'") + "). Next launch may differ." };
                }
            }
            catch (Exception e)
            {
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is ArgumentException)) throw;
            }
            return null;
        }
        public static GraphicsDetection Read(string renderer, string crashes)
        {
            try
            {
                if (!Directory.Exists(crashes)) return null;
                Disk.NoLinks(crashes);
                var records = new DirectoryInfo(crashes).EnumerateDirectories().Take(512)
                    .Select(dir => new FileInfo(Path.Combine(dir.FullName, "CrashContext.runtime-xml")))
                    .Where(file => file.Exists).OrderByDescending(file => file.LastWriteTimeUtc).Take(8);
                foreach (FileInfo record in records)
                {
                    if (record.Length > 2097152 || record.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7) ||
                        record.LastWriteTimeUtc < File.GetLastWriteTimeUtc(renderer) || record.LastWriteTimeUtc > DateTime.UtcNow.AddMinutes(5)) continue;
                    Disk.NoLinks(record.FullName);
                    XmlDocument doc = new XmlDocument { XmlResolver = null };
                    using (XmlReader reader = XmlReader.Create(record.FullName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2097152 })) doc.Load(reader);
                    string dir = Value(doc, "/FGenericCrashContext/RuntimeProperties/BaseDir");
                    string name = Value(doc, "/FGenericCrashContext/RuntimeProperties/ExecutableName");
                    string api = Value(doc, "/FGenericCrashContext/EngineData/RHI.RHIName");
                    if (String.IsNullOrEmpty(dir) || String.IsNullOrEmpty(name)) continue;
                    string expected = Path.GetFullPath(Path.Combine(dir, name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe"));
                    if (!String.Equals(expected, renderer, StringComparison.OrdinalIgnoreCase)) continue;
                    string mode = String.Equals(api, "D3D11", StringComparison.OrdinalIgnoreCase) ? "dx11" :
                        String.Equals(api, "D3D12", StringComparison.OrdinalIgnoreCase) ? "dx12" :
                        String.Equals(api, "Vulkan", StringComparison.OrdinalIgnoreCase) ? "vulkan" : null;
                    if (mode != null) return new GraphicsDetection { Api = mode, Evidence = "Last recorded run used " + GraphicsApi.Label(mode) + " (matched renderer, " + record.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm 'UTC'") + "). Next launch may differ." };
                }
            }
            catch (Exception e)
            {
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is XmlException) && !(e is ArgumentException)) throw;
                // Optional local evidence must never prevent selecting or restoring a game.
            }
            return null;
        }
        static string Value(XmlDocument doc, string xpath)
        { XmlNode node = doc.SelectSingleNode(xpath); return node == null ? null : node.InnerText.Trim(); }
    }
}
