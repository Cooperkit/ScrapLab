[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$part = Join-Path $root 'source\Patching\Parts\NetworkStorageChest'
$lua = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChest.lua') -Raw
$harness = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChestPhase3Harness.lua') -Raw
$deployer = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChestPhase0Deploy.ps1') -Raw
$gui = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChest.gui') -Raw | ConvertFrom-Json
function Assert-True([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
foreach($name in @('sv_collectDepositContainers','sv_planDepositSlot','sv_routeDepositSlot','sv_processDepositBuffer','sv_sendDepositStatus','cl_n_depositStatus')){
    Assert-True $lua.Contains("function NetworkStorageChest.$name") "Missing Phase 3 function: $name"
}
Assert-True $lua.Contains('sm.container.spendFromSlot( buffer, slot, itemUuid, routed, true )') 'Deposit source is not spent from its exact tray slot.'
Assert-True $lua.Contains('entry.descriptor.container:getRevision() ~= entry.revision') 'Destination revision guard is missing.'
Assert-True $lua.Contains('BLOCKED_DEPOSIT_UUIDS') 'Machine-owned destination exclusion is missing.'
Assert-True $lua.Contains('PARTIAL_DESTINATIONS_FULL') 'Safe partial routing UI state is missing.'
foreach($test in @('specialized-container-first','fullest-partial-stack-first','same-item-before-empty','split-allocation','partial-capacity-left-safe','no-destination-keeps-items','destination-revision-conflict','terminal-buffer-excluded')){
    Assert-True $harness.Contains($test) "Harness lost test: $test"
}
Assert-True $harness.Contains('-conservation') 'Harness conservation assertions are missing.'
Assert-True ($harness.Contains('chestShapes = {}') -and $harness.Contains('r.chests[#r.chests + 1] = sl3Container( shape )')) 'Harness does not separate spawned shapes from engine containers.'
Assert-True $harness.Contains('sm.container.abortTransaction') 'Harness does not abort a failed setup transaction.'
Assert-True $harness.Contains('/slstorage3 auto') 'Automatic Phase 3 command is missing.'
Assert-True $deployer.Contains('NetworkStorageChestPhase3Harness.lua') 'Deployer does not load/own the Phase 3 harness.'
Assert-True ((($gui.Childs | Where-Object Name -eq 'MainPanel').Count) -eq 0) 'Unexpected nested main panel.'
Write-Host 'Network Storage Chest Phase 3 regression checks passed.'
