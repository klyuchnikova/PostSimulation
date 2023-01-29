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

        public Dictionary<int, Dictionary<int, int>> skladLayout = new Dictionary<int, Dictionary<int, int>>();

        public List<(int x, int y)> source = new List<(int x, int y)>();
        public List<(int x, int y)> target = new List<(int x, int y)>();
        public List<(int x, int y)> charge = new List<(int x, int y)>();

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
                {
                    skladLayout[counter].Add(i, int.Parse(numbers[i]));
                }
                counter++;
            }

            for (int y = 0; y < skladLayout.Count; y++)
            {
                for (int x = 0; x < skladLayout[y].Count; x++)
                {
                    if (skladLayout[y][x] == 2)
                        source.Add((x, y));
                    if (skladLayout[y][x] == 3)
                        target.Add((x, y));
                    if (skladLayout[y][x] == 4)
                        charge.Add((x, y));
                    Console.Write(skladLayout[y][x] + " ");
                }
                Console.WriteLine();
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
