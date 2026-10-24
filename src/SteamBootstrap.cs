// C# 5 · SteamBootstrap.cs · original tiny native ASI generator · Windows x64 · .NET Framework 4.8
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FramegenLab
{
    // Ordinary Windows PE DLL, loaded by Ultimate ASI Loader. No remote process APIs.
    // DllMain only returns TRUE. InitializeASI sets process-local variables, then
    // calls LoadLibraryW on the exact backend path, outside the loader's DllMain.
    // All instructions use RIP-relative addressing; the PE includes unwind / ASLR metadata.
    public static class SteamBootstrap
    {
        sealed class Code
        {
            public readonly List<byte> Bytes = new List<byte>();
            readonly List<Tuple<int, string>> branches = new List<Tuple<int, string>>();
            readonly Dictionary<string, int> labels = new Dictionary<string, int>();
            public void Emit(params byte[] bytes) { Bytes.AddRange(bytes); }
            public void Int(int value) { Bytes.AddRange(BitConverter.GetBytes(value)); }
            public void Rip(int dataOffset, params byte[] opcode)
            { Emit(opcode); Int(0x2000 + dataOffset - (0x1000 + Bytes.Count + 4)); }
            public void Branch(string label, bool conditional)
            { if (conditional) Emit(0x0f, 0x84); else Emit(0xe9); branches.Add(Tuple.Create(Bytes.Count, label)); Int(0); }
            public void Label(string name) { labels.Add(name, Bytes.Count); }
            public void Status(int offset, int value)
            { Emit(0xc7, 0x05); Int(0x2000 + offset - (0x1000 + Bytes.Count + 8)); Int(value); }
            public byte[] Finish()
            {
                foreach (Tuple<int, string> fix in branches)
                { byte[] value = BitConverter.GetBytes(labels[fix.Item2] - fix.Item1 - 4); for (int i = 0; i < 4; ++i) Bytes[fix.Item1 + i] = value[i]; }
                return Bytes.ToArray();
            }
        }
        static int Align(int value, int alignment) { return (value + alignment - 1) / alignment * alignment; }
        static void U32(BinaryWriter writer, long at, uint value) { writer.BaseStream.Position = at; writer.Write(value); }
        static int Append(MemoryStream stream, BinaryWriter writer, byte[] data, int alignment)
        {
            stream.Position = Align((int)stream.Length, alignment); int at = (int)stream.Position; writer.Write(data); return at;
        }
        static int Text(MemoryStream stream, BinaryWriter writer, string value, bool wide)
        { return Append(stream, writer, wide ? Encoding.Unicode.GetBytes(value + "\0") : Encoding.ASCII.GetBytes(value + "\0"), wide ? 2 : 1); }
        public static byte[] Build(string core, IDictionary<string, string> environment)
        {
            if (!Path.IsPathRooted(core) || core.IndexOf('\0') >= 0) throw new IOException("The Steam backend must have an absolute local path.");
            using (MemoryStream data = new MemoryStream())
            using (BinaryWriter d = new BinaryWriter(data))
            {
                d.Write(new byte[256]);
                int status = Append(data, d, new byte[4], 4);
                int dllName = Text(data, d, "FramegenLab.Steam.asi", false), exportName = Text(data, d, "InitializeASI", false);
                int moduleName = Text(data, d, "KERNEL32.dll", false);
                int setName = Append(data, d, new byte[2], 2); d.Write(Encoding.ASCII.GetBytes("SetEnvironmentVariableW\0"));
                int loadName = Append(data, d, new byte[2], 2); d.Write(Encoding.ASCII.GetBytes("LoadLibraryW\0"));
                int lookup = Append(data, d, new byte[24], 8), iat = Append(data, d, new byte[24], 8);
                data.Position = lookup; d.Write((ulong)(0x2000 + setName)); d.Write((ulong)(0x2000 + loadName));
                data.Position = iat; d.Write((ulong)(0x2000 + setName)); d.Write((ulong)(0x2000 + loadName));
                int coreName = Text(data, d, core, true);
                List<Tuple<int, int>> values = new List<Tuple<int, int>>();
                foreach (string key in LaunchProfiles.Keys)
                {
                    string value; environment.TryGetValue(key, out value);
                    int name = Text(data, d, key, true), content = value == null ? -1 : Text(data, d, value, true);
                    values.Add(Tuple.Create(name, content));
                }
                Code code = new Code();
                code.Emit(0x48, 0x83, 0xec, 0x28); // sub rsp,40: Win64 shadow space and alignment
                code.Emit(0x31, 0xc0, 0xb9, 1, 0, 0, 0); // xor eax,eax; mov ecx,1
                code.Rip(status, 0xf0, 0x0f, 0xb1, 0x0d); // lock cmpxchg [state],ecx
                code.Emit(0x85, 0xc0); code.Branch("initialize", true); code.Branch("return", false);
                code.Label("initialize");
                foreach (Tuple<int, int> pair in values)
                {
                    code.Rip(pair.Item1, 0x48, 0x8d, 0x0d); // lea rcx,key
                    if (pair.Item2 < 0) code.Emit(0x31, 0xd2); // xor edx,edx: clear absent variable
                    else code.Rip(pair.Item2, 0x48, 0x8d, 0x15); // lea rdx,value
                    code.Rip(iat, 0xff, 0x15); // call [SetEnvironmentVariableW]
                    code.Emit(0x85, 0xc0); code.Branch("failed", true);
                }
                code.Rip(coreName, 0x48, 0x8d, 0x0d); code.Rip(iat + 8, 0xff, 0x15);
                code.Emit(0x48, 0x85, 0xc0); code.Branch("failed", true);
                code.Status(status, 2); code.Emit(0xb8, 1, 0, 0, 0); code.Branch("return", false);
                code.Label("failed"); code.Status(status, 0); code.Emit(0x31, 0xc0);
                code.Label("return"); code.Emit(0x48, 0x83, 0xc4, 0x28, 0xc3);
                int functionEnd = code.Bytes.Count, entry = code.Bytes.Count;
                code.Emit(0xb8, 1, 0, 0, 0, 0xc3); // DllMain: return TRUE
                byte[] text = code.Finish();
                int unwind = Append(data, d, new byte[] { 1, 4, 1, 0, 4, 0x42, 0, 0 }, 4);
                int runtime = Append(data, d, new byte[12], 4);
                U32(d, runtime, 0x1000); U32(d, runtime + 4, (uint)(0x1000 + functionEnd)); U32(d, runtime + 8, (uint)(0x2000 + unwind));
                int reloc = Append(data, d, new byte[12], 4); U32(d, reloc, 0x1000); U32(d, reloc + 4, 12); // two ABSOLUTE no-op relocations
                // Export InitializeASI, with no initialization in DllMain.
                U32(d, 12, (uint)(0x2000 + dllName)); U32(d, 16, 1); U32(d, 20, 1); U32(d, 24, 1);
                U32(d, 28, 0x2040); U32(d, 32, 0x2044); U32(d, 36, 0x2048);
                U32(d, 0x40, 0x1000); U32(d, 0x44, (uint)(0x2000 + exportName));
                U32(d, 0x80, (uint)(0x2000 + lookup)); U32(d, 0x8c, (uint)(0x2000 + moduleName)); U32(d, 0x90, (uint)(0x2000 + iat));
                int textRaw = Align(text.Length, 512), dataRaw = Align((int)data.Length, 512);
                using (MemoryStream result = new MemoryStream(new byte[512 + textRaw + dataRaw]))
                using (BinaryWriter w = new BinaryWriter(result))
                {
                    w.Write((ushort)0x5a4d); U32(w, 0x3c, 0x80); U32(w, 0x80, 0x4550);
                    result.Position = 0x84; w.Write((ushort)0x8664); w.Write((ushort)2);
                    result.Position = 0x94; w.Write((ushort)240); w.Write((ushort)0x2022); w.Write((ushort)0x20b);
                    U32(w, 0x9c, (uint)textRaw); U32(w, 0xa0, (uint)dataRaw); U32(w, 0xa8, (uint)(0x1000 + entry)); U32(w, 0xac, 0x1000);
                    result.Position = 0xb0; w.Write(0x180000000UL); w.Write(0x1000U); w.Write(512U); w.Write((ushort)6);
                    result.Position = 0xc8; w.Write((ushort)6);
                    U32(w, 0xd0, (uint)Align(0x2000 + (int)data.Length, 0x1000)); U32(w, 0xd4, 512);
                    result.Position = 0xdc; w.Write((ushort)2); w.Write((ushort)0x160);
                    w.Write(0x100000UL); w.Write(0x1000UL); w.Write(0x100000UL); w.Write(0x1000UL);
                    U32(w, 0x104, 16); U32(w, 0x108, 0x2000); U32(w, 0x10c, 0x50);
                    U32(w, 0x110, 0x2080); U32(w, 0x114, 40);
                    U32(w, 0x120, (uint)(0x2000 + runtime)); U32(w, 0x124, 12);
                    U32(w, 0x130, (uint)(0x2000 + reloc)); U32(w, 0x134, 12);
                    U32(w, 0x168, (uint)(0x2000 + iat)); U32(w, 0x16c, 24);
                    result.Position = 0x188; w.Write(Encoding.ASCII.GetBytes(".text\0\0\0")); w.Write((uint)text.Length); w.Write(0x1000U); w.Write((uint)textRaw); w.Write(512U);
                    U32(w, 0x188 + 36, 0x60000020);
                    result.Position = 0x1b0; w.Write(Encoding.ASCII.GetBytes(".data\0\0\0")); w.Write((uint)data.Length); w.Write(0x2000U); w.Write((uint)dataRaw); w.Write((uint)(512 + textRaw));
                    U32(w, 0x1b0 + 36, 0xc0000040);
                    result.Position = 512; w.Write(text); result.Position = 512 + textRaw; w.Write(data.ToArray());
                    return result.ToArray();
                }
            }
        }
    }
}
