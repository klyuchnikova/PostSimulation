using System;
using System.Collections.Generic;
using System.Linq;
using AbstractModel;

namespace SkladModel
{
    public class SkladCreate : AbstractEvent
    {
        private SkladConfig skladConfig;

        public SkladCreate(SkladConfig skladConfig)
        {
            this.skladConfig = skladConfig;
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {

            Sklad sklad = new Sklad(skladConfig);
            SkladLogger logger = (SkladLogger)objects.First(x => x is SkladLogger);
            logger.sklad = sklad;
            sklad.lastUpdated = timeSpan;
            objects.Add(sklad);
            Console.WriteLine($"sklad {sklad.uid} created {sklad.lastUpdated}");
        }
    }



}
