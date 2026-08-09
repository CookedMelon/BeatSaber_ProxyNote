$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $projectRoot 'ProxyNote.csproj'
$projectFiles = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.csproj' -File)

if (-not (Test-Path -LiteralPath $projectPath) -or
    $projectFiles.Count -ne 1 -or
    $projectFiles[0].Name -ne 'ProxyNote.csproj')
{
    throw 'ProxyNote.csproj must be the only project file in the plugin root.'
}

[xml]$project = Get-Content -LiteralPath $projectPath -Raw -Encoding utf8
$propertyGroup = $project.Project.PropertyGroup |
    Where-Object { $_.AssemblyName } |
    Select-Object -First 1
if ($propertyGroup.AssemblyName -ne 'ProxyNote' -or
    $propertyGroup.RootNamespace -ne 'ProxyNote')
{
    throw 'AssemblyName and RootNamespace must both be ProxyNote.'
}

foreach ($manifestName in 'manifest.1.40.8.json','manifest.1.44.2.json')
{
    $manifest = Get-Content -LiteralPath (
        Join-Path $projectRoot $manifestName) -Raw -Encoding utf8 |
        ConvertFrom-Json
    if ($manifest.id -ne 'ProxyNote' -or $manifest.name -ne 'ProxyNote')
    {
        throw "$manifestName must use id and name ProxyNote."
    }
}

$sourceFiles = Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|Tests)\\' }
foreach ($sourceFile in $sourceFiles)
{
    $source = Get-Content -LiteralPath $sourceFile.FullName -Raw -Encoding utf8
    if ($source -notmatch '\bnamespace\s+ProxyNote(?:\.Patches)?\b')
    {
        throw "Unexpected namespace in $($sourceFile.FullName)."
    }
}

$pluginSource = Get-Content -LiteralPath (
    Join-Path $projectRoot 'Plugin.cs') -Raw -Encoding utf8
if ($pluginSource -notmatch 'ProxyNote\.Views\.settings\.bsml')
{
    throw 'The embedded BSML resource name must use the ProxyNote assembly.'
}

$buildScript = Get-Content -LiteralPath (
    Join-Path $projectRoot 'build.ps1') -Raw -Encoding utf8
if ($buildScript -notmatch 'ProxyNote\.csproj' -or
    $buildScript -notmatch 'ProxyNote\.dll')
{
    throw 'build.ps1 must build and report ProxyNote.dll.'
}

Write-Output 'Product identity validation passed.'
