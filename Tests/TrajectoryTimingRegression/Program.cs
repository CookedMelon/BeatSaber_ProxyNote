using ProxyNote;

static void AssertNear(string name, float expected, float actual, float tolerance = 0.0001f)
{
    if (!float.IsFinite(actual) ||
        MathF.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException(
            $"{name}: expected {expected}, actual {actual}");
    }
}

static void AssertTrue(string name, bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{name}: expected true");
    }
}

float effectiveNjs = TrajectoryTiming.CalculateEffectiveNjs(
    jumpStartZ: 10f,
    jumpEndZ: -10f,
    jumpDuration: 1f);
AssertNear("effective NJS uses final provider jump", 20f, effectiveNjs);

AssertTrue(
    "practice-seek note remains hidden during vanilla waiting",
    TrajectoryTiming.ShouldWaitForFloorMovement(
        songTime: 57f,
        noteTime: 59.5f,
        spawnAheadTime: 3.5f,
        waitingDuration: 2.4f));
AssertTrue(
    "practice-seek note becomes visible at its own floor start",
    !TrajectoryTiming.ShouldWaitForFloorMovement(
        songTime: 58.4f,
        noteTime: 59.5f,
        spawnAheadTime: 3.5f,
        waitingDuration: 2.4f));
AssertTrue(
    "variable NJS waiting values are evaluated without a fixed duration",
    TrajectoryTiming.ShouldWaitForFloorMovement(
        songTime: 57f,
        noteTime: 58.1f,
        spawnAheadTime: 3.5f,
        waitingDuration: 2.5f));

AssertNear(
    "scheduled floor start is independent of batch initialization time",
    58.4f,
    TrajectoryTiming.CalculateFloorMovementStartTime(
        noteTime: 59.5f,
        halfJumpDuration: 0.6f,
        moveDuration: 0.5f));

float leadTime = TrajectoryTiming.CalculateLeadTime(
    leadDistance: 15f,
    effectiveNjs: effectiveNjs);
AssertNear("15m at 20m/s advances by 0.75s", 0.75f, leadTime);

float lifecycleLimitedLead = TrajectoryTiming.ClampLeadTimeToAvailable(
    requestedLeadTime: leadTime,
    proxySpawnTime: 9f,
    noteTime: 10f,
    halfJumpDuration: 0.5f);
AssertNear(
    "lead time is limited to the proxy lifetime",
    0.5f,
    lifecycleLimitedLead);

AssertNear(
    "lead time below the lifecycle limit is unchanged",
    0.25f,
    TrajectoryTiming.ClampLeadTimeToAvailable(
        requestedLeadTime: 0.25f,
        proxySpawnTime: 9f,
        noteTime: 10f,
        halfJumpDuration: 0.5f));

AssertNear(
    "floor progress starts where the proxy lifecycle starts",
    0f,
    TrajectoryTiming.CalculateFloorProgressAtSongTime(
        songTime: 9f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        moveDuration: 0.5f));

AssertNear(
    "floor progress remains continuous before the jump",
    0.5f,
    TrajectoryTiming.CalculateFloorProgressAtSongTime(
        songTime: 9.25f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        moveDuration: 0.5f));

AssertNear(
    "floor progress reaches the vanilla jump start",
    1f,
    TrajectoryTiming.CalculateFloorProgressAtSongTime(
        songTime: 9.5f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        moveDuration: 0.5f));

AssertNear(
    "lifecycle-limited jump begins at zero progress on the spawn frame",
    0f,
    TrajectoryTiming.CalculateVisualJumpProgress(
        songTime: 9f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        jumpDuration: 1f,
        leadTime: lifecycleLimitedLead));

float vanillaStart = TrajectoryTiming.CalculateVisualJumpProgress(
    songTime: 9.5f,
    noteTime: 10f,
    halfJumpDuration: 0.5f,
    jumpDuration: 1f,
    leadTime: 0f);
AssertNear("zero lead matches vanilla jump start", 0f, vanillaStart);

float vanillaQuarter = TrajectoryTiming.CalculateVisualJumpProgress(
    songTime: 9.75f,
    noteTime: 10f,
    halfJumpDuration: 0.5f,
    jumpDuration: 1f,
    leadTime: 0f);
AssertNear("zero lead matches vanilla quarter progress", 0.25f, vanillaQuarter);

float earlyProgress = TrajectoryTiming.CalculateVisualJumpProgress(
    songTime: 9.5f,
    noteTime: 10f,
    halfJumpDuration: 0.5f,
    jumpDuration: 1f,
    leadTime: leadTime);
AssertNear("positive lead is already moving at vanilla start", 0.3f, earlyProgress);

AssertNear(
    "positive lead remains inactive before visual jump start",
    -1f,
    TrajectoryTiming.CalculateVisualJumpProgress(
        songTime: 8.5f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        jumpDuration: 1f,
        leadTime: leadTime));

float previous = -1f;
for (int sample = 0; sample <= 20; sample++)
{
    float songTime = 8.75f + sample * 0.0625f;
    float progress = TrajectoryTiming.CalculateVisualJumpProgress(
        songTime,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        jumpDuration: 1f,
        leadTime: leadTime);
    AssertTrue("lead progress remains monotonic", progress >= previous);
    previous = progress;
}

AssertNear(
    "lead progress reaches hit phase at note time",
    0.5f,
    TrajectoryTiming.CalculateVisualJumpProgress(
        songTime: 10f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        jumpDuration: 1f,
        leadTime: leadTime));

AssertNear(
    "progress is continuous immediately after the hit",
    0.5001f,
    TrajectoryTiming.CalculateVisualJumpProgress(
        songTime: 10.0001f,
        noteTime: 10f,
        halfJumpDuration: 0.5f,
        jumpDuration: 1f,
        leadTime: lifecycleLimitedLead));

float stableTargetX = 1.5f;
foreach (float jumpProgress in new[] { 0f, 0.0625f, 0.125f, 0.25f, 0.5f })
{
    AssertNear(
        "removed swaps keep one final-lane X trajectory",
        stableTargetX,
        TrajectoryTiming.EvaluatePositionSwap(
            preserveSwaps: false,
            startX: -1.5f,
            endX: stableTargetX,
            jumpProgress));
    AssertNear(
        "removed swaps contain no vertical avoidance pulse",
        0f,
        TrajectoryTiming.EvaluateSwapAvoidance(
            preserveSwaps: false,
            yAvoidance: 0.4f,
            jumpProgress));
}

AssertNear(
    "preserved swaps start at the vanilla start X",
    -1.5f,
    TrajectoryTiming.EvaluatePositionSwap(
        preserveSwaps: true,
        startX: -1.5f,
        endX: stableTargetX,
        jumpProgress: 0f));

AssertNear(
    "preserved swaps use vanilla InOutQuad at midpoint",
    0f,
    TrajectoryTiming.EvaluatePositionSwap(
        preserveSwaps: true,
        startX: -1.5f,
        endX: stableTargetX,
        jumpProgress: 0.125f));

AssertNear(
    "preserved swaps finish at one quarter jump progress",
    stableTargetX,
    TrajectoryTiming.EvaluatePositionSwap(
        preserveSwaps: true,
        startX: -1.5f,
        endX: stableTargetX,
        jumpProgress: 0.25f));

AssertNear(
    "preserved swaps retain the vanilla avoidance peak",
    0.4f,
    TrajectoryTiming.EvaluateSwapAvoidance(
        preserveSwaps: true,
        yAvoidance: 0.4f,
        jumpProgress: 0.125f));

AssertNear(
    "preserved swap avoidance returns to zero",
    0f,
    TrajectoryTiming.EvaluateSwapAvoidance(
        preserveSwaps: true,
        yAvoidance: 0.4f,
        jumpProgress: 0.25f));

float avoidanceStep = 0.001f;
float removedAvoidanceBefore = TrajectoryTiming.EvaluateSwapAvoidance(
    preserveSwaps: false,
    yAvoidance: 0.4f,
    jumpProgress: 0.125f - avoidanceStep);
float removedAvoidanceAt = TrajectoryTiming.EvaluateSwapAvoidance(
    preserveSwaps: false,
    yAvoidance: 0.4f,
    jumpProgress: 0.125f);
float removedAvoidanceAfter = TrajectoryTiming.EvaluateSwapAvoidance(
    preserveSwaps: false,
    yAvoidance: 0.4f,
    jumpProgress: 0.125f + avoidanceStep);
AssertNear(
    "removed swaps add no avoidance velocity",
    0f,
    (removedAvoidanceAfter - removedAvoidanceBefore) /
    (avoidanceStep * 2f));
AssertNear(
    "removed swaps add no avoidance acceleration",
    0f,
    (removedAvoidanceAfter -
     2f * removedAvoidanceAt +
     removedAvoidanceBefore) /
    (avoidanceStep * avoidanceStep));

PluginConfig defaultConfig = new PluginConfig();
AssertNear(
    "new configs default rotation coefficient to 0.2",
    0.2f,
    defaultConfig.NoteRotationCoefficient);
AssertTrue(
    "new configs default to removed position swaps",
    !defaultConfig.EnableNotePositionSwaps);

PluginConfig migratedConfig = new PluginConfig
{
    ConfigVersion = 4,
    JumpLeadDistance = 12f,
    NoteRotationCoefficient = 0.8f,
    EnableNotePositionSwaps = true
};
migratedConfig.ApplyMigrations();
AssertNear(
    "v5 migration clamps jump lead to 5m",
    5f,
    migratedConfig.JumpLeadDistance);
AssertNear(
    "v5 migration applies rotation coefficient 0.2",
    0.2f,
    migratedConfig.NoteRotationCoefficient);
AssertTrue(
    "v5 migration removes position swaps by default",
    !migratedConfig.EnableNotePositionSwaps);
AssertTrue(
    "v5 migration advances config version",
    migratedConfig.ConfigVersion == 5);

PluginConfig resetConfig = new PluginConfig
{
    Enabled = false,
    JumpLeadDistance = 4.5f,
    NoteRotationCoefficient = 0.9f,
    EnableNotePositionSwaps = true,
    GuideEnabled = false,
    ShowProxyOnDesktop = true,
    DebugMode = true,
    OriginalNoteOpacity = 0.8f,
    ProxyNoteOpacity = 0.2f,
    SuppressVanillaDebris = false,
    UseChinese = true,
    LogCalibrationSamples = true
};
resetConfig.ResetToDefaults();
AssertTrue("reset enables the plugin", resetConfig.Enabled);
AssertNear("reset clears jump lead", 0f, resetConfig.JumpLeadDistance);
AssertNear(
    "reset restores rotation coefficient",
    0.2f,
    resetConfig.NoteRotationCoefficient);
AssertTrue(
    "reset removes position swaps",
    !resetConfig.EnableNotePositionSwaps);
AssertTrue("reset enables the guide", resetConfig.GuideEnabled);
AssertTrue(
    "reset restores original PC camera behavior",
    !resetConfig.ShowProxyOnDesktop);
AssertTrue("reset disables debug mode", !resetConfig.DebugMode);
AssertNear(
    "reset restores original opacity",
    0.1f,
    resetConfig.OriginalNoteOpacity);
AssertNear(
    "reset restores proxy opacity",
    1f,
    resetConfig.ProxyNoteOpacity);
AssertTrue(
    "reset hides vanilla debris",
    resetConfig.SuppressVanillaDebris);
AssertTrue("reset restores the default English language", !resetConfig.UseChinese);
AssertTrue(
    "reset disables calibration logging",
    !resetConfig.LogCalibrationSamples);

PluginConfig viewModelConfig = new PluginConfig
{
    Enabled = false,
    JumpLeadDistance = 5f,
    NoteRotationCoefficient = 1f,
    EnableNotePositionSwaps = true,
    GuideEnabled = false,
    ShowProxyOnDesktop = true,
    DebugMode = true,
    OriginalNoteOpacity = 0.8f,
    ProxyNoteOpacity = 0.2f,
    SuppressVanillaDebris = false,
    UseChinese = true
};
PluginConfig.Instance = viewModelConfig;
List<string> resetNotifications = new List<string>();
SettingsViewModel.Instance.PropertyChanged += (_, args) =>
    resetNotifications.Add(args.PropertyName);
typeof(SettingsViewModel)
    .GetMethod(
        "ResetSettings",
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic)
    ?.Invoke(SettingsViewModel.Instance, null);
AssertTrue(
    "reset publishes one all-properties UI refresh",
    resetNotifications.Count == 1 &&
    string.IsNullOrEmpty(resetNotifications[0]));
AssertNear(
    "view-model reset updates jump lead before notifying",
    0f,
    viewModelConfig.JumpLeadDistance);
AssertTrue(
    "view-model reset updates language before notifying",
    !viewModelConfig.UseChinese);

AssertNear(
    "vanilla start-to-middle rotation starts at zero",
    0f,
    TrajectoryTiming.CalculateVanillaStartToMiddleRotationProgress(
        jumpProgress: 0f));
AssertNear(
    "vanilla start-to-middle rotation follows sin at one sixteenth jump progress",
    MathF.Sqrt(0.5f),
    TrajectoryTiming.CalculateVanillaStartToMiddleRotationProgress(
        jumpProgress: 0.0625f),
    tolerance: 0.00001f);
AssertNear(
    "vanilla start-to-middle rotation reaches the middle pose at one eighth jump progress",
    1f,
    TrajectoryTiming.CalculateVanillaStartToMiddleRotationProgress(
        jumpProgress: 0.125f),
    tolerance: 0.00001f);

AssertNear(
    "vanilla middle-to-end rotation starts at zero",
    0f,
    TrajectoryTiming.CalculateVanillaMiddleToEndRotationProgress(
        jumpProgress: 0.125f));
AssertNear(
    "vanilla middle-to-end rotation follows sin at quarter jump progress",
    MathF.Sqrt(0.5f),
    TrajectoryTiming.CalculateVanillaMiddleToEndRotationProgress(
        jumpProgress: 0.25f),
    tolerance: 0.00001f);
AssertNear(
    "vanilla middle-to-end rotation reaches the end pose at three eighths jump progress",
    1f,
    TrajectoryTiming.CalculateVanillaMiddleToEndRotationProgress(
        jumpProgress: 0.375f),
    tolerance: 0.00001f);

float normalizedHeightAtVanillaMiddle =
    4f * 0.125f * (1f - 0.125f);
AssertNear(
    "vanilla middle pose happens at 43.75 percent height as a derived consequence",
    0.4375f,
    normalizedHeightAtVanillaMiddle);

float depthNoteTime = 10f;
float depthNjs = 17f;
float depthHalfDuration = 0.43f;
float depthHalfDistance = depthNjs * depthHalfDuration;
float depthLeadDistance = 5f;
float depthLeadTime = depthLeadDistance / depthNjs;
float depthJumpStartTime = depthNoteTime - depthHalfDuration;
float depthAdvancedStartTime =
    depthJumpStartTime - depthLeadTime;
float depthSpawnTime =
    depthJumpStartTime - 0.5f;
float depthSpawnZ = depthHalfDistance + 100f;
float depthAdvancedStartZ =
    depthHalfDistance + depthLeadDistance;
float depthJumpVelocityZ = -depthNjs;

AssertNear(
    "advanced depth keeps the original birth distance",
    depthSpawnZ,
    TrajectoryTiming.EvaluateAdvancedDepth(
        songTime: depthSpawnTime,
        spawnTime: depthSpawnTime,
        advancedStartTime: depthAdvancedStartTime,
        originalJumpStartTime: depthJumpStartTime,
        hitTime: depthNoteTime,
        spawnZ: depthSpawnZ,
        advancedStartZ: depthAdvancedStartZ,
        originalZ: depthSpawnZ,
        jumpVelocityZ: depthJumpVelocityZ));
AssertNear(
    "advanced depth reaches the farther jump start continuously",
    depthAdvancedStartZ,
    TrajectoryTiming.EvaluateAdvancedDepth(
        songTime: depthAdvancedStartTime,
        spawnTime: depthSpawnTime,
        advancedStartTime: depthAdvancedStartTime,
        originalJumpStartTime: depthJumpStartTime,
        hitTime: depthNoteTime,
        spawnZ: depthSpawnZ,
        advancedStartZ: depthAdvancedStartZ,
        originalZ: depthAdvancedStartZ,
        jumpVelocityZ: depthJumpVelocityZ));

float depthSampleTime = depthSpawnTime + 0.13f;
float originalDepthAtSample =
    depthSpawnZ - 200f * (depthSampleTime - depthSpawnTime);
float proxyDepthAtSample = TrajectoryTiming.EvaluateAdvancedDepth(
    songTime: depthSampleTime,
    spawnTime: depthSpawnTime,
    advancedStartTime: depthAdvancedStartTime,
    originalJumpStartTime: depthJumpStartTime,
    hitTime: depthNoteTime,
    spawnZ: depthSpawnZ,
    advancedStartZ: depthAdvancedStartZ,
    originalZ: originalDepthAtSample,
    jumpVelocityZ: depthJumpVelocityZ);
AssertTrue(
    "proxy is ahead of the original before its advanced jump",
    proxyDepthAtSample < originalDepthAtSample);

AssertNear(
    "original catches proxy at the original jump start",
    depthHalfDistance,
    TrajectoryTiming.EvaluateAdvancedDepth(
        songTime: depthJumpStartTime,
        spawnTime: depthSpawnTime,
        advancedStartTime: depthAdvancedStartTime,
        originalJumpStartTime: depthJumpStartTime,
        hitTime: depthNoteTime,
        spawnZ: depthSpawnZ,
        advancedStartZ: depthAdvancedStartZ,
        originalZ: depthHalfDistance,
        jumpVelocityZ: depthJumpVelocityZ));
AssertNear(
    "proxy copies original depth after catch-up",
    3.4f,
    TrajectoryTiming.EvaluateAdvancedDepth(
        songTime: depthJumpStartTime + 0.2f,
        spawnTime: depthSpawnTime,
        advancedStartTime: depthAdvancedStartTime,
        originalJumpStartTime: depthJumpStartTime,
        hitTime: depthNoteTime,
        spawnZ: depthSpawnZ,
        advancedStartZ: depthAdvancedStartZ,
        originalZ: 3.4f,
        jumpVelocityZ: depthJumpVelocityZ));

float zeroLeadSampleTime = depthSpawnTime + 0.25f;
float zeroLeadOriginalZ =
    depthSpawnZ - 200f * (zeroLeadSampleTime - depthSpawnTime);
AssertNear(
    "zero lead reproduces original approach depth",
    zeroLeadOriginalZ,
    TrajectoryTiming.EvaluateAdvancedDepth(
        songTime: zeroLeadSampleTime,
        spawnTime: depthSpawnTime,
        advancedStartTime: depthJumpStartTime,
        originalJumpStartTime: depthJumpStartTime,
        hitTime: depthNoteTime,
        spawnZ: depthSpawnZ,
        advancedStartZ: depthHalfDistance,
        originalZ: zeroLeadOriginalZ,
        jumpVelocityZ: depthJumpVelocityZ));

float fallbackNjs = 10f;
float fallbackHalfDuration = 0.6f;
float fallbackJumpStart =
    depthNoteTime - fallbackHalfDuration;
float fallbackSpawn = fallbackJumpStart - 0.5f;
float fallbackLeadTime = 5f / fallbackNjs;
float fallbackAdvancedStart =
    fallbackJumpStart - fallbackLeadTime;
AssertNear(
    "unavailable early lifetime starts closer on the slow path",
    fallbackNjs * fallbackHalfDuration + 5f,
    TrajectoryTiming.EvaluateAdvancedDepth(
        songTime: fallbackSpawn,
        spawnTime: fallbackSpawn,
        advancedStartTime: fallbackAdvancedStart,
        originalJumpStartTime: fallbackJumpStart,
        hitTime: depthNoteTime,
        spawnZ: fallbackNjs * fallbackHalfDuration + 100f,
        advancedStartZ: fallbackNjs * fallbackHalfDuration + 5f,
        originalZ: fallbackNjs * fallbackHalfDuration + 100f,
        jumpVelocityZ: -fallbackNjs));

float maximumLeadDistance = 5f;
var providerCases = new[]
{
    (Njs: 10f, HalfDuration: 0.60f),
    (Njs: 17f, HalfDuration: 0.43f),
    (Njs: 25f, HalfDuration: 0.30f),
    (Njs: 30f, HalfDuration: 0.50f)
};

foreach (var providerCase in providerCases)
{
    float leadDuration = maximumLeadDistance / providerCase.Njs;
    float warpExponent = TrajectoryTiming.CalculateTimeWarpExponent(
        providerCase.HalfDuration,
        leadDuration);
    AssertTrue(
        "time-warp exponent is finite and at least quadratic",
        float.IsFinite(warpExponent) && warpExponent >= 2f);

    foreach (float normalizedTime in
             new[] { 0f, 0.125f, 0.25f, 0.5f, 0.75f, 1f })
    {
        float elapsed = normalizedTime * providerCase.HalfDuration;
        float originalProgress =
            2f * normalizedTime - normalizedTime * normalizedTime;
        AssertNear(
            "zero lead exactly reproduces the original parabola",
            originalProgress,
            TrajectoryTiming.EvaluateTimeWarpedHeight(
                elapsed,
                providerCase.HalfDuration,
                leadTime: 0f,
                startY: 0f,
                endY: 1f));

        float warpedElapsed =
            providerCase.HalfDuration * normalizedTime +
            leadDuration * MathF.Pow(normalizedTime, warpExponent);
        AssertNear(
            "one warped expression preserves the original height parameter",
            originalProgress,
            TrajectoryTiming.EvaluateTimeWarpedHeight(
                warpedElapsed,
                providerCase.HalfDuration,
                leadDuration,
                startY: 0f,
                endY: 1f),
            tolerance: 0.00002f);
    }

    AssertNear(
        "warped ascent starts at the original low point",
        0f,
        TrajectoryTiming.EvaluateTimeWarpedHeight(
            0f,
            providerCase.HalfDuration,
            leadDuration,
            0f,
            1f));
    AssertTrue(
        "note is at least 99.5 percent high after the original duration",
        TrajectoryTiming.EvaluateTimeWarpedHeight(
            providerCase.HalfDuration,
            providerCase.HalfDuration,
            leadDuration,
            0f,
            1f) >= 0.995f - 0.00002f);
    AssertNear(
        "single warped curve reaches exact hit height",
        1f,
        TrajectoryTiming.EvaluateTimeWarpedHeight(
            providerCase.HalfDuration + leadDuration,
            providerCase.HalfDuration,
            leadDuration,
            0f,
            1f));

    float previousHeight = -1f;
    for (int sample = 0; sample <= 200; sample++)
    {
        float elapsed =
            (providerCase.HalfDuration + leadDuration) * sample / 200f;
        float height = TrajectoryTiming.EvaluateTimeWarpedHeight(
            elapsed,
            providerCase.HalfDuration,
            leadDuration,
            0f,
            1f);
        AssertTrue(
            "warped height remains finite and monotonic",
            float.IsFinite(height) && height + 0.0001f >= previousHeight);
        previousHeight = height;
    }

    float derivativeStep = providerCase.HalfDuration / 10000f;
    float startRate =
        (TrajectoryTiming.EvaluateTimeWarpedHeight(
             derivativeStep,
             providerCase.HalfDuration,
             leadDuration,
             0f,
             1f) -
         TrajectoryTiming.EvaluateTimeWarpedHeight(
             0f,
             providerCase.HalfDuration,
             leadDuration,
             0f,
             1f)) /
        derivativeStep;
    AssertNear(
        "maximum growth magnitude is identical to the original",
        2f / providerCase.HalfDuration,
        startRate,
        tolerance: 0.003f);

    float halfRateExtraDuration =
        leadDuration * MathF.Pow(0.5f, warpExponent);
    AssertTrue(
        "fastest-growth half keeps effectively the original duration",
        halfRateExtraDuration <= 0.00001f);

    float noteTime = 10f;
    float visualStart =
        noteTime - providerCase.HalfDuration - leadDuration;
    AssertNear(
        "warped phase starts at zero",
        0f,
        TrajectoryTiming.CalculateTimeWarpedJumpProgress(
            visualStart,
            noteTime,
            providerCase.HalfDuration,
            providerCase.HalfDuration * 2f,
            leadDuration));
    float halfRateSongTime =
        visualStart +
        providerCase.HalfDuration * 0.5f +
        halfRateExtraDuration;
    AssertNear(
        "shared phase follows the same implicit time warp",
        0.25f,
        TrajectoryTiming.CalculateTimeWarpedJumpProgress(
            halfRateSongTime,
            noteTime,
            providerCase.HalfDuration,
            providerCase.HalfDuration * 2f,
            leadDuration),
        tolerance: 0.00002f);
    AssertNear(
        "warped phase is continuous immediately after hit",
        0.5f + 0.0001f / (providerCase.HalfDuration * 2f),
        TrajectoryTiming.CalculateTimeWarpedJumpProgress(
            noteTime + 0.0001f,
            noteTime,
            providerCase.HalfDuration,
            providerCase.HalfDuration * 2f,
            leadDuration),
        tolerance: 0.00001f);
}

var jdFixerCases = new[]
{
    (Njs: 10f, JumpDistance: 10f),
    (Njs: 17f, JumpDistance: 18f),
    (Njs: 25f, JumpDistance: 12f),
    (Njs: 30f, JumpDistance: 30f)
};
float[] configuredLeadDistances = { 0f, 1f, 3f, 5f };
float[] rotationPhaseProgress = { 0f, 0.0625f, 0.125f, 0.25f, 0.375f, 0.5f };

foreach (var jdFixerCase in jdFixerCases)
{
    float providerHalfDuration =
        jdFixerCase.JumpDistance / (2f * jdFixerCase.Njs);
    float providerJumpDuration = providerHalfDuration * 2f;
    float providerJumpStartZ = jdFixerCase.JumpDistance * 0.5f;
    float providerJumpEndZ = -providerJumpStartZ;
    AssertNear(
        "effective NJS is reconstructed from JDFixer-adjusted provider data",
        jdFixerCase.Njs,
        TrajectoryTiming.CalculateEffectiveNjs(
            providerJumpStartZ,
            providerJumpEndZ,
            providerJumpDuration));

    foreach (float configuredLeadDistance in configuredLeadDistances)
    {
        float providerLeadTime = TrajectoryTiming.CalculateLeadTime(
            configuredLeadDistance,
            jdFixerCase.Njs);
        AssertNear(
            "configured lead distance uses JDFixer-adjusted effective NJS",
            configuredLeadDistance / jdFixerCase.Njs,
            providerLeadTime);

        float providerWarpExponent =
            TrajectoryTiming.CalculateTimeWarpExponent(
                providerHalfDuration,
                providerLeadTime);
        float providerNoteTime = 20f;
        float providerVisualStart =
            providerNoteTime -
            providerHalfDuration -
            providerLeadTime;

        foreach (float expectedJumpProgress in rotationPhaseProgress)
        {
            float riseProgress = expectedJumpProgress * 2f;
            float elapsedAtPhase =
                providerHalfDuration * riseProgress +
                providerLeadTime *
                MathF.Pow(riseProgress, providerWarpExponent);
            float phaseSongTime = providerVisualStart + elapsedAtPhase;
            float actualJumpProgress =
                TrajectoryTiming.CalculateTimeWarpedJumpProgress(
                    phaseSongTime,
                    providerNoteTime,
                    providerHalfDuration,
                    providerJumpDuration,
                    providerLeadTime);
            AssertNear(
                "rotation phase remains synchronized for JDFixer and lead settings",
                expectedJumpProgress,
                actualJumpProgress,
                tolerance: 0.00002f);

            float expectedNormalizedHeight =
                4f *
                expectedJumpProgress *
                (1f - expectedJumpProgress);
            AssertNear(
                "height and rotation consume the same advanced phase",
                expectedNormalizedHeight,
                TrajectoryTiming.EvaluateTimeWarpedHeight(
                    elapsedAtPhase,
                    providerHalfDuration,
                    providerLeadTime,
                    0f,
                    1f),
                tolerance: 0.00002f);

            float zeroLeadPhaseTime =
                providerNoteTime -
                providerHalfDuration +
                providerHalfDuration * riseProgress;
            float expectedAdvance =
                providerLeadTime *
                (1f - MathF.Pow(
                    riseProgress,
                    providerWarpExponent));
            AssertNear(
                "lead advances the complete rotation phase by the implicit warp",
                expectedAdvance,
                zeroLeadPhaseTime - phaseSongTime,
                tolerance: 0.00002f);
        }
    }
}

Console.WriteLine("Trajectory timing regressions passed.");
