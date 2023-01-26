using AbstractModel;
using ExtendedXmlSerializer.Core.Sources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SkladModel
{
    public class SkladWrapper : AbstractWrapper
    {
        public bool isDebug = false;

        public bool isEventCountEmpty()
        {
            return eventList.Count == 0;
        }



        public void Debug(string info)
        {
            if (isDebug)
                Console.WriteLine(info);
        }
        public SkladWrapper(string fileSkladConfig)
        {
            byte[] fileSkladConfigByte = File.ReadAllBytes(fileSkladConfig);
            SkladConfig skladConfig = Helper.DeserializeXML<SkladConfig>(fileSkladConfigByte);

            AddEvent(TimeSpan.Zero, new SkladLoggerCreate());
            AddEvent(TimeSpan.Zero, new SkladCreate(skladConfig));
            foreach (string line in File.ReadLines(skladConfig.antBotLayout))
            {
                string[] numbers = line.Split(',');
                AddEvent(TimeSpan.Zero, new AntBotCreate(int.Parse(numbers[0]), int.Parse(numbers[1])));
            }
        }

        public List<AntBot> GetAllAnts()
        {
            return objects.FindAll(x => x is AntBot).Cast<AntBot>().ToList();
        }

        public List<AntBot> GetFreeAnts()
        {
            return GetAllAnts().FindAll(x => x.isFree);
        }

        public void SaveLog(string fileName)
        {
            SkladLogger logger = (SkladLogger)objects.First(x => x is SkladLogger);
            logger.SaveLog(fileName);
        }


        public static AntBot cloneAnt(AntBot antBot)
        {
            return antBot.ShalowClone();
        }



        public void Move(AntBot antBot, Direction direction, int numCoord = 0, double time = 0)
        {
            Debug($"Macro - Move {direction}");
            if (antBot.xSpeed > 0 || antBot.ySpeed > 0)
                throw new AntBotNotPosibleMovement();

            int freePath = getFreePath(antBot, direction, antBot.lastUpdated);
            if (numCoord == 0)
                numCoord = freePath;
            if (freePath < numCoord)
                throw new AntBotNotPosibleMovement();

            antBot.CleanReservation();

            if (time != 0)
                antBot.commandList.AddCommand(new AntBotWait(antBot, TimeSpan.FromSeconds(time)));

            if (antBot.isNeedRotateForDirection(direction))
            {
                antBot.commandList.AddCommand(new AntBotRotate(antBot));
            }




            antBot.commandList.AddCommand(new AntBootAccelerate(antBot, direction));
            antBot.commandList.AddCommand(new AntBotMove(antBot, numCoord));
            antBot.commandList.AddCommand(new AntBotStop(antBot));

            Debug($"End Macro - Move {direction}");

        }



        public int getFreePath(AntBot antBot, Direction direction, TimeSpan actionTime)
        {
            AntBot _antBot = cloneAnt(antBot);
            _antBot.Update(actionTime);

            AntBotAbstractEvent ev;
            if (_antBot.isNeedRotateForDirection(direction))
            {
                ev = new AntBotRotate(_antBot);
                ev.runEvent(objects, _antBot.lastUpdated);
                _antBot.Update(ev.getEndTime());
            }

            ev = new AntBootAccelerate(_antBot, direction);
            ev.runEvent(objects, _antBot.lastUpdated);
            _antBot.Update(ev.getEndTime());

            return _antBot.getFreePath();
        }
        private void reservePath(AntBot antBot, Direction direction, TimeSpan actionTime, int numCoord)
        {
            AntBot _antBot = cloneAnt(antBot);
            _antBot.Update(actionTime);
            _antBot.reserved = antBot.reserved;
            
            AbstractEvent ev = new AntBotMove(_antBot);
            ev.runEvent(objects, actionTime);
            Reserve(_antBot, numCoord);
        }

        public bool Reserve(AntBot antBot, int numCoord)
        {
            for (int shift = 0; shift < numCoord; shift++)
            {
                var coord = antBot.getShift(shift);

                TimeSpan startInterval = antBot.lastUpdated + TimeSpan.FromSeconds(shift / antBot.sklad.skladConfig.unitSpeed);
                double wait = shift < numCoord - 1 ? 2.0 : 1.0;
                TimeSpan endInterval = startInterval + TimeSpan.FromSeconds(wait / antBot.sklad.skladConfig.unitSpeed);
                Debug($"Reserve x:{coord.x}, y:{coord.y} from {startInterval} to {endInterval}");
                antBot.ReserveRoom(coord.x, coord.y, startInterval, endInterval);
            }
            return true;
        }
    }
   
}
