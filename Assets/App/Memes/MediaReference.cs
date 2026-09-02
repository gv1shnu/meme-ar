using System;
using UnityEngine;
using UnityEngine.Video;
using MemeAR.Rendering;

namespace MemeAR.Memes
{
    /// <summary>
    /// Source-agnostic pointer to a reaction's visual media. It can resolve to a bundled
    /// VideoClip, a file under StreamingAssets (e.g. a user-imported clip), or a remote URL —
    /// so the content SOURCE (and therefore the licensing strategy) is a swappable decision,
    /// not baked into the pipeline. Loading is best-effort: if nothing resolves or a load
    /// fails, the renderer falls back to the dialogue text card.
    /// </summary>
    [Serializable]
    public sealed class MediaReference
    {
        public MediaKind kind = MediaKind.GeneratedCard;
        public BackgroundBlend backgroundBlend = BackgroundBlend.Opaque;

        [Tooltip("Chroma key color to remove when backgroundBlend = ChromaKey.")]
        public Color chromaKeyColor = Color.green;

        [Header("Visual source (first non-empty wins)")]
        public Sprite sprite;
        public VideoClip videoClip;
        [Tooltip("Path relative to Application.streamingAssetsPath, e.g. MemePacks/hype/clip1.mp4")]
        public string streamingAssetsPath;
        [Tooltip("Absolute URL. Loaded only when remote media is explicitly enabled.")]
        public string url;

        [Tooltip("Loop the clip for the reaction's duration instead of playing once.")]
        public bool loop = true;

        public bool HasResolvableSource =>
            kind == MediaKind.GeneratedCard
            || (kind == MediaKind.Sprite && sprite != null)
            || (kind == MediaKind.Video && (videoClip != null || !string.IsNullOrEmpty(streamingAssetsPath) || !string.IsNullOrEmpty(url)));
    }

    /// <summary>Optional audio track for a reaction. Volume is relative to the global master.</summary>
    [Serializable]
    public sealed class AudioReference
    {
        public AudioClip clip;
        [Tooltip("Path relative to Application.streamingAssetsPath for a standalone audio file.")]
        public string streamingAssetsPath;
        [Range(0f, 1f)] public float volume = 0.9f;

        [Tooltip("If true, the reaction's video supplies its own audio track (no separate clip).")]
        public bool useVideoAudio = true;

        public bool HasAudio => clip != null || !string.IsNullOrEmpty(streamingAssetsPath) || useVideoAudio;
    }
}
