// C# 5 · GraphicsApi.cs · executable import hints and user override · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FramegenLab
{
    public sealed class GraphicsDetection
    {
        public string Api = "unknown";
        public string Evidence = "No graphics API found in the EXE imports; dynamic loading may hide it.";
    }
    public static class GraphicsApi
    {
        public static readonly string[] Modes = { "auto", "dx12", "dx11", "vulkan" };
        static readonly string[] RecognizedApis = { "dx12", "dx11", "dx10", "dx9", "vulkan" };
        public static string Normalize(string mode)
        {
            string value = String.IsNullOrEmpty(mode) ? "auto" : mode.ToLowerInvariant();
            if (!Modes.Contains(value)) throw new IOException("Choose Automatic, DirectX 12, DirectX 11 or Vulkan.");
            return value;
        }
        public static string Label(string api)
        { return api == "auto" ? "Automatic" : api == "vulkan" ? "Vulkan" : api == "opengl" ? "OpenGL" : api.StartsWith("dx", StringComparison.Ordinal) ? "DirectX " + api.Substring(2) : "Unknown"; }
        public static GraphicsDetection Detect(PeInfo image)
        {
            List<string> matches = new List<string>(), evidence = new List<string>();
            foreach (string mode in RecognizedApis)
            {
                string stem = mode == "vulkan" ? "vulkan-1" : "d3d" + mode.Substring(2);
                string[] names = image.Imports.Where(name => String.Equals(name, stem + ".dll", StringComparison.OrdinalIgnoreCase) ||
                    (mode == "dx10" && String.Equals(name, "d3d10_1.dll", StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (names.Length > 0) { matches.Add(mode); evidence.AddRange(names); }
            }
            if (matches.Count == 0) return new GraphicsDetection();
            string[] direct = matches.Where(mode => image.DirectImports.Any(name =>
                String.Equals(name, (mode == "vulkan" ? "vulkan-1" : "d3d" + mode.Substring(2)) + ".dll", StringComparison.OrdinalIgnoreCase) ||
                (mode == "dx10" && String.Equals(name, "d3d10_1.dll", StringComparison.OrdinalIgnoreCase)))).ToArray();
            if (matches.Count > 1 && direct.Length == 1)
                return new GraphicsDetection { Api = direct[0], Evidence = "Likely " + Label(direct[0]) + " (direct import; other APIs are delay-loaded). Import hint only; runtime may differ." };
            // Legacy D3D imports may be auxiliary. Prefer a sole modern API as a
            // labelled heuristic, while DX11 + DX12 / Vulkan remains ambiguous.
            string[] modern = matches.Where(api => api == "dx11" || api == "dx12" || api == "vulkan").ToArray();
            if (matches.Count > 1 && modern.Length == 1)
                return new GraphicsDetection { Api = modern[0], Evidence = "Likely " + Label(modern[0]) + "; legacy imports also present (" + String.Join(", ", evidence.ToArray()) + "). Import hint only; runtime may differ." };
            return new GraphicsDetection {
                Api = matches.Count == 1 ? matches[0] : "unknown",
                Evidence = matches.Count == 1 ? "Likely " + Label(matches[0]) + " (EXE import: " + String.Join(", ", evidence.ToArray()) + ")." :
                    "Multiple APIs in EXE imports: " + String.Join(", ", matches.Select(Label).ToArray()) + "; runtime choice is unknown."
            };
        }
    }
}
