using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScottPlot;
using ScottPlot.Colormaps;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using System.Threading.Tasks;
using static eScope.Plot.eScopePlot;
using static eScope.Plot.CtrlBase;
using static IronPython.Modules._ast;
using static OpenTK.Graphics.OpenGL.GL;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace eScope.Plot
{
    public class Annotation : DataBase
    {
        public string Channel { get; set; }= string.Empty;
        public double TextLocationX { get; set; }= 0;
        public double TextLocationY { get; set; }= 75;
        public double TipLocationX { get; set; }= 0;
        public double TipLocationY { get; set; }= 25;
        public bool IsVisible { get; set; } = true;
        static int colorIdx = 0;

        public Annotation() 
        {
            Color = ColorRGB.GetNextColor(ref colorIdx);
        }
    }
    public class AnnotationCtrl : CtrlBase<Annotation>
    {
        public Callout L { get; set; }
        private bool IsTextNear = false;
        private bool IsTipNear = false;
        private ChannelCtrl C;
        private Coordinates grabOffset;
        private Pixel lastPixTo = new Pixel(float.NaN, float.NaN);
        public AnnotationCtrl(Annotation data, IPlotContext trc) : base(data, trc)
        {
            XDisplayEnd = 0;
            XTimeEnd = 0;
            
            Item.Name = "menuMarkers" + Data.Name;
            Item.Text = Data.Name;
            Item.Size = new System.Drawing.Size(180, 22);
            Item.Click += new System.EventHandler(EditClick);
            Item.Tag = this;
        }

        private void EditClick(object? sender, EventArgs e)
        {
            ToolStripMenuItem? item = sender as ToolStripMenuItem;
            if (item != null)
            {
                AnnotationCtrl? marker = item.Tag as AnnotationCtrl;
                if (marker != null)
                {
                    marker.Edit();
                }
            }
        }
        public override void Edit()
        {
            RunEditor(new AnnotationEdit(Data, Context));
        }
        public override void Update()
        {
            if (C != null)
            {
                L.TipCoordinates = new Coordinates(Data.TipLocationX + C.SignalBase.XOffset, Data.TipLocationY + C.SignalBase.YOffset * C.SignalBase.YGain);
                L.TextCoordinates = new Coordinates(Data.TextLocationX + C.SignalBase.XOffset, Data.TextLocationY + C.SignalBase.YOffset * C.SignalBase.YGain);
            }
        }
        public override void Configure()
        {
            var  ctrls = Context.GetSignalList();
            C = ctrls.FirstOrDefault(d => d.SignalBase.Name == Data.Channel);
 
            if (C != null)
            {
                L.Axes.XAxis = C.s.Axes.XAxis;
                L.Axes.YAxis = C.s.Axes.YAxis;
            }

            ScottPlot.Color c = ScottPlot.Color.FromHex(Data.Color);
            L.ArrowLineColor = c;
            L.ArrowFillColor = c;
            L.TextBorderColor = c;

            if (Context.DarkMode != 0)
            {
                L.TextColor = ScottPlot.Color.FromHex("#d7d7d7");
            }
            L.TextBackgroundColor = ScottPlot.Color.FromHex(Data.Color + "80"); // add alpha

            L.Text = Data.Name;
            L.TextCoordinates = new Coordinates(Data.TextLocationX, Data.TextLocationY);
            Update();
        }
        public override void Add()
        {
            L = Context.Plot.AddCallout(Data.Name, Data.TextLocationX, Data.TextLocationY, Data.TipLocationX, Data.TipLocationY);
            L.ArrowheadWidth = 1;
            L.ArrowLineWidth = 0;
            L.ArrowWidth = 1;

            Context.Plot.AddPlotObject(this);
            Configure();
        }
        public override void Remove()
        {
            Context.Plot.Remove(L);
            Context.Plot.RemovePlotObject(this);
        }
        public DataPoint GetNearestX(double x)
        {
            return new DataPoint(double.PositiveInfinity, double.PositiveInfinity, -1);
        }
        public ScottPlot.Color GetColor()
        {
            return L.TextColor;
        }
        public override void UpdateLegend(double x)
        {
            Update();
        }
        public override double GetNearestDistance(Pixel p, float maxDistance = 15)
        {
            Coordinates c = Context.Plot.GetCoordinates(p, L.Axes.XAxis, L.Axes.YAxis);
            CoordinateRect r = Context.Plot.GetCoordinateRect(p, (float)maxDistance*2, L.Axes.XAxis, L.Axes.YAxis);
            IsTextNear = L.LastRenderRect.Contains(p);
            IsTipNear  = r.Contains(L.TipCoordinates);

            if (IsTextNear || IsTipNear)
                return maxDistance;
            else
                return double.PositiveInfinity;

        }
        public override void HighLight()
        {
            if (IsTextNear || IsTipNear) Context.Plot.SetCursor(Cursors.SizeAll);
        }
        public override void LowLight()
        {
            Context.Plot.SetCursor(Cursors.Default);
        }
        public override void Move(Pixel pix_to, Pixel pix_from, moveDirection dir)
        {
            if (C == null) return;

            // pix_from is the previous mouse position: when it doesn't follow the last move, a new drag starts.
            // Keep the offset between the grab point and the annotation for the whole drag, so it doesn't jump under the mouse.
            if (pix_from.X != lastPixTo.X || pix_from.Y != lastPixTo.Y)
            {
                Coordinates grab = Context.Plot.GetCoordinates(pix_from, L.Axes.XAxis, L.Axes.YAxis);
                Coordinates anchor = IsTextNear ? L.TextCoordinates : L.TipCoordinates;
                grabOffset = new Coordinates(anchor.X - grab.X, anchor.Y - grab.Y);
            }
            lastPixTo = pix_to;

            Coordinates mouseNow = Context.Plot.GetCoordinates(pix_to, L.Axes.XAxis, L.Axes.YAxis);
            Coordinates pos = new Coordinates(Math.Round(mouseNow.X + grabOffset.X), Math.Round(mouseNow.Y + grabOffset.Y));
            if (IsTextNear)
            {
                L.TextCoordinates = pos;
                Data.TextLocationX = pos.X - C.SignalBase.XOffset;
                Data.TextLocationY = pos.Y - C.SignalBase.YOffset * C.SignalBase.YGain;
            }
            else if (IsTipNear)
            {
                L.TipCoordinates = pos;
                Data.TipLocationX = pos.X - C.SignalBase.XOffset;
                Data.TipLocationY = pos.Y - C.SignalBase.YOffset * C.SignalBase.YGain;
            }
        }
        public void RefreshMousePosition(double x, double y){}
        public override void SetVisibility(bool isVisible)
        {
            Data.IsVisible = isVisible;
        }
        public override bool IsVisible()
        {
            return Data.IsVisible;
        }
    }
}
