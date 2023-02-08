using AbstractModel;
using System;

namespace SkladModel
{
    public abstract class AntBotAbstractEvent : AbstractEvent
    {
        public AntBot antBot;
        public int RotateOnLoad = 0;
        public int RotateOnUnload = 0;
        public int MoveOnLoad = 0;
        public int MoveOnUnload = 0;


        public abstract TimeSpan getStartTime();
        public abstract TimeSpan getEndTime();
        public abstract bool CheckReservation();
        public abstract void ReserveRoom();
        public abstract AntBotAbstractEvent Clone();

        public virtual void CalculatePenalty() { }

    }

}
