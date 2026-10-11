using ScottPlot;
using ScottPlot.Plottables;
using static eScope.Plot.eScopePlot;
using eScope.Command;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Globalization;

namespace eScope.Plot
{
    // read-only view on a channel buffer: a sample not received yet returns 0 and flags the owner ScriptData
    public readonly struct SafeSeries
    {
        private readonly ScriptData owner;
        private readonly string name;
        private readonly List<double>? list;
        public SafeSeries(ScriptData owner, string name, List<double>? l) { this.owner = owner; this.name = name; list = l; }
        public double this[int i]
        {
            get
            {
                if (list == null) { owner.Missing ??= $"unknown channel \"{name}\""; return 0.0; }
                if (i < 0) return 0.0;
                if (i >= list.Count) { owner.Missing ??= $"waiting for \"{name}\" sample {i}"; return 0.0; }
                return list[i];
            }
        }
        public int Count => list?.Count ?? 0;
    }
    // script access by channel name
    public class ScriptData
    {
        private readonly Dictionary<string, List<double>> dict;
        // set when the last evaluation read a sample not available yet: result must be discarded and retried later
        public string? Missing { get; set; }
        public ScriptData(Dictionary<string, List<double>> dict) => this.dict = dict;
        public SafeSeries this[string name] => new SafeSeries(this, name, dict.TryGetValue(name, out var l) ? l : null);
    }
    public class ScriptGlobals
    {
        public ScriptData data;
        public ScriptGlobals(ScriptData data)
        {
            this.data = data;
        }
    }
    // samples [Index, Index + Count[ were not received and hold the previous value
    // Timeout: reception stopped before Index for an unknown time (Count = 0)
    public class LostData
    {
        public int Index { get; set; }
        public int Count { get; set; }
        public bool Timeout { get; set; } = false;
    }
    public class Channel:DataBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public linkHolder linkXOffset { get; set; }
        public linkHolder linkYOffset { get; set; }
        public linkHolder linkYGain { get; set; }
        public double XOffset
        {
            get => linkXOffset.DataRef.Data;
            set => linkXOffset.DataRef.Data = value;
        }
        public double YOffset
        {
            get => linkYOffset.DataRef.Data;
            set => linkYOffset.DataRef.Data = value;
        }
        public double YGain
        {
            get => linkYGain.DataRef.Data;
            set => linkYGain.DataRef.Data = value;
        }
        public int Size { get; set; } = 64;
        public int Shift { get; set; } = 0;
        public string Unit { get; set; } = string.Empty;
        public double XCreation { get; set; } = 0;
        public List<double> X { get; set; } = new List<double>();
        public double UnitRatio { get; set; } = 1;
        public Utils.Display Display { get; set; } = Utils.Display.signedInt;
        public bool IsVisible { get; set; } = true;
        public bool hasDevice { get; set; } = true;
        // period (ms) of a channel without device; a device channel uses the device sampling time
        public double SamplingTime { get; set; } = 2;
        public bool hasFunction { get; set; } = false;
        public string Script { get; set; }
        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public string RuntimeError { get; set; } = string.Empty;
        public List<UInt64> Y { get; set; } = new List<UInt64>(1);
        public string Symbol { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public UInt32 Address { get; set; } = 0;
        public List<LostData> Lost { get; set; } = new List<LostData>();
        public Channel()
        {
            Color = ColorRGB.GetNextSignalColor();
            Size = 16;
            Script = "return Y[i];";

            if (linkXOffset == null)
            {
                linkXOffset = new linkHolder(Id, null);
            }
            if (linkYOffset == null)
            {
                linkYOffset = new linkHolder(Id, null);
            }
            if (linkYGain == null)
            {
                linkYGain = new linkHolder(Id, null);
                YGain = 1000;
            }

        }
        public void InitializationRuntime()
        {
            // set up ID from json if missing
            linkXOffset.Id = Id;
            linkYOffset.Id = Id;
            linkYGain.Id = Id;
        }
    }

    public class ChannelCtrl : CtrlBase
    {
       public Channel SignalBase => (Channel) DataBase;
       protected Utils.Display Display { get; set; }

        public Signal s { get; set; }
        protected Arrow A;
        protected Coordinates ABase;
        protected Coordinates ATip;
        private Func<int, double>? CompiledScript;
        private ScriptData? CompiledScriptData;
        bool bError = false;
        // next sample to evaluate by the script, can lag behind XLastIdxCompute while waiting for another channel's data
        int ScriptIdx = 0;

         public double time = 0;
        public List<double> Y { get; } = new List<double>();
        public DeviceCtrl Dev;
        public uint protocolToken = 0;
        public ChannelCtrl(Channel data, IPlotContext trc) : base(data, trc)
        {
            trc.ScriptGlobalsDict[data.Name] = Y;
 
            Item.Name = "menuChannels" + SignalBase.Name;
            Item.Text = SignalBase.Name;
            Item.Size = new System.Drawing.Size(180, 22);
            Item.Click += new System.EventHandler(EditClick);
            Item.Tag = this;

            // keep our plot rendering in sync whenever a linked channel (or ourself) changes X/Y offset or Y gain
            SignalBase.linkXOffset.DataChanged += (_, _) => ApplyXOffset();
            SignalBase.linkYOffset.DataChanged += (_, _) => ApplyYOffset();
            SignalBase.linkYGain.DataChanged += (_, _) => ApplyYGain();
        }
        private void ApplyXOffset()
        {
            if (s == null) return;
            s.Data.XOffset = SignalBase.XOffset + SignalBase.XCreation;
            UpdateXDisplayEnd();
        }
        private void ApplyYOffset()
        {
            if (s == null) return;
            s.Data.YOffset = SignalBase.YOffset * SignalBase.YGain;
        }
        private void ApplyYGain()
        {
            if (s == null) return;
            s.Axes.YAxis.Max = SignalBase.YGain * Context.Plot.YDivisionNumber / 2;
            s.Axes.YAxis.Min = -SignalBase.YGain * Context.Plot.YDivisionNumber / 2;
            s.Data.YOffset = SignalBase.YOffset * SignalBase.YGain; // offset display scales with gain
        }
        public void ConfigureLink()
        {
            SignalBase.InitializationRuntime();

            var s = Context.GetSignalFromId(SignalBase.linkXOffset.SrcId);
            if (s != null)
            {
                SignalBase.linkXOffset.Link(s.SignalBase.linkXOffset);
            }
            s = Context.GetSignalFromId(SignalBase.linkYOffset.SrcId);
            if (s != null)
            {
                SignalBase.linkYOffset.Link(s.SignalBase.linkYOffset);
            }
            s = Context.GetSignalFromId(SignalBase.linkYGain.SrcId);
            if (s != null)
            {
                SignalBase.linkYGain.Link(s.SignalBase.linkYGain);
            }
        }
        public virtual DataPoint GetNearestX(double x)
        {
            double x_round = Math.Round(x / s.Data.Period) * s.Data.Period;
            int index = (int)(x_round / s.Data.Period);
            double y = double.NaN;

            index = s.Data.GetIndex(x, true);
            if (index < 0)
            {
                return new DataPoint(double.PositiveInfinity, double.PositiveInfinity, -1);
            }

            x = s.Data.GetX(index);
            y = s.Data.GetY(index);

            return new DataPoint(x, y, -1);
        }
        // theme changed: dark colors are lifted in dark mode
        public void ApplyThemeColor()
        {
            if (s == null) return;
            var c = Theme.TraceColor(SignalBase.Color);
            s.Color = c;
            s.LineColor = c;
            if (A != null) A.ArrowLineColor = c;
        }
        public virtual ScottPlot.Color GetColor()
        {
            return s.Color;
        }        
        public virtual string Displayformat(double x)
        {
            string ret;
            bool isPrefix;

            if(SignalBase.Display == Utils.Display.hexa)
            {
                isPrefix = true;
            }
            else
            {
                isPrefix = false;
            }

            int index = s.Data.GetIndex(x, true);
            if (index < 0)
            {
                ret = "NA";
            }
            else
            {
                ret = Utils.DoubleToStr(Y[index], SignalBase.Display, isPrefix);
            }

            return ret;
        }
        // end of the samples a channel without device computes: the device channels it reads by index (its own
        // length must not count, it would keep itself alive after a clear), the display end without any
        double ComputeEnd()
        {
            var fed = Context.GetSignalList().Where(c => c.SignalBase.hasDevice).ToList();
            return fed.Count > 0 ? fed.Max(c => c.XTimeEnd) : Context.Plot.XDisplayEnd;
        }
        public override void Update()
        {
            int lastSample = SignalBase.hasDevice ? 0 : (int)(ComputeEnd() / s.Data.Period);
            int i = 0;
            // buffer may have been cleared / rebuilt since last evaluation
            ScriptIdx = Math.Min(ScriptIdx, XLastIdxCompute);
            if(SignalBase.hasDevice == true)
            {
                for(i = XLastIdxCompute; i < SignalBase.Y.Count; i++)
                {
                    Y.Add(Utils.UInt64ToDouble(SignalBase.Y[i], SignalBase.Size, SignalBase.Shift, Display));
                }
            }
            else
            {
                // from the samples held (a data operation may have removed some), refreshed every tick
                int newSampleCount = Math.Max(0, lastSample - Y.Count);
                if (newSampleCount > 0)
                {
                    Y.AddRange(Enumerable.Repeat(0.0, newSampleCount));
                    // no device feeding SignalBase.X, keep it in sync ourselves so hover-selection and data export still work
                    SyncX();
                }
            }

            if ((bError == false) && (SignalBase.hasFunction == true) && (CompiledScript != null))
            {
                // make sure data is add at current XDisplayEnd
                Context.Plot.UpdateXEnd();
                // device fed: compute every received sample, otherwise up to display end (never past Y)
                int end = SignalBase.hasDevice ? Y.Count : Math.Min(lastSample, Y.Count);
                for (i = ScriptIdx; i < end; i++)
                {
                    try
                    {
                        // execute script
                        CompiledScriptData!.Missing = null;
                        double v = CompiledScript(i);
                        if (CompiledScriptData.Missing != null)
                        {
                            // another channel has not received this sample yet: retry from here next update
                            SignalBase.RuntimeError = CompiledScriptData.Missing;
                            break;
                        }
                        Y[i] = v;
                    }
                    catch (Exception ex)
                    {
                        // stop evaluating until next compile, to avoid one exception per sample every tick
                        Y[i] = 0;
                        bError = true;
                        SignalBase.RuntimeError = $"Runtime error at sample {i}: {ex.InnerException?.Message ?? ex.Message}";
                        Console.WriteLine("=== SCRIPT RUNTIME ERROR ===");
                        Console.WriteLine(SignalBase.RuntimeError);
                        break;
                    }
                }
                if (i >= end && bError == false)
                {
                    SignalBase.RuntimeError = string.Empty;
                }
                ScriptIdx = i;
            }

            if (SignalBase.hasDevice == true)
            {
                XLastIdxCompute = SignalBase.Y.Count;
            }
            else
            {
                XLastIdxCompute = Y.Count;
            }
            if ((SignalBase.hasFunction == false) || (CompiledScript == null))
            {
                ScriptIdx = XLastIdxCompute;
            }
        }

        public static string CompileScript(string name, string script, Dictionary<string, List<double>> scriptGlobalsDict, out Func<int, double>? compiledScript)
        {
            return CompileScript(name, script, scriptGlobalsDict, out compiledScript, out _);
        }
        public static string CompileScript(string name, string script, Dictionary<string, List<double>> scriptGlobalsDict, out Func<int, double>? compiledScript, out ScriptData scriptData)
        {
            var options = ScriptOptions.Default
                .AddReferences(typeof(Math).Assembly)
                .AddImports("System", "System.Collections.Generic");

            scriptData = new ScriptData(scriptGlobalsDict);
            var globals = new ScriptGlobals(scriptData);
            script = $"(int i) => {{ var Y = data[\"{name}\"]; {script}}}";
            string ret = "Function compiled Successfully";

            try
            {
                compiledScript = CSharpScript
                    .EvaluateAsync<Func<int, double>>(script, options, globals)
                    .Result;
            }
            catch (CompilationErrorException ex)
            {
                Console.WriteLine("=== SCRIPT COMPILATION ERROR ===");

                foreach (var diag in ex.Diagnostics)
                {
                    ret = $"{diag.Severity} {diag.Id} line {diag.Location.GetLineSpan().StartLinePosition.Line + 1}: {diag.GetMessage()}";
                    Console.WriteLine(
                        $"{diag.Severity} " +
                        $"{diag.Id}: " +
                        $"{diag.GetMessage()} " +
                        $"(Line {diag.Location.GetLineSpan().StartLinePosition.Line + 1})");
                }
                compiledScript = null;
            }
            catch (Exception ex)
            {
                ret = $"Error: {ex.InnerException?.Message ?? ex.Message}";
                Console.WriteLine("=== SCRIPT COMPILATION ERROR ===");
                Console.WriteLine(ret);
                compiledScript = null;
            }
            return ret;
        }

        public override void UpdateLegend(double x)
        {
            double gain = (s.Axes.YAxis.Max - s.Axes.YAxis.Min) / Context.Plot.YDivisionNumber;
            Legend = $"{Math.Round(gain,1)} {SignalBase.Unit}/DIV\n{Math.Round(s.Data.YOffset, 1)} {SignalBase.Unit} Offset\n{Math.Round(SignalBase.XOffset,1)} ms Offset";
            A.Base = new Coordinates(s.Axes.XAxis.Min + 1000, s.Data.YOffset);
            A.Tip = new Coordinates(s.Axes.XAxis.Min +     0, s.Data.YOffset);
        }
        public override double GetNearestDistance(Pixel p, float maxDistance = 15)
        {
            int pixXMin = (int)( p.X - maxDistance);
            int pixXMax = (int)(p.X + maxDistance);
            double maxDistanceSquared = maxDistance * maxDistance;
            double closestDistanceSquared = double.PositiveInfinity;

            double closestX = double.PositiveInfinity;
            double closestY = double.PositiveInfinity;

            // empty channel data
            if (SignalBase.X.Count == 0)
            {
                return double.PositiveInfinity;
            }

            for (int pixX = pixXMin; pixX < pixXMax; pixX++)
            {
                // 1 get PixY for current pixX
                Coordinates c = Context.Plot.GetCoordinates(p, s.Axes.XAxis, s.Axes.YAxis);
                PixelColumn pc = s.Data.GetPixelColumn(s.Axes, pixX);
                float pixY = pc.Bottom;
                if (p.Y > pc.Top) pixY = pc.Top;
                else if (p.Y < pc.Bottom) pixY = pc.Bottom;
                else { pixY = pc.Bottom; }

                double dX = pixX - p.X;
                double dY = pixY - p.Y;

                double distanceSquared = dX * dX + dY * dY;

                if (distanceSquared < maxDistanceSquared)
                {
                    if (distanceSquared <= closestDistanceSquared)
                    {
                        closestDistanceSquared = distanceSquared;

                        closestX = pixX;
                        closestY = pixY;
                    }
                }
            }
            //Console.WriteLine($"{closestDistanceSquared} {closestX} {p.X} {closestY} {p.Y}");
            return closestDistanceSquared <= maxDistanceSquared
                ? Math.Sqrt(closestDistanceSquared)
                : double.PositiveInfinity; 
            }
        public override void HighLight()
        {
            s.LineWidth = 3;
            A.ArrowheadWidth = 6;
            A.ArrowheadLength = 6;
        }
        public override void LowLight()
        {
            s.LineWidth = 1.5F;
            A.ArrowheadWidth = 5;
            A.ArrowheadLength = 5;
        }
        private void SetYOffset(double yOffset)
        {
            double round = SignalBase.YGain / 20;
            s.Data.YOffset = Math.Round(yOffset / round) * round;
            SignalBase.YOffset = s.Data.YOffset / SignalBase.YGain;
        }
        public override void Move(Pixel pix_to, Pixel pix_from, moveDirection dir)
        {
            Coordinates c_from = Context.Plot.GetCoordinates(pix_from, s.Axes.XAxis, s.Axes.YAxis);
            Coordinates c_to = Context.Plot.GetCoordinates(pix_to, s.Axes.XAxis, s.Axes.YAxis);

            if (dir == moveDirection.DirY)
            {
                SetYOffset(s.Data.YOffset + c_to.Y - c_from.Y);
                  
                Console.WriteLine($"{pix_to.Y}\t{pix_from.Y}\t{c_to.Y}\t{s.Data.YOffset}");

            }
            else if (dir == moveDirection.DirX)
            {
                s.Data.XOffset = s.Data.XOffset + c_to.X - c_from.X;
                SignalBase.XOffset = s.Data.XOffset;
                Console.WriteLine($"{pix_to.X}\t{pix_from.X}\t{c_to.X}\t{s.Data.XOffset}");
                UpdateXDisplayEnd();
            }
        }
        public override void YZoomIn(Pixel mousePixel, bool centerZero = false)
        {
            // anchor on the current offset (where raw value 0 sits) so it doesn't jump when zooming without a real mouse position
            double m1 = centerZero ? 0 : Context.Plot.GetCoordinates(mousePixel, s.Axes.XAxis, s.Axes.YAxis).Y - s.Data.YOffset;
            double z = 2; // zoom

            SignalBase.YGain = SignalBase.YGain / z;
            if (SignalBase.YGain == 0)
            {
                SignalBase.YGain = 1;
            }
            s.Axes.YAxis.Max = SignalBase.YGain * Context.Plot.YDivisionNumber / 2;
            s.Axes.YAxis.Min = -SignalBase.YGain * Context.Plot.YDivisionNumber / 2;
            SetYOffset(m1 * (1 / z - 1) + s.Data.YOffset);
            Console.WriteLine($"YZoomInt Min{s.Axes.YAxis.Min} MAX{s.Axes.YAxis.Max}");
        }
        public override void YZoomOut(Pixel mousePixel, bool centerZero = false)
        {
            double z = 2; // zoom
            double m2 = centerZero ? 0 : Context.Plot.GetCoordinates(mousePixel, s.Axes.XAxis, s.Axes.YAxis).Y - s.Data.YOffset;

            SignalBase.YGain = SignalBase.YGain * z;

            s.Axes.YAxis.Max = SignalBase.YGain * Context.Plot.YDivisionNumber / 2;
            s.Axes.YAxis.Min = -SignalBase.YGain * Context.Plot.YDivisionNumber / 2;
            SetYOffset(s.Data.YOffset = m2 * (z - 1) + s.Data.YOffset);
            Console.WriteLine($"YZoomOut Min{s.Axes.YAxis.Min} MAX{s.Axes.YAxis.Max}");
        }
        public virtual void RefreshMousePosition(double x, double y){}

        public virtual void ResetYOffset()
        {
            SignalBase.YOffset = 0;
            s.Data.YOffset = 0;
        }
        public virtual void ResetXOffset()
        {
            SignalBase.XOffset = 0;
            s.Data.XOffset = 0;
        }
        public override void SetVisibility(bool isVisible)
        {
            SignalBase.IsVisible = isVisible;
            s.IsVisible = isVisible;
            A.IsVisible = isVisible;
        }
        public override bool IsVisible()
        {
            return SignalBase.IsVisible;
        }
        public virtual void UpdateXDisplayEnd()
        {
            XDisplayEnd = SignalBase.XCreation + SignalBase.XOffset + SignalBase.X.Count * s.Data.Period;
            XTimeEnd = SignalBase.XCreation + SignalBase.X.Count * s.Data.Period;
        }
        private void EditClick(object? sender, EventArgs e)
        {
            ToolStripMenuItem? item = sender as ToolStripMenuItem;
            if (item != null)
            {
                ChannelCtrl? chan = item.Tag as ChannelCtrl;
                if (chan != null)
                {
                    chan.Edit();
                }
            }
        }
        public override void Edit()
        {
            var before = CaptureSettings();
            if (new ChannelEdit(SignalBase, Context).ShowDialog() != DialogResult.OK)
                return;
            var edit = new CmdEditObj(this, before);
            if (edit.IsEmpty)
            {
                Configure(); // nothing to undo (links are applied by the editor at once)
                return;
            }
            string oldName = (string)before[nameof(Channel.Name)]!;
            string newName = SignalBase.Name;
            var cmd = new CompositeCommand(edit.Description);
            cmd.Add(edit);
            // renamed: propose to rename it in the scripts reading it, in the same undo step
            string oldRef = $"data[\"{oldName}\"]";
            var readers = Context.GetSignalList().Where(c => c != this && c.SignalBase.Script.Contains(oldRef)).ToList();
            if (oldName != newName && readers.Count > 0
                && MessageBox.Show($"{string.Join(", ", readers.Select(c => c.SignalBase.Name))}: script reading '{oldName}'.\nRename it to '{newName}' in the script?",
                                   "Rename channel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                foreach (var c in readers)
                {
                    var b = c.CaptureSettings();
                    c.SignalBase.Script = c.SignalBase.Script.Replace(oldRef, $"data[\"{newName}\"]");
                    cmd.Add(new CmdEditObj(c, b));
                }
            }
            commandMgr.ExecuteCommand(cmd);
        }
        // set by the data operations / at run time, not by the editor
        protected override bool IsSetting(string property)
            => property is not (nameof(Channel.Id) or nameof(Channel.XCreation) or nameof(Channel.RuntimeError));
        public DeviceCtrl? GetDevice(string DeviceName)
        {
            foreach (DeviceCtrl ctrl in Context.Scope.Devices)
            {
                if (DeviceName == ctrl.Data.Name)
                {
                    return ctrl;
                }
            }
            return null;
        }

        public override void Configure()
        {
            // a channel without device keeps its symbol but is not fed by the device
            DeviceCtrl? dev = SignalBase.hasDevice ? GetDevice(SignalBase.Device) : null;

            Remove();
            s = Context.Plot.AddSignal(Y);

            Dev = dev;

            if(Dev != null)
                s.Data.Period = Dev.Data.SamplingTime;
            else
                s.Data.Period = SignalBase.SamplingTime > 0 ? SignalBase.SamplingTime : 2;
            s.LineWidth = 2;
            s.Color = Theme.TraceColor(SignalBase.Color);
            s.LineColor = Theme.TraceColor(SignalBase.Color);
            s.MarkerLineWidth = 2;
            s.MarkerSize = 3;
            s.Axes.YAxis.IsVisible = false;
            s.Axes.YAxis.Max = SignalBase.YGain * Context.Plot.YDivisionNumber / 2/* - SignalBase.YOffset * SignalBase.YGain*/;
            s.Axes.YAxis.Min = -SignalBase.YGain * Context.Plot.YDivisionNumber / 2/* - SignalBase.YOffset * SignalBase.YGain*/;
            s.LegendText = SignalBase.Name;
            s.Data.YOffset = SignalBase.YOffset * SignalBase.YGain;

            XTimeEnd = Context.Plot.XTimeEnd;
            if (SignalBase.Y.Count == 0)
            {
                SignalBase.XCreation = 0;
            }
            s.Data.XOffset = SignalBase.XOffset + SignalBase.XCreation; // plot with disply offset & xcreation


            UpdateXDisplayEnd();

            Coordinates bas = new Coordinates(s.Axes.XAxis.Min, 0);
            Coordinates tip = new Coordinates(s.Axes.XAxis.Min + 10, 0);
            A = Context.Plot.AddArrow(bas, tip);
            A.ArrowShape = ArrowShape.ArrowheadLine.GetShape();
            A.ArrowheadWidth = 5;
            A.ArrowheadLength = 5;
            A.Axes.XAxis = s.Axes.XAxis;
            A.Axes.YAxis = s.Axes.YAxis;
            ABase = new Coordinates(s.Axes.XAxis.Min + 1000, s.Data.YOffset);
            ATip = new Coordinates(s.Axes.XAxis.Min, s.Data.YOffset);
            A.Base = ABase;
            A.Tip = ATip;
            A.ArrowLineColor = Theme.TraceColor(SignalBase.Color);

            SetVisibility(SignalBase.IsVisible);
            if (Display != SignalBase.Display)
            {
                Display = SignalBase.Display;
                // Parallel.ForEach(Enumerable.Range(0, Y.Count), index =>
                // {
                //     Y[index] = Utils.UInt64ToDouble(SignalBase.Y[index], SignalBase.Size, SignalBase.Shift, Display);
                // });
            }

            Legend = string.Empty;
            UpdateLegend(0);

            // scripts read the channels by name: follow a rename (editor, undo / redo)
            var dict = Context.ScriptGlobalsDict;
            foreach (var oldName in dict.Where(kv => kv.Value == Y && kv.Key != SignalBase.Name).Select(kv => kv.Key).ToList())
            {
                dict.Remove(oldName);
            }
            dict[SignalBase.Name] = Y;
            Item.Name = "menuChannels" + SignalBase.Name;
            Item.Text = SignalBase.Name;

            string ret = CompileScript(SignalBase.Name, SignalBase.Script, Context.ScriptGlobalsDict, out CompiledScript, out CompiledScriptData);

            SignalBase.RuntimeError = string.Empty;
            if(ret.Contains("Successfull"))
            {
                bError = false;
                Y.Clear();
                if (!SignalBase.hasDevice)
                    SignalBase.X.Clear(); // rebuilt with Y by Update(), with the (possibly new) period
                XLastIdxCompute = 0;
            }
            else
            {
                bError = true;
            }
            Context.UpdateMarkers();
            Context.Plot.AddPlotObject(this);

            Dev?.AddChannel(this);

            Update();
        }
        public override void Add()
        {
            Configure();
        }
        public override void Remove()
        {
            DeviceCtrl? dev = GetDevice(SignalBase.Device);
            dev?.RemoveChannel(this);

            if(s != null)
            {
                Context.Plot.RemoveSignal(s);
                Context.Plot.RemovePlotObject(this);
            }
            if (A != null)
            {
                Context.Plot.RemoveArrow(A);
            }
        }
        public void Clear()
        {
            SignalBase.Y.Clear();
            SignalBase.X.Clear();
            SignalBase.Lost.Clear();
            Y.Clear();
            SignalBase.XCreation = 0;
            s.Data.XOffset = SignalBase.XOffset;
            XTimeEnd = 0;
            XDisplayEnd = 0;
            XLastIdxCompute = 0;
        }

        // called by the device once per frame, missing values are already completed by the device
        public void AddSample(UInt64 y)
        {
            double period = s.Data.Period;
            if (SignalBase.Y.Count == 0)
            {
                // channel started while the trace is running: hold its first value back to the trace time
                Context.Plot.UpdateXEnd();
                int missing = (int)Math.Round((Context.Plot.XTimeEnd - XTimeEnd) / period);
                for (int k = 0; k < missing; k++)
                {
                    SignalBase.X.Add(SignalBase.X.Count * period);
                    SignalBase.Y.Add(y);
                }
            }
            SignalBase.X.Add(SignalBase.X.Count * period);
            SignalBase.Y.Add(y);
            UpdateXDisplayEnd();
        }
        // samples not received: hold the last value to keep the time base, and remember the gap
        public void AddLost(int count)
        {
            if (count <= 0 || SignalBase.Y.Count == 0) return;

            var lost = SignalBase.Lost;
            if (lost.Count != 0 && lost[^1].Timeout == false && lost[^1].Index + lost[^1].Count == SignalBase.Y.Count)
                lost[^1].Count += count; // contiguous with the previous gap
            else
                lost.Add(new LostData { Index = SignalBase.Y.Count, Count = count });

            UInt64 y = SignalBase.Y[^1];
            for (int k = 0; k < count; k++)
            {
                SignalBase.X.Add(SignalBase.X.Count * s.Data.Period);
                SignalBase.Y.Add(y);
            }
            UpdateXDisplayEnd();
        }
        public void AddTimeout()
        {
            if (SignalBase.Y.Count == 0) return;

            // the timeout repeats while the target is silent: one mark per interruption
            var lost = SignalBase.Lost;
            if (lost.Count != 0 && lost[^1].Timeout && lost[^1].Index == SignalBase.Y.Count) return;
            lost.Add(new LostData { Index = SignalBase.Y.Count, Timeout = true });
        }
        // keep lost gaps on their samples when samples are removed / inserted at idx
        public void ShiftLost(int idx, int removed, int inserted)
        {
            var lost = SignalBase.Lost;
            for (int i = lost.Count - 1; i >= 0; i--)
            {
                var l = lost[i];
                int end = l.Index + l.Count;
                if (removed > 0 && l.Timeout && l.Index > idx && l.Index < idx + removed)
                {
                    lost.RemoveAt(i); // samples around the interruption removed
                    continue;
                }
                if (removed > 0 && end > idx && l.Index < idx + removed)
                {
                    // trim the part of the gap that was removed
                    int keepBefore = Math.Max(0, idx - l.Index);
                    int keepAfter = Math.Max(0, end - (idx + removed));
                    l.Count = keepBefore + keepAfter;
                    l.Index = Math.Min(l.Index, idx);
                    if (l.Count == 0) { lost.RemoveAt(i); continue; }
                }
                else if (l.Index >= idx + removed)
                {
                    l.Index -= removed;
                }
                if (inserted > 0 && l.Index >= idx)
                {
                    l.Index += inserted;
                }
            }
        }
        public void Start()
        {
            DeviceCtrl? dev = GetDevice(SignalBase.Device);

            dev?.Start();
        }
        public void Stop()
        {
            DeviceCtrl? dev = GetDevice(SignalBase.Device);

            dev?.Stop();
        }
        // Samples held by the channel: raw data if fed by a device, else the locally computed display data
        public int SampleCount => SignalBase.hasDevice ? SignalBase.Y.Count : Y.Count;

        // Keep X at one entry per sample, then refresh what depends on the signal length
        public void SyncX()
        {
            var x = SignalBase.X;
            if (x.Count > SampleCount)
                x.RemoveRange(SampleCount, x.Count - SampleCount);
            else if (x.Count < SampleCount)
                x.AddRange(Enumerable.Range(x.Count, SampleCount - x.Count).Select(i => i * s.Data.Period));

            s.Data.XOffset = SignalBase.XOffset + SignalBase.XCreation;
            UpdateXDisplayEnd();
        }
        // Rebuild display data from raw samples (conversion + function), device channels only
        public void RecomputeFromRaw()
        {
            if (SignalBase.hasDevice == false) return;
            Y.Clear();
            XLastIdxCompute = 0;
            Update();
        }
        // Device channel: edits raw samples only, call RecomputeFromRaw() afterwards to refresh Y
        public void RemoveRange(int idx, int count)
        {
            var list = SignalBase.hasDevice ? (System.Collections.IList)SignalBase.Y : Y;
            idx = Math.Clamp(idx, 0, list.Count);
            count = Math.Clamp(count, 0, list.Count - idx);

            if (SignalBase.hasDevice)
                SignalBase.Y.RemoveRange(idx, count);
            else
                Y.RemoveRange(idx, count);
            ShiftLost(idx, count, 0);
            SyncX();
        }
        // data: displayed values, raw: device samples (null = derived from data)
        // Device channel: edits raw samples only, call RecomputeFromRaw() afterwards to refresh Y
        public void InsertRange(int idx, List<double> data, List<UInt64>? raw)
        {
            if (SignalBase.hasDevice)
            {
                raw ??= data.Select(d => Utils.DoubleToUInt64(d, SignalBase.Size, SignalBase.Shift, SignalBase.Display)).ToList();
                idx = Math.Clamp(idx, 0, SignalBase.Y.Count);
                SignalBase.Y.InsertRange(idx, raw);
            }
            else
            {
                idx = Math.Clamp(idx, 0, Y.Count);
                Y.InsertRange(idx, data);
            }
            ShiftLost(idx, 0, data.Count);
            SyncX();
        }

        public ICommand CmdDeleteData(double xStart, double xWidth)
        {
            return new ChannelCmdDeleteData(this, xStart, xWidth);
        }
        // yStr: displayed value (after function), rawStr: device sample (empty without device)
        public List<(string xStr, string yStr, string rawStr)> Filter(double xStart, double xWidth)
        {
            // sample i is plotted at XCreation + XOffset + i * Period
            double period = s.Data.Period;
            double t0 = SignalBase.XCreation + SignalBase.XOffset;
            var result = new List<(string xStr, string yStr, string rawStr)>();
            for (int i = 0; i < Y.Count; i++)
            {
                double t = t0 + i * period;
                if (t < xStart) continue;
                if (t > xStart + xWidth) break;
                string raw = (SignalBase.hasDevice && i < SignalBase.Y.Count) ? SignalBase.Y[i].ToString(CultureInfo.InvariantCulture) : string.Empty;
                result.Add(((i * period).ToString(), Y[i].ToString(), raw));
            }
            return result;
        }
        public double Period => s.Data.Period;
        // displayed values (after function) of the samples lying in [xStart, xStart + xWidth]
        public List<double> FilterValues(double xStart, double xWidth)
        {
            double period = s.Data.Period;
            double t0 = SignalBase.XCreation + SignalBase.XOffset;
            var result = new List<double>();
            for (int i = 0; i < Y.Count; i++)
            {
                double t = t0 + i * period;
                if (t < xStart) continue;
                if (t > xStart + xWidth) break;
                result.Add(Y[i]);
            }
            return result;
        }
    }
}
