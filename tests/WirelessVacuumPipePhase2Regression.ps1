$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$manager = Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
$endpoint = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua'
$harness = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipePhase2Harness.lua'
$shapeSet = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.shapeset'
$layout = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.layout'
$installer = Join-Path $root 'tools\experiments\Manage-WirelessVacuumPipePhase2.ps1'
$gameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    Assert-True ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) $Message
}

function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }

$managerText = Get-Content -LiteralPath $manager -Raw
$endpointText = Get-Content -LiteralPath $endpoint -Raw
$harnessText = Get-Content -LiteralPath $harness -Raw

foreach ($needle in @(
    'WirelessPipeManager.isSaveObject = true',
    'self.sv.saved = self.storage:load()',
    'MAX_ACTIVE_ENDPOINT_CELLS = 64',
    'HANDLE_IDLE_GRACE_TICKS = 200',
    'RECONCILE_CONFIRM_TICKS = 80',
    'state = "UNCONFIRMED"',
    'state = "LOAD_ERROR"',
    'world:loadCellWithHandle',
    'entry.handle:release()',
    'sv_getMatchingIds',
    'CROSS-WORLD LINKED',
    'REMOTE CELL LOAD LIMIT',
    'sv_validateInvariants'
	,'directOnly = record.directOnly ~= false'
	,'Sv_GetDirectionalSourceEntries'
)) { Assert-Contains $managerText $needle "Manager contract is missing: $needle" }

foreach ($needle in @(
    'WirelessVacuumPipe.maxParentCount = 1',
    'WirelessVacuumPipe.maxChildCount = 0',
    'WirelessVacuumPipe.connectionInput = sm.interactable.connectionType.logic',
    'server_onWorldChanged',
    'server_onUnload',
    'Capture one final authoritative state',
    'WirelessPipeManager.Sv_RefreshEndpoint( data, self.shape, self, self.sv.generation )',
    'self.sv.unloaded = true',
    'server_onDestroy',
    'not self.sv.unloaded',
    'DUPLICATE ENDPOINT ID',
    'math.floor( position.x / 64 )',
    'ModeLink',
    'ModeSend',
    'ModeReceive',
    'sv_n_requestStatus',
    'sv_sendAuthoritativeStatus',
    'cl_n_applyAuthoritativeStatus',
    'self.cl.data.mode = mode',
    'guiDirty = true',
    'function WirelessVacuumPipe.client_onUpdate'
	,'ENDPOINT_STORAGE_VERSION = 2'
	,'directOnly = true'
	,'sv_n_setDirectOnly'
	,'ScopeButton'
)) { Assert-Contains $endpointText $needle "Endpoint contract is missing: $needle" }

foreach ($forbidden in @('sm.pipeGraph.getInputContainers', 'sm.pipeGraph.getOutputContainers', 'sm.container.beginTransaction')) {
    Assert-True ($managerText.IndexOf($forbidden, [StringComparison]::Ordinal) -lt 0) "Phase 3/4 behavior leaked into the Phase 2 manager: $forbidden"
    Assert-True ($endpointText.IndexOf($forbidden, [StringComparison]::Ordinal) -lt 0) "Phase 3/4 behavior leaked into the Phase 2 endpoint: $forbidden"
}

foreach ($needle in @('/slpipe2', 'sv_slpipe2Track', 'moving-creation-position', 'moving-creation-cell', 'elevator-world-change', 'bounded-handle-cap')) {
    Assert-Contains $harnessText $needle "Harness contract is missing: $needle"
}

$shape = Get-Content -LiteralPath $shapeSet -Raw | ConvertFrom-Json
$part = $shape.partList[0]
Assert-True ($part.uuid -eq 'a34d9af0-4ba0-431d-b647-2d5435ecf138') 'The permanent part UUID changed.'
Assert-True ($part.renderable -eq '$SURVIVAL_DATA/Objects/Renderable/vacuumpipe/obj_vacuumpipe_pipe1.rend') 'The shape does not reuse Vacuum Pipe 1 renderable.'
Assert-True ($part.hull.col -eq '$SURVIVAL_DATA/Objects/Collision/obj_pneumatic_pipe_01.obj') 'The shape does not reuse Vacuum Pipe 1 collision.'
Assert-True ($part.pipe.openings.Count -eq 2) 'The shape must have exactly two physical pipe openings.'
Assert-True ($part.pipe.type -eq 'Pipe') 'The shape is not registered as a physical pipe.'
[xml](Get-Content -LiteralPath $layout -Raw) | Out-Null
$layoutText = Get-Content -LiteralPath $layout -Raw
foreach ($needle in @('name="ModeLinkLabel"', 'value="LINK"', 'name="ModeSendLabel"', 'value="SEND"', 'name="ModeReceiveLabel"', 'value="RECEIVE"', 'name="ScopeButton"', 'value="DIRECT CONTAINER ONLY"')) {
    Assert-Contains $layoutText $needle "Mode button label is missing: $needle"
}
Assert-True ($layoutText.IndexOf('key="WordWrap"', [StringComparison]::Ordinal) -lt 0) 'The layout still uses the unsupported WordWrap property.'

if (Test-Path -LiteralPath $gameRoot) {
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('ScrapLabPhase2Regression-' + [Guid]::NewGuid().ToString('N'))
    $fakeGame = Join-Path $tempRoot 'Game'
    $receiptRoot = Join-Path $tempRoot 'Receipt'
    $backupRoot = Join-Path $tempRoot 'Backups'
    try {
		$developmentReceiptPath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Development State\WirelessVacuumPipePhase2.json'
		$developmentReceipt = if (Test-Path -LiteralPath $developmentReceiptPath) { Get-Content -LiteralPath $developmentReceiptPath -Raw | ConvertFrom-Json } else { $null }
		$productionReceiptPath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active\WirelessVacuumPipe.json'
		$productionReceipt = if (Test-Path -LiteralPath $productionReceiptPath) { Get-Content -LiteralPath $productionReceiptPath -Raw | ConvertFrom-Json } else { $null }
        foreach ($relative in @(
            'Survival\Scripts\game\survival_items.lua',
            'Survival\Objects\Database\shapesets.json',
            'Survival\ScriptableObjects\scriptableObjectSets\sob_managers.sobset',
            'Survival\Scripts\game\SurvivalGame.lua'
        )) {
            $source = Join-Path $gameRoot $relative
			if ($developmentReceipt) {
				$receiptFile = $developmentReceipt.Files | Where-Object { $_.Relative -eq $relative -and $_.Kind -ne 'Owned' }
				if ($receiptFile -and (Test-Path -LiteralPath $receiptFile.BackupPath)) { $source = $receiptFile.BackupPath }
			}
			elseif ($productionReceipt) {
				$receiptFile = $productionReceipt.Files | Where-Object { $_.RelativePath -eq $relative } | Select-Object -First 1
				if ($receiptFile -and $receiptFile.BackupPath -and (Test-Path -LiteralPath $receiptFile.BackupPath)) { $source = $receiptFile.BackupPath }
			}
            $target = Join-Path $fakeGame $relative
            [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
            [IO.File]::Copy($source, $target)
        }
        $originalHashes = @{}
        Get-ChildItem -LiteralPath $fakeGame -Recurse -File | ForEach-Object { $originalHashes[$_.FullName.Substring($fakeGame.Length)] = Get-Sha $_.FullName }

        $installJson = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Install -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame
        $install = $installJson | ConvertFrom-Json
        Assert-True ($install.State -eq 'INSTALLED') "Fixture install did not reach INSTALLED: $($install.State)"
        $null = Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Objects\Database\shapesets.json') -Raw | ConvertFrom-Json
        $managerSet = Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\ScriptableObjects\scriptableObjectSets\sob_managers.sobset') -Raw | ConvertFrom-Json
        $managerRegistration = @($managerSet.scriptableObjectList | Where-Object { $_.uuid -eq '8a6e31c4-575f-40fa-96f3-85bd23eb34ce' })
        Assert-True ($managerRegistration.Count -eq 1) 'Wireless manager registration must exist exactly once.'
        Assert-True ($managerRegistration[0].singleton -eq $true) 'Wireless manager must be auto-created as a singleton.'
        Assert-Contains (Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua') -Raw) 'WirelessPipeManager.isSaveObject = true' 'The singleton manager must declare save-object storage.'
        Assert-True (Test-Path -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua')) 'Owned manager was not installed.'

        $removeJson = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Remove -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame
        $remove = $removeJson | ConvertFrom-Json
        Assert-True ($remove.State -eq 'NOT_INSTALLED') "Fixture removal did not reach NOT_INSTALLED: $($remove.State)"
        foreach ($entry in $originalHashes.GetEnumerator()) {
            $path = $fakeGame + $entry.Key
            Assert-True ((Get-Sha $path) -eq $entry.Value) "Fixture removal did not restore exact bytes: $($entry.Key)"
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) { Remove-Item -LiteralPath $tempRoot -Recurse -Force }
    }
}

'Wireless Vacuum Pipe Phase 2 regression checks passed.'
