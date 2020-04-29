using System;
using System.Collections.Generic;
using System.Text;

namespace PMDDotNET.Common
{
    public class AutoExtendList<T>
    {
        private List<T> buf;

        public int Count
        {
            get
            {
                return buf == null ? 0 : buf.Count;
            }
        }

        public AutoExtendList()
        {
            buf = new List<T>();
        }

        public void Set(int adr,params T[] ds)
        {
            foreach (T d in ds)
            {
                if (adr >= buf.Count)
                {
                    int size = adr + 1;
                    for (int i = buf.Count; i < size; i++)
                        buf.Add(default(T));
                }
                buf[adr++] = d;
            }
        }

        public T Get(int adr)
        {
            if (adr >= buf.Count) return default(T);
            return buf[adr];
        }

        public T[] Get(int adr,int len)
        {
            T[] ret = new T[len];
            for (int i = 0; i < len; i++)
            {
                if (adr >= buf.Count)
                {
                    ret[i] = default(T);
                    continue;
                }
                ret[i] = buf[adr];
            }

            return ret;
        }

        public T[] GetByteArray()
        {
            return buf.ToArray();
        }
    }
}
