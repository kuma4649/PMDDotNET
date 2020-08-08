using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Driver
{
    public class PPZChannelWork
    {
        public int loopStartOffset;
        public int loopEndOffset;
        public bool playing;
        public ushort pan;
        public double panL;
        public double panR;
        public uint srcFrequency;
        public ushort volume;
        public uint frequency;

        public int _loopStartOffset;
        public int _loopEndOffset;
        //public uint _frequency;
        public uint _srcFrequency;

        public int bank;
        public int ptr;
        public int end;
        public double delta;
        public int num;
    }
}
