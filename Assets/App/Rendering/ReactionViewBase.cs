using UnityEngine;

namespace MemeAR.Rendering
{
    /// <summary>
    /// Shared lifecycle + animation for all reaction views (card and media). Owns the
    /// pop-in / hold / fade-out state machine, alpha, and scale, so subclasses only supply
    /// their visual content and per-frame position comes from the renderer. Pooled: views
    /// are shown/hidden and reconfigured, never Instantiate/Destroy'd at steady state.
    /// </summary>
    public abstract class ReactionViewBase : MonoBehaviour
    {
        protected enum Phase { Idle, PoppingIn, Holding, FadingOut }

        protected RectTransform Rect;
        protected CanvasGroup Group;

        private Phase _phase = Phase.Idle;
        private float _phaseTime;
        private float _popInSeconds = 0.18f;
        private float _fadeOutSeconds = 0.35f;
        private float _holdSeconds = 4f;
        private float _baseScale = 1f;
        private float _distanceScale = 1f;
        protected AnimationStyle Style = AnimationStyle.Pop;

        public bool IsActive => _phase != Phase.Idle;
        public string MemeId { get; private set; }

        protected void InitBase(RectTransform rect, CanvasGroup group)
        {
            Rect = rect;
            Group = group;
        }

        public abstract void Configure(in MemeRenderInstruction instruction, Font font);

        protected void ConfigureBase(in MemeRenderInstruction instruction)
        {
            MemeId = instruction.MemeId;
            Style = instruction.AnimationStyle;
            _holdSeconds = instruction.Duration;
            _baseScale = Mathf.Max(0.05f, instruction.Scale);
        }

        public void SetTimings(float popInSeconds, float fadeOutSeconds)
        {
            _popInSeconds = Mathf.Max(0.01f, popInSeconds);
            _fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        }

        public void SetScreenPosition(Vector2 pixelPosition, float distanceScale)
        {
            Rect.position = pixelPosition;
            _distanceScale = Mathf.Clamp(distanceScale, 0.4f, 2.5f);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _phase = Phase.PoppingIn;
            _phaseTime = 0f;
            Group.alpha = 0f;
            Rect.localScale = Vector3.one * (_baseScale * 0.6f);
            OnShown();
        }

        /// <summary>Advances animation/lifetime. Returns false when finished (recycle it).</summary>
        public bool Tick(float dt)
        {
            _phaseTime += dt;
            switch (_phase)
            {
                case Phase.PoppingIn:
                    TickPopIn();
                    break;
                case Phase.Holding:
                    TickHold();
                    break;
                case Phase.FadingOut:
                    return TickFadeOut();
            }

            OnTick(dt);
            return true;
        }

        private void TickPopIn()
        {
            float t = Mathf.Clamp01(_phaseTime / _popInSeconds);
            Group.alpha = t;

            float scale;
            if (Style == AnimationStyle.Pop || Style == AnimationStyle.Bounce)
            {
                float overshoot = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
                scale = Mathf.Lerp(0.6f, 1f, t) * overshoot;
            }
            else
            {
                scale = Mathf.Lerp(0.85f, 1f, t);
            }

            ApplyScale(scale);
            if (t >= 1f)
            {
                _phase = Phase.Holding;
                _phaseTime = 0f;
            }
        }

        private void TickHold()
        {
            Group.alpha = 1f;
            float settle = Style == AnimationStyle.Bounce ? 1f + 0.02f * Mathf.Sin(_phaseTime * 6f) : 1f;
            ApplyScale(settle);
            if (_phaseTime >= _holdSeconds)
            {
                _phase = Phase.FadingOut;
                _phaseTime = 0f;
            }
        }

        private bool TickFadeOut()
        {
            float t = Mathf.Clamp01(_phaseTime / _fadeOutSeconds);
            Group.alpha = 1f - t;
            ApplyScale(1f + 0.1f * t);
            if (t >= 1f)
            {
                Hide();
                return false;
            }

            return true;
        }

        protected void ApplyScale(float animScale)
        {
            Rect.localScale = Vector3.one * (_baseScale * animScale * _distanceScale);
        }

        public void RequestFadeOut()
        {
            if (_phase != Phase.FadingOut && _phase != Phase.Idle)
            {
                _phase = Phase.FadingOut;
                _phaseTime = 0f;
            }
        }

        public void Hide()
        {
            _phase = Phase.Idle;
            OnHidden();
            gameObject.SetActive(false);
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
        protected virtual void OnTick(float dt) { }

        protected static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
