$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$build = Get-Content -LiteralPath (Join-Path $projectRoot 'build.ps1') -Raw
$project = Get-Content -LiteralPath (Join-Path $projectRoot 'ProxyNote.csproj') -Raw

if ($build.Contains('[ValidateSet("1.40.8", "1.44.2")]'))
{
    throw 'build.ps1 still hard-codes two game versions.'
}

foreach ($required in @(
    'MinimumSupportedGameVersion',
    'validate-game-api-compatibility.ps1',
    'manifest.$GameVersion.json'))
{
    if (-not $build.Contains($required))
    {
        throw "build.ps1 is missing multi-version behavior '$required'."
    }
}

foreach ($required in @(
    '$(BeatSaberInstancesRoot)\$(BeatSaberVersion)',
    'manifest.$(BeatSaberVersion).json'))
{
    if (-not $project.Contains($required))
    {
        throw "ProxyNote.csproj is missing generic target '$required'."
    }
}

Write-Output 'Build matrix validation passed.'
