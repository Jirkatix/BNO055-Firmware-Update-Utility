using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BNO055;
using System.IO;

namespace kekaaa
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            BSTSensors.CSAMN166 thing = new BSTSensors.CSAMN166();
            FirmwareUpgrade fu = new FirmwareUpgrade(thing, new BNO055.BNO055());

            fu.UpdateFirmware(@"C:\Users\User\Downloads\BNO_Firmware_0.3.2.0.hex");
            
            var field1 = fu.GetType().GetField("_hexData", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            byte[] hexdata = (byte[])field1.GetValue(fu);
            
            var field2 = fu.GetType().GetField("_lengthOfHexData", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            uint _lengthOfHexData = (uint)field2.GetValue(fu);

            File.WriteAllBytes(@"C:\Users\User\Downloads\BNO_Firmware_0.3.2.0_decrypted.bin", hexdata);
            button1.Text = _lengthOfHexData.ToString();
        }
    }
}
