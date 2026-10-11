using eScope.Plot;
using eScope.Symbol;
using eScope.Watch;
using Microsoft.Scripting.Utils;
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
    public partial class AnnotationEdit : Form
    {
        Annotation Data;
        IPlotContext Context;
        public AnnotationEdit(Annotation data, IPlotContext trc)
        {
            Data = data;
            Context = trc;
            InitializeComponent();
            labelAnnotation.Text = "Text";
            labelTextPositionX.Text = "X (ms)";
            labelTextPositionY.Text = "Y";
            labelTipChannel.Text = "Channel";
            labelTipPositionX.Text = "X (ms)";
            var L = new UI.DialogLayout(this);
            L.Column()
                .Header(labelAnnotation, TextBoxAnnotation, buttonColor)
                .Section("Text position")
                .Row(labelTextPositionX, textBoxTextPositionX)
                .Row(labelTextPositionY, textBoxTextPositionY)
                .Section("Arrow tip")
                .Row(labelTipChannel, comboBoxTipChannel)
                .Row(labelTipPositionX, textBoxTipPositionX);
            L.Footer(buttonOK, buttonCancel);
            L.Done();
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);
            buttonColor.BackColor = (Color)ColorTranslator.FromHtml(Data.Color);
            TextBoxAnnotation.Text = Data.Name;
            textBoxTextPositionX.Text = Math.Round(Data.TextLocationX, 3).ToString();
            textBoxTextPositionY.Text = Math.Round(Data.TextLocationY, 3).ToString();
            textBoxTipPositionX.Text = Math.Round(Data.TipLocationX, 3).ToString();
            var ctrls = Context.GetSignalList();
            string[] names = ctrls.Select(d => d.SignalBase.Name).ToArray();
            comboBoxTipChannel.Items.AddRange(names);

            int index = ctrls.FindIndex(c => c.SignalBase.Name == Data.Channel);

            if (index == -1)
            {
                index = 0;
            }
            comboBoxTipChannel.SelectedIndex = index;
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
        private void AnnotationEdit_Load(object sender, EventArgs e)
        {
            TextBoxAnnotation.Focus();
        }
        private bool ValidateNumber(System.Windows.Forms.TextBox tb)
        {
            bool isValid = true;
            double dNumber;
            string text = tb.Text.Replace(",", ".");
            bool success = double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber);
            if (success)
            {
                tb.BackColor = Theme.FieldBack(true);
            }
            else
            {
                tb.BackColor = Theme.FieldBack(false);
                isValid = false;
            }
            return isValid;
        }
        private void IsValid()
        {
            bool isValid = true;
            if (Context.Exists(TextBoxAnnotation.Text) &&(TextBoxAnnotation.Text!= Data.Name))
            {
                TextBoxAnnotation.BackColor = Theme.FieldBack(false);
                isValid = false;
            }
            else
            {
                TextBoxAnnotation.BackColor = Theme.FieldBack(true);
            }

            isValid &= ValidateNumber(textBoxTextPositionX);
            isValid &= ValidateNumber(textBoxTextPositionY);
            isValid &= ValidateNumber(textBoxTipPositionX);

            buttonOK.Enabled = isValid;
        }
        private void buttonOK_Click(object sender, EventArgs e)
        {
            double dNumber;
            IsValid();
            if (buttonOK.Enabled == true)
            {
                Data.Name = TextBoxAnnotation.Text;
                Data.Color = ColorTranslator.ToHtml(Color.FromArgb(colorDialog1.Color.ToArgb()));

                if (double.TryParse(textBoxTextPositionX.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber))
                {
                    Data.TextLocationX = dNumber;
                }

                if (double.TryParse(textBoxTextPositionY.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber))
                {
                    Data.TextLocationY = dNumber;
                }
                if (double.TryParse(textBoxTipPositionX.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber))
                {
                    Data.TipLocationX = dNumber;
                }
                if((comboBoxTipChannel.Items != null)
                    &&(comboBoxTipChannel.Items.Count > comboBoxTipChannel.SelectedIndex)
                    && (comboBoxTipChannel.Items[comboBoxTipChannel.SelectedIndex] != null)) {
                    Data.Channel = comboBoxTipChannel.Items[comboBoxTipChannel.SelectedIndex].ToString();
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


        private void AnnotationEdit_KeyDown(object sender, KeyEventArgs e)
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
    }
}
