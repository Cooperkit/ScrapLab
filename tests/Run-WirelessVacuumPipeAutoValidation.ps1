param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$LogRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic\Logs'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$results = [Collections.Generic.List[object]]::new()

function Add-ValidationResult([string]$Name, [string]$Outcome, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Outcome = $Outcome; Detail = $Detail })
}

function Invoke-Regression([string]$Name, [string]$ScriptName) {
    $path = Join-Path $PSScriptRoot $ScriptName
    try {
        $output = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $path 2>&1)
        if ($LASTEXITCODE -ne 0) {
            Add-ValidationResult $Name 'FAIL' (($output | Select-Object -Last 1) -join '')
            return
        }
        Add-ValidationResult $Name 'PASS' (($output | Select-Object -Last 1) -join '')
    } catch {
        Add-ValidationResult $Name 'FAIL' $_.Exception.Message
    }
}

Invoke-Regression 'Phase 1 transaction safety' 'WirelessVacuumPipePhase1Regression.ps1'
Invoke-Regression 'Phase 2 manager and endpoint lifecycle' 'WirelessVacuumPipePhase2Regression.ps1'
Invoke-Regression 'Phase 3 consumers, migrations, and rollback' 'WirelessVacuumPipePhase3Regression.ps1'

$installer = Join-Path $projectRoot 'tools\experiments\Manage-WirelessVacuumPipePhase3.ps1'
$definitionInstalledUtc = $null
try {
    $statusJson = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Action Status -GameRoot $GameRoot -AllowRunningGame
    $status = $statusJson | ConvertFrom-Json
    if ($status.State -eq 'INSTALLED' -and $status.DefinitionVersion -eq 10) {
        Add-ValidationResult 'Installed Phase 3 definition' 'PASS' 'definition 10 is installed and receipt-verified'
        if (Test-Path -LiteralPath $status.Receipt) {
            $receipt = Get-Content -LiteralPath $status.Receipt -Raw | ConvertFrom-Json
            $definitionInstalledUtc = [DateTime]::Parse($receipt.InstalledUtc).ToUniversalTime()
        }
    } else {
        Add-ValidationResult 'Installed Phase 3 definition' 'FAIL' ("state={0}, definition={1}" -f $status.State, $status.DefinitionVersion)
    }
} catch {
    Add-ValidationResult 'Installed Phase 3 definition' 'FAIL' $_.Exception.Message
}

$flatVacuumRegistered = $false
$shapeSetRoot = Join-Path $GameRoot 'Survival\Objects\Database\ShapeSets'
if (Test-Path -LiteralPath $shapeSetRoot) {
    foreach ($file in Get-ChildItem -LiteralPath $shapeSetRoot -Recurse -File -Filter '*.shapeset') {
        if ([IO.File]::ReadAllText($file.FullName).IndexOf('FlatVacuum.lua', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $flatVacuumRegistered = $true
            break
        }
    }
}
if ($flatVacuumRegistered) {
    Add-ValidationResult 'Flat Vacuum availability' 'PASS' 'a placeable shape registers FlatVacuum.lua'
} else {
    Add-ValidationResult 'Flat Vacuum availability' 'SKIP' 'the script is shipped, but no placeable shape registers it in this game build'
}

$latestLog = $null
if (Test-Path -LiteralPath $LogRoot) {
    $latestLog = Get-ChildItem -LiteralPath $LogRoot -File -Filter 'game-*.log' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}
if (-not $latestLog) {
    Add-ValidationResult 'Latest in-game automatic run' 'SKIP' 'no game log is available'
} elseif ($definitionInstalledUtc -and $latestLog.LastWriteTimeUtc -lt $definitionInstalledUtc) {
    Add-ValidationResult 'Latest in-game automatic run' 'SKIP' ("run /slpipe3 auto after installing definition 10; latest log is {0}" -f $latestLog.Name)
} else {
    $logText = Get-Content -LiteralPath $latestLog.FullName -Raw
    $summaries = [regex]::Matches($logText, '\[ScrapLab Pipe Phase 3\].*?summary=(\d+) passed, (\d+) failed(?:, (\d+) skipped)?\.')
    if ($summaries.Count -eq 0) {
        Add-ValidationResult 'Latest in-game automatic run' 'SKIP' ("run /slpipe3 auto once; latest log is {0}" -f $latestLog.Name)
    } else {
        $summary = $summaries[$summaries.Count - 1]
        $passed = [int]$summary.Groups[1].Value
        $failed = [int]$summary.Groups[2].Value
        $skipped = if ($summary.Groups[3].Success) { [int]$summary.Groups[3].Value } else { 0 }
        if ($passed -ge 10 -and $failed -eq 0) {
            Add-ValidationResult 'Latest in-game automatic run' 'PASS' ("{0} passed, 0 failed, {1} skipped in {2}" -f $passed, $skipped, $latestLog.Name)
        } else {
            Add-ValidationResult 'Latest in-game automatic run' 'FAIL' ("expected at least 10 passed and 0 failed; got {0}/{1} in {2}" -f $passed, $failed, $latestLog.Name)
        }
    }

    $fatalPatterns = @(
        'physical pipe graph safety limit exceeded',
        'wireless endpoint safety limit exceeded',
        'directional pipe opening catalog mismatch'
    )
    $fatalHits = @($fatalPatterns | Where-Object { $logText.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($fatalHits.Count -eq 0) {
        Add-ValidationResult 'Wireless graph safety log scan' 'PASS' $latestLog.Name
    } else {
        Add-ValidationResult 'Wireless graph safety log scan' 'FAIL' ($fatalHits -join ', ')
    }
}

Write-Host ''
Write-Host 'ScrapLab Wireless Vacuum Pipe automatic validation'
Write-Host '--------------------------------------------------'
foreach ($result in $results) {
    Write-Host ("[{0}] {1} - {2}" -f $result.Outcome, $result.Name, $result.Detail)
}
$passedCount = @($results | Where-Object Outcome -eq 'PASS').Count
$failedCount = @($results | Where-Object Outcome -eq 'FAIL').Count
$skippedCount = @($results | Where-Object Outcome -eq 'SKIP').Count
Write-Host ("Summary: {0} passed, {1} failed, {2} skipped." -f $passedCount, $failedCount, $skippedCount)

if ($failedCount -gt 0) { exit 1 }
