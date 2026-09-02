using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Rendering;
using MemeAR.Retrieval;
using MemeAR.Temporal;

namespace MemeAR.Tests
{
    public class MemePackParserTests
    {
        private const string Json =
            "{\"pack\":\"hype\",\"memes\":[{" +
            "\"id\":\"hype_wow\",\"title\":\"WOW\",\"clip\":\"wow.mp4\"," +
            "\"source\":\"Source: @creator (YouTube)\",\"blend\":\"chroma\"," +
            "\"tags\":[\"surprise\"],\"events\":[\"SuddenMotion\"]," +
            "\"captions\":[\"WOW\"],\"durationSec\":3.5}]}";

        [Test]
        public void Parses_Video_Meme_With_Attribution_And_Blend()
        {
            List<MemeDefinition> memes = MemePackParser.Parse(Json, "MemePacks/hype");
            Assert.AreEqual(1, memes.Count);

            MemeDefinition m = memes[0];
            Assert.AreEqual("hype_wow", m.id);
            Assert.AreEqual("hype", m.pack);
            Assert.AreEqual("Source: @creator (YouTube)", m.attribution);
            Assert.AreEqual(MediaKind.Video, m.media.kind);
            Assert.AreEqual(BackgroundBlend.ChromaKey, m.media.backgroundBlend);
            Assert.AreEqual("MemePacks/hype/wow.mp4", m.media.streamingAssetsPath);
            Assert.IsTrue(m.SupportsEvent(EventType.SuddenMotion));
            Assert.AreEqual(3.5f, m.preferredDurationSeconds, 1e-4);
        }

        [Test]
        public void Malformed_Json_Returns_Empty_Not_Throws()
        {
            Assert.AreEqual(0, MemePackParser.Parse("not json at all", null).Count);
            Assert.AreEqual(0, MemePackParser.Parse("", null).Count);
        }
    }

    public class ScenePackConsistencyTests
    {
        private static MemeDefinition Meme(string id, string pack)
        {
            var m = ScriptableObject.CreateInstance<MemeDefinition>();
            m.id = id;
            m.pack = pack;
            m.weight = 1f;
            m.supportedEventTypes = new List<EventType> { EventType.SuddenMotion };
            m.tags = new List<string>();
            return m;
        }

        [Test]
        public void Locked_Pack_Is_Boosted_In_Ranking()
        {
            var retrieved = new List<MemeDefinition> { Meme("a", "awkward"), Meme("b", "hype") };
            var e = new SceneEvent(1, EventType.SuddenMotion, 1.0, EventPhase.Triggered, 0.9f);

            var session = new SessionState { ScenePackId = "hype" };
            var ranker = new DeterministicMemeRanker(new DeterministicRandom(1), jitter: 0f, scenePackBias: 5f);

            var output = new List<MemeCandidate>();
            ranker.Rank(e, retrieved, session, 1.0, output);

            Assert.AreEqual("b", output[0].Meme.id, "meme from the locked pack should rank first");
        }
    }

    public class DefaultCatalogMediaTests
    {
        [Test]
        public void All_Default_Memes_Have_Pack_And_Media()
        {
            foreach (var m in DefaultMemeCatalog.Build())
            {
                Assert.IsFalse(string.IsNullOrEmpty(m.pack), $"{m.id} missing pack");
                Assert.IsNotNull(m.media, $"{m.id} missing media");
                Assert.AreEqual(MediaKind.GeneratedCard, m.media.kind);
            }
        }
    }
}
