// C# 5 · Dx9Runtime.cs · reversible DX9 translation · Windows x64 · .NET Framework 4.8
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace FramegenLab
{
    public static class Dx9Runtime
    {
        public const string Profile = "DX9-VULKAN";
        public const string Hash = "c126316f7478eaff1ad8087702eda5930f2400187b9c8b3592219c34f6632780";
        public static Dictionary<string, string> EnvironmentFor(string layer, string trialFolder)
        {
            var values = VulkanRuntime.EnvironmentFor(layer);
            values["DXVK_LOG_PATH"] = trialFolder;
            // The named DX9 fixture reproduces a device-chain failure with RTSS's
            // Vulkan bootstrap. Opt out for this child only; no RTSS settings change.
            values["DISABLE_RTSS_LAYER"] = "1";
            return values;
        }
        public static PreparedCandidate Prepare(string root)
        {
            PreparedCandidate vk = VulkanRuntime.Prepare(root);
            string folder = Path.Combine(root, "dxvk-3.1.1-x64");
            Directory.CreateDirectory(folder); Disk.NoLinks(folder);
            string dll = Path.Combine(folder, "d3d9.dll"); Disk.NoLinks(dll);
            if (!File.Exists(dll) || Disk.Hash(dll) != Hash)
            {
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("FramegenLab.Dxvk9"))
                using (MemoryStream data = new MemoryStream())
                {
                    if (resource == null) throw new IOException("The embedded DXVK DirectX 9 runtime is missing.");
                    resource.CopyTo(data); Disk.AtomicBytes(dll, data.ToArray());
                }
            }
            PeInfo image = PeInspector.Inspect(dll);
            if (Disk.Hash(dll) != Hash || !image.IsDll || image.Managed || image.Architecture != "x64" || !image.Exports.Contains("Direct3DCreate9"))
                throw new IOException("DXVK DirectX 9 verification failed.");
            return new PreparedCandidate { Dll = dll, Target = "d3d9.dll", Companions = new string[0], Profile = Profile,
                VulkanLayer = vk.Dll, Label = "DirectX 9 -> DXVK -> NVSmooth30 Vulkan (experimental)" };
        }
    }
}
