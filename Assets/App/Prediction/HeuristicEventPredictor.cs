using System.Collections.Generic;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Temporal;

namespace MemeAR.Prediction
{
    /// <summary>
    /// Lightweight heuristic predictor. It promotes Anticipation-phase events into
    /// PotentialEvents (scaling probability by confidence) and predicts their trigger time
    /// using the configured window. This demonstrates the prediction -> prefetch -> trigger
    /// -> immediate-response architecture without requiring any ML model.
    /// </summary>
    public sealed class HeuristicEventPredictor : IEventPredictor
    {
        private readonly MemeArConfig _config;

        public HeuristicEventPredictor(MemeArConfig config)
        {
            _config = config;
        }

        public void Predict(
            TemporalSceneBuffer buffer,
            IReadOnlyList<SceneEvent> tickEvents,
            double now,
            List<PotentialEvent> output)
        {
            for (int i = 0; i < tickEvents.Count; i++)
            {
                SceneEvent e = tickEvents[i];
                if (e.Phase != EventPhase.Anticipation)
                {
                    continue;
                }

                // Probability grows with detector confidence; clamp to a believable range.
                float probability = Mathf.Clamp01(0.4f + 0.6f * e.Confidence);
                if (probability < _config.predictionPublishThreshold)
                {
                    continue;
                }

                double window = _config.predictedTriggerWindowMs / 1000.0;
                output.Add(new PotentialEvent(
                    e.CorrelationId,
                    e.Type,
                    probability,
                    now + window,
                    e.PrimaryParticipant,
                    e.PrimaryObject));
            }
        }
    }
}
