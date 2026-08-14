[CmdletBinding()]
param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$helperPath = Join-Path $kitRoot 'dist\ScrapLab.PatchHelper.exe'
$developmentScript = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChestPhase0Deploy.ps1'
$developmentReceipt = Join-Path $kitRoot 'dist\phase0-backups\NetworkStorageChest\active.json'
$migrationRoot = Join-Path $kitRoot ('dist\phase6-migration-backups\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
$activeRoot = Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active'
$secretBackupRoot = Join-Path $env:LOCALAPPDATA 'ScrapLab\Game Backups\Scrap Mechanic\Secret Mods'
$sharedState = Join-Path $activeRoot 'ScrapLab-Icon-Pack.json'
$sharedMirror = Join-Path $secretBackupRoot 'ScrapLab-Shared-Icon-Atlas\atlas-receipt.json'
$sharedBaseline = Join-Path $secretBackupRoot 'ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
$productionReceipt = Join-Path $activeRoot 'NetworkStorageChest.json'
$productionActivation = Join-Path $activeRoot 'NetworkStorageChest.activation.json'
$wirelessReceipt = Join-Path $activeRoot 'WirelessVacuumPipe.json'
$wirelessReceiptBackup = Get-ChildItem -LiteralPath (Join-Path $kitRoot 'dist\phase5-backups\NetworkStorageChest') -Recurse -File -Filter 'WirelessVacuumPipe-active.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
$iconXml = Join-Path $GamePath 'Survival\Gui\IconMapSurvival.xml'
$iconAtlas = Join-Path $GamePath 'Survival\Gui\IconMapSurvival.png'
$cachePath = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
$partUuid = 'bc7576a7-f226-459a-883c-e8460e955d63'
$snapshots = New-Object System.Collections.Generic.List[object]

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Write-AtomicBytes([string]$Path, [byte[]]$Bytes) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    $temporary = $Path + '.scraplab-phase6-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swap = $Path + '.scraplab-phase6-' + [Guid]::NewGuid().ToString('N') + '.swap'
    try {
        [IO.File]::WriteAllBytes($temporary, $Bytes)
        if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($temporary, $Path, $swap) }
        else { [IO.File]::Move($temporary, $Path) }
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
    }
}

function Write-AtomicUtf8([string]$Path, [string]$Text) {
    Write-AtomicBytes $Path ([Text.UTF8Encoding]::new($false).GetBytes($Text))
}

function Get-Utf8Bytes([string]$Text, [bool]$WithBom) {
    $encoding = [Text.UTF8Encoding]::new($WithBom)
    $body = $encoding.GetBytes($Text)
    if (-not $WithBom) { return $body }
    $preamble = $encoding.GetPreamble()
    $output = New-Object byte[] ($preamble.Length + $body.Length)
    [Buffer]::BlockCopy($preamble, 0, $output, 0, $preamble.Length)
    [Buffer]::BlockCopy($body, 0, $output, $preamble.Length, $body.Length)
    return $output
}

function Add-Snapshot([string]$Path) {
    if ($snapshots | Where-Object { [string]::Equals($_.Path, $Path, [StringComparison]::OrdinalIgnoreCase) }) { return }
    $exists = Test-Path -LiteralPath $Path
    $backup = $null
    if ($exists) {
        $backup = Join-Path $migrationRoot ('files\' + $snapshots.Count.ToString('D3') + '.bin')
        [IO.Directory]::CreateDirectory((Split-Path -Parent $backup)) | Out-Null
        [IO.File]::Copy($Path, $backup, $true)
    }
    $snapshots.Add([pscustomobject]@{ Path=$Path; Existed=$exists; Backup=$backup })
}

function Restore-Snapshots {
    for ($index = $snapshots.Count - 1; $index -ge 0; $index--) {
        $snapshot = $snapshots[$index]
        if ($snapshot.Existed) {
            [IO.Directory]::CreateDirectory((Split-Path -Parent $snapshot.Path)) | Out-Null
            [IO.File]::Copy($snapshot.Backup, $snapshot.Path, $true)
        }
        elseif (Test-Path -LiteralPath $snapshot.Path) {
            Remove-Item -LiteralPath $snapshot.Path -Force
        }
    }
}

function Update-SharedReceipt([string]$Path, [string]$XmlHash, [string]$AtlasHash) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Shared icon receipt is missing: $Path" }
    $receipt = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $networkIcons = @($receipt.Icons | Where-Object { $_.Uuid -eq $partUuid })
    if ($networkIcons.Count -ne 1) { throw "Shared icon receipt does not contain exactly one Network Storage Chest tile: $Path" }
    $receipt.ActiveMods = @($receipt.ActiveMods | Where-Object { $_ -ne 'NetworkStorageChest' })
    $receipt.IconXmlHash = $XmlHash
    $receipt.AtlasOutputHash = $AtlasHash
    $receipt.UpdatedUtc = [DateTime]::UtcNow.ToString('o')
    Write-AtomicUtf8 $Path ($receipt | ConvertTo-Json -Depth 12 -Compress)
}

if (-not (Test-Path -LiteralPath $GamePath)) { throw "Scrap Mechanic was not found at: $GamePath" }
if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) { throw 'Scrap Mechanic is running. Close it before migrating Phase 6.' }
foreach ($required in @($helperPath,$developmentScript,$developmentReceipt,$sharedState,$sharedMirror,$sharedBaseline,$iconXml,$iconAtlas,$wirelessReceipt,$wirelessReceiptBackup)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required migration input is missing: $required" }
}
if (Test-Path -LiteralPath $productionReceipt) { throw 'A production Network Storage Chest receipt already exists; migration was not started.' }

$development = Get-Content -LiteralPath $developmentReceipt -Raw -Encoding UTF8 | ConvertFrom-Json
if ($development.SchemaVersion -ne 3) { throw 'The active development receipt is not the qualified Phase 5 schema.' }
if (-not [string]::Equals($development.GamePath, $GamePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The active development receipt belongs to a different game installation.'
}
foreach ($target in $development.Targets) {
    $path = Join-Path $GamePath $target.RelativePath
    if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $target.OutputHash) {
        throw "A qualified development target changed after testing: $($target.RelativePath)"
    }
}
foreach ($owned in $development.Owned) {
    $path = Join-Path $GamePath $owned.RelativePath
    if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $owned.Hash) {
        throw "A qualified development asset changed after testing: $($owned.RelativePath)"
    }
}

$productionTargets = @(
    'Survival\Objects\Database\shapesets.json',
    'Survival\Scripts\game\survival_items.lua',
    'Survival\Scripts\game\util\pipes.lua',
    'Survival\CraftingRecipes\craftbot\craftbot_core.json',
    'Survival\Scripts\game\managers\RecipeManager.lua',
    'Survival\Gui\IconMapSurvival.xml',
    'Survival\Gui\IconMapSurvival.png',
    'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua',
    'Survival\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua',
    'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui',
    'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChestItem.gui',
    'Survival\Objects\Database\ShapeSets\ScrapLab\Parts\NetworkStorageChest.shapeset',
    'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.localization.json'
)
foreach ($language in @('Brazilian','Chinese','English','French','German','Italian','Japanese','Korean','Polish','Russian','Spanish')) {
    $productionTargets += "Survival\Gui\Language\$language\inventoryDescriptions.json"
}
[IO.Directory]::CreateDirectory($migrationRoot) | Out-Null
foreach ($relative in $productionTargets) { Add-Snapshot (Join-Path $GamePath $relative) }
foreach ($target in $development.Targets) { Add-Snapshot (Join-Path $GamePath $target.RelativePath) }
foreach ($owned in $development.Owned) { Add-Snapshot (Join-Path $GamePath $owned.RelativePath) }
foreach ($path in @($developmentReceipt,$sharedState,$sharedMirror,$sharedBaseline,$productionReceipt,$productionActivation,$wirelessReceipt,$cachePath)) { Add-Snapshot $path }

try {
    $xmlBytes = [IO.File]::ReadAllBytes($iconXml)
    $hasBom = $xmlBytes.Length -ge 3 -and $xmlBytes[0] -eq 0xEF -and $xmlBytes[1] -eq 0xBB -and $xmlBytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $xmlText = [Text.Encoding]::UTF8.GetString($xmlBytes, $offset, $xmlBytes.Length - $offset)
    $pattern = '(?m)^[ \t]*<!-- SCRAPLAB PART: Network Storage Chest icon\. -->\r?\n[ \t]*<Index name="' + [regex]::Escape($partUuid) + '">\r?\n[ \t]*<Frame point="[0-9]+ [0-9]+"/>\r?\n[ \t]*</Index>\r?\n'
    $matches = [regex]::Matches($xmlText, $pattern)
    if ($matches.Count -ne 1) { throw 'The development icon XML entry is missing, duplicated, or edited.' }
    $xmlOutput = [regex]::Replace($xmlText, $pattern, '', 1)
    Write-AtomicBytes $iconXml (Get-Utf8Bytes $xmlOutput $hasBom)
    $xmlHash = Get-Sha256 $iconXml
    $atlasHash = Get-Sha256 $iconAtlas
    Update-SharedReceipt $sharedState $xmlHash $atlasHash
    Update-SharedReceipt $sharedMirror $xmlHash $atlasHash

    & $developmentScript -Action Uninstall -GamePath $GamePath

    $wirelessBase = Get-Content -LiteralPath $wirelessReceiptBackup -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($wirelessBase.ModKey -ne 'WirelessVacuumPipe' -or -not $wirelessBase.Files) {
        throw 'The verified pre-Network Storage Wireless Vacuum Pipe receipt is invalid.'
    }
    foreach ($file in $wirelessBase.Files) {
        $path = Join-Path $GamePath $file.RelativePath
        if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $file.OutputHash) {
            throw "The restored game files do not match the pre-Network Storage Wireless receipt: $($file.RelativePath)"
        }
    }
    Write-AtomicBytes $wirelessReceipt ([IO.File]::ReadAllBytes($wirelessReceiptBackup))

    $assembly = [Reflection.Assembly]::LoadFrom($helperPath)
    $service = $assembly.GetType('RaidRescue.NetworkStorageChestPatchService', $true)
    $flags = [Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
    $setEnabled = $service.GetMethod('SetEnabledAt', $flags)
    $getStatus = $service.GetMethod('GetStatusAt', $flags)
    if (-not $setEnabled -or -not $getStatus) { throw 'The production Network Storage Chest service entry points are missing.' }
    $enableArguments = New-Object 'object[]' 3
    $enableArguments[0] = [string]$GamePath
    $enableArguments[1] = [string]$secretBackupRoot
    $enableArguments[2] = [bool]$true
    $result = $setEnabled.Invoke($null, $enableArguments)
    if (-not $result.Success -or -not $result.Installed) { throw ('Production installation failed: ' + $result.Error) }
    $statusArguments = New-Object 'object[]' 1
    $statusArguments[0] = [string]$GamePath
    $status = $getStatus.Invoke($null, $statusArguments)
    if (-not $status.Success -or -not $status.Installed) { throw ('Production status verification failed: ' + $status.Error) }
    if (Test-Path -LiteralPath $developmentReceipt) { throw 'The development receipt still exists after migration.' }
    if (-not (Test-Path -LiteralPath $productionReceipt)) { throw 'The production receipt was not created.' }

    $finalState = Get-Content -LiteralPath $sharedState -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($mod in @('RaidDetector','WirelessVacuumPipe','NetworkStorageChest')) {
        if ($finalState.ActiveMods -notcontains $mod) { throw "The shared icon state lost active mod $mod." }
    }
    $finalXml = Get-Content -LiteralPath $iconXml -Raw -Encoding UTF8
    foreach ($uuid in @('a638a8aa-6f4f-41c2-9e31-702687066092','a34d9af0-4ba0-431d-b647-2d5435ecf138',$partUuid)) {
        if (([regex]::Matches($finalXml, [regex]::Escape($uuid))).Count -ne 1) { throw "Final icon XML registration is not unique: $uuid" }
    }
    [pscustomobject]@{
        Success = $true
        Installed = $status.Installed
        CompatibilityState = $status.CompatibilityState
        FilesPatched = $result.FilesPatched
        ProductionReceipt = $productionReceipt
        MigrationBackup = $migrationRoot
    } | ConvertTo-Json -Compress
}
catch {
    $failure = $_
    Restore-Snapshots
    throw "Phase 6 migration failed and the complete pre-migration state was restored. $($failure.Exception.Message)"
}
