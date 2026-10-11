using eScope.Plot;
using ScottPlot.WinForms;
using System.Globalization;

namespace eScope.Analyzer
{
    // Floating window analysing the visible channels of a trace over the current selection (or the visible range)
    public class AnalyzerForm : Form
    {
        readonly TraceCtrl Trc;
        readonly DataGridView Grid;
        readonly FormsPlot FftPlot;
        readonly Label Info;
        readonly CheckBox AutoRefresh;
        readonly CheckBox FftDb;
        readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
        List<AnalysisResult> results = new();
        readonly HashSet<string> hiddenCurves = new(); // by channel name, kept across refreshes

        static readonly (string Header, Func<AnalysisResult, string> Value)[] Columns =
        {
            ("Channel",   r => r.Name),
            ("Samples",   r => r.Samples.ToString()),
            ("Duration",  r => FormatTime(r.Duration)),
            ("Min",       r => Format(r.Min, r.Unit)),
            ("Max",       r => Format(r.Max, r.Unit)),
            ("Pk-Pk",     r => Format(r.PeakToPeak, r.Unit)),
            ("Mean",      r => Format(r.Mean, r.Unit)),
            ("RMS",       r => Format(r.Rms, r.Unit)),
            ("Std dev",   r => Format(r.StdDev, r.Unit)),
            ("Frequency", r => FormatFreq(r.Frequency)),
            ("Period",    r => FormatTime(r.Period)),
            ("Duty",      r => double.IsNaN(r.Duty) ? "-" : r.Duty.ToString("F1", CultureInfo.InvariantCulture) + " %"),
            ("Edges",     r => r.RisingEdges.ToString()),
            ("FFT peak",  r => FormatFreq(r.FftPeakFrequency)),
            ("FFT amp",   r => Format(r.FftPeakAmplitude, r.Unit)),
        };

        public AnalyzerForm(TraceCtrl trc)
        {
            Trc = trc;
            Text = $"Analyzer - {trc.Data.Name}";
            Size = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterParent;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = false };
            var refresh = new Button { Text = "Refresh (F5)", AutoSize = true };
            refresh.Click += (_, _) => Analyze();
            AutoRefresh = new CheckBox { Text = "Auto refresh", AutoSize = true, Margin = new Padding(12, 6, 0, 0) };
            AutoRefresh.CheckedChanged += (_, _) => timer.Enabled = AutoRefresh.Checked;
            FftDb = new CheckBox { Text = "FFT in dB", AutoSize = true, Margin = new Padding(12, 6, 0, 0) };
            FftDb.CheckedChanged += (_, _) => UpdateFftPlot();
            var copy = new Button { Text = "Copy table", AutoSize = true, Margin = new Padding(12, 3, 3, 3) };
            copy.Click += (_, _) => CopyTable();
            Info = new Label { AutoSize = true, Margin = new Padding(12, 6, 0, 0) };
            top.Controls.AddRange(new Control[] { refresh, AutoRefresh, FftDb, copy, Info });

            Grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
            };
            Grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Show", HeaderText = "Show", SortMode = DataGridViewColumnSortMode.NotSortable });
            foreach (var (header, _) in Columns)
            {
                int i = Grid.Columns.Add(header, header);
                Grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
                Grid.Columns[i].ReadOnly = true;
            }
            Grid.CellContentClick += Grid_CellContentClick;
            Grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

            FftPlot = new FormsPlot { Dock = DockStyle.Fill };
            if (trc.DarkMode != 0)
            {
                FftPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#181818");
                FftPlot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#1f1f1f");
                FftPlot.Plot.Axes.Color(ScottPlot.Color.FromHex("#d7d7d7"));
                FftPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#404040");
                FftPlot.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#404040");
                FftPlot.Plot.Legend.FontColor = ScottPlot.Color.FromHex("#d7d7d7");
                FftPlot.Plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#d7d7d7");
            }

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            split.Panel1.Controls.Add(Grid);
            split.Panel2.Controls.Add(FftPlot);
            Controls.Add(split);
            Controls.Add(top);
            Load += (_, _) => split.SplitterDistance = Math.Max(120, split.Height / 3);

            KeyPreview = true;
            KeyDown += (_, e) => { if (e.KeyCode == Keys.F5) Analyze(); };
            timer.Tick += (_, _) => Analyze();
            FormClosed += (_, _) => timer.Dispose();

            Analyze();
        }

        public void Analyze()
        {
            double xStart = Trc.Plot.GetSelectStart();
            double xWidth = Trc.Plot.GetSelectWidth();
            string range = Trc.Plot.dataSelector.IsVisible() ? "Selection" : "Visible range";
            Info.Text = $"{range}: {FormatTime(xStart)} .. {FormatTime(xStart + xWidth)} ({FormatTime(xWidth)})";

            results = Trc.GetSignalList()
                .Where(c => c.IsVisible())
                .Select(c =>
                {
                    var r = SignalAnalysis.Analyze(c.FilterValues(xStart, xWidth), c.Period);
                    r.Name = c.SignalBase.Name;
                    r.Unit = c.SignalBase.Unit;
                    r.Color = c.SignalBase.Color;
                    return r;
                })
                .ToList();

            UpdateGrid();
            UpdateFftPlot();
        }

        void UpdateGrid()
        {
            int selected = Grid.CurrentRow?.Index ?? -1;
            Grid.Rows.Clear();
            foreach (var r in results)
            {
                var cells = new List<object> { !hiddenCurves.Contains(r.Name) };
                cells.AddRange(Columns.Select(c => (object)c.Value(r)));
                int row = Grid.Rows.Add(cells.ToArray());
                Grid.Rows[row].Cells[1].Style.ForeColor = ToColor(r.Color);
            }
            if (selected >= 0 && selected < Grid.Rows.Count)
                Grid.CurrentCell = Grid.Rows[selected].Cells[1];
        }

        // "Show" checkbox: hide / show the channel curve on the FFT plot
        void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.RowIndex < 0 || e.RowIndex >= results.Count) return;
            string name = results[e.RowIndex].Name;
            SetCurveVisible(name, hiddenCurves.Contains(name));
            Grid.EndEdit();
            Grid.Rows[e.RowIndex].Cells[0].Value = !hiddenCurves.Contains(name);
            UpdateFftPlot();
        }

        // click on the "Show" header: show all, or hide all if all are shown
        void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex != 0) return;
            bool allShown = results.All(r => !hiddenCurves.Contains(r.Name));
            foreach (var r in results) SetCurveVisible(r.Name, !allShown);
            Grid.EndEdit();
            UpdateGrid();
            UpdateFftPlot();
        }

        void SetCurveVisible(string name, bool visible)
        {
            if (visible) hiddenCurves.Remove(name);
            else hiddenCurves.Add(name);
        }

        void UpdateFftPlot()
        {
            FftPlot.Plot.Clear();
            bool db = FftDb.Checked;
            foreach (var r in results.Where(r => r.FftAmplitudes.Length > 1 && !hiddenCurves.Contains(r.Name)))
            {
                // DC bin is removed before the FFT: skip it
                double[] xs = r.FftFrequencies[1..];
                double[] ys = r.FftAmplitudes[1..].Select(a => db ? 20 * Math.Log10(Math.Max(a, 1e-12)) : a).ToArray();
                var sc = FftPlot.Plot.Add.Scatter(xs, ys);
                sc.MarkerSize = 0;
                sc.LineWidth = 1.5f;
                sc.Color = ScottPlot.Color.FromHex(r.Color);
                sc.LegendText = r.Name;
            }
            FftPlot.Plot.Axes.Bottom.Label.Text = "Frequency (Hz)";
            FftPlot.Plot.Axes.Left.Label.Text = db ? "Amplitude (dB)" : "Amplitude";
            FftPlot.Plot.ShowLegend();
            FftPlot.Plot.Axes.AutoScale();
            FftPlot.Refresh();
        }

        void CopyTable()
        {
            var lines = new List<string> { string.Join("\t", Columns.Select(c => c.Header)) };
            lines.AddRange(results.Select(r => string.Join("\t", Columns.Select(c => c.Value(r)))));
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }

        static System.Drawing.Color ToColor(string hex)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return SystemColors.ControlText; }
        }

        static string Format(double v, string unit)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "-";
            string s = v.ToString("G6", CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(unit) ? s : $"{s} {unit}";
        }

        // time base is ms
        static string FormatTime(double ms)
        {
            if (double.IsNaN(ms) || double.IsInfinity(ms)) return "-";
            double a = Math.Abs(ms);
            if (a >= 1000) return (ms / 1000).ToString("G5", CultureInfo.InvariantCulture) + " s";
            if (a >= 1 || a == 0) return ms.ToString("G5", CultureInfo.InvariantCulture) + " ms";
            return (ms * 1000).ToString("G5", CultureInfo.InvariantCulture) + " µs";
        }

        static string FormatFreq(double hz)
        {
            if (double.IsNaN(hz) || double.IsInfinity(hz)) return "-";
            if (Math.Abs(hz) >= 1000) return (hz / 1000).ToString("G5", CultureInfo.InvariantCulture) + " kHz";
            if (Math.Abs(hz) >= 1 || hz == 0) return hz.ToString("G5", CultureInfo.InvariantCulture) + " Hz";
            return (hz * 1000).ToString("G5", CultureInfo.InvariantCulture) + " mHz";
        }
    }
}
