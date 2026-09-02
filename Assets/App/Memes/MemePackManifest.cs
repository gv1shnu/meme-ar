using System;

namespace MemeAR.Memes
{
    /// <summary>
    /// JSON manifest describing a user-provided meme pack (a folder of clips under
    /// StreamingAssets/MemePacks). This is the source-agnostic content path: users add their
    /// own clips and supply the source/attribution, so the licensing responsibility sits with
    /// whoever provisions the pack — the app never bundles copyright-uncertain content itself.
    ///
    /// Example (MemePacks/hype/pack.json):
    /// {
    ///   "pack": "hype",
    ///   "memes": [
    ///     {
    ///       "id": "hype_wow", "title": "WOW", "clip": "wow.mp4",
    ///       "source": "Source: @creator (YouTube)", "blend": "chroma",
    ///       "tags": ["surprise","reaction"], "events": ["SuddenMotion"],
    ///       "captions": ["WOW"], "durationSec": 3.5
    ///     }
    ///   ]
    /// }
    /// </summary>
    [Serializable]
    public sealed class MemePackManifest
    {
        public string pack = "custom";
        public MemePackEntry[] memes;
    }

    [Serializable]
    public sealed class MemePackEntry
    {
        public string id;
        public string title;
        public string clip;        // file name (within the pack folder) or relative StreamingAssets path
        public string audio;       // optional separate audio file
        public string source;      // attribution text shown in the footer
        public string blend = "opaque"; // opaque | chroma | alpha
        public string placement = "auto";
        public string[] tags;
        public string[] events;    // EventType names
        public string[] captions;
        public float delayMinMs = 100f;
        public float delayMaxMs = 300f;
        public float durationSec = 4f;
        public int minPeople = 0;
        public int maxPeople = 0;
        public float weight = 1f;
    }
}
