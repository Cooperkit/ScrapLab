$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = (Resolve-Path -LiteralPath (Join-Path $root "dist\ScrapLab.exe")).Path
$assembly = [Reflection.Assembly]::LoadFrom($exe)
$type = $assembly.GetType("RaidRescue.ProductPaths", $true)
$flags = [Reflection.BindingFlags]::Static -bor
    [Reflection.BindingFlags]::Public -bor
    [Reflection.BindingFlags]::NonPublic

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}

$fixture = Join-Path ([IO.Path]::GetTempPath()) (
    "scraplab-product-migration-" + [Guid]::NewGuid().ToString("N"))
$legacy = Join-Path $fixture "Raid Rescue"
$current = Join-Path $fixture "ScrapLab"
try {
    [IO.Directory]::CreateDirectory((Join-Path $legacy "Patch State\Active")) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $legacy "Game Backups\Scrap Mechanic")) | Out-Null
    [IO.Directory]::CreateDirectory($current) | Out-Null
    [IO.File]::WriteAllText((Join-Path $legacy "preferences.ini"), "legacy")
    [IO.File]::WriteAllText((Join-Path $legacy "secret-mods.ini"), "secret")
    [IO.File]::WriteAllText((Join-Path $legacy "Patch State\Active\receipt.ini"), "receipt")
    [IO.File]::WriteAllText((Join-Path $legacy "Game Backups\Scrap Mechanic\backup.lua"), "backup")
    [IO.File]::WriteAllText((Join-Path $current "preferences.ini"), "current")

    $type.GetField("LocalDataRootOverride", $flags).SetValue($null, $current)
    $type.GetField("LegacyLocalDataRootOverride", $flags).SetValue($null, $legacy)
    $type.GetField("migrationComplete", $flags).SetValue($null, $false)
    $type.GetMethod("EnsureLegacyDataMigrated", $flags).Invoke($null, @())

    Assert-True ((Get-Content -Raw -LiteralPath (Join-Path $current "preferences.ini")) -eq "current") `
        "Migration overwrote a current ScrapLab preference file."
    Assert-True ((Get-Content -Raw -LiteralPath (Join-Path $current "secret-mods.ini")) -eq "secret") `
        "Secret-mod preferences were not migrated."
    Assert-True (Test-Path -LiteralPath (Join-Path $current "Patch State\Active\receipt.ini")) `
        "The active patch receipt was not migrated."
    Assert-True (Test-Path -LiteralPath (Join-Path $current "Game Backups\Scrap Mechanic\backup.lua")) `
        "The verified game backup was not migrated."
    Assert-True (Test-Path -LiteralPath (Join-Path $current "migration-from-raid-rescue-v1.complete")) `
        "The migration completion marker was not created."
    Assert-True (Test-Path -LiteralPath (Join-Path $legacy "preferences.ini")) `
        "Migration removed legacy Raid Rescue data."

    Write-Host "Product migration regression passed: missing-only copy, active receipts, backups, marker, and legacy preservation."
}
finally {
    if (Test-Path -LiteralPath $fixture) {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}
