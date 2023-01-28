using AbstractModel;
using System;

namespace SkladModel
{
    public abstract class AntBotAbstractEvent : AbstractEvent
    {
        public AntBot antBot;


        public abstract TimeSpan getStartTime();
        public abstract TimeSpan getEndTime();
        public abstract bool CheckReservation();
        public abstract void ReserveRoom();
        public abstract AntBotAbstractEvent Clone();
    }

}
