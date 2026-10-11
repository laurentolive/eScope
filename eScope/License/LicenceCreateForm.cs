using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.License
{
    using System;
    using System.Windows.Forms;

    public class LicenseCreateForm : Form
    {
        public LicenseType SelectedLicense { get; private set; } = LicenseType.Demonstration;

        public LicenseCreateForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            label1 = new Label();
            personal = new RadioButton();
            professional = new RadioButton();
            button1 = new Button();
            demonstration = new RadioButton();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(28, 27);
            label1.Name = "label1";
            label1.Size = new Size(220, 20);
            label1.TabIndex = 0;
            label1.Text = "How will you use this software ?";
            // 
            // personal
            // 
            personal.AutoSize = true;
            personal.Location = new Point(28, 62);
            personal.Name = "personal";
            personal.Size = new Size(289, 44);
            personal.TabIndex = 1;
            personal.TabStop = true;
            personal.Text = "Personal (Free)\r\nFor non-commercial, personal use only.";
            personal.UseVisualStyleBackColor = true;
            // 
            // professional
            // 
            professional.AutoSize = true;
            professional.Location = new Point(28, 112);
            professional.Name = "professional";
            professional.Size = new Size(301, 44);
            professional.TabIndex = 2;
            professional.TabStop = true;
            professional.Text = "Professional (Paid)\r\nRequired for work, business, or client use.";
            professional.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            button1.Location = new Point(154, 216);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 4;
            button1.Text = "Continue";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // demonstration
            // 
            demonstration.AutoSize = true;
            demonstration.Location = new Point(28, 162);
            demonstration.Name = "demonstration";
            demonstration.Size = new Size(326, 44);
            demonstration.TabIndex = 3;
            demonstration.TabStop = true;
            demonstration.Text = "Demonstration (Free)\r\n1 month trial for work, business, or client use.";
            demonstration.UseVisualStyleBackColor = true;
            // 
            // LicenseCreateForm
            // 
            ClientSize = new Size(402, 260);
            Controls.Add(demonstration);
            Controls.Add(button1);
            Controls.Add(professional);
            Controls.Add(personal);
            Controls.Add(label1);
            Name = "LicenseCreateForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Licence Creation";
            ResumeLayout(false);
            PerformLayout();

        }
        private Label label1;
        private RadioButton personal;
        private RadioButton professional;
        private Button button1;
        private RadioButton demonstration;

        private void button1_Click(object sender, EventArgs e)
        {
                if (personal.Checked)
                    SelectedLicense = LicenseType.Personal;
                else if (professional.Checked)
                    SelectedLicense = LicenseType.Professional;
                else //if (demonstration.Checked)
                    SelectedLicense = LicenseType.Demonstration;

                this.DialogResult = DialogResult.OK;
                this.Close();
        }
    }
}
