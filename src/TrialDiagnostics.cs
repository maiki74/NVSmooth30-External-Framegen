// C# 5 · TrialDiagnostics.cs · evidence-based failure explanations · Windows x64 · .NET Framework 4.8
using System;
using System.IO;

namespace FramegenLab
{
    public static class TrialDiagnostics
    {
        public static string Status(Trial trial)
        {
            if (trial == null) return "No test has run yet.";
            if (!String.IsNullOrEmpty(trial.BackendObservation)) return trial.BackendObservation;
            if (trial.Pid == 0)
                return trial.State == "Restored" ? "Renderer was never observed. Check the launcher or game error; framegen was not verified." :
                    "Waiting for the renderer: " + Path.GetFileName(trial.Game) + ".";
            if (trial.ModuleObservation == "Candidate path observed in module list")
                return "Test loader was observed; backend initialization is unconfirmed. Confirm visually or press F2 for the next option.";
            if (trial.ModuleObservation != null && trial.ModuleObservation.StartsWith("Module list unavailable", StringComparison.Ordinal))
                return "The game's module list could not be read. DLL loading and framegen remain unverified.";
            if (trial.ModuleObservation != null && trial.ModuleObservation.Contains("not observed"))
                return "Renderer observed; test DLL was absent from the module sample. Press F2 to try the next loader.";
            return "Renderer observed. Checking the test DLL and backend; opening the game does not confirm framegen.";
        }
        public static string Report(Trial trial)
        {
            return "FRAMEGEN TEST DIAGNOSIS\r\n" + Status(trial) + "\r\n\r\nRenderer: " + trial.Game +
                "\r\nRenderer SHA256: " + trial.GameHash + "\r\nLauncher: " + (trial.LaunchExecutable ?? trial.Game) +
                "\r\nLauncher SHA256: " + trial.LaunchHash + "\r\nRenderer PID: " + trial.Pid +
                "\r\nRenderer exit: " + trial.ExitCode + "\r\nLauncher exit: " + trial.LauncherExitCode +
                "\r\nTest target: " + trial.TargetName + "\r\nModule sample: " + trial.ModuleObservation +
                "\r\nObserved graphics modules (not proof of the active API): " + trial.GraphicsModules +
                "\r\nBackend: " + trial.BackendObservation + "\r\nUser result: " + trial.Outcome +
                "\r\n\r\nA loaded DLL, graphics module or successful initialization does not prove generated frames.\r\n";
        }
    }
}
