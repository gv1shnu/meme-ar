using UnityEngine;

namespace MemeAR.Rendering
{
    /// <summary>Global, per-session audio state for reaction clips, driven by config/HUD.</summary>
    public static class ReactionAudioState
    {
        public static bool Muted;
        public static float MasterVolume = 1f;

        public static float Effective(float clipVolume)
        {
            return Muted ? 0f : Mathf.Clamp01(clipVolume) * Mathf.Clamp01(MasterVolume);
        }
    }
}
