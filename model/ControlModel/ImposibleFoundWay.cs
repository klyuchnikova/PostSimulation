using System;
using System.Runtime.Serialization;

namespace ControlModel
{
    [Serializable]
    internal class ImposibleFoundWay : Exception
    {
        public ImposibleFoundWay()
        {
        }

        public ImposibleFoundWay(string message) : base(message)
        {
        }

        public ImposibleFoundWay(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ImposibleFoundWay(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}