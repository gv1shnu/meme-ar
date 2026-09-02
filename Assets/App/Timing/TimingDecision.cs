using MemeAR.Rendering;

namespace MemeAR.Timing
{
    /// <summary>Named comedic timing buckets. These are DELIBERATE delays, not compute latency.</summary>
    public enum TimingMode
    {
        Instant = 0,      // ~0-80 ms
        ShortBeat,        // ~100-300 ms
        DelayedReaction   // ~300-800 ms
    }

    /// <summary>
    /// The scheduling contract produced by the timing engine. The renderer/scheduler shows
    /// the reaction when <see cref="ShowAtTimestamp"/> is reached — it never sleeps or blocks
    /// a thread. If the triggering situation is invalidated before ShowAt, the reaction may
    /// be cancelled up to <see cref="CancellationDeadline"/>.
    /// </summary>
    public readonly struct TimingDecision
    {
        public readonly TimingMode Mode;
        public readonly double ShowAtTimestamp;
        public readonly float ComedicDelayMs;
        public readonly float DurationSeconds;
        public readonly AnimationStyle AnimationStyle;
        public readonly double CancellationDeadline;

        public TimingDecision(
            TimingMode mode,
            double showAtTimestamp,
            float comedicDelayMs,
            float durationSeconds,
            AnimationStyle animationStyle,
            double cancellationDeadline)
        {
            Mode = mode;
            ShowAtTimestamp = showAtTimestamp;
            ComedicDelayMs = comedicDelayMs;
            DurationSeconds = durationSeconds;
            AnimationStyle = animationStyle;
            CancellationDeadline = cancellationDeadline;
        }
    }
}
