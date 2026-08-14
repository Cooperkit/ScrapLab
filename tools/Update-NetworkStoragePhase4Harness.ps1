[CmdletBinding()]
param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic')

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$relative = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase4Harness.lua'
$source = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChestPhase4Harness.lua'
$target = Join-Path $GamePath $relative
$receiptPath = Join-Path $kitRoot 'dist\phase0-backups\NetworkStorageChest\active.json'
if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) { throw 'Scrap Mechanic is running. Close it before updating the test harness.' }
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$entry = $receipt.Owned | Where-Object { $_.RelativePath -eq $relative } | Select-Object -First 1
if (-not $entry) { throw 'The Phase 4 harness receipt entry is missing.' }
$actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if (-not [string]::Equals($actual, $entry.Hash, [StringComparison]::OrdinalIgnoreCase)) { throw 'The installed Phase 4 harness changed unexpectedly.' }
$backup = Join-Path $kitRoot ('dist\phase4-backups\NetworkStorageChest\Harness-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff') + '.lua')
[IO.Directory]::CreateDirectory((Split-Path -Parent $backup)) | Out-Null
[IO.File]::Copy($target, $backup, $true)
$temporary = $target + '.scraplab-update-' + [Guid]::NewGuid().ToString('N') + '.tmp'
$swap = $target + '.scraplab-update-' + [Guid]::NewGuid().ToString('N') + '.swap'
try {
    [IO.File]::Copy($source, $temporary, $true)
    [IO.File]::Replace($temporary, $target, $swap)
    $entry.Hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    $receiptText = $receipt | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($receiptPath, $receiptText, [Text.UTF8Encoding]::new($false))
    $cache = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
    if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }
    Write-Host "Updated and verified Phase 4 harness. Backup: $backup"
}
catch {
    [IO.File]::Copy($backup, $target, $true)
    throw
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
}
