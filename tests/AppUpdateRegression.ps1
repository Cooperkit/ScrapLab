$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exePath = Join-Path $root "dist\RaidRescue.exe"
$patchPath = Join-Path $root "dist\RaidRescue.PatchHelper.exe"
$updaterPath = Join-Path $root "dist\RaidRescue.Updater.exe"
if (-not (Test-Path -LiteralPath $exePath) -or
    -not (Test-Path -LiteralPath $patchPath) -or
    -not (Test-Path -LiteralPath $updaterPath)) {
    throw "Build the complete three-program bundle before running update regression tests."
}

$assembly = [Reflection.Assembly]::LoadFrom($exePath)
$service = $assembly.GetType("RaidRescue.AppUpdateService", $true)
$flags = [Reflection.BindingFlags]::Static -bor
    [Reflection.BindingFlags]::Public -bor
    [Reflection.BindingFlags]::NonPublic

function Invoke-UpdateMethod {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [object[]]$Arguments = @()
    )

    $method = $service.GetMethod($Name, $flags)
    if ($null -eq $method) {
        throw "Update method '$Name' was not found."
    }
    $invokeArguments = New-Object object[] $Arguments.Count
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        $invokeArguments[$index] = $Arguments[$index].PSObject.BaseObject
    }
    return $method.Invoke($null, $invokeArguments)
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

$currentVersion = [string]$service.GetProperty(
    "CurrentVersion", $flags).GetValue($null, $null)
$assemblyVersion = $assembly.GetName().Version
$expectedCurrentVersion = $assemblyVersion.ToString(3)
Assert-True ($currentVersion -eq $expectedCurrentVersion) `
    "The update service version did not match the executable assembly version."

$officialAsset =
    "https://github.com/Cooperkit/Raid-Rescue/releases/download/v1.16.1/RaidRescue.exe"
$officialPatchAsset =
    "https://github.com/Cooperkit/Raid-Rescue/releases/download/v1.16.1/RaidRescue.PatchHelper.exe"
$lookalikeAsset =
    "https://github.com.evil.example/Cooperkit/Raid-Rescue/releases/download/v1.14.1/RaidRescue.exe"
$wrongRepository =
    "https://github.com/SomeoneElse/Raid-Rescue/releases/download/v1.14.1/RaidRescue.exe"

Assert-True ([bool](Invoke-UpdateMethod "IsOfficialDownloadUrl" @($officialAsset))) `
    "The official release asset URL was rejected."
Assert-True (-not [bool](Invoke-UpdateMethod "IsOfficialDownloadUrl" @($lookalikeAsset))) `
    "A look-alike GitHub host was accepted."
Assert-True (-not [bool](Invoke-UpdateMethod "IsOfficialDownloadUrl" @($wrongRepository))) `
    "A different GitHub repository was accepted."

$digest = (Get-FileHash -Algorithm SHA256 -LiteralPath $exePath).Hash
$patchDigest = (Get-FileHash -Algorithm SHA256 -LiteralPath $patchPath).Hash
Assert-True ([bool](Invoke-UpdateMethod "IsSha256" @($digest))) `
    "The built executable's SHA-256 digest was rejected."
Assert-True (-not [bool](Invoke-UpdateMethod "IsSha256" @("ABC123"))) `
    "A malformed SHA-256 digest was accepted."

$expected = [Version]$expectedCurrentVersion
[void](Invoke-UpdateMethod "VerifyDownloadedExecutable" @(
    $exePath, $digest, $expected, "Raid Rescue for Scrap Mechanic"))
[void](Invoke-UpdateMethod "VerifyDownloadedExecutable" @(
    $patchPath, $patchDigest, $expected,
    "Raid Rescue Patch Helper for Scrap Mechanic"))

$tamperBlocked = $false
try {
    [void](Invoke-UpdateMethod "VerifyDownloadedExecutable" @(
        $exePath, ("0" * 64), $expected,
        "Raid Rescue for Scrap Mechanic"))
}
catch {
    $tamperBlocked = $true
}
Assert-True $tamperBlocked `
    "A mismatched GitHub digest was not rejected."

$sameVersion = Invoke-UpdateMethod "PrepareAndLaunchUpdate" @(
    $officialAsset, $digest,
    $officialPatchAsset, $patchDigest,
    $expectedCurrentVersion)
Assert-True (-not [bool]$sameVersion.Success) `
    "The updater tried to install the current version."
Assert-True (-not [bool]$sameVersion.ReadyToRestart) `
    "A rejected same-version update requested a restart."

$updaterAssembly = [Reflection.Assembly]::LoadFrom($updaterPath)
$updaterType = $updaterAssembly.GetType("RaidRescue.UpdaterProgram", $true)
$replaceMethod = $updaterType.GetMethod("Replace", $flags)
Assert-True ($null -ne $replaceMethod) `
    "The fixed updater replacement method was not found."

$replaceFixture = Join-Path ([IO.Path]::GetTempPath()) (
    "RaidRescue-Update-Test-" + [Guid]::NewGuid().ToString("N"))
try {
    [IO.Directory]::CreateDirectory($replaceFixture) | Out-Null
    $target = Join-Path $replaceFixture "RaidRescue.exe"
    $stage = Join-Path $replaceFixture ".RaidRescue.Update.test.tmp"
    Copy-Item -LiteralPath $exePath -Destination $target
    [IO.File]::AppendAllText($target, "OLD")
    Copy-Item -LiteralPath $exePath -Destination $stage
    [object[]]$replaceArguments = @(
        ([string]$stage),
        ([string]$target)
    )
    [void]$replaceMethod.Invoke($null, $replaceArguments)
    Assert-True ((Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash -eq $digest) `
        "The replacement helper did not install the staged executable."
    Assert-True (-not (Test-Path -LiteralPath $stage)) `
        "The replacement helper left the staged executable behind."
}
finally {
    if (Test-Path -LiteralPath $replaceFixture) {
        Remove-Item -LiteralPath $replaceFixture -Recurse -Force
    }
}

$legacyUpdateHelper = $service.GetMethod("TryRunHelper", $flags)
Assert-True ($null -eq $legacyUpdateHelper) `
    "The main executable still exposes the legacy self-update helper."
Assert-True ($null -eq $assembly.GetType("RaidRescue.GamePatchService", $false)) `
    "The main executable still contains the privileged game patch implementation."
Assert-True ($null -eq $assembly.GetType("RaidRescue.ElevatedPatchBroker", $false)) `
    "The main executable still contains the legacy self-elevation broker."

$patchInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($patchPath)
$updaterInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($updaterPath)
Assert-True ($patchInfo.ProductName -eq
    "Raid Rescue Patch Helper for Scrap Mechanic") `
    "The patch helper has the wrong product identity."
Assert-True ($updaterInfo.ProductName -eq
    "Raid Rescue Updater for Scrap Mechanic") `
    "The updater has the wrong product identity."

Write-Host "App update regression tests passed."
