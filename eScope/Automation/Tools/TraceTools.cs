using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    [McpServerToolType]
    public class TraceTools
    {
        internal const string TraceArg = "Trace (plot tab) name, the current trace when omitted.";

        readonly AutomationService svc;

        public TraceTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "list_traces", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Traces (plot tabs): current one, channel count, end of the recorded data, visible time range " +
                     "(view) and selection, in ms.")]
        public Task<List<TraceInfo>> ListTraces() => svc.ListTraces();

        [McpServerTool(Name = "list_channels", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Channels (plotted signals) of a trace: device variable (symbol, address, size, display format) " +
                     "or computed by a C# script, unit, sample count, sampling period and time span in ms, display gain/offset.")]
        public Task<List<ChannelInfo>> ListChannels([Description(TraceArg)] string? trace = null)
            => svc.ListChannels(trace);

        [McpServerTool(Name = "list_annotations", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Plot objects of a trace: time markers, deltas (time spans), text annotations, data markers " +
                     "(horizontal value lines) and the current selection.")]
        public Task<AnnotationList> ListAnnotations([Description(TraceArg)] string? trace = null)
            => svc.ListAnnotations(trace);

        [McpServerTool(Name = "screenshot", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("PNG image of the current trace as displayed (plot and legend), to look at the curves.")]
        public async Task<ContentBlock> Screenshot([Description(TraceArg)] string? trace = null)
            => ImageContentBlock.FromBytes(await svc.Screenshot(trace), "image/png");
    }
}
