namespace eScope.Watch
{
    // symbol completion popup for the expression cell being edited
    // (the matches come from the symbol resolver: substring for map files, struct members with GDB)
    public class Completion
    {
        const int MaxItems = 200;
        const int VisibleItems = 8;

        readonly DataGridView Grid;
        readonly List<DeviceCtrl> Devices;
        readonly SuggestionList List = new();
        readonly System.Windows.Forms.Timer Throttle = new() { Interval = 250 };
        TextBox? Editor;
        int Row, Col;

        public Completion(DataGridView grid, List<DeviceCtrl> devices)
        {
            Grid = grid;
            Devices = devices;
            List.Visible = false;
            List.IntegralHeight = false;
            List.Picked += Accept;
            Grid.Controls.Add(List);
            Throttle.Tick += (s, e) => { Throttle.Stop(); Refresh(); };
        }
        public void Attach(TextBox editor, int row, int col)
        {
            Detach();
            Editor = editor;
            Row = row;
            Col = col;
            Editor.TextChanged += Editor_TextChanged;
        }
        public void Detach()
        {
            Throttle.Stop();
            Hide();
            if (Editor != null)
                Editor.TextChanged -= Editor_TextChanged;
            Editor = null;
        }
        void Hide()
        {
            List.Visible = false;
            List.Items.Clear();
        }
        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            // typed by the user (Modified is cleared when the text is set by code)
            if (Editor != null && Editor.Modified)
                Suggest();
        }
        // refresh the list once the typing pauses
        public void Suggest()
        {
            Throttle.Stop();
            Throttle.Start();
        }
        // keys of the edited cell, called before the grid handles them; true when consumed
        public bool ProcessKey(Keys keyData)
        {
            if (Editor == null)
                return false;
            if (keyData == (Keys.Control | Keys.Space))
            {
                Refresh();
                return true;
            }
            if (!List.Visible)
                return false;

            switch (keyData)
            {
                case Keys.Down:
                    List.SelectedIndex = Math.Min(List.SelectedIndex + 1, List.Items.Count - 1);
                    return true;
                case Keys.Up:
                    if (List.SelectedIndex <= 0)
                        List.ClearSelected();
                    else
                        List.SelectedIndex--;
                    return true;
                case Keys.PageDown:
                    List.SelectedIndex = Math.Min(List.SelectedIndex + VisibleItems, List.Items.Count - 1);
                    return true;
                case Keys.PageUp:
                    List.SelectedIndex = Math.Max(List.SelectedIndex - VisibleItems, 0);
                    return true;
                case Keys.Enter:
                case Keys.Tab:
                    if (List.SelectedIndex < 0)
                    {
                        Hide();
                        return keyData == Keys.Tab; // Enter without a pick validates the cell
                    }
                    Accept(List.SelectedIndex);
                    return true;
                case Keys.Escape:
                    Hide(); // a second Escape cancels the edition
                    return true;
            }
            return false;
        }
        void Accept(int index)
        {
            if (Editor == null || index < 0 || index >= List.Items.Count)
                return;
            Editor.Text = List.Items[index].ToString();
            Editor.SelectionStart = Editor.Text.Length;
            Editor.Focus();
            Hide();
        }
        void Refresh()
        {
            if (Editor == null)
                return;
            string text = Editor.Text.Trim();
            // the device named by the prefix, otherwise all of them
            DeviceCtrl? dev = Devices.FirstOrDefault(d => text.StartsWith(d.Data.Name + "."));
            var matches = new List<string>();
            foreach (var d in dev != null ? new List<DeviceCtrl> { dev } : Devices)
                matches.AddRange(d.GetSymbols(text));

            List.BeginUpdate();
            List.Items.Clear();
            foreach (var m in matches.Distinct().Take(MaxItems))
                List.Items.Add(m);
            List.EndUpdate();
            if (List.Items.Count == 0 || (List.Items.Count == 1 && List.Items[0].ToString() == text))
            {
                Hide();
                return;
            }
            Place();
            List.Visible = true;
            List.BringToFront();
        }
        // below the cell, or above when there is no room left in the grid
        void Place()
        {
            Rectangle cell = Grid.GetCellDisplayRectangle(Col, Row, false);
            int width = Math.Min(Math.Max(cell.Width, 300), Grid.ClientSize.Width);
            int left = Math.Max(0, Math.Min(cell.Left, Grid.ClientSize.Width - width));
            int wanted = Math.Min(List.Items.Count, VisibleItems) * List.ItemHeight + 4;
            int below = Grid.ClientSize.Height - cell.Bottom;
            int above = cell.Top;
            if (below >= wanted || below >= above)
                List.Bounds = new Rectangle(left, cell.Bottom, width, Math.Min(wanted, below));
            else
                List.Bounds = new Rectangle(left, cell.Top - Math.Min(wanted, above), width, Math.Min(wanted, above));
        }

        // list that never takes the focus, so that the cell stays in edit mode
        class SuggestionList : ListBox
        {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONDBLCLK = 0x0203;
            public event Action<int>? Picked;
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK)
                {
                    long lParam = m.LParam.ToInt64();
                    var p = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
                    int index = IndexFromPoint(p);
                    if (index != NoMatches)
                        Picked?.Invoke(index);
                    return;
                }
                base.WndProc(ref m);
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int index = IndexFromPoint(e.Location);
                if (index != NoMatches && index != SelectedIndex)
                    SelectedIndex = index;
            }
        }
    }
}
