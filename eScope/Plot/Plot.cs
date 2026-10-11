using eScope.Command;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using ScottPlot.WinForms;
using System.Data;
using System.Globalization;

namespace eScope.Plot
{
    public class eScopePlot
    {
        public enum moveDirection
        {
            None,
            DirUnk,
            DirX,
            DirY,
        }

        public FormsPlot fp;
        readonly public TraceCtrl Trc;
        private CtrlBase? SelectableObject;
        public MarkerCtrl watchLine;
        public DeltaCtrl dataSelector;
        private bool watchFollow = true;
        private moveDirection moveDir;
        private Pixel movePixelFrom;
        private long moveGlobalBaseTime;
        private long moveXDynamic = 3000000;
        internal bool RefreshReq = false;
        private ChannelCmdView? pendingViewCmd; // channel offsets before the drag in progress
        private bool ZoomSelectionInProgress = false;
        /*        private double Trc.Data.XPos = 0;*/
        public double XPos_ = 0; //animation
/*        private double Trc.Data.XWidth = 2000;*/
/*        private double Trc.Data.XGridWidth = 10;*/
        private double XWidth_ = 2000; //animation
        public double XDisplayEnd = 0;
        public double XTimeEnd = 0;
        public double XTimeEndAtLastRefresh = 0;
        private double YPos = 0;
        public double YWidth = 100;
        private bool bRool = false; // is plot is Playing at the data end 
        private bool bRun = false; // is plot is acquiring data
        public Pixel mousePixel;
        public Coordinates mouseCoordinates;
        public int YDivisionNumber = 8;
        private DateTime LastRefreshTime;
        private double wheelDelta = 0;
        private double wheelCtrlDelta = 0;
        private double wheelSensibility = 240;
        public PlotLegend signalLegend;
        public eScopePlot(TraceCtrl trc)
        {
            Trc = trc;
            fp = new FormsPlot() { Dock = DockStyle.Fill };
            mousePixel = new(Trc.Data.XWidth / 2, YWidth / 2);
            fp.Plot.HideLegend();
            if (trc.DarkMode != 0)
            {
                fp.Plot.Add.Palette = new ScottPlot.Palettes.Penumbra();
                // change figure colors
                fp.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#181818");
                fp.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#1f1f1f");

                // change axis and grid colors
                fp.Plot.Axes.Color(ScottPlot.Color.FromHex("#d7d7d7"));
                fp.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#404040");
                // change legend colors
                fp.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#404040");
                fp.Plot.Legend.FontColor = ScottPlot.Color.FromHex("#d7d7d7");
                fp.Plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#d7d7d7");
            }
            else
            {
                fp.Plot.Add.Palette = new ScottPlot.Palettes.Nord();
            }
            trc.Plot = this;
            AddLostIndicator();
            Marker data = new();
            data.Name = "Watch";
            watchLine = new(data, trc);
            watchLine.Add();
            watchLine.Configure();

            Delta delta = new();
            delta.Name = "Select";
            dataSelector = new(delta, trc);
            dataSelector.Add();
            dataSelector.Configure();
            dataSelector.SetVisibility(false);

            SelectableObject = null;
            XPos_ = Trc.Data.XPos;
            XWidth_ = Trc.Data.XWidth;


            UpdateXLimit();
            SetXTickGenerator();

            fp.Plot.Axes.SetLimitsY(YPos, YPos + YWidth);
            NumericFixedInterval tickGenY = new NumericFixedInterval(YWidth / YDivisionNumber);

            fp.Plot.Axes.Left.TickGenerator = tickGenY;
            fp.Plot.Axes.Left.TickLabelStyle.IsVisible = false;
            fp.Plot.Axes.Left.IsVisible = false;
            fp.Plot.Axes.Right.IsVisible = false;
            fp.Plot.Axes.Top.IsVisible = false;

            fp.Interaction.IsEnabled = false;
            fp.UserInputProcessor.IsEnabled = false;

            fp.Location = new Point(Trc.Data.LegendWidth + SplitterWidth, 0);
            fp.Width = Trc.Data.LegendWidth;
            fp.Height = 100;
            fp.MouseMove += MouseMove;
            Trc.tabPage.MouseDown += MouseDownUpdateMenu;
            fp.MouseUp += MouseUp;
            fp.MouseDown += MouseDown;

            fp.MouseWheel += MouseWheel;
            fp.DoubleClick += DoubleClick;
            fp.MouseEnter += MouseEnter;
            fp.MouseLeave += MouseLeave;

            fp.Dock = DockStyle.None;
            fp.Refresh();
            signalLegend = new PlotLegend(this, Trc.Data.LegendWidth, fp.Height, Trc);

            // vertical bar between legend and plot, dragged to resize the legend width
            legendSplitter = new Panel();
            legendSplitter.Cursor = Cursors.VSplit;
            legendSplitter.Location = new Point(Trc.Data.LegendWidth, 0);
            legendSplitter.Size = new Size(SplitterWidth, fp.Height);
            legendSplitter.MouseDown += LegendSplitter_MouseDown;
            legendSplitter.MouseMove += LegendSplitter_MouseMove;
            legendSplitter.MouseUp += LegendSplitter_MouseUp;
            Trc.tabPage.Controls.Add(legendSplitter);
        }
        // lost data indicator: small vertical bars at the bottom of the plot, on a fixed hidden axis so Y pan / zoom don't move them
        private readonly List<double> lostX = new();
        private readonly List<double> lostY = new();
        private readonly List<string> lostLabel = new();
        private Scatter? lostIndicator;
        private Text? lostTooltip;
        private const double LostIndicatorY = 0.02;
        private const float LostTooltipDistance = 6; // px
        private void AddLostIndicator()
        {
            IYAxis axis = fp.Plot.Axes.AddLeftAxis();
            axis.IsVisible = false;
            axis.Min = 0;
            axis.Max = 1;

            lostIndicator = fp.Plot.Add.Scatter(lostX, lostY);
            lostIndicator.Axes.YAxis = axis;
            lostIndicator.Axes.XAxis = fp.Plot.Axes.Bottom;
            lostIndicator.LineWidth = 0;
            lostIndicator.MarkerShape = MarkerShape.VerticalBar;
            lostIndicator.MarkerSize = 7;
            lostIndicator.Color = ScottPlot.Color.FromHex("#E03030");
            lostIndicator.IsVisible = false;

            lostTooltip = fp.Plot.Add.Text("", 0, LostIndicatorY);
            lostTooltip.Axes.YAxis = axis;
            lostTooltip.Axes.XAxis = fp.Plot.Axes.Bottom;
            lostTooltip.LabelFontColor = ScottPlot.Colors.White;
            lostTooltip.LabelBackgroundColor = ScottPlot.Color.FromHex("#E03030");
            lostTooltip.LabelBorderColor = ScottPlot.Color.FromHex("#E03030");
            lostTooltip.LabelAlignment = Alignment.LowerLeft;
            lostTooltip.LabelOffsetX = 6;
            lostTooltip.LabelOffsetY = -6;
            lostTooltip.IsVisible = false;
        }
        private void UpdateLostIndicator()
        {
            if (lostIndicator == null) return;

            // one bar per gap start, channels of the same device share the same gaps
            var gaps = new SortedDictionary<double, (int count, bool timeout)>();
            foreach (ChannelCtrl c in Trc.GetSignalList())
            {
                if (c.SignalBase.IsVisible == false) continue;
                double t0 = c.SignalBase.XCreation + c.SignalBase.XOffset;
                foreach (var l in c.SignalBase.Lost)
                {
                    double t = t0 + l.Index * c.Period;
                    gaps.TryGetValue(t, out var g);
                    gaps[t] = (Math.Max(g.count, l.Count), g.timeout || l.Timeout);
                }
            }
            lostX.Clear();
            lostY.Clear();
            lostLabel.Clear();
            foreach (var (t, g) in gaps)
            {
                lostX.Add(t);
                lostY.Add(LostIndicatorY);
                lostLabel.Add(g.timeout ? "Timeout: reception interrupted" : $"{g.count} sample(s) lost");
            }
            lostIndicator.IsVisible = gaps.Count != 0;

            // hover: nearest bar under the mouse
            int nearest = -1;
            float best = LostTooltipDistance;
            for (int i = 0; i < lostX.Count; i++)
            {
                Pixel p = lostIndicator.Axes.GetPixel(new Coordinates(lostX[i], LostIndicatorY));
                float d = Math.Max(Math.Abs(p.X - mousePixel.X), Math.Abs(p.Y - mousePixel.Y));
                if (d <= best)
                {
                    best = d;
                    nearest = i;
                }
            }
            if (lostTooltip != null)
            {
                lostTooltip.IsVisible = nearest >= 0;
                if (nearest >= 0)
                {
                    lostTooltip.LabelText = lostLabel[nearest];
                    lostTooltip.Location = new Coordinates(lostX[nearest], LostIndicatorY);
                }
            }
        }
        private const int SplitterWidth = 4;
        private const int LegendMinWidth = 60;
        private const int PlotMinWidth = 200;
        private Panel legendSplitter;
        private bool legendSplitterDragging = false;
        private int lastWidth = 0;
        private int lastHeight = 0;
        public void Resize(int width, int height)
        {
            lastWidth = width;
            lastHeight = height;
            fp.Location = new Point(Trc.Data.LegendWidth + SplitterWidth, 0);
            fp.Width = width - Trc.Data.LegendWidth - SplitterWidth - 20; //don't why - 20
            fp.Height = height - 90;
            legendSplitter.Location = new Point(Trc.Data.LegendWidth, 0);
            legendSplitter.Height = fp.Height;
            signalLegend.Resize(Trc.Data.LegendWidth, height);
        }
        private void LegendSplitter_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                legendSplitterDragging = true;
                legendSplitter.Capture = true;
            }
        }
        private void LegendSplitter_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!legendSplitterDragging) return;
            // e.X is relative to the splitter, which sits at x = LegendWidth
            int width = Trc.Data.LegendWidth + e.X;
            int max = Math.Max(LegendMinWidth, lastWidth - PlotMinWidth);
            width = Math.Clamp(width, LegendMinWidth, max);
            if (width == Trc.Data.LegendWidth) return;
            Trc.Data.LegendWidth = width; // persisted with the workspace
            Resize(lastWidth, lastHeight);
            fp.Refresh();
        }
        private void LegendSplitter_MouseUp(object? sender, MouseEventArgs e)
        {
            legendSplitterDragging = false;
            legendSplitter.Capture = false;
        }
        public Control GetControl()
        {
            return fp;
        }
        public FormsPlot GetFormsPlot()
        {
            return fp;
        }

        public void AddPlotObject(CtrlBase o)
        {
            if(o is ChannelCtrl)
            {
                signalLegend.Add((ChannelCtrl)o);
                watchLine.Update();
                foreach (CtrlBase m in Trc.Ctrls)
                {
                    if(m is MarkerCtrl)
                    {
                        m.Update();
                    }
                    else if (m is DataMarkerCtrl)
                    {
                        m.Configure(); // associated channel may be back (undo delete)
                    }
                }
            }
            else if(o.DataBase.Name != "Watch")
            {
                SelectableObject = o;
            }
        }
        // AddPlotObject selects the added object: objects re-added by an undo must not stay selected (they would follow the mouse)
        public void ClearSelection()
        {
            SelectableObject?.LowLight();
            SelectableObject = null;
        }
        public void RemovePlotObject(CtrlBase o)
        {
            if (o == SelectableObject)
            {
                SelectableObject = null;
            }
            
            if (o is ChannelCtrl)
            {
                signalLegend.Remove((ChannelCtrl)o);

                watchLine.Update();
                foreach (CtrlBase m in Trc.Ctrls)
                {
                    if (m is MarkerCtrl)
                    {
                        m.Update();
                    }
                    else if (m is DataMarkerCtrl dm)
                    {
                        dm.ChannelRemoved((ChannelCtrl)o);
                    }
                }
            }
        }

        public Signal AddSignal(List<double> Y)
        {
            Signal s = fp.Plot.Add.Signal(Y);
            s.Axes.YAxis = fp.Plot.Axes.AddLeftAxis();
            s.Axes.XAxis = fp.Plot.Axes.Bottom;
            return s;
        }
        public void RemoveSignal(Signal s)
        {
            fp.Plot.Remove(s.Axes.YAxis);
            fp.Plot.Remove(s);
        }
        public ScottPlot.Plottables.Marker AddMarker(double x, double y)
        {
            ScottPlot.Plottables.Marker m = fp.Plot.Add.Marker(x, y);
            m.Size = 6;
            m.Shape = MarkerShape.FilledCircle;
            return m;
        }
        public void RemoveMarker(ScottPlot.Plottables.Marker m)
        {
            fp.Plot.Remove(m);
        }
        public VerticalLine AddVLine(double x)
        {
            VerticalLine l = fp.Plot.Add.VerticalLine(x);
            l.Axes.YAxis = fp.Plot.Axes.Left;
            l.Axes.XAxis = fp.Plot.Axes.Bottom;
            return l;
        }
        public HorizontalSpan AddHDelta(double x1, double x2)
        {
            HorizontalSpan l = fp.Plot.Add.HorizontalSpan(x1, x2);
            l.IsDraggable = true;
            l.IsResizable = true;

            return l;
        }
        public Callout AddCallout(string text, double textX, double textY, double tipX, double tipY)
        {
            return fp.Plot.Add.Callout(text, textX, textY, tipX, tipY);
        }
        public Arrow AddArrow(Coordinates bas, Coordinates tip)
        {
            Arrow a = fp.Plot.Add.Arrow(bas, tip);
            return a;
        }
        public void RemoveArrow(Arrow a)
        {
            fp.Plot.Remove(a);
        }
        public void RemoveVLine(VerticalLine l)
        {
            fp.Plot.Remove(l);
        }
        public HorizontalLine AddHLine(double y)
        {
            HorizontalLine l = fp.Plot.Add.HorizontalLine(y);
            l.Axes.YAxis = fp.Plot.Axes.Left;
            l.Axes.XAxis = fp.Plot.Axes.Bottom;
            return l;
        }
        public void RemoveHLine(HorizontalLine l)
        {
            fp.Plot.Remove(l);
        }
        public void RemoveHDelta(HorizontalSpan l)
        {
            fp.Plot.Remove(l);
        }
        public void Remove(IPlottable p)
        {
            fp.Plot.Remove(p);
        }

        public Text AddText(string t, double x, double y)
        {
            Coordinates c = new Coordinates(x, y);
            return fp.Plot.Add.Text(t, c);
        }
        public void RemoveText(ScottPlot.Plottables.Text t)
        {
            fp.Plot.Remove(t);
        }
        Arrow AddArrow(double x1, double y1, double x2, double y2)
        {
            CoordinateLine l = new CoordinateLine(x1, y1, x2, y2);
            return fp.Plot.Add.Arrow(l);
        }
        CtrlBase? GetNearestVisibleObject()
        {
            double distanceMin = double.PositiveInfinity;
            CtrlBase? nearObject = null;
            // look for first close object found
            foreach (CtrlBase o in Trc.Ctrls)
            {
                if (o.IsVisible())
                {
                    double d = o.GetNearestDistance(mousePixel, 10);
                    if (d < distanceMin)
                    {
                        distanceMin = d;
                        nearObject = o;
                    }
                }
            }
            return nearObject;
        }
        void selectObject(CtrlBase? newSelectedObject)
        {
            // selection changed ?
            if ((newSelectedObject != SelectableObject))
            {
                // current selected => deselected
                if (SelectableObject != null)
                {
                    SelectableObject.LowLight();
                    //Console.WriteLine($"{SelectableObject.Label} not selectable");
                }

                SelectableObject = newSelectedObject;

                // new object selected ?
                if (newSelectedObject != null)
                {
                    SelectableObject = newSelectedObject;
                    SelectableObject.HighLight();
                    RefreshReq = true;
                    //Console.WriteLine($"{SelectableObject.Label} selectable");
                }
            }
        }
        public void Refresh()
        {
            TimeSpan elapsedTicks = DateTime.Now - LastRefreshTime;
            moveDirection md = moveDirection.None;

            if (moveDir == moveDirection.None)
            {
                CtrlBase? newSelectedObject = null;

                // check contextual menu for selectable object
                newSelectedObject = signalLegend.GetSelectable(mousePixel);

                // nothing found?
                if (newSelectedObject == null)
                {
                    newSelectedObject = GetNearestVisibleObject();
                }
                selectObject(newSelectedObject);
            }

            if (moveDir == moveDirection.DirUnk)
            {
                float deltaX = mousePixel.X - movePixelFrom.X;
                float deltaY = mousePixel.Y - movePixelFrom.Y;

                //Console.WriteLine($"{deltaX}\t{deltaY}");
                if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
                {
                    if (Math.Abs(deltaX) > Math.Abs(deltaY))
                    {
                        md = moveDirection.DirX;
                    }
                    else
                    {
                        md = moveDirection.DirY;
                    }

                    //Console.WriteLine($"{SelectableObject.Label} Mouse direction {md}");
                    moveDir = md;
                }
            }
            
            if (SelectableObject != null)
            {
                // Selection move detection 
                if (moveDir != moveDirection.None)
                {
                    // apply move on selection
                    SelectableObject.Move(mousePixel, movePixelFrom, moveDir);
                    movePixelFrom = mousePixel;
                    UpdateXEnd(); // moving Object can modify XDisplayEnd
                }

                RefreshReq = true;
            }

            // limit refresh rate to 50ms
            if (elapsedTicks.TotalMilliseconds < 50)
            {
                return;
            }
            LastRefreshTime = DateTime.Now;

            UpdateXEnd();
            UpdateXLimit();

            if (moveGlobalBaseTime != 0)
            {
                if(moveDir == moveDirection.DirX)
                {
                    double dX = mousePixel.X - movePixelFrom.X;
                    double dT = DateTime.Now.Ticks - moveGlobalBaseTime;
                    double delta = Math.Round(moveXDynamic * dX / dT);
                    Trc.Data.XPos = Trc.Data.XPos - delta * fp.Plot.LastRender.UnitsPerPxX;
                    if (Trc.Data.XPos < 0)
                    {
                        Trc.Data.XPos = 0;
                    }
                    else if (Trc.Data.XPos > XDisplayEnd)
                    {
                        Trc.Data.XPos = XDisplayEnd;
                    }
                    moveGlobalBaseTime = DateTime.Now.Ticks;
                    movePixelFrom = mousePixel;
                    //Console.WriteLine($"{Trc.Data.XPos}\t{delta}\t{dX}\t{dT}");
                }
                else if(moveDir == moveDirection.DirY)
                {
                    foreach (ChannelCtrl c in Trc.GetSignalList())
                    {
                        // apply move on selection
                        c.Move(mousePixel, movePixelFrom, moveDir);
                    }
                    movePixelFrom = mousePixel;
                    UpdateXEnd(); // moving Object can modify XDisplayEnd                        
                }


                UpdateXLimit();
                RefreshReq = true;
            }

            if (RefreshReq == true)
            {
                RefreshReq = false;
                Play();
                XTimeEndAtLastRefresh = XTimeEnd;

                foreach (CtrlBase o in Trc.Ctrls)
                {
                    o.UpdateLegend(mouseCoordinates.X);
                }
            }

            if(watchFollow == true)
            {
                watchLine.Move(mousePixel, movePixelFrom, moveDirection.DirX);
                watchLine.UpdateLegend(mouseCoordinates.X);
            }
            UpdateLostIndicator();
            signalLegend.Update();
            fp.Refresh();
        }
        public Coordinates GetCoordinates(Pixel pixel, IXAxis? xAxis = null, IYAxis? yAxis = null)
        {
            return fp.Plot.GetCoordinates(pixel, xAxis, yAxis);
        }

        public CoordinateRect GetCoordinateRect(Pixel pixel, float r, IXAxis? xAxis = null, IYAxis? yAxis = null)
        {
            return fp.Plot.GetCoordinateRect(pixel.X, pixel.Y, r, xAxis, yAxis);
        }
        public void SetCursor(Cursor cursor)
        {
            fp.Cursor = cursor;
        }
        public void Delete()
        {
            if((SelectableObject != null)&&(SelectableObject != dataSelector))
            {
                Trc.Delete(SelectableObject);
                SelectableObject = null;
            }
            else
            {
                DeleteData("Delete Data");
            }
        }
        private void ShowObject(CtrlBase o)
        {
            o.SetVisibility(true);
            if(o is ChannelCtrl)
            {
                foreach(CtrlBase m in Trc.Ctrls)
                {
                    if(m is MarkerCtrl)
                    {
                        ((MarkerCtrl)m).SetChannelVisibility((ChannelCtrl)o, true);
                    }
                }
                watchLine.SetChannelVisibility((ChannelCtrl)o, true);
                foreach (CtrlBase m in Trc.Ctrls)
                {
                    if (m is DataMarkerCtrl) m.Update();
                }
            }
        }
        private void HideObject(CtrlBase o)
        {
            o.SetVisibility(false);
            if(o is ChannelCtrl)
            {
                foreach (CtrlBase m in Trc.Ctrls)
                {
                    if(m is MarkerCtrl)
                    {
                        ((MarkerCtrl)m).SetChannelVisibility((ChannelCtrl)o, false);
                    }
                }
                watchLine.SetChannelVisibility((ChannelCtrl)o, false);
                foreach (CtrlBase m in Trc.Ctrls)
                {
                    if (m is DataMarkerCtrl) m.Update();
                }
            }            
        }
        // records the offset/gain change done by action as one undoable command
        private void RecordView(string description, Action action)
        {
            var cmd = new ChannelCmdView(Trc, description);
            action();
            if (cmd.Commit())
            {
                commandMgr.ExecuteCommand(cmd);
            }
        }
        private void ResetOffsets(ChannelCtrl s)
        {
            s.ResetXOffset();
            s.ResetYOffset();
            foreach (CtrlBase m in Trc.Ctrls)
            {
                if (m is AnnotationCtrl or DataMarkerCtrl)
                {
                    m.Update();
                }
            }
        }
        public ChannelCtrl GetSelectedSignal()
        {
            if(SelectableObject is ChannelCtrl)
            {
                return (ChannelCtrl)SelectableObject;
            }
            else
            {
                return null;
            }
        }
        private void ResetXOffset(ChannelCtrl s)
        {
            s.ResetXOffset();
            foreach (CtrlBase m in Trc.Ctrls)
            {
                if (m is AnnotationCtrl or DataMarkerCtrl)
                {
                    m.Update();
                }
            }
        }
        private void ResetYOffset(ChannelCtrl s)
        {
            s.ResetYOffset();
            foreach (CtrlBase m in Trc.Ctrls)
            {
                if (m is AnnotationCtrl or DataMarkerCtrl)
                {
                    m.Update();
                }
            }
        }
        public void updateMenuRightClick()
        {
            CtrlBase? menuContextObj = SelectableObject;
            Trc.ContextMenu.Items.Clear();
            if (watchLine.IsVisible())
            {
                Trc.ContextMenu.Items.Add("Hide watch", null, (object? sender, EventArgs e) =>
                {
                    watchLine.SetVisibility(false);
                });
            }
            else
            {
                Trc.ContextMenu.Items.Add("Show watch", null, (object? sender, EventArgs e) =>
                {
                    watchLine.SetVisibility(true);
                });
            }
            Trc.ContextMenu.Items.Add("Copy data", null, (object? sender, EventArgs e) =>
            {
                CopyData();
            });
            Trc.ContextMenu.Items.Add(dataSelector.IsVisible() ? "Analyzer (selection) (A)" : "Analyzer (visible range)", null, (object? sender, EventArgs e) =>
            {
                OpenAnalyzer();
            });
            Trc.ContextMenu.Items.Add($"Clear data", null, (object? sender, EventArgs e) =>
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure to clear all data?", "Confirm", MessageBoxButtons.YesNo);

                if (dialogResult == DialogResult.Yes)
                {
                    foreach (ChannelCtrl c in Trc.GetSignalList())
                    {
                        c.Clear();
                    }
                    UpdateXEnd();
                    //ZoomFit();
                }
            });

            if (menuContextObj == null)
            {

                Trc.ContextMenu.Items.Add($"Delete all signals", null, (object? sender, EventArgs e) =>
                {
                    DialogResult dialogResult = MessageBox.Show("Are you sure to delete all signals?", "Confirm", MessageBoxButtons.YesNo);

                    if (dialogResult == DialogResult.Yes)
                    {
                        var signals = Trc.GetSignalList();
                        for (int i = signals.Count - 1; i >= 0; i--)
                        {
                            Trc.Delete(signals[i]);
                        }
                        ColorRGB.SignalColorReset();
                        UpdateXEnd();
                    }
                });
                Trc.ContextMenu.Items.Add($"Delete all markers", null, (object? sender, EventArgs e) =>
                {
                    DialogResult dialogResult = MessageBox.Show("Are you sure to delete all markers?", "Confirm", MessageBoxButtons.YesNo);

                    if (dialogResult == DialogResult.Yes)
                    {

                        for (int i = Trc.Ctrls.Count - 1; i >= 0; i--)
                        {
                            if ((Trc.Ctrls[i] is MarkerCtrl) || (Trc.Ctrls[i] is DeltaCtrl) || (Trc.Ctrls[i] is AnnotationCtrl) || (Trc.Ctrls[i] is DataMarkerCtrl))
                            {
                                Trc.Delete(Trc.Ctrls[i]);
                            }
                        }
                        ColorRGB.MarkerColorReset();
                        UpdateXEnd();
                    }
                });
            }

            string selectName = "all";

            if (menuContextObj != null)
            {
                selectName = menuContextObj.DataBase.Name;
            }

            Trc.ContextMenu.Items.Add(new ToolStripSeparator());
            Trc.ContextMenu.Items.Add($"Delete {selectName}", null, (object? sender, EventArgs e) =>
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure to delete?", "Confirm", MessageBoxButtons.YesNo);

                if (dialogResult == DialogResult.Yes)
                {
                    if (menuContextObj != null)
                    {
                        Trc.Delete(menuContextObj);
                    }
                    else
                    {
                        var signals = Trc.GetSignalList();
                        for (int i = signals.Count - 1; i >= 0; i--)
                        {
                            Trc.Delete(signals[i]);
                        }
                        ColorRGB.SignalColorReset();
                        UpdateXEnd();
                    }
                }
            });
            if ((menuContextObj == null) || menuContextObj.IsVisible())
            {
                Trc.ContextMenu.Items.Add($"Hide {selectName}", null, (object? sender, EventArgs e) =>
                {
                    if (menuContextObj != null)
                    {
                        HideObject(menuContextObj);
                    }
                    else
                    {
                        foreach (ChannelCtrl s in Trc.GetSignalList())
                        {
                            HideObject(s);
                        }
                    }
                });
            }
            if ((menuContextObj == null) || (menuContextObj.IsVisible() == false))
            {
                Trc.ContextMenu.Items.Add($"Show {selectName}", null, (object? sender, EventArgs e) =>
                {
                    if (menuContextObj != null)
                    {
                        ShowObject(menuContextObj);
                    }
                    else
                    {
                        foreach (ChannelCtrl s in Trc.GetSignalList())
                        {
                            ShowObject(s);
                        }
                    }
                });
            }
            if ((menuContextObj == null) ||(menuContextObj is ChannelCtrl))
            {
                Trc.ContextMenu.Items.Add($"Reset Offsets {selectName}", null, (object? sender, EventArgs e) =>
                {
                    RecordView("Reset offsets", () =>
                    {
                        if (menuContextObj != null)
                        {
                            ChannelCtrl s = (ChannelCtrl)menuContextObj;
                            ResetOffsets(s);
                        }
                        else
                        {
                            foreach (ChannelCtrl s in Trc.GetSignalList())
                            {
                                ResetOffsets(s);
                            }
                        }
                    });
                });
                Trc.ContextMenu.Items.Add($"Reset X Offsets {selectName}", null, (object? sender, EventArgs e) =>
                {
                    RecordView("Reset X offset", () =>
                    {
                        if (menuContextObj != null)
                        {
                            ChannelCtrl s = (ChannelCtrl)menuContextObj;
                            ResetXOffset(s);
                        }
                        else
                        {
                            foreach (ChannelCtrl s in Trc.GetSignalList())
                            {
                                ResetXOffset(s);
                            }
                        }
                    });
                });
                Trc.ContextMenu.Items.Add($"Reset Y Offsets {selectName}", null, (object? sender, EventArgs e) =>
                {
                    RecordView("Reset Y offset", () =>
                    {
                        if (menuContextObj != null)
                        {
                            ChannelCtrl s = (ChannelCtrl)menuContextObj;
                            ResetYOffset(s);
                        }
                        else
                        {
                            foreach (ChannelCtrl s in Trc.GetSignalList())
                            {
                                ResetYOffset(s);
                            }
                        }
                    });
                });
            }
        }
        public void OpenAnalyzer()
        {
            new Analyzer.AnalyzerForm(Trc).Show(Trc.tabPage.FindForm());
        }
        public void CopyData()
        {
            double xStart = GetSelectStart();
            double xWidth = GetSelectWidth();

            var columns = new List<List<string>>();
            foreach (ChannelCtrl c in Trc.GetSignalList())
            {
                var filtered = c.Filter(xStart, xWidth);
                var rowsX = filtered.Select(p => p.xStr).ToList();
                var rowsY = filtered.Select(p => p.yStr).ToList();
                rowsX.Insert(0, c.DataBase.Name + " Time");
                rowsY.Insert(0, c.DataBase.Name + " Data");
                columns.Add(rowsX);
                columns.Add(rowsY);
                if (c.SignalBase.hasDevice)
                {
                    // raw samples: paste restores them as is, "Data" may be a function output
                    var rowsRaw = filtered.Select(p => p.rawStr).ToList();
                    rowsRaw.Insert(0, c.DataBase.Name + " Raw");
                    columns.Add(rowsRaw);
                }
            }
            int maxRows = columns.Max(c => c.Count);
            var lines = new List<string>();
            for (int i = 0; i < maxRows; i++)
            {
                var row = columns
                    .Select(c => i < c.Count ? c[i].ToString(CultureInfo.InvariantCulture) : "")
                    .ToArray();
                lines.Add(string.Join("\t", row));
            }

            string clipboardText = string.Join(Environment.NewLine, lines);
            var obj = new DataObject();
            obj.SetText(clipboardText);
            // channel definitions: paste in another trace creates the missing channels
            obj.SetData(ChannelsClipboardFormat, SerializeChannelDefinitions(Trc.GetSignalList()));
            Clipboard.SetDataObject(obj, true);
        }
        public const string ChannelsClipboardFormat = "eScope.Channels";
        // channels without their samples (data travels in the text columns)
        static string SerializeChannelDefinitions(List<ChannelCtrl> signals)
        {
            var defs = new List<string>();
            foreach (var c in signals)
            {
                var ch = c.SignalBase;
                var (y, x, lost) = (ch.Y, ch.X, ch.Lost);
                ch.Y = new(); ch.X = new(); ch.Lost = new();
                try { defs.Add(System.Text.Json.JsonSerializer.Serialize(ch)); }
                finally { (ch.Y, ch.X, ch.Lost) = (y, x, lost); }
            }
            return "[" + string.Join(",", defs) + "]";
        }
        public double GetSelectStart()
        {
            if(dataSelector.IsVisible())
            {
                return dataSelector.Min();
            }
            else
            {
                return Trc.Data.XPos;
            }
        }
        public double GetSelectWidth()
        {
            if (dataSelector.IsVisible())
            {
                return dataSelector.Width();
            }
            else
            {
                return XDisplayEnd;
            }
        }

        public void CutData()
        {
            CopyData();
            DeleteData("Cut Data");
        }
        public void DeleteData(string action)
        {
            DialogResult dialogResult = DialogResult.Yes;

            string toDelete = "selected";
            if(dataSelector.IsVisible() == false)
            {
                toDelete = "visible";
            }
            if(action == "Delete Data")
            {
                dialogResult = MessageBox.Show($"Are you sure to delete {toDelete} data?", "Confirm", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            }
 
            if (dialogResult == DialogResult.Yes)
            {
                var delCmd = new PlotCmdDeleteData(Trc, action);
                commandMgr.ExecuteCommand(delCmd);
            }
        }

        public void PasteData()
        {
            try
            {
                var cmd = new PlotCmdPasteData(Trc);
                commandMgr.ExecuteCommand(cmd);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public void DataSelectorConfigure(double start, double width)
        {
            dataSelector.Data.X1 = start;
            dataSelector.Data.X2 = start + width;
            dataSelector.Configure();
            dataSelector.SetVisibility(true);
        }
        public void Select()
        {
            if (dataSelector.IsVisible() == false)
            {
                DataSelectorConfigure(watchLine.Data.Pos, 0);

                /* enable moving */
                double d = dataSelector.GetNearestDistance(mousePixel, float.PositiveInfinity);
                moveDir = moveDirection.DirX;
                movePixelFrom = mousePixel;
                selectObject(dataSelector);
            }
            else
            { 
                if (moveDir != moveDirection.None)
                {/* disable moving*/
                    SelectableObject = null;
                    moveDir = moveDirection.None;
                }
                else
                {/* enable moving*/
                    double d = dataSelector.GetNearestDistance(mousePixel, float.PositiveInfinity);
                    moveDir = moveDirection.DirX;
                    movePixelFrom = mousePixel;

                    DataSelectorConfigure(watchLine.Data.Pos, 0);
                    selectObject(dataSelector);
                }
            }
        }
        public void CancelSelect()
        {
            dataSelector.SetVisibility(false);
            ClearSelection(); /* LowLight restores the default cursor */
            ZoomSelectionInProgress = false; /* abort any zoom operation */
            stopMove();
        }

        private void MouseMove(object? sender, MouseEventArgs e)
        {
            mousePixel.X = e.X;
            mousePixel.Y = e.Y;
            mouseCoordinates = fp.Plot.GetCoordinates(mousePixel);

        }
        // Translates a mouse event received on another control (e.g. a legend label)
        // into fp's client coordinate space, so plot mouse handlers can be reused as-is.
        private MouseEventArgs TranslateMouseEventArgs(Control source, MouseEventArgs e)
        {
            Point p = fp.PointToClient(source.PointToScreen(new Point(e.X, e.Y)));
            return new MouseEventArgs(e.Button, e.Clicks, p.X, p.Y, e.Delta);
        }
        public void ForwardMouseMove(Control source, MouseEventArgs e)
        {
            MouseMove(source, TranslateMouseEventArgs(source, e));
        }
        public void ForwardMouseDown(Control source, MouseEventArgs e)
        {
            MouseMove(source, TranslateMouseEventArgs(source, e));
            MouseDown(source, e);
        }
        public void ForwardMouseUp(Control source, MouseEventArgs e)
        {
            MouseMove(source, TranslateMouseEventArgs(source, e));
            MouseUp(source, e);
        }
        public void ForwardMouseWheel(Control source, MouseEventArgs e)
        {
            MouseMove(source, TranslateMouseEventArgs(source, e));
            MouseWheel(source, e);
        }
        private void MouseDownUpdateMenu(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                updateMenuRightClick();
            }
        }
        private void MouseDown(object? sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                pendingViewCmd = new ChannelCmdView(Trc, "Move");
                if(SelectableObject != null)
                {
                    // start moving object
                    movePixelFrom = mousePixel;
                    moveDir = moveDirection.DirUnk;
                }
                else // move X(time)
                {
                    movePixelFrom = mousePixel;
                    moveGlobalBaseTime = DateTime.Now.Ticks;
                    moveDir = moveDirection.DirUnk;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                updateMenuRightClick();
                //SelectableObject = null;
                //                moveDir = moveDirection.None;
            }

        }
        private void stopMove()
        {
            moveDir = moveDirection.None;
            if (pendingViewCmd != null)
            {
                if (pendingViewCmd.Commit())
                {
                    commandMgr.ExecuteCommand(pendingViewCmd);
                }
                pendingViewCmd = null;
            }
            if (ZoomSelectionInProgress == true)
            {
                zoomSelectionApply();
            }

        }
        private void MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (moveDir != moveDirection.None) // signal move
                {
                    stopMove();
                }
                pendingViewCmd = null;
                if (moveGlobalBaseTime != 0)
                {
                    moveGlobalBaseTime = 0;
                }
            }
        }
        private void DoubleClick(object? sender, System.EventArgs e)
        {
            if(SelectableObject != null)
            {
                Trc.Edit(SelectableObject);
            }
        }
        private void MouseEnter(object? sender, System.EventArgs e)
        {
        }
        private void MouseLeave(object? sender, System.EventArgs e)
        {
        }
        private void MouseWheel(object? sender, MouseEventArgs e)
        {
            //Console.WriteLine(e.Delta);

            // zooming from the legend has no meaningful mouse position on the signal, so center on 0 instead
            bool centerZero = sender is Control c && c != fp;
            // one legend notch (120) should zoom once, whereas the plot accumulates to wheelSensibility
            double sensibility = centerZero ? 120 : wheelSensibility;

            if (e.Delta > 0 && Control.ModifierKeys == Keys.Control)
            {
                wheelCtrlDelta += e.Delta;
                if(wheelCtrlDelta >= sensibility)
                {
                    wheelCtrlDelta -= sensibility;
                    YZoomIn(centerZero);
                }
            }
            else if (e.Delta < 0 && Control.ModifierKeys == Keys.Control)
            {
                wheelCtrlDelta += e.Delta;
                if (wheelCtrlDelta <= -sensibility)
                {
                    wheelCtrlDelta += sensibility;
                    YZoomOut(centerZero);
                }
            }
            else if (e.Delta > 0)
            {
                wheelDelta += e.Delta;
                if (wheelDelta >= sensibility)
                {
                    wheelDelta -= sensibility;
                    ZoomIn();
                }
            }
            else if (e.Delta < 0)
            {
                wheelDelta += e.Delta;
                if (wheelDelta <= -sensibility)
                {
                    wheelDelta += sensibility;
                    ZoomOut();
                }
            }
        }
        public void ZoomFit() /* echap */
        {
            if(dataSelector.IsVisible())
            {
                CancelSelect();
            }
            else
            {
                FitAll();
            }
        }
        // whole recording visible, selection kept
        public void FitAll(bool immediate = false)
        {
            SetView(0, XDisplayEnd != 0 ? XDisplayEnd + XDisplayEnd / 10 : 2000, immediate);
        }
        // immediate: no zoom animation, the axis is at the new view right away (e.g. before a screenshot)
        public void SetView(double xPos, double xWidth, bool immediate = false)
        {
            Trc.Data.XPos = xPos;
            Trc.Data.XWidth = xWidth;
            if (immediate)
            {
                XPos_ = xPos;
                XWidth_ = xWidth;
            }
            UpdateXLimit();
            if (immediate)
                signalLegend.Update(); // timebase label, else updated on the next timer tick
            RefreshReq = true;
        }
        public void zoomSelectionApply()
        {
            Trc.Data.XPos = dataSelector.Min();
            Trc.Data.XWidth = dataSelector.Width();
            dataSelector.SetVisibility(false);
            ClearSelection();
            ZoomSelectionInProgress = false;
            UpdateXLimit();
            RefreshReq = true;
        }
        public void ZoomSelection()
        {
            if (dataSelector.IsVisible())
            {
                stopMove();
                zoomSelectionApply();
            }
            else
            {
                Select();
                ZoomSelectionInProgress = true;
            }
        }
        private void UpdateXLimit()
        {
            double gridNb;
            XPos_ = Math.Round(FilterLow(Trc.Data.XPos, XPos_, 100, 200));
            XWidth_ = Math.Round(FilterLow(Trc.Data.XWidth, XWidth_, 100, 200));
            AxisLimits al = fp.Plot.Axes.GetLimits();
            if (al.XRange.Min != XPos_ || al.XRange.Max != XPos_ + XWidth_)
            {
                fp.Plot.Axes.SetLimitsX(XPos_, XPos_ + XWidth_);
                RefreshReq = true; // RefreshReq for marker
            }

            gridNb = Math.Round(XWidth_ / Trc.GetXGrid());
            if (gridNb < 5)
            {
                while (gridNb < 5)
                {
                    Trc.XGridDecrease();
                    gridNb = Math.Round(XWidth_ / Trc.GetXGrid());
                }
                SetXTickGenerator();
            }
            else if (gridNb > 15)
            {
                while (gridNb > 15)
                {
                    Trc.XGridIncrease();
                    gridNb = Math.Round(XWidth_ / Trc.GetXGrid());
                }
                SetXTickGenerator();
            }
        }
        private void SetXTickGenerator()
        {
            double grid = Trc.GetXGrid();
            NumericFixedInterval tickGenX = new NumericFixedInterval((int)grid);
            // display seconds instead of milliseconds when a division is >= 1 s
            if (grid >= 1000)
                tickGenX.LabelFormatter = ms => $"{ms / 1000:0.###} s";
            fp.Plot.Axes.Bottom.TickGenerator = tickGenX;
        }
        public void ZoomIn()
        {
            double XWidthNew = Math.Round(Trc.Data.XWidth * 0.75);
            double ratio = (mouseCoordinates.X - Trc.Data.XPos) / (Trc.Data.XWidth);

            Trc.Data.XWidth = XWidthNew;
            if (Trc.Data.XWidth < 10)
            {
                Trc.Data.XWidth = 10;
            }
            else
            {
                // zoom around mouse

                Trc.Data.XPos = mouseCoordinates.X - ratio * Trc.Data.XWidth;
                if (Trc.Data.XPos < 0)
                {
                    Trc.Data.XPos = 0;
                }
            }
            //Console.WriteLine($"ZoomInMouse P{Trc.Data.XPos} W{Trc.Data.XWidth} End:{XDisplayEnd}");
            
            foreach (CtrlBase o in Trc.Ctrls)
            {
                o.ZoomIn();
            }

            UpdateXLimit();
            RefreshReq = true;
        }

        public void ZoomOut()
        {
            double XWidthNew = Math.Round(Trc.Data.XWidth * 1.25);
            double ratio = (mouseCoordinates.X - Trc.Data.XPos) / (Trc.Data.XWidth);
            Trc.Data.XWidth = XWidthNew;
            Trc.Data.XPos = mouseCoordinates.X - ratio * Trc.Data.XWidth;
            if (Trc.Data.XPos < 0)
            {
                Trc.Data.XPos = 0;
            }

            //Console.WriteLine($"ZoomOut P{Trc.Data.XPos} W{Trc.Data.XWidth} End:{XDisplayEnd}");
            foreach (CtrlBase o in Trc.Ctrls)
            {
                o.ZoomOut();
            }

            UpdateXLimit();
            RefreshReq = true;
        }
        public void YZoomIn(bool centerZero = false)
        {
            // over a legend the mouse position isn't on the signal (e.g. ctrl+up), so center on 0
            centerZero |= signalLegend.IsMouseOver;
            RecordView("Y gain", () =>
            {
                if (SelectableObject != null)
                {
                    SelectableObject.YZoomIn(mousePixel, centerZero);
                }
                else
                {
                    foreach (CtrlBase o in Trc.Ctrls)
                    {
                        o.YZoomIn(mousePixel, centerZero);
                    }
                }
            });
            RefreshReq = true;

        }
        public void YZoomOut(bool centerZero = false)
        {
            centerZero |= signalLegend.IsMouseOver;
            RecordView("Y gain", () =>
            {
                if (SelectableObject != null)
                {
                    SelectableObject.YZoomOut(mousePixel, centerZero);
                }
                else
                {
                    foreach (CtrlBase o in Trc.Ctrls)
                    {
                        o.YZoomOut(mousePixel, centerZero);
                    }
                }
            });
            RefreshReq = true;
        }
        public void MoveBegin()
        {
            Trc.Data.XPos = 0;
            
            UpdateXLimit();
            //Console.WriteLine($"MoveBegin P{Trc.Data.XPos} W{Trc.Data.XWidth} End:{XDisplayEnd}");

            RefreshReq = true;
        }
        double FilterLow(double X, double Y, double ts, double ct) // 3 x ct => 99%
        {
            double gain = 1.0 * ts / ct;

            if (gain < 0) // invalid gain
            {
                return 0.0;
            }

            Y = (X - Y) * gain + Y;

            return Y;
        }
        public void UpdateXEnd()
        {
            double xDisplayEnd = 0;
            double xTimeEnd = 0;
            foreach (CtrlBase o in Trc.Ctrls)
            {
                xDisplayEnd = Math.Max(o.XDisplayEnd, xDisplayEnd);
                xTimeEnd = Math.Max(o.XTimeEnd, xTimeEnd);
            }
            if(XDisplayEnd != xDisplayEnd)
            {
                XDisplayEnd = xDisplayEnd;
                RefreshReq = true;
            }
            XTimeEnd = xTimeEnd;
        }
        public void Play()
        {
            if(bRool)
            {
                Trc.Data.XPos += XTimeEnd - XTimeEndAtLastRefresh;
                if (Trc.Data.XPos < 0)
                {
                    Trc.Data.XPos = 0;
                }

                UpdateXLimit();
            }
        }
        public void MoveEnd()
        {
            // move to end plus 1 grind empty
            Trc.Data.XPos = XDisplayEnd - Trc.Data.XWidth + Trc.GetXGrid();
            if (Trc.Data.XPos < 0)
            {
                Trc.Data.XPos = 0;
            }
            
            UpdateXLimit();

/*            Console.WriteLine($"MoveEnd P{Trc.Data.XPos} W{Trc.Data.XWidth} End:{XDisplayEnd}");*/

        }
        public void MoveForward()
        {
            double width = Trc.Data.XWidth;

            Trc.Data.XPos += width/4;

            // if (Trc.Data.XPos + Trc.Data.XWidth > XDisplayEnd)
            // {
            //     MoveEnd();
            // }
            // else
            {
                
                UpdateXLimit();
            }
            //Console.WriteLine($"MoveForward P{Trc.Data.XPos} W{Trc.Data.XWidth} End:{XDisplayEnd}");

            RefreshReq = true;
        }
        public void MoveBackward()
        {
            Trc.Data.XPos = Trc.Data.XPos - Trc.Data.XWidth/4;
            if (Trc.Data.XPos < 0)
            {
                Trc.Data.XPos = 0;
            }
            
            UpdateXLimit();

            //Console.WriteLine($"MoveBackward P{Trc.Data.XPos} W{Trc.Data.XWidth} End:{XDisplayEnd}");

            RefreshReq = true;
        }
        public void RunStop()
        {
            if (bRun == false)
            {
                bRun = true;
                
            }
            else // if (bRun == true)
            {
                bRun = false;
                
            }
        }

        public void SetPlay()
        {
            bRool = true;
        }

        public void SetPause()
        {
            bRool = false;
        }        
    }
}
