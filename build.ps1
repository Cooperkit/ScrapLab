$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "source"
$output = Join-Path $root "dist"

function Get-SourcePath([string]$RelativePath) {
    $path = Join-Path $source $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "A required source file is missing: $RelativePath"
    }
    return $path
}

$manifest = Get-SourcePath "Assets\app.manifest"
$icon = Get-SourcePath "Assets\ScrapLab.ico"

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$compiler = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $compiler) {
    throw "The .NET Framework C# compiler was not found."
}

$frameworkWpf = Join-Path (Split-Path -Parent $compiler) 'WPF'
$presentationCore = Join-Path $frameworkWpf 'PresentationCore.dll'
$windowsBase = Join-Path $frameworkWpf 'WindowsBase.dll'
if (-not (Test-Path -LiteralPath $presentationCore) -or
    -not (Test-Path -LiteralPath $windowsBase)) {
    throw "The Windows PNG codec assemblies were not found."
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

$mainSources = @(
    "App\Program.cs",
    "App\UiHtml.cs",
    "App\AppUpdateService.cs",
    "App\PatchHelperClient.cs",
    "App\AssemblyInfo.cs",
    "Shared\ProductPaths.cs",
    "Shared\Models.cs",
    "Shared\GameInstallLocator.cs",
    "Shared\PatchHelperProtocol.cs",
    "Shared\CompanionSecurity.cs",
    "World\LuaStorage.cs",
    "World\WorldStorage.cs",
    "World\SqliteNative.cs",
    "World\ItemCatalog.cs",
    "World\RaidService.cs",
    "Performance\PerformanceScanner.cs",
    "Performance\PerformanceHotspotRanker.cs",
    "Performance\PerformanceScanOperationManager.cs",
    "Performance\PerformanceReportExporter.cs"
) | ForEach-Object { Get-SourcePath $_ }

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$manifest" `
    "/win32icon:$icon" `
    "/out:$output\ScrapLab.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Core.dll `
    $mainSources

if ($LASTEXITCODE -ne 0) {
    throw "ScrapLab did not compile."
}

$patchSources = @(
    "Companions\PatchHelper\PatchHelperProgram.cs",
    "Companions\PatchHelper\PatchHelperAssemblyInfo.cs",
    "Shared\ProductPaths.cs",
    "Shared\PatchHelperProtocol.cs",
    "Shared\CompanionSecurity.cs",
    "Shared\Models.cs",
    "Patching\GamePatchService.cs",
    "Patching\NoclipAssetSupport.cs",
    "Patching\RevivalBuffPatchService.cs",
    "Patching\CarrySprintPatchService.cs",
    "Patching\AdaptiveMultiFileModService.cs",
    "Patching\BetterEnginesPatchService.cs",
    "Patching\BetterFreezerBeehivePatchService.cs",
    "Patching\BetterPlasmaDrillsPatchService.cs",
    "Patching\AtomicCustomPartPatchSupport.cs",
    "Patching\ScrapLabIconAtlasCoordinator.cs",
    "Patching\RaidDetectorPatchService.cs",
    "Patching\ScrapLabCraftbotRecipeOrder.cs",
    "Patching\WirelessVacuumPipePatchService.cs",
    "Patching\NetworkStorageChestPatchService.cs",
    "Patching\TreeSaplingsPatchService.cs",
    "Patching\GameplayModsBatchCoordinator.cs",
    "Patching\AdaptivePatchSupport.cs"
) | ForEach-Object { Get-SourcePath $_ }

$noclipModule = Get-SourcePath "Patching\Scripts\ScrapLabNoclip.lua"
$noclipInputTool = Get-SourcePath "Patching\Scripts\ScrapLabNoclipInputTool.lua"
$raidDetectorScript = Get-SourcePath "Patching\Parts\RaidDetector\RaidDetector.lua"
$raidDetectorLegacyScript = Get-SourcePath "Patching\Parts\RaidDetector\RaidDetectorLegacyV1.lua"
$raidDetectorShape = Get-SourcePath "Patching\Parts\RaidDetector\RaidDetector.shapeset"
$raidDetectorIcon = Get-SourcePath "Patching\Parts\RaidDetector\RaidDetectorIcon.png"
$raidDetectorLegacyIcon = Get-SourcePath "Patching\Parts\RaidDetector\RaidDetectorIconLegacyOpaque.png"
$wirelessPipeManager = Get-SourcePath "Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua"
$wirelessPipeGraph = Get-SourcePath "Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua"
$wirelessPipeTransfer = Get-SourcePath "Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeTransfer.lua"
$wirelessPipeScript = Get-SourcePath "Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua"
$wirelessPipeShape = Get-SourcePath "Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.shapeset"
$wirelessPipeLayout = Get-SourcePath "Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.layout"
$wirelessPipeIcon = Get-SourcePath "Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipeIcon.png"
$networkStorageChestIcon = Get-SourcePath "Patching\Parts\NetworkStorageChest\NetworkStorageChestIcon.png"
$networkStorageChestScript = Get-SourcePath "Patching\Parts\NetworkStorageChest\NetworkStorageChest.lua"
$networkStorageChestIndex = Get-SourcePath "Patching\Scripts\ScrapLab\Storage\NetworkInventoryIndex.lua"
$networkStorageChestGui = Get-SourcePath "Patching\Parts\NetworkStorageChest\NetworkStorageChest.gui"
$networkStorageChestItemGui = Get-SourcePath "Patching\Parts\NetworkStorageChest\NetworkStorageChestItem.gui"
$networkStorageChestShape = Get-SourcePath "Patching\Parts\NetworkStorageChest\NetworkStorageChest.shapeset"
$networkStorageChestLocalization = Get-SourcePath "Patching\Parts\NetworkStorageChest\NetworkStorageChest.localization.json"
$treeSaplingTool = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingTool.lua"
$treeSaplingHeldMesh = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeld.fbx"
$treeSaplingHeldSkinnedMesh = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeld.dae"
$treeSaplingHeldRenderable = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeld.rend"
$treeSaplingHeldFpMesh = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeldFp.fbx"
$treeSaplingHeldFpSkinnedMesh = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeldFp.dae"
$treeSaplingHeldFpRenderable = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeldFp.rend"
$treeSaplingHeldTpMesh = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeldTp.fbx"
$treeSaplingHeldTpSkinnedMesh = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeldTp.dae"
$treeSaplingHeldTpRenderable = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHeldTp.rend"
$treeSaplingHeldVisual = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingVisual.generated.lua"
$treeSaplingHarvestable = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingHarvestable.lua"
$treeSaplingShape = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplings.shapeset"
$treeSaplingTools = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplings.tools.json"
$treeSaplingHarvestables = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplings.harvestableset"
$treeSaplingPotCollision = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplingPotCollision.obj"
$treeSaplingLocalization = Get-SourcePath "Patching\Parts\TreeSaplings\TreeSaplings.localization.json"
$treeSaplingSmallIcon = Get-SourcePath "Patching\Parts\TreeSaplings\SmallTreeSaplingIcon.png"
$treeSaplingMediumIcon = Get-SourcePath "Patching\Parts\TreeSaplings\MediumTreeSaplingIcon.png"
$treeSaplingLargeIcon = Get-SourcePath "Patching\Parts\TreeSaplings\LargeTreeSaplingIcon.png"

& $compiler `
    /nologo `
    /target:exe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$manifest" `
    "/win32icon:$icon" `
    "/resource:$noclipModule,RaidRescue.ScrapLabNoclip.lua" `
    "/resource:$noclipInputTool,RaidRescue.ScrapLabNoclipInputTool.lua" `
    "/resource:$raidDetectorScript,RaidRescue.Parts.RaidDetector.RaidDetector.lua" `
    "/resource:$raidDetectorLegacyScript,RaidRescue.Parts.RaidDetector.RaidDetectorLegacyV1.lua" `
    "/resource:$raidDetectorShape,RaidRescue.Parts.RaidDetector.RaidDetector.shapeset" `
    "/resource:$raidDetectorIcon,RaidRescue.Parts.RaidDetector.RaidDetectorIcon.png" `
    "/resource:$raidDetectorLegacyIcon,RaidRescue.Parts.RaidDetector.RaidDetectorIconLegacyOpaque.png" `
    "/resource:$wirelessPipeManager,RaidRescue.Parts.WirelessVacuumPipe.WirelessPipeManager.lua" `
    "/resource:$wirelessPipeGraph,RaidRescue.Parts.WirelessVacuumPipe.ScrapLabPipeGraph.lua" `
    "/resource:$wirelessPipeTransfer,RaidRescue.Parts.WirelessVacuumPipe.WirelessPipeTransfer.lua" `
    "/resource:$wirelessPipeScript,RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.lua" `
    "/resource:$wirelessPipeShape,RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.shapeset" `
    "/resource:$wirelessPipeLayout,RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.layout" `
    "/resource:$wirelessPipeIcon,RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipeIcon.png" `
    "/resource:$networkStorageChestIcon,RaidRescue.Parts.NetworkStorageChest.NetworkStorageChestIcon.png" `
    "/resource:$networkStorageChestScript,RaidRescue.Parts.NetworkStorageChest.NetworkStorageChest.lua" `
    "/resource:$networkStorageChestIndex,RaidRescue.Parts.NetworkStorageChest.NetworkInventoryIndex.lua" `
    "/resource:$networkStorageChestGui,RaidRescue.Parts.NetworkStorageChest.NetworkStorageChest.gui" `
    "/resource:$networkStorageChestItemGui,RaidRescue.Parts.NetworkStorageChest.NetworkStorageChestItem.gui" `
    "/resource:$networkStorageChestShape,RaidRescue.Parts.NetworkStorageChest.NetworkStorageChest.shapeset" `
    "/resource:$networkStorageChestLocalization,RaidRescue.Parts.NetworkStorageChest.NetworkStorageChest.localization.json" `
    "/resource:$treeSaplingHeldMesh,RaidRescue.Parts.TreeSaplings.TreeSaplingHeld.fbx" `
    "/resource:$treeSaplingHeldSkinnedMesh,RaidRescue.Parts.TreeSaplings.TreeSaplingHeld.dae" `
    "/resource:$treeSaplingHeldRenderable,RaidRescue.Parts.TreeSaplings.TreeSaplingHeld.rend" `
    "/resource:$treeSaplingHeldFpMesh,RaidRescue.Parts.TreeSaplings.TreeSaplingHeldFp.fbx" `
    "/resource:$treeSaplingHeldFpSkinnedMesh,RaidRescue.Parts.TreeSaplings.TreeSaplingHeldFp.dae" `
    "/resource:$treeSaplingHeldFpRenderable,RaidRescue.Parts.TreeSaplings.TreeSaplingHeldFp.rend" `
    "/resource:$treeSaplingHeldTpMesh,RaidRescue.Parts.TreeSaplings.TreeSaplingHeldTp.fbx" `
    "/resource:$treeSaplingHeldTpSkinnedMesh,RaidRescue.Parts.TreeSaplings.TreeSaplingHeldTp.dae" `
    "/resource:$treeSaplingHeldTpRenderable,RaidRescue.Parts.TreeSaplings.TreeSaplingHeldTp.rend" `
    "/resource:$treeSaplingHeldVisual,RaidRescue.Parts.TreeSaplings.TreeSaplingVisual.generated.lua" `
    "/resource:$treeSaplingTool,RaidRescue.Parts.TreeSaplings.TreeSaplingTool.lua" `
    "/resource:$treeSaplingHarvestable,RaidRescue.Parts.TreeSaplings.TreeSaplingHarvestable.lua" `
    "/resource:$treeSaplingShape,RaidRescue.Parts.TreeSaplings.TreeSaplings.shapeset" `
    "/resource:$treeSaplingTools,RaidRescue.Parts.TreeSaplings.TreeSaplings.tools.json" `
    "/resource:$treeSaplingHarvestables,RaidRescue.Parts.TreeSaplings.TreeSaplings.harvestableset" `
    "/resource:$treeSaplingPotCollision,RaidRescue.Parts.TreeSaplings.TreeSaplingPotCollision.obj" `
    "/resource:$treeSaplingLocalization,RaidRescue.Parts.TreeSaplings.TreeSaplings.localization.json" `
    "/resource:$treeSaplingSmallIcon,RaidRescue.Parts.TreeSaplings.SmallTreeSaplingIcon.png" `
    "/resource:$treeSaplingMediumIcon,RaidRescue.Parts.TreeSaplings.MediumTreeSaplingIcon.png" `
    "/resource:$treeSaplingLargeIcon,RaidRescue.Parts.TreeSaplings.LargeTreeSaplingIcon.png" `
    "/out:$output\ScrapLab.PatchHelper.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    "/reference:$presentationCore" `
    "/reference:$windowsBase" `
    /reference:System.Xaml.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Core.dll `
    $patchSources

if ($LASTEXITCODE -ne 0) {
    throw "The ScrapLab patch helper did not compile."
}

$updaterSources = @(
    "Companions\Updater\UpdaterProgram.cs",
    "Companions\Updater\UpdaterAssemblyInfo.cs",
    "Shared\ProductPaths.cs",
    "Shared\CompanionSecurity.cs"
) | ForEach-Object { Get-SourcePath $_ }

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$manifest" `
    "/win32icon:$icon" `
    "/out:$output\ScrapLab.Updater.exe" `
    /reference:System.Core.dll `
    $updaterSources

if ($LASTEXITCODE -ne 0) {
    throw "The fixed ScrapLab updater did not compile."
}

$builtFiles = @(
    "ScrapLab.exe",
    "ScrapLab.PatchHelper.exe",
    "ScrapLab.Updater.exe"
)

$signingThumbprint = $env:SCRAPLAB_SIGN_CERT_SHA1
if ([string]::IsNullOrWhiteSpace($signingThumbprint)) {
    $signingThumbprint = $env:RAID_RESCUE_SIGN_CERT_SHA1
}
if (-not [string]::IsNullOrWhiteSpace($signingThumbprint)) {
    $signTool = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if (-not $signTool) {
        throw "A ScrapLab signing certificate is configured, but signtool.exe was not found."
    }
    foreach ($name in $builtFiles) {
        & $signTool.Source sign `
            /sha1 $signingThumbprint `
            /fd SHA256 `
            /tr "http://timestamp.digicert.com" `
            /td SHA256 `
            (Join-Path $output $name)
        if ($LASTEXITCODE -ne 0) {
            throw "Authenticode signing failed for $name."
        }
    }
}

foreach ($name in $builtFiles) {
    $file = Get-Item -LiteralPath (Join-Path $output $name)
    Write-Host "Built $($file.FullName) ($($file.Length) bytes)"
}

$version = "2.11.1"
$releaseRoot = Join-Path $root "release"
$bundle = Join-Path $releaseRoot "ScrapLab-$version"
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
foreach ($name in $builtFiles) {
    Copy-Item `
        -LiteralPath (Join-Path $output $name) `
        -Destination (Join-Path $bundle $name) `
        -Force
}
$archive = Join-Path $releaseRoot "ScrapLab-$version.zip"
Compress-Archive `
    -Path (Join-Path $bundle "*") `
    -DestinationPath $archive `
    -CompressionLevel Optimal `
    -Force
Write-Host "Packaged $archive"
