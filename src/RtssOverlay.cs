// C# 5 · RtssOverlay.cs · reversible displayed-FPS overlay settings · Windows · .NET Framework 4.8
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FramegenLab
{
    [DataContract] public sealed class OverlayInstall
    {
        [DataMember] public string OriginalLayout;
        [DataMember] public string InstalledHash;
        [DataMember] public bool Enabled;
    }
    public static class RtssOverlay
    {
        static readonly string Install=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"RivaTuner Statistics Server");
        static readonly string Config=Path.Combine(Install,@"Plugins\Client\OverlayEditor.cfg");
        static readonly string Layouts=Path.Combine(Install,@"Plugins\Client\Overlays");
        const string ManagedName="FramegenLab.Displayed.ovl";
        public static bool Available {get{return File.Exists(Path.Combine(Install,"RTSS.exe"))&&File.Exists(Config);}}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetPrivateProfileString(string section,string key,string fallback,StringBuilder value,int count,string file);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool WritePrivateProfileString(string section,string key,string value,string file);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
        static string ReadLayout(){var text=new StringBuilder(32768);GetPrivateProfileString("Settings","Layout","default.ovl",text,text.Capacity,Config);return text.ToString();}
        static void Choose(string name){if(!WritePrivateProfileString("Settings","Layout",name,Config))throw new IOException("RTSS layout selection could not be saved.");}
        static string Root {get{return Path.Combine(Disk.DefaultRoot,"rtss-overlay");}}
        static string Journal {get{return Path.Combine(Root,"installation.json");}}
        static void Stop()
        {
            var processes=Process.GetProcessesByName("RTSS");
            try{
                if(processes.Length==0)return;
                IntPtr window=FindWindow(null,"RTSS");if(window==IntPtr.Zero)window=FindWindow(null,"RivaTunerStatisticsServer");
                if(window==IntPtr.Zero||!PostMessage(window,0x12,IntPtr.Zero,IntPtr.Zero))throw new IOException("RTSS could not be closed gracefully. Close RTSS and try again.");
                foreach(Process p in processes)if(!p.WaitForExit(12000))throw new IOException("RTSS did not exit. It will not be force-terminated.");
            }finally{foreach(Process p in processes)p.Dispose();}
        }
        static void Start(){using(Process p=Process.Start(new ProcessStartInfo(Path.Combine(Install,"RTSS.exe")){WorkingDirectory=Install,UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})){}}
        public static string Transform(string text)
        {
            if(!text.Contains("Text=<FR>"))throw new IOException("The selected layout does not use the standard FPS field. Keep your custom layout and select a compatible one in RTSS.");
            Match general=Regex.Match(text,@"(?ms)^\[General\]\r?\n(?<body>.*?)(?=^\[|\z)");
            Match count=Regex.Match(general.Groups["body"].Value,@"(?m)^Sources=(\d+)\r?$");
            int n;if(!general.Success||!count.Success||!Int32.TryParse(count.Groups[1].Value,out n)||n<0||n>1000)throw new IOException("Unrecognized RTSS layout source table.");
            string prefix="FramegenDisplayed";
            if(text.Contains("Name="+prefix))throw new IOException("This layout already contains generated output counters. Restore the original layout first.");
            string body=general.Groups["body"].Value;
            string changed=Regex.Replace(body,@"(?m)^Sources=\d+\r?$","Sources="+(n+4));
            text=text.Substring(0,general.Groups["body"].Index)+changed+text.Substring(general.Groups["body"].Index+body.Length);
            text=Regex.Replace(text,@"(?m)^Text=<FR>\r?$","Text=%"+prefix+"FPS%");
            text=Regex.Replace(text,@"(?m)^Text=<FR10L>\r?$","Text=%"+prefix+"Low%");
            text=Regex.Replace(text,@"(?m)^Text=<FRAVG>\r?$","Text=%"+prefix+"Average%");
            // The original frametime graph is deliberately untouched: it remains application-side.
            var sources=new StringBuilder();
            Action<int,string,string,string,string> add=delegate(int index,string name,string provider,string id,string formula){sources.Append("[Source"+index+"]\nName="+name+"\nUnits="+(name.EndsWith("Time",StringComparison.Ordinal)?"ms":"FPS")+"\nFormat=%.0f\nFormula="+formula+"\nProvider="+provider+"\nID="+id+"\n");};
            add(n,prefix+"FPS","PresentMon","FramerateDisplayed","");
            add(n+1,prefix+"Time","PresentMon","msBetweenDisplayChange","");
            add(n+2,prefix+"Low","HAL","Stub","1000/percentile("+prefix+"Time,99)");
            add(n+3,prefix+"Average","HAL","Stub","statavg("+prefix+"FPS)");
            int layer=text.IndexOf("[Layer0]",StringComparison.Ordinal);if(layer<0)throw new IOException("The RTSS layout has no layers.");
            text=text.Insert(layer,sources.ToString());
            Match settings=Regex.Match(text,@"(?ms)^\[Settings\]\r?\n(?<body>.*?)(?=^\[|\z)");
            if(!settings.Success)throw new IOException("The RTSS layout settings were not found.");
            body=settings.Groups["body"].Value;
            Match env=Regex.Match(body,@"(?m)^EnvVars=([^\r\n]*)");
            string vars=env.Success?env.Groups[1].Value:"";
            vars=Regex.Replace(vars,@"(?:^|;)(?:PM_RefreshPeriod|PM_DisplayDelay)=[^;]*","").Trim(';');
            vars+=(vars.Length>0?";":"")+"PM_RefreshPeriod=1000;PM_DisplayDelay=0";
            changed=env.Success?body.Substring(0,env.Index)+"EnvVars="+vars+body.Substring(env.Index+env.Length):"EnvVars="+vars+"\n"+body;
            text=text.Substring(0,settings.Groups["body"].Index)+changed+text.Substring(settings.Groups["body"].Index+body.Length);
            return Regex.Replace(text,@"\r?\n","\r\n");
        }
        public static int ApplyElevated(bool enable)
        {
            Directory.CreateDirectory(Root);Disk.NoLinks(Root);
            string result=Path.Combine(Root,"result.txt"),previous=null;bool stopped=false;
            try{
                if(!Available)throw new IOException("RTSS was not found in its default installation folder.");
                previous=ReadLayout();string target=Path.Combine(Layouts,ManagedName);Disk.NoLinks(Config);Disk.NoLinks(target);
                OverlayInstall state=File.Exists(Journal)?Disk.Read<OverlayInstall>(Journal):null;
                if(enable){
                    if(state!=null&&state.Enabled&&previous==ManagedName){if(Disk.Hash(target)!=state.InstalledHash)throw new IOException("The app's output layout changed outside the app. Keep it and restore manually in RTSS.");File.WriteAllText(result,"Displayed FPS is already enabled. Refresh: 1 second. Original frametime graph preserved.");return 0;}
                    string original=previous;
                    // Recognize this chat's earlier local preview and return to its untouched source layout.
                    if(original=="NV.output-fps.ovl"||original=="NV.output-fps-slow.ovl")original="NV.ovl";
                    string source=Path.IsPathRooted(original)?original:Path.Combine(Layouts,original);Disk.NoLinks(source);
                    if(!String.Equals(Path.GetExtension(source),".ovl",StringComparison.OrdinalIgnoreCase))throw new IOException("The RTSS layout file is not an .ovl file.");
                    if(File.Exists(target)&&(state==null||Disk.Hash(target)!=state.InstalledHash))throw new IOException("An unrelated file already uses the generated layout name. It will not be overwritten.");
                    byte[] raw=File.ReadAllBytes(source),updated=Encoding.Default.GetBytes(Transform(Encoding.Default.GetString(raw)));
                    Disk.AtomicBytes(Path.Combine(Root,"source-layout.ovl"),raw);Disk.AtomicCopy(Config,Path.Combine(Root,"OverlayEditor.before.cfg"));
                    Stop();stopped=true;Disk.AtomicBytes(target,updated);
                    state=new OverlayInstall{OriginalLayout=original,InstalledHash=Disk.Hash(target),Enabled=true};Disk.Write(Journal,state);
                    Choose(ManagedName);
                }else{
                    if(state==null||!state.Enabled){
                        if(previous=="NV.output-fps.ovl"||previous=="NV.output-fps-slow.ovl"){
                            if(!File.Exists(Path.Combine(Layouts,"NV.ovl")))throw new IOException("The original NV.ovl layout was not found.");
                            Stop();stopped=true;Choose("NV.ovl");Start();stopped=false;File.WriteAllText(result,"Original RTSS layout restored.");return 0;
                        }
                        File.WriteAllText(result,"No overlay setting installed by this app needs restoration.");return 0;
                    }
                    if(previous!=ManagedName||!File.Exists(target)||Disk.Hash(target)!=state.InstalledHash)throw new IOException("The selected RTSS layout changed outside the app. It will not be overwritten.");
                    Stop();stopped=true;Choose(state.OriginalLayout);state.Enabled=false;Disk.Write(Journal,state);
                }
                Start();stopped=false;
                File.WriteAllText(result,enable?"Displayed FPS enabled. Refresh: 1 second. Original frametime graph preserved.":"Original RTSS layout restored.");return 0;
            }catch(Exception e){
                if(stopped){try{if(previous!=null)Choose(previous);Start();}catch(Exception recovery){e=new IOException(e.Message+" Recovery: "+recovery.Message,e);}}
                File.WriteAllText(result,e.Message);return 1;
            }
        }
        public static void ShowSettings(Form parent)
        {
            using(var dialog=new Form{Text="FPS overlay",ClientSize=new System.Drawing.Size(550,210),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
                bool working=false;dialog.FormClosing+=delegate(object sender,FormClosingEventArgs args){if(working)args.Cancel=true;};
                var info=new Label{Text="Use measured displayed FPS, including frames the driver reports.\r\nFPS updates once per second. The original frametime graph stays unchanged.\r\nWindows may request administrator permission to update RTSS.",Bounds=new System.Drawing.Rectangle(18,16,514,82)};
                var on=new Button{Text="Use displayed FPS",Bounds=new System.Drawing.Rectangle(18,115,220,36),Enabled=Available};
                var off=new Button{Text="Restore original overlay",Bounds=new System.Drawing.Rectangle(254,115,270,36),Enabled=Available};
                var note=new Label{Text="The average and 1% low use the display data; the graph uses application frametimes.",Bounds=new System.Drawing.Rectangle(18,163,514,40)};
                Func<bool,Task> run=async delegate(bool enable){
                    working=true;
                    on.Enabled=off.Enabled=false;
                    try{
                        using(Process p=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--overlay-output "+(enable?"enable":"restore")){Verb="runas",UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden})){
                            await Task.Run(delegate{p.WaitForExit();});
                            string result=Path.Combine(Root,"result.txt");string message=File.Exists(result)?File.ReadAllText(result):"The settings helper did not return a result.";
                            if(p.ExitCode!=0)throw new IOException(message);MessageBox.Show(dialog,message,"FPS overlay",MessageBoxButtons.OK,MessageBoxIcon.Information);
                        }
                    }catch(Exception e){MessageBox.Show(dialog,e.Message,"FPS overlay",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                    finally{working=false;on.Enabled=off.Enabled=Available;}
                };
                on.Click+=async delegate{await run(true);};off.Click+=async delegate{await run(false);};dialog.Controls.AddRange(new Control[]{info,on,off,note});dialog.ShowDialog(parent);
            }
        }
    }
}
