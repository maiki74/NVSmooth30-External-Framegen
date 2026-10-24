// C# 5 · VulkanRuntime.cs · process-local Vulkan integration · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace FramegenLab
{
    public static class VulkanRuntime
    {
        public const string Profile = "VULKAN";
        public const string LayerName = "VK_LAYER_FRAMEGEN_nvsmooth30";
        public const string LayerHash = "6899b3862bd7c9dff129c803867ca3f5988ad5bbc4eef92a54286c6ebe2e4ca8";
        public const string BackendHash = "0bfdead0c48c00c8732a757703d27e6788c08bc71fe817d6a7630987838aa8dc";
        public static PreparedCandidate Prepare(string root)
        {
            string folder = Path.Combine(root, "vulkan-v2");Directory.CreateDirectory(folder);Disk.NoLinks(folder);
            string layer = Extract(folder, "FramegenVulkan.dll", "FramegenLab.VulkanLayer", LayerHash);
            Extract(folder, "NVSmoothBackend.dll", "FramegenLab.VulkanBackend", BackendHash);
            string manifest = "{\"file_format_version\":\"1.2.0\",\"layer\":{\"name\":\"" + LayerName + "\",\"type\":\"GLOBAL\",\"library_path\":\".\\\\FramegenVulkan.dll\",\"api_version\":\"1.3.0\",\"implementation_version\":\"2\",\"description\":\"Experimental process-local NVSmooth30 Vulkan integration\"}}";
            string manifestPath=Path.Combine(folder,"FramegenVulkan.json");Disk.NoLinks(manifestPath);
            if(!File.Exists(manifestPath)||File.ReadAllText(manifestPath)!=manifest) Disk.AtomicBytes(manifestPath,System.Text.Encoding.UTF8.GetBytes(manifest));
            return new PreparedCandidate { Dll=layer,Target="FramegenVulkan.dll",Companions=new string[0],Profile=Profile,Label="NVSmooth30 - Vulkan v2 (process-local; experimental)" };
        }
        static string Extract(string folder,string name,string resource,string expected)
        {
            string target=Path.Combine(folder,name);Disk.NoLinks(target);
            if(File.Exists(target)&&Disk.Hash(target)==expected)return target;
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if(stream==null)throw new IOException("The embedded Vulkan runtime is missing.");
                using(MemoryStream bytes=new MemoryStream()){stream.CopyTo(bytes);Disk.AtomicBytes(target,bytes.ToArray());}
            }
            if(Disk.Hash(target)!=expected)throw new IOException("Vulkan runtime verification failed: "+name);
            return target;
        }
        public static void RequireSupportedDriver()
        {
            string folder=Path.Combine(Environment.SystemDirectory,@"DriverStore\FileRepository\nvmdi.inf_amd64_8de67f3813b9d47d");
            RequireHash(Path.Combine(folder,"NvPresent64.dll"),"08357a3f054aa8c2eea251e63330e80b55006a09240cf573c74d927622eb4a34");
            RequireHash(Path.Combine(folder,"nvoglv64.dll"),"adda6dc8054888d5c52e3f97cf33b742952d16b3f145ced74cfb1012556bd696");
        }
        static void RequireHash(string path,string expected)
        {
            if(!File.Exists(path)||Disk.Hash(path)!=expected)
                throw new IOException("This experimental Vulkan integration requires the exact NVIDIA driver build used in the local tests. The installed file changed: "+Path.GetFileName(path)+". No game was launched or driver setting changed.");
        }
        public static string Renderer(string selected)
        {
            string full=Path.GetFullPath(selected),name=Path.GetFileName(full);
            if(new[]{"Play SpartanPort.exe","Play God of War III.exe","SpartanPort.exe","God of War III.exe"}.Contains(name,StringComparer.OrdinalIgnoreCase))
            {
                string probe=Path.Combine(Path.GetDirectoryName(full),@"bin\spartanport-probe.exe");
                if(File.Exists(probe))return probe;
            }
            return full;
        }
        public static string Launcher(string game)
        {
            if(!String.Equals(Path.GetFileName(game),"spartanport-probe.exe",StringComparison.OrdinalIgnoreCase))return game;
            string bin=Path.GetDirectoryName(game),root=Path.GetDirectoryName(bin);
            if(!String.Equals(Path.GetFileName(bin),"bin",StringComparison.OrdinalIgnoreCase)||String.IsNullOrEmpty(root))
                throw new IOException("Choose the SpartanPort installation's renderer or normal Play launcher.");
            string launcher=Path.Combine(root,"Play SpartanPort.exe");
            RequireHash(game,"4436bc2f6012eddccc182cb569e9c5f2a9b72b77f6bc2375d982b961b6e7209c");
            RequireHash(launcher,"f955d03ff134c8b50de0a66f470b2ae00883ecad01c42543eab938235b5d7fb7");
            return launcher;
        }
        public static Dictionary<string,string> EnvironmentFor(string layer)
        {
            var values=LaunchProfiles.Values(Profile);
            string previousPath=Environment.GetEnvironmentVariable("VK_LAYER_PATH"),previousLayers=Environment.GetEnvironmentVariable("VK_INSTANCE_LAYERS");
            values["VK_LAYER_PATH"]=Path.GetDirectoryName(layer)+(String.IsNullOrEmpty(previousPath)?"":";"+previousPath);
            values["VK_INSTANCE_LAYERS"]=LayerName+(String.IsNullOrEmpty(previousLayers)?"":";"+previousLayers);
            return values;
        }
    }
}
