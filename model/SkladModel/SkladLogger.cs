using AbstractModel;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace SkladModel
{
    [Serializable]
    public class AntStateChange
    {
        public AntStateChange() { }
        public AntStateChange(AntBot antBot, string command) 
        {
            xCoordinate= antBot.xCoordinate;
            yCoordinate= antBot.yCoordinate;
            isXDirection= antBot.isXDirection;
            xSpeed= antBot.xSpeed;
            ySpeed= antBot.ySpeed;
            charge= antBot.charge;
            isLoaded= antBot.isLoaded;
            uid= antBot.uid;
            lastUpdated= antBot.lastUpdated;
            state= antBot.state;
            this.command= command;
        }
        public double xCoordinate;
        public double yCoordinate;
        public bool isXDirection;
        public double xSpeed;
        public double ySpeed;
        public double charge;
        public bool isLoaded;
        public string uid;
        public TimeSpan lastUpdated;
        public AntBotState state;
        public string command;
    }


    public class SkladLogger : AbstractObject
    {
        public Sklad sklad;
        public List<AntStateChange> logs = new List<AntStateChange>();
        public void AddLog(AbstractObject obj, string command) 
        {
            if (obj is Sklad)
                sklad = (Sklad)obj;
            if (obj is AntBot)
            {
                logs.Add(new AntStateChange((AntBot)obj, command));
            }
            
        }

        public void SaveLog(string fileName)
        {
            var serializer = new ConfigurationContainer()
                .UseOptimizedNamespaces() //If you want to have all namespaces in root element
                .Create();

            var xml = serializer.Serialize(
                new XmlWriterSettings { Indent = true }, //If you want to formated xml
                this);
            File.WriteAllText(fileName, xml);
        }

        public static SkladLogger loadLogger(string fileName)
        {
            byte[] bytes_xml = File.ReadAllBytes(@"C:\SKLAD\log.xml");
            IExtendedXmlSerializer serializer = new ConfigurationContainer().UseOptimizedNamespaces().Create();
            MemoryStream contentStream = new MemoryStream(bytes_xml);
            return serializer.Deserialize<SkladLogger>(new XmlReaderSettings { /* ... */ }, contentStream);
        }

        public override (TimeSpan, AbstractEvent) getNearestEvent(List<AbstractObject> objects)
        {
            return (TimeSpan.MaxValue, null);
        }

        public override void Update(TimeSpan timeSpan)
        {
            lastUpdated = timeSpan;
        }
    }

}