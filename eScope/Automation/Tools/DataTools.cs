using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    [McpServerToolType]
    public class DataTools
    {
        const string ChannelsArg = "Channel names, all the channels of the trace when omitted.";
        const string RangeArg = "Time range: 'all' (default), 'selection' (selection on the plot) or 'view' (visible part).";
        const string FromArg = "Start of the range in ms, overrides the start given by range.";
        const string ToArg = "End of the range in ms, overrides the end given by range.";

        readonly AutomationService svc;

        public DataTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "get_channel_data", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Samples of channels over a time range: time (ms) and value (physical value, as plotted), " +
                     "optionally the raw value read on the target, and the gaps (lost frames, device timeouts). " +
                     "Every sample of the range is returned: restrict the range for long recordings.")]
        public Task<List<ChannelData>> GetChannelData(
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(ChannelsArg)] string[]? channels = null,
            [Description(RangeArg)] string range = "all",
            [Description(FromArg)] double? from_ms = null,
            [Description(ToArg)] double? to_ms = null,
            [Description("Also return the raw target values (device channels).")] bool include_raw = false)
            => svc.GetChannelData(trace, channels, range, from_ms, to_ms, include_raw);

        [McpServerTool(Name = "get_value_at", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Value of channels at a given time: nearest sample (null outside the recorded data).")]
        public Task<List<ChannelValue>> GetValueAt(
            [Description("Time in ms.")] double time_ms,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(ChannelsArg)] string[]? channels = null)
            => svc.GetValueAt(trace, time_ms, channels);

        [McpServerTool(Name = "analyze", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Signal analysis over a time range, per channel (same as the eScope Analyzer): min, max, " +
                     "peak to peak, mean, RMS, standard deviation, frequency / period / duty cycle (mid-level crossings " +
                     "with 10% hysteresis), dominant FFT frequency. Optionally the FFT amplitude spectrum " +
                     "(Hann window, DC removed). null = not computable (flat signal, too few edges).")]
        public Task<List<ChannelAnalysis>> Analyze(
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(ChannelsArg)] string[]? channels = null,
            [Description(RangeArg)] string range = "all",
            [Description(FromArg)] double? from_ms = null,
            [Description(ToArg)] double? to_ms = null,
            [Description("Also return the amplitude spectrum.")] bool fft = false,
            [Description("Maximum number of spectrum points (bins grouped by maximum, peaks kept).")] int fft_points = 256)
            => svc.Analyze(trace, channels, range, from_ms, to_ms, fft, fft_points);

        [McpServerTool(Name = "delete_data", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
        [Description("Deletes the samples of a time range from channels (undoable): the following samples move " +
                     "back in time to close the gap, as Edit > Delete does. A range is required.")]
        public async Task<SamplesRemoved> DeleteData(
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(ChannelsArg)] string[]? channels = null,
            [Description("Time range: 'selection' or 'view', or give from_ms / to_ms.")] string? range = null,
            [Description(FromArg)] double? from_ms = null,
            [Description(ToArg)] double? to_ms = null)
            => new(await svc.DeleteData(trace, channels, range, from_ms, to_ms, all: false));

        [McpServerTool(Name = "clear_data", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
        [Description("Deletes all the samples of channels (undoable), e.g. before a new acquisition.")]
        public async Task<SamplesRemoved> ClearData(
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(ChannelsArg)] string[]? channels = null)
            => new(await svc.DeleteData(trace, channels, null, null, null, all: true));

        [McpServerTool(Name = "find_edges", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Times (ms) where a channel crosses a threshold, rising and/or falling, with hysteresis " +
                     "against noise. Crossing times are interpolated between samples.")]
        public Task<EdgeList> FindEdges(
            [Description("Channel name.")] string channel,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description(RangeArg)] string range = "all",
            [Description(FromArg)] double? from_ms = null,
            [Description(ToArg)] double? to_ms = null,
            [Description("Threshold, mid-level between min and max of the range when omitted.")] double? threshold = null,
            [Description("Hysteresis around the threshold, 10% of peak to peak when omitted.")] double? hysteresis = null,
            [Description("'rising', 'falling' or 'both'.")] string direction = "both")
            => svc.FindEdges(trace, channel, range, from_ms, to_ms, threshold, hysteresis, direction);
    }
}
