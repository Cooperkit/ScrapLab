$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$part = Join-Path $root 'source\Patching\Parts\NetworkStorageChest'
$lua = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChest.lua') -Raw -Encoding UTF8
$gui = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChest.gui') -Raw -Encoding UTF8 | ConvertFrom-Json
$item = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChestItem.gui') -Raw -Encoding UTF8 | ConvertFrom-Json
$localization = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChest.localization.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$harness = Get-Content -LiteralPath (Join-Path $part 'NetworkStorageChestPhase5Harness.lua') -Raw -Encoding UTF8
$deployer = Get-Content -LiteralPath (Join-Path $root 'tools\Deploy-NetworkStoragePhase5.ps1') -Raw -Encoding UTF8
$coordinator = Get-Content -LiteralPath (Join-Path $root 'source\Patching\ScrapLabIconAtlasCoordinator.cs') -Raw
$build = Get-Content -LiteralPath (Join-Path $root 'build.ps1') -Raw

function Assert-True([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) { Assert-True $Text.Contains($Needle) $Message }
function Find-Widget([object]$Node, [string]$Name) {
    if ($Node.Name -eq $Name) { return $Node }
    foreach ($child in @($Node.Childs)) { $found = Find-Widget $child $Name; if ($found) { return $found } }
    return $null
}

$requiredLanguages = @('Brazilian','Chinese','English','French','German','Italian','Japanese','Korean','Polish','Russian','Spanish')
Assert-True ($localization.PSObject.Properties.Name.Count -eq 11) 'Localization catalog does not contain exactly 11 languages.'
foreach ($language in $requiredLanguages) {
    $entry = $localization.$language
    Assert-True ($null -ne $entry) "Missing language: $language"
    foreach ($key in @('inventoryTitle','inventoryUpper','inventoryDescription','title','catalog','takeAll','backpack','hotbar')) {
        Assert-True (-not [string]::IsNullOrWhiteSpace([string]$entry.$key)) "$language is missing $key."
    }
}
foreach ($key in @('inventoryDepositHelp','inventoryDepositClick','inventoryDepositWorking',
    'inventoryDepositSuccess','inventoryDepositFull','inventoryDepositChanged','inventoryDepositRetry')) {
    Assert-True (-not [string]::IsNullOrWhiteSpace([string]$localization.English.$key)) "English is missing $key."
}

Assert-True ($gui.width -eq 1120 -and $gui.height -eq 540) 'The accepted compact 1120x540 footprint changed.'
foreach ($name in @('SearchInput','SortButton','ClearSearch','CatalogScrollHost','IndexProgressHolder',
    'IndexProgressFill','SelectionStatus','TakeOneButton','TakeStackButton','TakeAllButton',
    'SelectedIconImage','SelectedDetail','PlayerInventoryScrollHost','DepositBox','DepositHelp')) {
    Assert-True ($null -ne (Find-Widget $gui $name)) "Missing GUI widget: $name"
}
Assert-True ((Find-Widget $gui 'IndexProgressFill').Skin -eq 'DressbotProgress') 'Indexing does not use the native progress skin.'
Assert-True ((Find-Widget $gui 'DepositBox').ContainerData.ContainerWidth -eq 320) 'The five-slot deposit tray width changed.'
Assert-True ((Find-Widget $gui 'DepositBox').ContainerData.ItemSize.width -eq 64) 'The deposit tray lost its compact five-slot rhythm.'
$playerHost = Find-Widget $gui 'PlayerInventoryScrollHost'
Assert-True ($playerHost.height -eq 256 -and $playerHost.width -eq 342) 'The unified player-inventory viewport changed.'
Assert-True ($null -eq (Find-Widget $gui 'InventoryBox') -and $null -eq (Find-Widget $gui 'HotbarBox')) 'Duplicate native player-inventory renderers returned.'
Assert-True ($null -eq $gui.Hotbar) 'The flicker-prone engine hotbar overlay is still enabled.'
foreach ($removed in @('Subtitle','CatalogLabel','CatalogCount','BufferStatus','ProbeStatus','BackpackTab','HotbarTab','CombinedInventory')) {
    Assert-True ($null -eq (Find-Widget $gui $removed)) "Redundant or obsolete widget still exists: $removed"
}
Assert-True ((Find-Widget $gui 'CatalogScrollHost').height -eq 300) 'The catalog did not receive the reclaimed heading space.'
Assert-True ($item.NeedKey -eq $true) 'Catalog cards are not keyboard/controller focusable.'
Assert-True ($null -ne (Find-Widget $item 'CatalogItemRoute')) 'Catalog cards do not show local/wireless route markers.'

foreach ($needle in @('buildVisibleCatalog','sourceKind','localSources','wirelessSources','crossWorldSources',
    'cl_applyLocalization','IndexProgressHolder','SelectedIconImage','cl_n_runPhase5UiQualification',
    'PlayerInventoryScrollHost','cl_rebuildPlayerInventory','hotbarBinding','playerInventorySlots',
    'cl_restoreCatalogScroll','selectionPreservedScroll','slotAccurate')) {
    Assert-Contains $lua $needle "Terminal script is missing Phase 5 behavior: $needle"
}
foreach ($needle in @('sv_n_stageInventorySlot','lastDepositTick','inventory:getRevision() ~= tonumber( data.revision )',
    'sm.container.spendFromSlot( inventory, slot, item.uuid, moved, true )','sm.container.collect( buffer, item.uuid, moved )')) {
    Assert-Contains $lua $needle "Slot-accurate click deposit is missing: $needle"
}
Assert-True (-not $lua.Contains([string][char]0x00C2)) 'Terminal script contains mojibake.'
Assert-True (-not $lua.Contains([string][char]0x00B7)) 'Terminal script still uses the encoding-sensitive middle-dot separator.'
Assert-True (-not $lua.Contains('sm.localPlayer.getHotbar()')) 'Limited-inventory mode still calls the forbidden hotbar accessor.'
foreach ($removed in @('CatalogCount','BufferStatus','ProbeStatus','cl_applyInventoryMode','findRequiredWidget( guiData, "HotbarBox" )')) {
    Assert-True (-not $lua.Contains($removed)) "Terminal script still references removed UI behavior: $removed"
}

foreach ($needle in @('/slstorage5','Phase 5 automatic UI','inventory-language-','Temporary terminal removed')) {
    Assert-Contains $harness $needle "Phase 5 automatic harness is missing: $needle"
}
foreach ($needle in @('NetworkStorageChest.localization.json','NetworkStorageChestPhase5Harness.lua',
    'ScrapLabPhase5Atlas','3744 3936','decoded pixel outside the managed icon tile','CatalogVersion = ''3''')) {
    Assert-Contains $deployer $needle "Phase 5 deployer is missing safety/integration behavior: $needle"
}
Assert-Contains $coordinator 'ModKey = "NetworkStorageChest"' 'Shared icon catalog does not include Network Storage Chest.'
Assert-Contains $coordinator 'internal const string CatalogVersion = "3"' 'Shared icon catalog definition was not raised to 3.'
Assert-Contains $build 'RaidRescue.Parts.NetworkStorageChest.NetworkStorageChestIcon.png' 'Build does not embed the Network Storage Chest icon.'

Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $part 'NetworkStorageChestIcon.png'
$icon = [Drawing.Bitmap]::FromFile($iconPath)
try {
    Assert-True ($icon.Width -eq 96 -and $icon.Height -eq 96) 'Prepared icon is not 96x96.'
    $transparent = 0
    for ($y = 0; $y -lt $icon.Height; $y++) { for ($x = 0; $x -lt $icon.Width; $x++) { if ($icon.GetPixel($x,$y).A -eq 0) { $transparent++ } } }
    Assert-True ($transparent -gt 4000) 'Prepared icon does not retain a transparent background.'
}
finally { $icon.Dispose() }

Write-Host 'Network Storage Chest Phase 5 regression passed.'
