using eScope.Analyzer;
using eScope.Plot;

namespace eScope.Automation
{
    // traces, channels, data, analysis, annotations, screenshot (read only)
    public partial class AutomationService
    {
        public Task<List<TraceInfo>> ListTraces() => ui.InvokeAsync(() =>
        {
            TraceCtrl? current = scope.GetCurrentTrace();
            return eScope.Traces.Select(t =>
            {
                var channels = t.GetSignalList();
                double? end = channels.Select(EndTime).Where(e => e != null).Max();
                return new TraceInfo(t.Data.Name, t == current, channels.Count, end, View(t), Selection(t));
            }).ToList();
        });

        static double? EndTime(ChannelCtrl c)
            => c.Y.Count == 0 ? null : StartTime(c) + (c.Y.Count - 1) * c.Period;

        public Task<List<ChannelInfo>> ListChannels(string? trace) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            return trc.GetSignalList().Select(ToChannelInfo).ToList();
        });

        static ChannelInfo ToChannelInfo(ChannelCtrl c)
        {
            var ch = c.SignalBase;
            return new ChannelInfo(
                ch.Name,
                ch.hasDevice ? ch.Device : null,
                ch.hasDevice ? ch.Symbol : null,
                ch.hasDevice,
                ch.hasFunction,
                ch.hasFunction || !ch.hasDevice ? ch.Script : null,
                string.IsNullOrEmpty(ch.RuntimeError) ? null : ch.RuntimeError,
                ch.hasDevice ? FormatAddress(ch.Address) : null,
                ch.Size,
                ch.Shift,
                DisplayName(ch.Display),
                ch.Unit,
                ch.Color,
                ch.IsVisible,
                c.Y.Count,
                c.Period,
                StartTime(c),
                EndTime(c),
                ch.YGain,
                ch.YOffset * ch.YGain, // stored in divisions, given in channel units (as in the channel editor)
                ch.XOffset);
        }

        // names accepted by add_channel / configure_channel
        static string DisplayName(Utils.Display d) => d switch
        {
            Utils.Display.unsignedInt => "unsigned",
            Utils.Display.hexa => "hex",
            Utils.Display.float32 => "float",
            _ => "signed",
        };

        public Task<List<ChannelData>> GetChannelData(string? trace, string[]? channels, string? range,
            double? fromMs, double? toMs, bool includeRaw) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var (from, to) = ResolveRange(trc, range, fromMs, toMs);
            return ResolveChannels(trc, channels).Select(c =>
            {
                var (first, last) = SampleRange(c, from, to);
                int n = Math.Max(0, last - first + 1);
                double t0 = StartTime(c);
                double period = c.Period;
                var t = new double[n];
                var v = new double[n];
                for (int k = 0; k < n; k++)
                {
                    t[k] = t0 + (first + k) * period;
                    v[k] = c.Y[first + k];
                }
                ulong[]? raw = null;
                if (includeRaw && c.SignalBase.hasDevice)
                {
                    var src = c.SignalBase.Y;
                    raw = Enumerable.Range(first, n).Select(i => i < src.Count ? src[i] : 0UL).ToArray();
                }
                var lost = c.SignalBase.Lost
                    .Where(l => l.Index >= first && l.Index <= last + 1)
                    .Select(l => new LostInfo(t0 + l.Index * period, l.Count, l.Timeout))
                    .ToList();
                return new ChannelData(c.SignalBase.Name, c.SignalBase.Unit, period, t, v, raw, lost);
            }).ToList();
        });

        public Task<List<ChannelValue>> GetValueAt(string? trace, double timeMs, string[]? channels) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            return ResolveChannels(trc, channels).Select(c =>
            {
                if (c.Y.Count == 0 || c.Period <= 0)
                    return new ChannelValue(c.SignalBase.Name, null, null, null);
                int i = (int)Math.Round((timeMs - StartTime(c)) / c.Period);
                if (i < 0 || i >= c.Y.Count)
                    return new ChannelValue(c.SignalBase.Name, null, null, null);
                var src = c.SignalBase.Y;
                ulong? raw = c.SignalBase.hasDevice && i < src.Count ? src[i] : null;
                return new ChannelValue(c.SignalBase.Name, StartTime(c) + i * c.Period, c.Y[i], raw);
            }).ToList();
        });

        public Task<List<ChannelAnalysis>> Analyze(string? trace, string[]? channels, string? range,
            double? fromMs, double? toMs, bool fft, int fftPoints) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var (from, to) = ResolveRange(trc, range, fromMs, toMs);
            return ResolveChannels(trc, channels).Select(c =>
            {
                var (first, last) = SampleRange(c, from, to);
                int n = Math.Max(0, last - first + 1);
                var r = SignalAnalysis.Analyze(c.Y.GetRange(first, n), c.Period);
                double t0 = StartTime(c);
                (double[]? freqs, double[]? amps) = fft ? Reduce(r.FftFrequencies, r.FftAmplitudes, fftPoints) : (null, null);
                return new ChannelAnalysis(
                    c.SignalBase.Name,
                    c.SignalBase.Unit,
                    r.Samples,
                    n > 0 ? t0 + first * c.Period : null,
                    n > 0 ? t0 + last * c.Period : null,
                    Finite(r.Duration),
                    Finite(r.Min),
                    Finite(r.Max),
                    Finite(r.PeakToPeak),
                    Finite(r.Mean),
                    Finite(r.Rms),
                    Finite(r.StdDev),
                    Finite(r.Frequency),
                    Finite(r.Period),
                    Finite(r.Duty),
                    r.RisingEdges,
                    Finite(r.FftPeakFrequency),
                    Finite(r.FftPeakAmplitude),
                    freqs,
                    amps);
            }).ToList();
        });

        // spectrum reduced to at most `points` bins: max amplitude of each group of bins (peaks are kept)
        static (double[], double[]) Reduce(double[] freqs, double[] amps, int points)
        {
            points = Math.Max(2, points);
            if (amps.Length <= points) return (freqs, amps);
            var f = new double[points];
            var a = new double[points];
            for (int k = 0; k < points; k++)
            {
                int start = (int)((long)k * amps.Length / points);
                int end = (int)((long)(k + 1) * amps.Length / points);
                int best = start;
                for (int i = start + 1; i < end; i++)
                    if (amps[i] > amps[best]) best = i;
                f[k] = freqs[best];
                a[k] = amps[best];
            }
            return (f, a);
        }

        public Task<EdgeList> FindEdges(string? trace, string channel, string? range, double? fromMs, double? toMs,
            double? threshold, double? hysteresis, string direction) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var c = ResolveChannel(trc, channel);
            var (from, to) = ResolveRange(trc, range, fromMs, toMs);
            var (first, last) = SampleRange(c, from, to);
            int n = Math.Max(0, last - first + 1);
            var y = c.Y.GetRange(first, n);

            double min = n > 0 ? y.Min() : 0, max = n > 0 ? y.Max() : 0;
            double th = threshold ?? (min + max) / 2;
            // a little above zero: an exact crossing value still decides the edge direction
            double hyst = Math.Max(hysteresis ?? (max - min) * 0.1, Math.Max(Math.Abs(max - min), 1) * 1e-9);
            var (rising, falling) = SignalAnalysis.FindEdges(y, c.Period, th, hyst);

            double t0 = StartTime(c) + first * c.Period;
            bool wantRising = direction is "rising" or "both";
            bool wantFalling = direction is "falling" or "both";
            if (!wantRising && !wantFalling)
                throw new AutomationException(AutomationError.InvalidArgument, $"direction '{direction}': expected rising, falling or both");
            return new EdgeList(
                c.SignalBase.Name,
                th,
                hyst,
                wantRising ? rising.Select(t => t0 + t).ToList() : Array.Empty<double>(),
                wantFalling ? falling.Select(t => t0 + t).ToList() : Array.Empty<double>());
        });

        public Task<AnnotationList> ListAnnotations(string? trace) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var selector = trc.Plot.dataSelector;
            return new AnnotationList(
                trc.Data.Name,
                Selection(trc),
                trc.Ctrls.OfType<MarkerCtrl>()
                    .Select(m => new MarkerInfo(m.Data.Name, m.Data.Pos, m.Data.Color, m.Data.IsVisible)).ToList(),
                trc.Ctrls.OfType<DeltaCtrl>().Where(d => d != selector)
                    .Select(d => new DeltaInfo(d.Data.Name, d.Min(), d.Max(), d.Width(), d.Data.Color, d.Data.IsVisible)).ToList(),
                trc.Ctrls.OfType<AnnotationCtrl>()
                    .Select(a => new AnnotationInfo(a.Data.Name, a.Data.Channel, a.Data.TipLocationX, a.Data.Color, a.Data.IsVisible)).ToList(),
                trc.Ctrls.OfType<DataMarkerCtrl>()
                    .Select(m => new DataMarkerInfo(m.Data.Name, m.Data.Channel, m.Data.Value, m.Data.Color, m.Data.IsVisible)).ToList());
        });

        // only the current trace is laid out on screen, and not while the window is minimized
        void CheckCapturable(TraceCtrl trc)
        {
            if (trc != scope.GetCurrentTrace())
                throw new AutomationException(AutomationError.NotSupported,
                    $"only the current trace ('{scope.GetCurrentTrace()?.Data.Name}') can be captured");
            if (trc.tabPage.ClientSize.Width <= 0 || trc.tabPage.ClientSize.Height <= 0)
                throw new AutomationException(AutomationError.NotSupported, "the eScope window is minimized: nothing to capture");
        }

        // PNG of the tab as displayed
        public Task<byte[]> Screenshot(string? trace) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            CheckCapturable(trc);
            return trc.RenderPng();
        });
    }
}
