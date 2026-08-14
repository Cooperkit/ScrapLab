param(
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe')
)
$ErrorActionPreference='Stop'
function Assert-True([bool]$Condition,[string]$Message){if(-not$Condition){throw "ASSERTION FAILED: $Message"}}
function Assert-Contains([string]$Text,[string]$Needle,[string]$Message){Assert-True $Text.Contains($Needle) $Message}

$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$part=Join-Path $root 'source\Patching\Parts\NetworkStorageChest'
$phase7=Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChestPhase7Harness.lua') -Raw -Encoding UTF8
$deployer=Get-Content -LiteralPath (Join-Path $root 'tools\Deploy-NetworkStoragePhase7.ps1') -Raw -Encoding UTF8
$plan=Get-Content -LiteralPath (Join-Path $root 'docs\NETWORK-STORAGE-CHEST-MOD-PLAN.md') -Raw -Encoding UTF8
$build=Get-Content -LiteralPath (Join-Path $root 'build.ps1') -Raw -Encoding UTF8

foreach($needle in @(
    '/slstorage','auto','local','wireless','all','soak','string.upper( runtime.mode )','" SUMMARY: "','SOAK SUMMARY',
    'SL7_SPAWN_COUNT = 500','SL7_SPAWN_BUDGET = 10','SL7_DESTROY_BUDGET = 30',
    'closed-terminal-idle-cost','cold-100-container-index','warm-cache-100','single-revision-rescan',
    'incremental-500-container-index','shared-cache-overlapping-terminal',
    'three-slot-buffer-refresh-persistence','bounded-unused-index-cache',
    'scrapLabStoragePhase7Cleanup','sl7Recover','sv_beginPhase1QualificationSession',
    'scanContainerScans == 400','scanCacheHits == 500','502 temporary parts removed'
)){Assert-Contains $phase7 $needle "Phase 7 harness is missing '$needle'."}

foreach($phase in 2,3,4,5){
    $text=Get-Content -LiteralPath (Join-Path $part "NetworkStorageChestPhase$($phase)Harness.lua") -Raw -Encoding UTF8
    Assert-Contains $text 'g_scrapLabStorageQualificationResults' "Phase $phase does not publish coordinator results."
    Assert-Contains $text "phase$phase = {" "Phase $phase completion key is missing."
}
$phase2=Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChestPhase2Harness.lua') -Raw -Encoding UTF8
Assert-True (-not$phase2.Contains('runtime.instance.sv.topologyKey = table.concat')) 'Phase 2 overwrote the canonical qualification topology key.'
Assert-Contains $phase2 'sv_beginPhase1QualificationSession owns the canonical' 'Phase 2 topology-key ownership guard is missing.'

foreach($needle in @(
    "[ValidateSet('Install','Uninstall')]",'Assert-GameClosed','network-storage-chest','wireless-vacuum-pipe',
    'Write-AtomicBytes','SurvivalGame.lua changed after Phase 7 installation','SourceHash','OutputHash',
    'NetworkStorageChestPhase7Harness.lua','Remove-Cache','production Network Storage Chest state was not intact'
)){Assert-Contains $deployer $needle "Phase 7 deployer is missing '$needle'."}
Assert-Contains $deployer 'Remove-CompletedBackups' 'Completed Phase 7 backups are not bounded.'
Assert-Contains $deployer 'Refusing to remove an out-of-scope Phase 7 backup' 'Phase 7 backup cleanup lacks an absolute-path boundary guard.'
$tokens=$null;$errors=$null
[void][Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'tools\Deploy-NetworkStoragePhase7.ps1'),[ref]$tokens,[ref]$errors)
Assert-True ($errors.Count-eq0) ('Phase 7 deployer syntax failed: '+(($errors|ForEach-Object Message)-join'; '))
Assert-True (-not$build.Contains('NetworkStorageChestPhase7Harness.lua')) 'Temporary Phase 7 harness was embedded in the public executable.'
Assert-True (-not$phase7.Contains("`t`tlocal =")) 'Phase 7 used the reserved Lua keyword local as a bare table key.'
Assert-True ($plan.Contains('### Phase 7') -and $plan.Contains('automated release qualification')) 'The Phase 7 plan section is missing.'

$storage=&$PatchHelperExe --status network-storage-chest|ConvertFrom-Json
$wireless=&$PatchHelperExe --status wireless-vacuum-pipe|ConvertFrom-Json
Assert-True ($storage.Success-and$storage.Installed-and$storage.CanApply) ('Production Network Storage status is not healthy: '+$storage.CompatibilityReason)
Assert-True ($wireless.Success-and$wireless.Installed-and$wireless.CanApply) ('Wireless composition status is not healthy: '+$wireless.CompatibilityReason)

Write-Host 'Network Storage Chest Phase 7 regression passed.'
