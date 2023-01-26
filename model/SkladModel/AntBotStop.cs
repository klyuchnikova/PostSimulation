﻿using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{


    public class AntBotStop : AntBotAbstractEvent
    {

        public AntBotStop(AntBot antBot)
        {
            this.antBot = antBot;
        }

        public override bool CheckReservation()
        {
            return antBot.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime()
        {
            int x = antBot.xCord;
            int y = antBot.yCord;
            return antBot.sklad.squaresIsBusy.GetPosibleReserve(x, y, getStartTime());
        }

        public override void ReserveRoom()
        {
            int x = antBot.xCord;
            int y = antBot.yCord;
            antBot.ReserveRoom(x, y, getStartTime(), getEndTime());
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.xCoordinate = antBot.xCord;
            antBot.yCoordinate = antBot.yCord;
            antBot.xSpeed = 0;
            antBot.ySpeed = 0;
            antBot.charge -= antBot.sklad.skladConfig.unitStopEnergy;
            antBot.isFree = true;
            antBot.state = AntBotState.Wait;
            antBot.RemoveFirstCommand(timeSpan);
            if (antBot.skladLogger != null)
            {
                Console.WriteLine($"antBot {antBot.uid} Stop {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                antBot.skladLogger.AddLog(antBot, "Stop");
            }
        }
    }
}
