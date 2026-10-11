namespace eScope
{
    partial class DeviceEdit
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
            radioButtonSerial = new RadioButton();
            radioButtonTCP = new RadioButton();
            radioButtonRTT = new RadioButton();
            panelRTT = new Panel();
            comboBoxRttTarget = new ComboBox();
            labelRttTarget = new Label();
            comboBoxRttProbe = new ComboBox();
            labelRttProbe = new Label();
            buttonRefreshProbes = new Button();
            labelRttHost = new Label();
            textBoxRttHost = new TextBox();
            labelRttPort = new Label();
            textBoxRttPort = new TextBox();
            panelSerial = new Panel();
            textBoxBaudrate = new TextBox();
            label4 = new Label();
            label3 = new Label();
            comboBoxPort = new ComboBox();
            panelTCP = new Panel();
            textBoxTcpPort = new TextBox();
            label7 = new Label();
            textBoxAddress = new TextBox();
            label6 = new Label();
            label8 = new Label();
            textBoxSymbol = new TextBox();
            openFileDialog1 = new OpenFileDialog();
            buttonBrowse = new Button();
            label9 = new Label();
            comboBoxProtocol = new ComboBox();
            buttonOK = new Button();
            buttonCancel = new Button();
            groupBox1 = new GroupBox();
            buttonGdbPath = new Button();
            textBoxGdbPath = new TextBox();
            labelGDBPath = new Label();
            groupBox2 = new GroupBox();
            textBoxMapRegex = new TextBox();
            labelMapRegex = new Label();
            radioMap = new RadioButton();
            radioGDB = new RadioButton();
            textBoxSamplingTime = new TextBox();
            labelSamplingTime = new Label();
            panelV1 = new Panel();
            labelV1Channels = new Label();
            textBoxV1Channels = new TextBox();
            labelV1DataSize = new Label();
            textBoxV1DataSize = new TextBox();
            panelRTT.SuspendLayout();
            panelSerial.SuspendLayout();
            panelTCP.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            panelV1.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(37, 33);
            label1.Name = "label1";
            label1.Size = new Size(49, 20);
            label1.TabIndex = 0;
            label1.Text = "Name";
            // 
            // TextBoxName
            // 
            TextBoxName.Location = new Point(101, 28);
            TextBoxName.Margin = new Padding(3, 4, 3, 4);
            TextBoxName.Name = "TextBoxName";
            TextBoxName.Size = new Size(114, 27);
            TextBoxName.TabIndex = 1;
            // 
            // radioButtonSerial
            // 
            radioButtonSerial.AutoSize = true;
            radioButtonSerial.Checked = true;
            radioButtonSerial.Location = new Point(23, 33);
            radioButtonSerial.Margin = new Padding(3, 4, 3, 4);
            radioButtonSerial.Name = "radioButtonSerial";
            radioButtonSerial.Size = new Size(67, 24);
            radioButtonSerial.TabIndex = 2;
            radioButtonSerial.TabStop = true;
            radioButtonSerial.Text = "Serial";
            radioButtonSerial.UseVisualStyleBackColor = true;
            radioButtonSerial.CheckedChanged += radioButtonSerial_CheckedChanged;
            // 
            // radioButtonTCP
            // 
            radioButtonTCP.AutoSize = true;
            radioButtonTCP.Location = new Point(93, 33);
            radioButtonTCP.Margin = new Padding(3, 4, 3, 4);
            radioButtonTCP.Name = "radioButtonTCP";
            radioButtonTCP.Size = new Size(54, 24);
            radioButtonTCP.TabIndex = 3;
            radioButtonTCP.TabStop = true;
            radioButtonTCP.Text = "TCP";
            radioButtonTCP.UseVisualStyleBackColor = true;
            radioButtonTCP.CheckedChanged += radioButtonTCP_CheckedChanged;
            // 
            // radioButtonRTT
            // 
            radioButtonRTT.AutoSize = true;
            radioButtonRTT.Location = new Point(155, 33);
            radioButtonRTT.Margin = new Padding(3, 4, 3, 4);
            radioButtonRTT.Name = "radioButtonRTT";
            radioButtonRTT.Size = new Size(93, 24);
            radioButtonRTT.TabIndex = 4;
            radioButtonRTT.TabStop = true;
            radioButtonRTT.Text = "RTT/SWD";
            radioButtonRTT.UseVisualStyleBackColor = true;
            radioButtonRTT.CheckedChanged += radioButtonRTT_CheckedChanged;
            // 
            // panelRTT
            // 
            panelRTT.Controls.Add(comboBoxRttTarget);
            panelRTT.Controls.Add(labelRttTarget);
            panelRTT.Controls.Add(comboBoxRttProbe);
            panelRTT.Controls.Add(labelRttProbe);
            panelRTT.Controls.Add(buttonRefreshProbes);
            panelRTT.Controls.Add(labelRttHost);
            panelRTT.Controls.Add(textBoxRttHost);
            panelRTT.Controls.Add(labelRttPort);
            panelRTT.Controls.Add(textBoxRttPort);
            panelRTT.Location = new Point(37, 140);
            panelRTT.Name = "panelRTT";
            panelRTT.Size = new Size(646, 140);
            panelRTT.TabIndex = 20;
            panelRTT.Visible = false;
            // 
            // comboBoxRttTarget
            // 
            comboBoxRttTarget.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBoxRttTarget.AutoCompleteSource = AutoCompleteSource.ListItems;
            comboBoxRttTarget.FormattingEnabled = true;
            comboBoxRttTarget.Location = new Point(65, 13);
            comboBoxRttTarget.Margin = new Padding(3, 4, 3, 4);
            comboBoxRttTarget.Name = "comboBoxRttTarget";
            comboBoxRttTarget.Size = new Size(563, 28);
            comboBoxRttTarget.TabIndex = 1;
            // 
            // labelRttTarget
            // 
            labelRttTarget.AutoSize = true;
            labelRttTarget.Location = new Point(4, 18);
            labelRttTarget.Name = "labelRttTarget";
            labelRttTarget.Size = new Size(50, 20);
            labelRttTarget.TabIndex = 0;
            labelRttTarget.Text = "Target";
            // 
            // comboBoxRttProbe
            // 
            comboBoxRttProbe.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxRttProbe.FormattingEnabled = true;
            comboBoxRttProbe.Location = new Point(65, 53);
            comboBoxRttProbe.Margin = new Padding(3, 4, 3, 4);
            comboBoxRttProbe.Name = "comboBoxRttProbe";
            comboBoxRttProbe.Size = new Size(486, 28);
            comboBoxRttProbe.TabIndex = 3;
            // 
            // labelRttProbe
            // 
            labelRttProbe.AutoSize = true;
            labelRttProbe.Location = new Point(4, 58);
            labelRttProbe.Name = "labelRttProbe";
            labelRttProbe.Size = new Size(48, 20);
            labelRttProbe.TabIndex = 2;
            labelRttProbe.Text = "Probe";
            // 
            // buttonRefreshProbes
            // 
            buttonRefreshProbes.Location = new Point(559, 50);
            buttonRefreshProbes.Margin = new Padding(3, 4, 3, 4);
            buttonRefreshProbes.Name = "buttonRefreshProbes";
            buttonRefreshProbes.Size = new Size(80, 31);
            buttonRefreshProbes.TabIndex = 4;
            buttonRefreshProbes.Text = "Refresh";
            buttonRefreshProbes.UseVisualStyleBackColor = true;
            buttonRefreshProbes.Click += buttonRefreshProbes_Click;
            // 
            // labelRttHost
            // 
            labelRttHost.AutoSize = true;
            labelRttHost.Location = new Point(4, 103);
            labelRttHost.Name = "labelRttHost";
            labelRttHost.Size = new Size(40, 20);
            labelRttHost.TabIndex = 5;
            labelRttHost.Text = "Host";
            // 
            // textBoxRttHost
            // 
            textBoxRttHost.Location = new Point(50, 98);
            textBoxRttHost.Margin = new Padding(3, 4, 3, 4);
            textBoxRttHost.Name = "textBoxRttHost";
            textBoxRttHost.Size = new Size(250, 27);
            textBoxRttHost.TabIndex = 6;
            textBoxRttHost.Text = "127.0.0.1";
            // 
            // labelRttPort
            // 
            labelRttPort.AutoSize = true;
            labelRttPort.Location = new Point(314, 103);
            labelRttPort.Name = "labelRttPort";
            labelRttPort.Size = new Size(35, 20);
            labelRttPort.TabIndex = 7;
            labelRttPort.Text = "Port";
            // 
            // textBoxRttPort
            // 
            textBoxRttPort.Location = new Point(357, 98);
            textBoxRttPort.Margin = new Padding(3, 4, 3, 4);
            textBoxRttPort.Name = "textBoxRttPort";
            textBoxRttPort.Size = new Size(90, 27);
            textBoxRttPort.TabIndex = 8;
            textBoxRttPort.Text = "9090";
            // 
            // panelSerial
            // 
            panelSerial.Controls.Add(textBoxBaudrate);
            panelSerial.Controls.Add(label4);
            panelSerial.Controls.Add(label3);
            panelSerial.Controls.Add(comboBoxPort);
            panelSerial.Location = new Point(37, 140);
            panelSerial.Margin = new Padding(3, 4, 3, 4);
            panelSerial.Name = "panelSerial";
            panelSerial.Size = new Size(314, 133);
            panelSerial.TabIndex = 5;
            // 
            // textBoxBaudrate
            // 
            textBoxBaudrate.Location = new Point(88, 56);
            textBoxBaudrate.Margin = new Padding(3, 4, 3, 4);
            textBoxBaudrate.Name = "textBoxBaudrate";
            textBoxBaudrate.Size = new Size(114, 27);
            textBoxBaudrate.TabIndex = 3;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(11, 61);
            label4.Name = "label4";
            label4.Size = new Size(69, 20);
            label4.TabIndex = 2;
            label4.Text = "Baudrate";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(11, 20);
            label3.Name = "label3";
            label3.Size = new Size(35, 20);
            label3.TabIndex = 1;
            label3.Text = "Port";
            // 
            // comboBoxPort
            // 
            comboBoxPort.FormattingEnabled = true;
            comboBoxPort.Location = new Point(64, 15);
            comboBoxPort.Margin = new Padding(3, 4, 3, 4);
            comboBoxPort.Name = "comboBoxPort";
            comboBoxPort.Size = new Size(138, 28);
            comboBoxPort.TabIndex = 0;
            // 
            // panelTCP
            // 
            panelTCP.Controls.Add(textBoxTcpPort);
            panelTCP.Controls.Add(label7);
            panelTCP.Controls.Add(textBoxAddress);
            panelTCP.Controls.Add(label6);
            panelTCP.Location = new Point(368, 140);
            panelTCP.Margin = new Padding(3, 4, 3, 4);
            panelTCP.Name = "panelTCP";
            panelTCP.Size = new Size(314, 133);
            panelTCP.TabIndex = 6;
            // 
            // textBoxTcpPort
            // 
            textBoxTcpPort.Location = new Point(88, 56);
            textBoxTcpPort.Margin = new Padding(3, 4, 3, 4);
            textBoxTcpPort.Name = "textBoxTcpPort";
            textBoxTcpPort.Size = new Size(114, 27);
            textBoxTcpPort.TabIndex = 5;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(18, 61);
            label7.Name = "label7";
            label7.Size = new Size(35, 20);
            label7.TabIndex = 4;
            label7.Text = "Port";
            // 
            // textBoxAddress
            // 
            textBoxAddress.Location = new Point(88, 15);
            textBoxAddress.Margin = new Padding(3, 4, 3, 4);
            textBoxAddress.Name = "textBoxAddress";
            textBoxAddress.Size = new Size(114, 27);
            textBoxAddress.TabIndex = 4;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(18, 20);
            label6.Name = "label6";
            label6.Size = new Size(62, 20);
            label6.TabIndex = 1;
            label6.Text = "Address";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(23, 84);
            label8.Name = "label8";
            label8.Size = new Size(86, 20);
            label8.TabIndex = 0;
            label8.Text = "Symbol File";
            // 
            // textBoxSymbol
            // 
            textBoxSymbol.Location = new Point(107, 79);
            textBoxSymbol.Margin = new Padding(3, 4, 3, 4);
            textBoxSymbol.Name = "textBoxSymbol";
            textBoxSymbol.Size = new Size(361, 27);
            textBoxSymbol.TabIndex = 1;
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // buttonBrowse
            // 
            buttonBrowse.Location = new Point(486, 80);
            buttonBrowse.Margin = new Padding(3, 4, 3, 4);
            buttonBrowse.Name = "buttonBrowse";
            buttonBrowse.Size = new Size(86, 31);
            buttonBrowse.TabIndex = 7;
            buttonBrowse.Text = "Browse";
            buttonBrowse.UseVisualStyleBackColor = true;
            buttonBrowse.Click += buttonBrowse_Click;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(34, 461);
            label9.Name = "label9";
            label9.Size = new Size(65, 20);
            label9.TabIndex = 8;
            label9.Text = "Protocol";
            // 
            // comboBoxProtocol
            // 
            comboBoxProtocol.FormattingEnabled = true;
            comboBoxProtocol.Items.AddRange(new object[] { "V1", "V2", "V3" });
            comboBoxProtocol.Location = new Point(101, 457);
            comboBoxProtocol.Margin = new Padding(3, 4, 3, 4);
            comboBoxProtocol.Name = "comboBoxProtocol";
            comboBoxProtocol.Size = new Size(138, 28);
            comboBoxProtocol.TabIndex = 9;
            comboBoxProtocol.SelectedIndexChanged += comboBoxProtocol_SelectedIndexChanged;
            // 
            // buttonOK
            // 
            buttonOK.Location = new Point(334, 548);
            buttonOK.Margin = new Padding(3, 4, 3, 4);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new Size(86, 31);
            buttonOK.TabIndex = 10;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(241, 548);
            buttonCancel.Margin = new Padding(3, 4, 3, 4);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(86, 31);
            buttonCancel.TabIndex = 11;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(radioButtonSerial);
            groupBox1.Controls.Add(radioButtonTCP);
            groupBox1.Controls.Add(radioButtonRTT);
            groupBox1.Location = new Point(37, 67);
            groupBox1.Margin = new Padding(3, 4, 3, 4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(3, 4, 3, 4);
            groupBox1.Size = new Size(340, 68);
            groupBox1.TabIndex = 12;
            groupBox1.TabStop = false;
            groupBox1.Text = "Communication";
            // 
            // buttonGdbPath
            // 
            buttonGdbPath.Location = new Point(486, 119);
            buttonGdbPath.Margin = new Padding(3, 4, 3, 4);
            buttonGdbPath.Name = "buttonGdbPath";
            buttonGdbPath.Size = new Size(86, 31);
            buttonGdbPath.TabIndex = 15;
            buttonGdbPath.Text = "Browse";
            buttonGdbPath.UseVisualStyleBackColor = true;
            buttonGdbPath.Click += buttonGdbPath_Click;
            // 
            // textBoxGdbPath
            // 
            textBoxGdbPath.Location = new Point(107, 117);
            textBoxGdbPath.Margin = new Padding(3, 4, 3, 4);
            textBoxGdbPath.Name = "textBoxGdbPath";
            textBoxGdbPath.Size = new Size(361, 27);
            textBoxGdbPath.TabIndex = 14;
            // 
            // labelGDBPath
            // 
            labelGDBPath.AutoSize = true;
            labelGDBPath.Location = new Point(23, 123);
            labelGDBPath.Name = "labelGDBPath";
            labelGDBPath.Size = new Size(73, 20);
            labelGDBPath.TabIndex = 13;
            labelGDBPath.Text = "GDB path";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(textBoxMapRegex);
            groupBox2.Controls.Add(labelMapRegex);
            groupBox2.Controls.Add(radioMap);
            groupBox2.Controls.Add(buttonGdbPath);
            groupBox2.Controls.Add(radioGDB);
            groupBox2.Controls.Add(textBoxGdbPath);
            groupBox2.Controls.Add(label8);
            groupBox2.Controls.Add(labelGDBPath);
            groupBox2.Controls.Add(textBoxSymbol);
            groupBox2.Controls.Add(buttonBrowse);
            groupBox2.Location = new Point(37, 283);
            groupBox2.Margin = new Padding(3, 4, 3, 4);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new Padding(3, 4, 3, 4);
            groupBox2.Size = new Size(646, 167);
            groupBox2.TabIndex = 13;
            groupBox2.TabStop = false;
            groupBox2.Text = "Symbols";
            // 
            // textBoxMapRegex
            // 
            textBoxMapRegex.Location = new Point(107, 117);
            textBoxMapRegex.Margin = new Padding(3, 4, 3, 4);
            textBoxMapRegex.Name = "textBoxMapRegex";
            textBoxMapRegex.Size = new Size(361, 27);
            textBoxMapRegex.TabIndex = 17;
            // 
            // labelMapRegex
            // 
            labelMapRegex.AutoSize = true;
            labelMapRegex.Location = new Point(23, 123);
            labelMapRegex.Name = "labelMapRegex";
            labelMapRegex.Size = new Size(80, 20);
            labelMapRegex.TabIndex = 16;
            labelMapRegex.Text = "Map regex";
            // 
            // radioMap
            // 
            radioMap.AutoSize = true;
            radioMap.Checked = true;
            radioMap.Location = new Point(23, 33);
            radioMap.Margin = new Padding(3, 4, 3, 4);
            radioMap.Name = "radioMap";
            radioMap.Size = new Size(60, 24);
            radioMap.TabIndex = 2;
            radioMap.TabStop = true;
            radioMap.Text = "Map";
            radioMap.UseVisualStyleBackColor = true;
            radioMap.CheckedChanged += radioMap_CheckedChanged;
            // 
            // radioGDB
            // 
            radioGDB.AutoSize = true;
            radioGDB.Location = new Point(85, 33);
            radioGDB.Margin = new Padding(3, 4, 3, 4);
            radioGDB.Name = "radioGDB";
            radioGDB.Size = new Size(60, 24);
            radioGDB.TabIndex = 3;
            radioGDB.TabStop = true;
            radioGDB.Text = "GDB";
            radioGDB.UseVisualStyleBackColor = true;
            radioGDB.CheckedChanged += radioGDB_CheckedChanged;
            // 
            // textBoxSamplingTime
            // 
            textBoxSamplingTime.Location = new Point(394, 462);
            textBoxSamplingTime.Margin = new Padding(3, 4, 3, 4);
            textBoxSamplingTime.Name = "textBoxSamplingTime";
            textBoxSamplingTime.Size = new Size(114, 27);
            textBoxSamplingTime.TabIndex = 5;
            // 
            // labelSamplingTime
            // 
            labelSamplingTime.AutoSize = true;
            labelSamplingTime.Location = new Point(282, 465);
            labelSamplingTime.Name = "labelSamplingTime";
            labelSamplingTime.Size = new Size(109, 20);
            labelSamplingTime.TabIndex = 4;
            labelSamplingTime.Text = "Sampling Time";
            // 
            // panelV1
            // 
            panelV1.Controls.Add(labelV1Channels);
            panelV1.Controls.Add(textBoxV1Channels);
            panelV1.Controls.Add(labelV1DataSize);
            panelV1.Controls.Add(textBoxV1DataSize);
            panelV1.Location = new Point(34, 497);
            panelV1.Name = "panelV1";
            panelV1.Size = new Size(420, 35);
            panelV1.TabIndex = 20;
            // 
            // labelV1Channels
            // 
            labelV1Channels.AutoSize = true;
            labelV1Channels.Location = new Point(0, 8);
            labelV1Channels.Name = "labelV1Channels";
            labelV1Channels.Size = new Size(71, 20);
            labelV1Channels.TabIndex = 0;
            labelV1Channels.Text = "Channels:";
            // 
            // textBoxV1Channels
            // 
            textBoxV1Channels.Location = new Point(78, 3);
            textBoxV1Channels.Margin = new Padding(3, 4, 3, 4);
            textBoxV1Channels.Name = "textBoxV1Channels";
            textBoxV1Channels.Size = new Size(55, 27);
            textBoxV1Channels.TabIndex = 1;
            // 
            // labelV1DataSize
            // 
            labelV1DataSize.AutoSize = true;
            labelV1DataSize.Location = new Point(145, 8);
            labelV1DataSize.Name = "labelV1DataSize";
            labelV1DataSize.Size = new Size(96, 20);
            labelV1DataSize.TabIndex = 2;
            labelV1DataSize.Text = "Data size (B):";
            // 
            // textBoxV1DataSize
            // 
            textBoxV1DataSize.Location = new Point(253, 3);
            textBoxV1DataSize.Margin = new Padding(3, 4, 3, 4);
            textBoxV1DataSize.Name = "textBoxV1DataSize";
            textBoxV1DataSize.Size = new Size(55, 27);
            textBoxV1DataSize.TabIndex = 3;
            // 
            // DeviceEdit
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(725, 599);
            Controls.Add(panelV1);
            Controls.Add(textBoxSamplingTime);
            Controls.Add(groupBox2);
            Controls.Add(labelSamplingTime);
            Controls.Add(groupBox1);
            Controls.Add(buttonCancel);
            Controls.Add(buttonOK);
            Controls.Add(comboBoxProtocol);
            Controls.Add(label9);
            Controls.Add(panelRTT);
            Controls.Add(panelTCP);
            Controls.Add(panelSerial);
            Controls.Add(TextBoxName);
            Controls.Add(label1);
            Margin = new Padding(3, 4, 3, 4);
            Name = "DeviceEdit";
            Text = "Device Edit";
            KeyDown += DeviceEdit_KeyDown;
            panelRTT.ResumeLayout(false);
            panelRTT.PerformLayout();
            panelSerial.ResumeLayout(false);
            panelSerial.PerformLayout();
            panelTCP.ResumeLayout(false);
            panelTCP.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            panelV1.ResumeLayout(false);
            panelV1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox TextBoxName;
        private RadioButton radioButtonSerial;
        private RadioButton radioButtonTCP;
        private RadioButton radioButtonRTT;
        private Panel panelRTT;
        private Label labelRttTarget;
        private ComboBox comboBoxRttTarget;
        private Label labelRttProbe;
        private ComboBox comboBoxRttProbe;
        private Button buttonRefreshProbes;
        private Label labelRttHost;
        private TextBox textBoxRttHost;
        private Label labelRttPort;
        private TextBox textBoxRttPort;
        private Panel panelSerial;
        private TextBox textBoxBaudrate;
        private Label label4;
        private Label label3;
        private ComboBox comboBoxPort;
        private Panel panelTCP;
        private TextBox textBoxTcpPort;
        private Label label7;
        private TextBox textBoxAddress;
        private Label label6;
        private Label label8;
        private TextBox textBoxSymbol;
        private OpenFileDialog openFileDialog1;
        private Button buttonBrowse;
        private Label label9;
        private ComboBox comboBoxProtocol;
        private Button buttonOK;
        private Button buttonCancel;
        private GroupBox groupBox1;
        private Button buttonGdbPath;
        private TextBox textBoxGdbPath;
        private Label labelGDBPath;
        private GroupBox groupBox2;
        private TextBox textBoxMapRegex;
        private Label labelMapRegex;
        private RadioButton radioMap;
        private RadioButton radioGDB;
        private TextBox textBoxSamplingTime;
        private Label labelSamplingTime;
        private Panel panelV1;
        private Label labelV1Channels;
        private TextBox textBoxV1Channels;
        private Label labelV1DataSize;
        private TextBox textBoxV1DataSize;
    }
}