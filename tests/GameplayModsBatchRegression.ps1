param(
    [string]$MainExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.exe'),
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe'),
    [string]$Node
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
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

$compiler = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
Assert-True (-not [String]::IsNullOrWhiteSpace($compiler)) 'The .NET Framework C# compiler is missing.'

$main = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($MainExe))
$helper = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($PatchHelperExe))
$html = [string]$main.GetType('RaidRescue.UiHtml', $true).GetField(
    'Content', [Reflection.BindingFlags]'Public,Static').GetValue($null)
$bridge = $main.GetType('RaidRescue.BrowserBridge', $true)
Assert-True ($null -ne $bridge.GetMethod('SetAllGameplayMods')) `
    'The all-gameplay-mods app bridge method is missing.'

$protocol = $helper.GetType('RaidRescue.PatchHelperProtocol', $true)
$action = $protocol.GetField('AllGameplayMods', [Reflection.BindingFlags]'Static,NonPublic')
Assert-True ($null -ne $action) 'The batch helper protocol constant is missing.'
Assert-True (([string]$action.GetRawConstantValue()) -eq 'all-gameplay-mods') `
    'The batch helper action name changed.'
Assert-True ($null -ne $helper.GetType('RaidRescue.GameplayModsBatchCoordinator', $true)) `
    'The elevated gameplay batch coordinator is missing.'
$resultType = $helper.GetType('RaidRescue.GamePatchResult', $true)
Assert-True ($null -ne $resultType.GetProperty('BatchItems')) `
    'Structured batch items are not present on GamePatchResult.'

$coordinator = Get-Content -LiteralPath (Join-Path $root 'source\Patching\GameplayModsBatchCoordinator.cs') -Raw
$helperSource = Get-Content -LiteralPath (Join-Path $root 'source\Companions\PatchHelper\PatchHelperProgram.cs') -Raw
$protocolSource = Get-Content -LiteralPath (Join-Path $root 'source\Shared\PatchHelperProtocol.cs') -Raw
$buildSource = Get-Content -LiteralPath (Join-Path $root 'build.ps1') -Raw
Assert-Contains $helperSource 'GameplayModsBatchCoordinator.SetEnabled' `
    'The helper does not dispatch the batch action.'
Assert-Contains $protocolSource '!String.Equals(action, AllGameplayMods' `
    'The mutating batch action was accidentally exposed as a status action.'
Assert-Contains $buildSource 'Patching\GameplayModsBatchCoordinator.cs' `
    'The helper build does not include the batch coordinator.'
Assert-True (-not $coordinator.Contains('DeveloperCommandsPatchService')) `
    'Developer Commands leaked into the gameplay batch coordinator.'
Assert-True (-not $coordinator.Contains('core_data.cbo')) `
    'The batch coordinator bypasses individual cache-invalidation safety.'

$keys = @(
    'resource-locator','revival-buffs','full-speed-carrying','better-engines',
    'better-freezer-beehive','better-plasma-drills','chemical-fertilizer',
    'dual-fluid-cannon','raid-detector','wireless-vacuum-pipe',
    'network-storage-chest'
)
foreach ($key in $keys) {
    Assert-Contains $coordinator ('"' + $key + '"') "Coordinator is missing gameplay mod: $key"
}
Assert-True ($coordinator.IndexOf('"wireless-vacuum-pipe"') -lt $coordinator.IndexOf('"network-storage-chest"')) `
    'Wireless Vacuum Pipe is not installed before Network Storage Chest.'
foreach ($needle in @(
    'EnableDependencyPair(result)',
    'DualFluidCannonPatchCoordinator.SetCannonEnabled',
    'DualFluidCannonPatchCoordinator.SetChemicalEnabled',
    'AddNotAttempted(batch, after, 0)',
    'Continue installing independent compatible mods')) {
    if ($needle -eq 'Continue installing independent compatible mods') { continue }
    Assert-Contains $coordinator $needle "Missing batch safety behavior: $needle"
}

foreach ($needle in @(
    'id="allGameplayModsSwitch"','role="switch"','aria-checked="false"',
    'gameplayBatchInstallModal','gameplayBatchDangerModal','gameplayBatchResultModal',
    'ENABLE ALL COMPATIBLE MODS','DEVELOPER COMMANDS: MANUAL',
    'window.external.SetAllGameplayMods(enabled)',
    "setAttribute('aria-checked',summary.state==='mixed'?'mixed'",
    'installedSaveSensitiveGameplayMods','secret-all-switch.busy',
    '@media(prefers-reduced-motion:reduce)')) {
    Assert-Contains $html $needle "Batch UI is missing: $needle"
}
$individualUpdateButtons = @{
    developerCommandsUpdate = 'updateDeveloperCommandsMod()'
    resourceLocatorUpdate = 'updateResourceLocatorMod()'
    fullSpeedCarryingUpdate = 'updateFullSpeedCarryingMod()'
    betterEnginesUpdate = 'updateBetterEnginesMod()'
    betterFreezerBeehiveUpdate = 'updateBetterFreezerBeehiveMod()'
    betterPlasmaDrillsUpdate = 'updateBetterPlasmaDrillsMod()'
    raidDetectorUpdate = 'updateRaidDetectorMod()'
    wirelessVacuumPipeUpdate = 'updateWirelessVacuumPipeMod()'
    networkStorageChestUpdate = 'updateNetworkStorageChestMod()'
    revivalBuffUpdate = 'updateRevivalBuffMod()'
    chemicalFertilizerUpdate = 'updateChemicalFertilizerMod()'
    dualFluidCannonUpdate = 'updateDualFluidCannonMod()'
}
foreach ($entry in $individualUpdateButtons.GetEnumerator()) {
    Assert-Contains $html ('id="' + $entry.Key + '"') `
        "Patch Catalog card is missing its update button: $($entry.Key)"
    Assert-Contains $html ('onclick="' + $entry.Value + '"') `
        "Patch Catalog update button has no callback: $($entry.Key)"
    Assert-Contains $html ('function ' + $entry.Value.Substring(0, $entry.Value.Length - 2) + '(){') `
        "Patch Catalog update callback is missing: $($entry.Value)"
}
Assert-Contains $html 'setDeveloperCommandsMod(true,secretDeveloperCommandsMode);' `
    'Developer Commands update does not preserve the selected access mode.'
$snapshotBlock = $html.Substring(
    $html.IndexOf('function gameplayModSnapshot'),
    $html.IndexOf('function renderAllGameplayModsControl') - $html.IndexOf('function gameplayModSnapshot'))
Assert-True (-not $snapshotBlock.Contains('secretDeveloperCommands')) `
    'Developer Commands leaked into all-mods UI state calculations.'
foreach ($key in $keys) {
    Assert-Contains $snapshotBlock ("key:'$key'") "UI state matrix is missing gameplay mod: $key"
}

$script = [regex]::Match($html, '(?is)<script[^>]*>(.*?)</script>').Groups[1].Value
$stateCode = @"
var secretResourceLocatorInstalled=false,secretResourceLocatorNeedsUpdate=false,secretResourceLocatorCanApply=true,secretResourceLocatorReason='';
var secretRevivalBuffInstalled=false,secretRevivalBuffNeedsUpdate=false,secretRevivalBuffCanApply=true,secretRevivalBuffReason='';
var secretFullSpeedCarryingInstalled=false,secretFullSpeedCarryingNeedsUpdate=false,secretFullSpeedCarryingCanApply=true,secretFullSpeedCarryingReason='';
var secretBetterEnginesInstalled=false,secretBetterEnginesNeedsUpdate=false,secretBetterEnginesCanApply=true,secretBetterEnginesReason='';
var secretBetterFreezerBeehiveInstalled=false,secretBetterFreezerBeehiveNeedsUpdate=false,secretBetterFreezerBeehiveCanApply=true,secretBetterFreezerBeehiveReason='';
var secretBetterPlasmaDrillsInstalled=false,secretBetterPlasmaDrillsNeedsUpdate=false,secretBetterPlasmaDrillsCanApply=true,secretBetterPlasmaDrillsReason='';
var secretChemicalFertilizerInstalled=false,secretChemicalFertilizerNeedsUpdate=false,secretChemicalFertilizerCanApply=true,secretChemicalFertilizerReason='';
var secretDualFluidCannonInstalled=false,secretDualFluidCannonNeedsUpdate=false,secretDualFluidCannonCanApply=true,secretDualFluidCannonError='',secretDualFluidCannonReason='';
var secretRaidDetectorInstalled=false,secretRaidDetectorNeedsUpdate=false,secretRaidDetectorCanApply=true,secretRaidDetectorReason='';
var secretWirelessVacuumPipeInstalled=false,secretWirelessVacuumPipeNeedsUpdate=false,secretWirelessVacuumPipeCanApply=true,secretWirelessVacuumPipeReason='';
var secretNetworkStorageChestInstalled=false,secretNetworkStorageChestNeedsUpdate=false,secretNetworkStorageChestCanApply=true,secretNetworkStorageChestReason='';
$snapshotBlock
function check(value,message){if(!value)throw new Error(message);}
var s=allGameplayModsState();check(s.state==='off'&&s.pending===11,'clean state is not Off');
secretResourceLocatorInstalled=true;s=allGameplayModsState();check(s.state==='mixed','partial set is not Mixed');
secretRevivalBuffInstalled=secretFullSpeedCarryingInstalled=secretBetterEnginesInstalled=secretBetterFreezerBeehiveInstalled=secretBetterPlasmaDrillsInstalled=secretChemicalFertilizerInstalled=secretDualFluidCannonInstalled=secretRaidDetectorInstalled=secretWirelessVacuumPipeInstalled=secretNetworkStorageChestInstalled=true;
s=allGameplayModsState();check(s.state==='on'&&s.pending===0,'complete set is not On');
secretNetworkStorageChestInstalled=false;secretNetworkStorageChestCanApply=false;s=allGameplayModsState();check(s.state==='on'&&s.skipped===1,'blocked mod incorrectly prevents On');
secretNetworkStorageChestCanApply=true;s=allGameplayModsState();check(s.state==='mixed'&&s.pending===1,'newly compatible mod does not return to Mixed');
secretNetworkStorageChestInstalled=true;secretBetterPlasmaDrillsNeedsUpdate=true;s=allGameplayModsState();check(s.state==='mixed','definition update does not produce Mixed');
"@

$syntaxPath = Join-Path $env:TEMP ('scraplab-all-mods-syntax-' + [guid]::NewGuid().ToString('N') + '.js')
$statePath = Join-Path $env:TEMP ('scraplab-all-mods-state-' + [guid]::NewGuid().ToString('N') + '.js')
try {
    [IO.File]::WriteAllText($syntaxPath, $script, [Text.UTF8Encoding]::new($false))
    & $Node --check $syntaxPath
    Assert-True ($LASTEXITCODE -eq 0) 'Embedded JavaScript syntax validation failed.'
    [IO.File]::WriteAllText($statePath, $stateCode, [Text.UTF8Encoding]::new($false))
    & $Node $statePath
    Assert-True ($LASTEXITCODE -eq 0) 'All-mods UI state matrix failed.'

    $harnessExe = Join-Path $env:TEMP ('scraplab-all-mods-harness-' + [guid]::NewGuid().ToString('N') + '.exe')
    & $compiler /nologo /target:exe /out:$harnessExe `
        (Join-Path $root 'source\Patching\GameplayModsBatchCoordinator.cs') `
        (Join-Path $root 'tests\GameplayModsBatchCoordinatorHarness.cs')
    Assert-True ($LASTEXITCODE -eq 0) 'Batch coordinator behavior harness did not compile.'
    & $harnessExe
    Assert-True ($LASTEXITCODE -eq 0) 'Batch coordinator behavior harness failed.'
}
finally {
    Remove-Item -LiteralPath $syntaxPath,$statePath -Force -ErrorAction SilentlyContinue
    if ($harnessExe) { Remove-Item -LiteralPath $harnessExe -Force -ErrorAction SilentlyContinue }
}

Write-Host 'Gameplay Mods batch regression passed: protocol, coordinator, dependencies, UI states, confirmations, and JavaScript.'
