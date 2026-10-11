using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using eScope.License;

namespace eScope.About
{
    // About > Support: a support request posted as a GitHub issue labelled "support".
    // With ESCOPE_GITHUB_TOKEN set the issue is created through the API, otherwise the browser opens
    // the issue form (.github/ISSUE_TEMPLATE/support.yml, which sets the label) prefilled with the fields.
    public class SupportForm : Form
    {
        const string RepoOwner = "laurentolive";
        const string Repo = "eScope";
        const string Label = "support";
        const string Template = "support.yml";
        const string TokenEnv = "ESCOPE_GITHUB_TOKEN";
        // GitHub rejects longer new-issue URLs
        const int MaxUrlLength = 8000;

        readonly TextBox title;
        readonly TextBox description;
        readonly TextBox contact;
        readonly TextBox environment;
        readonly Button send;

        static string? Token => Environment.GetEnvironmentVariable(TokenEnv) is { } t && !string.IsNullOrWhiteSpace(t) ? t.Trim() : null;

        public SupportForm()
        {
            Text = "Support";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            int width = 40 * Font.Height; // text column, scales with the font
            Label Text_(string text, Font? font = null, int bottom = 4) => new()
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                Font = font ?? Font,
                Margin = new Padding(3, 0, 3, bottom)
            };
            TextBox Box(int lines, bool readOnly = false) => new()
            {
                Multiline = lines > 1,
                ScrollBars = lines > 1 ? ScrollBars.Vertical : ScrollBars.None,
                ReadOnly = readOnly,
                Size = new Size(width, lines > 1 ? lines * Font.Height + 8 : 0),
                Width = width,
                Margin = new Padding(3, 0, 3, 10)
            };

            var header = Text_(
                "Describe your problem or question: it is sent as an issue on the eScope GitHub repository " +
                (Token != null
                    ? "(created directly with the token of " + TokenEnv + ")."
                    : "(your browser opens the prefilled issue: sign in to GitHub and click Create).") +
                " Issues are public: do not include confidential information.", bottom: 12);

            title = Box(1);
            description = Box(10);
            contact = Box(1);
            environment = Box(4, readOnly: true);
            environment.Text = EnvironmentInfo();

            send = new Button
            {
                Text = Token != null ? "Send" : "Open in GitHub",
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2),
                Enabled = false
            };
            send.Click += async (_, _) => await Send();
            title.TextChanged += (_, _) => UpdateSend();
            description.TextChanged += (_, _) => UpdateSend();

            var cancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                Padding = new Padding(8, 2, 8, 2),
                DialogResult = DialogResult.Cancel
            };
            CancelButton = cancel;

            var issues = new LinkLabel { Text = "Existing issues", AutoSize = true, Anchor = AnchorStyles.Left };
            issues.LinkClicked += (_, _) => OpenUrl($"https://github.com/{RepoOwner}/{Repo}/issues?q=label%3A{Label}");

            var buttons = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.Controls.Add(issues, 0, 0);
            buttons.Controls.Add(send, 1, 0);
            buttons.Controls.Add(cancel, 2, 0);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12)
            };
            layout.Controls.AddRange(new Control[]
            {
                header,
                Text_("Title"), title,
                Text_("Description (what happens, what you expected, steps to reproduce)"), description,
                Text_("Contact (optional, email — visible in the public issue)"), contact,
                Text_("Environment"), environment,
                buttons
            });
            Controls.Add(layout);
        }

        void UpdateSend() => send.Enabled = title.Text.Trim().Length > 0 && description.Text.Trim().Length > 0;

        static string EnvironmentInfo() =>
            "eScope " + global::eScope.eScope.AppVersion + "\r\n" +
            $"License: {LicenseManager.GetLicenseStatusText()}\r\n" +
            $"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}\r\n" +
            $"Runtime: {RuntimeInformation.FrameworkDescription}";

        async Task Send()
        {
            string issueTitle = title.Text.Trim();
            if (Token is { } token)
            {
                send.Enabled = false;
                UseWaitCursor = true;
                try
                {
                    string url = await CreateIssue(token, issueTitle, IssueBody());
                    OpenUrl(url);
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "The issue could not be created:\n" + ex.Message, "Support",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateSend();
                }
                finally
                {
                    UseWaitCursor = false;
                }
            }
            else
            {
                OpenUrl(NewIssueUrl(issueTitle));
                DialogResult = DialogResult.OK;
            }
        }

        // same layout as the issue form, for the issues created through the API
        string IssueBody()
        {
            string body = $"### Description\n\n{description.Text.Trim()}\n\n";
            if (contact.Text.Trim().Length > 0)
                body += $"### Contact (optional)\n\n{contact.Text.Trim()}\n\n";
            return body + $"### Environment\n\n```text\n{environment.Text.Trim()}\n```\n";
        }

        static async Task<string> CreateIssue(string token, string issueTitle, string body)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("eScope", global::eScope.eScope.AppVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await http.PostAsJsonAsync($"https://api.github.com/repos/{RepoOwner}/{Repo}/issues",
                new { title = issueTitle, body, labels = new[] { Label } });
            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}\n{json}");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("html_url").GetString()!;
        }

        // issue form prefilled by field id; the description is cut if the URL gets too long
        string NewIssueUrl(string issueTitle)
        {
            string Url(string text) =>
                $"https://github.com/{RepoOwner}/{Repo}/issues/new?template={Template}&labels={Label}" +
                $"&title={Uri.EscapeDataString("[Support] " + issueTitle)}" +
                $"&description={Uri.EscapeDataString(text)}" +
                $"&contact={Uri.EscapeDataString(contact.Text.Trim())}" +
                $"&environment={Uri.EscapeDataString(environment.Text.Trim())}";

            string text = description.Text.Trim();
            string url = Url(text);
            if (url.Length <= MaxUrlLength) return url;

            const string cut = "\n\n[...] (truncated, paste the rest here)";
            int keep = text.Length;
            while (keep > 0 && (url = Url(text[..keep] + cut)).Length > MaxUrlLength)
                keep -= Math.Max(1, (url.Length - MaxUrlLength) / 3); // an escaped char takes up to 9 chars
            Clipboard.SetText(text);
            MessageBox.Show(this, "The description is too long for the link: it is truncated in the issue, " +
                "the full text is in the clipboard.", "Support", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return url;
        }

        static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
