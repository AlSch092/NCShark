﻿//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace NCShark
{
    public partial class OutputForm : DockContent
    {
        public OutputForm(string pTitle)
        {
            InitializeComponent();
            Text = pTitle;
        }

        public void Append(string pOutput) { mTextBox.AppendText(pOutput); }
    }
}
