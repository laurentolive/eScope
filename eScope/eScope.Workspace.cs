using System.Diagnostics;

namespace eScope
{
    // Workspace file (.escope, JSON of eScopeModel): devices, traces, channels. Relative paths inside it (symbol
    // file...) are relative to its folder, which becomes the current directory.
    // Opening another workspace restarts eScope on it: protocols, COM ports, GDB and the automation server are
    // released by the process exit instead of being torn down one by one.
    public partial class eScope
    {
        internal const string WorkspaceExt = ".escope";
        private const string TraceExt = ".etrace";
        private const string LegacyWorkspace = "eScope.json"; // before .escope: loaded from the current folder
        private const string WorkspaceFileFilter = "eScope workspace (*.escope)|*.escope";
        private const string OpenFileFilter = "eScope files (*.escope, *.etrace)|*.escope;*.etrace;*.json|" +
            "Workspace (*.escope)|*.escope|Trace (*.etrace)|*.etrace|All files (*.*)|*.*";
        private const string ArgNew = "--new";   // start on an empty workspace
        private const string ArgWait = "--wait"; // pid of the instance being replaced, wait for its exit

        private string? workspacePath; // full path, null for a new workspace never saved
        private bool restarting;       // closing to reopen eScope on another workspace: saving already asked
        private readonly ToolStripMenuItem newWorkspaceMenuItem = new("New Workspace");
        private readonly ToolStripMenuItem saveWorkspaceMenuItem = new("Save Workspace") { ShortcutKeys = Keys.Control | Keys.S };
        private readonly ToolStripMenuItem saveWorkspaceAsMenuItem = new("Save Workspace As...");

        internal string? WorkspacePath => workspacePath;

        // workspace opened at startup: the command line, else eScope.json of the current folder (shortcut
        // "Start in" of the older versions), else the last workspace opened, else a new one
        internal static string? StartupWorkspace(string[] args)
        {
            string? path = null;
            bool isNew = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == ArgWait && i + 1 < args.Length)
                    WaitExit(args[++i]);
                else if (args[i] == ArgNew)
                    isNew = true;
                else
                    path ??= args[i];
            }
            if (isNew)
                return null;
            if (path != null)
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
                MessageBox.Show($"Workspace not found: {path}", "eScope", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (File.Exists(LegacyWorkspace))
                return Path.GetFullPath(LegacyWorkspace);
            string? last = UiSettings.Current.LastWorkspace;
            return !string.IsNullOrEmpty(last) && File.Exists(last) ? last : null;
        }

        static void WaitExit(string pid)
        {
            try
            {
                if (int.TryParse(pid, out int id))
                    Process.GetProcessById(id).WaitForExit(10000);
            }
            catch (ArgumentException) { } // already gone
        }

        void InitWorkspaceMenu()
        {
            openTraceToolStripMenuItem.Text = "Open...";
            openTraceToolStripMenuItem.ToolTipText = "Workspace (.escope) or trace (.etrace); files can also be dropped on the window";
            newWorkspaceMenuItem.Click += (_, _) => OpenWorkspace(null);
            saveWorkspaceMenuItem.Click += (_, _) => SaveWorkspace();
            saveWorkspaceAsMenuItem.Click += (_, _) => SaveWorkspaceAs();

            var items = filesToolStripMenuItem1.DropDownItems;
            items.Insert(items.IndexOf(openTraceToolStripMenuItem), newWorkspaceMenuItem);
            int save = items.IndexOf(saveTraceToolStripMenuItem);
            items.Insert(save, saveWorkspaceAsMenuItem);
            items.Insert(save, saveWorkspaceMenuItem);

            AllowDrop = true;
            DragEnter += (_, e) => e.Effect = DroppedFiles(e).Any(f => IsWorkspaceFile(f) || IsTraceFile(f)) ? DragDropEffects.Copy : DragDropEffects.None;
            DragDrop += (_, e) => BeginInvoke(() => OpenFiles(DroppedFiles(e))); // dialogs out of the drag loop
        }

        static string[] DroppedFiles(DragEventArgs e) => e.Data?.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
        static bool IsWorkspaceFile(string f) => f.EndsWith(WorkspaceExt, StringComparison.OrdinalIgnoreCase)
                                               || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        static bool IsTraceFile(string f) => f.EndsWith(TraceExt, StringComparison.OrdinalIgnoreCase);

        // a workspace replaces the current one (the other files are ignored), traces are added to it
        void OpenFiles(IEnumerable<string> files)
        {
            var list = files.ToList();
            string? workspace = list.FirstOrDefault(IsWorkspaceFile);
            if (workspace != null)
            {
                OpenWorkspace(workspace);
                return;
            }
            foreach (string f in list.Where(IsTraceFile))
                OpenTraceFile(f);
        }

        void SetWorkspacePath(string? path)
        {
            workspacePath = path;
            if (path != null)
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(path)!);
                UiSettings.Current.LastWorkspace = path;
                UiSettings.Current.Save();
            }
            if (statusBar != null)
                statusRight.Text = path ?? "New workspace (not saved)";
            UpdateTitle();
        }

        void UpdateTitle()
        {
            string file = workspacePath != null ? Path.GetFileName(workspacePath) + " - " : string.Empty;
            Text = $"{file}eScope {AppVersion} {License.LicenseManager.GetLicenseStatusText()}".TrimEnd();
        }

        // another workspace (null: a new one): eScope restarts on it once the current one is saved or dropped
        void OpenWorkspace(string? path)
        {
            if (path != null)
            {
                path = Path.GetFullPath(path);
                if (string.Equals(path, workspacePath, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            if (IsModified() && Save())
                return; // cancelled

            string exe = Environment.ProcessPath!;
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Directory.GetCurrentDirectory() };
            if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                psi.ArgumentList.Add(typeof(eScope).Assembly.Location); // started as "dotnet eScope.dll"
            psi.ArgumentList.Add(ArgWait);
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            psi.ArgumentList.Add(path ?? ArgNew);
            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot open workspace", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            restarting = true;
            Close();
        }

        // saves to the workspace file, asking for one if the workspace has none; false if cancelled
        bool SaveWorkspace()
        {
            if (workspacePath == null)
                return SaveWorkspaceAs();
            WriteWorkspace();
            return true;
        }

        bool SaveWorkspaceAs()
        {
            using var dialog = new SaveFileDialog
            {
                Filter = WorkspaceFileFilter,
                Title = "Save Workspace As",
                InitialDirectory = Directory.GetCurrentDirectory(),
                FileName = (workspacePath != null ? Path.GetFileNameWithoutExtension(workspacePath) : "workspace") + WorkspaceExt,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;
            // relative paths follow the folder of the file: kept valid when it changes
            string dir = Path.GetDirectoryName(Path.GetFullPath(dialog.FileName))!;
            if (!string.Equals(dir, Directory.GetCurrentDirectory(), StringComparison.OrdinalIgnoreCase))
            {
                foreach (DeviceCtrl d in Devices)
                {
                    d.Data.SymbolFilePath = AbsolutePath(d.Data.SymbolFilePath);
                    d.Data.GdbFilePath = AbsolutePath(d.Data.GdbFilePath);
                }
            }
            SetWorkspacePath(Path.GetFullPath(dialog.FileName));
            WriteWorkspace();
            return true;
        }

        static string AbsolutePath(string path) => string.IsNullOrEmpty(path) || Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }
}
