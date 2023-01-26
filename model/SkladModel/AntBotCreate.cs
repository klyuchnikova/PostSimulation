using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{
    public class AntBotCreate : AntBotAbstractEvent
    {
        int x;
        int y;
        public AntBotCreate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override bool CheckReservation()
        {
            throw new NotImplementedException();
        }

        public override TimeSpan getEndTime()
        {
            throw new NotImplementedException();
        }

        public override TimeSpan getStartTime()
        {
            throw new NotImplementedException();
        }

        public override void ReserveRoom()
        {
            throw new NotImplementedException();
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            AntBot antBot = new AntBot();
            antBot.sklad = (Sklad)objects.First(x=> x is Sklad);
            antBot.xCoordinate = x;
            antBot.yCoordinate = y;
            antBot.isXDirection = true;
            antBot.xSpeed = 0;
            antBot.ySpeed = 0;
            antBot.isLoaded = false;
            antBot.isFree = true;
            antBot.charge = antBot.sklad.skladConfig.unitChargeValue;
            antBot.targetXCoordinate = x;
            antBot.targetYCoordinate = y;
            antBot.state = AntBotState.Wait;
            antBot.lastUpdated = timeSpan;
            antBot.waitTime = TimeSpan.MaxValue;
            antBot.ReserveRoom(x, y, antBot.lastUpdated, TimeSpan.MaxValue);
            antBot.skladLogger = (SkladLogger)objects.First(x=> x is SkladLogger);
            antBot.skladLogger.AddLog(antBot, "Create AntBot");
            antBot.objects = objects;
            antBot.commandList = new CommandList(antBot);
            objects.Add(antBot);
            
            Console.WriteLine($"antBot {antBot.uid} created {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
        }
    }

}
