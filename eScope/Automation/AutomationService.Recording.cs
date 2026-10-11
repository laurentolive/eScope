using eScope.Plot;
using System.Diagnostics;

namespace eScope.Automation
{
    // acquisition: the recording of eScope (Rec button, Space), applied to the current trace
    public partial class AutomationService
    {
        public const int MaxAcquireMs = 600_000;

        bool acquiring; // acquire running: recording state and current trace locked (UI thread)
        // sample count of each channel when the API started the recording, for the new_* counts;
        // valid for that recording only (the user may stop and start again from the window)
        Dictionary<ChannelCtrl, int>? baseline;
        Dictionary<DeviceCtrl, long>? baselineFrames; // frames received by each device at that time
        int baselineRecord = -1;

        void CheckNotAcquiring()
        {
            if (acquiring)
                throw new AutomationException(AutomationError.Busy, "an acquire is running");
        }

        public Task<RecordingState> GetRecordingState() => ui.InvokeAsync(() => new RecordingState(
            scope.IsRecording,
            scope.GetCurrentTrace()?.Data.Name,
            scope.RecordElapsed?.TotalMilliseconds,
            acquiring));

        public Task<RecordingSummary> StartRecording(string? trace) => ui.InvokeAsync(() =>
        {
            CheckNotAcquiring();
            var trc = StartOn(trace);
            return Summary(trc, stoppedByUser: false, durationMs: null);
        });

        // frames already received are processed by the next device ticks (50 ms): counted before the summary
        const int DrainMs = 200;
        // devices connect when the recording starts (TCP / RTT bridge): the acquire duration starts once connected
        const int ConnectTimeoutMs = 3000;

        public async Task<RecordingSummary> StopRecording()
        {
            var (trc, duration) = await ui.InvokeAsync(() =>
            {
                CheckNotAcquiring();
                var t = scope.GetCurrentTrace()
                    ?? throw new AutomationException(AutomationError.NotFound, "no trace open");
                double? elapsed = scope.RecordElapsed?.TotalMilliseconds;
                if (scope.IsRecording) scope.ToggleRecord();
                return (t, elapsed);
            });
            await Task.Delay(DrainMs);
            return await ui.InvokeAsync(() =>
            {
                var summary = Summary(trc, stoppedByUser: false, durationMs: duration);
                baseline = null;
                return summary;
            });
        }

        // records for duration_ms then stops; the request waits for the end
        public async Task<RecordingSummary> Acquire(string? trace, int durationMs, bool clear, CancellationToken ct)
        {
            if (durationMs < 1 || durationMs > MaxAcquireMs)
                throw new AutomationException(AutomationError.InvalidArgument,
                    $"duration_ms: 1 to {MaxAcquireMs} ms (longer: start_recording / stop_recording)");

            var trc = await ui.InvokeAsync(() =>
            {
                CheckNotAcquiring();
                if (scope.IsRecording)
                    throw new AutomationException(AutomationError.Busy, "a recording is running: stop_recording first");
                var t = ResolveTrace(trace);
                if (clear) ClearAll(t);
                t = StartOn(t.Data.Name);
                acquiring = true;
                return t;
            });

            var watch = new Stopwatch();
            bool stoppedByUser = false;
            var warnings = new List<string>();
            try
            {
                // wait for the devices of the channels to connect, then the duration starts
                var connect = Stopwatch.StartNew();
                while (!await ui.InvokeAsync(() => DevicesStreaming(trc)))
                {
                    if (connect.ElapsedMilliseconds >= ConnectTimeoutMs)
                    {
                        warnings.Add($"no frame from the devices after {ConnectTimeoutMs} ms: recorded anyway");
                        break;
                    }
                    await Task.Delay(20, ct);
                }
                watch.Start();
                // poll the recording state: the user may stop it from the UI
                while (watch.ElapsedMilliseconds < durationMs)
                {
                    await Task.Delay((int)Math.Min(100, durationMs - watch.ElapsedMilliseconds), ct);
                    if (!await ui.InvokeAsync(() => scope.IsRecording))
                    {
                        stoppedByUser = true;
                        break;
                    }
                }
                watch.Stop();
            }
            finally
            {
                // stopped also when the request is cancelled or fails
                await ui.InvokeAsync(() =>
                {
                    acquiring = false;
                    if (scope.IsRecording && scope.GetCurrentTrace() == trc) scope.ToggleRecord();
                    return true;
                });
            }
            await Task.Delay(DrainMs, CancellationToken.None);
            return await ui.InvokeAsync(() =>
            {
                var summary = Summary(trc, stoppedByUser, watch.Elapsed.TotalMilliseconds, warnings);
                baseline = null;
                return summary;
            });
        }

        // devices feeding a channel of the trace (missing devices: reported by the summary)
        List<DeviceCtrl> TraceDevices(TraceCtrl trc) => trc.GetSignalList()
            .Where(c => c.SignalBase.hasDevice)
            .Select(c => scope.Devices.FirstOrDefault(d => d.Data.Name == c.SignalBase.Device))
            .OfType<DeviceCtrl>()
            .Distinct()
            .ToList();

        // true when every device of the trace has received a frame since the recording started (UI thread).
        // Protocol.IsConnected is set when the port opens, before the first frame
        bool DevicesStreaming(TraceCtrl trc) => baselineFrames != null
            && TraceDevices(trc).All(d => d.FrameCount > baselineFrames.GetValueOrDefault(d));

        // undoable clear of every channel of the trace, before a new acquisition
        static void ClearAll(TraceCtrl trc)
            => RemoveSamples(trc.GetSignalList(), double.NegativeInfinity, double.PositiveInfinity, "Clear data");

        // selects the trace and starts the recording if not running (UI thread)
        TraceCtrl StartOn(string? trace)
        {
            var trc = ResolveTrace(trace);
            if (scope.GetCurrentTrace() != trc && !scope.SelectTrace(trc))
                throw new AutomationException(AutomationError.Busy,
                    $"recording on trace '{scope.GetCurrentTrace()?.Data.Name}': stop it before recording another trace");
            if (!scope.IsRecording)
            {
                baseline = trc.GetSignalList().ToDictionary(c => c, c => c.Y.Count);
                baselineFrames = TraceDevices(trc).ToDictionary(d => d, d => d.FrameCount);
                scope.ToggleRecord();
                baselineRecord = scope.RecordCount;
            }
            return trc;
        }

        RecordingSummary Summary(TraceCtrl trc, bool stoppedByUser, double? durationMs, List<string>? extraWarnings = null)
        {
            var b = baselineRecord == scope.RecordCount ? baseline : null;
            var channels = trc.GetSignalList().Select(c =>
            {
                int from = b != null && b.TryGetValue(c, out int n) ? n : 0;
                var lost = c.SignalBase.Lost.Where(l => l.Index >= from).ToList();
                return new ChannelAcquisition(
                    c.SignalBase.Name,
                    c.Y.Count,
                    b != null && b.ContainsKey(c) ? c.Y.Count - from : null,
                    lost.Where(l => !l.Timeout).Sum(l => l.Count),
                    lost.Count(l => l.Timeout));
            }).ToList();

            var warnings = extraWarnings ?? new List<string>();
            var names = trc.GetSignalList()
                .Where(c => c.SignalBase.hasDevice)
                .Select(c => c.SignalBase.Device)
                .Distinct();
            foreach (string name in names)
            {
                var dev = scope.Devices.FirstOrDefault(d => d.Data.Name == name);
                if (dev == null)
                    warnings.Add($"device '{name}' is not configured in the workspace: its channels are not acquired");
                // recording done (stop / acquire): frames received from the device during it
                else if (durationMs != null && baselineFrames != null && b != null
                         && dev.FrameCount == baselineFrames.GetValueOrDefault(dev))
                    warnings.Add($"no frame received from device '{name}' ({dev.Data.Com}): check the connection and the firmware");
            }
            if (channels.Count == 0)
                warnings.Add("the trace has no channel");
            if (stoppedByUser)
                warnings.Add("the recording was stopped from the eScope window before the end");

            return new RecordingSummary(trc.Data.Name, scope.IsRecording, durationMs, stoppedByUser, channels, warnings);
        }
    }
}
