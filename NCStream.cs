//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System;

namespace MapleShark
{
    [Flags]
    public enum TransformMethod : int
    {
        AES_CBC_128 = 1 << 1,
        NONE = 0
    }

    public sealed class NCStream
    {
        private const int DEFAULT_SIZE = 4096;

        private bool mOutbound = false;
        private byte[] mBuffer = new byte[DEFAULT_SIZE];
        private int mCursor = 0;

        private TransformMethod _transformMethod;

        public NCStream(bool pOutbound)
        {
            mOutbound = pOutbound;          
        }

        public void Append(byte[] pBuffer) { Append(pBuffer, 0, pBuffer.Length); }
        public void Append(byte[] pBuffer, int pStart, int pLength)
        {
            if (mBuffer.Length - mCursor < pLength)
            {
                int newSize = mBuffer.Length * 2;
                while (newSize < mCursor + pLength) newSize *= 2;
                Array.Resize<byte>(ref mBuffer, newSize);
            }
            Buffer.BlockCopy(pBuffer, pStart, mBuffer, mCursor, pLength);
            mCursor += pLength;
        }

        public NCPacket Read(DateTime pTransmitted) //this function isn't necessary for regular use
        {
            //Console.WriteLine("Called Read");
            if (mCursor < 4)
            {
                //Console.WriteLine("Called Read RET 1");
                return null;
            }

            int packetSize = this.mBuffer.Length;//mAES.GetHeaderLength(mBuffer, 0, pBuild == 255 && pLocale == 1); //Modify here 
            
            if (mCursor < (packetSize + 4))
            {
                Console.WriteLine("Called Read RET 2");
                return null;
            }

            byte[] packetBuffer = new byte[packetSize];
            Buffer.BlockCopy(mBuffer, 5, packetBuffer, 0, packetSize);

            mCursor -= (packetSize + 4);
            if (mCursor > 0) Buffer.BlockCopy(mBuffer, packetSize + 4, mBuffer, 0, mCursor);
            ushort opcode;

            opcode = (ushort)(packetBuffer[0] | (packetBuffer[1] << 8));
            Buffer.BlockCopy(packetBuffer, 2, packetBuffer, 0, packetSize - 2);
            Array.Resize(ref packetBuffer, packetSize - 2);
            
            Definition definition = Config.Instance.GetDefinition(mOutbound, opcode);
            return new NCPacket(pTransmitted, mOutbound, opcode, definition == null ? "" : definition.Name, packetBuffer);
        }
    }
}
