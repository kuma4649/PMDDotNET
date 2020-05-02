namespace PMDDotNET.Driver
{
    public class x86Register
    {
        public byte al;
        public byte ah;
        public ushort ax
        {
            get
            {
                return (ushort)(ah * 0x100 + al);
            }
            set
            {
                ah = (byte)(value >> 8);
                al = (byte)value;
            }
        }

        public byte cl;
        public byte ch;
        public ushort cx
        {
            get
            {
                return (ushort)(ch * 0x100 + cl);
            }
            set
            {
                ch = (byte)(value >> 8);
                cl = (byte)value;
            }
        }
    }
}