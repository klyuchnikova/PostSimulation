using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AbstractModel
{
    public abstract class AbstractObject
    {
        static Random rnd = new Random();
        public string uid = rnd.Next(1, 100000).ToString();
        [XmlElement(Type = typeof(TimeSpan))]
        public TimeSpan lastUpdated;
        public abstract (TimeSpan, AbstractEvent) getNearestEvent(List<AbstractObject> objects);
        public abstract void Update(TimeSpan timeSpan);
    }


    public abstract class AbstractEvent
    {
        public abstract void runEvent(List<AbstractObject> objects, TimeSpan timeSpan);
    }

    public abstract class AbstractWrapper
    {
        public List<AbstractObject> objects = new List<AbstractObject>();
        protected SortedList<TimeSpan, AbstractEvent> eventList = new SortedList<TimeSpan, AbstractEvent>();
        public TimeSpan updatedTime;
        protected void UpdateObjects(TimeSpan timeSpan)
        {
            foreach (var obj in objects)
            {
                obj.Update(timeSpan);
            }
        }

        protected void AddEvent(TimeSpan timeSpan, AbstractEvent modelEvent)
        {
            while (eventList.ContainsKey(timeSpan))
            {
                timeSpan = timeSpan.Add(TimeSpan.FromTicks(1));
            }
            eventList.Add(timeSpan, modelEvent);
        }


        public bool Next()
        {
            CheckState();
            var nearObjectEventTime = TimeSpan.MaxValue;
            AbstractEvent modelEvent = null;
            for (int i = 0; i < objects.Count; i++)
            {
                var nr = objects[i].getNearestEvent(objects);
                if (nr.Item1 < nearObjectEventTime)
                {
                    nearObjectEventTime = nr.Item1;
                    modelEvent = nr.Item2;
                }
            }

            if (eventList.Count == 0 && modelEvent is null)
                return false;

            if (eventList.Count > 0 && eventList.First().Key < nearObjectEventTime)
            {
                var task = eventList.First();
                UpdateObjects(task.Key);
                updatedTime = task.Key;
                eventList.Remove(task.Key);
                task.Value.runEvent(objects, task.Key);
            }
            else
            {
                List<AbstractEvent> modelEvents = new List<AbstractEvent>();
                for (int i = 0; i < objects.Count; i++)
                {
                    var nr = objects[i].getNearestEvent(objects);
                    if (nr.Item1 == nearObjectEventTime)
                    {
                        modelEvents.Add(nr.Item2);
                    }
                }
                UpdateObjects(nearObjectEventTime);
                updatedTime = nearObjectEventTime;
                modelEvents.ForEach(x => x.runEvent(objects, nearObjectEventTime));
            }
            return true;
        }

        protected abstract void CheckState();
    }

}
