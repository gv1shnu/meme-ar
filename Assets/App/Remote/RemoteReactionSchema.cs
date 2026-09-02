using System;
using UnityEngine;

namespace MemeAR.Remote
{
    /// <summary>
    /// Compact declarative schema a future remote intelligence (VLM/LLM) would return. It is
    /// DATA ONLY: it names a bundled meme, a caption, a target, timing and animation. It can
    /// never carry code or cause arbitrary behavior — the local app validates it and maps it
    /// onto the same <see cref="MemeAR.Rendering.MemeRenderInstruction"/> the local pipeline
    /// produces. Networking is intentionally NOT implemented in the MVP; only the contract is.
    ///
    /// Serializable with Unity's JsonUtility. Example:
    /// {
    ///   "action":"spawn_meme","meme_id":"reaction_side_eye","caption":"...",
    ///   "target":{"type":"person","id":"P2","anchor":"above_head"},
    ///   "timing":{"mode":"short_beat","delay_ms":200},
    ///   "animation":"pop","duration_ms":5000,"confidence":0.87
    /// }
    /// </summary>
    [Serializable]
    public sealed class RemoteReactionInstruction
    {
        public string action;       // "spawn_meme"
        public string meme_id;
        public string caption;
        public RemoteTarget target;
        public RemoteTiming timing;
        public string animation;    // "pop" | "slide" | "fade" | "bounce"
        public int duration_ms = 4000;
        public float confidence;

        public static RemoteReactionInstruction FromJson(string json)
        {
            return JsonUtility.FromJson<RemoteReactionInstruction>(json);
        }
    }

    [Serializable]
    public sealed class RemoteTarget
    {
        public string type;   // "person" | "object" | "world" | "screen"
        public string id;     // ephemeral id (P1, O1...) or empty
        public string anchor; // "above_head" | "near" | "screen" ...
    }

    [Serializable]
    public sealed class RemoteTiming
    {
        public string mode;   // "instant" | "short_beat" | "delayed"
        public int delay_ms;
    }
}
