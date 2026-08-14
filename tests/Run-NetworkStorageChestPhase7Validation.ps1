[CmdletBinding()]
param(
    [switch]$RequireGameLog
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$results=New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Name,[string]$State,[string]$Detail){$results.Add([pscustomobject]@{Name=$Name;State=$State;Detail=$Detail})}
function Run-Test([string]$Name,[string]$File){
    try{& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root ('tests\'+$File));if($LASTEXITCODE-ne0){throw "exit code $LASTEXITCODE"};Add-Result $Name 'PASS' $File}catch{Add-Result $Name 'FAIL' $_.Exception.Message}
}

Run-Test 'Phase 2 withdrawal/concurrency regression' 'NetworkStorageChestPhase2Regression.ps1'
Run-Test 'Phase 3 deposit/conservation regression' 'NetworkStorageChestPhase3Regression.ps1'
Run-Test 'Phase 4 wireless terminal regression' 'NetworkStorageChestPhase4Regression.ps1'
Run-Test 'Phase 5 GUI/localization regression' 'NetworkStorageChestPhase5Regression.ps1'
Run-Test 'Phase 6 adaptive installer regression' 'NetworkStorageChestPhase6Regression.ps1'
Run-Test 'Phase 7 automation/performance contract' 'NetworkStorageChestPhase7Regression.ps1'
Run-Test 'Wireless shared-consumer regression' 'WirelessVacuumPipePhase3Regression.ps1'
Run-Test 'Wireless atomic patch-service regression' 'WirelessVacuumPipePatchServiceRegression.ps1'
Run-Test 'Elevated helper boundary regression' 'CompanionBoundaryRegression.ps1'

$helper=Join-Path $root 'dist\ScrapLab.PatchHelper.exe'
foreach($entry in @(
    @{Name='Network Storage production status';Action='network-storage-chest'},
    @{Name='Wireless production status';Action='wireless-vacuum-pipe'},
    @{Name='Raid Detector shared-icon status';Action='raid-detector'}
)){
    try{$status=&$helper --status $entry.Action|ConvertFrom-Json;if(-not$status.Success-or-not$status.Installed-or-not$status.CanApply){throw ($status.CompatibilityState+' / '+$status.CompatibilityReason)};Add-Result $entry.Name 'PASS' ($status.CompatibilityState+' on '+$status.SteamBuildId)}catch{Add-Result $entry.Name 'FAIL' $_.Exception.Message}
}

$logRoots=New-Object System.Collections.Generic.List[string]
$receiptPath=Join-Path $root 'dist\phase7-backups\NetworkStorageChest\active.json'
if(Test-Path -LiteralPath $receiptPath){
    try{
        $phase7Receipt=Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8|ConvertFrom-Json
        if($phase7Receipt.GamePath){$logRoots.Add((Join-Path $phase7Receipt.GamePath 'Logs'))}
    }catch{}
}
$logRoots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Scrap Mechanic\Logs'))
$logRoots.Add((Join-Path $env:ProgramFiles 'Steam\steamapps\common\Scrap Mechanic\Logs'))
$logRoots.Add((Join-Path $env:APPDATA 'Axolot Games\Scrap Mechanic'))
$logs=@()
foreach($logRoot in ($logRoots|Select-Object -Unique)){
    if(Test-Path -LiteralPath $logRoot){
        $logs+=Get-ChildItem -LiteralPath $logRoot -Recurse -File -Filter 'game-*.log' -ErrorAction SilentlyContinue
    }
}
$log=$logs|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
if(-not$log){
    $missingLogState=if($RequireGameLog){'FAIL'}else{'SKIP'}
    Add-Result 'Phase 7 in-game summaries' $missingLogState 'No Scrap Mechanic game log was found.'
}
else{
    $text=Get-Content -LiteralPath $log.FullName -Raw -Encoding UTF8
    $all=[regex]::Matches($text,'\[ScrapLab Storage Phase 7\] ALL SUMMARY: (\d+) passed, (\d+) failed, (\d+) skipped\.')
    $local=[regex]::Matches($text,'\[ScrapLab Storage Phase 7\] LOCAL SUMMARY: (\d+) passed, (\d+) failed, (\d+) skipped\.')
    $wireless=[regex]::Matches($text,'\[ScrapLab Storage Phase 7\] WIRELESS SUMMARY: (\d+) passed, (\d+) failed, (\d+) skipped\.')
    $soak=[regex]::Matches($text,'\[ScrapLab Storage Phase 7\] SOAK SUMMARY: (\d+) passed, (\d+) failed, (\d+) skipped\.')
    $suite=$null
    if($all.Count){$suite=$all[$all.Count-1]}
    elseif($local.Count-and$wireless.Count){
        $localResult=$local[$local.Count-1];$wirelessResult=$wireless[$wireless.Count-1]
        $combinedPassed=[int]$localResult.Groups[1].Value+[int]$wirelessResult.Groups[1].Value
        $combinedFailed=[int]$localResult.Groups[2].Value+[int]$wirelessResult.Groups[2].Value
        $combinedSkipped=[int]$localResult.Groups[3].Value+[int]$wirelessResult.Groups[3].Value
        $suite=[pscustomobject]@{Groups=@($null,[pscustomobject]@{Value=[string]$combinedPassed},[pscustomobject]@{Value=[string]$combinedFailed},[pscustomobject]@{Value=[string]$combinedSkipped})}
    }
    if(-not$suite-or-not$soak.Count){
        $missingSummaryState=if($RequireGameLog){'FAIL'}else{'SKIP'}
        Add-Result 'Phase 7 in-game summaries' $missingSummaryState "Run /slstorage auto all and /slstorage soak, then close the game. Latest log: $($log.Name)"
    }
    else{
        $soakResult=$soak[$soak.Count-1]
        $passed=[int]$suite.Groups[1].Value;$failed=[int]$suite.Groups[2].Value;$skipped=[int]$suite.Groups[3].Value
        $soakPassed=[int]$soakResult.Groups[1].Value;$soakFailed=[int]$soakResult.Groups[2].Value;$soakSkipped=[int]$soakResult.Groups[3].Value
        if($passed-ge50-and$failed-eq0-and$soakPassed-ge8-and$soakFailed-eq0){Add-Result 'Phase 7 in-game summaries' 'PASS' "suite=$passed/0/$skipped, soak=$soakPassed/0/$soakSkipped in $($log.Name)"}
        else{Add-Result 'Phase 7 in-game summaries' 'FAIL' "suite=$passed/$failed/$skipped, soak=$soakPassed/$soakFailed/$soakSkipped in $($log.Name)"}
        $marker=$text.LastIndexOf('[ScrapLab Storage Phase 7] release qualification ready')
        $runText=if($marker-ge0){$text.Substring($marker)}else{$text}
        $bad=@($runText -split "`r?`n"|Where-Object{$_-match'\[ScrapLab' -and $_-match'(runtime error|stack traceback|invalid script reference|FAIL )'})
        if($bad.Count){Add-Result 'Phase 7 ScrapLab log error scan' 'FAIL' ($bad -join ' | ')}else{Add-Result 'Phase 7 ScrapLab log error scan' 'PASS' 'No ScrapLab runtime, traceback, invalid-reference, or failed-case lines.'}
    }
}

$executables=Get-ChildItem -LiteralPath (Join-Path $root 'dist') -File -Filter '*.exe'
foreach($exe in $executables){
    $executableSizeState=if($exe.Length-lt8MB){'PASS'}else{'FAIL'}
    Add-Result ("Executable size: "+$exe.Name) $executableSizeState ("{0:N0} bytes"-f$exe.Length)
}
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'release') -File -Filter 'ScrapLab-*.zip'|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1
if($zip){
    $packageSizeState=if($zip.Length-lt8MB){'PASS'}else{'FAIL'}
    Add-Result 'Portable package size' $packageSizeState ("$($zip.Name) = {0:N0} bytes"-f$zip.Length)
}else{Add-Result 'Portable package size' 'FAIL' 'No release ZIP exists.'}

$results|Format-Table -AutoSize|Out-String -Width 240
$failed=@($results|Where-Object { $_.State -eq 'FAIL' })
if($failed.Count){throw "Phase 7 validation failed $($failed.Count) check(s)."}
if($RequireGameLog -and @($results|Where-Object { $_.State -eq 'SKIP' }).Count){throw 'Phase 7 validation still contains skipped checks.'}
Write-Host 'Network Storage Chest Phase 7 desktop validation passed.'
