namespace eScope
{
    partial class ChannelEdit
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
            labelName = new Label();
            TextBoxName = new TextBox();
            openFileDialog1 = new OpenFileDialog();
            textBoxAddress = new TextBox();
            labelAddress = new Label();
            textBoxSize = new TextBox();
            labelSize = new Label();
            buttonOK = new Button();
            buttonCancel = new Button();
            buttonColor = new Button();
            textBoxYGain = new TextBox();
            labelYGain = new Label();
            textBoxYOffset = new TextBox();
            labelYOffset = new Label();
            textBoxXOffset = new TextBox();
            labelXOffset = new Label();
            textBoxUnit = new TextBox();
            labelUnit = new Label();
            colorDialog1 = new ColorDialog();
            labelDisplay = new Label();
            comboBoxDisplay = new ComboBox();
            checkBoxVisible = new CheckBox();
            comboBoxYOffset = new ComboBox();
            comboBoxXOffset = new ComboBox();
            comboBoxYGain = new ComboBox();
            pictureBoxXOffset = new PictureBox();
            pictureBoxYOffset = new PictureBox();
            pictureBoxYGain = new PictureBox();
            textBoxScript = new TextBox();
            RemoteDeviceCheckbox = new CheckBox();
            localFunctionCheckbox = new CheckBox();
            labelBuild = new Label();
            buttonCompile = new Button();
            labelValidation = new Label();
            labelPeriod = new Label();
            textBoxPeriod = new TextBox();
            ((System.ComponentModel.ISupportInitialize)pictureBoxXOffset).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxYOffset).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxYGain).BeginInit();
            SuspendLayout();
            // 
            // labelName
            // 
            labelName.AutoSize = true;
            labelName.Location = new Point(38, 33);
            labelName.Name = "labelName";
            labelName.Size = new Size(49, 20);
            labelName.TabIndex = 0;
            labelName.Text = "Name";
            // 
            // TextBoxName
            // 
            TextBoxName.Location = new Point(101, 28);
            TextBoxName.Margin = new Padding(3, 4, 3, 4);
            TextBoxName.Name = "TextBoxName";
            TextBoxName.Size = new Size(224, 27);
            TextBoxName.TabIndex = 2;
            TextBoxName.TextChanged += TextBoxName_TextChanged;
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // textBoxAddress
            // 
            textBoxAddress.Location = new Point(101, 314);
            textBoxAddress.Margin = new Padding(3, 4, 3, 4);
            textBoxAddress.Name = "textBoxAddress";
            textBoxAddress.Size = new Size(150, 27);
            textBoxAddress.TabIndex = 15;
            // 
            // labelAddress
            // 
            labelAddress.AutoSize = true;
            labelAddress.Location = new Point(38, 320);
            labelAddress.Name = "labelAddress";
            labelAddress.Size = new Size(62, 20);
            labelAddress.TabIndex = 14;
            labelAddress.Text = "Address";
            // 
            // textBoxSize
            // 
            textBoxSize.Location = new Point(329, 314);
            textBoxSize.Margin = new Padding(3, 4, 3, 4);
            textBoxSize.Name = "textBoxSize";
            textBoxSize.Size = new Size(115, 27);
            textBoxSize.TabIndex = 16;
            textBoxSize.TextChanged += textBoxSize_TextChanged;
            // 
            // labelSize
            // 
            labelSize.AutoSize = true;
            labelSize.Location = new Point(286, 321);
            labelSize.Name = "labelSize";
            labelSize.Size = new Size(36, 20);
            labelSize.TabIndex = 16;
            labelSize.Text = "Size";
            // 
            // buttonOK
            // 
            buttonOK.Location = new Point(265, 430);
            buttonOK.Margin = new Padding(3, 4, 3, 4);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(86, 31);
            buttonOK.TabIndex = 101;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(101, 430);
            buttonCancel.Margin = new Padding(3, 4, 3, 4);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(86, 31);
            buttonCancel.TabIndex = 100;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // buttonColor
            // 
            buttonColor.Location = new Point(418, 28);
            buttonColor.Margin = new Padding(3, 4, 3, 4);
            buttonColor.Name = "buttonColor";
            buttonColor.Size = new Size(26, 31);
            buttonColor.TabIndex = 4;
            buttonColor.UseVisualStyleBackColor = true;
            buttonColor.Click += buttonColor_Click;
            // 
            // textBoxYGain
            // 
            textBoxYGain.Location = new Point(104, 188);
            textBoxYGain.Margin = new Padding(3, 4, 3, 4);
            textBoxYGain.Name = "textBoxYGain";
            textBoxYGain.Size = new Size(114, 27);
            textBoxYGain.TabIndex = 13;
            textBoxYGain.TextChanged += textBoxYGain_TextChanged;
            // 
            // labelYGain
            // 
            labelYGain.AutoSize = true;
            labelYGain.Location = new Point(38, 191);
            labelYGain.Name = "labelYGain";
            labelYGain.Size = new Size(51, 20);
            labelYGain.TabIndex = 25;
            labelYGain.Text = "Y Gain";
            // 
            // textBoxYOffset
            // 
            textBoxYOffset.Location = new Point(104, 146);
            textBoxYOffset.Margin = new Padding(3, 4, 3, 4);
            textBoxYOffset.Name = "textBoxYOffset";
            textBoxYOffset.Size = new Size(114, 27);
            textBoxYOffset.TabIndex = 11;
            textBoxYOffset.TextChanged += textBoxYOffset_TextChanged;
            // 
            // labelYOffset
            // 
            labelYOffset.AutoSize = true;
            labelYOffset.Location = new Point(38, 149);
            labelYOffset.Name = "labelYOffset";
            labelYOffset.Size = new Size(61, 20);
            labelYOffset.TabIndex = 23;
            labelYOffset.Text = "Y Offset";
            // 
            // textBoxXOffset
            // 
            textBoxXOffset.Location = new Point(104, 106);
            textBoxXOffset.Margin = new Padding(3, 4, 3, 4);
            textBoxXOffset.Name = "textBoxXOffset";
            textBoxXOffset.Size = new Size(114, 27);
            textBoxXOffset.TabIndex = 7;
            textBoxXOffset.TextChanged += textBoxXOffset_TextChanged;
            // 
            // labelXOffset
            // 
            labelXOffset.AutoSize = true;
            labelXOffset.Location = new Point(38, 109);
            labelXOffset.Name = "labelXOffset";
            labelXOffset.Size = new Size(62, 20);
            labelXOffset.TabIndex = 21;
            labelXOffset.Text = "X Offset";
            // 
            // textBoxUnit
            // 
            textBoxUnit.Location = new Point(329, 67);
            textBoxUnit.Margin = new Padding(3, 4, 3, 4);
            textBoxUnit.Name = "textBoxUnit";
            textBoxUnit.Size = new Size(115, 27);
            textBoxUnit.TabIndex = 6;
            // 
            // labelUnit
            // 
            labelUnit.AutoSize = true;
            labelUnit.Location = new Point(289, 72);
            labelUnit.Name = "labelUnit";
            labelUnit.Size = new Size(36, 20);
            labelUnit.TabIndex = 27;
            labelUnit.Text = "Unit";
            labelUnit.Click += label9_Click;
            // 
            // labelDisplay
            // 
            labelDisplay.AutoSize = true;
            labelDisplay.Location = new Point(38, 72);
            labelDisplay.Name = "labelDisplay";
            labelDisplay.Size = new Size(58, 20);
            labelDisplay.TabIndex = 32;
            labelDisplay.Text = "Display";
            // 
            // comboBoxDisplay
            // 
            comboBoxDisplay.DisplayMember = "Signed Decimal";
            comboBoxDisplay.FormattingEnabled = true;
            comboBoxDisplay.Items.AddRange(new object[] { "Signed Decimal", "Unsigned Decimal", "Hexadecimal", "Float", "Enum" });
            comboBoxDisplay.Location = new Point(102, 67);
            comboBoxDisplay.Margin = new Padding(3, 4, 3, 4);
            comboBoxDisplay.Name = "comboBoxDisplay";
            comboBoxDisplay.Size = new Size(150, 28);
            comboBoxDisplay.TabIndex = 5;
            comboBoxDisplay.ValueMember = "Signed Decimal";
            // 
            // checkBoxVisible
            // 
            checkBoxVisible.AutoSize = true;
            checkBoxVisible.Location = new Point(337, 32);
            checkBoxVisible.Margin = new Padding(3, 4, 3, 4);
            checkBoxVisible.Name = "checkBoxVisible";
            checkBoxVisible.Size = new Size(75, 24);
            checkBoxVisible.TabIndex = 3;
            checkBoxVisible.Text = "Visible";
            checkBoxVisible.UseVisualStyleBackColor = true;
            // 
            // comboBoxYOffset
            // 
            comboBoxYOffset.FormattingEnabled = true;
            comboBoxYOffset.Location = new Point(265, 145);
            comboBoxYOffset.Name = "comboBoxYOffset";
            comboBoxYOffset.Size = new Size(179, 28);
            comboBoxYOffset.TabIndex = 12;
            comboBoxYOffset.SelectedIndexChanged += comboBoxYOffset_SelectedIndexChanged;
            // 
            // comboBoxXOffset
            // 
            comboBoxXOffset.FormattingEnabled = true;
            comboBoxXOffset.Location = new Point(265, 105);
            comboBoxXOffset.Name = "comboBoxXOffset";
            comboBoxXOffset.Size = new Size(179, 28);
            comboBoxXOffset.TabIndex = 8;
            comboBoxXOffset.SelectedIndexChanged += comboBoxXOffset_SelectedIndexChanged;
            // 
            // comboBoxYGain
            // 
            comboBoxYGain.FormattingEnabled = true;
            comboBoxYGain.Location = new Point(265, 187);
            comboBoxYGain.Name = "comboBoxYGain";
            comboBoxYGain.Size = new Size(179, 28);
            comboBoxYGain.TabIndex = 14;
            comboBoxYGain.SelectedIndexChanged += comboBoxYGain_SelectedIndexChanged;
            // 
            // pictureBoxXOffset
            // 
            pictureBoxXOffset.Location = new Point(232, 109);
            pictureBoxXOffset.Name = "pictureBoxXOffset";
            pictureBoxXOffset.Size = new Size(20, 20);
            pictureBoxXOffset.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxXOffset.TabIndex = 39;
            pictureBoxXOffset.TabStop = false;
            // 
            // pictureBoxYOffset
            // 
            pictureBoxYOffset.Location = new Point(232, 149);
            pictureBoxYOffset.Name = "pictureBoxYOffset";
            pictureBoxYOffset.Size = new Size(20, 20);
            pictureBoxYOffset.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxYOffset.TabIndex = 40;
            pictureBoxYOffset.TabStop = false;
            // 
            // pictureBoxYGain
            // 
            pictureBoxYGain.Location = new Point(232, 191);
            pictureBoxYGain.Name = "pictureBoxYGain";
            pictureBoxYGain.Size = new Size(20, 20);
            pictureBoxYGain.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxYGain.TabIndex = 41;
            pictureBoxYGain.TabStop = false;
            // 
            // textBoxScript
            // 
            textBoxScript.Location = new Point(459, 68);
            textBoxScript.Multiline = true;
            textBoxScript.Name = "textBoxScript";
            textBoxScript.Size = new Size(532, 359);
            textBoxScript.TabIndex = 17;
            textBoxScript.Leave += textBoxScript_Leave;
            // 
            // RemoteDeviceCheckbox
            // 
            RemoteDeviceCheckbox.AutoSize = true;
            RemoteDeviceCheckbox.Location = new Point(38, 249);
            RemoteDeviceCheckbox.Margin = new Padding(3, 4, 3, 4);
            RemoteDeviceCheckbox.Name = "RemoteDeviceCheckbox";
            RemoteDeviceCheckbox.Size = new Size(132, 24);
            RemoteDeviceCheckbox.TabIndex = 64;
            RemoteDeviceCheckbox.Text = "Remote Device";
            RemoteDeviceCheckbox.UseVisualStyleBackColor = true;
            RemoteDeviceCheckbox.CheckedChanged += RemoteDeviceCheckbox_CheckedChanged;
            //
            // labelPeriod
            //
            labelPeriod.AutoSize = true;
            labelPeriod.Location = new Point(239, 250);
            labelPeriod.Name = "labelPeriod";
            labelPeriod.Size = new Size(87, 20);
            labelPeriod.TabIndex = 105;
            labelPeriod.Text = "Period (ms)";
            //
            // textBoxPeriod
            //
            textBoxPeriod.Location = new Point(329, 246);
            textBoxPeriod.Margin = new Padding(3, 4, 3, 4);
            textBoxPeriod.Name = "textBoxPeriod";
            textBoxPeriod.Size = new Size(115, 27);
            textBoxPeriod.TabIndex = 106;
            textBoxPeriod.TextChanged += textBoxPeriod_TextChanged;
            //
            // localFunctionCheckbox
            // 
            localFunctionCheckbox.AutoSize = true;
            localFunctionCheckbox.Location = new Point(459, 33);
            localFunctionCheckbox.Margin = new Padding(3, 4, 3, 4);
            localFunctionCheckbox.Name = "localFunctionCheckbox";
            localFunctionCheckbox.Size = new Size(126, 24);
            localFunctionCheckbox.TabIndex = 65;
            localFunctionCheckbox.Text = "Local Function";
            localFunctionCheckbox.UseVisualStyleBackColor = true;
            localFunctionCheckbox.CheckedChanged += localFunctionCheckbox_CheckedChanged;
            // 
            // labelBuild
            // 
            labelBuild.AutoSize = true;
            labelBuild.Location = new Point(459, 430);
            labelBuild.Name = "labelBuild";
            labelBuild.Size = new Size(158, 20);
            labelBuild.TabIndex = 102;
            labelBuild.Text = "Function not compiled";
            // 
            // buttonCompile
            // 
            buttonCompile.Location = new Point(905, 430);
            buttonCompile.Margin = new Padding(3, 4, 3, 4);
            buttonCompile.Name = "buttonCompile";
            buttonCompile.Size = new Size(86, 31);
            buttonCompile.TabIndex = 103;
            buttonCompile.Text = "Compile";
            buttonCompile.UseVisualStyleBackColor = true;
            buttonCompile.Click += buttonCompile_Click;
            //
            // labelValidation
            //
            labelValidation.AutoSize = false;
            labelValidation.ForeColor = Color.Red;
            labelValidation.Location = new Point(38, 467);
            labelValidation.Name = "labelValidation";
            labelValidation.Size = new Size(420, 36);
            labelValidation.TabIndex = 104;
            labelValidation.Text = "";
            //
            // ChannelEdit
            //
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1028, 510);
            Controls.Add(labelValidation);
            Controls.Add(textBoxPeriod);
            Controls.Add(labelPeriod);
            Controls.Add(buttonCompile);
            Controls.Add(labelBuild);
            Controls.Add(localFunctionCheckbox);
            Controls.Add(RemoteDeviceCheckbox);
            Controls.Add(textBoxScript);
            Controls.Add(pictureBoxYGain);
            Controls.Add(pictureBoxYOffset);
            Controls.Add(pictureBoxXOffset);
            Controls.Add(comboBoxYGain);
            Controls.Add(comboBoxXOffset);
            Controls.Add(comboBoxYOffset);
            Controls.Add(comboBoxDisplay);
            Controls.Add(labelDisplay);
            Controls.Add(TextBoxName);
            Controls.Add(textBoxUnit);
            Controls.Add(buttonColor);
            Controls.Add(textBoxAddress);
            Controls.Add(textBoxSize);
            Controls.Add(textBoxYGain);
            Controls.Add(textBoxYOffset);
            Controls.Add(textBoxXOffset);
            Controls.Add(buttonOK);
            Controls.Add(buttonCancel);
            Controls.Add(labelUnit);
            Controls.Add(labelYGain);
            Controls.Add(labelYOffset);
            Controls.Add(labelXOffset);
            Controls.Add(labelSize);
            Controls.Add(labelAddress);
            Controls.Add(labelName);
            Controls.Add(checkBoxVisible);
            Margin = new Padding(3, 4, 3, 4);
            Name = "ChannelEdit";
            Text = "Channel Edit";
            Load += ChannelEdit_Load;
            KeyDown += ChannelEdit_KeyDown;
            PreviewKeyDown += ChannelEdit_PreviewKeyDown;
            ((System.ComponentModel.ISupportInitialize)pictureBoxXOffset).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxYOffset).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxYGain).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelName;
        private TextBox TextBoxName;
        private OpenFileDialog openFileDialog1;
        private TextBox textBoxAddress;
        private Label labelAddress;
        private TextBox textBoxSize;
        private Label labelSize;
        private Button buttonOK;
        private Button buttonCancel;
        private Button buttonColor;
        private TextBox textBoxYGain;
        private Label labelYGain;
        private TextBox textBoxYOffset;
        private Label labelYOffset;
        private TextBox textBoxXOffset;
        private Label labelXOffset;
        private TextBox textBoxUnit;
        private Label labelUnit;
        private ColorDialog colorDialog1;
        private Label labelDisplay;
        private ComboBox comboBoxDisplay;
        private CheckBox checkBoxVisible;
        private ComboBox comboBoxYOffset;
        private ComboBox comboBoxXOffset;
        private ComboBox comboBoxYGain;
        private PictureBox pictureBoxXOffset;
        private PictureBox pictureBoxYOffset;
        private PictureBox pictureBoxYGain;
        private TextBox textBoxScript;
        private CheckBox RemoteDeviceCheckbox;
        private CheckBox localFunctionCheckbox;
        private Label labelBuild;
        private Button buttonCompile;
        private Label labelValidation;
        private Label labelPeriod;
        private TextBox textBoxPeriod;
    }
}