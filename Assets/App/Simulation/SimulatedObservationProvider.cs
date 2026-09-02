using UnityEngine;
using MemeAR.AR;
using MemeAR.Infrastructure;
using MemeAR.Perception;
using MemeAR.Scene;

namespace MemeAR.Simulation
{
    /// <summary>
    /// Drives believable synthetic scene sequences (a person reaching for food, a group's
    /// attention converging, a sudden movement) so the ENTIRE pipeline can be validated with
    /// no camera and no ML models. Crucially it emits the same <see cref="SceneSnapshot"/>
    /// type a real perception stack would: the only thing that changes between demo and live
    /// is which provider is installed. There is no separate fake rendering path.
    /// </summary>
    public sealed class SimulatedObservationProvider : IObservationProvider
    {
        private readonly MemeArConfig _config;
        private readonly SimulatedSpatialProvider _spatial;
        private readonly SceneSnapshot _snapshot = new SceneSnapshot();

        private int _scenarioIndex;
        private double _scenarioStart;
        private bool _started;

        private static readonly string[] ScenarioNames = { "A: Snack Heist", "B: All Eyes", "C: Sudden Motion" };
        private static readonly float[] ScenarioDurations = { 7.0f, 6.0f, 5.0f };
        private const float ScenarioGap = 1.0f;

        public SimulatedObservationProvider(MemeArConfig config, SimulatedSpatialProvider spatial)
        {
            _config = config;
            _spatial = spatial;
        }

        public string Name => "Simulation";
        public bool IsRunning => _started;
        public string CurrentScenarioName => ScenarioNames[_scenarioIndex];
        public float ScenarioLocalTime { get; private set; }

        public void StartProvider()
        {
            _started = true;
            _scenarioIndex = 0;
            _scenarioStart = -1;
        }

        public void StopProvider()
        {
            _started = false;
        }

        public SceneSnapshot Tick(float deltaTime, double now)
        {
            if (!_started)
            {
                return null;
            }

            if (_scenarioStart < 0)
            {
                _scenarioStart = now;
            }

            float speed = Mathf.Max(0.1f, _config.simulationSpeed);
            float t = (float)((now - _scenarioStart) * speed);
            float total = ScenarioDurations[_scenarioIndex] + ScenarioGap;

            if (t >= total)
            {
                _scenarioIndex = (_scenarioIndex + 1) % ScenarioNames.Length;
                _scenarioStart = now;
                t = 0f;
            }

            ScenarioLocalTime = t;

            _snapshot.Reset();
            _snapshot.Timestamp = now;
            _snapshot.CameraPose = _spatial.CameraPose;
            _snapshot.CameraVerticalFovDeg = _spatial.CameraVerticalFovDeg;
            _snapshot.ImageSize = _spatial.ImageSize;

            switch (_scenarioIndex)
            {
                case 0: BuildSnackHeist(t); break;
                case 1: BuildAllEyes(t); break;
                default: BuildSuddenMotion(t); break;
            }

            return _snapshot;
        }

        // --- Scenario A: a person reaches toward another's food. ---
        private void BuildSnackHeist(float t)
        {
            Vector2 foodPos = new Vector2(0.7f, 0.42f);
            AddObject("O1", "food", foodPos, 0.9f);

            if (t < 1.0f)
            {
                return; // setup beat: only the object is on screen
            }

            // P1 slides in, then reaches toward the food, then contacts it.
            float reachStart = 3.0f;
            float reachEnd = 4.4f;
            float cx;
            Vector2 velocity = Vector2.zero;

            if (t < reachStart)
            {
                cx = 0.30f;
            }
            else if (t < reachEnd)
            {
                float k = Mathf.InverseLerp(reachStart, reachEnd, t);
                cx = Mathf.Lerp(0.30f, 0.62f, k);
                velocity = new Vector2((0.62f - 0.30f) / (reachEnd - reachStart), 0f);
            }
            else
            {
                cx = 0.62f; // in contact with the food
            }

            Vector2 center = new Vector2(cx, 0.4f);
            bool looking = t >= 2.0f;
            Vector3 head = looking ? ToHead(center, foodPos) : Vector3.up * 0.2f + Vector3.forward;
            var p = AddPerson("P1", center, new Vector2(0.16f, 0.32f), velocity, head, 0.92f);
            p.Pose = t >= reachStart ? PoseState.Reaching : PoseState.Standing;
            p.FacialCues = new FacialCueObservation(0.2f, t >= reachEnd ? 0.7f : 0.2f, 0.3f, AttentionDirection.Right, 0.6f);
        }

        // --- Scenario B: two people, attention converges on one object. ---
        private void BuildAllEyes(float t)
        {
            Vector2 objPos = new Vector2(0.5f, 0.5f);
            AddObject("O1", "phone", objPos, 0.85f);

            if (t < 1.0f)
            {
                return;
            }

            var p1 = AddPerson("P1", new Vector2(0.25f, 0.42f), new Vector2(0.15f, 0.3f), Vector2.zero,
                ToHead(new Vector2(0.25f, 0.42f), objPos), 0.9f);
            p1.FacialCues = new FacialCueObservation(0.4f, 0.3f, 0.5f, AttentionDirection.Right, 0.6f);

            var p2 = AddPerson("P2", new Vector2(0.75f, 0.42f), new Vector2(0.15f, 0.3f), Vector2.zero,
                ToHead(new Vector2(0.75f, 0.42f), objPos), 0.9f);
            p2.FacialCues = new FacialCueObservation(0.4f, 0.3f, 0.5f, AttentionDirection.Left, 0.6f);
        }

        // --- Scenario C: a sudden movement. ---
        private void BuildSuddenMotion(float t)
        {
            float spikeStart = 1.5f;
            float spikeEnd = 1.9f;

            Vector2 center;
            Vector2 velocity = Vector2.zero;
            if (t < spikeStart)
            {
                center = new Vector2(0.4f, 0.4f);
            }
            else if (t < spikeEnd)
            {
                float k = Mathf.InverseLerp(spikeStart, spikeEnd, t);
                center = new Vector2(Mathf.Lerp(0.4f, 0.62f, k), Mathf.Lerp(0.4f, 0.55f, k));
                velocity = new Vector2(1.1f, 0.6f); // exceeds the sudden-motion speed threshold
            }
            else
            {
                center = new Vector2(0.62f, 0.55f);
            }

            var p = AddPerson("P1", center, new Vector2(0.16f, 0.32f), velocity, Vector3.forward, 0.9f);
            p.Pose = PoseState.Gesturing;
        }

        // --- helpers ---
        private TrackedPerson AddPerson(string id, Vector2 center, Vector2 size, Vector2 velocity, Vector3 head, float confidence)
        {
            var rect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            var p = new TrackedPerson
            {
                Id = id,
                NormalizedBounds = rect,
                ScreenVelocity = velocity,
                HeadDirection = head.sqrMagnitude < 1e-4f ? Vector3.forward : head.normalized,
                GazeDirection = head.sqrMagnitude < 1e-4f ? Vector3.forward : head.normalized,
                WorldPosition = _spatial.ScreenToWorldAtDepth(center, 1.8f),
                LastSeen = _snapshot.Timestamp,
                Confidence = confidence
            };
            _snapshot.People.Add(p);
            return p;
        }

        private TrackedObject AddObject(string id, string category, Vector2 center, float confidence)
        {
            var size = new Vector2(0.12f, 0.1f);
            var rect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            var o = new TrackedObject
            {
                Id = id,
                Category = category,
                NormalizedBounds = rect,
                WorldPosition = _spatial.ScreenToWorldAtDepth(center, 1.5f),
                LastSeen = _snapshot.Timestamp,
                Confidence = confidence
            };
            _snapshot.Objects.Add(o);
            return o;
        }

        private static Vector3 ToHead(Vector2 from, Vector2 to)
        {
            Vector2 d = (to - from);
            if (d.sqrMagnitude < 1e-5f)
            {
                return Vector3.forward;
            }

            d.Normalize();
            // Encode the screen-space look direction in x/y, keep a forward bias in z.
            return new Vector3(d.x, d.y, 0.6f).normalized;
        }
    }
}
