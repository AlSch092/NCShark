//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using System.Text.RegularExpressions;

namespace NCShark
{
    public partial class SessionForm : DockContent
    {
        public enum Results
        {
            Continue,
            Terminated,
            CloseMe
        }

        private string mFilename = null;
        private bool mTerminated = false;
        private ushort mLocalPort = 0;
        public ushort mRemotePort = 0;
        private ushort mProxyPort = 0;
        private uint mOutboundSequence = 0;
        private uint mInboundSequence = 0;
        private ushort mBuild = 0;
        private byte mLocale = 1; //todo: change this to better
        private string mPatchLocation = "";
        
        private Dictionary<uint, byte[]> mOutboundBuffer = new Dictionary<uint, byte[]>();
        private Dictionary<uint, byte[]> mInboundBuffer = new Dictionary<uint, byte[]>();
        
        private NCStream mOutboundStream = null;
        private NCStream mInboundStream = null;

        private List<NCPacket> mPackets = new List<NCPacket>();
        private List<Pair<bool, ushort>> mOpcodes = new List<Pair<bool, ushort>>();
        private int socks5 = 0;

        public string mRemoteEndpoint = "???";
        public string mLocalEndpoint = "???";
        private string mProxyEndpoint = "???";

        private bool firstSend_in = true;
        private bool firstSend_out = true;

        public bool logging = true;

        private Cipher cipher = new Cipher();


        // Used for determining if the session did receive a packet at all, or if it just emptied its buffers
        public bool ClearedPackets { get; private set; }

        internal SessionForm()
        {
            ClearedPackets = false;
            InitializeComponent();
            Saved = false;
        }

        public MainForm MainForm { get { return ParentForm as MainForm; } }
        public ListView ListView { get { return mPacketList; } }
        public byte Locale { get { return mLocale; } }
        public List<Pair<bool, ushort>> Opcodes { get { return mOpcodes; } }

        public bool Saved { get; private set; }

        private DateTime startTime;

        public void UpdateOpcodeList()
        {
            mOpcodes = mOpcodes.OrderBy(a => a.Second).ToList();
        }

        internal bool MatchTCPPacket(TcpPacket pTCPPacket)
        {
            if (mTerminated) return false;
            if (pTCPPacket.SourcePort == mLocalPort && pTCPPacket.DestinationPort == (mProxyPort > 0 ? mProxyPort : mRemotePort)) return true;
            if (pTCPPacket.SourcePort == (mProxyPort > 0 ? mProxyPort : mRemotePort) && pTCPPacket.DestinationPort == mLocalPort) return true;
            return false;
        }

        internal bool CloseMe(DateTime pTime)
        {
            if (!ClearedPackets && mPackets.Count == 0 && (pTime - startTime).TotalSeconds >= 5)
            {
                return true;
            }
            return false;
        }

        internal void SetGameInfo(ushort version, string patchLocation, byte locale, string ip, ushort port)
        {
            Console.WriteLine("Called SetGameInfo: {0}, {1}", ip, port);
            if (mPackets.Count > 0) return;
            mBuild = version;
            mPatchLocation = patchLocation;
            mLocale = locale;

            mRemoteEndpoint = ip;
            mRemotePort = port;

            mLocalEndpoint = "127.0.0.1";
            mLocalPort = 10000;
        }

        internal Results BufferTCPPacket(TcpPacket pTCPPacket, DateTime pArrivalTime, bool shouldLog)
        {           
            if (pTCPPacket.Fin || pTCPPacket.Rst)
            {
                mTerminated = true;
                Text += " (Terminated)";
                Console.WriteLine("FIN / RST packet: end session");
                return mPackets.Count == 0 ? Results.CloseMe : Results.Terminated;
            }

            if (pTCPPacket.Syn && !pTCPPacket.Ack)
            {
                Console.WriteLine("pTCPPacket.Syn -> First packet in session");
                mLocalPort = (ushort)pTCPPacket.SourcePort;
                mRemotePort = (ushort)pTCPPacket.DestinationPort;
                mOutboundSequence = (uint)(pTCPPacket.SequenceNumber + 1);
                Text = "Port " + mLocalPort + " - " + mRemotePort;
                startTime = DateTime.Now;

                firstSend_out = true;
                firstSend_in = true;

                try
                {
                    mRemoteEndpoint = ((PacketDotNet.IPv4Packet)pTCPPacket.ParentPacket).SourceAddress.ToString() + ":" + pTCPPacket.SourcePort.ToString();
                    mLocalEndpoint = ((PacketDotNet.IPv4Packet)pTCPPacket.ParentPacket).DestinationAddress.ToString() + ":" + pTCPPacket.DestinationPort.ToString();
                    
                    Console.WriteLine("[CONNECTION] From {0} to {1}", mRemoteEndpoint, mLocalEndpoint);
                }
                catch
                {
                    return Results.CloseMe;
                }
            }

            if(pTCPPacket.SourcePort == mLocalPort && !loggingOutbound) //IGNORE OUTBOUND
                return Results.Continue;
            else if(pTCPPacket.DestinationPort == mLocalPort && !loggingInbound)
                return Results.Continue;

            if (pTCPPacket.PayloadData.Length == 0)
                return Results.Continue;

            byte[] tcpData = pTCPPacket.PayloadData;

            if (pTCPPacket.SourcePort == mLocalPort) mOutboundSequence += (uint)tcpData.Length;
            else mInboundSequence += (uint)tcpData.Length;

            byte[] headerData = new byte[tcpData.Length];
            Buffer.BlockCopy(tcpData, 0, headerData, 0, tcpData.Length);

            if (Config.Instance.Protocol == "Night Crows")
            {
                mOutboundStream = new NCStream(true);
                mInboundStream = new NCStream(false);
                
                NCPacket packet = null;
                
                if (pTCPPacket.SourcePort == mLocalPort) //OUTBOUND
                {
                    byte[] tcpDataDecrypted = new byte[tcpData.Length];
                    Buffer.BlockCopy(tcpData, 0, tcpDataDecrypted, 0, tcpData.Length);

                    if(tcpData.Length >= 4)
                    {
                        Cipher.XorBytes(Cipher.xor_out, tcpDataDecrypted, tcpDataDecrypted.Length, firstSend_out);
                        firstSend_out = false;
                    }

                    ushort opcode = BitConverter.ToUInt16(tcpDataDecrypted, 2);
                    Definition definition = Config.Instance.GetDefinition(false, opcode);

                    byte[] tcpDataToLog = new byte[tcpData.Length - 2];
                    Buffer.BlockCopy(tcpDataDecrypted, 2, tcpDataToLog, 0, tcpData.Length - 2);

                    packet = new NCPacket(pArrivalTime, true, opcode, definition == null ? "" : definition.Name, tcpDataToLog);
                    
                }
                else //INBOUND
                {
                    byte[] tcpDataDecrypted = new byte[tcpData.Length];
                    Buffer.BlockCopy(tcpData, 0, tcpDataDecrypted, 0, tcpData.Length);

                    if (tcpData.Length >= 4)
                    {
                        Cipher.XorBytes(Cipher.xor_in, tcpDataDecrypted, tcpDataDecrypted.Length, firstSend_in);
                        firstSend_in = false;
                    }

                    ushort opcode = BitConverter.ToUInt16(tcpDataDecrypted, 2);
                    Definition definition = Config.Instance.GetDefinition(false, opcode);

                    byte[] tcpDataToLog = new byte[tcpData.Length - 2];
                    Buffer.BlockCopy(tcpDataDecrypted, 2, tcpDataToLog, 0, tcpData.Length - 2);

                    //decrypt tcp data
                    packet = new NCPacket(pArrivalTime, false, opcode, definition == null ? "" : definition.Name, tcpDataToLog);
                }

                if (!mOpcodes.Exists(kv => kv.First == packet.Outbound && kv.Second == packet.Opcode)) // Should be false, but w/e
                {
                    mOpcodes.Add(new Pair<bool, ushort>(packet.Outbound, packet.Opcode));
                }

                if (shouldLog)
                {
                    mPacketList.Items.Add(packet);
                    mPackets.Add(packet);

                    MainForm.SearchForm.RefreshOpcodes(true);
                }
            }

            if (pTCPPacket.SourcePort == mLocalPort)
            {
                //Console.WriteLine("Calling ProcessTCPPacket [OUTBOUND]");
                ProcessTCPPacket(pTCPPacket, ref mOutboundSequence, mOutboundBuffer, mOutboundStream, pArrivalTime);
            }
            else
            {
                //Console.WriteLine("Calling ProcessTCPPacket [INBOUND]");
                ProcessTCPPacket(pTCPPacket, ref mInboundSequence, mInboundBuffer, mInboundStream, pArrivalTime);
            } 

            return Results.Continue;
        }

        private void ProcessTCPPacket(TcpPacket pTCPPacket, ref uint pSequence, Dictionary<uint, byte[]> pBuffer, NCStream pStream, DateTime pArrivalDate)
        {
            //Console.WriteLine("Called ProcessTCPPacket");

            if (pTCPPacket.SequenceNumber > pSequence)
            {
                byte[] data;
                while ((data = pBuffer.GetOrDefault(pSequence, null)) != null)
                {
                    pBuffer.Remove(pSequence);
                    pStream.Append(data);
                    pSequence += (uint)data.Length;
                }
                if (pTCPPacket.SequenceNumber > pSequence) pBuffer[(uint)pTCPPacket.SequenceNumber] = pTCPPacket.PayloadData;
            }
            if (pTCPPacket.SequenceNumber < pSequence)
            {
                int difference = (int)(pSequence - pTCPPacket.SequenceNumber);
                if (difference > 0)
                {
                    byte[] data = pTCPPacket.PayloadData;
                    if (data.Length > difference)
                    {
                        pStream.Append(data, difference, data.Length - difference);
                        pSequence += (uint)(data.Length - difference);
                    }
                }
            }
            else if (pTCPPacket.SequenceNumber == pSequence)
            {
                byte[] data = pTCPPacket.PayloadData;
                pStream.Append(data);
                pSequence += (uint)data.Length;
            }

            NCPacket packet;
            bool refreshOpcodes = false;
            try
            {
                while ((packet = pStream.Read(pArrivalDate)) != null)
                {
                    mPackets.Add(packet);
                    Definition definition = Config.Instance.GetDefinition(packet.Outbound, packet.Opcode);
                    if (!mOpcodes.Exists(kv => kv.First == packet.Outbound && kv.Second == packet.Opcode))
                    {
                        mOpcodes.Add(new Pair<bool, ushort>(packet.Outbound, packet.Opcode));
                        refreshOpcodes = true;
                    }
                    if (definition != null && !mViewIgnoredMenu.Checked && definition.Ignore) continue;
                    if (packet.Outbound && !mViewOutboundMenu.Checked) continue;
                    if (!packet.Outbound && !mViewInboundMenu.Checked) continue;

                    mPacketList.Items.Add(packet);
                    if (mPacketList.SelectedItems.Count == 0) packet.EnsureVisible();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                mTerminated = true;
                Text += " (Terminated)";
                MainForm.CloseSession(this);
                return;
            }

            if (DockPanel.ActiveDocument == this && refreshOpcodes) MainForm.SearchForm.RefreshOpcodes(true);
        }

        public void OpenReadOnly(string pFilename)
        {
            // mFileSaveMenu.Enabled = false;
            Saved = true;

            mTerminated = true;
            using (FileStream stream = new FileStream(pFilename, FileMode.Open, FileAccess.Read))
            {
                BinaryReader reader = new BinaryReader(stream);
                ushort MapleSharkVersion = reader.ReadUInt16();
                mBuild = MapleSharkVersion;
                if (MapleSharkVersion < 0x2000)
                {

                    mLocalPort = reader.ReadUInt16();
                    // Old version
                    frmLocale loc = new frmLocale();
                    var res = loc.ShowDialog();
                    if (res == System.Windows.Forms.DialogResult.OK)
                    {
                        mLocale = loc.ChosenLocale;
                    }
                }
                else
                {
                    byte v1 = (byte)((MapleSharkVersion >> 12) & 0xF),
                           v2 = (byte)((MapleSharkVersion >> 8) & 0xF),
                           v3 = (byte)((MapleSharkVersion >> 4) & 0xF),
                           v4 = (byte)((MapleSharkVersion >> 0) & 0xF);
                    Console.WriteLine("Loading MSB file, saved by NCShark V{0}.{1}.{2}.{3}", v1, v2, v3, v4);

                    if (MapleSharkVersion == 0x2012)
                    {
                        mLocale = (byte)reader.ReadUInt16();
                        mBuild = reader.ReadUInt16();
                        mLocalPort = reader.ReadUInt16();
                    }
                    else if (MapleSharkVersion == 0x2014)
                    {
                        mLocalEndpoint = reader.ReadString();
                        mLocalPort = reader.ReadUInt16();
                        mRemoteEndpoint = reader.ReadString();
                        mRemotePort = reader.ReadUInt16();

                        mLocale = (byte)reader.ReadUInt16();
                        mBuild = reader.ReadUInt16();
                    }
                    else if (MapleSharkVersion == 0x2015 || MapleSharkVersion >= 0x2020)
                    {
                        mLocalEndpoint = reader.ReadString();
                        mLocalPort = reader.ReadUInt16();
                        mRemoteEndpoint = reader.ReadString();
                        mRemotePort = reader.ReadUInt16();

                        mLocale = reader.ReadByte();
                        mBuild = reader.ReadUInt16();

                        if (MapleSharkVersion >= 0x2021)
                        {
                            mPatchLocation = reader.ReadString();
                        }
                    }
                    else
                    {
                        MessageBox.Show("I have no idea how to open this MSB file. It looks to me as a version " + string.Format("{0}.{1}.{2}.{3}", v1, v2, v3, v4) + " NCShark MSB file... O.o?!");
                        return;
                    }
                }

                mPacketList.BeginUpdate();
                while (stream.Position < stream.Length)
                {
                    long timestamp = reader.ReadInt64();
                    ushort size = reader.ReadUInt16();
                    ushort opcode = reader.ReadUInt16();
                    bool outbound;

                    if (MapleSharkVersion >= 0x2020)
                    {
                        outbound = reader.ReadBoolean();
                    }
                    else
                    {
                        outbound = (size & 0x8000) != 0;
                        size = (ushort)(size & 0x7FFF);
                    }

                    byte[] buffer = reader.ReadBytes(size);

                    uint preDecodeIV = 0, postDecodeIV = 0;
                    if (MapleSharkVersion >= 0x2025)
                    {
                        preDecodeIV = reader.ReadUInt32();
                        postDecodeIV = reader.ReadUInt32();
                    }

                    Definition definition = Config.Instance.GetDefinition(outbound, opcode);
                    NCPacket packet = new NCPacket(new DateTime(timestamp), outbound, opcode, definition == null ? "" : definition.Name, buffer);
                    mPackets.Add(packet);
                    if (!mOpcodes.Exists(kv => kv.First == packet.Outbound && kv.Second == packet.Opcode)) mOpcodes.Add(new Pair<bool, ushort>(packet.Outbound, packet.Opcode));
                    if (definition != null && definition.Ignore) continue;
                    mPacketList.Items.Add(packet);
                }
                mPacketList.EndUpdate();
                if (mPacketList.Items.Count > 0) mPacketList.EnsureVisible(0);
            }

            Text = string.Format("{0} (ReadOnly)", Path.GetFileName(pFilename));
            Console.WriteLine("Loaded file: {0}", pFilename);
        }

        public void RefreshPackets()
        {

            Pair<bool, ushort> search = (MainForm.SearchForm.ComboBox.SelectedIndex >= 0 ? mOpcodes[MainForm.SearchForm.ComboBox.SelectedIndex] : null);
            NCPacket previous = mPacketList.SelectedItems.Count > 0 ? mPacketList.SelectedItems[0] as NCPacket : null;
            mOpcodes.Clear();
            mPacketList.Items.Clear();

            MainForm.DataForm.HexBox.ByteProvider = null;
            MainForm.StructureForm.Tree.Nodes.Clear();
            MainForm.PropertyForm.Properties.SelectedObject = null;

            if (!mViewOutboundMenu.Checked && !mViewInboundMenu.Checked) return;
            mPacketList.BeginUpdate();
            for (int index = 0; index < mPackets.Count; ++index)
            {
                NCPacket packet = mPackets[index];
                if (packet.Outbound && !mViewOutboundMenu.Checked) continue;
                if (!packet.Outbound && !mViewInboundMenu.Checked) continue;

                Definition definition = Config.Instance.GetDefinition(packet.Outbound, packet.Opcode);
                packet.Name = definition == null ? "" : definition.Name;
                if (!mOpcodes.Exists(kv => kv.First == packet.Outbound && kv.Second == packet.Opcode)) mOpcodes.Add(new Pair<bool, ushort>(packet.Outbound, packet.Opcode));
                if (definition != null && !mViewIgnoredMenu.Checked && definition.Ignore) continue;
                mPacketList.Items.Add(packet);

                if (packet == previous) packet.Selected = true;
            }
            mPacketList.EndUpdate();
            MainForm.SearchForm.RefreshOpcodes(true);

            if (previous != null) previous.EnsureVisible();
        }

        public void ReselectPacket()
        {
            mPacketList_SelectedIndexChanged(null, null);
        }

        private Regex _packetRegex = new Regex(@"\[(.{1,2}):(.{1,2}):(.{1,2})\]\[(\d+)\] (Recv|Send):  (.+)");
        internal void ParseMSnifferLine(string packetLine)
        {
            var match = _packetRegex.Match(packetLine);
            if (match.Captures.Count == 0) return;
            DateTime date = new DateTime(
                2012,
                10,
                10,
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
            int packetLength = int.Parse(match.Groups[4].Value);
            byte[] buffer = new byte[packetLength - 2];
            bool outbound = match.Groups[5].Value == "Send";
            string[] bytesText = match.Groups[6].Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            ushort opcode = (ushort)(byte.Parse(bytesText[0], System.Globalization.NumberStyles.HexNumber) | byte.Parse(bytesText[1], System.Globalization.NumberStyles.HexNumber) << 8);

            for (var i = 2; i < packetLength; i++)
            {
                buffer[i - 2] = byte.Parse(bytesText[i], System.Globalization.NumberStyles.HexNumber);
            }



            Definition definition = Config.Instance.GetDefinition(outbound, opcode);
            NCPacket packet = new NCPacket(date, outbound, opcode, definition == null ? "" : definition.Name, buffer);
            mPackets.Add(packet);
            if (!mOpcodes.Exists(kv => kv.First == packet.Outbound && kv.Second == packet.Opcode)) mOpcodes.Add(new Pair<bool, ushort>(packet.Outbound, packet.Opcode));
            if (definition != null && definition.Ignore) return;
            mPacketList.Items.Add(packet);
        }

        public void RunSaveCMD()
        {
            mFileSaveMenu.PerformClick();
        }

        private void mFileSaveMenu_Click(object pSender, EventArgs pArgs)
        {
            if (mFilename == null)
            {
                mSaveDialog.FileName = string.Format("Port {0}", mLocalPort);
                if (mSaveDialog.ShowDialog(this) == DialogResult.OK) mFilename = mSaveDialog.FileName;
                else return;
            }
            using (FileStream stream = new FileStream(mFilename, FileMode.Create, FileAccess.Write))
            {
                var version = (ushort)0x2025;

                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(version);
                writer.Write(mLocalEndpoint);
                writer.Write(mLocalPort);
                writer.Write(mRemoteEndpoint);
                writer.Write(mRemotePort);
                writer.Write(mLocale);
                writer.Write(mBuild);
                writer.Write(mPatchLocation);

                mPackets.ForEach(p =>
                {
                    writer.Write((ulong)p.Timestamp.Ticks);
                    writer.Write((ushort)p.Length);
                    writer.Write((ushort)p.Opcode);
                    writer.Write((byte)(p.Outbound ? 1 : 0));
                    writer.Write(p.Buffer);
                    if (version == 0x2025)
                    {
                        writer.Write(p.PreDecodeIV);
                        writer.Write(p.PostDecodeIV);
                    }
                });

                stream.Flush();
            }

            if (mTerminated)
            {
                mFileSaveMenu.Enabled = false;
                Text += " (ReadOnly)";
            }

            Saved = true;
        }

        private void mFileExportMenu_Click(object pSender, EventArgs pArgs)
        {
            mExportDialog.FileName = string.Format("Port {0}", mLocalPort);
            if (mExportDialog.ShowDialog(this) != DialogResult.OK) return;

            bool includeNames = MessageBox.Show("Export opcode names? (slow + generates big files!!!)", "-", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes;

            string tmp = "";
            tmp += string.Format("=== MapleStory Version: {0}; Locale: {1} ===\r\n", mBuild, mLocale);
            tmp += string.Format("Endpoint From: {0}\r\n", mLocalEndpoint);
            tmp += string.Format("Endpoint To: {0}\r\n", mRemoteEndpoint);
            tmp += string.Format("- Packets: {0}\r\n", mPackets.Count);

            long dataSize = 0;
            foreach (var packet in mPackets)
                dataSize += 2 + packet.Buffer.Length;

            tmp += string.Format("- Data: {0:N0} bytes\r\n", dataSize);
            tmp += string.Format("================================================\r\n");
            File.WriteAllText(mExportDialog.FileName, tmp);

            tmp = "";

            int outboundCount = 0;
            int inboundCount = 0;
            int i = 0;
            foreach (var packet in mPackets)
            {
                if (packet.Outbound) ++outboundCount;
                else ++inboundCount;

                Definition definition = Config.Instance.GetDefinition(packet.Outbound, packet.Opcode);

                tmp += string.Format("[{0}][{2}] [{3:X4}{5}] {4}\r\n",
                    packet.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    (packet.Outbound ? outboundCount : inboundCount),
                    (packet.Outbound ? "Outbound" : "Inbound "),
                    packet.Opcode,
                    BitConverter.ToString(packet.Buffer).Replace('-', ' '),
                    includeNames ? " | " + (definition == null ? "N/A" : definition.Name) : "");
                i++;
                if (i % 1000 == 0)
                {
                    File.AppendAllText(mExportDialog.FileName, tmp);
                    tmp = "";
                }
            }
            File.AppendAllText(mExportDialog.FileName, tmp);
        }

        private void mViewCommonScriptMenu_Click(object pSender, EventArgs pArgs)
        {
            string scriptPath = "Scripts" + Path.DirectorySeparatorChar + mLocale.ToString() + Path.DirectorySeparatorChar + mBuild.ToString() + Path.DirectorySeparatorChar + "Common.txt";
            if (!Directory.Exists(Path.GetDirectoryName(scriptPath))) Directory.CreateDirectory(Path.GetDirectoryName(scriptPath));
            ScriptForm script = new ScriptForm(scriptPath, null);
            script.FormClosed += CommonScript_FormClosed;
            script.Show(DockPanel, new Rectangle(MainForm.Location, new Size(600, 300)));
        }

        private void CommonScript_FormClosed(object pSender, FormClosedEventArgs pArgs)
        {
            if (mPacketList.SelectedIndices.Count == 0) return;
            NCPacket packet = mPacketList.SelectedItems[0] as NCPacket;
            MainForm.StructureForm.ParseNCPacket(packet);
            Activate();
        }

        public bool loggingOutbound = true;
        public bool loggingInbound = true;

        private void mViewRefreshMenu_Click(object pSender, EventArgs pArgs) { RefreshPackets(); }
        private void mViewOutboundMenu_CheckedChanged(object pSender, EventArgs pArgs) { loggingOutbound = mViewOutboundMenu.Checked; RefreshPackets(); }
        private void mViewInboundMenu_CheckedChanged(object pSender, EventArgs pArgs) { loggingInbound = mViewInboundMenu.Checked; RefreshPackets(); }
        private void mViewIgnoredMenu_CheckedChanged(object pSender, EventArgs pArgs) { RefreshPackets(); }

        private void mPacketList_SelectedIndexChanged(object pSender, EventArgs pArgs)
        {
            if (mPacketList.SelectedItems.Count == 0)
            {
                MainForm.DataForm.HexBox.ByteProvider = null;
                MainForm.StructureForm.Tree.Nodes.Clear();
                MainForm.PropertyForm.Properties.SelectedObject = null;
                return;
            }
            MainForm.DataForm.HexBox.ByteProvider = new DynamicByteProvider((mPacketList.SelectedItems[0] as NCPacket).Buffer);
            MainForm.StructureForm.ParseNCPacket(mPacketList.SelectedItems[0] as NCPacket);
        }

        private void mPacketList_ItemActivate(object pSender, EventArgs pArgs)
        {
            if (mPacketList.SelectedIndices.Count == 0) return;
            NCPacket packet = mPacketList.SelectedItems[0] as NCPacket;
            string scriptPath = "Scripts" + Path.DirectorySeparatorChar + mLocale.ToString() + Path.DirectorySeparatorChar + mBuild.ToString() + Path.DirectorySeparatorChar + (packet.Outbound ? "Outbound" : "Inbound") + Path.DirectorySeparatorChar + "0x" + packet.Opcode.ToString("X4") + ".txt";

            if (!Directory.Exists(Path.GetDirectoryName(scriptPath))) Directory.CreateDirectory(Path.GetDirectoryName(scriptPath));

            ScriptForm script = new ScriptForm(scriptPath, packet);
            script.FormClosed += Script_FormClosed;
            script.Show(DockPanel, new Rectangle(MainForm.Location, new Size(600, 300)));
        }

        private void Script_FormClosed(object pSender, FormClosedEventArgs pArgs)
        {
            ScriptForm script = pSender as ScriptForm;
            script.Packet.Selected = true;
            MainForm.StructureForm.ParseNCPacket(script.Packet);
            Activate();
        }

        bool openingContextMenu = false;
        private void mPacketContextMenu_Opening(object pSender, CancelEventArgs pArgs)
        {
            openingContextMenu = true;
            mPacketContextNameBox.Text = "";
            mPacketContextIgnoreMenu.Checked = false;
            if (mPacketList.SelectedItems.Count == 0) pArgs.Cancel = true;
            else
            {
                NCPacket packet = mPacketList.SelectedItems[0] as NCPacket;
                Definition definition = Config.Instance.GetDefinition(packet.Outbound, packet.Opcode);
                if (definition != null)
                {
                    mPacketContextNameBox.Text = definition.Name;
                    mPacketContextIgnoreMenu.Checked = definition.Ignore;
                }
            }
        }

        private void mPacketContextMenu_Opened(object pSender, EventArgs pArgs)
        {
            mPacketContextNameBox.Focus();
            mPacketContextNameBox.SelectAll();

            mPacketList.SelectedItems[0].EnsureVisible();
            openingContextMenu = false;
        }

        private void mPacketContextNameBox_KeyDown(object pSender, KeyEventArgs pArgs)
        {
            if (pArgs.Modifiers == Keys.None && pArgs.KeyCode == Keys.Enter)
            {
                NCPacket packet = mPacketList.SelectedItems[0] as NCPacket;
                Definition definition = Config.Instance.GetDefinition(packet.Outbound, packet.Opcode);
                if (definition == null)
                {
                    definition = new Definition();

                    definition.Outbound = packet.Outbound;
                    definition.Opcode = packet.Opcode;
                    definition.Locale = mLocale;
                }
                definition.Name = mPacketContextNameBox.Text;
                DefinitionsContainer.Instance.SaveDefinition(definition);
                pArgs.SuppressKeyPress = true;
                mPacketContextMenu.Close();
                RefreshPackets();

                packet.EnsureVisible();
            }
        }

        private void mPacketContextIgnoreMenu_CheckedChanged(object pSender, EventArgs pArgs)
        {
            if (openingContextMenu) return;
            NCPacket packet = mPacketList.SelectedItems[0] as NCPacket;
            Definition definition = Config.Instance.GetDefinition(packet.Outbound, packet.Opcode);
            if (definition == null)
            {
                definition = new Definition();
                definition.Locale = mLocale;

                definition.Outbound = packet.Outbound;
                definition.Opcode = packet.Opcode;
                definition.Locale = mLocale;
            }
            definition.Ignore = mPacketContextIgnoreMenu.Checked;
            DefinitionsContainer.Instance.SaveDefinition(definition);

            int newIndex = packet.Index - 1;
            for (var i = packet.Index - 1; i > 0; i--)
            {
                var pack = mPacketList.Items[i] as NCPacket;
                var def = Config.Instance.GetDefinition(pack.Outbound, pack.Opcode);
                if (def == definition)
                {
                    newIndex--;
                }
            }

            RefreshPackets();


            if (newIndex != 0 && mPacketList.Items[newIndex] != null)
            {
                packet = mPacketList.Items[newIndex] as NCPacket;
                packet.Selected = true;
                packet.EnsureVisible();
            }
        }

        private void SessionForm_Load(object sender, EventArgs e)
        {

        }

        private void mMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void sessionInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SessionInformation si = new SessionInformation();
            si.txtVersion.Text = mBuild.ToString();
            si.txtPatchLocation.Text = mPatchLocation;
            si.txtLocale.Text = mLocale.ToString();
            si.txtAdditionalInfo.Text = "Connection info:\r\n" + mLocalEndpoint + " <-> " + mRemoteEndpoint + (mProxyEndpoint != "???" ? "\r\nProxy:" + mProxyEndpoint : "");

            if (mLocale == 1 || mLocale == 2)
            {
                si.txtAdditionalInfo.Text += "\r\nRecording session of a MapleStory Korea" + (mLocale == 2 ? " Test" : "") + " server.\r\nAdditional KMS info:\r\n";

                try
                {
                    int test = int.Parse(mPatchLocation);
                    ushort maplerVersion = (ushort)(test & 0x7FFF);
                    int extraOption = (test >> 15) & 1;
                    int subVersion = (test >> 16) & 0xFF;
                    si.txtAdditionalInfo.Text += "Real Version: " + maplerVersion + "\r\nSubversion: " + subVersion + "\r\nExtra flag: " + extraOption;
                }
                catch { }
            }

            si.Show();
        }

        private void sendpropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DefinitionsContainer.Instance.SaveProperties();
            string tmp = Config.GetPropertiesFile(true, (byte)mLocale, mBuild);
            System.Diagnostics.Process.Start(tmp);
        }

        private void recvpropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DefinitionsContainer.Instance.SaveProperties();
            string tmp = Config.GetPropertiesFile(false, (byte)mLocale, mBuild);
            System.Diagnostics.Process.Start(tmp);
        }

        private void removeLoggedPacketsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete all logged packets?", "!!", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No) return;

            ClearedPackets = true;

            mPackets.Clear();
            ListView.Items.Clear();
            mOpcodes.Clear();
            RefreshPackets();
        }

        private void mFileSeparatorMenu_Click(object sender, EventArgs e)
        {

        }

        private void thisPacketOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mPacketList.SelectedItems.Count == 0) return;
            var packet = mPacketList.SelectedItems[0] as NCPacket;
            var index = packet.Index;
            mPackets.Remove(packet);

            packet.Selected = false;
            if (index > 0)
            {
                index--;
                mPackets[index].Selected = true;
            }
            RefreshPackets();
        }

        private void allBeforeThisOneToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void onlyVisibleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mPacketList.SelectedItems.Count == 0) return;
            var packet = mPacketList.SelectedItems[0] as NCPacket;

            for (int i = 0; i < packet.Index; i++)
                mPackets.Remove(mPacketList.Items[i] as NCPacket);
            RefreshPackets();
        }

        private void allToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mPacketList.SelectedItems.Count == 0) return;
            var packet = mPacketList.SelectedItems[0] as NCPacket;

            mPackets.RemoveRange(0, mPackets.FindIndex((p) => { return p == packet; }));
            RefreshPackets();
        }

        private void onlyVisibleToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (mPacketList.SelectedItems.Count == 0) return;
            var packet = mPacketList.SelectedItems[0] as NCPacket;

            for (int i = packet.Index + 1; i < mPacketList.Items.Count; i++)
                mPackets.Remove(mPacketList.Items[i] as NCPacket);
            RefreshPackets();
        }

        private void allToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (mPacketList.SelectedItems.Count == 0) return;
            var packet = mPacketList.SelectedItems[0] as NCPacket;
            var startIndex = mPackets.FindIndex((p) => { return p == packet; }) + 1;
            mPackets.RemoveRange(startIndex, mPackets.Count - startIndex);
            RefreshPackets();
        }

    }
}
