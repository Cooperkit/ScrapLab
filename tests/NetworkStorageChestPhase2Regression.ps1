[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$partRoot = Join-Path $root 'source\Patching\Parts\NetworkStorageChest'
$scriptPath = Join-Path $partRoot 'NetworkStorageChest.lua'
$guiPath = Join-Path $partRoot 'NetworkStorageChest.gui'
$harnessPath = Join-Path $partRoot 'NetworkStorageChestPhase2Harness.lua'
$deployerPath = Join-Path $partRoot 'NetworkStorageChestPhase0Deploy.ps1'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Find-Widget($Node, [string]$Name) {
    if ($Node.Name -eq $Name) { return $Node }
    foreach ($child in @($Node.Childs)) {
        $match = Find-Widget $child $Name
        if ($null -ne $match) { return $match }
    }
    return $null
}

$script = Get-Content -LiteralPath $scriptPath -Raw
$harness = Get-Content -LiteralPath $harnessPath -Raw
$deployer = Get-Content -LiteralPath $deployerPath -Raw
$gui = Get-Content -LiteralPath $guiPath -Raw | ConvertFrom-Json

foreach ($functionName in @(
    'sv_validateWithdrawalRequest', 'sv_executeLocalWithdrawal', 'sv_n_withdraw',
    'cl_requestWithdrawal', 'cl_n_withdrawResult', 'cl_refreshWithdrawalControls'
)) {
    Assert-True ($script.Contains("function NetworkStorageChest.$functionName")) "Missing Phase 2 function: $functionName"
}

Assert-True ($script.Contains('sm.container.spendFromSlot( entry.source.container, entry.source.slot, itemUuid, entry.quantity, true )')) 'Withdrawal does not spend exact source slots.'
Assert-True ($script.Contains('sm.container.collect( destination, itemUuid, quantity )')) 'Withdrawal does not collect once into the destination.'
Assert-True ($script.Contains('local descriptors, _, _, topologyFailure, currentTopologyKey = self:sv_collectTopologySnapshot()') -and
    $script.Contains('currentTopologyKey ~= self.sv.topologyKey')) 'Combined spend/collect topology generation protection is missing.'
Assert-True ($script.Contains('revision ~= record.revision')) 'Container revision protection is missing.'
Assert-True (-not $script.Contains('if not sm.game.getLimitedInventory() then return wanted end')) 'Capacity clamping incorrectly bypasses real container capacity.'

$takeOne = Find-Widget $gui 'TakeOneButton'
$takeStack = Find-Widget $gui 'TakeStackButton'
$takeAll = Find-Widget $gui 'TakeAllButton'
$scroll = Find-Widget $gui 'CatalogScrollHost'
$selection = Find-Widget $gui 'SelectionStatus'
foreach ($widget in @($takeOne, $takeStack, $takeAll, $scroll, $selection)) {
    Assert-True ($null -ne $widget) 'A required Phase 2 GUI widget is missing.'
}
Assert-True ($takeOne.Caption -eq 'TAKE 1' -and $takeStack.Caption -eq 'TAKE STACK' -and $takeAll.Caption -eq 'TAKE ALL THAT FITS') 'Withdrawal button captions changed unexpectedly.'
Assert-True ($takeOne.Enabled -eq $false -and $takeStack.Enabled -eq $false -and $takeAll.Enabled -eq $false) 'Withdrawal controls must begin disabled.'
Assert-True ($scroll.height -eq 300 -and $scroll.y -eq 62 -and $selection.y -eq 366 -and $takeOne.y -eq 400) 'Expanded catalog viewport or action rail geometry regressed.'
Assert-True (($takeOne.width + $takeStack.width + $takeAll.width) -eq 658) 'Action buttons no longer fit the catalog rail.'

foreach ($testName in @(
    'take-one-smallest-stack-first', 'take-stack-across-containers', 'take-all-multi-container',
    'take-all-capacity-clamp', 'full-destination-no-spend', 'stale-revision-aborts',
    'concurrent-final-item', 'missing-item-no-spend', 'session-token-rotation',
    'expired-session-rejected', 'stale-generation-rejected', 'request-rate-limited'
)) {
    Assert-True ($harness.Contains($testName)) "Automatic harness lost test: $testName"
}
Assert-True ($harness.Contains('beforeTotal') -and $harness.Contains('-conservation')) 'Per-scenario conservation checks are missing.'
Assert-True ($harness.Contains('/slstorage2 auto')) 'Automatic Phase 2 command is missing.'
Assert-True ($deployer.Contains('NetworkStorageChestPhase2Harness.lua')) 'Deployer does not own/load the Phase 2 harness.'

Write-Host 'Network Storage Chest Phase 2 regression checks passed.'
