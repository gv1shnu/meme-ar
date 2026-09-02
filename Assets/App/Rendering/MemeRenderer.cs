using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MemeAR.AR;
using MemeAR.Infrastructure;
using MemeAR.Placement;

namespace MemeAR.Rendering
{
    /// <summary>
    /// Pooled, screen-space reaction renderer. Schedules instructions and makes each visible
    /// exactly at its ShowAt timestamp (no thread sleeping). World-anchored instructions are
    /// projected to screen each frame via the spatial provider so they track the AR world;
    /// screen-anchored ones use their normalized anchor. Cards are pooled — no per-reaction
    /// Instantiate/Destroy at runtime.
    ///
    /// The renderer measures trigger-to-visible latency (computational latency), which is
    /// distinct from the deliberate comedic delay baked into ShowAt by the timing engine.
    /// </summary>
    public sealed class MemeRenderer : MonoBehaviour, IMemeRenderer
    {
        private sealed class Scheduled
        {
            public MemeRenderInstruction Instruction;
            public double TriggerTime;
            public double ShowAt;
            public ReactionViewBase View;
            public bool Shown;
        }

        public event Action<string, double> CardShown;
        public event Action<string> CardFinished;

        private ITimeSource _time;
        private IARSpatialProvider _spatial;
        private MemeArConfig _config;
        private Camera _camera;
        private Font _font;
        private Shader _chromaShader;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private readonly List<ReactionCardView> _cardPool = new List<ReactionCardView>(8);
        private readonly List<MediaReactionView> _mediaPool = new List<MediaReactionView>(4);
        private readonly List<Scheduled> _pending = new List<Scheduled>(8);
        private readonly List<Scheduled> _active = new List<Scheduled>(8);

        public int ActiveCount => _active.Count;
        public int PendingCount => _pending.Count;

        public void Initialize(ITimeSource time, IARSpatialProvider spatial, MemeArConfig config, Camera camera)
        {
            _time = time;
            _spatial = spatial;
            _config = config;
            _camera = camera;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _chromaShader = Shader.Find("MemeAR/UIChromaKey");

            ReactionAudioState.Muted = !config.enableAudio;
            ReactionAudioState.MasterVolume = config.masterVolume;

            EnsureCanvas();
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                return;
            }

            var go = new GameObject("MemeOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = go.GetComponent<RectTransform>();
        }

        public void Schedule(in MemeRenderInstruction instruction, double triggerTime)
        {
            if (!RenderInstructionValidator.Validate(instruction, out string reason))
            {
                Debug.LogWarning($"[MemeRenderer] Rejected instruction '{instruction.MemeId}': {reason}");
                return;
            }

            _pending.Add(new Scheduled
            {
                Instruction = instruction,
                TriggerTime = triggerTime,
                ShowAt = instruction.ShowAt,
                Shown = false
            });
        }

        private void Update()
        {
            if (_time == null)
            {
                return;
            }

            double now = _time.Now;
            float dt = Time.deltaTime;

            // Activate pending whose comedic delay has elapsed.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Scheduled s = _pending[i];
                if (now >= s.ShowAt)
                {
                    ActivateNow(s, now);
                    _pending.RemoveAt(i);
                }
            }

            // Update active cards.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Scheduled s = _active[i];
                PositionCard(s);
                if (!s.View.Tick(dt))
                {
                    ReturnToPool(s.View);
                    _active.RemoveAt(i);
                    CardFinished?.Invoke(s.Instruction.MemeId);
                }
            }
        }

        private void ActivateNow(Scheduled s, double now)
        {
            ReactionViewBase view = Rent(s.Instruction);
            view.Configure(s.Instruction, _font);
            view.SetTimings(_config.popInSeconds, _config.fadeOutSeconds);
            if (view is MediaReactionView media)
            {
                media.SetLoadTimeout(_config.mediaLoadTimeoutSeconds);
            }

            s.View = view;
            PositionCard(s);
            view.Show();
            s.Shown = true;
            _active.Add(s);

            double latencyMs = (now - s.TriggerTime) * 1000.0;
            CardShown?.Invoke(s.Instruction.MemeId, latencyMs);
        }

        private void PositionCard(Scheduled s)
        {
            Vector2 pixel;
            float distanceScale = 1f;

            bool positioned = false;
            if (s.Instruction.WorldPose.HasValue && _spatial != null)
            {
                Vector3 worldPos = s.Instruction.WorldPose.Value.position;
                if (_spatial.TryWorldToScreen(worldPos, out Vector2 normalized))
                {
                    pixel = new Vector2(normalized.x * Screen.width, normalized.y * Screen.height);
                    if (_camera != null)
                    {
                        float d = Vector3.Distance(_camera.transform.position, worldPos);
                        distanceScale = Mathf.Clamp(1.5f / Mathf.Max(0.4f, d), 0.4f, 2.0f);
                    }

                    s.View.SetScreenPosition(pixel, distanceScale);
                    positioned = true;
                }
            }

            if (!positioned)
            {
                Vector2 anchor = s.Instruction.ScreenAnchor;
                pixel = new Vector2(anchor.x * Screen.width, anchor.y * Screen.height);
                s.View.SetScreenPosition(pixel, 1f);
            }
        }

        private ReactionViewBase Rent(in MemeRenderInstruction instruction)
        {
            if (instruction.MediaKind == MediaKind.Video)
            {
                for (int i = 0; i < _mediaPool.Count; i++)
                {
                    if (!_mediaPool[i].IsActive)
                    {
                        return _mediaPool[i];
                    }
                }

                MediaReactionView media = MediaReactionView.Create(_canvasRect, _font, _chromaShader);
                _mediaPool.Add(media);
                return media;
            }

            for (int i = 0; i < _cardPool.Count; i++)
            {
                if (!_cardPool[i].IsActive)
                {
                    return _cardPool[i];
                }
            }

            ReactionCardView view = ReactionCardView.Create(_canvasRect, _font);
            _cardPool.Add(view);
            return view;
        }

        private void ReturnToPool(ReactionViewBase view)
        {
            view.Hide();
        }

        /// <summary>Immediately fades all active cards (used when switching modes or resetting).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].View.RequestFadeOut();
            }
        }
    }
}
