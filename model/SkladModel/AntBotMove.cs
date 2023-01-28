using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{

    public class AntBotMove : AntBotAbstractEvent
    {
        bool isCoordinate = false;
        int numCoord = 0;

        public AntBotMove() { }
        public AntBotMove(AntBot antBot)
        {
            this.antBot = antBot;
        }

        public override AntBotAbstractEvent Clone() => new AntBotMove(antBot, numCoord);
        public AntBotMove(AntBot antBot, int numCoord)
        {
            this.antBot = antBot;
            isCoordinate = true;
            this.numCoord = numCoord;
        }

        public override bool CheckReservation()
        {
            return true;
                //antBot.commandList.antState.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime() => getStartTime() + TimeSpan.FromSeconds(numCoord / antBot.sklad.skladConfig.unitSpeed);
        public override void ReserveRoom()
        {
            for (int shift = 0; shift < numCoord; shift++)
            {
                var coord = antBot.getShift(shift);

                TimeSpan startInterval = antBot.lastUpdated + TimeSpan.FromSeconds(shift / antBot.sklad.skladConfig.unitSpeed);
                double wait = shift < numCoord - 1 ? 2.0 : 1.0;
                TimeSpan endInterval = startInterval + TimeSpan.FromSeconds(wait / antBot.sklad.skladConfig.unitSpeed);
                antBot.ReserveRoom(coord.x, coord.y, startInterval, endInterval);
            }
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {

            antBot.RemoveFirstCommand(timeSpan);
            antBot.state = AntBotState.Move;
            if (antBot.skladLogger != null)
            {
                Console.WriteLine($"antBot {antBot.uid} move coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                antBot.skladLogger.AddLog(antBot, $"Move");
            }
        }
    }

}
