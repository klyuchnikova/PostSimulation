using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkladModel
{
    public class AntBotCharged : AntBotAbstractEvent
    {
        public override AntBotAbstractEvent Clone() => new AntBotCharged(antBot);
        public AntBotCharged(AntBot antBot)
        {
            this.antBot = antBot;
        }

        public override bool CheckReservation()
        {
            return antBot.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime() => antBot.lastUpdated;

        public override void ReserveRoom()
        {
            //antBot.ReserveRoom(getStartTime(), getEndTime());
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.state = AntBotState.Wait;
            antBot.charge = antBot.sklad.skladConfig.unitChargeValue;
            antBot.isFree = (antBot.commandList.commands.Count == 0);
            --antBot.sklad.skladTargeted[antBot.yCord][antBot.xCord];
            antBot.recalculateHorizon = antBot.lastUpdated;
            if (antBot.skladLogger != null)
            {
                antBot.skladLogger.AddLog(antBot, "Charged");
                if (antBot.isDebug)
                {
                    Console.WriteLine($"antBot {antBot.uid} Charged {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                }
            }

        }
    }
}
