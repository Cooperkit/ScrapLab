param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$guiPath = Join-Path $GamePath 'Survival\Gui'
$xmlPath = Join-Path $guiPath 'IconMapSurvival.xml'
$backupPath = Join-Path $guiPath 'IconMapSurvival.scraplab-group-test-backup.xml'
$testTexturePath = Join-Path $guiPath 'ScrapLabIconGroupTest.png'
$cachePath = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
$cacheBackupPath = $cachePath + '.scraplab-icon-group-test-original'

if (Get-Process -Name ScrapMechanic,ScrapMechanicServer -ErrorAction SilentlyContinue) {
    throw 'Close Scrap Mechanic completely before restoring the icon-group experiment.'
}
if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    throw "The dedicated experiment backup was not found: $backupPath"
}

$backupBytes = [IO.File]::ReadAllBytes($backupPath)
$temporary = $xmlPath + '.scraplab-group-restore.tmp'
$replaceBackup = $xmlPath + '.scraplab-group-restore-swap-backup.tmp'
try {
    [IO.File]::WriteAllBytes($temporary, $backupBytes)
    if (Test-Path -LiteralPath $xmlPath) {
        if (Test-Path -LiteralPath $replaceBackup) {
            Remove-Item -LiteralPath $replaceBackup -Force
        }
        [IO.File]::Replace($temporary, $xmlPath, $replaceBackup, $true)
        Remove-Item -LiteralPath $replaceBackup -Force
    }
    else {
        [IO.File]::Move($temporary, $xmlPath)
    }
}
finally {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }
}

if (Test-Path -LiteralPath $testTexturePath) {
    Remove-Item -LiteralPath $testTexturePath -Force
}
if (Test-Path -LiteralPath $cachePath) {
    Remove-Item -LiteralPath $cachePath -Force
}
if (Test-Path -LiteralPath $cacheBackupPath) {
    Move-Item -LiteralPath $cacheBackupPath -Destination $cachePath
}

$restoredHash = (Get-FileHash -LiteralPath $xmlPath -Algorithm SHA256).Hash
$backupHash = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
if ($restoredHash -ne $backupHash) {
    throw 'The restored IconMapSurvival.xml failed checksum verification.'
}
Write-Host 'The second ItemIcons group experiment was restored exactly.'
