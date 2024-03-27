//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System;
using System.Runtime.InteropServices;

namespace MapleShark
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public class XorKeyLookup
    {
        public ulong ResetCounter { get; set; } //+8   -> 0x40 value    8-10  R9
        public uint Unk2 { get; set; } //+10   -> 0x00  10-14
        public ulong count { get; set; } //0x14
        public ulong Referenced1 { get; set; } //+0x14
        public ulong Unk3 { get; set; } // value 0x200000
    };

    public class Cipher
    {
        public static byte[] NCXorkeyTable = { 0xDE, 0x90, 0xC3, 0xA6, 0xE2, 0xF6, 0xDC, 0xE8, 0x0A, 0x6F, 0xAA, 0xE6, 0xA6, 0xA8, 0xE5, 0x6B, 0x44, 0xB5, 0xCB, 0x9F, 0x0A, 0x36, 0x09, 0x46, 0xA0, 0x6D, 0x30, 0xED, 0x3E, 0x15, 0x38, 0x07, 0x44, 0xB5, 0xCB, 0x9F, 0x9A, 0x82, 0x37, 0xD3, 0x90, 0xB4, 0x87, 0xFC, 0xCE, 0xB7, 0xC5, 0x54, 0xAF, 0xC2, 0x3E, 0x3E, 0xAB, 0xE1, 0x5A, 0xAA, 0x3E, 0x3B, 0x6A, 0xA5, 0xAD, 0x52, 0xE2, 0xDD, 0x80, 0x8F, 0xC6, 0xA6, 0x31, 0x02, 0x00 };

        public static XorKeyLookup xor_out = new XorKeyLookup();
        public static XorKeyLookup xor_in = new XorKeyLookup();

        public unsafe static void XorBytes(XorKeyLookup keytable, byte[] buffer, int length, bool firstSend)
        {
            if (buffer == null || length == 0)
                return;

            if (firstSend)
            {
                keytable.count = 0;
                keytable.Referenced1 = 0;
                keytable.ResetCounter = 0x4000000040000000;
                keytable.Unk2 = 0;
                keytable.Unk3 = 0x200000;
            }

            uint r12 = 0;
            ulong r9 = 0x40;

            for (int i = 0; i < length; i++)
            {
                byte cl = NCXorkeyTable[keytable.count];
                keytable.count++;

                buffer[i] = (byte)(buffer[i] ^ cl);

                ulong eax = keytable.count;
                if (eax == r9)
                    eax = r12;

                keytable.count = eax;
            }
        }
    }
    
}