$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exePath = Join-Path $root "dist\RaidRescue.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Build dist\RaidRescue.exe before running update regression tests."
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
Assert-True ($currentVersion -eq "1.13.0") `
    "The update service did not report version 1.13.0."

$officialAsset =
    "https://github.com/Cooperkit/Raid-Rescue/releases/download/v1.13.1/RaidRescue.exe"
$lookalikeAsset =
    "https://github.com.evil.example/Cooperkit/Raid-Rescue/releases/download/v1.13.1/RaidRescue.exe"
$wrongRepository =
    "https://github.com/SomeoneElse/Raid-Rescue/releases/download/v1.13.1/RaidRescue.exe"

Assert-True ([bool](Invoke-UpdateMethod "IsOfficialDownloadUrl" @($officialAsset))) `
    "The official release asset URL was rejected."
Assert-True (-not [bool](Invoke-UpdateMethod "IsOfficialDownloadUrl" @($lookalikeAsset))) `
    "A look-alike GitHub host was accepted."
Assert-True (-not [bool](Invoke-UpdateMethod "IsOfficialDownloadUrl" @($wrongRepository))) `
    "A different GitHub repository was accepted."

$digest = (Get-FileHash -Algorithm SHA256 -LiteralPath $exePath).Hash
Assert-True ([bool](Invoke-UpdateMethod "IsSha256" @($digest))) `
    "The built executable's SHA-256 digest was rejected."
Assert-True (-not [bool](Invoke-UpdateMethod "IsSha256" @("ABC123"))) `
    "A malformed SHA-256 digest was accepted."

$expected = [Version]"1.13.0"
[void](Invoke-UpdateMethod "VerifyDownloadedExecutable" @(
    $exePath, $digest, $expected))

$tamperBlocked = $false
try {
    [void](Invoke-UpdateMethod "VerifyDownloadedExecutable" @(
        $exePath, ("0" * 64), $expected))
}
catch {
    $tamperBlocked = $true
}
Assert-True $tamperBlocked `
    "A mismatched GitHub digest was not rejected."

$sameVersion = Invoke-UpdateMethod "PrepareAndLaunchUpdate" @(
    $officialAsset, $digest, "1.13.0")
Assert-True (-not [bool]$sameVersion.Success) `
    "The updater tried to install the current version."
Assert-True (-not [bool]$sameVersion.ReadyToRestart) `
    "A rejected same-version update requested a restart."

$replaceFixture = Join-Path ([IO.Path]::GetTempPath()) (
    "RaidRescue-Update-Test-" + [Guid]::NewGuid().ToString("N"))
try {
    [IO.Directory]::CreateDirectory($replaceFixture) | Out-Null
    $target = Join-Path $replaceFixture "RaidRescue.exe"
    $stage = Join-Path $replaceFixture ".RaidRescue.Update.test.tmp"
    Copy-Item -LiteralPath $exePath -Destination $target
    [IO.File]::AppendAllText($target, "OLD")
    Copy-Item -LiteralPath $exePath -Destination $stage
    [void](Invoke-UpdateMethod "ReplaceExecutable" @($stage, $target))
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

Write-Host "App update regression tests passed."
