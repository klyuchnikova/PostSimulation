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
using System.Collections.Generic;

namespace ControlModel
{
    struct Source
    {
        public int x;
        public int y;
        public bool isXDirection;
    }
    class squareState
    {
        public TimeSpan xMinTime = TimeSpan.MaxValue;
        public TimeSpan yMinTime = TimeSpan.MaxValue;
        public double xMinMetric = double.MaxValue;
        public double yMinMetric = double.MaxValue;
        public CommandList xCommands;
        public CommandList yCommands;
    }
    public class MoveSort
    {

        SkladWrapper skladWrapper;
        TimeSpan maxTime = TimeSpan.MaxValue;
        int recalculate_period = 9;
        int horizon = 10;
        double optimalChargeValue;

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
            int numberFreeAnt = 0;
            long number_of_recalculations = 0;

            // first make commands to create Sklad and Logger
            while (!skladWrapper.isEventCountEmpty() && skladWrapper.Next())
            {
                if (timeProgress < skladWrapper.updatedTime)
                {
                    Console.WriteLine($"{skladWrapper.updatedTime}  {DateTime.Now - now}  {skladWrapper.GetSklad().deliveryCount}");
                    timeProgress += TimeSpan.FromMinutes(1);
                    break;
                }
            }

            optimalChargeValue = skladWrapper.GetSklad().skladConfig.unitChargeValue * 0.8;

            while (skladWrapper.Next())
            {
                if (timeProgress < skladWrapper.updatedTime)
                {
                    Console.WriteLine($"{skladWrapper.updatedTime}  {DateTime.Now - now}  {skladWrapper.GetSklad().deliveryCount}");
                    timeProgress += TimeSpan.FromMinutes(1);
                }
                if (skladWrapper.updatedTime > maxModelTime)
                {
                    break;
                }
                else if (skladWrapper.GetSklad().deliveryCount >= 5000)
                {
                    Console.WriteLine($"Delivery time: {skladWrapper.updatedTime.TotalSeconds}");
                    break;
                }
                else if (skladWrapper.GetAllAnts().Any(x => x.state == AntBotState.UnCharged))
                {
                    Console.WriteLine("UNCHARGED"); // --! this check should be moved
                    break;
                }
                else if (numberFreeAnt == skladWrapper.GetFreeAnts().Count)
                {
                    continue;
                }

                MakeCommands();
                numberFreeAnt = skladWrapper.GetFreeAnts().Count;
            }
        }

        private void MakeCommands()
        {
            skladWrapper.GetFreeAnts().ForEach(ant =>
            {
                if (ant.isLoaded)
                {
                    //RunToTarget(ant); --!
                }
                else if (ant.charge < optimalChargeValue)
                {
                    RunToChargePoint(ant);
                }
                else
                {
                    RunToLoadPoint(ant);
                }
            });
        }

        private void TryRunToFreePoint(List<AntBot> antBots)
        {
            foreach (var ant in antBots)
            {
                if (ant.reserved.Count > 0)
                {
                    if (ant.reserved.Any(r => r.x == ant.xCord && r.y == ant.yCord && (r.from - ant.lastUpdated).TotalSeconds < 0.0001 && r.to == TimeSpan.MaxValue))
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
                    foreach (var yKey in state[xKey].Keys)
                    {
                        if (state[xKey][yKey].xMinMetric < min)
                        {
                            if (ant.CheckRoom(xKey, yKey, state[xKey][yKey].xMinTime, TimeSpan.MaxValue))
                            {
                                min = state[xKey][yKey].xMinMetric;
                                minTime = state[xKey][yKey].xMinTime;
                                minPath = state[xKey][yKey].xCommands;
                            } else
                            {
                                var mr = ant.sklad.squaresIsBusy.GetPosibleReserve(xKey, yKey, state[xKey][yKey].xMinTime);
                                if (mr > maxReserve)
                                {
                                    maxReserve = mr;
                                    minPosiblePath = state[xKey][yKey].xCommands;
                                }
                            }
                        }
                        if (state[xKey][yKey].yMinMetric < min)
                        {
                            if (ant.CheckRoom(xKey, yKey, state[xKey][yKey].yMinTime, TimeSpan.MaxValue))
                            {
                                min = state[xKey][yKey].yMinMetric;
                                minTime = state[xKey][yKey].yMinTime;
                                minPath = state[xKey][yKey].yCommands;
                            }
                            else
                            {
                                var mr = ant.sklad.squaresIsBusy.GetPosibleReserve(xKey, yKey, state[xKey][yKey].yMinTime);
                                if (mr > maxReserve)
                                {
                                    maxReserve = mr;
                                    minPosiblePath = state[xKey][yKey].yCommands;
                                }
                            }
                        }
                    }
                }
                if (minTime != TimeSpan.MaxValue)
                {
                    applyPath(ant, minPath);
                    ant.commandList.antState.ReserveRoom(minTime, TimeSpan.MaxValue);
                    ant.isFree = false;
                } else
                {
                    if (maxReserve >= ant.lastUpdated + TimeSpan.FromSeconds(1.0 / 3.0))
                    {
                        applyPath(ant, minPosiblePath);
                        ant.commandList.antState.ReserveRoom(ant.commandList.lastTime, maxReserve);
                    }
                    else
                        throw new ImposibleFoundWay();
                }
            }
        }
        void RunToLoadPoint(AntBot freeAnt)
        {
            // instead of running to the queue we'll try to either stand in a queue or move to a free point to get out of the way
            (AntBot bot, CommandList cList, TimeSpan minTime) minBotPath = (null, null, TimeSpan.MaxValue);

            if (freeAnt.commandList.commands.Count > 0)
            {
                freeAnt.CleanReservation();
                // freeAnt.ClearCommands(); --!
            }

            // finding the closest source to assighn to
            List<(int x, int y, bool isXDirection)> sources = freeAnt.sklad.source;
            double closest_time = Double.MaxValue;

            (int x, int y, bool isXDirection) closest_source = (0, 0, true);
            foreach (var source in sources)
            {
                // --! will need to count number assigned to evade crowds + number of packages at the moment to estimate the efficiency
                double estimated_time = freeAnt.EstimateTimeToMoveFunc((source.x, source.y, source.isXDirection));
                if (estimated_time < closest_time)
                {
                    closest_time = estimated_time;
                    closest_source = source;
                }
            };
            var path_to_source = getPath(freeAnt, closest_source);
            applyPath(freeAnt, path_to_source.cList);
        }

        public IEnumerable<CommandList> GetPossibleCommands(AntBot antBot)
        {
            // attempt to
            // a) move (?) in the same direction b) stop (?) + move in different direction c) stop(?) + rotate + move d) stop (?) + wait
            // every action is like stop (?)  + wait (?) + (Accelerate + move (on int) + stop) + rotate (true or false)

            if (antBot.commandList.commands[antBot.commandList.commands.Count-1].GetType() == typeof(AntBotWait))
            {
                // also we could try rotate (meaningless if we rotated before or waited to move in one of the directions)
                var for_rotate = antBot.commandList.Clone();
                if (for_rotate.AddCommand(new AntBotRotate(antBot), false) &&
                    for_rotate.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false))
                {
                    yield return for_rotate;
                }
            }

            List<Direction> available_directions = new List<Direction>();
            if (antBot.isXDirection)
            {
                available_directions = new List<Direction>() { Direction.Left, Direction.Right };
            }
            else
            {
                available_directions = new List<Direction>() { Direction.Up, Direction.Down };
            }

            TimeSpan nearest_reserve_to_move = TimeSpan.MinValue;
            foreach (Direction direction in available_directions)
            {
                int max_distance = skladWrapper.getFreePath(antBot, direction, antBot.lastUpdated);
                if (max_distance == 0)
                {
                    // then let's wait
                    var commands_copy = antBot.commandList.Clone();
                    commands_copy.antState.setSpeedByDirection(direction);
                    var near = antBot.sklad.squaresIsBusy.GetNearestReserve(
                        (int)Math.Round(antBot.xCoordinate + commands_copy.antState.xSpeed / antBot.sklad.skladConfig.unitSpeed),
                        (int)Math.Round(antBot.yCoordinate + commands_copy.antState.ySpeed / antBot.sklad.skladConfig.unitSpeed),
                        commands_copy.lastTime);
                    // first we add the scenario where we simply wait before the nearest event
                    if (near > commands_copy.lastTime &&
                        near != TimeSpan.MaxValue &&
                        commands_copy.AddCommand(new AntBotWait(antBot, near - commands_copy.lastTime), false))
                    {
                        yield return commands_copy;
                    }
                }
                for (int move_on_dist = 1; move_on_dist < max_distance; ++move_on_dist)
                {
                    var commands_copy = antBot.commandList.Clone();
                    if (commands_copy.AddCommand(new AntBotAccelerate(antBot, direction), false) &&
                        commands_copy.AddCommand(new AntBotMove(antBot, move_on_dist), false) &&
                        commands_copy.AddCommand(new AntBotStop(antBot, false), false))
                    {
                        yield return commands_copy;
                        var for_rotate = commands_copy.Clone();
                        if (for_rotate.AddCommand(new AntBotRotate(antBot), false) &&
                            for_rotate.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false))
                        {
                            yield return for_rotate;
                        }
                    }
                    else
                    {
                        // first moment when we couldn't move on dist probably means that we're stuck
                        // of course it's not always so, but it's a simplification
                        break;
                    }
                }
            }
        }

        private double EstimateDistance()
        {
        }
        private void WHCAStarBuildPath(AntBot antBot, (int x, int y, bool isXDirection) goal)
        {
            var frontier = new PriorityQueue<CommandList, double>();
            frontier.Enqueue(new CommandList(antBot), 0);
            var came_from = new Dictionary<(int x, int y, bool isXDirection), CommandList>();
            var cost_so_far = new Dictionary<(int x, int y, bool isXDirection), TimeSpan>();

            // in fact here it would be more logical to keep only (x, y, isXDirection) with most optimal time and velocity... maybe
            came_from.Add(antBot.GetCurrentPoint(), new CommandList()); // None is not an option in C# so...
            cost_so_far.Add(antBot.GetCurrentPoint(), new TimeSpan(0));

            // this very time until we try to consider reservations. Then we don't care (and don't reserve)
            TimeSpan cooperate_until = antBot.waitTime + WINDOW_COOPERATE;
            CommandList current;
            while (frontier.Count > 0)
            {
                current = frontier.Dequeue();
                if (current.antState.xCord == goal.x && current.antState.yCord == goal.y && current.antState.isXDirection == goal.isXDirection)
                {
                    break;
                }
                if (current.lastTime > cooperate_until)
                {
                    // then we become dumb and use any algorythm to quickly get to the target.
                    // (there's even a case of dictionary with targets since there aren't many)
                    // but here we'll use A* once again
                    // also we won't store these commands anywhere nor reserve them. All we need is an approximate cost
                    // of course we could not only use ML for this matter but also Gene-based algorithms, yet we won't
                    // --!
                    // finally WE WILL HAVE TO PUT x, y, dir as final in antState!!!
                    // ALSO WE HAVE TO PUT cost_so_far[goal] TO THIS PARTICULAR lastTime
                }

                foreach (var commandList in GetPossibleCommands(current.antState)) // (TimeSpan Key, AntBotAbstractEvent Ev)
                {
                    TimeSpan last_time = commandList.lastTime;
                    (int x, int y, bool isXDirection) finish_point = commandList.antState.GetCurrentPoint();

                    if (!cost_so_far.ContainsKey(finish_point) || last_time < cost_so_far[finish_point])
                    {
                        cost_so_far[finish_point] = last_time;
                        double priority = last_time.TotalSeconds + antBot.EstimateTimeToMoveFunc(goal, finish_point);
                        frontier.Enqueue(commandList, priority);
                        came_from[finish_point] = commandList;
                    }
                }
            }
            // 1. first case : we didn't reach the goal => stand still (in the cycle we'll have to reassighn commands)
            // 2. second case : reached the goal within window => we have to count + reserve escape path
            // 3. third case : path found yet goal was reached out of reservation => simply assighn commands
            if (current.antState.xCord == goal.x && current.antState.yCord == goal.y && current.antState.direction == goal.isXDirection)
            {
                if (cost_so_far[finish_point] > cooperate_until)
                {
                    // 3
                    applyPath(antBot, current);
                }
                else
                {
                    // 2
                    // --!
                }
            }
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
                    state[ant.xCord][ant.yCord].xCommands = ant.commandList.Clone();
                    graph.Push(state[ant.xCord][ant.yCord].xMinMetric, state[ant.xCord][ant.yCord].xCommands);
                }
            }
            else
            {
                if (ant.commandList.metric < state[ant.xCord][ant.yCord].yMinMetric)
                {
                    state[ant.xCord][ant.yCord].yMinTime = ant.lastUpdated;
                    state[ant.xCord][ant.yCord].yMinMetric = ant.commandList.metric;
                    state[ant.xCord][ant.yCord].yCommands = ant.commandList.Clone();
                    graph.Push(state[ant.xCord][ant.yCord].xMinMetric, state[ant.xCord][ant.yCord].yCommands);
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
                        cList = state[point.x][point.y].xCommands;
                        break;
                    }
                }
                else
                {
                    if (state[point.x][point.y].yMinTime != TimeSpan.MaxValue)
                    {
                        cList = state[point.x][point.y].yCommands;
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
            var commandList = graph.Pop().Value;
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
                        state[st1.antState.xCord][st1.antState.yCord].yCommands = st1;
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
                        state[st1.antState.xCord][st1.antState.yCord].xCommands = st1;
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
                            waitSt.AddCommand(new AntBotAccelerate(antBot, dir), false);
                            waitSt.AddCommand(new AntBotMove(antBot, dist), false);
                            waitSt.AddCommand(new AntBotStop(antBot, false), false);
                        }
                        if (waitSt.AddCommand(new AntBotWait(antBot, near - waitSt.lastTime), false))
                        {
                            waitSt.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                            if (waitSt.AddCommand(new AntBotAccelerate(antBot, dir), false))
                            {
                                waitSt.AddCommand(new AntBotMove(antBot, 1), false);
                                waitSt.AddCommand(new AntBotStop(antBot, false), false);
                                if (waitSt.antState.isXDirection)
                                {
                                    if (state[waitSt.antState.xCord][waitSt.antState.yCord].xMinMetric > waitSt.metric + 0.1)
                                    {
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].xMinTime = waitSt.lastTime;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].xMinMetric = waitSt.metric;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].xCommands = waitSt;
                                        graph.Push(waitSt.metric, waitSt);
                                    }
                                } else
                                {
                                    if (state[waitSt.antState.xCord][waitSt.antState.yCord].yMinMetric > waitSt.metric + 0.1)
                                    {
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].yMinTime = waitSt.lastTime;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].yMinMetric = waitSt.metric;
                                        state[waitSt.antState.xCord][waitSt.antState.yCord].yCommands = waitSt;
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
                        st1.AddCommand(new AntBotAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        
                        if (state[st1.antState.xCord][st1.antState.yCord].xMinMetric > st1.metric + 0.1)
                        {
                            state[st1.antState.xCord][st1.antState.yCord].xMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].xMinMetric = st1.metric;
                            state[st1.antState.xCord][st1.antState.yCord].xCommands = st1;
                            graph.Push(st1.metric, st1);
                        }
                    }
                    else
                    {
                        var st1 = commandList.Clone();
                        st1.AddCommand(new AntBotAccelerate(antBot, dir), false);
                        st1.AddCommand(new AntBotMove(antBot, dst), false);
                        st1.AddCommand(new AntBotStop(antBot, false), false);
                        if (state[st1.antState.xCord][st1.antState.yCord].yMinMetric > st1.metric + 0.1)
                        { 
                            state[st1.antState.xCord][st1.antState.yCord].yMinTime = st1.lastTime;
                            state[st1.antState.xCord][st1.antState.yCord].yMinMetric = st1.metric;
                            state[st1.antState.xCord][st1.antState.yCord].yCommands = st1;
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
                // --!
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
