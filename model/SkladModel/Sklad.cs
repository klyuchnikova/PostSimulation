using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using AbstractModel;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;

namespace SkladModel
{


    public class Sklad : AbstractObject
    {
        // y, x
        public Dictionary<int, Dictionary<int, int>> skladLayout = new Dictionary<int, Dictionary<int, int>>();
        public SkladConfig skladConfig;
        [XmlIgnore]
        public SquaresIsBusy squaresIsBusy;

        public Sklad()
        {

        }

        public Sklad(SkladConfig skladConfig)
        {
            this.skladConfig = skladConfig;
            int counter = 0;
            foreach (string line in System.IO.File.ReadLines(skladConfig.skladLayout))
            {
                skladLayout.Add(counter, new Dictionary<int, int>());
                string[] numbers = line.Split(',');
                for (int i = 0; i < numbers.Length; i++)
                    skladLayout[counter].Add(i, int.Parse(numbers[i]));
                counter++;
            }
            squaresIsBusy = new SquaresIsBusy(skladLayout, uid);

        }


        public override (TimeSpan, AbstractEvent) getNearestEvent(List<AbstractObject> objects)
        {
            return (TimeSpan.MaxValue, null);
        }

        public override void Update(TimeSpan timeSpan)
        {
            return;
        }
    }



}
