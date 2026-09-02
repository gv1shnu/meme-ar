using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using MemeAR.Scene;

namespace MemeAR.AR
{
    /// <summary>
    /// The ONE place AR Foundation types are used. Wraps the AR session, camera and plane
    /// manager and exposes them through the engine-agnostic spatial/camera/observation
    /// interfaces so the rest of the app never references ARKit/ARCore/AR Foundation.
    ///
    /// For the MVP its observation output is camera pose + detected planes only (no people/
    /// objects), because on-device person/object perception models are a later milestone.
    /// The pipeline still runs end-to-end in live mode; scene population arrives when a real
    /// <see cref="MemeAR.Perception.IPersonDetector"/> etc. is installed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ARFoundationProvider : MonoBehaviour, ICameraSceneProvider, IARSpatialProvider, IObservationProvider
    {
        [Tooltip("The AR session. If null, one is searched for in the scene.")]
        public ARSession arSession;

        [Tooltip("AR camera manager (usually on the same camera). Optional.")]
        public ARCameraManager cameraManager;

        [Tooltip("AR plane manager for plane detection. Optional.")]
        public ARPlaneManager planeManager;

        private Camera _camera;
        private bool _running;
        private readonly SceneSnapshot _snapshot = new SceneSnapshot();

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (arSession == null)
            {
                arSession = FindObjectOfType<ARSession>();
            }

            if (cameraManager == null)
            {
                cameraManager = GetComponent<ARCameraManager>();
            }

            if (planeManager == null)
            {
                planeManager = FindObjectOfType<ARPlaneManager>();
            }
        }

        // ICameraSceneProvider
        public bool IsSessionReady => arSession != null && ARSession.state >= ARSessionState.SessionTracking;
        public float CameraVerticalFovDeg => _camera != null ? _camera.fieldOfView : 60f;
        public Vector2 ImageSize => new Vector2(Screen.width, Screen.height);

        public void StartSession()
        {
            if (arSession != null)
            {
                arSession.enabled = true;
            }

            if (planeManager != null)
            {
                planeManager.enabled = true;
            }
        }

        public void StopSession()
        {
            if (arSession != null)
            {
                arSession.enabled = false;
            }
        }

        // IARSpatialProvider
        public bool TrackingActive => IsSessionReady;
        public Pose CameraPose => _camera != null
            ? new Pose(_camera.transform.position, _camera.transform.rotation)
            : Pose.identity;

        public bool TryScreenToWorldRay(Vector2 normalizedScreenPoint, out Ray ray)
        {
            if (_camera == null)
            {
                ray = default;
                return false;
            }

            ray = _camera.ViewportPointToRay(new Vector3(normalizedScreenPoint.x, normalizedScreenPoint.y, 0f));
            return true;
        }

        public bool TryWorldToScreen(Vector3 worldPoint, out Vector2 normalizedScreenPoint)
        {
            if (_camera == null)
            {
                normalizedScreenPoint = new Vector2(0.5f, 0.5f);
                return false;
            }

            Vector3 vp = _camera.WorldToViewportPoint(worldPoint);
            normalizedScreenPoint = new Vector2(vp.x, vp.y);
            return vp.z > 0f;
        }

        public Pose CameraForwardPose(float distanceMeters)
        {
            Pose cam = CameraPose;
            Vector3 pos = cam.position + cam.rotation * Vector3.forward * distanceMeters;
            return new Pose(pos, cam.rotation);
        }

        public void GetPlaneHints(List<PlaneHint> output)
        {
            output.Clear();
            if (planeManager == null)
            {
                return;
            }

            foreach (ARPlane plane in planeManager.trackables)
            {
                output.Add(new PlaneHint(plane.transform.position, plane.normal, plane.extents));
            }
        }

        // IObservationProvider
        public string Name => "LiveAR";
        public bool IsRunning => _running;

        public void StartProvider()
        {
            _running = true;
            StartSession();
        }

        public void StopProvider()
        {
            _running = false;
        }

        public SceneSnapshot Tick(float deltaTime, double now)
        {
            if (!_running)
            {
                return null;
            }

            _snapshot.Reset();
            _snapshot.Timestamp = now;
            _snapshot.CameraPose = CameraPose;
            _snapshot.CameraVerticalFovDeg = CameraVerticalFovDeg;
            _snapshot.ImageSize = ImageSize;
            _snapshot.IsValid = IsSessionReady;
            _snapshot.Confidence = IsSessionReady ? 1f : 0f;

            GetPlaneHints(_snapshot.Planes);

            // People/objects intentionally empty until on-device perception modules land.
            return _snapshot;
        }
    }
}
