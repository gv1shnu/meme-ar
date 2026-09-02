using System.Collections.Generic;
using UnityEngine;
using MemeAR.Scene;
using MemeAR.Temporal;

namespace MemeAR.Events
{
    /// <summary>Emits PersonEntered / PersonLeft by diffing consecutive frames.</summary>
    public sealed class PresenceDetector : IEventDetector
    {
        private readonly EventIdGenerator _ids;
        private readonly HashSet<string> _known = new HashSet<string>();

        public PresenceDetector(EventIdGenerator ids) { _ids = ids; }
        public string Name => "presence";

        public void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output)
        {
            SceneSnapshot latest = buffer.Latest;
            if (latest == null)
            {
                return;
            }

            var current = new HashSet<string>();
            for (int i = 0; i < latest.People.Count; i++)
            {
                string id = latest.People[i].Id;
                current.Add(id);
                if (!_known.Contains(id))
                {
                    var e = new SceneEvent(_ids.Next(), EventType.PersonEntered, now, EventPhase.Triggered, 0.85f);
                    e.Participants.Add(id);
                    output.Add(e);
                }
            }

            // Departed people.
            _scratch.Clear();
            foreach (string id in _known)
            {
                if (!current.Contains(id))
                {
                    _scratch.Add(id);
                }
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                var e = new SceneEvent(_ids.Next(), EventType.PersonLeft, now, EventPhase.Triggered, 0.8f);
                e.Participants.Add(_scratch[i]);
                output.Add(e);
            }

            _known.Clear();
            foreach (string id in current)
            {
                _known.Add(id);
            }
        }

        private readonly List<string> _scratch = new List<string>();
    }

    /// <summary>Emits ObjectAppeared and ObjectMoved.</summary>
    public sealed class ObjectDetectorEvents : IEventDetector
    {
        private readonly EventIdGenerator _ids;
        private readonly HashSet<string> _known = new HashSet<string>();
        private const float MoveThreshold = 0.05f;

        public ObjectDetectorEvents(EventIdGenerator ids) { _ids = ids; }
        public string Name => "objects";

        public void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output)
        {
            SceneSnapshot latest = buffer.Latest;
            SceneSnapshot previous = buffer.Previous;
            if (latest == null)
            {
                return;
            }

            var current = new HashSet<string>();
            for (int i = 0; i < latest.Objects.Count; i++)
            {
                var obj = latest.Objects[i];
                current.Add(obj.Id);

                if (!_known.Contains(obj.Id))
                {
                    var e = new SceneEvent(_ids.Next(), EventType.ObjectAppeared, now, EventPhase.Triggered, obj.Confidence);
                    e.Objects.Add(obj.Id);
                    output.Add(e);
                }

                if (previous != null)
                {
                    var prev = previous.FindObject(obj.Id);
                    if (prev != null)
                    {
                        float moved = Vector2.Distance(prev.NormalizedCenter, obj.NormalizedCenter);
                        if (moved >= MoveThreshold)
                        {
                            var e = new SceneEvent(_ids.Next(), EventType.ObjectMoved, now, EventPhase.Triggered, 0.7f);
                            e.Objects.Add(obj.Id);
                            e.Metadata["magnitude"] = moved;
                            output.Add(e);
                        }
                    }
                }
            }

            _known.Clear();
            foreach (string id in current)
            {
                _known.Add(id);
            }
        }
    }

    /// <summary>
    /// Emits the anticipation/trigger pair for a person reaching toward an object. When a
    /// ReachingToward relation appears it publishes an Anticipation event (so candidates
    /// prefetch); when the person becomes Near the object it publishes the Triggered event
    /// with the SAME correlation id. This is the canonical setup->anticipation->trigger flow.
    /// </summary>
    public sealed class ReachTowardObjectDetector : IEventDetector
    {
        private readonly EventIdGenerator _ids;
        // pairKey -> correlationId of the outstanding anticipation
        private readonly Dictionary<string, long> _anticipating = new Dictionary<string, long>();
        private readonly Dictionary<string, double> _confirmed = new Dictionary<string, double>();
        private const double ReConfirmGuardSeconds = 3.0;

        public ReachTowardObjectDetector(EventIdGenerator ids) { _ids = ids; }
        public string Name => "reach";

        public void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output)
        {
            SceneSnapshot latest = buffer.Latest;
            if (latest == null)
            {
                return;
            }

            for (int i = 0; i < latest.Relations.Count; i++)
            {
                SceneRelation r = latest.Relations[i];
                if (r.Type != RelationType.ReachingToward)
                {
                    continue;
                }

                string key = r.SubjectId + "->" + r.ObjectId;
                if (_confirmed.TryGetValue(key, out double confirmedAt) && (now - confirmedAt) < ReConfirmGuardSeconds)
                {
                    continue;
                }

                if (!_anticipating.ContainsKey(key))
                {
                    long correlation = _ids.Next();
                    _anticipating[key] = correlation;

                    var antic = new SceneEvent(_ids.Next(), EventType.PersonReachedTowardObject, now, EventPhase.Anticipation, r.Confidence);
                    antic.CorrelationId = correlation;
                    antic.Participants.Add(r.SubjectId);
                    antic.Objects.Add(r.ObjectId);
                    output.Add(antic);
                }
            }

            // Confirm: subject is Near object (contact) while an anticipation is outstanding.
            for (int i = 0; i < latest.Relations.Count; i++)
            {
                SceneRelation r = latest.Relations[i];
                if (r.Type != RelationType.Near || r.ObjectIsPerson)
                {
                    continue;
                }

                string key = r.SubjectId + "->" + r.ObjectId;
                if (_anticipating.TryGetValue(key, out long correlation))
                {
                    var trig = new SceneEvent(_ids.Next(), EventType.PersonReachedTowardObject, now, EventPhase.Triggered, Mathf.Max(0.7f, r.Confidence));
                    trig.CorrelationId = correlation;
                    trig.Participants.Add(r.SubjectId);
                    trig.Objects.Add(r.ObjectId);
                    output.Add(trig);

                    _anticipating.Remove(key);
                    _confirmed[key] = now;
                }
            }
        }
    }

    /// <summary>Emits AttentionShift when a person's head direction changes sharply.</summary>
    public sealed class AttentionShiftDetector : IEventDetector
    {
        private readonly EventIdGenerator _ids;
        private const float ShiftDot = 0.6f; // below this cosine => notable shift

        public AttentionShiftDetector(EventIdGenerator ids) { _ids = ids; }
        public string Name => "attention";

        public void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output)
        {
            SceneSnapshot latest = buffer.Latest;
            SceneSnapshot previous = buffer.Previous;
            if (latest == null || previous == null)
            {
                return;
            }

            for (int i = 0; i < latest.People.Count; i++)
            {
                var p = latest.People[i];
                var prev = previous.FindPerson(p.Id);
                if (prev == null)
                {
                    continue;
                }

                Vector3 a = prev.HeadDirection.normalized;
                Vector3 b = p.HeadDirection.normalized;
                float dot = Vector3.Dot(a, b);
                if (dot < ShiftDot)
                {
                    var e = new SceneEvent(_ids.Next(), EventType.AttentionShift, now, EventPhase.Triggered, Mathf.Clamp01(1f - dot));
                    e.Participants.Add(p.Id);
                    e.Metadata["shift"] = 1f - dot;
                    output.Add(e);
                }
            }
        }
    }

    /// <summary>Emits GroupAttentionConverged when 2+ people have AttentionOn the same object.</summary>
    public sealed class GroupAttentionDetector : IEventDetector
    {
        private readonly EventIdGenerator _ids;
        private readonly Dictionary<string, List<string>> _byObject = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, double> _confirmed = new Dictionary<string, double>();
        private const double GuardSeconds = 4.0;

        public GroupAttentionDetector(EventIdGenerator ids) { _ids = ids; }
        public string Name => "group-attention";

        public void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output)
        {
            SceneSnapshot latest = buffer.Latest;
            if (latest == null)
            {
                return;
            }

            _byObject.Clear();
            for (int i = 0; i < latest.Relations.Count; i++)
            {
                SceneRelation r = latest.Relations[i];
                if (r.Type != RelationType.AttentionOn || r.ObjectIsPerson)
                {
                    continue;
                }

                if (!_byObject.TryGetValue(r.ObjectId, out var list))
                {
                    list = new List<string>();
                    _byObject[r.ObjectId] = list;
                }

                if (!list.Contains(r.SubjectId))
                {
                    list.Add(r.SubjectId);
                }
            }

            foreach (var kvp in _byObject)
            {
                if (kvp.Value.Count < 2)
                {
                    continue;
                }

                if (_confirmed.TryGetValue(kvp.Key, out double at) && (now - at) < GuardSeconds)
                {
                    continue;
                }

                var e = new SceneEvent(_ids.Next(), EventType.GroupAttentionConverged, now, EventPhase.Triggered, 0.8f);
                e.Objects.Add(kvp.Key);
                e.Participants.AddRange(kvp.Value);
                e.Metadata["count"] = kvp.Value.Count;
                output.Add(e);
                _confirmed[kvp.Key] = now;
            }
        }
    }

    /// <summary>Emits SuddenMotion when any entity's screen velocity spikes.</summary>
    public sealed class SuddenMotionDetector : IEventDetector
    {
        private readonly EventIdGenerator _ids;
        private const float SpeedThreshold = 0.9f; // normalized screen units / second
        private double _lastEmit = double.NegativeInfinity;
        private const double MinInterval = 1.5;

        public SuddenMotionDetector(EventIdGenerator ids) { _ids = ids; }
        public string Name => "sudden-motion";

        public void Detect(TemporalSceneBuffer buffer, double now, List<SceneEvent> output)
        {
            SceneSnapshot latest = buffer.Latest;
            if (latest == null || (now - _lastEmit) < MinInterval)
            {
                return;
            }

            float maxSpeed = 0f;
            string who = null;
            for (int i = 0; i < latest.People.Count; i++)
            {
                float s = latest.People[i].ScreenVelocity.magnitude;
                if (s > maxSpeed)
                {
                    maxSpeed = s;
                    who = latest.People[i].Id;
                }
            }

            if (maxSpeed >= SpeedThreshold && who != null)
            {
                var e = new SceneEvent(_ids.Next(), EventType.SuddenMotion, now, EventPhase.Triggered, Mathf.Clamp01(maxSpeed / (SpeedThreshold * 2f)));
                e.Participants.Add(who);
                e.Metadata["speed"] = maxSpeed;
                output.Add(e);
                _lastEmit = now;
            }
        }
    }
}
