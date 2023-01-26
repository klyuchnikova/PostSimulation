using AbstractModel;
using System;
using System.Collections.Generic;

namespace SkladModel
{
    public class SkladLoggerCreate : AbstractEvent
    {
        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            objects.Add(new SkladLogger());
        }
    }

}