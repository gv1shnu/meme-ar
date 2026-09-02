using UnityEngine;
using MemeAR.AR;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Rendering;
using MemeAR.Scene;

namespace MemeAR.Placement
{
    /// <summary>
    /// Decides WHERE a reaction is placed. Preference order: use a perception-derived world
    /// position when available (above person/object), else a screen-space anchor derived
    /// from the target's bounding box (kept ABOVE the box so it never covers a face center),
    /// else a safe camera-forward / screen-center fallback so a reaction always has a home.
    ///
    /// The MVP resolver is intentionally simple but structured so a richer placement-scoring
    /// pass (screen occupancy, edges, other bounding boxes, distance/visibility) can slot in.
    /// </summary>
    public sealed class PlacementSolver
    {
        private readonly MemeArConfig _config;
        private readonly IARSpatialProvider _spatial;

        public PlacementSolver(MemeArConfig config, IARSpatialProvider spatial)
        {
            _config = config;
            _spatial = spatial;
        }

        public PlacementResult Solve(SceneEvent e, MemeDefinition meme, SceneSnapshot snapshot)
        {
            PlacementPolicy policy = meme != null ? meme.placementPolicy : PlacementPolicy.Auto;

            // Choose a target entity from the event according to policy.
            TrackedObject targetObject = null;
            TrackedPerson targetPerson = null;

            bool wantObject = policy == PlacementPolicy.PreferAboveObject
                              || (policy == PlacementPolicy.Auto && e.Objects.Count > 0);
            bool wantPerson = policy == PlacementPolicy.PreferAbovePerson
                              || policy == PlacementPolicy.PreferNearParticipant
                              || (!wantObject && e.Participants.Count > 0);

            if (wantObject && snapshot != null && e.PrimaryObject != null)
            {
                targetObject = snapshot.FindObject(e.PrimaryObject);
            }

            if (targetObject == null && wantPerson && snapshot != null && e.PrimaryParticipant != null)
            {
                targetPerson = snapshot.FindPerson(e.PrimaryParticipant);
            }

            if (policy == PlacementPolicy.PreferScreenSpace)
            {
                targetObject = null;
                targetPerson = null;
            }

            // 1) Object with world position.
            if (targetObject != null && targetObject.WorldPosition.HasValue)
            {
                Vector3 pos = targetObject.WorldPosition.Value + Vector3.up * _config.abovePersonOffsetMeters;
                return World(PlacementMode.AboveObject, RenderTargetType.Object, targetObject.Id, pos);
            }

            // 2) Person with world position.
            if (targetPerson != null && targetPerson.WorldPosition.HasValue)
            {
                Vector3 pos = targetPerson.WorldPosition.Value + Vector3.up * _config.abovePersonOffsetMeters;
                return World(PlacementMode.AbovePerson, RenderTargetType.Person, targetPerson.Id, pos);
            }

            // 3) Object screen-space (anchor above the box so it does not cover contents).
            if (targetObject != null)
            {
                Vector2 anchor = AnchorAboveBox(targetObject.NormalizedBounds);
                return Screen(RenderTargetType.Object, targetObject.Id, anchor);
            }

            // 4) Person screen-space (anchor above the head, never over the face center).
            if (targetPerson != null)
            {
                Vector2 anchor = AnchorAboveBox(targetPerson.NormalizedBounds);
                return Screen(RenderTargetType.Person, targetPerson.Id, anchor);
            }

            // 5) Fallback: a card in front of the camera if tracking is active, else screen center.
            if (_spatial != null && _spatial.TrackingActive)
            {
                Pose pose = _spatial.CameraForwardPose(_config.cameraForwardDistanceMeters);
                return new PlacementResult(
                    PlacementMode.CameraForwardFallback, RenderTargetType.World, null,
                    new Vector2(0.5f, 0.6f), pose, Vector3.zero, true, TrackingBehavior.Billboard);
            }

            return Screen(RenderTargetType.Screen, null, new Vector2(0.5f, 0.62f));
        }

        private static Vector2 AnchorAboveBox(Rect normalizedBounds)
        {
            float x = Mathf.Clamp(normalizedBounds.center.x, 0.08f, 0.92f);
            float y = Mathf.Clamp(normalizedBounds.yMax + 0.06f, 0.08f, 0.92f);
            return new Vector2(x, y);
        }

        private PlacementResult World(PlacementMode mode, RenderTargetType type, string id, Vector3 position)
        {
            Pose pose = new Pose(position, Quaternion.identity);
            return new PlacementResult(mode, type, id, new Vector2(0.5f, 0.6f), pose, Vector3.zero, true, TrackingBehavior.Billboard);
        }

        private PlacementResult Screen(RenderTargetType type, string id, Vector2 anchor)
        {
            return new PlacementResult(PlacementMode.ScreenSpace, type, id, anchor, null, Vector3.zero, false, TrackingBehavior.FollowTarget);
        }
    }
}
