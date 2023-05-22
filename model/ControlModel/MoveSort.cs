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
    public static class ArrayExtensions
    {
        public static void Fill(this Array array, object value)
        {
            var indicies = new int[array.Rank];

            Fill(array, 0, indicies, value);
        }

        public static void Fill(Array array, int dimension, int[] indicies, object value)
        {
            if (dimension < array.Rank)
            {
                for (int i = array.GetLowerBound(dimension); i <= array.GetUpperBound(dimension); i++)
                {
                    indicies[dimension] = i;
                    Fill(array, dimension + 1, indicies, value);
                }
            }
            else
                array.SetValue(value, indicies);
        }
    }
    public class MoveSort
    {
        Sklad sklad;
        int skladHeight;
        int skladWidth;

        SkladWrapper skladWrapper;
        TimeSpan maxTime = TimeSpan.MaxValue;
        TimeSpan WINDOW_COOPERATE = TimeSpan.FromSeconds(10);
        TimeSpan WINDOW_RECALCULATE = TimeSpan.FromSeconds(6);
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
            
            // first make commands to create Sklad and Logger
            while (skladWrapper.Next())
            {
                if (timeProgress < skladWrapper.updatedTime)
                {
                    Console.WriteLine($"{skladWrapper.updatedTime}  {DateTime.Now - now}  {skladWrapper.GetSklad().deliveryCount}");
                    timeProgress += TimeSpan.FromMinutes(1);
                    break;
                }

                if (skladWrapper.updatedTime > maxModelTime)
                {
                    break;
                }
            }

            sklad = skladWrapper.GetSklad();
            skladHeight = sklad.skladLayout.Count; 
            skladWidth = sklad.skladLayout[0].Count;
            InitoptimalPathEstimation();
            // PrintEstimationMap();
            optimalChargeValue = sklad.skladConfig.unitChargeValue * 0.8;

            do
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

                MakeCommands();
            } while (skladWrapper.Next());
        }

        private void MakeCommands()
        {
            skladWrapper.GetAvailableAnts().ForEach(ant =>
            {
                ant.time_before_recount = skladWrapper.updatedTime + WINDOW_RECALCULATE;

                if (ant.isLoaded)
                {
                    RunToDrop(ant);
                }
                else if (ant.hasNoTarget() &&
                         ant.charge < optimalChargeValue)
                {
                    RunToChargePoint(ant);
                }
                else { RunToLoadPoint(ant); }

                // each of the previous commands
                // 1) unreserves path
                // 2) tries to build a path without conflicts and reserve it
                // 3) if it failed empties commands anyway

                if (ant.isFree && IsOnTarget(ant))
                {
                    TryRunToFreePoint(ant);
                }
            });
        }

        private bool IsOnTarget(AntBot antBot)
        {
            foreach (var point in antBot.sklad.source)
            {
                if (point.x == antBot.xCord && point.y == antBot.yCord)
                {
                    return true;
                }
            }

            foreach (var point in antBot.sklad.charge)
            {
                if (point.x == antBot.xCord && point.y == antBot.yCord)
                {
                    return true;
                }
            }

            foreach (var point in antBot.sklad.target)
            {
                if (point.x == antBot.xCord && point.y == antBot.yCord)
                {
                    return true;
                }
            }

            return false;
        }

        private void FreeCommandsWithExtendedOccupation(AntBot antbot)
        {
            // ant Bot is ONE HUNDRED PERCENT on the edge of starting a new task
            antbot.CleanReservation();
            antbot.commandList = new CommandList(antbot);
            antbot.isFree = true;
        }

        private void RunToDrop(AntBot antBot)
        {
            // always has a destination (target) already
            FreeCommandsWithExtendedOccupation(antBot);
            var actions_on_drop = new List<AntBotAbstractEvent>() {
                new AntBotUnload(antBot, (antBot.targetXCoordinate, antBot.targetYCoordinate, antBot.targetDirection)),
                new AntBotWait(antBot, TimeSpan.Zero)};
            var path = WHCAStarBuildPath(antBot, (antBot.targetXCoordinate, antBot.targetYCoordinate, antBot.targetDirection), actions_on_drop);
            if (path.doesExist)
            {
                addPath(antBot, path.path);
            }
        }

        private void RunToChargePoint(AntBot antBot)
        {
            // uloaded, maybe is moving towards a queue already
            // still, we'll simply reassighn target, this doesn't matter much
            // right now the algorythm is not right since in case someone is already moving or standing on a source
            // we will get stuck. So we'll need to change this part...
            var actions_on_charge = new List<AntBotAbstractEvent>() {
                new AntBotCharge(antBot),
                new AntBotWait(antBot, TimeSpan.Zero)};

            // finding the closest source to assighn to
            var actions_on_load = new List<AntBotAbstractEvent>() { new AntBotLoad(antBot) };

            var targets = new List<((int x, int y, bool isXDirection) source, double estimated_time)>();
            foreach (var target_point in antBot.sklad.charge)
            {
                targets.Add((target_point, EstimateTimeToMoveFunc(target_point, antBot.GetCurrentPoint())));
            };

            targets.Sort((first, second) =>
            {
                return first.estimated_time.CompareTo(second.estimated_time);
            });

            FreeCommandsWithExtendedOccupation(antBot);
            foreach (var target in targets)
            {
                var closest_source = target.source;
                var path = WHCAStarBuildPath(antBot, closest_source, actions_on_load);
                if (path.doesExist)
                {
                    addPath(antBot, path.path);
                    antBot.targetXCoordinate = closest_source.x;
                    antBot.targetYCoordinate = closest_source.y;
                    antBot.targetDirection = closest_source.isXDirection;
                    ++antBot.sklad.skladTargeted[antBot.targetYCoordinate][antBot.targetXCoordinate];
                    break;
                }
            }
        }
        private void RunToLoadPoint(AntBot antBot)
        {
            // run to closest source
            // after receiving a package empties the command List and
            // resets reservation immidiately cause we don't want to stand although since we're free
            // I suppose we could still reconsider.
            // Also sets a destination (drop)
            // (all of it happens in AntBotUnload event)

            // finding the closest source to assighn to
            var actions_on_load = new List<AntBotAbstractEvent>() {new AntBotLoad(antBot)};

            var targets = new List<((int x, int y, bool isXDirection) source, double estimated_time)>();
            foreach (var target_point in antBot.sklad.source)
            {
                targets.Add((target_point, EstimateTimeToMoveFunc(target_point, antBot.GetCurrentPoint())));
            };

            targets.Sort((first, second) =>
            {
                return first.estimated_time.CompareTo(second.estimated_time);
            });

            FreeCommandsWithExtendedOccupation(antBot);
            foreach (var target in targets)
            {
                var closest_source = target.source;
                var path = WHCAStarBuildPath(antBot, closest_source, actions_on_load);
                if (path.doesExist)
                {
                    addPath(antBot, path.path);
                    antBot.targetXCoordinate = closest_source.x;
                    antBot.targetYCoordinate = closest_source.y;
                    antBot.targetDirection = closest_source.isXDirection;
                    ++antBot.sklad.skladTargeted[antBot.targetYCoordinate][antBot.targetXCoordinate];
                    break;
                }
            }
        }

        private void TryRunToFreePoint(AntBot antBot)
        {
            // doesn't set target - only comands
            // basically it will ony be called in case we're close to the target but it's occupied too firmly
            TimeSpan cooperate_until = skladWrapper.updatedTime + WINDOW_COOPERATE;
            var path = WHCAStarBuildEscapePath(antBot, cooperate_until);
            if (path.doesExist)
            {
                addPath(antBot, path.path);
            }
        }

        public IEnumerable<CommandList> GetPossibleCommands(CommandList commandList)
        {
            // attempt to
            // a) move (?) in the same direction b) stop (?) + move in different direction c) stop(?) + rotate + move d) stop (?) + wait
            // every action is like stop (?)  + wait (?) + (Accelerate + move (on int) + stop) + rotate (true or false)

            AntBot antBot = commandList.antState;

            if (commandList.commands.Count == 0 || 
                commandList.commands[commandList.commands.Count-1].Ev.GetType() != typeof(AntBotWait))
            {
                // also we could try rotate (meaningless if we rotated before or waited to move in one of the directions)
                var for_rotate = commandList.Clone();
                if (for_rotate.AddCommand(new AntBotRotate(commandList.antState), false) &&
                    for_rotate.AddCommand(new AntBotWait(commandList.antState, TimeSpan.Zero), false))
                {
                    yield return for_rotate;
                }
            }

            List<Direction> available_directions = new List<Direction>();
            if (commandList.antState.isXDirection)
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
                    var commands_copy = commandList.Clone();
                    commands_copy.antState.setSpeedByDirection(direction);
                    var near = antBot.sklad.squaresIsBusy.GetNearestReserve(
                        (int)Math.Round(antBot.xCoordinate + commands_copy.antState.xSpeed / antBot.sklad.skladConfig.unitSpeed),
                        (int)Math.Round(antBot.yCoordinate + commands_copy.antState.ySpeed / antBot.sklad.skladConfig.unitSpeed),
                        commands_copy.lastTime + TimeSpan.FromSeconds(0.00001));
                    // first we add the scenario where we simply wait before the nearest event
                    if (near > commands_copy.lastTime &&
                        near != TimeSpan.MaxValue &&
                        commands_copy.AddCommand(new AntBotWait(antBot, near - commands_copy.lastTime), false))
                    {
                        yield return commands_copy;
                    }
                }
                if (commandList.commands.Count == 0 ||
                    commandList.commands[commandList.commands.Count - 1].Ev.GetType() != typeof(AntBotStop))
                {
                    int move_on_dist = 1;
                    while(true)
                    {
                        var commands_copy = commandList.Clone();
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
                        ++move_on_dist;
                    }
                }
            }
        }

        private class VerticeComparator<T> : IComparer<T>
        {
            private readonly Comparison<T> comparison;
            public VerticeComparator(Comparison<T> comparison)
            {
                this.comparison = comparison;
            }
            public int Compare(T x, T y)
            {
                return comparison(x, y);
            }
        }

        private (bool doesExist, CommandList path) WHCAStarBuildPath(AntBot antBot, (int x, int y, bool isXDirection) goal, List<AntBotAbstractEvent> actions_on_goal)
        {
            // this very time until we try to consider reservations. Then we don't care (and don't reserve)
            TimeSpan cooperate_until = skladWrapper.updatedTime + WINDOW_COOPERATE;
            return WHCAStarBuildPath(antBot, goal, actions_on_goal, cooperate_until);
        }
        private (bool doesExist, CommandList path) WHCAStarBuildPath(
            AntBot antBot, 
            (int x, int y, bool isXDirection) goal, 
            List<AntBotAbstractEvent> actions_on_goal, 
            TimeSpan cooperate_until)
        {
            // first try to build commands to reach goal within time window.
            // then add actions_on_goal if goal is reachable within window
            // if after all there's still time - try to build escape path

            if (antBot.lastUpdated >= cooperate_until)
            {
                return (true, new CommandList(antBot));
            }

            var frontier = new SortedSet<(CommandList cList, double priority)>(
                new VerticeComparator<(CommandList cList, double priority)>(
                    (a, b) => (a.cList.uid == b.cList.uid ? 0 : (a.priority > b.priority ? 1 : -1))
                )
            );

            if (antBot.xSpeed != 0 || antBot.ySpeed != 0)
            {
                // --! we do that using the fact that right now the stop and acceleration commands are instant
                antBot.commandList.AddCommand(new AntBotStop(antBot, false), true);
            }

            CommandList start_command_list = new CommandList(antBot);
            var came_from = new Dictionary<(int x, int y, bool isXDirection), CommandList>();
            var cost_so_far = new Dictionary<(int x, int y, bool isXDirection), TimeSpan>();

            // in fact here it would be more logical to keep only (x, y, isXDirection) with most optimal time and velocity... maybe
            frontier.Add((start_command_list, 0));
            came_from.Add(antBot.GetCurrentPoint(), start_command_list);
            cost_so_far.Add(antBot.GetCurrentPoint(), new TimeSpan(0));

            CommandList best_unreachable = null;
            double best_unreachable_priority = double.MaxValue;

            // antBot.sklad.squaresIsBusy.PrintReserves(sklad.skladLayout);
            while (frontier.Count > 0)
            {
                var min = frontier.Min;
                CommandList current = frontier.Min.cList;
                frontier.Remove(min);

                // Console.WriteLine(String.Format("{0:0} {1:0} {2,1} {3:0.00}", current.antState.xCord, current.antState.yCord, current.antState.isXDirection, current.lastTime.TotalSeconds));
                if (current.antState.xCord == goal.x && current.antState.yCord == goal.y && current.antState.isXDirection == goal.isXDirection)
                {
                    break;
                }
                if (current.lastTime > cooperate_until || frontier.Count > 1000)
                {
                    // then we become dumb and use any algorythm to quickly get to the target.
                    // (there's even a case of dictionary with targets since there aren't many)
                    // but here we'll use A* once again
                    // also we won't store these commands anywhere nor reserve them. All we need is an approximate cost
                    
                    (int x, int y, bool isXDirection) finish_point = current.antState.GetCurrentPoint();
                    double priority = current.lastTime.TotalSeconds + EstimateTimeToMoveFunc(goal, finish_point);
                    if (priority < best_unreachable_priority)
                    {
                        best_unreachable_priority = priority;
                        best_unreachable = current;
                    }

                    if (!cost_so_far.ContainsKey(finish_point) || current.lastTime < cost_so_far[finish_point])
                    {
                        cost_so_far[finish_point] = current.lastTime;
                        came_from[finish_point] = current;
                    }
                }
                else
                {
                    int number_commands = 0;
                    foreach (var commandList in GetPossibleCommands(current)) // (TimeSpan Key, AntBotAbstractEvent Ev)
                    {
                        ++number_commands;
                        TimeSpan last_time = commandList.lastTime;
                        (int x, int y, bool isXDirection) finish_point = commandList.antState.GetCurrentPoint();

                        if (!cost_so_far.ContainsKey(finish_point) || last_time < cost_so_far[finish_point] )
                        {
                            cost_so_far[finish_point] = last_time;
                            double priority = last_time.TotalSeconds + EstimateTimeToMoveFunc(goal, finish_point);
                            frontier.Add((commandList, priority));
                            came_from[finish_point] = commandList;
                        }
                    }
                }

                // Console.Write(String.Format("Number neighbours investigated {0}\n", number_commands));
                // PrintGraphState(cost_so_far);
                // foreach (var el in frontier) {
                //    Console.Write(String.Format("{0:0.00} ", el.priority));
                // }
                // Console.WriteLine();
            }
            // 1. first case : we didn't reach the goal => stand still (in the cycle we'll have to reassighn commands)
            // 2. second case : reached the goal within window => we have to count + reserve escape path
            // 3. third case : path found yet goal was reached out of reservation => simply assighn commands
            // PrintGraphState(cost_so_far);
            if (!came_from.ContainsKey(goal))
            {
                if (best_unreachable != null)
                {
                    return (true, best_unreachable);
                }
                return (false, null);
            }

            CommandList best_commands = came_from[goal];
            if (best_commands.antState.xCord == goal.x && 
                best_commands.antState.yCord == goal.y &&
                best_commands.antState.isXDirection == goal.isXDirection)
            {
                foreach (var act_on_goal in actions_on_goal) {
                    best_commands.AddCommand(act_on_goal, false);
                }

                if (best_commands.lastTime > cooperate_until)
                {
                    // 3
                    return (true, best_commands);
                } else {
                    // 2
                    var path_to_free = WHCAStarBuildEscapePath(best_commands.antState, TimeSpan.FromSeconds(
                        Math.Max(cooperate_until.TotalSeconds, best_commands.lastTime.TotalSeconds+3)));
                    if (path_to_free.doesExist)
                    {
                        foreach (var t_command in path_to_free.path.commands)
                        {
                            best_commands.AddCommand(t_command.Ev, false);
                        }
                        return (true, best_commands);
                    }
                }
            }
            return (false, null);
        }

        private (bool doesExist, CommandList path) WHCAStarBuildEscapePath(AntBot antBot, TimeSpan cooperate_until)
        {
            // AntBot antBot = cList.antState.ShalowClone();
            // antBot.lastUpdated = cList.lastTime;
            // var gr = graph.Peek();
            // if (gr.Value.antState.CheckRoom(gr.Value.lastTime, TimeSpan.MaxValue))
            (int x, int y, bool isXDirection) center = (skladWidth / 2, skladHeight / 2, true);
            if (antBot.lastUpdated >= cooperate_until)
            {
                return (true, new CommandList(antBot));
            }

            return WHCAStarBuildPath(antBot, center, new List<AntBotAbstractEvent>(), cooperate_until);
        }

        public FibonacciHeap<double, CommandList> graph;

        private void applyPath(AntBot antBot, CommandList cList)
        {
            antBot.CleanReservation();
            addPath(antBot, cList);
        }

        private void addPath(AntBot antBot, CommandList cList)
        {
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

        public IEnumerable<(int x, int y, bool isXDirection, TimeSpan action_time)> GetNeighbours((int x, int y, bool isXDirection) point)
        {
            if (point.isXDirection)
            {
                if (point.x > 0 && sklad.skladLayout[point.y][point.x - 1] != 0)
                {
                    yield return (point.x - 1, point.y, point.isXDirection, TimeSpan.FromSeconds(1.0 / sklad.skladConfig.unitSpeed));
                }
                if (point.x < skladWidth-1 && sklad.skladLayout[point.y][point.x + 1] != 0)
                {
                    yield return (point.x + 1, point.y, point.isXDirection, TimeSpan.FromSeconds(1.0 / sklad.skladConfig.unitSpeed));
                }
            } else
            {
                if (point.y > 0 && sklad.skladLayout[point.y-1][point.x] != 0)
                {
                    yield return (point.x, point.y-1, point.isXDirection, TimeSpan.FromSeconds(1.0 / sklad.skladConfig.unitSpeed));
                }
                if (point.y < skladHeight - 1 && sklad.skladLayout[point.y+1][point.x] != 0)
                {
                    yield return (point.x, point.y+1, point.isXDirection, TimeSpan.FromSeconds(1.0 / sklad.skladConfig.unitSpeed));
                }
            }
            yield return (point.x, point.y, !point.isXDirection, TimeSpan.FromSeconds(sklad.skladConfig.unitRotateTime));
        }

        private void CountPathesFromPoint((int x, int y, bool isXDirection) start_point, TimeSpan[,,] map)
        {
            Queue watched_points = new Queue();
            watched_points.Enqueue(start_point);
            map[start_point.y, start_point.x, Convert.ToInt32(start_point.isXDirection)] = TimeSpan.Zero;
            while(watched_points.Count != 0)
            {
                var point = ((int x, int y, bool isXDirection)) watched_points.Dequeue();
                TimeSpan current_best = map[point.y, point.x, Convert.ToInt32(point.isXDirection)];
                foreach (var neighbour in GetNeighbours(point))
                {
                    var next_best = current_best + neighbour.action_time;
                    if (map[neighbour.y, neighbour.x, Convert.ToInt32(neighbour.isXDirection)] > next_best)
                    {
                        map[neighbour.y, neighbour.x, Convert.ToInt32(neighbour.isXDirection)] = next_best;
                        watched_points.Enqueue((neighbour.x, neighbour.y, neighbour.isXDirection));
                    }
                }
            }
        }

        // one of the targets to map where at each state we precount the estimation of the best possible time of arrival from PointA to PointB
        Dictionary<(int x, int y, bool isXDirection), TimeSpan[,,]> optimalPathEstimation;
        private void InitoptimalPathEstimation()
        {
            optimalPathEstimation = new Dictionary<(int x, int y, bool isXDirection), TimeSpan[,,]>();
            foreach (var source in sklad.source)
            {
                optimalPathEstimation.Add(source, new TimeSpan[skladHeight, skladWidth, 2]);
                ArrayExtensions.Fill(optimalPathEstimation[source], TimeSpan.MaxValue);
                CountPathesFromPoint(source, optimalPathEstimation[source]);
            }
            foreach (var source in sklad.target)
            {
                optimalPathEstimation.Add(source, new TimeSpan[skladHeight, skladWidth, 2]);
                ArrayExtensions.Fill(optimalPathEstimation[source], TimeSpan.MaxValue);
                CountPathesFromPoint(source, optimalPathEstimation[source]);
            }
            foreach (var source in sklad.charge)
            {
                optimalPathEstimation.Add(source, new TimeSpan[skladHeight, skladWidth, 2]);
                ArrayExtensions.Fill(optimalPathEstimation[source], TimeSpan.MaxValue);
                CountPathesFromPoint(source, optimalPathEstimation[source]);
            }
        }

        public double EstimateTimeToMoveFunc((int x, int y, bool isXDirection) point_1, (int x, int y, bool isXDirection) point_2)
        {
            if (optimalPathEstimation.ContainsKey(point_1))
            {
                return optimalPathEstimation[point_1][point_2.y, point_2.x, Convert.ToInt32(point_2.isXDirection)].TotalSeconds;
            }
            return (Math.Abs(point_1.x - point_2.x) + Math.Abs(point_1.y - point_2.y)) * (1 / sklad.skladConfig.unitSpeed) +
                (Convert.ToInt32(point_1.isXDirection != point_2.isXDirection) +
                Convert.ToInt32((point_1.x != point_2.x) && (point_1.y != point_2.y))) * sklad.skladConfig.unitRotateTime;
        }

        private void PrintEstimationMap()
        {
            Console.WriteLine("Printing estimated shortest pathes to all posible targets");
            foreach (var pair in optimalPathEstimation)
            {
                Console.WriteLine("Printing estimated shortest pathes to source " + $"{pair.Key.x} {pair.Key.y} {pair.Key.isXDirection}");
                for (int y = 0; y < skladHeight; ++y )
                {
                    for (int x = 0; x < skladWidth; ++x)
                    {
                        if (pair.Value[y, x, 0] == TimeSpan.MaxValue)
                        {
                            Console.Write("Inf/");
                        }
                        else
                        {
                            Console.Write(String.Format("{0,3:0.0}/", pair.Value[y, x, 0].TotalSeconds));
                        }
                        if (pair.Value[y, x, 1] == TimeSpan.MaxValue)
                        {
                            Console.Write("Inf  ");
                        }
                        else
                        {
                            Console.Write(String.Format("{0,3:0.0}  ", pair.Value[y, x, 1].TotalSeconds));
                        }
                    }
                    Console.WriteLine();
                }
            }
        }

        private void PrintGraphState(Dictionary<(int x, int y, bool isXDirection), TimeSpan> cost_so_far)
        {
            Console.Write("With X coordination: \n");
            for (int y = 0; y < sklad.skladLayout.Count; y++)
            {
                for (int x = 0; x < sklad.skladLayout[y].Count; x++)
                {
                    if (!cost_so_far.ContainsKey((x, y, true)))
                        Console.Write(String.Format("{0,7}", "Inf"));
                    else
                        Console.Write(String.Format("{0,7:0.00}", cost_so_far[(x, y, true)].TotalSeconds));
                }
                Console.WriteLine();
            }
            Console.Write("With Y coordination: \n");
            for (int y = 0; y < sklad.skladLayout.Count; y++)
            {
                for (int x = 0; x < sklad.skladLayout[y].Count; x++)
                {
                    if (!cost_so_far.ContainsKey((x, y, false)))
                        Console.Write(String.Format("{0,7}", "Inf"));
                    else
                        Console.Write(String.Format("{0,7:0.00}", cost_so_far[(x, y, false)].TotalSeconds));
                }
                Console.WriteLine();
            }

        }

    }
}
