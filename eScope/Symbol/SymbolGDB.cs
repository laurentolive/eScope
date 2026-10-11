using eScope.Symbol.Gdb;
using System.Globalization;
using System.Text.RegularExpressions;

namespace eScope.Symbol
{
    // value of a C expression for the watch window
    public class ExprValue
    {
        public UInt32? Address;   // lvalue: value read in target memory by the device protocol
        public int Size;          // bits
        public string? Text;      // value computed by GDB (not an lvalue, or larger than 64 bits)
        public string? Error;
    }

    /// <summary>
    /// Symbols resolved by GDB from the ELF file. Any C expression is accepted (struct fields,
    /// arrays, pointers, casts...). GDB is always connected to a GdbRemoteBridge: memory reads
    /// needed by an expression (pointer dereference) are done on the target through the device
    /// protocol when it is connected, and fail otherwise (the expression is then not resolved,
    /// instead of being computed from the ELF initial values).
    /// </summary>
    public class SymbolGDB : ISymbol, IDisposable
    {
        const int MAX_SIZE_BYTES = 8;  // device reads return an UInt64
        readonly string DeviceName;
        readonly GdbMi? gdb;
        readonly Task ready;            // GDB started and connected to the bridge
        volatile Dictionary<string, SymbolData> symbols = new();

        public GdbRemoteBridge? Bridge { get; }
        public Dictionary<string, SymbolData> Symbols => symbols;

        static readonly string[] initCommands =
        {
            "-gdb-set confirm off",
            "-gdb-set pagination off",
            "-gdb-set width 0",
            "-gdb-set height 0",
            "-gdb-set trust-readonly-sections on", // .text / .rodata read from the ELF file
            "-gdb-set stack-cache off",            // no cache: target memory read on each evaluation
            "-gdb-set code-cache off",
            "-gdb-set remotetimeout 5",
            "-gdb-set print elements 16",
        };

        public SymbolGDB(string deviceName, string symbolPath, string gdbPath)
        {
            DeviceName = deviceName;
            string? path = GdbLocator.Resolve(gdbPath);
            if (path == null || !File.Exists(symbolPath))
            {
                LogMsg.Info($"{deviceName}: GDB or symbol file not found (gdb: '{gdbPath}', symbol: '{symbolPath}')");
                ready = Task.CompletedTask;
                return;
            }
            try
            {
                Bridge = new GdbRemoteBridge();
                gdb = new GdbMi(path, symbolPath);
                ready = Task.Run(Initialize);
            }
            catch (Exception ex)
            {
                LogMsg.Info($"{deviceName}: GDB start failed: {ex.Message}");
                gdb?.Dispose();
                gdb = null;
                ready = Task.CompletedTask;
            }
        }

        async Task Initialize()
        {
            foreach (string cmd in initCommands)
            {
                await gdb!.RunAsync(cmd, 30000).ConfigureAwait(false); // first one waits for the ELF loading
            }
            MiResult r = await gdb!.RunAsync($"-target-select remote 127.0.0.1:{Bridge!.Port}", 10000).ConfigureAwait(false);
            if (!r.Ok)
                LogMsg.Info($"{DeviceName}: GDB target connection failed: {r.Error}");
            Bridge.Ready = true;
            _ = Task.Run(LoadVariables);
        }

        // variable list for completion, in background
        async Task LoadVariables()
        {
            var dict = new Dictionary<string, SymbolData>();
            MiResult r = await gdb!.RunAsync("-symbol-info-variables", 30000).ConfigureAwait(false);
            if (r.Ok)
            {
                var root = r.Results.GetTuple("symbols");
                foreach (var file in root?.GetList("debug") ?? new List<object>())
                {
                    if (file is not Dictionary<string, object> f) continue;
                    foreach (var s in f.GetList("symbols"))
                    {
                        if (s is Dictionary<string, object> sym && sym.GetString("name") is string name)
                            dict.TryAdd(name, new SymbolData { Device = DeviceName, Name = name, Type = sym.GetString("type") ?? string.Empty });
                    }
                }
            }
            else // GDB < 10
            {
                r = await gdb.RunAsync($"-interpreter-exec console {MiParser.Quote("info variables")}", 30000).ConfigureAwait(false);
                var variableRegex = new Regex(@"(\w+)\s+\*?(\w+)\[?\d*\]?;");
                foreach (string line in r.Output)
                {
                    var m = variableRegex.Match(line);
                    if (m.Success)
                        dict.TryAdd(m.Groups[2].Value, new SymbolData { Device = DeviceName, Name = m.Groups[2].Value, Type = m.Groups[1].Value });
                }
            }
            symbols = dict;
        }

        /// <summary>
        /// keep the device protocol alive while a synchronous request waits on the thread
        /// processing the device (GDB may wait for target memory)
        /// </summary>
        public void SetWaitPump(Action pump, int threadId)
        {
            if (gdb == null) return;
            gdb.WaitPump = pump;
            gdb.PumpThreadId = threadId;
        }

        bool WaitReady(int timeoutMs = 15000)
            => (gdb != null) && ready.Wait(timeoutMs) && !gdb.HasExited;

        static bool TryValue(MiResult r, out UInt64 value)
        {
            value = 0;
            return r.Ok && MiParser.TryParseInteger(r.Results.GetString("value"), out value);
        }

        static string AddressOf(string expr) => $"(unsigned long long)&({expr})";

        public bool isValid(string name)
        {
            string expr = name.Trim();
            if (expr.Length == 0 || !WaitReady())
                return false;
            // sizeof: checks the expression without reading memory
            return gdb!.Evaluate($"sizeof({expr})", 2000).Ok;
        }

        public SymbolData? Get(string name)
        {
            string expr = name.Trim();
            if (expr.Length == 0 || !WaitReady())
                return null;

            if (!TryValue(gdb!.Evaluate($"sizeof({expr})"), out UInt64 bytes))
                return null;

            var sd = new SymbolData
            {
                Device = DeviceName,
                Name = expr,
                Size = (int)Math.Clamp(bytes, 1UL, MAX_SIZE_BYTES) * 8,
                Shift = 0,
                Type = TypeOf(expr),
            };
            MiResult addr = gdb.Evaluate(AddressOf(expr));
            if (TryValue(addr, out UInt64 address))
            {
                sd.Address = address.ToString("x", CultureInfo.InvariantCulture);
            }
            else
            {
                Console.WriteLine($"{DeviceName}: address of '{expr}' not resolved: {addr.Error}");
                sd.Address = string.Empty;
                sd.Resolved = false;
            }
            return sd;
        }

        /// <summary>type of the expression with the typedefs resolved (ptype output without "type = "), empty if unknown</summary>
        public string ResolvedType(string name)
        {
            string expr = name.Trim();
            if (expr.Length == 0 || !WaitReady())
                return string.Empty;
            MiResult r = gdb!.Cli($"ptype {expr}", 2000);
            string text = r.Ok ? r.OutputText.Trim() : string.Empty;
            return text.StartsWith("type = ") ? text.Substring(7).Trim() : string.Empty;
        }

        string TypeOf(string expr)
        {
            MiResult r = gdb!.Cli($"whatis {expr}", 2000);
            string text = r.OutputText.Trim();
            return text.StartsWith("type = ") ? text.Substring(7) : string.Empty;
        }

        /// <summary>completion: variables, or fields of the expression before the last '.' / '->'</summary>
        public List<string> List(string name)
        {
            var list = new List<string>();
            if (!WaitReady(2000))
                return list;

            int sep = LastMemberSeparator(name, out int sepLen);
            if (sep < 0)
            {
                return symbols.Keys
                    .Where(k => k.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            string baseExpr = name.Substring(0, sep).Trim();
            string partial = name.Substring(sep + sepLen).Trim();
            MiResult ptype = gdb!.Cli($"ptype {baseExpr}", 2000);
            if (!ptype.Ok)
                return list;

            string op = TypeOf(baseExpr).TrimEnd().EndsWith("*") ? "->" : ".";
            list.Add(baseExpr);
            list.AddRange(ParseFields(ptype.OutputText)
                .Where(f => f.Contains(partial, StringComparison.OrdinalIgnoreCase))
                .Select(f => baseExpr + op + f));
            return list;
        }

        // position of the last '.' or "->" outside brackets / parentheses
        static int LastMemberSeparator(string s, out int length)
        {
            int depth = 0;
            int found = -1;
            length = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (depth == 0 && c == '.' && !(i > 0 && char.IsDigit(s[i - 1]) && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                { found = i; length = 1; }
                else if (depth == 0 && c == '-' && i + 1 < s.Length && s[i + 1] == '>')
                { found = i; length = 2; i++; }
            }
            return found;
        }

        static readonly Regex funcPointer = new(@"\(\s*\*+\s*(\w+)\s*\)");
        static readonly Regex lastIdentifier = new(@"(\w+)\s*$");

        // top level field names of a ptype output
        static List<string> ParseFields(string ptype)
        {
            var fields = new List<string>();
            int depth = 0;
            foreach (string raw in ptype.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int before = depth;
                depth += line.Count(c => c == '{') - line.Count(c => c == '}');
                if (depth != 1 || !line.EndsWith(";") || (before != 1 && before != 2))
                    continue;

                string decl = line.TrimEnd(';').Trim();
                decl = Regex.Replace(decl, @"\s*:\s*\d+$", string.Empty);      // bitfield width
                Match m = funcPointer.Match(decl);
                if (!m.Success)
                {
                    decl = Regex.Replace(decl, @"(\s*\[[^\]]*\])+$", string.Empty); // array dimensions
                    m = lastIdentifier.Match(decl);
                }
                if (m.Success && !fields.Contains(m.Groups[1].Value))
                    fields.Add(m.Groups[1].Value);
            }
            return fields;
        }

        /// <summary>
        /// watch: address of an lvalue (value then read by the device protocol), or value
        /// computed by GDB. Can be awaited from the UI thread, does not block it.
        /// </summary>
        public async Task<ExprValue> EvaluateAsync(string name)
        {
            string expr = name.Trim();
            if (gdb == null || expr.Length == 0)
                return new ExprValue { Error = "no symbol" };
            if (await Task.WhenAny(ready, Task.Delay(15000)).ConfigureAwait(false) != ready || gdb.HasExited)
                return new ExprValue { Error = "gdb not ready" };

            MiResult size = await gdb.EvaluateAsync($"sizeof({expr})").ConfigureAwait(false);
            if (!size.Ok)
                return new ExprValue { Error = size.Error };
            MiParser.TryParseInteger(size.Results.GetString("value"), out UInt64 bytes);

            if (bytes >= 1 && bytes <= MAX_SIZE_BYTES)
            {
                MiResult addr = await gdb.EvaluateAsync(AddressOf(expr)).ConfigureAwait(false);
                if (TryValue(addr, out UInt64 address))
                    return new ExprValue { Address = (UInt32)address, Size = (int)bytes * 8 };
                if (!addr.Error.StartsWith("Attempt to take address"))
                    return new ExprValue { Error = addr.Error };
            }
            // not an lvalue (bitfield, arithmetic...) or too large: value formatted by GDB
            MiResult value = await gdb.EvaluateAsync(expr).ConfigureAwait(false);
            return value.Ok ? new ExprValue { Text = value.Results.GetString("value") ?? string.Empty }
                            : new ExprValue { Error = value.Error };
        }

        public void Dispose()
        {
            gdb?.Dispose();
            Bridge?.Dispose();
        }
    }
}
