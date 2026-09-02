using NUnit.Framework;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Placement;
using MemeAR.Rendering;
using MemeAR.Scene;
using MemeAR.Simulation;
using MemeAR.Timing;

namespace MemeAR.Tests
{
    public class TimingTests
    {
        private static MemeDefinition Meme()
        {
            var m = ScriptableObject.CreateInstance<MemeDefinition>();
            m.id = "t";
            m.preferredComedicDelayMs = new Vector2(150f, 350f);
            m.preferredDurationSeconds = 4f;
            return m;
        }

        [Test]
        public void Reach_Uses_ShortBeat_Within_Authored_Range()
        {
            var config = TestFactory.Config();
            var policy = new ComedyTimingPolicy(config, new DeterministicRandom(11));
            var e = new SceneEvent(1, EventType.PersonReachedTowardObject, 10.0, EventPhase.Triggered, 0.9f);

            TimingDecision d = policy.Decide(e, Meme(), 0f, 10.0);

            Assert.AreEqual(TimingMode.ShortBeat, d.Mode);
            Assert.GreaterOrEqual(d.ComedicDelayMs, 150f);
            Assert.LessOrEqual(d.ComedicDelayMs, 350f);
            Assert.AreEqual(10.0 + d.ComedicDelayMs / 1000.0, d.ShowAtTimestamp, 1e-6);
        }

        [Test]
        public void SuddenMotion_Uses_Instant()
        {
            var config = TestFactory.Config();
            var policy = new ComedyTimingPolicy(config, new DeterministicRandom(11));
            var e = new SceneEvent(1, EventType.SuddenMotion, 5.0, EventPhase.Triggered, 0.9f);
            TimingDecision d = policy.Decide(e, null, 0f, 5.0);
            Assert.AreEqual(TimingMode.Instant, d.Mode);
        }
    }

    public class PlacementTests
    {
        [Test]
        public void Falls_Back_To_CameraForward_When_No_Target_Position()
        {
            var config = TestFactory.Config();
            var spatial = new SimulatedSpatialProvider();
            var solver = new PlacementSolver(config, spatial);

            var e = new SceneEvent(1, EventType.SuddenMotion, 1.0, EventPhase.Triggered, 0.9f);
            e.Participants.Add("P1"); // present in event but not in (null) snapshot

            PlacementResult r = solver.Solve(e, null, null);
            Assert.AreEqual(PlacementMode.CameraForwardFallback, r.Mode);
            Assert.IsTrue(r.WorldPose.HasValue);
        }

        [Test]
        public void Person_Without_World_Pos_Anchors_Above_Head_In_Screen_Space()
        {
            var config = TestFactory.Config();
            var solver = new PlacementSolver(config, new SimulatedSpatialProvider());

            var snapshot = TestFactory.Snapshot(1.0);
            snapshot.People.Add(TestFactory.Person("P1", new Vector2(0.5f, 0.4f), new Vector2(0.16f, 0.32f), Vector2.zero, Vector3.forward));

            var meme = ScriptableObject.CreateInstance<MemeDefinition>();
            meme.id = "m";
            meme.placementPolicy = PlacementPolicy.PreferAbovePerson;

            var e = new SceneEvent(1, EventType.AttentionShift, 1.0, EventPhase.Triggered, 0.8f);
            e.Participants.Add("P1");

            PlacementResult r = solver.Solve(e, meme, snapshot);
            Assert.AreEqual(PlacementMode.ScreenSpace, r.Mode);
            Assert.AreEqual(RenderTargetType.Person, r.TargetType);
            // yMax = 0.4 + 0.16 = 0.56; anchored above by 0.06.
            Assert.AreEqual(0.62f, r.ScreenAnchor.y, 1e-3);
        }

        [Test]
        public void Person_With_World_Pos_Uses_AbovePerson()
        {
            var config = TestFactory.Config();
            var solver = new PlacementSolver(config, new SimulatedSpatialProvider());

            var snapshot = TestFactory.Snapshot(1.0);
            snapshot.People.Add(TestFactory.Person("P1", new Vector2(0.5f, 0.4f), new Vector2(0.16f, 0.32f), Vector2.zero, Vector3.forward, new Vector3(0f, 0f, 2f)));

            var meme = ScriptableObject.CreateInstance<MemeDefinition>();
            meme.id = "m";
            meme.placementPolicy = PlacementPolicy.PreferAbovePerson;

            var e = new SceneEvent(1, EventType.AttentionShift, 1.0, EventPhase.Triggered, 0.8f);
            e.Participants.Add("P1");

            PlacementResult r = solver.Solve(e, meme, snapshot);
            Assert.AreEqual(PlacementMode.AbovePerson, r.Mode);
            Assert.IsTrue(r.WorldPose.HasValue);
        }
    }
}
