using System.IO;

namespace eScope.Symbol
{
    /// <summary>
    /// Finds the gdb executable to use: the user-configured path if it exists,
    /// otherwise the gdb bundled next to eScope.exe (tools\gdb, see tools\fetch-gdb.ps1),
    /// otherwise a gdb found in PATH.
    /// </summary>
    public static class GdbLocator
    {
        static readonly string[] pathCandidates = { "gdb-multiarch.exe", "arm-none-eabi-gdb.exe", "gdb.exe" };

        public static string BundledPath => Path.Combine(AppContext.BaseDirectory, "tools", "gdb", "bin", "gdb-multiarch.exe");

        public static string? Resolve(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            if (File.Exists(BundledPath))
                return BundledPath;

            string[] dirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string exe in pathCandidates)
            {
                foreach (string dir in dirs)
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim('"'), exe);
                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch (ArgumentException) { } // malformed PATH entry
                }
            }
            return null;
        }
    }
}
