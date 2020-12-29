using System.Collections;
using System.Collections.Generic;

namespace PMDDotNET.Driver
{
    public class x86Register
    {
        public PW pw = null;

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
                if (pw != null && pw.checkJumpIndexBX)
                {
                    if (value == pw.jumpIndex)
                        pw.jumpIndex = -1;
                }

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

        private ushort _si;
        public ushort si {
            get
            {
                return _si;
            }
            set
            {
                if (pw != null && pw.checkJumpIndexSI)
                {
                    if (value == pw.jumpIndex) 
                        pw.jumpIndex = -1;
                }
                _si = value;
            }
        }

        public ushort bp { get; internal set; }

        public bool carry { get; internal set; }

        public bool sign { get; internal set; }

        public bool zero { get; internal set; }

        public Stack<ushort> stack = new Stack<ushort>();

        public object lockobj = new object();

        private int[] bitMask = new int[] { 0x00, 0x01, 0x03, 0x07, 0x0f, 0x1f, 0x3f, 0x7f, 0xff };

        public byte rol(byte r, int n)
        {
            n &= 7;
            byte ans = (byte)((r << n) | ((r >> (8 - n))));// & bitMask[n]));
            carry = ((ans & 0x01) != 0);
            return ans;
        }

        public byte ror(byte r, int n)
        {
            n &= 7;
            byte ans = (byte)((r << (8 - n)) | ((r >> n)));// & bitMask[8 - n]));
            carry = ((ans & 0x80) != 0);
            return ans;
        }

        public byte rcl(byte r, int n)
        {
            n &= 7;
            byte ans = (byte)(
                (r << n) 
                | ((carry ? 1 : 0) << n) 
                | (n < 2 ? 0 : (r >> (9 - n)))
                );// & bitMask[n]));
            carry = ((r & (0x100 >> n)) != 0);
            return ans;
        }

        public byte rcr(byte r, int n)
        {
            n &= 7;
            byte ans = (byte)(
                (n < 2 ? 0 : (r << (9 - n))) 
                | ((carry ? 0x100 : 0) >> n) 
                | (r >> n)
                );// & bitMask[n]));
            carry = ((r & (0x100 >> n)) != 0);
            return ans;
        }
    }
}