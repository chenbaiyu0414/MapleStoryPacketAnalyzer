using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap;
using SharpPcap.WinPcap;
using PacketDotNet;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace MapleStoryPacketAnalyzer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        int deviceIndex = -1;
        string filterRules = "";

        private object QueueLock = new object();
        private List<RawCapture> PacketQueue = new List<RawCapture>();
        private bool BackgroundThreadStop = false;

        private WinPcapDevice CaptureDevice;
        Thread backgroundThread;

        public delegate void AddItems(string PacketInfo, byte[] PayLoadData);
        public AddItems addItems;
        public delegate void UpdateVersion(byte version, byte[] sendIv, byte[] recvIv);
        public UpdateVersion updateVersion;

        public byte[] sendIv = new byte[4];
        public byte[] recvIv = new byte[4];
        public ushort mapleVersion;
        MapleAES clientSend;//server send
        MapleAES clientRecv;//server recv

        public void updateVersionMethod(byte version, byte[] sendIv,byte[] recvIv)
        {
            toolLb_GameVersion.Text = "游戏版本:V" + version.ToString();
            this.mapleVersion = version;
            this.sendIv = sendIv;
            this.recvIv = recvIv;
        }

        bool needUpdateIv = false;

        public void addItemsMethod(string packetInfo, byte[] PayLoadData)
        {
            bool isToClient = false;
            string[] sData = packetInfo.Split(new char[] { ',' });
            List<byte[]> data = new List<byte[]>();
            byte[] useIv = new byte[4];
            ListViewItem lvi = new ListViewItem((listView1.Items.Count + 1).ToString());

            foreach (string s in sData)
            {
                if (s.Contains("ToClient"))
                {
                    isToClient = true;
                    lvi.BackColor = Color.FromArgb(229, 235, 224);
                }
                    lvi.SubItems.Add(s);

            }
            if (PayLoadData[0] != 0x0F)
            {
                byte[] decryptData = new byte[PayLoadData.Length - 4];

                Buffer.BlockCopy(PayLoadData, 4, decryptData, 0, PayLoadData.Length - 4);

                byte[] tempData;
                if (isToClient)
                {

                    useIv = clientRecv.getIv();
                    tempData = clientRecv.crypt(decryptData);
                    Console.WriteLine(BitTools.GetHexString(tempData));
                    if (tempData[0] == 0x6D)//如果是验证账号密码的封包 接受的时候需要再换一次iv才能正确解包
                    {
                        clientRecv.updateIv();
                        needUpdateIv = true;
                    }
                        
                }
                else
                {
                    useIv = clientSend.getIv();
                    tempData = clientSend.crypt(decryptData);
                }

                data.Add(PayLoadData);
                data.Add(tempData);
            }
            else
            {
                data.Add(PayLoadData);
            }

            lvi.SubItems[5].Text = BitTools.GetHexString(useIv);

            lvi.Tag = data;
            listView1.Items.Add(lvi);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            addItems = new AddItems(addItemsMethod);
            updateVersion = new UpdateVersion(updateVersionMethod);
        }

        private void toolBtn_SetAdapter_Click(object sender, EventArgs e)
        {
            DeviceListForm deviceForm = new DeviceListForm();
            deviceForm.setAdapter += deviceForm_setAdapter;
            deviceForm.ShowDialog();
        }

        void deviceForm_setAdapter(int deviceIndex, string filterRules)
        {
            this.deviceIndex = deviceIndex;
            this.filterRules = filterRules;
        }

        private void toolBtn_StartCapture_Click(object sender, EventArgs e)
        {
            if (deviceIndex == -1)
            {
                MessageBox.Show("请选择要拦截封包的网卡!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            WinPcapDeviceList device = WinPcapDeviceList.Instance;
            CaptureDevice = device[deviceIndex];

            backgroundThread = new Thread(BackgroundThread);
            BackgroundThreadStop = true;
            backgroundThread.IsBackground = true;
            backgroundThread.Name = "CapturePackets";
            backgroundThread.Start();

            CaptureDevice.Open();
            if (this.filterRules != "")
            {
                CaptureDevice.Filter = this.filterRules;
            }
            else
            {
                CaptureDevice.Filter = "port 8484";
            }
            CaptureDevice.OnPacketArrival += captureDevice_OnPacketArrival;
            CaptureDevice.StartCapture();

            toolBtn_StartCapture.Enabled = false;
            toolBtn_StopCapture.Enabled = true;
            toolBtn_SetAdapter.Enabled = false;

        }

        void captureDevice_OnPacketArrival(object sender, SharpPcap.CaptureEventArgs e)
        {
            lock (QueueLock)
            {
                PacketQueue.Add(e.Packet);
            }

        }
        private void BackgroundThread()
        {
            while (BackgroundThreadStop)
            {
                bool shouldSleep = true;

                lock (QueueLock)
                {
                    if (PacketQueue.Count != 0)
                    {
                        shouldSleep = false;
                    }
                }

                if (shouldSleep)
                {
                    Thread.Sleep(250);
                }
                else // should process the queue
                {
                    List<RawCapture> ourQueue;
                    lock (QueueLock)
                    {
                        // swap queues, giving the capture callback a new one
                        ourQueue = PacketQueue;
                        PacketQueue = new List<RawCapture>();
                    }

                    foreach (var packet in ourQueue)
                    {
                        string packetSource = null;
                        int len = packet.Data.Length;
                        EthernetPacket epacket = (EthernetPacket)Packet.ParsePacket(LinkLayers.Ethernet, packet.Data);

                        IPv4Packet ipacket = (IPv4Packet)epacket.Extract(typeof(IPv4Packet));

                        byte[] payLoadData = ipacket.PayloadPacket.PayloadData;

                        if (payLoadData.Length != 0)
                        {
                            packetSource = epacket.SourceHwAddress.ToString() == CaptureDevice.MacAddress.ToString() ? "ToServer" : "ToClient";


                            string time = packet.Timeval.Date.ToLocalTime().ToString("HH:mm:ss.fff");

                            string itemData = string.Format("{0},{1},{2},{3},{4}",
                                time,
                                packetSource,
                                payLoadData.Length,
                                "Header",
                                "IV"
                            );
                            this.BeginInvoke(addItems, itemData, payLoadData);
                            if (payLoadData.Length == 17 && payLoadData[0] == 0x0F)
                            {
                                byte[] SIV = new byte[4];
                                Array.Copy(payLoadData, 7, SIV, 0, 4);
                                byte[] RIV= new byte[4];
                                Array.Copy(payLoadData, 11, RIV, 0, 4);                      
                                clientRecv = new MapleAES(RIV, payLoadData[2]);
                                clientSend = new MapleAES(SIV, (ushort)(0xFFFF - payLoadData[2]));
                                this.BeginInvoke(updateVersion, payLoadData[2], SIV, RIV);
                            }
                        }
                    }
                    ourQueue.Clear();
                    // Here is where we can process our packets freely without
                    // holding off packet capture.
                    //
                    // NOTE: If the incoming packet rate is greater than
                    //       the packet processing rate these queues will grow
                    //       to enormous sizes. Packets should be dropped in these
                    //       cases

                }
            }
        }

        private void toolBtn_StopCapture_Click(object sender, EventArgs e)
        {
            if (BackgroundThreadStop == true)
            {
                CaptureDevice.StopCapture();
                BackgroundThreadStop = false;
                backgroundThread.Join();
                CaptureDevice.Close();

                toolBtn_StartCapture.Enabled = true;
                toolBtn_StopCapture.Enabled = false;
                toolBtn_SetAdapter.Enabled = true;
            }
            else
            {
                MessageBox.Show("未开始拦截封包无法停止!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (BackgroundThreadStop == true)
            {
                CaptureDevice.StopCapture();
                BackgroundThreadStop = false;
                backgroundThread.Join();
                CaptureDevice.Close();

            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices != null && listView1.SelectedIndices.Count > 0)
            {
                ListView.SelectedIndexCollection c = listView1.SelectedIndices;
                ListViewItem selectItem = listView1.Items[c[0]];
                List<byte[]> data = (List<byte[]>)selectItem.Tag;
                if (data.Count == 2)
                {
                    richTextBox1.Text = string.Format("[原始数据 长度:{0}]\n{1} \n[解密数据 长度:{2}]\n{3} \n[Ascii]\n{4}",
                     data[0].Length, BitTools.GetHexString(data[0]),
                     data[1].Length, BitTools.GetHexString(data[1]),
                     Encoding.ASCII.GetString(data[1]));
                }
                else
                {
                    richTextBox1.Text = string.Format("[原始数据 长度:{0}]\n{1} ", data[0].Length, BitTools.GetHexString(data[0]));
                }
            }
        }


        private void toolBtn_Clear_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            MapleAES m_Cipher = null;

            string strIv = textBox1.Text.Trim();
            byte[] Iv = BitTools.StringToBytes(strIv);

            string strData = textBox2.Text.Trim();
            byte[] Data = BitTools.StringToBytes(strData);

            if (radBtn_SendIV.Checked)
            {
                m_Cipher = new MapleAES(Iv, this.mapleVersion);
            }
            else if (radBtn_RecvIV.Checked)
            {
                m_Cipher = new MapleAES(Iv, (ushort)(0xFFFF - this.mapleVersion));
            }

            if (checkBox1.Checked)
            {
                m_Cipher.updateIv();
            }
            try
            {
                byte[] packetBuffer = new byte[Data.Length - 4];
                Buffer.BlockCopy(Data, 4, packetBuffer, 0, packetBuffer.Length);

                m_Cipher.crypt(packetBuffer);

                textBox3.Text = BitConverter.ToString(packetBuffer).Replace("-", " ");
            }
            catch
            {

            }
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            try
            {
                textBox5.Text = string.Format("Default:\r\n{0}\r\nASCII:\r\n{1}\r\nUTF8:\r\n{2}\r\n",
                    Encoding.Default.GetString(BitTools.StringToBytes(textBox4.Text)),
                    Encoding.ASCII.GetString(BitTools.StringToBytes(textBox4.Text)),
                    Encoding.UTF8.GetString(BitTools.StringToBytes(textBox4.Text)));
            }
            catch
            {
                textBox5.Text = "";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            byte[] b = new byte[]
            {
                 0x76,0x2D,0xD0,0xC2,0xC9,0xCD,0x68,0xD4,0x49,0x6A,0x79,0x25,0x08,0x61,0x40,0x14,
                 0xB1,0x3B,0x6A,0xA5,0x11,0x28,0xC1,0x8C,0xD6,0xA9,0x0B,0x87,0x97,0x8C,0x2F,0xF1
            };

            byte[] c = new byte[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                if (i % 4 == 0)
                    c[i] = b[i];
                else
                    c[i] = 0x00;
            }
            textBox6.Text = BitTools.GetHexStringWithTrim(c);
        }

        private void textBox7_TextChanged(object sender, EventArgs e)
        {
            textBox8.Text = "0x" + textBox7.Text.Replace(" ", ",0x");
        }
    }
}
