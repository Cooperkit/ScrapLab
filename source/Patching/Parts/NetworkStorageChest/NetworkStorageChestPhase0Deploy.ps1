[CmdletBinding()]
param(
    [ValidateSet('Install', 'Uninstall')]
    [string]$Action = 'Install',
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$partUuid = 'bc7576a7-f226-459a-883c-e8460e955d63'
$shapeSetPath = '$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/NetworkStorageChest.shapeset'
$loaderStart = '-- SCRAPLAB NETWORK STORAGE CHEST PHASE 0 PROBE'
$loaderEnd = '-- END SCRAPLAB NETWORK STORAGE CHEST PHASE 0 PROBE'
$partSource = $PSScriptRoot
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..\..')).Path
$stateRoot = Join-Path $repositoryRoot 'dist\phase0-backups\NetworkStorageChest'
$activeReceiptPath = Join-Path $stateRoot 'active.json'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-TextState([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    return [pscustomobject]@{ Text = $text; HasBom = $hasBom; Newline = $newline }
}

function Write-AtomicText([string]$Path, [string]$Text, [bool]$HasBom) {
    $tempPath = $Path + '.scraplab-phase0-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swapBackup = $Path + '.scraplab-phase0-' + [Guid]::NewGuid().ToString('N') + '.swap'
    $encoding = New-Object System.Text.UTF8Encoding($HasBom)
    try {
        [System.IO.File]::WriteAllText($tempPath, $Text, $encoding)
        [System.IO.File]::Replace($tempPath, $Path, $swapBackup)
    }
    finally {
        if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force }
        if (Test-Path -LiteralPath $swapBackup) { Remove-Item -LiteralPath $swapBackup -Force }
    }
}

function Copy-Atomic([string]$Source, [string]$Destination) {
    $directory = Split-Path -Parent $Destination
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $tempPath = $Destination + '.scraplab-phase0-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swapBackup = $Destination + '.scraplab-phase0-' + [Guid]::NewGuid().ToString('N') + '.swap'
    try {
        [System.IO.File]::Copy($Source, $tempPath, $true)
        if (Test-Path -LiteralPath $Destination) {
            [System.IO.File]::Replace($tempPath, $Destination, $swapBackup)
        }
        else {
            [System.IO.File]::Move($tempPath, $Destination)
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force }
        if (Test-Path -LiteralPath $swapBackup) { Remove-Item -LiteralPath $swapBackup -Force }
    }
}

function Assert-GameClosed {
    if (Get-Process -Name 'ScrapMechanic' -ErrorAction SilentlyContinue) {
        throw 'Scrap Mechanic is running. Close it completely before changing the Phase 0 probe.'
    }
}

function Remove-CoreDataCache {
    $cachePath = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
    if (Test-Path -LiteralPath $cachePath) {
        Remove-Item -LiteralPath $cachePath -Force
        Write-Host 'Removed core_data.cbo so the probe loads on the next normal launch.'
    }
}

function Patch-ShapesIndex([string]$Text, [string]$Newline) {
    if ($Text.Contains($shapeSetPath)) { throw 'The Network Storage Chest shape-set registration already exists.' }
    $preferred = "`t`t`"`$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/WirelessVacuumPipe.shapeset`","
    $fallback = "`t`t`"`$SURVIVAL_DATA/Objects/Database/ShapeSets/interactive_shared.shapeset`","
    $anchor = if ($Text.Contains($preferred)) { $preferred } elseif ($Text.Contains($fallback)) { $fallback } else { $null }
    if (-not $anchor) { throw 'Could not find a protected shape-set insertion point.' }
    return $Text.Replace($anchor, $anchor + $Newline + "`t`t`"$shapeSetPath`",")
}

function Patch-SurvivalItems([string]$Text, [string]$Newline) {
    if ($Text.Contains($partUuid)) { throw 'The Network Storage Chest item declaration already exists.' }
    $anchor = "`tobj_container_smallchest_pipe = sm.uuid.new( `"4c474cff-3f6a-4306-93d1-c4c74578afd2`" ),"
    if (-not $Text.Contains($anchor)) { throw 'Could not find the piped Small Chest item declaration.' }
    $addition = "`tobj_container_network_storage_chest = sm.uuid.new( `"$partUuid`" ),"
    return $Text.Replace($anchor, $anchor + $Newline + $addition)
}

function Patch-SurvivalGame([string]$Text, [string]$Newline) {
    if ($Text.Contains($loaderStart) -or $Text.Contains('NetworkStorageChestPhase0Harness.lua')) {
        throw 'The Network Storage Chest Phase 0 loader already exists.'
    }
    $block = $loaderStart + $Newline +
        'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase0Harness.lua" )' + $Newline +
        'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase1Harness.lua" )' + $Newline +
        'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase1QualificationHarness.lua" )' + $Newline +
        'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase2Harness.lua" )' + $Newline +
        'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase3Harness.lua" )' + $Newline +
        'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase4Harness.lua" )' + $Newline +
        $loaderEnd
    return $Text.TrimEnd("`r", "`n") + $Newline + $Newline + $block + $Newline
}

function Patch-EnglishLanguage([string]$Text, [string]$Newline) {
    if ($Text.Contains($partUuid)) { throw 'The Network Storage Chest English inventory description already exists.' }
    $opening = '{' + $Newline
    $openingIndex = $Text.IndexOf($opening, [StringComparison]::Ordinal)
    if ($openingIndex -lt 0) { throw 'Could not find the English inventory-description root object.' }
    $entry = "`t`"$partUuid`": {" + $Newline +
        "`t`t`"description`": `"Browse a connected storage network, take items from one catalog, and automatically sort deposits into matching containers. Supports Wireless Vacuum Pipe routes when installed.`"," + $Newline +
        "`t`t`"title`": `"Network Storage Chest`"," + $Newline +
        "`t`t`"upperCaseTitle`": `"NETWORK STORAGE CHEST`"" + $Newline +
        "`t}," + $Newline
    # Insert once at the root. String.Replace would also match every nested
    # item object and duplicate the custom UUID throughout the entire file.
    $insertAt = $openingIndex + $opening.Length
    return $Text.Substring(0, $insertAt) + $entry + $Text.Substring($insertAt)
}

function New-TargetPlan([string]$RelativePath, [scriptblock]$PatchFunction, [string]$BackupRoot) {
    $path = Join-Path $GamePath $RelativePath
    if (-not (Test-Path -LiteralPath $path)) { throw "Required game file is missing: $RelativePath" }
    $state = Get-TextState $path
    $patched = & $PatchFunction $state.Text $state.Newline
    $backupPath = Join-Path $BackupRoot $RelativePath
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $backupPath)) | Out-Null
    [System.IO.File]::Copy($path, $backupPath, $true)
    return [pscustomobject]@{
        RelativePath = $RelativePath
        Path = $path
        BackupPath = $backupPath
        SourceHash = Get-Sha256 $path
        Text = $patched
        HasBom = $state.HasBom
        OutputHash = $null
    }
}

function Install-Probe {
    if (Test-Path -LiteralPath $activeReceiptPath) {
        throw 'A Phase 0 receipt already exists. Uninstall the previous probe before installing again.'
    }
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
    $backupRoot = Join-Path $stateRoot $stamp
    [System.IO.Directory]::CreateDirectory($backupRoot) | Out-Null
    $targets = @()
    $owned = @()
    try {
        $targets += New-TargetPlan 'Survival\Objects\Database\shapesets.json' ${function:Patch-ShapesIndex} $backupRoot
        $targets += New-TargetPlan 'Survival\Scripts\game\survival_items.lua' ${function:Patch-SurvivalItems} $backupRoot
        $targets += New-TargetPlan 'Survival\Scripts\game\SurvivalGame.lua' ${function:Patch-SurvivalGame} $backupRoot
        $targets += New-TargetPlan 'Survival\Gui\Language\English\inventoryDescriptions.json' ${function:Patch-EnglishLanguage} $backupRoot

        $ownedMap = @(
            @{ Source = 'NetworkStorageChest.lua'; Target = 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua' },
            @{ SourcePath = (Join-Path $repositoryRoot 'source\Patching\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua'); Source = 'NetworkInventoryIndex.lua'; Target = 'Survival\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua' },
            @{ Source = 'NetworkStorageChest.gui'; Target = 'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui' },
            @{ Source = 'NetworkStorageChestItem.gui'; Target = 'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChestItem.gui' },
            @{ Source = 'NetworkStorageChest.shapeset'; Target = 'Survival\Objects\Database\ShapeSets\ScrapLab\Parts\NetworkStorageChest.shapeset' },
            @{ Source = 'NetworkStorageChestPhase0Harness.lua'; Target = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase0Harness.lua' },
            @{ Source = 'NetworkStorageChestPhase1Harness.lua'; Target = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase1Harness.lua' }
            @{ Source = 'NetworkStorageChestPhase1QualificationHarness.lua'; Target = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase1QualificationHarness.lua' }
            @{ Source = 'NetworkStorageChestPhase2Harness.lua'; Target = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase2Harness.lua' }
            @{ Source = 'NetworkStorageChestPhase3Harness.lua'; Target = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase3Harness.lua' }
            @{ Source = 'NetworkStorageChestPhase4Harness.lua'; Target = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase4Harness.lua' }
        )
        foreach ($mapping in $ownedMap) {
            $source = if ($mapping.SourcePath) { $mapping.SourcePath } else { Join-Path $partSource $mapping.Source }
            $target = Join-Path $GamePath $mapping.Target
            if (-not (Test-Path -LiteralPath $source)) { throw "Phase 0 source asset is missing: $($mapping.Source)" }
            if (Test-Path -LiteralPath $target) { throw "Phase 0 owned target already exists: $($mapping.Target)" }
            $owned += [pscustomobject]@{ Source = $source; Path = $target; RelativePath = $mapping.Target; Hash = Get-Sha256 $source }
        }

        foreach ($target in $targets) {
            Write-AtomicText $target.Path $target.Text $target.HasBom
            $target.OutputHash = Get-Sha256 $target.Path
        }
        foreach ($asset in $owned) {
            Copy-Atomic $asset.Source $asset.Path
            if ((Get-Sha256 $asset.Path) -ne $asset.Hash) { throw "Owned asset verification failed: $($asset.RelativePath)" }
        }

        foreach ($target in $targets) {
            if ((Get-Sha256 $target.Path) -ne $target.OutputHash) { throw "Final verification failed: $($target.RelativePath)" }
        }
        $receipt = [ordered]@{
            SchemaVersion = 1
            InstalledUtc = [DateTime]::UtcNow.ToString('o')
            GamePath = $GamePath
            BackupRoot = $backupRoot
            Targets = @($targets | ForEach-Object { [ordered]@{ RelativePath = $_.RelativePath; BackupPath = $_.BackupPath; SourceHash = $_.SourceHash; OutputHash = $_.OutputHash } })
            Owned = @($owned | ForEach-Object { [ordered]@{ RelativePath = $_.RelativePath; Hash = $_.Hash } })
        }
        [System.IO.Directory]::CreateDirectory($stateRoot) | Out-Null
        [System.IO.File]::WriteAllText($activeReceiptPath, ($receipt | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
        Remove-CoreDataCache
        Write-Host 'Network Storage Chest Phase 0 probe installed and verified.'
    }
    catch {
        foreach ($target in $targets) {
            if (Test-Path -LiteralPath $target.BackupPath) { [System.IO.File]::Copy($target.BackupPath, $target.Path, $true) }
        }
        foreach ($asset in $owned) {
            if (Test-Path -LiteralPath $asset.Path) { Remove-Item -LiteralPath $asset.Path -Force }
        }
        throw
    }
}

function Uninstall-Probe {
    if (-not (Test-Path -LiteralPath $activeReceiptPath)) { throw 'No active Network Storage Chest Phase 0 receipt was found.' }
    $receipt = Get-Content -LiteralPath $activeReceiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not [string]::Equals($receipt.GamePath, $GamePath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The active Phase 0 receipt belongs to a different game installation.'
    }
    foreach ($target in $receipt.Targets) {
        $path = Join-Path $GamePath $target.RelativePath
        if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $target.OutputHash) {
            throw "Removal blocked because a patched target changed after installation: $($target.RelativePath)"
        }
    }
    foreach ($asset in $receipt.Owned) {
        $path = Join-Path $GamePath $asset.RelativePath
        if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $asset.Hash) {
            throw "Removal blocked because a Phase 0 owned asset changed: $($asset.RelativePath)"
        }
    }
    foreach ($target in $receipt.Targets) {
        $path = Join-Path $GamePath $target.RelativePath
        if (-not (Test-Path -LiteralPath $target.BackupPath)) { throw "Backup is missing: $($target.BackupPath)" }
        [System.IO.File]::Copy($target.BackupPath, $path, $true)
        if ((Get-Sha256 $path) -ne $target.SourceHash) { throw "Backup restoration failed: $($target.RelativePath)" }
    }
    foreach ($asset in $receipt.Owned) {
        $path = Join-Path $GamePath $asset.RelativePath
        Remove-Item -LiteralPath $path -Force
    }
    Remove-Item -LiteralPath $activeReceiptPath -Force
    Remove-CoreDataCache
    Write-Host 'Network Storage Chest Phase 0 probe removed; the pre-probe game files were restored.'
}

if (-not (Test-Path -LiteralPath $GamePath)) { throw "Scrap Mechanic was not found at: $GamePath" }
Assert-GameClosed
if ($Action -eq 'Install') { Install-Probe } else { Uninstall-Probe }
