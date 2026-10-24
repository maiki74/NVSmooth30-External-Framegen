// C# 5 · BundledCatalog.cs · embedded local test assets · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FramegenLab
{
    [DataContract] public sealed class BundleCandidate
    {
        [DataMember] public string Id;
        [DataMember] public string Label;
        [DataMember] public string Dll;
        [DataMember] public string Target;
        [DataMember] public string Config;
        [DataMember] public string Profile;
        [DataMember] public string Family;
        [DataMember] public string[] ExtraFiles;
    }
    [DataContract] public sealed class BundleFile
    {
        [DataMember] public string Key;
        [DataMember] public string Sha256;
        [DataMember] public long Length;
        [DataMember] public string Origin;
    }
    [DataContract] public sealed class BundleManifest
    {
        [DataMember] public string Version;
        [DataMember] public bool PrivateOnly;
        [DataMember] public List<BundleCandidate> Candidates;
        [DataMember] public List<BundleFile> Files;
    }
    public sealed class PreparedCandidate
    {
        public string Dll;
        public string Target;
        public string[] Companions;
        public string Profile;
        public string Label;
        public string VulkanLayer;
    }
    public interface ICandidateCatalog
    {
        int Count { get; }
        string Label(int index);
        PreparedCandidate Prepare(int index);
    }
    public interface IConflictCatalog
    {
        string[] InactiveKnownProxies(string game, string target);
    }
    public interface IGameCatalog
    {
        string ConfigureGame(string path, string graphicsApi = "auto");
        string GraphicsSummary { get; }
        string UnavailableReason { get; }
    }
    public sealed class BundledCatalog : ICandidateCatalog, IConflictCatalog, IGameCatalog
    {
        public readonly BundleManifest Manifest;
        readonly string cache;
        readonly object gate = new object();
        byte[][] blocks;
        readonly Dictionary<string, int[]> refs = new Dictionary<string, int[]>();
        List<BundleCandidate> active;
        public int Count { get { return active.Count; } }
        public bool VulkanActive {get{return active.Count==1&&(active[0].Profile==VulkanRuntime.Profile||active[0].Profile==Dx9Runtime.Profile);}}
        public bool TranslatedDx9 {get{return active.Count==1&&active[0].Profile==Dx9Runtime.Profile;}}
        public string GraphicsSummary { get; private set; }
        public string UnavailableReason { get; private set; }
        public string Label(int index) { return active[index].Label; }
        public BundledCatalog(string dataRoot)
        {
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("FramegenLab.BundleManifest"))
            {
                if (source == null) throw new IOException("The bundled DLL resource is missing. Rebuild with the local payload.");
                Manifest = (BundleManifest)new DataContractJsonSerializer(typeof(BundleManifest)).ReadObject(source);
            }
            active = new List<BundleCandidate>(Manifest.Candidates);
            cache = Path.Combine(dataRoot, "bundled", Manifest.Version); Directory.CreateDirectory(cache); Disk.NoLinks(cache);
        }
        public string ConfigureGame(string path, string graphicsApi = "auto")
        {
            string mode = GraphicsApi.Normalize(graphicsApi);
            GraphicsDetection detected = new GraphicsDetection();
            PeInfo image = null;
            if (!String.IsNullOrEmpty(path) && File.Exists(path))
            {
                image = PeInspector.Inspect(path); detected = GraphicsApi.Detect(image);
                GraphicsDetection recorded = RecordedGraphics.ForGame(path);
                if (recorded == null) recorded = GodotMetadata.ForGame(path, image);
                if (recorded != null) detected = recorded;
            }
            string selected = mode == "auto" ? detected.Api : mode;
            bool godotFallback = selected == "unknown" && image != null && image.GodotVersion != null;
            if (godotFallback) selected = "vulkan"; // Engine capability filter, not a runtime API claim.
            List<BundleCandidate> choices = new List<BundleCandidate>(Manifest.Candidates);
            if (selected == "dx11") choices = choices.Where(c => c.Family == "NVSmooth30-ASILoader")
                .OrderByDescending(c => image != null && image.Imports.Contains(c.Target, StringComparer.OrdinalIgnoreCase)).ToList();
            else if (selected == "dx12") choices = choices.Where(c => c.Family != "NVSmooth30-ASILoader").ToList();
            else if (selected == "vulkan") choices = new List<BundleCandidate> { new BundleCandidate { Id="VULKAN-v2",Label="NVSmooth30 - Vulkan v2 (process-local; experimental)",Dll="FramegenVulkan.dll",Target="FramegenVulkan.dll",Profile=VulkanRuntime.Profile,Family="Vulkan" } };
            else if (selected == "dx9") choices = new List<BundleCandidate> { new BundleCandidate { Id="DX9-DXVK-3.1.1-Vulkan-v2",Label="DirectX 9 -> DXVK -> NVSmooth30 Vulkan (experimental)",Dll="d3d9.dll",Target="d3d9.dll",Profile=Dx9Runtime.Profile,Family="DX9-Vulkan" } };
            else if (selected == "dx10" || selected == "opengl") choices.Clear();
            UnavailableReason = choices.Count == 0 ? "No bundled frame-generation backend supports " + GraphicsApi.Label(selected) + "." : null;
            GraphicsSummary = (mode == "auto" ? "Automatic: " : "Manual: " + GraphicsApi.Label(mode) + ". ") + detected.Evidence;
            if (godotFallback) GraphicsSummary += " Only the experimental Vulkan test is offered for this engine; no DirectX proxies are staged.";
            active = choices;
            return (mode == "auto" ? "" : mode + "|") + String.Join("|", active.Select(c => c.Id).ToArray());
        }
        void ReadPack()
        {
            if (blocks != null) return;
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("FramegenLab.Payload"))
            using (GZipStream gzip = new GZipStream(source, CompressionMode.Decompress))
            using (BinaryReader reader = new BinaryReader(gzip, Encoding.UTF8))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "FGTPACK2") throw new InvalidDataException("Unknown bundled payload.");
                int size = reader.ReadInt32(), count = reader.ReadInt32();
                if (size != 512 || count < 1 || count > 1000000) throw new InvalidDataException("Invalid bundle block table.");
                byte[][] loaded = new byte[count][];
                for (int i = 0; i < count; ++i) { loaded[i] = reader.ReadBytes(size); if (loaded[i].Length != size) throw new EndOfStreamException(); }
                int fileCount = reader.ReadInt32(); if (fileCount != Manifest.Files.Count) throw new InvalidDataException("Bundle manifest count mismatch.");
                Dictionary<string, int[]> loadedRefs = new Dictionary<string, int[]>();
                for (int i = 0; i < fileCount; ++i)
                {
                    int keyLength = reader.ReadInt32(); if (keyLength < 1 || keyLength > 200) throw new InvalidDataException("Invalid bundle key.");
                    string key = Encoding.UTF8.GetString(reader.ReadBytes(keyLength)); Disk.PlainName(key);
                    long length = reader.ReadInt64(); int chunks = reader.ReadInt32();
                    BundleFile file = Manifest.Files.SingleOrDefault(f => f.Key == key);
                    if (file == null || length != file.Length || chunks != (length + 511) / 512) throw new InvalidDataException("Bundle file length mismatch.");
                    int[] map = new int[chunks]; for (int n = 0; n < chunks; ++n) { map[n] = reader.ReadInt32(); if (map[n] < 0 || map[n] >= count) throw new InvalidDataException("Invalid bundle block index."); }
                    loadedRefs.Add(key, map);
                }
                foreach (KeyValuePair<string, int[]> entry in loadedRefs) refs.Add(entry.Key, entry.Value);
                blocks = loaded;
            }
        }
        string Extract(string key)
        {
            Disk.PlainName(key); BundleFile file = Manifest.Files.Single(f => f.Key == key);
            string destination = Path.Combine(cache, key); Disk.NoLinks(destination);
            if (File.Exists(destination) && new FileInfo(destination).Length == file.Length && Disk.Hash(destination) == file.Sha256) return destination;
            ReadPack();
            using (MemoryStream output = new MemoryStream((int)file.Length))
            {
                long left = file.Length;
                foreach (int index in refs[key]) { int take = (int)Math.Min(left, 512); output.Write(blocks[index], 0, take); left -= take; }
                Disk.AtomicBytes(destination, output.ToArray());
            }
            if (Disk.Hash(destination) != file.Sha256) throw new IOException("Bundled file verification failed: " + key);
            return destination;
        }
        public PreparedCandidate Prepare(int index)
        {
            lock (gate)
            {
                BundleCandidate candidate = active[index];
                if(candidate.Family=="Vulkan")return VulkanRuntime.Prepare(cache);
                if(candidate.Family=="DX9-Vulkan")return Dx9Runtime.Prepare(cache);
                string dll = Extract(candidate.Dll); PeInfo image = PeInspector.Inspect(dll);
                if (!image.IsDll || image.Managed || image.Architecture != "x64") throw new IOException("The bundled candidate is not a native x64 DLL.");
                List<string> companions = new List<string>();
                if (!String.IsNullOrEmpty(candidate.Config))
                {
                    string source = Extract(candidate.Config);
                    // Use the package's actual configuration file name at the staging boundary.
                    companions.Add(source);
                }
                if (candidate.ExtraFiles != null) foreach (string key in candidate.ExtraFiles) companions.Add(Extract(key));
                return new PreparedCandidate { Dll = dll, Target = candidate.Target, Companions = companions.ToArray(), Profile = candidate.Profile, Label = candidate.Label };
            }
        }
        public PreparedCandidate PrepareSteam(int index, string game)
        {
            PreparedCandidate normal = Prepare(index);
            if(normal.Profile==VulkanRuntime.Profile || normal.Profile==Dx9Runtime.Profile)throw new IOException("Vulkan uses the app's Start game button and process-local settings. Permanent normal-launch installation is not available for this integration.");
            if (normal.Profile == "DLSSG") return normal; // This family uses its existing INI, not SM86 variables.
            lock (gate)
            {
                BundleCandidate candidate = active[index];
                string original = candidate.Family == "NVSmooth30-ASILoader" ? Extract("FramegenLab.NVSmooth30.asi") : normal.Dll;
                string loader = Extract("ultimate-asi-loader.dll");
                string generated = Path.Combine(cache, "steam-" + Disk.Hash(game).Substring(0, 12) + "-" + candidate.Id);
                Directory.CreateDirectory(generated); Disk.NoLinks(generated);
                string coreName = "FramegenLab.SteamBackend.dll", bootstrapName = "FramegenLab.Steam.asi";
                string core = Path.Combine(generated, coreName), bootstrap = Path.Combine(generated, bootstrapName);
                Disk.AtomicCopy(original, core);
                if (Disk.Hash(core) != Disk.Hash(original)) throw new IOException("Steam backend copy verification failed.");
                Disk.AtomicBytes(bootstrap, SteamBootstrap.Build(Path.Combine(Path.GetDirectoryName(game), coreName), LaunchProfiles.Values(normal.Profile)));
                PeInfo boot = PeInspector.Inspect(bootstrap);
                if (!boot.IsDll || boot.Architecture != "x64" || !boot.Exports.Contains("InitializeASI")) throw new IOException("Steam configuration ASI is invalid.");
                string config = Path.Combine(generated, Path.GetFileNameWithoutExtension(normal.Target) + ".ini");
                File.WriteAllText(config, "[GlobalSets]\r\nLoadPlugins=1\r\nLoadFromScriptsOnly=1\r\nLoadRecursively=0\r\nLoadExtraPlugins=" + bootstrapName + "\r\nDontLoadFromDllMain=1\r\nDebugLog=1\r\n", Encoding.ASCII);
                return new PreparedCandidate { Dll = loader, Target = normal.Target, Companions = new[] { core, bootstrap, config }, Profile = normal.Profile, Label = normal.Label };
            }
        }
        public string[] InactiveKnownProxies(string game, string target)
        {
            HashSet<string> known = new HashSet<string>(Manifest.Files.Where(f => f.Key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(f => f.Sha256));
            List<string> inactive = new List<string>();
            foreach (string name in Manifest.Candidates.Select(c => c.Target).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (String.Equals(name, target, StringComparison.OrdinalIgnoreCase)) continue;
                string path = Path.Combine(Path.GetDirectoryName(game), name);
                if (!File.Exists(path)) continue;
                Disk.NoLinks(path); if (known.Contains(Disk.Hash(path))) inactive.Add(name);
            }
            return inactive.ToArray();
        }
    }
    public static class LaunchProfiles
    {
        public static readonly string[] Keys = { "SM86_ENABLE_OSD", "SM86_ENABLE_D3D11_BRIDGE", "SM86_DIAGNOSTICS", "SM86_FORCE_VSYNC", "SM86_LOW_LATENCY", "SM86_BASE_FPS_CAP", "SM86_HALF_REFRESH_CAP", "SM86_VK_RELAX_VSYNC" };
        public static Dictionary<string, string> Values(string profile)
        {
            if(profile==VulkanRuntime.Profile)return new Dictionary<string,string> { {Keys[0],"0"},{Keys[1],"0"},{Keys[2],"1"},{Keys[3],"0"},{Keys[4],"0"},{Keys[5],"0"},{Keys[6],"0"},{Keys[7],"1"} };
            if (profile == "RE4-D3D12") return new Dictionary<string, string> {
                { Keys[0], "0" }, { Keys[1], "0" }, { Keys[2], "1" }, { Keys[3], "0" }, { Keys[4], "0" }, { Keys[5], "0" }, { Keys[6], "0" } };
            if (profile == "DIAGNOSTIC") return new Dictionary<string, string> {
                { Keys[0], "1" }, { Keys[2], "1" }, { Keys[3], "0" }, { Keys[4], "0" }, { Keys[5], "0" }, { Keys[6], "0" } };
            if (profile == "DLSSG") return new Dictionary<string, string> { { Keys[3], "0" }, { Keys[5], "0" }, { Keys[6], "0" } };
            if (profile == "D3D11-ASI") return new Dictionary<string, string> {
                { Keys[0], "1" }, { Keys[1], "1" }, { Keys[2], "1" }, { Keys[3], "0" }, { Keys[4], "0" }, { Keys[5], "0" }, { Keys[6], "0" } };
            throw new IOException("Unknown launch configuration: " + profile);
        }
    }
}
