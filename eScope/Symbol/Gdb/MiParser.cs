using System.Globalization;
using System.Text;

namespace eScope.Symbol.Gdb
{
    /// <summary>
    /// Parser for GDB/MI output: c-strings, and result lists made of
    /// const (string), tuple (Dictionary) and list (List) values.
    /// </summary>
    public static class MiParser
    {
        // c-string starting at s[i] == '"', i is moved after the closing quote
        public static string ParseCString(string s, ref int i)
        {
            var bytes = new List<byte>();
            i++; // opening quote
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\' || i >= s.Length)
                {
                    bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
                    continue;
                }
                c = s[i++];
                switch (c)
                {
                    case 'n': bytes.Add((byte)'\n'); break;
                    case 't': bytes.Add((byte)'\t'); break;
                    case 'r': bytes.Add((byte)'\r'); break;
                    case 'a': bytes.Add(7); break;
                    case 'b': bytes.Add(8); break;
                    case 'f': bytes.Add(12); break;
                    case 'v': bytes.Add(11); break;
                    case 'e': bytes.Add(27); break;
                    default:
                        if (c >= '0' && c <= '7') // octal escape: one raw byte
                        {
                            int v = c - '0';
                            for (int n = 0; n < 2 && i < s.Length && s[i] >= '0' && s[i] <= '7'; n++)
                                v = v * 8 + (s[i++] - '0');
                            bytes.Add((byte)v);
                        }
                        else
                        {
                            bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString())); // \" \\ ...
                        }
                        break;
                }
            }
            i++; // closing quote
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static string Unescape(string cstring)
        {
            int i = 0;
            return cstring.StartsWith('"') ? ParseCString(cstring, ref i) : cstring;
        }

        // quote a string as a MI c-string argument
        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\');
                if (c == '\n') { sb.Append("\\n"); continue; }
                sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        // "name=value,name=value..." starting at s[i]
        public static Dictionary<string, object> ParseResults(string s, ref int i, char end = '\0')
        {
            var results = new Dictionary<string, object>();
            while (i < s.Length && s[i] != end)
            {
                if (s[i] == ',') { i++; continue; }
                int eq = s.IndexOf('=', i);
                if (eq < 0) break;
                string name = s.Substring(i, eq - i);
                i = eq + 1;
                results[name] = ParseValue(s, ref i);
            }
            return results;
        }

        static object ParseValue(string s, ref int i)
        {
            if (i >= s.Length) return string.Empty;
            switch (s[i])
            {
                case '"':
                    return ParseCString(s, ref i);
                case '{':
                    {
                        i++;
                        var tuple = ParseResults(s, ref i, '}');
                        i++;
                        return tuple;
                    }
                case '[':
                    {
                        i++;
                        var list = new List<object>();
                        while (i < s.Length && s[i] != ']')
                        {
                            if (s[i] == ',') { i++; continue; }
                            if (s[i] == '"' || s[i] == '{' || s[i] == '[')
                            {
                                list.Add(ParseValue(s, ref i));
                            }
                            else // list of results: name=value
                            {
                                int eq = s.IndexOf('=', i);
                                if (eq < 0) { i = s.Length; break; }
                                i = eq + 1;
                                list.Add(ParseValue(s, ref i));
                            }
                        }
                        i++;
                        return list;
                    }
                default:
                    return string.Empty;
            }
        }

        public static string? GetString(this Dictionary<string, object> d, string name)
            => d.TryGetValue(name, out var v) ? v as string : null;

        public static Dictionary<string, object>? GetTuple(this Dictionary<string, object> d, string name)
            => d.TryGetValue(name, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> GetList(this Dictionary<string, object> d, string name)
            => d.TryGetValue(name, out var v) && v is List<object> l ? l : new List<object>();

        public static bool TryParseInteger(string? s, out UInt64 value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt64.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            if (UInt64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;
            if (Int64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int64 signed))
            {
                value = unchecked((UInt64)signed);
                return true;
            }
            return false;
        }
    }
}
