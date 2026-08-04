param(
    [string]$MainExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.exe'),
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe'),
    [string]$UpdaterExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.Updater.exe'),
    [string]$Package,
    [string]$LogRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic\Logs',
    [string]$Node,
    [switch]$RunDefender
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([String]::IsNullOrWhiteSpace($Node)) {
    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    $Node = if ($nodeCommand) { $nodeCommand.Source } else {
        Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    }
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}

if ([String]::IsNullOrWhiteSpace($Package)) {
    $Package = Get-ChildItem -LiteralPath (Join-Path $root 'release') `
        -Filter 'ScrapLab-*.zip' -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

foreach ($path in @($MainExe, $PatchHelperExe, $UpdaterExe, $Package, $Node)) {
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) `
        "A release-validation input is missing: $path"
}

$binaryPaths = @($MainExe, $PatchHelperExe, $UpdaterExe)
foreach ($path in $binaryPaths + @($Package)) {
    $item = Get-Item -LiteralPath $path
    Assert-True ($item.Length -lt 8MB) `
        "$($item.Name) exceeds the eight-megabyte limit."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
    $expectedEntries = @(
        'ScrapLab.exe',
        'ScrapLab.PatchHelper.exe',
        'ScrapLab.Updater.exe'
    )
    Assert-True ($entryNames.Count -eq $expectedEntries.Count) `
        'The portable package contains an unexpected dependency or file.'
    foreach ($name in $expectedEntries) {
        Assert-True ($entryNames -contains $name) `
            "The portable package is missing $name."
    }
}
finally {
    $archive.Dispose()
}

$allowedReferences = @(
    'mscorlib', 'System', 'System.Core', 'System.Drawing',
    'System.Web.Extensions', 'System.Windows.Forms', 'System.Xml',
    'PresentationCore'
)
foreach ($path in $binaryPaths) {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom(
        [IO.Path]::GetFullPath($path))
    Assert-True ($assembly.ImageRuntimeVersion -eq 'v4.0.30319') `
        "$([IO.Path]::GetFileName($path)) targets an unexpected runtime."
    foreach ($reference in $assembly.GetReferencedAssemblies()) {
        Assert-True ($allowedReferences -contains $reference.Name) `
            "$([IO.Path]::GetFileName($path)) has an external dependency: $($reference.Name)."
    }
}

$mainAssembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($MainExe))
$uiType = $mainAssembly.GetType('RaidRescue.UiHtml', $true)
$html = [string]$uiType.GetField(
    'Content', [Reflection.BindingFlags]'Public,Static').GetValue($null)
$scripts = [regex]::Matches($html, '(?is)<script[^>]*>(.*?)</script>')
Assert-True ($scripts.Count -eq 1) 'Expected one embedded UI script.'
$tempScript = Join-Path ([IO.Path]::GetTempPath()) `
    ('scraplab-phase7-' + [Guid]::NewGuid().ToString('N') + '.js')
try {
    [IO.File]::WriteAllText(
        $tempScript, $scripts[0].Groups[1].Value,
        [Text.UTF8Encoding]::new($false))
    & $Node --check $tempScript
    Assert-True ($LASTEXITCODE -eq 0) 'Embedded JavaScript syntax is invalid.'
}
finally {
    Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue
}

$helperAssembly = [Reflection.Assembly]::LoadFrom(
    [IO.Path]::GetFullPath($PatchHelperExe))
$resources = @($helperAssembly.GetManifestResourceNames())
foreach ($resource in @(
    'RaidRescue.Parts.WirelessVacuumPipe.WirelessPipeManager.lua',
    'RaidRescue.Parts.WirelessVacuumPipe.ScrapLabPipeGraph.lua',
    'RaidRescue.Parts.WirelessVacuumPipe.WirelessPipeTransfer.lua',
    'RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.lua',
    'RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.shapeset',
    'RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.layout',
    'RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipeIcon.png'
)) {
    Assert-True ($resources -contains $resource) `
        "The patch helper is missing embedded resource $resource."
}

Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $root `
    'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipeIcon.png'
$bitmap = [Drawing.Bitmap]::new($iconPath)
try {
    Assert-True ($bitmap.Width -eq 96 -and $bitmap.Height -eq 96) `
        'The Wireless Vacuum Pipe icon is not 96 by 96 pixels.'
    $transparent = 0
    $visible = 0
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            if ($bitmap.GetPixel($x, $y).A -eq 0) { $transparent++ }
            else { $visible++ }
        }
    }
    Assert-True ($transparent -gt 0 -and $visible -gt 0) `
        'The Wireless Vacuum Pipe icon lost either its transparency or artwork.'
}
finally {
    $bitmap.Dispose()
}

$logChecks = @(
    @('game-20260804-021017.log', 'recorded gates: 10 passed, 0 failed'),
    @('game-20260804-040620.log', 'recorded=7 passed, 0 failed'),
    @('game-20260804-041446.log', 'recorded=7 passed, 0 failed'),
    @('game-20260804-154523.log', 'summary=11 passed, 0 failed, 0 skipped'),
    @('game-20260804-163835.log', 'summary=10 passed, 0 failed, 0 skipped')
)
foreach ($check in $logChecks) {
    $path = Join-Path $LogRoot $check[0]
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) `
        "In-game evidence log is missing: $($check[0])"
    $content = [IO.File]::ReadAllText($path)
    Assert-True $content.Contains($check[1]) `
        "In-game evidence summary is missing from $($check[0])."
    $customErrors = @($content -split "`r?`n" | Where-Object {
        $_ -cmatch 'ERROR:|Traceback' -and
        $_ -match 'Scripts[/\\]ScrapLab|WirelessVacuumPipe|WirelessPipeManager|ScrapLabPipeGraph|WirelessPipeTransfer'
    })
    Assert-True ($customErrors.Count -eq 0) `
        "A ScrapLab wireless runtime error appears in $($check[0])."
}

$statusJson = & $PatchHelperExe --status wireless-vacuum-pipe
Assert-True ($LASTEXITCODE -eq 0) 'The production helper status action failed.'
$status = $statusJson | ConvertFrom-Json
Assert-True $status.Success `
    "The production status probe failed: $($status.Error)"
$compatibilityState = [string]$(
    if ($status.CompatibilityState) { $status.CompatibilityState }
    else { $status.State })
$safeInstalled = $status.Installed -and (
    (-not $status.NeedsUpdate) -or (
        $status.NeedsUpdate -and $status.CanApply -and
        $compatibilityState -eq 'PATCH DEFINITION UPDATE'))
$safeClean = (-not $status.Installed) -and $status.CanApply -and
    @('KNOWN CLEAN', 'COMPATIBLE GAME UPDATE',
      'REINSTALL REQUIRED - SAVE PART AT RISK') -contains
        $compatibilityState
Assert-True ($safeInstalled -or $safeClean) `
    "The production files are neither safely installed nor safely clean: $($status.CompatibilityReason)"

if ($RunDefender) {
    $platform = Get-ChildItem -LiteralPath `
        "$env:ProgramData\Microsoft\Windows Defender\Platform" `
        -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    $scanner = if ($platform) {
        Join-Path $platform.FullName 'MpCmdRun.exe'
    }
    else {
        "$env:ProgramFiles\Windows Defender\MpCmdRun.exe"
    }
    Assert-True (Test-Path -LiteralPath $scanner -PathType Leaf) `
        'Microsoft Defender command-line scanner was not found.'
    foreach ($path in @((Split-Path -Parent $MainExe), $Package)) {
        & $scanner -Scan -ScanType 3 -File ([IO.Path]::GetFullPath($path)) `
            -DisableRemediation
        Assert-True ($LASTEXITCODE -eq 0) `
            "Microsoft Defender reported a problem while scanning $path."
    }
}

Write-Host (
    'Wireless Vacuum Pipe Phase 7 release regression passed: portable bundle, ' +
    'dependency boundary, UI JavaScript, embedded assets, transparent icon, ' +
    'in-game evidence, live game state' +
    $(if ($RunDefender) { ', and Microsoft Defender.' } else { '.' }))
