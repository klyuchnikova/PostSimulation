using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{
    public class AntBootAccelerate : AntBotAbstractEvent
    {
        Direction direction;

        public override AntBotAbstractEvent Clone()
        {
            return new AntBootAccelerate(antBot, direction);
        }
        public AntBootAccelerate() { }
        public AntBootAccelerate(AntBot antBot, Direction direction) 
        {
            this.antBot = antBot;
            this.direction = direction;
        }

        public override bool CheckReservation()
        {
            bool check = antBot.CheckRoom(getStartTime(), getStartTime() +
                TimeSpan.FromSeconds(1.0 / antBot.sklad.skladConfig.unitSpeed));
            return check;
        }

        public override TimeSpan getStartTime() => antBot.lastUpdated;
        public override TimeSpan getEndTime() => antBot.lastUpdated +
            TimeSpan.FromSeconds(antBot.sklad.skladConfig.unitAccelerationTime);

        public override void ReserveRoom()
        { 
            antBot.ReserveRoom(getStartTime(), getStartTime() +
                TimeSpan.FromSeconds(1.0 / antBot.sklad.skladConfig.unitSpeed));
        }

        public override void runEvent(List<AbstractObject> objects, TimeSpan timeSpan)
        {
            if (antBot.state == AntBotState.Move)
                throw new AntBotNotPosibleMovement();
            if (antBot.isXDirection & (direction == Direction.Down || direction == Direction.Up))
                throw new AntBotNotPosibleMovement();
            if (!antBot.isXDirection & (direction == Direction.Left || direction == Direction.Right))
                throw new AntBotNotPosibleMovement();
            if (antBot.sklad.skladConfig.unitAccelerationTime != 0)
                throw new NotImplementedException();
            antBot.setSpeedByDirection(direction);
            antBot.charge -= antBot.sklad.skladConfig.unitAccelerationEnergy;
            antBot.isFree = false;
            antBot.waitTime = getEndTime();
            antBot.state = AntBotState.Accelerating;


            if (antBot.commandList != null
                && antBot.commandList.commands.Count > 0
                && antBot.commandList.commands.First().Key == timeSpan)
            {
                antBot.commandList.commands.RemoveAt(0);
            }

            if (antBot.skladLogger != null)
            {
                if (antBot.isClone)
                    throw new ExecutionEngineException();
                antBot.skladLogger.AddLog(antBot, $"accelerating");
                if (antBot.isDebug)
                {
                    Console.WriteLine($"antBot {antBot.uid} accelerating{direction} {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                }
            }

        }
    }

}
