using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    // exports and trace files, on the disk of the eScope machine
    [McpServerToolType]
    public class FileTools
    {
        const string PathHelp = "File path on the eScope machine: absolute, or relative to the eScope working directory. " +
                                "The extension is added when missing.";
        const string OverwriteArg = "Replace the file if it exists (refused otherwise).";

        readonly AutomationService svc;

        public FileTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "export_csv", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Exports the channels of a trace to CSV (as File > Export): per channel the columns " +
                     "'<name> Time' (ms), '<name> Data [unit]' (plotted value) and '<name> Raw' (target value, device channels); " +
                     "one row per sample index, invariant culture (',' separator, '.' decimal). Use it for long recordings.")]
        public Task<FileResult> ExportCsv(
            [Description(PathHelp + " Extension .csv.")] string path,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Time range: 'all' (default), 'selection' or 'view'.")] string range = "all",
            [Description("Start of the range in ms, overrides the start given by range.")] double? from_ms = null,
            [Description("End of the range in ms, overrides the end given by range.")] double? to_ms = null,
            [Description(OverwriteArg)] bool overwrite = false)
            => svc.ExportCsv(trace, path, range, from_ms, to_ms, overwrite);

        [McpServerTool(Name = "export_png", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Saves the current trace as displayed (plot and legend) to a PNG file. To look at it, screenshot " +
                     "returns the image directly.")]
        public Task<FileResult> ExportPng(
            [Description(PathHelp + " Extension .png.")] string path,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(OverwriteArg)] bool overwrite = false)
            => svc.ExportPng(trace, path, overwrite);

        [McpServerTool(Name = "save_trace", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Saves a trace (channels, data, markers...) to an .etrace file (gzipped JSON of the trace model), " +
                     "which load_trace or File > Open Trace reopens.")]
        public Task<FileResult> SaveTrace(
            [Description(PathHelp + " Extension .etrace.")] string path,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(OverwriteArg)] bool overwrite = false)
            => svc.SaveTrace(trace, path, overwrite);

        [McpServerTool(Name = "load_trace", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Opens an .etrace file as a new trace (name made unique), which becomes the current trace except " +
                     "during a recording. missing_devices: devices used by its channels and absent from the workspace " +
                     "(data shown, no acquisition).")]
        public Task<LoadedTrace> LoadTrace([Description(PathHelp)] string path)
            => svc.LoadTrace(path);
    }
}
