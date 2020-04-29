using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace PMDDotNET.Common
{
    [Serializable]
    public class PmdException: Exception
    {
        public PmdException()
        {
        }

        public PmdException(string message) : base(message)
        {
        }

        public PmdException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected PmdException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public PmdException(string message, int row, int col) : base(string.Format(msg.get("E0300"), row, col, message))
        {
        }
    }
}
