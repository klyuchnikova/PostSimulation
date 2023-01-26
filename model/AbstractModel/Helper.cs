using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace AbstractModel
{
    public class Helper
    {
        public static byte[] SerializeXML<T>(T stamp)
        {
            XmlSerializer formatter = new XmlSerializer(typeof(T));
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, stamp);
            return stream.ToArray();
        }

        public static T DeserializeXML<T>(byte[] binaryData)
        {
            var formatter = new XmlSerializer(typeof(T));
            var ms = new MemoryStream(binaryData);
            return (T)formatter.Deserialize(ms);
        }

    }
}
