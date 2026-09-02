using MemeAR.Comedy;
using MemeAR.Events;

namespace MemeAR.Pipeline
{
    /// <summary>
    /// Snapshot of pipeline state for the debug HUD. Plain data, updated each tick. Latency
    /// fields separate COMPUTATIONAL latency (trigger-to-visible, stage timings) from the
    /// deliberate COMEDIC delay, so the two are never confused when tuning.
    /// </summary>
    public struct PipelineTelemetry
    {
        public int PeopleCount;
        public int ObjectCount;
        public int RelationCount;
        public int PredictedEventCount;
        public int PreparedCandidateSets;

        public EventType LastConfirmedEvent;
        public double LastConfirmedEventTime;
        public float LastOpportunityScore;
        public bool LastOpportunityAccepted;
        public string LastRejectReason;

        public string LastSelectedMemeId;
        public string ScenePack;
        public float LastComedicDelayMs;
        public double LastTriggerToVisibleMs;

        public int ActiveMemes;
        public int TotalMemesShown;
        public double GlobalCooldownRemaining;

        public double ObservationRateHz;
    }
}
