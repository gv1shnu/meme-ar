using UnityEngine;
using MemeAR.Perception;

namespace MemeAR.Scene
{
    /// <summary>
    /// A person tracked across frames with an EPHEMERAL, session-local ID (P1, P2, ...).
    /// No identity, recognition, or persistence across sessions is performed or stored.
    /// All fields are observable signals; optional values are null when the corresponding
    /// perception module is not yet available.
    /// </summary>
    public sealed class TrackedPerson
    {
        public string Id;
        public Rect NormalizedBounds;
        public Vector3? WorldPosition;
        public Vector2 ScreenVelocity;           // normalized units / second
        public PoseState Pose = PoseState.Unknown;
        public Vector3 HeadDirection = Vector3.forward;
        public Vector3 GazeDirection = Vector3.forward;
        public FacialCueObservation FacialCues = FacialCueObservation.Neutral;
        public double LastSeen;
        public float Confidence;

        public Vector2 NormalizedCenter => NormalizedBounds.center;

        public void CopyFrom(TrackedPerson other)
        {
            Id = other.Id;
            NormalizedBounds = other.NormalizedBounds;
            WorldPosition = other.WorldPosition;
            ScreenVelocity = other.ScreenVelocity;
            Pose = other.Pose;
            HeadDirection = other.HeadDirection;
            GazeDirection = other.GazeDirection;
            FacialCues = other.FacialCues;
            LastSeen = other.LastSeen;
            Confidence = other.Confidence;
        }
    }

    /// <summary>A tracked object with an ephemeral session-local ID (O1, O2, ...).</summary>
    public sealed class TrackedObject
    {
        public string Id;
        public string Category;
        public Rect NormalizedBounds;
        public Vector3? WorldPosition;
        public Vector2 ScreenVelocity;
        public string NearestPersonId;
        public double LastSeen;
        public float Confidence;

        public Vector2 NormalizedCenter => NormalizedBounds.center;
    }
}
