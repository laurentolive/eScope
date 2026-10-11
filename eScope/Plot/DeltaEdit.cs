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
    public partial class DeltaEdit : Form
    {
        Delta Data;
        IPlotContext Context;
        public DeltaEdit(Delta data, IPlotContext trc)
        {
            Data = data;
            Context = trc;
            InitializeComponent();
            colorDialog1.Color = (Color)ColorTranslator.FromHtml(Data.Color);
            buttonColor.BackColor = (Color)ColorTranslator.FromHtml(Data.Color);
            TextBoxName.Text = Data.Name;
            checkBoxVisible.Checked = Data.IsVisible;
            textBoxX1.Text = Data.X1.ToString();
            textBoxX2.Text = Data.X2.ToString();
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
        private void DeltaEdit_Load(object sender, EventArgs e)
        {
            TextBoxName.Focus();
        }
        private void IsValid()
        {
            bool valid = true;
            
            
            if (Context.Exists(TextBoxName.Text) &&(TextBoxName.Text!= Data.Name))
            {
                TextBoxName.BackColor = Color.Salmon;
                valid = false;
            }
            else
            {
                TextBoxName.BackColor = Color.White;
            }

            double dNumber;
            string text = textBoxX1.Text.Replace(",", ".");
            bool success = double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber);
            if (success)

            {
                textBoxX1.BackColor = Color.White;
            }
            else
            {
                textBoxX1.BackColor = Color.Salmon;
                valid = false;
            }

            text = textBoxX2.Text.Replace(",", ".");
            success = double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber);
            if (success)
            {
                textBoxX2.BackColor = Color.White;
            }
            else
            {
                textBoxX2.BackColor = Color.Salmon;
                valid = false;
            }
            buttonOK.Enabled = valid;
        }
        private void buttonOK_Click(object sender, EventArgs e)
        {
            double dNumber;
            IsValid();
            if (buttonOK.Enabled == true)
            {
                Data.Name = TextBoxName.Text;
                Data.Color = ColorTranslator.ToHtml(Color.FromArgb(colorDialog1.Color.ToArgb()));
                Data.IsVisible = checkBoxVisible.Checked;

                if (double.TryParse(textBoxX1.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber))
                {
                    Data.X1 = dNumber;
                }

                if (double.TryParse(textBoxX2.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber))
                {
                    Data.X2 = dNumber;
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


        private void DeltaEdit_KeyDown(object sender, KeyEventArgs e)
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
