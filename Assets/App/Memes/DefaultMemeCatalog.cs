using System.Collections.Generic;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Placement;
using MemeAR.Rendering;

namespace MemeAR.Memes
{
    /// <summary>
    /// Builds a bundled placeholder reaction set entirely in code. These are original text
    /// reaction cards (no third-party/copyrighted assets), which keeps the MVP fully
    /// functional offline and gives tests a deterministic catalog. Real authored assets can
    /// replace this via a <see cref="MemeCatalog"/> ScriptableObject at any time.
    /// </summary>
    public static class DefaultMemeCatalog
    {
        public static List<MemeDefinition> Build()
        {
            var list = new List<MemeDefinition>();

            list.Add(Make(
                "reaction_side_eye", "Side Eye", new Color(0.55f, 0.35f, 0.85f), "awkward",
                new[] { MemeTags.SideEye, MemeTags.Awkward, MemeTags.Reaction },
                new[] { EventType.PersonReachedTowardObject, EventType.AttentionShift },
                minPeople: 1, placement: PlacementPolicy.PreferAbovePerson,
                delay: new Vector2(150f, 350f), style: AnimationStyle.Pop,
                captions: new[] { "…really, {P}?", "I saw that." }));

            list.Add(Make(
                "reaction_gasp", "Gasp!", new Color(1f, 0.55f, 0.2f), "hype",
                new[] { MemeTags.Surprise, MemeTags.Reaction },
                new[] { EventType.SuddenMotion, EventType.ObjectMoved, EventType.PersonReachedTowardObject },
                minPeople: 0, placement: PlacementPolicy.PreferScreenSpace,
                delay: new Vector2(0f, 80f), style: AnimationStyle.Bounce,
                captions: new[] { "GASP", "No way!" }));

            list.Add(Make(
                "reaction_snack_thief", "Snack Thief", new Color(0.9f, 0.25f, 0.35f), "awkward",
                new[] { MemeTags.Food, MemeTags.Awkward, MemeTags.Reaction },
                new[] { EventType.PersonReachedTowardObject },
                minPeople: 1, placement: PlacementPolicy.PreferAboveObject,
                delay: new Vector2(200f, 400f), style: AnimationStyle.Pop,
                captions: new[] { "That's not yours, {P}", "Snack heist in progress" }));

            list.Add(Make(
                "reaction_all_eyes", "All Eyes", new Color(0.2f, 0.75f, 0.9f), "hype",
                new[] { MemeTags.Group, MemeTags.Attention },
                new[] { EventType.GroupAttentionConverged },
                minPeople: 2, placement: PlacementPolicy.PreferAboveObject,
                delay: new Vector2(100f, 250f), style: AnimationStyle.Fade,
                captions: new[] { "All eyes on it", "The main character has arrived" }));

            list.Add(Make(
                "reaction_new_challenger", "New Challenger", new Color(0.3f, 0.85f, 0.5f), "hype",
                new[] { MemeTags.Entrance, MemeTags.Reaction },
                new[] { EventType.PersonEntered },
                minPeople: 1, placement: PlacementPolicy.PreferAbovePerson,
                delay: new Vector2(80f, 200f), style: AnimationStyle.Slide,
                captions: new[] { "A wild {P} appears", "New challenger approaching" }));

            list.Add(Make(
                "reaction_exit_stage", "Exit Stage", new Color(0.6f, 0.6f, 0.65f), "awkward",
                new[] { MemeTags.Exit, MemeTags.Reaction },
                new[] { EventType.PersonLeft },
                minPeople: 0, placement: PlacementPolicy.PreferScreenSpace,
                delay: new Vector2(120f, 300f), style: AnimationStyle.Fade,
                captions: new[] { "And {P} is gone", "Exit, stage left" }));

            list.Add(Make(
                "reaction_confused", "Confused", new Color(0.85f, 0.7f, 0.2f), "awkward",
                new[] { MemeTags.Confusion, MemeTags.Reaction },
                new[] { EventType.AttentionShift, EventType.GenericInterestingChange },
                minPeople: 1, placement: PlacementPolicy.PreferAbovePerson,
                delay: new Vector2(150f, 350f), style: AnimationStyle.Pop,
                captions: new[] { "wait, what?", "{P}.exe stopped responding" }));

            list.Add(Make(
                "reaction_nice", "Nice", new Color(0.4f, 0.8f, 0.4f), "hype",
                new[] { MemeTags.Success, MemeTags.Reaction },
                new[] { EventType.GenericInterestingChange, EventType.ObjectAppeared },
                minPeople: 0, placement: PlacementPolicy.Auto,
                delay: new Vector2(100f, 250f), style: AnimationStyle.Bounce,
                captions: new[] { "nice.", "certified" }));

            return list;
        }

        private static MemeDefinition Make(
            string id, string title, Color accent, string pack,
            string[] tags, EventType[] events,
            int minPeople, PlacementPolicy placement,
            Vector2 delay, AnimationStyle style, string[] captions)
        {
            var m = ScriptableObject.CreateInstance<MemeDefinition>();
            m.id = id;
            m.title = title;
            m.accentColor = accent;
            m.pack = pack;
            m.media = new MediaReference { kind = MediaKind.GeneratedCard };
            m.audio = new AudioReference();
            m.attribution = string.Empty; // original placeholder content
            m.tags = new List<string>(tags);
            m.supportedEventTypes = new List<EventType>(events);
            m.minPeople = minPeople;
            m.maxPeople = 0;
            m.placementPolicy = placement;
            m.preferredDurationSeconds = 4f;
            m.preferredComedicDelayMs = delay;
            m.animationStyle = style;
            m.weight = 1f;
            m.priority = 0;
            m.cooldownSeconds = 6f;
            m.captionTemplates = new List<string>(captions);
            return m;
        }
    }
}
