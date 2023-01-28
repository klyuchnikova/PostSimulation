using System;
using System.Collections.Generic;
using System.Linq;
using ExtendedXmlSerializer.Configuration;
using System.Xml;
using SkladModel;
using ExtendedXmlSerializer;
using System.IO;
using ExtendedXmlSerializer.ExtensionModel.Content;
using System.Text;

namespace TestSklad2
{
    class Program
    {





        static void Main(string[] args)
        {
            void RandomMove(SkladWrapper skladWrapper)
            {
                System.Random rnd = new System.Random(DateTime.Now.Millisecond);
                skladWrapper.isDebug = true;
                while (skladWrapper.Next())
                {
                    List<AntBot> freeAnts = skladWrapper.GetFreeAnts();
                    if (freeAnts.Count > 0 && skladWrapper.isEventCountEmpty())
                    {
                        foreach (AntBot ant in freeAnts)
                        {
                            if (ant.charge > 7100)
                                if (ant.commandList.commands.Count == 0)
                                {
                                    Direction dir = (Direction)rnd.Next(0, 4);
                                    int pm = skladWrapper.getFreePath(ant, dir, ant.lastUpdated);
                                    int count = 0;
                                    while (pm == 0)
                                    {
                                        count++;
                                        if (count > 10)
                                            break;
                                        dir = (Direction)rnd.Next(0, 4);
                                        pm = skladWrapper.getFreePath(ant, dir, ant.lastUpdated);
                                    }
                                    if (pm != 0)
                                    {
                                        skladWrapper.Move(ant, dir, pm);
                                        break;
                                    }
                                }
                        }
                    }
                }
            }



            
            SkladWrapper skladWrapper = new SkladWrapper(@"..\..\..\..\..\wms-config.xml");

            /*
            while (skladWrapper.Next() && !skladWrapper.isEventCountEmpty())
            {

            }

            List<AntBot> freeAnts = skladWrapper.GetFreeAnts();
            AntBot trgBot = freeAnts[0];
            trgBot.CleanReservation();
            trgBot.commandList.AddCommand(new AntBotRotate(trgBot));
            trgBot.commandList.AddCommand(new AntBotWait(trgBot, TimeSpan.FromSeconds(10)));
            trgBot.commandList.AddCommand(new AntBotWait(trgBot, TimeSpan.FromSeconds(10)));
            trgBot.commandList.AddCommand(new AntBotWait(trgBot, TimeSpan.FromSeconds(10)));

            while (skladWrapper.Next())
            {
                Console.WriteLine($"control time {skladWrapper.updatedTime.TotalSeconds}");

            }

            */


            new MoveSort(skladWrapper).Run();


            skladWrapper.SaveLog(@"..\..\..\..\..\log.xml");
        }
    }
}
