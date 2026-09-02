using UnityEngine;
using MemeAR.Memes;
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

        // Media (video/audio). Media/Audio may be null (e.g. remote or text-only reactions),
        // in which case the renderer draws a generated/sprite card with the dialogue text.
        public readonly MediaKind MediaKind;
        public readonly MediaReference Media;
        public readonly AudioReference Audio;
        public readonly string Attribution;

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
            double showAt, float duration, AnimationStyle animationStyle, bool faceCamera, TrackingBehavior trackingBehavior,
            MediaKind mediaKind = MediaKind.GeneratedCard, MediaReference media = null, AudioReference audio = null, string attribution = null)
        {
            MemeId = memeId;
            Caption = caption;
            AccentColor = accentColor;
            Sprite = sprite;
            MediaKind = mediaKind;
            Media = media;
            Audio = audio;
            Attribution = attribution;
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
