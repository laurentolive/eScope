using System.Globalization;
using eScope.UI;
using ScottPlot;

namespace eScope.Plot
{
    // One channel of the legend: a row of the side panel. The mouse on the row acts on the channel in the plot
    // (drag: offset, wheel: zoom, double-click: edit), as on the curve itself.
    public class PlotLegendItem : IDisposable
    {
        public readonly ChannelRow Row;
        public bool IsMouseOver = false;
        public ChannelCtrl obj;
        readonly TraceCtrl Trc;

        public PlotLegendItem(ChannelCtrl o, TraceCtrl trc)
        {
            Trc = trc;
            obj = o;
            Row = new ChannelRow();
            Row.MouseEnter += (_, _) => IsMouseOver = true;
            Row.MouseLeave += (_, _) => IsMouseOver = false;
            Row.MouseDown += Row_MouseDown;
            Row.MouseUp += (_, e) =>
            {
                Row.Capture = false;
                Trc.Plot.ForwardMouseUp(Row, e);
            };
            Row.MouseMove += (_, e) => Trc.Plot.ForwardMouseMove(Row, e);
            Row.MouseWheel += (_, e) => Trc.Plot.ForwardMouseWheel(Row, e);
            Row.MouseDoubleClick += (_, _) => Trc.Edit(obj);
            Row.ContextMenuStrip = Trc.ContextMenu;
            Trc.Side.AddChannelRow(Row);
        }
        private void Row_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // keep receiving move/up events even once the cursor leaves the row
                Row.Capture = true;
            }
            Trc.Plot.ForwardMouseDown(Row, e);
            Trc.Plot.updateMenuRightClick();
        }

        public void Update()
        {
            var s = obj.SignalBase;
            var color = obj.GetColor();
            string unit = string.IsNullOrEmpty(s.Unit) ? string.Empty : " " + s.Unit;
            double watchX = Trc.Plot.watchLine.Data.Pos;
            string value = obj.s != null ? obj.Displayformat(watchX) : "NA";
            if (value != "NA") value += unit;

            double gain = obj.s != null ? (obj.s.Axes.YAxis.Max - obj.s.Axes.YAxis.Min) / Trc.Plot.YDivisionNumber : s.YGain;
            double yOffset = obj.s?.Data.YOffset ?? 0;
            var ic = CultureInfo.InvariantCulture;
            string scale = $"{Math.Round(gain, 3).ToString(ic)}{unit}/div · offset {Math.Round(yOffset, 2).ToString(ic)}{unit}";
            if (Math.Round(s.XOffset, 1) != 0) scale += $" · {Math.Round(s.XOffset, 1).ToString(ic)} ms";

            Row.SetData(s.Name, value, scale, System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B), !obj.IsVisible());
        }
        public void HighLight() => Row.Highlight = true;
        public void LowLight() => Row.Highlight = false;
        public bool IsOver(Pixel p) => IsMouseOver;
        public void SetVisibilty(bool isVisible) => Row.Visible = isVisible;
        public void Dispose()
        {
            Trc.Side.RemoveChannelRow(Row);
            Row.Dispose();
        }
    }

    public class PlotLegend
    {
        public eScopePlot pl;
        public List<PlotLegendItem> Items = new List<PlotLegendItem>();
        readonly TraceCtrl Trc;
        public bool IsMouseOver => Items.Any(item => item.IsMouseOver);
        public PlotLegend(eScopePlot Pl, TraceCtrl trc)
        {
            pl = Pl;
            Trc = trc;
        }
        public void Add(ChannelCtrl o)
        {
            Items.Add(new PlotLegendItem(o, Trc));
        }
        public void Remove(ChannelCtrl o)
        {
            var item = Items.FirstOrDefault(i => i.obj == o);
            if (item == null) return;
            Items.Remove(item);
            item.Dispose();
        }
        // "2 s/div": time per division, shown in the toolbar
        public string getXGainLegend()
        {
            double timebase = pl.Trc.GetXGrid();
            string unit = "ms";
            if (timebase >= 1000)
            {
                timebase /= 1000;
                unit = "s";
            }
            return $"{timebase.ToString(CultureInfo.InvariantCulture)} {unit}/div";
        }
        public void Update()
        {
            foreach (PlotLegendItem item in Items)
                item.Update();
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
