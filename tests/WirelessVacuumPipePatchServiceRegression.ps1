param(
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe')
)

$ErrorActionPreference = 'Stop'
$binding = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'

function Assert-True([bool]$Condition,[string]$Message){if(-not $Condition){throw "ASSERTION FAILED: $Message"}}
function Invoke-Static([Type]$Type,[string]$Name,[object[]]$Arguments){
    $methods=@($Type.GetMethods($binding)|Where-Object{$_.Name-eq$Name-and$_.GetParameters().Count-eq$Arguments.Count})
    if($methods.Count-ne1){throw "Expected one $($Type.FullName).$Name overload."}
    $parameters=$methods[0].GetParameters();[object[]]$values=@()
    for($i=0;$i-lt$Arguments.Count;$i++){
        if($parameters[$i].ParameterType-eq[string]){$values+=[string]$Arguments[$i]}
        elseif($parameters[$i].ParameterType-eq[bool]){$values+=[bool]$Arguments[$i]}
        else{$values+=$Arguments[$i]}
    }
    try {
        $methods[0].Invoke($null,$values)
    }
    catch [Reflection.TargetInvocationException] {
        throw $_.Exception.InnerException
    }
}
function Get-Hash([string]$Path){(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Get-BytesHash([byte[]]$Bytes){$sha=[Security.Cryptography.SHA256]::Create();try{([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-','')}finally{$sha.Dispose()}}
function Copy-Exact([string]$Source,[string]$Destination){[IO.Directory]::CreateDirectory((Split-Path -Parent $Destination))|Out-Null;[IO.File]::WriteAllBytes($Destination,[IO.File]::ReadAllBytes($Source))}
function Write-Manifest([string]$BuildId){
    $updated=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $text='"AppState"'+"`n{"+"`n`t"+'"appid"'+"`t`t"+'"387990"'+"`n`t"+'"buildid"'+"`t`t"+'"'+$BuildId+'"'+"`n`t"+'"LastUpdated"'+"`t`t"+'"'+$updated+'"'+"`n}`n"
    [IO.Directory]::CreateDirectory((Split-Path -Parent $manifestPath))|Out-Null
    [IO.File]::WriteAllText($manifestPath,$text,[Text.UTF8Encoding]::new($false))
}
function Assert-Baseline([hashtable]$Expected,[string]$Context){
    foreach($relative in $Expected.Keys){$path=Join-Path $fakeGame $relative;Assert-True (Test-Path -LiteralPath $path) "$Context missing $relative";Assert-True ((Get-Hash $path)-eq$Expected[$relative]) "$Context changed $relative"}
    foreach($relative in $ownedTargets){Assert-True (-not(Test-Path -LiteralPath (Join-Path $fakeGame $relative))) "$Context left owned file $relative"}
    if($null-ne$atlasBaselineHash){Assert-True ((Get-Hash $fixtureAtlasBaseline)-eq$atlasBaselineHash) "$Context changed the shared atlas baseline."}
    if($null-ne$sharedMirrorBytes){Assert-True ((Get-Hash $sharedMirrorPath)-eq(Get-BytesHash $sharedMirrorBytes)) "$Context changed the shared atlas mirror receipt."}
}
function Reset-SharedState {
    $statePath=Join-Path $receiptRoot 'ScrapLab-Icon-Pack.json'
    if($null-ne$sharedStateBytes){[IO.File]::WriteAllBytes($statePath,$sharedStateBytes)}else{Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue}
    if($null-ne$sharedMirrorBytes){[IO.File]::WriteAllBytes($sharedMirrorPath,$sharedMirrorBytes)}else{Remove-Item -LiteralPath $sharedMirrorPath -Force -ErrorAction SilentlyContinue}
    Remove-Item -LiteralPath (Join-Path $receiptRoot 'WirelessVacuumPipe.json') -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $receiptRoot 'WirelessVacuumPipe.activation.json') -Force -ErrorAction SilentlyContinue
}

$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $PatchHelperExe).Path)
$gameType=$assembly.GetType('RaidRescue.GamePatchService',$true)
$serviceType=$assembly.GetType('RaidRescue.WirelessVacuumPipePatchService',$true)
$supportType=$assembly.GetType('RaidRescue.AdaptivePatchSupport',$true)
$atomicType=$assembly.GetType('RaidRescue.AtomicCustomPartPatchSupport',$true)
$liveGame=Invoke-Static $gameType 'FindGameInstall' @()
Assert-True (-not[String]::IsNullOrWhiteSpace($liveGame)) 'Scrap Mechanic install was not found.'

$fixtureRoot=Join-Path $PSScriptRoot ('.wireless-pipe-service-'+[Guid]::NewGuid().ToString('N'))
$fakeGame=Join-Path $fixtureRoot 'steamapps\common\Scrap Mechanic'
$manifestPath=Join-Path $fixtureRoot 'steamapps\appmanifest_387990.acf'
$backupRoot=Join-Path $fixtureRoot 'backups'
$receiptRoot=Join-Path $fixtureRoot 'receipts'
$languages=@('Brazilian','Chinese','English','French','German','Italian','Japanese','Korean','Polish','Russian','Spanish')
$targets=@(
    'Survival\Objects\Database\shapesets.json','Survival\Scripts\game\survival_items.lua',
    'Survival\ScriptableObjects\scriptableObjectSets\sob_managers.sobset',
    'Survival\CraftingRecipes\craftbot\craftbot_core.json','Survival\Scripts\game\managers\RecipeManager.lua',
    'Survival\Scripts\game\interactables\Crafter.lua','Survival\Scripts\game\interactables\FlatVacuum.lua',
    'Survival\Scripts\game\interactables\GarageChest.lua','Survival\Scripts\game\interactables\OreCrusher.lua',
    'Survival\Scripts\game\interactables\Prospector.lua','Survival\Scripts\game\interactables\Refinery.lua',
    'Survival\Scripts\game\interactables\Vacuum.lua','Survival\Scripts\util.lua','Survival\Scripts\game\util\pipes.lua',
    'Survival\Gui\IconMapSurvival.xml','Survival\Gui\IconMapSurvival.png')
$targets+=$languages|ForEach-Object{"Survival\Gui\Language\$_\inventoryDescriptions.json"}
$ownedTargets=@(
    'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua','Survival\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua',
    'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeTransfer.lua','Survival\Scripts\ScrapLab\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua',
    'Survival\Objects\Database\ShapeSets\ScrapLab\Parts\WirelessVacuumPipe.shapeset','Survival\Gui\Layouts\ScrapLab\Parts\WirelessVacuumPipe.layout')
    $liveState=Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active'
    $liveWirelessReceiptPath=Join-Path $liveState 'WirelessVacuumPipe.json'
    $liveWirelessReceipt=if(Test-Path -LiteralPath $liveWirelessReceiptPath){Get-Content -LiteralPath $liveWirelessReceiptPath -Raw|ConvertFrom-Json}else{$null}
$liveAtlasBaseline=Join-Path $env:LOCALAPPDATA 'ScrapLab\Game Backups\Scrap Mechanic\Secret Mods\ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'

try{
    [IO.Directory]::CreateDirectory((Join-Path $fakeGame 'Release'))|Out-Null
    Copy-Exact (Join-Path $liveGame 'Release\ScrapMechanic.exe') (Join-Path $fakeGame 'Release\ScrapMechanic.exe')
    $cleanSources=@{}
    foreach($relative in $targets){
        $source=Join-Path $liveGame $relative
        $receiptFile=if($null-ne$liveWirelessReceipt){$liveWirelessReceipt.Files|Where-Object{$_.RelativePath-eq$relative}|Select-Object -First 1}else{$null}
        if($relative-ne'Survival\Gui\IconMapSurvival.png'-and$null-ne$receiptFile-and$receiptFile.BackupPath-and(Test-Path -LiteralPath $receiptFile.BackupPath)){$source=$receiptFile.BackupPath}
        $cleanSources[$relative]=$source
        Copy-Exact $source (Join-Path $fakeGame $relative)
    }
    [IO.Directory]::CreateDirectory($receiptRoot)|Out-Null
    if(Test-Path -LiteralPath $liveState){Get-ChildItem -LiteralPath $liveState -Filter '*.json' -File|ForEach-Object{Copy-Exact $_.FullName (Join-Path $receiptRoot $_.Name)}}
    Remove-Item -LiteralPath (Join-Path $receiptRoot 'WirelessVacuumPipe.json') -Force -ErrorAction SilentlyContinue
    $sharedStatePath=Join-Path $receiptRoot 'ScrapLab-Icon-Pack.json'
    $sharedStateBytes=if(Test-Path -LiteralPath $sharedStatePath){[IO.File]::ReadAllBytes($sharedStatePath)}else{$null}
    $fixtureAtlasBaseline=Join-Path $backupRoot 'ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
    $sharedMirrorPath=Join-Path $backupRoot 'ScrapLab-Shared-Icon-Atlas\atlas-receipt.json'
    $atlasBaselineSource=if(Test-Path -LiteralPath $liveAtlasBaseline){$liveAtlasBaseline}else{$cleanSources['Survival\Gui\IconMapSurvival.png']}
    Copy-Exact $atlasBaselineSource $fixtureAtlasBaseline
    $atlasBaselineHash=Get-Hash $fixtureAtlasBaseline
    Write-Manifest '24529696'
    $supportType.GetField('PatchStateRootOverride',$binding).SetValue($null,$receiptRoot)

    $baseline=@{};$baselineBytes=@{};foreach($relative in $targets){$path=Join-Path $fakeGame $relative;$baseline[$relative]=Get-Hash $path;$baselineBytes[$relative]=[IO.File]::ReadAllBytes($path)}
    $baselineHasRaidDetector=([Text.Encoding]::UTF8.GetString($baselineBytes['Survival\Gui\IconMapSurvival.xml'])).Contains('a638a8aa-6f4f-41c2-9e31-702687066092')
    $clean=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True $clean.Success ('Clean status failed: '+$clean.Error)
    Assert-True (-not$clean.Installed) 'Clean fixture reported installed.'
    Assert-True $clean.CanApply ('Verified compositional fixture was blocked: '+$clean.CompatibilityReason)

    $partialOwned=Join-Path $fakeGame $ownedTargets[0]
    Copy-Exact (Join-Path $PSScriptRoot '..\source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua') $partialOwned
    $partial=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($partial.Success-and(-not$partial.CanApply)-and([string]$partial.CompatibilityState-eq'PARTIAL PATCH - REPAIR REQUIRED')) 'A partial owned runtime set was not blocked.'
    Remove-Item -LiteralPath $partialOwned -Force

    $install=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $install.Success ('Install failed: '+$install.Error)
    Assert-True ($install.FilesPatched-ge30) "Unexpectedly small atomic plan: $($install.FilesPatched)"
    $installedStatus=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($installedStatus.Success-and$installedStatus.Installed) ('Restart status failed: '+$installedStatus.Error)

    # Recreate the verified definition-3 runtime from the current source. This
    # keeps the regression self-contained even after the live game is updated
    # to definition 4, and proves the compatibility correction changes only the
    # two affected owned runtime files while preserving clean uninstall data.
    $managerRelative='Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'
    $graphRelative='Survival\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'
    $managerSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'))
    $graphSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\source\Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'))
    $managerNewline=if($managerSource.Contains("`r`n")){"`r`n"}else{"`n"}
    $graphNewline=if($graphSource.Contains("`r`n")){"`r`n"}else{"`n"}
    $definition3Manager=$managerSource.Replace('WIRELESS VACUUM PIPE MANAGER v6','WIRELESS VACUUM PIPE MANAGER v5').Replace(
        "`t-- Scrap Mechanic's restricted Lua runtime does not expose setmetatable.$managerNewline`t-- Endpoint unload/delete paths explicitly remove these live shape keys.$managerNewline`tself.sv.endpointIdByShape = {}",
        "`tself.sv.endpointIdByShape = setmetatable( {}, { __mode = `"k`" } )")
    $definition3Graph=$graphSource.Replace('WIRELESS PIPE GRAPH v9','WIRELESS PIPE GRAPH v7').Replace('ScrapLabPipeGraph.DEFINITION_VERSION = 9','ScrapLabPipeGraph.DEFINITION_VERSION = 7').Replace(
        "`t-- Scrap Mechanic's restricted Lua runtime does not expose setmetatable.$graphNewline`t-- This ordinary table is safe because the whole physical cache is discarded$graphNewline`t-- every CACHE_INTERVAL_TICKS rather than surviving for the game session.$graphNewline`tshapeKeys = {},",
        "`tshapeKeys = setmetatable( {}, { __mode = `"k`" } ),").Replace(
        "`tphysicalCache.shapeKeys = {}",
        "`tphysicalCache.shapeKeys = setmetatable( {}, { __mode = `"k`" } )").Replace(
        "`t-- Output routing must not follow a wireless peer back into the same$graphNewline`t-- directional machine's input network. Input discovery is intentionally$graphNewline`t-- allowed to follow a peer located on the output-side storage network: that$graphNewline`t-- lets a Craftbot craft from the complete linked chest system while its$graphNewline`t-- finished items still remain on the output side.$graphNewline`tif requestedDirection == `"output`" and openingDirections( startShape ) then$graphNewline`t`tfor _, component in ipairs( getStartComponents( startShape, `"input`", tracker ) ) do",
        "`tif openingDirections( startShape ) and requestedDirection then$graphNewline`t`tlocal oppositeDirection = requestedDirection == `"input`" and `"output`" or `"input`"$graphNewline`t`tfor _, component in ipairs( getStartComponents( startShape, oppositeDirection, tracker ) ) do")
    [IO.File]::WriteAllText((Join-Path $fakeGame $managerRelative),$definition3Manager,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $fakeGame $graphRelative),$definition3Graph,[Text.UTF8Encoding]::new($false))
    $definition3Runtime=@{
        $managerRelative='C1F0FA66477AB6189A47F40BEA377991A13E3FE2E99BB077D0CE6A6665E43B57'
        $graphRelative='7EC649701A334452B8E4CD6B96403C977B1E6EB3AE5D7057B46B506D79537F4D'
    }
    $receiptPath=Join-Path $receiptRoot 'WirelessVacuumPipe.json'
    $legacyReceipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
    $legacyReceipt.DefinitionVersion='3'
    foreach($relative in $definition3Runtime.Keys){
        Assert-True ((Get-Hash (Join-Path $fakeGame $relative))-eq$definition3Runtime[$relative]) "The generated definition-3 fixture changed: $relative"
        $receiptFile=$legacyReceipt.Files|Where-Object{$_.RelativePath-eq$relative}|Select-Object -First 1
        Assert-True ($null-ne$receiptFile) "Definition-3 receipt entry is missing: $relative"
        $receiptFile.OutputHash=$definition3Runtime[$relative]
    }
    [IO.File]::WriteAllText($receiptPath,($legacyReceipt|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    $definitionUpdate=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($definitionUpdate.Success-and$definitionUpdate.Installed-and$definitionUpdate.NeedsUpdate) ('Definition-3 runtime did not offer the compatibility update: '+$definitionUpdate.CompatibilityReason)
    $migration=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True ($migration.Success-and$migration.FilesPatched-eq2) ('Definition-3 compatibility migration failed: '+$migration.Error)

    $shapeText=Get-Content -LiteralPath (Join-Path $fakeGame $ownedTargets[4]) -Raw
    Assert-True ($shapeText.Contains('"showInInventory" : true')-and$shapeText.Contains('"stackSize" : 5')) 'Locked inventory visibility/stack size was not embedded.'
    $recipeText=Get-Content -LiteralPath (Join-Path $fakeGame $targets[3]) -Raw
    Assert-True ($recipeText.Contains('"quantity": 2')-and$recipeText.Contains('"craftTime": 30')-and$recipeText.Contains('a34d9af0-4ba0-431d-b647-2d5435ecf138')) 'Locked Craftbot recipe is missing.'
    $crafterText=Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\game\interactables\Crafter.lua') -Raw
    Assert-True (-not$crafterText.Contains('[ScrapLab Pipe Crafter]')) 'Production Crafter bridge retained development print spam.'
    $graphText=Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua') -Raw
    $managerText=Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua') -Raw
    $transferText=Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeTransfer.lua') -Raw
    Assert-True ($graphText.Contains('ScrapLabPipeGraph.DEFINITION_VERSION = 9')-and$graphText.Contains('CACHE_INTERVAL_TICKS = 10')-and$graphText.Contains('componentCacheHits')-and$graphText.Contains('Sv_HasVirtualRoute')) 'Definition 5 graph caching and fast paths are missing.'
    Assert-True (-not$graphText.Contains('local function sortedNeighbours')) 'Definition 5 retained per-node neighbour sorting.'
    Assert-True (-not($graphText -match 'setmetatable\s*\(')) 'Definition 5 graph calls a Lua function unavailable in Scrap Mechanic.'
    Assert-True ($graphText.Contains('if requestedDirection == "output" and openingDirections( startShape ) then')-and$graphText.Contains('getStartComponents( startShape, "input", tracker )')) 'Definition 5 Craftbot one-way loop guard is missing.'
    Assert-True (-not$graphText.Contains('oppositeDirection = requestedDirection == "input"')) 'Definition 5 still blocks input traversal into linked output-side storage.'
    Assert-True ($managerText.Contains('WIRELESS VACUUM PIPE MANAGER v6')-and$managerText.Contains('function WirelessPipeManager.sv_getMatchingCount')-and$managerText.Contains('function WirelessPipeManager.Sv_HasVirtualRoute')-and$managerText.Contains('endpointIdByShape')) 'Definition 4 manager indexes are missing.'
    Assert-True (-not($managerText -match 'setmetatable\s*\(')) 'Definition 4 manager calls a Lua function unavailable in Scrap Mechanic.'
    Assert-True ($transferText.Contains('MAX_IDLE_BACKOFF_TICKS = 40')-and$transferText.Contains('idleBackoffs')-and$transferText.Contains('backoffSkips')) 'Definition 3 idle transfer backoff is missing.'
    foreach($language in $languages){Assert-True ((Get-Content -LiteralPath (Join-Path $fakeGame "Survival\Gui\Language\$language\inventoryDescriptions.json") -Raw).Contains('a34d9af0-4ba0-431d-b647-2d5435ecf138')) "Missing $language localization."}
    $wirelessEnglish=Join-Path $fakeGame 'Survival\Gui\Language\English\inventoryDescriptions.json'
    $wirelessEnglishBytes=[IO.File]::ReadAllBytes($wirelessEnglish)
    $wirelessEnglishText=[IO.File]::ReadAllText($wirelessEnglish)
    $wirelessNewline=if($wirelessEnglishText.Contains("`r`n")){"`r`n"}else{"`n"}
    $otherUuid='66666666-7777-4888-8999-aaaaaaaaaaaa'
    $otherEntry="`t`"$otherUuid`": {$wirelessNewline`t`t`"description`": `"Shared localization regression.`",$wirelessNewline`t`t`"title`": `"Later ScrapLab Part`",$wirelessNewline`t`t`"upperCaseTitle`": `"LATER SCRAPLAB PART`"$wirelessNewline`t}"
    $wirelessObjectEnd=$wirelessEnglishText.LastIndexOf($wirelessNewline+'}')
    Assert-True ($wirelessObjectEnd-ge0) 'Wireless English localization fixture has no object ending.'
    [IO.File]::WriteAllText($wirelessEnglish,($wirelessEnglishText.Substring(0,$wirelessObjectEnd)+','+$wirelessNewline+$otherEntry+$wirelessEnglishText.Substring($wirelessObjectEnd)),[Text.UTF8Encoding]::new($false))
    $composedWirelessStatus=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($composedWirelessStatus.Success-and$composedWirelessStatus.Installed) ('A later shared localization entry disabled Wireless Vacuum Pipe: '+$composedWirelessStatus.CompatibilityState+' / '+$composedWirelessStatus.CompatibilityReason)
    [IO.File]::WriteAllBytes($wirelessEnglish,$wirelessEnglishBytes)
    $iconXml=Get-Content -LiteralPath (Join-Path $fakeGame 'Survival\Gui\IconMapSurvival.xml') -Raw
    Assert-True ($iconXml.Contains('a34d9af0-4ba0-431d-b647-2d5435ecf138')) 'Wireless icon XML entry is missing.'
    Assert-True ($iconXml.Contains('a638a8aa-6f4f-41c2-9e31-702687066092')-eq$baselineHasRaidDetector) 'Wireless installation changed the Raid Detector icon registration state.'
    $receipt=Get-Content -LiteralPath (Join-Path $receiptRoot 'WirelessVacuumPipe.json') -Raw|ConvertFrom-Json
    Assert-True ($receipt.DefinitionVersion-eq'5'-and$receipt.Files.Count-ge30) 'Bounded production receipt is incomplete.'

    $remove=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True $remove.Success ('Exact removal failed: '+$remove.Error)
    # The shared catalog intentionally retains every known custom icon while
    # Raid Detector remains active. Promote that verified v2 catalog to the
    # fixture baseline; subsequent Wireless toggles change XML only.
    $atlasRelative='Survival\Gui\IconMapSurvival.png'
    $atlasPath=Join-Path $fakeGame $atlasRelative
    $baseline[$atlasRelative]=Get-Hash $atlasPath
    $baselineBytes[$atlasRelative]=[IO.File]::ReadAllBytes($atlasPath)
    $sharedStatePath=Join-Path $receiptRoot 'ScrapLab-Icon-Pack.json'
    $sharedStateBytes=if(Test-Path -LiteralPath $sharedStatePath){[IO.File]::ReadAllBytes($sharedStatePath)}else{$null}
    $sharedMirrorBytes=if(Test-Path -LiteralPath $sharedMirrorPath){[IO.File]::ReadAllBytes($sharedMirrorPath)}else{$null}
    if(-not$baselineHasRaidDetector){$atlasBaselineHash=$null}
    Assert-Baseline $baseline 'Exact removal'

    $orphanInstall=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $orphanInstall.Success ('Orphan-state fixture install failed: '+$orphanInstall.Error)
    foreach($relative in $targets){[IO.File]::WriteAllBytes((Join-Path $fakeGame $relative),$baselineBytes[$relative])}
    $orphanStatus=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($orphanStatus.Success-and(-not$orphanStatus.Installed)-and$orphanStatus.CanApply) ('Exact orphaned runtime files were blocked: '+$orphanStatus.CompatibilityReason)
    Assert-True (([string]$orphanStatus.CompatibilityState)-like'REINSTALL REQUIRED*') ('Exact orphaned runtime files received the wrong state: '+$orphanStatus.CompatibilityState)
    $orphanReinstall=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $orphanReinstall.Success ('Reinstall over exact orphaned runtime files failed: '+$orphanReinstall.Error)
    $orphanRemove=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True $orphanRemove.Success ('Orphan-state cleanup removal failed: '+$orphanRemove.Error)
    $sharedMirrorBytes=if(Test-Path -LiteralPath $sharedMirrorPath){[IO.File]::ReadAllBytes($sharedMirrorPath)}else{$null}
    Reset-SharedState
    Assert-Baseline $baseline 'Exact orphan recovery'

    $calibration=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $calibration.Success 'Write-count calibration install failed.'
    $writeCount=$calibration.FilesPatched
    $calibrationRemove=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True $calibrationRemove.Success 'Write-count calibration removal failed.'
    $sharedMirrorBytes=if(Test-Path -LiteralPath $sharedMirrorPath){[IO.File]::ReadAllBytes($sharedMirrorPath)}else{$null}
    Assert-Baseline $baseline 'Calibration removal'
    Reset-SharedState

    for($position=1;$position-le$writeCount;$position++){
        $script:seen=0
        $hook=[Action[string,string]]{param($path,$operation);$script:seen++;if($script:seen-eq$position){throw "Injected write failure $position"}}
        $atomicType.GetField('PlanWriteCompletedForTest',$binding).SetValue($null,$hook)
        try {
            $failed=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
        }
        finally {
            $atomicType.GetField('PlanWriteCompletedForTest',$binding).SetValue($null,$null)
        }
        Assert-True (-not$failed.Success) "Injected write $position unexpectedly succeeded."
        Assert-True ($script:seen-ge$position) "Write position $position was not reached."
        Assert-Baseline $baseline "Rollback $position"
        Assert-True (-not(Test-Path -LiteralPath (Join-Path $receiptRoot 'WirelessVacuumPipe.json'))) "Rollback $position left a receipt."
    }

    $install=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $install.Success ('Surgical fixture install failed: '+$install.Error)
    $utilPath=Join-Path $fakeGame 'Survival\Scripts\util.lua'
    [IO.File]::AppendAllText($utilPath,' -- unrelated post-install edit',[Text.UTF8Encoding]::new($false))
    $remove=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True $remove.Success ('Surgical removal failed: '+$remove.Error)
    Assert-True ((Get-Hash $utilPath)-ne$baseline['Survival\Scripts\util.lua']) 'Surgical removal erased the unrelated edit.'
    Assert-True ((Get-Content -LiteralPath $utilPath -Raw).Contains('unrelated post-install edit')) 'Unrelated edit text was lost.'
    Copy-Exact $cleanSources['Survival\Scripts\util.lua'] $utilPath
    Reset-SharedState
    Assert-Baseline $baseline 'Surgical reset'

    $install=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $install.Success 'Tamper fixture install failed.'
    $vacuumPath=Join-Path $fakeGame 'Survival\Scripts\game\interactables\Vacuum.lua'
    $installedVacuum=[IO.File]::ReadAllBytes($vacuumPath)
    $vacuumText=Get-Content -LiteralPath $vacuumPath -Raw
    [IO.File]::WriteAllText($vacuumPath,$vacuumText.Replace('SCRAPLAB WIRELESS VACUUM PIPE LINK GRAPH','SCRAPLAB WIRELESS VACUUM PIPE LINK GRAFX'),[Text.UTF8Encoding]::new($false))
    $blocked=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True (-not$blocked.Success) 'Edited protected loader did not block removal.'
    [IO.File]::WriteAllBytes($vacuumPath,$installedVacuum)
    $remove=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True $remove.Success 'Removal after restoring protected snippet failed.'
    Reset-SharedState
    Assert-Baseline $baseline 'Tamper recovery'

    Write-Manifest '99999999'
    $utilText=Get-Content -LiteralPath $utilPath -Raw
    [IO.File]::WriteAllText($utilPath,$utilText+' -- compatible future-build edit',[Text.UTF8Encoding]::new($false))
    [IO.File]::SetLastWriteTimeUtc($utilPath,[DateTime]::UtcNow)
    $future=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($future.Success-and$future.CanApply) ('Compatible future build was blocked: '+$future.CompatibilityReason)
    $futureInstall=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $futureInstall.Success ('Adaptive future install failed: '+$futureInstall.Error)
    $futureRemove=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$false)
    Assert-True $futureRemove.Success ('Adaptive future removal failed: '+$futureRemove.Error)
    Assert-True ((Get-Content -LiteralPath $utilPath -Raw).Contains('compatible future-build edit')) 'Adaptive removal lost unrelated future-build text.'
    Copy-Exact $cleanSources['Survival\Scripts\util.lua'] $utilPath
    Write-Manifest '24529696';Reset-SharedState

    $install=Invoke-Static $serviceType 'SetEnabledAt' @($fakeGame,$backupRoot,$true)
    Assert-True $install.Success 'Overwrite fixture install failed.'
    foreach($relative in $targets){[IO.File]::WriteAllBytes((Join-Path $fakeGame $relative),$baselineBytes[$relative])}
    foreach($relative in $ownedTargets){Remove-Item -LiteralPath (Join-Path $fakeGame $relative) -Force -ErrorAction SilentlyContinue}
    if($null-ne$sharedStateBytes){[IO.File]::WriteAllBytes((Join-Path $receiptRoot 'ScrapLab-Icon-Pack.json'),$sharedStateBytes)}
    $overwritten=Invoke-Static $serviceType 'GetStatusAt' @($fakeGame)
    Assert-True ($overwritten.Success -and (-not $overwritten.Installed)) 'Steam-overwrite fixture did not report uninstalled.'
    Assert-True (([string]$overwritten.CompatibilityState) -like 'REINSTALL REQUIRED*') ('Steam-overwrite risk state is wrong: '+$overwritten.CompatibilityState)
    Reset-SharedState
    Assert-Baseline $baseline 'Steam overwrite reset'
}
finally{
    $atomicType.GetField('PlanWriteCompletedForTest',$binding).SetValue($null,$null)
    $supportType.GetField('PatchStateRootOverride',$binding).SetValue($null,$null)
    if(Test-Path -LiteralPath $fixtureRoot){Remove-Item -LiteralPath $fixtureRoot -Recurse -Force}
}

'Wireless Vacuum Pipe Phase 5 patch-service regression passed.'
