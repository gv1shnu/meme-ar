using System.Collections.Generic;
using MemeAR.Scene;

namespace MemeAR.Perception
{
    /// <summary>
    /// Perception module boundary. These interfaces describe the ML/vision modules the
    /// product will eventually run on-device (person detection, pose, facial cues, object
    /// detection, gaze, depth). None are required to be functional for the MVP: the
    /// <see cref="MemeAR.Simulation"/> layer and future real implementations both produce
    /// the SAME <see cref="SceneSnapshot"/>, so everything downstream is model-agnostic.
    ///
    /// Each module reports the frequency it is designed to run at, so the scheduler can
    /// run heavy modules less often than the camera frame rate without changing callers.
    /// </summary>
    public interface IPerceptionModule
    {
        /// <summary>Intended update frequency in Hz (advisory; scheduler may throttle).</summary>
        float TargetHz { get; }

        /// <summary>Human-readable module name for the debug HUD.</summary>
        string Name { get; }

        /// <summary>True once the module is ready to produce observations.</summary>
        bool IsAvailable { get; }
    }

    public interface IPersonDetector : IPerceptionModule
    {
        void DetectPeople(PerceptionInput input, List<PersonObservation> output);
    }

    public interface IPersonTracker : IPerceptionModule
    {
        /// <summary>Associates raw detections with stable, ephemeral session IDs (P1, P2...).</summary>
        void Track(IReadOnlyList<PersonObservation> detections, List<TrackedPerson> tracked, double timestamp);
    }

    public interface IPoseEstimator : IPerceptionModule
    {
        PoseState EstimatePose(PersonObservation person, PerceptionInput input);
    }

    public interface IFacialCueEstimator : IPerceptionModule
    {
        FacialCueObservation EstimateCues(PersonObservation person, PerceptionInput input);
    }

    public interface IObjectDetector : IPerceptionModule
    {
        void DetectObjects(PerceptionInput input, List<ObjectObservation> output);
    }

    public interface IGazeEstimator : IPerceptionModule
    {
        UnityEngine.Vector3 EstimateGaze(PersonObservation person, PerceptionInput input);
    }

    public interface IDepthProvider : IPerceptionModule
    {
        /// <summary>Metric depth at a normalized screen point, or negative if unavailable.</summary>
        float SampleDepth(UnityEngine.Vector2 normalizedScreenPoint);
    }
}
