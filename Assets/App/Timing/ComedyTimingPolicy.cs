using UnityEngine;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;

namespace MemeAR.Timing
{
    /// <summary>
    /// Decides WHEN a selected reaction should appear. This is where comedic timing lives —
    /// and only here. The delay is a deliberate creative choice sampled from configured
    /// ranges; it is NEVER derived from model, network, or compute latency. The engine
    /// returns a target timestamp for the scheduler; it never sleeps or blocks.
    /// </summary>
    public interface IComedyTimingPolicy
    {
        TimingDecision Decide(SceneEvent e, MemeDefinition meme, float sceneActivity, double now);
    }

    /// <summary>
    /// Default policy: picks a timing mode from the meme's preferred delay and the event's
    /// urgency, samples a concrete delay within the mode's configured range (seeded for
    /// reproducibility), and computes the absolute show time.
    /// </summary>
    public sealed class ComedyTimingPolicy : IComedyTimingPolicy
    {
        private readonly MemeArConfig _config;
        private readonly DeterministicRandom _random;

        public ComedyTimingPolicy(MemeArConfig config, DeterministicRandom random)
        {
            _config = config;
            _random = random;
        }

        public TimingDecision Decide(SceneEvent e, MemeDefinition meme, float sceneActivity, double now)
        {
            TimingMode mode = ChooseMode(e, meme);
            Vector2 range = RangeFor(mode, meme);

            // A high-activity scene tightens timing slightly so reactions feel snappy.
            float activityBias = Mathf.Clamp01(sceneActivity);
            float delayMs = _random.Range(range.x, range.y);
            delayMs = Mathf.Lerp(delayMs, range.x, activityBias * 0.35f);

            double showAt = now + delayMs / 1000.0;
            float duration = meme != null && meme.preferredDurationSeconds > 0f
                ? meme.preferredDurationSeconds
                : _config.defaultMemeDurationSeconds;

            var style = meme != null ? meme.animationStyle : Rendering.AnimationStyle.Pop;

            // Reaction may be cancelled any time before it becomes visible.
            double cancelDeadline = showAt;

            return new TimingDecision(mode, showAt, delayMs, duration, style, cancelDeadline);
        }

        private TimingMode ChooseMode(SceneEvent e, MemeDefinition meme)
        {
            // Surprise-like, high-urgency events want an instant hit; social/awkward beats read
            // better with a short pause. Fall back to the meme's own preferred delay midpoint.
            switch (e.Type)
            {
                case EventType.SuddenMotion:
                case EventType.ObjectMoved:
                    return TimingMode.Instant;
                case EventType.PersonReachedTowardObject:
                case EventType.AttentionShift:
                    return TimingMode.ShortBeat;
                case EventType.GroupAttentionConverged:
                    return TimingMode.DelayedReaction;
            }

            if (meme != null)
            {
                float mid = (meme.preferredComedicDelayMs.x + meme.preferredComedicDelayMs.y) * 0.5f;
                if (mid < 90f)
                {
                    return TimingMode.Instant;
                }

                if (mid < 300f)
                {
                    return TimingMode.ShortBeat;
                }

                return TimingMode.DelayedReaction;
            }

            return TimingMode.ShortBeat;
        }

        private Vector2 RangeFor(TimingMode mode, MemeDefinition meme)
        {
            // Prefer the meme's authored range when it is non-degenerate; otherwise use config.
            if (meme != null && meme.preferredComedicDelayMs.y > meme.preferredComedicDelayMs.x)
            {
                return meme.preferredComedicDelayMs;
            }

            switch (mode)
            {
                case TimingMode.Instant:
                    return _config.instantDelayMs;
                case TimingMode.DelayedReaction:
                    return _config.delayedReactionMs;
                default:
                    return _config.shortBeatDelayMs;
            }
        }
    }
}
