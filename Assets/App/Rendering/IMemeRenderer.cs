using System;

namespace MemeAR.Rendering
{
    /// <summary>
    /// The rendering boundary the pipeline talks to. Kept as an interface so the core
    /// pipeline can run headless (tests, CI) with a mock renderer, while the device uses the
    /// pooled MonoBehaviour renderer. The pipeline hands over declarative instructions and
    /// never touches GameObjects.
    /// </summary>
    public interface IMemeRenderer
    {
        int ActiveCount { get; }

        /// <summary>Schedules an instruction. It becomes visible at instruction.ShowAt.</summary>
        void Schedule(in MemeRenderInstruction instruction, double triggerTime);

        /// <summary>Raised the frame a card actually becomes visible: (memeId, triggerToVisibleMs).</summary>
        event Action<string, double> CardShown;

        /// <summary>Raised when a card finishes and is recycled: (memeId).</summary>
        event Action<string> CardFinished;
    }
}
