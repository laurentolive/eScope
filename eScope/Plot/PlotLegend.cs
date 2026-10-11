using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using System.Data;

using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.Colormaps;
using ScottPlot.WinForms;
using ScottPlot.TickGenerators.TimeUnits;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Threading.Channels;
using ScottPlot.TickGenerators;
using System.Drawing;
using System.ComponentModel.Design;
using ScottPlot.Control;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using ScottPlot.PathStrategies;
using ScottPlot.DataSources;
using System.Reflection.Emit;
using static IronPython.Modules._ast;

namespace eScope.Plot
{
    public class PlotLegendItem : IDisposable
    {
        public System.Windows.Forms.Label labelTitle;
        public System.Windows.Forms.Label labelInfo;
        public bool IsMouseOver = false;
        public ChannelCtrl obj;
        TraceCtrl Trc;
        System.Drawing.Color Color;
        public int borderWidth = 2;
        public int lineHeight = 18;
        public int linePaddingHeight = 6;
        public PlotLegendItem(ChannelCtrl o, int width, TraceCtrl trc)
        {
            Trc = trc;
            obj = o;

            labelTitle = new System.Windows.Forms.Label();
            label_Setup(labelTitle, o.SignalBase.Name, width, linePaddingHeight + lineHeight, ContentAlignment.MiddleLeft, false);

            labelInfo = new System.Windows.Forms.Label();
            label_Setup(labelInfo, o.Legend, width, linePaddingHeight + lineHeight * 3, ContentAlignment.MiddleRight, true);
        }
        private void label_Setup(System.Windows.Forms.Label lbl, string text, int width, int height, ContentAlignment a, bool isBgWhite)
        {
            int argb = (int)obj.GetColor().ARGB;
            var c = System.Drawing.Color.FromArgb(argb);

            lbl.AutoSize = false;
            lbl.Location = new Point(50, 50);
            lbl.Name = text;
            lbl.Text = text;
            lbl.TextAlign = a;
            lbl.Size = new Size(width, height);
            lbl.BorderStyle = BorderStyle.None;
            lbl.DataContext = this;
            if(isBgWhite)
            {
                lbl.BackColor = System.Drawing.Color.White;
            }
            else
            {
                lbl.Font = new Font(lbl.Font, System.Drawing.FontStyle.Bold);
                lbl.ForeColor = System.Drawing.Color.White;
                lbl.BackColor = c;
            }
            lbl.MouseEnter += label_MouseEnter;
            lbl.MouseLeave += label_MouseLeave;
            lbl.MouseDown += label_MouseDown;
            lbl.MouseUp += label_MouseUp;
            lbl.MouseMove += label_MouseMove;
            lbl.MouseWheel += label_MouseWheel;
            lbl.MouseDoubleClick += label_MouseDoubleClick;
            lbl.Paint += label_Paint;
            Trc.tabPage.Controls.Add(lbl);
            lbl.BringToFront();

        }
        public void label_Update(System.Windows.Forms.Label lbl, string text, int x, int y, int width)
        {
            if (obj.IsVisible())
            {
                if(lbl.BackColor != System.Drawing.Color.White)
                {
                    int argb = (int)obj.GetColor().ARGB;
                    var c = System.Drawing.Color.FromArgb(argb);

                    if (Color != c)
                    {
                        Color = c;
                        lbl.BackColor = c;
                        lbl.Refresh();
                    }
                }
            }
            else
            {
                if (lbl.BackColor != System.Drawing.Color.White)
                {
                    var c = System.Drawing.Color.Gray;
                    if (lbl.BackColor != c)
                    {
                        Color = c;
                        lbl.BackColor = c;
                        lbl.Refresh();
                    }
                }
            }


            lbl.Location = new Point(x, y);
            lbl.Text = text;
            lbl.Width = width;
            y += lbl.Height + 1;

        }
        private void label_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                // Define the border color and width
                System.Drawing.Color borderColor = Color;

                using (Pen pen = new Pen(borderColor, borderWidth))
                {
                    // Adjust the rectangle to account for the border width
                    System.Drawing.Rectangle rect = label.ClientRectangle;
                    //rect.Inflate(-borderWidth / 2, -borderWidth / 2);
                    rect.X += borderWidth/2;
                    rect.Y += borderWidth/2;
                    rect.Width -= borderWidth;
                    rect.Height -= borderWidth;
                    // Draw the border
                    e.Graphics.DrawRectangle(pen, rect);
                }
            }
        }
        private void label_MouseEnter(object? sender, EventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                PlotLegendItem? item = (PlotLegendItem?)label.DataContext;
                if (item != null)
                {
                    item.IsMouseOver = true;
                }
            }
        }
        private void label_MouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                if (e.Button == MouseButtons.Left)
                {
                    // keep receiving move/up events even once the cursor leaves this small label
                    label.Capture = true;
                }
                Trc.Plot.ForwardMouseDown(label, e);
                Trc.Plot.updateMenuRightClick();
            }
        }
        private void label_MouseUp(object? sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                label.Capture = false;
                Trc.Plot.ForwardMouseUp(label, e);
            }
        }
        private void label_MouseMove(object? sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                Trc.Plot.ForwardMouseMove(label, e);
            }
        }
        private void label_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                Trc.Plot.ForwardMouseWheel(label, e);
            }
        }

        private void label_MouseLeave(object? sender, EventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                PlotLegendItem? item = (PlotLegendItem?)label.DataContext;
                if (item != null)
                {
                    item.IsMouseOver = false;
                }
            }
        }
        private void label_MouseDoubleClick(object? sender, EventArgs e)
        {
            if (sender is System.Windows.Forms.Label label)
            {
                PlotLegendItem? item = (PlotLegendItem?)label.DataContext;
                if (item != null)
                {
                    Trc.Edit(item.obj);
                }
            }
        }
        public void HighLight()
        {
            borderWidth = 2;
            labelTitle.Refresh();
            labelInfo.Refresh();

            //            txt.LabelBackgroundColor = obj.GetColor().WithLightness(0.8f);
        }
        public void LowLight()
        {
            borderWidth = 1;
            labelTitle.Refresh();
            labelInfo.Refresh();
            //            txt.LabelBackgroundColor = obj.GetColor().WithLightness(.7f);
        }
        public bool IsOver(Pixel p)
        {
            if (IsMouseOver)
            {
                return true;
            }
            {
                return false;
            }
        }
        public void SetVisibilty(bool isVisible)
        {
            labelTitle.Visible = isVisible;
            labelInfo.Visible = isVisible;
        }
        public void Dispose()
        {
            Trc.tabPage.Controls.Remove(labelTitle);
            Trc.tabPage.Controls.Remove(labelInfo);
        }
    }
    public class PlotLegend
    {
        public eScopePlot pl;
        public System.Windows.Forms.Label XGainTxt;
        public List<PlotLegendItem> Items = new List<PlotLegendItem>();
        TraceCtrl Trc;
        public int X = 0;
        public int Y = 0;
        public int Width = 100;
        public int Height = 100;
        private bool IsVisible = true;
        private int borderWidth = 2;
        public bool IsMouseOver => Items.Any(item => item.IsMouseOver);
        public PlotLegend(eScopePlot Pl, int width, int height, TraceCtrl trc)
        {
            pl = Pl;
            Trc = trc;
            Width = width;
            Height = height;
            XGainTxt = new System.Windows.Forms.Label();
            XGainTxt.AutoSize = false;
            XGainTxt.Location = new Point(50, 50);
            XGainTxt.TextAlign = ContentAlignment.MiddleRight;
            XGainTxt.Name = "Timebase";
            XGainTxt.Size = new Size(Width, 30);
            XGainTxt.TabIndex = 0;
            XGainTxt.Text = "Timebase";
            XGainTxt.BackColor = System.Drawing.Color.Gainsboro;
            Trc.tabPage.Controls.Add(XGainTxt);
            XGainTxt.BringToFront();

        }
        public void Add(ChannelCtrl o)
        {
            PlotLegendItem item = new(o, Width, Trc);
            Items.Add(item);
        }
        public void Remove(ChannelCtrl o)
        {
            foreach (PlotLegendItem item in Items)
            {
                if (item.obj == o)
                {
                    Items.Remove(item);
                    item.Dispose();
                    break;
                }
            }
        }
        public string getXGainLegend()
        {
            double timebase = pl.Trc.GetXGrid();
            string unit = "ms";
            if(timebase >= 1000)
            {
                timebase /= 1000;
                unit = "s";
            }
            return $"{timebase} {unit}/DIV";
        }
        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;
            Update();
        }
        public void Update()
        {
            int y = X;
            int x = Y;
            XGainTxt.Location = new Point(x,Height-90- XGainTxt.Height);
            XGainTxt.Text = getXGainLegend();

            foreach (PlotLegendItem item in Items)
            {
                item.label_Update(item.labelTitle, item.obj.SignalBase.Name, x, y, Width);
                y += item.labelTitle.Height - borderWidth;
                item.label_Update(item.labelInfo, item.obj.Legend, x, y, Width);
                y += item.labelInfo.Height + borderWidth;
            }
        }
        public void SetVisibilty(bool isVisible)
        {
            IsVisible = isVisible;
            foreach (PlotLegendItem item in Items)
            {
                item.SetVisibilty(IsVisible);
            }
        }
        public ChannelCtrl? GetSelectable(Pixel p)
        {
            ChannelCtrl? o = null;
            foreach (PlotLegendItem item in Items)
            {
                if (item.IsOver(p))
                {
                    item.HighLight();
                    o = item.obj;
                }
                else
                {
                    item.LowLight();
                }
            }
            return o;
        }
    }
}