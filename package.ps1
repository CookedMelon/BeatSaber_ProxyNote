param(
    [string[]] $GameVersions = @('1.40.8'),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $projectRoot 'ProxyNote.csproj'
$dist = Join-Path $projectRoot 'dist'
$versionText = [regex]::Match(
    (Get-Content -LiteralPath $project -Raw),
    '<Version>([^<]+)</Version>').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($versionText))
{
    throw 'Could not read the plugin version from ProxyNote.csproj.'
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Directory]::CreateDirectory($dist) | Out-Null

foreach ($GameVersion in $GameVersions)
{
    & (Join-Path $projectRoot 'build.ps1') `
        -GameVersion $GameVersion `
        -Configuration $Configuration

    $dll = Join-Path $projectRoot `
        "bin\$GameVersion\$Configuration\ProxyNote.dll"
    $archive = Join-Path $dist `
        "ProxyNote-$versionText-bs$GameVersion.zip"
    if (Test-Path -LiteralPath $archive)
    {
        Remove-Item -LiteralPath $archive -Force
    }

    $zip = [IO.Compression.ZipFile]::Open(
        $archive,
        [IO.Compression.ZipArchiveMode]::Create)
    try
    {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $dll,
            'Plugins/ProxyNote.dll',
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    finally
    {
        $zip.Dispose()
    }

    Write-Output "Packaged exact Beat Saber target ${GameVersion}: $archive"
}
