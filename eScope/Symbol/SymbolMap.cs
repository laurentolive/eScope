using System.Globalization;
using System.Text.RegularExpressions;

namespace eScope.Symbol
{
    /// <summary>
    /// Symbols read from a map file. Format detected from the content, unless a custom regex is given:
    ///   - GNU ld map ("Linker script and memory map"): "0xADDR  name" symbol lines of the memory map, size taken from
    ///     the input section of the variable (.bss.name / .data.name... with -fdata-sections)
    ///   - "FormatGNU" (first line): "0xADDR  _name" lines, name without the leading '_' added by the generator
    ///   - otherwise: "0xADDR name" or "name 0xADDR" lines
    /// Custom regex: groups "var" and "address" (hex, 0x optional), optional "size" in bytes (decimal or 0x hex).
    /// Size unknown (no size in the map, or not 1/2/4/8 bytes): SymbolData.Size = 0, the user keeps the size.
    /// </summary>
    public class SymbolMap : ISymbol
    {
        // default of the older versions: invalid (unbalanced parenthesis), treated as automatic detection
        public const string LegacyDefaultRegex = @"((?<var>\b\w+\b)\s+\b0x(?<address>[0-9A-Fa-f]+)\b";

        static readonly Regex GenericLine = new(
            @"^\s*(?:0x(?<address>[0-9A-Fa-f]+)\s+(?<var>[A-Za-z_]\w*)|(?<var>[A-Za-z_]\w*)\s+0x(?<address>[0-9A-Fa-f]+))(?:\s|$)");
        // GNU ld memory map
        static readonly Regex GnuSymbol = new(@"^\s+0x(?<address>[0-9A-Fa-f]+)\s+(?<var>[A-Za-z_]\w*)\s*$");
        static readonly Regex GnuSection = new(@"^ (?<sec>\.\S+)(?:\s+0x(?<address>[0-9A-Fa-f]+)\s+0x(?<size>[0-9A-Fa-f]+)\s+\S.*)?$");
        static readonly Regex GnuSectionPlace = new(@"^\s+0x(?<address>[0-9A-Fa-f]+)\s+0x(?<size>[0-9A-Fa-f]+)\s+\S");
        const string GnuMemoryMap = "Linker script and memory map";

        public Dictionary<string, SymbolData> Symbols { get; } = new();
        private readonly string DeviceName;

        public SymbolMap(string path, string deviceName, string mapRegEx)
        {
            DeviceName = deviceName;
            if (!File.Exists(path))
            {
                LogMsg.Info($"{deviceName}: map file not found: {path}");
                return;
            }
            try
            {
                string[] lines = File.ReadAllLines(path);
                Regex? custom = CustomRegex(mapRegEx);
                if (custom != null)
                    ParseCustom(lines, custom);
                else if (lines.Any(l => l.StartsWith(GnuMemoryMap)))
                    ParseGnu(lines);
                else
                    ParseGeneric(lines, stripUnderscore: lines.Length > 0 && lines[0].Trim() == "FormatGNU");
                LogMsg.Info($"{deviceName}: {Symbols.Count} symbols read from {Path.GetFileName(path)}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogMsg.Info($"{deviceName}: cannot read the map file: {ex.Message}");
            }
        }

        // null: automatic detection (empty, legacy default or invalid regex)
        Regex? CustomRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern == LegacyDefaultRegex)
                return null;
            try
            {
                var regex = new Regex(pattern);
                if (regex.GroupNumberFromName("var") >= 0 && regex.GroupNumberFromName("address") >= 0)
                    return regex;
                LogMsg.Info($"{DeviceName}: map regex without (?<var>) and (?<address>) groups, automatic detection used");
            }
            catch (ArgumentException ex)
            {
                LogMsg.Info($"{DeviceName}: invalid map regex ({ex.Message}), automatic detection used");
            }
            return null;
        }

        void ParseCustom(string[] lines, Regex regex)
        {
            foreach (string line in lines)
            {
                Match m = regex.Match(line);
                if (!m.Success) continue;
                int sizeBytes = m.Groups["size"].Success ? ParseNumber(m.Groups["size"].Value) : 0;
                Add(m.Groups["var"].Value, m.Groups["address"].Value, sizeBytes);
            }
        }

        void ParseGeneric(string[] lines, bool stripUnderscore)
        {
            foreach (string line in lines)
            {
                Match m = GenericLine.Match(line);
                if (!m.Success) continue;
                string name = m.Groups["var"].Value;
                if (stripUnderscore && name.Length > 1 && name[0] == '_')
                    name = name[1..];
                Add(name, m.Groups["address"].Value, 0);
            }
        }

        void ParseGnu(string[] lines)
        {
            int i = Array.FindIndex(lines, l => l.StartsWith(GnuMemoryMap));
            // last input section: its name, address and size give the size of the variable it holds
            string section = string.Empty;
            string sectionAddress = string.Empty;
            int sectionSize = 0;
            for (i++; i < lines.Length; i++)
            {
                string line = lines[i];
                Match m = GnuSection.Match(line);
                if (m.Success)
                {
                    section = m.Groups["sec"].Value;
                    sectionAddress = m.Groups["address"].Value;
                    sectionSize = m.Groups["size"].Success ? ParseHex(m.Groups["size"].Value) : 0;
                    // long section name: address and size on the next line
                    if (!m.Groups["address"].Success && i + 1 < lines.Length
                        && GnuSectionPlace.Match(lines[i + 1]) is { Success: true } place)
                    {
                        sectionAddress = place.Groups["address"].Value;
                        sectionSize = ParseHex(place.Groups["size"].Value);
                        i++;
                    }
                    continue;
                }
                m = GnuSymbol.Match(line);
                if (!m.Success) continue;
                string name = m.Groups["var"].Value;
                string address = m.Groups["address"].Value;
                bool ownSection = section.EndsWith("." + name) && ParseHex(address) == ParseHex(sectionAddress);
                Add(name, address, ownSection ? sectionSize : 0);
            }
        }

        // the first definition wins (same static name in several files)
        void Add(string name, string address, int sizeBytes)
        {
            if (name.Length == 0 || Symbols.ContainsKey(name)) return;
            if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) address = address[2..];
            Symbols[name] = new SymbolData
            {
                Device = DeviceName,
                Name = name,
                Address = address,
                Size = sizeBytes is 1 or 2 or 4 or 8 ? sizeBytes * 8 : 0,
                Shift = 0,
                Type = string.Empty,
            };
        }

        static int ParseHex(string s)
            => int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v) ? v : 0;

        static int ParseNumber(string s)
            => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ParseHex(s[2..])
                : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        public List<string> List(string name)
        {
            return Symbols.Keys.Where(key => key.Contains(name)).ToList();
        }
        public bool isValid(string name)
        {
            return Symbols.ContainsKey(name);
        }
        public SymbolData? Get(string name)
        {
            return Symbols.TryGetValue(name, out var sd) ? sd : null;
        }
    }
}
