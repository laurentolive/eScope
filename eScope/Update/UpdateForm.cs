namespace eScope.Update
{
    // lists the GitHub releases, installs the selected one (latest stable selected by default)
    public class UpdateForm : Form
    {
        // folder of the downloaded release, set when the form closes with DialogResult.OK
        public string? DownloadedDir { get; private set; }

        readonly ListView list = new()
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        readonly TextBox notes = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        readonly Label status = new() { AutoSize = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        readonly ProgressBar progress = new() { Visible = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        readonly Button install = new() { Text = "Install", Enabled = false, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        readonly Button page = new() { Text = "Release page", Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        readonly Button close = new() { Text = "Close", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        readonly CancellationTokenSource cts = new();
        List<Release>? releases;

        public UpdateForm(List<Release>? releases = null)
        {
            this.releases = releases;
            Text = "eScope update";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(600, 440);
            MinimumSize = new Size(420, 340);
            CancelButton = close;

            var current = new Label { Text = $"Installed version: {GitHubReleases.Current}", AutoSize = true, Location = new Point(12, 12) };
            list.Bounds = new Rectangle(12, 38, 576, 170);
            list.Columns.Add("Version", 140);
            list.Columns.Add("Date", 110);
            list.Columns.Add("", 300);
            notes.Bounds = new Rectangle(12, 216, 576, 136);
            status.Bounds = new Rectangle(12, 362, 576, 22);
            progress.Bounds = new Rectangle(12, 362, 576, 22);
            install.Bounds = new Rectangle(288, 396, 96, 32);
            page.Bounds = new Rectangle(390, 396, 110, 32);
            close.Bounds = new Rectangle(506, 396, 82, 32);
            AcceptButton = install;

            Controls.AddRange(new Control[] { current, list, notes, status, progress, install, page, close });

            list.SelectedIndexChanged += (_, _) => ShowSelected();
            list.DoubleClick += (_, _) => install.PerformClick();
            install.Click += Install_Click;
            page.Click += (_, _) => GitHubReleases.OpenInBrowser(Selected?.HtmlUrl ?? GitHubReleases.RepoUrl);
            Load += async (_, _) => await LoadReleases();
            FormClosing += (_, _) => cts.Cancel();
        }

        Release? Selected => list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as Release : null;

        async Task LoadReleases()
        {
            if (releases == null)
            {
                status.Text = "Looking for versions on GitHub...";
                try
                {
                    releases = await GitHubReleases.ListAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    if (IsDisposed) return;
                    status.Text = $"Cannot list the versions: {ex.Message}";
                    return;
                }
                if (IsDisposed) return;
            }

            Release? latest = GitHubReleases.Latest(releases);
            list.BeginUpdate();
            foreach (Release r in releases)
            {
                var tags = new List<string>();
                if (r == latest) tags.Add("latest");
                if (r.Prerelease) tags.Add("pre-release");
                if (GitHubReleases.Compare(r.Version, GitHubReleases.Current) == 0) tags.Add("installed");
                if (r.Package == null) tags.Add("no package");
                var item = new ListViewItem(new[] { r.Tag, r.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? "", string.Join(", ", tags) })
                {
                    Tag = r,
                };
                if (r == latest) item.Font = new Font(list.Font, FontStyle.Bold);
                list.Items.Add(item);
                if (r == latest) item.Selected = true;
            }
            list.EndUpdate();

            if (releases.Count == 0)
                status.Text = "No version published on GitHub.";
            else
                status.Text = latest != null && GitHubReleases.IsNewer(latest)
                    ? $"Version {latest.Version} is available."
                    : "eScope is up to date.";
            if (list.SelectedItems.Count == 0 && list.Items.Count > 0)
                list.Items[0].Selected = true;
            list.SelectedItems.Cast<ListViewItem>().FirstOrDefault()?.EnsureVisible();
            ShowSelected();
        }

        void ShowSelected()
        {
            Release? r = Selected;
            notes.Text = r == null ? "" : $"{r.Name ?? r.Tag}\r\n\r\n{(r.Body ?? "").ReplaceLineEndings("\r\n")}";
            install.Enabled = r?.Package != null && !progress.Visible;
            install.Text = r != null && GitHubReleases.Compare(r.Version, GitHubReleases.Current) < 0 ? "Downgrade" : "Install";
        }

        async void Install_Click(object? sender, EventArgs e)
        {
            Release? r = Selected;
            if (r?.Package == null) return;

            install.Enabled = false;
            list.Enabled = false;
            status.Visible = false;
            progress.Value = 0;
            progress.Visible = true;
            try
            {
                DownloadedDir = await GitHubReleases.DownloadAsync(r, new Progress<int>(p => progress.Value = Math.Clamp(p, 0, 100)), cts.Token);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                if (IsDisposed || cts.IsCancellationRequested) return;
                progress.Visible = false;
                status.Visible = true;
                status.Text = $"Download failed: {ex.Message}";
                list.Enabled = true;
                ShowSelected();
            }
        }
    }
}
