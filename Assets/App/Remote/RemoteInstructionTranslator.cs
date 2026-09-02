using System.Collections.Generic;
using UnityEngine;
using MemeAR.Memes;
using MemeAR.Placement;
using MemeAR.Rendering;

namespace MemeAR.Remote
{
    /// <summary>
    /// Validates a remote reaction instruction and translates it into a local, validated
    /// <see cref="MemeRenderInstruction"/>. The remote side may only REFERENCE a bundled
    /// meme by id and supply bounded parameters; anything unknown or out of range is
    /// rejected. This is the single choke point that keeps remote intelligence declarative
    /// and safe — no remote-supplied asset, code, or unbounded value ever reaches rendering.
    /// </summary>
    public static class RemoteInstructionTranslator
    {
        public static bool TryTranslate(
            RemoteReactionInstruction remote,
            IReadOnlyList<MemeDefinition> catalog,
            double now,
            out MemeRenderInstruction instruction,
            out string reason)
        {
            instruction = default;

            if (remote == null || remote.action != "spawn_meme")
            {
                reason = "unsupported action";
                return false;
            }

            MemeDefinition meme = FindMeme(catalog, remote.meme_id);
            if (meme == null)
            {
                reason = "unknown meme_id (remote may only reference bundled memes)";
                return false;
            }

            RenderTargetType targetType = ParseTarget(remote.target?.type);
            PlacementMode mode = ParseAnchor(remote.target?.anchor, targetType);
            AnimationStyle style = ParseAnimation(remote.animation);

            float delayMs = remote.timing != null ? Mathf.Clamp(remote.timing.delay_ms, 0f, 2000f) : 0f;
            float duration = Mathf.Clamp(remote.duration_ms / 1000f, RenderInstructionValidator.MinDuration, RenderInstructionValidator.MaxDuration);
            double showAt = now + delayMs / 1000.0;

            // Screen-space placement is the only safe universal fallback for a remote source
            // that cannot know the device's current world geometry; world anchors resolve
            // locally later via the placement solver if an id is supplied.
            var candidate = new MemeRenderInstruction(
                memeId: meme.id,
                caption: string.IsNullOrEmpty(remote.caption) ? meme.title : remote.caption,
                accentColor: meme.accentColor,
                sprite: meme.sprite,
                targetType: targetType,
                targetId: remote.target?.id,
                placementMode: PlacementMode.ScreenSpace,
                screenAnchor: new Vector2(0.5f, 0.62f),
                worldPose: null,
                offset: Vector3.zero,
                scale: 1f,
                showAt: showAt,
                duration: duration,
                animationStyle: style,
                faceCamera: false,
                trackingBehavior: TrackingBehavior.FollowTarget);

            if (!RenderInstructionValidator.Validate(candidate, out reason))
            {
                return false;
            }

            instruction = candidate;
            reason = null;
            return true;
        }

        private static MemeDefinition FindMeme(IReadOnlyList<MemeDefinition> catalog, string id)
        {
            if (catalog == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null && catalog[i].id == id)
                {
                    return catalog[i];
                }
            }

            return null;
        }

        private static RenderTargetType ParseTarget(string type)
        {
            switch (type)
            {
                case "person": return RenderTargetType.Person;
                case "object": return RenderTargetType.Object;
                case "world": return RenderTargetType.World;
                default: return RenderTargetType.Screen;
            }
        }

        private static PlacementMode ParseAnchor(string anchor, RenderTargetType type)
        {
            switch (anchor)
            {
                case "above_head": return PlacementMode.AbovePerson;
                case "near": return type == RenderTargetType.Object ? PlacementMode.NearObject : PlacementMode.NearPerson;
                default: return PlacementMode.ScreenSpace;
            }
        }

        private static AnimationStyle ParseAnimation(string animation)
        {
            switch (animation)
            {
                case "slide": return AnimationStyle.Slide;
                case "fade": return AnimationStyle.Fade;
                case "bounce": return AnimationStyle.Bounce;
                default: return AnimationStyle.Pop;
            }
        }
    }
}
