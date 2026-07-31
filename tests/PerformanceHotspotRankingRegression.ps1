param(
    [string]$RaidRescueExe
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

$resolvedExe = [IO.Path]::GetFullPath($RaidRescueExe)
Assert-True (Test-Path -LiteralPath $resolvedExe) `
    "Build ScrapLab.exe before running the ranking regression."

$assembly = [Reflection.Assembly]::LoadFrom($resolvedExe)
$resultType = $assembly.GetType(
    "RaidRescue.PerformanceScanResult", $true)
$worldType = $assembly.GetType(
    "RaidRescue.PerformanceWorldSummary", $true)
$cellType = $assembly.GetType(
    "RaidRescue.PerformanceCellSummary", $true)
$hotspotType = $assembly.GetType(
    "RaidRescue.PerformanceCellHotspot", $true)
$categoryType = $assembly.GetType(
    "RaidRescue.PerformanceCategoryMetric", $true)
$largestType = $assembly.GetType(
    "RaidRescue.PerformanceLargestRecord", $true)
$evidenceType = $assembly.GetType(
    "RaidRescue.PerformanceEvidence", $true)
$rankerType = $assembly.GetType(
    "RaidRescue.PerformanceHotspotRanker", $true)
$rankMethod = $rankerType.GetMethod(
    "Rank",
    [Reflection.BindingFlags]"Public,Static")
Assert-True ($null -ne $rankMethod) `
    "PerformanceHotspotRanker.Rank was not found."

function New-GenericList {
    param([Type]$ElementType)
    $openList = [Collections.Generic.List[int]].GetGenericTypeDefinition()
    $list = [Activator]::CreateInstance(
        $openList.MakeGenericType(@($ElementType)))
    return ,$list
}

function New-RankingResult {
    $result = [Activator]::CreateInstance($resultType)
    $result.Worlds = New-GenericList $worldType
    $result.Cells = New-GenericList $cellType
    $result.Hotspots = New-GenericList $hotspotType
    $result.Categories = New-GenericList $categoryType
    $result.LargestRecords = New-GenericList $largestType
    $result.Warnings = [Collections.Generic.List[string]]::new()
    return $result
}

function Add-World {
    param(
        [object]$Result,
        [int]$WorldId = 1,
        [string]$Name = "Overworld"
    )
    $world = [Activator]::CreateInstance($worldType)
    $world.WorldId = $WorldId
    $world.WorldName = $Name
    $Result.Worlds.Add($world)
}

function Add-Cell {
    param(
        [object]$Result,
        [int]$X,
        [int]$Y,
        [long]$Records,
        [long]$Bytes,
        [int]$WorldId = 1,
        [string]$WorldName = "Overworld"
    )
    $cell = [Activator]::CreateInstance($cellType)
    $cell.WorldId = $WorldId
    $cell.WorldName = $WorldName
    $cell.CellX = $X
    $cell.CellY = $Y
    $cell.ApproximateCenterX = ($Y * 64.0) + 32.0
    $cell.ApproximateCenterY = ($X * 64.0) + 32.0
    $cell.TotalRecords = $Records
    $cell.TotalPayloadBytes = $Bytes
    $Result.Cells.Add($cell)
}

function Invoke-Rank {
    param([object]$Result)
    $arguments = New-Object object[] 2
    $arguments[0] = $Result
    $arguments[1] = [Threading.CancellationToken]::None
    $rankMethod.Invoke($null, $arguments) | Out-Null
}

# One dense center and one dense neighbor prove 3-by-3 aggregation,
# deterministic center selection, overlap suppression, severity, evidence,
# confidence, and coordinate conversion.
$cluster = New-RankingResult
Add-World $cluster
Add-Cell $cluster 0 0 500 (1024 * 1024)
Add-Cell $cluster 1 0 250 (256 * 1024)
Invoke-Rank $cluster
Assert-True ($cluster.Hotspots.Count -eq 1) `
    "Overlapping neighborhoods were not collapsed."
$top = $cluster.Hotspots[0]
Assert-True ($top.CellX -eq 0 -and $top.CellY -eq 0) `
    "The strongest center cell was not selected deterministically."
Assert-True ($top.CenterRecords -eq 500) `
    "The center-cell total was not retained."
Assert-True ($top.NeighborhoodRecords -eq 750) `
    "The 3-by-3 record total was incorrect."
Assert-True (
    $top.NeighborhoodPayloadBytes -eq (1280 * 1024)) `
    "The 3-by-3 payload total was incorrect."
Assert-True ($top.NeighborhoodPopulatedCells -eq 2) `
    "The populated-neighbor total was incorrect."
Assert-True ($top.Severity -eq "VERY HEAVY") `
    "The calibrated multi-signal cluster was not VERY HEAVY."
Assert-True ($top.Confidence -eq "HIGH") `
    "A fully decoded Harvestable hotspot was not HIGH confidence."
Assert-True ($top.Categories.Count -eq 1) `
    "The hotspot category breakdown was missing."
Assert-True ($top.Categories[0].RecordCount -eq 750) `
    "The hotspot category did not use neighborhood totals."
Assert-True (
    $top.ApproximateCenter.X -eq 32.0 -and
    $top.ApproximateCenter.Y -eq 32.0) `
    "The proven world-center conversion was incorrect."
Assert-True ($top.Evidence.Count -ge 3) `
    "The VERY HEAVY label did not contain sufficient evidence."
foreach ($evidence in $top.Evidence) {
    Assert-True (
        -not [String]::IsNullOrWhiteSpace($evidence.Key) -and
        -not [String]::IsNullOrWhiteSpace($evidence.Label) -and
        -not [String]::IsNullOrWhiteSpace($evidence.Explanation)) `
        "A serialized evidence statement was incomplete."
    Assert-True (
        $evidence.ObservedValue -ge $evidence.ComparisonValue) `
        "Displayed evidence did not meet its serialized threshold."
}

# The policy's three labels are calibrated independently.
$notable = New-RankingResult
Add-World $notable
Add-Cell $notable 0 0 24 (24 * 64)
Invoke-Rank $notable
Assert-True (
    $notable.Hotspots.Count -eq 1 -and
    $notable.Hotspots[0].Severity -eq "NOTABLE") `
    "The minimum conservative evidence floor was not NOTABLE."

$heavy = New-RankingResult
Add-World $heavy
Add-Cell $heavy 0 0 500 (500 * 64)
Invoke-Rank $heavy
Assert-True (
    $heavy.Hotspots.Count -eq 1 -and
    $heavy.Hotspots[0].Severity -eq "HEAVY") `
    "One strong absolute signal was not HEAVY."

# A single large 64-bit count must survive aggregation without overflow.
$large = New-RankingResult
Add-World $large
$largeCount = [long][int]::MaxValue + 12345L
Add-Cell $large 0 0 $largeCount (2L * 1024L * 1024L)
Invoke-Rank $large
Assert-True (
    $large.Hotspots[0].NeighborhoodRecords -eq $largeCount) `
    "A 64-bit record total was truncated."

# More than 50 separated candidates prove the output cap and tie-break order.
$capped = New-RankingResult
Add-World $capped
for ($index = 0; $index -lt 60; $index++) {
    Add-Cell $capped ($index * 4) 0 500 (500 * 64)
}
Invoke-Rank $capped
Assert-True ($capped.Hotspots.Count -eq 50) `
    "The result exceeded or missed the 50-card cap."
for ($index = 0; $index -lt $capped.Hotspots.Count; $index++) {
    $hotspot = $capped.Hotspots[$index]
    Assert-True ($hotspot.Rank -eq ($index + 1)) `
        "Global ranks were not sequential."
    Assert-True ($hotspot.WorldRank -eq ($index + 1)) `
        "Per-world ranks were not sequential."
    Assert-True ($hotspot.CellX -eq ($index * 4)) `
        "Equal candidates did not use stable coordinate tie-breaking."
}

# A repeated run must be byte-for-byte stable in all ranked fields.
$firstSnapshot = @(
    $capped.Hotspots |
    ForEach-Object {
        "$($_.Rank)|$($_.WorldRank)|$($_.WorldId)|" +
        "$($_.CellX)|$($_.CellY)|$($_.Severity)|" +
        "$($_.Percentile)|$($_.NeighborhoodRecords)"
    }
) -join "`n"
Invoke-Rank $capped
$secondSnapshot = @(
    $capped.Hotspots |
    ForEach-Object {
        "$($_.Rank)|$($_.WorldRank)|$($_.WorldId)|" +
        "$($_.CellX)|$($_.CellY)|$($_.Severity)|" +
        "$($_.Percentile)|$($_.NeighborhoodRecords)"
    }
) -join "`n"
Assert-True ($firstSnapshot -ceq $secondSnapshot) `
    "Repeated ranking was not deterministic."

Write-Host (
    "Performance hotspot ranking regression passed: 3-by-3 totals, " +
    "overlap suppression, deterministic ordering, three severities, " +
    "serialized evidence, confidence, coordinates, 64-bit counts, " +
    "and the 50-card cap.")
