// C# 5 · HookshotLog.cs · read the Hookshot host log's renderer record · Windows x64 · .NET Framework 4.8
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FramegenLab
{
    // Hookshot ports load Vulkan / WebGPU at runtime, so the EXE imports say
    // nothing. Their host log (beside the EXE or one folder up) names the renderer.
    public static class HookshotLog
    {
        public static GraphicsDetection ForGame(string renderer)
        {
            try
            {
                string folder = Path.GetDirectoryName(Path.GetFullPath(renderer));
                foreach (string root in new[] { folder, Path.GetDirectoryName(folder) })
                {
                    if (root == null) continue;
                    FileInfo file = new FileInfo(Path.Combine(root, "hookshot_log.txt"));
                    if (!file.Exists || file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7) ||
                        file.LastWriteTimeUtc < File.GetLastWriteTimeUtc(renderer) || file.LastWriteTimeUtc > DateTime.UtcNow.AddMinutes(5)) continue;
                    Disk.NoLinks(file.FullName);
                    string text;
                    using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    // The renderer is recorded at startup; long sessions grow the log, so read only its first 1 MB.
                    using (var reader = new StreamReader(stream)) { char[] head = new char[1048576]; text = new string(head, 0, reader.ReadBlock(head, 0, head.Length)); }
                    // shortcut: only Vulkan presentation is recognised (native renderer or wgpu on Vulkan); add other backends if a port ships one.
                    if (!Regex.IsMatch(text, @"(?m)^\[hos host\] (rx: renderer: Vulkan\b|rx: WebGPU on [^\r\n]*\(Vulkan\)|vk: swapchain )")) continue;
                    return new GraphicsDetection { Api = "vulkan", Evidence = "Last recorded run: Vulkan (Hookshot log, " + file.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm 'UTC'") + "). Next launch may differ." };
                }
            }
            catch (Exception e)
            {
                // Optional local evidence must never prevent selecting or restoring a game.
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is ArgumentException)) throw;
            }
            return null;
        }
    }
}
