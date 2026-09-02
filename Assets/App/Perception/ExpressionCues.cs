using UnityEngine;

namespace MemeAR.Perception
{
    /// <summary>
    /// Coarse direction a person's attention appears to point, relative to the camera.
    /// This is an OBSERVABLE cue, never a claim about intent or internal state.
    /// </summary>
    public enum AttentionDirection
    {
        Unknown = 0,
        TowardCamera,
        AwayFromCamera,
        Left,
        Right,
        Up,
        Down
    }

    /// <summary>
    /// Probabilistic facial cues. Deliberately named "cue" / "observation" rather than
    /// "emotion": these are surface signals (a smile-like mouth shape), not objective
    /// internal emotional state, and downstream code must treat them as such.
    ///
    /// No identity, demographic, or sensitive-characteristic inference is represented here.
    /// </summary>
    public readonly struct FacialCueObservation
    {
        public readonly float SmileProbability;
        public readonly float MouthOpenProbability;
        public readonly float BrowRaiseProbability;
        public readonly AttentionDirection Attention;
        public readonly float Confidence;

        public FacialCueObservation(
            float smileProbability,
            float mouthOpenProbability,
            float browRaiseProbability,
            AttentionDirection attention,
            float confidence)
        {
            SmileProbability = Mathf.Clamp01(smileProbability);
            MouthOpenProbability = Mathf.Clamp01(mouthOpenProbability);
            BrowRaiseProbability = Mathf.Clamp01(browRaiseProbability);
            Attention = attention;
            Confidence = Mathf.Clamp01(confidence);
        }

        public static FacialCueObservation Neutral =>
            new FacialCueObservation(0.1f, 0.1f, 0.1f, AttentionDirection.Unknown, 0f);
    }

    /// <summary>Coarse, observable body posture classification.</summary>
    public enum PoseState
    {
        Unknown = 0,
        Standing,
        Sitting,
        Reaching,
        Walking,
        Gesturing
    }
}
