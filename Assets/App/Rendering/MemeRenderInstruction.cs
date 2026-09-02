using UnityEngine;
using MemeAR.Placement;

namespace MemeAR.Rendering
{
    public enum RenderTargetType
    {
        None = 0,
        Person,
        Object,
        World,
        Screen
    }

    /// <summary>
    /// A fully DECLARATIVE description of a reaction to render. The intelligence layers
    /// (director, timing, placement) never instantiate GameObjects — they emit one of these,
    /// and the renderer interprets it. This is also the exact shape a future remote service
    /// would return (see FUTURE_CLOUD_SCHEMA), which is why it is validated before use and
    /// carries no code, only data.
    /// </summary>
    public readonly struct MemeRenderInstruction
    {
        public readonly string MemeId;
        public readonly string Caption;
        public readonly Color AccentColor;
        public readonly Sprite Sprite;

        public readonly RenderTargetType TargetType;
        public readonly string TargetId;

        public readonly PlacementMode PlacementMode;
        public readonly Vector2 ScreenAnchor;    // normalized 0..1 (used for screen-space)
        public readonly Pose? WorldPose;         // used for world-space
        public readonly Vector3 Offset;
        public readonly float Scale;

        public readonly double ShowAt;
        public readonly float Duration;
        public readonly AnimationStyle AnimationStyle;
        public readonly bool FaceCamera;
        public readonly TrackingBehavior TrackingBehavior;

        public MemeRenderInstruction(
            string memeId, string caption, Color accentColor, Sprite sprite,
            RenderTargetType targetType, string targetId,
            PlacementMode placementMode, Vector2 screenAnchor, Pose? worldPose, Vector3 offset, float scale,
            double showAt, float duration, AnimationStyle animationStyle, bool faceCamera, TrackingBehavior trackingBehavior)
        {
            MemeId = memeId;
            Caption = caption;
            AccentColor = accentColor;
            Sprite = sprite;
            TargetType = targetType;
            TargetId = targetId;
            PlacementMode = placementMode;
            ScreenAnchor = screenAnchor;
            WorldPose = worldPose;
            Offset = offset;
            Scale = scale;
            ShowAt = showAt;
            Duration = duration;
            AnimationStyle = animationStyle;
            FaceCamera = faceCamera;
            TrackingBehavior = trackingBehavior;
        }
    }
}
