[CmdletBinding()]
param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic'
)

$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$partRoot = Join-Path $kitRoot 'source\Patching\Parts\NetworkStorageChest'
$developmentReceiptPath = Join-Path $kitRoot 'dist\phase0-backups\NetworkStorageChest\active.json'
$activeStateRoot = Join-Path $env:LOCALAPPDATA 'ScrapLab\Patch State\Active'
$wirelessReceiptPath = Join-Path $activeStateRoot 'WirelessVacuumPipe.json'
$iconPackStatePath = Join-Path $activeStateRoot 'ScrapLab-Icon-Pack.json'
$atlasStatePath = Join-Path $env:LOCALAPPDATA 'ScrapLab\Game Backups\Scrap Mechanic\Secret Mods\ScrapLab-Shared-Icon-Atlas\atlas-receipt.json'
$cachePath = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
$backupRoot = Join-Path $kitRoot (Join-Path 'dist\phase5-backups\NetworkStorageChest' $stamp)
$partUuid = 'bc7576a7-f226-459a-883c-e8460e955d63'
$languages = @('Brazilian','Chinese','English','French','German','Italian','Japanese','Korean','Polish','Russian','Spanish')

function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Assert-Hash([string]$Path, [string]$Expected, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "$Label is missing: $Path" }
    $actual = Get-Sha $Path
    if (-not [string]::Equals($actual, $Expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label changed unexpectedly. Expected $Expected, got $actual."
    }
}
function Get-TextState([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($bom) { 3 } else { 0 }
    $text = [Text.Encoding]::UTF8.GetString($bytes, $offset, $bytes.Length - $offset)
    if ($text.Contains("`r`n") -and $text.Replace("`r`n", '').Contains("`n")) { throw "Mixed newlines are unsupported: $Path" }
    [pscustomobject]@{ Text = $text; HasBom = $bom; Newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" } }
}
function Write-AtomicText([string]$Path, [string]$Text, [bool]$HasBom) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    $temporary = $Path + '.scraplab-phase5-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swap = $Path + '.scraplab-phase5-' + [Guid]::NewGuid().ToString('N') + '.swap'
    try {
        [IO.File]::WriteAllText($temporary, $Text, [Text.UTF8Encoding]::new($HasBom))
        if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($temporary, $Path, $swap) }
        else { [IO.File]::Move($temporary, $Path) }
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
    }
}
function Copy-Atomic([string]$Source, [string]$Destination) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Destination)) | Out-Null
    $temporary = $Destination + '.scraplab-phase5-' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swap = $Destination + '.scraplab-phase5-' + [Guid]::NewGuid().ToString('N') + '.swap'
    try {
        [IO.File]::Copy($Source, $temporary, $true)
        if (Test-Path -LiteralPath $Destination) { [IO.File]::Replace($temporary, $Destination, $swap) }
        else { [IO.File]::Move($temporary, $Destination) }
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        if (Test-Path -LiteralPath $swap) { Remove-Item -LiteralPath $swap -Force }
    }
}
function Save-Json([string]$Path, [object]$Value, [int]$Depth = 12) {
    Write-AtomicText $Path ($Value | ConvertTo-Json -Depth $Depth -Compress) $false
}
function Quote-Json([string]$Value) { ConvertTo-Json ([string]$Value) -Compress }
function Set-InventoryDescription([string]$Path, [object]$Translation) {
    $state = Get-TextState $Path
    $escaped = [regex]::Escape($partUuid)
    $without = [regex]::Replace($state.Text, '(?ms)^[\t ]*"' + $escaped + '"\s*:\s*\{.*?^[\t ]*\},\r?\n', '')
    if (([regex]::Matches($without, $escaped)).Count -ne 0) { throw "Could not isolate an existing terminal description in $Path" }
    $opening = '{' + $state.Newline
    $openingIndex = $without.IndexOf($opening, [StringComparison]::Ordinal)
    if ($openingIndex -lt 0) { throw "Unexpected inventory-description root: $Path" }
    $entry = "`t`"$partUuid`": {" + $state.Newline +
        "`t`t`"description`": " + (Quote-Json $Translation.inventoryDescription) + ',' + $state.Newline +
        "`t`t`"title`": " + (Quote-Json $Translation.inventoryTitle) + ',' + $state.Newline +
        "`t`t`"upperCaseTitle`": " + (Quote-Json $Translation.inventoryUpper) + $state.Newline +
        "`t}," + $state.Newline
    $insertAt = $openingIndex + $opening.Length
    $output = $without.Substring(0, $insertAt) + $entry + $without.Substring($insertAt)
    if (([regex]::Matches($output, $escaped)).Count -ne 1) { throw "Generated description is not unique: $Path" }
    [pscustomobject]@{ Text = $output; HasBom = $state.HasBom }
}
function Set-OwnedReceipt([object]$Receipt, [string]$RelativePath, [string]$Hash) {
    $entry = $Receipt.Owned | Where-Object { $_.RelativePath -eq $RelativePath } | Select-Object -First 1
    if ($entry) { $entry.Hash = $Hash }
    else { $Receipt.Owned = @($Receipt.Owned) + [pscustomobject]@{ RelativePath = $RelativePath; Hash = $Hash } }
}
function Set-TargetReceipt([object]$Receipt, [string]$RelativePath, [string]$BackupPath, [string]$SourceHash, [string]$OutputHash) {
    $entry = $Receipt.Targets | Where-Object { $_.RelativePath -eq $RelativePath } | Select-Object -First 1
    if ($entry) { $entry.OutputHash = $OutputHash }
    else {
        $Receipt.Targets = @($Receipt.Targets) + [pscustomobject]@{
            RelativePath = $RelativePath; BackupPath = $BackupPath; SourceHash = $SourceHash; OutputHash = $OutputHash
        }
    }
}

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Xaml
$presentationCore = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'PresentationCore' } | Select-Object -First 1 -ExpandProperty Location
$windowsBase = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'WindowsBase' } | Select-Object -First 1 -ExpandProperty Location
$systemXaml = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'System.Xaml' } | Select-Object -First 1 -ExpandProperty Location
if (-not $presentationCore -or -not $windowsBase -or -not $systemXaml) { throw 'The Windows PNG codec assemblies were not found.' }
Add-Type -ReferencedAssemblies $presentationCore,$windowsBase,$systemXaml -TypeDefinition @'
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
public static class ScrapLabPhase5Atlas {
    private sealed class RawImage { public int Width, Height, Stride; public byte[] Pixels; }
    private static RawImage Decode(string path) {
        using (FileStream input = File.OpenRead(path)) {
            PngBitmapDecoder decoder = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count != 1) throw new InvalidOperationException("PNG must contain one frame.");
            BitmapSource source = decoder.Frames[0];
            if (source.Format != PixelFormats.Bgra32) source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = source.PixelWidth * 4; byte[] pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            return new RawImage { Width = source.PixelWidth, Height = source.PixelHeight, Stride = stride, Pixels = pixels };
        }
    }
    private static void Save(RawImage image, string path) {
        BitmapSource source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Pixels, image.Stride);
        PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using (FileStream output = File.Create(path)) encoder.Save(output);
    }
    public static void Apply(string atlasPath, string iconPath, string outputPath, int x, int y) {
        RawImage atlas = Decode(atlasPath), icon = Decode(iconPath);
        if (atlas.Width != 4096 || atlas.Height != 4096 || icon.Width != 96 || icon.Height != 96)
            throw new InvalidOperationException("Unsupported atlas or icon dimensions.");
        byte[] before = (byte[])atlas.Pixels.Clone();
        for (int py = 0; py < 96; py++) for (int px = 0; px < 96; px++)
            if (atlas.Pixels[(y + py) * atlas.Stride + (x + px) * 4 + 3] != 0)
                throw new InvalidOperationException("The selected bottom-row atlas cell is occupied.");
        for (int py = 0; py < 96; py++)
            Buffer.BlockCopy(icon.Pixels, py * icon.Stride, atlas.Pixels, (y + py) * atlas.Stride + x * 4, 96 * 4);
        Save(atlas, outputPath);
        RawImage verify = Decode(outputPath);
        for (int py = 0; py < atlas.Height; py++) for (int px = 0; px < atlas.Width; px++) {
            int i = py * atlas.Stride + px * 4;
            if (px >= x && px < x + 96 && py >= y && py < y + 96) {
                int ii = (py - y) * icon.Stride + (px - x) * 4;
                for (int c = 0; c < 4; c++) if (verify.Pixels[i+c] != icon.Pixels[ii+c])
                    throw new InvalidOperationException("The managed icon tile failed verification.");
            } else for (int c = 0; c < 4; c++) if (before[i+c] != verify.Pixels[i+c])
                throw new InvalidOperationException("A decoded pixel outside the managed icon tile changed.");
        }
    }
}
'@

if (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue) { throw 'Scrap Mechanic is running. Close it before installing Phase 5.' }
foreach ($path in @($developmentReceiptPath,$wirelessReceiptPath,$iconPackStatePath,$atlasStatePath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required active state is missing: $path" }
}

$development = Get-Content -LiteralPath $developmentReceiptPath -Raw | ConvertFrom-Json
$wireless = Get-Content -LiteralPath $wirelessReceiptPath -Raw | ConvertFrom-Json
$iconPack = Get-Content -LiteralPath $iconPackStatePath -Raw | ConvertFrom-Json
$atlasState = Get-Content -LiteralPath $atlasStatePath -Raw | ConvertFrom-Json
$localization = Get-Content -LiteralPath (Join-Path $partRoot 'NetworkStorageChest.localization.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($localization.PSObject.Properties.Name.Count -ne 11) { throw 'The canonical localization catalog must contain exactly 11 languages.' }

$loaderRelative = 'Survival\Scripts\game\SurvivalGame.lua'
$terminalRelative = 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.lua'
$guiRelative = 'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChest.gui'
$itemGuiRelative = 'Survival\Gui\JsonGuis\ScrapLab\Parts\NetworkStorageChestItem.gui'
$localizationRelative = 'Survival\Scripts\ScrapLab\Parts\NetworkStorageChest\NetworkStorageChest.localization.json'
$harnessRelative = 'Survival\Scripts\ScrapLab\Development\NetworkStorageChestPhase5Harness.lua'
$loaderPath = Join-Path $GamePath $loaderRelative
$terminalPath = Join-Path $GamePath $terminalRelative
$guiPath = Join-Path $GamePath $guiRelative
$itemGuiPath = Join-Path $GamePath $itemGuiRelative
$localizationPath = Join-Path $GamePath $localizationRelative
$harnessPath = Join-Path $GamePath $harnessRelative
$xmlPath = Join-Path $GamePath 'Survival\Gui\IconMapSurvival.xml'
$atlasPath = Join-Path $GamePath 'Survival\Gui\IconMapSurvival.png'
$iconPath = Join-Path $partRoot 'NetworkStorageChestIcon.png'

foreach ($relative in @($loaderRelative,$terminalRelative,$guiRelative,$itemGuiRelative)) {
    $target = $development.Targets | Where-Object RelativePath -eq $relative | Select-Object -First 1
    $owned = $development.Owned | Where-Object RelativePath -eq $relative | Select-Object -First 1
    $entry = if ($target) { $target.OutputHash } else { $owned.Hash }
    Assert-Hash (Join-Path $GamePath $relative) $entry $relative
}
Assert-Hash $atlasPath $atlasState.AtlasOutputHash 'Shared icon atlas'
Assert-Hash $xmlPath $atlasState.IconXmlHash 'Shared icon XML'
Assert-Hash $atlasState.BaselinePath $atlasState.BaselineHash 'Shared icon baseline'
if (([regex]::Matches((Get-Content -LiteralPath $xmlPath -Raw), [regex]::Escape($partUuid))).Count -ne 0) {
    throw 'The Network Storage Chest icon XML registration already exists.'
}
if ($atlasState.Icons | Where-Object { $_.Uuid -eq $partUuid }) { throw 'The shared atlas receipt already owns the Network Storage Chest icon.' }

$loaderState = Get-TextState $loaderPath
$phase4Entry = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase4Harness.lua" )'
$phase5Entry = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Development/NetworkStorageChestPhase5Harness.lua" )'
if (([regex]::Matches($loaderState.Text, [regex]::Escape($phase4Entry))).Count -ne 1 -or $loaderState.Text.Contains($phase5Entry)) {
    throw 'The protected Phase 4 loader anchor is missing, duplicated, or Phase 5 is already present.'
}
$newLoader = $loaderState.Text.Replace($phase4Entry, $phase4Entry + $loaderState.Newline + $phase5Entry)

$xmlState = Get-TextState $xmlPath
$closeIndex = $xmlState.Text.LastIndexOf('        </Group>', [StringComparison]::Ordinal)
if ($closeIndex -lt 0) { throw 'The ItemIcons group closing tag was not found.' }
$xmlEntry = '            <!-- SCRAPLAB PART: Network Storage Chest icon. -->' + $xmlState.Newline +
    '            <Index name="' + $partUuid + '">' + $xmlState.Newline +
    '                <Frame point="3744 3936"/>' + $xmlState.Newline +
    '            </Index>' + $xmlState.Newline
$newXml = $xmlState.Text.Substring(0, $closeIndex) + $xmlEntry + $xmlState.Text.Substring($closeIndex)

[IO.Directory]::CreateDirectory($backupRoot) | Out-Null
$backupMap = @{}
$created = New-Object System.Collections.Generic.List[string]
function Backup-File([string]$Path, [string]$Name) {
    $destination = Join-Path $backupRoot $Name
    [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
    [IO.File]::Copy($Path, $destination, $true)
    $script:backupMap[$Path] = $destination
}
foreach ($pair in @(
    @($loaderPath,'SurvivalGame.lua'),@($terminalPath,'NetworkStorageChest.lua'),@($guiPath,'NetworkStorageChest.gui'),
    @($itemGuiPath,'NetworkStorageChestItem.gui'),@($xmlPath,'IconMapSurvival.xml'),@($atlasPath,'IconMapSurvival.png'),
    @($developmentReceiptPath,'NetworkStorage-active.json'),@($wirelessReceiptPath,'WirelessVacuumPipe-active.json'),
    @($iconPackStatePath,'ScrapLab-Icon-Pack.json'),@($atlasStatePath,'atlas-receipt.json')
)) { Backup-File $pair[0] $pair[1] }
foreach ($language in $languages) {
    $languagePath = Join-Path $GamePath "Survival\Gui\Language\$language\inventoryDescriptions.json"
    Backup-File $languagePath "Language\$language\inventoryDescriptions.json"
}
if (-not (Test-Path -LiteralPath $localizationPath)) { $created.Add($localizationPath) }
if (-not (Test-Path -LiteralPath $harnessPath)) { $created.Add($harnessPath) }

$temporaryAtlas = Join-Path $backupRoot 'IconMapSurvival.phase5.png'
[ScrapLabPhase5Atlas]::Apply($atlasPath, $iconPath, $temporaryAtlas, 3744, 3936)

try {
    Write-AtomicText $loaderPath $newLoader $loaderState.HasBom
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChest.lua') $terminalPath
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChest.gui') $guiPath
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChestItem.gui') $itemGuiPath
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChest.localization.json') $localizationPath
    Copy-Atomic (Join-Path $partRoot 'NetworkStorageChestPhase5Harness.lua') $harnessPath
    foreach ($language in $languages) {
        $relative = "Survival\Gui\Language\$language\inventoryDescriptions.json"
        $path = Join-Path $GamePath $relative
        $beforeHash = Get-Sha $path
        $generated = Set-InventoryDescription $path $localization.$language
        $target = $development.Targets | Where-Object RelativePath -eq $relative | Select-Object -First 1
        $baselinePath = if ($target) { $target.BackupPath } else { Join-Path $development.BackupRoot $relative }
        if (-not $target) {
            [IO.Directory]::CreateDirectory((Split-Path -Parent $baselinePath)) | Out-Null
            [IO.File]::Copy($path, $baselinePath, $false)
        }
        Write-AtomicText $path $generated.Text $generated.HasBom
        Set-TargetReceipt $development $relative $baselinePath $beforeHash (Get-Sha $path)
        $wirelessFile = $wireless.Files | Where-Object RelativePath -eq $relative | Select-Object -First 1
        if ($wirelessFile) { $wirelessFile.OutputHash = Get-Sha $path }
    }
    Write-AtomicText $xmlPath $newXml $xmlState.HasBom
    Copy-Atomic $temporaryAtlas $atlasPath

    $loaderTarget = $development.Targets | Where-Object RelativePath -eq $loaderRelative | Select-Object -First 1
    $loaderTarget.OutputHash = Get-Sha $loaderPath
    Set-OwnedReceipt $development $terminalRelative (Get-Sha $terminalPath)
    Set-OwnedReceipt $development $guiRelative (Get-Sha $guiPath)
    Set-OwnedReceipt $development $itemGuiRelative (Get-Sha $itemGuiPath)
    Set-OwnedReceipt $development $localizationRelative (Get-Sha $localizationPath)
    Set-OwnedReceipt $development $harnessRelative (Get-Sha $harnessPath)
    $development.SchemaVersion = 3

    $placement = [pscustomobject]@{ ModKey='NetworkStorageChest'; Uuid=$partUuid; X=3744; Y=3936; IconHash=(Get-Sha $iconPath) }
    foreach ($state in @($iconPack,$atlasState)) {
        $state.CatalogVersion = '3'
        $state.ActiveMods = @($state.ActiveMods | Where-Object { $_ -ne 'NetworkStorageChest' }) + 'NetworkStorageChest'
        $state.Icons = @($state.Icons) + $placement
        $state.AtlasOutputHash = Get-Sha $atlasPath
        $state.IconXmlHash = Get-Sha $xmlPath
        $state.UpdatedUtc = [DateTime]::UtcNow.ToString('o')
    }
    $wirelessXml = $wireless.Files | Where-Object RelativePath -eq 'Survival\Gui\IconMapSurvival.xml' | Select-Object -First 1
    if ($wirelessXml) { $wirelessXml.OutputHash = Get-Sha $xmlPath }

    Save-Json $developmentReceiptPath $development
    Save-Json $wirelessReceiptPath $wireless
    Save-Json $iconPackStatePath $iconPack
    Save-Json $atlasStatePath $atlasState

    Assert-Hash $terminalPath (($development.Owned | Where-Object RelativePath -eq $terminalRelative).Hash) 'Phase 5 terminal script'
    Assert-Hash $guiPath (($development.Owned | Where-Object RelativePath -eq $guiRelative).Hash) 'Phase 5 GUI'
    Assert-Hash $itemGuiPath (($development.Owned | Where-Object RelativePath -eq $itemGuiRelative).Hash) 'Phase 5 item card'
    Assert-Hash $atlasPath $atlasState.AtlasOutputHash 'Phase 5 shared atlas'
    Assert-Hash $xmlPath $atlasState.IconXmlHash 'Phase 5 icon XML'
    if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    Write-Host "Phase 5 installed and verified. Rollback backup: $backupRoot"
}
catch {
    foreach ($path in $backupMap.Keys) { [IO.File]::Copy($backupMap[$path], $path, $true) }
    foreach ($path in $created) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } }
    throw
}
