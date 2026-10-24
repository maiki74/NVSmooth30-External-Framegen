// C# 5 · AnswerHotkeys.cs · global F1/F2 answers · Windows x64 · .NET Framework 4.8
using System;
using System.Runtime.InteropServices;

namespace FramegenLab
{
    public sealed class AnswerHotkeys : IDisposable
    {
        public const int MessageId = 0x0312, YesId = 0x46f1, NoId = 0x46f2;
        readonly IntPtr window;
        public bool YesRegistered { get; private set; }
        public bool NoRegistered { get; private set; }
        bool enabled;
        DateTime retryAfter;
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
        public AnswerHotkeys(IntPtr handle) { window = handle; }
        public void Enable(bool value)
        {
            if (value == enabled && (!value || (YesRegistered && NoRegistered) || DateTime.UtcNow < retryAfter)) return;
            enabled = value;
            if (value)
            {
                if (!YesRegistered) YesRegistered = RegisterHotKey(window, YesId, 0x4000, 0x70); // MOD_NOREPEAT, VK_F1
                if (!NoRegistered) NoRegistered = RegisterHotKey(window, NoId, 0x4000, 0x71); // MOD_NOREPEAT, VK_F2
                retryAfter = DateTime.UtcNow.AddSeconds(2);
            }
            else
            {
                if (YesRegistered) UnregisterHotKey(window, YesId);
                if (NoRegistered) UnregisterHotKey(window, NoId);
                YesRegistered = NoRegistered = false;
            }
        }
        public int Answer(int message, IntPtr id)
        {
            if (!enabled || message != MessageId) return 0;
            if (id.ToInt64() == YesId && YesRegistered) return 1;
            if (id.ToInt64() == NoId && NoRegistered) return 2;
            return 0;
        }
        public void Dispose() { Enable(false); }
    }
}
