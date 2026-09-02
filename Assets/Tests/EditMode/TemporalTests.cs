using NUnit.Framework;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Scene;
using MemeAR.Temporal;

namespace MemeAR.Tests
{
    public class RingBufferTests
    {
        [Test]
        public void Evicts_Oldest_When_Full()
        {
            var rb = new RingBuffer<int>(3);
            Assert.AreEqual(default(int), rb.Add(1));
            rb.Add(2);
            rb.Add(3);
            Assert.IsTrue(rb.IsFull);
            int evicted = rb.Add(4); // evicts 1
            Assert.AreEqual(1, evicted);
            Assert.AreEqual(3, rb.Count);
            Assert.AreEqual(2, rb.Oldest);
            Assert.AreEqual(4, rb.Newest);
        }

        [Test]
        public void Indexes_Oldest_To_Newest()
        {
            var rb = new RingBuffer<int>(3);
            rb.Add(10);
            rb.Add(20);
            rb.Add(30);
            rb.Add(40); // now [20,30,40]
            Assert.AreEqual(20, rb[0]);
            Assert.AreEqual(30, rb[1]);
            Assert.AreEqual(40, rb[2]);
        }
    }

    public class TemporalSceneBufferTests
    {
        [Test]
        public void PushFrame_Stores_Independent_Clone()
        {
            var buffer = new TemporalSceneBuffer(4, 8);
            var live = TestFactory.Snapshot(1.0);
            live.People.Add(TestFactory.Person("P1", new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.2f), Vector2.zero, Vector3.forward));
            buffer.PushFrame(live);

            // Mutating the live snapshot must not affect the retained frame.
            live.People[0].Id = "MUTATED";
            live.People.Clear();

            Assert.AreEqual(1, buffer.Latest.People.Count);
            Assert.AreEqual("P1", buffer.Latest.People[0].Id);
        }

        [Test]
        public void CountEventsOfType_Respects_Window()
        {
            var buffer = new TemporalSceneBuffer(4, 16);
            buffer.PushEvent(new SceneEvent(1, EventType.SuddenMotion, 10.0, EventPhase.Triggered, 1f));
            buffer.PushEvent(new SceneEvent(2, EventType.SuddenMotion, 12.0, EventPhase.Triggered, 1f));
            buffer.PushEvent(new SceneEvent(3, EventType.PersonEntered, 12.5, EventPhase.Triggered, 1f));

            Assert.AreEqual(2, buffer.CountEventsOfType(EventType.SuddenMotion, 13.0, 5.0));
            Assert.AreEqual(1, buffer.CountEventsOfType(EventType.SuddenMotion, 13.0, 1.5)); // only t=12 within 1.5s
        }

        [Test]
        public void Bounded_Memory_Never_Exceeds_Capacity()
        {
            var buffer = new TemporalSceneBuffer(3, 8);
            for (int i = 0; i < 50; i++)
            {
                buffer.PushFrame(TestFactory.Snapshot(i));
            }

            Assert.LessOrEqual(buffer.FrameCount, 3);
        }
    }

    public class SessionStateTests
    {
        [Test]
        public void RecordMemeShown_Tracks_LastUse_And_Total()
        {
            var s = new SessionState();
            s.RecordMemeShown("m1", 5.0);
            s.RecordMemeShown("m2", 6.0);
            Assert.AreEqual(2, s.TotalMemesShown);
            Assert.AreEqual(6.0, s.LastMemeTimestamp, 1e-6);
            Assert.AreEqual(5.0, s.LastUseTime("m1"), 1e-6);
            Assert.AreEqual(double.NegativeInfinity, s.LastUseTime("unknown"));
        }
    }
}
