namespace eScope
{
    partial class MarkerEdit
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
            textBoxPosition = new TextBox();
            colorDialog1 = new ColorDialog();
            checkBoxFixed = new CheckBox();
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
            label2.Location = new Point(17, 56);
            label2.Name = "label2";
            label2.Size = new Size(61, 15);
            label2.TabIndex = 12;
            label2.Text = "Position";
            // 
            // buttonOK
            // 
            buttonOK.Location = new Point(197, 109);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(75, 23);
            buttonOK.TabIndex = 4;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(88, 109);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 23);
            buttonCancel.TabIndex = 5;
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
            // textBoxPosition
            // 
            textBoxPosition.Location = new Point(88, 51);
            textBoxPosition.Name = "textBoxPosition";
            textBoxPosition.Size = new Size(193, 23);
            textBoxPosition.TabIndex = 3;
            textBoxPosition.TextChanged += textBoxPosition_TextChanged;
            // 
            // checkBoxFixed
            // 
            checkBoxFixed.AutoSize = true;
            checkBoxFixed.Location = new Point(90, 83);
            checkBoxFixed.Name = "checkBoxFixed";
            checkBoxFixed.Size = new Size(54, 19);
            checkBoxFixed.TabIndex = 13;
            checkBoxFixed.Text = "Fixed";
            checkBoxFixed.UseVisualStyleBackColor = true;
            checkBoxFixed.CheckedChanged += checkBoxFixed_CheckedChanged;
            // 
            // checkBoxVisible
            // 
            checkBoxVisible.AutoSize = true;
            checkBoxVisible.Location = new Point(197, 83);
            checkBoxVisible.Name = "checkBoxVisible";
            checkBoxVisible.Size = new Size(60, 19);
            checkBoxVisible.TabIndex = 14;
            checkBoxVisible.Text = "Visible";
            checkBoxVisible.UseVisualStyleBackColor = true;
            // 
            // MarkerEdit
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(297, 148);
            Controls.Add(checkBoxVisible);
            Controls.Add(checkBoxFixed);
            Controls.Add(textBoxPosition);
            Controls.Add(TextBoxName);
            Controls.Add(buttonColor);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Controls.Add(label2);
            Controls.Add(label1);
            Name = "MarkerEdit";
            Text = "Marker Edit";
            Load += MarkerEdit_Load;
            KeyDown += MarkerEdit_KeyDown;
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
        private TextBox textBoxPosition;
        private ColorDialog colorDialog1;
        private CheckBox checkBoxFixed;
        private CheckBox checkBoxVisible;
    }
}