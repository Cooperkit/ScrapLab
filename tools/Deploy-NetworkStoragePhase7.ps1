[CmdletBinding()]
param(
    [ValidateSet('Install','Uninstall')]
    [string]$Action = 'Install',
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sourceRoot = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest'
$helperPath = Join-Path $kitRoot 'dist\ScrapLab.PatchHelper.exe'
$stateRoot = Join-Path $kitRoot 'dist\phase7-backups\NetworkStorageChest'
$receiptPath = Join-Path $stateRoot 'active.json'
$survivalRelative = 'Survival\Scripts\game\SurvivalGame.lua'
$survivalPath = Join-Path $GamePath $survivalRelative
$loaderStart = '-- SCRAPLAB NETWORK STORAGE CHEST PHASE 7 QUALIFICATION'
$loaderEnd = '-- END SCRAPLAB NETWORK STORAGE CHEST PHASE 7 QUALIFICATION'
$harnesses = @(
    'NetworkStorageChestPhase2Harness.lua',
    'NetworkStorageChestPhase3Harness.lua',
    'NetworkStorageChestPhase4Harness.lua',
    'NetworkStorageChestPhase5Harness.lua',
    'NetworkStorageChestPhase7Harness.lua'
)

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Get-TextState([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if($bom){3}else{0}
    $text = [Text.Encoding]::UTF8.GetString($bytes,$offset,$bytes.Length-$offset)
    if($text.Contains("`r`n") -and $text.Replace("`r`n",'').Contains("`n")){throw "Mixed newlines are unsupported: $Path"}
    [pscustomobject]@{Text=$text;HasBom=$bom;Newline=if($text.Contains("`r`n")){"`r`n"}else{"`n"}}
}
function Write-AtomicBytes([string]$Path,[byte[]]$Bytes) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path))|Out-Null
    $temporary=$Path+'.scraplab-phase7-'+[guid]::NewGuid().ToString('N')+'.tmp'
    $swap=$Path+'.scraplab-phase7-'+[guid]::NewGuid().ToString('N')+'.swap'
    try{
        [IO.File]::WriteAllBytes($temporary,$Bytes)
        if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temporary,$Path,$swap)}else{[IO.File]::Move($temporary,$Path)}
    }finally{
        if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}
        if(Test-Path -LiteralPath $swap){Remove-Item -LiteralPath $swap -Force}
    }
}
function Get-Utf8Bytes([string]$Text,[bool]$WithBom) {
    $encoding=[Text.UTF8Encoding]::new($WithBom);$body=$encoding.GetBytes($Text)
    if(-not$WithBom){return $body}
    $preamble=$encoding.GetPreamble();$output=New-Object byte[] ($preamble.Length+$body.Length)
    [Buffer]::BlockCopy($preamble,0,$output,0,$preamble.Length);[Buffer]::BlockCopy($body,0,$output,$preamble.Length,$body.Length)
    $output
}
function Copy-Atomic([string]$Source,[string]$Destination){Write-AtomicBytes $Destination ([IO.File]::ReadAllBytes($Source))}
function Remove-Cache {
    $path=Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
    if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force;Write-Host 'Removed core_data.cbo so the Phase 7 loader state is used on the next normal launch.'}
}
function Remove-CompletedBackups {
    if(-not(Test-Path -LiteralPath $backupBase)){return}
    $resolvedBase=[IO.Path]::GetFullPath($backupBase).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    foreach($directory in Get-ChildItem -LiteralPath $backupBase -Directory -Force){
        $resolved=[IO.Path]::GetFullPath($directory.FullName)
        if(-not$resolved.StartsWith($resolvedBase,[StringComparison]::OrdinalIgnoreCase)){
            throw "Refusing to remove an out-of-scope Phase 7 backup: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
function Assert-GameClosed { if(Get-Process ScrapMechanic -ErrorAction SilentlyContinue){throw 'Scrap Mechanic is running. Close it before changing the Phase 7 qualification layer.'} }
function Get-ModStatus([string]$ActionName){
    $json=&$helperPath --status $ActionName
    if($LASTEXITCODE-ne0){throw "Could not read $ActionName status."}
    $json|ConvertFrom-Json
}

function Install-Phase7 {
    if(Test-Path -LiteralPath $receiptPath){throw 'The Phase 7 qualification layer is already installed.'}
    $storage=Get-ModStatus 'network-storage-chest'
    if(-not$storage.Success -or -not$storage.Installed){throw 'Install and verify Network Storage Chest before adding its Phase 7 qualification layer.'}
    $wireless=Get-ModStatus 'wireless-vacuum-pipe'
    if(-not$wireless.Success -or -not$wireless.Installed){throw 'Wireless Vacuum Pipe must be installed for the Phase 7 wireless suite.'}
    $state=Get-TextState $survivalPath
    if($state.Text.Contains($loaderStart) -or $state.Text.Contains('NetworkStorageChestPhase7Harness.lua')){throw 'A Phase 7 loader already exists without its receipt.'}
    $lines=@($loaderStart)
    foreach($name in $harnesses){$lines+='dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/'+$name+'" )'}
    $lines+=$loaderEnd
    $output=$state.Text.TrimEnd("`r","`n")+$state.Newline+$state.Newline+($lines-join$state.Newline)+$state.Newline
    $stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
    $backupRoot=Join-Path $stateRoot $stamp
    $survivalBackup=Join-Path $backupRoot $survivalRelative
    [IO.Directory]::CreateDirectory((Split-Path -Parent $survivalBackup))|Out-Null
    [IO.File]::Copy($survivalPath,$survivalBackup,$true)
    $sourceHash=Get-Sha256 $survivalPath
    $owned=@()
    foreach($name in $harnesses){
        $source=Join-Path $sourceRoot $name
        $relative='Survival\Scripts\ScrapLab\Development\'+$name
        $target=Join-Path $GamePath $relative
        if(-not(Test-Path -LiteralPath $source)){throw "Phase 7 harness source is missing: $name"}
        if(Test-Path -LiteralPath $target){throw "Phase 7 owned target already exists: $relative"}
        $owned+=[pscustomobject]@{Source=$source;Target=$target;RelativePath=$relative;Hash=Get-Sha256 $source}
    }
    try{
        Write-AtomicBytes $survivalPath (Get-Utf8Bytes $output $state.HasBom)
        foreach($asset in $owned){Copy-Atomic $asset.Source $asset.Target;if((Get-Sha256 $asset.Target)-ne$asset.Hash){throw "Harness verification failed: $($asset.RelativePath)"}}
        $outputHash=Get-Sha256 $survivalPath
        $receipt=[ordered]@{SchemaVersion=1;InstalledUtc=[DateTime]::UtcNow.ToString('o');GamePath=$GamePath;BackupRoot=$backupRoot;Survival=[ordered]@{RelativePath=$survivalRelative;BackupPath=$survivalBackup;SourceHash=$sourceHash;OutputHash=$outputHash};Owned=@($owned|ForEach-Object{[ordered]@{RelativePath=$_.RelativePath;Hash=$_.Hash}})}
        [IO.Directory]::CreateDirectory($stateRoot)|Out-Null
        Write-AtomicBytes $receiptPath ([Text.UTF8Encoding]::new($false).GetBytes(($receipt|ConvertTo-Json -Depth 8 -Compress)))
        Remove-Cache
        Write-Host "Phase 7 qualification layer installed and verified. Backup: $backupRoot"
    }catch{
        [IO.File]::Copy($survivalBackup,$survivalPath,$true)
        foreach($asset in $owned){if(Test-Path -LiteralPath $asset.Target){Remove-Item -LiteralPath $asset.Target -Force}}
        throw
    }
}

function Uninstall-Phase7 {
    if(-not(Test-Path -LiteralPath $receiptPath)){throw 'No active Phase 7 qualification receipt exists.'}
    $receipt=Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8|ConvertFrom-Json
    if(-not[string]::Equals($receipt.GamePath,$GamePath,[StringComparison]::OrdinalIgnoreCase)){throw 'The Phase 7 receipt belongs to another game installation.'}
    if((Get-Sha256 $survivalPath)-ne$receipt.Survival.OutputHash){throw 'SurvivalGame.lua changed after Phase 7 installation; removal was blocked.'}
    foreach($asset in $receipt.Owned){$path=Join-Path $GamePath $asset.RelativePath;if(-not(Test-Path -LiteralPath $path)-or(Get-Sha256 $path)-ne$asset.Hash){throw "A Phase 7 harness changed or is missing: $($asset.RelativePath)"}}
    if(-not(Test-Path -LiteralPath $receipt.Survival.BackupPath)-or(Get-Sha256 $receipt.Survival.BackupPath)-ne$receipt.Survival.SourceHash){throw 'The verified Phase 7 SurvivalGame backup is missing or changed.'}
    [IO.File]::Copy($receipt.Survival.BackupPath,$survivalPath,$true)
    if((Get-Sha256 $survivalPath)-ne$receipt.Survival.SourceHash){throw 'SurvivalGame.lua restoration verification failed.'}
    foreach($asset in $receipt.Owned){Remove-Item -LiteralPath (Join-Path $GamePath $asset.RelativePath) -Force}
    Remove-Item -LiteralPath $receiptPath -Force
    Remove-Cache
    $storage=Get-ModStatus 'network-storage-chest'
    if(-not$storage.Success -or -not$storage.Installed){throw 'The production Network Storage Chest state was not intact after removing Phase 7.'}
    Remove-CompletedBackups
    Write-Host 'Phase 7 qualification layer removed; the exact pre-test SurvivalGame.lua was restored.'
}

if(-not(Test-Path -LiteralPath $GamePath)){throw "Scrap Mechanic was not found at: $GamePath"}
if(-not(Test-Path -LiteralPath $helperPath)){throw 'Build ScrapLab before deploying Phase 7.'}
Assert-GameClosed
if($Action-eq'Install'){Install-Phase7}else{Uninstall-Phase7}
