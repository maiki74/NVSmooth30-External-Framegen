// C# 5 · PeInspector.cs · static file inspection only · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FramegenLab
{
    public sealed class PeInfo
    {
        public string Architecture;
        public bool IsDll;
        public bool Managed;
        public string BootstrapTarget;
        public string BootstrapArguments;
        public string GodotVersion;
        public string GodotApplicationName;
        public string GodotRenderingMethod;
        public bool GodotCustomUserDir;
        public string Sha256;
        public List<string> Exports = new List<string>();
        public List<string> Imports = new List<string>();
        public List<string> DirectImports = new List<string>();
        public List<string> DelayedImports = new List<string>();
        public override string ToString()
        {
            return Architecture + (Managed ? " / managed .NET" : " / native") + (IsDll ? " DLL" : " executable") +
                "\r\nSHA-256: " + Sha256 + "\r\nNamed exports (up to 256): " + (Exports.Count == 0 ? "none" : String.Join(", ", Exports.ToArray())) +
                "\r\n\r\nInspection reads bytes only. A valid PE or matching architecture does not prove that the game loads this DLL or that frame generation works.";
        }
    }
    public static class PeInspector
    {
        struct Section { public uint Address, VirtualSize, Raw, Size; }
        static void Bound(long at, long size, long length)
        { if (at < 0 || size < 0 || at > length || size > length - at) throw new InvalidDataException("Truncated or invalid PE image."); }
        static long Offset(uint rva, List<Section> sections, long length)
        {
            foreach (Section s in sections)
                // Packed Godot executables may have a huge on-disk PCK section
                // with only a few mapped bytes. Its raw extent must not swallow
                // the RVAs belonging to later import/resource sections.
                if ((ulong)rva >= s.Address && (ulong)rva < (ulong)s.Address + (s.VirtualSize == 0 ? s.Size : s.VirtualSize))
                {
                    long delta = (long)rva - s.Address;
                    if (delta >= s.Size) throw new InvalidDataException("PE RVA points outside stored section bytes.");
                    long offset = s.Raw + delta;
                    Bound(offset, 1, length); return offset;
                }
            throw new InvalidDataException("Unmapped PE RVA.");
        }
        public static PeInfo Inspect(string path)
        {
            PeInfo info = new PeInfo();
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader r = new BinaryReader(fs))
            {
                Bound(0, 64, fs.Length);
                if (r.ReadUInt16() != 0x5a4d) throw new InvalidDataException("This is not a Windows PE file (MZ header missing).");
                fs.Position = 0x3c; long pe = r.ReadUInt32(); Bound(pe, 24, fs.Length); fs.Position = pe;
                if (r.ReadUInt32() != 0x4550) throw new InvalidDataException("Invalid PE signature.");
                ushort machine = r.ReadUInt16(); ushort count = r.ReadUInt16();
                info.Architecture = machine == 0x8664 ? "x64" : machine == 0x14c ? "x86" : machine == 0xaa64 ? "ARM64" : "Unknown (0x" + machine.ToString("X4") + ")";
                fs.Position = pe + 20; ushort optionalSize = r.ReadUInt16(); info.IsDll = (r.ReadUInt16() & 0x2000) != 0;
                long optional = pe + 24; Bound(optional, optionalSize, fs.Length); fs.Position = optional;
                ushort magic = r.ReadUInt16(); int dirOffset = magic == 0x20b ? 112 : magic == 0x10b ? 96 : -1;
                if (dirOffset < 0 || optionalSize < dirOffset + 8 || count == 0 || count > 96) throw new InvalidDataException("Unsupported PE optional header.");
                fs.Position = optional + dirOffset - 4; uint directories = r.ReadUInt32();
                fs.Position = optional + dirOffset; uint exports = directories > 0 ? r.ReadUInt32() : 0;
                uint imports = 0, delayedImports = 0, resources = 0, resourceSize = 0;
                if (directories > 1 && optionalSize >= dirOffset + 16) { fs.Position = optional + dirOffset + 8; imports = r.ReadUInt32(); }
                if (directories > 2 && optionalSize >= dirOffset + 24) { fs.Position = optional + dirOffset + 16; resources = r.ReadUInt32(); resourceSize = r.ReadUInt32(); }
                if (directories > 13 && optionalSize >= dirOffset + 14 * 8) { fs.Position = optional + dirOffset + 13 * 8; delayedImports = r.ReadUInt32(); }
                if (directories > 14 && optionalSize >= dirOffset + 15 * 8)
                { fs.Position = optional + dirOffset + 14 * 8; info.Managed = r.ReadUInt32() != 0; }
                List<Section> sections = new List<Section>();
                long sectionStart = optional + optionalSize; Bound(sectionStart, count * 40, fs.Length);
                for (int i = 0; i < count; ++i)
                {
                    fs.Position = sectionStart + i * 40;
                    string sectionName = Encoding.ASCII.GetString(r.ReadBytes(8)).TrimEnd('\0');
                    Section s = new Section { VirtualSize = r.ReadUInt32(), Address = r.ReadUInt32(), Size = r.ReadUInt32(), Raw = r.ReadUInt32() };
                    Bound(s.Raw, s.Size, fs.Length); sections.Add(s);
                    if (sectionName == "pck") GodotMetadata.Inspect(r, s.Raw, s.Size, info);
                }
                if (resources != 0 && resourceSize != 0)
                {
                    info.BootstrapTarget = ResourceText(r, resources, resourceSize, sections, 201);
                    if (info.BootstrapTarget != null) info.BootstrapArguments = ResourceText(r, resources, resourceSize, sections, 202);
                }
                if (imports != 0)
                {
                    long table = Offset(imports, sections, fs.Length);
                    for (int i = 0; i < 1024; ++i)
                    {
                        long at = table + i * 20; Bound(at, 20, fs.Length); fs.Position = at;
                        uint lookup = r.ReadUInt32(), stamp = r.ReadUInt32(), forward = r.ReadUInt32(), nameRva = r.ReadUInt32(), address = r.ReadUInt32();
                        if ((lookup | stamp | forward | nameRva | address) == 0) break;
                        fs.Position = Offset(nameRva, sections, fs.Length); StringBuilder name = new StringBuilder();
                        for (int n = 0; n < 260 && fs.Position < fs.Length; ++n) { byte b = r.ReadByte(); if (b == 0) break; name.Append(b < 128 ? (char)b : '?'); }
                        info.Imports.Add(name.ToString());
                        info.DirectImports.Add(name.ToString());
                    }
                }
                if (delayedImports != 0)
                {
                    long table = Offset(delayedImports, sections, fs.Length);
                    for (int i = 0; i < 1024; ++i)
                    {
                        Bound(table + i * 32, 32, fs.Length); fs.Position = table + i * 32;
                        uint attributes = r.ReadUInt32(), nameRva = r.ReadUInt32();
                        if (attributes == 0 && nameRva == 0) break;
                        // Modern delay descriptors use RVAs; legacy VA descriptors are not guessed.
                        if ((attributes & 1) == 0) continue;
                        fs.Position = Offset(nameRva, sections, fs.Length); StringBuilder name = new StringBuilder();
                        for (int n = 0; n < 260 && fs.Position < fs.Length; ++n) { byte b = r.ReadByte(); if (b == 0) break; name.Append(b < 128 ? (char)b : '?'); }
                        info.Imports.Add(name.ToString());
                        info.DelayedImports.Add(name.ToString());
                    }
                }
                if (exports != 0)
                {
                    long exportAt = Offset(exports, sections, fs.Length); Bound(exportAt, 40, fs.Length); fs.Position = exportAt + 24;
                    uint names = r.ReadUInt32(); fs.Position = exportAt + 32; uint namesRva = r.ReadUInt32();
                    if (names > 1000000) throw new InvalidDataException("Invalid PE export count.");
                    if (names > 0)
                    {
                        long table = Offset(namesRva, sections, fs.Length); Bound(table, (long)names * 4, fs.Length);
                        for (int i = 0; i < Math.Min(names, 256); ++i)
                        {
                            fs.Position = table + i * 4; uint nameRva = r.ReadUInt32(); fs.Position = Offset(nameRva, sections, fs.Length);
                            StringBuilder name = new StringBuilder();
                            for (int n = 0; n < 256 && fs.Position < fs.Length; ++n) { byte b = r.ReadByte(); if (b == 0) break; name.Append(b < 128 ? (char)b : '?'); }
                            info.Exports.Add(name.ToString());
                        }
                    }
                }
            }
            info.Sha256 = Disk.Hash(path); return info;
        }
        // Read RCDATA as bytes only. Never map the executable or run its entry point.
        static string ResourceText(BinaryReader reader, uint rva, uint size, List<Section> sections, uint id)
        {
            long start = Offset(rva, sections, reader.BaseStream.Length); uint relative = 0;
            uint[] keys = { 10, id, UInt32.MaxValue };
            for (int depth = 0; depth < keys.Length; depth++)
            {
                Bound(relative, 16, size); Bound(start + relative, 16, reader.BaseStream.Length);
                reader.BaseStream.Position = start + relative + 12;
                int count = reader.ReadUInt16() + reader.ReadUInt16();
                if (count > 4096) throw new InvalidDataException("Invalid PE resource entry count.");
                Bound((long)relative + 16, count * 8L, size);
                Bound(start + relative + 16, count * 8L, reader.BaseStream.Length);
                bool found = false; uint next = 0;
                for (int n = 0; n < count; n++)
                {
                    uint name = reader.ReadUInt32(), value = reader.ReadUInt32();
                    if ((name & 0x80000000) == 0 && (keys[depth] == UInt32.MaxValue || name == keys[depth]))
                    { next = value; found = true; break; }
                }
                if (!found) return null;
                if (((next & 0x80000000) != 0) != (depth < 2)) throw new InvalidDataException("Invalid PE resource tree.");
                relative = next & 0x7fffffff;
            }
            Bound(relative, 16, size); Bound(start + relative, 16, reader.BaseStream.Length);
            reader.BaseStream.Position = start + relative;
            uint data = reader.ReadUInt32(), length = reader.ReadUInt32();
            if (length < 2 || length > 8192 || (length & 1) != 0) return null;
            long offset = Offset(data, sections, reader.BaseStream.Length); Bound(offset, length, reader.BaseStream.Length);
            reader.BaseStream.Position = offset;
            string text;
            try { text = new UnicodeEncoding(false, false, true).GetString(reader.ReadBytes((int)length)).TrimEnd('\0'); }
            catch (DecoderFallbackException) { return null; }
            return text.IndexOf('\0') >= 0 ? null : text;
        }
    }
}
