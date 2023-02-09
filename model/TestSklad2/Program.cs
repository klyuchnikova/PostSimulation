using System;
using System.Collections.Generic;
using System.Linq;
using ExtendedXmlSerializer.Configuration;
using System.Xml;
using SkladModel;
using ExtendedXmlSerializer;
using System.IO;
using ExtendedXmlSerializer.ExtensionModel.Content;
using System.Text;
using ControlModel;
using System.Xml.Linq;
using System.Data.Common;

namespace TestSklad2
{
    class Program
    {
        private static double timeEnergyMetric(CommandList arg)
        {
            return arg.lastTime.TotalSeconds + 
                (1 - arg.antState.charge/arg.antBot.sklad.skladConfig.unitChargeValue) * 
                arg.antBot.sklad.skladConfig.unitChargeTime + arg.RotateOnLoad * 4 + arg.MoveOnLoad * 5 + arg.MoveOnUnload * 0.33;
        }


        static void Main(string[] args)
        {            
            SkladWrapper skladWrapper = new SkladWrapper(@"..\..\..\..\..\wms-config.xml", false);
            skladWrapper.AddLogger();
            skladWrapper.AddSklad(timeEnergyMetric);
            skladWrapper.AddAnts(24);
            new MoveSort(skladWrapper).Run();
            //new MoveSort(skladWrapper).Run();
            skladWrapper.SaveLog(@"..\..\..\..\..\log.xml");
            SkladLogger logger = (SkladLogger)skladWrapper.objects.First(x => x is SkladLogger);
            File.WriteAllBytes(@"..\..\..\..\..\log_unity.xml", SkladWrapper.SerializeXML(logger.logs.ToArray()));
        }


    }
}
