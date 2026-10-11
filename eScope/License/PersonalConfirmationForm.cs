using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.License
{
    public class PersonalConfirmationForm : Form
    {
        public bool Accepted { get; private set; }

        public PersonalConfirmationForm()
        {
            InitializeComponent();

        }
        private void InitializeComponent()
        {
            label2 = new Label();
            checkBox1 = new CheckBox();
            button1 = new Button();
            SuspendLayout();
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 35);
            label2.Name = "label2";
            label2.Size = new Size(401, 120);
            label2.TabIndex = 0;
            label2.Text = "This software is free for personal, non-commercial use only.\r\n\r\nPersonal use does NOT include\r\n• Work or business use\r\n• Client or consulting work\r\n• Revenue-generating activity";
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(22, 185);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(538, 24);
            checkBox1.TabIndex = 1;
            checkBox1.Text = "I confirm I will use this software only for personal, non-commercial purposes.";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            button1.Location = new Point(234, 235);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 2;
            button1.Text = "Continue";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // PersonalConfirmationForm
            // 
            ClientSize = new Size(572, 285);
            Controls.Add(button1);
            Controls.Add(checkBox1);
            Controls.Add(label2);
            Name = "PersonalConfirmationForm";
            ResumeLayout(false);
            PerformLayout();

        }
        private Label label2;
        private CheckBox checkBox1;
        private Button button1;

        private void button1_Click(object sender, EventArgs e)
        {
            if (!checkBox1.Checked)
            {
                MessageBox.Show(
                    "You must confirm personal use to continue.",
                    "Confirmation Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Accepted = true;
            DialogResult = DialogResult.OK;

        }
    }
}
