using System.Collections.Generic;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Placement;
using MemeAR.Rendering;

namespace MemeAR.Memes
{
    /// <summary>
    /// Data-driven definition of a single reaction/meme, authored as a ScriptableObject so
    /// designers can add content without code. Assets referenced here are BUNDLED and local
    /// (placeholder reaction cards for the MVP) — the app never depends on remote or
    /// copyright-uncertain assets. Timing/placement here are PREFERENCES; the timing engine
    /// and placement resolver make the final decision from live scene context.
    /// </summary>
    [CreateAssetMenu(menuName = "MemeAR/Meme Definition", fileName = "Meme_")]
    public sealed class MemeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "meme_id";
        public string title = "Reaction";

        [Header("Presentation")]
        [Tooltip("Optional sprite. If null the renderer draws a generated placeholder card.")]
        public Sprite sprite;
        [Tooltip("Background tint used for the placeholder card when no sprite is set.")]
        public Color accentColor = new Color(0.15f, 0.7f, 1f);

        [Header("Semantics")]
        public List<string> tags = new List<string>();
        public List<EventType> supportedEventTypes = new List<EventType>();

        [Header("People constraints")]
        [Min(0)] public int minPeople = 0;
        [Tooltip("Maximum people the event may involve. 0 = no upper limit.")]
        [Min(0)] public int maxPeople = 0;

        [Header("Placement / timing preferences")]
        public PlacementPolicy placementPolicy = PlacementPolicy.Auto;
        [Min(0.25f)] public float preferredDurationSeconds = 4f;
        [Tooltip("Preferred comedic delay range (ms). The timing engine samples within this.")]
        public Vector2 preferredComedicDelayMs = new Vector2(100f, 300f);
        public AnimationStyle animationStyle = AnimationStyle.Pop;

        [Header("Selection tuning")]
        [Tooltip("Base selection weight; higher = more likely to be chosen.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Priority breaks ties; higher wins.")]
        public int priority = 0;
        [Tooltip("Per-meme cooldown in seconds before this exact meme may repeat.")]
        [Min(0f)] public float cooldownSeconds = 6f;

        [Header("Captions")]
        [Tooltip("Optional caption templates. {P} -> primary person, {O} -> primary object.")]
        public List<string> captionTemplates = new List<string>();

        public bool SupportsEvent(EventType type)
        {
            if (supportedEventTypes == null || supportedEventTypes.Count == 0)
            {
                return true; // no constraint = applies to all
            }

            for (int i = 0; i < supportedEventTypes.Count; i++)
            {
                if (supportedEventTypes[i] == type)
                {
                    return true;
                }
            }

            return false;
        }

        public bool PeopleCountAllowed(int peopleInEvent)
        {
            if (peopleInEvent < minPeople)
            {
                return false;
            }

            if (maxPeople > 0 && peopleInEvent > maxPeople)
            {
                return false;
            }

            return true;
        }

        public bool HasTag(string tag)
        {
            if (tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
