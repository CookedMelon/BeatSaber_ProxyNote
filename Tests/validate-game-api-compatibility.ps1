param(
    [Parameter(Mandatory = $true)]
    [string] $GameVersion,
    [string] $InstancesRoot = 'D:\Programs\BSManager\BSInstances',
    [string] $CecilSourceVersion = '1.40.8'
)

$ErrorActionPreference = 'Stop'

function Assert-ManagedAssembly
{
    param([string] $Path, [string] $Label)

    if (-not (Test-Path -LiteralPath $Path))
    {
        throw "$Label was not found at '$Path'."
    }

    $header = Get-Content -LiteralPath $Path -Encoding Byte -TotalCount 2
    if ($header.Count -ne 2 -or $header[0] -ne 0x4D -or $header[1] -ne 0x5A)
    {
        throw "$Label is not a valid PE file. Repair or launch the BSManager instance first: '$Path'."
    }
}

function Get-RequiredType
{
    param([Mono.Cecil.ModuleDefinition] $Module, [string] $Name)

    $type = $Module.Types | Where-Object FullName -eq $Name
    if ($null -eq $type)
    {
        throw "Required game type '$Name' is missing in Beat Saber $GameVersion."
    }
    return $type
}

function Assert-Field
{
    param([Mono.Cecil.TypeDefinition] $Type, [string] $Name)

    if ($null -eq ($Type.Fields | Where-Object Name -eq $Name))
    {
        throw "Required field '$($Type.FullName).$Name' is missing in Beat Saber $GameVersion."
    }
}

function Assert-Property
{
    param([Mono.Cecil.TypeDefinition] $Type, [string] $Name)

    if ($null -eq ($Type.Properties | Where-Object Name -eq $Name))
    {
        throw "Required property '$($Type.FullName).$Name' is missing in Beat Saber $GameVersion."
    }
}

function Assert-Method
{
    param([Mono.Cecil.TypeDefinition] $Type, [string] $Name)

    if ($null -eq ($Type.Methods | Where-Object Name -eq $Name))
    {
        throw "Required method '$($Type.FullName).$Name' is missing in Beat Saber $GameVersion."
    }
}

$gameRoot = Join-Path $InstancesRoot $GameVersion
$managedRoot = Join-Path $gameRoot 'Beat Saber_Data\Managed'
$mainPath = Join-Path $managedRoot 'Main.dll'
Assert-ManagedAssembly $mainPath "Beat Saber $GameVersion Main.dll"

$cecilRoot = Join-Path $InstancesRoot $CecilSourceVersion
$cecilPath = Join-Path $cecilRoot 'Libs\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecilPath))
{
    $cecilPath = Join-Path $cecilRoot 'IPA\Libs\Mono.Cecil.dll'
}
if (-not (Test-Path -LiteralPath $cecilPath))
{
    throw "Mono.Cecil was not found under '$cecilRoot'."
}

Add-Type -Path $cecilPath
try
{
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($mainPath)
}
catch
{
    throw "Beat Saber $GameVersion Main.dll has no readable managed metadata. Repair or launch the BSManager instance first. $($_.Exception.Message)"
}

try
{
    $module = $assembly.MainModule
    $gameNote = Get-RequiredType $module 'GameNoteController'
    $burstNote = Get-RequiredType $module 'BurstSliderGameNoteController'
    $bombNote = Get-RequiredType $module 'BombNoteController'
    $noteController = Get-RequiredType $module 'NoteController'
    $noteMovement = Get-RequiredType $module 'NoteMovement'
    $noteJump = Get-RequiredType $module 'NoteJump'
    $movementProvider = Get-RequiredType $module 'IVariableMovementDataProvider'

    foreach ($type in @($gameNote, $burstNote, $bombNote))
    {
        Assert-Method $type 'Init'
    }
    foreach ($type in @($gameNote, $burstNote))
    {
        Assert-Property $type 'noteMovement'
    }
    foreach ($property in @(
        'noteTransform',
        'noteData',
        'noteTime',
        'worldRotation',
        'inverseWorldRotation'))
    {
        Assert-Property $noteController $property
    }
    foreach ($property in @('localPosition', 'position'))
    {
        Assert-Property $noteMovement $property
    }
    foreach ($field in @('_jump', '_zOffset'))
    {
        Assert-Field $noteMovement $field
    }
    foreach ($field in @(
        '_variableMovementDataProvider',
        '_playerTransforms',
        '_audioTimeSyncController',
        '_startRotation',
        '_middleRotation',
        '_endRotation',
        '_yAvoidance',
        '_rotateTowardsPlayer',
        '_playerSpaceConvertor'))
    {
        Assert-Field $noteJump $field
    }
    foreach ($property in @(
        'jumpDuration',
        'halfJumpDuration',
        'moveDuration',
        'spawnAheadTime',
        'waitingDuration',
        'moveStartPosition',
        'moveEndPosition',
        'jumpEndPosition'))
    {
        Assert-Property $movementProvider $property
    }
}
finally
{
    $assembly.Dispose()
}

foreach ($dependency in @(
    (Join-Path $managedRoot 'IPA.Loader.dll'),
    (Join-Path $gameRoot 'Libs\0Harmony.dll'),
    (Join-Path $gameRoot 'Plugins\BSML.dll'),
    (Join-Path $gameRoot 'Plugins\CameraUtils.dll')))
{
    if (-not (Test-Path -LiteralPath $dependency))
    {
        throw "Required build/runtime dependency is missing for Beat Saber ${GameVersion}: '$dependency'."
    }
}

Write-Output "Beat Saber $GameVersion API compatibility validation passed."
