using System.Collections.Generic;
using MemeAR.Temporal;

namespace MemeAR.Events
{
    /// <summary>
    /// Runs all registered detectors against the temporal buffer each tick and routes the
    /// emitted events. Anticipation events are returned for the predictor/prefetch path;
    /// Triggered/Resolved events are also written into event memory (for novelty/duplicate
    /// suppression) and returned for opportunity scoring.
    ///
    /// The engine itself does no perception and no rendering: it only turns snapshots into
    /// typed events, keeping detection isolated and testable.
    /// </summary>
    public sealed class EventEngine
    {
        private readonly List<IEventDetector> _detectors = new List<IEventDetector>();
        private readonly List<SceneEvent> _working = new List<SceneEvent>(16);

        public IReadOnlyList<IEventDetector> Detectors => _detectors;

        public void AddDetector(IEventDetector detector)
        {
            if (detector != null)
            {
                _detectors.Add(detector);
            }
        }

        /// <summary>
        /// Detects events for this tick. Appends all emitted events to
        /// <paramref name="emitted"/>. Confirmed (Triggered/Resolved) events are also pushed
        /// into the buffer's event memory.
        /// </summary>
        public void Update(TemporalSceneBuffer buffer, double now, List<SceneEvent> emitted)
        {
            _working.Clear();
            for (int i = 0; i < _detectors.Count; i++)
            {
                _detectors[i].Detect(buffer, now, _working);
            }

            for (int i = 0; i < _working.Count; i++)
            {
                SceneEvent e = _working[i];
                emitted.Add(e);

                if (e.Phase == EventPhase.Triggered || e.Phase == EventPhase.Resolved)
                {
                    buffer.PushEvent(e);
                }
            }
        }
    }
}
