using System.Collections.Generic;
using NUnit.Framework;
using MemeAR.Comedy;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Prediction;
using MemeAR.Retrieval;
using MemeAR.Temporal;

namespace MemeAR.Tests
{
    public class OpportunityTests
    {
        private static SceneEvent ReachEvent(double t)
        {
            var e = new SceneEvent(1, EventType.PersonReachedTowardObject, t, EventPhase.Triggered, 0.9f);
            e.Participants.Add("P1");
            e.Objects.Add("O1");
            return e;
        }

        [Test]
        public void Strong_Event_Is_Accepted()
        {
            var config = TestFactory.Config();
            var engine = new ComedyOpportunityEngine(config);
            var buffer = new TemporalSceneBuffer(4, 16);

            OpportunityScore score = engine.Evaluate(ReachEvent(1.0), buffer, 1.0);
            Assert.IsTrue(score.Accepted, "strong, novel event should pass");
            Assert.Greater(score.Total, config.opportunityThreshold);
        }

        [Test]
        public void Global_Cooldown_Blocks_Reaction()
        {
            var config = TestFactory.Config();
            var engine = new ComedyOpportunityEngine(config);
            var buffer = new TemporalSceneBuffer(4, 16);

            buffer.Session.RecordMemeShown("m1", 1.0);
            OpportunityScore score = engine.Evaluate(ReachEvent(1.5), buffer, 1.5); // within global cooldown
            Assert.IsFalse(score.Accepted);
            Assert.AreEqual("global-cooldown", score.RejectReason);
        }

        [Test]
        public void Max_Simultaneous_Blocks_Reaction()
        {
            var config = TestFactory.Config();
            var engine = new ComedyOpportunityEngine(config);
            var buffer = new TemporalSceneBuffer(4, 16);
            buffer.Session.ActiveMemeCount = config.maxSimultaneousMemes;

            OpportunityScore score = engine.Evaluate(ReachEvent(20.0), buffer, 20.0);
            Assert.IsFalse(score.Accepted);
            Assert.AreEqual("max-simultaneous", score.RejectReason);
        }
    }

    public class RetrievalRankingTests
    {
        [Test]
        public void Retrieve_Returns_Only_Supported_Memes()
        {
            var catalog = DefaultMemeCatalog.Build();
            var retriever = new LocalMemeRetriever();
            var e = new SceneEvent(1, EventType.PersonReachedTowardObject, 1.0, EventPhase.Triggered, 0.9f);
            e.Participants.Add("P1");

            var output = new List<MemeDefinition>();
            retriever.Retrieve(e, catalog, output);

            Assert.Greater(output.Count, 0);
            foreach (var m in output)
            {
                Assert.IsTrue(m.SupportsEvent(EventType.PersonReachedTowardObject));
                Assert.IsTrue(m.PeopleCountAllowed(1));
            }
        }

        [Test]
        public void Ranker_Is_Deterministic_For_Same_Seed()
        {
            var catalog = DefaultMemeCatalog.Build();
            var retriever = new LocalMemeRetriever();
            var e = new SceneEvent(1, EventType.PersonReachedTowardObject, 1.0, EventPhase.Triggered, 0.9f);
            e.Participants.Add("P1");
            var retrieved = new List<MemeDefinition>();
            retriever.Retrieve(e, catalog, retrieved);

            var session = new SessionState();

            var r1 = new DeterministicMemeRanker(new DeterministicRandom(99));
            var r2 = new DeterministicMemeRanker(new DeterministicRandom(99));
            var o1 = new List<MemeCandidate>();
            var o2 = new List<MemeCandidate>();
            r1.Rank(e, retrieved, session, 1.0, o1);
            r2.Rank(e, retrieved, session, 1.0, o2);

            Assert.AreEqual(o1.Count, o2.Count);
            Assert.AreEqual(o1[0].Meme.id, o2[0].Meme.id);
        }

        [Test]
        public void Recently_Used_Meme_Is_Penalized()
        {
            var catalog = DefaultMemeCatalog.Build();
            var retriever = new LocalMemeRetriever();
            var e = new SceneEvent(1, EventType.PersonReachedTowardObject, 1.0, EventPhase.Triggered, 0.9f);
            e.Participants.Add("P1");
            var retrieved = new List<MemeDefinition>();
            retriever.Retrieve(e, catalog, retrieved);

            var session = new SessionState();
            var ranker = new DeterministicMemeRanker(new DeterministicRandom(5), recentUsagePenalty: 5f, jitter: 0f);

            var baseline = new List<MemeCandidate>();
            ranker.Rank(e, retrieved, session, 1.0, baseline);
            string topId = baseline[0].Meme.id;

            // Mark the top meme as just used; it should no longer be first.
            session.RecordMemeShown(topId, 1.0);
            var after = new List<MemeCandidate>();
            ranker.Rank(e, retrieved, session, 1.0, after);

            Assert.AreNotEqual(topId, after[0].Meme.id, "recently used meme should be demoted");
        }
    }

    public class CandidateCacheTests
    {
        [Test]
        public void Prepare_Then_Consume_By_Correlation()
        {
            var catalog = DefaultMemeCatalog.Build();
            var retriever = new LocalMemeRetriever();
            var ranker = new DeterministicMemeRanker(new DeterministicRandom(3));
            var cache = new ReactionCandidateCache(retriever, ranker, ttlSeconds: 2.0);
            var session = new SessionState();

            var potential = new PotentialEvent(42, EventType.PersonReachedTowardObject, 0.8, 1.5, "P1", "O1");
            cache.Prepare(potential, catalog, session, 1.0);
            Assert.AreEqual(1, cache.Count);

            var candidates = cache.Consume(42, 1.4);
            Assert.IsNotNull(candidates);
            Assert.Greater(candidates.Count, 0);

            // Consumed entries are removed.
            Assert.IsNull(cache.Consume(42, 1.4));
        }

        [Test]
        public void Expired_Entries_Are_Dropped()
        {
            var catalog = DefaultMemeCatalog.Build();
            var cache = new ReactionCandidateCache(new LocalMemeRetriever(), new DeterministicMemeRanker(new DeterministicRandom(3)), ttlSeconds: 1.0);
            var session = new SessionState();

            cache.Prepare(new PotentialEvent(7, EventType.SuddenMotion, 0.7, 1.5, "P1", null), catalog, session, 1.0);
            cache.Expire(3.0); // well past ttl
            Assert.AreEqual(0, cache.Count);
            Assert.IsNull(cache.Consume(7, 3.0));
        }
    }
}
