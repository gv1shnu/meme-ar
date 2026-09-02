using UnityEngine;
using UnityEngine.UI;

namespace MemeAR.Rendering
{
    /// <summary>
    /// A reaction card (screen-space UI) built entirely in code — no prefab assets needed.
    /// Renders a generated animated card or a still sprite, the dialogue caption, and an
    /// attribution footer. This is also the text fallback the media view degrades to when a
    /// clip cannot load. Animation/lifetime come from <see cref="ReactionViewBase"/>.
    /// </summary>
    public sealed class ReactionCardView : ReactionViewBase
    {
        private Image _background;
        private Image _image;
        private Text _caption;
        private Text _footer;
        private Color _accent = Color.white;
        private bool _animatedCard;

        public static ReactionCardView Create(Transform parent, Font font)
        {
            var go = new GameObject("ReactionCard", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<ReactionCardView>();
            var rect = go.GetComponent<RectTransform>();
            view.InitBase(rect, go.GetComponent<CanvasGroup>());
            rect.sizeDelta = new Vector2(360f, 210f);

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

            view._caption = MakeText(go.transform, font, 34, TextAnchor.MiddleCenter);
            var capRect = view._caption.rectTransform;
            Stretch(capRect);
            capRect.offsetMin = new Vector2(16f, 34f);
            capRect.offsetMax = new Vector2(-16f, -16f);

            view._footer = MakeText(go.transform, font, 18, TextAnchor.LowerCenter);
            view._footer.color = new Color(1f, 1f, 1f, 0.6f);
            var footRect = view._footer.rectTransform;
            footRect.anchorMin = new Vector2(0f, 0f);
            footRect.anchorMax = new Vector2(1f, 0f);
            footRect.pivot = new Vector2(0.5f, 0f);
            footRect.offsetMin = new Vector2(12f, 8f);
            footRect.offsetMax = new Vector2(-12f, 30f);

            go.SetActive(false);
            return view;
        }

        public override void Configure(in MemeRenderInstruction instruction, Font font)
        {
            ConfigureBase(instruction);
            _accent = instruction.AccentColor;
            _animatedCard = instruction.MediaKind == MediaKind.GeneratedCard;

            _background.color = new Color(_accent.r, _accent.g, _accent.b, 0.92f);
            _caption.text = instruction.Caption;
            _footer.text = string.IsNullOrEmpty(instruction.Attribution) ? "" : instruction.Attribution;

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

        protected override void OnTick(float dt)
        {
            if (!_animatedCard)
            {
                return;
            }

            // Gentle animated shimmer so a card feels alive without any asset.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            float b = Mathf.Lerp(0.82f, 1f, pulse);
            _background.color = new Color(_accent.r * b, _accent.g * b, _accent.b * b, 0.92f);
        }

        private static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Color.white;
            return t;
        }
    }
}
