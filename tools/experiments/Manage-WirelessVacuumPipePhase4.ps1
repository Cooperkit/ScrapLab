param(
    [ValidateSet('Status','Install','Update','Remove')]
    [string]$Action = 'Status',
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$ReceiptRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development State'),
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development Backups\WirelessVacuumPipePhase4'),
    [switch]$AllowRunningGame
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$receiptPath = Join-Path $ReceiptRoot 'WirelessVacuumPipePhase4.json'
$phase2ReceiptPath = Join-Path $ReceiptRoot 'WirelessVacuumPipePhase2.json'
$phase3ReceiptPath = Join-Path $ReceiptRoot 'WirelessVacuumPipePhase3.json'
$cacheRelative = 'Cache\Bundle\core_data.cbo'
$markerStart = '-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 4 HARNESS'
$markerEnd = '-- END SCRAPLAB WIRELESS VACUUM PIPE PHASE 4 HARNESS'
$harnessDofile = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Experiments/WirelessVacuumPipePhase4/WirelessVacuumPipePhase4Harness.lua" )'
$definitionVersion = 3

$files = @(
    [pscustomobject]@{ Kind='Phase2Owned'; Relative='Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'; Source=Join-Path $repoRoot 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua' },
    [pscustomobject]@{ Kind='Phase3Owned'; Relative='Survival\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'; Source=Join-Path $repoRoot 'source\Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua' },
    [pscustomobject]@{ Kind='Phase2Owned'; Relative='Survival\Scripts\ScrapLab\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua'; Source=Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua' },
    [pscustomobject]@{ Kind='Owned'; Relative='Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeTransfer.lua'; Source=Join-Path $repoRoot 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeTransfer.lua' },
    [pscustomobject]@{ Kind='Owned'; Relative='Survival\Scripts\ScrapLab\Experiments\WirelessVacuumPipePhase4\WirelessVacuumPipePhase4Harness.lua'; Source=Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipePhase4Harness.lua' }
)
$gameRelative = 'Survival\Scripts\game\SurvivalGame.lua'

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Get-BytesSha256([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '') } finally { $sha.Dispose() }
}
function Get-Count([string]$Text,[string]$Needle) {
    $count=0; $offset=0
    while (($offset=$Text.IndexOf($Needle,$offset,[StringComparison]::Ordinal)) -ge 0) { $count++; $offset += $Needle.Length }
    $count
}
function Get-Utf8Document([string]$Path) {
    [byte[]]$bytes=[IO.File]::ReadAllBytes($Path)
    $bom=$bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset=if($bom){3}else{0}; $text=[Text.UTF8Encoding]::new($false,$true).GetString($bytes,$offset,$bytes.Length-$offset)
    $crlf=$text.Contains("`r`n"); $bareLf=[regex]::IsMatch($text,'(?<!\r)\n')
    if($crlf -and $bareLf){throw "Mixed newlines are unsupported: $Path"}
    [pscustomobject]@{Text=$text;HasBom=$bom;Newline=if($crlf){"`r`n"}else{"`n"}}
}
function ConvertTo-Utf8Bytes([string]$Text,[bool]$HasBom) {
    [byte[]]$payload=[Text.UTF8Encoding]::new($false).GetBytes($Text)
    if(-not $HasBom){return $payload}
    [byte[]]$result=New-Object byte[] ($payload.Length+3); $result[0]=0xEF;$result[1]=0xBB;$result[2]=0xBF
    [Array]::Copy($payload,0,$result,3,$payload.Length); $result
}
function Write-AtomicBytes([string]$Path,[byte[]]$Bytes) {
    $directory=Split-Path -Parent $Path; [IO.Directory]::CreateDirectory($directory)|Out-Null
    $temporary=Join-Path $directory ('.scraplab-phase4-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $replaceBackup=Join-Path $directory ('.scraplab-phase4-'+[Guid]::NewGuid().ToString('N')+'.bak')
    try {
        [IO.File]::WriteAllBytes($temporary,$Bytes)
        if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temporary,$Path,$replaceBackup);Remove-Item -LiteralPath $replaceBackup -Force}else{[IO.File]::Move($temporary,$Path)}
    } finally {
        if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}
        if(Test-Path -LiteralPath $replaceBackup){Remove-Item -LiteralPath $replaceBackup -Force}
    }
}
function Write-Json([string]$Path,[object]$Value) { Write-AtomicBytes $Path ([Text.UTF8Encoding]::new($false).GetBytes(($Value|ConvertTo-Json -Depth 12))) }
function Assert-GameClosed { if(-not $AllowRunningGame -and (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue)){throw 'Scrap Mechanic is running. Close it before changing Phase 4.'} }
function Get-GameBlock([string]$Nl) { $markerStart+$Nl+$harnessDofile+$Nl+$markerEnd }

function Get-Status {
    $details=[ordered]@{}; $signals=0; $expected=$files.Count+1
    foreach($file in $files){
        $path=Join-Path $GameRoot $file.Relative
        $same=(Test-Path -LiteralPath $path) -and (Get-Sha256 $path) -eq (Get-Sha256 $file.Source)
        $details[$file.Relative]=if($same){'PRESENT'}elseif(Test-Path -LiteralPath $path){'CHANGED'}else{'ABSENT'}
        if($same){$signals++}
    }
    $gamePath=Join-Path $GameRoot $gameRelative
    if(Test-Path -LiteralPath $gamePath){
        $text=(Get-Utf8Document $gamePath).Text
        $present=(Get-Count $text $markerStart)-eq 1 -and (Get-Count $text $harnessDofile)-eq 1
        $details[$gameRelative]=if($present){'PRESENT'}else{'ABSENT'};if($present){$signals++}
    }else{$details[$gameRelative]='MISSING'}
    $state=if($signals -eq 0){'NOT_INSTALLED'}elseif($signals -eq $expected){'INSTALLED'}else{'PARTIAL_OR_CONFLICT'}
    [pscustomobject]@{State=$state;Installed=$state -eq 'INSTALLED';DefinitionVersion=$definitionVersion;GameRoot=$GameRoot;Receipt=$receiptPath;Details=$details}
}

function Get-Phase2Receipt {
    if(-not(Test-Path -LiteralPath $phase2ReceiptPath)){throw 'Phase 2 receipt is missing.'}
    Get-Content -LiteralPath $phase2ReceiptPath -Raw|ConvertFrom-Json
}
function Get-Phase3Receipt {
    if(-not(Test-Path -LiteralPath $phase3ReceiptPath)){throw 'Phase 3 receipt is missing.'}
    Get-Content -LiteralPath $phase3ReceiptPath -Raw|ConvertFrom-Json
}
function Assert-Phase2OwnedMatchesReceipt([object]$Receipt) {
    foreach($file in $files|Where-Object Kind -eq 'Phase2Owned'){
        $entry=$Receipt.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1
        $path=Join-Path $GameRoot $file.Relative
        if(-not $entry -or -not(Test-Path -LiteralPath $path) -or (Get-Sha256 $path)-ne $entry.InstalledHash){throw "Phase 2 owned file is not receipt-verified: $($file.Relative)"}
    }
}
function Assert-Phase3OwnedMatchesReceipt([object]$Receipt) {
    foreach($file in $files|Where-Object Kind -eq 'Phase3Owned'){
        $entry=$Receipt.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1
        $path=Join-Path $GameRoot $file.Relative
        if(-not $entry -or -not(Test-Path -LiteralPath $path) -or (Get-Sha256 $path)-ne $entry.InstalledHash){throw "Phase 3 owned file is not receipt-verified: $($file.Relative)"}
    }
}
function Assert-Phase3InstalledMatchesReceipt([object]$Receipt) {
    foreach($entry in $Receipt.Files){
        $path=Join-Path $GameRoot $entry.Relative
        if(-not(Test-Path -LiteralPath $path) -or (Get-Sha256 $path)-ne $entry.InstalledHash){throw "Phase 3 must be installed before Phase 4: $($entry.Relative)"}
    }
}
function New-Plan([string]$Kind,[string]$Relative,[byte[]]$Output) {
    $path=Join-Path $GameRoot $Relative
    [pscustomobject]@{Kind=$Kind;Relative=$Relative;Path=$path;OriginalBytes=if(Test-Path -LiteralPath $path){[IO.File]::ReadAllBytes($path)}else{$null};OutputBytes=$Output;OutputHash=Get-BytesSha256 $Output}
}

function Install-Phase4 {
    Assert-GameClosed
    $status=Get-Status;if($status.State -eq 'INSTALLED'){return $status};if($status.State -ne 'NOT_INSTALLED'){throw "Install blocked by state $($status.State)."}
    if(Test-Path -LiteralPath $receiptPath){throw 'A stale Phase 4 receipt exists.'}
    $phase2=Get-Phase2Receipt;Assert-Phase2OwnedMatchesReceipt $phase2
    $phase3=Get-Phase3Receipt;Assert-Phase3InstalledMatchesReceipt $phase3
    $plans=@()
    foreach($file in $files){
        if(-not(Test-Path -LiteralPath $file.Source)){throw "Source file missing: $($file.Source)"}
        $target=Join-Path $GameRoot $file.Relative
        if($file.Kind -eq 'Owned' -and (Test-Path -LiteralPath $target)){throw "Owned Phase 4 target already exists: $target"}
        $plans+=New-Plan $file.Kind $file.Relative ([IO.File]::ReadAllBytes($file.Source))
    }
    $gamePath=Join-Path $GameRoot $gameRelative;$doc=Get-Utf8Document $gamePath
    if((Get-Count $doc.Text $markerStart)-ne 0 -or (Get-Count $doc.Text $harnessDofile)-ne 0){throw 'Phase 4 SurvivalGame registration already exists.'}
    $text=$doc.Text;if(-not $text.EndsWith($doc.Newline,[StringComparison]::Ordinal)){$text+=$doc.Newline};$text+=(Get-GameBlock $doc.Newline)+$doc.Newline
    $plans+=New-Plan 'Game' $gameRelative (ConvertTo-Utf8Bytes $text $doc.HasBom)

    $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff');$backupDirectory=Join-Path $BackupRoot $stamp;[IO.Directory]::CreateDirectory($backupDirectory)|Out-Null
    $phase2Backup=Join-Path $backupDirectory 'WirelessVacuumPipePhase2.json';[IO.File]::Copy($phase2ReceiptPath,$phase2Backup)
    $phase3Backup=Join-Path $backupDirectory 'WirelessVacuumPipePhase3.json';[IO.File]::Copy($phase3ReceiptPath,$phase3Backup)
    $receiptFiles=@();$written=@()
    try{
        foreach($plan in $plans){
            $backup=$null;$originalHash=$null
            if($null -ne $plan.OriginalBytes){$backup=Join-Path $backupDirectory ($plan.Relative-replace '[\\/:*?"<>|]','_');[IO.File]::WriteAllBytes($backup,$plan.OriginalBytes);$originalHash=Get-BytesSha256 $plan.OriginalBytes;if((Get-Sha256 $backup)-ne $originalHash){throw "Backup verification failed: $($plan.Relative)"}}
            Write-AtomicBytes $plan.Path $plan.OutputBytes;if((Get-Sha256 $plan.Path)-ne $plan.OutputHash){throw "Output verification failed: $($plan.Relative)"};$written+=$plan
            $receiptFiles+=[pscustomobject]@{Kind=$plan.Kind;Relative=$plan.Relative;OriginalHash=$originalHash;InstalledHash=$plan.OutputHash;BackupPath=$backup}
        }
        foreach($file in $files|Where-Object Kind -eq 'Phase2Owned'){$entry=$phase2.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1;$entry.InstalledHash=Get-Sha256 (Join-Path $GameRoot $file.Relative)}
        foreach($file in $files|Where-Object Kind -eq 'Phase3Owned'){$entry=$phase3.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1;$entry.InstalledHash=Get-Sha256 (Join-Path $GameRoot $file.Relative)}
        $phase2|Add-Member -NotePropertyName UpdatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force;Write-Json $phase2ReceiptPath $phase2
        $phase3|Add-Member -NotePropertyName UpdatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force;Write-Json $phase3ReceiptPath $phase3
        [IO.Directory]::CreateDirectory($ReceiptRoot)|Out-Null
        Write-Json $receiptPath ([pscustomobject]@{SchemaVersion=1;DefinitionVersion=$definitionVersion;InstalledUtc=[DateTime]::UtcNow.ToString('o');Phase2ReceiptBackup=$phase2Backup;Phase3ReceiptBackup=$phase3Backup;Files=$receiptFiles})
        $cache=Join-Path $GameRoot $cacheRelative;if(Test-Path -LiteralPath $cache){Remove-Item -LiteralPath $cache -Force}
    }catch{
        for($i=$written.Count-1;$i-ge 0;$i--){$plan=$written[$i];if($null-ne $plan.OriginalBytes){Write-AtomicBytes $plan.Path $plan.OriginalBytes}elseif(Test-Path -LiteralPath $plan.Path){Remove-Item -LiteralPath $plan.Path -Force}}
        if(Test-Path -LiteralPath $phase2Backup){[IO.File]::Copy($phase2Backup,$phase2ReceiptPath,$true)}
        if(Test-Path -LiteralPath $phase3Backup){[IO.File]::Copy($phase3Backup,$phase3ReceiptPath,$true)}
        if(Test-Path -LiteralPath $receiptPath){Remove-Item -LiteralPath $receiptPath -Force};throw
    }
    Get-Status
}

function Update-Phase4 {
    Assert-GameClosed
    if(-not(Test-Path -LiteralPath $receiptPath)){throw 'Phase 4 receipt is missing.'}
    [byte[]]$receiptOriginalBytes=[IO.File]::ReadAllBytes($receiptPath)
    [byte[]]$phase2OriginalBytes=[IO.File]::ReadAllBytes($phase2ReceiptPath)
    [byte[]]$phase3OriginalBytes=[IO.File]::ReadAllBytes($phase3ReceiptPath)
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json;$phase2=Get-Phase2Receipt;$phase3=Get-Phase3Receipt
    foreach($entry in $receipt.Files){$path=Join-Path $GameRoot $entry.Relative;if(-not(Test-Path -LiteralPath $path) -or (Get-Sha256 $path)-ne $entry.InstalledHash){throw "Installed Phase 4 file changed: $($entry.Relative)"}}
    Assert-Phase2OwnedMatchesReceipt $phase2
    Assert-Phase3OwnedMatchesReceipt $phase3
    $updateBackupDirectory=Join-Path $BackupRoot ('update-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
    $plans=@()
    foreach($file in $files){
        $path=Join-Path $GameRoot $file.Relative
        if(-not(Test-Path -LiteralPath $path)){throw "Phase 4 update target is missing: $($file.Relative)"}
        [byte[]]$output=[IO.File]::ReadAllBytes($file.Source);$hash=Get-BytesSha256 $output
        if((Get-Sha256 $path)-ne $hash){
            $plan=New-Plan $file.Kind $file.Relative $output
            $existingEntry=$receipt.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1
            if(-not $existingEntry){
                [IO.Directory]::CreateDirectory($updateBackupDirectory)|Out-Null
                $backup=Join-Path $updateBackupDirectory ($file.Relative-replace '[\\/:*?"<>|]','_')
                [IO.File]::WriteAllBytes($backup,$plan.OriginalBytes)
                $originalHash=Get-BytesSha256 $plan.OriginalBytes
                if((Get-Sha256 $backup)-ne $originalHash){throw "Update backup verification failed: $($file.Relative)"}
                $plan|Add-Member -NotePropertyName NewReceiptEntry -NotePropertyValue ([pscustomobject]@{Kind=$file.Kind;Relative=$file.Relative;OriginalHash=$originalHash;InstalledHash=$plan.OutputHash;BackupPath=$backup})
            }
            $plans+=$plan
        }
    }
    $written=@()
    try{
        if(-not $receipt.Phase3ReceiptBackup){
            [IO.Directory]::CreateDirectory($updateBackupDirectory)|Out-Null
            $phase3Backup=Join-Path $updateBackupDirectory 'WirelessVacuumPipePhase3.json'
            [IO.File]::WriteAllBytes($phase3Backup,$phase3OriginalBytes)
            if((Get-Sha256 $phase3Backup)-ne(Get-BytesSha256 $phase3OriginalBytes)){throw 'Phase 3 receipt backup verification failed.'}
            $receipt|Add-Member -NotePropertyName Phase3ReceiptBackup -NotePropertyValue $phase3Backup -Force
        }
        foreach($plan in $plans){
            Write-AtomicBytes $plan.Path $plan.OutputBytes
            if((Get-Sha256 $plan.Path)-ne $plan.OutputHash){throw "Update verification failed: $($plan.Relative)"}
            $written+=$plan
            $entry=$receipt.Files|Where-Object Relative -eq $plan.Relative|Select-Object -First 1
            if($entry){$entry.InstalledHash=$plan.OutputHash}else{$receipt.Files=@($receipt.Files)+$plan.NewReceiptEntry}
        }
        foreach($file in $files|Where-Object Kind -eq 'Phase2Owned'){$entry=$phase2.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1;$entry.InstalledHash=Get-Sha256 (Join-Path $GameRoot $file.Relative)}
        foreach($file in $files|Where-Object Kind -eq 'Phase3Owned'){$entry=$phase3.Files|Where-Object Relative -eq $file.Relative|Select-Object -First 1;$entry.InstalledHash=Get-Sha256 (Join-Path $GameRoot $file.Relative)}
        $receipt.DefinitionVersion=$definitionVersion
        $receipt|Add-Member -NotePropertyName UpdatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force;Write-Json $receiptPath $receipt
        $phase2|Add-Member -NotePropertyName UpdatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force;Write-Json $phase2ReceiptPath $phase2
        $phase3|Add-Member -NotePropertyName UpdatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force;Write-Json $phase3ReceiptPath $phase3
        if($plans.Count-gt 0){$cache=Join-Path $GameRoot $cacheRelative;if(Test-Path -LiteralPath $cache){Remove-Item -LiteralPath $cache -Force}}
    }catch{
        for($i=$written.Count-1;$i-ge 0;$i--){Write-AtomicBytes $written[$i].Path $written[$i].OriginalBytes}
        Write-AtomicBytes $receiptPath $receiptOriginalBytes
        Write-AtomicBytes $phase2ReceiptPath $phase2OriginalBytes
        Write-AtomicBytes $phase3ReceiptPath $phase3OriginalBytes
        throw
    }
    Get-Status
}

function Remove-Phase4 {
    Assert-GameClosed
    $status=Get-Status;if($status.State -eq 'NOT_INSTALLED'){return $status};if($status.State-ne'INSTALLED'){throw "Removal blocked by state $($status.State)."}
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
    foreach($entry in $receipt.Files){$path=Join-Path $GameRoot $entry.Relative;if((Get-Sha256 $path)-ne $entry.InstalledHash){throw "Removal blocked; installed file changed: $($entry.Relative)"}}
    $written=@()
    try{
        foreach($entry in $receipt.Files){$path=Join-Path $GameRoot $entry.Relative;$current=[IO.File]::ReadAllBytes($path);if($entry.Kind-eq'Owned'){Remove-Item -LiteralPath $path -Force}else{if(-not(Test-Path -LiteralPath $entry.BackupPath) -or (Get-Sha256 $entry.BackupPath)-ne $entry.OriginalHash){throw "Backup verification failed: $($entry.Relative)"};Write-AtomicBytes $path ([IO.File]::ReadAllBytes($entry.BackupPath))};$written+=[pscustomobject]@{Path=$path;Bytes=$current}}
        if(-not(Test-Path -LiteralPath $receipt.Phase2ReceiptBackup)){throw 'Phase 2 receipt backup is missing.'};[IO.File]::Copy($receipt.Phase2ReceiptBackup,$phase2ReceiptPath,$true)
        if($receipt.Phase3ReceiptBackup){if(-not(Test-Path -LiteralPath $receipt.Phase3ReceiptBackup)){throw 'Phase 3 receipt backup is missing.'};[IO.File]::Copy($receipt.Phase3ReceiptBackup,$phase3ReceiptPath,$true)}
        Remove-Item -LiteralPath $receiptPath -Force;$cache=Join-Path $GameRoot $cacheRelative;if(Test-Path -LiteralPath $cache){Remove-Item -LiteralPath $cache -Force}
    }catch{foreach($entry in $written){Write-AtomicBytes $entry.Path $entry.Bytes};throw}
    Get-Status
}

$result=switch($Action){'Install'{Install-Phase4};'Update'{Update-Phase4};'Remove'{Remove-Phase4};default{Get-Status}}
$result|ConvertTo-Json -Depth 10
