using eScope.Command;
using eScope.License;
using eScope.Plot;
using eScope.Update;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using static IronPython.Modules.PythonIterTools;

namespace eScope
{
    public partial class eScope : Form
    {
        internal static readonly string AppVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? "0.0.0";
        static eScopeModel Data = new();
        public static List<TraceCtrl> Traces = new();
        public List<DeviceCtrl> Devices = new();
        readonly System.Windows.Forms.Timer DeviceProcessTimer = new() { Interval = 50, Enabled = true };
        private bool bRecord = false;
        private System.Diagnostics.Stopwatch swTimer;
        private System.Diagnostics.Stopwatch Timer1s = new();
        private int recordBmpToggle = 0;
        private int playBmpToggle = 0;
        private TraceCtrl? loadedTrace = null; // trace whose menus are currently in menuStrip1
        private const string TraceFileFilter = "eScope trace (*.etrace)|*.etrace|All files (*.*)|*.*";
        private const string WorkspacePath = "eScope.json";
        private static readonly JsonSerializerOptions WorkspaceJsonOptions = new() { WriteIndented = true };
        private byte[]? savedHash; // SHA-256 of the workspace file as last loaded/saved, null if none
        private TextBox? renameBox; // in-place editor over a tab while renaming a trace
        private ToolStripButton? updateButton; // top right, shown only when a newer release is on GitHub
        private List<Release>? releases; // GitHub releases found at startup

        public eScope()
        {
            InitializeComponent();
            BuildLayout();
            DpiChanged += (_, _) => UpdateToolbarIcons();
            this.KeyPreview = true;
            this.FormClosing += eScope_FormClosing;
            // the recording applies to the current trace: it stays current until the recording stops
            tabControl1.Selecting += (_, e) => { if (bRecord) e.Cancel = true; };
            updateToolStripMenuItem.Click += updateToolStripMenuItem_Click;
            lIcenceToolStripMenuItem.Click += (_, _) =>
            {
                using var form = new LicenseForm();
                form.ShowDialog(this);
            };
            supportToolStripMenuItem.Click += (_, _) =>
            {
                using var form = new About.SupportForm();
                form.ShowDialog(this);
            };
            copyrightToolStripMenuItem.Click += (_, _) =>
            {
                using var form = new About.CopyrightForm();
                form.ShowDialog(this);
            };
            Shown += async (_, _) => await CheckUpdate();

            this.Text = $"eScope {AppVersion}";

            try
            {
                using var fs = File.OpenRead(WorkspacePath);
                Data = JsonSerializer.Deserialize<eScopeModel>(fs)!;
                savedHash = FileHash(WorkspacePath);
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message, "Cannot load workspace", MessageBoxButtons.OK);
                // set default Data if load fails 
                //return    
            }

            foreach (var data in Data.Devices)
            {
                DevicesAdd(data);
            }
            foreach (var data in Data.Traces)
            {
                TraceCtrl ctrl = new(data, this);
                this.tabControl1.Controls.Add(ctrl.tabPage);
                Traces.Add(ctrl);
            }
            if (tabControl1.TabCount == 0)
            {
                EventArgs e = new EventArgs();
                newTraceToolStripMenuItem_Click(0, e);
            }
            ActivateCurrentTrace();
            ApplyTheme();
            // single-key shortcuts need the focus on the plot, not on a side panel list
            Shown += (_, _) => GetCurrentTrace()?.Plot.GetControl().Focus();

            Timer1s.Start();

            // setup a timer to add data to the streamer periodically
            DeviceProcessTimer.Tick += (s, e) =>
            {
                long timerElapsed = 0;
                if(swTimer != null)
                {
                    swTimer.Stop();
                    timerElapsed = swTimer.ElapsedMilliseconds;
                }
                swTimer = System.Diagnostics.Stopwatch.StartNew();
                var swDevice = System.Diagnostics.Stopwatch.StartNew();
                foreach (DeviceCtrl ctrl in Devices)
                {
                    ctrl.Process();
                }
                swDevice.Stop();

                var swPlot = System.Diagnostics.Stopwatch.StartNew();
                TraceCtrl? trc = GetCurrentTrace();
                trc?.Refresh();
                swPlot.Stop();
                UpdateTimebase();

                if(Timer1s.ElapsedMilliseconds > 500)
                {
                    Timer1s.Restart();
                    if (automation?.IsRunning == true) UpdateAutomationLabel(); // "AI: <tool>" back to "AI"
                    UpdateStatus();
                    if(bRecord)
                    {
                        recordBmpToggle = 1 - recordBmpToggle;
                        UpdateToolbarIcons();
                    }

                }

                //Console.WriteLine($"Timer {timerElapsed}ms, Plot {swPlot.ElapsedMilliseconds}ms | Device {swDevice.ElapsedMilliseconds}ms");
            };
            LicenseManager.StateChanged += License_StateChanged;
            License_StateChanged(null, EventArgs.Empty);
            commandMgr.StateChanged += commandMgr_StateChanged;
            commandMgr_StateChanged(null, EventArgs.Empty);
            InitAutomation();

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.WriteLine($"Unhandled Exception: {((Exception)e.ExceptionObject).Message}");
                Dispose();
            };
        }
        public void Dispose()
        {
            foreach (DeviceCtrl d in Devices)
            {
                d.Protocol?.Dispose();
            }
        }
        public void ToggleRecord()
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            bRecord = !bRecord;
            recordBmpToggle = 1;
            UpdateToolbarIcons();
            UpdateTabStrip();
            if (bRecord == false)
            {
                ctrl?.Stop();
                SetPause();
            }
            else
            {
                recordWatch.Restart();
                RecordCount++;
                ctrl?.Start();
                SetPlay();
            }
        }
        public bool Save()
        {
            // Display a confirmation dialog
            DialogResult result = MessageBox.Show("Save workspace?", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                return true;
            }
            else if (result == DialogResult.Yes)
            {
                WriteWorkspace();
            }
            return false;
        }
        // saves the workspace without confirmation, returns the full path of the file
        internal string WriteWorkspace()
        {
            SyncWorkspace();
            // stream directly to disk: large traces overflow a single in-memory string
            // write to a temp file first so a failure does not corrupt the existing workspace
            string tmpPath = WorkspacePath + ".tmp";
            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            {
                JsonSerializer.Serialize(fs, Data, WorkspaceJsonOptions);
            }
            File.Move(tmpPath, WorkspacePath, true);
            savedHash = FileHash(WorkspacePath);
            return Path.GetFullPath(WorkspacePath);
        }

        // copy the live traces and devices back into the serializable model
        private void SyncWorkspace()
        {
            Data.Traces.Clear();
            foreach (TraceCtrl t in Traces)
            {
                t.SyncData();
                Data.Traces.Add(t.Data);
            }
            Data.Devices.Clear();
            foreach (DeviceCtrl d in Devices)
            {
                Data.Devices.Add(d.Data);
            }
        }

        // true if saving now would write something different from the last loaded/saved workspace file
        private bool IsModified()
        {
            if (savedHash == null) return true;
            SyncWorkspace();
            // hash the serialization on the fly: large traces overflow a single in-memory string
            using var sha = SHA256.Create();
            using (var cs = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            {
                JsonSerializer.Serialize(cs, Data, WorkspaceJsonOptions);
            }
            return !sha.Hash!.AsSpan().SequenceEqual(savedHash);
        }

        private static byte[]? FileHash(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                return SHA256.HashData(fs);
            }
            catch
            {
                return null;
            }
        }

        public DeviceCtrl? GetDevice(string DeviceName)
        {
            foreach (DeviceCtrl ctrl in Devices)
            {
                if (DeviceName == ctrl.Data.Name)
                {
                    return ctrl;
                }
            }
            return null;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActivateCurrentTrace();
        }

        // swap the per-trace Channels/Markers menus to the selected tab
        private void ActivateCurrentTrace()
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            if (ctrl == loadedTrace) return;
            loadedTrace?.UnLoad();
            loadedTrace = ctrl;
            foreach (DeviceCtrl d in Devices)
            {
                d.SetActiveTrace(loadedTrace);
            }
            loadedTrace?.Load();
            ShowSidePanel(loadedTrace);
            UpdateTabStrip();
        }

        public TraceCtrl? GetCurrentTrace()
        {
            int idx = tabControl1.SelectedIndex;
            if (idx >= 0 && idx < Traces.Count)
            {
                return Traces[idx];
            }
            else
            {
                return null;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Right:
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                    return true;
                case Keys.Shift | Keys.Right:
                case Keys.Shift | Keys.Left:
                case Keys.Shift | Keys.Up:
                case Keys.Shift | Keys.Down:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //Console.WriteLine($"Key press {keyData}");

            // keys go to the text being typed (tab rename, watch expression...), not to the scope shortcuts
            if (TextInputFocused())
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            TraceCtrl? ctrl = GetCurrentTrace();

            if (keyData == (Keys.Space))
            {
                ToggleRecord();
            }
            else if (keyData == (Keys.P))
            {
                PlayButton_Click(null, null);
            }
            else if (keyData == (Keys.W))
            {
                ctrl?.WatchShow(Devices);
            }
            else if (keyData == (Keys.Control | Keys.S))
            {
                Save();
            }
            else if (keyData == (Keys.Control | Keys.C))
            {
                ctrl?.CopyData();
            }
            else if (keyData == (Keys.Control | Keys.X))
            {
                ctrl?.CutData();
            }
            else if (keyData == (Keys.Control | Keys.V))
            {
                ctrl?.PasteData();
            }
            else if (keyData == (Keys.Delete))
            {
                ctrl?.Delete();
            }
            else if (keyData == (Keys.S))
            {
                ctrl?.Select();
            }
            else if ((keyData == (Keys.Control | Keys.Right)) || (keyData == Keys.E) || (keyData == Keys.End))
            {
                ctrl?.MoveEnd();
            }
            else if ((keyData == (Keys.Control | Keys.Left)) || (keyData == Keys.B) || (keyData == Keys.Home))
            {
                ctrl?.MoveBegin();
            }
            else if ((keyData == (Keys.Right)) || (keyData == Keys.F))
            {
                ctrl?.MoveForward();
            }
            else if ((keyData == (Keys.Left)) || (keyData == Keys.B))
            {
                ctrl?.MoveBackward();
            }
            else if (keyData == (Keys.Escape))
            {
                ctrl?.ZoomFit();
            }
            else if (keyData == (Keys.Z))
            {
                ctrl?.ZoomSelection();
            }
            else if (keyData == (Keys.A))
            {
                ctrl?.AnalyzeSelection();
            }
            else if (keyData == (Keys.H))
            {
                ctrl?.AddTimeMarker();
            }
            else if (keyData == (Keys.V))
            {
                ctrl?.AddDataMarker();
            }
            else if (keyData == (Keys.D))
            {
                ctrl?.AddTimeDelta();
            }
            else if (keyData == (Keys.T))
            {
                ctrl?.AddAnnotation();
            }
            else if (keyData == (Keys.Control | Keys.Up))
            {
                ctrl?.YZoomIn();
            }
            else if (keyData == (Keys.Control | Keys.Down))
            {
                ctrl?.YZoomOut();
            }
            else if ((keyData == (Keys.Up)) || (keyData == Keys.I))
            {
                ctrl?.ZoomIn();
            }
            else if ((keyData == (Keys.Down)) || (keyData == Keys.O))
            {
                ctrl?.ZoomOut();
            }
            else
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }
        private void eScope_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Down:
                case Keys.Up:
                    e.IsInputKey = true;
                    break;
            }
        }
        private void eScope_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Check if the user clicked the close button or if the form is being closed programmatically
            if (e.CloseReason == CloseReason.UserClosing && IsModified())
            {
                e.Cancel = Save();
            }
            // Add code here to perform any necessary cleanup tasks before closing the form
            Dispose();
        }

        private void DevicesAdd(Device dev)
        {
            // check if already device name is already present 
            if (devicesMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == dev.Name) == false)
            {
                ToolStripMenuItem item = new System.Windows.Forms.ToolStripMenuItem();
                item.Name = "menuDevices" + dev.Name;
                item.Size = new System.Drawing.Size(180, 22);
                item.Text = dev.Name;
                item.Click += new System.EventHandler(this.menuDevicesEdit_Click);
                item.Tag = dev;
                devicesMenu.DropDownItems.Add(item);

                DeviceCtrl ctrl = new(dev);
                ctrl.SetActiveTrace(loadedTrace);
                Devices.Add(ctrl);
            }
            else
            {
                DeviceCtrl? devCtrl = Devices.FirstOrDefault(i => i.Data.Name == dev.Name);
                devCtrl?.Configure(dev);
            }

        }
        private void menuDevicesEdit(Device dev)
        {
            DeviceEdit dialog = new DeviceEdit(dev);
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                DevicesAdd(dev);
            }
        }
        private void menuDevicesEdit_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem? item = sender as ToolStripMenuItem;
            if (item != null)
            {
                Device? dev = item.Tag as Device;
                if (dev != null)
                {
                    menuDevicesEdit(dev);
                }
            }
        }
        private void menuDevicesNew_Click(object sender, EventArgs e)
        {
            int id = Data.Devices.Count + 1;
            Device dev = new Device();
            dev.Name = "Device" + id.ToString();
            menuDevicesEdit(dev);
        }
        private void toolStripButtonRec_Click(object sender, EventArgs e)
        {
            ToggleRecord();
        }
        private void toolStripButtonForward_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveForward();
        }
        private void toolStripButtonEnd_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveEnd();
        }

        private void toolStripButtonRewind_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveBackward();
        }
        private void toolStripButtonBegin_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveBegin();
        }
        // --- Files menu ---
        private string UniqueTraceName(string baseName)
        {
            string name = baseName;
            for (int i = 2; Traces.Any(t => t.Data.Name == name); i++)
            {
                name = $"{baseName} ({i})";
            }
            return name;
        }
        private void AddTrace(TraceCtrl ctrl)
        {
            tabControl1.Controls.Add(ctrl.tabPage);
            Traces.Add(ctrl);
            tabControl1.SelectedTab = ctrl.tabPage;
            ActivateCurrentTrace();
            UpdateTabStrip();
        }
        // double-click on a tab: rename the trace in place (Enter or focus loss applies, Escape cancels)
        private void BeginRenameTrace(TraceCtrl trc, Rectangle tabRect)
        {
            EndRenameTrace(false);
            var box = new TextBox
            {
                Text = trc.Data.Name,
                Location = PointToClient(tabStrip.PointToScreen(new Point(tabRect.X + this.Px(8), tabRect.Y + this.Px(4)))),
                Width = Math.Max(tabRect.Width, this.Px(140)),
                Tag = trc,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Current.Input,
                ForeColor = Theme.Current.Text,
            };
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    EndRenameTrace(e.KeyCode == Keys.Enter);
                }
            };
            box.LostFocus += (s, e) => EndRenameTrace(true);
            renameBox = box;
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            box.SelectAll();
        }
        private void EndRenameTrace(bool apply)
        {
            TextBox? box = renameBox;
            if (box == null) return;
            renameBox = null; // removing the box raises LostFocus again
            var trc = (TraceCtrl)box.Tag!;
            string name = box.Text.Trim();
            Controls.Remove(box);
            BeginInvoke(box.Dispose); // may be called from the box's own event handler

            if (!apply || name.Length == 0 || name == trc.Data.Name) return;
            if (Traces.Any(t => t != trc && t.Data.Name == name))
            {
                MessageBox.Show($"A trace named '{name}' already exists.", "Rename Trace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            trc.Rename(name);
        }
        private void newTraceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = tabControl1.TabCount + 1;
            while (Traces.Any(t => t.Data.Name == "Trace" + index)) index++;
            AddTrace(new TraceCtrl("Trace" + index.ToString(), this));
        }
        private void openTraceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog { Filter = TraceFileFilter, Title = "Open Trace" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            Trace data;
            try
            {
                data = TraceCtrl.LoadFromFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot open trace", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var (_, missing) = AddTraceFromFile(data);
            if (missing.Count != 0)
            {
                MessageBox.Show($"Device(s) not configured in this workspace: {string.Join(", ", missing)}\n" +
                    "Recorded data is shown, but these channels cannot acquire until the device is added.",
                    "Open Trace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        // trace read from a file, added under a unique name; returns it with the devices it needs that the workspace lacks
        internal (TraceCtrl Trace, List<string> MissingDevices) AddTraceFromFile(Trace data)
        {
            data.Name = UniqueTraceName(data.Name);
            var ctrl = new TraceCtrl(data, this);
            AddTrace(ctrl);
            return (ctrl, ctrl.MissingDevices(Devices.Select(d => d.Data.Name)));
        }
        // returns false if the user cancelled
        private bool SaveTraceAs(TraceCtrl ctrl)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = TraceFileFilter,
                Title = $"Save Trace '{ctrl.Data.Name}'",
                FileName = ctrl.Data.Name + ".etrace",
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            try
            {
                ctrl.SaveToFile(dialog.FileName);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot save trace", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        private void saveTraceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            if (ctrl != null) SaveTraceAs(ctrl);
        }
        private void closeTraceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            if (ctrl == null) return;

            DialogResult result = MessageBox.Show($"Save trace '{ctrl.Data.Name}' to a file before closing?",
                "Close Trace", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes && !SaveTraceAs(ctrl)) return;

            if (bRecord) ToggleRecord();
            ctrl.UnLoad();
            loadedTrace = null;
            ctrl.Close();
            Traces.Remove(ctrl);
            tabControl1.TabPages.Remove(ctrl.tabPage);
            ctrl.tabPage.Dispose();
            // stacked commands may reference the closed trace
            commandMgr.Clear();

            if (Traces.Count == 0)
            {
                newTraceToolStripMenuItem_Click(this, EventArgs.Empty);
            }
            ActivateCurrentTrace();
            UpdateTabStrip();
        }
        private void exportTraceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            if (ctrl == null) return;

            using var dialog = new SaveFileDialog
            {
                Filter = "CSV data (*.csv)|*.csv|PNG image (*.png)|*.png",
                Title = $"Export Trace '{ctrl.Data.Name}'",
                FileName = ctrl.Data.Name,
                AddExtension = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (Path.GetExtension(dialog.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl.ExportPng(dialog.FileName);
                }
                else
                {
                    ctrl.ExportCsv(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot export trace", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.CutData();
        }
        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.CopyData();
        }
        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.PasteData();
        }
        private void selectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.Select();
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();

            if(playBmpToggle == 0)
            {
                SetPlay();
            }
            else
            {
                SetPause();
            }
        }
        private void SetPlay()
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.SetPlay();
            playBmpToggle = 1;
            UpdateToolbarIcons();
        }
        private void SetPause()
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.SetPause();
            playBmpToggle = 0;
            UpdateToolbarIcons();
        }
        // transport: "Record" becomes a red "Recording 00:12" with a blinking disc, "Play" a filled "Pause"
        private void UpdateToolbarIcons()
        {
            var p = Theme.Current;
            int dpi = DeviceDpi;
            toolStripButtonBegin.Image = Icons.Get(Icons.Begin, p.Text2, dpi);
            toolStripButtonRewind.Image = Icons.Get(Icons.Rewind, p.Text2, dpi);
            toolStripButtonForward.Image = Icons.Get(Icons.Forward, p.Text2, dpi);
            toolStripButtonEnd.Image = Icons.Get(Icons.End, p.Text2, dpi);
            toolStripButtonRec.Image = Icons.Get(Icons.Record,
                !bRecord || recordBmpToggle == 1 ? p.Rec : Theme.Blend(p.Rec, p.RecBg, 0.35f), dpi);
            toolStripButtonRec.Tag = bRecord ? "rec" : "outline";
            toolStripButtonRec.Text = bRecord ? $"Recording {recordWatch.Elapsed:mm\\:ss}" : "Record";
            bool playing = playBmpToggle == 1;
            PlayButton.Image = playing ? Icons.Get(Icons.Pause, p.OnAccent, dpi) : Icons.Get(Icons.Play, p.Text2, dpi);
            PlayButton.Tag = playing ? "accent" : "outline";
            PlayButton.Text = playing ? "Pause" : "Play";
            themeButton.Image = Icons.Get(Theme.IsDark ? Icons.Sun : Icons.Moon, p.Text2, dpi);
            menuStrip1.Invalidate();
        }
        private void commandMgr_StateChanged(object? sender, EventArgs e)
        {
            undoToolStripMenuItem.Enabled = commandMgr.CanUndo;
            redoToolStripMenuItem.Enabled = commandMgr.CanRedo;

            undoToolStripMenuItem.Text = commandMgr.UndoLabel;
            redoToolStripMenuItem.Text = commandMgr.RedoLabel;
        }
        private void License_StateChanged(object? sender, EventArgs e)
        {
            this.Text = $"eScope {AppVersion} {LicenseManager.GetLicenseStatusText()}";
        }
        // silent startup check: shows the update button when a newer stable release exists
        private async Task CheckUpdate()
        {
            try
            {
                releases = await GitHubReleases.ListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update check failed: {ex.Message}");
                return;
            }
            Release? latest = GitHubReleases.Latest(releases);
            if (IsDisposed || latest == null || !GitHubReleases.IsNewer(latest)) return;

            updateButton = new ToolStripButton
            {
                Text = $"⬆ {latest.Version}",
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = Theme.Current.Ok,
                Tag = "outline",
                ToolTipText = $"eScope {latest.Version} is available (installed {AppVersion})",
            };
            updateButton.Click += updateToolStripMenuItem_Click;
            menuStrip1.Items.Add(updateButton);
        }

        private void updateToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var form = new UpdateForm(releases);
            if (form.ShowDialog(this) != DialogResult.OK || form.DownloadedDir == null) return;

            // the usual close path (save prompt), the install runs once the process has exited
            Close();
            if (IsDisposed)
                GitHubReleases.InstallOnExit(form.DownloadedDir);
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            commandMgr.Undo();
        }
        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            commandMgr.Redo();
        }
        private void updateRedo(string label)
        {
            if (label.Length != 0)
            {
                redoToolStripMenuItem.Text = $"Undo {label}";
                redoToolStripMenuItem.Enabled = true;
            }
            else
            {
                redoToolStripMenuItem.Enabled = false;
            }
        }
        private void updateUndo(string label)
        {
            if (label.Length != 0)
            {
                undoToolStripMenuItem.Text = $"Undo {label}";
                undoToolStripMenuItem.Enabled = true;
            }
            else
            {
                undoToolStripMenuItem.Enabled = false;
            }
        }
        private void copyToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.CopyData();
        }

        private void cutToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.CutData();
        }

        private void pasteToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.PasteData();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.Delete();
        }
        private void selectToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.Select();
        }

        private void zoomFitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.ZoomFit();
        }

        private void zoomInToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.ZoomIn();
        }

        private void zoomOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.ZoomOut();
        }

        private void moveBeginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveBegin();
        }

        private void moveEndToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveEnd();
        }

        private void moveForwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveForward();
        }

        private void moveBackwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.MoveBackward();
        }

        private void analyzerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.Plot.OpenAnalyzer();
        }

        private void watchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.WatchShow(Devices);
        }

        private void zoomSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TraceCtrl? ctrl = GetCurrentTrace();
            ctrl?.ZoomSelection();
        }
    }
    public class eScopeModel
    {
        public List<Device> Devices { get; set; }
        public List<Trace> Traces { get; set; }

        public eScopeModel()
        {
            Devices = new();
            Traces = new();
        }
    }

}
