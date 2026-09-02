using System.Collections.Generic;
using UnityEngine;
using MemeAR.AR;
using MemeAR.Comedy;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Placement;
using MemeAR.Prediction;
using MemeAR.Rendering;
using MemeAR.Retrieval;
using MemeAR.Scene;
using MemeAR.Temporal;
using MemeAR.Timing;

namespace MemeAR.Pipeline
{
    /// <summary>
    /// Wires a fully-configured <see cref="MemePipeline"/> from a config, a spatial provider,
    /// a renderer and a catalog. Centralizing construction here means the app and the tests
    /// build the exact same pipeline, and component swaps (e.g. a learned predictor) happen
    /// in one place.
    /// </summary>
    public static class PipelineFactory
    {
        public sealed class Assembled
        {
            public MemePipeline Pipeline;
            public TemporalSceneBuffer Buffer;
            public EventEngine Events;
            public ReactionCandidateCache Cache;
            public IReadOnlyList<MemeDefinition> Catalog;
            public DeterministicRandom Random;
        }

        public static Assembled Build(
            MemeArConfig config,
            IARSpatialProvider spatial,
            IMemeRenderer renderer,
            IReadOnlyList<MemeDefinition> catalog,
            StageProfiler profiler)
        {
            uint seed = config.randomSeed != 0 ? config.randomSeed : (uint)System.Environment.TickCount;
            var random = new DeterministicRandom(seed);

            int frameCapacity = Mathf.Max(2, Mathf.RoundToInt(config.frameMemorySeconds * config.observationHz) + 1);
            var buffer = new TemporalSceneBuffer(frameCapacity, 64);

            var relations = new RelationExtractor(config);

            var ids = new EventIdGenerator();
            var events = new EventEngine();
            events.AddDetector(new PresenceDetector(ids));
            events.AddDetector(new ObjectDetectorEvents(ids));
            events.AddDetector(new ReachTowardObjectDetector(ids));
            events.AddDetector(new AttentionShiftDetector(ids));
            events.AddDetector(new GroupAttentionDetector(ids));
            events.AddDetector(new SuddenMotionDetector(ids));

            var predictor = new HeuristicEventPredictor(config);
            var opportunity = new ComedyOpportunityEngine(config);

            var retriever = new LocalMemeRetriever();
            var ranker = new DeterministicMemeRanker(random);
            var cache = new ReactionCandidateCache(retriever, ranker);

            var timing = new ComedyTimingPolicy(config, random);
            var placement = new PlacementSolver(config, spatial);
            var director = new ARDirector(placement, random);

            var resolvedCatalog = (catalog != null && catalog.Count > 0) ? catalog : DefaultMemeCatalog.Build();

            var pipeline = new MemePipeline(
                config, buffer, relations, events, predictor, opportunity,
                retriever, ranker, cache, timing, director, renderer, resolvedCatalog, profiler);

            return new Assembled
            {
                Pipeline = pipeline,
                Buffer = buffer,
                Events = events,
                Cache = cache,
                Catalog = resolvedCatalog,
                Random = random
            };
        }
    }
}
