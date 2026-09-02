using System;
using System.Diagnostics;

namespace MemeAR.Infrastructure
{
    /// <summary>
    /// Lightweight stage timing used to keep the pipeline honest against the latency
    /// budget (scene update, event detection, retrieval, ranking, timing, placement,
    /// render activation). Records the last measured duration per named stage so the
    /// debug HUD can surface where computational latency is being spent.
    ///
    /// This measures COMPUTATIONAL latency only. Comedic delay is scheduled separately
    /// by the timing engine and is never derived from these measurements.
    /// </summary>
    public sealed class StageProfiler
    {
        public readonly struct StageSample
        {
            public readonly string Stage;
            public readonly double LastMilliseconds;
            public readonly double MaxMilliseconds;

            public StageSample(string stage, double last, double max)
            {
                Stage = stage;
                LastMilliseconds = last;
                MaxMilliseconds = max;
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, double> _last =
            new System.Collections.Generic.Dictionary<string, double>(16);

        private readonly System.Collections.Generic.Dictionary<string, double> _max =
            new System.Collections.Generic.Dictionary<string, double>(16);

        public void Record(string stage, double milliseconds)
        {
            _last[stage] = milliseconds;
            if (!_max.TryGetValue(stage, out double m) || milliseconds > m)
            {
                _max[stage] = milliseconds;
            }
        }

        public double LastMilliseconds(string stage)
        {
            return _last.TryGetValue(stage, out double v) ? v : 0.0;
        }

        public void CopyInto(System.Collections.Generic.List<StageSample> destination)
        {
            destination.Clear();
            foreach (var kvp in _last)
            {
                double max = _max.TryGetValue(kvp.Key, out double m) ? m : kvp.Value;
                destination.Add(new StageSample(kvp.Key, kvp.Value, max));
            }
        }

        /// <summary>
        /// Times the supplied action and records it under <paramref name="stage"/>.
        /// Prefer <see cref="Measure"/> for zero-alloc scoping in hot paths.
        /// </summary>
        public Measurement Measure(string stage)
        {
            return new Measurement(this, stage);
        }

        public readonly struct Measurement : IDisposable
        {
            private readonly StageProfiler _profiler;
            private readonly string _stage;
            private readonly long _start;

            public Measurement(StageProfiler profiler, string stage)
            {
                _profiler = profiler;
                _stage = stage;
                _start = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                long elapsed = Stopwatch.GetTimestamp() - _start;
                double ms = elapsed * 1000.0 / Stopwatch.Frequency;
                _profiler.Record(_stage, ms);
            }
        }
    }

    /// <summary>Canonical stage names so HUD and profiler agree on keys.</summary>
    public static class PipelineStages
    {
        public const string SceneUpdate = "scene.update";
        public const string EventDetection = "event.detect";
        public const string Prediction = "prediction";
        public const string Opportunity = "opportunity";
        public const string Retrieval = "retrieval";
        public const string Ranking = "ranking";
        public const string Timing = "timing";
        public const string Placement = "placement";
        public const string RenderActivation = "render.activate";
    }
}
