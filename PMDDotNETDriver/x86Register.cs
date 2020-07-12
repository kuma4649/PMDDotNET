using System.Collections;
using System.Collections.Generic;

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

        public byte bl;
        public byte bh;
        public ushort bx
        {
            get
            {
                return (ushort)(bh * 0x100 + bl);
            }
            set
            {
                bh = (byte)(value >> 8);
                bl = (byte)value;
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

        public byte dl;
        public byte dh;
        public ushort dx
        {
            get
            {
                return (ushort)(dh * 0x100 + dl);
            }
            set
            {
                dh = (byte)(value >> 8);
                dl = (byte)value;
            }
        }

        public ushort di { get; internal set; }
        public ushort si { get; internal set; }

        public bool carry { get; internal set; }
        public bool sign { get; internal set; }

        public Stack<ushort> stack = new Stack<ushort>();
    }
}