using System;
using System.Collections.Generic;
using System.Text;
using PMDDotNET.Common;
using musicDriverInterface;

namespace PMDDotNET.Compiler
{
    public class m_seg
    {
        public string m_filename;
        public int file_ext_adr;//w
#if efc && olddat
        public byte m_start;//b dummy
        public AutoExtendList<MmlDatum> m_buf = new AutoExtendList<MmlDatum>();//[63 * 1024 - 1];
        public byte mbuf_end;
#else
        public byte m_start;//b
        public AutoExtendList<MmlDatum> m_buf = new AutoExtendList<MmlDatum>();//[63 * 1024 - 2];
        public byte mbuf_end;
        public List<Tuple<int, MmlDatum>> dummy = new List<Tuple<int, MmlDatum>>();
#endif
    }
}
