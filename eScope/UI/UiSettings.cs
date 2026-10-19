using System.Text.Json;

namespace eScope
{
    // Per-user display preferences, kept out of the .escope workspace (it can be shared)
    public class UiSettings
    {
        public bool DarkMode { get; set; } = false;
        public int SidePanelWidth { get; set; } = 288; // logical pixels
        public List<string> CollapsedSections { get; set; } = new();
        public string? LastWorkspace { get; set; } // opened at startup when no workspace is given

        static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eScope", "ui.json");

        static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static UiSettings Current { get; } = Load();

        static UiSettings Load()
        {
            try
            {
                using var fs = File.OpenRead(FilePath);
                return JsonSerializer.Deserialize<UiSettings>(fs) ?? new();
            }
            catch (Exception)
            {
                return new(); // missing or corrupted: defaults
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UI settings not saved: {ex.Message}");
            }
        }
    }
}
