param(
    [string]$RaidRescueExe,
    [string]$Python = "python"
)

$ErrorActionPreference = "Stop"

if ([String]::IsNullOrWhiteSpace($RaidRescueExe)) {
    $RaidRescueExe = Join-Path $PSScriptRoot "..\dist\ScrapLab.exe"
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
}

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Invoke-InstanceMethod {
    param(
        [object]$Target,
        [Reflection.MethodInfo]$Method,
        [object[]]$Arguments
    )
    $invokeArguments = New-Object object[] $Arguments.Count
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        $invokeArguments[$index] = $Arguments[$index].PSObject.BaseObject
    }
    return $Method.Invoke($Target, $invokeArguments)
}

$resolvedExe = [IO.Path]::GetFullPath($RaidRescueExe)
Assert-True (Test-Path -LiteralPath $resolvedExe) `
    "Build ScrapLab.exe before running the operation regression."

$fixtureRoot = Join-Path (
    [IO.Path]::GetTempPath()) (
    "scraplab-performance-operation-" +
    [Guid]::NewGuid().ToString("N"))

try {
    [IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
    $generator = Join-Path $PSScriptRoot "GeneratePerformanceFixtures.py"
    & $Python $generator $fixtureRoot --dense-count 200000
    if ($LASTEXITCODE -ne 0) {
        throw "The lifecycle fixtures could not be generated."
    }

    $densePath = Join-Path $fixtureRoot "dense-long-running.db"
    $ordinaryPath = Join-Path $fixtureRoot "ordinary-new.db"
    $denseHash = Get-Sha256 $densePath
    $ordinaryHash = Get-Sha256 $ordinaryPath

    $assembly = [Reflection.Assembly]::LoadFrom($resolvedExe)
    $managerType = $assembly.GetType(
        "RaidRescue.PerformanceScanOperationManager", $true)
    $flags = [Reflection.BindingFlags]"Public,Instance"
    $beginMethod = $managerType.GetMethod("Begin", $flags)
    $statusMethod = $managerType.GetMethod("GetStatus", $flags)
    $cancelMethod = $managerType.GetMethod("Cancel", $flags)
    $disposeMethod = $managerType.GetMethod("Dispose", $flags)
    Assert-True ($null -ne $beginMethod) "Begin was not found."
    Assert-True ($null -ne $statusMethod) "GetStatus was not found."
    Assert-True ($null -ne $cancelMethod) "Cancel was not found."
    Assert-True ($null -ne $disposeMethod) "Dispose was not found."

    $manager = [Activator]::CreateInstance($managerType, $true)
    $startWatch = [Diagnostics.Stopwatch]::StartNew()
    $start = Invoke-InstanceMethod $manager $beginMethod @($densePath)
    $startWatch.Stop()
    Assert-True $start.Success "The first background scan did not start."
    Assert-True ($startWatch.ElapsedMilliseconds -lt 500) `
        "Begin blocked instead of returning promptly."

    $duplicate = Invoke-InstanceMethod $manager $beginMethod @($ordinaryPath)
    Assert-True (-not $duplicate.Success) `
        "A second simultaneous scan was incorrectly accepted."

    $operationId = $start.OperationId
    $lastPercent = 0
    $sawNonTerminal = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $status = Invoke-InstanceMethod `
            $manager $statusMethod @($operationId)
        Assert-True $status.Success "The running operation was lost."
        $percent = [int]$status.Progress.OverallPercent
        Assert-True ($percent -ge $lastPercent) `
            "Operation progress moved backwards."
        $lastPercent = $percent
        if (-not $status.Terminal) {
            $sawNonTerminal = $true
            Start-Sleep -Milliseconds 5
        }
        if ([DateTime]::UtcNow -gt $deadline) {
            throw "The background scan did not finish in time."
        }
    } while (-not $status.Terminal)

    Assert-True $sawNonTerminal `
        "The lifecycle never exposed a non-terminal status."
    Assert-True ($status.State -eq "completed") `
        "The operation did not finish as completed: $($status.State)"
    Assert-True ($status.Progress.OverallPercent -eq 100) `
        "Completed progress did not reach 100 percent."
    Assert-True $status.Result.Success `
        "The completed operation did not contain a successful result."
    Assert-True ($status.Result.PopulatedCells -eq 289) `
        "The background result contained the wrong cell total."
    Assert-True ($status.Result.Cells.Count -eq 0) `
        "The browser result exposed the unbounded per-cell collection."
    Assert-True ($status.Result.Hotspots.Count -eq 1) `
        "The bounded browser result did not include its ranked hotspot."
    Assert-True (
        $status.Result.Hotspots[0].Evidence.Count -ge 3) `
        "The browser hotspot did not include severity evidence."

    $unknown = Invoke-InstanceMethod `
        $manager $statusMethod @("not-an-operation")
    Assert-True (-not $unknown.Success) `
        "An unknown operation ID was incorrectly accepted."
    Assert-True ($unknown.State -eq "not_found") `
        "An unknown operation did not fail closed."

    $cancelStart = Invoke-InstanceMethod `
        $manager $beginMethod @($densePath)
    Assert-True $cancelStart.Success `
        "The cancellation scan did not start."
    $cancelAccepted = Invoke-InstanceMethod `
        $manager $cancelMethod @($cancelStart.OperationId)
    Assert-True $cancelAccepted "Cancellation was not accepted."
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $cancelStatus = Invoke-InstanceMethod `
            $manager $statusMethod @($cancelStart.OperationId)
        if (-not $cancelStatus.Terminal) {
            Start-Sleep -Milliseconds 5
        }
        if ([DateTime]::UtcNow -gt $deadline) {
            throw "The cancelled scan did not stop in time."
        }
    } while (-not $cancelStatus.Terminal)
    Assert-True ($cancelStatus.State -eq "cancelled") `
        "Cancellation ended in an unexpected state."
    Assert-True $cancelStatus.Result.Cancelled `
        "The cancelled operation did not preserve its cancelled result."

    $restart = Invoke-InstanceMethod `
        $manager $beginMethod @($ordinaryPath)
    Assert-True $restart.Success `
        "The manager was not reusable after cancellation."
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $restartStatus = Invoke-InstanceMethod `
            $manager $statusMethod @($restart.OperationId)
        if (-not $restartStatus.Terminal) {
            Start-Sleep -Milliseconds 5
        }
        if ([DateTime]::UtcNow -gt $deadline) {
            throw "The post-cancel scan did not finish in time."
        }
    } while (-not $restartStatus.Terminal)
    Assert-True ($restartStatus.State -eq "completed") `
        "A scan after cancellation did not complete."

    $shutdownStart = Invoke-InstanceMethod `
        $manager $beginMethod @($densePath)
    Assert-True $shutdownStart.Success `
        "The shutdown-cancellation scan did not start."
    $shutdownWatch = [Diagnostics.Stopwatch]::StartNew()
    Invoke-InstanceMethod $manager $disposeMethod @() | Out-Null
    $shutdownWatch.Stop()
    Assert-True ($shutdownWatch.ElapsedMilliseconds -lt 500) `
        "Dispose waited for the scanner thread."

    Assert-True ((Get-Sha256 $densePath) -eq $denseHash) `
        "The dense source changed during lifecycle testing."
    Assert-True ((Get-Sha256 $ordinaryPath) -eq $ordinaryHash) `
        "The ordinary source changed during lifecycle testing."

    $uiType = $assembly.GetType("RaidRescue.UiHtml", $true)
    $uiField = $uiType.GetField(
        "Content",
        [Reflection.BindingFlags]"Public,Static")
    $ui = [string]$uiField.GetValue($null)
    foreach ($requiredText in @(
        "SCAN PERFORMANCE",
        "CANCEL SCAN",
        "BeginPerformanceScan",
        "GetPerformanceScanStatus",
        "CancelPerformanceScan",
        "performanceZone",
        "POTENTIAL HOTSPOTS",
        "WORLD COMPARISON",
        "COPY WORLD CENTER",
        "selectPerformanceWorld",
        "CopyText"
    )) {
        Assert-True ($ui.Contains($requiredText)) `
            "The Phase 2 UI is missing '$requiredText'."
    }
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host (
    "Performance operation regression passed: asynchronous start, " +
    "single-operation guard, monotonic progress, bounded browser result, " +
    "ranked UI data, cancellation, restart, and non-blocking shutdown.")
