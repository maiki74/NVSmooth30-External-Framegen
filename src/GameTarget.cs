// C# 5 · GameTarget.cs · separate bootstrap launcher and renderer · Windows x64 · .NET Framework 4.8
using System;
using System.IO;

namespace FramegenLab
{
    public sealed class GameTarget
    {
        public string Launcher;
        public string Renderer;
        public string Evidence;
        public static GameTarget Resolve(string selected)
        {
            string full = Path.GetFullPath(selected); Disk.NoLinks(full);
            if (!File.Exists(full) || !String.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose an existing game executable.");
            PeInfo image = PeInspector.Inspect(full);
            string known = VulkanRuntime.Renderer(full);
            if (!String.Equals(known, full, StringComparison.OrdinalIgnoreCase) || String.Equals(Path.GetFileName(full), "spartanport-probe.exe", StringComparison.OrdinalIgnoreCase))
                return new GameTarget { Launcher = VulkanRuntime.Launcher(known), Renderer = known, Evidence = "Normal SpartanPort launcher; renderer: " + Path.GetFileName(known) + "." };
            string relative = image.BootstrapTarget;
            if (!String.IsNullOrEmpty(relative) && relative.Replace('/', '\\').IndexOf("\\Binaries\\Win64\\", StringComparison.OrdinalIgnoreCase) >= 0 && !String.IsNullOrEmpty(image.BootstrapArguments))
            {
                string root = Path.GetDirectoryName(full), path = relative.Replace('/', '\\');
                if (Path.IsPathRooted(path) || path.Contains(":") || Array.Exists(path.Split('\\'), part => part == ".." || part == "."))
                    throw new IOException("The launcher's embedded renderer path is outside the game installation.");
                string renderer = Path.GetFullPath(Path.Combine(root, path));
                if (!renderer.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid embedded renderer path.");
                Disk.NoLinks(renderer);
                if (!File.Exists(renderer)) throw new IOException("The launcher points to a missing renderer: " + renderer + ". Repair the game installation before testing.");
                PeInfo target = PeInspector.Inspect(renderer);
                if (target.Architecture != "x64" || target.IsDll) throw new IOException("The embedded renderer must be an x64 executable.");
                return new GameTarget { Launcher = full, Renderer = renderer, Evidence = "Launcher detected from embedded RCDATA 201/202. DLLs and monitoring use " + Path.GetFileName(renderer) + "; Start keeps the original launcher." };
            }
            if (image.Architecture != "x64" || image.IsDll) throw new IOException("This package needs an x64 game executable or a recognized launcher pointing to one.");
            return new GameTarget { Launcher = full, Renderer = full, Evidence = "Selected executable is used directly; no recognized bootstrap target." };
        }
    }
}
