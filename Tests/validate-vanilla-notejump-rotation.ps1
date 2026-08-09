param(
    [string[]] $GameVersions = @('1.40.8'),
    [string] $InstancesRoot = 'D:\Programs\BSManager\BSInstances',
    [string] $CecilSourceVersion = '1.40.8'
)

$ErrorActionPreference = 'Stop'

function Get-InstructionText
{
    param([Mono.Cecil.MethodDefinition] $Method)

    return ($Method.Body.Instructions | ForEach-Object {
        $operand = if ($null -eq $_.Operand) { '' } else { " $($_.Operand)" }
        "$($_.OpCode.Name)$operand"
    }) -join "`n"
}

function Assert-Matches
{
    param(
        [string] $Text,
        [string] $Pattern,
        [string] $Message
    )

    if ($Text -notmatch $Pattern)
    {
        throw $Message
    }
}

$cecilGameRoot = Join-Path $InstancesRoot $CecilSourceVersion
$cecilPath = Join-Path $cecilGameRoot 'Libs\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecilPath))
{
    $cecilPath = Join-Path $cecilGameRoot 'IPA\Libs\Mono.Cecil.dll'
}
if (-not (Test-Path -LiteralPath $cecilPath))
{
    throw "Mono.Cecil was not found under '$cecilGameRoot'."
}

Add-Type -Path $cecilPath

$rotationBodies = @{}
foreach ($version in $GameVersions)
{
    $mainPath = Join-Path (
        Join-Path $InstancesRoot $version
    ) 'Beat Saber_Data\Managed\Main.dll'
    if (-not (Test-Path -LiteralPath $mainPath))
    {
        throw "Beat Saber Main.dll was not found for version $version."
    }

    $header = Get-Content -LiteralPath $mainPath -Encoding Byte -TotalCount 2
    if ($header.Count -ne 2 -or $header[0] -ne 0x4D -or $header[1] -ne 0x5A)
    {
        throw "Beat Saber Main.dll for version $version is not a readable managed PE file."
    }

    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($mainPath)
    try
    {
        $noteJump = $assembly.MainModule.Types |
            Where-Object FullName -eq 'NoteJump'
        $manualUpdate = $noteJump.Methods |
            Where-Object Name -eq 'ManualUpdate'
        if ($null -eq $manualUpdate -or -not $manualUpdate.HasBody)
        {
            throw "NoteJump.ManualUpdate was not found for version $version."
        }

        $instructions = Get-InstructionText $manualUpdate
        Assert-Matches $instructions (
            'get_songTime\(\)\n' +
            'dup\n' +
            'ldarg\.0\n' +
            'ldfld System\.Single NoteJump::_noteTime\n' +
            'ldarg\.0\n' +
            'ldfld System\.Single NoteJump::_halfJumpDuration\n' +
            'sub\nsub\nstloc\.0\n' +
            'ldloc\.0\n' +
            'ldarg\.0\n' +
            'ldfld System\.Single NoteJump::_jumpDuration\n' +
            'div\nstloc\.1'
        ) "Version $version no longer derives p from elapsed time / jump duration."
        Assert-Matches $instructions (
            'ldloc\.1\n' +
            'ldc\.r4 0\.125\n' +
            'blt.*\n' +
            '(?:.*\n){0,18}' +
            'ldloc\.1\n' +
            'ldc\.r4 0\.125\n' +
            'sub\n' +
            'ldc\.r4 3\.141593\n' +
            'mul\n' +
            'ldc\.r4 2\n' +
            'mul\n' +
            'call System\.Single UnityEngine\.Mathf::Sin'
        ) "Version $version no longer has the vanilla middle-to-end sin segment."
        Assert-Matches $instructions (
            'ldloc\.1\n' +
            'ldc\.r4 3\.141593\n' +
            'mul\n' +
            'ldc\.r4 4\n' +
            'mul\n' +
            'call System\.Single UnityEngine\.Mathf::Sin'
        ) "Version $version no longer has the vanilla start-to-middle sin segment."

        $slerpCount = (
            [regex]::Matches(
                $instructions,
                'UnityEngine\.Quaternion::Slerp')
        ).Count
        if ($slerpCount -ne 2)
        {
            throw "Version $version expected two rotation Slerp calls, found $slerpCount."
        }

        Assert-Matches $instructions (
            'ldfld System\.Boolean NoteJump::_rotateTowardsPlayer' +
            '(?:.|\n)*' +
            'ldloc\.1\n' +
            'ldc\.r4 2\n' +
            'mul\n' +
            'call UnityEngine\.Quaternion UnityEngine\.Quaternion::Lerp'
        ) "Version $version no longer applies the vanilla player-facing Lerp by 2p."

        $rotationStart = $instructions.IndexOf(
            'ldfld System.Boolean NoteJump::_rotateTowardsPlayer')
        $rotationBodies[$version] = $instructions.Substring(
            [Math]::Max(0, $rotationStart - 900),
            [Math]::Min(
                2200,
                $instructions.Length - [Math]::Max(0, $rotationStart - 900)))
    }
    finally
    {
        $assembly.Dispose()
    }
}

if ($GameVersions.Count -gt 1)
{
    $baseline = $rotationBodies[$GameVersions[0]]
    foreach ($version in $GameVersions | Select-Object -Skip 1)
    {
        if ($rotationBodies[$version] -ne $baseline)
        {
            throw "Vanilla NoteJump rotation IL differs between $($GameVersions[0]) and $version."
        }
    }
}

Write-Output (
    'Vanilla NoteJump rotation validation passed for ' +
    ($GameVersions -join ', ') +
    '.')
