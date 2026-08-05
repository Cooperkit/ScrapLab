$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$managerPath = Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
$partPath = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua'
$graphPath = Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'
$transferPath = Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeTransfer.lua'
$harnessPath = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipePhase4Harness.lua'
$installerPath = Join-Path $root 'tools\experiments\Manage-WirelessVacuumPipePhase4.ps1'

function Assert-True([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Assert-Contains([string]$Text,[string]$Needle,[string]$Message){Assert-True ($Text.IndexOf($Needle,[StringComparison]::Ordinal)-ge 0) $Message}
function Assert-Before([string]$Text,[string]$First,[string]$Second,[string]$Message){
    $a=$Text.IndexOf($First,[StringComparison]::Ordinal);$b=$Text.IndexOf($Second,[StringComparison]::Ordinal)
    Assert-True ($a-ge 0 -and $b-ge 0 -and $a-lt $b) $Message
}

$manager=Get-Content -LiteralPath $managerPath -Raw
$part=Get-Content -LiteralPath $partPath -Raw
$graph=Get-Content -LiteralPath $graphPath -Raw
$transfer=Get-Content -LiteralPath $transferPath -Raw
$harness=Get-Content -LiteralPath $harnessPath -Raw
$installer=Get-Content -LiteralPath $installerPath -Raw

foreach($needle in @(
    'WIRELESS VACUUM PIPE MANAGER v6','MANAGER_SCHEMA_VERSION = 3','WirelessPipeTransfer.Sv_ServerOnCreate',
    'WirelessPipeTransfer.Sv_ServerOnFixedUpdate','WirelessPipeTransfer.Sv_OnEndpointTopologyChanged',
    'directionalCursors','Sv_GetDirectionalDebugSnapshot','Sv_ConsumeEndpointActivity','Sv_DebugSetEndpointMode'
)){Assert-Contains $manager $needle "Manager Phase 4 contract missing: $needle"}
Assert-True (-not($manager -match 'setmetatable\s*\(')) 'The manager calls setmetatable, which Scrap Mechanic does not expose.'

foreach($needle in @(
    'DIRECTIONAL TRANSFER v4','ATTEMPT_INTERVAL_TICKS = 4','COMMIT_DELAY_TICKS = 1','MAX_IDLE_BACKOFF_TICKS = 40',
    'sm.pipeGraph.getInputContainers','sm.pipeGraph.getOutputContainers','nativeContainerShapes',
    'ScrapLabPipeGraph.getLocalPhysicalContainerShapes',
    'pending','locks','orderedFromCursor','directionalCursors','senderGeneration','receiverGeneration',
    'endpoint cell not ready','endpoint generation changed','findFreshContainerShape',
    'sm.container.canSpend','sm.container.canCollect','sm.container.beginTransaction',
    'sm.container.spend','sm.container.collect','sm.container.endTransaction',
    'recordSuccessfulCursor','transactionFailures','staleGuardRejects','Sv_OnEndpointTopologyChanged','Sv_ConsumeEndpointActivity',
    'quantityPerTransfer = 1','directContainerShapes','senderDirectOnly','receiverDirectOnly','endpoint scope changed',
    'applyIdleBackoff','resetBackoff','idleBackoffs','backoffSkips'
)){Assert-Contains $transfer $needle "Directional transfer contract missing: $needle"}

foreach($needle in @(
    'function ScrapLabPipeGraph.getLocalPhysicalContainerShapes','getPhysicalContainerShapes( shape )',
    'Local-only physical view for SEND/RECEIVE routing','never follows a'
)){Assert-Contains $graph $needle "Local physical graph contract missing: $needle"}

Assert-True ($transfer.IndexOf('ScrapLabPipeGraph.getInputContainers',[StringComparison]::Ordinal)-lt 0) 'SEND must use the local native graph, not the virtual Link graph.'
Assert-True ($transfer.IndexOf('ScrapLabPipeGraph.getOutputContainers',[StringComparison]::Ordinal)-lt 0) 'RECEIVE must use the local native graph, not the virtual Link graph.'
Assert-Contains $transfer 'tick >= pending.commitTick then commitPending' 'Commit processing must honor the delayed pending tick.'
Assert-Before $transfer 'local sourceShape = findFreshContainerShape' 'sm.container.beginTransaction()' 'Both graph routes must be freshly resolved before transaction start.'
Assert-Before $transfer 'if not canSpend( source' 'sm.container.beginTransaction()' 'Source capacity must be revalidated before transaction start.'
Assert-Before $transfer 'if not canCollect( destination' 'sm.container.beginTransaction()' 'Destination capacity must be revalidated before transaction start.'
Assert-True ($transfer.IndexOf('sm.container.endTransaction()',[StringComparison]::Ordinal)-lt $transfer.LastIndexOf('recordSuccessfulCursor(',[StringComparison]::Ordinal)) 'Fairness cursors may advance only after a successful commit.'
Assert-True (($transfer|Select-String -Pattern 'sm\.container\.beginTransaction\(\)' -AllMatches).Matches.Count -eq 1) 'Directional transfer must have exactly one transaction assembly path.'
Assert-True (($transfer|Select-String -Pattern 'sm\.container\.spend\(' -AllMatches).Matches.Count -eq 1) 'Directional transfer must queue exactly one spend operation.'
Assert-True (($transfer|Select-String -Pattern 'sm\.container\.collect\(' -AllMatches).Matches.Count -eq 1) 'Directional transfer must queue exactly one collect operation.'
foreach($forbidden in @('sm.shape.createPart','sm.container.collect( source','itemBuffer','escrowInventory','spawnItem')){
    Assert-True ($transfer.IndexOf($forbidden,[StringComparison]::Ordinal)-lt 0) "Unsafe item fallback found: $forbidden"
}
Assert-True ($transfer.IndexOf('live.owner:',[StringComparison]::Ordinal)-lt 0) 'Manager transfer code must not call a foreign shape-script method.'

foreach($needle in @(
    'sv_onDirectionalActivity','cl_n_directionalActivity','PipeEffectPlayer','sm.pipeGraph.direction.incoming',
    'sm.pipeGraph.direction.outgoing','data.crossWorld and 16 or 10','Sv_ConsumeEndpointActivity'
)){Assert-Contains $part $needle "Endpoint directional presentation contract missing: $needle"}

foreach($needle in @(
    'PHASE 4 HARNESS v3','/slpipe4','action == "auto"','sm.creation.importFromString','Automatic directional-transfer station created',
    'scheduler-contract','exact-transaction-accounting','same-world-delivery','cross-world-delivery',
    'receiver-round-robin','empty-source-backpressure','full-destination-backpressure',
    'bounded-group-lock','manager-invariants','fresh-resolution-guard','cleanup','sv_slpipe4BeginRecovery',
    'sv_slpipe4RecoveryCellLoaded','p4RequestRecoveryCell','Interrupted fixture cleanup is still pending'
)){Assert-Contains $harness $needle "Phase 4 automatic harness missing: $needle"}
Assert-Contains $harness 'WirelessPipeManager.Sv_DebugSetEndpointMode' 'Harness must change disposable modes through the manager script context.'
Assert-True ($harness.IndexOf('live.owner:',[StringComparison]::Ordinal)-lt 0) 'Harness must not call a foreign shape-script method.'
Assert-True ($harness.IndexOf('{ token = "recovery" }',[StringComparison]::Ordinal)-lt 0) 'Recovery must use its dedicated cell-ready callback instead of the normal remote-fixture callback.'
Assert-Before $harness 'if self.sv.scrapLabPipePhase4.cleanup then' 'self.sv.scrapLabPipePhase4.cleanup = { token = token' 'A pending cleanup receipt must block creation of a replacement receipt.'

foreach($needle in @(
    'WirelessVacuumPipePhase3.json','Phase 3 must be installed before Phase 4',
    '$definitionVersion = 3','ScrapLabPipeGraph.lua','WirelessPipeTransfer.lua','WirelessVacuumPipePhase4Harness.lua','Phase2ReceiptBackup','Phase3ReceiptBackup',
    'Assert-Phase2OwnedMatchesReceipt','Assert-Phase3OwnedMatchesReceipt','Write-AtomicBytes','Backup verification failed','core_data.cbo',
    'NewReceiptEntry','Update backup verification failed','PARTIAL_OR_CONFLICT'
)){Assert-Contains $installer $needle "Phase 4 installer safety contract missing: $needle"}

$errors=$null;$tokens=$null
[Management.Automation.Language.Parser]::ParseFile($installerPath,[ref]$tokens,[ref]$errors)|Out-Null
Assert-True ($errors.Count-eq 0) ('Phase 4 installer has PowerShell syntax errors: '+(($errors|ForEach-Object Message)-join '; '))

'Wireless Vacuum Pipe Phase 4 directional-transfer regression checks passed.'
