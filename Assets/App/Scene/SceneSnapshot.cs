using System.Collections.Generic;
using UnityEngine;

namespace MemeAR.Scene
{
    /// <summary>
    /// A single detected plane hint (subset of AR Foundation's plane data we care about),
    /// kept engine-agnostic so scene logic does not depend on AR Foundation types.
    /// </summary>
    public readonly struct PlaneHint
    {
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly Vector2 Extents;

        public PlaneHint(Vector3 center, Vector3 normal, Vector2 extents)
        {
            Center = center;
            Normal = normal;
            Extents = extents;
        }
    }

    /// <summary>
    /// THE central boundary of the whole system. Perception (real or simulated) produces a
    /// SceneSnapshot; every stage after it — temporal memory, prediction, events, comedy,
    /// retrieval, timing, placement, rendering — consumes only SceneSnapshots and never
    /// touches AR Foundation or a specific ML model. This is what makes real models
    /// substitutable without rewriting the pipeline.
    ///
    /// Snapshots are treated as immutable once published. The provider owns construction.
    /// </summary>
    public sealed class SceneSnapshot
    {
        public double Timestamp;
        public Pose CameraPose;
        public float CameraVerticalFovDeg = 60f;
        public Vector2 ImageSize = new Vector2(1080f, 1920f);

        public readonly List<TrackedPerson> People = new List<TrackedPerson>(8);
        public readonly List<TrackedObject> Objects = new List<TrackedObject>(8);
        public readonly List<SceneRelation> Relations = new List<SceneRelation>(16);
        public readonly List<PlaneHint> Planes = new List<PlaneHint>(4);

        /// <summary>Overall confidence in this snapshot's contents (0..1).</summary>
        public float Confidence = 1f;

        /// <summary>True while the underlying source (AR/sim) is producing valid data.</summary>
        public bool IsValid = true;

        public void Reset()
        {
            Timestamp = 0;
            CameraPose = Pose.identity;
            People.Clear();
            Objects.Clear();
            Relations.Clear();
            Planes.Clear();
            Confidence = 1f;
            IsValid = true;
        }

        public TrackedPerson FindPerson(string id)
        {
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Id == id)
                {
                    return People[i];
                }
            }

            return null;
        }

        public TrackedObject FindObject(string id)
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                if (Objects[i].Id == id)
                {
                    return Objects[i];
                }
            }

            return null;
        }

        /// <summary>Deep-ish clone used by the temporal buffer to retain history safely.</summary>
        public SceneSnapshot CloneInto(SceneSnapshot dest)
        {
            dest.Reset();
            dest.Timestamp = Timestamp;
            dest.CameraPose = CameraPose;
            dest.CameraVerticalFovDeg = CameraVerticalFovDeg;
            dest.ImageSize = ImageSize;
            dest.Confidence = Confidence;
            dest.IsValid = IsValid;

            for (int i = 0; i < People.Count; i++)
            {
                var p = new TrackedPerson();
                p.CopyFrom(People[i]);
                dest.People.Add(p);
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                var src = Objects[i];
                dest.Objects.Add(new TrackedObject
                {
                    Id = src.Id,
                    Category = src.Category,
                    NormalizedBounds = src.NormalizedBounds,
                    WorldPosition = src.WorldPosition,
                    ScreenVelocity = src.ScreenVelocity,
                    NearestPersonId = src.NearestPersonId,
                    LastSeen = src.LastSeen,
                    Confidence = src.Confidence
                });
            }

            dest.Relations.AddRange(Relations);
            dest.Planes.AddRange(Planes);
            return dest;
        }
    }
}
