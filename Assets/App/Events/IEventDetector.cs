using System.Collections.Generic;
using MemeAR.Temporal;

namespace MemeAR.Events
{
    /// <summary>
    /// A single deterministic event detector. Detectors read the temporal buffer (current +
    /// recent snapshots) and emit zero or more events into the supplied list. They must not
    /// mutate the buffer or allocate per-frame beyond the emitted events.
    ///
    /// Detectors are deliberately small and composable so real ML-driven detectors can be
    /// added alongside heuristic ones without changing the engine.
    /// </summary>
    public interface IEventDetector
    {
        string Name { get; }
        void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output);
    }
}
