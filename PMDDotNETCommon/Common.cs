using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Common
{
    public static class Common
    {
        public static ushort GetLe16(MmlDatum[] md, int adr)
        {
            return (ushort)(md[adr].dat + md[adr + 1].dat * 0x100);
        }

    }
}
