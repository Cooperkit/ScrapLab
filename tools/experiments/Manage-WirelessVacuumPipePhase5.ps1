param(
    [ValidateSet('Status', 'Install', 'Remove')]
    [string]$Action = 'Status',
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Game Backups\Scrap Mechanic\Secret Mods'),
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\..\dist\ScrapLab.PatchHelper.exe')
)

$ErrorActionPreference = 'Stop'
$binding = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'

function Invoke-Static([Type]$Type,[string]$Name,[object[]]$Arguments) {
    $methods = @($Type.GetMethods($binding) | Where-Object {
        $_.Name -eq $Name -and $_.GetParameters().Count -eq $Arguments.Count
    })
    if ($methods.Count -ne 1) {
        throw "Expected one $($Type.FullName).$Name overload."
    }
    try {
        return $methods[0].Invoke($null, $Arguments)
    }
    catch [Reflection.TargetInvocationException] {
        throw $_.Exception.InnerException
    }
}

if ($Action -ne 'Status' -and
    (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue)) {
    throw 'Scrap Mechanic is running. Close it before changing Phase 5.'
}

$assembly = [Reflection.Assembly]::LoadFrom(
    (Resolve-Path -LiteralPath $PatchHelperExe).Path)
$service = $assembly.GetType(
    'RaidRescue.WirelessVacuumPipePatchService', $true)

if ($Action -eq 'Status') {
    $result = Invoke-Static $service 'GetStatusAt' @([string]$GameRoot)
}
else {
    $enabled = $Action -eq 'Install'
    $result = Invoke-Static $service 'SetEnabledAt' @(
        [string]$GameRoot, [string]$BackupRoot, [bool]$enabled)
    if ($result.Success -and $result.FilesPatched -gt 0) {
        $cache = Join-Path $GameRoot 'Cache\Bundle\core_data.cbo'
        if (Test-Path -LiteralPath $cache) {
            Remove-Item -LiteralPath $cache -Force
        }
    }
}

$result | ConvertTo-Json -Depth 10
if (-not $result.Success) { exit 1 }
