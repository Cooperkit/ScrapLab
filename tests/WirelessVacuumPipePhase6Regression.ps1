param(
    [string]$MainExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.exe'),
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe'),
    [string]$Node
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

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    Assert-True ($Text.Contains($Needle)) $Message
}

foreach ($path in @($MainExe, $PatchHelperExe, $Node)) {
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Missing validation input: $path"
}

$main = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($MainExe))
$helper = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($PatchHelperExe))
$uiType = $main.GetType('RaidRescue.UiHtml', $true)
$html = [string]$uiType.GetField(
    'Content', [Reflection.BindingFlags]'Public,Static').GetValue($null)

$bridge = $main.GetType('RaidRescue.BrowserBridge', $true)
Assert-True ($null -ne $bridge.GetMethod('GetWirelessVacuumPipeModStatus')) `
    'The read-only Wireless Vacuum Pipe bridge method is missing.'
Assert-True ($null -ne $bridge.GetMethod('SetWirelessVacuumPipeMod')) `
    'The Wireless Vacuum Pipe toggle bridge method is missing.'

$protocol = $helper.GetType('RaidRescue.PatchHelperProtocol', $true)
$wirelessField = $protocol.GetField(
    'WirelessVacuumPipe', [Reflection.BindingFlags]'Static,NonPublic')
Assert-True ($null -ne $wirelessField) 'The helper protocol action constant is missing.'
Assert-True (([string]$wirelessField.GetRawConstantValue()) -eq 'wireless-vacuum-pipe') `
    'The helper protocol action name changed unexpectedly.'
$knownAction = $protocol.GetMethod(
    'IsKnownAction', [Reflection.BindingFlags]'Static,NonPublic')
Assert-True ([bool]$knownAction.Invoke($null, @('wireless-vacuum-pipe'))) `
    'The helper protocol rejects the Wireless Vacuum Pipe action.'

foreach ($required in @(
    '.secret-category-nav{position:relative;display:flex',
    'flex:1 1 auto;flex-direction:column',
    '.secret-category.active:after{background:#fff1a2',
    '.secret-category-nav::-webkit-scrollbar',
    'PATCH SAFETY ACTIVE',
    'VERIFIED BACKUPS &middot; GUARDED REMOVAL',
    'data-category="logistics"',
    'id="wirelessVacuumPipeRow"',
    'LOGISTICS &middot; PIPE AUTOMATION &middot; SAVE-SENSITIVE',
    'id="wirelessVacuumPipeSwitch"',
    'GetWirelessVacuumPipeModStatus',
    'SetWirelessVacuumPipeMod',
    'function toggleWirelessVacuumPipeMod()',
    'function setWirelessVacuumPipeMod(enabled)',
    'UNSUPPORTED ICON ATLAS',
    'UNSUPPORTED PIPE CODE',
    '11 AVAILABLE',
    'id="wirelessVacuumPipeDangerModal"',
    'I REMOVED EVERY WIRELESS VACUUM PIPE - DISABLE',
    'Remove every pipe from placed worlds, including every underground world.',
    'player inventory, hotbar, container, Lift, and saved creation',
    'SUPER SECRET MODS &mdash; WIRELESS VACUUM PIPE',
    'PAINT IS THE CHANNEL',
    'LINK MODE',
    'SEND / RECEIVE',
    'CROSS-WORLD ROUTING',
    'OPTIONAL LOGIC INPUT',
    'BACKPRESSURE SAFETY',
    '64-cell safety limit'
)) {
    Assert-Contains $html $required "The embedded Phase 6 UI is missing: $required"
}

$toggleStart = $html.IndexOf('function toggleSecretModsEnabled()')
$wirelessGate = $html.IndexOf(
    "secretWirelessVacuumPipeInstalled){openWirelessVacuumPipeDangerConfirm('masterOff')",
    $toggleStart)
$detectorGate = $html.IndexOf(
    "secretRaidDetectorInstalled){openRaidDetectorDangerConfirm('masterOff')",
    $toggleStart)
Assert-True ($toggleStart -ge 0 -and $wirelessGate -gt $toggleStart -and
    $detectorGate -gt $wirelessGate) `
    'The master switch does not gate Wireless Vacuum Pipe before other mods.'

$removeStart = $html.IndexOf('function disableAllSecretModsConfirmed()')
$wirelessRemove = $html.IndexOf('setWirelessVacuumPipeMod(false)', $removeStart)
$detectorRemove = $html.IndexOf('setRaidDetectorMod(false)', $removeStart)
Assert-True ($removeStart -ge 0 -and $wirelessRemove -gt $removeStart -and
    $detectorRemove -gt $wirelessRemove) `
    'Master removal does not remove Wireless Vacuum Pipe first.'
Assert-Contains $html "if(secretWirelessVacuumPipeInstalled&&!setWirelessVacuumPipeMod(false))return false;" `
    'Master removal does not stop immediately after a Wireless Vacuum Pipe failure.'
Assert-True (-not $html.Contains("||id==='betterPlasmaDrillsReason'")) `
    'Benign Better Plasma Drills compatibility text is still forced visible.'
Assert-Contains $html "if(secretWirelessVacuumPipeCompatibility==='PARTIAL PATCH - REPAIR REQUIRED')wirelessLabel='PARTIAL PATCH \u2014 REPAIR REQUIRED';" `
    'A partial Wireless Vacuum Pipe state can still be mislabeled as an atlas failure.'

$scriptMatch = [regex]::Match($html, '(?is)<script[^>]*>(.*?)</script>')
Assert-True $scriptMatch.Success 'The embedded application script was not found.'
Assert-True (([regex]::Matches($html, '(?is)<script[^>]*>')).Count -eq 1) `
    'Expected exactly one embedded application script.'
$tempScript = Join-Path ([IO.Path]::GetTempPath()) `
    ('scraplab-wireless-phase6-' + [Guid]::NewGuid().ToString('N') + '.js')
try {
    [IO.File]::WriteAllText(
        $tempScript, $scriptMatch.Groups[1].Value,
        [Text.UTF8Encoding]::new($false))
    & $Node --check $tempScript
    Assert-True ($LASTEXITCODE -eq 0) 'Embedded JavaScript syntax validation failed.'
}
finally {
    Remove-Item -LiteralPath $tempScript -Force -ErrorAction SilentlyContinue
}

$readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'))
$changelog = [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
$phaseDoc = [IO.File]::ReadAllText(
    (Join-Path $root 'docs\WIRELESS-VACUUM-PIPE-PHASE-6.md'))
foreach ($required in @(
    '**Wireless Vacuum Pipe**',
    'a34d9af0-4ba0-431d-b647-2d5435ecf138',
    '**Link** mode',
    '**Send** networks',
    '**Receive**',
    '64-cell cap',
    'Wireless Vacuum Pipe save warning'
)) {
    Assert-Contains $readme $required "README is missing Phase 6 guidance: $required"
}
Assert-Contains $changelog 'Logistics Patch Bay category' `
    'The Unreleased changelog omits Patch Bay integration.'
Assert-Contains $changelog 'master-switch removal confirmations' `
    'The Unreleased changelog omits master removal safety.'
Assert-Contains $phaseDoc 'Phase 7 is now unlocked.' `
    'The Phase 6 handoff does not unlock Phase 7.'

$statusJson = & $PatchHelperExe --status wireless-vacuum-pipe
Assert-True ($LASTEXITCODE -eq 0) 'The read-only Wireless Vacuum Pipe helper status failed.'
$status = $statusJson | ConvertFrom-Json
Assert-True ($null -ne $status.Success) 'The helper did not return a typed status result.'
Assert-True ($status.Success) "Live helper status failed: $($status.Error)"

Write-Host (
    'Wireless Vacuum Pipe Phase 6 regression passed: protocol, bridge, Patch Bay, ' +
    'save-sensitive master ordering, Field Manual, public docs, JavaScript, and live status.')
