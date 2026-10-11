namespace eScope
{
    partial class DeltaEdit
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            TextBoxName = new TextBox();
            label2 = new Label();
            buttonOK = new Button();
            buttonCancel = new Button();
            buttonColor = new Button();
            textBoxX1 = new TextBox();
            colorDialog1 = new ColorDialog();
            label3 = new Label();
            textBoxX2 = new TextBox();
            checkBoxVisible = new CheckBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(32, 25);
            label1.Name = "label1";
            label1.Size = new Size(39, 15);
            label1.TabIndex = 0;
            label1.Text = "Name";
            // 
            // TextBoxName
            // 
            TextBoxName.Location = new Point(88, 21);
            TextBoxName.Name = "TextBoxName";
            TextBoxName.Size = new Size(100, 23);
            TextBoxName.TabIndex = 1;
            TextBoxName.TextChanged += TextBoxName_TextChanged;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(32, 55);
            label2.Name = "label2";
            label2.Size = new Size(20, 15);
            label2.TabIndex = 12;
            label2.Text = "X1";
            // 
            // buttonOK
            // 
            buttonOK.Location = new Point(197, 131);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(75, 23);
            buttonOK.TabIndex = 5;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(88, 131);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 23);
            buttonCancel.TabIndex = 6;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // buttonColor
            // 
            buttonColor.Location = new Point(197, 21);
            buttonColor.Name = "buttonColor";
            buttonColor.Size = new Size(23, 23);
            buttonColor.TabIndex = 2;
            buttonColor.UseVisualStyleBackColor = true;
            buttonColor.Click += buttonColor_Click;
            // 
            // textBoxX1
            // 
            textBoxX1.Location = new Point(88, 51);
            textBoxX1.Name = "textBoxX1";
            textBoxX1.Size = new Size(193, 23);
            textBoxX1.TabIndex = 3;
            textBoxX1.TextChanged += textBoxPosition_TextChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(32, 83);
            label3.Name = "label3";
            label3.Size = new Size(20, 15);
            label3.TabIndex = 12;
            label3.Text = "X2";
            // 
            // textBoxX2
            // 
            textBoxX2.Location = new Point(88, 79);
            textBoxX2.Name = "textBoxX2";
            textBoxX2.Size = new Size(193, 23);
            textBoxX2.TabIndex = 4;
            textBoxX2.TextChanged += textBoxPosition_TextChanged;
            // 
            // checkBoxVisible
            // 
            checkBoxVisible.AutoSize = true;
            checkBoxVisible.Location = new Point(88, 108);
            checkBoxVisible.Name = "checkBoxVisible";
            checkBoxVisible.Size = new Size(60, 19);
            checkBoxVisible.TabIndex = 15;
            checkBoxVisible.Text = "Visible";
            checkBoxVisible.UseVisualStyleBackColor = true;
            // 
            // DeltaEdit
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(303, 167);
            Controls.Add(checkBoxVisible);
            Controls.Add(textBoxX2);
            Controls.Add(textBoxX1);
            Controls.Add(TextBoxName);
            Controls.Add(buttonColor);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Name = "DeltaEdit";
            Text = "Delta Edit";
            Load += DeltaEdit_Load;
            KeyDown += DeltaEdit_KeyDown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox TextBoxName;
        private Label label2;
        private Button buttonOK;
        private Button buttonCancel;
        private Button buttonColor;
        private TextBox textBoxX1;
        private ColorDialog colorDialog1;
        private Label label3;
        private TextBox textBoxX2;
        private CheckBox checkBoxVisible;
    }
}