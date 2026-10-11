using System.Numerics;

namespace eScope.Analyzer
{
    // Result of the analysis of one channel over the selection. NaN = not computable (flat signal, too few edges...)
    public class AnalysisResult
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Samples { get; set; }
        public double Duration { get; set; } = double.NaN; // ms
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public double PeakToPeak => Max - Min;
        public double Mean { get; set; } = double.NaN;
        public double Rms { get; set; } = double.NaN;
        public double StdDev { get; set; } = double.NaN;
        public double Frequency { get; set; } = double.NaN; // Hz, from edge detection
        public double Period { get; set; } = double.NaN; // ms
        public double Duty { get; set; } = double.NaN; // %
        public int RisingEdges { get; set; }
        public double FftPeakFrequency { get; set; } = double.NaN; // Hz, dominant bin (DC excluded)
        public double FftPeakAmplitude { get; set; } = double.NaN;
        public double[] FftFrequencies { get; set; } = Array.Empty<double>(); // Hz
        public double[] FftAmplitudes { get; set; } = Array.Empty<double>(); // single-sided amplitude spectrum
    }

    public static class SignalAnalysis
    {
        // y: samples, period: sampling period in ms
        public static AnalysisResult Analyze(IReadOnlyList<double> y, double period)
        {
            var r = new AnalysisResult { Samples = y.Count };
            if (y.Count == 0) return r;

            r.Duration = (y.Count - 1) * period;
            double min = double.PositiveInfinity, max = double.NegativeInfinity, sum = 0, sum2 = 0;
            foreach (double v in y)
            {
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
                sum2 += v * v;
            }
            r.Min = min;
            r.Max = max;
            r.Mean = sum / y.Count;
            r.Rms = Math.Sqrt(sum2 / y.Count);
            r.StdDev = Math.Sqrt(Math.Max(0, sum2 / y.Count - r.Mean * r.Mean));

            ComputeEdges(y, period, r);
            ComputeFft(y, period, r);
            return r;
        }

        // Frequency and duty cycle from threshold crossings at mid-level, with 10% hysteresis against noise
        static void ComputeEdges(IReadOnlyList<double> y, double period, AnalysisResult r)
        {
            double pp = r.Max - r.Min;
            if (pp <= 0 || period <= 0) return;
            var (rising, falling) = FindEdges(y, period, (r.Max + r.Min) / 2, pp * 0.1);
            r.RisingEdges = rising.Count;
            if (rising.Count < 2) return;

            // average over whole periods only: first to last rising edge
            double first = rising[0];
            double last = rising[^1];
            r.Period = (last - first) / (rising.Count - 1);
            r.Frequency = 1000.0 / r.Period;

            // high time = rising edge -> next falling edge, for each full period
            double high = 0;
            int fi = 0;
            for (int k = 0; k < rising.Count - 1; k++)
            {
                while (fi < falling.Count && falling[fi] <= rising[k]) fi++;
                if (fi < falling.Count && falling[fi] < rising[k + 1])
                    high += falling[fi] - rising[k];
            }
            r.Duty = 100.0 * high / (last - first);
        }

        // Crossings of the threshold, with hysteresis (±hysteresis around it) against noise.
        // Times in ms from the first sample, interpolated at the threshold level.
        public static (List<double> Rising, List<double> Falling) FindEdges(IReadOnlyList<double> y, double period, double threshold, double hysteresis)
        {
            var rising = new List<double>();
            var falling = new List<double>();
            if (y.Count == 0 || period <= 0) return (rising, falling);
            double hi = threshold + hysteresis;
            double lo = threshold - hysteresis;
            int state = y[0] >= hi ? 1 : y[0] <= lo ? -1 : 0;
            for (int i = 1; i < y.Count; i++)
            {
                if (state != 1 && y[i] >= hi)
                {
                    if (state == -1) rising.Add(CrossingTime(y, i, threshold, period));
                    state = 1;
                }
                else if (state != -1 && y[i] <= lo)
                {
                    if (state == 1) falling.Add(CrossingTime(y, i, threshold, period));
                    state = -1;
                }
            }
            return (rising, falling);
        }

        // linear interpolation of the mid-level crossing, searching back from the hysteresis crossing at index i
        static double CrossingTime(IReadOnlyList<double> y, int i, double mid, double period)
        {
            int j = i;
            bool up = y[i] > mid;
            while (j > 0 && (up ? y[j - 1] > mid : y[j - 1] < mid)) j--;
            if (j == 0) return 0;
            double a = y[j - 1], b = y[j];
            double frac = b != a ? (mid - a) / (b - a) : 0;
            return (j - 1 + frac) * period;
        }

        // Hann window, DC removed, zero padded to a power of 2. Amplitude corrected for the window gain.
        static void ComputeFft(IReadOnlyList<double> y, double period, AnalysisResult r)
        {
            int n = y.Count;
            if (n < 4 || period <= 0) return;
            int size = 1;
            while (size < n) size <<= 1;

            var buf = new Complex[size];
            double windowSum = 0;
            for (int i = 0; i < n; i++)
            {
                double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1));
                windowSum += w;
                buf[i] = new Complex((y[i] - r.Mean) * w, 0);
            }
            Fft(buf);

            double fs = 1000.0 / period; // Hz
            int bins = size / 2 + 1;
            r.FftFrequencies = new double[bins];
            r.FftAmplitudes = new double[bins];
            int peak = -1;
            for (int k = 0; k < bins; k++)
            {
                double amp = buf[k].Magnitude / windowSum;
                if (k != 0 && k != size / 2) amp *= 2; // single-sided
                r.FftFrequencies[k] = k * fs / size;
                r.FftAmplitudes[k] = amp;
                if (k > 0 && (peak < 0 || amp > r.FftAmplitudes[peak])) peak = k;
            }
            if (peak > 0 && r.FftAmplitudes[peak] > 0)
            {
                r.FftPeakFrequency = r.FftFrequencies[peak];
                r.FftPeakAmplitude = r.FftAmplitudes[peak];
            }
        }

        // in-place iterative radix-2 FFT, buf.Length must be a power of 2
        static void Fft(Complex[] buf)
        {
            int n = buf.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) (buf[i], buf[j]) = (buf[j], buf[i]);
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                var wlen = new Complex(Math.Cos(ang), Math.Sin(ang));
                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    for (int k = 0; k < len / 2; k++)
                    {
                        Complex u = buf[i + k];
                        Complex v = buf[i + k + len / 2] * w;
                        buf[i + k] = u + v;
                        buf[i + k + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
        }
    }
}
