using UnityEngine;

namespace MemeAR.Perception
{
    /// <summary>
    /// The per-tick input handed to perception modules. Intentionally does NOT expose raw
    /// pixel buffers as a stored/persisted object: the MVP never retains or uploads camera
    /// frames. A real on-device module would read the transient camera texture here; the
    /// abstraction keeps that access behind a single, auditable boundary.
    /// </summary>
    public readonly struct PerceptionInput
    {
        /// <summary>World-space camera pose for this tick.</summary>
        public readonly Pose CameraPose;

        /// <summary>Camera vertical field of view in degrees (for screen/world math).</summary>
        public readonly float CameraVerticalFovDeg;

        /// <summary>Pixel dimensions of the current camera image / screen.</summary>
        public readonly Vector2 ImageSize;

        /// <summary>Seconds since session start.</summary>
        public readonly double Timestamp;

        public PerceptionInput(Pose cameraPose, float cameraVerticalFovDeg, Vector2 imageSize, double timestamp)
        {
            CameraPose = cameraPose;
            CameraVerticalFovDeg = cameraVerticalFovDeg;
            ImageSize = imageSize;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// A raw person detection before tracking assigns a stable ID. Bounding box is in
    /// normalized screen space (0..1, origin bottom-left) so it is resolution independent.
    /// </summary>
    public readonly struct PersonObservation
    {
        public readonly Rect NormalizedBounds;
        public readonly Vector3? WorldPosition;
        public readonly float Confidence;

        public PersonObservation(Rect normalizedBounds, Vector3? worldPosition, float confidence)
        {
            NormalizedBounds = normalizedBounds;
            WorldPosition = worldPosition;
            Confidence = Mathf.Clamp01(confidence);
        }

        public Vector2 NormalizedCenter => NormalizedBounds.center;
    }

    /// <summary>A raw object detection. Category is a coarse, non-sensitive class label.</summary>
    public readonly struct ObjectObservation
    {
        public readonly string Category;
        public readonly Rect NormalizedBounds;
        public readonly Vector3? WorldPosition;
        public readonly float Confidence;

        public ObjectObservation(string category, Rect normalizedBounds, Vector3? worldPosition, float confidence)
        {
            Category = category;
            NormalizedBounds = normalizedBounds;
            WorldPosition = worldPosition;
            Confidence = Mathf.Clamp01(confidence);
        }

        public Vector2 NormalizedCenter => NormalizedBounds.center;
    }
}
