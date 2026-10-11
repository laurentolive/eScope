namespace eScope.License
{
    // About > License: current license, buy / activate / renew / move to another computer
    public class LicenseForm : Form
    {
        readonly Label info;
        readonly Button activate, check, deactivate, manage;

        public LicenseForm()
        {
            Text = "License";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            // sized by its content: no clipped text whatever the font / DPI scaling
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            info = new Label { AutoSize = true, Margin = new Padding(3, 3, 3, 12) };

            activate = NewButton("Enter license key...");
            activate.Click += async (_, _) => { await LicenseManager.ActivateAsync(this); RefreshInfo(); };

            var buy = NewButton("Buy...");
            buy.Click += (_, _) => ProfessionalActivationForm.OpenUrl(LemonSqueezy.BuyUrl);

            manage = NewButton("Manage subscription...");
            manage.Click += (_, _) => ProfessionalActivationForm.OpenUrl(LemonSqueezy.PortalUrl);

            check = NewButton("Check now");
            check.Click += async (_, _) =>
            {
                check.Enabled = false;
                if (await LicenseManager.RevalidateAsync(silent: false) == true)
                    MessageBox.Show(this, "The license is valid.", "License", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshInfo();
            };

            deactivate = NewButton("Deactivate...");
            deactivate.Click += async (_, _) =>
            {
                if (MessageBox.Show(this,
                        "Remove the Professional license from this computer?\n" +
                        "The key can then be activated on another computer.",
                        "License", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                deactivate.Enabled = false;
                await LicenseManager.DeactivateAsync();
                RefreshInfo();
            };

            var close = NewButton("Close");
            close.DialogResult = DialogResult.Cancel;
            CancelButton = close;

            // 3 columns of buttons under the status text, each column as wide as its widest button
            var layout = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12)
            };
            layout.Controls.Add(info, 0, 0);
            layout.SetColumnSpan(info, 3);
            layout.Controls.Add(activate, 0, 1);
            layout.Controls.Add(buy, 1, 1);
            layout.Controls.Add(manage, 2, 1);
            layout.Controls.Add(check, 0, 2);
            layout.Controls.Add(deactivate, 1, 2);
            layout.Controls.Add(close, 2, 2);
            Controls.Add(layout);
            RefreshInfo();
        }

        static Button NewButton(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 2, 8, 2)
        };

        void RefreshInfo()
        {
            if (IsDisposed)
                return;

            var l = LicenseManager.Current;
            bool pro = l is { Type: LicenseType.Professional, LicenseKey: not null };
            // only out of grace offline: "Check now" is the way out, not a new activation
            bool validPro = pro && (!l!.IsExpired || l.ServerStatus == "active");

            var text = LicenseManager.GetLicenseStatusText();
            if (pro)
            {
                text += $"\n\nKey: {Mask(l!.LicenseKey!)}";
                if (!string.IsNullOrEmpty(l.CustomerEmail))
                    text += $"\nRegistered to: {l.CustomerEmail}";
                text += l.LastValidatedUtc.HasValue
                    ? $"\nLast validated: {l.LastValidatedUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                    : "\nNever validated";
            }
            info.Text = text;

            // a valid key must be deactivated before entering another one (it would use a second activation)
            activate.Enabled = !validPro;
            manage.Enabled = pro;
            check.Enabled = pro;
            deactivate.Enabled = pro;
        }

        static string Mask(string key) =>
            key.Length <= 8 ? key : new string('•', key.Length - 8) + key[^8..];
    }
}
