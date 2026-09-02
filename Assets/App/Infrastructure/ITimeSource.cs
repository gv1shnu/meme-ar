namespace MemeAR.Infrastructure
{
    /// <summary>
    /// Abstraction over "now" so the whole pipeline can be driven deterministically
    /// from tests and from the simulation clock, instead of reading UnityEngine.Time
    /// directly. Values are expressed in seconds since an arbitrary but stable epoch
    /// (typically session start).
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>Seconds since session start. Monotonic, non-decreasing.</summary>
        double Now { get; }
    }

    /// <summary>
    /// A time source whose value is advanced explicitly. Used by tests and by the
    /// simulation driver so timing decisions are fully reproducible.
    /// </summary>
    public sealed class ManualTimeSource : ITimeSource
    {
        public double Now { get; private set; }

        public ManualTimeSource(double start = 0.0)
        {
            Now = start;
        }

        public void Advance(double seconds)
        {
            if (seconds < 0.0)
            {
                return;
            }

            Now += seconds;
        }

        public void SetNow(double value)
        {
            if (value < Now)
            {
                return;
            }

            Now = value;
        }
    }
}
