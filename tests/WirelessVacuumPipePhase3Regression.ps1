$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$manager = Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
$wrapper = Join-Path $root 'source\Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'
$harness = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipePhase3Harness.lua'
$installer = Join-Path $root 'tools\experiments\Manage-WirelessVacuumPipePhase3.ps1'
$autoValidation = Join-Path $root 'tests\Run-WirelessVacuumPipeAutoValidation.ps1'
$gameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'

$consumers = [ordered]@{
    'Survival\Scripts\game\interactables\Crafter.lua' = [ordered]@{ getInputContainers = 3; getOutputContainers = 2; getContainerShapeToCollectTo = 1; getContainerPath = 2 }
    'Survival\Scripts\game\interactables\FlatVacuum.lua' = [ordered]@{ getInputContainers = 4; getOutputContainers = 1; getContainerShapeToCollectTo = 2; getContainerPath = 2 }
    'Survival\Scripts\game\interactables\GarageChest.lua' = [ordered]@{ getInputContainers = 2 }
    'Survival\Scripts\game\interactables\OreCrusher.lua' = [ordered]@{ getContainerShapeToCollectTo = 2; getContainerPath = 1 }
    'Survival\Scripts\game\interactables\Prospector.lua' = [ordered]@{ getInputContainers = 1; getOutputContainers = 1; getMatchingPipedContainers = 1; getContainerPath = 2 }
    'Survival\Scripts\game\interactables\Refinery.lua' = [ordered]@{ getContainerShapeToCollectTo = 2; getContainerPath = 1 }
    'Survival\Scripts\game\interactables\Vacuum.lua' = [ordered]@{ getInputContainers = 8; getOutputContainers = 1; getContainerShapeToCollectTo = 11; getContainerShapeToSpendFrom = 2; getContainerPath = 2 }
    'Survival\Scripts\util.lua' = [ordered]@{ getMatchingPipedContainers = 2 }
    'Survival\Scripts\game\util\pipes.lua' = [ordered]@{}
}

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) { Assert-True ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) $Message }
function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Get-Count([string]$Text, [string]$Needle) {
    $count = 0; $offset = 0
    while (($offset = $Text.IndexOf($Needle, $offset, [StringComparison]::Ordinal)) -ge 0) { $count++; $offset += $Needle.Length }
    $count
}
function Get-WrapperCall([string]$Method) { if ($Method -eq 'getContainerPath') { 'ScrapLabPipeGraph.getVisualRoute' } else { "ScrapLabPipeGraph.$Method" } }
function Copy-CleanGameFile([string]$Relative, [string]$Target, [object]$DevelopmentReceipt) {
    $source = Join-Path $gameRoot $Relative
    if ($DevelopmentReceipt) {
        $receiptFile = $DevelopmentReceipt.Files | Where-Object { $_.Relative -eq $Relative -and $_.Kind -ne 'Owned' } | Select-Object -First 1
        if ($receiptFile -and (Test-Path -LiteralPath $receiptFile.BackupPath)) { $source = $receiptFile.BackupPath }
    }
    elseif ($script:productionReceipt) {
        $receiptFile = $script:productionReceipt.Files | Where-Object { $_.RelativePath -eq $Relative } | Select-Object -First 1
        if ($receiptFile -and $receiptFile.BackupPath -and (Test-Path -LiteralPath $receiptFile.BackupPath)) { $source = $receiptFile.BackupPath }
    }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Target)) | Out-Null
    [IO.File]::Copy($source, $Target)
}

$managerText = Get-Content -LiteralPath $manager -Raw
$wrapperText = Get-Content -LiteralPath $wrapper -Raw
$harnessText = Get-Content -LiteralPath $harness -Raw
$installerText = Get-Content -LiteralPath $installer -Raw
$autoValidationText = Get-Content -LiteralPath $autoValidation -Raw

foreach ($needle in @('self.sv.topologyRevision = 1', 'sv_bumpTopologyRevision', 'sv_getEndpointIdForShape', 'sv_getLinkPeerEntries', 'record.mode ~= "LINK"', 'handleState.ready', 'Sv_GetTopologyRevision')) {
    Assert-Contains $managerText $needle "Manager Phase 3 contract is missing: $needle"
}
foreach ($needle in @(
    'SCRAPLAB WIRELESS PIPE GRAPH v10', 'ScrapLabPipeGraph.DEFINITION_VERSION = 10', 'CACHE_INTERVAL_TICKS = 10', 'getNativeShapeList', 'discoverRemoteEndpoints',
	'discoverDirectionalSourceEntries', 'Sv_GetDirectionalSourceEntries', 'getDirectionalSourceContainerShapes',
    'MAX_PHYSICAL_SHAPES = 4096', 'MAX_WIRELESS_ENDPOINTS = 256', 'appendUniqueShapes', 'buildPhysicalComponent', 'componentStillValid', 'virtualQueryHits',
    'PIPE_OPENING_DIRECTIONS', 'directionalNeighbours', 'directional pipe opening catalog mismatch',
    'discoverRemoteEndpoints( startShape, requestedDirection, tracker )', 'discoverOriginEndpoints', 'discoverLinkedRoots', 'debugGetLinkedContainerShapes',
    'requestedDirection == "output" and openingDirections( startShape )', 'getStartComponents( startShape, "input", tracker )',
    'selectionKind == "collect" and "output" or "input"',
    'validateCollectRequest', 'slotsRequired <= emptySlots', 'validateSpendRequest',
    'if sm.interactable == nil or sm.interactable.connectionType == nil then return {} end',
    'ScrapLabPipeGraph.getInputContainers', 'ScrapLabPipeGraph.getOutputContainers',
    'ScrapLabPipeGraph.getMatchingPipedContainers', 'ScrapLabPipeGraph.getContainerShapeToCollectTo',
    'ScrapLabPipeGraph.getContainerShapeToSpendFrom', 'ScrapLabPipeGraph.getVisualRoute',
    'ScrapLabPipeGraph.getGuiInputContainers',
    'Sv_HasVirtualRoute', 'return ok and extended or localResults'
)) { Assert-Contains $wrapperText $needle "Wrapper contract is missing: $needle" }

Assert-True ($wrapperText.IndexOf('local function sortedNeighbours', [StringComparison]::Ordinal) -lt 0) 'Definition 7 must not sort every physical adjacency list.'
Assert-True (-not($wrapperText -match 'setmetatable\s*\(')) 'The graph calls setmetatable, which Scrap Mechanic does not expose.'
Assert-True ($wrapperText.IndexOf('oppositeDirection = requestedDirection == "input"',[StringComparison]::Ordinal)-lt0) 'Input discovery is still blocked from a linked output-side chest system.'

Assert-True (($wrapperText | Select-String -Pattern 'sm\.container\.(spend|collect|beginTransaction|endTransaction|abortTransaction)' -AllMatches).Matches.Count -eq 0) 'Phase 4 inventory mutation leaked into the Phase 3 wrapper.'
Assert-True ($wrapperText.IndexOf('g_wirelessPipeManager =', [StringComparison]::Ordinal) -lt 0) 'The graph wrapper must not mutate manager ownership.'

foreach ($needle in @('/slpipe3', 'action == "auto"', 'phase3FixtureBlueprint', 'sm.creation.importFromString', 'Automatic disposable test station created', 'fixtureCleanup', 'sv_slpipe3BeginFixtureRecovery', 'interrupted disposable fixture cleanup completed', 'phase3FixtureDestroyBodies', 'manager-topology-contract', 'remote-link-discovery', 'cross-world-link-discovery', 'native-results-first', 'deterministic-order', 'cycle-and-duplicate-guards', 'remote-container-discovery', 'multi-link-container-union', 'resource-container-union', 'getMatchingPipedContainers', 'no connected Water', 'cross-world-visual-safety', 'exact-native-fallback')) {
    Assert-Contains $harnessText $needle "Phase 3 harness check is missing: $needle"
}
foreach ($needle in @('DefinitionVersion = 10', 'Use Update for a verified Phase 3 definition migration', 'New-ConsumerOutput', 'Test-ConsumerInstalled', 'Get-CrafterBridgeBlockV4', 'Move-CrafterBridgeBeforeSubclasses', 'sv_n_requestScrapLabGuiContainers', 'cl_n_setScrapLabGuiContainers', '[ScrapLab Pipe Crafter] server GUI sync', 'SCRAPLAB WIRELESS PIPE VISUAL ROUTE GUARD', 'Write-AtomicBytes', 'Backup verification failed', 'PARTIAL_OR_CONFLICT', 'core_data.cbo')) {
    Assert-Contains $installerText $needle "Phase 3 installer safety contract is missing: $needle"
}
foreach ($needle in @('WirelessVacuumPipePhase1Regression.ps1', 'WirelessVacuumPipePhase2Regression.ps1', 'WirelessVacuumPipePhase3Regression.ps1', 'Flat Vacuum availability', 'run /slpipe3 auto after installing definition 10', '$passed -ge 10', 'Wireless graph safety log scan')) {
    Assert-Contains $autoValidationText $needle "Automatic validation coordinator is missing: $needle"
}
foreach ($relative in $consumers.Keys) { Assert-Contains $installerText $relative "Installer consumer definition is missing: $relative" }

if (Test-Path -LiteralPath $gameRoot) {
    $directionCatalog = @{}
    $shapeSetRoot = Join-Path $gameRoot 'Survival\Objects\Database\ShapeSets'
    Get-ChildItem -LiteralPath $shapeSetRoot -Recurse -File -Filter '*.shapeset' | ForEach-Object {
        try {
            $shapeSet = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
            foreach ($part in @($shapeSet.partList)) {
                if (-not $part.pipe -or -not $part.pipe.openings) { continue }
                $directions = @($part.pipe.openings | ForEach-Object { [string]$_.direction })
                if ($directions -notcontains 'input' -and $directions -notcontains 'output') { continue }
                $uuid = ([string]$part.uuid).ToLowerInvariant(); $signature = $directions -join ','
                if ($directionCatalog.ContainsKey($uuid)) { Assert-True ($directionCatalog[$uuid] -eq $signature) "Official duplicate pipe direction catalog disagrees for $uuid." }
                else { $directionCatalog[$uuid] = $signature }
            }
        } catch { throw "Could not validate official pipe directions in $($_.FullName): $($_.Exception.Message)" }
    }
    foreach ($entry in $directionCatalog.GetEnumerator()) {
        $luaDirections = (@($entry.Value.Split(',')) | ForEach-Object { '"' + $_ + '"' }) -join ', '
        Assert-Contains $wrapperText ('["' + $entry.Key + '"] = { ' + $luaDirections + ' }') "Wrapper direction catalog is missing or reordered for $($entry.Key)."
    }

    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('ScrapLabPipePhase3Regression-' + [Guid]::NewGuid().ToString('N'))
    $fakeGame = Join-Path $temporaryRoot 'Game'; $receiptRoot = Join-Path $temporaryRoot 'Receipt'; $backupRoot = Join-Path $temporaryRoot 'Backups'
    try {
        $developmentReceiptPath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Development State\WirelessVacuumPipePhase3.json'
        $developmentReceipt = if (Test-Path -LiteralPath $developmentReceiptPath) { Get-Content -LiteralPath $developmentReceiptPath -Raw | ConvertFrom-Json } else { $null }
        $productionReceiptPath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active\WirelessVacuumPipe.json'
        $script:productionReceipt = if (Test-Path -LiteralPath $productionReceiptPath) { Get-Content -LiteralPath $productionReceiptPath -Raw | ConvertFrom-Json } else { $null }
        foreach ($relative in @($consumers.Keys) + @('Survival\Scripts\game\SurvivalGame.lua')) {
            Copy-CleanGameFile $relative (Join-Path $fakeGame $relative) $developmentReceipt
        }
        $managerTarget = Join-Path $fakeGame 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
        [IO.Directory]::CreateDirectory((Split-Path -Parent $managerTarget)) | Out-Null
        [IO.File]::Copy($manager, $managerTarget)

        $originalHashes = @{}
        Get-ChildItem -LiteralPath $fakeGame -Recurse -File | ForEach-Object { $originalHashes[$_.FullName.Substring($fakeGame.Length)] = Get-Sha $_.FullName }

        $install = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Install -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame) | ConvertFrom-Json
        Assert-True ($install.State -eq 'INSTALLED') "Fixture install did not reach INSTALLED: $($install.State)"
        Assert-True ($install.DefinitionVersion -eq 10) 'Fixture did not report Phase 3 definition 10.'
        foreach ($relative in $consumers.Keys) {
            $text = Get-Content -LiteralPath (Join-Path $fakeGame $relative) -Raw
            Assert-True ((Get-Count $text '-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 3 LINK GRAPH') -eq 1) "Consumer loader missing: $relative"
            foreach ($entry in $consumers[$relative].GetEnumerator()) {
                Assert-True ((Get-Count $text (Get-WrapperCall $entry.Key)) -eq [int]$entry.Value) "Wrong wrapper count for $relative $($entry.Key)."
                Assert-True ((Get-Count $text "sm.pipeGraph.$($entry.Key)") -eq 0) "Native protected call remained in $relative $($entry.Key)."
            }
        }
        $installedCrafter = Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\game\interactables\Crafter.lua') -Raw
        Assert-True ((Get-Count $installedCrafter '-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE') -eq 1) 'Crafter GUI bridge marker is missing.'
        Assert-True ((Get-Count $installedCrafter 'self.network:sendToServer( "sv_n_requestScrapLabGuiContainers" )') -eq 1) 'Crafter GUI bridge request is missing.'
        Assert-True ($installedCrafter.IndexOf('function Crafter.sv_n_requestScrapLabGuiContainers', [StringComparison]::Ordinal) -lt $installedCrafter.IndexOf('Craftbot = class( Crafter )', [StringComparison]::Ordinal)) 'Crafter network callbacks must exist before Craftbot subclasses are created.'
        Assert-True ($installedCrafter.IndexOf('`tprint(', [StringComparison]::Ordinal) -lt 0) 'Generated Crafter Lua contains a literal PowerShell tab escape.'

        # Recreate definition 4's invalid literal `t diagnostics and prove the
        # updater repairs the Lua syntax before any game-side validation.
        $receiptPath = Join-Path $receiptRoot 'WirelessVacuumPipePhase3.json'
        $crafterPath = Join-Path $fakeGame 'Survival\Scripts\game\interactables\Crafter.lua'
        $definition4Receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $crafterText = Get-Content -LiteralPath $crafterPath -Raw
        $definition5Bridge = [regex]::Match($crafterText, '(?s)-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE\r?\n.*?-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE').Value
        $definition4Bridge = $definition5Bridge.Replace("`tprint(", '`tprint(')
        $brokenCrafterText = $crafterText.Replace($definition5Bridge, $definition4Bridge)
        $brokenEscapeCount = Get-Count $brokenCrafterText '`tprint('
        Assert-True ($brokenEscapeCount -eq 2) "Definition 4 fixture did not recreate both invalid diagnostic lines (count=$brokenEscapeCount)."
        [IO.File]::WriteAllText($crafterPath, $brokenCrafterText, [Text.UTF8Encoding]::new($false))
        ($definition4Receipt.Files | Where-Object { $_.Kind -eq 'Crafter' }).InstalledHash = Get-Sha $crafterPath
        $definition4Receipt.DefinitionVersion = 4
        $definition4Receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8
        $definition5Repair = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Update -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame) | ConvertFrom-Json
        Assert-True ($definition5Repair.State -eq 'INSTALLED' -and $definition5Repair.DefinitionVersion -eq 10) 'Definition 4 Crafter syntax repair failed.'

        # Recreate definition 3's misplaced end-of-file bridge and prove the
        # updater moves it ahead of subclass creation, where callbacks inherit.
        $definition3Receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $crafterText = Get-Content -LiteralPath $crafterPath -Raw
        $currentBridge = [regex]::Match($crafterText, '(?s)-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE\r?\n.*?-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE').Value
        Assert-True (-not [string]::IsNullOrEmpty($currentBridge)) 'Could not locate the definition 4 Crafter bridge.'
        $nl = if ($crafterText.Contains("`r`n")) { "`r`n" } else { "`n" }
        $legacyBridge = @(
            '-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE',
            'function Crafter.sv_n_requestScrapLabGuiContainers( self, _, player )',
            "`tlocal containers = {}",
            "`tfor _, shape in ipairs( ScrapLabPipeGraph.getGuiInputContainers( self.shape ) ) do",
            "`t`tlocal ok, container = pcall( function() return GetPipeGraphObjectContainer( shape ) end )",
            "`t`tif ok and container then containers[#containers + 1] = container end",
            "`tend",
            ("`t" + 'self.network:sendToClient( player, "cl_n_setScrapLabGuiContainers", containers )'),
            'end', '',
            'function Crafter.cl_n_setScrapLabGuiContainers( self, containers )',
            "`tif self.cl.guiInterface == nil then return end",
            "`tlocal guiContainers = {}",
            "`tfor _, container in ipairs( containers or {} ) do",
            "`t`tif container then guiContainers[#guiContainers + 1] = container end",
            "`tend",
            "`tguiContainers[#guiContainers + 1] = sm.localPlayer.getPlayer():getInventory()",
            ("`t" + 'self.cl.guiInterface:setContainers( "", guiContainers )'),
            'end',
            '-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE'
        ) -join $nl
        $crafterText = $crafterText.Replace($currentBridge + $nl + $nl, '')
        if (-not $crafterText.EndsWith($nl, [StringComparison]::Ordinal)) { $crafterText += $nl }
        $crafterText += $legacyBridge + $nl
        [IO.File]::WriteAllText($crafterPath, $crafterText, [Text.UTF8Encoding]::new($false))
        Assert-True ((Get-Count $crafterText $legacyBridge) -eq 1) 'Definition 3 fixture bridge does not match the protected legacy block.'
        Assert-True ($crafterText.IndexOf($legacyBridge, [StringComparison]::Ordinal) -gt $crafterText.IndexOf('Workbench = class( Crafter )', [StringComparison]::Ordinal)) 'Definition 3 fixture bridge was not placed after subclass creation.'
        foreach ($entry in $consumers['Survival\Scripts\game\interactables\Crafter.lua'].GetEnumerator()) {
            Assert-True ((Get-Count $crafterText (Get-WrapperCall $entry.Key)) -eq [int]$entry.Value) "Definition 3 fixture lost protected wrapper call $($entry.Key)."
        }
        ($definition3Receipt.Files | Where-Object { $_.Kind -eq 'Crafter' }).InstalledHash = Get-Sha $crafterPath
        $definition3Receipt.DefinitionVersion = 3
        $definition3Receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8
        $definition5From3 = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Update -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame) | ConvertFrom-Json
        Assert-True ($definition5From3.State -eq 'INSTALLED' -and $definition5From3.DefinitionVersion -eq 10) 'Definition 3 Crafter callback-order migration failed.'

        # Downgrade only the Crafter bridge to definition 2 and prove that the
        # receipt-aware updater adds the GUI sync without replacing its clean backup.
        $definition2Receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $crafterText = Get-Content -LiteralPath $crafterPath -Raw
        $crafterText = $crafterText.Replace("`t`t" + 'self.network:sendToServer( "sv_n_requestScrapLabGuiContainers" )' + "`r`n", '')
        $crafterText = $crafterText.Replace("`t`t" + 'self.network:sendToServer( "sv_n_requestScrapLabGuiContainers" )' + "`n", '')
        $crafterText = [regex]::Replace($crafterText, '(?s)-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE\r?\n.*?-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE\r?\n?', '')
        [IO.File]::WriteAllText($crafterPath, $crafterText, [Text.UTF8Encoding]::new($false))
        ($definition2Receipt.Files | Where-Object { $_.Kind -eq 'Crafter' }).InstalledHash = Get-Sha $crafterPath
        $definition2Receipt.DefinitionVersion = 2
        $definition2Receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8
        $definition5From2 = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Update -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame) | ConvertFrom-Json
        Assert-True ($definition5From2.State -eq 'INSTALLED' -and $definition5From2.DefinitionVersion -eq 10) 'Definition 2 Crafter bridge migration failed.'

        # Convert the full fixture into a truthful legacy Garage Chest-only receipt,
        # then prove Update migrates it atomically without replacing original backups.
        $legacyReceipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $legacyKinds = @('GarageChest', 'Game', 'Owned')
        foreach ($file in @($legacyReceipt.Files | Where-Object { $_.Kind -notin $legacyKinds })) {
            [IO.File]::Copy($file.BackupPath, (Join-Path $fakeGame $file.Relative), $true)
        }
        $legacyReceipt.Files = @($legacyReceipt.Files | Where-Object { $_.Kind -in $legacyKinds })
        $legacyReceipt.SchemaVersion = 1
        $legacyReceipt.PSObject.Properties.Remove('DefinitionVersion')
        $legacyReceipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receiptPath -Encoding utf8

        $migration = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Update -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame) | ConvertFrom-Json
        Assert-True ($migration.State -eq 'INSTALLED') "Legacy migration did not reach INSTALLED: $($migration.State)"
        $migratedReceipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        Assert-True ($migratedReceipt.DefinitionVersion -eq 10) 'Legacy migration receipt was not promoted to definition 10.'
        Assert-True ($migratedReceipt.Files.Count -eq ($consumers.Count + 3)) 'Legacy migration receipt does not cover every consumer, harness, and owned file.'

        $remove = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Remove -GameRoot $fakeGame -ReceiptRoot $receiptRoot -BackupRoot $backupRoot -AllowRunningGame) | ConvertFrom-Json
        Assert-True ($remove.State -eq 'NOT_INSTALLED') "Fixture removal did not reach NOT_INSTALLED: $($remove.State)"
        foreach ($entry in $originalHashes.GetEnumerator()) {
            $path = $fakeGame + $entry.Key
            Assert-True ((Get-Sha $path) -eq $entry.Value) "Fixture removal was not byte-exact: $($entry.Key)"
        }
    } finally {
        if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
    }
}

'Wireless Vacuum Pipe Phase 3 full-consumer regression checks passed.'
