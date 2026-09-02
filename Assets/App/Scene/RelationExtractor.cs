using UnityEngine;
using MemeAR.Infrastructure;

namespace MemeAR.Scene
{
    /// <summary>
    /// Derives spatial/social relations (near, looking_at, reaching_toward, approaching)
    /// from the raw people/objects in a snapshot. This is SHARED by simulated and real
    /// perception: both produce people + objects with positions/velocities, and this stage
    /// turns them into relations. Keeping it shared means simulation exercises the same
    /// relation logic the device will use.
    /// </summary>
    public sealed class RelationExtractor
    {
        private readonly MemeArConfig _config;

        public RelationExtractor(MemeArConfig config)
        {
            _config = config;
        }

        public void Extract(SceneSnapshot snapshot)
        {
            snapshot.Relations.Clear();

            var people = snapshot.People;
            var objects = snapshot.Objects;

            // Person <-> Object relations.
            for (int pi = 0; pi < people.Count; pi++)
            {
                var person = people[pi];
                string nearestObjId = null;
                float nearestObjDist = float.MaxValue;

                for (int oi = 0; oi < objects.Count; oi++)
                {
                    var obj = objects[oi];
                    float dist = Vector2.Distance(person.NormalizedCenter, obj.NormalizedCenter);

                    if (dist < nearestObjDist)
                    {
                        nearestObjDist = dist;
                        nearestObjId = obj.Id;
                    }

                    if (dist <= _config.nearScreenDistance)
                    {
                        snapshot.Relations.Add(new SceneRelation(
                            RelationType.Near, person.Id, true, obj.Id, false,
                            Confidence01(1f - dist / _config.nearScreenDistance)));
                    }

                    // Reaching: person moving toward object faster than threshold.
                    Vector2 toObj = obj.NormalizedCenter - person.NormalizedCenter;
                    float approach = Vector2.Dot(person.ScreenVelocity, toObj.normalized);
                    if (approach >= _config.reachApproachSpeed && dist <= _config.nearScreenDistance * 2.2f)
                    {
                        float conf = Confidence01(approach / (_config.reachApproachSpeed * 2f));
                        snapshot.Relations.Add(new SceneRelation(
                            RelationType.ReachingToward, person.Id, true, obj.Id, false, conf));
                    }

                    // Attention: head/gaze pointing at object direction (screen approx).
                    if (LooksToward(person, obj.NormalizedCenter))
                    {
                        snapshot.Relations.Add(new SceneRelation(
                            RelationType.AttentionOn, person.Id, true, obj.Id, false, 0.6f));
                    }
                }

                if (nearestObjId != null)
                {
                    var nearest = snapshot.FindObject(nearestObjId);
                    if (nearest != null)
                    {
                        nearest.NearestPersonId = person.Id;
                    }
                }
            }

            // Person <-> Person relations.
            for (int a = 0; a < people.Count; a++)
            {
                for (int b = 0; b < people.Count; b++)
                {
                    if (a == b)
                    {
                        continue;
                    }

                    var pa = people[a];
                    var pb = people[b];
                    float dist = Vector2.Distance(pa.NormalizedCenter, pb.NormalizedCenter);

                    if (dist <= _config.nearScreenDistance * 1.5f)
                    {
                        snapshot.Relations.Add(new SceneRelation(
                            RelationType.Near, pa.Id, true, pb.Id, true, 0.7f));
                    }

                    if (LooksToward(pa, pb.NormalizedCenter))
                    {
                        snapshot.Relations.Add(new SceneRelation(
                            RelationType.LookingAt, pa.Id, true, pb.Id, true, 0.6f));
                    }

                    Vector2 toB = pb.NormalizedCenter - pa.NormalizedCenter;
                    float approach = Vector2.Dot(pa.ScreenVelocity, toB.normalized);
                    if (approach >= _config.reachApproachSpeed)
                    {
                        snapshot.Relations.Add(new SceneRelation(
                            RelationType.ApproachingPerson, pa.Id, true, pb.Id, true,
                            Confidence01(approach / (_config.reachApproachSpeed * 2f))));
                    }
                }
            }
        }

        private bool LooksToward(TrackedPerson person, Vector2 targetNormalized)
        {
            // Approximate head direction in screen space using its x/y components.
            Vector2 headScreen = new Vector2(person.HeadDirection.x, person.HeadDirection.y);
            if (headScreen.sqrMagnitude < 1e-5f)
            {
                return false;
            }

            Vector2 toTarget = (targetNormalized - person.NormalizedCenter);
            if (toTarget.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            float dot = Vector2.Dot(headScreen.normalized, toTarget.normalized);
            return dot >= _config.lookingAtDot;
        }

        private static float Confidence01(float v)
        {
            return Mathf.Clamp01(v);
        }
    }
}
