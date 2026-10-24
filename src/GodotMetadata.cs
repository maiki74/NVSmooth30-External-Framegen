// C# 5 · GodotMetadata.cs · bounded packed-project and log inspection · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FramegenLab
{
    public static class GodotMetadata
    {
        static void Bound(long at, long length, long end)
        { if (at < 0 || length < 0 || at > end || length > end - at) throw new InvalidDataException("Truncated Godot metadata."); }
        public static void Inspect(BinaryReader reader, long start, uint size, PeInfo image)
        {
            // No engine code, archive extraction, decryption or resource loading.
            try
            {
                long end = Math.Min(reader.BaseStream.Length, start + size);
                Bound(start, 100, end); reader.BaseStream.Position = start;
                if (reader.ReadUInt32() != 0x43504447 || reader.ReadUInt32() != 2) return;
                uint major = reader.ReadUInt32(), minor = reader.ReadUInt32(), patch = reader.ReadUInt32();
                if (major != 4 || minor > 99 || patch > 99) return;
                image.GodotVersion = major + "." + minor + "." + patch;
                uint flags = reader.ReadUInt32(); ulong fileBase = reader.ReadUInt64();
                if ((flags & 1) != 0 || fileBase > (ulong)end) return; // Encrypted directories are not inspected.
                reader.BaseStream.Position += 64; uint count = reader.ReadUInt32();
                if (count > 65536) return;
                for (uint index = 0; index < count; index++)
                {
                    if (reader.BaseStream.Position - start > 8388608) return;
                    Bound(reader.BaseStream.Position, 4, end); uint length = reader.ReadUInt32();
                    if (length == 0 || length > 4096) return;
                    Bound(reader.BaseStream.Position, length + 36L, end);
                    string name = Encoding.UTF8.GetString(reader.ReadBytes((int)length)).TrimEnd('\0');
                    ulong offset = reader.ReadUInt64(), bytes = reader.ReadUInt64(); reader.BaseStream.Position += 16;
                    uint entryFlags = reader.ReadUInt32();
                    if (name != "res://project.binary" && name != "project.binary") continue;
                    if ((entryFlags & 1) != 0 || bytes > 262144 || fileBase > (ulong)end || offset > (ulong)end - fileBase) return;
                    long at = (long)(fileBase + offset); Bound(at, (long)bytes, end); reader.BaseStream.Position = at;
                    var values = Settings(reader.ReadBytes((int)bytes));
                    string value;
                    if (values.TryGetValue("application/config/name", out value)) image.GodotApplicationName = value;
                    if (values.TryGetValue("application/config/use_custom_user_dir", out value)) image.GodotCustomUserDir = value == "true";
                    if (values.TryGetValue("rendering/renderer/rendering_method", out value)) image.GodotRenderingMethod = value;
                    return;
                }
            }
            catch (IOException) { } // Optional project metadata must not invalidate an otherwise valid PE.
            catch (ArgumentException) { }
        }
        static Dictionary<string, string> Settings(byte[] bytes)
        {
            var result = new Dictionary<string, string>();
            using (MemoryStream stream = new MemoryStream(bytes)) using (BinaryReader reader = new BinaryReader(stream))
            {
                Bound(0, 8, stream.Length); if (reader.ReadUInt32() != 0x47464345) return result;
                uint count = reader.ReadUInt32(); if (count > 8192) return result;
                for (uint n = 0; n < count; n++)
                {
                    Bound(stream.Position, 4, stream.Length); uint keySize = reader.ReadUInt32();
                    if (keySize > 4096) return result; Bound(stream.Position, keySize + 4L, stream.Length);
                    string key = Encoding.UTF8.GetString(reader.ReadBytes((int)keySize));
                    uint valueSize = reader.ReadUInt32(); Bound(stream.Position, valueSize, stream.Length); long next = stream.Position + valueSize;
                    if (valueSize >= 8)
                    {
                        uint type = reader.ReadUInt32(), length = reader.ReadUInt32();
                        if (type == 4 && length <= 4096 && length <= valueSize - 8)
                            result[key] = Encoding.UTF8.GetString(reader.ReadBytes((int)length));
                        else if (type == 1) result[key] = length == 0 ? "false" : "true";
                    }
                    stream.Position = next;
                }
            }
            return result;
        }
        public static GraphicsDetection ForGame(string renderer, PeInfo image)
        {
            if (String.IsNullOrEmpty(image.GodotVersion)) return null;
            if (!image.GodotCustomUserDir && !String.IsNullOrWhiteSpace(image.GodotApplicationName))
            {
                try
                {
                    if (image.GodotApplicationName == "." || image.GodotApplicationName == "..") throw new IOException("Invalid Godot project folder name.");
                    Disk.PlainName(image.GodotApplicationName);
                    string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata", image.GodotApplicationName, "logs", "godot.log");
                    GraphicsDetection recorded = ReadLog(renderer, image, log); if (recorded != null) return recorded;
                }
                catch (IOException) { }
            }
            string method = image.GodotRenderingMethod;
            if (method == "gl_compatibility") return new GraphicsDetection { Api = "opengl", Evidence = "Godot project requests OpenGL compatibility. A launch override may differ; no OpenGL backend is included." };
            if (method == "forward_plus" || method == "mobile") return new GraphicsDetection { Api = "vulkan", Evidence = "Godot " + image.GodotVersion + " project rendering method suggests Vulkan. Runtime remains unconfirmed." };
            return new GraphicsDetection { Api = "unknown", Evidence = "Godot " + image.GodotVersion + " embedded project detected; Vulkan/OpenGL runtime is unconfirmed." };
        }
        public static GraphicsDetection ReadLog(string renderer, PeInfo image, string log)
        {
            try
            {
                if (!File.Exists(log) || File.GetLastWriteTimeUtc(log) < DateTime.UtcNow.AddDays(-7) || File.GetLastWriteTimeUtc(log) > DateTime.UtcNow.AddMinutes(5) || File.GetLastWriteTimeUtc(log) < File.GetLastWriteTimeUtc(renderer)) return null;
                Disk.NoLinks(log);
                string text;
                using (FileStream stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                { byte[] bytes = new byte[4096]; int read = stream.Read(bytes, 0, bytes.Length); text = Encoding.UTF8.GetString(bytes, 0, read); }
                string[] lines = text.Replace("\r", "").Split('\n');
                if (lines.Length < 2 || !lines[0].StartsWith("Godot Engine v" + image.GodotVersion + ".", StringComparison.Ordinal)) return null;
                string api = lines[1].StartsWith("Vulkan API ", StringComparison.Ordinal) ? "vulkan" : lines[1].StartsWith("OpenGL API ", StringComparison.Ordinal) ? "opengl" : null;
                return api == null ? null : new GraphicsDetection { Api = api, Evidence = "Last Godot project log used " + GraphicsApi.Label(api) + " (embedded project name/engine matched). Historical hint; launch overrides or another copy may differ." };
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
