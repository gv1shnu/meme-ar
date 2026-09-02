using System.Collections.Generic;
using UnityEngine;
using MemeAR.Comedy;
using MemeAR.Events;
using MemeAR.Infrastructure;
using MemeAR.Memes;
using MemeAR.Prediction;
using MemeAR.Rendering;
using MemeAR.Retrieval;
using MemeAR.Scene;
using MemeAR.Temporal;
using MemeAR.Timing;

namespace MemeAR.Pipeline
{
    /// <summary>
    /// The end-to-end orchestrator, written as pure C# (no MonoBehaviour) so it can run in
    /// headless tests. It wires the shared post-snapshot pipeline:
    ///
    ///   RelationExtractor -> TemporalBuffer -> EventEngine -> Predictor(+PrefetchCache)
    ///     -> ComedyOpportunity -> (cached or fresh) Retrieve/Rank -> Timing -> Director
    ///     -> Renderer
    ///
    /// It is driven by <see cref="ProcessSnapshot"/> once per observation tick and is agnostic
    /// to whether the snapshot came from simulation or live AR.
    /// </summary>
    public sealed class MemePipeline
    {
        private readonly MemeArConfig _config;
        private readonly RelationExtractor _relations;
        private readonly EventEngine _events;
        private readonly IEventPredictor _predictor;
        private readonly ComedyOpportunityEngine _opportunity;
        private readonly IMemeRetriever _retriever;
        private readonly IMemeRanker _ranker;
        private readonly ReactionCandidateCache _cache;
        private readonly IComedyTimingPolicy _timing;
        private readonly ARDirector _director;
        private readonly IMemeRenderer _renderer;
        private readonly IReadOnlyList<MemeDefinition> _catalog;
        private readonly StageProfiler _profiler;

        private readonly TemporalSceneBuffer _buffer;

        // Reusable working buffers (avoid per-tick allocation).
        private readonly List<SceneEvent> _tickEvents = new List<SceneEvent>(16);
        private readonly List<PotentialEvent> _potentials = new List<PotentialEvent>(8);
        private readonly List<MemeDefinition> _retrieveScratch = new List<MemeDefinition>(16);
        private readonly List<MemeCandidate> _rankScratch = new List<MemeCandidate>(16);

        private PipelineTelemetry _telemetry;
        private double _lastObservationTime = double.NegativeInfinity;

        public MemePipeline(
            MemeArConfig config,
            TemporalSceneBuffer buffer,
            RelationExtractor relations,
            EventEngine events,
            IEventPredictor predictor,
            ComedyOpportunityEngine opportunity,
            IMemeRetriever retriever,
            IMemeRanker ranker,
            ReactionCandidateCache cache,
            IComedyTimingPolicy timing,
            ARDirector director,
            IMemeRenderer renderer,
            IReadOnlyList<MemeDefinition> catalog,
            StageProfiler profiler)
        {
            _config = config;
            _buffer = buffer;
            _relations = relations;
            _events = events;
            _predictor = predictor;
            _opportunity = opportunity;
            _retriever = retriever;
            _ranker = ranker;
            _cache = cache;
            _timing = timing;
            _director = director;
            _renderer = renderer;
            _catalog = catalog;
            _profiler = profiler;

            if (_renderer != null)
            {
                _renderer.CardShown += OnCardShown;
                _renderer.CardFinished += OnCardFinished;
            }
        }

        public TemporalSceneBuffer Buffer => _buffer;
        public PipelineTelemetry Telemetry => _telemetry;

        /// <summary>Runs the full post-snapshot pipeline for one observation tick.</summary>
        public void ProcessSnapshot(SceneSnapshot live, double now)
        {
            if (live == null)
            {
                return;
            }

            if (_lastObservationTime > double.NegativeInfinity)
            {
                double dt = now - _lastObservationTime;
                if (dt > 0.0)
                {
                    _telemetry.ObservationRateHz = 1.0 / dt;
                }
            }

            _lastObservationTime = now;

            // 1) Shared relation extraction + temporal memory.
            using (_profiler.Measure(PipelineStages.SceneUpdate))
            {
                _relations.Extract(live);
                _buffer.PushFrame(live);
            }

            _telemetry.PeopleCount = live.People.Count;
            _telemetry.ObjectCount = live.Objects.Count;
            _telemetry.RelationCount = live.Relations.Count;

            // 2) Event detection.
            _tickEvents.Clear();
            using (_profiler.Measure(PipelineStages.EventDetection))
            {
                _events.Update(_buffer, now, _tickEvents);
            }

            // 3) Prediction -> prefetch candidate cache (before triggers confirm).
            _potentials.Clear();
            using (_profiler.Measure(PipelineStages.Prediction))
            {
                _predictor.Predict(_buffer, _tickEvents, now, _potentials);
                for (int i = 0; i < _potentials.Count; i++)
                {
                    _cache.Prepare(_potentials[i], _catalog, _buffer.Session, now);
                }
            }

            _telemetry.PredictedEventCount = _potentials.Count;
            _telemetry.PreparedCandidateSets = _cache.Count;

            // 4) Confirmed triggers -> opportunity -> selection -> timing -> render.
            for (int i = 0; i < _tickEvents.Count; i++)
            {
                SceneEvent e = _tickEvents[i];
                if (e.Phase != EventPhase.Triggered)
                {
                    continue;
                }

                HandleTriggeredEvent(e, live, now);
            }

            _cache.Expire(now);
            UpdateCooldownTelemetry(now);
        }

        private void HandleTriggeredEvent(SceneEvent e, SceneSnapshot live, double now)
        {
            OpportunityScore score;
            using (_profiler.Measure(PipelineStages.Opportunity))
            {
                score = _opportunity.Evaluate(e, _buffer, now);
            }

            _telemetry.LastConfirmedEvent = e.Type;
            _telemetry.LastConfirmedEventTime = now;
            _telemetry.LastOpportunityScore = score.Total;
            _telemetry.LastOpportunityAccepted = score.Accepted;
            _telemetry.LastRejectReason = score.RejectReason;

            if (!score.Accepted)
            {
                return;
            }

            // Selection: prefer prefetched candidates; otherwise retrieve+rank now.
            MemeDefinition selected = SelectMeme(e, now);
            if (selected == null)
            {
                _telemetry.LastRejectReason = "no-candidate";
                return;
            }

            // Timing decision (deliberate comedic delay; not compute latency).
            float activity = ComputeSceneActivity(live);
            TimingDecision timing;
            using (_profiler.Measure(PipelineStages.Timing))
            {
                timing = _timing.Decide(e, selected, activity, now);
            }

            // Placement + declarative instruction.
            MemeRenderInstruction instruction;
            using (_profiler.Measure(PipelineStages.Placement))
            {
                instruction = _director.Direct(e, selected, live, timing);
            }

            using (_profiler.Measure(PipelineStages.RenderActivation))
            {
                _renderer?.Schedule(instruction, now);
            }

            // Commit cooldowns/counters at schedule time so the comedic-delay window
            // cannot be exploited to fire a burst of memes.
            _buffer.Session.RecordTrigger(e.Type, now);
            _buffer.Session.RecordMemeShown(selected.id, now);
            _buffer.Session.ActiveMemeCount++;

            _telemetry.LastSelectedMemeId = selected.id;
            _telemetry.LastComedicDelayMs = timing.ComedicDelayMs;
        }

        private MemeDefinition SelectMeme(SceneEvent e, double now)
        {
            IReadOnlyList<MemeCandidate> cached = _cache.Consume(e.CorrelationId, now);
            if (cached != null && cached.Count > 0)
            {
                return cached[0].Meme; // prefetched: no retrieval/ranking on the punchline path
            }

            using (_profiler.Measure(PipelineStages.Retrieval))
            {
                _retriever.Retrieve(e, _catalog, _retrieveScratch);
            }

            using (_profiler.Measure(PipelineStages.Ranking))
            {
                _ranker.Rank(e, _retrieveScratch, _buffer.Session, now, _rankScratch);
            }

            return _rankScratch.Count > 0 ? _rankScratch[0].Meme : null;
        }

        private static float ComputeSceneActivity(SceneSnapshot live)
        {
            float activity = 0f;
            for (int i = 0; i < live.People.Count; i++)
            {
                activity += live.People[i].ScreenVelocity.magnitude;
            }

            activity += live.People.Count * 0.1f + live.Relations.Count * 0.05f;
            return Mathf.Clamp01(activity);
        }

        private void UpdateCooldownTelemetry(double now)
        {
            SessionState session = _buffer.Session;
            _telemetry.ActiveMemes = session.ActiveMemeCount;
            _telemetry.TotalMemesShown = session.TotalMemesShown;

            if (session.LastMemeTimestamp > double.NegativeInfinity)
            {
                double remaining = _config.globalMemeCooldownSeconds - (now - session.LastMemeTimestamp);
                _telemetry.GlobalCooldownRemaining = remaining > 0 ? remaining : 0;
            }
        }

        private void OnCardShown(string memeId, double triggerToVisibleMs)
        {
            _telemetry.LastTriggerToVisibleMs = triggerToVisibleMs;
        }

        private void OnCardFinished(string memeId)
        {
            if (_buffer.Session.ActiveMemeCount > 0)
            {
                _buffer.Session.ActiveMemeCount--;
            }
        }

        public void Dispose()
        {
            if (_renderer != null)
            {
                _renderer.CardShown -= OnCardShown;
                _renderer.CardFinished -= OnCardFinished;
            }
        }
    }
}
