﻿//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace NCShark
{
    public class StructureNode : TreeNode
    {
        public byte[] Buffer;
        public int Cursor;
        public int Length;

        public StructureNode(string pDisplay, byte[] pBuffer, int pCursor, int pLength)
            : base(pDisplay)
        {
            Buffer = pBuffer;
            Cursor = pCursor;
            Length = pLength;
        }
    }
}
