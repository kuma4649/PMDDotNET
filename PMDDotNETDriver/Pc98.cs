using System;

namespace PMDDotNET.Driver
{
    public class Pc98
    {
        public byte InPort(int v)
        {
            if (v == 0xa468)
            {
                return 0;
            }

            throw new NotImplementedException();
        }
    }
}