using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Scene;
using MemeAR.Temporal;

namespace MemeAR.Tests
{
    public class EventDetectorTests
    {
        private static SceneSnapshot WithReach(double t)
        {
            var s = TestFactory.Snapshot(t);
            s.People.Add(TestFactory.Person("P1", new Vector2(0.5f, 0.4f), new Vector2(0.15f, 0.3f), new Vector2(0.3f, 0f), Vector3.right));
            s.Objects.Add(TestFactory.Object("O1", "food", new Vector2(0.7f, 0.4f), new Vector2(0.1f, 0.1f)));
            s.Relations.Add(new SceneRelation(RelationType.ReachingToward, "P1", true, "O1", false, 0.9f));
            return s;
        }

        private static SceneSnapshot WithNear(double t)
        {
            var s = TestFactory.Snapshot(t);
            s.People.Add(TestFactory.Person("P1", new Vector2(0.62f, 0.4f), new Vector2(0.15f, 0.3f), Vector2.zero, Vector3.right));
            s.Objects.Add(TestFactory.Object("O1", "food", new Vector2(0.7f, 0.4f), new Vector2(0.1f, 0.1f)));
            s.Relations.Add(new SceneRelation(RelationType.Near, "P1", true, "O1", false, 0.9f));
            return s;
        }

        [Test]
        public void Reach_Emits_Anticipation_Then_Trigger_With_Same_Correlation()
        {
            var buffer = new TemporalSceneBuffer(4, 16);
            var ids = new EventIdGenerator();
            var detector = new ReachTowardObjectDetector(ids);
            var output = new List<SceneEvent>();

            buffer.PushFrame(WithReach(1.0));
            detector.Detect(buffer, 1.0, output);

            var antic = output.Find(e => e.Phase == EventPhase.Anticipation);
            Assert.IsNotNull(antic, "expected an anticipation event");
            Assert.AreEqual(EventType.PersonReachedTowardObject, antic.Type);
            long correlation = antic.CorrelationId;
            Assert.AreNotEqual(0, correlation);

            output.Clear();
            buffer.PushFrame(WithNear(1.5));
            detector.Detect(buffer, 1.5, output);

            var trigger = output.Find(e => e.Phase == EventPhase.Triggered);
            Assert.IsNotNull(trigger, "expected a triggered event on contact");
            Assert.AreEqual(correlation, trigger.CorrelationId, "trigger must correlate to its anticipation");
        }

        [Test]
        public void Presence_Emits_Enter_Then_Leave()
        {
            var buffer = new TemporalSceneBuffer(4, 16);
            var ids = new EventIdGenerator();
            var detector = new PresenceDetector(ids);
            var output = new List<SceneEvent>();

            var withPerson = TestFactory.Snapshot(1.0);
            withPerson.People.Add(TestFactory.Person("P1", new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.2f), Vector2.zero, Vector3.forward));
            buffer.PushFrame(withPerson);
            detector.Detect(buffer, 1.0, output);
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(EventType.PersonEntered, output[0].Type);

            output.Clear();
            buffer.PushFrame(TestFactory.Snapshot(2.0)); // nobody
            detector.Detect(buffer, 2.0, output);
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(EventType.PersonLeft, output[0].Type);
        }
    }

    public class RelationExtractorTests
    {
        [Test]
        public void Detects_Reach_From_Velocity_Toward_Object()
        {
            var config = TestFactory.Config();
            var extractor = new RelationExtractor(config);

            var s = TestFactory.Snapshot(1.0);
            // Person moving right toward an object slightly to its right.
            s.People.Add(TestFactory.Person("P1", new Vector2(0.55f, 0.4f), new Vector2(0.15f, 0.3f), new Vector2(0.3f, 0f), Vector3.right));
            s.Objects.Add(TestFactory.Object("O1", "food", new Vector2(0.62f, 0.4f), new Vector2(0.1f, 0.1f)));

            extractor.Extract(s);

            bool hasReach = s.Relations.Exists(r => r.Type == RelationType.ReachingToward && r.SubjectId == "P1" && r.ObjectId == "O1");
            Assert.IsTrue(hasReach, "expected a ReachingToward relation");
        }
    }
}
