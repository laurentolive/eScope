
namespace eScope.Symbol
{
    public class symbolListBox
    {
        public TextBox textBoxSymbol;
        private ListBox listBoxSymbol;
        public Label labelSymbol;
        List<DeviceCtrl> Devices;
        List<string> Symbols;
        private System.Windows.Forms.Timer ThrottledTimer;
        private Point Pos;
        public Action<SymbolData?> OnChanged { get; set; }
        public symbolListBox(Control.ControlCollection Controls, string symbolStr, List<DeviceCtrl> devices, Point pos)
        {
            ThrottledTimer = new System.Windows.Forms.Timer();
            ThrottledTimer.Interval = 300;
            ThrottledTimer.Tick += ThrottledTimeout;

            Symbols = new List<string>();
            Devices = devices;
            Pos = pos;
            labelSymbol = new Label();
            labelSymbol.AutoSize = true;
            labelSymbol.Name = "labelSymbol";
            labelSymbol.Size = new Size(47, 20);
            labelSymbol.TabIndex = 12;
            labelSymbol.Text = "Symbol";

            textBoxSymbol = new TextBox();
            textBoxSymbol.Name = "textBoxSymbol";
            textBoxSymbol.Size = new Size(350, 23);
            textBoxSymbol.TabIndex = 1;
            textBoxSymbol.Text = symbolStr;
            textBoxSymbol.TextChanged += textBoxSymbol_TextChanged;
            textBoxSymbol.KeyUp += textBoxSymbol_KeyUp;
            textBoxSymbol.LostFocus += textBoxSymbol_LostFocus;

            listBoxSymbol = new ListBox();
            listBoxSymbol.FormattingEnabled = true;
            listBoxSymbol.ItemHeight = 15;
            listBoxSymbol.Name = "listBoxSymbol";
            listBoxSymbol.Size = new Size(350, 94);
            listBoxSymbol.TabIndex = 30;
            listBoxSymbol.TabStop = false;
            listBoxSymbol.Click += listBoxSymbol_Click;
            listBoxSymbol.KeyDown += listBoxSymbol_KeyDown;
            listBoxSymbol.MouseMove += listBoxSymbol_MouseMove;
            listBoxSymbol.Visible = false;
            SetPosition(Pos);
            Controls.Add(textBoxSymbol);
            Controls.Add(listBoxSymbol);
            Controls.Add(labelSymbol);
            listBoxSymbol.BringToFront();

            UpdateSymbols();
        }
        public void SetPosition(Point p)
        {
            Pos = p;
            labelSymbol.Location = new Point(Pos.X, Pos.Y + 4);
            textBoxSymbol.Location = new Point(Pos.X + 63, Pos.Y);
            listBoxSymbol.Location = new Point(Pos.X + 63, Pos.Y + 22);
        }
        private void ThrottledTimeout(object sender, EventArgs e)
        {
            // Stop the timer
            ThrottledTimer.Stop();

        }
        public void Focus()
        {
            textBoxSymbol.Focus();
        }
        string ShortenFromLeft(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;

            return "..." + text.Substring(text.Length - maxLength);
        }
        public void UpdateSymbols()
        {
            Symbols.Clear();
            foreach (DeviceCtrl dev in Devices)
            {
                List<string> modifiedList = dev.GetSymbols(textBoxSymbol.Text);
                Symbols.AddRange(modifiedList);
            }

        }
        private bool isSymbolValid(string name)
        {
            bool bRet = false;
            DeviceCtrl? dev = getDeviceFromSymbol(name);

            if ((dev != null) && (dev.isSymbolValid(name)))
            {
                bRet = true;
            }
            return bRet;
        }
        public SymbolData? GetSymbolData()
        {
            DeviceCtrl? dev = getDeviceFromSymbol(textBoxSymbol.Text);
            SymbolData? sd = dev?.GetSymbol(textBoxSymbol.Text);
            return sd;
        }
        public bool validate()
        {
            if (isSymbolValid(textBoxSymbol.Text))
            {
                textBoxSymbol.BackColor = Color.White;
                return true;
            }
            else
            {
                textBoxSymbol.BackColor = Color.Salmon;
                return false;
            }
        }
        private void textBoxSymbol_TextChanged(object sender, EventArgs e)
        {
            if(textBoxSymbol.Focused)
            {
                UpdateSymbols();
                listBoxSymbol.Items.Clear();

                if (Symbols.Count >= 1)
                {
                    foreach (string s in Symbols)
                    {
                        listBoxSymbol.Items.Add(ShortenFromLeft(s, 40));
                    }
                    listBoxSymbol.Visible = true;
                }
                else
                {
                    listBoxSymbol.Visible = false;
                }
            }

            if (validate())
            {
                DeviceCtrl? dev = getDeviceFromSymbol(textBoxSymbol.Text);
                SymbolData? sd = dev?.GetSymbol(textBoxSymbol.Text);
                OnChanged?.Invoke(sd);
            }
        }
        public string getNameDeviceFromSymbol(string symbol)
        {
            string deviceName = symbol;
            int index = symbol.IndexOf('.');
            if (index != -1) // Split the string into two parts
            {
                deviceName = symbol.Substring(0, index);
            }
            return deviceName;
        }
        public DeviceCtrl? getDeviceFromSymbol(string symbol)
        {
            DeviceCtrl? dev = null;
            string deviceName = getNameDeviceFromSymbol(symbol);

            dev = Devices.FirstOrDefault(d => d.Data.Name == deviceName);

            return dev;

        }
        private void textBoxSymbol_LostFocus(object sender, EventArgs e)
        {
            if(!listBoxSymbol.Focused)
            {
                listBoxSymbol.Visible = false;
            }
        }
        private void textBoxSymbol_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                listBoxSymbol.Focus();
                listBoxSymbol.Visible = true;
                if (listBoxSymbol.Items.Count > 0)
                {
                    listBoxSymbol.SelectedIndex = 0;
                }
            }
        }
        private void listBoxSymbol_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                textBoxSymbol.Text = Symbols[listBoxSymbol.SelectedIndex];
                textBoxSymbol.Focus();
                textBoxSymbol.Select(textBoxSymbol.Text.Length, 0);//lace cursor at the end of string to ease edition
                listBoxSymbol.Visible = false;
            }
            else if ((e.KeyCode == Keys.Escape) || ((e.KeyCode == Keys.Up) && (listBoxSymbol.SelectedIndex == 0)))
            {
                textBoxSymbol.Focus();
                listBoxSymbol.Visible = false;
            }

        }
        public bool isFocus()
        {
            return listBoxSymbol.Visible;
        }
        public void Echap()
        {
            listBoxSymbol.Visible = false;
        }
        private void listBoxSymbol_Click(object sender, EventArgs e)
        {
            textBoxSymbol.Text = Symbols[listBoxSymbol.SelectedIndex];
            textBoxSymbol.Focus();
            listBoxSymbol.Visible = false;
        }
        private void listBoxSymbol_MouseEnter(object sender, EventArgs e)
        {
            listBoxSymbol.Focus();
        }
        
        private void listBoxSymbol_MouseMove(object sender, MouseEventArgs e)
        {
            // Get the index of the item under the mouse pointer
            int index = listBoxSymbol.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                // Select the item under the mouse pointer
                //listBoxSymbol.Focus();
                listBoxSymbol.SelectedIndex = index;
            }
        }
    }
}
