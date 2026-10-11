using System.Drawing.Drawing2D;

namespace eScope.UI
{
    // Base of the self-drawn controls: double buffered, not focusable, repainted on theme change
    public abstract class ThemedControl : Control, IThemed
    {
        protected ThemedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }
        public virtual void ApplyTheme() => Invalidate();
        protected Palette P => Theme.Current;
    }

    // TabControl without its own tab headers and borders: the pages fill the control, TraceTabStrip shows the tabs
    public class TabHost : TabControl
    {
        const int TCM_ADJUSTRECT = 0x1328;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == TCM_ADJUSTRECT && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }

    // Panel painted with a theme color and an optional border line on one side
    public class BarPanel : Panel, IThemed
    {
        readonly Func<Palette, Color> back;
        readonly AnchorStyles border;
        public BarPanel(Func<Palette, Color> back, AnchorStyles border)
        {
            this.back = back;
            this.border = border;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = back(Theme.Current);
        }
        public void ApplyTheme()
        {
            BackColor = back(Theme.Current);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Theme.Current.Border);
            if (border.HasFlag(AnchorStyles.Bottom)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            if (border.HasFlag(AnchorStyles.Top)) e.Graphics.DrawLine(pen, 0, 0, Width, 0);
            if (border.HasFlag(AnchorStyles.Right)) e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            if (border.HasFlag(AnchorStyles.Left)) e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }
    }

    // Trace tabs above the plot card: the selected tab opens into the card
    public class TraceTabStrip : ThemedControl
    {
        List<string> names = new();
        int selected = -1;
        bool recording;
        readonly List<Rectangle> rects = new();
        Rectangle addRect;
        int hover = -1;
        bool hoverAdd;

        public event Action<int>? TabClick;
        public event Action<int, Rectangle>? TabDoubleClick;
        public event Action? AddClick;
        public event Action? SelectedSpanChanged;

        // horizontal extent of the selected tab, in the strip coordinates
        public (int X1, int X2) SelectedSpan => selected >= 0 && selected < rects.Count ? (rects[selected].Left, rects[selected].Right) : (-1, -1);

        public void SetTabs(IEnumerable<string> tabNames, int selectedIndex, bool isRecording)
        {
            names = tabNames.ToList();
            selected = selectedIndex;
            recording = isRecording;
            LayoutTabs();
            Invalidate();
            SelectedSpanChanged?.Invoke();
        }

        public Rectangle GetTabRect(int i) => i >= 0 && i < rects.Count ? rects[i] : Rectangle.Empty;

        void LayoutTabs()
        {
            rects.Clear();
            int x = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var font = i == selected ? Theme.UiSemibold(9) : Theme.Ui(9);
                int w = TextRenderer.MeasureText(names[i], font).Width + this.Px(26);
                if (i == selected && recording) w += this.Px(14);
                rects.Add(new Rectangle(x, this.Px(2), w, Height - this.Px(2)));
                x += w + this.Px(2);
            }
            addRect = new Rectangle(x + this.Px(2), this.Px(5), this.Px(28), Height - this.Px(9));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutTabs();
            SelectedSpanChanged?.Invoke();
        }
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            LayoutTabs();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(P.Bg);
            int r = this.Px(6);
            for (int i = 0; i < names.Count; i++)
            {
                var rc = rects[i];
                bool sel = i == selected;
                if (sel)
                {
                    using var path = TopRounded(rc, r);
                    using var brush = new SolidBrush(P.Surface);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(brush, path);
                    using var pen = new Pen(P.Border);
                    g.DrawPath(pen, path);
                    g.SmoothingMode = SmoothingMode.None;
                }
                else if (i == hover)
                {
                    Theme.FillRounded(g, new Rectangle(rc.X, rc.Y + this.Px(2), rc.Width, rc.Height - this.Px(5)), P.Hover, r);
                }
                var text = rc;
                text.X += this.Px(13);
                text.Width -= this.Px(13);
                if (sel && recording)
                {
                    int d = this.Px(7);
                    using var dot = new SolidBrush(P.Rec);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(dot, text.X, rc.Y + (rc.Height - d) / 2, d, d);
                    g.SmoothingMode = SmoothingMode.None;
                    text.X += this.Px(14);
                    text.Width -= this.Px(14);
                }
                Theme.DrawText(g, names[i], sel ? Theme.UiSemibold(9) : Theme.Ui(9), text, sel ? P.Text : (i == hover ? P.Text2 : P.Muted));
            }
            if (hoverAdd) Theme.FillRounded(g, addRect, P.Hover, r);
            var icon = Icons.Get(Icons.Add, P.Muted, DeviceDpi, 12);
            g.DrawImage(icon, addRect.X + (addRect.Width - icon.Width) / 2, addRect.Y + (addRect.Height - icon.Height) / 2);
        }

        // open at the bottom: the tab joins the card below
        static GraphicsPath TopRounded(Rectangle rc, int r)
        {
            var path = new GraphicsPath();
            int bottom = rc.Bottom + 1;
            path.AddLine(rc.Left, bottom, rc.Left, rc.Top + r);
            path.AddArc(rc.Left, rc.Top, r * 2, r * 2, 180, 90);
            path.AddArc(rc.Right - 1 - r * 2, rc.Top, r * 2, r * 2, 270, 90);
            path.AddLine(rc.Right - 1, rc.Top + r, rc.Right - 1, bottom);
            return path;
        }

        int HitTab(Point p) => rects.FindIndex(r => r.Contains(p));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTab(e.Location);
            bool a = addRect.Contains(e.Location);
            if (h != hover || a != hoverAdd)
            {
                hover = h;
                hoverAdd = a;
                Invalidate();
            }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            hoverAdd = false;
            Invalidate();
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            int h = HitTab(e.Location);
            if (h >= 0) TabClick?.Invoke(h);
            else if (addRect.Contains(e.Location)) AddClick?.Invoke();
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int h = HitTab(e.Location);
            if (h >= 0) TabDoubleClick?.Invoke(h, rects[h]);
        }
    }

    // Rounded card holding the plot pages; its top border opens under the selected tab
    public class PlotCard : Panel, IThemed
    {
        readonly TraceTabStrip strip;
        public PlotCard(TraceTabStrip strip)
        {
            this.strip = strip;
            DoubleBuffered = true;
            ResizeRedraw = true;
            strip.SelectedSpanChanged += Invalidate;
            ApplyTheme();
        }
        public void ApplyTheme()
        {
            BackColor = Theme.Current.Surface;
            Padding = new Padding(this.Px(1) + this.Px(4));
            Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Current.Bg);
            var (x1, x2) = strip.SelectedSpan;
            int r = this.Px(8);
            var rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = new GraphicsPath();
            // top-left corner square when the first tab is selected: the tab sits on it
            if (x1 == 0) path.AddLine(rc.Left, rc.Top, rc.Left + r, rc.Top);
            else path.AddArc(rc.Left, rc.Top, r * 2, r * 2, 180, 90);
            path.AddArc(rc.Right - r * 2, rc.Top, r * 2, r * 2, 270, 90);
            path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rc.Left, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Theme.Current.Surface)) g.FillPath(brush, path);
            using (var pen = new Pen(Theme.Current.Border)) g.DrawPath(pen, path);
            g.SmoothingMode = SmoothingMode.None;
            if (x1 >= 0)
            {
                using var pen = new Pen(Theme.Current.Surface);
                g.DrawLine(pen, x1 + 1, 0, x2 - 2, 0);
            }
        }
    }

    // Collapsible header of a side panel section: chevron, title, count, right text and "+" button
    public class SectionHeader : ThemedControl
    {
        public string Title { get; }
        string count = string.Empty;
        string right = string.Empty;
        public bool Collapsed { get; private set; }
        public bool HasAdd { get; set; }
        public string AddTip { get; set; } = string.Empty;
        bool hoverAdd;
        readonly ToolTip tip = new();

        public event Action? Toggled;
        public event Action<Point>? AddClick; // screen point under the "+" button

        public SectionHeader(string title, bool collapsed)
        {
            Title = title;
            Collapsed = collapsed;
            Cursor = Cursors.Hand;
        }

        public void SetCollapsed(bool collapsed)
        {
            if (collapsed == Collapsed) return;
            Collapsed = collapsed;
            Invalidate();
            Toggled?.Invoke();
        }
        public void SetCount(string text)
        {
            if (text == count) return;
            count = text;
            Invalidate();
        }
        public void SetRight(string text)
        {
            if (text == right) return;
            right = text;
            Invalidate();
        }

        Rectangle AddRect => new(Width - this.Px(32), (Height - this.Px(24)) / 2, this.Px(24), this.Px(24));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(P.Panel);
            int x = this.Px(12);
            int cy = Height / 2;
            // chevron
            using (var pen = new Pen(P.Muted, Math.Max(1.2f, this.Px(1.4f))))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float s = this.Px(3.5f);
                float cx = x + this.Px(4);
                if (Collapsed)
                    g.DrawLines(pen, new[] { new PointF(cx - s / 2, cy - s), new PointF(cx + s / 2, cy), new PointF(cx - s / 2, cy + s) });
                else
                    g.DrawLines(pen, new[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) });
                g.SmoothingMode = SmoothingMode.None;
            }
            x += this.Px(16);
            var titleFont = Theme.UiSemibold(8);
            int tw = TextRenderer.MeasureText(Title, titleFont).Width;
            Theme.DrawText(g, Title, titleFont, new Rectangle(x, 0, tw + this.Px(4), Height), P.Text2);
            x += tw + this.Px(4);
            if (count.Length != 0)
                Theme.DrawText(g, count, Theme.Ui(8), new Rectangle(x, 0, this.Px(40), Height), P.Faint);

            int rightEdge = HasAdd ? AddRect.Left - this.Px(4) : Width - this.Px(12);
            if (right.Length != 0)
                Theme.DrawText(g, right, Theme.Mono(8.5f), new Rectangle(x + this.Px(40), 0, rightEdge - x - this.Px(40), Height), P.Accent, TextFormatFlags.Right);
            if (HasAdd)
            {
                if (hoverAdd) Theme.FillRounded(g, AddRect, P.Hover);
                var icon = Icons.Get(Icons.Add, P.Muted, DeviceDpi, 12);
                g.DrawImage(icon, AddRect.X + (AddRect.Width - icon.Width) / 2, AddRect.Y + (AddRect.Height - icon.Height) / 2);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = HasAdd && AddRect.Contains(e.Location);
            if (h != hoverAdd)
            {
                hoverAdd = h;
                tip.SetToolTip(this, h ? AddTip : string.Empty);
                Invalidate();
            }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverAdd = false;
            Invalidate();
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (HasAdd && AddRect.Contains(e.Location))
            {
                AddClick?.Invoke(PointToScreen(new Point(AddRect.Left, AddRect.Bottom)));
                return;
            }
            SetCollapsed(!Collapsed);
        }
    }

    // One channel of the side panel: color, name, value at the watch cursor, scale and offset
    public class ChannelRow : ThemedControl
    {
        string name = string.Empty, value = string.Empty, scale = string.Empty;
        Color color;
        bool hidden;
        bool highlight;

        public ChannelRow()
        {
            Height = this.Px(44);
        }

        public void SetData(string name, string value, string scale, Color color, bool hidden)
        {
            if (name == this.name && value == this.value && scale == this.scale && color == this.color && hidden == this.hidden) return;
            (this.name, this.value, this.scale, this.color, this.hidden) = (name, value, scale, color, hidden);
            Invalidate();
        }
        public bool Highlight
        {
            get => highlight;
            set
            {
                if (value == highlight) return;
                highlight = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(P.Panel);
            var bg = new Rectangle(this.Px(6), this.Px(1), Width - this.Px(12), Height - this.Px(2));
            if (highlight) Theme.FillRounded(g, bg, P.Selected, this.Px(6));

            int s = this.Px(12);
            var sw = new Rectangle(this.Px(14), this.Px(9), s, s);
            if (hidden)
                Theme.DrawRounded(g, new Rectangle(sw.X + 1, sw.Y + 1, s - 3, s - 3), color, this.Px(3));
            else
                Theme.FillRounded(g, sw, color, this.Px(3));

            Color text = hidden ? P.Faint : P.Text;
            int x = this.Px(36);
            int valueWidth = TextRenderer.MeasureText(value, Theme.Mono(9)).Width;
            int right = Width - this.Px(14);
            Theme.DrawText(g, name, Theme.MonoSemibold(9), new Rectangle(x, this.Px(4), right - valueWidth - x - this.Px(6), this.Px(20)), text);
            Theme.DrawText(g, value, Theme.Mono(9), new Rectangle(right - valueWidth - this.Px(4), this.Px(4), valueWidth + this.Px(4), this.Px(20)), text, TextFormatFlags.Right);
            Theme.DrawText(g, hidden ? "hidden · " + scale : scale, Theme.Ui(8), new Rectangle(x, this.Px(22), right - x, this.Px(18)), hidden ? P.Faint : P.Muted);
        }
    }

    // One marker, delta, data marker or text annotation of the side panel
    public class MarkerRow : ThemedControl
    {
        public enum Kind { Time, Delta, Value, Text }
        public Plot.CtrlBase Ctrl { get; }
        readonly Kind kind;
        string name = string.Empty, info = string.Empty;
        Color color;
        bool hidden;
        bool hover;

        public MarkerRow(Plot.CtrlBase ctrl, Kind kind)
        {
            Ctrl = ctrl;
            this.kind = kind;
            Height = this.Px(26);
        }

        public void SetData(string name, string info, Color color, bool hidden)
        {
            if (name == this.name && info == this.info && color == this.color && hidden == this.hidden) return;
            (this.name, this.info, this.color, this.hidden) = (name, info, color, hidden);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(P.Panel);
            if (hover) Theme.FillRounded(g, new Rectangle(this.Px(6), 0, Width - this.Px(12), Height), P.Hover, this.Px(6));
            var c = hidden ? P.Faint : color;
            int cx = this.Px(20), cy = Height / 2, s = this.Px(5);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(c, Math.Max(1.2f, this.Px(1.5f))))
            using (var brush = new SolidBrush(c))
            {
                switch (kind)
                {
                    case Kind.Time: // flag on a pole
                        g.DrawLine(pen, cx - s, cy - s - 1, cx - s, cy + s + 1);
                        g.FillPolygon(brush, new[] { new PointF(cx - s, cy - s - 1), new PointF(cx + s, cy - s / 2f), new PointF(cx - s, cy) });
                        break;
                    case Kind.Delta: // span between two bars
                        g.DrawLine(pen, cx - s, cy - s, cx - s, cy + s);
                        g.DrawLine(pen, cx + s, cy - s, cx + s, cy + s);
                        g.DrawLine(pen, cx - s, cy, cx + s, cy);
                        break;
                    case Kind.Value: // horizontal level
                        g.DrawLine(pen, cx - s - 1, cy, cx + s + 1, cy);
                        g.FillEllipse(brush, cx - s / 2f, cy - s / 2f, s, s);
                        break;
                    default: // speech bubble
                        g.DrawRectangle(pen, cx - s, cy - s, s * 2, s * 1.4f);
                        g.FillPolygon(brush, new[] { new PointF(cx - s / 2f, cy + s * 0.4f), new PointF(cx - s / 2f, cy + s + 1), new PointF(cx + s / 4f, cy + s * 0.4f) });
                        break;
                }
            }
            g.SmoothingMode = SmoothingMode.None;
            int x = this.Px(36);
            int right = Width - this.Px(14);
            int iw = TextRenderer.MeasureText(info, Theme.Mono(8.5f)).Width;
            Theme.DrawText(g, name, Theme.Ui(9), new Rectangle(x, 0, right - iw - x - this.Px(6), Height), hidden ? P.Faint : P.Text);
            Theme.DrawText(g, info, Theme.Mono(8.5f), new Rectangle(right - iw - this.Px(4), 0, iw + this.Px(4), Height), P.Muted, TextFormatFlags.Right);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            Invalidate();
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false;
            Invalidate();
        }
    }
}
