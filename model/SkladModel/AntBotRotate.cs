using AbstractModel;
using System;
using System.Collections.Generic;

namespace SkladModel
{
    public class AntBotRotate : AntBotAbstractEvent
    {
        public override AntBotAbstractEvent Clone() => new AntBotRotate(antBot);
        public AntBotRotate() { }
        public AntBotRotate(AntBot antBot)
        {
            this.antBot = antBot;
        }

        public override bool CheckReservation()
        {
            return antBot.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime()
        {
            return getStartTime() + TimeSpan.FromSeconds(antBot.sklad.skladConfig.unitRotateTime);
        }

        public override void ReserveRoom()
        {
            int x = antBot.xCord;
            int y = antBot.yCord;
            antBot.ReserveRoom(x, y, getStartTime(), getEndTime());
        }


        public override void CalculatePenalty()
        {
            int x = antBot.xCord;
            int y = antBot.yCord;
            if (antBot.sklad.skladLayout[y][x] == 2 || antBot.sklad.skladLayout[y][x] == 5)
                RotateOnLoad = 1;
            if (antBot.sklad.skladLayout[y][x] == 3 || antBot.sklad.skladLayout[y][x] == 6)
                RotateOnUnload = 1;
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.xCoordinate = antBot.xCord;
            antBot.yCoordinate = antBot.yCord;
            antBot.xSpeed = 0;
            antBot.ySpeed = 0;
            antBot.waitTime = getEndTime();
            antBot.charge -= antBot.sklad.skladConfig.unitRotateEnergy;
            antBot.state = AntBotState.Rotate;
            antBot.isXDirection = !antBot.isXDirection;
            antBot.isFree = false;
            antBot.RemoveFirstCommand(timeSpan);
            if (antBot.skladLogger != null)
            {
                antBot.skladLogger.AddLog(antBot, "Rotate");
                if (antBot.isDebug)
                {
                    Console.WriteLine($"antBot {antBot.uid} Rotate {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                }
            }
        }
    }
}