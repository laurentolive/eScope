using ModelContextProtocol.Server;
using System.ComponentModel;

namespace eScope.Automation.Tools
{
    // acquisition: the eScope recording (Rec button), applied to the current trace
    [McpServerToolType]
    public class RecordingTools
    {
        const string SummaryHelp =
            "Returns per channel the sample count, the samples added by this recording, the lost samples and the " +
            "device timeouts, plus warnings (device not connected...).";

        readonly AutomationService svc;

        public RecordingTools(AutomationService svc) => this.svc = svc;

        [McpServerTool(Name = "get_recording_state", ReadOnly = true, Idempotent = true, OpenWorld = false)]
        [Description("Recording state: running or not, recorded (current) trace, elapsed time, acquire running.")]
        public Task<RecordingState> GetRecordingState() => svc.GetRecordingState();

        [McpServerTool(Name = "acquire", ReadOnly = false, Idempotent = false, OpenWorld = true)]
        [Description("Records the channels of a trace during duration_ms, then stops: the call returns at the end. " +
                     "The trace becomes the current one. Samples are appended to the existing data unless clear = true " +
                     "(undoable clear). BUSY when a recording is already running. " + SummaryHelp)]
        public Task<RecordingSummary> Acquire(
            [Description("Recording duration in ms (1 to 600000; longer: start_recording / stop_recording).")] int duration_ms,
            [Description(TraceTools.TraceArg)] string? trace = null,
            [Description("Delete the data of the trace before recording.")] bool clear = false,
            CancellationToken cancellationToken = default)
            => svc.Acquire(trace, duration_ms, clear, cancellationToken);

        [McpServerTool(Name = "start_recording", ReadOnly = false, Idempotent = true, OpenWorld = true)]
        [Description("Starts recording the channels of a trace, which becomes the current one, until stop_recording " +
                     "(or the user stops it). The current trace cannot change while recording. " +
                     "Data is appended to the existing samples.")]
        public Task<RecordingSummary> StartRecording([Description(TraceTools.TraceArg)] string? trace = null)
            => svc.StartRecording(trace);

        [McpServerTool(Name = "stop_recording", ReadOnly = false, Idempotent = true, OpenWorld = true)]
        [Description("Stops the recording. " + SummaryHelp)]
        public Task<RecordingSummary> StopRecording() => svc.StopRecording();
    }
}
