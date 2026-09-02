namespace MemeAR.Comedy
{
    /// <summary>Transparent breakdown of an opportunity decision, surfaced on the debug HUD.</summary>
    public readonly struct OpportunityScore
    {
        public readonly float Total;
        public readonly bool Accepted;
        public readonly string RejectReason;

        public readonly float EventConfidence;
        public readonly float Novelty;
        public readonly float Movement;
        public readonly float Social;
        public readonly float ObjectInteraction;
        public readonly float RecentMemePenalty;
        public readonly float DuplicatePenalty;
        public readonly float CooldownPenalty;

        public OpportunityScore(
            float total, bool accepted, string rejectReason,
            float eventConfidence, float novelty, float movement, float social, float objectInteraction,
            float recentMemePenalty, float duplicatePenalty, float cooldownPenalty)
        {
            Total = total;
            Accepted = accepted;
            RejectReason = rejectReason;
            EventConfidence = eventConfidence;
            Novelty = novelty;
            Movement = movement;
            Social = social;
            ObjectInteraction = objectInteraction;
            RecentMemePenalty = recentMemePenalty;
            DuplicatePenalty = duplicatePenalty;
            CooldownPenalty = cooldownPenalty;
        }
    }
}
