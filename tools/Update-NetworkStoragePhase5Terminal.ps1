[CmdletBinding()]
param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic')

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$relative = 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua'
$source = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChest.lua'
$target = Join-Path $GamePath $relative
$receiptPath = Join-Path $kitRoot 'dist\phase0-backups\NetworkStorageChest\active.json'

if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) {
    throw 'Scrap Mechanic is running. Close it before updating the Phase 5 terminal.'
}

$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$entry = $receipt.Owned | Where-Object { $_.RelativePath -eq $relative } | Select-Object -First 1
if (-not $entry) { throw 'The Network Storage Chest terminal receipt entry is missing.' }

$actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if (-not [string]::Equals($actual, $entry.Hash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The installed Network Storage Chest terminal changed unexpectedly.'
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
$backupRoot = Join-Path $kitRoot ('dist\phase5-backups\NetworkStorageChest\Terminal-' + $stamp)
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$targetBackup = Join-Path $backupRoot 'NetworkStorageChest.lua'
$receiptBackup = Join-Path $backupRoot 'active.json'
[IO.File]::Copy($target, $targetBackup, $true)
[IO.File]::Copy($receiptPath, $receiptBackup, $true)

$temporary = $target + '.scraplab-update-' + [Guid]::NewGuid().ToString('N') + '.tmp'
$swap = $target + '.scraplab-update-' + [Guid]::NewGuid().ToString('N') + '.swap'
try {
    [IO.File]::Copy($source, $temporary, $true)
    [IO.File]::Replace($temporary, $target, $swap)
    $entry.Hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))

    $cache = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
    if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }
    Write-Host "Updated and verified the Phase 5 terminal. Backup: $backupRoot"
}
catch {
    [IO.File]::Copy($targetBackup, $target, $true)
    [IO.File]::Copy($receiptBackup, $receiptPath, $true)
    throw
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
}
