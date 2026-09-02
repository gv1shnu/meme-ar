using System.Collections.Generic;

namespace MemeAR.Events
{
    /// <summary>Catalog of scene events the pipeline can react to.</summary>
    public enum EventType
    {
        None = 0,
        PersonEntered,
        PersonLeft,
        ObjectAppeared,
        ObjectMoved,
        PersonReachedTowardObject,
        AttentionShift,
        PersonApproachedPerson,
        GroupAttentionConverged,
        SuddenMotion,
        GenericInterestingChange
    }

    /// <summary>
    /// Comedic lifecycle phase of an event. This models the SETUP -> ANTICIPATION ->
    /// TRIGGER -> (BEAT) -> PUNCHLINE structure. Anticipation lets candidates be prefetched
    /// BEFORE the trigger confirms, so trigger-to-render stays low; the optional comedic
    /// beat is added later by the timing engine, never by compute/model latency.
    /// </summary>
    public enum EventPhase
    {
        Setup = 0,
        Anticipation,
        Triggered,
        Resolved
    }

    /// <summary>
    /// A detected (or anticipated) scene event. Participants/objects are ephemeral entity
    /// IDs. Confidence is the detector's own estimate. Metadata is optional free-form
    /// numeric context (e.g. motion magnitude) that scoring may use.
    /// </summary>
    public sealed class SceneEvent
    {
        public long EventId;
        public EventType Type;
        public double Timestamp;
        public EventPhase Phase;
        public float Confidence;

        public readonly List<string> Participants = new List<string>(2);
        public readonly List<string> Objects = new List<string>(2);
        public readonly Dictionary<string, float> Metadata = new Dictionary<string, float>();

        /// <summary>Correlates an anticipation event with the trigger it precedes.</summary>
        public long CorrelationId;

        public SceneEvent() { }

        public SceneEvent(long eventId, EventType type, double timestamp, EventPhase phase, float confidence)
        {
            EventId = eventId;
            Type = type;
            Timestamp = timestamp;
            Phase = phase;
            Confidence = confidence;
        }

        public string PrimaryParticipant => Participants.Count > 0 ? Participants[0] : null;
        public string PrimaryObject => Objects.Count > 0 ? Objects[0] : null;

        public float GetMeta(string key, float fallback = 0f)
        {
            return Metadata.TryGetValue(key, out float v) ? v : fallback;
        }

        public override string ToString()
        {
            return $"#{EventId} {Type}/{Phase} conf={Confidence:0.00} p=[{string.Join(",", Participants)}] o=[{string.Join(",", Objects)}]";
        }
    }
}
