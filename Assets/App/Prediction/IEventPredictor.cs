using System.Collections.Generic;
using MemeAR.Events;
using MemeAR.Temporal;

namespace MemeAR.Prediction
{
    /// <summary>
    /// Turns anticipation signals into <see cref="PotentialEvent"/>s. The MVP ships a
    /// heuristic implementation; a learned predictor can be dropped in later without
    /// changing the prefetch path, because both speak only in PotentialEvents.
    /// </summary>
    public interface IEventPredictor
    {
        void Predict(
            TemporalSceneBuffer buffer,
            IReadOnlyList<SceneEvent> tickEvents,
            double now,
            List<PotentialEvent> output);
    }
}
