using System;

namespace ProxyNote
{
    internal static class TrajectoryTiming
    {
        private const float Epsilon = 0.0001f;

        internal static float CalculateEffectiveNjs(
            float jumpStartZ,
            float jumpEndZ,
            float jumpDuration)
        {
            if (!IsFinite(jumpDuration) || jumpDuration <= Epsilon)
            {
                return 0f;
            }

            float jumpDistance = Math.Abs(jumpEndZ - jumpStartZ);
            return IsFinite(jumpDistance) ? jumpDistance / jumpDuration : 0f;
        }

        internal static float CalculateLeadTime(
            float leadDistance,
            float effectiveNjs)
        {
            if (!IsFinite(leadDistance) ||
                !IsFinite(effectiveNjs) ||
                leadDistance <= 0f ||
                effectiveNjs <= Epsilon)
            {
                return 0f;
            }

            return leadDistance / effectiveNjs;
        }

        internal static bool ShouldWaitForFloorMovement(
            float songTime,
            float noteTime,
            float spawnAheadTime,
            float waitingDuration)
        {
            if (!IsFinite(songTime) ||
                !IsFinite(noteTime) ||
                !IsFinite(spawnAheadTime) ||
                !IsFinite(waitingDuration))
            {
                return false;
            }

            return
                songTime - (noteTime - spawnAheadTime) <
                waitingDuration;
        }

        internal static float CalculateFloorMovementStartTime(
            float noteTime,
            float halfJumpDuration,
            float moveDuration)
        {
            if (!IsFinite(noteTime) ||
                !IsFinite(halfJumpDuration) ||
                !IsFinite(moveDuration))
            {
                return noteTime;
            }

            return noteTime - halfJumpDuration - moveDuration;
        }

        internal static float ClampLeadTimeToAvailable(
            float requestedLeadTime,
            float proxySpawnTime,
            float noteTime,
            float halfJumpDuration)
        {
            if (!IsFinite(requestedLeadTime) ||
                !IsFinite(proxySpawnTime) ||
                !IsFinite(noteTime) ||
                !IsFinite(halfJumpDuration) ||
                requestedLeadTime <= 0f ||
                halfJumpDuration < 0f)
            {
                return 0f;
            }

            float vanillaJumpStartTime = noteTime - halfJumpDuration;
            float availableLeadTime = Math.Max(
                0f,
                vanillaJumpStartTime - proxySpawnTime);
            return Math.Min(requestedLeadTime, availableLeadTime);
        }

        internal static float CalculateFloorProgressAtSongTime(
            float songTime,
            float noteTime,
            float halfJumpDuration,
            float moveDuration)
        {
            if (!IsFinite(songTime) ||
                !IsFinite(noteTime) ||
                !IsFinite(halfJumpDuration) ||
                !IsFinite(moveDuration) ||
                moveDuration <= Epsilon)
            {
                return 1f;
            }

            float floorStartTime =
                noteTime -
                halfJumpDuration -
                moveDuration;
            float progress =
                (songTime - floorStartTime) /
                moveDuration;
            return Math.Max(0f, Math.Min(1f, progress));
        }

        internal static float CalculateTimeWarpExponent(
            float halfJumpDuration,
            float leadTime)
        {
            const float minimumExponent = 8f;
            const float targetHeightAtOriginalEnd = 0.995f;

            if (!IsFinite(halfJumpDuration) ||
                !IsFinite(leadTime) ||
                halfJumpDuration <= Epsilon ||
                leadTime <= Epsilon)
            {
                return minimumExponent;
            }

            float durationRatio = leadTime / halfJumpDuration;
            float targetProgress =
                1f - (float)Math.Sqrt(1f - targetHeightAtOriginalEnd);
            float allowedWarpAtTarget =
                (1f - targetProgress) / durationRatio;
            if (allowedWarpAtTarget >= 1f)
            {
                return minimumExponent;
            }

            float requiredExponent =
                (float)(Math.Log(allowedWarpAtTarget) /
                        Math.Log(targetProgress));
            if (!IsFinite(requiredExponent))
            {
                return minimumExponent;
            }

            return Math.Max(minimumExponent, requiredExponent);
        }

        internal static float EvaluateTimeWarpedHeight(
            float elapsedTime,
            float halfJumpDuration,
            float leadTime,
            float startY,
            float endY)
        {
            if (!IsFinite(endY))
            {
                return 0f;
            }

            if (!IsFinite(elapsedTime) ||
                !IsFinite(halfJumpDuration) ||
                !IsFinite(leadTime) ||
                !IsFinite(startY) ||
                halfJumpDuration <= Epsilon)
            {
                return endY;
            }

            float progress = SolveTimeWarpProgress(
                elapsedTime,
                halfJumpDuration,
                Math.Max(0f, leadTime));
            float heightProgress =
                2f * progress - progress * progress;
            float height =
                startY + (endY - startY) * heightProgress;
            return IsFinite(height) ? height : endY;
        }

        internal static float CalculateTimeWarpedJumpProgress(
            float songTime,
            float noteTime,
            float halfJumpDuration,
            float jumpDuration,
            float leadTime)
        {
            if (!IsFinite(songTime) ||
                !IsFinite(noteTime) ||
                !IsFinite(halfJumpDuration) ||
                !IsFinite(jumpDuration) ||
                halfJumpDuration <= Epsilon ||
                jumpDuration <= Epsilon)
            {
                return -1f;
            }

            if (songTime > noteTime)
            {
                return
                    (songTime - (noteTime - halfJumpDuration)) /
                    jumpDuration;
            }

            float safeLeadTime =
                IsFinite(leadTime) ? Math.Max(0f, leadTime) : 0f;
            float visualStartTime =
                noteTime - halfJumpDuration - safeLeadTime;
            if (songTime < visualStartTime)
            {
                return -1f;
            }

            float elapsedTime = songTime - visualStartTime;
            return 0.5f * SolveTimeWarpProgress(
                elapsedTime,
                halfJumpDuration,
                safeLeadTime);
        }

        internal static float CalculateVisualJumpProgress(
            float songTime,
            float noteTime,
            float halfJumpDuration,
            float jumpDuration,
            float leadTime)
        {
            if (!IsFinite(songTime) ||
                !IsFinite(noteTime) ||
                !IsFinite(halfJumpDuration) ||
                !IsFinite(jumpDuration) ||
                halfJumpDuration <= Epsilon ||
                jumpDuration <= Epsilon)
            {
                return -1f;
            }

            if (songTime > noteTime)
            {
                return
                    (songTime - (noteTime - halfJumpDuration)) /
                    jumpDuration;
            }

            float safeLeadTime =
                IsFinite(leadTime) ? Math.Max(0f, leadTime) : 0f;
            float visualHalfDuration = halfJumpDuration + safeLeadTime;
            float visualStartTime = noteTime - visualHalfDuration;
            if (songTime < visualStartTime)
            {
                return -1f;
            }

            float progress =
                (songTime - visualStartTime) /
                (visualHalfDuration * 2f);
            return Math.Max(0f, Math.Min(0.5f, progress));
        }

        internal static float EvaluatePositionSwap(
            bool preserveSwaps,
            float startX,
            float endX,
            float jumpProgress)
        {
            if (!IsFinite(endX))
            {
                return 0f;
            }

            if (!preserveSwaps)
            {
                return endX;
            }

            if (!IsFinite(startX) || !IsFinite(jumpProgress))
            {
                return endX;
            }

            if (jumpProgress <= 0f)
            {
                return startX;
            }

            if (startX == endX || jumpProgress >= 0.25f)
            {
                return endX;
            }

            float progress = Math.Max(
                0f,
                Math.Min(1f, jumpProgress * 4f));
            float easedProgress = progress < 0.5f
                ? 2f * progress * progress
                : -1f + (4f - 2f * progress) * progress;
            return startX + (endX - startX) * easedProgress;
        }

        internal static float EvaluateSwapAvoidance(
            bool preserveSwaps,
            float yAvoidance,
            float jumpProgress)
        {
            if (!preserveSwaps ||
                !IsFinite(yAvoidance) ||
                !IsFinite(jumpProgress) ||
                yAvoidance == 0f ||
                jumpProgress <= 0f ||
                jumpProgress >= 0.25f)
            {
                return 0f;
            }

            float avoidanceProgress =
                0.5f -
                (float)Math.Cos(jumpProgress * 8f * Math.PI) * 0.5f;
            return avoidanceProgress * yAvoidance;
        }

        internal static float CalculateVanillaStartToMiddleRotationProgress(
            float jumpProgress)
        {
            return (float)Math.Sin(
                jumpProgress * Math.PI * 4f);
        }

        internal static float CalculateVanillaMiddleToEndRotationProgress(
            float jumpProgress)
        {
            return (float)Math.Sin(
                (jumpProgress - 0.125f) * Math.PI * 2f);
        }

        internal static float EvaluateAdvancedDepth(
            float songTime,
            float spawnTime,
            float advancedStartTime,
            float originalJumpStartTime,
            float hitTime,
            float spawnZ,
            float advancedStartZ,
            float originalZ,
            float jumpVelocityZ)
        {
            float safeOriginalZ = IsFinite(originalZ) ? originalZ : 0f;
            if (!IsFinite(songTime) ||
                !IsFinite(spawnTime) ||
                !IsFinite(advancedStartTime) ||
                !IsFinite(originalJumpStartTime) ||
                !IsFinite(hitTime) ||
                !IsFinite(spawnZ) ||
                !IsFinite(advancedStartZ) ||
                !IsFinite(jumpVelocityZ) ||
                originalJumpStartTime > hitTime)
            {
                return safeOriginalZ;
            }

            if (songTime >= originalJumpStartTime)
            {
                return safeOriginalZ;
            }

            if (advancedStartTime <= spawnTime + Epsilon)
            {
                return advancedStartZ +
                    jumpVelocityZ * (songTime - advancedStartTime);
            }

            if (songTime <= spawnTime)
            {
                return spawnZ;
            }

            if (songTime < advancedStartTime)
            {
                float progress =
                    (songTime - spawnTime) /
                    (advancedStartTime - spawnTime);
                progress = Math.Max(0f, Math.Min(1f, progress));
                return spawnZ +
                    (advancedStartZ - spawnZ) * progress;
            }

            return advancedStartZ +
                jumpVelocityZ * (songTime - advancedStartTime);
        }

        internal static float EvaluateLeadAwareHeight(
            float elapsedTime,
            float duration,
            float startY,
            float endY,
            float leadDistance,
            float maximumLeadDistance)
        {
            if (!IsFinite(endY))
            {
                return 0f;
            }

            if (!IsFinite(elapsedTime) ||
                !IsFinite(duration) ||
                !IsFinite(startY) ||
                duration <= Epsilon)
            {
                return endY;
            }

            if (elapsedTime <= 0f)
            {
                return startY;
            }

            if (elapsedTime >= duration)
            {
                return endY;
            }

            float progress = elapsedTime / duration;
            float vanillaProgress =
                2f * progress - progress * progress;
            float smoothProgress =
                progress * progress * progress *
                (progress * (progress * 6f - 15f) + 10f);
            float leadBlend =
                !IsFinite(leadDistance) ||
                !IsFinite(maximumLeadDistance) ||
                maximumLeadDistance <= Epsilon
                    ? 0f
                    : Math.Max(
                        0f,
                        Math.Min(1f, leadDistance / maximumLeadDistance));
            float heightProgress =
                vanillaProgress +
                (smoothProgress - vanillaProgress) * leadBlend;
            float height =
                startY + (endY - startY) * heightProgress;
            return IsFinite(height) ? height : endY;
        }

        internal static float EvaluateQuinticHeight(
            float elapsedTime,
            float duration,
            float startY,
            float endY,
            float startVelocity,
            float endVelocity,
            float startAcceleration,
            float endAcceleration)
        {
            if (!IsFinite(endY))
            {
                return 0f;
            }

            if (!IsFinite(elapsedTime) ||
                !IsFinite(duration) ||
                !IsFinite(startY) ||
                !IsFinite(startVelocity) ||
                !IsFinite(endVelocity) ||
                !IsFinite(startAcceleration) ||
                !IsFinite(endAcceleration) ||
                duration <= Epsilon)
            {
                return endY;
            }

            if (elapsedTime <= 0f)
            {
                return startY;
            }

            if (elapsedTime >= duration)
            {
                return endY;
            }

            float durationSquared = duration * duration;
            float c0 = startY;
            float c1 = startVelocity * duration;
            float c2 = 0.5f * startAcceleration * durationSquared;
            float positionRemainder = endY - c0 - c1 - c2;
            float velocityRemainder =
                endVelocity * duration -
                c1 -
                2f * c2;
            float accelerationRemainder =
                endAcceleration * durationSquared -
                2f * c2;
            float c3 =
                10f * positionRemainder -
                4f * velocityRemainder +
                0.5f * accelerationRemainder;
            float c4 =
                -15f * positionRemainder +
                7f * velocityRemainder -
                accelerationRemainder;
            float c5 =
                6f * positionRemainder -
                3f * velocityRemainder +
                0.5f * accelerationRemainder;
            float progress = elapsedTime / duration;
            float height =
                ((((c5 * progress + c4) * progress + c3) * progress + c2) *
                 progress + c1) *
                progress +
                c0;
            return IsFinite(height) ? height : endY;
        }

        private static float SolveTimeWarpProgress(
            float elapsedTime,
            float halfJumpDuration,
            float leadTime)
        {
            if (elapsedTime <= 0f)
            {
                return 0f;
            }

            float totalDuration = halfJumpDuration + leadTime;
            if (elapsedTime >= totalDuration)
            {
                return 1f;
            }

            if (leadTime <= Epsilon)
            {
                return Math.Max(
                    0f,
                    Math.Min(1f, elapsedTime / halfJumpDuration));
            }

            float exponent = CalculateTimeWarpExponent(
                halfJumpDuration,
                leadTime);
            float lower = 0f;
            float upper = 1f;
            for (int iteration = 0; iteration < 28; iteration++)
            {
                float candidate = (lower + upper) * 0.5f;
                float candidateTime =
                    halfJumpDuration * candidate +
                    leadTime *
                    (float)Math.Pow(candidate, exponent);
                if (candidateTime < elapsedTime)
                {
                    lower = candidate;
                }
                else
                {
                    upper = candidate;
                }
            }

            return (lower + upper) * 0.5f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
