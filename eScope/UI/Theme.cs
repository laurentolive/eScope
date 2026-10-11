using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace eScope
{
    // Color tokens of one theme (light or dark)
    public class Palette
    {
        public Color Bg, Chrome, Surface, Panel, Popup, Input;
        public Color Border, BorderStrong, Grid, GridMajor;
        public Color Text, Text2, Muted, Faint;
        public Color Hover, Pressed, Selected, Chip;
        public Color Accent, OnAccent, AccentSubtle;
        public Color Rec, RecBg, RecBorder, RecText, Ok;
        public Color ErrorBg; // background of an invalid field

        static Color H(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        public static readonly Palette Light = new()
        {
            Bg = H(0xF3F4F6), Chrome = H(0xE9EBEE), Surface = H(0xFFFFFF), Panel = H(0xF8F9FA), Popup = H(0xFFFFFF), Input = H(0xFFFFFF),
            Border = H(0xE3E5E8), BorderStrong = H(0xD0D3D8), Grid = H(0xEEF0F2), GridMajor = H(0xDDE0E4),
            Text = H(0x1B1D21), Text2 = H(0x3B3F45), Muted = H(0x5B6068), Faint = H(0x8B9098),
            Hover = H(0xEBEDF0), Pressed = H(0xE0E3E7), Selected = H(0xE4ECF6), Chip = H(0xF1F2F4),
            Accent = H(0x005FB8), OnAccent = H(0xFFFFFF), AccentSubtle = H(0xDCE8F5),
            Rec = H(0xD13438), RecBg = H(0xFDF0F0), RecBorder = H(0xF1B9BA), RecText = H(0xA4262C), Ok = H(0x107C10),
            ErrorBg = H(0xFDE2E2),
        };

        public static readonly Palette Dark = new()
        {
            Bg = H(0x18191C), Chrome = H(0x111214), Surface = H(0x1F2024), Panel = H(0x1B1C1F), Popup = H(0x26282D), Input = H(0x26282D),
            Border = H(0x2C2E33), BorderStrong = H(0x3D4047), Grid = H(0x27292E), GridMajor = H(0x34373D),
            Text = H(0xE8E9EB), Text2 = H(0xC9CCD1), Muted = H(0x9A9FA8), Faint = H(0x6E737C),
            Hover = H(0x2C2E33), Pressed = H(0x35383E), Selected = H(0x1E3550), Chip = H(0x2A2C31),
            Accent = H(0x4F9BE8), OnAccent = H(0x0B1622), AccentSubtle = H(0x1E3550),
            Rec = H(0xFF5A5F), RecBg = H(0x3A1B1D), RecBorder = H(0x6B2A2E), RecText = H(0xFF9A9E), Ok = H(0x4CC38A),
            ErrorBg = H(0x4A2226),
        };

        // every token, to recognize a color set by a previous theme
        public IEnumerable<Color> All() => typeof(Palette).GetFields()
            .Where(f => f.FieldType == typeof(Color)).Select(f => (Color)f.GetValue(this)!);
    }

    // Implemented by the controls that draw themselves with the palette: the generic theming leaves them alone
    public interface IThemed
    {
        void ApplyTheme();
    }

    public static class Theme
    {
        public static Palette Current { get; private set; } = Palette.Light;
        public static bool IsDark => Current == Palette.Dark;
        public static int Version { get; private set; } // incremented on each change
        public static event Action? Changed;

        // shorthands used by the renderer and the icons
        public static Color Surface => Current.Surface;
        public static Color Popup => Current.Popup;
        public static Color Border => Current.Border;
        public static Color Hover => Current.Hover;
        public static Color Pressed => Current.Pressed;
        public static Color Text => Current.Text;
        public static Color TextMuted => Current.Muted;
        public static Color TextDisabled => Current.Faint;
        public static Color Accent => Current.Accent;
        public static Color AccentSubtle => Current.AccentSubtle;

        public const int Radius = 4;

        // every ToolStrip in ManagerRenderMode (context menus included) uses this renderer
        public static void Initialize(bool dark)
        {
            Current = dark ? Palette.Dark : Palette.Light;
            ToolStripManager.Renderer = new ThemeRenderer();
            ThemeApplier.Install();
        }

        public static void SetDark(bool dark)
        {
            if (dark == IsDark) return;
            Current = dark ? Palette.Dark : Palette.Light;
            Version++;
            ToolStripManager.Renderer = new ThemeRenderer();
            Changed?.Invoke();
            ThemeApplier.ApplyOpenForms();
        }

        // --- Fonts ---

        static readonly Dictionary<(string, float, FontStyle), Font> fonts = new();
        static readonly HashSet<string> installed = InstalledFamilies();
        static HashSet<string> InstalledFamilies()
        {
            using var c = new InstalledFontCollection();
            return c.Families.Select(f => f.Name).ToHashSet();
        }
        static string Pick(params string[] names) => names.FirstOrDefault(installed.Contains) ?? "Segoe UI";

        public static readonly string UiFamily = Pick("Segoe UI Variable Text", "Segoe UI");
        public static readonly string UiSemiboldFamily = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold", "Segoe UI");
        public static readonly string MonoFamily = Pick("Cascadia Mono", "Consolas");
        public static readonly string MonoSemiboldFamily = Pick("Cascadia Mono SemiBold", "Cascadia Mono", "Consolas");

        static Font Get(string family, float size, FontStyle style = FontStyle.Regular)
        {
            if (!fonts.TryGetValue((family, size, style), out var f))
                fonts[(family, size, style)] = f = new Font(family, size, style);
            return f;
        }
        // sizes in points: they follow the monitor dpi
        public static Font Ui(float size = 9) => Get(UiFamily, size);
        public static Font UiSemibold(float size = 9) => Get(UiSemiboldFamily, size);
        public static Font Mono(float size = 9) => Get(MonoFamily, size);
        public static Font MonoSemibold(float size = 9) => Get(MonoSemiboldFamily, size);

        // --- Drawing helpers ---

        public static int Px(this Control c, float v) => (int)Math.Round(v * c.DeviceDpi / 96.0);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Rectangle r, Color c, int radius = Radius)
        {
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(r, radius);
            using var brush = new SolidBrush(c);
            g.FillPath(brush, path);
            g.SmoothingMode = mode;
        }

        public static void DrawRounded(Graphics g, Rectangle r, Color c, int radius = Radius)
        {
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(r, radius);
            using var pen = new Pen(c);
            g.DrawPath(pen, path);
            g.SmoothingMode = mode;
        }

        public static void DrawText(Graphics g, string text, Font font, Rectangle r, Color c,
            TextFormatFlags align = TextFormatFlags.Left)
        {
            TextRenderer.DrawText(g, text, font, r, c,
                align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // --- Trace colors ---

        // dark colors of the user palette are lifted in dark mode so the curves stay readable on the dark background
        public static Color TraceColor(Color c)
        {
            if (!IsDark) return c;
            float l = c.GetBrightness();
            if (l >= 0.55f) return c;
            return FromHsl(c.GetHue(), Math.Max(c.GetSaturation(), 0.45f), 0.62f, c.A);
        }
        public static ScottPlot.Color TraceColor(string hex)
        {
            var c = ScottPlot.Color.FromHex(hex);
            var t = TraceColor(Color.FromArgb(c.A, c.R, c.G, c.B));
            return new ScottPlot.Color(t.R, t.G, t.B, t.A);
        }
        public static ScottPlot.Color Sc(Color c) => new(c.R, c.G, c.B, c.A);

        static Color FromHsl(float h, float s, float l, int a)
        {
            float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
            float p = 2 * l - q;
            float Hue(float t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1f / 6) return p + (q - p) * 6 * t;
                if (t < 0.5f) return q;
                if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
                return p;
            }
            float k = h / 360f;
            return Color.FromArgb(a, (int)Math.Round(Hue(k + 1f / 3) * 255), (int)Math.Round(Hue(k) * 255), (int)Math.Round(Hue(k - 1f / 3) * 255));
        }

        // background of a text field after validation
        public static Color FieldBack(bool valid) => valid ? Current.Input : Current.ErrorBg;

        public static Color Blend(Color a, Color b, float amount)
        {
            int Mix(int x, int y) => (int)Math.Round(x * amount + y * (1 - amount));
            return Color.FromArgb(255, Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B));
        }

        // --- Title bar ---

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_CAPTION_COLOR = 35;
        const int DWMWA_TEXT_COLOR = 36;

        // dark title bar in dark mode; on Windows 11 the caption takes the chrome color
        public static void ApplyTitleBar(Form f)
        {
            if (!f.IsHandleCreated) return;
            int dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int caption = ColorTranslator.ToWin32(Current.Chrome);
            DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            int text = ColorTranslator.ToWin32(Current.Text);
            DwmSetWindowAttribute(f.Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }
    }

    // Toolbar icons drawn from the Windows icon font: sharp at any DPI and colored by the theme
    public static class Icons
    {
        public const string Begin = "";
        public const string End = "";
        public const string Rewind = "";
        public const string Forward = "";
        public const string Play = "";
        public const string Pause = "";
        public const string Moon = "";
        public const string Sun = "";
        public const string Add = "";
        public const string Link = "";
        public const string Record = "record"; // drawn as a disc: the font glyph differs between Windows 10 and 11

        // Segoe Fluent Icons ships with Windows 11, Segoe MDL2 Assets with Windows 10
        static readonly string family = Installed("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        static readonly Dictionary<(string, Color, int, int), Bitmap> cache = new();

        static bool Installed(string name)
        {
            using var fonts = new InstalledFontCollection();
            return fonts.Families.Any(f => f.Name == name);
        }

        // size x size logical pixels, scaled to the dpi
        public static Bitmap Get(string glyph, Color color, int dpi, int size = 16)
        {
            if (cache.TryGetValue((glyph, color, dpi, size), out var bmp)) return bmp;

            float scale = dpi / 96f;
            int px = (int)Math.Round(size * scale);
            bmp = new Bitmap(px, px);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using var brush = new SolidBrush(color);
                if (glyph == Record)
                {
                    float d = size * 0.6f * scale;
                    g.FillEllipse(brush, (px - d) / 2, (px - d) / 2, d, d);
                }
                else
                {
                    using var font = new Font(family, size * 0.75f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
                    using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(glyph, font, brush, new RectangleF(0, 0, px, px), format);
                }
            }
            cache[(glyph, color, dpi, size)] = bmp;
            return bmp;
        }
    }

    // Flat rendering: no gradient, rounded hover, thin border around the drop-downs.
    // Item tags: "accent" (filled with the accent color), "rec" (recording), "outline" (bordered), "box" (bordered label)
    public class ThemeRenderer : ToolStripProfessionalRenderer
    {
        public ThemeRenderer() : base(new ThemeColorTable())
        {
            RoundedEdges = false;
        }

        // Windows 11 rounds the drop-downs and draws their border itself
        static readonly bool roundedPopups = Environment.OSVersion.Version.Build >= 22000;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWCP_ROUNDSMALL = 3;

        protected override void Initialize(ToolStrip toolStrip)
        {
            base.Initialize(toolStrip);
            if (roundedPopups && toolStrip is ToolStripDropDown dropDown)
            {
                if (dropDown.IsHandleCreated) RoundCorners(dropDown);
                dropDown.HandleCreated += (_, _) => RoundCorners(dropDown);
            }
        }

        static void RoundCorners(Control c)
        {
            int corner = DWMWCP_ROUNDSMALL;
            DwmSetWindowAttribute(c.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            int border = ColorTranslator.ToWin32(Theme.Current.BorderStrong);
            DwmSetWindowAttribute(c.Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Color c = e.ToolStrip switch
            {
                ToolStripDropDown => Theme.Popup,
                StatusStrip => Theme.Current.Chrome,
                _ => Theme.Surface,
            };
            using var brush = new SolidBrush(c);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is not ToolStripDropDown || roundedPopups) return;
            using var pen = new Pen(Theme.Current.BorderStrong);
            var r = e.AffectedBounds;
            e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Theme.Popup);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            if (!item.Enabled || !(item.Selected || item.Pressed)) return;

            Rectangle r = item.IsOnDropDown
                ? new Rectangle(4, 1, item.Width - 8, item.Height - 2)
                : new Rectangle(1, 3, item.Width - 2, item.Height - 6);
            Theme.FillRounded(e.Graphics, r, item.Pressed && !item.IsOnDropDown ? Theme.Pressed : Theme.Hover);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            var p = Theme.Current;
            var r = new Rectangle(1, 2, item.Width - 2, item.Height - 4);
            string? tag = item.Tag as string;
            if (tag == "accent")
            {
                Color c = item.Pressed ? Theme.Blend(p.Accent, p.Surface, 0.8f) : item.Selected ? Theme.Blend(p.Accent, p.Surface, 0.9f) : p.Accent;
                Theme.FillRounded(e.Graphics, r, c);
                return;
            }
            if (tag == "rec")
            {
                Theme.FillRounded(e.Graphics, r, item.Selected ? Theme.Blend(p.RecBorder, p.RecBg, 0.35f) : p.RecBg);
                Theme.DrawRounded(e.Graphics, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), p.RecBorder);
                return;
            }
            bool isChecked = item is ToolStripButton b && b.Checked;
            Color? fill = item.Pressed ? p.Pressed
                : item.Selected && item.Enabled ? p.Hover
                : isChecked ? p.AccentSubtle
                : null;
            if (fill != null) Theme.FillRounded(e.Graphics, r, fill.Value);
            if (tag == "outline")
                Theme.DrawRounded(e.Graphics, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), p.BorderStrong);
        }

        protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            var r = new Rectangle(1, 2, item.Width - 2, item.Height - 4);
            if ((item.Tag as string) == "box")
                Theme.DrawRounded(e.Graphics, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), Theme.Current.BorderStrong);
            else if (item.Selected && item.IsOnOverflow == false && item.Owner is not StatusStrip)
                Theme.FillRounded(e.Graphics, r, Theme.Hover);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Current.Border);
            var item = e.Item;
            if (e.Vertical)
            {
                int x = item.Width / 2;
                e.Graphics.DrawLine(pen, x, item.Height / 4, x, item.Height * 3 / 4);
            }
            else
            {
                int y = item.Height / 2;
                e.Graphics.DrawLine(pen, 8, y, item.Width - 8, y);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // keep a color set on purpose by the item
            if (!e.Item.Enabled) e.TextColor = Theme.TextDisabled;
            else if (e.TextColor == SystemColors.ControlText || e.TextColor == SystemColors.MenuText
                || e.TextColor == SystemColors.HighlightText || Palette.Light.All().Contains(e.TextColor) || Palette.Dark.All().Contains(e.TextColor))
            {
                e.TextColor = (e.Item.Tag as string) switch
                {
                    "accent" => Theme.Current.OnAccent,
                    "rec" => Theme.Current.RecText,
                    "muted" => Theme.Current.Muted,
                    _ => Theme.Text,
                };
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == false ? Theme.TextDisabled : Theme.TextMuted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            r.Inflate(2, 2);
            Theme.FillRounded(e.Graphics, r, Theme.AccentSubtle);
            // the check glyph in the theme text color
            using var pen = new Pen(Theme.Text, Math.Max(1.5f, e.ImageRectangle.Height / 10f));
            var c = e.ImageRectangle;
            var mode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, new[]
            {
                new PointF(c.Left + c.Width * 0.22f, c.Top + c.Height * 0.52f),
                new PointF(c.Left + c.Width * 0.42f, c.Top + c.Height * 0.72f),
                new PointF(c.Left + c.Width * 0.78f, c.Top + c.Height * 0.30f),
            });
            e.Graphics.SmoothingMode = mode;
        }
    }

    // fallback colors for what the professional renderer still draws itself
    public class ThemeColorTable : ProfessionalColorTable
    {
        public ThemeColorTable() { UseSystemColors = false; }

        public override Color MenuBorder => Theme.Current.BorderStrong;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemSelectedGradientBegin => Theme.Hover;
        public override Color MenuItemSelectedGradientEnd => Theme.Hover;
        public override Color MenuItemPressedGradientBegin => Theme.Pressed;
        public override Color MenuItemPressedGradientEnd => Theme.Pressed;
        public override Color MenuStripGradientBegin => Theme.Surface;
        public override Color MenuStripGradientEnd => Theme.Surface;
        public override Color ToolStripBorder => Theme.Surface;
        public override Color ToolStripDropDownBackground => Theme.Popup;
        public override Color ToolStripGradientBegin => Theme.Surface;
        public override Color ToolStripGradientMiddle => Theme.Surface;
        public override Color ToolStripGradientEnd => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Popup;
        public override Color ImageMarginGradientMiddle => Theme.Popup;
        public override Color ImageMarginGradientEnd => Theme.Popup;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
        public override Color ButtonSelectedHighlight => Theme.Hover;
        public override Color ButtonSelectedBorder => Color.Transparent;
        public override Color ButtonPressedBorder => Color.Transparent;
        public override Color ButtonCheckedHighlight => Theme.AccentSubtle;
        public override Color CheckBackground => Theme.AccentSubtle;
        public override Color CheckSelectedBackground => Theme.AccentSubtle;
        public override Color CheckPressedBackground => Theme.AccentSubtle;
        public override Color OverflowButtonGradientBegin => Theme.Surface;
        public override Color OverflowButtonGradientMiddle => Theme.Surface;
        public override Color OverflowButtonGradientEnd => Theme.Surface;
        public override Color StatusStripGradientBegin => Theme.Current.Chrome;
        public override Color StatusStripGradientEnd => Theme.Current.Chrome;
        public override Color GripDark => Theme.Border;
        public override Color GripLight => Theme.Surface;
    }
}
