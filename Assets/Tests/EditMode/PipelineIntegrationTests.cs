using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MemeAR.Infrastructure;
using MemeAR.Pipeline;
using MemeAR.Rendering;
using MemeAR.Simulation;

namespace MemeAR.Tests
{
    /// <summary>Captures scheduled instructions so the pipeline can be driven headless.</summary>
    internal sealed class MockRenderer : IMemeRenderer
    {
        public readonly List<MemeRenderInstruction> Scheduled = new List<MemeRenderInstruction>();
        public int ActiveCount { get; private set; }
        public event Action<string, double> CardShown;
        public event Action<string> CardFinished;

        public void Schedule(in MemeRenderInstruction instruction, double triggerTime)
        {
            Scheduled.Add(instruction);
            ActiveCount++;
            // Simulate immediate visibility for latency telemetry.
            CardShown?.Invoke(instruction.MemeId, 1.0);
        }

        public void Finish(string memeId)
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            CardFinished?.Invoke(memeId);
        }
    }

    public class PipelineIntegrationTests
    {
        [Test]
        public void Simulation_Drives_Pipeline_To_Schedule_A_Meme()
        {
            var config = TestFactory.Config();
            config.startInSimulation = true;

            var spatial = new SimulatedSpatialProvider(60f, new Vector2(1080f, 1920f));
            var renderer = new MockRenderer();
            var profiler = new StageProfiler();

            PipelineFactory.Assembled assembled = PipelineFactory.Build(config, spatial, renderer, null, profiler);
            MemePipeline pipeline = assembled.Pipeline;

            var provider = new SimulatedObservationProvider(config, spatial);
            provider.StartProvider();

            // Drive ~12 seconds at 30 Hz so multiple scenarios (incl. the reach) play out.
            double now = 0.0;
            float dt = 1f / 30f;
            int steps = (int)(12.0 / dt);
            for (int i = 0; i < steps; i++)
            {
                now += dt;
                var snapshot = provider.Tick(dt, now);
                pipeline.ProcessSnapshot(snapshot, now);
            }

            Assert.Greater(renderer.Scheduled.Count, 0, "expected at least one meme scheduled across the scenarios");

            // Every scheduled instruction must be valid data.
            foreach (var instr in renderer.Scheduled)
            {
                Assert.IsTrue(RenderInstructionValidator.Validate(instr, out string reason), reason);
            }
        }

        [Test]
        public void Cooldown_Prevents_Meme_Spam()
        {
            var config = TestFactory.Config();
            config.globalMemeCooldownSeconds = 5f;

            var spatial = new SimulatedSpatialProvider();
            var renderer = new MockRenderer();
            var profiler = new StageProfiler();
            PipelineFactory.Assembled assembled = PipelineFactory.Build(config, spatial, renderer, null, profiler);
            MemePipeline pipeline = assembled.Pipeline;

            var provider = new SimulatedObservationProvider(config, spatial);
            provider.StartProvider();

            double now = 0.0;
            float dt = 1f / 30f;
            int steps = (int)(5.0 / dt); // within a single global cooldown window
            for (int i = 0; i < steps; i++)
            {
                now += dt;
                pipeline.ProcessSnapshot(provider.Tick(dt, now), now);
            }

            // With a 5s global cooldown, at most one meme can have been committed in 5s.
            Assert.LessOrEqual(renderer.Scheduled.Count, 1);
        }
    }
}
