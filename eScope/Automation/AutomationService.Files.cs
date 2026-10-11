namespace eScope.Automation
{
    // exports (CSV, PNG) and trace files (.etrace)
    public partial class AutomationService
    {
        // absolute, or relative to the eScope working directory; extension added when missing;
        // an existing file is replaced only with overwrite
        static string OutputPath(string path, string extension, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new AutomationException(AutomationError.InvalidArgument, "empty path");
            string full;
            try
            {
                full = Path.GetFullPath(path.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new AutomationException(AutomationError.InvalidArgument, $"path '{path}': {ex.Message}");
            }
            if (Path.GetExtension(full).Length == 0)
                full += extension;
            string? dir = Path.GetDirectoryName(full);
            if (dir != null && !Directory.Exists(dir))
                throw new AutomationException(AutomationError.NotFound, $"directory '{dir}' does not exist");
            if (File.Exists(full) && !overwrite)
                throw new AutomationException(AutomationError.InvalidArgument, $"'{full}' already exists (overwrite: true to replace it)");
            return full;
        }

        // IO errors of a file operation reported as IO_ERROR
        static T FileOperation<T>(string path, Func<T> operation)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new AutomationException(AutomationError.IoError, $"'{path}': {ex.Message}");
            }
        }

        public Task<FileResult> ExportCsv(string? trace, string path, string? range, double? fromMs, double? toMs, bool overwrite)
            => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var (from, to) = ResolveRange(trc, range, fromMs, toMs);
            string full = OutputPath(path, ".csv", overwrite);
            int rows = FileOperation(full, () => trc.ExportCsv(full, from, to));
            return new FileResult(full, new FileInfo(full).Length, rows);
        });

        // the tab as displayed: only the current trace is laid out on screen
        public Task<FileResult> ExportPng(string? trace, string path, bool overwrite) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            CheckCapturable(trc);
            string full = OutputPath(path, ".png", overwrite);
            FileOperation(full, () => { trc.ExportPng(full); return true; });
            return new FileResult(full, new FileInfo(full).Length);
        });

        public Task<FileResult> SaveTrace(string? trace, string path, bool overwrite) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            string full = OutputPath(path, ".etrace", overwrite);
            FileOperation(full, () => { trc.SaveToFile(full); return true; });
            return new FileResult(full, new FileInfo(full).Length);
        });

        // added as a new trace (unique name), selected unless a recording is running
        public Task<LoadedTrace> LoadTrace(string path) => ui.InvokeAsync(() =>
        {
            string full = Path.GetFullPath(path.Trim());
            if (!File.Exists(full))
                throw new AutomationException(AutomationError.NotFound, $"'{full}' not found");
            Trace data;
            try
            {
                data = FileOperation(full, () => TraceCtrl.LoadFromFile(full));
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
            {
                throw new AutomationException(AutomationError.InvalidArgument, $"'{full}' is not an eScope trace: {ex.Message}");
            }
            var (trc, missing) = scope.AddTraceFromFile(data);
            return new LoadedTrace(trc.Data.Name, full, trc.GetSignalList().Count, missing);
        });
    }
}
