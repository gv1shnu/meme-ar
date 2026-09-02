using UnityEngine;

namespace MemeAR.Infrastructure
{
    /// <summary>
    /// Production time source backed by the engine clock. Uses unscaled realtime so
    /// comedic timing is measured in wall-clock milliseconds and is not affected by
    /// Time.timeScale changes.
    /// </summary>
    public sealed class UnityTimeSource : ITimeSource
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
    }
}
