// C# 5 · WizardForm.cs · select game, start, yes/no · Windows x64 · .NET Framework 4.8
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FramegenLab
{
    public sealed class WizardForm : Form
    {
        static readonly Color Bg = Color.FromArgb(15, 21, 32), Card = Color.FromArgb(24, 33, 47), Ink = Color.FromArgb(230, 237, 247), Muted = Color.FromArgb(153, 174, 197), Accent = Color.FromArgb(79, 221, 190);
        readonly TextBox game = new TextBox();
        readonly ComboBox graphics = new ApiComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly Label graphicsHint = new Label();
        readonly CheckBox steamSetup = new CheckBox { Text = "Keep framegen setup installed", AutoSize = true, ForeColor = Accent };
        readonly Label vulkanLaunchHint=new Label {Text="Vulkan setup applies when you start the game from this app.",AutoSize=true};
        readonly Label option = new Label(), instructions = new Label(), status = new Label();
        readonly Button browse = Button("Choose game...", false), launch = Button("Start game", true), yes = Button("Yes, it worked (F1)", true), no = Button("No, try next (F2)", false), stop = Button("Stop + restore", false), reset = Button("Reset tests", false), remove = Button("Remove test DLL", false);
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 700 };
        readonly WizardController flow;
        readonly BundledCatalog catalog;
        readonly string preview;
        bool updating, leaving, syncing;
        AnswerHotkeys hotkeys;
        string actionNotice;
        public WizardForm(string previewPath,bool previewVulkan=false)
        {
            preview = previewPath;
            string root = preview == null ? Disk.DefaultRoot : Path.Combine(Path.GetTempPath(), "FramegenDllTester-preview-" + Guid.NewGuid().ToString("N"));
            TrialStore store = new TrialStore(root); catalog = new BundledCatalog(root); flow = new WizardController(store, catalog);
            if (String.IsNullOrEmpty(flow.State.Game) && File.Exists(Path.Combine(root, "settings.json")))
            {
                string previousGame = Disk.Read<Preferences>(Path.Combine(root, "settings.json")).Game;
                if (!String.IsNullOrEmpty(previousGame)) flow.SelectGame(previousGame);
            }
            Text = "Framegen DLL Tester  |  0.3.7 preview"; ClientSize = new Size(800, 825); MinimumSize = new Size(720, 825);
            BackColor = Bg; ForeColor = Ink; Font = new Font("Segoe UI", 11); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterScreen;
            TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(26, 18, 26, 14), BackColor = Bg };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(body);
            Panel heading = new Panel();
            heading.Controls.Add(Label("FRAMEGEN DLL TESTER", 24, Ink, new Rectangle(0, 0, 710, 43)));
            heading.Controls.Add(Label("Choose your game. Start it. Tell the app if it worked.", 11, Muted, new Rectangle(2, 49, 720, 28))); Add(body, heading, 90);
            TableLayoutPanel select = new TableLayoutPanel { ColumnCount = 2, RowCount = 2 };
            select.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); select.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
            select.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); select.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            select.Controls.Add(new Label { Text = "GAME EXECUTABLE", ForeColor = Muted, Dock = DockStyle.Fill }, 0, 0);
            game.ReadOnly = true; game.BackColor = Card; game.ForeColor = Ink; game.BorderStyle = BorderStyle.FixedSingle; game.Dock = DockStyle.Fill; game.Margin = new Padding(0, 3, 10, 0);
            select.Controls.Add(game, 0, 1); browse.Dock = DockStyle.Fill; browse.Margin = new Padding(0); select.Controls.Add(browse, 1, 1); Add(body, select, 79);
            TableLayoutPanel apiRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 2 };
            apiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144)); apiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            apiRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); apiRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            apiRow.Controls.Add(new Label { Text = "GRAPHICS API", ForeColor = Muted, Dock = DockStyle.Fill, Padding = new Padding(2, 6, 0, 0) }, 0, 0);
            graphics.Items.AddRange(new object[] { "Automatic (detect from EXE)", "DirectX 12", "DirectX 11", "Vulkan (experimental)" });
            graphics.FlatStyle = FlatStyle.Standard;
            graphics.Dock = DockStyle.Fill; graphics.BackColor = Color.White; graphics.ForeColor = Color.Black; apiRow.Controls.Add(graphics, 1, 0);
            graphicsHint.ForeColor = Muted; graphicsHint.Dock = DockStyle.Fill; graphicsHint.Font = new Font("Segoe UI", 9);
            apiRow.Controls.Add(graphicsHint, 0, 1); apiRow.SetColumnSpan(graphicsHint, 2); Add(body, apiRow, 90);
            option.Font = new Font("Segoe UI", 13, FontStyle.Bold); option.ForeColor = Accent; option.BackColor = Card; option.Padding = new Padding(14, 13, 12, 8); Add(body, option, 75);
            instructions.ForeColor = Ink; instructions.Padding = new Padding(2, 14, 0, 0); Add(body, instructions, 74);
            FlowLayoutPanel startRow = new FlowLayoutPanel { WrapContents = false }; launch.Width = 206; startRow.Controls.Add(launch); Add(body, startRow, 52);
            LinkLabel fps=new LinkLabel{Text="FPS overlay...",LinkColor=Muted,ActiveLinkColor=Accent,AutoSize=true,Margin=new Padding(16,12,0,0)};fps.LinkClicked+=delegate{RtssOverlay.ShowSettings(this);};startRow.Controls.Add(fps);
            FlowLayoutPanel steamRow = new FlowLayoutPanel { WrapContents = false, Padding = new Padding(2, 2, 0, 0) }; steamRow.Controls.Add(steamSetup);steamRow.Controls.Add(vulkanLaunchHint); Add(body, steamRow, 40);
            Panel answer = new Panel { BackColor = Card, Padding = new Padding(12) };
            answer.Controls.Add(Label("DID FRAME GENERATION WORK?", 10, Muted, new Rectangle(14, 11, 650, 23)));
            FlowLayoutPanel verdicts = new FlowLayoutPanel { Location = new Point(14, 34), Width = 690, Height = 48, WrapContents = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            yes.Width = 207; no.Width = 207; verdicts.Controls.AddRange(new Control[] { yes, no }); answer.Controls.Add(verdicts); Add(body, answer, 96);
            status.ForeColor = Muted; status.Padding = new Padding(2, 9, 0, 0); Add(body, status, 73);
            FlowLayoutPanel utilities = new FlowLayoutPanel { WrapContents = false }; utilities.Controls.AddRange(new Control[] { stop, reset, remove });
            LinkLabel details = new LinkLabel { Text = "Details, logs and sources", LinkColor = Muted, ActiveLinkColor = Accent, AutoSize = true, Margin = new Padding(15, 12, 0, 0) };
            details.LinkClicked += delegate { ShowDetails(); }; utilities.Controls.Add(details); Add(body, utilities, 48);
            Label warning = new Label { ForeColor = Color.FromArgb(244, 193, 120), Text = "Offline, unprotected tests only. Easy Anti-Cheat and other anti-cheats may block DLLs or cause errors. Stop testing if an anti-cheat error appears.", Padding = new Padding(2, 6, 0, 0) }; Add(body, warning, 60);
            browse.Click += delegate
            {
                using (OpenFileDialog pick = new OpenFileDialog { Filter = "Game executable|*.exe", CheckFileExists = true, Title = "Choose the game's actual x64 executable" })
                    if (pick.ShowDialog(this) == DialogResult.OK) { try { flow.SelectGame(pick.FileName); RefreshUi(); } catch (Exception e) { Error(e); } }
            };
            launch.Click += async delegate { await Do(async delegate { await flow.StartGame(); }); };
            no.Click += async delegate { await Do(async delegate { await flow.No(); }); };
            yes.Click += delegate { try { flow.Yes(); RefreshUi(); } catch (Exception e) { Error(e); } };
            stop.Click += async delegate { await Do(async delegate { await flow.Stop(); }); };
            reset.Click += async delegate { await Do(async delegate { await flow.Reset(); }); };
            graphics.SelectedIndexChanged += async delegate
            {
                if (syncing || updating || graphics.SelectedIndex < 0) return;
                string selected = GraphicsApi.Modes[graphics.SelectedIndex];
                await Do(async delegate { await flow.SelectGraphicsApi(selected); });
            };
            steamSetup.CheckedChanged += async delegate
            {
                if (syncing || updating) return;
                bool enable = steamSetup.Checked;
                if (await Do(async delegate { if (enable) await flow.EnableSteam(); else await flow.Stop(); }))
                { actionNotice = enable ? "Setup installed. You may close this app and launch the game normally. Toggle off or Remove test DLL to restore." : "Persistent setup disabled. Test files removed and originals restored."; RefreshUi(); }
            };
            remove.Click += async delegate
            {
                if (await Do(async delegate { await flow.Stop(); }))
                { actionNotice = "Test DLL and companion files removed; any original files restored."; RefreshUi(); }
            };
            timer.Tick += async delegate
            {
                if (updating || flow.Busy || preview != null) return;
                try { await flow.Observe(); RefreshUi(); }
                catch (Exception e) { timer.Stop(); Error(e); }
            };
            Shown += delegate
            {
                if (preview != null)
                {
                    flow.State.Game = @"C:\Games\Example Game\Game.exe"; flow.State.Phase = "Running";
                    if(previewVulkan){flow.State.GraphicsApi="vulkan";catalog.ConfigureGame(flow.State.Game,"vulkan");flow.State.Phase="Idle";}
                    RefreshUi();
                    // Native combo text needs its first paint before WinForms exports the preview.
                    Refresh(); Application.DoEvents();
                    if (graphics.SelectedIndex < 0 || String.IsNullOrEmpty(graphics.Text)) throw new InvalidOperationException("The DirectX selector has no visible selection.");
                    using (Bitmap image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(Point.Empty, Size)); image.Save(preview, System.Drawing.Imaging.ImageFormat.Png); }
                    Close(); return;
                }
                hotkeys = new AnswerHotkeys(Handle); RefreshUi(); timer.Start();
            };
            FormClosing += async delegate(object sender, FormClosingEventArgs e)
            {
                if (leaving || preview != null) return;
                if (updating || flow.Busy) { e.Cancel = true; return; }
                if (flow.Session.Current != null || flow.State.Phase == "Recovery")
                {
                    e.Cancel = true;
                    bool restored = await Do(async delegate { await flow.Stop(); });
                    if (restored) { leaving = true; Close(); }
                }
            };
            FormClosed += delegate { timer.Stop(); timer.Dispose(); if (hotkeys != null) hotkeys.Dispose(); };
        }
        static Label Label(string text, float size, Color color, Rectangle rectangle)
        { return new Label { Text = text, Font = new Font("Segoe UI", size, size > 18 ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, Bounds = rectangle }; }
        static Button Button(string text, bool primary)
        { return new Button { Text = text, Height = 39, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Card, ForeColor = primary ? Bg : Ink, Margin = new Padding(0, 2, 12, 2), Padding = new Padding(10, 2, 10, 2), Cursor = Cursors.Hand }; }
        static void Add(TableLayoutPanel body, Control control, int height)
        { int row = body.RowCount++; body.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 0, 0, 6); body.Controls.Add(control, 0, row); }
        async System.Threading.Tasks.Task<bool> Do(Func<System.Threading.Tasks.Task> action)
        {
            if (updating) return false;
            updating = true; actionNotice = null; RefreshUi(); status.Text = "Preparing files / waiting for the game to close. Please wait...";
            try { await action(); timer.Start(); return true; }
            catch (Exception e) { Error(e); return false; }
            finally { updating = false; RefreshUi(); }
        }
        void RefreshUi()
        {
            string phase = flow.State.Phase; bool busy = updating || flow.Busy;
            game.Text = flow.State.Game;
            syncing = true;
            try { graphics.SelectedIndex = Array.IndexOf(GraphicsApi.Modes, GraphicsApi.Normalize(flow.State.GraphicsApi)); steamSetup.Checked = flow.SteamEnabled; }
            finally { syncing = false; }
            graphics.Enabled = !busy && !String.IsNullOrEmpty(flow.State.Game) && phase != "Recovery";
            graphicsHint.Text = (catalog.GraphicsSummary ?? "Choose a game for an EXE import hint.") + "\r\n"+(catalog.VulkanActive?"Vulkan v2 relaxes VSync for this launch; saved game settings are unchanged.":"Selects DLL tests; does not change the game's renderer.");
            steamSetup.Enabled = !busy && (flow.SteamEnabled || (!String.IsNullOrEmpty(flow.State.Game) && catalog.Count > 0 && phase != "Recovery"));
            steamSetup.Visible=!catalog.VulkanActive;vulkanLaunchHint.Visible=catalog.VulkanActive;
            int current = Math.Min(flow.State.Index, catalog.Count - 1);
            option.Text = catalog.Count == 0 ? "NO COMPATIBLE BUNDLED OPTIONS\r\n" + catalog.UnavailableReason : "OPTION " + (current + 1) + " OF " + catalog.Count + "  -  DLLs included\r\n" + catalog.Label(current);
            launch.Enabled = !busy && catalog.Count > 0 && !String.IsNullOrEmpty(flow.State.Game) && new[] { "Idle", "Stopped", "Saved", "Ready", "SteamEnabled" }.Contains(phase);
            yes.Enabled = !busy && flow.CanAnswerYes;
            no.Enabled = !busy && flow.CanAnswerNo;
            browse.Enabled = !busy && flow.Session.Current == null && phase != "Recovery" && !flow.SteamEnabled;
            stop.Enabled = !busy && (flow.Session.Current != null || phase == "Recovery" || flow.SteamEnabled); stop.Text = phase == "Recovery" ? "Restore interrupted test" : "Stop + restore";
            reset.Enabled = !busy && !String.IsNullOrEmpty(flow.State.Game);
            remove.Enabled = !busy && !String.IsNullOrEmpty(flow.State.Game);
            remove.Visible=!catalog.VulkanActive || catalog.TranslatedDx9;
            no.Text=catalog.VulkanActive?"No, stop test (F2)":"No, try next (F2)";
            if (hotkeys != null) hotkeys.Enable(!busy && flow.CanAnswerNo);
            launch.Text = flow.SteamEnabled ? "Open game" : phase == "Saved" ? "Start saved setup" : "Start game";
            if (phase == "Running") { instructions.Text = "Test the game. If frame generation did not work, click No.\r\nThe app will close the game and prepare the next option."; status.Text = "Game started with this option's launch settings. Answer Yes or No."; }
            else if (phase == "Ready") { instructions.Text = "The next DLL and its configuration are ready.\r\nClick Start game to try this option."; status.Text = "Previous test restored. Waiting for your next start."; }
            else if (phase == "AwaitingAnswer") { instructions.Text = "The game closed. Did frame generation work?\r\nAnswer No to prepare the next option, or Yes to save this one."; status.Text = "Original files restored after the game exited."; }
            else if (phase == "Success") { instructions.Text = "Working option saved. You can keep playing.\r\nStop + restore ends the session and restores your original files."; status.Text = "Your result is saved locally. No more options will be tried."; }
            else if (phase == "Saved") { instructions.Text = "Your working option is saved.\r\nClick Start saved setup to use it again."; status.Text = "Game closed. Original files restored."; }
            else if (phase == "Recovery") { instructions.Text = "The previous test needs restoration.\r\nUse Restore interrupted test before starting another option."; status.Text = "No DLL will be replaced while the selected game is running."; }
            else if (phase == "SteamEnabled") { instructions.Text = "Test the installed setup, then answer Yes / F1 or No / F2.\r\nNo closes the game, restores files and installs the next option."; status.Text = flow.State.Winner == flow.State.Index ? "Working option saved. Persistent setup remains installed until you disable or remove it." : "Persistent setup enabled. Answer Yes or No after testing the game."; }
            else if (phase == "Exhausted") { instructions.Text = "All included options have been tested.\r\nNo working option was confirmed for this game."; status.Text = "Original files restored. No success is assumed."; }
            else { instructions.Text = "Select your game's executable and click Start game.\r\nThe app chooses the DLL and applies its launch settings automatically."; status.Text = "No DLL selection, rename, batch file or launch settings to fill in."; }
            if (catalog.Count == 0 && phase != "Recovery") { instructions.Text = catalog.UnavailableReason; status.Text = "No files staged. Select the actual renderer or return to Automatic."; }
            if(catalog.VulkanActive){
                if(phase=="Idle"||phase=="Stopped"||phase=="Saved"){instructions.Text="Start the game with the included Vulkan integration.\r\nClick Start game, then confirm whether the image is smoother.";status.Text=catalog.TranslatedDx9?"DX9 -> Vulkan test. DXVK d3d9.dll is backed up / restored automatically.":"Experimental Vulkan support; no DLL is copied into the game folder.";}
                else if(phase=="Ready"){instructions.Text="The Vulkan runtime is ready.\r\nClick Start game to test it.";status.Text=catalog.TranslatedDx9?"DXVK is staged with verified backup / restore. Waiting for your next start.":"No game files staged. Waiting for your next start.";}
                else if(phase=="AwaitingAnswer")status.Text=catalog.TranslatedDx9?"Game closed. The DXVK test DLL was removed and original files restored.":"Game closed. This Vulkan session changed no game files.";
            }
            if (phase == "Running" && flow.Session.Current != null)
                status.Text = TrialDiagnostics.Status(flow.Session.Current);
            if (hotkeys != null && !busy && flow.CanAnswerNo && (!hotkeys.YesRegistered || !hotkeys.NoRegistered))
                status.Text = "A shortcut is already reserved by another app. Use the Yes / No buttons.";
            if (!busy && flow.Session.Current == null && phase != "Recovery" && !String.IsNullOrEmpty(actionNotice)) status.Text = actionNotice;
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (!updating && !flow.Busy)
            {
                if (keyData == Keys.F1 && yes.Enabled) { yes.PerformClick(); return true; }
                if (keyData == Keys.F2 && no.Enabled) { no.PerformClick(); return true; }
            }
            return base.ProcessCmdKey(ref message, keyData);
        }
        protected override void WndProc(ref Message message)
        {
            int answer = hotkeys == null ? 0 : hotkeys.Answer(message.Msg, message.WParam);
            if (answer != 0)
            {
                if (!updating && !flow.Busy)
                {
                    if (answer == 1 && yes.Enabled) yes.PerformClick();
                    else if (answer == 2 && no.Enabled) no.PerformClick();
                }
                return;
            }
            base.WndProc(ref message);
        }
        void Error(Exception e)
        {
            string recovery = flow.State.Phase == "Recovery" || flow.Session.Current != null ? "\r\n\r\nClose the selected game and use Stop + restore to recover the pending files." : "";
            MessageBox.Show(this, e.Message + recovery, "Framegen DLL Tester", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        void ShowDetails()
        {
            using (Form details = new Form { Text = "Details, logs and sources", Width = 820, Height = 650, StartPosition = FormStartPosition.CenterParent, BackColor = Bg, ForeColor = Ink })
            {
                TextBox text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Card, ForeColor = Ink, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None };
                text.Text = "FRAMEGEN DLL TESTER 0.3.7 - PREVIEW\r\n\r\nChoose a game, Start game, then answer F1 / Yes or F2 / No. The app only advances after the game has closed and originals have been restored. It never force-terminates a game. Reset clears the saved choice and returns to the first option without erasing history.\r\n\r\nAutomatic inspects normal and delayed EXE imports. Manual DX12 / DX11 / Vulkan selects the tests; it does not change the game's renderer. Automatic DX9 uses official DXVK 3.1.1 to translate into Vulkan, with tracked backup / restore. DX10 has no bundled route.\r\n\r\nVulkan v2 is a process-local experimental integration with exact NVIDIA driver hash guards. The DLL is embedded, extracted into the app's own cache, and enabled only for the launched process tree. Native Vulkan stages no game files. DX9 translation stages tracked d3d9.dll and restores it afterward. No global Vulkan layer is registered. DX9 launches disable RTSS's conflicting Vulkan layer only for that child process; RTSS itself and its saved settings stay unchanged. Overlay availability still needs a gameplay check. SpartanPort uses its normal Play launcher and saved boot configuration. Vulkan v2 may replace FIFO/VSync with supported immediate presentation for that launch; saved display settings remain unchanged. Start Vulkan games from this app; persistent normal-launch installation is not offered for this route. No FPS cap is imposed.\r\n\r\nCURRENT OPTIONS\r\n" +
                    String.Join("\r\n", Enumerable.Range(0, catalog.Count).Select(i => (i + 1) + ". " + catalog.Label(i)).ToArray()) +
                    "\r\n\r\nThe DirectX routes retain their verified backup / restore and optional Keep framegen setup installed toggle. NVSmooth30 uses Ultimate ASI Loader and a configuration ASI; DLSSG retains its matching INI. Remove test DLL disables persistent setup and restores all tracked originals. Unrelated DLLs, logs and backups are preserved. Installed Steam games receive their App ID from the local library. No global environment, Steam launch option, protection or driver profile is changed.\r\n\r\nFPS OVERLAY\r\nDisplayed FPS uses the installed RTSS PresentMon provider. Updates once per second, with no assumed frame multiplier. The original application frametime graph remains unchanged. Average and 1% low use display data; the percentile covers the provider's recent frame buffer. FPS overlay... applies or restores a separate layout; Windows may request administrator permission for the protected RTSS folder. This is an overlay setting, separate from game DLL removal.\r\n\r\nGame opened / DLL initialized is not proof of frame generation. Yes is your observation. Actual interpolation, image quality and input latency need gameplay validation. On an anti-cheat error, stop and restore. This tool does not bypass protection.\r\n\r\nLocal data:\r\n" + flow.Store.Root +
                    "\r\n\r\nCREDITS / SOURCES\r\nmaiki74: product direction and game testing. OpenAI Codex: app, native Vulkan layer and integration.\r\nItsAdeline / NVSmooth30: existing prepared and original images, with separate full-file hashes. The previous six-byte patch at RVA 0x80A0 is a binary patch, not decompilation or recompilation.\r\nhttps://github.com/ItsAdeline/NVSmooth30\r\nThirteenAG / Ultimate ASI Loader v10.0.0, MIT.\r\nhttps://github.com/ThirteenAG/Ultimate-ASI-Loader\r\nsdli1995 / DLSSG for SM86: owner's local 0.3.5 / 310.9 and 310.1 builds.\r\nhttps://github.com/sdli1995/dlssg_for_sm86\r\nKhronos / Vulkan-Headers v1.4.351, Apache-2.0 OR MIT; native source: native/vulkan/framegen_vulkan_layer.c.\r\nhttps://github.com/KhronosGroup/Vulkan-Headers/tree/v1.4.351\r\nRTSS / Unwinder: installed PresentMon provider and sample layout. Intel PresentMon: presentation telemetry.\r\nhttps://github.com/GameTechDev/PresentMon\r\nSpartanPort: normal game boot preparation.\r\nhttps://github.com/FelipePrado0/SpartanPort\r\nMicrosoft: Windows / .NET APIs. NVIDIA: installed proprietary driver components, models and runtime technologies. Driver DLLs are not included.\r\n\r\nSpartanPort local feedback: v1 opened at 72 FPS rather than the user's usual 120; v2 was described as smooth. A separate PresentMon read later reported about 145 displayed FPS, with varying subsequent samples. The main-app launch and real generated-frame multiplier still need user validation.\r\n\r\nThird-party rights remain with their authors. Project: https://github.com/maiki74/NVSmooth30-External-Framegen\r\n\r\n";
                using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FramegenLab.ThirdPartyNotices"))
                    if (stream != null) using (StreamReader reader = new StreamReader(stream)) text.AppendText(reader.ReadToEnd());
                text.Text = "CURRENT GAME DIAGNOSIS\r\nLauncher: " + (flow.State.LaunchExecutable ?? flow.State.Game) +
                    "\r\nRenderer: " + flow.State.Game + "\r\n" + flow.State.TargetEvidence + "\r\n" + catalog.GraphicsSummary +
                    "\r\n\r\n" + flow.Diagnosis() + "\r\n\r\n" + text.Text;
                using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("FramegenLab.DxvkLicense"))
                    if (stream != null) using (StreamReader reader = new StreamReader(stream)) text.AppendText("\r\n\r\nDXVK 3.1.1 / doitsujin and contributors\r\nhttps://github.com/doitsujin/dxvk/tree/v3.1.1\r\n" + reader.ReadToEnd());
                details.Controls.Add(text);
                Button logs = Button("Open local logs", false); logs.Dock = DockStyle.Bottom; logs.Click += delegate { Process.Start("explorer.exe", "\"" + flow.Store.Root + "\""); }; details.Controls.Add(logs);
                details.ShowDialog(this);
            }
        }
    }
}
