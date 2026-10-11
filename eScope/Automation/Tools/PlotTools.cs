using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    // plot objects (markers, deltas, annotations), view and selection
    [McpServerToolType]
    public class PlotTools
    {
        readonly AutomationService svc;

        public PlotTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "add_marker", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Adds a time marker (vertical line) at a time (undoable).")]
        public async Task<NameResult> AddMarker(
            [Description("Time in ms.")] double time_ms,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Name, 'MarkerN' when omitted.")] string? name = null,
            [Description("Color '#RRGGBB'.")] string? color = null)
            => new(await svc.AddMarker(trace, time_ms, name, color));

        [McpServerTool(Name = "add_delta", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Adds a delta: a time span shown with its duration (undoable).")]
        public Task<DeltaInfo> AddDelta(
            [Description("Start in ms.")] double from_ms,
            [Description("End in ms.")] double to_ms,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Name, 'DeltaN' when omitted.")] string? name = null,
            [Description("Color '#RRGGBB'.")] string? color = null)
            => svc.AddDelta(trace, from_ms, to_ms, name, color);

        [McpServerTool(Name = "add_annotation", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Adds a text pointing at the curve of a channel at a time (undoable). The text is its name: " +
                     "unique in the trace.")]
        public async Task<NameResult> AddAnnotation(
            [Description("Channel the annotation points at.")] string channel,
            [Description("Time in ms.")] double time_ms,
            [Description("Text.")] string text,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Color '#RRGGBB', the channel color when omitted.")] string? color = null)
            => new(await svc.AddAnnotation(trace, channel, time_ms, text, color));

        [McpServerTool(Name = "add_data_marker", ReadOnly = false, Idempotent = false, OpenWorld = false)]
        [Description("Adds a data marker: horizontal line at a value of a channel, e.g. a threshold (undoable).")]
        public async Task<NameResult> AddDataMarker(
            [Description("Channel.")] string channel,
            [Description("Value in channel units.")] double value,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Name, 'DataMarkerN' when omitted.")] string? name = null)
            => new(await svc.AddDataMarker(trace, channel, value, name));

        [McpServerTool(Name = "remove_annotation", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
        [Description("Removes a marker, delta, annotation or data marker by name (undoable).")]
        public async Task<NameResult> RemoveAnnotation(
            [Description("Name (the text for an annotation).")] string name,
            [Description(TraceTools.TraceArg)] string? trace = null)
            => new(await svc.RemoveAnnotation(trace, name));

        [McpServerTool(Name = "set_view", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Shows a time range on the plot (horizontal zoom and position).")]
        public Task<TimeRange> SetView(
            [Description("Start in ms.")] double from_ms,
            [Description("End in ms.")] double to_ms,
            [Description(TraceTools.TraceArg)] string? trace = null)
            => svc.SetView(trace, from_ms, to_ms);

        [McpServerTool(Name = "zoom_fit", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Shows the whole recording on the plot.")]
        public Task<TimeRange> ZoomFit([Description(TraceTools.TraceArg)] string? trace = null)
            => svc.ZoomFit(trace);

        [McpServerTool(Name = "set_selection", ReadOnly = false, Idempotent = true, OpenWorld = false)]
        [Description("Selects a time range on the plot (used by range = 'selection' and by the user's Analyzer, " +
                     "copy, delete). Without from_ms and to_ms the selection is removed.")]
        public Task<TimeRange?> SetSelection(
            [Description("Start in ms.")] double? from_ms = null,
            [Description("End in ms.")] double? to_ms = null,
            [Description(TraceTools.TraceArg)] string? trace = null)
            => svc.SetSelection(trace, from_ms, to_ms);
    }
}
