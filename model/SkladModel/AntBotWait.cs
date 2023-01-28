using AbstractModel;
using System;
using System.Collections.Generic;

namespace SkladModel
{
    public class AntBotWait : AntBotAbstractEvent
    {
        TimeSpan time;
        public AntBotWait(AntBot antBot, TimeSpan time) {
            this.antBot = antBot;
            this.time = time;
        }

        public override bool CheckReservation()
        {
            return antBot.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.commandList.lastTime;
        public override TimeSpan getEndTime() => antBot.commandList.lastTime + time;

        public override void ReserveRoom()
        {
            antBot.ReserveRoom(getStartTime(), getEndTime());
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.state = AntBotState.Wait;
            antBot.RemoveFirstCommand(timeSpan);
            if (antBot.skladLogger != null)
            {
                Console.WriteLine($"antBot {antBot.uid} Wait {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                antBot.skladLogger.AddLog(antBot, "Wait");
            }
        }
    }

}
