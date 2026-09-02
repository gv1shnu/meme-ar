using UnityEngine;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Temporal;

namespace MemeAR.Comedy
{
    /// <summary>
    /// Decides whether a confirmed event is worth reacting to. Not every recognized event
    /// should produce a meme: this stage combines a weighted positive score (confidence,
    /// novelty, movement, social/object relevance) with penalties (recent meme, duplicate
    /// event, cooldown) and applies hard gates (global cooldown, max simultaneous overlays).
    /// All weights and thresholds come from <see cref="MemeArConfig"/>.
    /// </summary>
    public sealed class ComedyOpportunityEngine
    {
        private readonly MemeArConfig _config;

        public ComedyOpportunityEngine(MemeArConfig config)
        {
            _config = config;
        }

        public OpportunityScore Evaluate(SceneEvent e, TemporalSceneBuffer buffer, double now)
        {
            SessionState session = buffer.Session;

            // --- Positive contributions ---
            float eventConfidence = _config.weightEventConfidence * e.Confidence;

            // Novelty: high if this type has not fired recently.
            int recentSameType = buffer.CountEventsOfType(e.Type, now, _config.eventMemorySeconds);
            float novelty = _config.weightNovelty * Mathf.Clamp01(1f - 0.25f * Mathf.Max(0, recentSameType - 1));

            float magnitude = Mathf.Max(e.GetMeta("magnitude"), Mathf.Max(e.GetMeta("speed"), e.GetMeta("shift")));
            float movement = _config.weightMovement * Mathf.Clamp01(magnitude);

            float social = _config.weightSocial * (e.Participants.Count >= 2 ? 1f : (e.Participants.Count == 1 ? 0.4f : 0f));
            float objectInteraction = _config.weightObjectInteraction * (e.Objects.Count >= 1 ? 1f : 0f);

            // --- Penalties ---
            double sinceLastMeme = now - session.LastMemeTimestamp;
            float recentMemePenalty = 0f;
            if (session.LastMemeTimestamp > double.NegativeInfinity && sinceLastMeme < _config.globalMemeCooldownSeconds * 2.0)
            {
                float t = Mathf.Clamp01((float)(sinceLastMeme / (_config.globalMemeCooldownSeconds * 2.0)));
                recentMemePenalty = _config.penaltyRecentMeme * (1f - t);
            }

            double sinceSameType = now - session.LastTriggerTime(e.Type);
            float duplicatePenalty = 0f;
            if (sinceSameType < _config.duplicateEventCooldownSeconds)
            {
                float t = Mathf.Clamp01((float)(sinceSameType / _config.duplicateEventCooldownSeconds));
                duplicatePenalty = _config.penaltyDuplicateEvent * (1f - t);
            }

            bool inGlobalCooldown = session.LastMemeTimestamp > double.NegativeInfinity &&
                                    sinceLastMeme < _config.globalMemeCooldownSeconds;
            float cooldownPenalty = inGlobalCooldown ? _config.penaltyCooldown : 0f;

            float total = eventConfidence + novelty + movement + social + objectInteraction
                          - recentMemePenalty - duplicatePenalty - cooldownPenalty;

            // --- Hard gates ---
            string reject = null;
            if (session.ActiveMemeCount >= _config.maxSimultaneousMemes)
            {
                reject = "max-simultaneous";
            }
            else if (inGlobalCooldown)
            {
                reject = "global-cooldown";
            }
            else if (total < _config.opportunityThreshold)
            {
                reject = "below-threshold";
            }

            bool accepted = reject == null;
            return new OpportunityScore(
                total, accepted, reject,
                eventConfidence, novelty, movement, social, objectInteraction,
                recentMemePenalty, duplicatePenalty, cooldownPenalty);
        }
    }
}
