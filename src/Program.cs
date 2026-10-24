// C# 5 · Program.cs · Framegen DLL Tester · Windows x64 · .NET Framework 4.8
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Framegen DLL Tester")]
[assembly: AssemblyDescription("Frame generation DLL compatibility tests with verified backup and restoration")]
[assembly: AssemblyCompany("maiki74")]
[assembly: AssemblyProduct("Framegen DLL Tester")]
[assembly: AssemblyCopyright("Copyright (c) 2026 maiki74")]
[assembly: AssemblyVersion("0.3.7.0")]
[assembly: AssemblyFileVersion("0.3.7.0")]

namespace FramegenLab
{
    static class Program
    {
        [STAThread] static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if(args.Length==2&&args[0]=="--overlay-output")
                {
                    if(args[1]!="enable"&&args[1]!="restore")throw new IOException("Choose enable or restore for the FPS overlay.");
                    return RtssOverlay.ApplyElevated(args[1]=="enable");
                }
                if (args.Length == 1 && args[0] == "--fixture-exit") return 13;
                if (args.Length == 1 && args[0] == "--launcher-fixture")
                {
                    GameTarget target = GameTarget.Resolve(Assembly.GetExecutingAssembly().Location);
                    using (System.Diagnostics.Process child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target.Renderer, "--fixture") { WorkingDirectory = Path.GetDirectoryName(target.Renderer), UseShellExecute = false })) { }
                    return 0;
                }
                if (args.Length == 1 && (args[0] == "--fixture" || args[0] == "--fixture-veto"))
                {
                    using (Form fixture = new Form { Text = "Framegen DLL Tester - disposable test fixture", Width = 500, Height = 180, StartPosition = FormStartPosition.CenterScreen })
                    {
                        string values = String.Join("\r\n", Array.ConvertAll(LaunchProfiles.Keys, key => key + "=" + (Environment.GetEnvironmentVariable(key) ?? "<unset>"))) +
                            "\r\nSteamAppId=" + (Environment.GetEnvironmentVariable("SteamAppId") ?? "<unset>") + "\r\nSteamGameId=" + (Environment.GetEnvironmentVariable("SteamGameId") ?? "<unset>");
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture-environment.txt"), values);
                        if (args[0] == "--fixture-veto") fixture.FormClosing += delegate(object sender, FormClosingEventArgs closing) { closing.Cancel = !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture-allow-close.txt")); };
                        fixture.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Disposable test fixture. No mod DLL is loaded.\r\nThe automated test closes this window.", TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
                        Application.Run(fixture);
                    }
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--self-test") return SelfTests.Run(args[1]);
                if (args.Length == 3 && args[0] == "--bootstrap-fixture") return BootstrapFixture.Run(args[1], args[2]);
                bool preview = args.Length == 2 && (args[0] == "--preview"||args[0]=="--preview-vulkan");
                bool created;
                using (Mutex mutex = new Mutex(true, "Local\\FramegenDllTester-maiki74-0", out created))
                {
                    if (!created) { MessageBox.Show("Framegen DLL Tester is already open.", "Framegen DLL Tester"); return 2; }
                    try { Application.Run(new WizardForm(preview ? args[1] : null,preview&&args[0]=="--preview-vulkan")); }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception e)
            {
                if (args.Length == 2 && args[0] == "--self-test") { File.WriteAllText(args[1], "FAIL\r\n" + e); return 1; }
                if (args.Length == 3 && args[0] == "--bootstrap-fixture") { File.WriteAllText(args[2], "FAIL\r\n" + e); return 1; }
                MessageBox.Show(e.Message + "\r\n\r\nKeep trial backups. Reopen the app to recover unfinished tests.", "Framegen DLL Tester", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
