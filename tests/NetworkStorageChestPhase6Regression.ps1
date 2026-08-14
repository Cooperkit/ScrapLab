param(
    [string]$MainExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.exe'),
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe')
)
$ErrorActionPreference = 'Stop'
$binding = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
function Assert-True([bool]$condition,[string]$message){if(-not $condition){throw "ASSERTION FAILED: $message"}}
function Invoke-Static([Type]$type,[string]$name,[object[]]$arguments){
    $method=$type.GetMethods($binding)|Where-Object{$_.Name -eq $name -and $_.GetParameters().Count -eq $arguments.Count}
    if(@($method).Count -ne 1){throw "Expected one $($type.FullName).$name overload."}
    $parameters=$method.GetParameters()
    [object[]]$invokeArguments=@(for($index=0;$index-lt$arguments.Count;$index++){
        if($parameters[$index].ParameterType-eq[string]){[string]$arguments[$index]}
        elseif($parameters[$index].ParameterType-eq[bool]){[bool]$arguments[$index]}
        else{$arguments[$index]}
    })
    try{return $method.Invoke($null,$invokeArguments)}catch [Reflection.TargetInvocationException]{throw $_.Exception.InnerException}
}
function Write-Utf8([string]$path,[string]$text){[IO.Directory]::CreateDirectory((Split-Path -Parent $path))|Out-Null;[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))}

$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$main=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $MainExe).Path)
$helper=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $PatchHelperExe).Path)
$html=[string]$main.GetType('RaidRescue.UiHtml',$true).GetField('Content',[Reflection.BindingFlags]'Public,Static').GetValue($null)
$bridge=$main.GetType('RaidRescue.BrowserBridge',$true)
Assert-True ($null-ne$bridge.GetMethod('GetNetworkStorageChestModStatus')) 'Status bridge missing.'
Assert-True ($null-ne$bridge.GetMethod('SetNetworkStorageChestMod')) 'Toggle bridge missing.'
$protocol=$helper.GetType('RaidRescue.PatchHelperProtocol',$true)
$known=$protocol.GetMethod('IsKnownAction',[Reflection.BindingFlags]'Static,NonPublic')
Assert-True ([bool]$known.Invoke($null,@('network-storage-chest'))) 'Protocol rejects network-storage-chest.'
foreach($needle in @('id="networkStorageChestRow"','id="networkStorageChestSwitch"','function toggleNetworkStorageChestMod()','function setNetworkStorageChestMod(enabled)','id="networkStorageChestDangerModal"','I REMOVED EVERY NETWORK STORAGE CHEST - DISABLE','SUPER SECRET MODS &mdash; NETWORK STORAGE CHEST','12 AVAILABLE')){Assert-True $html.Contains($needle) "UI missing $needle"}
$script=[regex]::Match($html,'(?is)<script[^>]*>(.*?)</script>').Groups[1].Value
$node=(Get-Command node -ErrorAction SilentlyContinue).Source
if($node){$temp=Join-Path $env:TEMP ('scraplab-storage-phase6-'+[guid]::NewGuid().ToString('N')+'.js');try{Write-Utf8 $temp $script;&$node --check $temp;Assert-True ($LASTEXITCODE-eq0) 'Embedded JavaScript syntax failed.'}finally{Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue}}

$gameType=$helper.GetType('RaidRescue.GamePatchService',$true)
$support=$helper.GetType('RaidRescue.AdaptivePatchSupport',$true)
$service=$helper.GetType('RaidRescue.NetworkStorageChestPatchService',$true)
$wirelessSource=Get-Content -LiteralPath (Join-Path $root 'source\Patching\WirelessVacuumPipePatchService.cs') -Raw -Encoding UTF8
Assert-True $wirelessSource.Contains('"NetworkStorageChest"') 'Wireless Vacuum Pipe does not trust the Network Storage shared-file receipt.'
$live=Invoke-Static $gameType 'FindGameInstall' @()
Assert-True (-not[String]::IsNullOrWhiteSpace($live)) 'Live Scrap Mechanic install not found.'
$fixture=Join-Path $PSScriptRoot ('.network-storage-phase6-'+[guid]::NewGuid().ToString('N'))
$fake=Join-Path $fixture 'steamapps\common\Scrap Mechanic'
$backups=Join-Path $fixture 'backups';$receipts=Join-Path $fixture 'receipts'
$languages=@('Brazilian','Chinese','English','French','German','Italian','Japanese','Korean','Polish','Russian','Spanish')
$targets=@('Survival\Objects\Database\shapesets.json','Survival\Scripts\game\survival_items.lua','Survival\Scripts\game\util\pipes.lua','Survival\CraftingRecipes\craftbot\craftbot_core.json','Survival\Scripts\game\managers\RecipeManager.lua','Survival\Gui\IconMapSurvival.xml','Survival\Gui\IconMapSurvival.png')
$targets+=$languages|ForEach-Object{"Survival\Gui\Language\$_\inventoryDescriptions.json"}
$devReceiptPath=Join-Path $root 'dist\phase0-backups\NetworkStorageChest\active.json'
$productionReceiptPath=Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active\NetworkStorageChest.json'
$sourceReceiptFiles=@()
if(Test-Path -LiteralPath $devReceiptPath){$sourceReceiptFiles=@((Get-Content -LiteralPath $devReceiptPath -Raw -Encoding UTF8|ConvertFrom-Json).Targets)}
elseif(Test-Path -LiteralPath $productionReceiptPath){$sourceReceiptFiles=@((Get-Content -LiteralPath $productionReceiptPath -Raw -Encoding UTF8|ConvertFrom-Json).Files)}
try{
    [IO.Directory]::CreateDirectory((Join-Path $fake 'Release'))|Out-Null
    Copy-Item -LiteralPath (Join-Path $live 'Release\ScrapMechanic.exe') -Destination (Join-Path $fake 'Release\ScrapMechanic.exe')
    foreach($relative in $targets){
        $source=Join-Path $live $relative
        $old=$sourceReceiptFiles|Where-Object{$_.RelativePath-eq$relative}|Select-Object -First 1
        if($old-and(Test-Path -LiteralPath $old.BackupPath)){$source=$old.BackupPath}
        $destination=Join-Path $fake $relative;[IO.Directory]::CreateDirectory((Split-Path -Parent $destination))|Out-Null;Copy-Item -LiteralPath $source -Destination $destination
    }
    $xmlPath=Join-Path $fake 'Survival\Gui\IconMapSurvival.xml'
    $xml=Get-Content -LiteralPath $xmlPath -Raw -Encoding UTF8
    $xml=[regex]::Replace($xml,'(?ms)^\s*<!-- SCRAPLAB PART: Network Storage Chest icon\. -->\r?\n\s*<Index name="bc7576a7-f226-459a-883c-e8460e955d63">\r?\n\s*<Frame point="\d+ \d+"/>\r?\n\s*</Index>\r?\n','')
    Write-Utf8 $xmlPath $xml
    $manifest=Join-Path $fixture 'steamapps\appmanifest_387990.acf';$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Utf8 $manifest ('"AppState"' + "`n{" + "`n`t`"appid`"`t`t`"387990`"" + "`n`t`"buildid`"`t`t`"99999999`"" + "`n`t`"LastUpdated`"`t`t`"$now`"`n}`n")
    $liveAtlasBaseline=Join-Path $env:LOCALAPPDATA 'ScrapLab\Game Backups\Scrap Mechanic\Secret Mods\ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
    $fixtureAtlasBaseline=Join-Path $backups 'ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $fixtureAtlasBaseline))|Out-Null
    Copy-Item -LiteralPath $liveAtlasBaseline -Destination $fixtureAtlasBaseline
    $support.GetField('PatchStateRootOverride',$binding).SetValue($null,$receipts)
    $clean=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True $clean.Success "Clean probe failed: $($clean.Error)"
    Assert-True (-not$clean.Installed) 'Clean fixture reported installed.'
    Assert-True $clean.CanApply "Adaptive fixture was blocked: $($clean.CompatibilityReason)"
    $install=Invoke-Static $service 'SetEnabledAt' @($fake,$backups,$true)
    Assert-True $install.Success "Install failed: $($install.Error)"
    Assert-True $install.Installed 'Install did not report installed.'
    $installed=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True ($installed.Success-and$installed.Installed) "Installed probe failed: $($installed.Error)"
    foreach($owned in @('Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua','Survival\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua','Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui','Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChestItem.gui','Survival\Objects\Database\ShapeSets\ScrapLab\Parts\NetworkStorageChest.shapeset','Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.localization.json')){Assert-True (Test-Path -LiteralPath (Join-Path $fake $owned)) "Owned file missing: $owned"}
    $remove=Invoke-Static $service 'SetEnabledAt' @($fake,$backups,$false)
    Assert-True $remove.Success "Removal failed: $($remove.Error)"
    Assert-True (-not$remove.Installed) 'Removal still reports installed.'
    $removed=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True ($removed.Success-and-not$removed.Installed-and$removed.CanApply) "Removed probe failed: $($removed.Error)"
}
finally{
    $support.GetField('PatchStateRootOverride',$binding).SetValue($null,$null)
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'Network Storage Chest Phase 6 regression passed.'
