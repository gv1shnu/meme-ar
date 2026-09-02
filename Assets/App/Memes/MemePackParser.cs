using System.Collections.Generic;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Placement;
using MemeAR.Rendering;

namespace MemeAR.Memes
{
    /// <summary>
    /// Pure translation of a pack manifest (JSON) into runtime <see cref="MemeDefinition"/>s.
    /// Kept free of file IO so it is unit-testable. Each entry becomes a Video-kind meme whose
    /// clip is resolved from StreamingAssets, with the supplied attribution rendered in the
    /// footer. Unknown enum values fall back to safe defaults rather than throwing.
    /// </summary>
    public static class MemePackParser
    {
        public static List<MemeDefinition> Parse(string json, string streamingSubfolder = null)
        {
            var result = new List<MemeDefinition>();
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }

            MemePackManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<MemePackManifest>(json);
            }
            catch
            {
                return result;
            }

            if (manifest == null || manifest.memes == null)
            {
                return result;
            }

            for (int i = 0; i < manifest.memes.Length; i++)
            {
                MemePackEntry e = manifest.memes[i];
                if (e == null || string.IsNullOrEmpty(e.id) || string.IsNullOrEmpty(e.clip))
                {
                    continue;
                }

                var m = ScriptableObject.CreateInstance<MemeDefinition>();
                m.id = e.id;
                m.title = string.IsNullOrEmpty(e.title) ? e.id : e.title;
                m.pack = string.IsNullOrEmpty(manifest.pack) ? "custom" : manifest.pack;
                m.attribution = e.source ?? "";
                m.weight = e.weight <= 0f ? 1f : e.weight;
                m.minPeople = Mathf.Max(0, e.minPeople);
                m.maxPeople = Mathf.Max(0, e.maxPeople);
                m.preferredDurationSeconds = e.durationSec > 0f ? e.durationSec : 4f;
                m.preferredComedicDelayMs = new Vector2(
                    Mathf.Max(0f, e.delayMinMs),
                    Mathf.Max(e.delayMinMs, e.delayMaxMs));
                m.placementPolicy = ParsePlacement(e.placement);

                m.media = new MediaReference
                {
                    kind = MediaKind.Video,
                    backgroundBlend = ParseBlend(e.blend),
                    streamingAssetsPath = ResolveClipPath(streamingSubfolder, e.clip),
                    loop = true
                };
                m.audio = new AudioReference
                {
                    useVideoAudio = string.IsNullOrEmpty(e.audio),
                    streamingAssetsPath = e.audio,
                    volume = 0.9f
                };

                m.tags = new List<string>();
                if (e.tags != null)
                {
                    m.tags.AddRange(e.tags);
                }

                m.supportedEventTypes = new List<EventType>();
                if (e.events != null)
                {
                    for (int k = 0; k < e.events.Length; k++)
                    {
                        if (System.Enum.TryParse(e.events[k], true, out EventType type))
                        {
                            m.supportedEventTypes.Add(type);
                        }
                    }
                }

                m.captionTemplates = new List<string>();
                if (e.captions != null)
                {
                    m.captionTemplates.AddRange(e.captions);
                }

                result.Add(m);
            }

            return result;
        }

        private static string ResolveClipPath(string subfolder, string clip)
        {
            if (clip.Contains("/") || string.IsNullOrEmpty(subfolder))
            {
                return clip;
            }

            return subfolder.TrimEnd('/') + "/" + clip;
        }

        private static BackgroundBlend ParseBlend(string blend)
        {
            switch (blend)
            {
                case "chroma": return BackgroundBlend.ChromaKey;
                case "alpha": return BackgroundBlend.AlphaVideo;
                default: return BackgroundBlend.Opaque;
            }
        }

        private static PlacementPolicy ParsePlacement(string placement)
        {
            switch (placement)
            {
                case "person":
                case "above_person": return PlacementPolicy.PreferAbovePerson;
                case "object":
                case "above_object": return PlacementPolicy.PreferAboveObject;
                case "screen": return PlacementPolicy.PreferScreenSpace;
                default: return PlacementPolicy.Auto;
            }
        }
    }
}
