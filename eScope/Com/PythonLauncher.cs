using System;
using System.Diagnostics;

namespace eScope.Com
{
    // Windows only ships "python"/"py"; most Linux distros only ship "python3".
    // Resolve once and reuse everywhere a python interpreter needs to be launched.
    public static class PythonLauncher
    {
        private static readonly Lazy<string> _executable = new(Resolve);

        public static string Executable => _executable.Value;

        private static string Resolve()
        {
            string[] candidates = OperatingSystem.IsWindows()
                ? new[] { "python", "python3" }
                : new[] { "python3", "python" };

            foreach (string candidate in candidates)
            {
                if (IsAvailable(candidate))
                    return candidate;
            }
            return candidates[0];
        }

        private static bool IsAvailable(string name)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo(name, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (proc == null) return false;
                proc.WaitForExit(1000);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
