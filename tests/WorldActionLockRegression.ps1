param(
    [string]$RaidRescueExe,
    [string]$Node
)

$ErrorActionPreference = "Stop"

if ([String]::IsNullOrWhiteSpace($RaidRescueExe)) {
    $RaidRescueExe = Join-Path $PSScriptRoot "..\dist\ScrapLab.exe"
}
if ([String]::IsNullOrWhiteSpace($Node)) {
    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    $Node = if ($nodeCommand) { $nodeCommand.Source } else {
        Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
    }
}
if (-not (Test-Path -LiteralPath $Node)) {
    throw "The bundled Node runtime was not found."
}

$assembly = [Reflection.Assembly]::LoadFrom(
    [IO.Path]::GetFullPath($RaidRescueExe))
$uiType = $assembly.GetType("RaidRescue.UiHtml", $true)
$html = [string]$uiType.GetField(
    "Content", [Reflection.BindingFlags]"Public,Static").GetValue($null)
$matches = [regex]::Matches(
    $html, "(?is)<script[^>]*>(.*?)</script>")
if ($matches.Count -ne 1) {
    throw "Expected one embedded application script."
}
foreach ($requiredUi in @(
    'id="betterPlasmaDrillsUpdate"',
    'DAMAGE UPDATE AVAILABLE',
    'function updateBetterPlasmaDrillsMod()',
    'unit damage now scales from 20 to 300 per second',
    'id="betterFreezerBeehiveSwitch"',
    'function toggleBetterFreezerBeehiveMod()',
    'GetBetterFreezerBeehiveModStatus',
    'SetBetterFreezerBeehiveMod',
    'id="raidDetectorSwitch"',
    'id="raidDetectorUpdate"',
    'DETECTOR UPDATE AVAILABLE',
    'function updateRaidDetectorMod()',
    'function toggleRaidDetectorMod()',
    'GetRaidDetectorModStatus',
    'SetRaidDetectorMod',
    'I REMOVED EVERY RAID DETECTOR - DISABLE',
    '11 AVAILABLE'
)) {
    if (-not $html.Contains($requiredUi)) {
        throw "The Better Plasma Drills update UI is missing: $requiredUi"
    }
}

$harness = @'
var nodes={
 analyzeBtn:{disabled:false},browseBtn:{disabled:false},saveDisplay:{disabled:false},
 scanDroppedItemsBtn:{disabled:false},scanPerformanceBtn:{disabled:false},
 repairOrphanedCropsBtn:{disabled:false},clearAllBtn:{disabled:false},
 clearExpiredItemsBtn:{disabled:false},clearDroppedItemsBtn:{disabled:false}
};
document={getElementById:function(id){return nodes[id]||null;}};
renderSecretModsState=function(){};
closeSaveMenu=function(){};
lastAnalysis={
 CanRepairOrphanedCrops:true,CanClear:false,
 CanClearExpiredDroppedItems:false,CanClearDroppedItems:true
};
gameRunning=false;
operationBusy=true;
applyGameLock(false);
if(!nodes.scanDroppedItemsBtn.disabled||!nodes.scanPerformanceBtn.disabled||
   !nodes.repairOrphanedCropsBtn.disabled||!nodes.clearDroppedItemsBtn.disabled){
 throw new Error('World actions were not locked during an operation.');
}
operationBusy=false;
applyGameLock(false);
if(nodes.scanDroppedItemsBtn.disabled||nodes.scanPerformanceBtn.disabled){
 throw new Error('Scan actions stayed disabled after the operation finished.');
}
if(nodes.repairOrphanedCropsBtn.disabled){
 throw new Error('An eligible crop repair stayed disabled.');
}
if(!nodes.clearAllBtn.disabled||!nodes.clearExpiredItemsBtn.disabled){
 throw new Error('An ineligible repair action was incorrectly enabled.');
}
if(nodes.clearDroppedItemsBtn.disabled){
 throw new Error('An eligible dropped-item action stayed disabled.');
}
gameRunning=true;
applyGameLock(true);
if(!nodes.scanDroppedItemsBtn.disabled||!nodes.scanPerformanceBtn.disabled||
   !nodes.repairOrphanedCropsBtn.disabled||!nodes.clearDroppedItemsBtn.disabled){
 throw new Error('The game-running safety lock did not disable world actions.');
}
'@

$script = $matches[0].Groups[1].Value +
    [Environment]::NewLine + $harness
$script | & $Node -
if ($LASTEXITCODE -ne 0) {
    throw "The world-action lock regression failed."
}

Write-Host (
    "World action lock regression passed: active-operation lock, delayed " +
    "unlock, per-action eligibility, and game-running safety lock.")
