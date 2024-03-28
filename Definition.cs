//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
namespace NCShark
{
    public sealed class Definition
    {
		public byte Locale = 0;
        public bool Outbound = false;
        public ushort Opcode = 0;
        public string Name = "";
        public bool Ignore = false;

        public override string ToString()
        {
            return "Locale: " + Locale + ";" + "; Name: " + Name + "; Opcode: 0x" + Opcode.ToString("X4") + "; Outbound: " + Outbound + "; Ignored: " + Ignore;
        }
    }
}
