param(
    [string] $GameRoot =
        'D:\Programs\BSManager\BSInstances\1.40.8'
)

$ErrorActionPreference = 'Stop'

$cecilPath = Join-Path $GameRoot 'Libs\Mono.Cecil.dll'
if (-not (Test-Path -LiteralPath $cecilPath))
{
    $cecilPath = Join-Path $GameRoot 'IPA\Libs\Mono.Cecil.dll'
}
$jdFixerPath = Join-Path $GameRoot 'Plugins\JDFixer.dll'
if (-not (Test-Path -LiteralPath $cecilPath))
{
    throw "Mono.Cecil was not found under '$GameRoot'."
}
if (-not (Test-Path -LiteralPath $jdFixerPath))
{
    throw "JDFixer.dll was not found under '$GameRoot\Plugins'."
}

Add-Type -Path $cecilPath
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($jdFixerPath)
try
{
    $patchType = $assembly.MainModule.Types |
        Where-Object {
            $_.FullName -eq
                'JDFixer.VariableMovementDataProviderPatch'
        }
    if ($null -eq $patchType)
    {
        throw 'JDFixer provider patch type was not found.'
    }

    $harmonyPatch = $patchType.CustomAttributes |
        Where-Object {
            $_.AttributeType.FullName -eq
                'HarmonyLib.HarmonyPatch'
        }
    $targetValues = @(
        $harmonyPatch.ConstructorArguments |
            ForEach-Object { $_.Value.ToString() }
    )
    if ($targetValues -notcontains 'VariableMovementDataProvider' -or
        $targetValues -notcontains 'Init')
    {
        throw 'JDFixer no longer patches VariableMovementDataProvider.Init.'
    }

    $prefix = $patchType.Methods |
        Where-Object Name -eq 'Prefix'
    if ($null -eq $prefix -or -not $prefix.HasBody)
    {
        throw 'JDFixer provider Prefix was not found.'
    }

    $parameterNames = @($prefix.Parameters | ForEach-Object Name)
    foreach ($requiredParameter in @(
        'noteJumpMovementSpeed',
        'bpm',
        'noteJumpValueType',
        'noteJumpValue'))
    {
        if ($parameterNames -notcontains $requiredParameter)
        {
            throw "JDFixer Prefix is missing '$requiredParameter'."
        }
    }

    foreach ($byRefParameter in @('noteJumpValueType', 'noteJumpValue'))
    {
        $parameter = $prefix.Parameters |
            Where-Object Name -eq $byRefParameter
        if (-not $parameter.ParameterType.IsByReference)
        {
            throw "JDFixer parameter '$byRefParameter' is no longer by-reference."
        }
    }

    $instructionText = (
        $prefix.Body.Instructions |
            ForEach-Object { $_.ToString() }
    ) -join "`n"
    if ($instructionText -notmatch
        'SpawnMovementDataUpdateHelper::Get_Modified_DesiredJD' -or
        $instructionText -notmatch 'stind\.r4')
    {
        throw 'JDFixer no longer writes its calculated jump offset.'
    }

    $controllerPath = Join-Path $PSScriptRoot (
        '..\ProxyNoteVisualController.cs'
    )
    $controller = Get-Content $controllerPath -Raw -Encoding utf8
    foreach ($providerRead in @(
        '_movementData.moveStartPosition',
        '_movementData.moveEndPosition',
        '_movementData.jumpEndPosition',
        '_movementData.halfJumpDuration',
        '_movementData.jumpDuration'))
    {
        if (-not $controller.Contains($providerRead))
        {
            throw "Proxy controller does not consume '$providerRead'."
        }
    }
    if ($controller.Contains('JDFixer.'))
    {
        throw 'Proxy controller must not depend on JDFixer implementation types.'
    }

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $jdFixerPath).Hash
    Write-Output (
        "Installed JDFixer compatibility validation passed: " +
        "version $($assembly.Name.Version), SHA256 $hash.")
}
finally
{
    $assembly.Dispose()
}
