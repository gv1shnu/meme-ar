using MemeAR.Scene;

namespace MemeAR.AR
{
    /// <summary>
    /// Produces <see cref="SceneSnapshot"/>s that drive the whole pipeline. There are two
    /// implementations — simulated and real perception — and they are the ONLY thing that
    /// differs between demo and live modes. Everything after the snapshot is shared.
    ///
    /// Pull-based and main-thread: the pipeline calls <see cref="Tick"/> at the configured
    /// observation rate. Heavy real perception can do its work on background threads and
    /// simply hand back the latest completed snapshot here.
    /// </summary>
    public interface IObservationProvider
    {
        string Name { get; }
        bool IsRunning { get; }

        void StartProvider();
        void StopProvider();

        /// <summary>
        /// Returns the latest scene snapshot for this tick, or null if none is available yet.
        /// The returned instance is owned by the provider and is cloned by the temporal buffer.
        /// </summary>
        SceneSnapshot Tick(float deltaTime, double now);
    }
}
