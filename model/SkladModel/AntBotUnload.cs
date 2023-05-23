﻿using AbstractModel;
using System;
using System.Collections.Generic;

namespace SkladModel
{
    public class AntBotUnload : AntBotAbstractEvent
    {
        (int x, int y, bool Direction) unloadPoint;
        public override AntBotAbstractEvent Clone() => new AntBotUnload(antBot, unloadPoint);
        public AntBotUnload(AntBot antBot, (int x, int y, bool Direction) unloadPoint)
        {
            this.antBot = antBot;
            this.unloadPoint = unloadPoint;
        }

        public override bool CheckReservation()
        {
            return antBot.CheckRoom(getStartTime(), getEndTime());
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime()
        {
            return getStartTime() + TimeSpan.FromSeconds(antBot.sklad.skladConfig.unloadTime);
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
            antBot.charge -= antBot.sklad.skladConfig.unitUnloadEnergy;
            antBot.state = AntBotState.Unloading;
            antBot.isLoaded = false;
            antBot.RemoveFirstCommand(timeSpan);
            antBot.isFree = false;
            antBot.targetYCoordinate = antBot.targetXCoordinate = -1;
            antBot.waitTime = getEndTime();
            if (antBot.skladLogger != null)
            {
                antBot.sklad.deliveryCount++;
                antBot.skladLogger.AddLog(antBot, "Unload");
                if (antBot.isDebug)
                {
                    Console.WriteLine($"antBot {antBot.uid} Unload {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                }
            }

        }
    }
}