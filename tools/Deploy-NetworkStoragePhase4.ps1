[CmdletBinding()]
param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$partRoot = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest'
$pipeRoot = Join-Path $kitRoot 'source\Patching\Scripts\ScrapLab\PipeSystem'
$developmentReceiptPath = Join-Path $kitRoot 'dist\phase0-backups\NetworkStorageChest\active.json'
$wirelessReceiptPath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active\WirelessVacuumPipe.json'
$cachePath = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
$backupRoot = Join-Path $kitRoot (Join-Path 'dist\phase4-backups\NetworkStorageChest' $stamp)

function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Assert-Hash([string]$Path, [string]$Expected, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Label is missing: $Path" }
    $actual = Get-Sha $Path
    if (-not [string]::Equals($actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label changed unexpectedly. Expected $Expected, got $actual."
    }
}
function Get-TextState([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($bom) { 3 } else { 0 }
    $text = [Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    [pscustomobject]@{ Text = $text; HasBom = $bom; Newline = $newline }
}
function Write-AtomicText([string]$Path, [string]$Text, [bool]$HasBom) {
    $temporary = $Path + '.scraplab-phase4-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swap = $Path + '.scraplab-phase4-' + [Guid]::NewGuid().ToString('N') + '.swap'
    try {
        [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($HasBom))
        if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($temporary, $Path, $swap) }
        else { [IO.File]::Move($temporary, $Path) }
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
    }
}
function Copy-Atomic([string]$Source, [string]$Destination) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Destination)) | Out-Null
    $temporary = $Destination + '.scraplab-phase4-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swap = $Destination + '.scraplab-phase4-' + [Guid]::NewGuid().ToString('N') + '.swap'
    try {
        [IO.File]::Copy($Source, $temporary, $true)
        if (Test-Path -LiteralPath $Destination) { [IO.File]::Replace($temporary, $Destination, $swap) }
        else { [IO.File]::Move($temporary, $Destination) }
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
    }
}
function Save-Json([string]$Path, [object]$Value, [int]$Depth) {
    Write-AtomicText $Path ($Value | ConvertTo-Json -Depth $Depth) $false
}

if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) {
    throw 'Scrap Mechanic is running. Close it before installing the Phase 4 development build.'
}
if (-not (Test-Path -LiteralPath $developmentReceiptPath)) { throw 'The active Network Storage Chest development receipt is missing.' }
if (-not (Test-Path -LiteralPath $wirelessReceiptPath)) { throw 'The active Wireless Vacuum Pipe receipt is missing.' }

$developmentReceipt = Get-Content -LiteralPath $developmentReceiptPath -Raw | ConvertFrom-Json
$wirelessReceipt = Get-Content -LiteralPath $wirelessReceiptPath -Raw | ConvertFrom-Json
$loaderEntry = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase4Harness.lua" )'
$loaderRelative = 'Survival\Scripts\game\SurvivalGame.lua'
$englishRelative = 'Survival\Gui\Language\English\inventoryDescriptions.json'
$terminalRelative = 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua'
$harnessRelative = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase4Harness.lua'
$managerRelative = 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
$graphRelative = 'Survival\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'

$loaderReceipt = $developmentReceipt.Targets | Where-Object { $_.RelativePath -eq $loaderRelative } | Select-Object -First 1
$englishReceipt = $developmentReceipt.Targets | Where-Object { $_.RelativePath -eq $englishRelative } | Select-Object -First 1
$terminalReceipt = $developmentReceipt.Owned | Where-Object { $_.RelativePath -eq $terminalRelative } | Select-Object -First 1
if (-not $loaderReceipt -or -not $englishReceipt -or -not $terminalReceipt) { throw 'The development receipt is incomplete.' }

$loaderPath = Join-Path $GamePath $loaderRelative
$englishPath = Join-Path $GamePath $englishRelative
$terminalPath = Join-Path $GamePath $terminalRelative
$harnessPath = Join-Path $GamePath $harnessRelative
$managerPath = Join-Path $GamePath $managerRelative
$graphPath = Join-Path $GamePath $graphRelative
Assert-Hash $loaderPath $loaderReceipt.OutputHash 'SurvivalGame loader'
Assert-Hash $englishPath $englishReceipt.OutputHash 'English description file'
Assert-Hash $terminalPath $terminalReceipt.Hash 'Network Storage Chest script'
Assert-Hash $englishReceipt.BackupPath $englishReceipt.SourceHash 'Verified pre-terminal English backup'
Assert-Hash $managerPath '2EE306FA1303FDA36CC2CE64964CCD4E567CC27EA7D82D4F47B5B6CCE31BC321' 'Definition-5 WirelessPipeManager'
Assert-Hash $graphPath '8C8641F1069968D0750ABCDCB0C56261616D44B11E2C1814C4664222BED2BD2A' 'Definition-5 ScrapLabPipeGraph'

$loaderState = Get-TextState $loaderPath
if ($loaderState.Text.Contains($loaderEntry)) { throw 'The Phase 4 harness loader already exists.' }
$phase3Entry = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase3Harness.lua" )'
if (([regex]::Matches($loaderState.Text, [regex]::Escape($phase3Entry))).Count -ne 1) { throw 'The protected Phase 3 loader anchor is missing or duplicated.' }
$newLoader = $loaderState.Text.Replace($phase3Entry, $phase3Entry + $loaderState.Newline + $loaderEntry)

$englishState = Get-TextState $englishReceipt.BackupPath
$opening = '{' + $englishState.Newline
$openingIndex = $englishState.Text.IndexOf($opening, [StringComparison]::Ordinal)
if ($openingIndex -lt 0) { throw 'The verified English backup has an unexpected root.' }
$partUuid = 'bc7576a7-f226-459a-883c-e8460e955d63'
if ($englishState.Text.Contains($partUuid)) { throw 'The verified English backup already contains the terminal UUID.' }
$entry = "`t`"$partUuid`": {" + $englishState.Newline +
    "`t`t`"description`": `"Browse a connected storage network, take items from one catalog, and automatically sort deposits into matching containers. Supports Wireless Vacuum Pipe routes when installed.`"," + $englishState.Newline +
    "`t`t`"title`": `"Network Storage Chest`"," + $englishState.Newline +
    "`t`t`"upperCaseTitle`": `"NETWORK STORAGE CHEST`"" + $englishState.Newline +
    "`t}," + $englishState.Newline
$insertAt = $openingIndex + $opening.Length
$newEnglish = $englishState.Text.Substring(0, $insertAt) + $entry + $englishState.Text.Substring($insertAt)
if (([regex]::Matches($newEnglish, [regex]::Escape($partUuid))).Count -ne 1) { throw 'The repaired English output does not contain exactly one terminal entry.' }

[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$backups = @{}
foreach ($pair in @(
    @($loaderPath, 'SurvivalGame.lua'), @($englishPath, 'inventoryDescriptions.json'),
    @($terminalPath, 'NetworkStorageChest.lua'), @($managerPath, 'WirelessPipeManager.lua'),
    @($graphPath, 'ScrapLabPipeGraph.lua'), @($developmentReceiptPath, 'NetworkStorage-active.json'),
    @($wirelessReceiptPath, 'WirelessVacuumPipe-active.json')
)) {
    $destination = Join-Path $backupRoot $pair[1]
    [IO.File]::Copy($pair[0], $destination, $true)
    $backups[$pair[0]] = $destination
}

try {
    Write-AtomicText $loaderPath $newLoader $loaderState.HasBom
    Write-AtomicText $englishPath $newEnglish $englishState.HasBom
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChest.lua') $terminalPath
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChestPhase4Harness.lua') $harnessPath
    Copy-Atomic (Join-Path $pipeRoot 'WirelessPipeManager.lua') $managerPath
    Copy-Atomic (Join-Path $pipeRoot 'ScrapLabPipeGraph.lua') $graphPath

    $loaderReceipt.OutputHash = Get-Sha $loaderPath
    $englishReceipt.OutputHash = Get-Sha $englishPath
    $terminalReceipt.Hash = Get-Sha $terminalPath
    $developmentReceipt.Owned = @($developmentReceipt.Owned) + [pscustomobject]@{
        RelativePath = $harnessRelative
        Hash = Get-Sha $harnessPath
    }
    $developmentReceipt.SchemaVersion = 2
    Save-Json $developmentReceiptPath $developmentReceipt 8

    foreach ($relative in @($managerRelative, $graphRelative)) {
        $file = $wirelessReceipt.Files | Where-Object { $_.RelativePath -eq $relative } | Select-Object -First 1
        if (-not $file) { throw "The Wireless receipt is missing $relative." }
        $file.OutputHash = Get-Sha (Join-Path $GamePath $relative)
    }
    $wirelessReceipt.DefinitionVersion = '6'
    Save-Json $wirelessReceiptPath $wirelessReceipt 10

    Assert-Hash $loaderPath $loaderReceipt.OutputHash 'Updated SurvivalGame loader'
    Assert-Hash $englishPath $englishReceipt.OutputHash 'Repaired English description file'
    Assert-Hash $terminalPath $terminalReceipt.Hash 'Updated Network Storage Chest script'
    Assert-Hash $harnessPath (($developmentReceipt.Owned | Where-Object { $_.RelativePath -eq $harnessRelative }).Hash) 'Phase 4 harness'
    if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    Write-Host "Phase 4 installed and verified. Rollback backup: $backupRoot"
}
catch {
    foreach ($path in $backups.Keys) { [IO.File]::Copy($backups[$path], $path, $true) }
    if (Test-Path -LiteralPath $harnessPath) { Remove-Item -LiteralPath $harnessPath -Force }
    throw
}
