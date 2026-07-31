$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-Method {
    param(
        [object]$Target,
        [Reflection.MethodInfo]$Method,
        [object[]]$Arguments
    )
    $invokeArguments = New-Object object[] $Arguments.Count
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        $invokeArguments[$index] = $Arguments[$index].PSObject.BaseObject
    }
    try {
        return $Method.Invoke($Target, $invokeArguments)
    }
    catch [Reflection.TargetInvocationException] {
        throw $_.Exception.InnerException
    }
}

$root = Split-Path -Parent $PSScriptRoot
$exe = (Resolve-Path -LiteralPath (Join-Path $root "dist\ScrapLab.exe")).Path
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "raidrescue-performance-phase5-" + [Guid]::NewGuid().ToString("N"))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null

try {
    & python (Join-Path $PSScriptRoot "GeneratePerformanceFixtures.py") `
        $fixtureRoot | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Fixture generation failed." }

    $unitPath = Join-Path $fixtureRoot "unit-cells.db"
    $beforeHash = (Get-FileHash -LiteralPath $unitPath -Algorithm SHA256).Hash
    $assembly = [Reflection.Assembly]::LoadFrom($exe)
    $scannerType = $assembly.GetType("RaidRescue.PerformanceScanner", $true)
    $resultType = $assembly.GetType("RaidRescue.PerformanceScanResult", $true)
    $scanMethod = $scannerType.GetMethod(
        "Scan",
        [Reflection.BindingFlags]"Public,Static",
        $null,
        [Type[]]@([string], [Threading.CancellationToken]),
        $null)
    $scanArgs = New-Object object[] 2
    $scanArgs[0] = $unitPath
    $scanArgs[1] = [Threading.CancellationToken]::None
    $scan = Invoke-Method $null $scanMethod $scanArgs
    Assert-True $scan.Success "The Phase 5 fixture scan failed."

    # A modded descriptor must not smuggle a local path into an export even
    # if it reaches the display-name layer.
    $scan.Worlds[0].WorldName = $unitPath
    $scan.Hotspots[0].WorldName = $unitPath

    $exporterType = $assembly.GetType(
        "RaidRescue.PerformanceReportExporter", $true)
    $createMethod = $exporterType.GetMethod(
        "Create",
        [Reflection.BindingFlags]"Public,NonPublic,Static",
        $null,
        [Type[]]@($resultType, [string], [DateTime]),
        $null)
    Assert-True ($null -ne $createMethod) `
        "The privacy-safe report exporter was not found."
    $exportedAt = [DateTime]::SpecifyKind(
        [DateTime]::Parse("2026-07-31T12:34:56"),
        [DateTimeKind]::Utc)
    $exportArgs = New-Object object[] 3
    $exportArgs[0] = $scan
    $exportArgs[1] = "9.9.9-test"
    $exportArgs[2] = $exportedAt
    $firstExport = Invoke-Method $null $createMethod $exportArgs
    $secondExport = Invoke-Method $null $createMethod $exportArgs
    Assert-True $firstExport.Success "A completed report was not exportable."
    Assert-True ($firstExport.Json -ceq $secondExport.Json) `
        "The report export was not deterministic for fixed inputs."
    Assert-True ($firstExport.SuggestedFileName -eq `
        "ScrapLab-Performance-Report-v3.json") `
        "The suggested report filename was not scanner-versioned."

    $json = [string]$firstExport.Json
    $document = $json | ConvertFrom-Json
    Assert-True ($document.Format -eq `
        "scraplab-performance-report") `
        "The report format identifier was incorrect."
    Assert-True ($document.FormatVersion -eq 1) `
        "The report contract version was incorrect."
    Assert-True ($document.AppVersion -eq "9.9.9-test") `
        "The app version was missing from the report."
    Assert-True ($document.ScannerVersion -eq 3) `
        "The scanner version was missing from the report."
    Assert-True ($document.SaveVersion -eq 28) `
        "The save version was missing from the report."
    Assert-True ($document.ExportedUtc -eq "2026-07-31T12:34:56Z") `
        "The UTC export time was not normalized."
    Assert-True ($document.Summary.TotalRecords -eq 603) `
        "The exported aggregate record count was incorrect."
    Assert-True ($document.Hotspots.Count -eq 1) `
        "The exported hotspot list was incorrect."
    Assert-True ($document.Worlds.Count -eq 1) `
        "The exported world summaries were incorrect."
    Assert-True ($document.Worlds[0].WorldName -eq "World 1") `
        "A path-like world display name was not replaced safely."
    Assert-True ($document.Categories.Count -ge 2) `
        "The exported category summaries were incomplete."
    Assert-True ($document.Coverage.RecordsConsidered -eq 603) `
        "The exported coverage denominator was incorrect."

    foreach ($forbiddenProperty in @(
        "Cells", "LargestRecords", "Schema", "UnsupportedTables",
        "Path", "FileName", "SourcePath", "RawPayloads")) {
        Assert-True (
            $document.PSObject.Properties.Name -notcontains
                $forbiddenProperty) `
            "The default report exposed forbidden property $forbiddenProperty."
    }
    Assert-True (-not $json.Contains($unitPath)) `
        "The report leaked the absolute source path."
    Assert-True (-not $json.Contains([IO.Path]::GetFileName($unitPath))) `
        "The report leaked the source filename."
    Assert-True (-not $json.Contains("RawPayload")) `
        "The report exposed a raw-payload field."

    $managerType = $assembly.GetType(
        "RaidRescue.PerformanceScanOperationManager", $true)
    $manager = [Activator]::CreateInstance($managerType, $true)
    $begin = $managerType.GetMethod("Begin")
    $status = $managerType.GetMethod("GetStatus")
    $getCells = $managerType.GetMethod("GetWorldCells")
    $dispose = $managerType.GetMethod("Dispose")
    $start = Invoke-Method $manager $begin @($unitPath)
    Assert-True $start.Success "The paging scan did not start."
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $operation = Invoke-Method $manager $status @($start.OperationId)
        if (-not $operation.Terminal) { Start-Sleep -Milliseconds 5 }
        if ([DateTime]::UtcNow -gt $deadline) {
            throw "The paging scan did not finish in time."
        }
    } while (-not $operation.Terminal)
    Assert-True ($operation.State -eq "completed") `
        "The paging scan did not complete."

    $page = Invoke-Method $manager $getCells @(
        $start.OperationId, 1, -50, 999999)
    Assert-True $page.Success "The first aggregate cell page failed."
    Assert-True ($page.Offset -eq 0) `
        "A negative cell offset was not normalized."
    Assert-True ($page.Limit -eq 250) `
        "An overlarge cell request was not capped at 250."
    Assert-True ($page.TotalCells -eq 2) `
        "The aggregate cell total was incorrect."
    Assert-True ($page.Cells.Count -eq 2) `
        "The aggregate cell page returned the wrong rows."
    Assert-True (-not $page.HasMore) `
        "The complete two-cell page incorrectly reported more data."
    Assert-True ($page.Cells[0].PSObject.Properties.Name -notcontains `
        "RawPayload") "A cell page exposed raw payload data."

    $page.Cells[0].Categories.Clear()
    $freshPage = Invoke-Method $manager $getCells @(
        $start.OperationId, 1, 0, 1)
    Assert-True ($freshPage.Cells[0].Categories.Count -gt 0) `
        "Caller mutation changed the retained scanner result."
    $badWorld = Invoke-Method $manager $getCells @(
        $start.OperationId, 999999, 0, 25)
    Assert-True (-not $badWorld.Success) `
        "An unknown world ID did not fail closed."
    $badOperation = Invoke-Method $manager $getCells @(
        "not-an-operation", 1, 0, 25)
    Assert-True (-not $badOperation.Success) `
        "A stale operation ID did not fail closed."

    $densePath = Join-Path $fixtureRoot "dense-long-running.db"
    $denseStart = Invoke-Method $manager $begin @($densePath)
    Assert-True $denseStart.Success "The multi-page scan did not start."
    $replacedPage = Invoke-Method $manager $getCells @(
        $start.OperationId, 1, 0, 25)
    Assert-True (-not $replacedPage.Success) `
        "A replaced operation retained access to cell pages."
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $denseOperation = Invoke-Method $manager $status @(
            $denseStart.OperationId)
        if (-not $denseOperation.Terminal) { Start-Sleep -Milliseconds 5 }
        if ([DateTime]::UtcNow -gt $deadline) {
            throw "The multi-page scan did not finish in time."
        }
    } while (-not $denseOperation.Terminal)
    Assert-True ($denseOperation.State -eq "completed") `
        "The multi-page scan did not complete."
    $denseFirst = Invoke-Method $manager $getCells @(
        $denseStart.OperationId, 1, 0, 999999)
    $denseSecond = Invoke-Method $manager $getCells @(
        $denseStart.OperationId, 1, 250, 999999)
    Assert-True ($denseFirst.TotalCells -eq 289) `
        "The multi-page world cell total was incorrect."
    Assert-True ($denseFirst.Cells.Count -eq 250) `
        "The first cell page did not enforce the 250-row cap."
    Assert-True $denseFirst.HasMore `
        "The first cell page did not advertise its continuation."
    Assert-True ($denseSecond.Cells.Count -eq 39) `
        "The second cell page returned the wrong remainder."
    Assert-True (-not $denseSecond.HasMore) `
        "The final cell page incorrectly advertised more rows."
    Invoke-Method $manager $dispose @() | Out-Null

    $uiType = $assembly.GetType("RaidRescue.UiHtml", $true)
    $ui = [string]$uiType.GetField(
        "Content", [Reflection.BindingFlags]"Public,Static").GetValue($null)
    foreach ($required in @(
        "EXPORT JSON",
        "EXPLORE CELLS",
        "ExportPerformanceReport",
        "GetPerformanceWorldCells",
        "AGGREGATED CELL EXPLORER",
        "No save path or raw payload is included."
    )) {
        Assert-True $ui.Contains($required) `
            "The Phase 5 UI is missing '$required'."
    }

    $afterHash = (Get-FileHash -LiteralPath $unitPath -Algorithm SHA256).Hash
    Assert-True ($beforeHash -eq $afterHash) `
        "Phase 5 export or paging changed the fixture."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host (
    "Performance Phase 5 regression passed: versioned privacy-safe JSON, " +
    "deterministic export, path and raw-field exclusion, bounded cell " +
    "paging, stale-ID rejection, copy isolation, UI bridge, and unchanged " +
    "source hash.")
