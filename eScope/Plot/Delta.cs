using Newtonsoft.Json.Linq;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

using ScottPlot;
using ScottPlot.Colormaps;
using ScottPlot.WinForms;
using Newtonsoft.Json;
using static eScope.Plot.eScopePlot;
using static eScope.Plot.CtrlBase;
using static OpenTK.Graphics.OpenGL.GL;
using System.Runtime.Intrinsics.Arm;

namespace eScope.Plot
{
    public class Delta:DataBase
    {
        public double X1 { get; set; }= 0;
        public double X2 { get; set; }= 0;
        public bool IsVisible { get; set; }= true;
        static int colorIdx = 0;

        public Delta()
        {
            Name = string.Empty;
            Color = ColorRGB.GetNextColor(ref colorIdx);
            X1 = 0;
            X2 = 0;
        }
  
    }
    public class DeltaCtrl : CtrlBase<Delta>
    {
        public HorizontalSpan L { get; set; }
        public Arrow A { get; set; }
        public Text T { get; set; }
        AxisSpanUnderMouse? spanUnderMouse;
        public DeltaCtrl(Delta data, IPlotContext context) : base(data, context)
        {            
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
                DeltaCtrl? m = item.Tag as DeltaCtrl;
                if (m != null)
                {
                    m.Edit();
                }
            }
        }
        public override void Edit()
        {
            RunEditor(new DeltaEdit(Data, Context));
        }
        public override void Configure()
        {
            double D = Math.Round(L.X2 - L.X1);
            ScottPlot.Color c = ScottPlot.Color.FromHex(Data.Color + "40"); // add alpha
            L.FillColor = c;
            L.LineColor = c;
            L.LegendText = Data.Name;
            L.X1 = Data.X1;
            L.X2 = Data.X2;

            A.ArrowLineColor = ScottPlot.Color.FromHex(Data.Color);
            A.LegendText = D.ToString();
            A.Base = new Coordinates(L.X1, A.Base.Y);
            A.Tip = new Coordinates(L.X2, A.Tip.Y);

            T.Location = new Coordinates(A.Base.X, A.Base.Y + 10);

            SetVisibility(Data.IsVisible);
        }
        public override void Add()
        {
            L = Context.Plot.AddHDelta(Data.X1, Data.X2);
            L.LineWidth = 2;
            L.FillColor = ScottPlot.Color.FromHex(Data.Color + "40");
            L.LineColor = ScottPlot.Color.FromHex(Data.Color);
            Coordinates bas = new(Data.X1, 1);
            Coordinates tip = new(Data.X2, 1);
            A = Context.Plot.AddArrow(bas, tip);
            A.ArrowShape = ArrowShape.DoubleLine.GetShape();
            A.ArrowheadWidth = 5;
            A.ArrowheadLength = 5;
            A.ArrowLineColor = L.LineColor;

            double D = Math.Round(L.X2 - L.X1);
            T = Context.Plot.AddText(D.ToString(), 0, 0);
            T.Alignment = Alignment.LowerCenter;
            //T.LabelFontColor = L.LineColor;
            Context.Plot.AddPlotObject(this);
        }
        public override void Remove()
        {
            Context.Plot.RemoveHDelta(L);
            Context.Plot.RemoveArrow(A);
            Context.Plot.RemoveText(T);
            Context.Plot.RemovePlotObject(this);
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
            double D = Math.Round(Max() - Min());
            double Tx = D / 2 + Min();

            L.LegendText = Data.Name;
            T.LabelText = $"{Data.Name} = {D} ms";
            T.Location = new Coordinates(Tx, A.Base.Y);

        }
        public override double GetNearestDistance(Pixel p, float maxDistance = 15)
        {
            CoordinateRect rect = Context.Plot.GetCoordinateRect(p, maxDistance, L.Axes.XAxis, L.Axes.YAxis);

            spanUnderMouse = L.UnderMouse(rect);

            if (spanUnderMouse is not null)
            {
                if (spanUnderMouse == null) Context.Plot.SetCursor(Cursors.SizeWE);
                else if (spanUnderMouse.IsResizingHorizontally) Context.Plot.SetCursor(Cursors.SizeWE);
                else if (spanUnderMouse.IsResizingVertically) Context.Plot.SetCursor(Cursors.SizeNS);
                else if (spanUnderMouse.IsMoving) Context.Plot.SetCursor(Cursors.SizeAll);
                return (double)maxDistance;
            }
            else
                return double.PositiveInfinity;
        }
        public override void HighLight()
        {
            L.LineWidth = 3;
            if (spanUnderMouse == null) Context.Plot.SetCursor(Cursors.SizeWE);
            else if (spanUnderMouse.IsResizingHorizontally) Context.Plot.SetCursor(Cursors.SizeWE);
            else if (spanUnderMouse.IsResizingVertically) Context.Plot.SetCursor(Cursors.SizeNS);
            else if (spanUnderMouse.IsMoving) Context.Plot.SetCursor(Cursors.SizeAll);
        }
        public override void LowLight()
        {
            L.LineWidth = 2;
            Context.Plot.SetCursor(Cursors.Default);
        }
        public override void Move(Pixel pix_to, Pixel pix_from, moveDirection dir)
        {
            // currently dragging something so update it
            Coordinates mouseNow = Context.Plot.GetCoordinates(pix_from);
            spanUnderMouse?.DragTo(mouseNow);

            Data.X1 = L.X1;
            Data.X2 = L.X2;
            A.Base = new Coordinates(L.X1, A.Base.Y);
            A.Tip = new Coordinates(L.X2, A.Tip.Y);
        }
        public void RefreshMousePosition(double x, double y) { }
        public override void SetVisibility(bool isVisible)
        {
            Data.IsVisible = isVisible;
            L.IsVisible = Data.IsVisible;
            A.IsVisible = Data.IsVisible;
            T.IsVisible = Data.IsVisible;
        }
        public override bool IsVisible()
        {
            return Data.IsVisible;
        }
        public double Min()
        {
            return Math.Min(Data.X1, Data.X2);
        }
        public double Max()
        {
            return Math.Max(Data.X1, Data.X2);
        }
        public double Width()
        {
            return Math.Abs(Data.X1 - Data.X2);
        }

    }
}
