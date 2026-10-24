// C# 5 · ApiComboBox.cs · native selector with printable preview · Windows x64 · .NET Framework 4.8
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FramegenLab
{
    public sealed class ApiComboBox : ComboBox
    {
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            // Native combo boxes omit their selection in DrawToBitmap on some themes.
            // Print the actual selected item for WM_PRINT / WM_PRINTCLIENT only.
            if ((message.Msg == 0x317 || message.Msg == 0x318) && message.WParam != IntPtr.Zero)
            {
                using (Graphics canvas = Graphics.FromHdc(message.WParam))
                using (SolidBrush background = new SolidBrush(BackColor))
                {
                    canvas.FillRectangle(background, ClientRectangle);
                    Rectangle bounds = new Rectangle(4, 1, Math.Max(0, ClientSize.Width - 25), ClientSize.Height - 2);
                    TextRenderer.DrawText(canvas, Text, Font, bounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    ControlPaint.DrawComboButton(canvas, ClientSize.Width - 21, 1, 20, ClientSize.Height - 2, Enabled ? ButtonState.Normal : ButtonState.Inactive);
                }
            }
        }
    }
}
