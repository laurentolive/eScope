using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eScope.Update
{
    public sealed class ReleaseAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("url")] public string ApiUrl { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
    }

    public sealed class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; set; } = "";
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("published_at")] public DateTime? PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<ReleaseAsset> Assets { get; set; } = new();

        public string Version => GitHubReleases.Normalize(Tag);

        // the Windows build: first .zip asset, one named "win*" preferred
        public ReleaseAsset? Package =>
            Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                       && a.Name.Contains("win", StringComparison.OrdinalIgnoreCase))
            ?? Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    // eScope versions published as GitHub releases (tag vX.Y.Z + a zip of the publish folder)
    public static class GitHubReleases
    {
        const string Owner = "laurentolive";
        const string Repo = "eScope";
        // optional, needed while the repository is private
        const string TokenEnv = "ESCOPE_GITHUB_TOKEN";

        public static string Current => global::eScope.eScope.AppVersion;
        public static string RepoUrl => $"https://github.com/{Owner}/{Repo}/releases";

        static readonly HttpClient http = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("eScope", Current));
            string? token = Environment.GetEnvironmentVariable(TokenEnv);
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            return client;
        }

        // published releases, newest version first
        public static async Task<List<Release>> ListAsync(CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=100");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));
            using var resp = await http.SendAsync(req, cts.Token);
            resp.EnsureSuccessStatusCode();
            await using var s = await resp.Content.ReadAsStreamAsync(cts.Token);
            var list = await JsonSerializer.DeserializeAsync<List<Release>>(s, cancellationToken: cts.Token) ?? new();
            return list.Where(r => !r.Draft)
                       .OrderByDescending(r => r.Version, Comparer<string>.Create(Compare))
                       .ToList();
        }

        // highest stable release, null if none
        public static Release? Latest(IEnumerable<Release> releases) =>
            releases.Where(r => !r.Prerelease).MaxBy(r => r.Version, Comparer<string>.Create(Compare));

        public static bool IsNewer(Release r) => Compare(r.Version, Current) > 0;

        public static string Normalize(string tag) => tag.TrimStart('v', 'V').Split('+')[0];

        // semver order on "X.Y.Z[-pre]": a pre-release sorts before its release
        public static int Compare(string? a, string? b)
        {
            var (va, pa) = Parse(a);
            var (vb, pb) = Parse(b);
            int c = va.CompareTo(vb);
            if (c != 0) return c;
            if (pa.Length == 0 || pb.Length == 0) return pb.Length.CompareTo(pa.Length);
            return string.CompareOrdinal(pa, pb);
        }

        static (Version, string) Parse(string? s)
        {
            s = Normalize(s ?? "");
            int dash = s.IndexOf('-');
            string core = dash < 0 ? s : s[..dash];
            string pre = dash < 0 ? "" : s[(dash + 1)..];
            return (Version.TryParse(core, out var v) ? v : new Version(0, 0), pre);
        }

        // download the release package and extract it, returns the folder holding eScope.exe
        public static async Task<string> DownloadAsync(Release r, IProgress<int>? progress, CancellationToken ct)
        {
            ReleaseAsset asset = r.Package ?? throw new InvalidOperationException($"Release {r.Tag} has no .zip package.");

            string root = Path.Combine(Path.GetTempPath(), "eScopeUpdate", r.Version);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            string zipPath = Path.Combine(root, asset.Name);

            // the API url serves private assets too (with a token), the browser url only public ones
            bool useApi = http.DefaultRequestHeaders.Authorization != null;
            using (var req = new HttpRequestMessage(HttpMethod.Get, useApi ? asset.ApiUrl : asset.DownloadUrl))
            {
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? asset.Size;
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(zipPath);
                var buf = new byte[1 << 16];
                long done = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    done += n;
                    if (total > 0) progress?.Report((int)(done * 100 / total));
                }
            }

            string dir = Path.Combine(root, "files");
            ZipFile.ExtractToDirectory(zipPath, dir);
            // zip made of a single top folder: use its content
            while (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir) is { Length: 1 } sub)
                dir = sub[0];
            if (!File.Exists(Path.Combine(dir, Path.GetFileName(Environment.ProcessPath ?? "eScope.exe"))))
                throw new InvalidOperationException($"{asset.Name} does not contain eScope.exe.");
            return dir;
        }

        // copy the extracted files over the application folder once this process exits, then restart
        public static void InstallOnExit(string sourceDir)
        {
            string appDir = AppContext.BaseDirectory.TrimEnd('\\');
            string exe = Environment.ProcessPath!;
            string script = Path.Combine(Path.GetTempPath(), "eScopeUpdate", "install.ps1");
            File.WriteAllText(script, """
                param([int]$ProcId, [string]$Src, [string]$Dst, [string]$Exe, [string]$WorkDir)
                Wait-Process -Id $ProcId -ErrorAction SilentlyContinue
                $err = $null
                for ($i = 0; $i -lt 10; $i++) {
                    try { Copy-Item -Path (Join-Path $Src '*') -Destination $Dst -Recurse -Force -ErrorAction Stop; $err = $null; break }
                    catch { $err = $_; Start-Sleep -Milliseconds 500 }
                }
                if ($err) {
                    Add-Type -AssemblyName System.Windows.Forms
                    [System.Windows.Forms.MessageBox]::Show("eScope update failed:`n$err", 'eScope update') | Out-Null
                }
                Start-Process -FilePath $Exe -WorkingDirectory $WorkDir
                """);

            var psi = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
                                         "-ProcId", Environment.ProcessId.ToString(), "-Src", sourceDir, "-Dst", appDir,
                                         "-Exe", exe, "-WorkDir", Environment.CurrentDirectory })
                psi.ArgumentList.Add(a);
            Process.Start(psi);
        }

        public static void OpenInBrowser(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
