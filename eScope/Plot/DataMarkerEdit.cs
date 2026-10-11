using eScope.Plot;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace eScope
{
    public partial class DataMarkerEdit : Form
    {
        DataMarker Data;
        IPlotContext Context;
        bool colorChosen = false; // user picked a color: stop following the channel color
        public DataMarkerEdit(DataMarker data, IPlotContext trc)
        {
            Data = data;
            Context = trc;
            InitializeComponent();
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);
            buttonColor.BackColor = (Color)ColorTranslator.FromHtml(Data.Color);
            TextBoxName.Text = Data.Name;
            checkBoxFixed.Checked = (Data.Fixed != 0) ? true : false;
            checkBoxVisible.Checked = Data.IsVisible;

            var ctrls = Context.GetSignalList();
            comboBoxChannel.Items.AddRange(ctrls.Select(d => d.SignalBase.Name).ToArray());
            int index = ctrls.FindIndex(c => c.SignalBase.Name == Data.Channel);
            if (index == -1 && ctrls.Count > 0)
            {
                index = 0;
            }
            // a color different from its channel one was chosen before: keep it
            if (index >= 0)
            {
                colorChosen = !string.Equals(ctrls[index].SignalBase.Color, Data.Color, StringComparison.OrdinalIgnoreCase);
            }
            comboBoxChannel.SelectedIndex = index;

            SetPositionText();
            IsValid();
            KeyPreview = true;
        }
        private void SetPositionText()
        {
            if (checkBoxFixed.Checked)
            {
                textBoxPosition.Text = Data.FixedPos.ToString();
                label2.Text = "Position (%)";
            }
            else
            {
                textBoxPosition.Text = Data.Value.ToString();
                label2.Text = "Value";
            }
        }
        // Handle the key down event
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Check if the pressed key is Escape
            if (e.KeyCode == Keys.Escape)
            {
                // Close the form
                this.Close();
            }
        }
        private void DataMarkerEdit_Load(object sender, EventArgs e)
        {
            TextBoxName.Focus();
        }
        private void IsValid()
        {
            bool valid = true;
            double dNumber;
            string text = textBoxPosition.Text.Replace(",", ".");
            bool success = double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber);

            if (success && (!checkBoxFixed.Checked || ((dNumber >= 0) && (dNumber <= 100))))
            {
                textBoxPosition.BackColor = Color.White;
            }
            else
            {
                textBoxPosition.BackColor = Color.Salmon;
                valid = false;
            }

            if (Context.Exists(TextBoxName.Text) && (TextBoxName.Text != Data.Name))
            {
                TextBoxName.BackColor = Color.Salmon;
                valid = false;
            }
            else
            {
                TextBoxName.BackColor = Color.White;
            }

            if (comboBoxChannel.SelectedIndex < 0)
            {
                valid = false;
            }
            buttonOK.Enabled = valid;
        }
        private void buttonOK_Click(object sender, EventArgs e)
        {
            IsValid();
            if (buttonOK.Enabled == true)
            {
                Data.Name = TextBoxName.Text;
                Data.Color = ColorTranslator.ToHtml(Color.FromArgb(colorDialog1.Color.ToArgb()));
                Data.IsVisible = checkBoxVisible.Checked;
                Data.Channel = comboBoxChannel.Items[comboBoxChannel.SelectedIndex]?.ToString() ?? string.Empty;
                if (double.TryParse(textBoxPosition.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dNumber))
                {
                    Data.Fixed = checkBoxFixed.Checked ? 1 : 0;
                    if (checkBoxFixed.Checked)
                    {
                        Data.FixedPos = dNumber;
                    }
                    else
                    {
                        Data.Value = dNumber;
                    }
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void DataMarkerEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                buttonOK_Click(sender, e);
            }
            if (e.KeyCode == Keys.Escape)
            {
                buttonCancel_Click(sender, e);
            }
        }
        private void buttonColor_Click(object sender, EventArgs e)
        {
            colorDialog1.Color = buttonColor.BackColor;

            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                buttonColor.BackColor = colorDialog1.Color;
                colorChosen = true;
            }
            buttonOK.Focus();
        }

        private void textBoxPosition_TextChanged(object sender, EventArgs e)
        {
            IsValid();
        }

        private void TextBoxName_TextChanged(object sender, EventArgs e)
        {
            IsValid();
        }

        private void comboBoxChannel_SelectedIndexChanged(object sender, EventArgs e)
        {
            // default color is the one of the associated channel
            var c = Context.GetSignalFromName(comboBoxChannel.SelectedItem?.ToString());
            if (!colorChosen && c != null)
            {
                colorDialog1.Color = ColorTranslator.FromHtml(c.SignalBase.Color);
                buttonColor.BackColor = colorDialog1.Color;
            }
            IsValid();
        }

        private void checkBoxFixed_CheckedChanged(object sender, EventArgs e)
        {
            SetPositionText();
            IsValid();
        }
    }
}
