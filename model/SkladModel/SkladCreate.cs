using System;
using System.Collections.Generic;
using System.Linq;
using AbstractModel;

namespace SkladModel
{
    public class SkladCreate : AbstractEvent
    {
        private SkladConfig skladConfig;
        Func<CommandList, double> metricFunc;


        public SkladCreate(SkladConfig skladConfig, Func<CommandList, double> metricFunc)
        {
            this.skladConfig = skladConfig;
            this.metricFunc = metricFunc;
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {

            Sklad sklad = new Sklad(skladConfig);
            SkladLogger logger = (SkladLogger)objects.First(x => x is SkladLogger);
            logger.sklad = sklad;
            sklad.lastUpdated = timeSpan;
            sklad.getMetric = metricFunc;
            objects.Add(sklad);
            Console.WriteLine($"sklad {sklad.uid} created {sklad.lastUpdated}");
        }
    }
}
