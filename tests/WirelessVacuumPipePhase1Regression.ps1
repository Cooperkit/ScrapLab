$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$probePath = Join-Path $projectRoot 'source\Patching\Parts\WirelessVacuumPipe\ScrapLabPipePhase1Probe.lua'
$lockPath = Join-Path $projectRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.phase1.json'
$managerPath = Join-Path $projectRoot 'tools\experiments\Manage-WirelessVacuumPipePhase1Probe.ps1'

Assert-True (Test-Path -LiteralPath $probePath) 'Phase 1 Lua probe is missing.'
Assert-True (Test-Path -LiteralPath $lockPath) 'Phase 1 lock file is missing.'
Assert-True (Test-Path -LiteralPath $managerPath) 'Phase 1 probe manager is missing.'

$probe = Get-Content -LiteralPath $probePath -Raw -Encoding UTF8
$requiredLua = @(
    'world:loadCellWithHandle( endpoint.cellX, endpoint.cellY, nil )',
    'sm.body.getAllBodies( world )',
    'sm.container.beginTransaction()',
    'sm.container.spend( source, ProbeItem, quantity, true )',
    'sm.container.collect( destination, ProbeItem, quantity, true )',
    'sm.container.endTransaction()',
    'sm.container.abortTransaction()',
	'container:canCollect( ProbeItem, middle )',
	'sm.shape.createPart( ProbeChest, position, sm.quat.identity(), false, true, world )',
    'error-before-commit',
    'error-after-commit',
    'receiver-unload',
    'endpoint-destroyed',
    'source-changed',
    'exact-full',
    'already-full',
    'save-reload',
    'host-client-observation',
	'host-client-loopback',
	'cl_slppLoopbackProbe',
	'sv_slppLoopbackAck',
    'worldType ~= "Overworld"',
    'worldType ~= "UndergroundWorld"',
	'destroyShape( 0 )',
	'sv_slppUpdateDestroyedEndpointCase',
	'commit skipped=true',
    'self.storage:save( self.sv.saved )'
)
foreach ($guard in $requiredLua) {
    Assert-True $probe.Contains($guard) "Lua guard is missing: $guard"
}
Assert-True ($probe.Contains('4c474cff-3f6a-4306-93d1-c4c74578afd2')) 'Probe does not use the verified vanilla piped Small Chest UUID.'
Assert-True (-not $probe.Contains('world = sm.world.loadWorld( world )')) 'Probe still replaces a World reference with loadWorld''s boolean return value.'
Assert-True (-not $probe.Contains('obj_pneumatic_pipe_wireless')) 'Phase 1 incorrectly registers the permanent part before Phase 2.'

$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
Assert-True ($lock.phase -eq 1) 'Phase 1 lock has the wrong phase.'
Assert-True ($lock.probeDefinitionVersion -eq 3) 'Phase 1 probe definition is not version 3.'
Assert-True ($lock.status -eq 'COMPLETE') 'Phase 1 completion evidence is not locked.'
Assert-True ($lock.automaticCases.Count -eq 8) 'Automatic matrix does not contain eight cases.'
Assert-True ($lock.manualCases.Count -eq 2) 'Manual matrix does not contain two cases.'
Assert-True ($lock.deferredReleaseCases -contains 'connected-client-observation') 'The unavailable real connected-client gate is not recorded as deferred.'
Assert-True ($lock.validationEvidence.automaticPassed -eq 8) 'The eight automatic passes were not recorded.'
Assert-True ($lock.validationEvidence.manualPassed -eq 2) 'The reload and loopback passes were not recorded.'
Assert-True ($lock.validationEvidence.failed -eq 0) 'Phase 1 evidence still records a failure.'
Assert-True ($lock.productionPhasesBlockedUntilPass.Count -eq 0) 'Phase 2 was not unlocked after the successful gate.'
Assert-True ($lock.releasePhasesBlockedUntilDeferredPass -contains 7) 'The connected-client release gate was lost.'

$fixtureRoot = Join-Path $PSScriptRoot ('.wireless-pipe-phase1-' + [Guid]::NewGuid().ToString('N'))
$gameRoot = Join-Path $fixtureRoot 'Scrap Mechanic'
$receiptRoot = Join-Path $fixtureRoot 'receipts'
$backupRoot = Join-Path $fixtureRoot 'backups'
$survivalGame = Join-Path $gameRoot 'Survival\Scripts\game\SurvivalGame.lua'
$cachePath = Join-Path $gameRoot 'Cache\Bundle\core_data.cbo'
$ownedProbe = Join-Path $gameRoot 'Survival\Scripts\ScrapLab\Experiments\WirelessVacuumPipePhase1\ScrapLabPipePhase1Probe.lua'

try {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $survivalGame)) | Out-Null
    [IO.Directory]::CreateDirectory((Split-Path -Parent $cachePath)) | Out-Null
    $baselineText = @"
SurvivalGame = class( nil )
function SurvivalGame.server_onCreate( self )
end
-- SCRAPLAB DEVELOPER COMMANDS NOCLIP v4
dofile( "`$SURVIVAL_DATA/Scripts/ScrapLab/Noclip.lua" )
-- END SCRAPLAB DEVELOPER COMMANDS NOCLIP v4
"@ -replace "(?<!`r)`n", "`r`n"
    [byte[]]$payload = [Text.UTF8Encoding]::new($false).GetBytes($baselineText)
    [byte[]]$baseline = New-Object byte[] ($payload.Length + 3)
    $baseline[0] = 0xEF; $baseline[1] = 0xBB; $baseline[2] = 0xBF
    [Array]::Copy($payload, 0, $baseline, 3, $payload.Length)
    [IO.File]::WriteAllBytes($survivalGame, $baseline)
    [IO.File]::WriteAllBytes($cachePath, [byte[]](1, 2, 3))

    $installJson = & $managerPath -Action Install -GameRoot $gameRoot `
        -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame
    $install = $installJson | ConvertFrom-Json
    Assert-True ($install.State -eq 'INSTALLED') 'Fixture install did not report INSTALLED.'
    Assert-True (Test-Path -LiteralPath $ownedProbe) 'Fixture install did not copy the owned probe.'
    Assert-True (-not (Test-Path -LiteralPath $cachePath)) 'Fixture install did not invalidate core_data.cbo.'
    [byte[]]$installedBytes = [IO.File]::ReadAllBytes($survivalGame)
    Assert-True ($installedBytes[0] -eq 0xEF -and $installedBytes[1] -eq 0xBB -and $installedBytes[2] -eq 0xBF) 'Installer did not preserve the UTF-8 BOM.'
    $installedText = [Text.UTF8Encoding]::new($false).GetString($installedBytes, 3, $installedBytes.Length - 3)
    Assert-True ($installedText.Contains("`r`n-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 1 PROBE`r`n")) 'Installer did not preserve CRLF newlines.'
    Assert-True ($installedText.Contains('-- SCRAPLAB DEVELOPER COMMANDS NOCLIP v4')) 'Installer damaged the existing developer-command patch.'
    $markerMatches = [regex]::Matches($installedText, [regex]::Escape('-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 1 PROBE')).Count
    Assert-True ($markerMatches -eq 1) 'Installer duplicated the marker.'

    $secondInstallJson = & $managerPath -Action Install -GameRoot $gameRoot `
        -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame
    $secondInstall = $secondInstallJson | ConvertFrom-Json
    Assert-True ($secondInstall.State -eq 'INSTALLED') 'Idempotent install failed.'
    Assert-True ([Linq.Enumerable]::SequenceEqual($installedBytes, [IO.File]::ReadAllBytes($survivalGame))) 'Idempotent install rewrote SurvivalGame.lua.'

    [IO.Directory]::CreateDirectory((Split-Path -Parent $cachePath)) | Out-Null
    [IO.File]::WriteAllBytes($cachePath, [byte[]](4, 5, 6))
    $removeJson = & $managerPath -Action Remove -GameRoot $gameRoot `
        -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame
    $remove = $removeJson | ConvertFrom-Json
    Assert-True ($remove.State -eq 'NOT_INSTALLED') 'Fixture removal did not report NOT_INSTALLED.'
    Assert-True (-not (Test-Path -LiteralPath $ownedProbe)) 'Fixture removal left the owned probe behind.'
    Assert-True (-not (Test-Path -LiteralPath $cachePath)) 'Fixture removal did not invalidate core_data.cbo.'
    Assert-True ([Linq.Enumerable]::SequenceEqual($baseline, [IO.File]::ReadAllBytes($survivalGame))) 'Fixture removal was not byte-exact.'

    [IO.File]::AppendAllText($survivalGame, "-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 1 PROBE`r`n", [Text.UTF8Encoding]::new($false))
    $conflictJson = & $managerPath -Action Status -GameRoot $gameRoot `
        -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame
    $conflict = $conflictJson | ConvertFrom-Json
    Assert-True ($conflict.State -eq 'PARTIAL_OR_CONFLICT') 'Partial marker was not blocked.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}

Write-Host 'Wireless Vacuum Pipe Phase 1 regression passed.'
