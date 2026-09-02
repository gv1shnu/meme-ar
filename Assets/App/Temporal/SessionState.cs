using System.Collections.Generic;
using MemeAR.Events;

namespace MemeAR.Temporal
{
    /// <summary>
    /// Mutable, session-local memory of what the app has already done: which memes have
    /// appeared, when the last one showed, recent trigger types, and per-event-type
    /// cooldown timestamps. Used by opportunity scoring and cooldown gating to prevent
    /// meme spam. No camera data or identity is stored here.
    /// </summary>
    public sealed class SessionState
    {
        public double LastMemeTimestamp = double.NegativeInfinity;
        public int ActiveMemeCount;
        public int TotalMemesShown;

        private readonly Dictionary<EventType, double> _lastTriggerByType = new Dictionary<EventType, double>();
        private readonly Dictionary<string, double> _lastUseByMemeId = new Dictionary<string, double>();
        private readonly Queue<EventType> _recentTriggerTypes = new Queue<EventType>();
        private const int RecentTriggerWindow = 8;

        public IReadOnlyCollection<EventType> RecentTriggerTypes => _recentTriggerTypes;

        public double LastTriggerTime(EventType type)
        {
            return _lastTriggerByType.TryGetValue(type, out double t) ? t : double.NegativeInfinity;
        }

        public double LastUseTime(string memeId)
        {
            return _lastUseByMemeId.TryGetValue(memeId, out double t) ? t : double.NegativeInfinity;
        }

        public void RecordTrigger(EventType type, double timestamp)
        {
            _lastTriggerByType[type] = timestamp;
            _recentTriggerTypes.Enqueue(type);
            while (_recentTriggerTypes.Count > RecentTriggerWindow)
            {
                _recentTriggerTypes.Dequeue();
            }
        }

        public void RecordMemeShown(string memeId, double timestamp)
        {
            LastMemeTimestamp = timestamp;
            TotalMemesShown++;
            _lastUseByMemeId[memeId] = timestamp;
        }

        public void Reset()
        {
            LastMemeTimestamp = double.NegativeInfinity;
            ActiveMemeCount = 0;
            TotalMemesShown = 0;
            _lastTriggerByType.Clear();
            _lastUseByMemeId.Clear();
            _recentTriggerTypes.Clear();
        }
    }
}
