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
            return antBot.commandList.antState.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.commandList.lastTime;
        public override TimeSpan getEndTime() => antBot.commandList.lastTime + time;

        public override void ReserveRoom()
        {
            antBot.commandList.antState.ReserveRoom(getStartTime(), getEndTime());
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            throw new NotImplementedException();
        }
    }

}
