namespace eScope
{
    partial class AnnotationEdit
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
            labelAnnotation = new Label();
            TextBoxAnnotation = new TextBox();
            labelTextPositionX = new Label();
            buttonOK = new Button();
            buttonCancel = new Button();
            buttonColor = new Button();
            textBoxTextPositionX = new TextBox();
            colorDialog1 = new ColorDialog();
            textBoxTextPositionY = new TextBox();
            folderBrowserDialog1 = new FolderBrowserDialog();
            textBoxTipPositionX = new TextBox();
            labelTextPositionY = new Label();
            labelTipPositionX = new Label();
            comboBoxTipChannel = new ComboBox();
            labelTipChannel = new Label();
            SuspendLayout();
            // 
            // labelAnnotation
            // 
            labelAnnotation.AutoSize = true;
            labelAnnotation.Location = new Point(53, 35);
            labelAnnotation.Name = "labelAnnotation";
            labelAnnotation.Size = new Size(83, 20);
            labelAnnotation.TabIndex = 0;
            labelAnnotation.Text = "Annotation";
            // 
            // TextBoxAnnotation
            // 
            TextBoxAnnotation.Location = new Point(146, 29);
            TextBoxAnnotation.Margin = new Padding(3, 4, 3, 4);
            TextBoxAnnotation.Name = "TextBoxAnnotation";
            TextBoxAnnotation.Size = new Size(141, 27);
            TextBoxAnnotation.TabIndex = 1;
            TextBoxAnnotation.TextChanged += TextBoxName_TextChanged;
            // 
            // labelTextPositionX
            // 
            labelTextPositionX.AutoSize = true;
            labelTextPositionX.Location = new Point(33, 75);
            labelTextPositionX.Name = "labelTextPositionX";
            labelTextPositionX.Size = new Size(105, 20);
            labelTextPositionX.TabIndex = 12;
            labelTextPositionX.Text = "Text Position X";
            // 
            // buttonOK
            // 
            buttonOK.Location = new Point(202, 247);
            buttonOK.Margin = new Padding(3, 4, 3, 4);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(86, 31);
            buttonOK.TabIndex = 7;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(37, 247);
            buttonCancel.Margin = new Padding(3, 4, 3, 4);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(86, 31);
            buttonCancel.TabIndex = 8;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // buttonColor
            // 
            buttonColor.Location = new Point(295, 29);
            buttonColor.Margin = new Padding(3, 4, 3, 4);
            buttonColor.Name = "buttonColor";
            buttonColor.Size = new Size(26, 31);
            buttonColor.TabIndex = 2;
            buttonColor.UseVisualStyleBackColor = true;
            buttonColor.Click += buttonColor_Click;
            // 
            // textBoxTextPositionX
            // 
            textBoxTextPositionX.Location = new Point(146, 68);
            textBoxTextPositionX.Margin = new Padding(3, 4, 3, 4);
            textBoxTextPositionX.Name = "textBoxTextPositionX";
            textBoxTextPositionX.Size = new Size(141, 27);
            textBoxTextPositionX.TabIndex = 3;
            textBoxTextPositionX.TextChanged += textBoxPosition_TextChanged;
            // 
            // textBoxTextPositionY
            // 
            textBoxTextPositionY.Location = new Point(146, 107);
            textBoxTextPositionY.Margin = new Padding(3, 4, 3, 4);
            textBoxTextPositionY.Name = "textBoxTextPositionY";
            textBoxTextPositionY.Size = new Size(141, 27);
            textBoxTextPositionY.TabIndex = 4;
            textBoxTextPositionY.TextChanged += textBoxPosition_TextChanged;
            // 
            // textBoxTipPositionX
            // 
            textBoxTipPositionX.Location = new Point(146, 145);
            textBoxTipPositionX.Margin = new Padding(3, 4, 3, 4);
            textBoxTipPositionX.Name = "textBoxTipPositionX";
            textBoxTipPositionX.Size = new Size(141, 27);
            textBoxTipPositionX.TabIndex = 5;
            textBoxTipPositionX.TextChanged += textBoxPosition_TextChanged;
            // 
            // labelTextPositionY
            // 
            labelTextPositionY.AutoSize = true;
            labelTextPositionY.Location = new Point(33, 115);
            labelTextPositionY.Name = "labelTextPositionY";
            labelTextPositionY.Size = new Size(104, 20);
            labelTextPositionY.TabIndex = 15;
            labelTextPositionY.Text = "Text Position Y";
            // 
            // labelTipPositionX
            // 
            labelTipPositionX.AutoSize = true;
            labelTipPositionX.Location = new Point(39, 155);
            labelTipPositionX.Name = "labelTipPositionX";
            labelTipPositionX.Size = new Size(99, 20);
            labelTipPositionX.TabIndex = 17;
            labelTipPositionX.Text = "Tip Position X";
            // 
            // comboBoxTipChannel
            // 
            comboBoxTipChannel.FormattingEnabled = true;
            comboBoxTipChannel.Location = new Point(146, 184);
            comboBoxTipChannel.Margin = new Padding(3, 4, 3, 4);
            comboBoxTipChannel.Name = "comboBoxTipChannel";
            comboBoxTipChannel.Size = new Size(141, 28);
            comboBoxTipChannel.TabIndex = 6;
            // 
            // labelTipChannel
            // 
            labelTipChannel.AutoSize = true;
            labelTipChannel.Location = new Point(49, 195);
            labelTipChannel.Name = "labelTipChannel";
            labelTipChannel.Size = new Size(87, 20);
            labelTipChannel.TabIndex = 19;
            labelTipChannel.Text = "Tip Channel";
            // 
            // AnnotationEdit
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(342, 305);
            Controls.Add(labelTipChannel);
            Controls.Add(comboBoxTipChannel);
            Controls.Add(labelTipPositionX);
            Controls.Add(textBoxTipPositionX);
            Controls.Add(labelTextPositionY);
            Controls.Add(textBoxTextPositionY);
            Controls.Add(textBoxTextPositionX);
            Controls.Add(TextBoxAnnotation);
            Controls.Add(buttonColor);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Controls.Add(labelTextPositionX);
            Controls.Add(labelAnnotation);
            Margin = new Padding(3, 4, 3, 4);
            Name = "AnnotationEdit";
            Text = "Annotation Edit";
            Load += AnnotationEdit_Load;
            KeyDown += AnnotationEdit_KeyDown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelAnnotation;
        private TextBox TextBoxAnnotation;
        private Label labelTextPositionX;
        private Button buttonOK;
        private Button buttonCancel;
        private Button buttonColor;
        private TextBox textBoxTextPositionX;
        private ColorDialog colorDialog1;
        private TextBox textBoxTextPositionY;
        private FolderBrowserDialog folderBrowserDialog1;
        private TextBox textBoxTipPositionX;
        private Label labelTextPositionY;
        private Label labelTipPositionX;
        private ComboBox comboBoxTipChannel;
        private Label labelTipChannel;
    }
}