using eScope.Automation;

namespace eScope
{
    // AI / script automation server (Automation/): menu, toolbar indicator, lifetime
    public partial class eScope
    {
        AutomationManager? automation;
        readonly ToolStripLabel automationLabel = new()
        {
            Alignment = ToolStripItemAlignment.Right,
            Visible = false,
        };

        internal bool IsRecording => bRecord;
        readonly System.Diagnostics.Stopwatch recordWatch = new(); // restarted when a recording starts
        internal TimeSpan? RecordElapsed => bRecord ? recordWatch.Elapsed : null;
        internal int RecordCount { get; private set; } // recordings started since launch

        // new empty trace, selected unless a recording is running (the current trace is then kept)
        internal TraceCtrl NewTrace(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                int index = tabControl1.TabCount + 1;
                while (Traces.Any(t => t.Data.Name == "Trace" + index)) index++;
                name = "Trace" + index;
            }
            var ctrl = new TraceCtrl(name.Trim(), this);
            AddTrace(ctrl);
            return ctrl;
        }

        // false while recording (the tab change is refused)
        internal bool SelectTrace(TraceCtrl trc)
        {
            tabControl1.SelectedTab = trc.tabPage;
            return GetCurrentTrace() == trc;
        }

        void InitAutomation()
        {
            automation = new AutomationManager(new AutomationService(this, new WinFormsDispatcher(this)));
            automation.StateChanged += () =>
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(UpdateAutomationLabel);
            };

            var settingsItem = new ToolStripMenuItem("AI automation...");
            settingsItem.Click += (_, _) => ShowAutomationSettings();
            var toolsMenu = new ToolStripMenuItem("Tools");
            toolsMenu.DropDownItems.Add(settingsItem);
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(aboutToolStripMenuItem), toolsMenu);

            automationLabel.Click += (_, _) => ShowAutomationSettings();
            menuStrip1.Items.Add(automationLabel);

            // the UI dispatcher needs the window handle
            Shown += (_, _) => automation.Start();
            FormClosed += (_, _) => automation.Dispose();
        }

        void ShowAutomationSettings()
        {
            using var form = new AutomationForm(automation!);
            form.ShowDialog(this);
        }

        // "AI" in the toolbar while the server listens, last tool called in the tooltip
        void UpdateAutomationLabel()
        {
            var a = automation!;
            automationLabel.Visible = a.IsRunning || a.Error != null;
            if (a.Error != null)
            {
                automationLabel.Text = "AI ⚠";
                automationLabel.ToolTipText = $"AI automation server error: {a.Error}";
                return;
            }
            automationLabel.Text = a.LastTool != null && DateTime.Now - a.LastCall < TimeSpan.FromSeconds(5)
                ? $"AI: {a.LastTool}"
                : "AI";
            automationLabel.ToolTipText = $"AI automation listening on {a.McpUrl}" +
                (a.LastTool != null ? $"\nLast call: {a.LastTool} at {a.LastCall:T}" : string.Empty);
        }
    }
}
