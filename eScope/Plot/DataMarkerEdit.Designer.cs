namespace eScope
{
    partial class DataMarkerEdit
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
            label3 = new Label();
            buttonOK = new Button();
            buttonCancel = new Button();
            buttonColor = new Button();
            textBoxPosition = new TextBox();
            comboBoxChannel = new ComboBox();
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
            // buttonColor
            //
            buttonColor.Location = new Point(197, 21);
            buttonColor.Name = "buttonColor";
            buttonColor.Size = new Size(23, 23);
            buttonColor.TabIndex = 2;
            buttonColor.UseVisualStyleBackColor = true;
            buttonColor.Click += buttonColor_Click;
            //
            // label3
            //
            label3.Location = new Point(17, 56);
            label3.Name = "label3";
            label3.Size = new Size(65, 15);
            label3.TabIndex = 15;
            label3.Text = "Channel";
            //
            // comboBoxChannel
            //
            comboBoxChannel.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxChannel.Location = new Point(88, 51);
            comboBoxChannel.Name = "comboBoxChannel";
            comboBoxChannel.Size = new Size(193, 23);
            comboBoxChannel.TabIndex = 3;
            comboBoxChannel.SelectedIndexChanged += comboBoxChannel_SelectedIndexChanged;
            //
            // label2
            //
            label2.Location = new Point(17, 86);
            label2.Name = "label2";
            label2.Size = new Size(65, 15);
            label2.TabIndex = 12;
            label2.Text = "Value";
            //
            // textBoxPosition
            //
            textBoxPosition.Location = new Point(88, 81);
            textBoxPosition.Name = "textBoxPosition";
            textBoxPosition.Size = new Size(193, 23);
            textBoxPosition.TabIndex = 4;
            textBoxPosition.TextChanged += textBoxPosition_TextChanged;
            //
            // checkBoxFixed
            //
            checkBoxFixed.AutoSize = true;
            checkBoxFixed.Location = new Point(90, 113);
            checkBoxFixed.Name = "checkBoxFixed";
            checkBoxFixed.Size = new Size(54, 19);
            checkBoxFixed.TabIndex = 5;
            checkBoxFixed.Text = "Fixed";
            checkBoxFixed.UseVisualStyleBackColor = true;
            checkBoxFixed.CheckedChanged += checkBoxFixed_CheckedChanged;
            //
            // checkBoxVisible
            //
            checkBoxVisible.AutoSize = true;
            checkBoxVisible.Location = new Point(197, 113);
            checkBoxVisible.Name = "checkBoxVisible";
            checkBoxVisible.Size = new Size(60, 19);
            checkBoxVisible.TabIndex = 6;
            checkBoxVisible.Text = "Visible";
            checkBoxVisible.UseVisualStyleBackColor = true;
            //
            // buttonOK
            //
            buttonOK.Location = new Point(197, 139);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(75, 23);
            buttonOK.TabIndex = 7;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            //
            // buttonCancel
            //
            buttonCancel.Location = new Point(88, 139);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 23);
            buttonCancel.TabIndex = 8;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            //
            // DataMarkerEdit
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(297, 178);
            Controls.Add(checkBoxVisible);
            Controls.Add(checkBoxFixed);
            Controls.Add(textBoxPosition);
            Controls.Add(comboBoxChannel);
            Controls.Add(TextBoxName);
            Controls.Add(buttonColor);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Name = "DataMarkerEdit";
            Text = "Data Marker Edit";
            Load += DataMarkerEdit_Load;
            KeyDown += DataMarkerEdit_KeyDown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox TextBoxName;
        private Label label2;
        private Label label3;
        private Button buttonOK;
        private Button buttonCancel;
        private Button buttonColor;
        private TextBox textBoxPosition;
        private ComboBox comboBoxChannel;
        private ColorDialog colorDialog1;
        private CheckBox checkBoxFixed;
        private CheckBox checkBoxVisible;
    }
}
