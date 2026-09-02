namespace MemeAR.Placement
{
    /// <summary>
    /// How a reaction overlay is anchored. When no perception-derived position is available
    /// the resolver falls back to a safe mode (camera-forward or screen-space) so a reaction
    /// always has somewhere to render.
    /// </summary>
    public enum PlacementMode
    {
        ScreenSpace = 0,
        WorldSpace,
        AbovePerson,
        NearPerson,
        AboveObject,
        NearObject,
        DetectedPlane,
        CameraForwardFallback
    }

    /// <summary>How the meme catalog prefers a definition to be placed (resolved later to a mode).</summary>
    public enum PlacementPolicy
    {
        Auto = 0,
        PreferScreenSpace,
        PreferAbovePerson,
        PreferAboveObject,
        PreferNearParticipant
    }
}
