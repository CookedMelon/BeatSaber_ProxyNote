$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$patchPath = Join-Path $projectRoot 'Patches\BurstSliderGameNoteInitPatch.cs'
$controllerPath = Join-Path $projectRoot 'ProxyNoteVisualController.cs'

if (-not (Test-Path -LiteralPath $patchPath))
{
    throw 'Burst slider notes are not patched into the proxy trajectory pipeline.'
}

$patch = Get-Content -LiteralPath $patchPath -Raw -Encoding utf8
$controller = Get-Content -LiteralPath $controllerPath -Raw -Encoding utf8

foreach ($required in @(
    'HarmonyPatch(typeof(BurstSliderGameNoteController), nameof(BurstSliderGameNoteController.Init))',
    'BurstSliderGameNoteController __instance',
    'visual.Initialize(__instance, noteData, in noteSpawnData)'))
{
    if (-not $patch.Contains($required))
    {
        throw "Burst slider patch is missing '$required'."
    }
}

if ($controller -notmatch 'Initialize\(\s*BurstSliderGameNoteController noteController,')
{
    throw 'Proxy controller has no BurstSliderGameNoteController initializer.'
}

# Beat Saber's BurstSliderSpawner assigns each slice an evenly interpolated
# note time. Every element must then use the same lead warp so those offsets
# remain invariant at every corresponding trajectory phase.
$headTime = 10.0
$tailTime = 10.4
$sliceCount = 5
$halfJumpDuration = 0.75
$leadTime = 0.30
$exponent = 12.0

function Get-AdvancedPhaseTime(
    [double] $noteTime,
    [double] $phase)
{
    return $noteTime - $halfJumpDuration - $leadTime +
        $halfJumpDuration * $phase +
        $leadTime * [Math]::Pow($phase, $exponent)
}

function Get-VanillaPhaseTime(
    [double] $noteTime,
    [double] $phase)
{
    return $noteTime - $halfJumpDuration +
        $halfJumpDuration * $phase
}

$tolerance = 0.0000001
foreach ($sliceIndex in 1..($sliceCount - 1))
{
    $slice = $sliceIndex / ($sliceCount - 1)
    $sliceTime = $headTime + ($tailTime - $headTime) * $slice
    $expectedOffset = $sliceTime - $headTime

    foreach ($phase in @(0.0, 0.25, 0.5, 0.75, 0.95, 1.0))
    {
        $headPhaseTime = Get-AdvancedPhaseTime $headTime $phase
        $slicePhaseTime = Get-AdvancedPhaseTime $sliceTime $phase
        $actualOffset = $slicePhaseTime - $headPhaseTime
        if ([Math]::Abs($actualOffset - $expectedOffset) -gt $tolerance)
        {
            throw "Advanced burst slice lost its original phase offset at q=$phase."
        }

        $brokenSliceTime = Get-VanillaPhaseTime $sliceTime $phase
        $brokenExtraLag =
            ($brokenSliceTime - $headPhaseTime) - $expectedOffset
        $expectedExtraLag =
            $leadTime * (1.0 - [Math]::Pow($phase, $exponent))
        if ([Math]::Abs($brokenExtraLag - $expectedExtraLag) -gt $tolerance)
        {
            throw 'The regression model no longer represents an unpatched slice.'
        }
    }
}

Write-Output 'Burst slider trajectory validation passed.'
