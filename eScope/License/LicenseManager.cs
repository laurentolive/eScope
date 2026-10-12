namespace eScope.License
{
    // The license is not meant to resist tampering (whoever wants to cheat takes the free Personal license),
    // but the Professional one must be paid: its key is checked against the eScope product and validated
    // online again, an expired / unvalidated license shows the reminder at each startup.
    // Demonstration / Personal licenses are local; a Professional license is a Lemon Squeezy key activated on
    // this machine, validated at each startup when online and trusted offline for LicenseInfo.OfflineGraceDays.
    public static class LicenseManager
    {
        static System.Windows.Forms.Timer Timer = new() { Interval = 1000, Enabled = true };
        static LicenseInfo? License;
        public static LicenseInfo? Current => License;
        public static event EventHandler? StateChanged;

        static LicenseManager()
        {
            // first check once the main window is up
            Timer.Tick += async (s, e) =>
            {
                Timer.Stop();
                // validate first: a renewed subscription must not show the expired reminder
                await RevalidateAsync(silent: true);
                EnsureLicense();
                RemindManagedPersonal();
            };
            License = LicenseStore.Load();
        }
        private static void OnStateChanged() => StateChanged?.Invoke(null, EventArgs.Empty);

        public static void EnsureLicense()
        {
            // still active on the server, just not validated for too long: a new activation would not help
            if (License is { IsOfflineTooLong: true, ServerStatus: "active" })
            {
                MessageBox.Show(
                    $"The Professional license has not been validated online for more than {LicenseInfo.OfflineGraceDays} days.\n" +
                    "Please connect this computer to the Internet and restart eScope (or About > License > Check now).",
                    "License", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if ((License == null) || (License.IsExpired))
            {
                if ((License != null) && (License.IsExpired))
                {
                    string msg = License.Type == LicenseType.Professional
                        ? "Your Professional license is no longer valid (" + License.ServerStatus + ").\n" +
                          "Renew your subscription or enter another license key."
                        : "License Expired. Please acquire License";
                    MessageBox.Show(msg, "License", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                using var selDlg = new LicenseCreateForm();
                selDlg.ShowDialog();

                // managed computer: OK goes on with the Personal license, Cancel goes back to the license choice
                bool managed = EnvironmentSignals.IsManagedByOrganization();
                while (managed && (selDlg.SelectedLicense == LicenseType.Personal))
                {
                    if (ShowManagedNotice(MessageBoxButtons.OKCancel) == DialogResult.OK)
                        break;
                    selDlg.ShowDialog();
                }
                if (selDlg.SelectedLicense == LicenseType.Personal)
                {
                    using var dlg = new PersonalConfirmationForm();
                    if (dlg.ShowDialog() == DialogResult.OK && (dlg.Accepted == true))
                    {
                        License = new LicenseInfo(LicenseType.Personal);
                    }
                }
                if (selDlg.SelectedLicense == LicenseType.Professional)
                {
                    var pro = ActivateProfessionalLicense();
                    if (pro != null)
                        License = pro;
                    else if (License?.Type == LicenseType.Professional)
                        License = null; // activation cancelled: fall back to the demonstration below
                }

                if ((selDlg.SelectedLicense == LicenseType.Demonstration) || (License == null))
                {
                    var licDemo = LicenseStore.Load(LicenseType.Demonstration);

                    if (licDemo != null)
                    {
                        License = licDemo; /* reload existing demo license */
                    }
                    else
                    {
                        License = new LicenseInfo(LicenseType.Demonstration);
                    }
                }
                LicenseStore.Save(License);
                OnStateChanged();
            }
        }

        static bool ManagedNoticeShown;

        private static DialogResult ShowManagedNotice(MessageBoxButtons buttons)
        {
            ManagedNoticeShown = true;
            return MessageBox.Show(
                "This computer appears to be managed by an organization.\n\n" +
                "Personal licenses are intended for non-commercial use.\n" +
                "If you are using this software for work or professional purposes,\n" +
                "a Professional license is required.",
                "Notice", buttons, MessageBoxIcon.Warning);
        }

        // a Personal license on a managed computer (obtained before it joined the domain, or copied
        // profile): reminder at each startup, without blocking; not twice when the license was just chosen
        private static void RemindManagedPersonal()
        {
            if (License is not { Type: LicenseType.Personal } || ManagedNoticeShown)
                return;
            if (EnvironmentSignals.IsManagedByOrganization())
                ShowManagedNotice(MessageBoxButtons.OK);
        }

        // validates the Professional key against Lemon Squeezy and refreshes its status / expiry
        // returns null when the server could not be reached
        public static async Task<bool?> RevalidateAsync(bool silent)
        {
            if (License is not { Type: LicenseType.Professional, LicenseKey: not null, InstanceId: not null } l)
                return false;

            try
            {
                var r = await LemonSqueezy.ValidateAsync(l.LicenseKey, l.InstanceId);
                l.Update(r);
                if (!r.Valid || !LemonSqueezy.IsOurProduct(r))
                {
                    // instance deactivated from the customer portal, key deleted, refunded...
                    if (l.ServerStatus is not ("expired" or "disabled"))
                        l.ServerStatus = "invalid";
                }
                LicenseStore.Save(l);
                OnStateChanged();
                return !l.IsExpired;
            }
            catch (LicenseServerUnreachableException ex)
            {
                if (!silent)
                    MessageBox.Show(ex.Message, "License", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }

        // frees the activation so the key can be used on another computer
        public static async Task<bool> DeactivateAsync()
        {
            if (License is not { Type: LicenseType.Professional, LicenseKey: not null, InstanceId: not null } l)
                return false;

            try
            {
                var r = await LemonSqueezy.DeactivateAsync(l.LicenseKey, l.InstanceId);
                // an instance already removed on the server is gone as well
                if (!r.Deactivated && r.LicenseKey == null)
                {
                    MessageBox.Show(r.Error ?? "The license could not be deactivated.", "License", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (LicenseServerUnreachableException ex)
            {
                MessageBox.Show(ex.Message + "\n\nAn Internet connection is needed to deactivate the license.",
                    "License", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            LicenseStore.Delete(LicenseType.Professional);
            License = LicenseStore.Load();
            OnStateChanged();
            EnsureLicense();
            return true;
        }

        // activates a new Professional key (License menu); the previous activation is released if possible
        public static async Task ActivateAsync(IWin32Window owner)
        {
            var pro = ActivateProfessionalLicense(owner);
            if (pro == null)
                return;

            if (License is { Type: LicenseType.Professional, LicenseKey: not null, InstanceId: not null } old
                && old.InstanceId != pro.InstanceId)
            {
                try { await LemonSqueezy.DeactivateAsync(old.LicenseKey, old.InstanceId); } catch (LicenseServerUnreachableException) { }
            }

            License = pro;
            LicenseStore.Save(License);
            OnStateChanged();
        }

        private static LicenseInfo? ActivateProfessionalLicense(IWin32Window? owner = null)
        {
            using var dlg = new ProfessionalActivationForm();
            if (dlg.ShowDialog(owner) != DialogResult.OK)
                return null;

            return dlg.License;
        }
        public static string GetLicenseStatusText()
        {
            if(License != null)
            {
                return License.GetLicenseStatusText();
            }
            else
            {
                return "Unlicenced";
            }
        }
    }
}
