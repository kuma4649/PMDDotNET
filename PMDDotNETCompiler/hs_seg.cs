using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Compiler
{
    public class hs_seg
    {
        public int hs_length = 32;
        public byte[] hsbuf2 = new byte[2 * 256];
        public byte[] hsbuf3 = new byte[32 * 256];// hs_length * 256];
        public int hsbuf_end;// label   byte
        public byte[] currentBuf;

        public LinePos[] hsLp2 = new LinePos[256];
        public LinePos[] hsLp3 = new LinePos[256];
        public LinePos[] currentLp;
    }
}
