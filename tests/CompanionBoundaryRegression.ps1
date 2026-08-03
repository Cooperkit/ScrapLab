$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$mainPath = Join-Path $root 'dist\ScrapLab.exe'
$patchPath = Join-Path $root 'dist\ScrapLab.PatchHelper.exe'
$updaterPath = Join-Path $root 'dist\ScrapLab.Updater.exe'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
}

foreach ($path in @($mainPath, $patchPath, $updaterPath)) {
    Assert-True (Test-Path -LiteralPath $path) "Missing bundle file: $path"
}

$main = [Reflection.Assembly]::LoadFrom($mainPath)
$patch = [Reflection.Assembly]::LoadFrom($patchPath)
$updater = [Reflection.Assembly]::LoadFrom($updaterPath)

Assert-True ($null -eq $main.GetType('RaidRescue.GamePatchService', $false)) `
    'GamePatchService leaked into the main UI executable.'
Assert-True ($null -eq $main.GetType('RaidRescue.ElevatedPatchBroker', $false)) `
    'The legacy elevated broker leaked into the main UI executable.'
Assert-True ($null -eq $main.GetType('RaidRescue.UpdaterProgram', $false)) `
    'The updater implementation leaked into the main UI executable.'
Assert-True ($null -ne $patch.GetType('RaidRescue.GamePatchService', $false)) `
    'The dedicated patch helper is missing GamePatchService.'
Assert-True ($null -eq $patch.GetType('RaidRescue.ElevatedPatchBroker', $false)) `
    'The dedicated helper still contains the legacy self-elevation broker.'
Assert-True ($null -ne $updater.GetType('RaidRescue.UpdaterProgram', $false)) `
    'The fixed updater is missing its restricted entry point.'

$security = $main.GetType('RaidRescue.CompanionSecurity', $true)
$signatureCheck = $security.GetMethod(
    'HasValidAuthenticodeSignature',
    [Reflection.BindingFlags]'Static,NonPublic')
Assert-True ($null -ne $signatureCheck) `
    'Authenticode validation is missing from companion trust checks.'
[object[]]$signatureArguments = @([string]$mainPath)
$unsignedAccepted = [bool]$signatureCheck.Invoke($null, $signatureArguments)
Assert-True (-not $unsignedAccepted) `
    'The unsigned development build was incorrectly reported as Authenticode-valid.'

$status = & $patchPath --status resource-locator
Assert-True ($LASTEXITCODE -eq 0) 'A valid read-only helper status failed.'
$parsed = $status | ConvertFrom-Json
Assert-True ($null -ne $parsed.Success) 'The helper did not return its typed result.'

$freezerBeehiveStatus = & $patchPath --status better-freezer-beehive
Assert-True ($LASTEXITCODE -eq 0) `
    'The Better Freezer & Beehive helper status action failed.'
$freezerBeehiveParsed = $freezerBeehiveStatus | ConvertFrom-Json
Assert-True ($null -ne $freezerBeehiveParsed.Success) `
    'The Better Freezer & Beehive helper did not return its typed result.'

$raidDetectorStatus = & $patchPath --status raid-detector
Assert-True ($LASTEXITCODE -eq 0) `
    'The Raid Detector helper status action failed.'
$raidDetectorParsed = $raidDetectorStatus | ConvertFrom-Json
Assert-True ($null -ne $raidDetectorParsed.Success) `
    'The Raid Detector helper did not return its typed result.'

& $patchPath --status definitely-not-an-action 2>$null
Assert-True ($LASTEXITCODE -eq 2) 'The helper accepted an unknown status action.'

& $updaterPath --not-a-real-command 2>$null
Assert-True ($LASTEXITCODE -eq 2) 'The updater accepted an unknown command.'

Write-Host 'Companion boundary regression tests passed.'
