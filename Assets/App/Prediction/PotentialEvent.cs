using MemeAR.Events;

namespace MemeAR.Prediction
{
    /// <summary>
    /// A speculative, not-yet-confirmed event with a probability and a predicted trigger
    /// window. This is what drives PREFETCH: while probability is high the candidate cache
    /// prepares reactions, so when the real event confirms the response is immediate.
    /// </summary>
    public readonly struct PotentialEvent
    {
        public readonly long CorrelationId;
        public readonly EventType Type;
        public readonly float Probability;
        public readonly double PredictedTriggerTime;
        public readonly string PrimaryParticipant;
        public readonly string PrimaryObject;

        public PotentialEvent(
            long correlationId,
            EventType type,
            float probability,
            double predictedTriggerTime,
            string primaryParticipant,
            string primaryObject)
        {
            CorrelationId = correlationId;
            Type = type;
            Probability = probability;
            PredictedTriggerTime = predictedTriggerTime;
            PrimaryParticipant = primaryParticipant;
            PrimaryObject = primaryObject;
        }
    }
}
