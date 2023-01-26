using AbstractModel;
using System;
using System.Collections.Generic;

namespace SkladModel
{
    internal class AntBotRotate : AntBotAbstractEvent
    {
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
            throw new NotImplementedException();
        }
    }
}