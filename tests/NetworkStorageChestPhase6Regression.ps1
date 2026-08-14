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
foreach($needle in @('id="networkStorageChestRow"','id="networkStorageChestSwitch"','id="networkStorageChestUpdate"','function toggleNetworkStorageChestMod()','function updateNetworkStorageChestMod()','function setNetworkStorageChestMod(enabled)','ROUTING MODE UPDATE AVAILABLE','UPGRADING DEPOSIT ROUTING MODES','id="networkStorageChestDangerModal"','I REMOVED EVERY NETWORK STORAGE CHEST - DISABLE','SUPER SECRET MODS &mdash; NETWORK STORAGE CHEST','12 AVAILABLE')){Assert-True $html.Contains($needle) "UI missing $needle"}
$script=[regex]::Match($html,'(?is)<script[^>]*>(.*?)</script>').Groups[1].Value
$node=(Get-Command node -ErrorAction SilentlyContinue).Source
if($node){$temp=Join-Path $env:TEMP ('scraplab-storage-phase6-'+[guid]::NewGuid().ToString('N')+'.js');try{Write-Utf8 $temp $script;&$node --check $temp;Assert-True ($LASTEXITCODE-eq0) 'Embedded JavaScript syntax failed.'}finally{Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue}}

$gameType=$helper.GetType('RaidRescue.GamePatchService',$true)
$support=$helper.GetType('RaidRescue.AdaptivePatchSupport',$true)
$service=$helper.GetType('RaidRescue.NetworkStorageChestPatchService',$true)
$serviceSource=Get-Content -LiteralPath (Join-Path $root 'source\Patching\NetworkStorageChestPatchService.cs') -Raw -Encoding UTF8
foreach($needle in @('DefinitionVersion = "3"','LegacyV1RuntimeHash','LegacyV1IndexHash','LegacyV2RuntimeHash','LegacyV2GuiHash','LegacyV2LocalizationHash','BuildDefinitionUpdatePlans','ApplyDefinitionUpdate','PatchCompatibilityState.DefinitionUpdate')){Assert-True $serviceSource.Contains($needle) "Routing-mode migration support missing: $needle"}
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
$liveAtlasBaseline=Join-Path $env:LOCALAPPDATA 'ScrapLab\Game Backups\Scrap Mechanic\Secret Mods\ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
try{
    [IO.Directory]::CreateDirectory((Join-Path $fake 'Release'))|Out-Null
    Copy-Item -LiteralPath (Join-Path $live 'Release\ScrapMechanic.exe') -Destination (Join-Path $fake 'Release\ScrapMechanic.exe')
    foreach($relative in $targets){
        $source=Join-Path $live $relative
        $old=$sourceReceiptFiles|Where-Object{$_.RelativePath-eq$relative}|Select-Object -First 1
        if($old-and(Test-Path -LiteralPath $old.BackupPath)){$source=$old.BackupPath}
        if($relative-eq'Survival\Gui\IconMapSurvival.png'-and(Test-Path -LiteralPath $liveAtlasBaseline)){$source=$liveAtlasBaseline}
        $destination=Join-Path $fake $relative;[IO.Directory]::CreateDirectory((Split-Path -Parent $destination))|Out-Null;Copy-Item -LiteralPath $source -Destination $destination
    }
    $xmlPath=Join-Path $fake 'Survival\Gui\IconMapSurvival.xml'
    $xml=Get-Content -LiteralPath $xmlPath -Raw -Encoding UTF8
    foreach($uuid in @('bc7576a7-f226-459a-883c-e8460e955d63','a34d9af0-4ba0-431d-b647-2d5435ecf138','a638a8aa-6f4f-41c2-9e31-702687066092')){
        $xml=[regex]::Replace($xml,'(?ms)^\s*(?:<!--[^\r\n]*-->\r?\n\s*)?<Index name="'+[regex]::Escape($uuid)+'">\r?\n\s*<Frame point="\d+ \d+"/>\r?\n\s*</Index>\r?\n','')
    }
    Write-Utf8 $xmlPath $xml
    $manifest=Join-Path $fixture 'steamapps\appmanifest_387990.acf';$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Utf8 $manifest ('"AppState"' + "`n{" + "`n`t`"appid`"`t`t`"387990`"" + "`n`t`"buildid`"`t`t`"99999999`"" + "`n`t`"LastUpdated`"`t`t`"$now`"`n}`n")
    $fixtureAtlasBaseline=Join-Path $backups 'ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $fixtureAtlasBaseline))|Out-Null
    Copy-Item -LiteralPath $liveAtlasBaseline -Destination $fixtureAtlasBaseline
    $support.GetField('PatchStateRootOverride',$binding).SetValue($null,$receipts)
    $clean=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True $clean.Success "Clean probe failed: $($clean.Error)"
    Assert-True (-not$clean.Installed) 'Clean fixture reported installed.'
    Assert-True $clean.CanApply "Adaptive fixture was blocked: $($clean.CompatibilityReason)"
    foreach($relative in $targets){
        $snapshot=Join-Path (Join-Path $fixture 'clean') $relative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $snapshot))|Out-Null
        Copy-Item -LiteralPath (Join-Path $fake $relative) -Destination $snapshot
    }
    $install=Invoke-Static $service 'SetEnabledAt' @($fake,$backups,$true)
    Assert-True $install.Success "Install failed: $($install.Error)"
    Assert-True $install.Installed 'Install did not report installed.'
    $installed=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True ($installed.Success-and$installed.Installed) "Installed probe failed: $($installed.Error)"
    $ownedFiles=@('Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua','Survival\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua','Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui','Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChestItem.gui','Survival\Objects\Database\ShapeSets\ScrapLab\Parts\NetworkStorageChest.shapeset','Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.localization.json')
    foreach($owned in $ownedFiles){Assert-True (Test-Path -LiteralPath (Join-Path $fake $owned)) "Owned file missing: $owned"}
    $legacyRuntime=Join-Path $live 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua'
    $legacyIndex=Join-Path $live 'Survival\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua'
    $legacyGui=Join-Path $live 'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui'
    $legacyLocalization=Join-Path $live 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.localization.json'
    if((Test-Path -LiteralPath $productionReceiptPath)-and
       (Test-Path -LiteralPath $legacyRuntime)-and
       (Test-Path -LiteralPath $legacyIndex)-and
       (Test-Path -LiteralPath $legacyGui)-and
       (Test-Path -LiteralPath $legacyLocalization)-and
       ((Get-FileHash -Algorithm SHA256 -LiteralPath $legacyRuntime).Hash-eq'B42F99CA53E5D36188F8BFC352DCB0F560A649BD6783E31DAE38BC22ECC3FB49')-and
       ((Get-FileHash -Algorithm SHA256 -LiteralPath $legacyIndex).Hash-eq'B8FC29D4E85319FE64D9E706A9ACB5F4BACE9CD37EAC684539DFDD85007B91E8')-and
       ((Get-FileHash -Algorithm SHA256 -LiteralPath $legacyGui).Hash-eq'999B00353C31FBCA9EE94BF9B816132C8F773890BBC489B155236F028D6D5A37')-and
       ((Get-FileHash -Algorithm SHA256 -LiteralPath $legacyLocalization).Hash-eq'4FF131F150F0FF472786CA49A701AFD05D3E04375A89C710A9412417A80A010F')){
        Copy-Item -LiteralPath $legacyRuntime -Destination (Join-Path $fake $ownedFiles[0]) -Force
        Copy-Item -LiteralPath $legacyIndex -Destination (Join-Path $fake $ownedFiles[1]) -Force
        Copy-Item -LiteralPath $legacyGui -Destination (Join-Path $fake $ownedFiles[2]) -Force
        Copy-Item -LiteralPath $legacyLocalization -Destination (Join-Path $fake $ownedFiles[5]) -Force
        Copy-Item -LiteralPath $productionReceiptPath -Destination (Join-Path $receipts 'NetworkStorageChest.json') -Force
        $legacyStatus=Invoke-Static $service 'GetStatusAt' @($fake)
        Assert-True ($legacyStatus.Success-and$legacyStatus.Installed-and$legacyStatus.NeedsUpdate-and$legacyStatus.CanApply) "Legacy smart-routing update was not offered: $($legacyStatus.CompatibilityReason)"
        Assert-True ($legacyStatus.CompatibilityState-eq'PATCH DEFINITION UPDATE') "Legacy install received the wrong state: $($legacyStatus.CompatibilityState)"
        $definitionUpdate=Invoke-Static $service 'SetEnabledAt' @($fake,$backups,$true)
        Assert-True ($definitionUpdate.Success-and$definitionUpdate.Installed-and-not$definitionUpdate.NeedsUpdate) "Smart-routing definition update failed: $($definitionUpdate.Error)"
        Assert-True ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $fake $ownedFiles[0])).Hash-eq(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChest.lua')).Hash) 'Runtime definition update output is wrong.'
        Assert-True ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $fake $ownedFiles[1])).Hash-eq(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'source\Patching\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua')).Hash) 'Index definition update output is wrong.'
        Assert-True ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $fake $ownedFiles[2])).Hash-eq(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChest.gui')).Hash) 'GUI definition update output is wrong.'
        Assert-True ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $fake $ownedFiles[5])).Hash-eq(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'source\Patching\Parts\NetworkStorageChest\NetworkStorageChest.localization.json')).Hash) 'Localization definition update output is wrong.'
    }
    foreach($relative in $targets){[IO.File]::WriteAllBytes((Join-Path $fake $relative),[IO.File]::ReadAllBytes((Join-Path (Join-Path $fixture 'clean') $relative)))}
    $verified=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True ($verified.Success-and-not$verified.Installed-and$verified.CanApply) "Steam Verify state was blocked: $($verified.CompatibilityReason)"
    Assert-True ($verified.CompatibilityState-eq'REINSTALL REQUIRED - SAVE PART AT RISK') "Steam Verify received the wrong state: $($verified.CompatibilityState)"
    $reinstall=Invoke-Static $service 'SetEnabledAt' @($fake,$backups,$true)
    Assert-True ($reinstall.Success-and$reinstall.Installed) "Steam Verify reinstall failed: $($reinstall.Error)"
    $remove=Invoke-Static $service 'SetEnabledAt' @($fake,$backups,$false)
    Assert-True $remove.Success "Removal failed: $($remove.Error)"
    Assert-True (-not$remove.Installed) 'Removal still reports installed.'
    $removed=Invoke-Static $service 'GetStatusAt' @($fake)
    Assert-True ($removed.Success-and-not$removed.Installed-and$removed.CanApply) "Removed probe failed: $($removed.Error)"
    foreach($owned in $ownedFiles){Assert-True (-not(Test-Path -LiteralPath (Join-Path $fake $owned))) "Removal retained an orphaned owned file: $owned"}
}
finally{
    $support.GetField('PatchStateRootOverride',$binding).SetValue($null,$null)
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host 'Network Storage Chest Phase 6 regression passed.'
