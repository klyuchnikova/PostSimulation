﻿using AbstractModel;
using System;
using System.Collections.Generic;

namespace SkladModel
{
    public class AntBotUnCharging : AbstractEvent
    {
        AntBot antBot;
        public AntBotUnCharging(AntBot antBot)
        {
            this.antBot = antBot;
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            antBot.charge = 0; 
            antBot.xSpeed = 0;
            antBot.ySpeed = 0;
            antBot.state = AntBotState.UnCharged;
            antBot.waitTime = TimeSpan.MaxValue;
            antBot.commandList = new CommandList(antBot);
            antBot.CleanReservation();
            //antBot.ReserveRoom(antBot.lastUpdated, TimeSpan.MaxValue);

            if (antBot.skladLogger != null)
            {
                antBot.skladLogger.AddLog(antBot, "UnCharging");
                if (antBot.isDebug)
                {
                    Console.WriteLine($"antBot {antBot.uid} uncharged {antBot.lastUpdated}");
                }
            }

        }
    }

}
