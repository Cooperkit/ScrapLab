param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$logDirectory = Join-Path $GameRoot 'Logs'
if (-not (Test-Path -LiteralPath $logDirectory)) { throw "Game log directory was not found: $logDirectory" }
$log = Get-ChildItem -LiteralPath $logDirectory -File -Filter 'game-*.log' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $log) { throw 'No Scrap Mechanic game log was found.' }

$matches = Select-String -LiteralPath $log.FullName -SimpleMatch '[ScrapLab Pipe Phase 1]'
$pass = @($matches | Where-Object { $_.Line -match '\bPASS\b' })
$fail = @($matches | Where-Object { $_.Line -match '\bFAIL\b' })

[pscustomobject]@{
    Log = $log.FullName
    LastWriteTimeUtc = $log.LastWriteTimeUtc
    ProbeLines = @($matches).Count
    PassLines = $pass.Count
    FailLines = $fail.Count
    Lines = @($matches | ForEach-Object { $_.Line.Trim() })
} | ConvertTo-Json -Depth 4
