[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$manager = Get-Content -LiteralPath (Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua') -Raw
$graph = Get-Content -LiteralPath (Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua') -Raw
$terminal = Get-Content -LiteralPath (Join-Path $root 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChest.lua') -Raw
$harness = Get-Content -LiteralPath (Join-Path $root 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChestPhase4Harness.lua') -Raw
$deployer = Get-Content -LiteralPath (Join-Path $root 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChestPhase0Deploy.ps1') -Raw
$service = Get-Content -LiteralPath (Join-Path $root 'source\Patching\WirelessVacuumPipePatchService.cs') -Raw

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    Assert-True ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) $Message
}

foreach ($needle in @(
    'WIRELESS VACUUM PIPE MANAGER v8', 'sv_getTerminalPeerEntries', 'Sv_GetTerminalPeerEntries',
    'peer.mode ~= "LINK" and peer.directOnly ~= false',
    'directionalDebugScopes', 'Sv_DebugSetEndpointScope'
)) { Assert-Contains $manager $needle "Manager terminal contract is missing: $needle" }

foreach ($needle in @(
    'SCRAPLAB WIRELESS PIPE GRAPH v10', 'ScrapLabPipeGraph.DEFINITION_VERSION = 10',
    'getTerminalSpendContainers', 'getTerminalCollectContainers', 'buildTerminalContainers',
    'wirelessState = "LOCAL_ONLY"', 'wirelessState = "LIMITED"', 'wirelessState = "OFFLINE"',
    'routePriority = route.wireless and 1 or 0', 'topologyGeneration'
)) { Assert-Contains $graph $needle "Graph terminal contract is missing: $needle" }

foreach ($needle in @(
    'sv_collectNetworkContainers', 'sv_collectTopologySnapshot',
    'getTerminalSpendContainers', 'getTerminalCollectContainers',
    'descriptorKey( spend, spendState ) .. "||" .. descriptorKey( collect, collectState )',
    'freshFailure or "wireless deposit route changed before commit"',
    'wirelessState', 'reachableWorlds',
    'a.descriptor.routePriority < b.descriptor.routePriority'
)) { Assert-Contains $terminal $needle "Terminal Phase 4 contract is missing: $needle" }
Assert-True ($terminal.Contains('local descriptors, failure = self:sv_collectLocalContainers()')) 'Optional local-only fallback is missing.'

foreach ($needle in @(
    '/slstorage4 auto', 'fixture-direct-versus-whole', 'link-spend-union', 'link-collect-union',
    'receive-sees-send-sources', 'receive-whole-network', 'send-sees-receive-destinations',
    'send-whole-network', 'cross-world-link', 'wireless-withdrawal', 'wireless-deposit',
    'withdrawal-conservation', 'deposit-conservation', 'manager-invariants',
    'sm.creation.importFromString', 'Disposable networks removed'
)) { Assert-Contains $harness $needle "Automatic Phase 4 harness is missing: $needle" }
Assert-Contains $harness 'pos = { x = 0, y = 2, z = 0 }' 'The terminal fixture endpoint is not aligned to the 3-wide terminal port.'
Assert-Contains $harness 'pos = { x = 1, y = 4, z = 0 }' 'The indirect fixture pipe is not adjacent to the one-block endpoint.'
Assert-Contains $harness 'pos = { x = 5, y = 8, z = 0 }' 'The reversed indirect container transform is incorrect.'

Assert-Contains $deployer 'NetworkStorageChestPhase4Harness.lua' 'The development deployer does not own/load the Phase 4 harness.'
Assert-Contains $deployer '$Text.Substring(0, $insertAt)' 'The English localization installer does not use a single root insertion.'
Assert-True (-not $deployer.Contains('return $Text.Replace($opening, $opening + $entry)')) 'The localization installer still duplicates entries in every nested object.'
Assert-Contains $service 'private const string DefinitionVersion = "8"' 'Wireless patch definition no longer includes the Link-scope correction and current UI update.'
Assert-Contains $service '3411D6804F6D874C4B9BD8D8C80C4109BF3CECFB0F44F31EDF49C0DF4F3D8DC8' 'Definition-6 manager migration hash is missing.'
Assert-Contains $service '2EE306FA1303FDA36CC2CE64964CCD4E567CC27EA7D82D4F47B5B6CCE31BC321' 'Definition-5 manager migration hash is missing.'
Assert-Contains $service '8C8641F1069968D0750ABCDCB0C56261616D44B11E2C1814C4664222BED2BD2A' 'Definition-5 graph migration hash is missing.'

Write-Host 'Network Storage Chest Phase 4 regression checks passed.'
