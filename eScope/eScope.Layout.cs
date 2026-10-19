using eScope.UI;

namespace eScope
{
    // Window layout: command bar (menus, transport, timebase, theme), side panel of the current trace,
    // trace tabs above the plot card, status bar with the devices
    public partial class eScope : IThemed
    {
        BarPanel sideHost = null!;
        Splitter splitter = null!;
        BarPanel center = null!;
        TraceTabStrip tabStrip = null!;
        PlotCard card = null!;
        StatusStrip statusBar = null!;
        readonly ToolStripStatusLabel statusRight = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight, Tag = "muted" };
        readonly ToolStripLabel timebaseCaption = new("Timebase") { Tag = "muted" };
        readonly ToolStripLabel timebaseValue = new("-") { Tag = "box" };
        readonly ToolStripButton themeButton = new() { Alignment = ToolStripItemAlignment.Right, DisplayStyle = ToolStripItemDisplayStyle.Image };
        readonly ToolStripMenuItem darkModeMenuItem = new("Dark mode") { ShortcutKeys = Keys.Control | Keys.Shift | Keys.D };
        readonly Dictionary<DeviceCtrl, (ToolStripStatusLabel Label, long Frames, DateTime Time, double Rate)> deviceStatus = new();

        void BuildLayout()
        {
            SuspendLayout();
            Controls.Clear();

            // one command bar: menus then transport, the toolbar strip is emptied into it
            menuStrip1.SuspendLayout();
            menuStrip1.MaximumSize = Size.Empty;
            menuStrip1.Stretch = true;
            menuStrip1.AutoSize = false;
            menuStrip1.Dock = DockStyle.Top;
            menuStrip1.Height = this.Px(46);
            menuStrip1.Padding = new Padding(this.Px(6), this.Px(7), this.Px(8), this.Px(7));
            menuStrip1.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            menuStrip1.ImageScalingSize = new Size(this.Px(16), this.Px(16));
            foreach (ToolStripItem item in menuStrip1.Items)
                item.Padding = new Padding(this.Px(6), this.Px(4), this.Px(6), this.Px(4));

            menuStrip1.Items.Add(new ToolStripSeparator { Margin = new Padding(this.Px(6), 0, this.Px(6), 0) });
            foreach (var b in new[] { toolStripButtonRec, PlayButton })
            {
                b.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                b.TextImageRelation = TextImageRelation.ImageBeforeText;
                b.Padding = new Padding(this.Px(8), 0, this.Px(10), 0);
                b.Margin = new Padding(this.Px(2), 0, this.Px(4), 0);
                b.Font = Theme.UiSemibold(9);
                menuStrip1.Items.Add(b);
            }
            foreach (var b in new[] { toolStripButtonBegin, toolStripButtonRewind, toolStripButtonForward, toolStripButtonEnd })
            {
                b.Padding = new Padding(this.Px(6), this.Px(4), this.Px(6), this.Px(4));
                b.ToolTipText = b.Text;
                menuStrip1.Items.Add(b);
            }
            toolStripButtonRec.ToolTipText = "Record (Space)";
            PlayButton.ToolTipText = "Play / Pause (P)";
            menuStrip1.Items.Add(new ToolStripSeparator { Margin = new Padding(this.Px(6), 0, this.Px(6), 0) });
            timebaseCaption.Font = Theme.Ui(9);
            timebaseValue.Font = Theme.MonoSemibold(9);
            timebaseValue.Padding = new Padding(this.Px(8), this.Px(4), this.Px(8), this.Px(4));
            timebaseValue.ToolTipText = "Time per division (zoom: wheel, I / O)";
            menuStrip1.Items.Add(timebaseCaption);
            menuStrip1.Items.Add(timebaseValue);
            themeButton.ToolTipText = "Dark mode (Ctrl+Shift+D)";
            themeButton.Padding = new Padding(this.Px(6), this.Px(4), this.Px(6), this.Px(4));
            themeButton.Click += (_, _) => ToggleDarkMode();
            menuStrip1.Items.Add(themeButton);
            menuStrip1.ResumeLayout();

            darkModeMenuItem.Checked = Theme.IsDark;
            darkModeMenuItem.Click += (_, _) => ToggleDarkMode();
            viewToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            viewToolStripMenuItem.DropDownItems.Add(darkModeMenuItem);
            watchToolStripMenuItem.Text = "Watch (W)";

            statusBar = new StatusStrip
            {
                SizingGrip = false,
                Dock = DockStyle.Bottom,
                RenderMode = ToolStripRenderMode.ManagerRenderMode,
                Padding = new Padding(this.Px(8), 0, this.Px(8), 0),
                ShowItemToolTips = true,
            };
            statusBar.Items.Add(statusRight);

            sideHost = new BarPanel(p => p.Panel, AnchorStyles.Right)
            {
                Dock = DockStyle.Left,
                Width = this.Px(UiSettings.Current.SidePanelWidth),
                Padding = new Padding(0, 0, 1, 0),
            };
            splitter = new Splitter { Dock = DockStyle.Left, Width = this.Px(3), MinSize = this.Px(220), MinExtra = this.Px(320) };
            splitter.SplitterMoved += (_, _) =>
            {
                UiSettings.Current.SidePanelWidth = (int)Math.Round(sideHost.Width * 96.0 / DeviceDpi);
                UiSettings.Current.Save();
            };

            center = new BarPanel(p => p.Bg, AnchorStyles.None)
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(this.Px(10), this.Px(8), this.Px(12), this.Px(12)),
            };
            tabStrip = new TraceTabStrip { Dock = DockStyle.Top, Height = this.Px(34) };
            tabStrip.TabClick += i => { if (i < tabControl1.TabCount) tabControl1.SelectedIndex = i; UpdateTabStrip(); };
            tabStrip.TabDoubleClick += (i, rect) => { if (i < Traces.Count) BeginRenameTrace(Traces[i], rect); };
            tabStrip.AddClick += () => newTraceToolStripMenuItem_Click(this, EventArgs.Empty);
            card = new PlotCard(tabStrip) { Dock = DockStyle.Fill };
            tabControl1.Dock = DockStyle.Fill;
            card.Controls.Add(tabControl1);
            center.Controls.Add(card);
            center.Controls.Add(tabStrip);

            // the last added is docked first
            Controls.Add(center);
            Controls.Add(splitter);
            Controls.Add(sideHost);
            Controls.Add(statusBar);
            Controls.Add(menuStrip1);
            MinimumSize = new Size(this.Px(800), this.Px(500));
            ResumeLayout();
            UpdateToolbarIcons();
        }

        public void ApplyTheme()
        {
            var p = Theme.Current;
            BackColor = p.Bg;
            ForeColor = p.Text;
            splitter.BackColor = p.Bg;
            foreach (var c in new IThemed[] { sideHost, center, tabStrip, card })
                c.ApplyTheme();
            foreach (var t in Traces)
                t.ApplyTheme();
            darkModeMenuItem.Checked = Theme.IsDark;
            if (updateButton != null) updateButton.ForeColor = p.Ok;
            UpdateToolbarIcons();
            UpdateStatus();
            Theme.ApplyTitleBar(this);
            menuStrip1.Invalidate();
            statusBar.Invalidate();
            Invalidate(true);
        }

        void ToggleDarkMode()
        {
            Theme.SetDark(!Theme.IsDark);
            UiSettings.Current.DarkMode = Theme.IsDark;
            UiSettings.Current.Save();
        }

        internal void UpdateTabStrip()
        {
            tabStrip?.SetTabs(Traces.Select(t => t.Data.Name), tabControl1.SelectedIndex, bRecord);
        }

        void ShowSidePanel(TraceCtrl? trc)
        {
            sideHost.SuspendLayout();
            sideHost.Controls.Clear();
            if (trc != null) sideHost.Controls.Add(trc.Side);
            sideHost.ResumeLayout();
        }

        void UpdateTimebase()
        {
            string text = GetCurrentTrace()?.Plot.signalLegend.getXGainLegend() ?? "-";
            if (timebaseValue.Text != text) timebaseValue.Text = text;
        }

        // devices: link, protocol and data rate (frames per second over the last update)
        void UpdateStatus()
        {
            if (statusBar == null) return;
            var p = Theme.Current;
            foreach (var d in deviceStatus.Keys.Where(d => !Devices.Contains(d)).ToList())
            {
                statusBar.Items.Remove(deviceStatus[d].Label);
                deviceStatus.Remove(d);
            }
            int index = 0;
            foreach (var d in Devices)
            {
                if (!deviceStatus.TryGetValue(d, out var st))
                {
                    var label = new ToolStripStatusLabel { Margin = new Padding(0, 0, this.Px(16), 0) };
                    statusBar.Items.Insert(index, label);
                    st = (label, d.FrameCount, DateTime.Now, 0);
                }
                var now = DateTime.Now;
                double seconds = (now - st.Time).TotalSeconds;
                double rate = seconds > 0.2 ? (d.FrameCount - st.Frames) / seconds : st.Rate;
                if (seconds > 0.2) st = (st.Label, d.FrameCount, now, rate);
                deviceStatus[d] = st;

                bool receiving = rate > 0;
                string link = string.Join(" · ", new[] { d.Data.Protocol, d.Data.Com }.Where(x => !string.IsNullOrEmpty(x)));
                st.Label.Text = receiving
                    ? $"{d.Data.Name} · {link} · {rate:0} frames/s"
                    : $"{d.Data.Name} · {link} · idle";
                st.Label.ToolTipText = receiving ? "Receiving data" : "No data received (start a recording)";
                st.Label.Image = Icons.Get(Icons.Record, receiving ? p.Ok : p.Faint, DeviceDpi, 12);
                index++;
            }
        }

        // a text being typed (rename, watch expression, list) keeps the keys: no single-key shortcut
        bool TextInputFocused()
        {
            Control? c = ActiveControl;
            while (c is ContainerControl cc && cc.ActiveControl != null) c = cc.ActiveControl;
            for (Control? x = c; x != null; x = x.Parent)
            {
                if (x is TextBoxBase || x is ComboBox || x is DataGridView || x is NumericUpDown || x is ListBox)
                    return true;
                if (x == this) break;
            }
            return false;
        }
    }
}
