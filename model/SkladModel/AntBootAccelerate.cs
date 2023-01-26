using AbstractModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{
    public class AntBootAccelerate : AntBotAbstractEvent
    {
        Direction direction;
        public AntBootAccelerate(AntBot antBot, Direction direction) 
        {
            this.antBot = antBot;
            this.direction = direction;
        }

        public override bool CheckReservation()
        {
            return true;
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
            if (antBot.state != AntBotState.Wait)
                throw new AntBotNotPosibleMovement();
            /*
            if (antBot.isXDirection & (direction == Direction.Down || direction == Direction.Up))
                throw new AntBotNotPosibleMovement();
            if (!antBot.isXDirection & (direction == Direction.Left || direction == Direction.Right))
                throw new AntBotNotPosibleMovement();
            */
            if (antBot.sklad.skladConfig.unitAccelerationTime != 0)
                throw new NotImplementedException();
            if (direction == Direction.Left)
                antBot.xSpeed = -antBot.sklad.skladConfig.unitSpeed;
            else if (direction == Direction.Right)
                antBot.xSpeed = antBot.sklad.skladConfig.unitSpeed;
            else if (direction == Direction.Up)
                antBot.ySpeed = -antBot.sklad.skladConfig.unitSpeed;
            else if (direction == Direction.Down)
                antBot.ySpeed = antBot.sklad.skladConfig.unitSpeed;
            antBot.charge -= antBot.sklad.skladConfig.unitAccelerationEnergy;
            antBot.isFree = false;
            antBot.state = AntBotState.Accelerating;


            if (antBot.commandList != null
                && antBot.commandList.commands.Count > 0
                && antBot.commandList.commands.First().Key == timeSpan)
            {
                antBot.commandList.commands.RemoveAt(0);
            }

            if (antBot.skladLogger != null)
            {
                Console.WriteLine($"antBot {antBot.uid} accelerating{direction} {antBot.lastUpdated} coordinate {antBot.xCoordinate}, {antBot.yCoordinate}");
                antBot.skladLogger.AddLog(antBot, $"accelerating");
            }
        }
    }

}
