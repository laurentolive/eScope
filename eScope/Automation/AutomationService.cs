using eScope.Plot;

namespace eScope.Automation
{
    // Facade of the automation API, shared by MCP and REST: each operation runs on the UI thread.
    // One partial file per domain (AutomationService.*.cs).
    public partial class AutomationService
    {
        readonly eScope scope;
        readonly IUiDispatcher ui;

        public AutomationService(eScope scope, IUiDispatcher ui)
        {
            this.scope = scope;
            this.ui = ui;
        }

        public Task<WorkspaceInfo> GetWorkspace() => ui.InvokeAsync(() =>
        {
            TraceCtrl? current = scope.GetCurrentTrace();
            var devices = scope.Devices.Select(ToDeviceInfo).ToList();
            var traces = eScope.Traces.Select(t => new TraceInfo(
                t.Data.Name,
                t == current,
                t.GetSignalList().Count)).ToList();
            return new WorkspaceInfo(eScope.AppVersion, scope.IsRecording, current?.Data.Name, devices, traces);
        });

        // --- name resolution (UI thread) ---

        // null: current trace
        TraceCtrl ResolveTrace(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return scope.GetCurrentTrace()
                    ?? throw new AutomationException(AutomationError.NotFound, "no trace open");
            return eScope.Traces.FirstOrDefault(t => t.Data.Name == name)
                ?? throw new AutomationException(AutomationError.NotFound,
                    $"unknown trace '{name}' (traces: {string.Join(", ", eScope.Traces.Select(t => t.Data.Name))})");
        }

        // null: the only device of the workspace
        DeviceCtrl ResolveDevice(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return scope.Devices.Count switch
                {
                    1 => scope.Devices[0],
                    0 => throw new AutomationException(AutomationError.NotFound, "no device in the workspace"),
                    _ => throw new AutomationException(AutomationError.InvalidArgument,
                        $"several devices, 'device' is required ({DeviceNames()})"),
                };
            }
            return scope.Devices.FirstOrDefault(d => d.Data.Name == name)
                ?? throw new AutomationException(AutomationError.NotFound, $"unknown device '{name}' ({DeviceNames()})");
        }

        string DeviceNames() => "devices: " + string.Join(", ", scope.Devices.Select(d => d.Data.Name));

        static ChannelCtrl ResolveChannel(TraceCtrl trc, string name)
            => trc.GetSignalFromName(name)
                ?? throw new AutomationException(AutomationError.NotFound,
                    $"unknown channel '{name}' in trace '{trc.Data.Name}' (channels: {string.Join(", ", trc.GetSignalList().Select(c => c.SignalBase.Name))})");

        // null or empty: all the channels of the trace
        static List<ChannelCtrl> ResolveChannels(TraceCtrl trc, string[]? names)
            => names == null || names.Length == 0
                ? trc.GetSignalList()
                : names.Select(n => ResolveChannel(trc, n)).ToList();

        // range: "all" (default), "selection" or "view"; from_ms / to_ms override the bounds
        static (double From, double To) ResolveRange(TraceCtrl trc, string? range, double? fromMs, double? toMs)
        {
            (double from, double to) = (range ?? "all").ToLowerInvariant() switch
            {
                "all" => (double.NegativeInfinity, double.PositiveInfinity),
                "view" => (trc.Data.XPos, trc.Data.XPos + trc.Data.XWidth),
                "selection" => Selection(trc) is TimeRange s ? (s.FromMs, s.ToMs)
                    : throw new AutomationException(AutomationError.InvalidArgument, "no selection on the trace"),
                _ => throw new AutomationException(AutomationError.InvalidArgument,
                    $"range '{range}': expected all, selection or view"),
            };
            from = fromMs ?? from;
            to = toMs ?? to;
            if (to < from)
                throw new AutomationException(AutomationError.InvalidArgument, "to_ms is before from_ms");
            return (from, to);
        }

        static TimeRange? Selection(TraceCtrl trc)
        {
            var sel = trc.Plot.dataSelector;
            return sel.IsVisible() ? new TimeRange(sel.Min(), sel.Max()) : null;
        }

        static TimeRange View(TraceCtrl trc) => new(trc.Data.XPos, trc.Data.XPos + trc.Data.XWidth);

        // sample i of a channel is plotted at XCreation + XOffset + i * Period
        static double StartTime(ChannelCtrl c) => c.SignalBase.XCreation + c.SignalBase.XOffset;

        // indexes of the displayed samples lying in [from, to], empty range: first > last
        static (int First, int Last) SampleRange(ChannelCtrl c, double from, double to)
        {
            int count = c.Y.Count;
            double period = c.Period;
            if (count == 0 || period <= 0) return (0, -1);
            double t0 = StartTime(c);
            int first = double.IsNegativeInfinity(from) ? 0 : (int)Math.Max(0, Math.Ceiling((from - t0) / period - 1e-9));
            int last = double.IsPositiveInfinity(to) ? count - 1 : (int)Math.Min(count - 1, Math.Floor((to - t0) / period + 1e-9));
            return (first, last);
        }

        static double? Finite(double v) => double.IsFinite(v) ? v : null;

        static string FormatAddress(uint address) => $"0x{address:X8}";
    }
}
