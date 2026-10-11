using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace eScope
{
    // Themes the designer-made forms (dialogs, editors) without touching their code: every form is styled when it
    // is activated (CBT hook), and the open forms again when the theme changes.
    // Controls implementing IThemed draw themselves and are only told to refresh.
    public static class ThemeApplier
    {
        static readonly ConditionalWeakTable<Form, StrongBox<int>> themed = new();

        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        const int WH_CBT = 5;
        const int HCBT_ACTIVATE = 5;
        static HookProc? proc; // kept alive: the hook calls it
        static IntPtr hook;

        public static void Install()
        {
            if (hook != IntPtr.Zero) return;
            proc = Hook;
            hook = SetWindowsHookEx(WH_CBT, proc, IntPtr.Zero, GetCurrentThreadId());
        }

        static IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == HCBT_ACTIVATE && Control.FromHandle(wParam) is Form f)
            {
                try { Apply(f); }
                catch (Exception ex) { Console.WriteLine($"Theme: {ex.Message}"); }
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        public static void ApplyOpenForms()
        {
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
                Apply(f);
        }

        // whole form, once per theme version
        public static void Apply(Form f)
        {
            if (themed.TryGetValue(f, out var v) && v.Value == Theme.Version) return;
            themed.AddOrUpdate(f, new StrongBox<int>(Theme.Version));
            Theme.ApplyTitleBar(f);
            if (f is IThemed t)
            {
                t.ApplyTheme();
                return;
            }
            f.SuspendLayout();
            ApplyTree(f);
            f.ResumeLayout();
            f.Invalidate(true);
        }

        public static void ApplyTree(Control c)
        {
            if (c is IThemed t && c is not Form)
                t.ApplyTheme(); // draws itself; its children are themed below
            else
                ApplyOne(c);
            if (c is ScottPlot.WinForms.FormsPlot) return;
            foreach (Control child in c.Controls)
                ApplyTree(child);
        }

        static void ApplyOne(Control c)
        {
            var p = Theme.Current;
            switch (c)
            {
                case Form f:
                    f.BackColor = p.Surface;
                    f.ForeColor = p.Text;
                    break;
                case Button b:
                    StyleButton(b, p);
                    break;
                case TextBoxBase or NumericUpDown or ListBox or ListView or TreeView:
                    if (Replaceable(c, nameof(Control.BackColor), c.BackColor)) c.BackColor = p.Input;
                    if (Replaceable(c, nameof(Control.ForeColor), c.ForeColor)) c.ForeColor = p.Text;
                    // themed border in light, a plain line in dark (the 3D border stays light)
                    if (c is TextBox tb && tb.BorderStyle != BorderStyle.None)
                        tb.BorderStyle = Theme.IsDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;
                case ComboBox cb:
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = p.Input;
                    cb.ForeColor = p.Text;
                    ComboPainter.Attach(cb);
                    break;
                case DataGridView g:
                    StyleGrid(g);
                    break;
                case LinkLabel l:
                    l.LinkColor = p.Accent;
                    l.ActiveLinkColor = p.Accent;
                    l.VisitedLinkColor = p.Accent;
                    if (Replaceable(c, nameof(Control.ForeColor), c.ForeColor)) c.ForeColor = p.Text;
                    break;
                case TabPage tp:
                    tp.UseVisualStyleBackColor = false;
                    tp.BackColor = p.Panel;
                    tp.ForeColor = p.Text;
                    break;
                case ToolStrip:
                case ScottPlot.WinForms.FormsPlot:
                    break;
                default:
                    if (Replaceable(c, nameof(Control.BackColor), c.BackColor)) c.ResetBackColor();
                    if (Replaceable(c, nameof(Control.ForeColor), c.ForeColor))
                    {
                        // muted text set on purpose (Gray, GrayText) follows the theme too
                        bool muted = c.ForeColor.ToArgb() == Color.Gray.ToArgb() || c.ForeColor.ToArgb() == SystemColors.GrayText.ToArgb()
                            || c.ForeColor.ToArgb() == Palette.Light.Muted.ToArgb() || c.ForeColor.ToArgb() == Palette.Dark.Muted.ToArgb();
                        c.ForeColor = muted ? p.Muted : p.Text;
                    }
                    break;
            }
        }

        // a color left at its default, a system color or a color of a theme: the theme may change it.
        // Any other color was chosen on purpose (e.g. a color swatch)
        static bool Replaceable(Control c, string property, Color color)
        {
            var prop = TypeDescriptor.GetProperties(c)[property];
            if (prop == null || !prop.ShouldSerializeValue(c)) return true;
            if (color.IsSystemColor) return true;
            int argb = color.ToArgb();
            bool back = property == nameof(Control.BackColor);
            if (back && (argb == SystemColors.Control.ToArgb() || argb == SystemColors.Window.ToArgb() || argb == Color.Gainsboro.ToArgb()))
                return true;
            if (!back && (argb == SystemColors.ControlText.ToArgb() || argb == Color.Black.ToArgb()
                || argb == Color.Gray.ToArgb() || argb == SystemColors.GrayText.ToArgb()))
                return true;
            // an invalid field keeps its color until it is validated again
            if (back && (argb == Palette.Light.ErrorBg.ToArgb() || argb == Palette.Dark.ErrorBg.ToArgb()))
                return false;
            if (back)
                return Palette.Light.All().Any(x => x.ToArgb() == argb) || Palette.Dark.All().Any(x => x.ToArgb() == argb);
            return new[] { Palette.Light, Palette.Dark }.SelectMany(t => new[] { t.Text, t.Text2, t.Muted, t.Faint }).Any(x => x.ToArgb() == argb);
        }

        static void StyleButton(Button b, Palette p)
        {
            bool custom = !Replaceable(b, nameof(Control.BackColor), b.BackColor); // color swatch
            bool primary = b.FindForm()?.AcceptButton == b || (b.Tag as string) == "primary";
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.FlatAppearance.BorderColor = primary && !custom ? p.Accent : p.BorderStrong;
            b.FlatAppearance.MouseOverBackColor = custom ? b.BackColor : primary ? Theme.Blend(p.Accent, p.Surface, 0.88f) : p.Hover;
            b.FlatAppearance.MouseDownBackColor = custom ? b.BackColor : primary ? Theme.Blend(p.Accent, p.Surface, 0.75f) : p.Pressed;
            if (!custom) b.BackColor = primary ? p.Accent : p.Input;
            if (Replaceable(b, nameof(Control.ForeColor), b.ForeColor) || b.ForeColor == Palette.Light.OnAccent || b.ForeColor == Palette.Dark.OnAccent)
                b.ForeColor = primary && !custom ? p.OnAccent : p.Text;
        }

        public static void AttachComboPainter(ComboBox combo) => ComboPainter.Attach(combo);

        // flat combo box: border and drop-down arrow drawn with the theme (the system ones stay light)
        sealed class ComboPainter : NativeWindow
        {
            static readonly ConditionalWeakTable<ComboBox, ComboPainter> attached = new();
            readonly ComboBox combo;
            const int WM_PAINT = 0x000F;

            ComboPainter(ComboBox combo)
            {
                this.combo = combo;
                if (combo.IsHandleCreated) AssignHandle(combo.Handle);
                combo.HandleCreated += (_, _) => AssignHandle(combo.Handle);
                combo.HandleDestroyed += (_, _) => ReleaseHandle();
                combo.GotFocus += (_, _) => combo.Invalidate();
                combo.LostFocus += (_, _) => combo.Invalidate();
            }

            public static void Attach(ComboBox combo)
            {
                if (!attached.TryGetValue(combo, out _)) attached.Add(combo, new ComboPainter(combo));
                combo.Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != WM_PAINT || combo.DropDownStyle == ComboBoxStyle.Simple) return;
                var p = Theme.Current;
                using var g = Graphics.FromHwnd(Handle);
                var r = new Rectangle(0, 0, combo.Width - 1, combo.Height - 1);
                int w = SystemInformation.VerticalScrollBarWidth;
                var button = new Rectangle(combo.Width - w - 1, 1, w, combo.Height - 2);
                using (var back = new SolidBrush(combo.Enabled ? p.Input : p.Panel)) g.FillRectangle(back, button);
                using (var border = new Pen(combo.Focused ? p.Accent : p.BorderStrong)) g.DrawRectangle(border, r);
                float cx = button.Left + button.Width / 2f, cy = button.Top + button.Height / 2f, s = combo.DeviceDpi / 96f * 3.5f;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var arrow = new Pen(combo.Enabled ? p.Muted : p.Faint, combo.DeviceDpi / 96f * 1.4f);
                g.DrawLines(arrow, new[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) });
            }
        }

        public static void StyleGrid(DataGridView g)
        {
            var p = Theme.Current;
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = p.Surface;
            g.GridColor = p.Border;
            g.EnableHeadersVisualStyles = false;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle.BackColor = p.Surface;
            g.DefaultCellStyle.ForeColor = p.Text;
            g.DefaultCellStyle.SelectionBackColor = p.Selected;
            g.DefaultCellStyle.SelectionForeColor = p.Text;
            g.ColumnHeadersDefaultCellStyle.BackColor = p.Panel;
            g.ColumnHeadersDefaultCellStyle.ForeColor = p.Muted;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Panel;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.Muted;
            g.RowHeadersDefaultCellStyle.BackColor = p.Panel;
            g.RowHeadersDefaultCellStyle.ForeColor = p.Muted;
            g.AlternatingRowsDefaultCellStyle.BackColor = p.Surface;
        }
    }
}
