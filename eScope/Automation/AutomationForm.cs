namespace eScope.Automation
{
    // Tools > AI automation: server activation, port, write permission, token and client setup
    public class AutomationForm : Form
    {
        readonly AutomationManager manager;
        readonly AutomationSettings edited;

        readonly CheckBox enabledBox = new() { Text = "Enable the automation server (MCP + REST, local only)", AutoSize = true };
        readonly NumericUpDown portBox = new() { Minimum = 1024, Maximum = 65535 };
        readonly CheckBox writeBox = new() { Text = "Allow writing target memory (write_variable, write_memory)", AutoSize = true };
        readonly TextBox tokenBox = new() { ReadOnly = true };
        readonly TextBox commandBox = new() { ReadOnly = true, Multiline = true, WordWrap = true };
        readonly Label statusLabel = new() { AutoSize = true };

        public AutomationForm(AutomationManager manager)
        {
            this.manager = manager;
            var s = manager.Settings;
            edited = new AutomationSettings { Enabled = s.Enabled, Port = s.Port, AllowWrite = s.AllowWrite, Token = s.Token };
            edited.EnsureToken(); // shown even before the first activation, so the client can be set up first

            Text = "AI automation";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            int width = 44 * Font.Height;
            Label Text_(string text, int bottom = 8) => new()
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                Margin = new Padding(3, 0, 3, bottom)
            };
            Button Button_(string text, EventHandler onClick)
            {
                var b = new Button { Text = text, AutoSize = true };
                b.Click += onClick;
                return b;
            }
            FlowLayoutPanel Row(params Control[] controls)
            {
                var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
                row.Controls.AddRange(controls);
                return row;
            }

            var intro = Text_(
                "AI agents (Claude Code, Claude Desktop, Cursor... through MCP) and scripts (REST) can drive eScope: " +
                "acquisitions, channels, data, analysis, exports, target memory. Changes they make can be undone with Ctrl+Z.");

            enabledBox.Checked = edited.Enabled;
            enabledBox.Margin = new Padding(3, 0, 3, 8);
            portBox.Value = Math.Clamp(edited.Port, 1024, 65535);
            portBox.Width = 6 * Font.Height;
            writeBox.Checked = edited.AllowWrite;
            writeBox.Margin = new Padding(3, 0, 3, 8);
            tokenBox.Width = width - 14 * Font.Height;
            commandBox.Size = new Size(width, 4 * Font.Height);
            commandBox.Margin = new Padding(3, 0, 3, 8);

            var portLabel = Text_("Port:", 0);
            portLabel.Anchor = AnchorStyles.Left;
            var tokenLabel = Text_("Token:", 0);
            tokenLabel.Anchor = AnchorStyles.Left;

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            ok.Click += (_, _) => Save();
            AcceptButton = ok;
            CancelButton = cancel;
            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 4, 0, 0)
            };
            buttons.Controls.AddRange(new Control[] { cancel, ok });

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(12)
            };
            layout.Controls.AddRange(new Control[]
            {
                intro,
                enabledBox,
                Row(portLabel, portBox),
                writeBox,
                Row(tokenLabel, tokenBox,
                    Button_("Copy", (_, _) => Clipboard.SetText(tokenBox.Text)),
                    Button_("Regenerate", (_, _) => { edited.RegenerateToken(); UpdateTexts(); })),
                Text_("Claude Code setup (the other MCP clients take the same URL and header):", 2),
                commandBox,
                Button_("Copy command", (_, _) => Clipboard.SetText(commandBox.Text)),
                statusLabel,
                buttons,
            });
            Controls.Add(layout);

            portBox.ValueChanged += (_, _) => UpdateTexts();
            UpdateTexts();
        }

        void UpdateTexts()
        {
            int port = (int)portBox.Value;
            tokenBox.Text = edited.Token;
            commandBox.Text = $"claude mcp add --transport http escope http://localhost:{port}/mcp " +
                              $"--header \"Authorization: Bearer {edited.Token}\"";
            statusLabel.Text = manager.IsRunning ? $"Status: listening on {manager.McpUrl}"
                : manager.Error != null ? $"Status: error, {manager.Error}"
                : "Status: stopped";
        }

        void Save()
        {
            edited.Enabled = enabledBox.Checked;
            edited.Port = (int)portBox.Value;
            edited.AllowWrite = writeBox.Checked;
            manager.Apply(edited);
            if (edited.Enabled && !manager.IsRunning)
                MessageBox.Show(this, $"The automation server cannot start: {manager.Error}", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
