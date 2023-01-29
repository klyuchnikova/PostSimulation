using AbstractModel;
using ExtendedXmlSerializer.Core.Sources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SkladModel
{
    public enum AntBotState : int 
    {
        Wait = 0,
        Move = 1,
        Rotate = 2, 
        Accelerating = 3,
        Stoping = 4, 
        Loading = 5,
        Unloading = 6,
        Charging = 7,
        UnCharged = 8, 
        Work = 9
    }

    public enum Direction : int
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    public class CommandList
    {
        public CommandList() { }

        public AntBot antBot;
        public AntBot antState;
        public TimeSpan lastTime;
        public CommandList(AntBot antBot) {
            this.antBot = antBot;
            antState = antBot.ShalowClone();
            antState.commandList = new CommandList();
            lastTime = antBot.lastUpdated;
        }

        public CommandList Clone()
        {
            CommandList cl = new CommandList(antBot);
            cl.antState = antState.ShalowClone();
            commands.ForEach(c => {
                var ev = c.Ev.Clone();
                cl.commands.Add((c.Key, ev));
            });
            cl.lastTime= lastTime;
            return cl;
        }

        public bool AddCommand(AntBotAbstractEvent abstractEvent, bool isNeedReserve = true)
        {

            if (commands.Count == 0)
            {
                antState = antBot.ShalowClone();
                lastTime = antBot.lastUpdated;
            }
            if (antState.commandList == null)
            {
                antState.commandList = new CommandList();
            }

            antState.Update(lastTime);
            abstractEvent.antBot = antState;
            if (!abstractEvent.CheckReservation())
            {
                abstractEvent.CheckReservation();
                throw new AntBotNotPosibleMovement();
            }
            if (isNeedReserve)
                abstractEvent.ReserveRoom();
            commands.Add((lastTime, abstractEvent));
            antState.commandList.commands.Add((lastTime, abstractEvent));        
            abstractEvent.runEvent(null, abstractEvent.getEndTime());
            lastTime = abstractEvent.getEndTime();
            abstractEvent.antBot = antBot;
            return true;
        }

        public List<(TimeSpan Key, AntBotAbstractEvent Ev)> commands = new List<(TimeSpan Key, AntBotAbstractEvent Ev)>();
    }

    public class AntBot : AbstractObject
    {
        public double xCoordinate;
        public double yCoordinate;
        [XmlIgnore]
        public int xCord => (int)Math.Round(xCoordinate);
        [XmlIgnore]
        public int yCord => (int)Math.Round(yCoordinate);

        public bool isXDirection;
        public double xSpeed;
        public double ySpeed;
        public double charge;
        public bool isLoaded;
        public bool isFree;
        public int targetXCoordinate;
        public int targetYCoordinate;
        public TimeSpan waitTime;
        public AntBotState state;
        public Sklad sklad;
        public SkladLogger skladLogger;

        [XmlIgnore]
        public CommandList commandList;

        [XmlIgnore]
        public List<(int x, int y, TimeSpan from, TimeSpan to)> reserved = new List<(int x, int y, TimeSpan from, TimeSpan to)>();
        internal List<AbstractObject> objects;

        private int nextShift(double speed)
        {
            if (speed > 0)
                return 1;
            if (speed < 0) 
                return -1;
            return 0;
        }

        private double getDelta()
        {
            if (xSpeed < 0)
                return xCoordinate - Math.Floor(xCoordinate);
            if (xSpeed > 0)
                return Math.Ceiling(xCoordinate)-xCoordinate;
            if (ySpeed < 0)
                return yCoordinate - Math.Floor(yCoordinate);
            if (ySpeed > 0)
                return Math.Ceiling(yCoordinate) - yCoordinate;
            return 0;
        }




        public (int x, int y) getShift(int shift)
        {
            if (xSpeed < 0)
                return ((int)Math.Floor(xCoordinate) - 1 - shift, (int)yCoordinate);
            if (xSpeed > 0)
                return ((int)Math.Ceiling(xCoordinate) + 1 + shift, (int)yCoordinate);
            if (ySpeed < 0)
                return ((int)(xCoordinate), (int)Math.Floor(yCoordinate) - 1 - shift);
            if (ySpeed > 0)
                return ((int)xCoordinate, (int)Math.Ceiling(yCoordinate) + 1 + shift);
            return (0, 0);
        }

        public override (TimeSpan, AbstractEvent) getNearestEvent(List<AbstractObject> objects)
        {
            TimeSpan timeUncharged;
            switch (state)
            {
                case AntBotState.UnCharged:
                    return (TimeSpan.MaxValue, null);
                case AntBotState.Wait:
                    timeUncharged = lastUpdated + TimeSpan.FromSeconds(charge / sklad.skladConfig.unitWaitEnergy);
                    if (commandList.commands.Count> 0)
                    {
                        var task = commandList.commands.First();
                        if (task.Key<timeUncharged)
                            return task;
                    }                   
                    return (timeUncharged, new AntBotUnCharging(this));
                case AntBotState.Move:
                    timeUncharged = lastUpdated + TimeSpan.FromSeconds(charge / sklad.skladConfig.unitMoveEnergy);
                    if (commandList.commands.Count > 0)
                    {
                        var task = commandList.commands.First();
                        if (task.Key < timeUncharged)
                            return task;
                    }
                    return (timeUncharged, new AntBotUnCharging(this));
                case AntBotState.Accelerating:
                    if (charge <= 0)
                        return (lastUpdated, new AntBotUnCharging(this));
                    return (waitTime, new AntBotEndTask(this));
                case AntBotState.Rotate:
                    if (charge <= 0)
                        return (lastUpdated, new AntBotUnCharging(this));
                    return (waitTime, new AntBotEndTask(this));
                case AntBotState.Charging:
                    if (charge <= 0)
                        return (lastUpdated, new AntBotUnCharging(this));
                    return (waitTime, new AntBotEndTask(this));
                case AntBotState.Loading:
                    if (charge <= 0)
                        return (lastUpdated, new AntBotUnCharging(this));
                    return (waitTime, new AntBotEndTask(this));
                case AntBotState.Unloading:
                    if (charge <= 0)
                        return (lastUpdated, new AntBotUnCharging(this));
                    return (waitTime, new AntBotEndTask(this));
                case AntBotState.Work:
                    if (charge <= 0)
                        return (lastUpdated, new AntBotUnCharging(this));
                    timeUncharged = lastUpdated + TimeSpan.FromSeconds(charge / sklad.skladConfig.unitWaitEnergy);
                    return (waitTime, new AntBotEndTask(this));



            }
            return (TimeSpan.MaxValue, null);
        }



        public double getSecond(TimeSpan timeSpan)
        {
            return (timeSpan - lastUpdated).TotalSeconds;
        }

        public override void Update(TimeSpan timeSpan)
        {
            double second = getSecond(timeSpan);
            xCoordinate += second * xSpeed;
            yCoordinate += second * ySpeed;
            switch (state)
            {
                case AntBotState.Wait:
                    charge -= sklad.skladConfig.unitWaitEnergy * second;
                    break;
                case AntBotState.Move:
                    charge -= sklad.skladConfig.unitMoveEnergy * second;
                    break;
            }
            lastUpdated = timeSpan;
        }


        public void Stop(TimeSpan timeSpan)
        {
            commandList.commands.Add((timeSpan, new AntBotStop(this)));
        }



        public int getFreePath()
        {
            double delta = getDelta();
            int shift = 0;
            while (isNotBusy(shift, delta)) { shift++; }

            return shift;
        }



        public void CleanReservation()
        {
            reserved.ForEach(res => sklad.squaresIsBusy.UnReserveRoom(res.x, res.y, res.from));
            reserved.Clear();
            commandList.lastTime = lastUpdated;
        }


        private bool isNotBusy(int shift, double delta)
        {
            var coord = getShift(shift);

            TimeSpan startInterval = lastUpdated + TimeSpan.FromSeconds((delta + shift) / sklad.skladConfig.unitSpeed);
            TimeSpan endInterval = startInterval + TimeSpan.FromSeconds(2.0 / sklad.skladConfig.unitSpeed);
            return !sklad.squaresIsBusy.CheckIsBusy(coord.x, coord.y, startInterval, endInterval, uid);
        }

        public bool CheckRoom(TimeSpan from, TimeSpan to)
        {
            return CheckRoom(xCord, yCord, from, to);
        }

        public bool CheckRoom(int x, int y, TimeSpan from, TimeSpan to)
        {
            return !sklad.squaresIsBusy.CheckIsBusy(x, y, from, to, uid);
        }
        public void ReserveRoom(TimeSpan from, TimeSpan to)
        {
            ReserveRoom(xCord, yCord, from, to);
        }
        public void ReserveRoom(int x, int y, TimeSpan from, TimeSpan to)
        {
            if (!CheckRoom(x, y, from, to))
                throw new AntBotNotPosibleMovement();
            sklad.squaresIsBusy.ReserveRoom(x, y, from, to, uid);         
            reserved.Add((x, y, from, to));
            Console.WriteLine($"Reserve x:{x}, y:{y} from {from} to {to} uid {uid}");
        }


        public AntBot ShalowClone()
        {
            AntBot _antBot = new AntBot();
            _antBot.charge = this.charge;
            _antBot.lastUpdated = this.lastUpdated;
            _antBot.xCoordinate = this.xCoordinate;
            _antBot.yCoordinate = this.yCoordinate;
            _antBot.xSpeed = this.xSpeed;
            _antBot.ySpeed = this.ySpeed;
            _antBot.uid = this.uid;
            _antBot.sklad = this.sklad;
            _antBot.isXDirection = this.isXDirection;
            _antBot.reserved= this.reserved;
            return _antBot;
        }

        internal TimeSpan actionTime()
        {
            return commandList.lastTime;
        }

        internal void RemoveFirstCommand(TimeSpan timeSpan)
        {
            if (commandList != null
                && commandList.commands.Count > 0
                && commandList.commands.First().Key == timeSpan)
            {
                commandList.commands.RemoveAt(0);
            }
        }

        public bool isNeedRotateForDirection(Direction direction)
        {
            if (direction == Direction.Up || direction == Direction.Down)
                return isXDirection;
            return !isXDirection;
        }

        public TimeSpan getTimeForFullCharge()
        {
            return TimeSpan.FromSeconds(
                (sklad.skladConfig.unitChargeValue - charge) /
                sklad.skladConfig.unitChargeValue * sklad.skladConfig.unitChargeTime);
        }

    }

}