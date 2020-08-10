using musicDriverInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PMDDotNET.Common
{
    public static class Common
    {
        public static ushort GetLe16(MmlDatum[] md, int adr)
        {
            return (ushort)(md[adr].dat + md[adr + 1].dat * 0x100);
        }


        public static byte[] GetPCMDataFromFile(string fnPcm, Func<string, Stream> appendFileReaderCallback)
        {
            try
            {
                using (Stream pd = appendFileReaderCallback?.Invoke(fnPcm))
                {
                    return ReadAllBytes(pd);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// ストリームから一括でバイナリを読み込む
        /// </summary>
        public static byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null) return null;

            var buf = new byte[8192];
            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    var r = stream.Read(buf, 0, buf.Length);
                    if (r < 1)
                    {
                        break;
                    }
                    ms.Write(buf, 0, r);
                }
                return ms.ToArray();
            }
        }

    }
}