param(
    [ValidateSet('Status', 'Install', 'Remove')]
    [string]$Action = 'Status',
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$ReceiptRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development State'),
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development Backups\WirelessVacuumPipePhase1'),
    [switch]$AllowRunningGame
)

$ErrorActionPreference = 'Stop'
$probeRelative = 'Survival\Scripts\ScrapLab\Experiments\WirelessVacuumPipePhase1\ScrapLabPipePhase1Probe.lua'
$gameRelative = 'Survival\Scripts\game\SurvivalGame.lua'
$cacheRelative = 'Cache\Bundle\core_data.cbo'
$receiptPath = Join-Path $ReceiptRoot 'WirelessVacuumPipePhase1.json'
$sourceProbe = Join-Path $PSScriptRoot '..\..\source\Patching\Parts\WirelessVacuumPipe\ScrapLabPipePhase1Probe.lua'
$markerStart = '-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 1 PROBE'
$markerEnd = '-- END SCRAPLAB WIRELESS VACUUM PIPE PHASE 1 PROBE'
$dofileLine = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Experiments/WirelessVacuumPipePhase1/ScrapLabPipePhase1Probe.lua" )'

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-BytesSha256([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-', '')
    }
    finally { $algorithm.Dispose() }
}

function Get-Utf8Document([string]$Path) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    $text = $encoding.GetString($bytes, $offset, $bytes.Length - $offset)
    $hasCrLf = $text.Contains("`r`n")
    $withoutCrLf = $text.Replace("`r`n", '')
    $hasBareLf = $withoutCrLf.Contains("`n")
    if ($hasCrLf -and $hasBareLf) { throw "Mixed newlines are unsupported: $Path" }
    [pscustomobject]@{
        Text = $text
        HasBom = $hasBom
        Newline = if ($hasCrLf) { "`r`n" } else { "`n" }
    }
}

function ConvertTo-Utf8Bytes([string]$Text, [bool]$HasBom) {
    [byte[]]$payload = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    if (-not $HasBom) { return $payload }
    [byte[]]$result = New-Object byte[] ($payload.Length + 3)
    $result[0] = 0xEF; $result[1] = 0xBB; $result[2] = 0xBF
    [Array]::Copy($payload, 0, $result, 3, $payload.Length)
    return $result
}

function Write-AtomicBytes([string]$Path, [byte[]]$Bytes) {
    $directory = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporary = Join-Path $directory ('.scraplab-phase1-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllBytes($temporary, $Bytes)
        if (Test-Path -LiteralPath $Path) {
            $replaceBackup = Join-Path $directory ('.scraplab-phase1-replaced-' + [Guid]::NewGuid().ToString('N') + '.tmp')
            try { [IO.File]::Replace($temporary, $Path, $replaceBackup) }
            finally {
                if (Test-Path -LiteralPath $replaceBackup) { Remove-Item -LiteralPath $replaceBackup -Force }
            }
        }
        else {
            [IO.File]::Move($temporary, $Path)
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Write-AtomicJson([string]$Path, [object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 8
    Write-AtomicBytes $Path ([Text.UTF8Encoding]::new($false).GetBytes($json))
}

function Get-MarkerBlock([string]$Newline) {
    return $markerStart + $Newline + $dofileLine + $Newline + $markerEnd
}

function Get-OccurrenceCount([string]$Text, [string]$Needle) {
    if ([String]::IsNullOrEmpty($Needle)) { return 0 }
    $count = 0
    $offset = 0
    while (($offset = $Text.IndexOf($Needle, $offset, [StringComparison]::Ordinal)) -ge 0) {
        $count++
        $offset += $Needle.Length
    }
    return $count
}

function Assert-GameClosed {
    if ($AllowRunningGame) { return }
    if (Get-Process -Name 'ScrapMechanic' -ErrorAction SilentlyContinue) {
        throw 'Scrap Mechanic is running. Close it before installing or removing the Phase 1 probe.'
    }
}

function Get-ProbeStatus {
    $gamePath = Join-Path $GameRoot $gameRelative
    $probePath = Join-Path $GameRoot $probeRelative
    if (-not (Test-Path -LiteralPath $gamePath)) {
        return [pscustomobject]@{ State = 'GAME_NOT_FOUND'; Installed = $false; GameRoot = $GameRoot }
    }
    $document = Get-Utf8Document $gamePath
    $startCount = Get-OccurrenceCount $document.Text $markerStart
    $endCount = Get-OccurrenceCount $document.Text $markerEnd
    $scriptExists = Test-Path -LiteralPath $probePath
    if ($startCount -eq 0 -and $endCount -eq 0 -and -not $scriptExists) {
        $state = 'NOT_INSTALLED'
    }
    elseif ($startCount -eq 1 -and $endCount -eq 1 -and $scriptExists) {
        $state = if ((Get-Sha256 $probePath) -eq (Get-Sha256 $sourceProbe)) { 'INSTALLED' } else { 'SCRIPT_CHANGED' }
    }
    else { $state = 'PARTIAL_OR_CONFLICT' }
    [pscustomobject]@{
        State = $state
        Installed = $state -eq 'INSTALLED'
        GameRoot = $GameRoot
        SurvivalGame = $gamePath
        ProbeScript = $probePath
        Receipt = $receiptPath
    }
}

function Install-Probe {
    Assert-GameClosed
    if (-not (Test-Path -LiteralPath $sourceProbe)) { throw "Probe source is missing: $sourceProbe" }
    $status = Get-ProbeStatus
    if ($status.State -eq 'INSTALLED') { return $status }
    if ($status.State -ne 'NOT_INSTALLED') { throw "Install blocked by state $($status.State)." }

    $gamePath = Join-Path $GameRoot $gameRelative
    $probePath = Join-Path $GameRoot $probeRelative
    $document = Get-Utf8Document $gamePath
    $block = Get-MarkerBlock $document.Newline
    $patchedText = $document.Text
    if (-not $patchedText.EndsWith($document.Newline, [StringComparison]::Ordinal)) { $patchedText += $document.Newline }
    $patchedText += $block + $document.Newline
    [byte[]]$patchedBytes = ConvertTo-Utf8Bytes $patchedText $document.HasBom
    [byte[]]$originalBytes = [IO.File]::ReadAllBytes($gamePath)

    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
    $backupDirectory = Join-Path $BackupRoot $timestamp
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    $backupPath = Join-Path $backupDirectory 'SurvivalGame.lua'
    [IO.File]::WriteAllBytes($backupPath, $originalBytes)
    if ((Get-Sha256 $backupPath) -ne (Get-Sha256 $gamePath)) { throw 'Backup verification failed.' }

    $wroteGame = $false
    $wroteProbe = $false
    try {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $probePath)) | Out-Null
        [IO.File]::Copy($sourceProbe, $probePath, $false)
        $wroteProbe = $true
        if ((Get-Sha256 $probePath) -ne (Get-Sha256 $sourceProbe)) { throw 'Probe script verification failed.' }
        Write-AtomicBytes $gamePath $patchedBytes
        $wroteGame = $true
        $expectedHash = Get-BytesSha256 $patchedBytes
        if ((Get-Sha256 $gamePath) -ne $expectedHash) { throw 'SurvivalGame.lua verification failed.' }

        [IO.Directory]::CreateDirectory($ReceiptRoot) | Out-Null
        Write-AtomicJson $receiptPath ([ordered]@{
            SchemaVersion = 1
            InstalledUtc = [DateTime]::UtcNow.ToString('o')
            GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
            SurvivalGamePath = $gamePath
            OriginalSurvivalGameHash = (Get-Sha256 $backupPath)
            InstalledSurvivalGameHash = (Get-Sha256 $gamePath)
            ProbePath = $probePath
            ProbeHash = (Get-Sha256 $probePath)
            BackupPath = $backupPath
        })
        $cachePath = Join-Path $GameRoot $cacheRelative
        if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    }
    catch {
        if ($wroteGame) { Write-AtomicBytes $gamePath $originalBytes }
        if ($wroteProbe -and (Test-Path -LiteralPath $probePath)) { Remove-Item -LiteralPath $probePath -Force }
        throw
    }
    return Get-ProbeStatus
}

function Remove-Probe {
    Assert-GameClosed
    $status = Get-ProbeStatus
    if ($status.State -eq 'NOT_INSTALLED') { return $status }
    if ($status.State -ne 'INSTALLED') { throw "Removal blocked by state $($status.State)." }
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'Removal receipt is missing.' }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $gamePath = Join-Path $GameRoot $gameRelative
    $probePath = Join-Path $GameRoot $probeRelative
    if ((Get-Sha256 $probePath) -ne $receipt.ProbeHash) { throw 'Probe script changed after installation; removal blocked.' }

    [byte[]]$installedBytes = [IO.File]::ReadAllBytes($gamePath)
    if ((Get-Sha256 $gamePath) -eq $receipt.InstalledSurvivalGameHash) {
        if (-not (Test-Path -LiteralPath $receipt.BackupPath)) { throw 'Exact uninstall backup is missing.' }
        if ((Get-Sha256 $receipt.BackupPath) -ne $receipt.OriginalSurvivalGameHash) { throw 'Exact uninstall backup failed checksum verification.' }
        [byte[]]$updatedBytes = [IO.File]::ReadAllBytes($receipt.BackupPath)
    }
    else {
        $document = Get-Utf8Document $gamePath
        $block = Get-MarkerBlock $document.Newline
        if ((Get-OccurrenceCount $document.Text $block) -ne 1) { throw 'The exact Phase 1 marker block is not intact.' }
        $updatedText = $document.Text.Replace($block + $document.Newline, '')
        if ($updatedText -eq $document.Text) { $updatedText = $document.Text.Replace($block, '') }
        [byte[]]$updatedBytes = ConvertTo-Utf8Bytes $updatedText $document.HasBom
    }

    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
    $backupDirectory = Join-Path $BackupRoot ($timestamp + '-remove')
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $backupDirectory 'SurvivalGame.installed.lua'), $installedBytes)
    try {
        Write-AtomicBytes $gamePath $updatedBytes
        Remove-Item -LiteralPath $probePath -Force
        if ((Get-OccurrenceCount (Get-Utf8Document $gamePath).Text $markerStart) -ne 0) { throw 'Marker removal verification failed.' }
        Remove-Item -LiteralPath $receiptPath -Force
        $cachePath = Join-Path $GameRoot $cacheRelative
        if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    }
    catch {
        Write-AtomicBytes $gamePath $installedBytes
        if (-not (Test-Path -LiteralPath $probePath)) { [IO.File]::Copy($sourceProbe, $probePath, $false) }
        throw
    }
    return Get-ProbeStatus
}

$result = switch ($Action) {
    'Install' { Install-Probe }
    'Remove' { Remove-Probe }
    default { Get-ProbeStatus }
}
$result | ConvertTo-Json -Depth 5
