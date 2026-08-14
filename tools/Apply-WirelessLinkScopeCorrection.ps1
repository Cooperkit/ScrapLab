[CmdletBinding()]
param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic')

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$relative = 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
$source = Join-Path $kitRoot 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
$target = Join-Path $GamePath $relative
$receiptPath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active\WirelessVacuumPipe.json'
$legacyHash = '3411D6804F6D874C4B9BD8D8C80C4109BF3CECFB0F44F31EDF49C0DF4F3D8DC8'
if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) { throw 'Scrap Mechanic is running. Close it before updating Wireless Vacuum Pipe.' }
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$entry = $receipt.Files | Where-Object { $_.RelativePath -eq $relative } | Select-Object -First 1
if (-not $entry) { throw 'The Wireless Vacuum Pipe manager receipt entry is missing.' }
$actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if (-not [string]::Equals($actual, $legacyHash, [StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals($entry.OutputHash, $legacyHash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The installed manager is not the verified definition-6 file; no changes were made.'
}
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
$backupRoot = Join-Path $kitRoot (Join-Path 'dist\phase4-backups\NetworkStorageChest' ('LinkScope-' + $stamp))
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$managerBackup = Join-Path $backupRoot 'WirelessPipeManager.lua'
$receiptBackup = Join-Path $backupRoot 'WirelessVacuumPipe.json'
[IO.File]::Copy($target, $managerBackup, $true)
[IO.File]::Copy($receiptPath, $receiptBackup, $true)
$temporary = $target + '.scraplab-link-' + [Guid]::NewGuid().ToString('N') + '.tmp'
$swap = $target + '.scraplab-link-' + [Guid]::NewGuid().ToString('N') + '.swap'
try {
    [IO.File]::Copy($source, $temporary, $true)
    [IO.File]::Replace($temporary, $target, $swap)
    $entry.OutputHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    $receipt.DefinitionVersion = '7'
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $cache = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
    if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }
    Write-Host "Wireless Link scope correction installed and verified. Backup: $backupRoot"
}
catch {
    [IO.File]::Copy($managerBackup, $target, $true)
    [IO.File]::Copy($receiptBackup, $receiptPath, $true)
    throw
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
}
