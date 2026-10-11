namespace eScope.UI
{
    // Common layout of the edit dialogs, built over the controls of the designer:
    // columns of sections, each row a label (fixed width) and its field(s), a footer with a status text and Cancel / OK.
    // The designer positions are ignored: the dialogs all share the same widths, spacing and order.
    public class DialogLayout
    {
        public const int LabelWidth = 116;
        public const int FieldWidth = 300;

        readonly Form form;
        readonly TableLayoutPanel root;
        readonly FlowLayoutPanel columns;
        readonly List<Control> initial;
        readonly HashSet<Control> floating = new();
        int tab;

        public DialogLayout(Form form)
        {
            this.form = form;
            initial = form.Controls.Cast<Control>().ToList();
            form.SuspendLayout();
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.CenterParent;

            root = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Location = Point.Empty,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            columns = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = new Padding(Px(20), Px(14), Px(20), Px(18)),
            };
            root.Controls.Add(columns, 0, 0);
        }

        internal int Px(float v) => form.Px(v);
        internal int NextTab() => tab++;

        // labelWidth 0: fields on the whole width
        public DialogColumn Column(int fieldWidth = FieldWidth, int labelWidth = LabelWidth)
        {
            var column = new DialogColumn(this, fieldWidth, labelWidth);
            if (columns.Controls.Count > 0) column.Grid.Margin = new Padding(Px(28), 0, 0, 0);
            columns.Controls.Add(column.Grid);
            return column;
        }

        // control kept over the layout at its own position (e.g. an autocomplete list)
        public void Float(Control c) => floating.Add(c);

        public void Footer(Button ok, Button cancel, Control? status = null)
        {
            var bar = new BarPanel(p => p.Bg, AnchorStyles.Top)
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(Px(20), Px(12), Px(20), Px(12)),
            };
            var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill, Margin = Padding.Empty };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            if (status != null)
            {
                status.Anchor = AnchorStyles.Left;
                status.Margin = new Padding(0, 0, Px(12), 0);
                if (status is Label l)
                {
                    l.AutoSize = true;
                    l.MaximumSize = new Size(Px(560), 0);
                }
                grid.Controls.Add(status, 0, 0);
            }
            foreach (var (b, col) in new[] { (cancel, 1), (ok, 2) })
            {
                b.AutoSize = true;
                b.MinimumSize = new Size(Px(92), Px(30));
                b.Margin = new Padding(Px(8), 0, 0, 0);
                b.Anchor = AnchorStyles.Right;
                grid.Controls.Add(b, col, 0);
            }
            ok.Tag = "primary";
            ok.TabIndex = 1000;
            cancel.TabIndex = 1001;
            bar.Controls.Add(grid);
            root.Controls.Add(bar, 0, 1);
        }

        // fixed: the size does not follow the content anymore (a floating list must not grow the dialog)
        public void Done(bool fixedSize = false)
        {
            // what the layout did not take is not shown
            foreach (var c in initial)
            {
                if (c.Parent == form && !floating.Contains(c))
                    form.Controls.Remove(c);
            }
            form.Controls.Add(root);
            foreach (var c in floating) c.BringToFront();
            form.AutoSize = true;
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            form.ResumeLayout(true);
            if (fixedSize)
            {
                form.AutoSize = false;
                form.ClientSize = root.PreferredSize;
            }
        }
    }

    // One column of a dialog: a 2-column grid (label | field)
    public class DialogColumn
    {
        readonly DialogLayout L;
        public TableLayoutPanel Grid { get; }
        int row;

        internal DialogColumn(DialogLayout layout, int fieldWidth, int labelWidth, TableLayoutPanel? grid = null)
        {
            L = layout;
            Grid = grid ?? NewGrid(fieldWidth, labelWidth);
        }

        TableLayoutPanel NewGrid(int fieldWidth, int labelWidth)
        {
            var g = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L.Px(labelWidth)));
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, L.Px(fieldWidth)));
            return g;
        }

        void Add(Control c, int column, int span = 1)
        {
            if (Grid.RowStyles.Count <= row) Grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Grid.Controls.Add(c, column, row);
            if (span > 1) Grid.SetColumnSpan(c, span);
        }

        void Tab(Control c)
        {
            if (c.TabStop || c is TextBoxBase || c is ComboBox || c is ButtonBase) c.TabIndex = L.NextTab();
        }

        Label StyleLabel(Label label)
        {
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0, L.Px(4), L.Px(8), L.Px(4));
            return label;
        }

        // field(s) of a row: the first one takes the width left by the others
        Control Fields(Control[] fields)
        {
            if (fields.Length == 1)
            {
                var f = fields[0];
                f.Anchor = f is CheckBox or RadioButton ? AnchorStyles.Left : AnchorStyles.Left | AnchorStyles.Right;
                f.Margin = new Padding(0, L.Px(3), 0, L.Px(3));
                Tab(f);
                return f;
            }
            var panel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = fields.Length,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                panel.ColumnStyles.Add(i == 0 ? new ColumnStyle(SizeType.Percent, 100) : new ColumnStyle(SizeType.AutoSize));
                f.Anchor = i == 0 ? AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Left;
                f.Margin = new Padding(i == 0 ? 0 : L.Px(6), L.Px(3), 0, L.Px(3));
                Tab(f);
                panel.Controls.Add(f, i, 0);
            }
            return panel;
        }

        public DialogColumn Section(string title)
        {
            var label = new Label
            {
                Text = title.ToUpperInvariant(),
                AutoSize = true,
                Font = Theme.UiSemibold(8),
                ForeColor = Theme.Current.Muted,
                Margin = new Padding(0, row == 0 ? L.Px(2) : L.Px(16), 0, L.Px(4)),
            };
            Add(label, 0, 2);
            row++;
            return this;
        }

        public DialogColumn Row(Label? label, params Control[] fields)
        {
            if (label != null) Add(StyleLabel(label), 0);
            Add(Fields(fields), 1);
            row++;
            return this;
        }

        public DialogColumn Row(string label, params Control[] fields) => Row(new Label { Text = label }, fields);

        // name of the object, its color and its visibility on one row
        public DialogColumn Header(Label label, TextBox name, Button? color = null, CheckBox? visible = null)
        {
            var fields = new List<Control> { name };
            if (color != null)
            {
                color.Text = string.Empty;
                color.Size = new Size(L.Px(28), L.Px(26));
                color.MinimumSize = color.Size;
                fields.Add(color);
            }
            if (visible != null) fields.Add(visible);
            return Row(label, fields.ToArray());
        }

        // checkboxes or radio buttons side by side, under the fields
        public DialogColumn Options(Label? label, params Control[] options)
        {
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0, L.Px(2), 0, L.Px(2)),
                Anchor = AnchorStyles.Left,
            };
            foreach (var o in options)
            {
                o.AutoSize = true;
                o.Margin = new Padding(0, L.Px(3), L.Px(18), L.Px(3));
                Tab(o);
                flow.Controls.Add(o);
            }
            if (label != null) Add(StyleLabel(label), 0);
            Add(flow, label == null && Grid.ColumnStyles[0].Width == 0 ? 0 : 1, label == null && Grid.ColumnStyles[0].Width == 0 ? 2 : 1);
            row++;
            return this;
        }

        // fields on the whole width, the first one stretched
        public DialogColumn WideRow(params Control[] fields)
        {
            Add(Fields(fields), 0, 2);
            row++;
            return this;
        }

        // control on the whole width (script editor...)
        public DialogColumn Wide(Control c, int height)
        {
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            c.Height = L.Px(height);
            c.Margin = new Padding(0, L.Px(4), 0, L.Px(4));
            Tab(c);
            Add(c, 0, 2);
            row++;
            return this;
        }

        // rows shown or hidden together: the host (a designer panel) holds a grid aligned on this one
        public DialogColumn Group(Panel host)
        {
            host.Controls.Clear();
            host.AutoSize = true;
            host.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            host.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            host.BorderStyle = BorderStyle.None;
            var inner = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Location = Point.Empty,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            foreach (ColumnStyle s in Grid.ColumnStyles) inner.ColumnStyles.Add(new ColumnStyle(s.SizeType, s.Width));
            host.Controls.Add(inner);
            Add(host, 0, 2);
            row++;
            return new DialogColumn(L, 0, 0, inner);
        }
    }
}
