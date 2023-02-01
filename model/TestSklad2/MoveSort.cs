using ExtendedXmlSerializer.Core.Sources;
using SkladModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static SkladModel.SquaresIsBusy;

namespace TestSklad2
{
    class squareState
    {
        public TimeSpan xMinTime = TimeSpan.MaxValue;
        public TimeSpan yMinTime = TimeSpan.MaxValue;
        public CommandList xCommans;
        public CommandList yCommans;
    }
    internal class MoveSort
    {
        SkladWrapper skladWrapper;
        

        public MoveSort(SkladWrapper skladWrapper) { 
            this.skladWrapper = skladWrapper;
        }

        public void Run()
        {
            while (skladWrapper.Next())
            {
                if (!skladWrapper.isEventCountEmpty())
                    continue;

                if (skladWrapper.GetAllAnts().Any(x => x.state == AntBotState.UnCharged))
                    break;

                RunToLoadPoint(skladWrapper.GetFreeUnloadedAnts());
            }
        }

        void RunToLoadPoint(List<AntBot> freeAnts)
        {
            (AntBot bot, CommandList cList, TimeSpan minTime) minBotPath = (null, null, TimeSpan.MaxValue);
            freeAnts.ForEach(freeAnt => {
                freeAnt.sklad.source.ForEach(source =>
                {
                    var gp = getPath(freeAnt, source);
                    if (gp.isPathExist)
                    {
                        if (gp.cList.lastTime < minBotPath.minTime)
                        {
                            minBotPath.minTime = gp.cList.lastTime;
                            minBotPath.bot = freeAnt;
                            minBotPath.cList = gp.cList;
                        }
                    }
                });
            });
            if (minBotPath.minTime < TimeSpan.MaxValue)
            {
                AntBot bot = minBotPath.bot;
                if (minBotPath.cList.AddCommand(new AntBotLoad(bot), false))
                {
                    AntBot clone = minBotPath.cList.antState.ShalowClone();
                    clone.lastUpdated = minBotPath.cList.lastTime;
                    Random rnd = new Random();
                    int next = rnd.Next(bot.sklad.target.Count);
                    var gp = getPath(clone, bot.sklad.target[next]);
                    if (gp.isPathExist)
                    {
                        if (gp.cList.AddCommand(new AntBotUnload(minBotPath.bot), false))
                        {
                            TimeSpan reserveTimeForLeave = TimeSpan.FromSeconds(
                                bot.sklad.skladConfig.unitRotateTime +
                                1.0 / bot.sklad.skladConfig.unitSpeed);
                            if (gp.cList.antState.CheckRoom(minBotPath.cList.lastTime,
                                gp.cList.lastTime + reserveTimeForLeave))
                            {
                                gp.cList.commands.ForEach(c => c.Ev.antBot = bot);
                                minBotPath.cList.commands.AddRange(gp.cList.commands);
                                applyPath(bot, minBotPath.cList);
                                bot.ReserveRoom(gp.cList.antState.xCord, gp.cList.antState.yCord,
                                    minBotPath.cList.lastTime,
                                    gp.cList.lastTime + reserveTimeForLeave);
                            }
                        }
                    }
                }
            }
        }

        void RunToUnloadPoint(AntBot antBot)
        {
            Random rnd = new Random();
            int next = rnd.Next(antBot.sklad.target.Count);
            if (RunToPoint(antBot, antBot.sklad.target[next]))
                antBot.commandList.AddCommand(new AntBotUnload(antBot));
        }

        Dictionary<int, Dictionary<int, squareState>> state;
        public Dictionary<int, Dictionary<int, int>> skladLayout;
        public FibonacciHeap<TimeSpan, CommandList> graph;
        private bool RunToPoint(AntBot antBot, (int x, int y, bool isXDirection) point)
        {
            (bool isPathExist, CommandList cList) = getPath(antBot, point);
            if (!isPathExist) return false;
            applyPath(antBot, cList);
            return true;
        }

        private void applyPath(AntBot antBot, CommandList cList)
        {
            antBot.CleanReservation();
            for (int i = 0; i < cList.commands.Count; i++)
            {
                antBot.commandList.AddCommand(cList.commands[i].Ev);
            }
        }

        private (bool isPathExist, CommandList cList) getPath(AntBot antBot, (int x, int y, bool isXDirection) point)
        {
            CommandList cList;
            graph = new FibonacciHeap<TimeSpan, CommandList>();
            state = new Dictionary<int, Dictionary<int, squareState>>();
            skladLayout = antBot.sklad.skladLayout;
            for (int y = 0; y < skladLayout.Count; y++)
            {
                for (int x = 0; x < skladLayout[y].Count; x++)
                {
                    if (y == 0)
                        state.Add(x, new Dictionary<int, squareState>());
                    state[x].Add(y, new squareState());
                }
            }

            AntBot ant = antBot.ShalowClone();
            ant.commandList = new CommandList(ant);
            if (ant.isXDirection)
            {
                if (ant.lastUpdated < state[ant.xCord][ant.yCord].xMinTime)
                {
                    state[ant.xCord][ant.yCord].xMinTime = ant.lastUpdated;
                    state[ant.xCord][ant.yCord].xCommans = ant.commandList.Clone();
                    graph.Push(state[ant.xCord][ant.yCord].xMinTime, state[ant.xCord][ant.yCord].xCommans);
                }
            }
            else
            {
                if (ant.lastUpdated < state[ant.xCord][ant.yCord].yMinTime)
                {
                    state[ant.xCord][ant.yCord].yMinTime = ant.lastUpdated;
                    state[ant.xCord][ant.yCord].yCommans = ant.commandList.Clone();
                    graph.Push(state[ant.xCord][ant.yCord].xMinTime, state[ant.xCord][ant.yCord].yCommans);
                }
            }

            while (true)
            {
                NextStep(antBot);
                if (point.isXDirection)
                {
                    if (state[point.x][point.y].xMinTime != TimeSpan.MaxValue)
                    {
                        cList = state[point.x][point.y].xCommans;
                        break;
                    }
                }
                else
                {
                    if (state[point.x][point.y].yMinTime != TimeSpan.MaxValue)
                    {
                        cList = state[point.x][point.y].yCommans;
                        break;
                    }
                }
                if (graph.Count() == 0)
                    return (false, null);
            }
            return (true, cList);
        }

        void NextStep(AntBot antBot)
        {
            var gf = graph.Pop();
            var commandList = gf.Value;
            var ant = commandList.antState;
            var st = state[ant.xCord][ant.yCord];
            if (st.yMinTime > commandList.lastTime)
            {
                var st1 = commandList.Clone();
                if (st1.AddCommand(new AntBotRotate(antBot), false))
                {
                    st1.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                    if (st1.lastTime < st.yMinTime)
                    {
                        state[st1.antState.xCord][st1.antState.yCord].yMinTime = st1.lastTime;
                        state[st1.antState.xCord][st1.antState.yCord].yCommans = st1;
                        graph.Push(st1.lastTime, st1);
                    }
                }

            }
            if (st.xMinTime > commandList.lastTime)
            {
                var st1 = commandList.Clone();
                if (st1.AddCommand(new AntBotRotate(antBot), false))
                {
                    st1.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                    if (st1.lastTime < st.xMinTime)
                    {
                        state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                        state[st1.antState.xCord][st1.antState.yCord].xCommans = st1;
                        graph.Push(st1.lastTime, st1);
                    }
                }

            }
            for (int i = 0; i < 4; i++)
            {
                Direction dir = (Direction)i;
                if (ant.isXDirection && (dir == Direction.Up || dir == Direction.Down))
                    continue;
                if (!ant.isXDirection && (dir == Direction.Left || dir == Direction.Right))
                    continue;
                int dist = skladWrapper.getFreePath(ant, dir, ant.lastUpdated);
                for (int dst = 1; dst <= dist; dst++)
                {
                    if (ant.isXDirection)
                    {
                        var st1 = commandList.Clone();
                        st1.AddCommand(new AntBootAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        if (state[st1.antState.xCord][st1.antState.yCord].xMinTime > st1.lastTime + TimeSpan.FromSeconds(0.01))
                        {
                            state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].xCommans = st1;
                            graph.Push(st1.lastTime, st1);
                            for (int t = 1; t<10; t++)
                            {
                                var st2 = st1.Clone();
                                if (st2.AddCommand(new AntBotWait(antBot, TimeSpan.FromSeconds(i * 1.0 / antBot.sklad.skladConfig.unitSpeed)), false))
                                {
                                    st2.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                                    graph.Push(st2.lastTime, st2);
                                } else
                                {
                                    break;
                                }

                            }
                        }
                    }
                    else
                    {
                        var st1 = commandList.Clone();
                        st1.AddCommand(new AntBootAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        if (state[st1.antState.xCord][st1.antState.yCord].yMinTime > st1.lastTime + TimeSpan.FromSeconds(0.0001))
                        { 
                            state[st1.antState.xCord][st1.antState.yCord].yMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].yCommans = st1;
                            graph.Push(st1.lastTime, st1);
                            for (int t = 1; t < 10; t++)
                            {
                                var st2 = st1.Clone();
                                if (st2.AddCommand(new AntBotWait(antBot, TimeSpan.FromSeconds(i * 1.0 / antBot.sklad.skladConfig.unitSpeed)), false))
                                {
                                    st2.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                                    graph.Push(st2.lastTime, st2);
                                } else
                                {
                                    break;
                                }

                            }
                        }
                    }
                }
            }
            //xPrintState();
            //yPrintState();
        }
        private void xPrintState()
        {
            Console.WriteLine("xState");
            for (int y = 0; y < skladLayout.Count; y++)
            {
                for (int x = 0; x < skladLayout[y].Count; x++)
                {
                    if (state[x][y].xMinTime == TimeSpan.MaxValue)
                        Console.Write("Inf  ");
                    else
                        Console.Write(String.Format("{0:0.00}", state[x][y].xMinTime.TotalSeconds) + " ");
                }
                Console.WriteLine();
            }
        }

        private void yPrintState()
        {
            Console.WriteLine("yState");
            for (int y = 0; y < skladLayout.Count; y++)
            {
                for (int x = 0; x < skladLayout[y].Count; x++)
                {
                    if (state[x][y].yMinTime == TimeSpan.MaxValue)
                        Console.Write("Inf  ");
                    else
                        Console.Write(String.Format("{0:0.00}", state[x][y].yMinTime.TotalSeconds) + " ");
                }
                Console.WriteLine();
            }
        }








        private void RunToChargePoint(AntBot antBot)
        {
            RunToPoint(antBot, antBot.sklad.charge[0]);
        }
    }
}
