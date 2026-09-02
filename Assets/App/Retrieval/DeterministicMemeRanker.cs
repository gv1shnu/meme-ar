using System.Collections.Generic;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Temporal;

namespace MemeAR.Retrieval
{
    /// <summary>
    /// Deterministic, seedable ranker. Score combines: base weight, event-type specificity,
    /// tag relevance to the event, a recent-usage penalty (so the same asset does not repeat
    /// back-to-back), and a small seeded jitter for variety. Ties break on priority. Because
    /// the PRNG is seedable, tests get reproducible ordering while runtime stays varied.
    /// </summary>
    public sealed class DeterministicMemeRanker : IMemeRanker
    {
        private readonly DeterministicRandom _random;
        private readonly float _recentUsagePenalty;
        private readonly float _jitter;
        private readonly float _scenePackBias;

        public DeterministicMemeRanker(DeterministicRandom random, float recentUsagePenalty = 0.6f, float jitter = 0.15f, float scenePackBias = 1.25f)
        {
            _random = random;
            _recentUsagePenalty = recentUsagePenalty;
            _jitter = jitter;
            _scenePackBias = scenePackBias;
        }

        public void Rank(
            SceneEvent e,
            IReadOnlyList<MemeDefinition> retrieved,
            SessionState session,
            double now,
            List<MemeCandidate> output)
        {
            output.Clear();
            if (retrieved == null)
            {
                return;
            }

            for (int i = 0; i < retrieved.Count; i++)
            {
                MemeDefinition m = retrieved[i];
                if (m == null)
                {
                    continue;
                }

                float score = Mathf.Max(0.01f, m.weight);

                // Reward explicit event-type support (vs. wildcard memes).
                if (m.supportedEventTypes != null && m.supportedEventTypes.Count > 0)
                {
                    score += 0.5f;
                }

                // Tag relevance to the event type.
                score += 0.4f * TagRelevance(m, e.Type);

                // Scene consistency: boost memes from the pack the current scene locked onto.
                if (!string.IsNullOrEmpty(session.ScenePackId) && m.pack == session.ScenePackId)
                {
                    score += _scenePackBias;
                }

                // Recent usage penalty (per-meme cooldown window and simple recency).
                double sinceUse = now - session.LastUseTime(m.id);
                if (m.cooldownSeconds > 0f && sinceUse < m.cooldownSeconds)
                {
                    score -= _recentUsagePenalty * (1f - Mathf.Clamp01((float)(sinceUse / m.cooldownSeconds)));
                }

                // Seeded jitter for variety.
                score += _random.Range(0f, _jitter);

                // Priority nudges ties.
                score += m.priority * 0.001f;

                output.Add(new MemeCandidate(m, score));
            }

            output.Sort(ScoreDescending);
        }

        private static readonly System.Comparison<MemeCandidate> ScoreDescending =
            (a, b) => b.Score.CompareTo(a.Score);

        private static float TagRelevance(MemeDefinition m, EventType type)
        {
            switch (type)
            {
                case EventType.PersonReachedTowardObject:
                    return Match(m, MemeTags.Food, MemeTags.Awkward, MemeTags.SideEye);
                case EventType.GroupAttentionConverged:
                    return Match(m, MemeTags.Group, MemeTags.Attention);
                case EventType.SuddenMotion:
                    return Match(m, MemeTags.Surprise, MemeTags.Motion);
                case EventType.PersonEntered:
                    return Match(m, MemeTags.Entrance);
                case EventType.PersonLeft:
                    return Match(m, MemeTags.Exit);
                case EventType.AttentionShift:
                    return Match(m, MemeTags.Confusion, MemeTags.Attention);
                default:
                    return Match(m, MemeTags.Reaction);
            }
        }

        private static float Match(MemeDefinition m, params string[] tags)
        {
            int hits = 0;
            for (int i = 0; i < tags.Length; i++)
            {
                if (m.HasTag(tags[i]))
                {
                    hits++;
                }
            }

            return tags.Length == 0 ? 0f : (float)hits / tags.Length;
        }
    }
}
