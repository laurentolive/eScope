using eScope.Symbol;
using System.Globalization;
using System.Text.Json;

namespace eScope.Automation
{
    // one-shot reads and writes of the target memory (protocol V3), the device link being opened on demand
    public partial class AutomationService
    {
        // "write target memory" setting of the automation server
        public Func<bool> AllowWrite { get; set; } = () => false;

        // a read not answered is failed by the protocol after 1 s; this one only guards against a lost callback
        const int ReadTimeoutMs = 5000;

        enum ValueKind { Signed, Unsigned, Hex, Float, Double }

        // resolved expression: an address to read / write, or a value text computed by GDB
        record Target(DeviceCtrl Dev, string Expression, uint Address, int SizeBits, ValueKind Kind, string? Type, string? Text);

        // the link is opened on demand (in background, TCP connection bounded to 2 s, retried after 1 s)
        const int LinkWaitMs = 3500;

        async Task<DeviceCtrl> ConnectedDevice(string? device)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                var (dev, connected) = await ui.InvokeAsync(() =>
                {
                    var d = ResolveDevice(device);
                    if (d.Protocol?.SupportsReadMem != true)
                        throw new AutomationException(AutomationError.NotSupported,
                            $"device '{d.Data.Name}': protocol {d.Data.Protocol} has no one-shot read / write (V3 only)");
                    bool open = d.Protocol.IsConnected;
                    if (!open)
                    {
                        if (watch.ElapsedMilliseconds > LinkWaitMs)
                            throw new AutomationException(AutomationError.NotConnected,
                                $"device '{d.Data.Name}': link not open after {LinkWaitMs} ms ({d.Data.Com}): target absent?");
                        d.Protocol.RequestLink(); // kept open while requested, closed after a while outside a recording
                    }
                    return (d, open);
                });
                if (connected) return dev;
                await Task.Delay(20);
            }
        }

        static ValueKind KindOf(TypeClass cls, int sizeBits) => cls switch
        {
            TypeClass.Signed => ValueKind.Signed,
            TypeClass.Enum or TypeClass.Unsigned => ValueKind.Unsigned,
            TypeClass.Float when sizeBits == 32 => ValueKind.Float,
            TypeClass.Double when sizeBits == 64 => ValueKind.Double,
            _ => ValueKind.Hex, // pointer, small struct / array: raw value
        };

        static ValueKind? ParseKind(string? display) => display?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "signed" or "int" => ValueKind.Signed,
            "unsigned" or "uint" => ValueKind.Unsigned,
            "hex" or "hexa" => ValueKind.Hex,
            "float" or "float32" => ValueKind.Float,
            "double" or "float64" => ValueKind.Double,
            _ => throw new AutomationException(AutomationError.InvalidArgument,
                $"display '{display}': expected signed, unsigned, hex, float or double"),
        };

        // address, size and kind of an expression: GDB evaluates it on the target (pointers followed), a map gives the symbol
        async Task<Target> ResolveTarget(string? device, string expression, int? sizeBits, string? display)
        {
            expression = expression.Trim();
            ValueKind? kind = ParseKind(display);
            var connected = await ConnectedDevice(device);
            var (dev, evaluation) = await ui.InvokeAsync(() =>
                (connected, connected.Sym is SymbolGDB ? connected.EvaluateAsync(connected.getVarFromSymbol(expression)) : null));
            string expr = dev.getVarFromSymbol(expression);

            if (evaluation != null)
            {
                ExprValue v = await evaluation;
                if (v.Error != null)
                    throw new AutomationException(AutomationError.InvalidArgument, $"'{expr}': {v.Error}");
                if (v.Address == null)
                    return new Target(dev, expr, 0, 0, ValueKind.Hex, null, v.Text ?? string.Empty);
                if (sizeBits != null && sizeBits != v.Size)
                    throw new AutomationException(AutomationError.InvalidArgument, $"'{expr}' is {v.Size} bits (size_bits must match or be omitted)");
                string ptype = await ui.InvokeAsync(() => ((SymbolGDB)dev.Sym!).ResolvedType(expr));
                var (cls, _) = ClassifyType(ptype);
                return new Target(dev, expr, v.Address.Value, v.Size, kind ?? KindOf(cls, v.Size), FirstLine(ptype), null);
            }

            return await ui.InvokeAsync(() =>
            {
                if (dev.Sym == null)
                    throw new AutomationException(AutomationError.NotSupported, $"device '{dev.Data.Name}': symbols not loaded");
                var sd = dev.GetSymbol(expr)
                    ?? throw new AutomationException(AutomationError.NotFound, $"'{expr}' not found in the symbols of '{dev.Data.Name}'");
                int size;
                if (sd.Size > 0)
                {
                    if (sizeBits != null && sizeBits != sd.Size)
                        throw new AutomationException(AutomationError.InvalidArgument, $"'{expr}' is {sd.Size} bits in the map (size_bits must match or be omitted)");
                    size = sd.Size;
                }
                else
                {
                    size = sizeBits ?? 32; // map without size
                    CheckAccessSize(size);
                }
                return new Target(dev, expr, Utils.strToAddress(sd.Address), size, kind ?? ValueKind.Signed, null, null);
            });
        }

        static string? FirstLine(string ptype)
        {
            string line = ptype.Split('\n')[0].Trim().TrimEnd('{').Trim();
            return line.Length == 0 ? null : line;
        }

        static void CheckAccessSize(int sizeBits)
        {
            if (sizeBits is not (8 or 16 or 32 or 64))
                throw new AutomationException(AutomationError.InvalidArgument, $"size_bits {sizeBits}: expected 8, 16, 32 or 64");
        }

        // one-shot read: the value arrives in a later frame, the callbacks run in Device.Process (UI thread)
        async Task<ulong> ReadRaw(DeviceCtrl dev, uint address, int sizeBits)
        {
            var tcs = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
            await ui.InvokeAsync(() =>
            {
                dev.ReadMem(address, sizeBits, v => tcs.TrySetResult(v),
                    () => tcs.TrySetException(new AutomationException(AutomationError.ReadFailed,
                        $"read 0x{address:X8} failed: no answer from device '{dev.Data.Name}' (or reconfiguration)")));
                return true;
            });
            if (await Task.WhenAny(tcs.Task, Task.Delay(ReadTimeoutMs)) != tcs.Task)
                throw new AutomationException(AutomationError.Timeout, $"read 0x{address:X8}: no answer after {ReadTimeoutMs} ms");
            return await tcs.Task;
        }

        static ulong Mask(ulong raw, int sizeBits) => sizeBits >= 64 ? raw : raw & ((1UL << sizeBits) - 1);

        static double Decode(ulong raw, int sizeBits, ValueKind kind) => kind switch
        {
            ValueKind.Signed => sizeBits >= 64 ? (long)raw : (long)(raw << (64 - sizeBits)) >> (64 - sizeBits),
            ValueKind.Float => BitConverter.Int32BitsToSingle((int)(uint)raw),
            ValueKind.Double => BitConverter.Int64BitsToDouble((long)raw),
            _ => Mask(raw, sizeBits),
        };

        static string KindName(ValueKind k) => k.ToString().ToLowerInvariant();

        VariableValue ToValue(Target t, ulong raw)
        {
            raw = Mask(raw, t.SizeBits);
            double value = Decode(raw, t.SizeBits, t.Kind);
            return new VariableValue(t.Dev.Data.Name, t.Expression, FormatAddress(t.Address), t.SizeBits,
                t.Type ?? KindName(t.Kind), double.IsFinite(value) ? value : null, raw,
                "0x" + raw.ToString("X" + Math.Max(1, t.SizeBits / 4)), null);
        }

        public async Task<VariableValue> ReadVariable(string? device, string expression, int? sizeBits, string? display)
        {
            var t = await ResolveTarget(device, expression, sizeBits, display);
            if (t.Text != null)
                return new VariableValue(t.Dev.Data.Name, t.Expression, null, null, null, null, null, null, t.Text);
            return ToValue(t, await ReadRaw(t.Dev, t.Address, t.SizeBits));
        }

        // one after the other: a single one-shot read at a time on the link
        public async Task<List<VariableValue>> ReadVariables(string? device, string[] expressions)
        {
            var list = new List<VariableValue>();
            foreach (string e in expressions)
            {
                try
                {
                    list.Add(await ReadVariable(device, e, null, null));
                }
                catch (AutomationException ex) when (ex.Code is not (AutomationError.NotConnected or AutomationError.NotSupported))
                {
                    list.Add(new VariableValue(device ?? string.Empty, e, null, null, null, null, null, null, null, $"{ex.Code}: {ex.Detail}"));
                }
            }
            return list;
        }

        // JSON number or string ("0x20000010", "536870928")
        static uint ParseAddress(JsonElement address)
        {
            string text = address.ValueKind switch
            {
                JsonValueKind.Number => address.GetRawText(),
                JsonValueKind.String => address.GetString()!.Trim(),
                _ => string.Empty,
            };
            bool ok = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint a)
                : uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out a);
            if (!ok)
                throw new AutomationException(AutomationError.InvalidArgument, $"address '{text}': 32-bit address, decimal or 0x hexadecimal");
            return a;
        }

        public async Task<VariableValue> ReadMemory(string? device, JsonElement addressArg, int sizeBits)
        {
            uint address = ParseAddress(addressArg);
            CheckAccessSize(sizeBits);
            var dev = await ConnectedDevice(device);
            var t = new Target(dev, FormatAddress(address), address, sizeBits, ValueKind.Hex, null, null);
            return ToValue(t, await ReadRaw(dev, address, sizeBits));
        }

        void CheckWriteAllowed()
        {
            if (!AllowWrite())
                throw new AutomationException(AutomationError.WriteDisabled,
                    "writing the target memory is disabled (eScope > Tools > AI automation > Allow writing target memory)");
        }

        public async Task<VariableValue> WriteVariable(string? device, string expression, JsonElement value, int? sizeBits, string? display)
        {
            CheckWriteAllowed();
            var t = await ResolveTarget(device, expression, sizeBits, display);
            if (t.Text != null)
                throw new AutomationException(AutomationError.InvalidArgument,
                    $"'{t.Expression}' has no address (not an lvalue, or larger than 64 bits): cannot be written");
            return await Write(t, Encode(value, t.SizeBits, t.Kind));
        }

        public async Task<VariableValue> WriteMemory(string? device, JsonElement addressArg, int sizeBits, JsonElement value)
        {
            CheckWriteAllowed();
            uint address = ParseAddress(addressArg);
            CheckAccessSize(sizeBits);
            var dev = await ConnectedDevice(device);
            var t = new Target(dev, FormatAddress(address), address, sizeBits, ValueKind.Hex, null, null);
            return await Write(t, Encode(value, sizeBits, ValueKind.Unsigned, allowNegative: true));
        }

        // write queued before the read back: both are sent in this order
        async Task<VariableValue> Write(Target t, ulong data)
        {
            await ui.InvokeAsync(() =>
            {
                t.Dev.WriteMem(t.Address, t.SizeBits, data);
                return true;
            });
            return ToValue(t, await ReadRaw(t.Dev, t.Address, t.SizeBits));
        }

        // JSON number or string ("42", "-3", "0x1F", "1.5") to the raw bits of the variable
        static ulong Encode(JsonElement value, int sizeBits, ValueKind kind, bool allowNegative = false)
        {
            string text = value.ValueKind switch
            {
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.String => value.GetString()!.Trim(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => throw new AutomationException(AutomationError.InvalidArgument, "value: a number or a string such as \"0x1F\""),
            };
            var ic = CultureInfo.InvariantCulture;
            if (kind is ValueKind.Float or ValueKind.Double)
            {
                if (!double.TryParse(text, NumberStyles.Float, ic, out double d))
                    throw new AutomationException(AutomationError.InvalidArgument, $"value '{text}': a number is expected");
                return kind == ValueKind.Float
                    ? (uint)BitConverter.SingleToInt32Bits((float)d)
                    : (ulong)BitConverter.DoubleToInt64Bits(d);
            }

            bool negative;
            ulong magnitude;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                negative = false;
                if (!ulong.TryParse(text[2..], NumberStyles.HexNumber, ic, out magnitude))
                    throw new AutomationException(AutomationError.InvalidArgument, $"value '{text}': invalid hexadecimal");
            }
            else if (long.TryParse(text, NumberStyles.Integer, ic, out long l))
            {
                negative = l < 0;
                magnitude = negative ? (ulong)(-(l + 1)) + 1 : (ulong)l;
            }
            else if (ulong.TryParse(text, NumberStyles.Integer, ic, out ulong u))
            {
                negative = false;
                magnitude = u;
            }
            else
            {
                throw new AutomationException(AutomationError.InvalidArgument,
                    $"value '{text}': an integer is expected (decimal or 0x hexadecimal)");
            }

            ulong max = sizeBits >= 64 ? ulong.MaxValue : (1UL << sizeBits) - 1;
            if (negative)
            {
                ulong minMagnitude = 1UL << (sizeBits - 1); // most negative value of the signed type
                if ((kind != ValueKind.Signed && !allowNegative) || magnitude > minMagnitude)
                    throw new AutomationException(AutomationError.InvalidArgument, $"value {text} out of range for {sizeBits} bits {KindName(kind)}");
                return Mask(unchecked(0UL - magnitude), sizeBits);
            }
            ulong limit = kind == ValueKind.Signed && !text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? max >> 1 : max;
            if (magnitude > limit)
                throw new AutomationException(AutomationError.InvalidArgument, $"value {text} out of range for {sizeBits} bits {KindName(kind)}");
            return magnitude;
        }
    }
}
