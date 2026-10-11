using eScope.Command;
using eScope.Plot;
using eScope.Symbol;
using System.Text.RegularExpressions;

namespace eScope.Automation
{
    // changes: traces, channels, data, plot objects, view, undo / redo.
    // Changes of the model go through commandMgr (undoable, "AI: ..."); view, selection and trace creation do not,
    // as in the UI.
    public partial class AutomationService
    {
        static void Execute(ICommand cmd, string? description = null)
            => commandMgr.ExecuteCommand(new AiCommand(cmd, description));

        // --- workspace ---

        public Task<string> CreateTrace(string? name) => ui.InvokeAsync(() =>
        {
            if (!string.IsNullOrWhiteSpace(name) && eScope.Traces.Any(t => t.Data.Name == name.Trim()))
                throw new AutomationException(AutomationError.InvalidArgument, $"a trace named '{name}' already exists");
            return scope.NewTrace(name).Data.Name;
        });

        public Task<string> SelectTrace(string trace) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            if (!scope.SelectTrace(trc))
                throw new AutomationException(AutomationError.Busy,
                    $"recording on trace '{scope.GetCurrentTrace()?.Data.Name}': the current trace cannot change until it stops");
            return trc.Data.Name;
        });

        public Task<string> RenameTrace(string? trace, string name) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            name = name.Trim();
            if (name.Length == 0)
                throw new AutomationException(AutomationError.InvalidArgument, "empty name");
            if (eScope.Traces.Any(t => t != trc && t.Data.Name == name))
                throw new AutomationException(AutomationError.InvalidArgument, $"a trace named '{name}' already exists");
            if (name != trc.Data.Name)
                Execute(new CmdRenameTrace(trc, name));
            return name;
        });

        public Task<string> SaveWorkspace() => ui.InvokeAsync(() => scope.WriteWorkspace());

        // the user's history: undoes the last change, made by the AI or by the user
        public Task<string?> Undo() => ui.InvokeAsync(() =>
        {
            if (!commandMgr.CanUndo) return (string?)null;
            string label = commandMgr.UndoLabel["Undo ".Length..];
            commandMgr.Undo();
            return label;
        });

        public Task<string?> Redo() => ui.InvokeAsync(() =>
        {
            if (!commandMgr.CanRedo) return (string?)null;
            string label = commandMgr.RedoLabel["Redo ".Length..];
            commandMgr.Redo();
            return label;
        });

        // --- channels ---

        static Utils.Display? ParseDisplay(string? display) => display?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "signed" or "signedint" or "int" => Utils.Display.signedInt,
            "unsigned" or "unsignedint" or "uint" => Utils.Display.unsignedInt,
            "hex" or "hexa" => Utils.Display.hexa,
            "float" or "float32" => Utils.Display.float32,
            _ => throw new AutomationException(AutomationError.InvalidArgument,
                $"display '{display}': expected signed, unsigned, hex or float"),
        };

        // a size known by the symbols is applied by the device to the channel: another one would not stay
        static void CheckSymbolSize(SymbolData sd, int sizeBits)
        {
            if (sd.Size > 0 && Math.Min(sd.Size, 64) != sizeBits)
                throw new AutomationException(AutomationError.InvalidArgument,
                    $"'{sd.Name}' is {sd.Size} bits in the symbols: the channel size follows it (size_bits only for a symbol of unknown size)");
        }

        static void CheckSize(int sizeBits)
        {
            if (sizeBits < 1 || sizeBits > 64)
                throw new AutomationException(AutomationError.InvalidArgument, $"size_bits {sizeBits}: expected 1 to 64");
        }

        internal enum TypeClass { Unknown, Pointer, Array, Aggregate, Enum, Float, Double, Unsigned, Signed }

        static readonly Regex PointerToArray = new(@"\(\s*\*\s*\)\s*\[");
        static readonly Regex ArraySuffix = new(@"\[\s*\d*\s*\]");
        static readonly Regex Qualifiers = new(@"\b(const|volatile|restrict|__restrict)\b\s*");

        // class of a C type given by GDB (ptype, typedefs resolved); head: the type before a struct / union body
        internal static (TypeClass Class, string Head) ClassifyType(string ptype)
        {
            string text = ptype.Trim();
            if (text.Length == 0)
                return (TypeClass.Unknown, text);
            // a struct / union / enum: only what is outside its body describes the expression
            string head = text, outside = text;
            int brace = text.IndexOf('{');
            if (brace >= 0)
            {
                head = text[..brace].Trim();
                outside = head + " " + text[(text.LastIndexOf('}') + 1)..].Trim();
            }
            head = Qualifiers.Replace(head, " ").Trim(); // 'volatile float' is a float
            if (PointerToArray.IsMatch(outside)) return (TypeClass.Pointer, head); // pointer to an array
            if (ArraySuffix.IsMatch(outside)) return (TypeClass.Array, head);
            if (outside.Contains('*')) return (TypeClass.Pointer, head); // pointer, function pointer
            if (head.StartsWith("struct") || head.StartsWith("union") || head.StartsWith("class"))
                return (TypeClass.Aggregate, head);
            if (head.StartsWith("enum")) return (TypeClass.Enum, head);
            if (head is "float") return (TypeClass.Float, head);
            if (head.Contains("double")) return (TypeClass.Double, head);
            if (head.Contains("unsigned") || head is "_Bool" or "bool") return (TypeClass.Unsigned, head);
            return (TypeClass.Signed, head);
        }

        // display format of a channel, error text when the type cannot be plotted
        internal static (Utils.Display? Display, string? Error) DisplayFromType(string expression, string ptype)
        {
            var (cls, head) = ClassifyType(ptype);
            return cls switch
            {
                TypeClass.Unknown => (null, $"type of '{expression}' unknown: give size_bits and display"),
                TypeClass.Pointer => (Utils.Display.hexa, null),
                TypeClass.Array => (null, $"'{expression}' is an array: give an element (e.g. {expression}[0])"),
                TypeClass.Aggregate => (null, $"'{expression}' is a {head.Split(' ')[0]}: give a member (e.g. {expression}.field)"),
                TypeClass.Enum or TypeClass.Unsigned => (Utils.Display.unsignedInt, null),
                TypeClass.Float => (Utils.Display.float32, null),
                TypeClass.Double => (null, $"'{expression}' is a double: not supported (float, integers and pointers only)"),
                _ => (Utils.Display.signedInt, null),
            };
        }

        public Task<ChannelInfo> AddChannel(string? trace, string? device, string symbol, string? name, string? unit,
            string? color, int? sizeBits, string? display) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var dev = ResolveDevice(device);
            if (dev.Sym == null)
                throw new AutomationException(AutomationError.NotSupported, $"device '{dev.Data.Name}': symbols not loaded");
            symbol = dev.getVarFromSymbol(symbol.Trim());
            name = string.IsNullOrWhiteSpace(name) ? symbol : name.Trim();
            if (trc.Exists(name))
                throw new AutomationException(AutomationError.InvalidArgument, $"'{name}' already exists in trace '{trc.Data.Name}'");

            var sd = dev.GetSymbol(symbol)
                ?? throw new AutomationException(AutomationError.NotFound, $"'{symbol}' not found in the symbols of '{dev.Data.Name}'");

            var ch = new Channel { Name = name, Device = dev.Data.Name, Symbol = symbol, hasDevice = true, hasFunction = false };
            if (!string.IsNullOrEmpty(color)) ch.Color = color;
            if (unit != null) ch.Unit = unit;

            // size: the symbol's when known (the device applies it to the channel), else given, else channel default.
            // format: given, else deduced from the GDB type, else channel default
            if (sizeBits is int s)
            {
                CheckSize(s);
                CheckSymbolSize(sd, s);
                ch.Size = s;
            }
            else if (sd.Size > 0)
            {
                ch.Size = Math.Min(sd.Size, 64);
            }
            Utils.Display? disp = ParseDisplay(display);
            if (dev.Sym is SymbolGDB gdb)
            {
                var (deduced, error) = DisplayFromType(symbol, gdb.ResolvedType(symbol));
                if (deduced == null)
                    throw new AutomationException(AutomationError.InvalidArgument, error!);
                disp ??= deduced;
            }
            if (disp != null) ch.Display = disp.Value;
            if (ch.Display == Utils.Display.float32 && ch.Size != 32)
                throw new AutomationException(AutomationError.InvalidArgument, "float display needs size_bits = 32");

            // pointer not readable yet (device not connected): resolved again when the device connects
            if (sd.Resolved) ch.Address = Utils.strToAddress(sd.Address);

            Execute(new CmdAddObj(trc, ch), $"Add channel '{name}'");
            return ToChannelInfo(trc.GetSignalFromName(name)!);
        });

        public Task<ChannelInfo> AddComputedChannel(string? trace, string name, string script, string? unit, string? color, double? periodMs)
            => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            name = name.Trim();
            if (name.Length == 0)
                throw new AutomationException(AutomationError.InvalidArgument, "empty name");
            if (trc.Exists(name))
                throw new AutomationException(AutomationError.InvalidArgument, $"'{name}' already exists in trace '{trc.Data.Name}'");
            CheckScript(trc, name, script);
            CheckPeriod(periodMs);

            var ch = new Channel { Name = name, hasDevice = false, hasFunction = true, Script = script, Device = string.Empty, Symbol = string.Empty };
            if (periodMs != null) ch.SamplingTime = periodMs.Value;
            if (!string.IsNullOrEmpty(color)) ch.Color = color;
            if (unit != null) ch.Unit = unit;
            Execute(new CmdAddObj(trc, ch), $"Add computed channel '{name}'");
            return ToChannelInfo(trc.GetSignalFromName(name)!);
        });

        static void CheckPeriod(double? periodMs)
        {
            if (periodMs is double p && !(p > 0))
                throw new AutomationException(AutomationError.InvalidArgument, "period_ms must be > 0");
        }

        static void CheckScript(TraceCtrl trc, string name, string script)
        {
            string result = ChannelCtrl.CompileScript(name, script, trc.ScriptGlobalsDict, out var compiled);
            if (compiled == null)
                throw new AutomationException(AutomationError.InvalidArgument, $"script: {result}");
        }

        public Task<ChannelInfo> ConfigureChannel(string? trace, string channel, string? name, string? unit, string? color,
            bool? visible, string? display, int? sizeBits, double? yGain, double? yOffset, double? xOffset, string? script, double? periodMs)
            => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var ctrl = ResolveChannel(trc, channel);
            var s = CmdConfigureChannel.Capture(ctrl.SignalBase);

            if (name != null)
            {
                name = name.Trim();
                if (name.Length == 0)
                    throw new AutomationException(AutomationError.InvalidArgument, "empty name");
                if (name != s.Name && trc.Exists(name))
                    throw new AutomationException(AutomationError.InvalidArgument, $"'{name}' already exists in trace '{trc.Data.Name}'");
                s = s with { Name = name };
            }
            if (unit != null) s = s with { Unit = unit };
            if (!string.IsNullOrEmpty(color)) s = s with { Color = color };
            if (visible != null) s = s with { IsVisible = visible.Value };
            if (ParseDisplay(display) is Utils.Display d) s = s with { Display = d };
            if (sizeBits is int size)
            {
                CheckSize(size);
                if (ctrl.SignalBase.hasDevice && ctrl.Dev?.GetSymbol(ctrl.SignalBase.Symbol) is SymbolData sd)
                    CheckSymbolSize(sd, size);
                s = s with { Size = size };
            }
            if (yGain != null)
            {
                if (yGain <= 0)
                    throw new AutomationException(AutomationError.InvalidArgument, "y_gain must be > 0");
                s = s with { YGain = yGain.Value };
            }
            // the API gives the offset in channel units, stored in divisions (as in the channel editor)
            if (yOffset != null) s = s with { YOffset = yOffset.Value / s.YGain };
            else if (yGain != null) s = s with { YOffset = ctrl.SignalBase.YOffset * ctrl.SignalBase.YGain / s.YGain };
            if (xOffset != null) s = s with { XOffset = xOffset.Value };
            if (script != null)
            {
                if (script.Trim().Length == 0)
                {
                    if (!ctrl.SignalBase.hasDevice)
                        throw new AutomationException(AutomationError.InvalidArgument, "a computed channel needs a script");
                    s = s with { HasFunction = false };
                }
                else
                {
                    CheckScript(trc, s.Name, script);
                    s = s with { HasFunction = true, Script = script };
                }
            }
            if (periodMs != null)
            {
                if (ctrl.SignalBase.hasDevice)
                    throw new AutomationException(AutomationError.InvalidArgument,
                        "period_ms: only for a channel without device (a device channel uses the device sampling time)");
                CheckPeriod(periodMs);
                s = s with { SamplingTime = periodMs.Value };
            }
            if (s.Display == Utils.Display.float32 && s.Size != 32)
                throw new AutomationException(AutomationError.InvalidArgument, "float display needs size_bits = 32");

            if (s != CmdConfigureChannel.Capture(ctrl.SignalBase))
                Execute(new CmdConfigureChannel(trc, ctrl, s));
            return ToChannelInfo(ctrl);
        });

        public Task<string> RemoveChannel(string? trace, string channel) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var ctrl = ResolveChannel(trc, channel);
            Execute(new CmdDeleteObj(trc, ctrl), $"Remove channel '{ctrl.SignalBase.Name}'");
            return ctrl.SignalBase.Name;
        });

        // --- data ---

        // samples of [from, to] removed from each channel, the following samples move back in time (as Edit > Delete)
        public Task<int> DeleteData(string? trace, string[]? channels, string? range, double? fromMs, double? toMs, bool all)
            => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var (from, to) = all ? (double.NegativeInfinity, double.PositiveInfinity) : ResolveRange(trc, range, fromMs, toMs);
            if (!all && range is null or "all" && fromMs == null && toMs == null)
                throw new AutomationException(AutomationError.InvalidArgument, "give a range (from_ms / to_ms, selection or view), or use clear_data");
            return RemoveSamples(ResolveChannels(trc, channels), from, to, all ? "Clear data" : "Delete data");
        });

        // samples of [from, to] removed from the channels in one undoable command, returns their count
        static int RemoveSamples(List<ChannelCtrl> channels, double from, double to, string description)
        {
            var cmds = new CompositeCommand();
            int removed = 0;
            foreach (var c in channels)
            {
                var (first, last) = SampleRange(c, from, to);
                if (last < first) continue;
                removed += last - first + 1;
                double t0 = StartTime(c);
                // half a sample around the bounds, so that the rounding of the command keeps exactly these samples;
                // never before t0: the command would move the channel offset back
                double start = first == 0 ? t0 : t0 + (first - 0.5) * c.Period;
                double width = t0 + (last + 0.5) * c.Period - start;
                cmds.Add(c.CmdDeleteData(start, width));
            }
            if (removed > 0)
                Execute(cmds, description);
            return removed;
        }

        // --- plot objects ---

        string UniqueName(TraceCtrl trc, string? name, string prefix)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = name.Trim();
                if (trc.Exists(name))
                    throw new AutomationException(AutomationError.InvalidArgument, $"'{name}' already exists in trace '{trc.Data.Name}'");
                return name;
            }
            int i = 1;
            while (trc.Exists(prefix + i)) i++;
            return prefix + i;
        }

        public Task<string> AddMarker(string? trace, double timeMs, string? name, string? color) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var m = new Marker { Name = UniqueName(trc, name, "Marker"), Pos = timeMs };
            if (!string.IsNullOrEmpty(color)) m.Color = color;
            Execute(new CmdAddObj(trc, m), $"Add marker '{m.Name}'");
            return m.Name;
        });

        public Task<DeltaInfo> AddDelta(string? trace, double fromMs, double toMs, string? name, string? color) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            if (toMs < fromMs)
                throw new AutomationException(AutomationError.InvalidArgument, "to_ms is before from_ms");
            var d = new Plot.Delta { Name = UniqueName(trc, name, "Delta"), X1 = fromMs, X2 = toMs };
            if (!string.IsNullOrEmpty(color)) d.Color = color;
            Execute(new CmdAddObj(trc, d), $"Add delta '{d.Name}'");
            return new DeltaInfo(d.Name, fromMs, toMs, toMs - fromMs, d.Color, d.IsVisible);
        });

        // text pointing at the channel curve at time_ms, one division above it
        public Task<string> AddAnnotation(string? trace, string channel, double timeMs, string text, string? color) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var c = ResolveChannel(trc, channel);
            if (string.IsNullOrWhiteSpace(text))
                throw new AutomationException(AutomationError.InvalidArgument, "empty text");
            if (trc.Exists(text))
                throw new AutomationException(AutomationError.InvalidArgument, $"'{text}' already exists in trace '{trc.Data.Name}'");
            double value = 0;
            if (c.Y.Count > 0 && c.Period > 0)
            {
                int i = Math.Clamp((int)Math.Round((timeMs - StartTime(c)) / c.Period), 0, c.Y.Count - 1);
                value = c.Y[i];
            }
            // stored relative to the channel offsets (as the annotation editor does)
            double x = timeMs - c.SignalBase.XOffset;
            var a = new Annotation
            {
                Name = text,
                Channel = c.SignalBase.Name,
                TipLocationX = x,
                TipLocationY = value,
                TextLocationX = x,
                TextLocationY = value + c.SignalBase.YGain,
                Color = string.IsNullOrEmpty(color) ? c.SignalBase.Color : color,
            };
            Execute(new CmdAddObj(trc, a), $"Add annotation '{text}'");
            return a.Name;
        });

        public Task<string> AddDataMarker(string? trace, string channel, double value, string? name) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var c = ResolveChannel(trc, channel);
            var m = new DataMarker { Name = UniqueName(trc, name, "DataMarker"), Channel = c.SignalBase.Name, Value = value, Color = c.SignalBase.Color };
            Execute(new CmdAddObj(trc, m), $"Add data marker '{m.Name}'");
            return m.Name;
        });

        public Task<string> RemoveAnnotation(string? trace, string name) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            var ctrl = trc.Ctrls.FirstOrDefault(c =>
                    c.DataBase.Name == name && c is MarkerCtrl or DeltaCtrl or AnnotationCtrl or DataMarkerCtrl
                    && c != trc.Plot.dataSelector)
                ?? throw new AutomationException(AutomationError.NotFound, $"no marker, delta, annotation or data marker '{name}' in trace '{trc.Data.Name}'");
            Execute(new CmdDeleteObj(trc, ctrl), $"Remove '{name}'");
            return name;
        });

        // --- view ---

        public Task<TimeRange> SetView(string? trace, double fromMs, double toMs) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            if (toMs <= fromMs)
                throw new AutomationException(AutomationError.InvalidArgument, "to_ms must be after from_ms");
            trc.Plot.SetView(fromMs, toMs - fromMs, immediate: true);
            return View(trc);
        });

        public Task<TimeRange> ZoomFit(string? trace) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            trc.Plot.FitAll(immediate: true);
            return View(trc);
        });

        // both bounds null: selection removed
        public Task<TimeRange?> SetSelection(string? trace, double? fromMs, double? toMs) => ui.InvokeAsync(() =>
        {
            var trc = ResolveTrace(trace);
            if (fromMs == null && toMs == null)
            {
                trc.Plot.CancelSelect();
                return null;
            }
            if (fromMs == null || toMs == null || toMs <= fromMs)
                throw new AutomationException(AutomationError.InvalidArgument, "give from_ms < to_ms, or neither to remove the selection");
            trc.Plot.DataSelectorConfigure(fromMs.Value, toMs.Value - fromMs.Value);
            trc.Plot.RefreshReq = true;
            return Selection(trc);
        });
    }
}
