using eScope.Command;
using Microsoft.Scripting.Runtime;
using ScottPlot.Rendering.RenderActions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.Plot
{
    internal class PlotCmdDeleteData:ICommand
    {
        public string Description { get; }

        CompositeCommand cmds;
        double XStart;
        double XWidth;
        IPlotContext Context;
        public PlotCmdDeleteData(IPlotContext context, string description) 
        {
            Description = description;
            Context = context;
            XStart = Context.Plot.GetSelectStart();
            XWidth = Context.Plot.GetSelectWidth();
            cmds = new CompositeCommand();
            var signals = Context.GetSignalList();
            foreach (ChannelCtrl c in signals)
            {
                var cmd = c.CmdDeleteData(XStart, XWidth);
                cmds.Add(cmd);
            }
            SaveXPositions();
            AddInSelectionDeletes();
        }

        bool InSelection(double x) => x >= XStart && x <= XStart + XWidth;

        // Markers, deltas and annotations lying inside the selection are deleted with the data (undoable)
        void AddInSelectionDeletes()
        {
            var inSelection = new List<CtrlBase>();
            foreach (var c in Context.Ctrls)
            {
                bool inside = c.DataBase switch
                {
                    Marker m => m.Fixed == 0 && m.Name != "Watch" && InSelection(m.Pos),
                    Delta d => InSelection(d.X1) && InSelection(d.X2),
                    // annotation is stored relative to its signal offset: the tip decides
                    Annotation a => InSelection(a.TipLocationX + (Context.GetSignalFromName(a.Channel)?.SignalBase.XOffset ?? 0)),
                    _ => false
                };
                if (inside) inSelection.Add(c);
            }
            // CmdDeleteObj stores the list index: delete from the last one so the indexes stay valid
            foreach (var c in inSelection.OrderByDescending(c => Context.Ctrls.IndexOf(c)))
                cmds.Add(new CmdDeleteObj((TraceCtrl)Context, c));
        }

        // Saved x positions of markers/deltas/annotations, restored on undo
        List<(Marker D, double Pos)> markerOld = new();
        List<(Delta D, double X1, double X2)> deltaOld = new();
        List<(Annotation D, double TextX, double TipX, double OffsetBefore)> annotOld = new();

        // Position after removing [XStart, XStart+XWidth]: later data shifts left, removed area collapses
        double Shift(double x)
        {
            if (x <= XStart) return x;
            if (x >= XStart + XWidth) return x - XWidth;
            return XStart;
        }
        void SaveXPositions()
        {
            foreach (var c in Context.Ctrls)
            {
                switch (c.DataBase)
                {
                    case Marker m when m.Fixed == 0: markerOld.Add((m, m.Pos)); break;
                    case Delta d: deltaOld.Add((d, d.X1, d.X2)); break;
                    case Annotation a:
                        var sig = Context.GetSignalFromName(a.Channel);
                        annotOld.Add((a, a.TextLocationX, a.TipLocationX, sig?.SignalBase.XOffset ?? 0));
                        break;
                }
            }
        }
        void ShiftXPositions()
        {
            foreach (var (m, pos) in markerOld) m.Pos = Shift(pos);
            foreach (var (d, x1, x2) in deltaOld) { d.X1 = Shift(x1); d.X2 = Shift(x2); }
            foreach (var (a, textX, tipX, before) in annotOld)
            {
                double after = Context.GetSignalFromName(a.Channel)?.SignalBase.XOffset ?? 0;
                a.TextLocationX = Shift(textX + before) - after;
                a.TipLocationX = Shift(tipX + before) - after;
            }
        }
        void RestoreXPositions()
        {
            foreach (var (m, pos) in markerOld) m.Pos = pos;
            foreach (var (d, x1, x2) in deltaOld) { d.X1 = x1; d.X2 = x2; }
            foreach (var (a, textX, tipX, _) in annotOld) { a.TextLocationX = textX; a.TipLocationX = tipX; }
        }
        void ConfigureXObjects()
        {
            foreach (var c in Context.Ctrls)
            {
                if (c is MarkerCtrl or DeltaCtrl or AnnotationCtrl)
                    c.Configure();
            }
        }
        public void Execute()
        {
            cmds.Execute();
            ShiftXPositions();
            ConfigureXObjects();
            Context.Plot.dataSelector.SetVisibility(false);
        }
        public void Undo()
        {
            cmds.Undo();
            Context.Plot.ClearSelection();
            RestoreXPositions();
            ConfigureXObjects();
            Context.Plot.DataSelectorConfigure(XStart, XWidth);
        }
    }
    internal class PlotCmdPasteData:ICommand
    {
        public string Description { get; } = "Paste Data";

        // channels missing in this trace, created before the data is inserted
        List<Channel> newChannels = new();
        CompositeCommand? addCmds;
        CompositeCommand? cmds;
        double XStart;
        double XWidth;
        Dictionary<string, List<double>> data;
        IPlotContext Context;
        public PlotCmdPasteData(IPlotContext context)
        {
            Context = context;
            string text = Clipboard.GetText();

            data = TableParser.ParseToDictionary(text);
            XStart = Context.Plot.watchLine.Data.Pos;
            XWidth = 0;

            // definitions copied from an eScope trace, otherwise plain columns (e.g. spreadsheet)
            var defs = new List<Channel>();
            if (Clipboard.GetData(eScopePlot.ChannelsClipboardFormat) is string json)
                defs = System.Text.Json.JsonSerializer.Deserialize<List<Channel>>(json) ?? defs;

            foreach (string col in data.Keys.Where(k => k.EndsWith(" Data")))
            {
                string name = col[..^" Data".Length];
                if (Context.GetSignalFromName(name) != null || newChannels.Any(c => c.Name == name)) continue;
                newChannels.Add(defs.FirstOrDefault(d => d.Name == name) is Channel def
                    ? NewFromDefinition(def)
                    : new Channel { Name = name, Device = string.Empty, Display = Utils.Display.float32, Size = 32 });
            }
            if (!Context.GetSignalList().Any(c => data.ContainsKey(c.SignalBase.Name + " Data")) && newChannels.Count == 0)
                throw new InvalidOperationException("No channel data to paste");
        }
        // copy of a channel from another trace: own id, no link to the source trace channels
        static Channel NewFromDefinition(Channel def)
        {
            def.Id = Guid.NewGuid();
            foreach (var lh in new[] { def.linkXOffset, def.linkYOffset, def.linkYGain })
            {
                lh.Id = def.Id;
                lh.SrcId = null;
            }
            def.XCreation = 0;
            return def;
        }
        // built at first execution: needs the channels created and configured (period, device)
        CompositeCommand BuildPasteCmds()
        {
            var pasteCmds = new CompositeCommand();
            var targets = Context.GetSignalList()
                .Where(c => data.ContainsKey(c.SignalBase.Name + " Data")) // not copied: left as is
                .Where(c => c.SignalBase.hasDevice || !newChannels.Contains(c.SignalBase)) // new function channel: computed by its script
                .ToList();

            // snap the paste position on the sample grid of a channel holding data, the others follow it
            if (targets.FirstOrDefault(c => c.SampleCount > 0) is ChannelCtrl gridRef)
            {
                double t0 = gridRef.SignalBase.XOffset + gridRef.SignalBase.XCreation;
                XStart = t0 + Math.Round((XStart - t0) / gridRef.Period) * gridRef.Period;
            }

            foreach (ChannelCtrl c in targets)
            {
                var dataCol = data[c.SignalBase.Name + " Data"];

                // shorter columns are padded with empty cells, parsed as NaN
                var values = TrimTrailingNaN(dataCol);

                // raw column (copied from eScope): restore device samples as is, "Data" may be a function output
                List<UInt64>? raw = null;
                if (c.SignalBase.hasDevice && data.TryGetValue(c.SignalBase.Name + " Raw", out var rawCol))
                {
                    var rawValues = TrimTrailingNaN(rawCol);
                    if (rawValues.Count == values.Count && rawValues.All(v => !double.IsNaN(v)))
                    {
                        raw = rawValues.Select(v => (UInt64)v).ToList();
                    }
                }

                pasteCmds.Add(PasteAt(c, values, raw));
                XWidth = Math.Max(XWidth, values.Count * c.Period);
            }
            return pasteCmds;
        }
        // data starts at XStart for every channel: a gap with the existing samples is filled by holding
        // the nearest value and marked as lost, as for samples not received
        ISignalPlotCmdPasteData PasteAt(ChannelCtrl c, List<double> values, List<UInt64>? raw)
        {
            var ch = c.SignalBase;
            int n = c.SampleCount;
            if (n == 0)
                return new ISignalPlotCmdPasteData(c, 0, values, raw, XStart - ch.XOffset); // empty: starts at XStart

            int pos = (int)Math.Round((XStart - ch.XOffset - ch.XCreation) / c.Period);
            if (pos >= 0 && pos <= n)
                return new ISignalPlotCmdPasteData(c, pos, values, raw);

            if (ch.hasDevice)
                raw ??= values.Select(d => Utils.DoubleToUInt64(d, ch.Size, ch.Shift, ch.Display)).ToList();
            if (pos > n)
            {
                // after the end: hold the last sample up to XStart
                int gap = pos - n;
                var data = Enumerable.Repeat(c.Y.Count > 0 ? c.Y[^1] : 0, gap).Concat(values).ToList();
                var r = raw == null ? null : Enumerable.Repeat(ch.Y[^1], gap).Concat(raw).ToList();
                return new ISignalPlotCmdPasteData(c, n, data, r, lostIndex: n, lostCount: gap);
            }
            else
            {
                // before the start: the channel now starts at XStart, hold the first sample up to the old start
                int gap = Math.Max(0, -pos - values.Count);
                var data = values.Concat(Enumerable.Repeat(c.Y.Count > 0 ? c.Y[0] : 0, gap)).ToList();
                var r = raw == null ? null : raw.Concat(Enumerable.Repeat(ch.Y[0], gap)).ToList();
                return new ISignalPlotCmdPasteData(c, 0, data, r, ch.XCreation + pos * c.Period, values.Count, gap);
            }
        }
        static List<double> TrimTrailingNaN(List<double> l)
        {
            int n = l.Count;
            while (n > 0 && double.IsNaN(l[n - 1])) n--;
            return l.GetRange(0, n);
        }
        // raw samples changed: rebuild display data, plain channels first since functions may read other channels
        void RecomputeChannels()
        {
            var signals = Context.GetSignalList();
            foreach (var c in signals.Where(c => !c.SignalBase.hasFunction)) c.RecomputeFromRaw();
            foreach (var c in signals.Where(c => c.SignalBase.hasFunction)) c.RecomputeFromRaw();
        }
        public void Execute()
        {
            if (addCmds == null)
            {
                // executed one by one: each add takes the index after the previous one
                addCmds = new CompositeCommand();
                foreach (var ch in newChannels)
                {
                    var add = new CmdAddObj((TraceCtrl)Context, ch);
                    add.Execute();
                    addCmds.Add(add);
                }
            }
            else
            {
                addCmds.Execute();
            }
            cmds ??= BuildPasteCmds();
            cmds.Execute();
            RecomputeChannels();

            Context.Plot.DataSelectorConfigure(XStart, XWidth);
        }
        public void Undo()
        {
            cmds?.Undo();
            addCmds?.Undo();
            RecomputeChannels();
            Context.Plot.dataSelector.SetVisibility(false);
        }
    }
    internal class ISignalPlotCmdPasteData:ICommand
    {
        ChannelCtrl C;
        int Index;
        List<double> Data;
        List<UInt64>? Raw;
        double? XCreation;
        double OldXCreation;
        int LostIndex;
        int LostCount;
        public string Description { get { return "Paste data"; } }
        // xCreation: new start time of the channel (data inserted before its first sample, or empty channel)
        // lostIndex/lostCount: filler samples between the pasted data and the existing ones
        public ISignalPlotCmdPasteData(ChannelCtrl c, int index, List<double> data, List<UInt64>? raw, double? xCreation = null, int lostIndex = 0, int lostCount = 0)
        {
            C = c;
            Index = index;
            Data = data;
            Raw = raw;
            XCreation = xCreation;
            LostIndex = lostIndex;
            LostCount = lostCount;
        }
        public void Execute()
        {
            OldXCreation = C.SignalBase.XCreation;
            if (XCreation is double x) C.SignalBase.XCreation = x;
            C.InsertRange(Index, Data, Raw);
            if (LostCount > 0 && C.SignalBase.hasDevice)
            {
                var lost = C.SignalBase.Lost;
                int at = lost.FindIndex(l => l.Index > LostIndex);
                lost.Insert(at < 0 ? lost.Count : at, new LostData { Index = LostIndex, Count = LostCount });
            }
        }
        public void Undo()
        {
            // the filler lost entry lies inside the removed range: dropped by RemoveRange
            C.SignalBase.XCreation = OldXCreation;
            C.RemoveRange(Index, Data.Count);
        }
    }
    /// <summary>
    /// Settings changed by an editor (channel, marker, delta, annotation, data marker), restored on undo.
    /// Only the changed values are kept, so that the changes made meanwhile outside the editor
    /// (marker moved, channel offset...) are not reverted.
    /// </summary>
    internal class CmdEditObj : ICommand
    {
        readonly CtrlBase ctrl;
        readonly Dictionary<string, object?> before = new();
        readonly Dictionary<string, object?> after = new();
        public string Description { get; }
        public bool IsEmpty => after.Count == 0;

        // settingsBefore: CaptureSettings() before the editor, the current settings are the edited ones
        public CmdEditObj(CtrlBase ctrl, Dictionary<string, object?> settingsBefore)
        {
            this.ctrl = ctrl;
            foreach (var (name, value) in ctrl.CaptureSettings())
            {
                if (settingsBefore.TryGetValue(name, out var old) && !Equals(old, value))
                {
                    before[name] = old;
                    after[name] = value;
                }
            }
            Description = $"Edit '{ctrl.DataBase.Name}'";
        }
        public void Execute() => ctrl.ApplySettings(after);
        public void Undo() => ctrl.ApplySettings(before);
    }
}
