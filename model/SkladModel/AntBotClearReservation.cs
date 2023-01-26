using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{
    public class AntBotClearReservation: AbstractEvent
    {
        AntBot antBot;
        public AntBotClearReservation(AntBot antBot)
        {
            this.antBot = antBot;
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.CleanReservation();
            antBot.RemoveFirstCommand(timeSpan);

        }
    }

}
