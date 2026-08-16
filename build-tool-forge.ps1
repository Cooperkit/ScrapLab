param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "source"
$forge = Join-Path $source "ToolForge"
$output = Join-Path $root "dist\ToolForge"
$releaseRoot = Join-Path $root "release"
$version = "1.0.0"

function Get-RequiredFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "A required Tool Forge file is missing: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Reset-GeneratedDirectory([string]$Path) {
    $fullRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a directory outside the ScrapLab workspace."
    }
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$compiler = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $compiler) { throw "The .NET Framework C# compiler was not found." }

$webView = Join-Path $forge "Vendor\WebView2"
$webViewCore = Get-RequiredFile (Join-Path $webView "Microsoft.Web.WebView2.Core.dll")
$webViewForms = Get-RequiredFile (Join-Path $webView "Microsoft.Web.WebView2.WinForms.dll")
$webViewLoader = Get-RequiredFile (Join-Path $webView "WebView2Loader.dll")
$manifest = Get-RequiredFile (Join-Path $forge "tool-forge.manifest")
$icon = Get-RequiredFile (Join-Path $source "Assets\ScrapLab.ico")
$binaryFbxFixture = Get-RequiredFile (Join-Path $forge "TestAssets\blender-5-binary.fbx")
$webRequired = @(
    "Web\index.html",
    "Web\styles.css",
    "Web\app.js",
    "Web\preview.js",
    "Web\vendor\three.module.min.js",
    "Web\vendor\three.core.min.js",
    "Web\vendor\loaders\FBXLoader.js",
    "Web\vendor\loaders\ColladaLoader.js",
    "Web\vendor\loaders\TGALoader.js",
    "Web\vendor\controls\OrbitControls.js",
    "Web\vendor\controls\TransformControls.js",
    "Web\vendor\curves\NURBSCurve.js",
    "Web\vendor\curves\NURBSUtils.js",
    "Web\vendor\libs\fflate.module.js",
    "Web\vendor\loaders\collada\ColladaParser.js",
    "Web\vendor\loaders\collada\ColladaComposer.js",
    "Web\vendor\THREE-LICENSE.txt"
) | ForEach-Object { Get-RequiredFile (Join-Path $forge $_) }

$sources = @(
    "ToolForgeModels.cs",
    "ToolForgeUtilities.cs",
    "FbxDocument.cs",
    "AsciiFbxDocument.cs",
    "BinaryFbxDocument.cs",
    "ColladaHeldToolGenerator.cs",
    "ToolForgeProjectService.cs",
    "ScrapMechanicPreviewAssets.cs",
    "ToolForgeValidator.cs",
    "TreeSaplingToolGenerator.cs",
    "SaplingPackageBuilder.cs",
    "ToolForgeMainForm.cs",
    "ToolForgeSelfTests.cs",
    "ToolForgeProgram.cs",
    "ToolForgeAssemblyInfo.cs"
) | ForEach-Object { Get-RequiredFile (Join-Path $forge $_) }
$sources += Get-RequiredFile (Join-Path $source "Shared\GameInstallLocator.cs")

Reset-GeneratedDirectory $output

& $compiler `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    "/win32manifest:$manifest" `
    "/win32icon:$icon" `
    "/resource:$binaryFbxFixture,ScrapLab.ToolForge.Tests.Blender5Binary.fbx" `
    "/out:$output\ScrapLab.ToolForge.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Core.dll `
    /reference:System.Web.Extensions.dll `
    "/reference:$webViewCore" `
    "/reference:$webViewForms" `
    $sources

if ($LASTEXITCODE -ne 0) { throw "ScrapLab Tool Forge did not compile." }

Copy-Item -LiteralPath $webViewCore -Destination $output -Force
Copy-Item -LiteralPath $webViewForms -Destination $output -Force
Copy-Item -LiteralPath $webViewLoader -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $webView "WEBVIEW2-LICENSE.txt") -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $webView "WEBVIEW2-NOTICE.txt") -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $forge "Web") -Destination $output -Recurse -Force
Copy-Item -LiteralPath (Join-Path $forge "README.md") -Destination $output -Force

if (-not $SkipTests) {
    $testProcess = Start-Process `
        -FilePath (Join-Path $output "ScrapLab.ToolForge.exe") `
        -ArgumentList "selftest" `
        -NoNewWindow `
        -Wait `
        -PassThru
    if ($testProcess.ExitCode -ne 0) {
        throw "ScrapLab Tool Forge self-tests failed."
    }
}

$bundle = Join-Path $releaseRoot "ScrapLab-Tool-Forge-$version"
Reset-GeneratedDirectory $bundle
Copy-Item -Path (Join-Path $output "*") -Destination $bundle -Recurse -Force
$archive = Join-Path $releaseRoot "ScrapLab-Tool-Forge-$version.zip"
Compress-Archive -Path (Join-Path $bundle "*") -DestinationPath $archive -CompressionLevel Optimal -Force

$exe = Get-Item -LiteralPath (Join-Path $output "ScrapLab.ToolForge.exe")
$zip = Get-Item -LiteralPath $archive
Write-Host "Built $($exe.FullName) ($($exe.Length) bytes)"
Write-Host "Packaged $($zip.FullName) ($($zip.Length) bytes)"
