using System;
using System.Runtime.Serialization;

namespace SkladModel
{
    [Serializable]
    public class CheckStateException : Exception
    {
        public CheckStateException()
        {
        }

        public CheckStateException(string message) : base(message)
        {
        }

        public CheckStateException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CheckStateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}