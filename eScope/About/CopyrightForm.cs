using System.Diagnostics;
using System.Reflection;
using eScope.License;

namespace eScope.About
{
    // About > Copyright: copyright, license terms, warranty disclaimer and the third-party components shipped
    public class CopyrightForm : Form
    {
        const string Author = "Laurent Olive";
        const int FirstYear = 2023;

        // component, license, project page (opened by a double-click)
        static readonly (string Name, string License, string Url)[] Components =
        {
            ("ScottPlot", "MIT", "https://github.com/ScottPlot/ScottPlot"),
            ("SkiaSharp", "MIT", "https://github.com/mono/SkiaSharp"),
            ("OpenTK", "MIT", "https://github.com/opentk/opentk"),
            ("RJCP SerialPortStream", "MS-PL", "https://github.com/jcurl/RJCP.DLL.SerialPortStream"),
            ("Mono.Posix", "MIT", "https://github.com/mono/mono.posix"),
            ("Roslyn (Microsoft.CodeAnalysis)", "MIT", "https://github.com/dotnet/roslyn"),
            (".NET libraries (System.IO.Pipelines, System.IO.Ports...)", "MIT", "https://github.com/dotnet/runtime"),
            ("Serilog", "Apache-2.0", "https://github.com/serilog/serilog"),
            ("Model Context Protocol C# SDK", "Apache-2.0", "https://github.com/modelcontextprotocol/csharp-sdk"),
            ("Newtonsoft.Json", "MIT", "https://github.com/JamesNK/Newtonsoft.Json"),
            ("IronPython / Dynamic Language Runtime", "Apache-2.0", "https://github.com/IronLanguages/ironpython3"),
            ("GDB (tools\\gdb, unmodified MSYS2 build)", "GPL-3.0-or-later", "https://sourceware.org/gdb/"),
        };

        static string GdbDir => Path.Combine(AppContext.BaseDirectory, "tools", "gdb");

        public CopyrightForm()
        {
            Text = "Copyright";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            // sized by its content: no clipped text whatever the font / DPI scaling
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            int width = 40 * Font.Height; // text column, scales with the font
            Label Text_(string text, Font? font = null, int bottom = 8) => new()
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                Font = font ?? Font,
                Margin = new Padding(3, 0, 3, bottom)
            };

            var title = Text_("eScope", new Font(Font.FontFamily, Font.Size * 1.6f, FontStyle.Bold), 0);
            var version = Text_("Version " + global::eScope.eScope.AppVersion);
            var copyright = Text_($"Copyright © {FirstYear}–{BuildYear()} {Author}", bottom: 12);

            var terms = Text_(
                "eScope is source-available under the PolyForm Noncommercial License 1.0.0 " +
                "(https://polyformproject.org/licenses/noncommercial/1.0.0): free for noncommercial use; " +
                "a Professional license is required for work, business or client use.\n" +
                $"Current license: {LicenseManager.GetLicenseStatusText()}");

            var warranty = Text_(
                "THIS SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND. eScope reads and writes the " +
                "memory of the connected target: the author cannot be held liable for any damage to the device, " +
                "the equipment it drives or the data, resulting from its use.", bottom: 12);

            var componentsTitle = Text_("Third-party components (double-click to open the project page):",
                new Font(Font, FontStyle.Bold), 4);

            var list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Size = new Size(width, (Components.Length + 2) * (Font.Height + 4)),
                Margin = new Padding(3, 0, 3, 8)
            };
            list.Columns.Add("Component", width * 2 / 3);
            list.Columns.Add("License", width / 3 - SystemInformation.VerticalScrollBarWidth);
            foreach (var c in Components)
                list.Items.Add(new ListViewItem(new[] { c.Name, c.License }) { Tag = c.Url });
            list.ItemActivate += (_, _) =>
            {
                if (list.SelectedItems.Count == 1)
                    OpenUrl((string)list.SelectedItems[0].Tag!);
            };

            var gdb = Text_(
                "GDB and its runtime libraries are free software, shipped unmodified. Their license texts and " +
                "where to get the corresponding source code are in the tools\\gdb folder (SOURCES.txt, share\\licenses).");

            var openGdb = new Button
            {
                Text = "Open GDB licenses folder",
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2),
                Enabled = Directory.Exists(GdbDir)
            };
            openGdb.Click += (_, _) => OpenUrl(GdbDir);

            var close = new Button
            {
                Text = "Close",
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2),
                Anchor = AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };
            CancelButton = close;
            AcceptButton = close;

            var buttons = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.Controls.Add(openGdb, 0, 0);
            buttons.Controls.Add(close, 1, 0);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12)
            };
            layout.Controls.AddRange(new Control[] { title, version, copyright, terms, warranty, componentsTitle, list, gdb, buttons });
            Controls.Add(layout);
        }

        // year of this build, so the copyright range follows the releases
        static int BuildYear()
        {
            try { return Math.Max(FirstYear, File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).Year); }
            catch { return DateTime.Now.Year; }
        }

        static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
