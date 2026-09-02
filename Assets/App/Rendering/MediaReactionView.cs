using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using MemeAR.Memes;

namespace MemeAR.Rendering
{
    /// <summary>
    /// A reaction that plays a video clip (optionally with audio) composited onto the live
    /// camera feed, with the clip's background keyed out when requested. If the clip cannot
    /// be resolved or fails to load within a timeout, it degrades gracefully to the dialogue
    /// text card (same footer + caption). Playback starts only once the object is active, so
    /// the VideoPlayer prepares correctly. Pooled and reused.
    /// </summary>
    public sealed class MediaReactionView : ReactionViewBase
    {
        private Image _background;
        private RawImage _video;
        private Text _caption;
        private Text _footer;

        private VideoPlayer _player;
        private AudioSource _audio;
        private RenderTexture _renderTexture;
        private Material _chromaMaterial;

        private MediaReference _media;
        private AudioReference _audioRef;
        private Color _accent = Color.white;
        private float _loadTimeout = 2.5f;
        private float _elapsed;
        private bool _started;
        private bool _playing;
        private bool _failed;

        public static MediaReactionView Create(Transform parent, Font font, Shader chromaShader)
        {
            var go = new GameObject("MediaReaction", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<MediaReactionView>();
            var rect = go.GetComponent<RectTransform>();
            view.InitBase(rect, go.GetComponent<CanvasGroup>());
            rect.sizeDelta = new Vector2(420f, 300f);

            var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            view._background = bg.GetComponent<Image>();
            view._background.color = new Color(0.06f, 0.06f, 0.08f, 0.85f);
            Stretch(bg.GetComponent<RectTransform>());

            var vid = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            vid.transform.SetParent(go.transform, false);
            view._video = vid.GetComponent<RawImage>();
            Stretch(view._video.rectTransform);
            view._video.rectTransform.offsetMin = new Vector2(8f, 34f);
            view._video.rectTransform.offsetMax = new Vector2(-8f, -8f);
            view._video.enabled = false;

            view._caption = MakeText(go.transform, font, 32, TextAnchor.MiddleCenter);
            Stretch(view._caption.rectTransform);
            view._caption.rectTransform.offsetMin = new Vector2(16f, 34f);
            view._caption.rectTransform.offsetMax = new Vector2(-16f, -16f);

            view._footer = MakeText(go.transform, font, 18, TextAnchor.LowerCenter);
            view._footer.color = new Color(1f, 1f, 1f, 0.65f);
            var footRect = view._footer.rectTransform;
            footRect.anchorMin = new Vector2(0f, 0f);
            footRect.anchorMax = new Vector2(1f, 0f);
            footRect.pivot = new Vector2(0.5f, 0f);
            footRect.offsetMin = new Vector2(12f, 8f);
            footRect.offsetMax = new Vector2(-12f, 30f);

            view._renderTexture = new RenderTexture(640, 640, 0, RenderTextureFormat.ARGB32);
            view._renderTexture.Create();

            view._player = go.AddComponent<VideoPlayer>();
            view._player.playOnAwake = false;
            view._player.renderMode = VideoRenderMode.RenderTexture;
            view._player.targetTexture = view._renderTexture;
            view._player.errorReceived += view.OnVideoError;

            view._audio = go.AddComponent<AudioSource>();
            view._audio.playOnAwake = false;
            view._audio.spatialBlend = 0f;

            if (chromaShader != null)
            {
                view._chromaMaterial = new Material(chromaShader);
            }

            go.SetActive(false);
            return view;
        }

        public void SetLoadTimeout(float seconds)
        {
            _loadTimeout = Mathf.Max(0.2f, seconds);
        }

        public override void Configure(in MemeRenderInstruction instruction, Font font)
        {
            ConfigureBase(instruction);
            _media = instruction.Media;
            _audioRef = instruction.Audio;
            _accent = instruction.AccentColor;

            _caption.text = instruction.Caption;
            _footer.text = string.IsNullOrEmpty(instruction.Attribution) ? "" : instruction.Attribution;
            _background.color = new Color(_accent.r * 0.25f, _accent.g * 0.25f, _accent.b * 0.25f, 0.85f);

            // Reset playback state; show caption until the clip is ready.
            _started = false;
            _playing = false;
            _failed = false;
            _elapsed = 0f;
            _video.enabled = false;
            _caption.enabled = true;
        }

        protected override void OnShown()
        {
            // VideoPlayer only prepares while the GameObject is active.
            StartPlayback();
        }

        private void StartPlayback()
        {
            if (_media == null || _media.kind != MediaKind.Video || !_media.HasResolvableSource)
            {
                _failed = true; // nothing to play -> stay on the text card
                return;
            }

            _player.isLooping = _media.loop;

            if (_media.videoClip != null)
            {
                _player.source = VideoSource.VideoClip;
                _player.clip = _media.videoClip;
            }
            else
            {
                _player.source = VideoSource.Url;
                _player.url = ResolveUrl(_media);
            }

            ConfigureAudio();
            ApplyChroma();

            _started = true;
            _player.Prepare();
        }

        private void ConfigureAudio()
        {
            bool useVideoAudio = _audioRef == null || _audioRef.useVideoAudio;
            float vol = ReactionAudioState.Effective(_audioRef != null ? _audioRef.volume : 0.9f);

            if (useVideoAudio)
            {
                _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                _player.controlledAudioTrackCount = 1;
                _player.EnableAudioTrack(0, true);
                _player.SetTargetAudioSource(0, _audio);
                _audio.volume = vol;
            }
            else
            {
                _player.audioOutputMode = VideoAudioOutputMode.None;
                if (_audioRef != null && _audioRef.clip != null)
                {
                    _audio.clip = _audioRef.clip;
                    _audio.volume = vol;
                    _audio.loop = _media.loop;
                    _audio.Play();
                }
            }
        }

        private void ApplyChroma()
        {
            if (_media.backgroundBlend == BackgroundBlend.ChromaKey && _chromaMaterial != null)
            {
                _chromaMaterial.SetColor("_KeyColor", _media.chromaKeyColor);
                _video.material = _chromaMaterial;
            }
            else
            {
                _video.material = null; // default UI material (Opaque / AlphaVideo)
            }
        }

        protected override void OnTick(float dt)
        {
            if (_playing || _failed)
            {
                return;
            }

            _elapsed += dt;

            if (_started && _player.isPrepared && !_playing)
            {
                _video.texture = _renderTexture;
                _video.enabled = true;
                _caption.enabled = false; // clip carries the payload now
                _player.Play();
                _playing = true;
                return;
            }

            if (_elapsed >= _loadTimeout)
            {
                _failed = true; // fall back to the dialogue text card (already visible)
            }
        }

        protected override void OnHidden()
        {
            if (_player != null)
            {
                _player.Stop();
            }

            if (_audio != null)
            {
                _audio.Stop();
            }

            _playing = false;
            _started = false;
            _video.enabled = false;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            _failed = true;
            Debug.LogWarning($"[MediaReactionView] '{MemeId}' clip failed: {message} — using text fallback.");
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
            }
        }

        private static string ResolveUrl(MediaReference media)
        {
            if (!string.IsNullOrEmpty(media.url))
            {
                return media.url;
            }

            return System.IO.Path.Combine(Application.streamingAssetsPath, media.streamingAssetsPath);
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
