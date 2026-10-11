using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class TableParser
{
    public static Dictionary<string, List<double>> ParseToDictionary(string text)
    {
        // 1️⃣ Split lines (cross-platform)
        var lines = text.Split(
            new[] { "\r\n", "\n", "\r" },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (lines.Length < 2)
            throw new InvalidOperationException("Not enough lines.");

        // 2️⃣ Detect separator from header
        char separator = DetectSeparator(lines[0]);

        // 3️⃣ Extract column names
        var headers = lines[0]
            .Split(separator)
            .Select(h => h.Trim())
            .ToArray();

        // 4️⃣ Initialize dictionary
        var result = headers.ToDictionary(
            h => h,
            _ => new List<double>()
        );

        // 5️⃣ Detect decimal culture from first data row
        CultureInfo culture = DetectCulture(lines[1], separator);

        // 6️⃣ Parse data rows
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(separator);

            if (parts.Length != headers.Length)
                continue; // skip malformed row

            for (int c = 0; c < headers.Length; c++)
            {
                if (double.TryParse(parts[c].Trim(), NumberStyles.Any, culture, out double value))
                    result[headers[c]].Add(value);
                else
                    result[headers[c]].Add(double.NaN); // preserve row alignment
            }
        }

        return result;
    }

    // Separator detection
    private static char DetectSeparator(string line)
    {
        char[] candidates = { '\t', ';', ',' };

        return candidates
            .Select(s => new { Sep = s, Count = line.Count(c => c == s) })
            .OrderByDescending(x => x.Count)
            .First().Sep;
    }

    // Decimal separator detection
    private static CultureInfo DetectCulture(string line, char separator)
    {
        var parts = line.Split(separator);

        foreach (var p in parts)
        {
            if (p.Contains(',') && !p.Contains('.'))
                return CultureInfo.GetCultureInfo("fr-FR");

            if (p.Contains('.') && !p.Contains(','))
                return CultureInfo.InvariantCulture;
        }

        return CultureInfo.CurrentCulture;
    }
}
