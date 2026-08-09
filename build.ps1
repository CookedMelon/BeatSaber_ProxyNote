param(
    [string]$GameVersion = "1.40.8",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Install
)

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$MinimumSupportedGameVersion = [Version]"1.40.8"
try {
    $parsedGameVersion = [Version]$GameVersion
}
catch {
    throw "GameVersion '$GameVersion' is not a valid version."
}
if ($parsedGameVersion -lt $MinimumSupportedGameVersion) {
    throw "GameVersion '$GameVersion' is below the minimum supported version $MinimumSupportedGameVersion."
}

$gameDir = "D:\Programs\BSManager\BSInstances\$GameVersion"
$manifest = Join-Path $projectDir "manifest.$GameVersion.json"
if (-not (Test-Path -LiteralPath $manifest)) {
    throw "Missing exact-version manifest: $manifest"
}

$compatibilityValidator = Join-Path $projectDir `
    "Tests\validate-game-api-compatibility.ps1"
& $compatibilityValidator -GameVersion $GameVersion

$mainAssembly = Join-Path $gameDir "Beat Saber_Data\Managed\Main.dll"
$ipaAssembly = Join-Path $gameDir "Beat Saber_Data\Managed\IPA.Loader.dll"

if (-not (Test-Path -LiteralPath $mainAssembly)) {
    throw "Missing game assembly: $mainAssembly"
}

$stream = [System.IO.File]::OpenRead($mainAssembly)
try {
    $first = $stream.ReadByte()
    $second = $stream.ReadByte()
}
finally {
    $stream.Dispose()
}

if ($first -ne 0x4D -or $second -ne 0x5A) {
    throw "Main.dll for Beat Saber $GameVersion is an empty or invalid placeholder. Repair or launch this BSManager instance before building."
}

if (-not (Test-Path -LiteralPath $ipaAssembly)) {
    throw "BSIPA is not installed for Beat Saber $GameVersion. Install core mods for this instance first."
}

$installValue = if ($Install) { "true" } else { "false" }
dotnet build (Join-Path $projectDir "ProxyNote.csproj") `
    --configuration $Configuration `
    "-p:BeatSaberVersion=$GameVersion" `
    "-p:ManifestFile=$manifest" `
    "-p:InstallToGame=$installValue"

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$output = Join-Path $projectDir "bin\$GameVersion\$Configuration\ProxyNote.dll"
Write-Host "Built: $output"
if ($Install) {
    Write-Host "Installed to: $gameDir\Plugins"
}
