namespace MemeAR.Rendering
{
    /// <summary>What kind of visual a reaction renders. GeneratedCard needs no assets and is
    /// the always-available default; the rest reference bundled or user-provided media.</summary>
    public enum MediaKind
    {
        GeneratedCard = 0, // procedural animated card (no asset required)
        Sprite,            // a still image
        Video,             // a VideoClip / StreamingAssets file / URL, optionally with audio
    }

    /// <summary>
    /// How a media clip's own background is composited onto the live camera feed. ChromaKey
    /// and AlphaVideo let the clip's subject sit "in" the scene instead of on an opaque card;
    /// Opaque just shows the clip framed on the card.
    /// </summary>
    public enum BackgroundBlend
    {
        Opaque = 0,
        ChromaKey,   // key out a solid background color (e.g. green screen)
        AlphaVideo   // clip already carries an alpha channel
    }
}
