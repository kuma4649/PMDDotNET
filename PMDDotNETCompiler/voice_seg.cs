using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Compiler
{
    public class voice_seg
    {
        public string v_filename;//b 128 dup(?)
        public byte[] voice_buf = new byte[8192];
    }
}
