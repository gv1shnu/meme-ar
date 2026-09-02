using UnityEngine;

namespace MemeAR.Infrastructure
{
    /// <summary>
    /// Central, data-driven tuning surface for the whole pipeline. Kept as a
    /// ScriptableObject so parameters can be adjusted in the editor without touching
    /// code, and so multiple config profiles (e.g. "demo", "aggressive") can coexist.
    ///
    /// Nothing in the runtime should hard-code these values; components read them here.
    /// </summary>
    [CreateAssetMenu(menuName = "MemeAR/Config", fileName = "MemeArConfig")]
    public sealed class MemeArConfig : ScriptableObject
    {
        [Header("Observation / Perception")]
        [Tooltip("How often the observation provider is polled for a new SceneSnapshot (Hz).")]
        [Range(5f, 60f)] public float observationHz = 30f;

        [Header("Temporal Memory")]
        [Tooltip("Seconds of per-frame snapshot history to retain.")]
        [Range(0.25f, 5f)] public float frameMemorySeconds = 1.0f;

        [Tooltip("Seconds of confirmed-event history to retain.")]
        [Range(5f, 60f)] public float eventMemorySeconds = 20f;

        [Header("Relations (heuristic thresholds)")]
        [Tooltip("Normalized screen distance under which two entities are considered 'near'.")]
        [Range(0.02f, 0.5f)] public float nearScreenDistance = 0.18f;

        [Tooltip("Dot-product threshold for a head/gaze vector to count as 'looking at'.")]
        [Range(0.5f, 0.99f)] public float lookingAtDot = 0.8f;

        [Tooltip("Approach speed (normalized screen units/sec) above which a reach is anticipated.")]
        [Range(0.01f, 1f)] public float reachApproachSpeed = 0.12f;

        [Header("Prediction")]
        [Tooltip("Minimum probability for a potential event to be published for prefetch.")]
        [Range(0f, 1f)] public float predictionPublishThreshold = 0.45f;

        [Tooltip("Default predicted trigger window, in milliseconds.")]
        [Range(100f, 2000f)] public float predictedTriggerWindowMs = 500f;

        [Header("Comedy Opportunity Scoring")]
        [Range(0f, 1f)] public float opportunityThreshold = 0.5f;
        [Range(0f, 2f)] public float weightEventConfidence = 1.0f;
        [Range(0f, 2f)] public float weightNovelty = 0.6f;
        [Range(0f, 2f)] public float weightMovement = 0.4f;
        [Range(0f, 2f)] public float weightSocial = 0.5f;
        [Range(0f, 2f)] public float weightObjectInteraction = 0.5f;
        [Range(0f, 2f)] public float penaltyRecentMeme = 0.7f;
        [Range(0f, 2f)] public float penaltyDuplicateEvent = 0.9f;
        [Range(0f, 2f)] public float penaltyCooldown = 1.0f;

        [Header("Cooldowns / Limits")]
        [Tooltip("Global minimum seconds between any two memes.")]
        [Range(0f, 30f)] public float globalMemeCooldownSeconds = 4.0f;

        [Tooltip("Minimum seconds before the same event type may trigger again.")]
        [Range(0f, 60f)] public float duplicateEventCooldownSeconds = 8.0f;

        [Tooltip("Maximum number of meme overlays visible at once.")]
        [Range(1, 8)] public int maxSimultaneousMemes = 3;

        [Header("Comedic Timing (milliseconds, NOT compute latency)")]
        [Tooltip("Instant reaction delay range.")]
        public Vector2 instantDelayMs = new Vector2(0f, 80f);

        [Tooltip("Short comedic beat delay range.")]
        public Vector2 shortBeatDelayMs = new Vector2(100f, 300f);

        [Tooltip("Delayed reaction delay range.")]
        public Vector2 delayedReactionMs = new Vector2(300f, 800f);

        [Header("Media (video/audio clips)")]
        [Tooltip("Master switch for reaction audio.")]
        public bool enableAudio = true;

        [Tooltip("Master volume applied to all reaction clip audio.")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;

        [Tooltip("Seconds to wait for a clip to load before falling back to the dialogue card.")]
        [Range(0.2f, 8f)] public float mediaLoadTimeoutSeconds = 2.5f;

        [Header("Scene consistency (meme packs)")]
        [Tooltip("A scene locks onto one meme pack; reactions from that pack are boosted.")]
        [Range(0f, 3f)] public float scenePackBias = 1.25f;

        [Tooltip("Seconds of inactivity after which the scene theme resets and a new pack may lock.")]
        [Range(2f, 60f)] public float sceneResetSeconds = 12f;

        [Header("Meme Presentation")]
        [Tooltip("Default visible duration for a reaction, in seconds.")]
        [Range(0.5f, 12f)] public float defaultMemeDurationSeconds = 4.0f;

        [Tooltip("Seconds for the pop-in animation.")]
        [Range(0.05f, 1f)] public float popInSeconds = 0.18f;

        [Tooltip("Seconds for the fade-out animation.")]
        [Range(0.05f, 1.5f)] public float fadeOutSeconds = 0.35f;

        [Header("Placement")]
        [Tooltip("Meters a world card is offset above a person's tracked position.")]
        [Range(0f, 1.5f)] public float abovePersonOffsetMeters = 0.35f;

        [Tooltip("Meters in front of the camera for the camera-forward fallback.")]
        [Range(0.3f, 4f)] public float cameraForwardDistanceMeters = 1.4f;

        [Header("Modes")]
        public bool startInSimulation = true;

        [Tooltip("Multiplier applied to simulation scenario playback speed.")]
        [Range(0.1f, 4f)] public float simulationSpeed = 1.0f;

        [Header("Determinism")]
        [Tooltip("Seed used for ranking jitter and simulation. 0 = time-based (non-deterministic).")]
        public uint randomSeed = 12345u;

        [Header("Debug")]
        public bool debugHudEnabled = true;

        public float ObservationInterval => observationHz <= 0f ? 0f : 1f / observationHz;
    }
}
