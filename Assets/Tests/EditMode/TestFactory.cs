using UnityEngine;
using MemeAR.Infrastructure;
using MemeAR.Scene;

namespace MemeAR.Tests
{
    /// <summary>Shared helpers for constructing deterministic test fixtures.</summary>
    internal static class TestFactory
    {
        public static MemeArConfig Config()
        {
            var c = ScriptableObject.CreateInstance<MemeArConfig>();
            c.randomSeed = 777;
            return c;
        }

        public static TrackedPerson Person(string id, Vector2 center, Vector2 size, Vector2 velocity, Vector3 head, Vector3? world = null)
        {
            var rect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            return new TrackedPerson
            {
                Id = id,
                NormalizedBounds = rect,
                ScreenVelocity = velocity,
                HeadDirection = head.sqrMagnitude < 1e-4f ? Vector3.forward : head.normalized,
                WorldPosition = world,
                Confidence = 0.9f
            };
        }

        public static TrackedObject Object(string id, string category, Vector2 center, Vector2 size, Vector3? world = null)
        {
            var rect = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            return new TrackedObject
            {
                Id = id,
                Category = category,
                NormalizedBounds = rect,
                WorldPosition = world,
                Confidence = 0.9f
            };
        }

        public static SceneSnapshot Snapshot(double timestamp)
        {
            return new SceneSnapshot { Timestamp = timestamp, CameraPose = Pose.identity };
        }
    }
}
