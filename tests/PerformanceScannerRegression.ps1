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

$resolvedExe = [IO.Path]::GetFullPath($RaidRescueExe)
Assert-True (Test-Path -LiteralPath $resolvedExe) `
    "Build ScrapLab.exe before running the scanner regression."

$fixtureRoot = Join-Path (
    [IO.Path]::GetTempPath()) (
    "scraplab-performance-scanner-" +
    [Guid]::NewGuid().ToString("N"))
$sourceRoot = Join-Path $fixtureRoot "source"
$scanRoot = Join-Path $fixtureRoot "scan"

try {
    [IO.Directory]::CreateDirectory($sourceRoot) | Out-Null
    [IO.Directory]::CreateDirectory($scanRoot) | Out-Null

    $generator = Join-Path $PSScriptRoot "GeneratePerformanceFixtures.py"
    & $Python $generator $sourceRoot
    if ($LASTEXITCODE -ne 0) {
        throw "The synthetic performance fixtures could not be generated."
    }

    Get-ChildItem -LiteralPath $sourceRoot -Filter "*.db" -File |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (
                Join-Path $scanRoot $_.Name)
        }

    $expected = @{
        "ordinary-new.db" = @{
            Harvestables = 3L
            Units = 0L
            Cells = 3L
            Worlds = 1L
            Total = 4L
            Hotspots = 0L
            Unsupported = 0L
            UnitCapability = $true
        }
        "dense-long-running.db" = @{
            Harvestables = 50576L
            Units = 0L
            Cells = 289L
            Worlds = 1L
            Total = 50576L
            Hotspots = 1L
            Unsupported = 0L
            UnitCapability = $true
        }
        "multi-world.db" = @{
            Harvestables = 12L
            Units = 0L
            Cells = 12L
            Worlds = 4L
            Total = 16L
            Hotspots = 0L
            Unsupported = 0L
            UnitCapability = $true
        }
        "legacy-v26.db" = @{
            Harvestables = 1L
            Units = 0L
            Cells = 1L
            Worlds = 1L
            Total = 1L
            Hotspots = 0L
            Unsupported = 0L
            UnitCapability = $true
        }
        "unit-cells.db" = @{
            Harvestables = 1L
            Units = 601L
            Cells = 2L
            Worlds = 1L
            Total = 603L
            Hotspots = 1L
            Unsupported = 0L
            UnitCapability = $true
        }
        "modded-extra-table.db" = @{
            Harvestables = 1L
            Units = 0L
            Cells = 1L
            Worlds = 1L
            Total = 1L
            Hotspots = 0L
            Unsupported = 2L
            UnitCapability = $false
        }
    }

    $assembly = [Reflection.Assembly]::LoadFrom($resolvedExe)
    $scanner = $assembly.GetType(
        "RaidRescue.PerformanceScanner", $true)
    $resultType = $assembly.GetType(
        "RaidRescue.PerformanceScanResult", $true)
    $scanMethod = $scanner.GetMethod(
        "Scan",
        [Reflection.BindingFlags]"Public,Static",
        $null,
        [Type[]]@(
            [string],
            [Threading.CancellationToken]
        ),
        $null)
    $serializeMethod = $scanner.GetMethod(
        "SerializeDeterministic",
        [Reflection.BindingFlags]"Public,Static",
        $null,
        [Type[]]@($resultType),
        $null)
    Assert-True ($null -ne $scanMethod) `
        "PerformanceScanner.Scan(path, cancellation) was not found."
    Assert-True ($null -ne $serializeMethod) `
        "The deterministic scanner serializer was not found."

    function Invoke-PerformanceScan {
        param(
            [string]$Path,
            [Threading.CancellationToken]$Cancellation
        )
        $arguments = New-Object object[] 2
        $arguments[0] = $Path
        $arguments[1] = $Cancellation
        return $scanMethod.Invoke($null, $arguments)
    }

    function ConvertTo-DeterministicScanJson {
        param([object]$Result)
        $arguments = New-Object object[] 1
        $arguments[0] = $Result
        return $serializeMethod.Invoke($null, $arguments)
    }

    foreach ($fixtureName in ($expected.Keys | Sort-Object)) {
        [string]$name = $fixtureName
        $sourcePath = Join-Path $sourceRoot $name
        $scanPath = Join-Path $scanRoot $name
        $sourceBefore = Get-Sha256 $sourcePath
        $scanBefore = Get-Sha256 $scanPath

        $result = Invoke-PerformanceScan `
            -Path $scanPath `
            -Cancellation ([Threading.CancellationToken]::None)
        Assert-True $result.Success "$name scan failed: $($result.Error)"
        Assert-True $result.SourceUnchanged `
            "$name did not prove its source unchanged."
        Assert-True ($result.ScanVersion -eq 3) `
            "$name returned an unexpected scan version."
        Assert-True (
            $result.Schema.CanReadUnitCells -eq
                $expected[$name].UnitCapability) `
            "$name returned the wrong Unit capability."
        Assert-True (
            $result.UnsupportedTableCount -eq
                $expected[$name].Unsupported) `
            "$name returned the wrong unsupported-table count."
        Assert-True (
            $result.Hotspots.Count -eq
                $expected[$name].Hotspots) `
            "$name returned an unexpected hotspot count."
        Assert-True ($result.Hotspots.Count -le 50) `
            "$name exceeded the bounded hotspot list."
        Assert-True ($result.PopulatedCells -eq $expected[$name].Cells) `
            "$name populated-cell count was incorrect."
        Assert-True ($result.WorldsScanned -eq $expected[$name].Worlds) `
            "$name world count was incorrect."
        Assert-True ($result.TotalRecords -eq $expected[$name].Total) `
            "$name total record count was incorrect."
        Assert-True ($result.LargestRecords.Count -le 25) `
            "$name exceeded the bounded largest-record list."

        $harvestable = $result.Categories |
            Where-Object { $_.Key -eq "harvestable" } |
            Select-Object -First 1
        Assert-True ($null -ne $harvestable) `
            "$name did not include Harvestable metrics."
        Assert-True (
            $harvestable.RecordCount -eq
                $expected[$name].Harvestables) `
            "$name Harvestable count was incorrect."
        $unit = $result.Categories |
            Where-Object { $_.Key -eq "unit" } |
            Select-Object -First 1
        if ($expected[$name].UnitCapability) {
            Assert-True ($null -ne $unit) `
                "$name did not include Unit metrics."
            Assert-True (
                $unit.RecordCount -eq
                    $expected[$name].Units) `
                "$name Unit count was incorrect."
            $expectedUnreadable = if ($name -eq "unit-cells.db") {
                1L
            }
            else {
                0L
            }
            Assert-True (
                $unit.UnreadableCount -eq $expectedUnreadable) `
                "$name returned the wrong unreadable Unit count."
        }
        else {
            Assert-True ($null -eq $unit) `
                "$name decoded an unsupported Unit layout."
        }

        $json = ConvertTo-DeterministicScanJson $result
        $repeat = Invoke-PerformanceScan `
            -Path $scanPath `
            -Cancellation ([Threading.CancellationToken]::None)
        $repeatJson = ConvertTo-DeterministicScanJson $repeat
        Assert-True ($json -ceq $repeatJson) `
            "$name JSON was not deterministic across scans."
        Assert-True (-not $json.Contains($scanPath)) `
            "$name JSON exposed its absolute path."
        Assert-True (-not $json.Contains($name)) `
            "$name JSON exposed its filename."

        Assert-True ((Get-Sha256 $sourcePath) -eq $sourceBefore) `
            "$name source fixture changed."
        Assert-True ((Get-Sha256 $scanPath) -eq $scanBefore) `
            "$name scan copy changed."
    }

    $multi = Invoke-PerformanceScan `
        -Path (Join-Path $scanRoot "multi-world.db") `
        -Cancellation ([Threading.CancellationToken]::None)
    $warehouse = $multi.Worlds |
        Where-Object { $_.WorldName -eq "Warehouse" }
    Assert-True (@($warehouse).Count -eq 2) `
        "Current world metadata did not decode both warehouses."

    $dense = Invoke-PerformanceScan `
        -Path (Join-Path $scanRoot "dense-long-running.db") `
        -Cancellation ([Threading.CancellationToken]::None)
    $denseHotspot = $dense.Hotspots[0]
    Assert-True ($denseHotspot.Severity -eq "VERY HEAVY") `
        "The dense fixture was not classified VERY HEAVY."
    Assert-True ($denseHotspot.WorldName -eq "Overworld") `
        "The dense hotspot did not retain its decoded world name."
    Assert-True (
        $denseHotspot.CellX -eq 3 -and
        $denseHotspot.CellY -eq -2) `
        "The dense fixture ranked the wrong center cell."
    Assert-True ($denseHotspot.NeighborhoodRecords -eq 50016) `
        "The dense fixture returned the wrong 3-by-3 total."
    Assert-True ($denseHotspot.Evidence.Count -ge 3) `
        "The dense severity was not backed by serialized evidence."

    $unitResult = Invoke-PerformanceScan `
        -Path (Join-Path $scanRoot "unit-cells.db") `
        -Cancellation ([Threading.CancellationToken]::None)
    $unitHotspot = $unitResult.Hotspots[0]
    Assert-True ($unitHotspot.Severity -eq "HEAVY") `
        "The Unit fixture was not classified HEAVY."
    Assert-True (
        $unitHotspot.CellX -eq 5 -and
        $unitHotspot.CellY -eq 6) `
        "The Unit fixture ranked the wrong center cell."
    Assert-True ($unitHotspot.NeighborhoodRecords -eq 601) `
        "The Unit fixture returned the wrong neighborhood total."
    Assert-True ($unitHotspot.Confidence -eq "PARTIAL") `
        "One malformed optional Unit payload did not reduce confidence."
    $unitCategory = $unitHotspot.Categories |
        Where-Object { $_.Key -eq "unit" } |
        Select-Object -First 1
    Assert-True (
        $null -ne $unitCategory -and
        $unitCategory.RecordCount -eq 601 -and
        $unitCategory.DecodedCount -eq 600 -and
        $unitCategory.UnreadableCount -eq 1) `
        "The Unit hotspot category breakdown was incorrect."
    Assert-True (
        @($unitHotspot.Evidence |
            Where-Object {
                $_.Key -eq "unit-concentration"
            }).Count -eq 1) `
        "The Unit hotspot did not explain its concentration."

    $fingerprintType = $scanner.GetNestedType(
        "SourceFingerprint",
        [Reflection.BindingFlags]"NonPublic")
    $captureMethod = $fingerprintType.GetMethod(
        "Capture",
        [Reflection.BindingFlags]"Public,Static")
    Assert-True ($null -ne $captureMethod) `
        "The source fingerprint capture method was not found."
    function Get-SourceFingerprint {
        param([string]$Path)
        $arguments = New-Object object[] 2
        $arguments[0] = $Path
        $arguments[1] = [Threading.CancellationToken]::None
        return $captureMethod.Invoke($null, $arguments)
    }
    [string]$changePath = Join-Path `
        $scanRoot "fingerprint-change.db"
    Copy-Item -LiteralPath (
        Join-Path $sourceRoot "ordinary-new.db") `
        -Destination $changePath
    $fingerprintBefore = Get-SourceFingerprint $changePath
    $stream = [IO.File]::Open(
        $changePath,
        [IO.FileMode]::Append,
        [IO.FileAccess]::Write,
        [IO.FileShare]::ReadWrite)
    try {
        $stream.WriteByte(0x5A)
    }
    finally {
        $stream.Dispose()
    }
    $fingerprintAfter = Get-SourceFingerprint $changePath
    Assert-True (-not $fingerprintBefore.Equals($fingerprintAfter)) `
        "A changed save produced the same source fingerprint."

    $cancelSource = [Threading.CancellationTokenSource]::new()
    $cancelSource.Cancel()
    $cancelled = Invoke-PerformanceScan `
        -Path (Join-Path $scanRoot "dense-long-running.db") `
        -Cancellation $cancelSource.Token
    Assert-True $cancelled.Cancelled `
        "A pre-cancelled scan did not return Cancelled."
    Assert-True (-not $cancelled.Success) `
        "A cancelled scan was incorrectly successful."
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host (
    "Performance scanner regression passed: six read-only fixtures, " +
    "validated Unit coverage, unsupported-schema fallback, 50,576-row " +
    "bounded aggregation, deterministic JSON, ranked evidence, " +
    "fingerprints, and cancellation.")
