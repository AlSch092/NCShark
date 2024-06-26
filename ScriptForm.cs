﻿//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace NCShark
{
    public partial class ScriptForm : DockContent
    {
        private string mPath = "";
        private NCPacket mPacket = null;

        public ScriptForm(string pPath, NCPacket pPacket)
        {
            mPath = pPath;
            mPacket = pPacket;
            InitializeComponent();
            if (pPacket != null)
                Text = "Script 0x" + pPacket.Opcode.ToString("X4") + ", " + (pPacket.Outbound ? "Outbound" : "Inbound");
            else
                Text = "Common Script";
        }

        internal NCPacket Packet { get { return mPacket; } }

        private void ScriptForm_Load(object pSender, EventArgs pArgs)
        {
            mScriptEditor.Document.SetSyntaxFromEmbeddedResource(Assembly.GetExecutingAssembly(), "MapleShark.ScriptSyntax.txt");
            if (File.Exists(mPath)) mScriptEditor.Open(mPath);
        }

        private void mScriptEditor_TextChanged(object pSender, EventArgs pArgs)
        {
            mSaveButton.Enabled = true;
        }

        private void mSaveButton_Click(object pSender, EventArgs pArgs)
        {
            if (mScriptEditor.Document.Text.Length == 0) File.Delete(mPath);
            else mScriptEditor.Save(mPath);
            Close();
        }

        private void mImportButton_Click(object sender, EventArgs e)
        {

            if (FileImporter.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(FileImporter.FileName))
                {
                    if (mScriptEditor.Document.Text.Length > 0 && MessageBox.Show("Are you sure you want to open this file? The current script will be replaced with the one from the file you selected.", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                        return;
                    mScriptEditor.Open(FileImporter.FileName);
                }
            }
        }
    }
}
