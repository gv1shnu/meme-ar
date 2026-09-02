using System.Collections.Generic;
using UnityEngine;
using MemeAR.AR;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Pipeline;
using MemeAR.Rendering;
using MemeAR.Scene;
using MemeAR.Simulation;

namespace MemeAR.AppRoot
{
    /// <summary>
    /// Composition root and per-frame driver. Owns the time source, the observation provider
    /// (simulated or live AR), the renderer and the pipeline, and ticks the pipeline at the
    /// configured observation rate on the Unity main thread. Switching mode swaps ONLY the
    /// observation + spatial provider and rebuilds the pipeline; nothing downstream changes.
    /// </summary>
    public sealed class AppController : MonoBehaviour
    {
        public enum Mode { Simulation, LiveAR }

        [Header("Configuration")]
        public MemeArConfig config;
        public MemeCatalog catalog;

        [Header("Scene references")]
        [Tooltip("Camera used for world<->screen projection. Defaults to Camera.main.")]
        public Camera arCamera;

        [Tooltip("Optional AR Foundation provider for LiveAR mode. Required to run live on device.")]
        public ARFoundationProvider arProvider;

        [Header("Runtime")]
        public Mode mode = Mode.Simulation;

        private ITimeSource _time;
        private StageProfiler _profiler;
        private MemeRenderer _renderer;

        private SimulatedSpatialProvider _simSpatial;
        private SimulatedObservationProvider _simProvider;

        private IObservationProvider _activeProvider;
        private IARSpatialProvider _activeSpatial;
        private MemePipeline _pipeline;
        private List<MemeDefinition> _catalog;

        private float _accumulator;

        public PipelineTelemetry Telemetry => _pipeline != null ? _pipeline.Telemetry : default;
        public StageProfiler Profiler => _profiler;
        public Mode CurrentMode => mode;
        public string CurrentScenarioName => _simProvider != null ? _simProvider.CurrentScenarioName : "-";
        public bool IsSimulation => mode == Mode.Simulation;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning("[AppController] No config assigned; creating defaults at runtime.");
                config = ScriptableObject.CreateInstance<MemeArConfig>();
            }

            if (arCamera == null)
            {
                arCamera = Camera.main;
            }

            _time = new UnityTimeSource();
            _profiler = new StageProfiler();

            _simSpatial = new SimulatedSpatialProvider(60f, new Vector2(Screen.width, Screen.height));
            _simProvider = new SimulatedObservationProvider(config, _simSpatial);

            _catalog = BuildCatalog();

            _renderer = GetComponent<MemeRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<MemeRenderer>();
            }

            mode = config.startInSimulation ? Mode.Simulation : Mode.LiveAR;
        }

        private void Start()
        {
            BuildForMode(mode);
        }

        private List<MemeDefinition> BuildCatalog()
        {
            var list = new List<MemeDefinition>();
            if (catalog != null && catalog.Memes != null && catalog.Memes.Count > 0)
            {
                list.AddRange(catalog.Memes);
            }
            else
            {
                list.AddRange(DefaultMemeCatalog.Build());
            }

            // Append any user-provided packs (their own clips + attribution).
            List<MemeDefinition> packs = MemePackLoader.LoadAll();
            if (packs.Count > 0)
            {
                Debug.Log($"[AppController] Loaded {packs.Count} meme(s) from user packs.");
                list.AddRange(packs);
            }

            return list;
        }

        private void BuildForMode(Mode target)
        {
            _pipeline?.Dispose();

            IReadOnlyList<MemeDefinition> catalogList = _catalog;

            if (target == Mode.LiveAR && arProvider != null)
            {
                _activeProvider = arProvider;
                _activeSpatial = arProvider;
            }
            else
            {
                if (target == Mode.LiveAR)
                {
                    Debug.LogWarning("[AppController] LiveAR requested but no ARFoundationProvider assigned; using Simulation.");
                    target = Mode.Simulation;
                }

                _activeProvider = _simProvider;
                _activeSpatial = _simSpatial;
            }

            mode = target;

            _renderer.Initialize(_time, _activeSpatial, config, arCamera);

            var assembled = PipelineFactory.Build(config, _activeSpatial, _renderer, catalogList, _profiler);
            _pipeline = assembled.Pipeline;

            _activeProvider.StartProvider();
            _accumulator = 0f;
        }

        public void SwitchMode(Mode target)
        {
            if (target == mode && _pipeline != null)
            {
                return;
            }

            _activeProvider?.StopProvider();
            _renderer.ClearAll();
            BuildForMode(target);
        }

        public void ToggleMode()
        {
            SwitchMode(mode == Mode.Simulation ? Mode.LiveAR : Mode.Simulation);
        }

        private void Update()
        {
            if (_pipeline == null || _activeProvider == null)
            {
                return;
            }

            float interval = config.ObservationInterval;
            _accumulator += Time.deltaTime;

            // Run at most a few observation ticks per frame to catch up without stalling.
            int guard = 0;
            while (interval > 0f && _accumulator >= interval && guard < 4)
            {
                _accumulator -= interval;
                guard++;
                TickOnce(interval);
            }

            if (interval <= 0f)
            {
                TickOnce(Time.deltaTime);
            }
        }

        private void TickOnce(float dt)
        {
            double now = _time.Now;
            SceneSnapshot snapshot = _activeProvider.Tick(dt, now);
            if (snapshot != null)
            {
                _pipeline.ProcessSnapshot(snapshot, now);
            }
        }

        private void OnDestroy()
        {
            _pipeline?.Dispose();
            _activeProvider?.StopProvider();
        }
    }
}
