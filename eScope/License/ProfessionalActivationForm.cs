using System.Diagnostics;

namespace eScope.License
{
    public class ProfessionalActivationForm : Form
    {
        public LicenseInfo? License { get; private set; }

        readonly TextBox textbox;
        readonly Button activate;
        readonly Label status;
        bool busy;

        public ProfessionalActivationForm()
        {
            Text = "Activate Professional License";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            // sized by its content: no clipped text whatever the font / DPI scaling
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // wide enough for a Lemon Squeezy key (UUID)
            int keyWidth = TextRenderer.MeasureText("XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX", Font).Width + 20;

            var label = new Label
            {
                Text = "Enter your Professional license key:",
                AutoSize = true
            };

            textbox = new TextBox
            {
                Dock = DockStyle.Fill,
                MinimumSize = new Size(keyWidth, 0)
            };

            status = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(keyWidth, 0), // long errors wrap instead of widening the dialog
                MinimumSize = new Size(0, 2 * Font.Height),
                ForeColor = Color.Firebrick
            };

            var buy = new LinkLabel
            {
                Text = "Buy a license",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            buy.LinkClicked += (_, _) => OpenUrl(LemonSqueezy.BuyUrl);

            activate = new Button
            {
                Text = "Activate",
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2)
            };
            activate.Click += async (_, _) => await ActivateKey();

            var cancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2),
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = activate;
            CancelButton = cancel;

            // link on the left, buttons on the right
            var layout = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.Controls.Add(label, 0, 0);
            layout.SetColumnSpan(label, 3);
            layout.Controls.Add(textbox, 0, 1);
            layout.SetColumnSpan(textbox, 3);
            layout.Controls.Add(status, 0, 2);
            layout.SetColumnSpan(status, 3);
            layout.Controls.Add(buy, 0, 3);
            layout.Controls.Add(activate, 1, 3);
            layout.Controls.Add(cancel, 2, 3);
            Controls.Add(layout);

            // closing during the request would lose an activation already counted by the server
            FormClosing += (_, e) => e.Cancel = busy;
        }

        async Task ActivateKey()
        {
            string key = textbox.Text.Trim();
            if (key.Length == 0)
            {
                status.Text = "Please enter a license key.";
                return;
            }

            busy = true;
            activate.Enabled = false;
            textbox.Enabled = false;
            status.ForeColor = SystemColors.ControlText;
            status.Text = "Activating...";
            try
            {
                var r = await LemonSqueezy.ActivateAsync(key, Environment.MachineName);
                if (!r.Activated)
                {
                    Fail(r.Error ?? "This license key could not be activated.");
                    return;
                }
                if (!LemonSqueezy.IsOurProduct(r))
                {
                    // the key is now activated on Lemon Squeezy: release it
                    if (r.Instance != null)
                        try { await LemonSqueezy.DeactivateAsync(key, r.Instance.Id); } catch (LicenseServerUnreachableException) { }
                    Fail("This license key is not an eScope license.");
                    return;
                }
                License = LicenseInfo.FromServer(r);
            }
            catch (LicenseServerUnreachableException ex)
            {
                Fail(ex.Message + "\nAn Internet connection is needed to activate the license.");
            }
            finally
            {
                busy = false;
            }
            if (License != null)
                DialogResult = DialogResult.OK;
        }

        void Fail(string message)
        {
            status.ForeColor = Color.Firebrick;
            status.Text = message;
            activate.Enabled = true;
            textbox.Enabled = true;
        }

        public static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

}
