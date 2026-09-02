using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MemeAR.AppRoot;
using MemeAR.Capture;
using MemeAR.Infrastructure;
using MemeAR.Pipeline;

namespace MemeAR.DebugTools
{
    /// <summary>
    /// Toggleable developer HUD (IMGUI, no prefabs). Surfaces FPS, frame time, observation
    /// rate, scene counts, predicted/confirmed events, opportunity score, selected meme,
    /// comedic delay, trigger-to-visible latency, cooldown, active overlays, and per-stage
    /// compute timings. Toggle at runtime with the on-screen button or the configured key,
    /// and enable/disable by default via <see cref="MemeArConfig.debugHudEnabled"/> (no code
    /// change required).
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        public AppController controller;
        public ScreenshotService screenshots;
        public KeyCode toggleKey = KeyCode.H;

        private bool _visible = true;
        private float _fps;
        private float _frameMs;
        private readonly List<StageProfiler.StageSample> _stages = new List<StageProfiler.StageSample>(16);
        private readonly StringBuilder _sb = new StringBuilder(512);
        private GUIStyle _panel;
        private GUIStyle _label;

        private void Start()
        {
            if (controller == null)
            {
                controller = FindObjectOfType<AppController>();
            }

            if (controller != null && controller.config != null)
            {
                _visible = controller.config.debugHudEnabled;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _frameMs = dt * 1000f;
            _fps = Mathf.Lerp(_fps, dt > 0f ? 1f / dt : 0f, 0.1f);

            // Guard against projects configured for the new Input System only, where the
            // legacy Input API throws. The on-screen button remains available regardless.
            try
            {
                if (Input.GetKeyDown(toggleKey))
                {
                    _visible = !_visible;
                }
            }
            catch (System.InvalidOperationException)
            {
                // Legacy input disabled; ignore keyboard toggle.
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            float w = Mathf.Min(420f, Screen.width - 20f);
            var buttonRect = new Rect(10f, 10f, 120f, 34f);
            if (GUI.Button(buttonRect, _visible ? "Hide HUD" : "Show HUD"))
            {
                _visible = !_visible;
            }

            if (controller != null)
            {
                if (GUI.Button(new Rect(140f, 10f, 150f, 34f), $"Mode: {controller.CurrentMode}"))
                {
                    controller.ToggleMode();
                }

                if (screenshots != null && GUI.Button(new Rect(300f, 10f, 120f, 34f), "Screenshot"))
                {
                    screenshots.Capture();
                }
            }

            if (!_visible || controller == null)
            {
                return;
            }

            PipelineTelemetry t = controller.Telemetry;
            _sb.Clear();
            _sb.AppendLine($"<b>MemeAR Debug</b>   mode={controller.CurrentMode}");
            if (controller.IsSimulation)
            {
                _sb.AppendLine($"scenario: {controller.CurrentScenarioName}");
            }

            _sb.AppendLine($"FPS {_fps:00.0}   frame {_frameMs:00.0} ms   obs {t.ObservationRateHz:00.0} Hz");
            _sb.AppendLine($"people {t.PeopleCount}  objects {t.ObjectCount}  relations {t.RelationCount}");
            _sb.AppendLine($"predicted {t.PredictedEventCount}  prefetch-sets {t.PreparedCandidateSets}");
            _sb.AppendLine($"last event: {t.LastConfirmedEvent}");
            _sb.AppendLine($"opportunity: {t.LastOpportunityScore:0.00}  accepted={t.LastOpportunityAccepted}  {(string.IsNullOrEmpty(t.LastRejectReason) ? "" : "(" + t.LastRejectReason + ")")}");
            _sb.AppendLine($"selected meme: {t.LastSelectedMemeId}");
            _sb.AppendLine($"comedic delay: {t.LastComedicDelayMs:0} ms   trigger→visible: {t.LastTriggerToVisibleMs:0.0} ms");
            _sb.AppendLine($"active overlays: {t.ActiveMemes}   total shown: {t.TotalMemesShown}");
            _sb.AppendLine($"global cooldown: {t.GlobalCooldownRemaining:0.0} s");

            if (controller.Profiler != null)
            {
                controller.Profiler.CopyInto(_stages);
                _sb.AppendLine("<b>compute stages (last / max ms)</b>");
                for (int i = 0; i < _stages.Count; i++)
                {
                    _sb.AppendLine($"  {_stages[i].Stage,-16} {_stages[i].LastMilliseconds,6:0.00} / {_stages[i].MaxMilliseconds,6:0.00}");
                }
            }

            float h = _label.CalcHeight(new GUIContent(_sb.ToString()), w - 20f) + 20f;
            GUI.Box(new Rect(10f, 54f, w, h), GUIContent.none, _panel);
            GUI.Label(new Rect(20f, 62f, w - 20f, h), _sb.ToString(), _label);
        }

        private void EnsureStyles()
        {
            if (_panel == null)
            {
                _panel = new GUIStyle(GUI.skin.box);
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
                tex.Apply();
                _panel.normal.background = tex;
            }

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    fontSize = 15,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft
                };
                _label.normal.textColor = Color.white;
            }
        }
    }
}
