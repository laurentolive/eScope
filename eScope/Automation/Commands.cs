using eScope.Command;
using eScope.Plot;

namespace eScope.Automation
{
    // Commands of the automation API: undone by the user like any other (Ctrl+Z), labelled "AI: ..."
    internal class AiCommand : ICommand
    {
        readonly ICommand inner;
        public string Description { get; }

        public AiCommand(ICommand inner, string? description = null)
        {
            this.inner = inner;
            Description = "AI: " + (description ?? inner.Description);
        }

        public void Execute() => inner.Execute();
        public void Undo() => inner.Undo();
    }

    // Channel settings changed by configure_channel, restored on undo
    internal class CmdConfigureChannel : ICommand
    {
        internal record State(
            string Name, string Color, string Unit, bool IsVisible, Utils.Display Display, int Size,
            double YGain, double YOffset, double XOffset, bool HasFunction, string Script, double SamplingTime);

        readonly TraceCtrl trc;
        readonly ChannelCtrl ctrl;
        readonly State before;
        readonly State after;
        public string Description { get; }

        public CmdConfigureChannel(TraceCtrl trc, ChannelCtrl ctrl, State after)
        {
            this.trc = trc;
            this.ctrl = ctrl;
            before = Capture(ctrl.SignalBase);
            this.after = after;
            Description = $"Configure '{before.Name}'";
        }

        public static State Capture(Channel c) => new(
            c.Name, c.Color, c.Unit, c.IsVisible, c.Display, c.Size,
            c.YGain, c.YOffset, c.XOffset, c.hasFunction, c.Script, c.SamplingTime);

        public void Execute() => Apply(after);
        public void Undo() => Apply(before);

        void Apply(State s)
        {
            var c = ctrl.SignalBase;
            if (c.Name != s.Name)
            {
                // scripts read the channels by name
                trc.ScriptGlobalsDict.Remove(c.Name);
                trc.ScriptGlobalsDict[s.Name] = ctrl.Y;
                c.Name = s.Name;
            }
            c.Color = s.Color;
            c.Unit = s.Unit;
            c.IsVisible = s.IsVisible;
            c.Display = s.Display;
            c.Size = s.Size;
            c.YGain = s.YGain; // gain first: the plotted Y offset scales with it
            c.YOffset = s.YOffset;
            c.XOffset = s.XOffset;
            c.hasFunction = s.HasFunction;
            c.Script = s.Script;
            c.SamplingTime = s.SamplingTime;
            // display values computed again from the raw samples (format, size or function changed)
            ctrl.Configure();
            ctrl.RecomputeFromRaw();
            trc.Plot.RefreshReq = true;
        }
    }
}
