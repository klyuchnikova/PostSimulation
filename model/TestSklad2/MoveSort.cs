using ExtendedXmlSerializer.Core.Sources;
using SkladModel;
using System;
using System.Collections.Generic;
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
                List<AntBot> freeAnts = skladWrapper.GetFreeAnts();
                if (freeAnts.Count> 0)
                {
                    AntBot antBot = freeAnts[0];
                    if (antBot.charge < 200)
                    {
                        RunToChargePoint(antBot);
                        antBot.commandList.AddCommand(new AntBotCharge(antBot));
                        continue;
                    }

                    if (antBot.isLoaded) {
                        RunToUnloadPoint(antBot);
                        antBot.commandList.AddCommand(new AntBotUnload(antBot));
                    } else
                    {
                        RunToLoadPoint(antBot);
                        antBot.commandList.AddCommand(new AntBotLoad(antBot));
                    }
                }
            }
        }



        Dictionary<int, Dictionary<int, squareState>> state;
        public Dictionary<int, Dictionary<int, int>> skladLayout;
        public FibonacciHeap<TimeSpan, CommandList> graph;
        private void RunToPoint(AntBot antBot, (int x, int y, bool isXDirection) point)
        {
            antBot.CleanReservation();

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

            CommandList cList;
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
            }

            for (int i = 0;i<cList.commands.Count;i++)
            {
                antBot.commandList.AddCommand(cList.commands[i].Ev);
            }
        }

        void NextStep(AntBot antBot)
        {
            var gf = graph.Pop();
            var commandList = gf.Value;
            var ant = commandList.antState;
            var st = state[ant.xCord][ant.yCord];
            if (st.xMinTime < st.yMinTime)
            {
                var st1 = st.xCommans.Clone();
                st1.AddCommand(new AntBotRotate(antBot), false);
                if (st1.lastTime < st.yMinTime)
                {
                    state[st1.antState.xCord][st1.antState.yCord].yMinTime = st1.lastTime;
                    state[st1.antState.xCord][st1.antState.yCord].yCommans = st1;
                    graph.Push(st1.lastTime, st1);
                }
            }
            if (st.xMinTime > st.yMinTime)
            {
                var st1 = st.yCommans.Clone();
                st1.AddCommand(new AntBotRotate(antBot), false);
                if (st1.lastTime < st.xMinTime)
                {
                    state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                    state[st1.antState.xCord][st1.antState.yCord].xCommans = st1;
                    graph.Push(st1.lastTime, st1);
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
                        var st1 = st.xCommans.Clone();
                        st1.AddCommand(new AntBootAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        if (state[st1.antState.xCord][st1.antState.yCord].xMinTime > st1.lastTime + TimeSpan.FromSeconds(0.0001))
                        {
                            state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].xCommans = st1;
                            graph.Push(st1.lastTime, st1);
                            for (int t = 1; t<10; t++)
                            {
                                var st2 = st1.Clone();
                                st2.AddCommand(new AntBotWait(antBot, TimeSpan.FromSeconds(i * 1.0 / antBot.sklad.skladConfig.unitSpeed)), false);
                                st2.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                                graph.Push(st2.lastTime, st2);
                            }
                        }
                    }
                    else
                    {
                        var st1 = st.yCommans.Clone();
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
                                st2.AddCommand(new AntBotWait(antBot, TimeSpan.FromSeconds(i * 1.0 / antBot.sklad.skladConfig.unitSpeed)), false);
                                st2.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                                graph.Push(st2.lastTime, st2);
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


        void RunToLoadPoint(AntBot antBot)
        {
            RunToPoint(antBot, antBot.sklad.source[0]);
        }


        void RunToUnloadPoint(AntBot antBot)
        {
            Random rnd = new Random();
            int next = rnd.Next(antBot.sklad.target.Count);
            RunToPoint(antBot, antBot.sklad.target[next]);
        }

        private void RunToChargePoint(AntBot antBot)
        {
            RunToPoint(antBot, antBot.sklad.charge[0]);
        }
    }
}
