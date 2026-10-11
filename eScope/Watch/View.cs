namespace eScope.Watch
{
    public class View : Form, IThemed
    {
        private DataGridView Grid;
        private System.Windows.Forms.Timer updateTimer;
        List<DeviceCtrl> Devices;
        List<Watch.Ctrl> Ctrl = new List<Ctrl>();
        List<Watch.Data> Data;
        ContextMenuStrip contextMenu = new ContextMenuStrip();
        private int insertRowIndex = -1;
        private Rectangle dragBox = Rectangle.Empty;  // drag starts when the mouse leaves it
        private int dragRowIndex = -1;
        private Watch.Data? newWatch;                 // expression typed in the "add watch" row, validated
        private Completion completion;

        const int ExpressionColumn = Watch.Ctrl.ExpressionColumn;
        const int AddressColumn = Watch.Ctrl.AddressColumn;
        const int DisplayColumn = Watch.Ctrl.DisplayColumn;
        const int ValueColumn = Watch.Ctrl.ValueColumn;
        const string AddWatchText = "add watch";

        public View(List<Watch.Data> data, List<DeviceCtrl> devices)
        {
            Text = "Watch";
            Size = new Size(400, 300);
            Data = data;
            Devices = devices;
            Grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                ReadOnly = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                AllowUserToOrderColumns = false,
                AllowUserToAddRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false, // no highlight of the current column header
                EditMode = DataGridViewEditMode.EditProgrammatically, // double-click, Enter or F2
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
            };
            Load += View_Load;

            Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expression", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            Grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Display",
                DataSource = Enum.GetValues(typeof(Utils.Display)),
                ValueType = typeof(Utils.Display),
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            });
            Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value" });
            foreach (DataGridViewColumn column in Grid.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            // headers: neither selectable nor highlighted
            Grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Grid.ColumnHeadersDefaultCellStyle.BackColor;
            Grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Grid.ColumnHeadersDefaultCellStyle.ForeColor;
            // current row lightly highlighted, current cell a bit more (see Grid_CellFormatting)
            Grid.DefaultCellStyle.SelectionBackColor = RowHighlight;
            Grid.DefaultCellStyle.SelectionForeColor = Grid.DefaultCellStyle.ForeColor;
            Grid.CellFormatting += Grid_CellFormatting;
            Grid.CellPainting += Grid_CellPainting;

            Grid.CellDoubleClick += Grid_CellDoubleClick;
            Grid.MouseDown += Grid_MouseDown;
            Grid.MouseMove += Grid_MouseMove;
            Grid.MouseUp += (s, e) => dragBox = Rectangle.Empty;
            Grid.DragOver += Grid_DragOver;
            Grid.DragDrop += Grid_DragDrop;
            Grid.DragLeave += Grid_DragLeave;
            Grid.Paint += Grid_Paint;
            Grid.KeyDown += Grid_KeyDown;
            Grid.KeyPress += Grid_KeyPress;
            Grid.CellBeginEdit += Grid_CellBeginEdit;
            Grid.CellValidating += Grid_CellValidating;
            Grid.CellEndEdit += Grid_CellEndEdit;
            Grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
            Grid.CellValueChanged += Grid_CellValueChanged;
            Grid.EditingControlShowing += Grid_EditingControlShowing;
            Grid.DataError += (s, e) => e.ThrowException = false;
            Grid.AllowDrop = true;
            Controls.Add(Grid);
            completion = new Completion(Grid, Devices);

            foreach (var d in Data)
            {
                int newRowIndex = Grid.Rows.Add();
                Ctrl.Add(new Watch.Ctrl(d, devices, Grid.Rows[newRowIndex]));
            }
            AddWatchRow();

            Grid.ClearSelection();
            updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            updateTimer.Tick += UpdateSpyData;
            updateTimer.Start();
            FormClosed += (s, e) => updateTimer.Stop();

            Grid.PerformLayout();
            Grid.Refresh();
        }
        bool IsAddRow(int row) => row == Grid.Rows.Count - 1;

        // --- Selection look ---

        static Color RowHighlight => Blend(Theme.Current.Accent, Theme.Current.Surface, 0.20);
        static Color CellHighlight => Blend(Theme.Current.Accent, Theme.Current.Surface, 0.40);

        // grid colors from the theme (window or side panel of the main window)
        public void ApplyTheme()
        {
            var p = Theme.Current;
            BackColor = p.Surface;
            ThemeApplier.StyleGrid(Grid);
            Grid.Font = Theme.Mono(9);
            Grid.ColumnHeadersDefaultCellStyle.Font = Theme.Ui(8.5f);
            Grid.DefaultCellStyle.SelectionBackColor = RowHighlight;
            Grid.DefaultCellStyle.SelectionForeColor = Grid.DefaultCellStyle.ForeColor;
            // rows styled under the previous theme follow the grid again
            foreach (DataGridViewRow row in Grid.Rows)
            {
                row.DefaultCellStyle.ForeColor = Color.Empty;
                row.DefaultCellStyle.SelectionForeColor = Color.Empty;
            }
            if (Grid.Rows.Count > 0) RestoreAddRow();
            Grid.Invalidate();
        }

        // narrow side panel: no address column
        public void SetCompact(bool compact) => Grid.Columns[AddressColumn].Visible = !compact;

        // embedded in the side panel: values read only while the panel is shown
        public void SetActive(bool active) => updateTimer.Enabled = active;

        public void FocusGrid(bool add)
        {
            Grid.Focus();
            if (add && Grid.Rows.Count > 0)
            {
                Grid.CurrentCell = Grid.Rows[Grid.Rows.Count - 1].Cells[ExpressionColumn];
                EditCell(Grid.Rows.Count - 1, ExpressionColumn);
            }
        }
        static Color Blend(Color c, Color background, double amount)
        {
            int Mix(int a, int b) => (int)Math.Round(a * amount + b * (1 - amount));
            return Color.FromArgb(Mix(c.R, background.R), Mix(c.G, background.G), Mix(c.B, background.B));
        }
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.CellStyle != null && Grid.CurrentCell != null
                && e.RowIndex == Grid.CurrentCell.RowIndex && e.ColumnIndex == Grid.CurrentCell.ColumnIndex)
                e.CellStyle.SelectionBackColor = CellHighlight;
        }
        // no dotted focus rectangle: the current cell is shown by its color
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);
            e.Handled = true;
        }
        private void removeWatch(int row)
        {
            if (row < 0 || row >= Ctrl.Count)
                return;
            var result = MessageBox.Show(
                $"Delete {Ctrl[row].Data.Expression}?",
                "Confirm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                Grid.Rows.RemoveAt(row);
                Ctrl.RemoveAt(row);
                Data.RemoveAt(row);
            }
        }
        private void View_Load(object? sender, EventArgs e)
        {
            Width = Grid.Location.X + Grid.Width + 40;
        }
        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (Grid.CurrentCell != null)
                    EditCell(Grid.CurrentCell.RowIndex, Grid.CurrentCell.ColumnIndex);
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (Grid.CurrentRow != null)
                    removeWatch(Grid.CurrentRow.Index);
                e.Handled = true;
            }
        }

        // --- Reorder by drag and drop ---

        private void Grid_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragBox == Rectangle.Empty || dragBox.Contains(e.Location))
                return;
            dragBox = Rectangle.Empty;
            if (!Grid.IsCurrentCellInEditMode && dragRowIndex >= 0 && dragRowIndex < Ctrl.Count)
                Grid.DoDragDrop(Grid.Rows[dragRowIndex], DragDropEffects.Move);
        }
        private void Grid_DragOver(object? sender, DragEventArgs e)
        {
            Point clientPoint = Grid.PointToClient(new Point(e.X, e.Y));
            var hit = Grid.HitTest(clientPoint.X, clientPoint.Y);

            if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex >= 0 && insertRowIndex != hit.RowIndex)
            {
                insertRowIndex = hit.RowIndex; // dropped above this row ("add watch" row = at the end)
                Grid.Invalidate();
            }
            e.Effect = DragDropEffects.Move;
        }
        private void Grid_DragLeave(object? sender, EventArgs e)
        {
            insertRowIndex = -1;
            Grid.Invalidate();
        }
        private void Grid_DragDrop(object? sender, DragEventArgs e)
        {
            var draggedRow = e.Data?.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
            if (draggedRow != null && insertRowIndex >= 0 && insertRowIndex <= Ctrl.Count)
            {
                int from = draggedRow.Index;
                int to = insertRowIndex > from ? insertRowIndex - 1 : insertRowIndex;
                if (from != to)
                {
                    Ctrl c = Ctrl[from];
                    Data d = Data[from];
                    Ctrl.RemoveAt(from);
                    Data.RemoveAt(from);
                    Ctrl.Insert(to, c);
                    Data.Insert(to, d);
                    for (int i = 0; i < Ctrl.Count; i++)
                        Ctrl[i].UpdateRow(Grid.Rows[i]);
                    Grid.ClearSelection();
                    Grid.CurrentCell = Grid.Rows[to].Cells[ExpressionColumn];
                }
            }

            insertRowIndex = -1;
            Grid.Invalidate();
        }
        private void Grid_Paint(object? sender, PaintEventArgs e)
        {
            if (insertRowIndex >= 0 && insertRowIndex < Grid.Rows.Count)
            {
                Rectangle rowRect = Grid.GetRowDisplayRectangle(insertRowIndex, true);
                int y = rowRect.Top;

                using (Pen pen = new Pen(Color.Red, 3))
                {
                    e.Graphics.DrawLine(pen, new Point(rowRect.Left, y), new Point(rowRect.Right, y));
                }
            }
        }

        // --- Context menu ---

        private void Grid_MouseDown(object? sender, MouseEventArgs e)
        {
            var hit = Grid.HitTest(e.X, e.Y);
            if (e.Button == MouseButtons.Left)
            {
                dragRowIndex = hit.Type == DataGridViewHitTestType.Cell ? hit.RowIndex : -1;
                Size dragSize = SystemInformation.DragSize;
                dragBox = new Rectangle(new Point(e.X - dragSize.Width / 2, e.Y - dragSize.Height / 2), dragSize);
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (hit.RowIndex >= 0)
                {
                    Grid.ClearSelection();
                    Grid.Rows[hit.RowIndex].Selected = true;
                    Grid.CurrentCell = Grid.Rows[hit.RowIndex].Cells[ExpressionColumn];
                }

                contextMenu.Items.Clear();

                if ((Grid.CurrentCell != null) && (Grid.CurrentCell.RowIndex < Ctrl.Count))
                {
                    int row = Grid.CurrentCell.RowIndex;
                    ToolStripMenuItem deleteItem = new ToolStripMenuItem($"Delete {Ctrl[row].Data.Expression}");
                    deleteItem.Click += (s, args) => removeWatch(row);
                    contextMenu.Items.Add(deleteItem);
                }

                ToolStripMenuItem showMenu = new ToolStripMenuItem("Show/Hide");
                foreach (DataGridViewColumn column in Grid.Columns)
                {
                    var item = new ToolStripMenuItem(column.HeaderText)
                    {
                        Checked = column.Visible,
                        CheckOnClick = true,
                        Tag = column
                    };
                    item.CheckedChanged += (s, args) =>
                    {
                        if (s is ToolStripMenuItem menuItem && menuItem.Tag is DataGridViewColumn col)
                            col.Visible = menuItem.Checked;
                    };
                    showMenu.DropDownItems.Add(item);
                }
                contextMenu.Items.Add(showMenu);
                contextMenu.Show(Grid, e.Location);
            }
        }

        // --- Inline edition ---

        private void EditCell(int row, int col)
        {
            if (row < 0 || col < 0 || Grid.IsCurrentCellInEditMode)
                return;

            if (IsAddRow(row))
            {
                // only the expression of a new watch can be typed
                col = ExpressionColumn;
                var cell = Grid.Rows[row].Cells[col];
                cell.Value = string.Empty;
                Grid.Rows[row].DefaultCellStyle.Font = Grid.Font;
                Grid.Rows[row].DefaultCellStyle.ForeColor = Grid.DefaultCellStyle.ForeColor;
                Grid.Rows[row].DefaultCellStyle.SelectionForeColor = Grid.DefaultCellStyle.SelectionForeColor;
            }
            Grid.CurrentCell = Grid.Rows[row].Cells[col];
            if (!Grid.BeginEdit(true)) // true = sélectionne le contenu existant
                RestoreAddRow();
        }
        // typing a character starts the edition and replaces the text (Enter / F2 / double-click keep it)
        private void Grid_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (Grid.IsCurrentCellInEditMode || Grid.CurrentCell == null || char.IsControl(e.KeyChar)
                || (ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
                return;
            int row = Grid.CurrentCell.RowIndex;
            int col = IsAddRow(row) ? ExpressionColumn : Grid.CurrentCell.ColumnIndex;
            if (col == DisplayColumn)
                return;

            EditCell(row, col);
            if (Grid.EditingControl is TextBox editor)
            {
                editor.Text = e.KeyChar.ToString();
                editor.SelectionStart = editor.TextLength;
                if (col == ExpressionColumn)
                    completion.Suggest();
                e.Handled = true;
            }
        }
        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            EditCell(e.RowIndex, e.ColumnIndex);
        }
        private void Grid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex == AddressColumn || (IsAddRow(e.RowIndex) && e.ColumnIndex != ExpressionColumn))
                e.Cancel = true;
        }
        private void Grid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox combo)
                BeginInvoke(() => combo.DroppedDown = true);
            else if (e.Control is TextBox editor && Grid.CurrentCell?.ColumnIndex == ExpressionColumn)
                completion.Attach(editor, Grid.CurrentCell.RowIndex, ExpressionColumn);
        }
        // completion keys handled before the grid (which would move the current cell or end the edition)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.W))
            {
                Grid.CancelEdit(); // an invalid expression being typed must not block the closing
                Grid.EndEdit();
                if (TopLevel) Close();
                return true;
            }
            if (Grid.IsCurrentCellInEditMode && completion.ProcessKey(keyData))
                return true;
            if (Grid.EditingControl is TextBox editor && MoveCaret(editor, keyData))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
        // Home / End stay in the text: with the whole text selected the grid would take them
        // (jump to the first / last cell and end the edition)
        static bool MoveCaret(TextBox editor, Keys keyData)
        {
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            Keys key = keyData & ~Keys.Shift;
            if (key != Keys.Home && key != Keys.End)
                return false;

            int caret = key == Keys.Home ? 0 : editor.TextLength;
            if (shift)
            {
                // extend from the anchor (the end of the selection opposite to the caret)
                int anchor = key == Keys.Home ? editor.SelectionStart + editor.SelectionLength : editor.SelectionStart;
                editor.Select(Math.Min(anchor, caret), Math.Abs(anchor - caret));
            }
            else
            {
                editor.Select(caret, 0);
            }
            return true;
        }
        private void Grid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != ExpressionColumn || !Grid.IsCurrentCellInEditMode)
                return;

            string expression = e.FormattedValue?.ToString()?.Trim() ?? string.Empty;
            string? error = null;
            if (IsAddRow(e.RowIndex))
            {
                newWatch = null;
                if (expression.Length == 0)
                    return; // nothing typed: back to "add watch"
                Watch.Data d = new();
                error = Watch.Ctrl.Resolve(d, Devices, expression);
                if (error == null)
                    newWatch = d;
            }
            else if (e.RowIndex < Ctrl.Count && expression != Ctrl[e.RowIndex].Data.Expression)
            {
                error = expression.Length == 0 ? "Empty expression" : Ctrl[e.RowIndex].SetExpression(expression);
            }

            if (error != null)
            {
                Grid.Rows[e.RowIndex].ErrorText = error + " (Esc to cancel)";
                e.Cancel = true;
            }
        }
        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            completion.Detach();
            if (e.RowIndex < 0)
                return;
            Grid.Rows[e.RowIndex].ErrorText = string.Empty;

            if (IsAddRow(e.RowIndex))
            {
                if (newWatch != null)
                {
                    Data.Add(newWatch);
                    Ctrl.Add(new Watch.Ctrl(newWatch, Devices, Grid.Rows[e.RowIndex]));
                    newWatch = null;
                    AddWatchRow();
                }
                else
                {
                    RestoreAddRow();
                }
                return;
            }
            if (e.RowIndex >= Ctrl.Count)
                return;

            var ctrl = Ctrl[e.RowIndex];
            if (e.ColumnIndex == ValueColumn)
            {
                string raw = Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
                if (TryParseValue(raw, ctrl.Data, out UInt64 val))
                {
                    ctrl.Data.Value = val;
                    ctrl.Write(val);   // écrit en mémoire via le protocole
                }
            }
            ctrl.Update();         // réaffiche la valeur formatée (ou l'ancienne si invalide)
        }
        // display format applied as soon as it is picked in the combo
        private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (Grid.IsCurrentCellDirty && Grid.CurrentCell?.ColumnIndex == DisplayColumn)
                Grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != DisplayColumn || e.RowIndex < 0 || e.RowIndex >= Ctrl.Count)
                return;
            if (Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is Utils.Display display && display != Ctrl[e.RowIndex].Data.Display)
            {
                Ctrl[e.RowIndex].Data.Display = display;
                Ctrl[e.RowIndex].Update();
            }
        }
        // Convertit la saisie utilisateur en valeur brute mémoire selon le format d'affichage
        static bool TryParseValue(string raw, Watch.Data data, out UInt64 val)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            val = 0;
            raw = raw.Trim();
            if (raw.Length == 0)
                return false;

            int size = Math.Clamp(data.Size, 1, 64);
            UInt64 mask = size >= 64 ? UInt64.MaxValue : ((1UL << size) - 1);
            UInt64 v;

            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!UInt64.TryParse(raw.Substring(2), System.Globalization.NumberStyles.HexNumber, inv, out v))
                    return false;
            }
            else
            {
                if (raw.StartsWith("0u", StringComparison.OrdinalIgnoreCase) ||
                    raw.StartsWith("0d", StringComparison.OrdinalIgnoreCase) ||
                    raw.StartsWith("0f", StringComparison.OrdinalIgnoreCase))
                    raw = raw.Substring(2);

                if (data.Display == Utils.Display.float32)
                {
                    if (!double.TryParse(raw.Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out double d))
                        return false;
                    val = size == 64 ? BitConverter.DoubleToUInt64Bits(d) : BitConverter.SingleToUInt32Bits((float)d);
                    return true;
                }
                else if (data.Display == Utils.Display.hexa)
                {
                    if (!UInt64.TryParse(raw, System.Globalization.NumberStyles.HexNumber, inv, out v))
                        return false;
                }
                else if (Int64.TryParse(raw, System.Globalization.NumberStyles.Integer, inv, out Int64 s))
                {
                    v = unchecked((UInt64)s);
                }
                else if (!UInt64.TryParse(raw, System.Globalization.NumberStyles.Integer, inv, out v))
                {
                    return false;
                }
            }

            // Réinsère le champ décalé (shift) dans la dernière valeur lue
            int shift = Math.Clamp(data.Shift, 0, 63);
            val = ((data.Value & ~(mask << shift)) | ((v & mask) << shift)) & mask;
            return true;
        }
        private void UpdateSpyData(object? sender, EventArgs e)
        {
            foreach (var c in Ctrl)
            {
                c.Read();
            }
        }
        private void AddWatchRow()
        {
            int row = Grid.Rows.Add();
            // the previous "add watch" row became a watch
            if (row > 0)
            {
                Grid.Rows[row - 1].DefaultCellStyle.Font = Grid.Font;
                Grid.Rows[row - 1].DefaultCellStyle.ForeColor = Grid.DefaultCellStyle.ForeColor;
                Grid.Rows[row - 1].DefaultCellStyle.SelectionForeColor = Grid.DefaultCellStyle.SelectionForeColor;
            }
            RestoreAddRow();
        }
        private void RestoreAddRow()
        {
            var row = Grid.Rows[Grid.Rows.Count - 1];
            row.DefaultCellStyle.Font = new Font(Grid.Font, FontStyle.Italic);
            row.DefaultCellStyle.ForeColor = Theme.Current.Faint;
            row.DefaultCellStyle.SelectionForeColor = Theme.Current.Faint;
            row.Cells[ExpressionColumn].Value = AddWatchText;
        }
    }
}
