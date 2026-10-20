using eScope.Plot;
using eScope.Symbol;
using System.Data;

namespace eScope
{
    public partial class ChannelEdit : Form
    {
        Channel Data;
        symbolListBox SymbolListBox;
        IPlotContext Context;
        bool InitDone = false;
        private System.Windows.Forms.Timer CompilationTimer;
        string CompilationResult;

        public ChannelEdit(Channel data, IPlotContext trc)
        {
            int Ymargin = 5;
            Context = trc;
            Data = data;

            CompilationTimer = new System.Windows.Forms.Timer();
            CompilationTimer.Interval = 300;
            CompilationTimer.Tick += CompilationUpdate;

            InitializeComponent();

            SymbolListBox = new symbolListBox(Controls, Data.Device + "." + Data.Symbol, trc.Scope.Devices, new Point(RemoteDeviceCheckbox.Left, RemoteDeviceCheckbox.Top + RemoteDeviceCheckbox.Height + Ymargin));
            SymbolListBox.OnChanged += SymbolChanged;
            BuildLayout();
            KeyPreview = true;
            TextBoxName.Text = Data.Name;
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);
            buttonColor.BackColor = (Color)ColorTranslator.FromHtml(Data.Color);
            textBoxAddress.Text = Utils.AddressToStr(Data.Address);
            textBoxSize.Text = Data.Size.ToString();
            textBoxPeriod.Text = Data.SamplingTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
            textBoxUnit.Text = Data.Unit.ToString();
            textBoxYOffset.Text = (Data.YOffset * Data.YGain).ToString();
            textBoxXOffset.Text = Data.XOffset.ToString();
            textBoxYGain.Text = Data.YGain.ToString();
            checkBoxVisible.Checked = Data.IsVisible;
            comboBoxDisplay.SelectedIndex = (int)Data.Display;
            RemoteDeviceCheckbox.Checked = Data.hasDevice;
            localFunctionCheckbox.Checked = Data.hasFunction;

            RemoteDeviceCheckbox_CheckedChanged(null, null);
            localFunctionCheckbox_CheckedChanged(null, null);

            textBoxScript.Text = Data.Script;
            setupComboxLink(Data.linkXOffset, comboBoxXOffset, pictureBoxXOffset);
            setupComboxLink(Data.linkYOffset, comboBoxYOffset, pictureBoxYOffset);
            setupComboxLink(Data.linkYGain, comboBoxYGain, pictureBoxYGain);

            textBoxAddress.TextChanged += textBoxAddress_TextChanged;
            buttonCompile_Click(null, null);
            // compiles fine but failed while running: show why
            if (!string.IsNullOrEmpty(Data.RuntimeError) && CompilationResult.Contains("Successfull"))
            {
                CompilationResult = Data.RuntimeError;
            }
            InitDone = true;

        }
        // common dialog layout: general, scale and source on the left, the function on the right
        void BuildLayout()
        {
            labelXOffset.Text = "X offset (ms)";
            labelYOffset.Text = "Y offset";
            labelYGain.Text = "Y gain (/div)";
            labelSize.Text = "Size (bits)";
            foreach (var c in new[] { comboBoxXOffset, comboBoxYOffset, comboBoxYGain })
                c.Width = this.Px(124);
            var tip = new ToolTip();
            foreach (var p in new[] { pictureBoxXOffset, pictureBoxYOffset, pictureBoxYGain })
            {
                p.SizeMode = PictureBoxSizeMode.CenterImage;
                p.Size = new Size(this.Px(24), this.Px(24));
                p.BorderStyle = BorderStyle.None;
                tip.SetToolTip(p, "Shared with the channel selected on the right");
            }
            buttonCompile.AutoSize = true;
            labelBuild.AutoSize = true;
            labelBuild.MaximumSize = new Size(this.Px(300), 0);

            var L = new UI.DialogLayout(this);
            L.Float(SymbolListBox.List);
            L.Column()
                .Header(labelName, TextBoxName, buttonColor, checkBoxVisible)
                .Row(labelDisplay, comboBoxDisplay)
                .Row(labelUnit, textBoxUnit)
                .Section("Scale")
                .Row(labelXOffset, textBoxXOffset, pictureBoxXOffset, comboBoxXOffset)
                .Row(labelYOffset, textBoxYOffset, pictureBoxYOffset, comboBoxYOffset)
                .Row(labelYGain, textBoxYGain, pictureBoxYGain, comboBoxYGain)
                .Section("Source")
                .Options(null, RemoteDeviceCheckbox)
                .Row(SymbolListBox.labelSymbol, SymbolListBox.textBoxSymbol)
                .Row(labelAddress, textBoxAddress)
                .Row(labelSize, textBoxSize)
                .Row(labelPeriod, textBoxPeriod);
            L.Column(380, 0)
                .Section("Function")
                .Options(null, localFunctionCheckbox)
                .Wide(textBoxScript, 300)
                .WideRow(labelBuild, buttonCompile);
            L.Footer(buttonOK, buttonCancel, labelValidation);
            L.Done(fixedSize: true);
        }

        // link state of an offset / gain: accent when shared with another channel
        Image LinkIcon(bool linked) => Icons.Get(Icons.Link, linked ? Theme.Current.Accent : Theme.Current.Faint, DeviceDpi);

        void setupComboxLink(linkHolder lh, ComboBox c, PictureBox p)
        {
            var ctrls = Context.GetSignalList();
            string[] names = ctrls.Select(d => d.SignalBase.Name).ToArray();

            c.Items.Add("None");
            c.Items.AddRange(names);
            c.Items.Remove(Data.Name); // remove current channel which correspong to (None)

            // set up name list of already linked signal
            var idList = lh.GetLinkIdList();
            List<string> linkNameList = new();
            foreach (var id in idList)
            {
                var s = Context.GetSignalFromId(id);
                if (s != null)
                {
                    linkNameList.Add(s.DataBase.Name);
                }
            }


            // remove already linked signal
            foreach (string linkName in linkNameList)
            {
                if (c.Items.Contains(linkName))
                {
                    c.Items.Remove(linkName);
                }
            }

            p.Image = LinkIcon(false);
            c.SelectedIndex = 0;

            // select currnet linked signal
            var linkSignal = Context.GetSignalFromId(lh.SrcId);
            if (linkSignal != null)
            {
                int index = c.FindStringExact(linkSignal.DataBase.Name);
                if (index != -1)
                {
                    c.SelectedIndex = index;
                    p.Image = LinkIcon(true);
                }
            }
        }
        // Handle the key down event
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
        }
        private void ChannelEdit_Load(object sender, EventArgs e)
        {
            SymbolListBox.Focus();
        }
        private void SymbolChanged(SymbolData? sd)
        {
            if (sd != null)
            {
                if (sd.Resolved) // address read through a pointer: known once the target is connected
                    textBoxAddress.Text = "0x" + sd.Address;
                if (sd.Size > 0) // 0: size unknown (map without size), keep the one typed
                    textBoxSize.Text = (sd.Size).ToString();
            }
        }
        private void buttonOK_Click(object sender, EventArgs e)
        {
            validate();

            if (buttonOK.Enabled == true)
            {
                Data.Name = TextBoxName.Text;
                Data.Color = ColorTranslator.ToHtml(Color.FromArgb(colorDialog1.Color.ToArgb()));

                SymbolData? sd = SymbolListBox.GetSymbolData();

                if (sd != null) // Split the string into two parts
                {
                    Data.Device = sd.Device;
                    Data.Symbol = sd.Name;
                }
                else
                {
                    Data.Device = "";
                    Data.Symbol = "";
                }

                Data.Address = Utils.strToAddress(textBoxAddress.Text);
                Data.Unit = textBoxUnit.Text;
                Data.Size = int.Parse(textBoxSize.Text);
                Data.YGain = double.Parse(textBoxYGain.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                Data.YOffset = double.Parse(textBoxYOffset.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture) / Data.YGain;
                Data.XOffset = double.Parse(textBoxXOffset.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                Data.Display = (Utils.Display)comboBoxDisplay.SelectedIndex;
                Data.IsVisible = checkBoxVisible.Checked;
                Data.hasDevice = RemoteDeviceCheckbox.Checked;
                if (!Data.hasDevice)
                    Data.SamplingTime = double.Parse(textBoxPeriod.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                Data.hasFunction = localFunctionCheckbox.Checked;
                Data.Script = textBoxScript.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
        private void buttonColor_Click(object sender, EventArgs e)
        {
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);

            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                buttonColor.BackColor = colorDialog1.Color;
            }
            buttonOK.Focus();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void validate()
        {
            var messages = new List<string>();

            // Name
            if (Context.Exists(TextBoxName.Text) && TextBoxName.Text != Data.Name)
            {
                TextBoxName.BackColor = Theme.FieldBack(false);
                messages.Add("Name already exists");
            }
            else
                TextBoxName.BackColor = Theme.FieldBack(true);

            // Address
            string addrTest = textBoxAddress.Text;
            if (addrTest.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                addrTest = addrTest.Substring(2);
            if (UInt64.TryParse(addrTest, System.Globalization.NumberStyles.HexNumber, null, out _))
                textBoxAddress.BackColor = Theme.FieldBack(true);
            else
            {
                textBoxAddress.BackColor = Theme.FieldBack(false);
                messages.Add("Invalid address");
            }

            // Size
            bool sizeValid = int.TryParse(textBoxSize.Text.Replace(".", ","), out int sizeValue);
            if (sizeValid)
                textBoxSize.BackColor = Theme.FieldBack(true);
            else
            {
                textBoxSize.BackColor = Theme.FieldBack(false);
                messages.Add("Invalid size");
            }

            // Y Offset
            if (double.TryParse(textBoxYOffset.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                textBoxYOffset.BackColor = Theme.FieldBack(true);
            else
            {
                textBoxYOffset.BackColor = Theme.FieldBack(false);
                messages.Add("Invalid Y offset");
            }

            // X Offset
            if (double.TryParse(textBoxXOffset.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                textBoxXOffset.BackColor = Theme.FieldBack(true);
            else
            {
                textBoxXOffset.BackColor = Theme.FieldBack(false);
                messages.Add("Invalid X offset");
            }

            // Y Gain
            if (double.TryParse(textBoxYGain.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                textBoxYGain.BackColor = Theme.FieldBack(true);
            else
            {
                textBoxYGain.BackColor = Theme.FieldBack(false);
                messages.Add("Invalid Y gain");
            }

            // Period of a channel without device
            if (RemoteDeviceCheckbox.Checked
                || (double.TryParse(textBoxPeriod.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double period) && period > 0))
                textBoxPeriod.BackColor = Theme.FieldBack(true);
            else
            {
                textBoxPeriod.BackColor = Theme.FieldBack(false);
                messages.Add("Invalid period");
            }

            // Protocol V1 constraints
            if (RemoteDeviceCheckbox.Checked)
            {
                SymbolData? sd = SymbolListBox.GetSymbolData();
                if (sd != null)
                {
                    DeviceCtrl? dev = Context.Scope.Devices
                        .FirstOrDefault(d => d.Data.Name == sd.Device);

                    if (dev != null && dev.Data.Protocol == "V1")
                    {
                        // Size must match the firmware's fixed data size
                        int expectedBits = dev.Data.V1DataSize * 8;
                        if (sizeValid && sizeValue != expectedBits)
                        {
                            textBoxSize.BackColor = Theme.FieldBack(false);
                            messages.Add($"V1: size must be {expectedBits} bits ({dev.Data.V1DataSize} B per channel)");
                        }

                        // Channel count: only fails when adding to this device for the first time
                        int channelsOnDevice = Context.GetSignalList()
                            .Count(c => c.SignalBase.Device == sd.Device);
                        bool alreadyOnDevice = Data.Device == sd.Device
                                               && !string.IsNullOrEmpty(Data.Symbol);
                        if (!alreadyOnDevice && channelsOnDevice >= dev.Data.V1ChannelMax)
                            messages.Add($"V1: '{sd.Device}' already has {channelsOnDevice}/{dev.Data.V1ChannelMax} channels");
                    }
                }
            }

            labelValidation.Text = string.Join("  |  ", messages);
            buttonOK.Enabled = messages.Count == 0;
        }
        private void ChannelEdit_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {

        }

        private void ChannelEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if ((SymbolListBox.isFocus() == false) && (textBoxScript.Focused == false))
                {
                    buttonOK_Click(sender, e);
                }
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (SymbolListBox.isFocus())
                {
                    SymbolListBox.Echap();
                }
                else if (textBoxScript.Focused == true)
                {
                    labelBuild.Focus();
                }
                else
                {
                    buttonCancel_Click(sender, e);
                }
            }
        }

        private void textBoxXOffset_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void textBoxYOffset_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void textBoxYGain_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void textBoxShift_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void textBoxSize_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void textBoxAddress_TextChanged(object sender, EventArgs e)
        {
            if (textBoxAddress.Focused) // does the change comes from user through textbox adress?
            {
                string test;
                SymbolData? sd = SymbolListBox.GetSymbolData();
                if (sd != null)
                {
                    test = textBoxAddress.Text;
                    if (test.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        test = test.Substring(2);
                    }
                    if (test != sd.Address)
                    {
                        SymbolListBox.textBoxSymbol.Text = SymbolListBox.getNameDeviceFromSymbol(SymbolListBox.textBoxSymbol.Text);
                    }
                }
                else
                {
                    SymbolListBox.textBoxSymbol.Text = SymbolListBox.getNameDeviceFromSymbol(SymbolListBox.textBoxSymbol.Text);
                }
                validate();
            }
        }

        private void TextBoxName_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        void link(PictureBox p, TextBox t, linkHolder lhDst, linkHolder? lhSrc)
        {
            if (lhSrc == null)
            {
                p.Image = LinkIcon(false);
                lhDst.Unlink();
            }
            else
            {
                lhDst.Link(lhSrc);
                p.Image = LinkIcon(true);
                t.Text = lhDst.DataRef.Data.ToString();
            }
        }
        private void comboBoxXOffset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (InitDone == true)
            {
                linkHolder? lhSrc = null;
                string? signalName = (string?)comboBoxXOffset.SelectedItem;
                var s = Context.GetSignalFromName(signalName);
                if (s != null)
                {
                    lhSrc = s.SignalBase.linkXOffset;
                }

                link(pictureBoxXOffset, textBoxXOffset, Data.linkXOffset, lhSrc);
            }
        }
        private void comboBoxYOffset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (InitDone == true)
            {
                linkHolder? lhSrc = null;
                string? signalName = (string?)comboBoxYOffset.SelectedItem;
                var s = Context.GetSignalFromName(signalName);
                if (s != null)
                {
                    lhSrc = s.SignalBase.linkYOffset;
                }

                link(pictureBoxYOffset, textBoxYOffset, Data.linkYOffset, lhSrc);
            }
        }
        private void comboBoxYGain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (InitDone == true)
            {
                linkHolder? lhSrc = null;
                string? signalName = (string?)comboBoxYGain.SelectedItem;
                var s = Context.GetSignalFromName(signalName);
                if (s != null)
                {
                    lhSrc = s.SignalBase.linkYGain;
                }

                link(pictureBoxYGain, textBoxYGain, Data.linkYGain, lhSrc);
            }

        }

        private void RemoteDeviceCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            labelAddress.Enabled = RemoteDeviceCheckbox.Checked;
            textBoxAddress.Enabled = RemoteDeviceCheckbox.Checked;
            labelSize.Enabled = RemoteDeviceCheckbox.Checked;
            textBoxSize.Enabled = RemoteDeviceCheckbox.Checked;
            SymbolListBox.labelSymbol.Enabled = RemoteDeviceCheckbox.Checked;
            SymbolListBox.textBoxSymbol.Enabled = RemoteDeviceCheckbox.Checked;
            // without device the channel has its own period (a device channel uses the device sampling time)
            labelPeriod.Enabled = !RemoteDeviceCheckbox.Checked;
            textBoxPeriod.Enabled = !RemoteDeviceCheckbox.Checked;
            if (InitDone)
                validate();
        }

        private void textBoxPeriod_TextChanged(object sender, EventArgs e)
        {
            validate();
        }

        private void localFunctionCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            textBoxScript.Enabled = localFunctionCheckbox.Checked;
            labelBuild.Enabled = localFunctionCheckbox.Checked;
            buttonCompile.Enabled = localFunctionCheckbox.Checked;
        }

        private void textBoxScript_Leave(object sender, EventArgs e)
        {
            buttonCompile_Click(sender, e);
        }

        private void buttonCompile_Click(object sender, EventArgs e)
        {
            Func<int, double>? compiledScript = null;
            CompilationResult = ChannelCtrl.CompileScript(Data.Name, textBoxScript.Text,
                Context.ScriptGlobalsDict, out compiledScript);
            CompilationTimer.Start();
        }
        private void CompilationUpdate(object sender, EventArgs e)
        {
            labelBuild.Text = CompilationResult;
            CompilationTimer.Stop();
        }

    }
}
