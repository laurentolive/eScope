using eScope.Analyzer;
using eScope.Plot;
using System.Globalization;

namespace eScope.UI
{
    // Left panel of a trace: channels, markers, selection measures and watch, each in a collapsible section.
    // One panel per trace, shown by the main window for the current trace.
    public class SidePanel : Panel, IThemed
    {
        readonly TraceCtrl Trc;
        readonly SectionHeader hdrChannels, hdrMarkers, hdrSelection, hdrWatch;
        readonly List<ChannelRow> channelRows = new();
        readonly List<MarkerRow> markerRows = new();
        readonly SelectionView selection;
        readonly Panel watchHost;
        Watch.View? watch;
        string markerSignature = string.Empty;
        readonly List<int> dividers = new();
        readonly ContextMenuStrip addMarkerMenu = new();
        readonly bool ready; // layout once every section exists

        public SidePanel(TraceCtrl trc)
        {
            Trc = trc;
            DoubleBuffered = true;
            var collapsed = UiSettings.Current.CollapsedSections;

            hdrChannels = Header("CHANNELS", "channels", collapsed);
            hdrChannels.HasAdd = true;
            hdrChannels.AddTip = "New channel (Insert)";
            hdrChannels.AddClick += _ => Trc.AddChannel();

            hdrMarkers = Header("MARKERS", "markers", collapsed);
            hdrMarkers.HasAdd = true;
            hdrMarkers.AddTip = "New marker";
            addMarkerMenu.Items.Add("New Horizontal Axis\tH", null, (_, _) => Trc.AddTimeMarker());
            addMarkerMenu.Items.Add("New Vertical Axis\tV", null, (_, _) => Trc.AddDataMarker());
            addMarkerMenu.Items.Add("New Time Delta\tD", null, (_, _) => Trc.AddTimeDelta());
            addMarkerMenu.Items.Add("New Text\tT", null, (_, _) => Trc.AddAnnotation());
            hdrMarkers.AddClick += p => addMarkerMenu.Show(p);

            hdrSelection = Header("SELECTION", "selection", collapsed);
            selection = new SelectionView(trc);
            Controls.Add(selection);

            hdrWatch = Header("WATCH", "watch", collapsed);
            hdrWatch.HasAdd = true;
            hdrWatch.AddTip = "Add a watch expression";
            hdrWatch.AddClick += _ => FocusWatch(true);
            watchHost = new Panel();
            Controls.Add(watchHost);

            ready = true;
            ApplyTheme();
        }

        SectionHeader Header(string title, string key, List<string> collapsed)
        {
            var h = new SectionHeader(title, collapsed.Contains(key));
            h.Toggled += () =>
            {
                collapsed.Remove(key);
                if (h.Collapsed) collapsed.Add(key);
                UiSettings.Current.Save();
                PerformLayout();
                Invalidate();
            };
            Controls.Add(h);
            return h;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Current.Panel;
            foreach (Control c in Controls)
                if (c is IThemed t) t.ApplyTheme();
            watch?.ApplyTheme();
            Invalidate();
        }

        // --- Channels (rows owned by the plot legend) ---

        public void AddChannelRow(ChannelRow row)
        {
            channelRows.Add(row);
            Controls.Add(row);
            PerformLayout();
        }
        public void RemoveChannelRow(ChannelRow row)
        {
            channelRows.Remove(row);
            Controls.Remove(row);
            PerformLayout();
        }

        // --- Watch ---

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null && watch == null)
            {
                watch = new Watch.View(Trc.Data.Watches, Trc.Scope.Devices)
                {
                    TopLevel = false,
                    FormBorderStyle = FormBorderStyle.None,
                    Dock = DockStyle.Fill,
                };
                watchHost.Controls.Add(watch);
                watch.SetCompact(true);
                watch.Show();
                watch.ApplyTheme();
            }
            watch?.SetActive(Parent != null);
        }

        // shortcut W / View > Watch: open the section and focus the grid (on the "add watch" row with add)
        public void FocusWatch(bool add = false)
        {
            hdrWatch.SetCollapsed(false);
            watch?.FocusGrid(add);
        }

        public void CloseWatch()
        {
            watch?.SetActive(false);
            watch?.Dispose();
            watch = null;
        }

        // --- Periodic update (current trace only, each plot refresh) ---

        public void Tick()
        {
            var signals = Trc.GetSignalList();
            hdrChannels.SetCount(signals.Count.ToString());
            UpdateMarkers();
            UpdateSelectionHeader();
            if (!hdrSelection.Collapsed) selection.Tick();
            hdrWatch.SetCount(Trc.Data.Watches.Count.ToString());
        }

        void UpdateSelectionHeader()
        {
            if (Trc.Plot.dataSelector.IsVisible())
                hdrSelection.SetRight("Δ " + AnalyzerForm.FormatTime(Trc.Plot.dataSelector.Width()));
            else
                hdrSelection.SetRight("view " + AnalyzerForm.FormatTime(Trc.Data.XWidth));
        }

        static (MarkerRow.Kind kind, bool ok) KindOf(CtrlBase c) => c switch
        {
            MarkerCtrl => (MarkerRow.Kind.Time, true),
            DeltaCtrl => (MarkerRow.Kind.Delta, true),
            DataMarkerCtrl => (MarkerRow.Kind.Value, true),
            AnnotationCtrl => (MarkerRow.Kind.Text, true),
            _ => (MarkerRow.Kind.Time, false),
        };

        void UpdateMarkers()
        {
            var markers = Trc.Ctrls.Where(c => KindOf(c).ok).ToList();
            string signature = string.Join("|", markers.Select(m => m.GetHashCode()));
            if (signature != markerSignature)
            {
                markerSignature = signature;
                foreach (var r in markerRows)
                {
                    Controls.Remove(r);
                    r.Dispose();
                }
                markerRows.Clear();
                foreach (var m in markers)
                {
                    var row = new MarkerRow(m, KindOf(m).kind);
                    row.MouseDoubleClick += (_, _) => m.Edit();
                    row.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) MarkerMenu(m).Show(row, e.Location); };
                    markerRows.Add(row);
                    Controls.Add(row);
                }
                PerformLayout();
            }
            hdrMarkers.SetCount(markers.Count.ToString());
            foreach (var row in markerRows)
            {
                var c = row.Ctrl;
                string info = c switch
                {
                    MarkerCtrl mc => AnalyzerForm.FormatTime(mc.Data.Pos),
                    DeltaCtrl dc => "Δ " + AnalyzerForm.FormatTime(dc.Width()),
                    DataMarkerCtrl dm => dm.Data.Value.ToString("G5", CultureInfo.InvariantCulture),
                    AnnotationCtrl ac => ac.Data.Channel,
                    _ => string.Empty,
                };
                row.SetData(c.DataBase.Name, info, ToColor(c.DataBase.Color), !c.IsVisible());
            }
        }

        ContextMenuStrip MarkerMenu(CtrlBase m)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Edit...", null, (_, _) => m.Edit());
            menu.Items.Add(m.IsVisible() ? "Hide" : "Show", null, (_, _) =>
            {
                m.SetVisibility(!m.IsVisible());
                Trc.Plot.RefreshReq = true;
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete", null, (_, _) => Trc.Delete(m));
            menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
            return menu;
        }

        internal static Color ToColor(string hex)
        {
            try { return Theme.TraceColor(ColorTranslator.FromHtml(hex)); }
            catch { return Theme.Current.Muted; }
        }

        // --- Layout ---

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (!ready) return;
            int w = ClientSize.Width;
            int y = this.Px(4);
            int hh = this.Px(34);
            dividers.Clear();

            void Place(Control c, int height, bool visible)
            {
                c.Visible = visible;
                if (!visible) return;
                c.SetBounds(0, y, w, height);
                y += height;
            }

            Place(hdrChannels, hh, true);
            foreach (var r in channelRows) Place(r, this.Px(44), !hdrChannels.Collapsed);

            y += this.Px(6);
            dividers.Add(y);
            y += this.Px(3);
            Place(hdrMarkers, hh, true);
            foreach (var r in markerRows) Place(r, this.Px(26), !hdrMarkers.Collapsed);

            y += this.Px(6);
            dividers.Add(y);
            y += this.Px(3);
            Place(hdrSelection, hh, true);
            Place(selection, this.Px(176), !hdrSelection.Collapsed);

            y += this.Px(6);
            dividers.Add(y);
            y += this.Px(3);
            Place(hdrWatch, hh, true);
            int rest = Math.Max(this.Px(110), ClientSize.Height - y - this.Px(8));
            watchHost.Visible = !hdrWatch.Collapsed;
            if (watchHost.Visible)
                watchHost.SetBounds(this.Px(8), y, w - this.Px(16), rest);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Theme.Current.Border);
            foreach (int y in dividers)
                e.Graphics.DrawLine(pen, this.Px(12), y, Width - this.Px(12), y);
        }
    }

    // Measures of one channel over the selection (or the visible range without selection)
    public class SelectionView : Panel, IThemed
    {
        readonly TraceCtrl Trc;
        readonly ComboBox channelBox;
        readonly LinkLabel analyzer;
        List<ChannelCtrl> channels = new();
        string? channelName;
        AnalysisResult? result;
        string inputs = string.Empty;
        DateTime lastCompute;

        public SelectionView(TraceCtrl trc)
        {
            Trc = trc;
            DoubleBuffered = true;
            channelBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
            };
            channelBox.DrawItem += DrawChannelItem;
            ThemeApplier.AttachComboPainter(channelBox);
            channelBox.SelectionChangeCommitted += (_, _) =>
            {
                channelName = channelBox.SelectedIndex >= 0 && channelBox.SelectedIndex < channels.Count ? channels[channelBox.SelectedIndex].SignalBase.Name : null;
                inputs = string.Empty;
                Tick();
                // back to the plot: single-key shortcuts must not go to the list
                Trc.Plot.GetControl().Focus();
            };
            analyzer = new LinkLabel { Text = "Open analyzer (FFT)…", AutoSize = true, TabStop = false };
            analyzer.LinkClicked += (_, _) => Trc.Plot.OpenAnalyzer();
            Controls.Add(channelBox);
            Controls.Add(analyzer);
            ready = true;
            ApplyTheme();
        }
        readonly bool ready;

        public void ApplyTheme()
        {
            var p = Theme.Current;
            BackColor = p.Panel;
            channelBox.BackColor = p.Input;
            channelBox.ForeColor = p.Text;
            analyzer.LinkColor = analyzer.ActiveLinkColor = analyzer.VisitedLinkColor = p.Accent;
            analyzer.BackColor = p.Panel;
            analyzer.Font = Theme.Ui(8.5f);
            channelBox.Font = Theme.Mono(9);
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (!ready) return;
            channelBox.SetBounds(this.Px(14), this.Px(2), Width - this.Px(28), this.Px(26));
            channelBox.ItemHeight = this.Px(20);
            analyzer.Location = new Point(this.Px(12), this.Px(152));
        }

        void DrawChannelItem(object? sender, DrawItemEventArgs e)
        {
            var p = Theme.Current;
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var bg = new SolidBrush(selected ? p.Selected : p.Input)) e.Graphics.FillRectangle(bg, e.Bounds);
            if (e.Index < 0 || e.Index >= channels.Count) return;
            var c = channels[e.Index];
            int s = this.Px(10);
            var sw = new Rectangle(e.Bounds.X + this.Px(6), e.Bounds.Y + (e.Bounds.Height - s) / 2, s, s);
            Theme.FillRounded(e.Graphics, sw, SidePanel.ToColor(c.SignalBase.Color), this.Px(3));
            var text = new Rectangle(sw.Right + this.Px(8), e.Bounds.Y, e.Bounds.Width - sw.Right - this.Px(8), e.Bounds.Height);
            Theme.DrawText(e.Graphics, c.SignalBase.Name, Theme.Mono(9), text, p.Text);
        }

        public void Tick()
        {
            // channel list follows the visible channels
            var visible = Trc.GetSignalList().Where(c => c.IsVisible()).ToList();
            if (!visible.SequenceEqual(channels))
            {
                channels = visible;
                channelBox.Items.Clear();
                foreach (var c in channels) channelBox.Items.Add(c.SignalBase.Name);
                inputs = string.Empty;
            }
            int idx = channels.FindIndex(c => c.SignalBase.Name == channelName);
            if (idx < 0 && channels.Count > 0) idx = 0;
            if (channelBox.SelectedIndex != idx && !channelBox.DroppedDown) channelBox.SelectedIndex = idx;
            var ch = idx >= 0 ? channels[idx] : null;

            // computed again when the range, the channel or the data change, at most twice a second
            // selection, else the window displayed (the view target leads the axis while playing)
            bool selected = Trc.Plot.dataSelector.IsVisible();
            var axis = Trc.Plot.fp.Plot.Axes.Bottom.Range;
            double start = selected ? Trc.Plot.dataSelector.Min() : Math.Round(axis.Min);
            double width = selected ? Trc.Plot.dataSelector.Width() : Math.Round(axis.Max - axis.Min);
            string key = $"{ch?.SignalBase.Name}|{start}|{width}|{ch?.SampleCount}";
            if (key == inputs || (DateTime.Now - lastCompute).TotalMilliseconds < 500) return;
            inputs = key;
            lastCompute = DateTime.Now;
            if (ch == null)
            {
                result = null;
            }
            else
            {
                result = SignalAnalysis.AnalyzeBasic(ch.FilterValues(start, width), ch.Period);
                result.Unit = ch.SignalBase.Unit;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var p = Theme.Current;
            var g = e.Graphics;
            string unit = result?.Unit ?? string.Empty;
            string V(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("G4", CultureInfo.InvariantCulture) + (unit.Length != 0 ? " " + unit : string.Empty);
            var rows = result == null
                ? new[] { ("No visible channel", string.Empty) }
                : new[]
                {
                    ("Frequency", AnalyzerForm.FormatFreq(result.Frequency)),
                    ("Period", AnalyzerForm.FormatTime(result.Period)),
                    ("Duty cycle", double.IsNaN(result.Duty) ? "-" : result.Duty.ToString("F1", CultureInfo.InvariantCulture) + " %"),
                    ("Min / Max", double.IsNaN(result.Min) ? "-" : $"{result.Min.ToString("G4", CultureInfo.InvariantCulture)} / {V(result.Max)}"),
                    ("Mean", V(result.Mean)),
                    ("RMS", V(result.Rms)),
                };
            int y = this.Px(34);
            int lh = this.Px(19);
            foreach (var (label, value) in rows)
            {
                Theme.DrawText(g, label, Theme.Ui(8.5f), new Rectangle(this.Px(14), y, Width / 2, lh), p.Muted);
                Theme.DrawText(g, value, Theme.Mono(8.5f), new Rectangle(Width / 3, y, Width - Width / 3 - this.Px(14), lh), p.Text, TextFormatFlags.Right);
                y += lh;
            }
        }
    }
}
