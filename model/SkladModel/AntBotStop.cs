using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{


    public class AntBotStop : AntBotAbstractEvent
    {

        public AntBotStop() { }
        bool isLongStop = true;

        public override AntBotAbstractEvent Clone() => new AntBotStop(antBot, isLongStop);
        public AntBotStop(AntBot antBot, bool isLongStop = true)
        {
            this.antBot = antBot;
            this.isLongStop = isLongStop;
        }

        public override bool CheckReservation()
        {
            return antBot.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime()
        {
            int x = antBot.xCord;
            int y = antBot.yCord;
            if (isLongStop)
                return antBot.sklad.squaresIsBusy.GetPosibleReserve(x, y, getStartTime());
            else
                return getStartTime() + TimeSpan.FromSeconds(antBot.sklad.skladConfig.unitStopTime);
        }
       

        public override void ReserveRoom()
        {
            int x = antBot.xCord;
            int y = antBot.yCord;
            antBot.ReserveRoom(x, y, getStartTime(), getEndTime());
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.xCoordinate = antBot.xCord;
            antBot.yCoordinate = antBot.yCord;
            antBot.xSpeed = 0;
            antBot.ySpeed = 0;
            antBot.charge -= antBot.sklad.skladConfig.unitStopEnergy;
            antBot.state = AntBotState.Wait;
            antBot.RemoveFirstCommand(timeSpan);
            antBot.isFree = (antBot.commandList.commands.Count == 0);
            if (antBot.skladLogger != null)
            {
                antBot.skladLogger.AddLog(antBot, "Stop");
                if (antBot.isDebug)
                {
                    Console.WriteLine($"antBot {antBot.uid} Stop {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                }
            }
        }
    }
}
