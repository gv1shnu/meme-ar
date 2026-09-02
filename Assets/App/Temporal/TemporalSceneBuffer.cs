using System.Collections.Generic;
using MemeAR.Scene;
using MemeAR.Events;

namespace MemeAR.Temporal
{
    /// <summary>
    /// Rolling temporal memory. Holds two horizons:
    ///  - Frame memory: ~1s of recent SceneSnapshots (for velocity/change detection).
    ///  - Event memory: ~5-30s of confirmed events (for novelty/duplicate suppression).
    /// Both are bounded ring buffers, so memory never grows without limit. Retained
    /// snapshots are clones, so callers may keep publishing/mutating the live snapshot.
    /// </summary>
    public sealed class TemporalSceneBuffer
    {
        private readonly RingBuffer<SceneSnapshot> _frames;
        private readonly RingBuffer<SceneEvent> _events;
        private readonly Stack<SceneSnapshot> _snapshotPool = new Stack<SceneSnapshot>();

        public SessionState Session { get; } = new SessionState();

        public TemporalSceneBuffer(int frameCapacity, int eventCapacity)
        {
            _frames = new RingBuffer<SceneSnapshot>(frameCapacity);
            _events = new RingBuffer<SceneEvent>(eventCapacity);
        }

        public int FrameCount => _frames.Count;
        public int EventCount => _events.Count;
        public SceneSnapshot Latest => _frames.Newest;
        public SceneSnapshot Previous => _frames.Count >= 2 ? _frames[_frames.Count - 2] : null;

        public SceneSnapshot FrameAt(int index) => _frames[index];
        public SceneEvent EventAt(int index) => _events[index];

        /// <summary>Stores a clone of the live snapshot, recycling evicted snapshots.</summary>
        public void PushFrame(SceneSnapshot live)
        {
            SceneSnapshot dest = _snapshotPool.Count > 0 ? _snapshotPool.Pop() : new SceneSnapshot();
            live.CloneInto(dest);
            SceneSnapshot evicted = _frames.Add(dest);
            if (evicted != null)
            {
                _snapshotPool.Push(evicted);
            }
        }

        public void PushEvent(SceneEvent evt)
        {
            _events.Add(evt);
        }

        /// <summary>Most recent confirmed event of a type within a time window, or null.</summary>
        public SceneEvent MostRecentEvent(EventType type, double now, double withinSeconds)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                SceneEvent e = _events[i];
                if (e.Type == type && (now - e.Timestamp) <= withinSeconds)
                {
                    return e;
                }
            }

            return null;
        }

        public int CountEventsOfType(EventType type, double now, double withinSeconds)
        {
            int count = 0;
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                SceneEvent e = _events[i];
                if ((now - e.Timestamp) > withinSeconds)
                {
                    break;
                }

                if (e.Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        public void Clear()
        {
            _frames.Clear();
            _events.Clear();
            Session.Reset();
        }
    }
}
