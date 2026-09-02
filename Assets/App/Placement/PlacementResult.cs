using UnityEngine;
using MemeAR.Rendering;

namespace MemeAR.Placement
{
    /// <summary>Resolved placement, consumed by the AR director to fill a render instruction.</summary>
    public readonly struct PlacementResult
    {
        public readonly PlacementMode Mode;
        public readonly RenderTargetType TargetType;
        public readonly string TargetId;
        public readonly Vector2 ScreenAnchor;      // normalized 0..1
        public readonly Pose? WorldPose;
        public readonly Vector3 Offset;
        public readonly bool FaceCamera;
        public readonly TrackingBehavior TrackingBehavior;

        public PlacementResult(
            PlacementMode mode, RenderTargetType targetType, string targetId,
            Vector2 screenAnchor, Pose? worldPose, Vector3 offset,
            bool faceCamera, TrackingBehavior trackingBehavior)
        {
            Mode = mode;
            TargetType = targetType;
            TargetId = targetId;
            ScreenAnchor = screenAnchor;
            WorldPose = worldPose;
            Offset = offset;
            FaceCamera = faceCamera;
            TrackingBehavior = trackingBehavior;
        }
    }
}
