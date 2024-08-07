using BNO055_Firmware_Update_Utility.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.Management;

namespace BNO055_Firmware_Update_Utility
{
    public partial class Form1 : Form
    {
        string dir_path = "";

        public Form1()
        {
            InitializeComponent();
            string assembly_loc = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase;
            string loc = assembly_loc.Substring(8, assembly_loc.LastIndexOf('/') - 8);
            string[] files = System.IO.Directory.GetFiles(loc, "*.bin");
            dir_path = loc;
            if (files.Length > 1)
            {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("Found more than one firmware file.", "Error", buttons);
                this.Close();
            }

            if (files.Length == 0)
            {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("No firmware file found.", "Error", buttons);
                this.Close();
            }

            _hexData = File.ReadAllBytes(files[0]);

            if (_hexData.Length != 0x3c000)
            {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("File is crap.", "Error", buttons);
                this.Close();
            }

            string fn = files[0].Substring(files[0].LastIndexOf('\\') + 1);

            this.Text = "BNO055 Firmware Update Utility - Ready to upload file: " + fn;

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["Caption"].ToString());

                var portList = portnames.Select(n => ports.FirstOrDefault(s => s.Contains(n))).ToList();

                foreach (string s in portList)
                {
                    comboBox1.Items.Add(s);
                    if (comboBox1.Items.Count != 0)
                    {
                        comboBox1.SelectedIndex = 0;
                        selected_port = comboBox1.SelectedItem.ToString();
                    }
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync();
            
        }

        //decompiled stuff from BNO055.dll

        private byte[] _hexData;
        uint lengthOfHexData_default = 152992;

        //CRC-16/IBM-3740
        private ushort crc_16bit(byte[] data_p, int length)
        {
            int num1 = 0;
            ushort num2 = ushort.MaxValue;
            while (length-- > 0)
            {
                ushort num3 = (ushort)((uint)(ushort)((uint)data_p[num1++] & (uint)byte.MaxValue) << 8);
                for (int index = 0; index < 8; ++index)
                {
                    if ((((int)num2 ^ (int)num3) & 32768) != 0)
                        num2 = (ushort)((int)num2 << 1 ^ 4129);
                    else
                        num2 <<= 1;
                    num3 <<= 1;
                }
            }
            return (ushort)~num2;
        }

        private void UpdateCRC(byte[] data, int len)
        {
            ushort num = this.crc_16bit(data, len);
            data[len++] = (byte)((uint)~num >> 8);
            data[len] = (byte)~num;
        }

        string selected_port;

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            System.Threading.Thread.Sleep(3000);


            byte[] data1 = new byte[6];
            byte[] data2 = new byte[3] { (byte)30, (byte)0, (byte)0 };
            data1[0] = (byte)(lengthOfHexData_default >> 24 & (uint)byte.MaxValue);
            data1[1] = (byte)(lengthOfHexData_default >> 16 & (uint)byte.MaxValue);
            data1[2] = (byte)(lengthOfHexData_default >> 8 & (uint)byte.MaxValue);
            data1[3] = (byte)(lengthOfHexData_default & (uint)byte.MaxValue);


            double num1 = Math.Ceiling((double)lengthOfHexData_default / (double)data2[0]);


            this.UpdateCRC(data1, 4);

            if (this.SendCommands(data1))
            {
                this.UpdateCRC(data2, 1);
                if (this.SendCommands(data2))
                {
                    uint lengthOfHexData = lengthOfHexData_default;
                    int percentProgress = 1;
                    do
                    {
                        if (lengthOfHexData < (uint)data2[0])
                            data2[0] = (byte)lengthOfHexData;
                        byte[] numArray = new byte[(int)data2[0] + 2];
                        Array.Copy((Array)this._hexData, (long)(lengthOfHexData_default - lengthOfHexData), (Array)numArray, 0L, (long)data2[0]);
                        this.UpdateCRC(numArray, (int)data2[0]);
                        if (!this.SendCommands(numArray))
                        {
                            error = true;
                            e.Cancel = true;
                            return;
                        }

                        lengthOfHexData -= (uint)data2[0];
                        if ((long)((uint)(((int)lengthOfHexData_default - (int)lengthOfHexData) * 100) / lengthOfHexData_default) - (long)percentProgress >= 0L)
                        {
                            ++percentProgress;
                            backgroundWorker1.ReportProgress(percentProgress);
                        }
                    }
                    while (lengthOfHexData > 0U);
                }
                else
                {
                    error = true;
                    e.Cancel = true;
                }
            }
            else
            {
                error = true;
                e.Cancel = true;
            }
            
        }

        private void TransmitToProtocol(byte[] data)
        {
            serialPort1.Write(data, 0, data.Length);

            bool avr_responded = false;
            while (!avr_responded)
            {
                if (serialPort1.BytesToRead == 1)
                {
                    avr_responded = true;
                }
            }

            receivedAcknowledgement = (byte)serialPort1.ReadByte();
        }


        bool error = false;
        byte receivedAcknowledgement = 0;

        private bool SendCommands(byte[] data)
        {
            bool flag = false;

            this.TransmitToProtocol(data);

            if (receivedAcknowledgement != (byte)115)
            {
                error = true;
                backgroundWorker1.CancelAsync();
                MessageBox.Show("CRC failed during transmission, reset arduino and try again", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (receivedAcknowledgement == (byte)115)
                flag = true;
            receivedAcknowledgement = (byte)0;
            return flag;
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage <= 100)
            {
                progressBar1.Value = e.ProgressPercentage;
                label1.Text = e.ProgressPercentage.ToString() + " %";
            } else
            {
                progressBar1.Value = 100;
                label1.Text = "100 %";
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(!error)
            {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("Update finished", "Done", buttons);
            } else
            {
                MessageBox.Show("Update failed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            selected_port = comboBox1.SelectedItem.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string comport = "";

            int index = selected_port.IndexOf("COM");
            if (index != -1)
            {
                comport = selected_port.Substring(index, 4);
            }

            try
            {
                serialPort1.DtrEnable = true;
                serialPort1.PortName = comport;
                serialPort1.Open();
            }
            catch
            {
                if (!serialPort1.IsOpen)
                {
                    MessageBox.Show("Device not ready.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            bool avr_responded = false;
            while (!avr_responded)
            {
                if (serialPort1.BytesToRead == 2)
                {
                    avr_responded = true;
                }
            }

            byte msb = (byte)serialPort1.ReadByte();
            byte lsb = (byte)serialPort1.ReadByte();

            label2.Text = "BNO055 current firmware version: " + Convert.ToString((msb & 0xf0) >> 4, 16) + "." + Convert.ToString((msb & 0x0f), 16) + "." + Convert.ToString((lsb & 0xf0) >> 4, 16) + "." + Convert.ToString((lsb & 0x0f), 16);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string comport = "";

            int index = selected_port.IndexOf("COM");
            if (index != -1)
            {
                comport = selected_port.Substring(index, 4);
            }

            string progArduino = "/C " + dir_path + "/avrdude/avrdude.exe" + " -C" + dir_path + "/avrdude/avrdude.conf" + " -patmega328p -carduino -P" + comport + " -b 115200 -D -U flash:w:" + dir_path + "/bnoprog.hex:i";

            //File.WriteAllText("progArduino.bat", "start " + progArduino.Substring(2));
            
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = progArduino;
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            string output = process.StandardError.ReadToEnd();
            textBox1.Text = output;
        }

        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
            pictureBox1.BringToFront();
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
        }
    }
}
