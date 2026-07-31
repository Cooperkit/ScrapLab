$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "source"
$output = Join-Path $root "dist"

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

New-Item -ItemType Directory -Path $output -Force | Out-Null

$mainSources = @(
    "Program.cs",
    "Models.cs",
    "LuaStorage.cs",
    "WorldStorage.cs",
    "SqliteNative.cs",
    "ItemCatalog.cs",
    "GameInstallLocator.cs",
    "RaidService.cs",
    "PatchHelperProtocol.cs",
    "CompanionSecurity.cs",
    "PatchHelperClient.cs",
    "AppUpdateService.cs",
    "UiHtml.cs",
    "AssemblyInfo.cs"
) | ForEach-Object { Join-Path $source $_ }

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$source\app.manifest" `
    "/win32icon:$source\RaidRescue.ico" `
    "/out:$output\RaidRescue.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Core.dll `
    $mainSources

if ($LASTEXITCODE -ne 0) {
    throw "Raid Rescue did not compile."
}

$patchSources = @(
    "PatchHelperProgram.cs",
    "PatchHelperProtocol.cs",
    "CompanionSecurity.cs",
    "Models.cs",
    "GamePatchService.cs",
    "RevivalBuffPatchService.cs",
    "AdaptivePatchSupport.cs",
    "PatchHelperAssemblyInfo.cs"
) | ForEach-Object { Join-Path $source $_ }

& $compiler `
    /nologo `
    /target:exe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$source\app.manifest" `
    "/win32icon:$source\RaidRescue.ico" `
    "/out:$output\RaidRescue.PatchHelper.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Core.dll `
    $patchSources

if ($LASTEXITCODE -ne 0) {
    throw "The Raid Rescue patch helper did not compile."
}

$updaterSources = @(
    "UpdaterProgram.cs",
    "CompanionSecurity.cs",
    "UpdaterAssemblyInfo.cs"
) | ForEach-Object { Join-Path $source $_ }

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$source\app.manifest" `
    "/win32icon:$source\RaidRescue.ico" `
    "/out:$output\RaidRescue.Updater.exe" `
    /reference:System.Core.dll `
    $updaterSources

if ($LASTEXITCODE -ne 0) {
    throw "The fixed Raid Rescue updater did not compile."
}

$builtFiles = @(
    "RaidRescue.exe",
    "RaidRescue.PatchHelper.exe",
    "RaidRescue.Updater.exe"
)

$signingThumbprint = $env:RAID_RESCUE_SIGN_CERT_SHA1
if (-not [string]::IsNullOrWhiteSpace($signingThumbprint)) {
    $signTool = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if (-not $signTool) {
        throw "RAID_RESCUE_SIGN_CERT_SHA1 is set, but signtool.exe was not found."
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

$version = "1.16.0"
$releaseRoot = Join-Path $root "release"
$bundle = Join-Path $releaseRoot "RaidRescue-$version"
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
foreach ($name in $builtFiles) {
    Copy-Item `
        -LiteralPath (Join-Path $output $name) `
        -Destination (Join-Path $bundle $name) `
        -Force
}
$archive = Join-Path $releaseRoot "RaidRescue-$version.zip"
Compress-Archive `
    -Path (Join-Path $bundle "*") `
    -DestinationPath $archive `
    -CompressionLevel Optimal `
    -Force
Write-Host "Packaged $archive"
