using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap.WinPcap;
using System.Net.NetworkInformation;

namespace MapleStoryPacketAnalyzer
{
    public partial class DeviceListForm : Form
    {
        public DeviceListForm()
        {
            InitializeComponent();
        }
        public delegate void SetCaptureAdapter(int deviceIndex,string filterRules);
        public event SetCaptureAdapter setAdapter;

        private void DeviceListForm_Load(object sender, EventArgs e)
        {

            WinPcapDeviceList deviceList = WinPcapDeviceList.Instance;

            
            foreach (WinPcapDevice device in deviceList)
            {
                comboBox1.Items.Add(device.Interface.FriendlyName);
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex>=0)
            {
                setAdapter(comboBox1.SelectedIndex, textBox1.Text);
                Close();
            }
            else {
                MessageBox.Show("请选择要拦截封包的网卡!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }


    }
}
