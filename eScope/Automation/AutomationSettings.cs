using System.Security.Cryptography;
using System.Text.Json;

namespace eScope.Automation
{
    // Per-user settings of the automation server, kept out of the .escope workspace (it can be shared)
    public class AutomationSettings
    {
        public const int DefaultPort = 47653;

        public bool Enabled { get; set; } = false;
        public int Port { get; set; } = DefaultPort;
        public bool AllowWrite { get; set; } = false; // write_variable / write_memory on the target
        public string Token { get; set; } = string.Empty;

        static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eScope", "automation.json");

        static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static AutomationSettings Load()
        {
            try
            {
                using var fs = File.OpenRead(FilePath);
                return JsonSerializer.Deserialize<AutomationSettings>(fs) ?? new();
            }
            catch (Exception)
            {
                return new(); // missing or corrupted: defaults, server disabled
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }

        // generated on first activation, kept afterwards so the clients stay configured
        public void EnsureToken()
        {
            if (string.IsNullOrEmpty(Token)) RegenerateToken();
        }

        public void RegenerateToken()
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
