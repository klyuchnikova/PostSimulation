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
        public squareState()
        {

        }
        public TimeSpan xMinTime = TimeSpan.MaxValue;
        public TimeSpan yMinTime = TimeSpan.MaxValue;
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
            while (!skladWrapper.isEventCountEmpty() && skladWrapper.Next())
            {
                if (timeProgress < skladWrapper.updatedTime)
                {
                    Console.WriteLine($"{skladWrapper.updatedTime}  {DateTime.Now - now}  {skladWrapper.GetSklad().deliveryCount}");
                    timeProgress += TimeSpan.FromMinutes(1);
                    break;
                }
            }

            sklad = skladWrapper.GetSklad();
            skladHeight = sklad.skladLayout.Count; 
            skladWidth = sklad.skladLayout[0].Count;
            InitopimalPathEstimation();
            optimalChargeValue = sklad.skladConfig.unitChargeValue * 0.8;

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

                MakeCommands();
            }
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

            List<(int x, int y, bool isXDirection)> sources = antBot.sklad.charge;
            double closest_time = Double.MaxValue;

            (int x, int y, bool isXDirection) closest_source = (0, 0, true);
            foreach (var source in sources)
            {
                // --! will need to count number assigned to evade crowds + number of packages at the moment to estimate the efficiency
                double estimated_time = antBot.EstimateTimeToMoveFunc((source.x, source.y, source.isXDirection));
                if (estimated_time < closest_time)
                {
                    closest_time = estimated_time;
                    closest_source = source;
                }
            };

            FreeCommandsWithExtendedOccupation(antBot);
            var path = WHCAStarBuildPath(antBot, closest_source, actions_on_charge);
            if (path.doesExist)
            {
                addPath(antBot, path.path);
                antBot.targetXCoordinate = closest_source.x;
                antBot.targetYCoordinate = closest_source.y;
                antBot.targetDirection = closest_source.isXDirection;
            }
        }
        private void RunToLoadPoint(AntBot antBot)
        {
            // run to closest source
            // after receiving a package empties the command List and
            // resets reservation immidiately cause we don't want to stand although since we're free I suppose we could still reconsider.
            // Also sets a destination (drop)
            // (all of it happens in AntBotUnload event)

            // finding the closest source to assighn to
            var actions_on_load = new List<AntBotAbstractEvent>() {new AntBotLoad(antBot)};

            List<(int x, int y, bool isXDirection)> sources = antBot.sklad.charge;
            double closest_time = Double.MaxValue;

            (int x, int y, bool isXDirection) closest_source = (0, 0, true);
            foreach (var source in sources)
            {
                // --! will need to count number assigned to evade crowds + number of packages at the moment to estimate the efficiency
                double estimated_time = antBot.EstimateTimeToMoveFunc((source.x, source.y, source.isXDirection));
                if (estimated_time < closest_time)
                {
                    closest_time = estimated_time;
                    closest_source = source;
                }
            };

            FreeCommandsWithExtendedOccupation(antBot);
            var path = WHCAStarBuildPath(antBot, closest_source, actions_on_load);
            if (path.doesExist)
            {
                addPath(antBot, path.path);
                antBot.targetXCoordinate = closest_source.x;
                antBot.targetYCoordinate = closest_source.y;
                antBot.targetDirection = closest_source.isXDirection;
            }
        }

        private void TryRunToFreePoint(AntBot antBot)
        {
            // doesn't set target - only comands

            // in this case the bot is already standing on one place just fine (???)
            // if (ant.reserved.Count > 0 
            //    && ant.reserved.Any(r => r.x == ant.xCord && r.y == ant.yCord 
            //    && (r.from - ant.lastUpdated).TotalSeconds < 0.0001 && r.to == TimeSpan.MaxValue))
            return;
        }

        public IEnumerable<CommandList> GetPossibleCommands(CommandList commandList)
        {
            // attempt to
            // a) move (?) in the same direction b) stop (?) + move in different direction c) stop(?) + rotate + move d) stop (?) + wait
            // every action is like stop (?)  + wait (?) + (Accelerate + move (on int) + stop) + rotate (true or false)

            AntBot antBot = commandList.antState;

            if (commandList.commands.Count == 0 || 
                commandList.commands[commandList.commands.Count-1].GetType() != typeof(AntBotWait))
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
                for (int move_on_dist = 1; move_on_dist < max_distance; ++move_on_dist)
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

        private (bool doesExist, CommandList path) WHCAStarBuildPath(AntBot antBot, (int x, int y, bool isXDirection) goal, List<AntBotAbstractEvent> actions_on_goal)
        {
            // first try to build commands to reach goal within time window.
            // then add actions_on_goal if goal is reachable within window
            // if after all there's still time - try to build escape path
            var frontier = new SortedSet<(CommandList cList, double priority)>(
                new VerticeComparator<(CommandList cList, double priority)>(
                    (a, b) => (a.cList.uid == b.cList.uid ? 0 : (a.priority > b.priority ? 1 : -1))
                )
            );
            frontier.Add((new CommandList(antBot), 0));
            var came_from = new Dictionary<(int x, int y, bool isXDirection), CommandList>();
            var cost_so_far = new Dictionary<(int x, int y, bool isXDirection), TimeSpan>();

            // in fact here it would be more logical to keep only (x, y, isXDirection) with most optimal time and velocity... maybe
            came_from.Add(antBot.GetCurrentPoint(), new CommandList()); // None is not an option in C# so...
            cost_so_far.Add(antBot.GetCurrentPoint(), new TimeSpan(0));

            // this very time until we try to consider reservations. Then we don't care (and don't reserve)
            TimeSpan cooperate_until = skladWrapper.updatedTime + WINDOW_COOPERATE;
            CommandList current = new CommandList(antBot);
            while (frontier.Count > 0)
            {
                current = frontier.Min.cList;
                frontier.Remove(frontier.Min);

                Console.WriteLine(String.Format("{0:0} {1:0} {2,1} {3:0.00}", current.antState.xCord, current.antState.yCord, current.antState.isXDirection, current.lastTime.TotalSeconds));
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
                    // --! state -> we can use this Shit just in case...
                    // finally WE WILL HAVE TO PUT x, y, dir as final in antState!!!
                    // ALSO WE HAVE TO PUT cost_so_far[goal] TO THIS PARTICULAR lastTime
                    TimeSpan last_time = current.lastTime;
                    (int x, int y, bool isXDirection) finish_point = current.antState.GetCurrentPoint();
                    last_time += TimeSpan.FromSeconds(EstimateTimeToMoveFunc(goal, finish_point));
                    if (!cost_so_far.ContainsKey(goal) || last_time < cost_so_far[goal])
                    {
                        cost_so_far[goal] = last_time;
                        came_from[finish_point] = current;
                    }
                    current.antState.xCoordinate = goal.x;
                    current.antState.yCoordinate = goal.y;
                    current.antState.isXDirection = goal.isXDirection;
                }

                int number_commands = 0;
                foreach (var commandList in GetPossibleCommands(current)) // (TimeSpan Key, AntBotAbstractEvent Ev)
                {
                    ++number_commands;
                    TimeSpan last_time = commandList.lastTime;
                    (int x, int y, bool isXDirection) finish_point = commandList.antState.GetCurrentPoint();

                    if (!cost_so_far.ContainsKey(finish_point) || last_time < cost_so_far[finish_point])
                    {
                        cost_so_far[finish_point] = last_time;
                        double priority = last_time.TotalSeconds + antBot.EstimateTimeToMoveFunc(goal, finish_point);
                        frontier.Add((commandList, priority));
                        came_from[finish_point] = commandList;
                    }
                }

                Console.Write(String.Format("Number neighbours investigated {0}\n", number_commands));
                PrintGraphState(cost_so_far);
                foreach (var el in frontier) {
                    Console.Write(String.Format("{0:0.00} ", el.priority));
                }
                Console.WriteLine();

            }
            // 1. first case : we didn't reach the goal => stand still (in the cycle we'll have to reassighn commands)
            // 2. second case : reached the goal within window => we have to count + reserve escape path
            // 3. third case : path found yet goal was reached out of reservation => simply assighn commands
            if (current.antState.xCord == goal.x && current.antState.yCord == goal.y && 
                current.antState.isXDirection == goal.isXDirection)
            {
                if (cost_so_far[current.antState.GetCurrentPoint()] > cooperate_until)
                {
                    // 3
                    return (true, current);
                }
                else
                {
                    // 2
                    var path_to_free = WHCAStarBuildEscapePath(current.antState, TimeSpan.FromSeconds(
                        Math.Max(cooperate_until.TotalSeconds, current.lastTime.TotalSeconds+3)));
                    if (path_to_free.doesExist)
                    {
                        // --!
                        // ExtendPath current by path_to_free.path
                    }
                }
            }
            return (false, null);
        }

        private (bool doesExist, CommandList path) WHCAStarBuildEscapePath(AntBot antBot, TimeSpan TIME_WINDOW)
        {
            // TIME_WINDOW is supposed to be time from antBot state until which we'll count theb path considering the reservation
            //  AntBot antBot = cList.antState.ShalowClone();
            // antBot.lastUpdated = cList.lastTime;
            // var gr = graph.Peek();
            // if (gr.Value.antState.CheckRoom(gr.Value.lastTime, TimeSpan.MaxValue))
            return (false, null);
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
            map[start_point.y, start_point.y, Convert.ToInt32(start_point.isXDirection)] = TimeSpan.Zero;
            while(watched_points.Count != 0)
            {
                var point = ((int x, int y, bool isXDirection)) watched_points.Dequeue();
                TimeSpan current_best = map[point.x, point.y, Convert.ToInt32(start_point.isXDirection)];
                foreach (var neighbour in GetNeighbours(point))
                {
                    TimeSpan next_best = current_best + neighbour.action_time;
                    if (map[neighbour.x, neighbour.y, Convert.ToInt32(neighbour.isXDirection)] > next_best)
                    {
                        map[neighbour.x, neighbour.y, Convert.ToInt32(neighbour.isXDirection)] = next_best;
                        watched_points.Enqueue((neighbour.x, neighbour.y, neighbour.isXDirection));
                    }
                }
            }
        }

        // one of the targets to map where at each state we precount the estimation of the best possible time of arrival from PointA to PointB
        Dictionary<(int x, int y, bool isXDirection), TimeSpan[,,]> opimalPathEstimation;
        private void InitopimalPathEstimation()
        {
            opimalPathEstimation = new Dictionary<(int x, int y, bool isXDirection), TimeSpan[,,]>();
            foreach (var source in sklad.source)
            {
                opimalPathEstimation.Add(source, new TimeSpan[skladHeight, skladWidth, 2]);
                CountPathesFromPoint(source, opimalPathEstimation[source]);
            }
            foreach (var source in sklad.target)
            {
                opimalPathEstimation.Add(source, new TimeSpan[skladHeight, skladWidth, 2]);
                CountPathesFromPoint(source, opimalPathEstimation[source]);
            }
            foreach (var source in sklad.charge)
            {
                opimalPathEstimation.Add(source, new TimeSpan[skladHeight, skladWidth, 2]);
                CountPathesFromPoint(source, opimalPathEstimation[source]);
            }
        }

        public double EstimateTimeToMoveFunc((int x, int y, bool isXDirection) point_1, (int x, int y, bool isXDirection) point_2)
        {
            if (opimalPathEstimation.ContainsKey(point_1))
            {
                return opimalPathEstimation[point_1][point_2.y, point_2.x, Convert.ToInt32(point_2.isXDirection)].TotalSeconds;
            }
            return (Math.Abs(point_1.x - point_2.x) + Math.Abs(point_1.y - point_2.y)) * (1 / sklad.skladConfig.unitSpeed) +
                (Convert.ToInt32(point_1.isXDirection != point_2.isXDirection) +
                Convert.ToInt32((point_1.x != point_2.x) && (point_1.y != point_2.y))) * sklad.skladConfig.unitRotateTime;
        }


    }
}
