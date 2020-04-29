using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Compiler
{
    public class work
    {
        public CompilerInfo compilerInfo = new CompilerInfo();
        public byte[] ppzfile_buf = new byte[128 * 8];

        public int si { get; internal set; }
        public int di { get; internal set; }
        public int bp { get; internal set; }
        public byte al { get; internal set; }
        public byte ah { get; internal set; }
        public int bx { get; internal set; }
        public int dx { get; internal set; }
    }
}
