using ScottPlot;
using ScottPlot.Plottables;
using static eScope.Plot.eScopePlot;

namespace eScope.Plot
{
    public class Marker: DataBase
    {
        public double Pos { get; set; }
        public double FixedPos { get; set; }
        public int Fixed { get; set; }

        public bool IsVisible { get; set; } = true;
        static int colorIdx = 0;

        public Marker()
        {
            Name = string.Empty;
            Color = ColorRGB.GetNextColor(ref colorIdx);
            Pos = 0;
            FixedPos = 50; // percent
            Fixed = 0;
        }       
    }

    public class MarkerAnnotation : IDisposable
    {
        public Signal S { get; set; }
        public ScottPlot.Plottables.Marker M { get; set; }
        public ScottPlot.Plottables.Text T { get; set; }
        public ChannelCtrl C { get; set; }
        public bool isVisibleAsked = true;
        IPlotContext Context;
        public MarkerAnnotation(Marker data, ChannelCtrl c, IPlotContext context)
        {
            Context = context;
            ScottPlot.Color cColor = c.GetColor(); // signal color with transparency
            ScottPlot.Color mColor = ScottPlot.Color.FromHex(data.Color); // marker color
            C = c;
            S = c.s;
            M = Context.Plot.AddMarker(0, 0);
            M.MarkerColor = cColor;
            M.Axes = S.Axes;
            T = Context.Plot.AddText("", 0, 0);
            T.LabelBackgroundColor = ScottPlot.Colors.White;
            T.LabelBackgroundColor = cColor;
            T.LabelBorderColor = cColor;
            T.LabelFontColor = ScottPlot.Colors.White;
            T.LabelAlignment = Alignment.MiddleCenter;
            T.LabelPadding = 2;
            T.Axes = S.Axes;
        }
        public void Dispose()
        {
            Context.Plot.RemoveMarker(M);
            Context.Plot.RemoveText(T);
        }
        public void SetVisibility(bool isVisible)
        {
            isVisibleAsked = isVisible;
            T.IsVisible = isVisible;
            M.IsVisible = isVisible;
        }
    }

    public class MarkerCtrl : CtrlBase<Marker>
    {
        public VerticalLine L { get; set; }
        private List<MarkerAnnotation> Annotations = new List<MarkerAnnotation>();
        public MarkerCtrl(Marker data, IPlotContext context) : base(data, context)
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
                MarkerCtrl? marker = item.Tag as MarkerCtrl;
                if (marker != null)
                {
                    marker.Edit();
                }
            }
        }
        public override void Edit()
        {
            RunEditor(new MarkerEdit(Data, Context));
        }

        public override void Configure()
        {
            L.Color = ScottPlot.Color.FromHex(Data.Color);
            L.LineColor = ScottPlot.Color.FromHex(Data.Color);
            L.LabelBackgroundColor= ScottPlot.Color.FromHex(Data.Color);

            L.LegendText = Data.Name;

            if (Data.Fixed != 0)
            {
                SetFixedPos();
            }
            else
            {
                L.Position = Data.Pos;
            }
            SetVisibility(Data.IsVisible);
            Update();
        }
        public override void Add()
        {
            L = Context.Plot.AddVLine(Data.Pos);
            L.LineWidth = 1;
            L.LabelBold = false;

            Configure();

            if (Data.Name != "Watch")
            {
                Context.Plot.AddPlotObject(this);
            }

        }
        public override void Remove()
        {
            Context.Plot.RemoveVLine(L);
            if (Data.Name != "Watch")
            { 
                Context.Plot.RemovePlotObject(this);
            }

            foreach (MarkerAnnotation a in Annotations)
            {
                a.Dispose();
            }
        }
        public override void Update()
        {
            foreach (MarkerAnnotation a in Annotations)
            {
                a.Dispose();
            }
            Annotations.Clear();
            // Create enough circle marker to mark each signal
            var signals = Context.GetSignalList();
            foreach (ChannelCtrl c in signals)
            {
                MarkerAnnotation A = new MarkerAnnotation(Data, c, Context);
                Annotations.Add(A);
                A.SetVisibility(c.IsVisible());
            }
        }
        public DataPoint GetNearestX(double x)
        {
            return new DataPoint(double.PositiveInfinity, double.PositiveInfinity, -1);
        }
        public ScottPlot.Color GetColor()
        {
            return L.LineColor;
        }           
        public override void UpdateLegend(double x)
        {
            if (Data.Fixed != 0)
            {
                SetFixedPos();
            }
            L.LabelText = Data.Name + " " + Math.Round(Data.Pos).ToString();
            foreach (MarkerAnnotation a in Annotations)
            {
                DataPoint d = a.C.GetNearestX(Data.Pos);
                Coordinates c = d.Coordinates;
                Pixel p;

                if(d.IsReal == false)
                {
                    a.T.IsVisible = false;
                    a.M.IsVisible = false;
                    continue;
                }
                else if(a.isVisibleAsked)
                {
                    a.T.IsVisible = true;
                    a.M.IsVisible = true;
                }

                double y = c.Y;
                c.Y += a.C.SignalBase.YOffset * a.C.SignalBase.YGain;
                a.M.Location = c;

                // label manage out of screen on Y
                if (c.Y > a.T.Axes.YAxis.Max)
                {
                    c.Y = a.T.Axes.YAxis.Max;
                    p = a.T.Axes.GetPixel(c);
                    p.X += 5;
                    p.Y -= 5; // cancel offset
                }
                else if (c.Y < a.T.Axes.YAxis.Min)
                {
                    c.Y = a.T.Axes.YAxis.Min;
                    p = a.T.Axes.GetPixel(c);
                    p.X += 5;
                    p.Y += 5; // cancel offset
                }
                else
                {
                    p = a.T.Axes.GetPixel(c);
                    p.X += 5;                    
                }
                if (c.Y > (a.T.Axes.YAxis.Min + (a.T.Axes.YAxis.Max - a.T.Axes.YAxis.Min) / 2))
                {
                    a.T.Alignment = Alignment.UpperLeft;
                    p.Y += 5;
                }
                else
                {
                    a.T.Alignment = Alignment.LowerLeft;
                    p.Y -= 5;
                }

                // label manage out of screen on X
                if ((p.X + a.T.LabelLastRenderPixelRect.Width - 1) >= Context.Plot.fp.Size.Width)
                {
                    p.X = Context.Plot.fp.Size.Width - a.T.LabelLastRenderPixelRect.Width - 1; // cancel offset
                }

                c = a.T.Axes.GetCoordinates(p);
                a.T.LabelText = a.C.Displayformat(d.X);
                a.T.Location = c;
            }
        }
        public override double GetNearestDistance(Pixel p, float maxDistance = 15)
        {
            float pixelMin = L.Axes.GetPixelX(L.X) - (float)maxDistance;
            float pixelMax = L.Axes.GetPixelX(L.X) + (float)maxDistance;
            double distance = Math.Abs(L.Axes.GetPixelX(L.X)  - p.X);
            if(distance > maxDistance)
            {
                distance = double.PositiveInfinity;
            }

            return distance;
        }

        public override void HighLight()
        {
            L.LineWidth = 2;
            Context.Plot.SetCursor(Cursors.SizeWE);
        }
        public override void LowLight()
        {
            L.LineWidth = 1;
            Context.Plot.SetCursor(Cursors.Default);
        }
        private void SetDataPosition(double x)
        {
            L.X = Math.Round(x);
            Data.Pos = L.X;
            Data.FixedPos = (L.X - L.Axes.XAxis.Min) / (L.Axes.XAxis.Max - L.Axes.XAxis.Min) * 100;
        }
        public override void Move(Pixel pix_to, Pixel pix_from, moveDirection dir)
        {
            Coordinates c_to = Context.Plot.GetCoordinates(pix_to, L.Axes.XAxis, L.Axes.YAxis);
            SetDataPosition(c_to.X);
        }
        public void SetFixedPos()
        {
            L.Position = Math.Round(L.Axes.XAxis.Min + (L.Axes.XAxis.Max - L.Axes.XAxis.Min) * Data.FixedPos / 100);
            Data.Pos = L.Position;
        }
        public override void ZoomIn()
        {
            if (Data.Fixed != 0)
            {
                SetFixedPos();
            }
        }
        public override void ZoomOut()
        {
            if (Data.Fixed != 0)
            {
                SetFixedPos();
            }
        }

        public void RefreshMousePosition(double x, double y)
        {
        }
        public void SetChannelVisibility(ChannelCtrl c, bool isVisible)
        {
            foreach (MarkerAnnotation a in Annotations)
            {
                if(c == a.C)
                {
                    a.SetVisibility(isVisible);
                }
            }
        }
        public override void SetVisibility(bool isVisible)
        {
            Data.IsVisible = isVisible;
            L.IsVisible = isVisible;
            foreach (MarkerAnnotation a in Annotations)
            {
                a.SetVisibility(isVisible);
            }

        }
        public override bool IsVisible()
        {
            return Data.IsVisible;
        }
    }
}
