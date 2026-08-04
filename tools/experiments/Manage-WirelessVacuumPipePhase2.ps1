param(
    [ValidateSet('Status', 'Install', 'Update', 'Remove')]
    [string]$Action = 'Status',
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$ReceiptRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development State'),
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development Backups\WirelessVacuumPipePhase2'),
    [switch]$AllowRunningGame
)

$ErrorActionPreference = 'Stop'
$partUuid = 'a34d9af0-4ba0-431d-b647-2d5435ecf138'
$managerUuid = '8a6e31c4-575f-40fa-96f3-85bd23eb34ce'
$shapeSetRegistration = '$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/WirelessVacuumPipe.shapeset'
$managerScriptRegistration = '$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/WirelessPipeManager.lua'
$receiptPath = Join-Path $ReceiptRoot 'WirelessVacuumPipePhase2.json'
$cacheRelative = 'Cache\Bundle\core_data.cbo'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$ownedFiles = @(
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'; Relative = 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua' },
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua'; Relative = 'Survival\Scripts\ScrapLab\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua' },
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.shapeset'; Relative = 'Survival\Objects\Database\ShapeSets\ScrapLab\Parts\WirelessVacuumPipe.shapeset' },
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.layout'; Relative = 'Survival\Gui\Layouts\ScrapLab\Parts\WirelessVacuumPipe.layout' },
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipePhase2Harness.lua'; Relative = 'Survival\Scripts\ScrapLab\Experiments\WirelessVacuumPipePhase2\WirelessVacuumPipePhase2Harness.lua' }
)

$modifiedFiles = @(
    [pscustomobject]@{ Kind = 'Items'; Relative = 'Survival\Scripts\game\survival_items.lua' },
    [pscustomobject]@{ Kind = 'ShapeSets'; Relative = 'Survival\Objects\Database\shapesets.json' },
    [pscustomobject]@{ Kind = 'Managers'; Relative = 'Survival\ScriptableObjects\scriptableObjectSets\sob_managers.sobset' },
    [pscustomobject]@{ Kind = 'Game'; Relative = 'Survival\Scripts\game\SurvivalGame.lua' }
)

$gameMarkerStart = '-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 2 HARNESS'
$gameMarkerEnd = '-- END SCRAPLAB WIRELESS VACUUM PIPE PHASE 2 HARNESS'
$gameDofile = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Experiments/WirelessVacuumPipePhase2/WirelessVacuumPipePhase2Harness.lua" )'
$itemMarkerStart = "`t-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 2 ITEM"
$itemMarkerEnd = "`t-- END SCRAPLAB WIRELESS VACUUM PIPE PHASE 2 ITEM"

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-BytesSha256([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-', '') }
    finally { $algorithm.Dispose() }
}

function Get-Utf8Document([string]$Path) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    $text = $encoding.GetString($bytes, $offset, $bytes.Length - $offset)
    $hasCrLf = $text.Contains("`r`n")
    $hasBareLf = $text.Replace("`r`n", '').Contains("`n")
    if ($hasCrLf -and $hasBareLf) { throw "Mixed newlines are unsupported: $Path" }
    return [pscustomobject]@{ Text = $text; HasBom = $hasBom; Newline = if ($hasCrLf) { "`r`n" } else { "`n" } }
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
    $temporary = Join-Path $directory ('.scraplab-phase2-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllBytes($temporary, $Bytes)
        if (Test-Path -LiteralPath $Path) {
            $replaceBackup = Join-Path $directory ('.scraplab-phase2-replaced-' + [Guid]::NewGuid().ToString('N') + '.tmp')
            try { [IO.File]::Replace($temporary, $Path, $replaceBackup) }
            finally { if (Test-Path -LiteralPath $replaceBackup) { Remove-Item -LiteralPath $replaceBackup -Force } }
        }
        else { [IO.File]::Move($temporary, $Path) }
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}

function Write-AtomicJson([string]$Path, [object]$Value) {
    Write-AtomicBytes $Path ([Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 12)))
}

function Get-OccurrenceCount([string]$Text, [string]$Needle) {
    if ([String]::IsNullOrEmpty($Needle)) { return 0 }
    $count = 0; $offset = 0
    while (($offset = $Text.IndexOf($Needle, $offset, [StringComparison]::Ordinal)) -ge 0) {
        $count++; $offset += $Needle.Length
    }
    return $count
}

function Get-ItemBlock([string]$Newline) {
    return $itemMarkerStart + $Newline +
        "`tobj_pneumatic_pipe_wireless = sm.uuid.new( `"$partUuid`" )," + $Newline +
        $itemMarkerEnd
}

function Get-GameBlock([string]$Newline) {
    return $gameMarkerStart + $Newline + $gameDofile + $Newline + $gameMarkerEnd
}

function Get-ManagerObject([string]$Newline) {
    return "`t`t{" + $Newline +
        "`t`t`t`"filename`" : `"$managerScriptRegistration`"," + $Newline +
        "`t`t`t`"classname`" : `"WirelessPipeManager`"," + $Newline +
        "`t`t`t`"name`" : `"scraplab_wireless_pipe_manager`"," + $Newline +
        "`t`t`t`"uuid`" : `"$managerUuid`"," + $Newline +
        "`t`t`t`"singleton`" : true" + $Newline +
        "`t`t}"
}

function Get-LegacyManagerObject([string]$Newline) {
    return "`t`t{" + $Newline +
        "`t`t`t`"filename`" : `"$managerScriptRegistration`"," + $Newline +
        "`t`t`t`"classname`" : `"WirelessPipeManager`"," + $Newline +
        "`t`t`t`"name`" : `"scraplab_wireless_pipe_manager`"," + $Newline +
        "`t`t`t`"uuid`" : `"$managerUuid`"" + $Newline +
        "`t`t}"
}

function Add-BeforeFinalArrayClose([string]$Text, [string]$Newline, [string]$Entry) {
    $suffix = $Newline + "`t]" + $Newline + '}'
    $index = $Text.LastIndexOf($suffix, [StringComparison]::Ordinal)
    if ($index -lt 0 -or -not [String]::IsNullOrWhiteSpace($Text.Substring($index + $suffix.Length))) { throw 'Registration JSON has an unexpected ending.' }
    return $Text.Insert($index, ',' + $Newline + $Entry)
}

function Remove-BeforeFinalArrayCloseEntry([string]$Text, [string]$Newline, [string]$Entry) {
    $needle = ',' + $Newline + $Entry
    if ((Get-OccurrenceCount $Text $needle) -ne 1) { throw 'The exact Phase 2 registration is not intact.' }
    return $Text.Replace($needle, '')
}

function Assert-GameClosed {
    if ($AllowRunningGame) { return }
    if (Get-Process -Name 'ScrapMechanic' -ErrorAction SilentlyContinue) {
        throw 'Scrap Mechanic is running. Close it before installing or removing Phase 2.'
    }
}

function Get-Phase2Status {
    $signals = 0; $expected = 4 + $ownedFiles.Count
    $details = [ordered]@{}
    foreach ($file in $modifiedFiles) {
        $path = Join-Path $GameRoot $file.Relative
        if (-not (Test-Path -LiteralPath $path)) {
            $details[$file.Kind] = 'MISSING_TARGET'
            continue
        }
        $text = (Get-Utf8Document $path).Text
        $present = switch ($file.Kind) {
            'Items' { (Get-OccurrenceCount $text $itemMarkerStart) -eq 1 -and (Get-OccurrenceCount $text $partUuid) -eq 1 }
            'ShapeSets' { (Get-OccurrenceCount $text $shapeSetRegistration) -eq 1 }
            'Managers' { (Get-OccurrenceCount $text $managerUuid) -eq 1 -and (Get-OccurrenceCount $text $managerScriptRegistration) -eq 1 }
            'Game' { (Get-OccurrenceCount $text $gameMarkerStart) -eq 1 -and (Get-OccurrenceCount $text $gameDofile) -eq 1 }
        }
        $details[$file.Kind] = if ($present) { 'PRESENT' } else { 'ABSENT' }
        if ($present) { $signals++ }
    }
    foreach ($owned in $ownedFiles) {
        $path = Join-Path $GameRoot $owned.Relative
        $name = 'Owned:' + [IO.Path]::GetFileName($owned.Relative)
        if (Test-Path -LiteralPath $path) {
            $same = (Test-Path -LiteralPath $owned.Source) -and (Get-Sha256 $path) -eq (Get-Sha256 $owned.Source)
            $details[$name] = if ($same) { 'PRESENT' } else { 'CHANGED' }
            if ($same) { $signals++ }
        }
        else { $details[$name] = 'ABSENT' }
    }
    $state = if ($signals -eq 0) { 'NOT_INSTALLED' } elseif ($signals -eq $expected) { 'INSTALLED' } else { 'PARTIAL_OR_CONFLICT' }
    return [pscustomobject]@{ State = $state; Installed = $state -eq 'INSTALLED'; GameRoot = $GameRoot; Receipt = $receiptPath; Details = $details }
}

function New-InstallPlans {
    $plans = @()
    foreach ($file in $modifiedFiles) {
        $path = Join-Path $GameRoot $file.Relative
        if (-not (Test-Path -LiteralPath $path)) { throw "Required target is missing: $path" }
        $document = Get-Utf8Document $path
        $text = $document.Text
        $updated = switch ($file.Kind) {
            'Items' {
                if ((Get-OccurrenceCount $text $partUuid) -ne 0 -or (Get-OccurrenceCount $text $itemMarkerStart) -ne 0) { throw 'Wireless item declaration already exists or conflicts.' }
                $closeIndex = $text.LastIndexOf('}', [StringComparison]::Ordinal)
                if ($closeIndex -lt 0 -or -not [String]::IsNullOrWhiteSpace($text.Substring($closeIndex + 1))) { throw 'survival_items.lua has an unexpected ending.' }
                $text.Insert($closeIndex, (Get-ItemBlock $document.Newline) + $document.Newline)
            }
            'ShapeSets' {
                if ((Get-OccurrenceCount $text $shapeSetRegistration) -ne 0) { throw 'Wireless shape-set registration already exists.' }
                Add-BeforeFinalArrayClose $text $document.Newline ("`t`t`"$shapeSetRegistration`"")
            }
            'Managers' {
                if ((Get-OccurrenceCount $text $managerUuid) -ne 0 -or (Get-OccurrenceCount $text $managerScriptRegistration) -ne 0) { throw 'Wireless manager registration already exists or conflicts.' }
                Add-BeforeFinalArrayClose $text $document.Newline (Get-ManagerObject $document.Newline)
            }
            'Game' {
                if ((Get-OccurrenceCount $text $gameMarkerStart) -ne 0 -or (Get-OccurrenceCount $text $gameDofile) -ne 0) { throw 'Phase 2 harness registration already exists or conflicts.' }
                if (-not $text.EndsWith($document.Newline, [StringComparison]::Ordinal)) { $text += $document.Newline }
                $text + (Get-GameBlock $document.Newline) + $document.Newline
            }
        }
        [byte[]]$bytes = ConvertTo-Utf8Bytes $updated $document.HasBom
        $plans += [pscustomobject]@{ Kind = $file.Kind; Relative = $file.Relative; Path = $path; OriginalBytes = [IO.File]::ReadAllBytes($path); OutputBytes = $bytes; OutputHash = Get-BytesSha256 $bytes }
    }
    foreach ($owned in $ownedFiles) {
        if (-not (Test-Path -LiteralPath $owned.Source)) { throw "Owned source is missing: $($owned.Source)" }
        $path = Join-Path $GameRoot $owned.Relative
        if (Test-Path -LiteralPath $path) { throw "Owned target already exists: $path" }
        [byte[]]$bytes = [IO.File]::ReadAllBytes($owned.Source)
        $plans += [pscustomobject]@{ Kind = 'Owned'; Relative = $owned.Relative; Path = $path; OriginalBytes = $null; OutputBytes = $bytes; OutputHash = Get-BytesSha256 $bytes }
    }
    return $plans
}

function Install-Phase2 {
    Assert-GameClosed
    $status = Get-Phase2Status
    if ($status.State -eq 'INSTALLED') { return $status }
    if ($status.State -ne 'NOT_INSTALLED') { throw "Install blocked by state $($status.State)." }
    $plans = New-InstallPlans

    foreach ($plan in $plans | Where-Object Kind -in @('ShapeSets', 'Managers')) {
        $text = [Text.UTF8Encoding]::new($false).GetString($plan.OutputBytes).TrimStart([char]0xFEFF)
        $null = $text | ConvertFrom-Json
    }
    [xml](Get-Content -LiteralPath ($ownedFiles | Where-Object Relative -like '*.layout').Source -Raw) | Out-Null
    $null = Get-Content -LiteralPath ($ownedFiles | Where-Object Relative -like '*.shapeset').Source -Raw | ConvertFrom-Json

    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff')
    $backupDirectory = Join-Path $BackupRoot $timestamp
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    $receiptFiles = @()
    foreach ($plan in $plans) {
        $backupPath = $null
        $originalHash = $null
        if ($plan.OriginalBytes) {
            $backupPath = Join-Path $backupDirectory (($plan.Relative -replace '[\\/:*?"<>|]', '_') + '.bak')
            [IO.File]::WriteAllBytes($backupPath, $plan.OriginalBytes)
            $originalHash = Get-BytesSha256 $plan.OriginalBytes
            if ((Get-Sha256 $backupPath) -ne $originalHash) { throw "Backup verification failed: $($plan.Relative)" }
        }
        $receiptFiles += [ordered]@{ Kind = $plan.Kind; Relative = $plan.Relative; OriginalHash = $originalHash; InstalledHash = $plan.OutputHash; BackupPath = $backupPath }
    }

    $written = @()
    try {
        foreach ($plan in $plans) {
            Write-AtomicBytes $plan.Path $plan.OutputBytes
            $written += $plan
            if ((Get-Sha256 $plan.Path) -ne $plan.OutputHash) { throw "Output verification failed: $($plan.Relative)" }
        }
        [IO.Directory]::CreateDirectory($ReceiptRoot) | Out-Null
        Write-AtomicJson $receiptPath ([ordered]@{
            SchemaVersion = 1; InstalledUtc = [DateTime]::UtcNow.ToString('o'); GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
            PartUuid = $partUuid; ManagerUuid = $managerUuid; BackupDirectory = $backupDirectory; Files = $receiptFiles
        })
        $cachePath = Join-Path $GameRoot $cacheRelative
        if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    }
    catch {
        foreach ($plan in $written) {
            if ($plan.OriginalBytes) { Write-AtomicBytes $plan.Path $plan.OriginalBytes }
            elseif (Test-Path -LiteralPath $plan.Path) { Remove-Item -LiteralPath $plan.Path -Force }
        }
        throw
    }
    return Get-Phase2Status
}

function Update-Phase2OwnedFiles {
    Assert-GameClosed
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'Phase 2 receipt is missing.' }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $updates = @()
    $managerReceiptFile = $receipt.Files | Where-Object { $_.Kind -eq 'Managers' }
    if (-not $managerReceiptFile) { throw 'Manager registration receipt entry is missing.' }
    $managerPath = Join-Path $GameRoot $managerReceiptFile.Relative
    $managerDocument = Get-Utf8Document $managerPath
    $legacyManager = Get-LegacyManagerObject $managerDocument.Newline
    $currentManager = Get-ManagerObject $managerDocument.Newline
    if ((Get-OccurrenceCount $managerDocument.Text $legacyManager) -eq 1) {
        if ((Get-Sha256 $managerPath) -ne $managerReceiptFile.InstalledHash) { throw 'Manager registration changed outside the Phase 2 installer.' }
        [byte[]]$managerCurrentBytes = [IO.File]::ReadAllBytes($managerPath)
        [byte[]]$managerOutputBytes = ConvertTo-Utf8Bytes ($managerDocument.Text.Replace($legacyManager, $currentManager)) $managerDocument.HasBom
        $updates += [pscustomobject]@{ Path = $managerPath; Relative = $managerReceiptFile.Relative; CurrentBytes = $managerCurrentBytes; OutputBytes = $managerOutputBytes; OutputHash = Get-BytesSha256 $managerOutputBytes; ReceiptFile = $managerReceiptFile }
    }
    elseif ((Get-OccurrenceCount $managerDocument.Text $currentManager) -ne 1) { throw 'Manager registration is not intact.' }
    foreach ($owned in $ownedFiles) {
        $file = $receipt.Files | Where-Object { $_.Relative -eq $owned.Relative -and $_.Kind -eq 'Owned' }
        if (-not $file) { throw "Owned receipt entry is missing: $($owned.Relative)" }
        $path = Join-Path $GameRoot $owned.Relative
        if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $file.InstalledHash) {
            throw "Installed owned file no longer matches its receipt: $($owned.Relative)"
        }
        [byte[]]$current = [IO.File]::ReadAllBytes($path)
        [byte[]]$output = [IO.File]::ReadAllBytes($owned.Source)
        $updates += [pscustomobject]@{ Path = $path; Relative = $owned.Relative; CurrentBytes = $current; OutputBytes = $output; OutputHash = Get-BytesSha256 $output; ReceiptFile = $file }
    }
    $written = @()
    try {
        foreach ($update in $updates) {
            Write-AtomicBytes $update.Path $update.OutputBytes
            $written += $update
            if ((Get-Sha256 $update.Path) -ne $update.OutputHash) { throw "Owned update verification failed: $($update.Relative)" }
            $update.ReceiptFile.InstalledHash = $update.OutputHash
        }
        $receipt | Add-Member -NotePropertyName UpdatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
        Write-AtomicJson $receiptPath $receipt
        $cachePath = Join-Path $GameRoot $cacheRelative
        if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    }
    catch {
        foreach ($update in $written) { Write-AtomicBytes $update.Path $update.CurrentBytes }
        throw
    }
    return Get-Phase2Status
}

function Get-SurgicalRemovalBytes([object]$ReceiptFile) {
    $path = Join-Path $GameRoot $ReceiptFile.Relative
    if ($ReceiptFile.Kind -eq 'Owned') {
        if ((Get-Sha256 $path) -ne $ReceiptFile.InstalledHash) { throw "Owned file changed; removal blocked: $($ReceiptFile.Relative)" }
        return $null
    }
    $document = Get-Utf8Document $path
    $updated = switch ($ReceiptFile.Kind) {
        'Items' {
            $block = Get-ItemBlock $document.Newline
            if ((Get-OccurrenceCount $document.Text $block) -ne 1) { throw 'Wireless item block is not intact.' }
            $document.Text.Replace($document.Newline + $block, '')
        }
        'ShapeSets' { Remove-BeforeFinalArrayCloseEntry $document.Text $document.Newline ("`t`t`"$shapeSetRegistration`"") }
        'Managers' { Remove-BeforeFinalArrayCloseEntry $document.Text $document.Newline (Get-ManagerObject $document.Newline) }
        'Game' {
            $block = Get-GameBlock $document.Newline
            if ((Get-OccurrenceCount $document.Text $block) -ne 1) { throw 'Phase 2 harness block is not intact.' }
            $value = $document.Text.Replace($block + $document.Newline, '')
            if ($value -eq $document.Text) { $value = $document.Text.Replace($block, '') }
            $value
        }
    }
    return ConvertTo-Utf8Bytes $updated $document.HasBom
}

function Remove-Phase2 {
    Assert-GameClosed
    $status = Get-Phase2Status
    if ($status.State -eq 'NOT_INSTALLED') { return $status }
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'Phase 2 receipt is missing.' }
    # Source files may advance after an earlier development receipt is
    # created. The receipt-aware preflight below remains authoritative and
    # blocks any edited owned hash or damaged protected snippet before writes.
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $plans = @()
    foreach ($file in $receipt.Files) {
        $path = Join-Path $GameRoot $file.Relative
        if (-not (Test-Path -LiteralPath $path)) { throw "Installed target is missing: $($file.Relative)" }
        [byte[]]$current = [IO.File]::ReadAllBytes($path)
        [byte[]]$output = $null
        if ($file.Kind -ne 'Owned' -and (Get-Sha256 $path) -eq $file.InstalledHash) {
            if (-not (Test-Path -LiteralPath $file.BackupPath)) { throw "Backup is missing: $($file.Relative)" }
            if ((Get-Sha256 $file.BackupPath) -ne $file.OriginalHash) { throw "Backup checksum failed: $($file.Relative)" }
            $output = [IO.File]::ReadAllBytes($file.BackupPath)
        }
        else { $output = Get-SurgicalRemovalBytes $file }
        $plans += [pscustomobject]@{ Kind = $file.Kind; Relative = $file.Relative; Path = $path; CurrentBytes = $current; OutputBytes = $output }
    }

    $written = @()
    try {
        foreach ($plan in $plans) {
            if ($plan.Kind -eq 'Owned') { Remove-Item -LiteralPath $plan.Path -Force }
            else { Write-AtomicBytes $plan.Path $plan.OutputBytes }
            $written += $plan
        }
        Remove-Item -LiteralPath $receiptPath -Force
        $cachePath = Join-Path $GameRoot $cacheRelative
        if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    }
    catch {
        foreach ($plan in $written) { Write-AtomicBytes $plan.Path $plan.CurrentBytes }
        throw
    }
    return Get-Phase2Status
}

$result = switch ($Action) {
    'Install' { Install-Phase2 }
    'Update' { Update-Phase2OwnedFiles }
    'Remove' { Remove-Phase2 }
    default { Get-Phase2Status }
}
$result | ConvertTo-Json -Depth 8
