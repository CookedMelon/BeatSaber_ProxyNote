$ErrorActionPreference = 'Stop'

$settingsPath = Join-Path $PSScriptRoot '..\Views\settings.bsml'
$viewModelPath = Join-Path $PSScriptRoot '..\SettingsViewModel.cs'
$configPath = Join-Path $PSScriptRoot '..\PluginConfig.cs'
[xml]$settings = Get-Content $settingsPath -Raw -Encoding utf8
$viewModel = Get-Content $viewModelPath -Raw -Encoding utf8
$config = Get-Content $configPath -Raw -Encoding utf8

$settingsControls = $settings.SelectNodes('//checkbox-setting | //slider-setting')
foreach ($control in $settingsControls)
{
    if ($control.'bind-value' -ne 'true')
    {
        throw "Setting '$($control.value)' must use bind-value=true so Reset can refresh the control."
    }

    if ([string]::IsNullOrWhiteSpace($control.'on-change'))
    {
        throw "Setting '$($control.value)' must declare an on-change action."
    }

    $valuePattern = '\[UIValue\("' + [regex]::Escape($control.value) + '"\)\]'
    if ($viewModel -notmatch $valuePattern)
    {
        throw "Setting '$($control.value)' has no matching UIValue."
    }

    $writableValuePattern =
        '(?s)\[UIValue\("' +
        [regex]::Escape($control.value) +
        '"\)\]\s*private\s+\w+\s+\w+\s*\{.*?\bset\s*\{'
    if ($viewModel -notmatch $writableValuePattern)
    {
        throw "Setting '$($control.value)' must expose a private setter for BSML."
    }
}

$resetButton = $settings.SelectSingleNode("//*[@on-click='reset-settings']")
if ($null -eq $resetButton -or $resetButton.text -ne '~reset-label')
{
    throw 'The header must contain a localized Reset button.'
}

$jumpLeadControl = $settings.SelectSingleNode(
    "//*[@value='jump-lead-distance']")
if ($null -eq $jumpLeadControl -or $jumpLeadControl.max -ne '5')
{
    throw 'Jump lead distance must be limited to 0..5m.'
}

$settingValues = @($settingsControls | ForEach-Object { $_.value })
$debrisIndex = [array]::IndexOf($settingValues, 'suppress-debris')
$debugIndex = [array]::IndexOf($settingValues, 'debug-mode')
if ($debrisIndex -lt 0 -or $debugIndex -lt 0 -or $debrisIndex -gt $debugIndex)
{
    throw 'Hide vanilla debris must appear before Debug mode.'
}

$preserveSwapsChinese = [string]::Concat(
    [char]0x4FDD,
    [char]0x7559,
    [char]0x65B9,
    [char]0x5757,
    [char]0x4EA4,
    [char]0x6362)
if (-not $viewModel.Contains('"Preserve note swaps"') -or
    -not $viewModel.Contains('"' + $preserveSwapsChinese + '"'))
{
    throw 'Position swap labels must use the requested English and Chinese text.'
}

if ($config -notmatch 'NoteRotationCoefficient\s*\{\s*get;\s*set;\s*\}\s*=\s*0\.2f;' -or
    $config -notmatch 'EnableNotePositionSwaps\s*\{\s*get;\s*set;\s*\}\s*=\s*false;')
{
    throw 'Config defaults must use rotation coefficient 0.2 and preserve swaps false.'
}

if ($config -notmatch 'if\s*\(ConfigVersion\s*<\s*5\)')
{
    throw 'Config migration v5 is required.'
}

$actionElements = $settings.SelectNodes('//*[@on-change or @on-click]')
foreach ($element in $actionElements)
{
    $action = if ($element.'on-change')
    {
        $element.'on-change'
    }
    else
    {
        $element.'on-click'
    }

    $actionPattern = '\[UIAction\("' + [regex]::Escape($action) + '"\)\]'
    if ($viewModel -notmatch $actionPattern)
    {
        throw "BSML action '$action' has no matching UIAction."
    }

    $methodPattern =
        '(?s)\[UIAction\("' +
        [regex]::Escape($action) +
        '"\)\]\s*private void \w+\([^)]*\)\s*\{(?<body>.*?)\r?\n\s*\}'
    $methodMatch = [regex]::Match($viewModel, $methodPattern)
    if (-not $methodMatch.Success -or
        $methodMatch.Groups['body'].Value -notmatch '\bSave\(\);')
    {
        throw "UIAction '$action' must persist its config change through Save()."
    }
}

if ($viewModel -notmatch '(?s)private static void Save\(\)\s*\{.*?PluginConfig\.Instance\.Changed\(\);.*?\}')
{
    throw 'SettingsViewModel.Save() must call PluginConfig.Instance.Changed().'
}

Write-Output 'Settings binding validation passed.'
