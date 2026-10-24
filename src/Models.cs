// C# 5 · Models.cs · Framegen DLL Tester · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace FramegenLab
{
    [DataContract] public sealed class FileEntry
    {
        [DataMember] public string Source;
        [DataMember] public string Name;
        [DataMember] public string CandidateHash;
        [DataMember] public string OriginalHash;
        [DataMember] public bool Existed;
        [DataMember] public int Attributes;
        [DataMember] public long WriteTicks;
        [DataMember] public bool Restored;
        [DataMember] public bool RemovedDuringTrial;
    }
    [DataContract] public sealed class Trial
    {
        [DataMember] public string Id;
        [DataMember] public string Game;
        [DataMember] public string GameHash;
        [DataMember] public string Candidate;
        [DataMember] public string TargetName;
        [DataMember] public string State;
        [DataMember] public string StartedUtc;
        [DataMember] public string EndedUtc;
        [DataMember] public string Outcome;
        [DataMember] public string Notes;
        [DataMember] public string LaunchMode;
        [DataMember] public string Arguments;
        [DataMember] public int LimitSeconds;
        [DataMember] public int Pid;
        [DataMember] public long ProcessStartTicks;
        [DataMember] public string ExitCode;
        [DataMember] public string LauncherExitCode;
        [DataMember] public string GraphicsModules;
        [DataMember] public string ModuleObservation;
        [DataMember] public string BackendObservation;
        [DataMember] public string BaseFps;
        [DataMember] public string DisplayedFps;
        [DataMember] public Dictionary<string, string> LaunchEnvironment;
        [DataMember] public bool Baseline;
        [DataMember] public bool PersistentSteam;
        [DataMember] public bool ExternalLayer;
        [DataMember] public string VulkanLayer;
        [IgnoreDataMember] public string RuntimeLayer { get { return String.IsNullOrEmpty(VulkanLayer) ? Candidate : VulkanLayer; } }
        [DataMember] public string LaunchExecutable;
        [DataMember] public string LaunchHash;
        [DataMember] public List<FileEntry> Files = new List<FileEntry>();
        [IgnoreDataMember] public string Folder;
    }
    [DataContract] public sealed class Preferences
    {
        [DataMember] public string Game = "";
        [DataMember] public string Candidate = "";
        [DataMember] public string Target = "version.dll";
        [DataMember] public string Arguments = "";
        [DataMember] public int LaunchMode;
        [DataMember] public int LimitSeconds = 180;
    }
    public static class Disk
    {
        public static string DefaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Framegen DLL Tester");
        public static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }
        public static string Encode<T>(T data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(ms, data);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
        public static T Read<T>(string path)
        {
            using (FileStream fs = File.OpenRead(path))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(fs);
        }
        public static void Write<T>(string path, T data) { AtomicBytes(path, Encoding.UTF8.GetBytes(Encode(data))); }
        public static void AtomicBytes(string destination, byte[] data)
        {
            string temporary = destination + ".fgt-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream fs = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { fs.Write(data, 0, data.Length); fs.Flush(true); }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void AtomicCopy(string source, string destination)
        {
            string temporary = destination + ".fgt-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream input = File.OpenRead(source))
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { input.CopyTo(output); output.Flush(true); }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void PlainName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(":"))
                throw new IOException("Use a file name only, without folders or drive letters.");
        }
        public static void NoLinks(string path)
        {
            string p = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(p))
            {
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic links and junctions are not supported: " + p);
                p = Path.GetDirectoryName(p);
            }
        }
        public static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
    }
}
