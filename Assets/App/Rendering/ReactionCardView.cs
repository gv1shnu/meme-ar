using UnityEngine;
using UnityEngine.UI;

namespace MemeAR.Rendering
{
    /// <summary>
    /// A single reusable reaction card (screen-space UI). Built entirely in code so the MVP
    /// needs no prefab assets. Supports pop-in with a small overshoot, an optional settle
    /// bounce, a gentle idle float, and a fade-out. Position each frame is driven by the
    /// renderer (screen anchor, or a world pose projected to screen), so a single robust
    /// screen-space canvas can render both screen- and world-anchored reactions.
    ///
    /// Object-pooled: cards are shown/hidden and reconfigured, never Instantiate/Destroy'd
    /// during steady-state runtime.
    /// </summary>
    public sealed class ReactionCardView : MonoBehaviour
    {
        public enum Phase { Idle, PoppingIn, Holding, FadingOut }

        private RectTransform _rect;
        private CanvasGroup _group;
        private Image _background;
        private Image _image;
        private Text _caption;

        private Phase _phase = Phase.Idle;
        private float _phaseTime;
        private float _popInSeconds = 0.18f;
        private float _fadeOutSeconds = 0.35f;
        private float _holdSeconds = 4f;
        private AnimationStyle _style = AnimationStyle.Pop;
        private float _baseScale = 1f;

        public bool IsActive => _phase != Phase.Idle;
        public string MemeId { get; private set; }

        public static ReactionCardView Create(Transform parent, Font font)
        {
            var go = new GameObject("ReactionCard", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<ReactionCardView>();
            view._rect = go.GetComponent<RectTransform>();
            view._group = go.GetComponent<CanvasGroup>();
            view._rect.sizeDelta = new Vector2(360f, 200f);

            var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            view._background = bg.GetComponent<Image>();
            view._background.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            Stretch(bg.GetComponent<RectTransform>());

            var img = new GameObject("Image", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            view._image = img.GetComponent<Image>();
            view._image.preserveAspect = true;
            var imgRect = img.GetComponent<RectTransform>();
            imgRect.anchorMin = new Vector2(0.5f, 1f);
            imgRect.anchorMax = new Vector2(0.5f, 1f);
            imgRect.pivot = new Vector2(0.5f, 1f);
            imgRect.anchoredPosition = new Vector2(0f, -14f);
            imgRect.sizeDelta = new Vector2(120f, 120f);
            view._image.enabled = false;

            var cap = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            cap.transform.SetParent(go.transform, false);
            view._caption = cap.GetComponent<Text>();
            view._caption.font = font;
            view._caption.fontSize = 34;
            view._caption.alignment = TextAnchor.LowerCenter;
            view._caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            view._caption.verticalOverflow = VerticalWrapMode.Overflow;
            view._caption.color = Color.white;
            var capRect = cap.GetComponent<RectTransform>();
            Stretch(capRect);
            capRect.offsetMin = new Vector2(16f, 16f);
            capRect.offsetMax = new Vector2(-16f, -16f);

            go.SetActive(false);
            return view;
        }

        public void Configure(in MemeRenderInstruction instruction, Font font)
        {
            MemeId = instruction.MemeId;
            _style = instruction.AnimationStyle;
            _holdSeconds = instruction.Duration;
            _baseScale = Mathf.Max(0.05f, instruction.Scale);

            _background.color = new Color(instruction.AccentColor.r, instruction.AccentColor.g, instruction.AccentColor.b, 0.92f);
            _caption.text = instruction.Caption;

            if (instruction.Sprite != null)
            {
                _image.enabled = true;
                _image.sprite = instruction.Sprite;
            }
            else
            {
                _image.enabled = false;
            }
        }

        public void SetTimings(float popInSeconds, float fadeOutSeconds)
        {
            _popInSeconds = Mathf.Max(0.01f, popInSeconds);
            _fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _phase = Phase.PoppingIn;
            _phaseTime = 0f;
            _group.alpha = 0f;
            _rect.localScale = Vector3.one * (_baseScale * 0.6f);
        }

        public void SetScreenPosition(Vector2 pixelPosition, float distanceScale)
        {
            _rect.position = pixelPosition;
            // Distance scaling is applied on top of the animation scale in Tick().
            _distanceScale = Mathf.Clamp(distanceScale, 0.4f, 2.5f);
        }

        private float _distanceScale = 1f;

        /// <summary>Advances animation/lifetime. Returns false when the card is finished.</summary>
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

            return true;
        }

        private void TickPopIn()
        {
            float t = Mathf.Clamp01(_phaseTime / _popInSeconds);
            _group.alpha = t;

            float scale;
            if (_style == AnimationStyle.Pop || _style == AnimationStyle.Bounce)
            {
                // Overshoot then settle.
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
            _group.alpha = 1f;
            // Gentle settle bounce (scale only; screen position is owned by the renderer each frame).
            float settle = _style == AnimationStyle.Bounce ? 1f + 0.02f * Mathf.Sin(_phaseTime * 6f) : 1f;
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
            _group.alpha = 1f - t;
            ApplyScale(1f + 0.1f * t);
            if (t >= 1f)
            {
                Hide();
                return false;
            }

            return true;
        }

        private void ApplyScale(float animScale)
        {
            _rect.localScale = Vector3.one * (_baseScale * animScale * _distanceScale);
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
            gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
