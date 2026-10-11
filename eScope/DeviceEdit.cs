using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using eScope.Com;
using eScope.Symbol;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace eScope
{
    public partial class DeviceEdit : Form
    {
        Device Dev;
        private string _pendingProbeUid = "";

        public DeviceEdit(Device device)
        {
            Dev = device;
            InitializeComponent();
            KeyPreview = true;
            TextBoxName.Text = device.Name;

            string ComType = "Serial";
            string[] ComArgs = ",115200".Split(',');
            string rttTarget = "";
            string rttProbeUid = "";
            string rttHost = "127.0.0.1";
            string rttPort = "19021";

            textBoxAddress.Text = rttHost; /* default value */
            textBoxTcpPort.Text = rttPort;

            try
            {
                // RTT:target:probeUID:host:port
                string[] comCfg = device.Com.Split(':');
                ComType = comCfg[0];
                ComArgs = comCfg[1].Split(',');
                if (ComType == "RTT")
                {
                    rttTarget = comCfg.Length > 1 ? comCfg[1] : "";
                    rttProbeUid = comCfg.Length > 2 ? comCfg[2] : "";
                    rttHost = comCfg.Length > 3 ? comCfg[3] : "127.0.0.1";
                    rttPort = comCfg.Length > 4 ? comCfg[4] : "19021";
                }
                else if (ComType == "TCP")
                {
                    rttHost = comCfg.Length > 0 ? ComArgs[0] : "127.0.0.1";
                    rttPort = comCfg.Length > 1 ? ComArgs[1] : "19021";
                }
            }
            catch
            {
                ComType = "Serial";
            }

            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
            {
                comboBoxPort.Items.AddRange(ports);
                int selectedIdx = comboBoxPort.FindStringExact(ComArgs[0]);
                comboBoxPort.SelectedIndex = selectedIdx == -1 ? 0 : selectedIdx;
            }

            if (ComType == "Serial")
            {
                radioButtonSerial.Checked = true;
                textBoxBaudrate.Text = ComArgs[1];

            }
            else if (ComType == "TCP")
            {
                radioButtonTCP.Checked = true;
                textBoxAddress.Text = rttHost;
                textBoxTcpPort.Text = rttPort;
            }
            else if (ComType == "RTT")
            {
                radioButtonRTT.Checked = true;
                comboBoxRttTarget.Text = rttTarget;
                textBoxRttHost.Text = rttHost;
                textBoxRttPort.Text = rttPort;
                _pendingProbeUid = rttProbeUid;
            }

            UpdateComPanels();
            this.Load += DeviceEdit_Load;
            textBoxSymbol.Text = device.SymbolFilePath;
            comboBoxProtocol.Text = device.Protocol;
            textBoxSamplingTime.Text = device.SamplingTime.ToString();
            // Configured path if it exists here, otherwise the default one (bundled gdb, then PATH)
            textBoxGdbPath.Text = GdbLocator.Resolve(device.GdbFilePath) ?? device.GdbFilePath;
            textBoxGdbPath.PlaceholderText = "no gdb found (bundled or PATH)";
            if (device.SymbolType == "Map")
            {
                radioMap.Checked = true;
                radioGDB.Checked = false;
            }
            else
            {
                radioMap.Checked = false;
                radioGDB.Checked = true;
            }
            // empty: automatic detection of the map format
            textBoxMapRegex.Text = device.MapRegex == Symbol.SymbolMap.LegacyDefaultRegex ? string.Empty : device.MapRegex;
            textBoxV1Channels.Text = device.V1ChannelMax.ToString();
            textBoxV1DataSize.Text = device.V1DataSize.ToString();
            panelV1.Visible = device.Protocol == "V1";
            radioSymbolUpdate();
            IsValid();
        }
        private void comboBoxProtocol_SelectedIndexChanged(object sender, EventArgs e)
        {
            panelV1.Visible = comboBoxProtocol.Text == "V1";
        }

        private void radioSymbolUpdate()
        {
            if (radioMap.Checked)
            {
                textBoxGdbPath.Visible = false;
                buttonGdbPath.Visible = false;
                labelGDBPath.Visible = false;
                textBoxMapRegex.Visible = true;
                labelMapRegex.Visible = true;
            }
            else
            {
                textBoxMapRegex.Visible = false;
                labelMapRegex.Visible = false;
                textBoxGdbPath.Visible = true;
                buttonGdbPath.Visible = true;
                labelGDBPath.Visible = true;
            }
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select a symbol file (map/elf)";
            openFileDialog.Filter = "All files (*.*)|*.*";

            DialogResult result = openFileDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                Console.WriteLine("Selected File: " + filePath);
                textBoxSymbol.Text = filePath;
            }
            IsValid();
        }
        private bool IsValid()
        {
            string comType;
            bool valid = true;

            string comText = string.Empty;

            if (radioButtonSerial.Checked)
            {
                comType = radioButtonSerial.Text;
                if (int.TryParse(textBoxBaudrate.Text, out int baudrate) == false)
                {
                    valid = false;
                    textBoxBaudrate.BackColor = Color.Salmon;
                }
                else
                {
                    textBoxBaudrate.BackColor = Color.White;
                }
                comText = comType + ":" + comboBoxPort.Text + "," + baudrate.ToString() + ",8,N,1,1";
            }
            else if (radioButtonTCP.Checked)
            {
                if (string.IsNullOrWhiteSpace(textBoxAddress.Text))
                {
                    valid = false;
                    textBoxAddress.BackColor = Color.Salmon;
                }
                else
                {
                    textBoxAddress.BackColor = Color.White;
                }

                if (!int.TryParse(textBoxTcpPort.Text, out _))
                {
                    valid = false;
                    textBoxTcpPort.BackColor = Color.Salmon;
                }
                else
                {
                    textBoxTcpPort.BackColor = Color.White;
                }
            }
            else if (radioButtonRTT.Checked)
            {
                string target = comboBoxRttTarget.Text.Trim();
                if (string.IsNullOrEmpty(target))
                {
                    valid = false;
                    comboBoxRttTarget.BackColor = Color.Salmon;
                }
                else
                {
                    comboBoxRttTarget.BackColor = Color.White;
                }

                if (!int.TryParse(textBoxRttPort.Text, out _))
                {
                    valid = false;
                    textBoxRttPort.BackColor = Color.Salmon;
                }
                else
                {
                    textBoxRttPort.BackColor = Color.White;
                }

            }
            else
            {
                valid = false;
            }

            if (File.Exists(textBoxSymbol.Text) == false)
            {
                valid = false;
                textBoxSymbol.BackColor = Color.Salmon;
            }
            else
            {
                textBoxSymbol.BackColor = Color.White;
            }

            // Empty path = auto (gdb bundled in tools\gdb, then PATH)
            if (radioGDB.Checked && GdbLocator.Resolve(textBoxGdbPath.Text) == null)
            {
                valid = false;
                textBoxGdbPath.BackColor = Color.Salmon;
            }
            else
            {
                textBoxGdbPath.BackColor = Color.White;
            }

            if (radioMap.Checked == true)
            {
                Dev.SymbolType = "map";

                if (IsValidRegex(textBoxMapRegex.Text) == false)
                {
                    valid = false;
                    textBoxMapRegex.BackColor = Color.Salmon;
                }
                else
                {
                    textBoxMapRegex.BackColor = Color.White;
                }
            }
            else
            {
                Dev.SymbolType = "gdb";
            }
            string test = textBoxSamplingTime.Text.Replace(",", ".");
            double dNumber;
            bool success = double.TryParse(test, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dNumber);
            if (success)
            {
                textBoxSamplingTime.BackColor = Color.White;
            }
            else
            {
                textBoxSamplingTime.BackColor = Color.Salmon;
                valid = false;
            }

            if (comboBoxProtocol.Text == "V1")
            {
                if (!int.TryParse(textBoxV1Channels.Text, out int ch) || ch <= 0)
                {
                    textBoxV1Channels.BackColor = Color.Salmon;
                    valid = false;
                }
                else
                    textBoxV1Channels.BackColor = Color.White;

                if (!int.TryParse(textBoxV1DataSize.Text, out int ds) || ds <= 0)
                {
                    textBoxV1DataSize.BackColor = Color.Salmon;
                    valid = false;
                }
                else
                    textBoxV1DataSize.BackColor = Color.White;
            }

            return valid;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            string comType;
            string comText = string.Empty;

            if (radioButtonSerial.Checked)
            {
                string baudrate = int.Parse(textBoxBaudrate.Text).ToString();
                comType = radioButtonSerial.Text;
                comText = $"{comType}:{comboBoxPort.Text},{baudrate},8,N,1,1";
            }
            else if (radioButtonTCP.Checked)
            {
                string address = textBoxAddress.Text.Trim();
                string port = textBoxTcpPort.Text.Trim();
                comText = $"TCP:{address},{port}";
            }
            else if (radioButtonRTT.Checked)
            {
                string target = comboBoxRttTarget.Text.Trim();
                string probeUid = ExtractProbeUid(comboBoxRttProbe.Text);
                string host = textBoxRttHost.Text.Trim();
                string port = textBoxRttPort.Text.Trim();
                string cbAddr = LookupSeggerRttAddress();
                comText = $"RTT:{target}:{probeUid}:{host}:{port}:{cbAddr}";
            }
            if (IsValid() == true)
            {
                Dev.Name = TextBoxName.Text;
                Dev.Com = comText;
                Dev.Protocol = comboBoxProtocol.Text;
                Dev.SymbolFilePath = textBoxSymbol.Text;
                Dev.GdbFilePath = textBoxGdbPath.Text;
                Dev.MapRegex = textBoxMapRegex.Text;
                Dev.SamplingTime = double.Parse(textBoxSamplingTime.Text.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                if (Dev.Protocol == "V1")
                {
                    Dev.V1ChannelMax = int.Parse(textBoxV1Channels.Text);
                    Dev.V1DataSize = int.Parse(textBoxV1DataSize.Text);
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
        static bool IsValidRegex(string pattern)
        {
            try
            {
                Regex regex = new Regex(pattern);
                return true; // No exception means regex is valid
            }
            catch (ArgumentException)
            {
                return false; // Invalid regex
            }
        }
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void DeviceEdit_KeyDown(object sender, KeyEventArgs e)
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

        private void buttonGdbPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select a GDB executable";
            openFileDialog.Filter = "Exe files (*.exe)|*.exe";

            DialogResult result = openFileDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                Console.WriteLine("Selected File: " + filePath);
                textBoxGdbPath.Text = filePath;
            }
            IsValid();
        }

        private void radioGDB_CheckedChanged(object sender, EventArgs e)
        {
            radioSymbolUpdate();
        }

        private void radioMap_CheckedChanged(object sender, EventArgs e)
        {
            radioSymbolUpdate();
        }

        private void radioButtonRTT_CheckedChanged(object sender, EventArgs e)
        {
            UpdateComPanels();
        }

        private void UpdateComPanels()
        {
            panelSerial.Visible = radioButtonSerial.Checked;
            panelTCP.Visible = radioButtonTCP.Checked;
            panelRTT.Visible = radioButtonRTT.Checked;
        }

        private async void DeviceEdit_Load(object sender, EventArgs e)
        {
            await PopulateRttProbesAsync(_pendingProbeUid);
            await PopulateRttTargetsAsync();
        }

        private async void buttonRefreshProbes_Click(object sender, EventArgs e)
        {
            await PopulateRttProbesAsync();
        }

        private async Task PopulateRttTargetsAsync()
        {
            try
            {
                var psi = new ProcessStartInfo(PythonLauncher.Executable, "-m pyocd list --targets")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi)!;
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                string currentText = comboBoxRttTarget.Text;
                comboBoxRttTarget.Items.Clear();
                foreach (string name in ParsePyocdTargets(output))
                    comboBoxRttTarget.Items.Add(name);
                comboBoxRttTarget.Text = currentText; // restore typed/loaded value
            }
            catch { /* pyocd not installed or unavailable — user types manually */ }
        }

        private async Task PopulateRttProbesAsync(string selectUid = "")
        {
            try
            {
                var psi = new ProcessStartInfo(PythonLauncher.Executable, "-m pyocd list --probes")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi)!;
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                comboBoxRttProbe.Items.Clear();
                foreach (string entry in ParsePyocdProbes(output))
                    comboBoxRttProbe.Items.Add(entry);

                if (!string.IsNullOrEmpty(selectUid))
                {
                    for (int i = 0; i < comboBoxRttProbe.Items.Count; i++)
                    {
                        if (comboBoxRttProbe.Items[i].ToString()!.Contains(selectUid))
                        {
                            comboBoxRttProbe.SelectedIndex = i;
                            return;
                        }
                    }
                }
                if (comboBoxRttProbe.Items.Count > 0)
                    comboBoxRttProbe.SelectedIndex = 0;
            }
            catch { }
        }

        private static List<string> ParsePyocdTargets(string output)
        {
            var targets = new List<string>();
            bool pastHeader = false;
            foreach (string line in output.Split('\n'))
            {
                string t = line.TrimStart();
                if (!pastHeader)
                {
                    if (t.StartsWith("─") || t.StartsWith("-") || t.StartsWith("="))
                        pastHeader = true;
                    continue;
                }
                string[] parts = t.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                    targets.Add(parts[0]);
            }
            return targets;
        }

        private static List<string> ParsePyocdProbes(string output)
        {
            var probes = new List<string>();
            bool pastHeader = false;
            foreach (string line in output.Split('\n'))
            {
                string t = line.Trim();
                if (!pastHeader)
                {
                    if (t.StartsWith("─") || t.StartsWith("-") || t.StartsWith("="))
                        pastHeader = true;
                    continue;
                }
                // Format: "  0   Description text...   UniqueID   target"
                // Split on 2+ spaces to keep multi-word descriptions intact
                string[] parts = Regex.Split(t, @"\s{2,}");
                if (parts.Length >= 3)
                {
                    string desc = parts[1].Trim();
                    string uid = parts[2].Trim();
                    if (!string.IsNullOrEmpty(uid))
                        probes.Add($"{desc}  [{uid}]");
                }
            }
            return probes;
        }

        private static string ExtractProbeUid(string probeText)
        {
            int start = probeText.IndexOf('[');
            int end = probeText.LastIndexOf(']');
            if (start >= 0 && end > start)
                return probeText.Substring(start + 1, end - start - 1);
            return probeText.Trim(); // raw UID or empty string
        }

        // Resolves _SEGGER_RTT's address from the symbol file so the bridge can skip
        // its SRAM scan (see bridge_TcpRtt.py --cb-addr). Works for both the Map
        // backend (fast, synchronous regex lookup) and the GDB/elf backend (spawns
        // a throwaway gdb process for this one query — disposed right after, since
        // SymbolGDB otherwise leaves it running forever).
        private string LookupSeggerRttAddress()
        {
            if (!File.Exists(textBoxSymbol.Text)) return string.Empty;

            string symbolType;
            if (radioMap.Checked)
            {
                if (!IsValidRegex(textBoxMapRegex.Text)) return string.Empty;
                symbolType = "map";
            }
            else
            {
                if (GdbLocator.Resolve(textBoxGdbPath.Text) == null) return string.Empty;
                symbolType = "gdb";
            }

            ISymbol? sym = null;
            try
            {
                sym = ISymbol.New(symbolType, TextBoxName.Text, textBoxSymbol.Text,
                                   textBoxGdbPath.Text, textBoxMapRegex.Text);
                SymbolData? data = sym?.Get("_SEGGER_RTT");
                if (data != null && !string.IsNullOrWhiteSpace(data.Address))
                    return data.Address;
            }
            catch (Exception ex)
            {
                LogMsg.Info($"DeviceEdit: _SEGGER_RTT lookup failed: {ex.Message}");
            }
            finally
            {
                (sym as IDisposable)?.Dispose();
            }
            return string.Empty;
        }

        private void radioButtonTCP_CheckedChanged(object sender, EventArgs e)
        {
            UpdateComPanels();
        }

        private void radioButtonSerial_CheckedChanged(object sender, EventArgs e)
        {
            UpdateComPanels();
        }
    }
}
