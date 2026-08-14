[CmdletBinding()]
param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic')

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$partRoot = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest'
$receiptPath = Join-Path $kitRoot 'dist\phase0-backups\NetworkStorageChest\active.json'
$files = @(
    [pscustomobject]@{ RelativePath='Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua'; Source='NetworkStorageChest.lua' },
    [pscustomobject]@{ RelativePath='Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui'; Source='NetworkStorageChest.gui' },
    [pscustomobject]@{ RelativePath='Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.localization.json'; Source='NetworkStorageChest.localization.json' }
)

if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) {
    throw 'Scrap Mechanic is running. Close it before updating the Phase 5 UI.'
}

$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
foreach ($file in $files) {
    $file | Add-Member Target (Join-Path $GamePath $file.RelativePath)
    $file | Add-Member SourcePath (Join-Path $partRoot $file.Source)
    $entry = $receipt.Owned | Where-Object { $_.RelativePath -eq $file.RelativePath } | Select-Object -First 1
    if (-not $entry) { throw "Owned-file receipt entry is missing: $($file.RelativePath)" }
    $file | Add-Member Entry $entry
    $actual = (Get-FileHash -LiteralPath $file.Target -Algorithm SHA256).Hash
    if (-not [string]::Equals($actual, $entry.Hash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Installed file changed unexpectedly: $($file.RelativePath)"
    }
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
$backupRoot = Join-Path $kitRoot ('dist\phase5-backups\NetworkStorageChest\Ui-' + $stamp)
[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$receiptBackup = Join-Path $backupRoot 'active.json'
[IO.File]::Copy($receiptPath, $receiptBackup, $true)
foreach ($file in $files) {
    $backup = Join-Path $backupRoot $file.Source
    [IO.File]::Copy($file.Target, $backup, $true)
    $file | Add-Member Backup $backup
}

$temporaryFiles = New-Object System.Collections.Generic.List[string]
$swapFiles = New-Object System.Collections.Generic.List[string]
try {
    foreach ($file in $files) {
        $temporary = $file.Target + '.scraplab-update-' + [Guid]::NewGuid().ToString('N') + '.tmp'
        $swap = $file.Target + '.scraplab-update-' + [Guid]::NewGuid().ToString('N') + '.swap'
        $temporaryFiles.Add($temporary)
        $swapFiles.Add($swap)
        [IO.File]::Copy($file.SourcePath, $temporary, $true)
        [IO.File]::Replace($temporary, $file.Target, $swap)
        $file.Entry.Hash = (Get-FileHash -LiteralPath $file.Target -Algorithm SHA256).Hash
    }
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))

    $cache = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
    if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }
    Write-Host "Updated and verified the Phase 5 UI files. Backup: $backupRoot"
}
catch {
    foreach ($file in $files) { [IO.File]::Copy($file.Backup, $file.Target, $true) }
    [IO.File]::Copy($receiptBackup, $receiptPath, $true)
    throw
}
finally {
    foreach ($path in $temporaryFiles) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } }
    foreach ($path in $swapFiles) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } }
}
