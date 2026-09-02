using System.Collections.Generic;
using UnityEngine;
using MemeAR.AR;
using MemeAR.Scene;

namespace MemeAR.Simulation
{
    /// <summary>
    /// A pure-math virtual camera used in Simulation mode so world placement, projection and
    /// the camera-forward fallback all exercise the SAME code paths the AR Foundation
    /// provider will. No device required: it implements a simple pinhole model with a fixed
    /// identity camera pose.
    /// </summary>
    public sealed class SimulatedSpatialProvider : IARSpatialProvider, ICameraSceneProvider
    {
        private readonly float _fovVDeg;
        private readonly Vector2 _imageSize;

        public SimulatedSpatialProvider(float fovVDeg = 60f, Vector2? imageSize = null)
        {
            _fovVDeg = fovVDeg;
            _imageSize = imageSize ?? new Vector2(1080f, 1920f);
        }

        // ICameraSceneProvider
        public bool IsSessionReady => true;
        public float CameraVerticalFovDeg => _fovVDeg;
        public Vector2 ImageSize => _imageSize;
        public void StartSession() { }
        public void StopSession() { }

        // IARSpatialProvider
        public bool TrackingActive => true;
        public Pose CameraPose => Pose.identity;

        private float Aspect => _imageSize.x / Mathf.Max(1f, _imageSize.y);
        private float TanHalfFov => Mathf.Tan(_fovVDeg * 0.5f * Mathf.Deg2Rad);

        public bool TryScreenToWorldRay(Vector2 normalizedScreenPoint, out Ray ray)
        {
            Vector3 dir = DirForNormalized(normalizedScreenPoint);
            ray = new Ray(CameraPose.position, (CameraPose.rotation * dir).normalized);
            return true;
        }

        public bool TryWorldToScreen(Vector3 worldPoint, out Vector2 normalizedScreenPoint)
        {
            Vector3 camRel = Quaternion.Inverse(CameraPose.rotation) * (worldPoint - CameraPose.position);
            if (camRel.z <= 0.0001f)
            {
                normalizedScreenPoint = new Vector2(0.5f, 0.5f);
                return false;
            }

            float ndcY = camRel.y / (camRel.z * TanHalfFov);
            float ndcX = camRel.x / (camRel.z * TanHalfFov * Aspect);
            normalizedScreenPoint = new Vector2((ndcX + 1f) * 0.5f, (ndcY + 1f) * 0.5f);
            return true;
        }

        public Pose CameraForwardPose(float distanceMeters)
        {
            Vector3 pos = CameraPose.position + CameraPose.rotation * Vector3.forward * distanceMeters;
            return new Pose(pos, CameraPose.rotation);
        }

        public void GetPlaneHints(List<PlaneHint> output)
        {
            output.Clear();
            // A single synthetic floor plane 1.2 m below the camera.
            output.Add(new PlaneHint(new Vector3(0f, -1.2f, 1.5f), Vector3.up, new Vector2(3f, 3f)));
        }

        /// <summary>Places a normalized screen point at a metric depth in world space.</summary>
        public Vector3 ScreenToWorldAtDepth(Vector2 normalizedScreenPoint, float depthMeters)
        {
            Vector3 dir = DirForNormalized(normalizedScreenPoint); // camera-space, z = 1 plane
            Vector3 camSpace = dir * depthMeters;                  // planar depth
            return CameraPose.position + CameraPose.rotation * camSpace;
        }

        private Vector3 DirForNormalized(Vector2 n)
        {
            float ndcX = n.x * 2f - 1f;
            float ndcY = n.y * 2f - 1f;
            return new Vector3(ndcX * TanHalfFov * Aspect, ndcY * TanHalfFov, 1f);
        }
    }
}
