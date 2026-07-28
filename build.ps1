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

$sources = @(
    "Program.cs",
    "Models.cs",
    "LuaStorage.cs",
    "SqliteNative.cs",
    "RaidService.cs",
    "UiHtml.cs",
    "AssemblyInfo.cs"
) | ForEach-Object { Join-Path $source $_ }

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    "/win32manifest:$source\app.manifest" `
    "/out:$output\RaidRescue.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Core.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Raid Rescue did not compile."
}

$file = Get-Item -LiteralPath (Join-Path $output "RaidRescue.exe")
Write-Host "Built $($file.FullName) ($($file.Length) bytes)"

