using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace PMDDotNET.Common
{
    [Serializable]
    public class PmdDosExitException : PmdException
    {
        public PmdDosExitException()
        {
        }

        public PmdDosExitException(string message) : base(message)
        {
        }

        public PmdDosExitException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PmdDosExitException(string message, int row, int col) : base(message, row, col)
        {
        }

        protected PmdDosExitException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }


    [Serializable]
    public class PmdErrorExitException : PmdException
    {
        public PmdErrorExitException()
        {
        }

        public PmdErrorExitException(string message) : base(message)
        {
        }

        public PmdErrorExitException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PmdErrorExitException(string message, int row, int col) : base(message, row, col)
        {
        }

        protected PmdErrorExitException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
