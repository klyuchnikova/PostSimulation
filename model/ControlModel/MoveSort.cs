using SkladModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using static SkladModel.SquaresIsBusy;

namespace ControlModel
{
    class squareState
    {
        public TimeSpan xMinTime = TimeSpan.MaxValue;
        public TimeSpan yMinTime = TimeSpan.MaxValue;
        public double xMinMetric = double.MaxValue;
        public double yMinMetric = double.MaxValue;
        public CommandList xCommans;
        public CommandList yCommans;
    }
    public class MoveSort
    {
        SkladWrapper skladWrapper;
        TimeSpan maxTime = TimeSpan.MaxValue;

        public MoveSort(SkladWrapper skladWrapper) { 
            this.skladWrapper = skladWrapper;
        }

        public void Run()
        {
            Run(TimeSpan.MaxValue);
        }
        public void Run(TimeSpan maxModelTime)
        {
            TimeSpan timeProgress = TimeSpan.Zero;
            DateTime now = DateTime.Now;
            while (skladWrapper.Next())
            {

                if (timeProgress < skladWrapper.updatedTime)
                {
                    Console.WriteLine($"{skladWrapper.updatedTime}  {DateTime.Now - now}  {skladWrapper.GetSklad().deliveryCount}");
                    timeProgress += TimeSpan.FromMinutes(1);
                }

                if (!skladWrapper.isEventCountEmpty())
                    continue;

                if (skladWrapper.GetSklad().deliveryCount >= 5000)
                {
                    Console.WriteLine($"Delivery time: {skladWrapper.updatedTime.TotalSeconds}");
                    break;
                }

                if (skladWrapper.updatedTime > maxModelTime)
                    break;

                if (skladWrapper.GetAllAnts().Any(x => x.state == AntBotState.UnCharged))
                {
                    Console.WriteLine("UNCHARGED");
                    break;
                }
                    

                skladWrapper.GetFreeAnts().ForEach(x => {
                    if (x.charge < 3600)
                        RunToChargePoint(x);
                });

                RunToLoadPoint(skladWrapper.GetFreeUnloadedAnts());
                try
                {
                    TryRunToFreePoint(skladWrapper.GetFreeAnts());
                }
                catch (ImposibleFoundWay ex)
                {
                    Console.WriteLine("Good try but need another exit!");
                    break;
                }                
                /*
                try
                {
                    
                } catch (ImposibleFoundWay ex)
                {
                    Console.WriteLine("We going to the hell!");
                    List<AntBot> allAnt = skladWrapper.GetAllAnts();
                    foreach (AntBot ant in allAnt)
                    {
                        if (ant.state == AntBotState.Wait)
                        {
                            ant.CleanReservation();
                            ant.commandList = new CommandList(ant);
                            ant.commandList.AddCommand(new AntBotWait(ant.lastUpdated, TimeSpan.MaxValue));
                        }
                        else if (ant.state == AntBotState.Rotate)
                        {
                            ant.CleanReservation();
                            ant.commandList = new CommandList(ant);
                            ant.commandList.AddCommand(new AntBotWait(ant.lastUpdated, ant.waitTime));
                            ant.commandList.AddCommand(new AntBotWait(ant.lastUpdated, TimeSpan.MaxValue));
                        }
                        else if (ant.state == AntBotState.Move)
                        {
                            ant.CleanReservation();
                            ant.commandList = new CommandList(ant);
                            ant.commandList.AddCommand(new AntBotWait(ant.lastUpdated, ant.waitTime));
                            ant.commandList.AddCommand(new AntBotWait(ant.lastUpdated, TimeSpan.MaxValue));
                        }
                    }
                }
                */
            }
        }

        private void TryRunToFreePoint(List<AntBot> antBots)
        {
            foreach (var ant in antBots)
            {
                if (ant.reserved.Count > 0)
                {
                    if (ant.reserved.Any(r=>r.x == ant.xCord && r.y == ant.yCord && r.from<=ant.lastUpdated && r.to == TimeSpan.MaxValue))
                        continue;
                }
                var gp = getPath(ant, ant.sklad.source[0]);
                double min = double.MaxValue;
                TimeSpan minTime = TimeSpan.MaxValue;
                TimeSpan posibleReserve = TimeSpan.MaxValue;
                CommandList minPath = new CommandList(ant);
                CommandList minPosiblePath = new CommandList(ant);
                ant.CleanReservation();
                ant.commandList = new CommandList(ant);
                TimeSpan maxReserve = TimeSpan.Zero;
                foreach (var xKey in state.Keys)
                {
                    foreach(var yKey in state[xKey].Keys)
                    {
                        if (state[xKey][yKey].xMinMetric < min)
                        {
                            if (ant.CheckRoom(xKey, yKey, state[xKey][yKey].xMinTime, TimeSpan.MaxValue))
                            {
                                min = state[xKey][yKey].xMinMetric;
                                minTime = state[xKey][yKey].xMinTime;
                                minPath = state[xKey][yKey].xCommans;
                            } else
                            {
                                var mr = ant.sklad.squaresIsBusy.GetPosibleReserve(xKey, yKey, state[xKey][yKey].xMinTime);
                                if (mr>maxReserve)
                                {
                                    maxReserve = mr;
                                    minPosiblePath = state[xKey][yKey].xCommans;
                                }
                            }
                        }
                        if (state[xKey][yKey].yMinMetric < min)
                        {
                            if (ant.CheckRoom(xKey, yKey, state[xKey][yKey].yMinTime, TimeSpan.MaxValue))
                            {
                                min = state[xKey][yKey].yMinMetric;
                                minTime = state[xKey][yKey].yMinTime;
                                minPath = state[xKey][yKey].yCommans;                              
                            }
                            else
                            {
                                var mr = ant.sklad.squaresIsBusy.GetPosibleReserve(xKey, yKey, state[xKey][yKey].yMinTime);
                                if (mr > maxReserve)
                                {
                                    maxReserve = mr;
                                    minPosiblePath = state[xKey][yKey].yCommans;
                                }
                            }
                        }
                    }
                }
                if (minTime!=TimeSpan.MaxValue)
                {
                    applyPath(ant, minPath);
                    ant.commandList.antState.ReserveRoom(minTime, TimeSpan.MaxValue);
                } else
                {
                    if (maxReserve >= ant.lastUpdated + TimeSpan.FromSeconds(1.0/3.0))
                    { 
                        applyPath(ant, minPosiblePath);
                        ant.commandList.antState.ReserveRoom(ant.commandList.lastTime, maxReserve);
                    }
                    else 
                        throw new ImposibleFoundWay();
                }
            }
        }




        void RunToLoadPoint(List<AntBot> freeAnts)
        {
            (AntBot bot, CommandList cList, TimeSpan minTime) minBotPath = (null, null, TimeSpan.MaxValue);
            int freeAntCount = freeAnts.Count;
            do
            {
                freeAntCount = freeAnts.Count;
                freeAnts.ForEach(freeAnt =>
                {
                    if (freeAnt.commandList.commands.Count>0)
                    {
                        throw new CheckStateException();
                    }
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
                        Random rnd = new Random();
                        int next = rnd.Next(bot.sklad.target.Count);
                        var gp = getPathFromLastStep(minBotPath.cList, bot.sklad.target[next]);
                        if (gp.isPathExist)
                        {
                            if (gp.cList.AddCommand(new AntBotUnload(minBotPath.bot, bot.sklad.target[next]), false))
                            {
                                gp.cList.AddCommand(new AntBotWait(minBotPath.bot, TimeSpan.Zero));
                                var escapePath = getPathToEscape(gp.cList);
                                if (escapePath.isPathExist)
                                {                                
                                    gp.cList.commands.ForEach(c => c.Ev.antBot = bot);
                                    minBotPath.cList.commands.AddRange(gp.cList.commands);
                                    applyPath(bot, minBotPath.cList);
                                    AntBot ant = gp.cList.antState.ShalowClone();
                                    ant.commandList = new CommandList(ant);
                                    ant.lastUpdated = gp.cList.lastTime;
                                    escapePath.cList.AddCommand(new AntBotStop(ant));
                                    reservePath(ant, escapePath.cList);
                                    bot.isFree = false;          
                                }
                            }
                        }
                    }
                }
            } while (freeAntCount != freeAnts.Count);

        }


        Dictionary<int, Dictionary<int, squareState>> state;
        public Dictionary<int, Dictionary<int, int>> skladLayout;
        public FibonacciHeap<double, CommandList> graph;
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

        private void reservePath(AntBot antBot, CommandList cList)
        {
            for (int i = 0; i < cList.commands.Count; i++)
            {
                antBot.commandList.AddCommand(cList.commands[i].Ev);
            }
        }


        private (bool isPathExist, CommandList cList) getPathFromLastStep(CommandList cList, (int x, int y, bool isXDirection) point)
        {
            AntBot clone = cList.antState.ShalowClone();
            clone.lastUpdated = cList.lastTime;
            return getPath(clone, point);

        }

        private void initState(AntBot antBot)
        {
            skladLayout = antBot.sklad.skladLayout;
            state = new Dictionary<int, Dictionary<int, squareState>>();
            for (int y = 0; y < skladLayout.Count; y++)
            {
                for (int x = 0; x < skladLayout[y].Count; x++)
                {
                    if (y == 0)
                        state.Add(x, new Dictionary<int, squareState>());
                    state[x].Add(y, new squareState());
                }
            }
        }

        private void initGraph(AntBot antBot)
        {
            AntBot ant = antBot.ShalowClone();
            graph = new FibonacciHeap<double, CommandList>();
            ant.commandList = new CommandList(ant);
            if (ant.isXDirection)
            {
                if (ant.commandList.metric < state[ant.xCord][ant.yCord].xMinMetric)
                {
                    state[ant.xCord][ant.yCord].xMinTime = ant.lastUpdated;
                    state[ant.xCord][ant.yCord].xMinMetric = ant.commandList.metric;
                    state[ant.xCord][ant.yCord].xCommans = ant.commandList.Clone();
                    graph.Push(state[ant.xCord][ant.yCord].xMinMetric, state[ant.xCord][ant.yCord].xCommans);
                }
            }
            else
            {
                if (ant.commandList.metric < state[ant.xCord][ant.yCord].yMinMetric)
                {
                    state[ant.xCord][ant.yCord].yMinTime = ant.lastUpdated;
                    state[ant.xCord][ant.yCord].yMinMetric = ant.commandList.metric;
                    state[ant.xCord][ant.yCord].yCommans = ant.commandList.Clone();
                    graph.Push(state[ant.xCord][ant.yCord].xMinMetric, state[ant.xCord][ant.yCord].yCommans);
                }
            }
        }



        private (bool isPathExist, CommandList cList) getPathToEscape(CommandList cList)
        {
            AntBot antBot = cList.antState.ShalowClone();
            antBot.lastUpdated = cList.lastTime;
            initState(antBot);
            initGraph(antBot);
            while (true)
            {
                NextStep(antBot);
                if (graph.Count() == 0)
                    return (false, null);

                var gr = graph.Peek();
                if (gr.Value.antState.CheckRoom(gr.Value.lastTime, TimeSpan.MaxValue))
                    if (skladLayout[gr.Value.antState.yCord][gr.Value.antState.xCord] == 1)
                        return (true, gr.Value);
            }
        }

        private (bool isPathExist, CommandList cList) getPath(AntBot antBot, (int x, int y, bool isXDirection) point)
        {
            CommandList cList;
            initState(antBot);
            initGraph(antBot);

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
            if (st.yMinMetric > commandList.metric)
            {
                var st1 = commandList.Clone();
                if (st1.AddCommand(new AntBotRotate(antBot), false))
                {
                    st1.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                    if (st1.metric < st.yMinMetric)
                    {
                        state[st1.antState.xCord][st1.antState.yCord].yMinTime = st1.lastTime;
                        state[st1.antState.xCord][st1.antState.yCord].yMinMetric = st1.metric;
                        state[st1.antState.xCord][st1.antState.yCord].yCommans = st1;
                        graph.Push(st1.metric, st1);
                    }
                }

            }
            if (st.xMinMetric > commandList.metric)
            {
                var st1 = commandList.Clone();
                if (st1.AddCommand(new AntBotRotate(antBot), false))
                {
                    st1.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                    if (st1.metric < st.xMinMetric)
                    {
                        state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                        state[st1.antState.xCord][st1.antState.yCord].xMinMetric = st1.metric;
                        state[st1.antState.xCord][st1.antState.yCord].xCommans = st1;
                        graph.Push(st1.metric, st1);
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


                var waitSt = commandList.Clone();
                if (dist == 0) 
                    if (!waitSt.antState.CheckRoom(waitSt.lastTime, waitSt.lastTime +
                    TimeSpan.FromSeconds(1.0 / waitSt.antBot.sklad.skladConfig.unitSpeed))) 
                    {                 
                        continue;
                    }
                waitSt.antState.setSpeedByDirection(dir);
                waitSt.antState.xCoordinate += waitSt.antState.xSpeed * (dist + 1) / antBot.sklad.skladConfig.unitSpeed;
                waitSt.antState.yCoordinate += waitSt.antState.ySpeed * (dist + 1) / antBot.sklad.skladConfig.unitSpeed;
                var near = antBot.sklad.squaresIsBusy.GetNearestReserve(waitSt.antState.xCord,
                    waitSt.antState.yCord, waitSt.lastTime + TimeSpan.FromSeconds((double)dist/ antBot.sklad.skladConfig.unitSpeed));
                
                if (near != TimeSpan.MaxValue)
                {
                    if (near > waitSt.lastTime)
                    {
                        waitSt = commandList.Clone();
                        if (dist != 0)
                        {
                            waitSt.AddCommand(new AntBootAccelerate(antBot, dir), false);
                            waitSt.AddCommand(new AntBotMove(antBot, dist), false);
                            waitSt.AddCommand(new AntBotStop(antBot, false), false);
                        }
                        if (waitSt.AddCommand(new AntBotWait(antBot, near - waitSt.lastTime), false))
                        {
                            waitSt.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                            if (waitSt.AddCommand(new AntBootAccelerate(antBot, dir), false))
                            {
                                waitSt.AddCommand(new AntBotMove(antBot, 1), false);
                                waitSt.AddCommand(new AntBotStop(antBot, false), false);
                                if (waitSt.antState.isXDirection)
                                {
                                    if (state[waitSt.antState.xCord][waitSt.antState.yCord].xMinMetric > waitSt.metric + 0.1)
                                    {
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].xMinTime = waitSt.lastTime;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].xMinMetric = waitSt.metric;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].xCommans = waitSt;
                                        graph.Push(waitSt.metric, waitSt);
                                    }
                                } else
                                {
                                    if (state[waitSt.antState.xCord][waitSt.antState.yCord].yMinMetric > waitSt.metric + 0.1)
                                    {
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].yMinTime = waitSt.lastTime;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].yMinMetric = waitSt.metric;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].yCommans = waitSt;
                                        graph.Push(waitSt.metric, waitSt);
                                    }
                                }

                            }

                        }

                    }
                }
                for (int dst = 1; dst <= dist; dst++)
                {
                    if (ant.isXDirection)
                    {
                        var st1 = commandList.Clone();
                        st1.AddCommand(new AntBootAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        
                        if (state[st1.antState.xCord][st1.antState.yCord].xMinMetric > st1.metric + 0.1)
                        {
                            state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].xMinMetric = st1.metric;
                            state[st1.antState.xCord][st1.antState.yCord].xCommans = st1;
                            graph.Push(st1.metric, st1);
                        }
                    }
                    else
                    {
                        var st1 = commandList.Clone();
                        st1.AddCommand(new AntBootAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        if (state[st1.antState.xCord][st1.antState.yCord].yMinMetric > st1.metric + 0.1)
                        { 
                            state[st1.antState.xCord][st1.antState.yCord].yMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].yMinMetric = st1.metric;
                            state[st1.antState.xCord][st1.antState.yCord].yCommans = st1;
                            graph.Push(st1.metric, st1);
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
            (AntBot bot, CommandList cList, TimeSpan minTime) minBotPath = (null, null, TimeSpan.MaxValue);
            antBot.sklad.charge.ForEach(charge =>
            {
                var gp = getPath(antBot, charge);
                if (gp.isPathExist)
                {
                    if (gp.cList.lastTime < minBotPath.minTime)
                    {
                        minBotPath.minTime = gp.cList.lastTime;
                        minBotPath.bot = antBot;
                        minBotPath.cList = gp.cList;
                    }
                }
            });
            if (minBotPath.bot == null)
                return;
            if (minBotPath.cList.AddCommand(new AntBotCharge(antBot), false))
            {
                var escapePath = getPathToEscape(minBotPath.cList);
                if (escapePath.isPathExist)
                {
                    minBotPath.cList.commands.ForEach(c => c.Ev.antBot = antBot); ;
                    applyPath(antBot, minBotPath.cList);
                    antBot.isFree = false;
                    AntBot ant = minBotPath.cList.antState.ShalowClone();
                    ant.commandList = new CommandList(ant);
                    ant.lastUpdated = minBotPath.cList.lastTime;
                    escapePath.cList.AddCommand(new AntBotStop(ant));
                    reservePath(ant, escapePath.cList);
                }
            }
        }
    }
}
