using eScope.Plot;
using eScope.Symbol;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static OpenTK.Graphics.OpenGL.GL;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace eScope
{
    public partial class MarkerEdit : Form
    {
        Marker Data;
        IPlotContext Context;
        public MarkerEdit(Marker data, IPlotContext trc)
        {
            Data = data;
            Context = trc;
            InitializeComponent();
            checkBoxFixed.Text = "Fixed (% of the view)";
            var L = new UI.DialogLayout(this);
            L.Column()
                .Header(label1, TextBoxName, buttonColor, checkBoxVisible)
                .Section("Position")
                .Row(label2, textBoxPosition)
                .Options(null, checkBoxFixed);
            L.Footer(buttonOK, buttonCancel);
            L.Done();
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);
            buttonColor.BackColor = (Color)ColorTranslator.FromHtml(Data.Color);
            TextBoxName.Text = Data.Name;
            checkBoxFixed.Checked = (Data.Fixed!=0)?true:false;
            checkBoxVisible.Checked = Data.IsVisible;
            if (checkBoxFixed.Checked)
            {
                textBoxPosition.Text = Data.FixedPos.ToString();
                label2.Text = "Position (%)";
            }
            else
            {
                textBoxPosition.Text = Data.Pos.ToString();
                label2.Text = "Position (ms)";
            }
            IsValid();
            KeyPreview = true;
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
        private void MarkerEdit_Load(object sender, EventArgs e)
        {
            TextBoxName.Focus();
        }
        private void IsValid()
        {
            bool valid = true;
            double dNumber;
            string text = textBoxPosition.Text.Replace(",", ".");
            bool success = double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber);

            if (success)
            {
                if (checkBoxFixed.Checked == true)
                {
                    if ((dNumber < 0) || (dNumber > 100))
                    {
                        textBoxPosition.BackColor = Theme.FieldBack(false);
                        valid = false;
                    }
                    else
                    {
                        textBoxPosition.BackColor = Theme.FieldBack(true);
                    }
                }
                else
                {
                    textBoxPosition.BackColor = Theme.FieldBack(true);
                }
            }
            else
            {
                textBoxPosition.BackColor = Theme.FieldBack(false);
                valid = false;
            }

            if (Context.Exists(TextBoxName.Text) && (TextBoxName.Text != Data.Name))
            {
                TextBoxName.BackColor = Theme.FieldBack(false);
                valid = false;
            }
            else
            {
                TextBoxName.BackColor = Theme.FieldBack(true);
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
                if (double.TryParse(textBoxPosition.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dNumber))
                {
                    Data.Fixed = checkBoxFixed.Checked?1:0;
                    if (checkBoxFixed.Checked)
                    {
                        Data.FixedPos = dNumber;
                    }
                    else
                    {
                        Data.Pos = dNumber;
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


        private void MarkerEdit_KeyDown(object sender, KeyEventArgs e)
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
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);

            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                buttonColor.BackColor = colorDialog1.Color;
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

        private void checkBoxFixed_CheckedChanged(object sender, EventArgs e)
        {
            if(checkBoxFixed.Checked)
            {
                label2.Text = "Position (%)";
                textBoxPosition.Text = Data.FixedPos.ToString();
            }
            else
            {
                label2.Text = "Position (ms)";
                textBoxPosition.Text = Data.Pos.ToString();
            }
            IsValid();
        }
    }
}
