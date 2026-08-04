param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$LogRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic\Logs'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$results = [Collections.Generic.List[object]]::new()
function Add-Result([string]$Name,[string]$Outcome,[string]$Detail){$results.Add([pscustomobject]@{Name=$Name;Outcome=$Outcome;Detail=$Detail})}

foreach($phase in 1..4){
    $test=Join-Path $root "tests\WirelessVacuumPipePhase${phase}Regression.ps1"
    try{$output=(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $test 2>&1|Out-String).Trim();if($LASTEXITCODE-ne 0){throw $output};Add-Result "Phase $phase source regression" 'PASS' (($output-split "`r?`n")[-1])}
    catch{Add-Result "Phase $phase source regression" 'FAIL' $_.Exception.Message}
}

try{
    $status=(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\experiments\Manage-WirelessVacuumPipePhase4.ps1') -Action Status -GameRoot $GameRoot|ConvertFrom-Json)
    if($status.State-ne'INSTALLED'){throw "state=$($status.State)"}
    Add-Result 'Installed Phase 4 definition' 'PASS' "definition $($status.DefinitionVersion) is receipt-verified"
}catch{Add-Result 'Installed Phase 4 definition' 'FAIL' $_.Exception.Message}

$log=Get-ChildItem -LiteralPath $LogRoot -Filter 'game-*.log'|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
if(-not $log){Add-Result 'Latest in-game automatic run' 'FAIL' 'no Scrap Mechanic game log exists'}
else{
    $text=Get-Content -LiteralPath $log.FullName -Raw
    $matches=[regex]::Matches($text,'\[ScrapLab Pipe Phase 4\] summary=(\d+) passed, (\d+) failed, (\d+) skipped\.')
    if($matches.Count-eq 0){Add-Result 'Latest in-game automatic run' 'FAIL' "run /slpipe4 auto, wait for its summary, then close the game ($($log.Name))"}
    else{
        $match=$matches[$matches.Count-1];$passed=[int]$match.Groups[1].Value;$failed=[int]$match.Groups[2].Value;$skipped=[int]$match.Groups[3].Value
        if($passed-ge 9 -and $failed-eq 0){Add-Result 'Latest in-game automatic run' 'PASS' "$passed passed, $failed failed, $skipped skipped in $($log.Name)"}
        else{Add-Result 'Latest in-game automatic run' 'FAIL' "$passed passed, $failed failed, $skipped skipped in $($log.Name)"}
    }
    $danger=[regex]::Matches($text,'(?im)^.*(?:WirelessPipeTransfer\.lua:|WirelessVacuumPipePhase4Harness\.lua:|\[ScrapLab Pipe Transfer\].*(?:failed|error)).*$')
    if($danger.Count-eq 0){Add-Result 'Directional runtime safety log scan' 'PASS' $log.Name}
    else{Add-Result 'Directional runtime safety log scan' 'FAIL' (($danger|ForEach-Object Value|Select-Object -Unique)-join ' | ')}
}

''
'ScrapLab Wireless Vacuum Pipe Phase 4 validation'
'------------------------------------------------'
foreach($result in $results){"[$($result.Outcome)] $($result.Name) - $($result.Detail)"}
$passedCount=@($results|Where-Object Outcome -eq 'PASS').Count
$failedCount=@($results|Where-Object Outcome -eq 'FAIL').Count
$skippedCount=@($results|Where-Object Outcome -eq 'SKIP').Count
"Summary: $passedCount passed, $failedCount failed, $skippedCount skipped."
if($failedCount-gt 0){exit 1}
