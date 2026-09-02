namespace MemeAR.Rendering
{
    /// <summary>Entry/exit animation styles a reaction card can use.</summary>
    public enum AnimationStyle
    {
        Pop = 0,       // scale pop-in with a small overshoot
        Slide,         // slide in from a screen edge
        Fade,          // simple fade in/out
        Bounce         // pop-in plus a gentle settle bounce
    }

    /// <summary>How a placed overlay behaves relative to its target over its lifetime.</summary>
    public enum TrackingBehavior
    {
        Static = 0,        // stays where it spawned
        FollowTarget,      // re-reads the target's position each frame
        Billboard          // world card always faces the camera (position fixed)
    }
}
