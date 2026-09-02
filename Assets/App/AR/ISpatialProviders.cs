using System.Collections.Generic;
using UnityEngine;
using MemeAR.Scene;

namespace MemeAR.AR
{
    /// <summary>
    /// Abstraction over the camera feed / AR session lifecycle. Meme logic must not touch AR
    /// Foundation directly; it goes through this and <see cref="IARSpatialProvider"/>. A
    /// simulated implementation and an AR Foundation implementation are interchangeable.
    /// </summary>
    public interface ICameraSceneProvider
    {
        bool IsSessionReady { get; }
        float CameraVerticalFovDeg { get; }
        Vector2 ImageSize { get; }

        void StartSession();
        void StopSession();
    }

    /// <summary>
    /// Provides spatial services: camera pose, screen<->world conversion, plane hints, and a
    /// safe camera-forward fallback pose. This is the ONLY seam through which placement and
    /// rendering obtain world geometry, so the AR backend can be swapped without touching
    /// downstream logic.
    /// </summary>
    public interface IARSpatialProvider
    {
        bool TrackingActive { get; }
        Pose CameraPose { get; }

        /// <summary>Normalized (0..1) screen point -> world ray. Returns false if unavailable.</summary>
        bool TryScreenToWorldRay(Vector2 normalizedScreenPoint, out Ray ray);

        /// <summary>Projects a world point to normalized (0..1) screen space; false if behind camera.</summary>
        bool TryWorldToScreen(Vector3 worldPoint, out Vector2 normalizedScreenPoint);

        /// <summary>A pose a fixed distance in front of the camera (used as a safe fallback).</summary>
        Pose CameraForwardPose(float distanceMeters);

        /// <summary>Current detected plane hints (may be empty).</summary>
        void GetPlaneHints(List<PlaneHint> output);
    }
}
