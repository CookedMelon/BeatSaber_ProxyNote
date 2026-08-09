$ErrorActionPreference = 'Stop'

$controllerPath = Join-Path $PSScriptRoot '..\ProxyNoteVisualController.cs'
$controller = Get-Content $controllerPath -Raw -Encoding utf8

$updateStart = $controller.IndexOf('private void UpdateVisualPoses()')
$finalizeStart = $controller.IndexOf(
    'private void FinalizeVisualPose(',
    $updateStart)
if ($updateStart -lt 0 -or $finalizeStart -lt 0)
{
    throw 'Could not isolate ProxyNoteVisualController.UpdateVisualPoses().'
}

$updateMethod = $controller.Substring(
    $updateStart,
    $finalizeStart - $updateStart)

foreach ($setting in @(
    'PluginConfig.Instance.JumpLeadDistance',
    'PluginConfig.Instance.NoteRotationCoefficient',
    'PluginConfig.Instance.EnableNotePositionSwaps'))
{
    if ($updateMethod.Contains($setting))
    {
        throw "Trajectory setting '$setting' must be captured at note initialization."
    }
}

if ($controller.Contains('ApplyPositionSwapModifier('))
{
    throw 'Controller must not calculate a swap path and overwrite X afterward.'
}

foreach ($requiredCall in @(
    'TrajectoryTiming.ShouldWaitForFloorMovement(',
    'TrajectoryTiming.CalculateFloorMovementStartTime(',
    'TrajectoryTiming.EvaluatePositionSwap(',
    'TrajectoryTiming.EvaluateSwapAvoidance(',
    'TrajectoryTiming.EvaluateTimeWarpedHeight(',
    'TrajectoryTiming.EvaluateAdvancedDepth(',
    'TrajectoryTiming.CalculateTimeWarpedJumpProgress(',
    'TrajectoryTiming.CalculateVanillaStartToMiddleRotationProgress(',
    'TrajectoryTiming.CalculateVanillaMiddleToEndRotationProgress('))
{
    if (-not $controller.Contains($requiredCall))
    {
        throw "Controller is missing unified trajectory call '$requiredCall'."
    }
}

if ($controller.Contains('_proxySpawnSongTime'))
{
    throw 'Proxy depth must use the scheduled floor start, not component creation time.'
}

if ($controller.Contains('return new Vector3(x, y, currentZ);'))
{
    throw 'Proxy depth must not always copy the original currentZ.'
}

if ($controller -notmatch
    'Quaternion\.Slerp\(\s*_endRotation,\s*_lastVanillaRotation,\s*coefficient\)')
{
    throw 'Rotation coefficient must scale the exact vanilla pose deviation.'
}

foreach ($forbiddenCall in @(
    'TrajectoryTiming.CalculateSmoothRotationProgress(',
    'TrajectoryTiming.CalculateVanillaRotationBlend('))
{
    if ($controller.Contains($forbiddenCall))
    {
        throw "Controller must not replace vanilla rotation timing with '$forbiddenCall'."
    }
}

if ($controller.Contains(
    '_playerSpaceConvertor.worldToPlayerSpaceRotation *' +
    [Environment]::NewLine +
    '                        proxyTransform.up'))
{
    throw 'Vanilla look-at reconstruction must not feed back the coefficient-scaled proxy up vector.'
}

if ($controller.Contains('TrajectoryTiming.EvaluateLeadAwareHeight(') -or
    $controller.Contains('TrajectoryTiming.CalculateVisualJumpProgress(') -or
    $controller.Contains('TrajectoryTiming.EvaluateQuinticHeight(') -or
    $controller.Contains('_movementData.CalculateCurrentNoteJumpGravity('))
{
    throw 'Controller must use only the single implicit time-warp trajectory.'
}

Write-Output 'Trajectory controller validation passed.'
