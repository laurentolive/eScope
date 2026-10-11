using eScope.Command;
using eScope.Plot;
using System.IO.Compression;

namespace eScope
{
    public class Trace
    {
        public string Name { get; set; }
        public double XPos { get; set; }
        public double XWidth { get; set; }
        public double XGridDecadeNb { get; set; }
        public double XGridDecadeVal { get; set; }
        public int LegendWidth { get; set; }
        public int DarkMode { get; set; }
        public int signalNb { get; set; } /* never reset signal number */

        public List<Channel> Channels { get; set; }
        public List<Marker> Markers { get; set; }
        public List<Plot.Delta> Deltas { get; set; }
        public List<Annotation> Annotations { get; set; }
        public List<DataMarker> DataMarkers { get; set; }
        public List<Watch.Data> Watches { get; set; }
        
        public Trace()
        {
            Name = "Trace";
            XPos = 0;
            XWidth = 2000;
            XGridDecadeNb = 100;
            XGridDecadeVal = 1;
            LegendWidth = 100;
            DarkMode = 0;
            Channels = new List<Channel>();
            Markers = new List<Marker>();
            Deltas = new List<Plot.Delta>();
            Annotations = new List<Annotation>();
            DataMarkers = new List<DataMarker>();
            Watches = new List<Watch.Data>();
        }
    }    

    internal class CmdDeleteObj : ICommand
    {
        public string Description { get; }
        private readonly TraceCtrl Trc;
        private int idx;
        private CtrlBase Ctrl;
        //private readonly TraceCollectionDescriptor Descriptor;

        public CmdDeleteObj(TraceCtrl trc, CtrlBase ctrl)
        {
            Trc = trc;
            Ctrl = ctrl;

            //Descriptor = trc.Collections
            //    .FirstOrDefault(d => d.ContainsCtrl(ctrl))
            //    ?? throw new InvalidOperationException("Control not found in any collection");

            Description = $"Delete '{ctrl.DataBase.Name}'";
            idx = Trc.Ctrls.IndexOf(ctrl);
        }

        public void Execute()
        {
            Trc.Ctrls[idx].Remove();
            Trc.Ctrls.RemoveAt(idx);
        }
        public void Undo()
        {
            Trc.Ctrls.Insert(idx, Ctrl);
            Trc.Ctrls[idx].Add();
        }
    }
    internal class CmdAddObj : ICommand
    {
        public string Description { get; }
        private readonly TraceCtrl Trc;
        private int Idx;
        private CtrlBase Ctrl;
        //private readonly TraceCollectionDescriptor Descriptor;

        public CmdAddObj(TraceCtrl trc, object data)
        {
            Trc = trc;

            //Descriptor = Trc.Ctrls.FirstOrDefault(d => d.DataType == data.GetType())
            //    ?? throw new InvalidOperationException($"No collection for {data.GetType().Name}");

            Ctrl = Trc._factory.Create(data, Trc);
            //Ctrl = Descriptor.CreateControl(trc, data);
            Description = $"Add '{Ctrl.DataBase.Name}'";

            Idx = Trc.Ctrls.Count();
        }
        public void Execute() {

            Trc.Ctrls.Insert(Idx, Ctrl);
            Ctrl.Add();
        }
        public void Undo()
        {
            Ctrl.Remove();
            Trc.Ctrls.RemoveAt(Idx);
        }
    }
    internal class CmdRenameTrace : ICommand
    {
        public string Description { get; }
        private readonly TraceCtrl Trc;
        private readonly string OldName;
        private readonly string NewName;

        public CmdRenameTrace(TraceCtrl trc, string newName)
        {
            Trc = trc;
            OldName = trc.Data.Name;
            NewName = newName;
            Description = $"Rename '{OldName}'";
        }
        public void Execute() => Trc.SetName(NewName);
        public void Undo() => Trc.SetName(OldName);
    }
    public class TraceCtrl: IPlotContext
    {
        //internal readonly List<TraceCollectionDescriptor> Collections = new();
        internal readonly PlotObjectFactory _factory = new();
        public Trace Data { get; set; }
        public IList<CtrlBase> Ctrls { get; } = new List<CtrlBase>();
        public Dictionary<string, List<double>> ScriptGlobalsDict { get; } = new();

        public eScope Scope { get; set; }
        public eScopePlot Plot { get; set; }
        public int DarkMode{ get { return Data.DarkMode; } }

        public TabPage tabPage = new();

        public ContextMenuStrip ContextMenu = new();
        public ToolStripMenuItem ChannelsMenu = new();
        public ToolStripMenuItem MarkersMenu = new();
        public ToolStripMenuItem newChannelsMenuItem = new();
        public ToolStripMenuItem addVerticalMarkerMenuItem = new();
        public ToolStripMenuItem addHorizintalDeltaMenuItem = new();
        public ToolStripMenuItem addAnnotationMenuItem = new();
        public ToolStripMenuItem addDataMarkerMenuItem = new();

        private Watch.View? _watchForm = null;

        public TraceCtrl(Trace data, eScope scope)
        {
            Data = data;
            Create(scope);
        }
        public int GetNextSignalUid()
        {
            Data.signalNb++;
            return Data.signalNb;
        }

        public TraceCtrl(string name, eScope scope)
        {
            Data = new Trace { Name = name };
            Create(scope);
        }

        private void Create(eScope scope)
        {
            Scope = scope;
            Plot = new eScopePlot(this);

            // Initialize factory
            _factory.Register<Channel, ChannelCtrl>();
            _factory.Register<Marker, MarkerCtrl>();
            _factory.Register<Plot.Delta, DeltaCtrl>();
            _factory.Register<Annotation, AnnotationCtrl>();
            _factory.Register<DataMarker, DataMarkerCtrl>();
            foreach(var c in Data.Channels)
            {
                var ctrl = _factory.Create(c, this);
                Ctrls.Add(ctrl);
                ctrl.Add();
            }
            foreach (var c in Data.Markers)
            {
                var ctrl = _factory.Create(c, this);
                ctrl.Add();
                Ctrls.Add(ctrl);
            }
            foreach (var c in Data.Deltas)
            {
                var ctrl = _factory.Create(c, this);
                ctrl.Add();
                Ctrls.Add(ctrl);
            }
            foreach (var c in Data.Annotations)
            {
                var ctrl = _factory.Create(c, this);
                ctrl.Add();
                Ctrls.Add(ctrl);
            }
            Data.DataMarkers ??= new List<DataMarker>(); // workspace saved before data markers existed
            foreach (var c in Data.DataMarkers)
            {
                var ctrl = _factory.Create(c, this);
                ctrl.Add();
                Ctrls.Add(ctrl);
            }
            foreach(var s in GetSignalList())
            {
                s.ConfigureLink();
            }
            // --- Initialize Menus ---
            SetupMenu(ChannelsMenu, newChannelsMenuItem, "Channels", Keys.Insert, menuChannelsNew_Click);
            SetupMarkersMenu();
            ChannelsMenu.DropDownOpening += (s, e) => RefreshObjectMenu(ChannelsMenu, newChannelsMenuItem, c => c is ChannelCtrl);
            MarkersMenu.DropDownOpening += (s, e) => RefreshObjectMenu(MarkersMenu, addAnnotationMenuItem, c => c is MarkerCtrl || c is DataMarkerCtrl || c is DeltaCtrl || c is AnnotationCtrl);

            // --- Initialize Collections ---
            //AddCollection(Data.Channels, ChannelsMenu, (d, trc) => new ChannelCtrl(d, trc));
            //AddCollection(Data.Functions, FunctionsMenu, (d, trc) => new FunctionCtrl(d, trc));
            //AddCollection(Data.Markers, MarkersMenu, (d, trc) => new MarkerCtrl(d, trc));
            //AddCollection(Data.Deltas, MarkersMenu, (d, trc) => new DeltaCtrl(d, trc));
            //AddCollection(Data.Annotations, MarkersMenu, (d, trc) => new AnnotationCtrl(d, trc));

            tabPage.Location = new System.Drawing.Point(0, 0);
            tabPage.Name = Data.Name;
            tabPage.Padding = new System.Windows.Forms.Padding(0);
            tabPage.Margin = new System.Windows.Forms.Padding(0);
            tabPage.TabIndex = 0;
            tabPage.Text = Data.Name;
            tabPage.UseVisualStyleBackColor = true;
            tabPage.BackColor = Color.FromKnownColor(KnownColor.Control);
            tabPage.Controls.Add(Plot.GetControl());
            tabPage.ContextMenuStrip = ContextMenu;   
        }

        private void SetupMenu(ToolStripMenuItem menu, ToolStripMenuItem newItem, string text, Keys shortcut, EventHandler handler)
        {
            menu.DropDownItems.AddRange(new ToolStripItem[] { newItem });
            menu.Name = text.ToLower() + "Menu";
            menu.Text = text;
            newItem.Text = "New";
            newItem.ShortcutKeys = shortcut;
            newItem.Click += handler;
        }

        // rebuild the per-object entries after the fixed "New" items, so add/delete/undo/rename are always reflected
        private void RefreshObjectMenu(ToolStripMenuItem menu, ToolStripItem lastFixedItem, Func<CtrlBase, bool> filter)
        {
            int fixedCount = menu.DropDownItems.IndexOf(lastFixedItem) + 1;
            while (menu.DropDownItems.Count > fixedCount)
            {
                menu.DropDownItems.RemoveAt(fixedCount);
            }
            var items = Ctrls.Where(filter).ToList();
            if (items.Count == 0) return;
            menu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var c in items)
            {
                c.Item.Text = c.DataBase.Name;
                menu.DropDownItems.Add(c.Item);
            }
        }

        private void SetupMarkersMenu()
        {
            MarkersMenu.DropDownItems.AddRange(new ToolStripItem[] { addVerticalMarkerMenuItem, addDataMarkerMenuItem, addHorizintalDeltaMenuItem, addAnnotationMenuItem });
            MarkersMenu.Text = "Markers";

            SetupMarkerItem(addVerticalMarkerMenuItem, "New Horizontal Axis", "H", addVerticalMarkerMenuItem_Click);
            SetupMarkerItem(addDataMarkerMenuItem, "New Vertical Axis", "V", addDataMarkerMenuItem_Click);
            SetupMarkerItem(addHorizintalDeltaMenuItem, "New Time Delta", "D", addTimeDeltaToolStripMenuItem_Click);
            SetupMarkerItem(addAnnotationMenuItem, "New Text", "T", addAnnotationToolStripMenuItem_Click);
        }

        // single-key shortcuts (no Ctrl, mouse in the other hand): ToolStripMenuItem rejects a bare letter as ShortcutKeys,
        // so the menu only displays it and eScope.ProcessCmdKey dispatches the key
        private void SetupMarkerItem(ToolStripMenuItem item, string text, string shortcut, EventHandler handler)
        {
            item.Text = text;
            item.ShortcutKeyDisplayString = shortcut;
            item.Click += handler;
        }
        public void AddTimeMarker() => addVerticalMarkerMenuItem_Click(null, EventArgs.Empty);
        public void AddDataMarker() => addDataMarkerMenuItem_Click(null, EventArgs.Empty);
        public void AddTimeDelta() => addTimeDeltaToolStripMenuItem_Click(null, EventArgs.Empty);
        public void AddAnnotation() => addAnnotationToolStripMenuItem_Click(null, EventArgs.Empty);
        public bool Exists(string name)
        {
            foreach(var c in Ctrls)
            {
                if(c.DataBase.Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        // --- Generic Add/Delete/Edit methods ---
        public void Add(object data)
        {
            commandMgr.ExecuteCommand(new CmdAddObj(this, data));   
        }

        public void Delete(CtrlBase ctrl)
        {
            commandMgr.ExecuteCommand(new CmdDeleteObj(this, ctrl));
        }
        public void Rename(string name)
        {
            commandMgr.ExecuteCommand(new CmdRenameTrace(this, name));
        }
        internal void SetName(string name)
        {
            Data.Name = name;
            tabPage.Name = name;
            tabPage.Text = name;
        }
        public void UpdateMarkers()
        {
            foreach(var m in Ctrls)
            {
                if (m is MarkerCtrl)
                {
                    m.Update();
                }
            }
            Plot.watchLine.Update();
        }
        public void Edit(object ctrl)
        {
            ((CtrlBase)ctrl).Edit();
        }

        // --- Menu callbacks ---
        private void menuChannelsNew_Click(object? sender, EventArgs e) => AddNewItem(Data.Channels, "Channel", (d, trc) => new ChannelCtrl(d, trc));
        private void addVerticalMarkerMenuItem_Click(object? sender, EventArgs e) => AddNewItem(Data.Markers, "Marker", (d, trc) => new MarkerCtrl(d, trc), pos: Math.Round(Plot.watchLine.Data.Pos));
        private void addTimeDeltaToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            AddNewItem(Data.Deltas, "Delta", (d, trc) => new DeltaCtrl(d, trc), x1: Math.Round(Plot.watchLine.Data.Pos), x2: Math.Round(Plot.watchLine.Data.Pos + GetXGrid()));
        }
        private void addDataMarkerMenuItem_Click(object? sender, EventArgs e)
        {
            var signal = Plot.GetSelectedSignal();
            if (signal == null)
            {
                var signals = GetSignalList();
                if (signals.Count > 0)
                    signal = signals[0];
            }
            if (signal == null) return;

            // value under the mouse, relative to the channel offset
            var c = Plot.GetCoordinates(Plot.mousePixel, signal.s.Axes.XAxis, signal.s.Axes.YAxis);
            double value = c.Y - signal.SignalBase.YOffset * signal.SignalBase.YGain;
            double round = signal.SignalBase.YGain / 100;
            if (round > 0) value = Math.Round(value / round) * round;
            AddNewItem(Data.DataMarkers, "DataMarker", (d, trc) => new DataMarkerCtrl(d, trc), value: value, channel: signal.SignalBase.Name, color: signal.SignalBase.Color);
        }
        private void addAnnotationToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var cText = Plot.mouseCoordinates;
            var cTip = Plot.mouseCoordinates;
            var signal = Plot.GetSelectedSignal();
            string signalName = string.Empty;
            string color = string.Empty;

            /* get first signal as default */
            if (signal == null)
            {
                 var signals = GetSignalList();
                 if(signals.Count > 0)
                     signal = signals[0];
            }
            if (signal != null)
            {
                cTip = Plot.GetCoordinates(Plot.mousePixel, signal.s.Axes.XAxis, signal.s.Axes.YAxis);
                cTip.X -= signal.SignalBase.XOffset;
                cTip.Y -= signal.SignalBase.YOffset * signal.SignalBase.YGain;
                cText = cTip;
                cText.Y += signal.SignalBase.YGain;
                signalName = signal.SignalBase.Name; 
                color = signal.SignalBase.Color;
            }
            AddNewItem(Data.Annotations, "Annotation", (d, trc) => new AnnotationCtrl(d, trc), textX: cText.X, textY: cText.Y,
                tipX: cTip.X, tipY: cTip.Y, channel: signalName, color: color);
        }

        // --- Generic helper to add new items ---
        private void AddNewItem<T>(IList<T> list, string prefix,
            Func<T, TraceCtrl, CtrlBase> createCtrl, double pos = double.NaN,
            double x1 = double.NaN, double x2 = double.NaN, double textX = double.NaN, double textY = double.NaN, double tipX = double.NaN, double tipY = double.NaN, string channel = "", string color = "", double value = double.NaN)
            where T : new()
        {
            int id = list.Count + 1;
            dynamic item = new T();
            item.Name = prefix + id;

            if (!double.IsNaN(pos)) item.Pos = pos;
            if (!double.IsNaN(x1)) item.X1 = x1;
            if (!double.IsNaN(x2)) item.X2 = x2;
            if (!double.IsNaN(textX)) item.TextLocationX = textX;
            if (!double.IsNaN(tipX)) item.TipLocationX = tipX;
            if (!double.IsNaN(textY)) item.TextLocationY = textY;
            if (!double.IsNaN(tipY)) item.TipLocationY = tipY;
            if (!double.IsNaN(value)) item.Value = value;
            if (channel!= "") item.Channel = channel;
            if (color != "") item.Color = color;

            var dialog = CreateEditDialog(item);
            if (dialog.ShowDialog() != DialogResult.OK) return;

            list.Add(item);
            Add(item);
        }
        private Form CreateEditDialog(dynamic item)
        {
            return item switch
            {
                Channel c => new ChannelEdit(c, this),
                Marker m => new MarkerEdit(m, this),
                Plot.Delta d => new DeltaEdit(d, this),
                Annotation a => new AnnotationEdit(a, this),
                DataMarker dm => new DataMarkerEdit(dm, this),
                _ => throw new InvalidOperationException("Unknown type")
            };
        }
        public void Resize(int width, int height)
        {
            tabPage.Width = width;
            tabPage.Height = height;
            Plot.Resize(width, height);
        }
        public void Load()
        {
            Scope.menuStrip1.Items.Insert(3, ChannelsMenu);
            Scope.menuStrip1.Items.Insert(4, MarkersMenu); // Markers before View
        }
        public void UnLoad()
        {
            Scope.menuStrip1.Items.Remove(ChannelsMenu);
            Scope.menuStrip1.Items.Remove(MarkersMenu);
        }

        public double GetXGrid() => Data.XGridDecadeNb * Data.XGridDecadeVal;
        public List<ChannelCtrl>GetSignalList()
        {
            List<ChannelCtrl> signals = new List<ChannelCtrl>();

            foreach(var  c in Ctrls)
            {
                if (c is ChannelCtrl)
                {
                    signals.Add((ChannelCtrl)c);
                }
            }
            return signals;
        }
        public ChannelCtrl? GetSignalFromId(Guid? id)
        {
            if(id == null)
            {
                return null;
            }
            var signalList = GetSignalList();
            return signalList.FirstOrDefault(s => s.SignalBase.Id == id); ;
        }
        public ChannelCtrl? GetSignalFromName(string? name)
        {
            var signalList = GetSignalList();
            return signalList.FirstOrDefault(s => s.SignalBase.Name == name); ;
        }
        public void Start()
        {
            var channelList = Ctrls.OfType<ChannelCtrl>();
            foreach (ChannelCtrl c in channelList)
            {
                c.Start();
            }
            ToggleRecord();
        }
        public void Stop()
        {
            var channelList = Ctrls.OfType<ChannelCtrl>();
            foreach (ChannelCtrl c in channelList)
            {
                c.Stop();
            }
            ToggleRecord();
        }
        // shortcut (A): only with an active selection
        public void AnalyzeSelection()
        {
            if (Plot.dataSelector.IsVisible())
                Plot.OpenAnalyzer();
        }
        public void ToggleRecord()
        {
            Plot.RunStop();
        }
        public void SetPlay()
        {
            Plot.SetPlay();
        }
        public void SetPause()
        {
            Plot.SetPause();
        }        
        public void ZoomFit()
        {
            Plot.ZoomFit();
        }
        public void ZoomIn()
        {
            Plot.ZoomIn();
        }
        public void ZoomOut()
        {
            Plot.ZoomOut();
        }
        public void YZoomIn()
        {
            Plot.YZoomIn();
        }
        public void YZoomOut()
        {
            Plot.YZoomOut();
        }
        public void ZoomSelection()
        {
            Plot.ZoomSelection();
        }
        public void CopyData()
        {
            Plot.CopyData();
        }
        public void CutData()
        {
            Plot.CutData();
        }
        public void PasteData()
        {
            Plot.PasteData();
        }
        public void Delete()
        {
            Plot.Delete();
        }
        public void Select()
        {
            Plot.Select();
        }
        public void MoveBegin()
        {
            Plot.MoveBegin(); 
        }
        public void MoveEnd()
        {
            Plot.MoveEnd();
        }
        public void MoveForward()
        {
            Plot.MoveForward();
        }
        public void MoveBackward()
        {
            Plot.MoveBackward();
        }
        public void Refresh()
        {
            // channels without device are computed up to the display end (device channels on their frames)
            foreach (ChannelCtrl c in GetSignalList())
            {
                if (!c.SignalBase.hasDevice)
                    c.Update();
            }
            Plot.Refresh();
        }
        public void XGridIncrease()
        {
            if(Data.XGridDecadeVal == 5)
            {
                Data.XGridDecadeVal = 1;
                Data.XGridDecadeNb *= 10;
            }
            else if (Data.XGridDecadeVal == 2)
            {
                Data.XGridDecadeVal = 5;
            }
            else if (Data.XGridDecadeVal == 1)
            {
                Data.XGridDecadeVal = 2;
            }
        }
        public void XGridDecrease()
        {
            if (Data.XGridDecadeVal == 5)
            {
                Data.XGridDecadeVal = 2;
            }
            else if (Data.XGridDecadeVal == 2)
            {
                Data.XGridDecadeVal = 1;
            }
            else if (Data.XGridDecadeVal == 1)
            {
                Data.XGridDecadeVal = 5;
                if(Data.XGridDecadeNb < 10)
                {
                    Data.XGridDecadeNb = 1;
                }
                else
                {
                    Data.XGridDecadeNb /= 10;
                }
            }
        }
        public void WatchShow(List<DeviceCtrl> devices)
        {

            if (_watchForm == null || _watchForm.IsDisposed)
            {
                _watchForm = new Watch.View(Data.Watches, devices);
                _watchForm.FormClosed += (s, e) => _watchForm = null;
                _watchForm.Show();
            }
            else
            {
                _watchForm.BringToFront();  // déjà ouvert, ramène au premier plan
            }
        }

        public void WatchClose()
        {
            _watchForm?.Close();
        }

        // --- File operations ---

        // copy the live controls back into the serializable model
        public void SyncData()
        {
            Data.Channels.Clear();
            Data.Annotations.Clear();
            Data.Markers.Clear();
            Data.Deltas.Clear();
            Data.DataMarkers.Clear();

            foreach (CtrlBase c in Ctrls)
            {
                switch (c.DataBase)
                {
                    case Channel ch: Data.Channels.Add(ch); break;
                    case Marker m: Data.Markers.Add(m); break;
                    case Plot.Delta d: Data.Deltas.Add(d); break;
                    case Annotation a: Data.Annotations.Add(a); break;
                    case DataMarker dm: Data.DataMarkers.Add(dm); break;
                }
            }
        }
        public void SaveToFile(string path)
        {
            SyncData();
            // same temp-file strategy as the workspace: a failure keeps the previous file intact
            string tmpPath = path + ".tmp";
            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            {
                System.Text.Json.JsonSerializer.Serialize(gz, Data);
            }
            File.Move(tmpPath, path, true);
        }
        public static Trace LoadFromFile(string path)
        {
            using var fs = File.OpenRead(path);
            // gzip magic 1F 8B, otherwise legacy plain JSON trace
            bool gzip = fs.ReadByte() == 0x1F && fs.ReadByte() == 0x8B;
            fs.Position = 0;
            using Stream s = gzip ? new GZipStream(fs, CompressionMode.Decompress) : fs;
            return System.Text.Json.JsonSerializer.Deserialize<Trace>(s)
                ?? throw new InvalidDataException("Empty trace file");
        }
        // one Time/Data column pair per channel (+ Raw for device channels), invariant culture.
        // Samples plotted in [from, to] (full history by default); returns the number of data rows
        public int ExportCsv(string path, double from = double.NegativeInfinity, double to = double.PositiveInfinity)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var signals = GetSignalList();
            using var w = new StreamWriter(path, false, System.Text.Encoding.UTF8, 1 << 16);

            var header = new List<string>();
            foreach (var c in signals)
            {
                string unit = !string.IsNullOrEmpty(c.SignalBase.Unit) ? $" [{c.SignalBase.Unit}]" : string.Empty;
                header.Add($"{c.SignalBase.Name} Time");
                header.Add($"{c.SignalBase.Name} Data{unit}");
                if (c.SignalBase.hasDevice) header.Add($"{c.SignalBase.Name} Raw");
            }
            w.WriteLine(string.Join(",", header));

            // sample i is plotted at XCreation + XOffset + i * Period: first sample and count of each channel in range
            var first = new int[signals.Count];
            var count = new int[signals.Count];
            for (int k = 0; k < signals.Count; k++)
            {
                var c = signals[k];
                double period = c.s?.Data.Period ?? 1;
                double t0 = c.SignalBase.XCreation + c.SignalBase.XOffset;
                int f = double.IsNegativeInfinity(from) ? 0 : (int)Math.Max(0, Math.Ceiling((from - t0) / period - 1e-9));
                int l = double.IsPositiveInfinity(to) ? c.Y.Count - 1 : (int)Math.Min(c.Y.Count - 1, Math.Floor((to - t0) / period + 1e-9));
                first[k] = f;
                count[k] = Math.Max(0, l - f + 1);
            }
            int maxRows = signals.Count == 0 ? 0 : count.Max();
            var row = new List<string>(header.Count);
            for (int i = 0; i < maxRows; i++)
            {
                row.Clear();
                for (int k = 0; k < signals.Count; k++)
                {
                    var c = signals[k];
                    bool has = i < count[k];
                    int idx = first[k] + i;
                    double t = c.SignalBase.XCreation + c.SignalBase.XOffset + idx * (c.s?.Data.Period ?? 1);
                    row.Add(has ? t.ToString(ic) : string.Empty);
                    row.Add(has ? c.Y[idx].ToString(ic) : string.Empty);
                    if (c.SignalBase.hasDevice)
                        row.Add(has && idx < c.SignalBase.Y.Count ? c.SignalBase.Y[idx].ToString(ic) : string.Empty);
                }
                w.WriteLine(string.Join(",", row));
            }
            return maxRows;
        }
        // snapshot of the tab as displayed: plot + WinForms legend
        public void ExportPng(string path) => File.WriteAllBytes(path, RenderPng());

        public byte[] RenderPng()
        {
            using var bmp = new Bitmap(tabPage.ClientSize.Width, tabPage.ClientSize.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(tabPage.BackColor);

                // render the plot through ScottPlot: DrawToBitmap is not reliable on the Skia control
                var fp = Plot.GetFormsPlot();
                if (fp.Width > 0 && fp.Height > 0)
                {
                    byte[] png = fp.Plot.GetImage(fp.Width, fp.Height).GetImageBytes(ScottPlot.ImageFormat.Png);
                    using var ms = new MemoryStream(png);
                    using var plotImg = Image.FromStream(ms);
                    g.DrawImage(plotImg, fp.Bounds);
                }
            }
            // remaining controls (legend, timebase), back to front so the z-order is kept
            for (int i = tabPage.Controls.Count - 1; i >= 0; i--)
            {
                Control c = tabPage.Controls[i];
                if (c == Plot.GetControl() || !c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                c.DrawToBitmap(bmp, c.Bounds);
            }
            using var output = new MemoryStream();
            bmp.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            return output.ToArray();
        }
        // detach from devices and floating windows before the tab is dropped
        public void Close()
        {
            WatchClose();
            foreach (var c in GetSignalList())
            {
                c.Remove();
            }
        }
        // channels pointing to a device that is not configured in this workspace
        public List<string> MissingDevices(IEnumerable<string> knownDevices)
        {
            return Data.Channels
                .Where(c => c.hasDevice && !string.IsNullOrEmpty(c.Device) && !knownDevices.Contains(c.Device))
                .Select(c => c.Device)
                .Distinct()
                .ToList();
        }
    }
}
