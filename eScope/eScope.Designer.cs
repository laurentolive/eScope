namespace eScope
{
    partial class eScope
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(eScope));
            tabControl1 = new TabControl();
            filesToolStripMenuItem1 = new ToolStripMenuItem();
            newTraceToolStripMenuItem = new ToolStripMenuItem();
            openTraceToolStripMenuItem = new ToolStripMenuItem();
            closeTraceToolStripMenuItem = new ToolStripMenuItem();
            saveTraceToolStripMenuItem = new ToolStripMenuItem();
            exportTraceToolStripMenuItem = new ToolStripMenuItem();
            quitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem1 = new ToolStripMenuItem();
            undoToolStripMenuItem = new ToolStripMenuItem();
            redoToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            toolStripSeparatorFiles1 = new ToolStripSeparator();
            toolStripSeparatorFiles2 = new ToolStripSeparator();
            toolStripSeparatorFiles3 = new ToolStripSeparator();
            copyToolStripMenuItem = new ToolStripMenuItem();
            cutToolStripMenuItem = new ToolStripMenuItem();
            pasteToolStripMenuItem = new ToolStripMenuItem();
            deleteToolStripMenuItem = new ToolStripMenuItem();
            selectToolStripMenuItem = new ToolStripMenuItem();
            deselectEchapToolStripMenuItem = new ToolStripMenuItem();
            devicesMenu = new ToolStripMenuItem();
            menuDevicesNew = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            zoomInToolStripMenuItem1 = new ToolStripMenuItem();
            zoomOutToolStripMenuItem = new ToolStripMenuItem();
            zoomFitToolStripMenuItem = new ToolStripMenuItem();
            zoomSelectionToolStripMenuItem = new ToolStripMenuItem();
            analyzerToolStripMenuItem = new ToolStripMenuItem();
            watchToolStripMenuItem = new ToolStripMenuItem();
            moveBeginToolStripMenuItem = new ToolStripMenuItem();
            moveEndToolStripMenuItem = new ToolStripMenuItem();
            moveForwardToolStripMenuItem = new ToolStripMenuItem();
            moveBackwardToolStripMenuItem = new ToolStripMenuItem();
            textToolStripMenuItem2 = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            lIcenceToolStripMenuItem = new ToolStripMenuItem();
            supportToolStripMenuItem = new ToolStripMenuItem();
            updateToolStripMenuItem = new ToolStripMenuItem();
            copyrightToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1 = new MenuStrip();
            toolStrip1 = new ToolStrip();
            toolStripButtonBegin = new ToolStripButton();
            toolStripButtonRewind = new ToolStripButton();
            toolStripButtonRec = new ToolStripButton();
            PlayButton = new ToolStripButton();
            toolStripButtonForward = new ToolStripButton();
            toolStripButtonEnd = new ToolStripButton();
            menuStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Alignment = TabAlignment.Bottom;
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 30);
            tabControl1.Margin = new Padding(3, 4, 3, 4);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(914, 570);
            tabControl1.TabIndex = 0;
            tabControl1.SelectedIndexChanged += tabControl1_SelectedIndexChanged;
            tabControl1.Resize += tabControl1_Resize;
            // 
            // filesToolStripMenuItem1
            // 
            filesToolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { newTraceToolStripMenuItem, openTraceToolStripMenuItem, toolStripSeparatorFiles1, saveTraceToolStripMenuItem, exportTraceToolStripMenuItem, toolStripSeparatorFiles2, closeTraceToolStripMenuItem, toolStripSeparatorFiles3, quitToolStripMenuItem });
            filesToolStripMenuItem1.Name = "filesToolStripMenuItem1";
            filesToolStripMenuItem1.Size = new Size(52, 24);
            filesToolStripMenuItem1.Text = "Files";
            // 
            // newTraceToolStripMenuItem
            // 
            newTraceToolStripMenuItem.Name = "newTraceToolStripMenuItem";
            newTraceToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newTraceToolStripMenuItem.Size = new Size(260, 26);
            newTraceToolStripMenuItem.Text = "New Trace";
            newTraceToolStripMenuItem.Click += newTraceToolStripMenuItem_Click;
            // 
            // openTraceToolStripMenuItem
            // 
            openTraceToolStripMenuItem.Name = "openTraceToolStripMenuItem";
            openTraceToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openTraceToolStripMenuItem.Size = new Size(260, 26);
            openTraceToolStripMenuItem.Text = "Open Trace...";
            openTraceToolStripMenuItem.Click += openTraceToolStripMenuItem_Click;
            // 
            // closeTraceToolStripMenuItem
            // 
            closeTraceToolStripMenuItem.Name = "closeTraceToolStripMenuItem";
            closeTraceToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.W;
            closeTraceToolStripMenuItem.Size = new Size(260, 26);
            closeTraceToolStripMenuItem.Text = "Close Trace";
            closeTraceToolStripMenuItem.Click += closeTraceToolStripMenuItem_Click;
            // 
            // saveTraceToolStripMenuItem
            // 
            saveTraceToolStripMenuItem.Name = "saveTraceToolStripMenuItem";
            saveTraceToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveTraceToolStripMenuItem.Size = new Size(260, 26);
            saveTraceToolStripMenuItem.Text = "Save Trace As...";
            saveTraceToolStripMenuItem.Click += saveTraceToolStripMenuItem_Click;
            // 
            // exportTraceToolStripMenuItem
            // 
            exportTraceToolStripMenuItem.Name = "exportTraceToolStripMenuItem";
            exportTraceToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.E;
            exportTraceToolStripMenuItem.Size = new Size(260, 26);
            exportTraceToolStripMenuItem.Text = "Export Trace (CSV, PNG)...";
            exportTraceToolStripMenuItem.Click += exportTraceToolStripMenuItem_Click;
            // 
            // quitToolStripMenuItem
            // 
            quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            quitToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
            quitToolStripMenuItem.Size = new Size(260, 26);
            quitToolStripMenuItem.Text = "Quit";
            quitToolStripMenuItem.Click += quitToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem1
            // 
            editToolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { undoToolStripMenuItem, redoToolStripMenuItem, toolStripSeparator1, copyToolStripMenuItem, cutToolStripMenuItem, pasteToolStripMenuItem, deleteToolStripMenuItem, selectToolStripMenuItem, deselectEchapToolStripMenuItem });
            editToolStripMenuItem1.Name = "editToolStripMenuItem1";
            editToolStripMenuItem1.Size = new Size(49, 24);
            editToolStripMenuItem1.Text = "Edit";
            // 
            // undoToolStripMenuItem
            // 
            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            undoToolStripMenuItem.Size = new Size(214, 26);
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += undoToolStripMenuItem_Click;
            // 
            // redoToolStripMenuItem
            // 
            redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            redoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
            redoToolStripMenuItem.Size = new Size(214, 26);
            redoToolStripMenuItem.Text = "Redo";
            redoToolStripMenuItem.Click += redoToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(211, 6);
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
            copyToolStripMenuItem.Size = new Size(214, 26);
            copyToolStripMenuItem.Text = "Copy";
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click_1;
            // 
            // cutToolStripMenuItem
            // 
            cutToolStripMenuItem.Name = "cutToolStripMenuItem";
            cutToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.X;
            cutToolStripMenuItem.Size = new Size(214, 26);
            cutToolStripMenuItem.Text = "Cut";
            cutToolStripMenuItem.Click += cutToolStripMenuItem_Click_1;
            // 
            // pasteToolStripMenuItem
            // 
            pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            pasteToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.V;
            pasteToolStripMenuItem.Size = new Size(214, 26);
            pasteToolStripMenuItem.Text = "Paste";
            pasteToolStripMenuItem.Click += pasteToolStripMenuItem_Click_1;
            // 
            // deleteToolStripMenuItem
            // 
            deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            deleteToolStripMenuItem.ShortcutKeys = Keys.Delete;
            deleteToolStripMenuItem.Size = new Size(214, 26);
            deleteToolStripMenuItem.Text = "Delete";
            deleteToolStripMenuItem.Click += deleteToolStripMenuItem_Click;
            // 
            // selectToolStripMenuItem
            // 
            selectToolStripMenuItem.Name = "selectToolStripMenuItem";
            selectToolStripMenuItem.Size = new Size(214, 26);
            selectToolStripMenuItem.Text = "Select (S)";
            selectToolStripMenuItem.Click += selectToolStripMenuItem_Click_1;
            // 
            // deselectEchapToolStripMenuItem
            // 
            deselectEchapToolStripMenuItem.Name = "deselectEchapToolStripMenuItem";
            deselectEchapToolStripMenuItem.Size = new Size(214, 26);
            deselectEchapToolStripMenuItem.Text = "Deselect (Echap)";
            // 
            // devicesMenu
            // 
            devicesMenu.DropDownItems.AddRange(new ToolStripItem[] { menuDevicesNew });
            devicesMenu.Name = "devicesMenu";
            devicesMenu.Size = new Size(74, 24);
            devicesMenu.Text = "Devices";
            // 
            // menuDevicesNew
            // 
            menuDevicesNew.Name = "menuDevicesNew";
            menuDevicesNew.Size = new Size(122, 26);
            menuDevicesNew.Text = "New";
            menuDevicesNew.Click += menuDevicesNew_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { zoomInToolStripMenuItem1, zoomOutToolStripMenuItem, zoomFitToolStripMenuItem, zoomSelectionToolStripMenuItem, analyzerToolStripMenuItem, watchToolStripMenuItem, moveBeginToolStripMenuItem, moveEndToolStripMenuItem, moveForwardToolStripMenuItem, moveBackwardToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(55, 24);
            viewToolStripMenuItem.Text = "View";
            // 
            // zoomInToolStripMenuItem1
            // 
            zoomInToolStripMenuItem1.Name = "zoomInToolStripMenuItem1";
            zoomInToolStripMenuItem1.Size = new Size(221, 26);
            zoomInToolStripMenuItem1.Text = "Zoom In (I)";
            // 
            // zoomOutToolStripMenuItem
            // 
            zoomOutToolStripMenuItem.Name = "zoomOutToolStripMenuItem";
            zoomOutToolStripMenuItem.Size = new Size(221, 26);
            zoomOutToolStripMenuItem.Text = "Zoom Out (O)";
            // 
            // zoomFitToolStripMenuItem
            // 
            zoomFitToolStripMenuItem.Name = "zoomFitToolStripMenuItem";
            zoomFitToolStripMenuItem.Size = new Size(221, 26);
            zoomFitToolStripMenuItem.Text = "Zoom Fit (Echap)";
            // 
            // zoomSelectionToolStripMenuItem
            // 
            zoomSelectionToolStripMenuItem.Name = "zoomSelectionToolStripMenuItem";
            zoomSelectionToolStripMenuItem.Size = new Size(221, 26);
            zoomSelectionToolStripMenuItem.Text = "Zoom Selection (Z)";
            zoomSelectionToolStripMenuItem.Click += zoomSelectionToolStripMenuItem_Click;
            // 
            // analyzerToolStripMenuItem
            // 
            analyzerToolStripMenuItem.Name = "analyzerToolStripMenuItem";
            analyzerToolStripMenuItem.Size = new Size(221, 26);
            analyzerToolStripMenuItem.Text = "Analyzer (A)";
            analyzerToolStripMenuItem.Click += analyzerToolStripMenuItem_Click;
            // 
            // watchToolStripMenuItem
            // 
            watchToolStripMenuItem.Name = "watchToolStripMenuItem";
            watchToolStripMenuItem.Size = new Size(221, 26);
            watchToolStripMenuItem.Text = "Watch (W)";
            watchToolStripMenuItem.Click += watchToolStripMenuItem_Click;
            // 
            // moveBeginToolStripMenuItem
            // 
            moveBeginToolStripMenuItem.Name = "moveBeginToolStripMenuItem";
            moveBeginToolStripMenuItem.Size = new Size(221, 26);
            moveBeginToolStripMenuItem.Text = "Move Begin (B)";
            // 
            // moveEndToolStripMenuItem
            // 
            moveEndToolStripMenuItem.Name = "moveEndToolStripMenuItem";
            moveEndToolStripMenuItem.Size = new Size(221, 26);
            moveEndToolStripMenuItem.Text = "Move End (E)";
            // 
            // moveForwardToolStripMenuItem
            // 
            moveForwardToolStripMenuItem.Name = "moveForwardToolStripMenuItem";
            moveForwardToolStripMenuItem.Size = new Size(221, 26);
            moveForwardToolStripMenuItem.Text = "Move Forward (F)";
            // 
            // moveBackwardToolStripMenuItem
            // 
            moveBackwardToolStripMenuItem.Name = "moveBackwardToolStripMenuItem";
            moveBackwardToolStripMenuItem.Size = new Size(221, 26);
            moveBackwardToolStripMenuItem.Text = "Move Backward (B)";
            // 
            // textToolStripMenuItem2
            // 
            textToolStripMenuItem2.Name = "textToolStripMenuItem2";
            textToolStripMenuItem2.Size = new Size(32, 19);
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { lIcenceToolStripMenuItem, supportToolStripMenuItem, updateToolStripMenuItem, copyrightToolStripMenuItem });
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(64, 24);
            aboutToolStripMenuItem.Text = "About";
            // 
            // lIcenceToolStripMenuItem
            // 
            lIcenceToolStripMenuItem.Name = "lIcenceToolStripMenuItem";
            lIcenceToolStripMenuItem.Size = new Size(157, 26);
            lIcenceToolStripMenuItem.Text = "License";
            // 
            // supportToolStripMenuItem
            // 
            supportToolStripMenuItem.Name = "supportToolStripMenuItem";
            supportToolStripMenuItem.Size = new Size(157, 26);
            supportToolStripMenuItem.Text = "Support";
            // 
            // updateToolStripMenuItem
            // 
            updateToolStripMenuItem.Name = "updateToolStripMenuItem";
            updateToolStripMenuItem.Size = new Size(157, 26);
            updateToolStripMenuItem.Text = "Update";
            // 
            // copyrightToolStripMenuItem
            // 
            copyrightToolStripMenuItem.Name = "copyrightToolStripMenuItem";
            copyrightToolStripMenuItem.Size = new Size(157, 26);
            copyrightToolStripMenuItem.Text = "Copyright";
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { filesToolStripMenuItem1, editToolStripMenuItem1, devicesMenu, viewToolStripMenuItem, aboutToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.MaximumSize = new Size(800, 32);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(7, 3, 0, 3);
            menuStrip1.RenderMode = ToolStripRenderMode.Professional;
            menuStrip1.ShowItemToolTips = true;
            menuStrip1.Size = new Size(800, 30);
            menuStrip1.Stretch = false;
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // toolStrip1
            // 
            toolStrip1.Dock = DockStyle.None;
            toolStrip1.ImageScalingSize = new Size(20, 20);
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripButtonBegin, toolStripButtonRewind, toolStripButtonRec, PlayButton, toolStripButtonForward, toolStripButtonEnd });
            toolStrip1.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            toolStrip1.Location = new Point(562, 3);
            toolStrip1.MaximumSize = new Size(343, 33);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(226, 25);
            toolStrip1.TabIndex = 9;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButtonBegin
            // 
            toolStripButtonBegin.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButtonBegin.Image = (Image)resources.GetObject("toolStripButtonBegin.Image");
            toolStripButtonBegin.ImageScaling = ToolStripItemImageScaling.None;
            toolStripButtonBegin.ImageTransparentColor = Color.Magenta;
            toolStripButtonBegin.Name = "toolStripButtonBegin";
            toolStripButtonBegin.Size = new Size(29, 22);
            toolStripButtonBegin.Text = "Begin (Ctrl+Left)";
            toolStripButtonBegin.Click += toolStripButtonBegin_Click;
            // 
            // toolStripButtonRewind
            // 
            toolStripButtonRewind.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButtonRewind.Image = (Image)resources.GetObject("toolStripButtonRewind.Image");
            toolStripButtonRewind.ImageScaling = ToolStripItemImageScaling.None;
            toolStripButtonRewind.ImageTransparentColor = Color.Magenta;
            toolStripButtonRewind.Name = "toolStripButtonRewind";
            toolStripButtonRewind.Size = new Size(29, 22);
            toolStripButtonRewind.Text = "Rewind (Left)";
            toolStripButtonRewind.Click += toolStripButtonRewind_Click;
            // 
            // toolStripButtonRec
            // 
            toolStripButtonRec.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButtonRec.Image = Properties.Resources.rec;
            toolStripButtonRec.ImageScaling = ToolStripItemImageScaling.None;
            toolStripButtonRec.ImageTransparentColor = Color.Magenta;
            toolStripButtonRec.Name = "toolStripButtonRec";
            toolStripButtonRec.Size = new Size(29, 22);
            toolStripButtonRec.Text = "Rec (Space)";
            toolStripButtonRec.Click += toolStripButtonRec_Click;
            // 
            // PlayButton
            // 
            PlayButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            PlayButton.Image = Properties.Resources.play;
            PlayButton.ImageScaling = ToolStripItemImageScaling.None;
            PlayButton.ImageTransparentColor = Color.Magenta;
            PlayButton.Name = "PlayButton";
            PlayButton.Size = new Size(29, 22);
            PlayButton.Text = "Play (P)";
            PlayButton.Click += PlayButton_Click;
            // 
            // toolStripButtonForward
            // 
            toolStripButtonForward.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButtonForward.Image = (Image)resources.GetObject("toolStripButtonForward.Image");
            toolStripButtonForward.ImageScaling = ToolStripItemImageScaling.None;
            toolStripButtonForward.ImageTransparentColor = Color.Magenta;
            toolStripButtonForward.Name = "toolStripButtonForward";
            toolStripButtonForward.Size = new Size(29, 22);
            toolStripButtonForward.Text = "Forward (Right)";
            toolStripButtonForward.Click += toolStripButtonForward_Click;
            // 
            // toolStripButtonEnd
            // 
            toolStripButtonEnd.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripButtonEnd.Image = (Image)resources.GetObject("toolStripButtonEnd.Image");
            toolStripButtonEnd.ImageScaling = ToolStripItemImageScaling.None;
            toolStripButtonEnd.ImageTransparentColor = Color.Magenta;
            toolStripButtonEnd.Name = "toolStripButtonEnd";
            toolStripButtonEnd.Size = new Size(29, 22);
            toolStripButtonEnd.Text = "End (ctrl+Right)";
            toolStripButtonEnd.Click += toolStripButtonEnd_Click;
            // 
            // eScope
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(914, 600);
            Controls.Add(toolStrip1);
            Controls.Add(tabControl1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(3, 4, 3, 4);
            Name = "eScope";
            Text = "eScope";
            PreviewKeyDown += eScope_PreviewKeyDown;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TabControl tabControl1;
        private ToolStripMenuItem filesToolStripMenuItem1;
        private ToolStripMenuItem editToolStripMenuItem1;
        private ToolStripMenuItem devicesMenu;
        private ToolStripMenuItem menuDevicesNew;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem zoomInToolStripMenuItem1;
        private ToolStripMenuItem zoomOutToolStripMenuItem;
        private ToolStripMenuItem zoomFitToolStripMenuItem;
        private ToolStripMenuItem moveBeginToolStripMenuItem;
        private ToolStripMenuItem moveEndToolStripMenuItem;
        private ToolStripMenuItem moveForwardToolStripMenuItem;
        private ToolStripMenuItem moveBackwardToolStripMenuItem;
        private ToolStripMenuItem textToolStripMenuItem2;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem lIcenceToolStripMenuItem;
        private ToolStripMenuItem supportToolStripMenuItem;
        private ToolStripMenuItem updateToolStripMenuItem;
        private ToolStripMenuItem copyrightToolStripMenuItem;
        public MenuStrip menuStrip1;
        private ToolStripMenuItem newTraceToolStripMenuItem;
        private ToolStripMenuItem openTraceToolStripMenuItem;
        private ToolStripMenuItem saveTraceToolStripMenuItem;
        private ToolStripMenuItem exportTraceToolStripMenuItem;
        private ToolStripMenuItem quitToolStripMenuItem;
        private ToolStripMenuItem closeTraceToolStripMenuItem;
        private ToolStrip toolStrip1;
        private ToolStripButton toolStripButtonBegin;
        private ToolStripButton toolStripButtonRewind;
        private ToolStripButton toolStripButtonRec;
        private ToolStripButton toolStripButtonForward;
        private ToolStripButton toolStripButtonEnd;
        private ToolStripButton PlayButton;
        private ToolStripMenuItem undoToolStripMenuItem;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem cutToolStripMenuItem;
        private ToolStripMenuItem pasteToolStripMenuItem;
        private ToolStripMenuItem deleteToolStripMenuItem;
        private ToolStripMenuItem selectToolStripMenuItem;
        private ToolStripMenuItem zoomSelectionToolStripMenuItem;
        private ToolStripMenuItem analyzerToolStripMenuItem;
        private ToolStripMenuItem watchToolStripMenuItem;
        private ToolStripMenuItem deselectEchapToolStripMenuItem;
        private ToolStripMenuItem redoToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripSeparator toolStripSeparatorFiles1;
        private ToolStripSeparator toolStripSeparatorFiles2;
        private ToolStripSeparator toolStripSeparatorFiles3;
    }
}