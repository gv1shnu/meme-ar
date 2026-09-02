using NUnit.Framework;
using UnityEngine;
using MemeAR.Infrastructure;
using MemeAR.Placement;
using MemeAR.Rendering;

namespace MemeAR.Tests
{
    public class RenderInstructionValidationTests
    {
        private static MemeRenderInstruction Base(
            string id = "m",
            float scale = 1f,
            float duration = 4f,
            PlacementMode mode = PlacementMode.ScreenSpace,
            Pose? world = null)
        {
            return new MemeRenderInstruction(
                id, "cap", Color.white, null,
                RenderTargetType.Screen, null,
                mode, new Vector2(0.5f, 0.5f), world, Vector3.zero, scale,
                showAt: 10.0, duration: duration, animationStyle: AnimationStyle.Pop,
                faceCamera: false, trackingBehavior: TrackingBehavior.Static);
        }

        [Test]
        public void Valid_Instruction_Passes()
        {
            Assert.IsTrue(RenderInstructionValidator.Validate(Base(), out _));
        }

        [Test]
        public void Empty_Id_Fails()
        {
            Assert.IsFalse(RenderInstructionValidator.Validate(Base(id: ""), out string reason));
            StringAssert.Contains("id", reason);
        }

        [Test]
        public void Out_Of_Range_Scale_Fails()
        {
            Assert.IsFalse(RenderInstructionValidator.Validate(Base(scale: 999f), out _));
            Assert.IsFalse(RenderInstructionValidator.Validate(Base(scale: 0.001f), out _));
        }

        [Test]
        public void World_Mode_Without_Pose_Fails()
        {
            Assert.IsFalse(RenderInstructionValidator.Validate(Base(mode: PlacementMode.AbovePerson, world: null), out string reason));
            StringAssert.Contains("world", reason);
        }

        [Test]
        public void World_Mode_With_Pose_Passes()
        {
            Assert.IsTrue(RenderInstructionValidator.Validate(Base(mode: PlacementMode.AbovePerson, world: Pose.identity), out _));
        }
    }

    public class DeterministicRandomTests
    {
        [Test]
        public void Same_Seed_Reproduces_Sequence()
        {
            var a = new DeterministicRandom(1234);
            var b = new DeterministicRandom(1234);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void Different_Seeds_Diverge()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);
            bool anyDifferent = false;
            for (int i = 0; i < 10; i++)
            {
                if (a.NextUInt() != b.NextUInt())
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void NextFloat_In_Unit_Interval()
        {
            var r = new DeterministicRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                float f = r.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void Range_Int_Respects_Bounds()
        {
            var r = new DeterministicRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Range(3, 8);
                Assert.GreaterOrEqual(v, 3);
                Assert.Less(v, 8);
            }
        }
    }
}
