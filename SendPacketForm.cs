//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System;
using PacketDotNet;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Net;
using SharpPcap.LibPcap;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace MapleShark
{
    public partial class SendPacketForm : Form
    {
        private LibPcapLiveDevice mDevice = null;
        public string sourceIp = null;
        public string destIp = null;
        public int sourcePort = 0;
        public int destPort = 0;

        public string sourceMAC = null;
        public string destinationMac = null;

        public ushort lastIdentificationNum = 0;
        public uint lastSequenceNumber = 0;
        public uint lastAcknowledgmentNumber = 0;
        public uint lastPacketSize = 0;

        public bool isFirstSend = false;

        public SendPacketForm()
        {
            InitializeComponent();
        }

        public void SetDevice(LibPcapLiveDevice device, string filter)
        {
            mDevice = device;
            mDevice.Filter = filter;
        }

        static byte[] ConvertAsciiStringToBytes(string asciiString)
        {
            string[] asciiBytes = asciiString.Split(' ');
            byte[] byteArray = new byte[asciiBytes.Length];

            for (int i = 0; i < asciiBytes.Length; i++)
            {
                byteArray[i] = byte.Parse(asciiBytes[i], System.Globalization.NumberStyles.HexNumber);
            }

            return byteArray;
        }

        void sendPshAck() //packet contains TCP data -> Wrap Ethernet into TCP packet
        {
            if (mDevice == null)
            {
                MessageBox.Show("[ERROR] Device was NULL!");
                return;
            }

            mDevice.Open();

            Console.WriteLine("Sending to: {0}:{1} from {2}:{3} Device: {4}, LastId: {5}", destIp, destPort, sourceIp, sourcePort, mDevice.Addresses[0], lastIdentificationNum);

            byte[] data_payload = null;

            string textBytesPayload = textBox_Send.Text;
            if(textBytesPayload.Length > 2) //convert ascii byte string to actual hex
            {
                data_payload = ConvertAsciiStringToBytes(textBytesPayload);
            }
            else
            {
                MessageBox.Show("Error parsing input packet, check spaces");
                return;
            }

            //Cipher.XorBytes(Cipher.xor_out, data_payload, data_payload.Length, isFirstSend); //wont work, need to use ebcryption table from game itself!

            isFirstSend = false;

            //Create a new Ethernet packet wrapped in the TCP packet
            var ethPacket = new EthernetPacket(
                PhysicalAddress.Parse("00-00-00-00-00-00"),    // Source MAC address, replace with your own or write a function to grab this
                PhysicalAddress.Parse("00-00-00-00-00-01"),   // Destination MAC address, typically your gateway device in home setups                    
                EthernetPacketType.IpV4                   // Ethernet type (IPv4)
            );

            // Create a new IPv4 packet
            var ipPacket = new IPv4Packet(
                IPAddress.Parse(sourceIp),   // Source IP address
                IPAddress.Parse(destIp)    // Destination IP address (same as source for loopback)
            );

            ipPacket.Version = (IpVersion)4;
            ipPacket.TimeToLive = 128;
            // Set the "Don't Fragment" (DF) bit
            ipPacket.FragmentFlags = (ushort)(1 << 1); // Shift 1 bit to the left to set the DF bit
            ipPacket.FragmentOffset = 0;
            ipPacket.Id = (ushort)(lastIdentificationNum + 1);


            // Create a new TCP packet
            var tcpPacket = new TcpPacket((ushort)sourcePort, (ushort)destPort)
            {
                Psh = true,
                Ack = true,
                WindowSize = 515,      // TCP window size, todo: write function to auto-set this
                Checksum = 0x69e0,	//todo: function to auto-set checksum
                SequenceNumber = lastSequenceNumber,
                AcknowledgmentNumber = lastAcknowledgmentNumber,
            };

            //tcpPacket.SequenceNumber = 

            // Set the payload for the TCP packet
            tcpPacket.PayloadData = data_payload;

            // Add the TCP packet to the IPv4 packet
            ipPacket.PayloadPacket = tcpPacket;

            ethPacket.PayloadPacket = ipPacket;

            lock (mDevice)
            {
                mDevice.SendPacket(ethPacket);
            }
        }

        private void button_SendPacket_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(sendPshAck));
            t.Start();
        }

        private void SendPacketForm_Load(object sender, EventArgs e)
        {

        }
    }
}
