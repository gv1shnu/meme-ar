using UnityEngine;
using MemeAR.Placement;

namespace MemeAR.Rendering
{
    /// <summary>
    /// Validates a <see cref="MemeRenderInstruction"/> before the renderer acts on it. This
    /// is the trust boundary: whether an instruction is produced locally or (later) returned
    /// by a remote service, it is validated as pure data here. It can never carry code or
    /// cause arbitrary behavior — only a bounded, well-formed reaction is accepted.
    /// </summary>
    public static class RenderInstructionValidator
    {
        public const float MinScale = 0.05f;
        public const float MaxScale = 20f;
        public const float MinDuration = 0.1f;
        public const float MaxDuration = 30f;

        public static bool Validate(in MemeRenderInstruction instruction, out string reason)
        {
            if (string.IsNullOrEmpty(instruction.MemeId))
            {
                reason = "missing meme id";
                return false;
            }

            if (!IsFinite(instruction.Scale) || instruction.Scale < MinScale || instruction.Scale > MaxScale)
            {
                reason = "scale out of range";
                return false;
            }

            if (!IsFinite(instruction.Duration) || instruction.Duration < MinDuration || instruction.Duration > MaxDuration)
            {
                reason = "duration out of range";
                return false;
            }

            if (double.IsNaN(instruction.ShowAt) || double.IsInfinity(instruction.ShowAt))
            {
                reason = "invalid show time";
                return false;
            }

            bool needsWorld =
                instruction.PlacementMode == PlacementMode.WorldSpace ||
                instruction.PlacementMode == PlacementMode.AbovePerson ||
                instruction.PlacementMode == PlacementMode.NearPerson ||
                instruction.PlacementMode == PlacementMode.AboveObject ||
                instruction.PlacementMode == PlacementMode.NearObject ||
                instruction.PlacementMode == PlacementMode.DetectedPlane ||
                instruction.PlacementMode == PlacementMode.CameraForwardFallback;

            if (needsWorld && !instruction.WorldPose.HasValue)
            {
                reason = "world placement requires a world pose";
                return false;
            }

            if (instruction.PlacementMode == PlacementMode.ScreenSpace)
            {
                Vector2 a = instruction.ScreenAnchor;
                if (!IsFinite(a.x) || !IsFinite(a.y))
                {
                    reason = "invalid screen anchor";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
