using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Placement;
using MemeAR.Scene;
using MemeAR.Timing;

namespace MemeAR.Rendering
{
    /// <summary>
    /// Decides WHAT / WHERE / WHEN / HOW and emits a single declarative
    /// <see cref="MemeRenderInstruction"/>. It combines the selected meme, the resolved
    /// placement, and the timing decision. It NEVER instantiates GameObjects — that is the
    /// renderer's job — which keeps the intelligence layers free of engine object lifecycle.
    /// </summary>
    public sealed class ARDirector
    {
        private readonly PlacementSolver _placement;
        private readonly DeterministicRandom _random;

        public ARDirector(PlacementSolver placement, DeterministicRandom random)
        {
            _placement = placement;
            _random = random;
        }

        public MemeRenderInstruction Direct(
            SceneEvent e,
            MemeDefinition meme,
            SceneSnapshot snapshot,
            TimingDecision timing)
        {
            PlacementResult placement = _placement.Solve(e, meme, snapshot);
            string caption = BuildCaption(meme, e);

            return new MemeRenderInstruction(
                memeId: meme.id,
                caption: caption,
                accentColor: meme.accentColor,
                sprite: meme.sprite,
                targetType: placement.TargetType,
                targetId: placement.TargetId,
                placementMode: placement.Mode,
                screenAnchor: placement.ScreenAnchor,
                worldPose: placement.WorldPose,
                offset: placement.Offset,
                scale: 1f,
                showAt: timing.ShowAtTimestamp,
                duration: timing.DurationSeconds,
                animationStyle: timing.AnimationStyle,
                faceCamera: placement.FaceCamera,
                trackingBehavior: placement.TrackingBehavior,
                mediaKind: meme.media != null ? meme.media.kind : MediaKind.GeneratedCard,
                media: meme.media,
                audio: meme.audio,
                attribution: meme.attribution);
        }

        private string BuildCaption(MemeDefinition meme, SceneEvent e)
        {
            if (meme.captionTemplates == null || meme.captionTemplates.Count == 0)
            {
                return meme.title;
            }

            int index = _random.Range(0, meme.captionTemplates.Count);
            string template = meme.captionTemplates[index];

            string person = e.PrimaryParticipant ?? "someone";
            string obj = e.PrimaryObject ?? "that";
            return template.Replace("{P}", person).Replace("{O}", obj);
        }
    }
}
