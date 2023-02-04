using System;
using System.Collections.Generic;
using System.Linq;

namespace SkladModel
{
    public class SquaresIsBusy
    {

        public SquaresIsBusy() { }
        public class TimeBusy
        {
            public TimeSpan endTime;
            public string uid;
            public TimeBusy(TimeSpan timeSpan, string uid) 
            {
                endTime = timeSpan;
                this.uid = uid;
            }
        }

        private Dictionary<int, Dictionary<int, SortedDictionary<TimeSpan, TimeBusy>>> squareIsBusy = new Dictionary<int, Dictionary<int, SortedDictionary<TimeSpan, TimeBusy>>>();
        private string skladUid;
        public SquaresIsBusy(Dictionary<int, Dictionary<int, int>> skladLayout, string uid) 
        {
            skladUid = uid;
            for (int y = 0; y < skladLayout.Count; y++)
            {
                for (int x = 0; x < skladLayout[y].Count; x++)
                {
                    if (y == 0)
                        squareIsBusy.Add(x, new Dictionary<int, SortedDictionary<TimeSpan, TimeBusy>>());
                    squareIsBusy[x].Add(y, new SortedDictionary<TimeSpan, TimeBusy>());
                    if (skladLayout[y][x] == 0)
                        squareIsBusy[x][y].Add(TimeSpan.MinValue, new TimeBusy(TimeSpan.MaxValue, skladUid));
                }
            }
        }

        public void ReserveUpTo(int x, int y, TimeSpan endInterval, string uid)
        {
            if (squareIsBusy[x][y].Count > 0)
            {
                TimeSpan startInterval = squareIsBusy[x][y].Last().Value.endTime;
                ReserveRoom(x, y, startInterval, endInterval, uid);
            }
            else
                ReserveRoom(x, y, TimeSpan.Zero, endInterval, uid);
        }

        private bool IsCross(TimeSpan from1, TimeSpan to1, TimeSpan from2, TimeSpan to2)
        {
            var intersectionFrom = from1 > from2 ? from1 : from2;
            var intersectionTo = to1 < to2 ? to1 : to2;
            return intersectionTo - intersectionFrom > TimeSpan.FromSeconds(0.001); 
        }

        public bool CheckIsBusy(int x, int y, TimeSpan startInterval, TimeSpan endInterval, string uid)
        {
            return squareIsBusy[x][y].Any(z => uid != z.Value.uid && IsCross(startInterval, endInterval, 
                z.Key, z.Value.endTime));
        }

        public bool ReserveRoom (int x, int y, TimeSpan startInterval, TimeSpan endInterval, string uid)
        {
            if (CheckIsBusy(x, y, startInterval, endInterval, uid))
                return false;
            if (squareIsBusy[x][y].ContainsKey(startInterval))
            {
                if ((squareIsBusy[x][y][startInterval].endTime < endInterval))
                    squareIsBusy[x][y][startInterval].endTime = endInterval;
            }
            else
                squareIsBusy[x][y].Add(startInterval, new TimeBusy(endInterval, uid));
            return true;
        }

        public TimeSpan GetPosibleReserve(int x, int y, TimeSpan from)
        {
            if (CheckIsBusy(x, y, from, from, ""))
                return TimeSpan.Zero;
            var first = squareIsBusy[x][y].FirstOrDefault(t => t.Value.endTime > from);
            return first.Key == TimeSpan.Zero? TimeSpan.MaxValue : first.Key;
        }

        public TimeSpan GetNearestReserve(int x, int y, TimeSpan from)
        {
            TimeSpan posibleTime = from;
            foreach (var sb in squareIsBusy[x][y].Keys)
            {
                if (sb < from && squareIsBusy[x][y][sb].endTime < from)
                    continue;
                if (sb <= from && squareIsBusy[x][y][sb].endTime > from)
                    posibleTime = squareIsBusy[x][y][sb].endTime;
                if (sb > from)
                {
                    if ((sb - posibleTime) > TimeSpan.FromSeconds(0.33))
                        return posibleTime;
                    else 
                        posibleTime = squareIsBusy[x][y][sb].endTime;
                }
            }
            return posibleTime;
        }


        public void UnReserveRoom(int x, int y, TimeSpan time)
        {
            squareIsBusy[x][y].Remove(time);
        }
        public void PrintRoom(int x, int y)
        {
            foreach(var sq in squareIsBusy[x][y])
            {
                Console.WriteLine($"{x} {y} {sq.Value.uid}, {sq.Key}, {sq.Value.endTime}");
            }
            Console.WriteLine();
        }


    }



}
