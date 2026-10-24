// C# 5 · HotkeyTests.cs · own message-only window · Windows x64 · .NET Framework 4.8
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FramegenLab
{
    public static class HotkeyTests
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        sealed class TestWindow : NativeWindow, IDisposable
        {
            public AnswerHotkeys Keys;
            public int Yes, No;
            public TestWindow()
            {
                CreateHandle(new CreateParams { Caption = "Framegen hotkey self-test", Parent = new IntPtr(-3) });
                Keys = new AnswerHotkeys(Handle);
            }
            protected override void WndProc(ref Message message)
            {
                if (Keys != null)
                {
                    int answer = Keys.Answer(message.Msg, message.WParam);
                    if (answer == 1) Yes++; else if (answer == 2) No++;
                }
                base.WndProc(ref message);
            }
            public void Dispose() { Keys.Dispose(); DestroyHandle(); }
        }
        static void Pump() { for (int i = 0; i < 10; ++i) { Application.DoEvents(); Thread.Sleep(5); } }
        public static void Run(Action<bool, string> check)
        {
            using (TestWindow window = new TestWindow())
            {
                window.Keys.Enable(true);
                check(window.Keys.YesRegistered && window.Keys.NoRegistered, "Windows registers global F1 and F2 shortcuts for the app's own window");
                PostMessage(window.Handle, AnswerHotkeys.MessageId, new IntPtr(AnswerHotkeys.YesId), IntPtr.Zero);
                PostMessage(window.Handle, AnswerHotkeys.MessageId, new IntPtr(AnswerHotkeys.NoId), IntPtr.Zero); Pump();
                check(window.Yes == 1 && window.No == 1, "The real Windows hotkey message dispatches F1 to Yes and F2 to No");
                window.Keys.Enable(false);
                PostMessage(window.Handle, AnswerHotkeys.MessageId, new IntPtr(AnswerHotkeys.NoId), IntPtr.Zero); Pump();
                check(window.No == 1, "Disabled shortcuts cannot enqueue another answer during a transition");
            }
            using (TestWindow window = new TestWindow())
            {
                window.Keys.Enable(true); check(window.Keys.YesRegistered && window.Keys.NoRegistered, "Closing the registration releases F1 and F2 for reuse");
            }
        }
    }
}
