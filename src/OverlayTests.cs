// C# 5 · OverlayTests.cs · preserve layout while changing measured counters · Windows · .NET Framework 4.8
using System;
using System.IO;
namespace FramegenLab
{
    public static class OverlayTests
    {
        public static void Run(Action<bool,string> check)
        {
            string original="[Settings]\r\nEnvVars=Custom=7;PM_RefreshPeriod=100;PM_DisplayDelay=1000\r\nRefreshPeriod=1000\r\n[General]\r\nSources=1\r\nLayers=4\r\n[Source0]\r\nName=CPU usage\r\nProvider=PerfCounter\r\nObjectName=Processor\r\nInstanceName=_Total\r\nCounterName=% Processor Time\r\n[Layer0]\r\nText=<FR>\r\nPositionX=83\r\nTextColor=76B900\r\n[Layer1]\r\nText=<FR10L>\r\n[Layer2]\r\nText=<FRAVG>\r\n[Layer3]\r\nText=<G=Frametime>\r\nPositionX=3\r\n";
            string updated=RtssOverlay.Transform(original);
            check(updated.Contains("PM_RefreshPeriod=1000;PM_DisplayDelay=0")&&updated.Contains("Custom=7")&&!updated.Contains("PM_RefreshPeriod=100;"),"FPS overlay refresh returns to one second while unrelated environment settings survive");
            check(updated.Contains("Text=<G=Frametime>\r\nPositionX=3")&&updated.Contains("PositionX=83\r\nTextColor=76B900"),"The original application frametime graph, positions and colors are preserved");
            check(updated.Contains("Provider=PerfCounter\r\nObjectName=Processor\r\nInstanceName=_Total\r\nCounterName=% Processor Time"),"The existing CPU overlay repair survives displayed-FPS setup");
            check(updated.Contains("ID=FramerateDisplayed")&&updated.Contains("ID=msBetweenDisplayChange")&&updated.Contains("Text=%FramegenDisplayedFPS%")&&updated.Contains("Formula=statavg(FramegenDisplayedFPS)"),"FPS and display statistics use PresentMon telemetry rather than an assumed frame multiplier");
            bool refused=false;try{RtssOverlay.Transform("[General]\nSources=0\n[Layer0]\nText=My custom metric\n");}catch(IOException){refused=true;}
            check(refused,"An unrecognized custom FPS layout is refused rather than silently replaced");
        }
    }
}
