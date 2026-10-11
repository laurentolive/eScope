using ScottPlot;
using ScottPlot.Plottables;
using static eScope.Plot.eScopePlot;

namespace eScope.Plot
{
    // horizontal marker on a data value of one channel: follows the channel Y offset/gain
    public class DataMarker : DataBase
    {
        public string Channel { get; set; } = string.Empty;
        public double Value { get; set; } // channel value, without the channel display offset
        public double FixedPos { get; set; }
        public int Fixed { get; set; }
        public bool IsVisible { get; set; } = true;
        static int colorIdx = 0;

        public DataMarker()
        {
            Color = ColorRGB.GetNextColor(ref colorIdx);
            Value = 0;
            FixedPos = 50; // percent
            Fixed = 0;
        }
    }

    public class DataMarkerCtrl : CtrlBase<DataMarker>
    {
        public HorizontalLine L { get; set; }
        public ScottPlot.Plottables.Text T { get; set; } // Y value, on the left of the data area
        private ChannelCtrl? C;
        public DataMarkerCtrl(DataMarker data, IPlotContext context) : base(data, context)
        {
            XDisplayEnd = 0;
            XTimeEnd = 0;

            Item.Name = "menuMarkers" + data.Name;
            Item.Text = data.Name;
            Item.Size = new System.Drawing.Size(180, 22);
            Item.Click += new System.EventHandler(EditClick);
            Item.Tag = this;
        }
        private void EditClick(object? sender, EventArgs e)
        {
            ToolStripMenuItem? item = sender as ToolStripMenuItem;
            if (item != null)
            {
                DataMarkerCtrl? marker = item.Tag as DataMarkerCtrl;
                if (marker != null)
                {
                    marker.Edit();
                }
            }
        }
        public override void Edit()
        {
            RunEditor(new DataMarkerEdit(Data, Context));
        }
        // display offset of the associated channel, in plot Y units
        private double ChannelOffset()
        {
            return (C == null) ? 0 : C.SignalBase.YOffset * C.SignalBase.YGain;
        }
        private double Round(double v)
        {
            double round = (C == null) ? 1 : C.SignalBase.YGain / 100;
            if (round <= 0) return v;
            return Math.Round(v / round) * round;
        }
        public override void Configure()
        {
            C = Context.GetSignalFromName(Data.Channel);
            if (C != null)
            {
                L.Axes.XAxis = C.s.Axes.XAxis;
                L.Axes.YAxis = C.s.Axes.YAxis;
                T.Axes.XAxis = C.s.Axes.XAxis;
                T.Axes.YAxis = C.s.Axes.YAxis;
            }

            L.Color = ScottPlot.Color.FromHex(Data.Color);
            L.LineColor = ScottPlot.Color.FromHex(Data.Color);
            L.LabelBackgroundColor = ScottPlot.Color.FromHex(Data.Color);
            T.LabelBackgroundColor = ScottPlot.Color.FromHex(Data.Color);
            T.LabelBorderColor = ScottPlot.Color.FromHex(Data.Color);
            L.LegendText = Data.Name;

            Update();
        }
        // re-place the line from the channel offset: called on every refresh, so it follows the channel
        public override void Update()
        {
            if (C == null)
            {
                L.IsVisible = false;
                T.IsVisible = false;
                return;
            }
            if (Data.Fixed != 0)
            {
                SetFixedPos();
            }
            L.Position = Data.Value + ChannelOffset();
            L.IsVisible = Data.IsVisible && C.IsVisible();
            T.IsVisible = L.IsVisible;

            // keep the value on screen when the line is out of the Y range
            double y = Math.Clamp(L.Position, L.Axes.YAxis.Min, L.Axes.YAxis.Max);
            T.Location = new Coordinates(L.Axes.XAxis.Min, y);
            T.LabelText = Math.Round(Data.Value, 3).ToString() + " " + C.SignalBase.Unit;
        }
        public override void Add()
        {
            L = Context.Plot.AddHLine(Data.Value);
            L.LineWidth = 1;
            L.LabelBold = false;
            L.LabelOppositeAxis = true; // name on the right, value on the left

            T = Context.Plot.AddText("", 0, Data.Value);
            T.LabelFontColor = ScottPlot.Colors.White;
            T.LabelAlignment = Alignment.MiddleLeft;
            T.LabelPadding = 2;

            Context.Plot.AddPlotObject(this);
            Configure();
        }
        public override void Remove()
        {
            Context.Plot.RemoveHLine(L);
            Context.Plot.RemoveText(T);
            Context.Plot.RemovePlotObject(this);
        }
        // associated channel deleted: its Y axis leaves the plot, detach from it
        public void ChannelRemoved(ChannelCtrl c)
        {
            if (C != c) return;
            C = null;
            L.Axes.YAxis = Context.Plot.GetFormsPlot().Plot.Axes.Left;
            L.Axes.XAxis = Context.Plot.GetFormsPlot().Plot.Axes.Bottom;
            T.Axes.YAxis = L.Axes.YAxis;
            T.Axes.XAxis = L.Axes.XAxis;
            L.IsVisible = false;
            T.IsVisible = false;
        }
        public ScottPlot.Color GetColor()
        {
            return L.LineColor;
        }
        public override void UpdateLegend(double x)
        {
            Update();
            L.LabelText = Data.Name;
        }
        public override double GetNearestDistance(Pixel p, float maxDistance = 15)
        {
            if (C == null || !L.IsVisible)
            {
                return double.PositiveInfinity;
            }
            double distance = Math.Abs(L.Axes.GetPixelY(L.Y) - p.Y);
            if (distance > maxDistance)
            {
                distance = double.PositiveInfinity;
            }
            return distance;
        }
        public override void HighLight()
        {
            L.LineWidth = 2;
            Context.Plot.SetCursor(Cursors.SizeNS);
        }
        public override void LowLight()
        {
            L.LineWidth = 1;
            Context.Plot.SetCursor(Cursors.Default);
        }
        public override void Move(Pixel pix_to, Pixel pix_from, moveDirection dir)
        {
            if (C == null) return;
            Coordinates c_to = Context.Plot.GetCoordinates(pix_to, L.Axes.XAxis, L.Axes.YAxis);
            Data.Value = Round(c_to.Y - ChannelOffset());
            // FixedPos first: Update() recomputes the value from it when fixed
            Data.FixedPos = (Data.Value + ChannelOffset() - L.Axes.YAxis.Min) / (L.Axes.YAxis.Max - L.Axes.YAxis.Min) * 100;
            Update();
        }
        // fixed: stays at the same screen height, the value follows what is under it
        public void SetFixedPos()
        {
            double y = L.Axes.YAxis.Min + (L.Axes.YAxis.Max - L.Axes.YAxis.Min) * Data.FixedPos / 100;
            Data.Value = Round(y - ChannelOffset());
        }
        public override void SetVisibility(bool isVisible)
        {
            Data.IsVisible = isVisible;
            Update();
        }
        public override bool IsVisible()
        {
            return Data.IsVisible;
        }
    }
}
