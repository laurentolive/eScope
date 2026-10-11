using eScope.Command;
using System;
using System.Collections.Generic;
using System.Linq;

namespace eScope.Plot
{
    /// <summary>
    /// Removes [xStart, xStart+xWidth] from one channel.
    /// Y (display) and SignalBase.Y (raw) hold the samples; SignalBase.X only needs one entry per sample.
    /// </summary>
    internal class ChannelCmdDeleteData:ICommand
    {
        ChannelCtrl C;
        int Index;              // first sample removed
        int Count;              // number of samples removed
        double XOffsetAdjust = 0; // selection starts before the signal: signal offset moves back by this much
        List<UInt64> YRawRemoved = new();
        List<double> YDisplayRemoved = new();
        List<LostData> LostBefore = new();
        public string Description { get { return "Delete data"; } }

        int SampleCount => C.SampleCount;

        public ChannelCmdDeleteData(ChannelCtrl c, double xStart, double xWidth) 
        {
            C = c;
            double period = C.Period; // device sampling time, or the channel's own without device

            double t0 = C.SignalBase.XOffset + C.SignalBase.XCreation;
            Index = Math.Clamp((int)Math.Ceiling((xStart - t0) / period), 0, SampleCount);

            // same samples as CopyData: those plotted inside [xStart, xStart + xWidth]
            int endIdx = (int)Math.Floor((xStart + xWidth - t0) / period);
            Count = Math.Clamp(endIdx - Index + 1, 0, SampleCount - Index);

            if (xStart < t0)
            {
                XOffsetAdjust = t0 - xStart;
            }
        }

        void SyncAfterChange()
        {
            C.SyncX(); // plot-wide end (ZoomFit) is derived from it
            C.XLastIdxCompute = C.Y.Count;
        }
        public void Execute()
        {
            C.SignalBase.XOffset -= XOffsetAdjust;

            // a list can be shorter than Count (raw Y is empty without device)
            int nDisplay = Math.Clamp(C.Y.Count - Index, 0, Count);
            YDisplayRemoved = C.Y.GetRange(Index, nDisplay);
            C.Y.RemoveRange(Index, nDisplay);

            int nRaw = Math.Clamp(C.SignalBase.Y.Count - Index, 0, Count);
            YRawRemoved = C.SignalBase.Y.GetRange(Index, nRaw);
            C.SignalBase.Y.RemoveRange(Index, nRaw);

            LostBefore = C.SignalBase.Lost.Select(l => new LostData { Index = l.Index, Count = l.Count, Timeout = l.Timeout }).ToList();
            C.ShiftLost(Index, Count, 0);

            SyncAfterChange();
        }
        public void Undo()
        {
            C.SignalBase.Y.InsertRange(Index, YRawRemoved);
            C.Y.InsertRange(Index, YDisplayRemoved);
            C.SignalBase.XOffset += XOffsetAdjust;
            C.SignalBase.Lost = LostBefore;

            SyncAfterChange();
        }
    }

    /// <summary>
    /// X/Y offsets and Y gain of every channel of a trace, captured before and after a view change
    /// (move, Y zoom, reset). Linked channels share their values, so restoring them all keeps links consistent.
    /// </summary>
    internal class ChannelCmdView : ICommand
    {
        record struct State(double XOffset, double YOffset, double YGain);

        readonly TraceCtrl Trc;
        readonly List<Channel> Channels;
        readonly List<State> Before;
        List<State> After = new();
        public string Description { get; }

        public ChannelCmdView(TraceCtrl trc, string description)
        {
            Trc = trc;
            Description = description;
            Channels = trc.GetSignalList().Select(c => c.SignalBase).ToList();
            Before = Capture();
        }
        List<State> Capture() => Channels.Select(c => new State(c.XOffset, c.YOffset, c.YGain)).ToList();

        // takes the current values as the "after" state, false when nothing changed
        public bool Commit()
        {
            After = Capture();
            return !After.SequenceEqual(Before);
        }
        void Apply(List<State> states)
        {
            for (int i = 0; i < Channels.Count; i++)
            {
                // gain first: the plotted Y offset scales with it
                Channels[i].YGain = states[i].YGain;
                Channels[i].YOffset = states[i].YOffset;
                Channels[i].XOffset = states[i].XOffset;
            }
            foreach (CtrlBase m in Trc.Ctrls)
            {
                if (m is AnnotationCtrl or DataMarkerCtrl)
                {
                    m.Update();
                }
            }
            Trc.Plot.RefreshReq = true;
        }
        public void Execute() => Apply(After);
        public void Undo() => Apply(Before);
    }
}
