// C# 5 · GodotTests.cs · packed-EXE regression fixtures, never executed · Windows x64 · .NET Framework 4.8
using System;
using System.IO;
using System.Text;

namespace FramegenLab
{
    public static class GodotTests
    {
        static byte[] Settings()
        {
            using (MemoryStream stream = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x47464345u); writer.Write(1u);
                byte[] key = Encoding.UTF8.GetBytes("application/config/name"), value = Encoding.UTF8.GetBytes("FixtureGodot");
                writer.Write((uint)key.Length); writer.Write(key); writer.Write((uint)(value.Length + 8)); writer.Write(4u); writer.Write((uint)value.Length); writer.Write(value);
                return stream.ToArray();
            }
        }
        public static void Run(string root, string inert, Action<bool, string> check)
        {
            byte[] bytes = new byte[8192]; Array.Copy(File.ReadAllBytes(inert), bytes, 1024);
            using (MemoryStream stream = new MemoryStream(bytes)) using (BinaryWriter writer = new BinaryWriter(stream))
            {
                stream.Position = 0x86; writer.Write((ushort)2);
                stream.Position = 0x96; writer.Write((ushort)0x22);
                stream.Position = 0x104; writer.Write(3u);
                stream.Position = 0x110; writer.Write(0x2100u); writer.Write(40u);
                stream.Position = 0x118; writer.Write(0x2000u); writer.Write(512u);
                stream.Position = 0x188; writer.Write(Encoding.ASCII.GetBytes("pck\0\0\0\0\0"));
                writer.Write(8u); writer.Write(0x1000u); writer.Write(7680u); writer.Write(512u);
                stream.Position = 0x1b0; writer.Write(Encoding.ASCII.GetBytes(".rsrc\0\0\0"));
                writer.Write(512u); writer.Write(0x2000u); writer.Write(512u); writer.Write(0x800u);
                stream.Position = 0x900 + 12; writer.Write(0x2180u);
                stream.Position = 0x980; writer.Write(Encoding.ASCII.GetBytes("OPENGL32.dll\0"));
                // The incorrect raw-size RVA mapping reads these false counts.
                stream.Position = 0x120c; writer.Write((ushort)65535); writer.Write((ushort)65535);
                stream.Position = 0x200; writer.Write(0x43504447u); writer.Write(2u); writer.Write(4u); writer.Write(1u); writer.Write(1u); writer.Write(0u); writer.Write(512ul);
                stream.Position = 0x260; writer.Write(1u);
                byte[] name = Encoding.UTF8.GetBytes("res://project.binary"), settings = Settings();
                writer.Write((uint)name.Length); writer.Write(name); writer.Write(0x1200ul); writer.Write((ulong)settings.Length); writer.Write(new byte[16]); writer.Write(0u);
                stream.Position = 0x1400; writer.Write(settings);
            }
            string fixture = Path.Combine(root, "PackedGodotFixture.exe"); File.WriteAllBytes(fixture, bytes);
            PeInfo image = PeInspector.Inspect(fixture);
            check(image.Architecture == "x64" && !image.IsDll && image.BootstrapTarget == null, "A large raw PCK extent does not shadow later resource RVAs or cause a false resource-count error");
            check(image.Imports.Contains("OPENGL32.dll"), "Import RVAs after a short virtual / long raw packed section resolve to their own section");
            check(image.GodotVersion == "4.1.1" && image.GodotApplicationName == "FixtureGodot", "Bounded embedded PCK metadata identifies the engine and actual project name without executing code");
            BundledCatalog catalog = new BundledCatalog(Path.Combine(root, "godot-catalog")); catalog.ConfigureGame(fixture);
            check(catalog.Count == 1 && catalog.VulkanActive && catalog.GraphicsSummary.Contains("unconfirmed"), "Unknown Godot runtime offers only the supported experimental Vulkan route with explicit uncertainty");
            string log = Path.Combine(root, "godot-test.log");
            File.WriteAllText(log, "Godot Engine v4.1.1.stable.custom_build\nVulkan API 1.4.351 - Forward+ - Using Vulkan Device #0: fixture\n");
            check(GodotMetadata.ReadLog(fixture, image, log).Api == "vulkan", "A matching recent Godot engine log identifies the previously used Vulkan API");
            File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddDays(1));
            check(GodotMetadata.ReadLog(fixture, image, log) == null, "Godot runtime logs with future timestamps are ignored");
            File.WriteAllText(log, "Godot Engine v4.1.1.stable.custom_build\nOpenGL API 3.3.0 - Compatibility - fixture\n");
            check(GodotMetadata.ReadLog(fixture, image, log).Api == "opengl" && GraphicsApi.Label("opengl") == "OpenGL", "Godot OpenGL compatibility logs are distinguished from Vulkan");
            File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddDays(-8));
            check(GodotMetadata.ReadLog(fixture, image, log) == null, "Old Godot runtime logs are ignored");
            File.WriteAllText(log, "Godot Engine v3.5.0.stable\nVulkan API fixture\n");
            check(GodotMetadata.ReadLog(fixture, image, log) == null, "A Godot log from a different engine version is ignored");
            image.GodotApplicationName = "..\\outside";
            check(GodotMetadata.ForGame(fixture, image).Api == "unknown", "An embedded project name cannot escape the Godot user-data folder");
            image.GodotApplicationName = "..";
            check(GodotMetadata.ForGame(fixture, image).Api == "unknown", "A bare parent-directory name is rejected before looking up a project log");
            image.GodotRenderingMethod = "gl_compatibility";
            check(GodotMetadata.ForGame(fixture, image).Api == "opengl", "An explicit Godot OpenGL configuration is reported instead of treated as DirectX");
            image.GodotRenderingMethod = "forward_plus";
            check(GodotMetadata.ForGame(fixture, image).Api == "vulkan", "An explicit Forward+ project configuration provides a labelled Vulkan hint");
            // Corrupt optional pack metadata; leave the actual PE sections valid.
            Array.Clear(bytes, 0x200, 4); string noPack = Path.Combine(root, "NoPackFixture.exe"); File.WriteAllBytes(noPack, bytes);
            check(PeInspector.Inspect(noPack).GodotVersion == null, "A section named pck without the expected format signature is not identified as Godot");
            // Native resource corruption is still rejected, rather than hidden by a blanket catch.
            bytes[0x80c] = 255; bytes[0x80d] = 255; File.WriteAllBytes(noPack, bytes);
            bool rejected = false; try { PeInspector.Inspect(noPack); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Real malformed resource tables remain rejected after packed-section support");
        }
    }
}
