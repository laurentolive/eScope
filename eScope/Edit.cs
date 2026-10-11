using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Reflection;

namespace eScope
{
    public partial class Edit : Form
    {
        private Dictionary<string, Control> formControls = new Dictionary<string, Control>();
        public  object Data;
        private Button ButtonCancel;
        private Button ButtonOK;


        public Edit(object editObject)
        {
            Data = editObject;

            InitializeComponent();

            int paddingX = 20;
            int paddingY = 20;
            int spaceH = 40;
            int spaceW = 100;
            int x = paddingX, y = paddingY;


            // Get the properties of MyClass using reflection
            PropertyInfo[] properties = Data.GetType().GetProperties();
            // Dynamically create form controls and populate them with data
            foreach (var property in properties)
            {
                if ((property.PropertyType == typeof(string)) || (property.PropertyType == typeof(double)) || (property.PropertyType == typeof(int)))
                {
                    x = paddingX;
                    y += spaceH;

                    // Create labels
                    Label label = new Label();
                    label.Text = property.Name + ":";
                    label.AutoSize = true;
                    label.Location = new System.Drawing.Point(x, y);
                    Controls.Add(label);                // Add controls to form
                    x += spaceW;

                    // Create textboxes for simple values or nested objects
                    TextBox textBox = new TextBox();
                    textBox.Tag = property.Name; // Store property name as tag for later reference

                    textBox.Text = property.GetValue(Data)?.ToString();
                    textBox.Location = new System.Drawing.Point(x, y);
                    // Keep track of form controls for later use
                    formControls[property.Name] = textBox;
                    Controls.Add(textBox);
                    x += spaceW;
                }
            }

            x = paddingX;
            y += spaceH;
            this.ButtonCancel = new System.Windows.Forms.Button();
            this.ButtonCancel.Location = new System.Drawing.Point(x, y);
            this.ButtonCancel.Name = "ButtonCancel";
            this.ButtonCancel.Size = new System.Drawing.Size(75, 23);
            this.ButtonCancel.TabIndex = 8;
            this.ButtonCancel.Text = "Cancel";
            this.ButtonCancel.UseVisualStyleBackColor = true;
            this.ButtonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);

            x += spaceW;
            this.ButtonOK = new System.Windows.Forms.Button();
            this.ButtonOK.Location = new System.Drawing.Point(x, y);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 23);
            this.ButtonOK.TabIndex = 7;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            this.ButtonOK.Click += new System.EventHandler(this.ButtonOK_Click);

            x += spaceW;
            y += spaceH;

            Text = "Edit";
            Width = x + paddingX;
            Height = y + paddingY;

        }

        private void UpdateObjectValue()
        {
            // Get the properties of MyClass using reflection
            PropertyInfo[] properties = Data.GetType().GetProperties();
            // Dynamically create form controls and populate them with data
            foreach (var property in properties)
            {
                Control widget = Controls[property.Name];

                if(widget != null )
                {
                    // Update the property value
                    property.SetValue(Data, Convert.ChangeType(((TextBox)widget).Text, property.PropertyType));
                }
            }
        }
        private void ButtonOK_Click(object? sender, EventArgs e)
        {
            UpdateObjectValue();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            this.Close();
        }
    }
}
