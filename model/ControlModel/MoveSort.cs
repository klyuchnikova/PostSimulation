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
        TimeSpan WINDOW_COOPERATE = TimeSpan.FromSeconds(15);
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
            skladWrapper.RunAllInitEvents();

            sklad = skladWrapper.GetSklad();
            skladHeight = sklad.skladLayout.Count; 
            skladWidth = sklad.skladLayout[0].Count;
            InitoptimalPathEstimation();

            // uncommet if you need to check estimation map
            // PrintEstimationMap();
            optimalChargeValue = sklad.skladConfig.unitChargeValue * 0.5;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // uncomment if you need to see events printed in the console
            // skladWrapper.GetAllAnts().ForEach(ant => ant.isDebug = true);

            // main cycle of the simulation
            do
            {
                if (timeProgress < skladWrapper.updatedTime)
                {
                    Console.WriteLine($"{skladWrapper.updatedTime}  {DateTime.Now - now}  {skladWrapper.GetSklad().deliveryCount}");
                    // sklad.squaresIsBusy.PrintReserves(sklad.skladLayout);
                    // PrintSklad();
                    timeProgress += TimeSpan.FromSeconds(60);
                }
                if (skladWrapper.updatedTime > maxModelTime)
                {
                    break;
                }
                else if (skladWrapper.GetSklad().deliveryCount >= 1000)
                {
                    Console.WriteLine($"Delivery time: {skladWrapper.updatedTime.TotalSeconds}");
                    break;
                }

                MakeCommands();
            } while (skladWrapper.Next());
        }

        private void MakeCommands()
        {
            skladWrapper.GetAvailableAnts(skladWrapper.updatedTime).ForEach(ant =>
            {
                ant.time_before_recount = skladWrapper.updatedTime + WINDOW_RECALCULATE;
                if (ant.isLoaded)
                {
                    RunToDrop(ant);
                }
                else if (ant.hasNoTarget() &&
                         ant.charge < optimalChargeValue)
                {
                    Console.WriteLine("Robot went charging!");   
                    RunToChargePoint(ant);
                }
                else { RunToLoadPoint(ant); }

                // each of the previous commands
                // 1) unreserves path
                // 2) tries to build a path without conflicts and reserve it
                // 3) if it failed empties commands anyway
                if (ant.commandList.commands.Count == 0) {
                    TryRunToFreePoint(ant);
                    ant.PrintCommands();
                }
                
                if (ant.commandList.commands.Count == 0)
                {
                    sklad.squaresIsBusy.PrintReserves(sklad.skladLayout);
                    throw new Exception($"Robot {ant.uid} couldn't make it out alive");
                }

                // ant.PrintCommands();
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
            // ant Bot is guaranteed to be on a verge of starting a new task
            // the only chance that we might not see coming is when the next command is going to happen right now but we erase and something breaks
            // such command is AntBotStop - if we move we ansolutely must run the event till the end or the state won't update correctly
            antbot.CleanReservation();
            if (antbot.commandList.commands.Count != 0)
            {
                if (antbot.commandList.commands[0].Ev.GetType() == typeof(AntBotStop))
                {
                    antbot.commandList = new CommandList(antbot);
                    antbot.commandList.AddCommand(new AntBotStop(antbot));
                } else
                {
                    antbot.commandList = new CommandList(antbot);
                }
            }
        }

        private void RunToDrop(AntBot antBot)
        {
            // always has a destination (target) already
            FreeCommandsWithExtendedOccupation(antBot);
            var actions_on_drop = new List<AntBotAbstractEvent>() {
                new AntBotUnload(antBot, (antBot.targetXCoordinate, antBot.targetYCoordinate, antBot.targetDirection)),
                new AntBotWait(antBot, TimeSpan.Zero)};
            var path = WHCAStarBuildPath(antBot.commandList.antState, (antBot.targetXCoordinate, antBot.targetYCoordinate, antBot.targetDirection), actions_on_drop);
            if (path.doesExist)
            {
                addPath(antBot, path.path);
            }
        }

        private void RunToChargePoint(AntBot antBot)
        {
            // AntBot is uloaded, maybe is moving towards a queue already
            // still, we'll simply reassighn target
            
            var actions_on_charge = new List<AntBotAbstractEvent>() {
                new AntBotCharge(antBot),
                new AntBotWait(antBot, TimeSpan.Zero)};

            if (!antBot.hasNoTarget())
            {
                --antBot.sklad.skladTargeted[antBot.targetYCoordinate][antBot.targetXCoordinate];
                antBot.targetXCoordinate = -1;
            }

            // finding the closest source to assighn to
            var actions_on_load = new List<AntBotAbstractEvent>() { new AntBotLoad(antBot) };

            var targets = new List<((int x, int y, bool isXDirection) source, double estimated_time)>();
            double count_rob_coef = 100;
            foreach (var target_point in antBot.sklad.charge)
            {
                targets.Add((target_point,
                    antBot.sklad.skladTargeted[target_point.y][target_point.x] * count_rob_coef +
                    EstimateTimeToMoveFunc(target_point, antBot.GetCurrentPoint())));
            };

            targets.Sort((first, second) =>
            {
                return first.estimated_time.CompareTo(second.estimated_time);
            });

            FreeCommandsWithExtendedOccupation(antBot);
            foreach (var target in targets)
            {
                var closest_source = target.source;
                var path = WHCAStarBuildPath(antBot.commandList.antState, closest_source, actions_on_load);
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
            // Also sets a destination (drop)
            // (all of it happens in AntBotLoad event)

            var actions_on_load = new List<AntBotAbstractEvent>() {new AntBotLoad(antBot)};
            if (!antBot.hasNoTarget())
            {
                --antBot.sklad.skladTargeted[antBot.targetYCoordinate][antBot.targetXCoordinate];
                antBot.targetXCoordinate = -1;
            }

            var targets = new List<((int x, int y, bool isXDirection) source, double estimated_time)>();
            double count_rob_coef = 5;
            foreach (var target_point in antBot.sklad.source)
            {
                // PrintEstimationMap(target_point);
                targets.Add((target_point, 
                    antBot.sklad.skladTargeted[target_point.y][target_point.x]*count_rob_coef + 
                    EstimateTimeToMoveFunc(target_point, antBot.GetCurrentPoint())));
            };

            targets.Sort((first, second) =>
            {
                return first.estimated_time.CompareTo(second.estimated_time);
            });

            FreeCommandsWithExtendedOccupation(antBot);
            foreach (var target in targets)
            {
                var closest_source = target.source;
                var path = WHCAStarBuildPath(antBot.commandList.antState, closest_source, actions_on_load);
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
            var path = WHCAStarBuildEscapePath(antBot.commandList.antState, cooperate_until, IsOnTarget(antBot));
            if (path != null) //&& (path.lastTime - antBot.commandList.lastTime).TotalSeconds >= WINDOW_COOPERATE.TotalSeconds/2)
            {
                addPath(antBot, path);
            } else {
                PrintSklad();
                antBot.sklad.squaresIsBusy.PrintReserves(sklad.skladLayout);
                throw new Exception($"{skladWrapper.updatedTime}: Robot {antBot.uid} on ({antBot.xCord},{antBot.yCord}) couldn't run to free point");
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
                int max_distance = skladWrapper.getFreePath(antBot, direction, commandList.lastTime);
                if (max_distance == 0)
                {
                    // then let's wait
                    var commands_copy = commandList.Clone();
                    commands_copy.antState.setSpeedByDirection(direction);
                    var near = antBot.sklad.squaresIsBusy.GetNearestReserve(
                        (int)Math.Round(antBot.xCoordinate + commands_copy.antState.xSpeed / antBot.sklad.skladConfig.unitSpeed),
                        (int)Math.Round(antBot.yCoordinate + commands_copy.antState.ySpeed / antBot.sklad.skladConfig.unitSpeed),
                        commands_copy.lastTime + TimeSpan.FromSeconds(0.00001));
                    commands_copy.antState.xSpeed = commands_copy.antState.ySpeed = 0;
                    // first we add the scenario where we simply wait before the nearest event
                    if (near > commands_copy.lastTime + TimeSpan.FromSeconds(0.00001) &&
                        near != TimeSpan.MaxValue &&
                        commands_copy.AddCommand(new AntBotWait(antBot, near - commands_copy.lastTime), false))
                    {
                        yield return commands_copy;
                    }
                    continue;
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
            yield break;
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
            // first try to build commands to reach goal within time window.
            // then add actions_on_goal if goal is reachable within window
            // if after all there's still time - try to build escape path
            if (antBot.xSpeed != 0 || antBot.ySpeed != 0)
            {
                throw new Exception();
            }

            // Console.WriteLine($"WHCAStarBuildPath ({antBot.xCord}, {antBot.yCord}) -> ({goal.x}, {goal.y})");
            TimeSpan cooperate_until = skladWrapper.updatedTime + WINDOW_COOPERATE;
            CommandList best_commands = WHCAStarBuildPathToTarget(antBot, goal, actions_on_goal, cooperate_until);
            return (best_commands != null, best_commands);
        }
        private CommandList WHCAStarBuildPathToTarget(
            AntBot antBot, 
            (int x, int y, bool isXDirection) goal, 
            List<AntBotAbstractEvent> actions_on_goal, 
            TimeSpan cooperate_until)
        {

            if (antBot.lastUpdated >= cooperate_until)
            {
                return new CommandList(antBot);
            }

            var sorted_cLists = new SortedSet<(CommandList cList, double priority)>(
                new VerticeComparator<(CommandList cList, double priority)>(
                    (a, b) => ((a.cList.antState.charge == b.cList.antState.charge) && (a.priority == b.priority) ? 0 : (a.priority > b.priority ? 1 : -1))
                )
            );

            (CommandList cList, double priority) best_cList_to_goal = (null, 0);
            var cost_so_far = new Dictionary<(int x, int y, bool isXDirection), TimeSpan>();

            sorted_cLists.Add((new CommandList(antBot), 0));
            cost_so_far.Add(antBot.GetCurrentPoint(), new TimeSpan(0));

            CommandList best_unreachable = null;
            double best_unreachable_priority = double.MaxValue;

            while (sorted_cLists.Count > 0)
            {
                var min = sorted_cLists.Min;
                CommandList current = sorted_cLists.Min.cList;
                sorted_cLists.Remove(min);

                if (current.antState.xCord == goal.x && current.antState.yCord == goal.y && current.antState.isXDirection == goal.isXDirection)
                {
                    bool can_go_by_this_path = true;
                    foreach (var a_command in actions_on_goal)
                    {
                        if (!current.AddCommand(a_command, false))
                        {
                            can_go_by_this_path = false;
                            break;
                        }
                    }
                    if (!can_go_by_this_path)
                    {
                        continue;
                    }
                    current.AddCommand(new AntBotWait(antBot, TimeSpan.Zero), false);
                    var path_to_escape = WHCAStarBuildEscapePath(current.antState, current.lastTime + WINDOW_COOPERATE);
                    if (path_to_escape == null)
                        continue;

                    foreach (var t_command in path_to_escape.commands)
                    {
                        current.AddCommand(t_command.Ev, false);
                    }

                    return current;
                }
                if (current.lastTime > cooperate_until || sorted_cLists.Count > 2000)
                {
                    // then we become dumb and use any algorithm to quickly get to the target.
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
                            sorted_cLists.Add((commandList, priority));
                        }
                    }
                }
            }
            return best_unreachable;
        }
        private CommandList WHCAStarBuildEscapePath(AntBot antBot, TimeSpan cooperate_until, bool is_on_target = true)
        {
            // instead of getting somewhere permenantly,
            // the only thing we need to do is get away from the targets
            // so the priority is in fact calculated from command ADDITIONAL time and summ distance from targets
            // priority = current distance from start + [sum(optimal distance from final point to targets)/number targets]
            // the greater priority wins in this case

            if (antBot.xSpeed != 0 || antBot.ySpeed !=0)
            {
                throw new Exception();
            }

            Func<CommandList, TimeSpan> CountPriority;
            int minDistanceToStand;
            if (is_on_target)
            {
                CountPriority = (CommandList cList) =>
                {
                    TimeSpan priority = TimeSpan.Zero;
                    foreach (var source_map in optimalPathEstimation)
                    {
                        priority += source_map.Value[cList.antState.yCord, cList.antState.xCord, Convert.ToInt32(cList.antState.isXDirection)];
                    }
                    priority = TimeSpan.FromSeconds(priority.TotalSeconds / optimalPathEstimation.Count) +
                    optimalPathEstimation[antBot.GetCurrentPoint()][cList.antState.yCord, cList.antState.xCord, Convert.ToInt32(cList.antState.isXDirection)];
                    return priority;
                };
                minDistanceToStand = 5;
            } else
            {
                CountPriority = (CommandList cList) =>
                {
                    TimeSpan priority = TimeSpan.Zero;
                    foreach (var source_map in optimalPathEstimation)
                    {
                        priority += source_map.Value[cList.antState.yCord, cList.antState.xCord, Convert.ToInt32(cList.antState.isXDirection)];

                    }
                    priority = TimeSpan.FromSeconds(priority.TotalSeconds / optimalPathEstimation.Count) + cList.lastTime;
                    return priority;
                };
                minDistanceToStand = 0;
            }

            var sorted_cLists = new SortedSet<(CommandList cList, TimeSpan priority)>(
                new VerticeComparator<(CommandList cList, TimeSpan priority)>(
                    (a, b) => ((a.cList.antState.charge == b.cList.antState.charge) && (a.priority == b.priority) ? 0 : (a.priority > b.priority ? 1 : -1))
                )
            );

            (CommandList cList, TimeSpan priority) best_vertice = (null, TimeSpan.MinValue);
            var cost_so_far = new Dictionary<(int x, int y, bool isXDirection), TimeSpan>();

            sorted_cLists.Add((new CommandList(antBot), TimeSpan.Zero));
            cost_so_far.Add(antBot.GetCurrentPoint(), TimeSpan.MaxValue);

            while (sorted_cLists.Count > 0)
            {
                var max = sorted_cLists.Max;
                CommandList current = max.cList;
                sorted_cLists.Remove(max);

                if (current.antState.DistanceFrom(antBot.GetCurrentPoint()) >= minDistanceToStand)
                {
                    CommandList current_clone = current.Clone();
                    if (current_clone.AddCommand(new AntBotWait(current_clone.antState, maxTime - current.lastTime - TimeSpan.FromMinutes(1)), false))
                    {
                        return current_clone;
                    }
                }

                if (current.lastTime > cooperate_until)
                {
                    continue;
                }

                foreach (var commandList in GetPossibleCommands(current))
                {
                    TimeSpan priority_ = CountPriority(commandList);
                    (int x, int y, bool isXDirection) finish_point = commandList.antState.GetCurrentPoint();

                    if (!cost_so_far.ContainsKey(finish_point) || priority_ > cost_so_far[finish_point])
                    {
                        cost_so_far[finish_point] = priority_;
                        sorted_cLists.Add((commandList, priority_));
                    }
                }
            }
            
            if (best_vertice.cList != null && best_vertice.cList.antState.DistanceFrom(antBot.GetCurrentPoint()) >= minDistanceToStand)
            {
                return best_vertice.cList;
            }
            return null;
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

        private void PrintEstimationMap((int x, int y, bool isXDirection) target_point)
        {
            Console.WriteLine("Printing estimated shortest pathes to source " + $"{target_point.x} {target_point.y} {target_point.isXDirection}");
            var map = optimalPathEstimation[target_point];
            for (int y = 0; y < skladHeight; ++y)
            {
                for (int x = 0; x < skladWidth; ++x)
                {
                    if (map[y, x, 0] == TimeSpan.MaxValue)
                    {
                        Console.Write("Inf/");
                    }
                    else
                    {
                        Console.Write(String.Format("{0,3:0.0}/", map[y, x, 0].TotalSeconds));
                    }
                    if (map[y, x, 1] == TimeSpan.MaxValue)
                    {
                        Console.Write("Inf  ");
                    }
                    else
                    {
                        Console.Write(String.Format("{0,3:0.0}  ", map[y, x, 1].TotalSeconds));
                    }
                }
                Console.WriteLine();
            }
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

        private void PrintSklad()
        {
            var bot_positions = new int[skladHeight, skladWidth];

            foreach (var bot in skladWrapper.GetAllAnts())
            {
                bot_positions[bot.yCord, bot.xCord] = bot.GetDirection();
                // bot.PrintCommands();
            }

            for (int y = 0; y < skladHeight; ++y)
            {
                for (int x = 0; x < skladWidth; ++x)
                {
                    if (sklad.skladLayout[y][x] == 0)
                    {
                        Console.Write("-X-");
                    }
                    else
                    {
                        switch (bot_positions[y, x])
                        {
                            case 1:
                                Console.Write("→");
                                break;
                            case 2:
                                Console.Write("↓");
                                break;
                            case 3:
                                Console.Write("←");
                                break;
                            case 4:
                                Console.Write("↑");
                                break;
                            case 5:
                                Console.Write("↔");
                                break;
                            case 6:
                                Console.Write("↕");
                                break;
                            case 0:
                                Console.Write(" ");
                                break;
                        }
                        switch (sklad.skladLayout[y][x])
                        {
                            case 1:
                                Console.Write("  ");
                                break;
                            case 2:
                                Console.Write("-S");
                                break;
                            case 3:
                                Console.Write("-D");
                                break;
                            case 4:
                                Console.Write("-C");
                                break;
                            case 5:
                                Console.Write("|S");
                                break;
                            case 6:
                                Console.Write("|D");
                                break;
                            case 7:
                                Console.Write("|C");
                                break;
                        }
                    }
                    Console.Write(" ");
                }
                Console.WriteLine();
            }
        }
    }
}
