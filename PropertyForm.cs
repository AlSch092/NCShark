//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
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
    public partial class PropertyForm : DockContent
    {
        public PropertyForm()
        {
            InitializeComponent();
        }

        public MainForm MainForm { get { return ParentForm as MainForm; } }
        public PropertyGrid Properties { get { return mProperties; } }
    }
}
