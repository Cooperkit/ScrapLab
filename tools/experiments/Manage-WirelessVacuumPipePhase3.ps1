param(
    [ValidateSet('Status', 'Install', 'Update', 'Remove')]
    [string]$Action = 'Status',
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$ReceiptRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development State'),
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'ScrapLab\Development Backups\WirelessVacuumPipePhase3'),
    [switch]$AllowRunningGame
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$receiptPath = Join-Path $ReceiptRoot 'WirelessVacuumPipePhase3.json'
$cacheRelative = 'Cache\Bundle\core_data.cbo'
$markerStart = '-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 3 LINK GRAPH'
$markerEnd = '-- END SCRAPLAB WIRELESS VACUUM PIPE PHASE 3 LINK GRAPH'
$gameMarkerStart = '-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 3 HARNESS'
$gameMarkerEnd = '-- END SCRAPLAB WIRELESS VACUUM PIPE PHASE 3 HARNESS'
$crafterBridgeMarkerStart = '-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE'
$crafterBridgeMarkerEnd = '-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE'
$crafterGuiRequest = 'self.network:sendToServer( "sv_n_requestScrapLabGuiContainers" )'
$pipeEffectGuard = 'if type( shapeList ) ~= "table" or #shapeList < 2 then return end -- SCRAPLAB WIRELESS PIPE VISUAL ROUTE GUARD'
$wrapperDofile = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/ScrapLabPipeGraph.lua" )'
$harnessDofile = 'dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Experiments/WirelessVacuumPipePhase3/WirelessVacuumPipePhase3Harness.lua" )'

$consumerDefinitions = @(
    [pscustomobject]@{
        Kind = 'Crafter'; Relative = 'Survival\Scripts\game\interactables\Crafter.lua'
        Guards = @('function Crafter.server_onFixedUpdate', 'function Crafter.sv_getContainerShapeForRecipe')
        Methods = [ordered]@{ getInputContainers = 3; getOutputContainers = 2; getContainerShapeToCollectTo = 1; getContainerPath = 2 }
    },
    [pscustomobject]@{
        Kind = 'FlatVacuum'; Relative = 'Survival\Scripts\game\interactables\FlatVacuum.lua'
        Guards = @('function FlatVacuum.server_onFixedUpdate', 'function FlatVacuum.cl_n_onIncomingFire')
        Methods = [ordered]@{ getInputContainers = 4; getOutputContainers = 1; getContainerShapeToCollectTo = 2; getContainerPath = 2 }
    },
    [pscustomobject]@{
        Kind = 'GarageChest'; Relative = 'Survival\Scripts\game\interactables\GarageChest.lua'
        Guards = @('function GarageChest.server_onCreate', 'function GarageChest.server_onFixedUpdate')
        Methods = [ordered]@{ getInputContainers = 2 }
    },
    [pscustomobject]@{
        Kind = 'OreCrusher'; Relative = 'Survival\Scripts\game\interactables\OreCrusher.lua'
        Guards = @('function OreCrusher.server_onFixedUpdate', 'function OreCrusher.cl_n_finishProduction')
        Methods = [ordered]@{ getContainerShapeToCollectTo = 2; getContainerPath = 1 }
    },
    [pscustomobject]@{
        Kind = 'Prospector'; Relative = 'Survival\Scripts\game\interactables\Prospector.lua'
        Guards = @('function Prospector.server_onFixedUpdate', 'function Prospector.cl_n_depositToChest')
        Methods = [ordered]@{ getInputContainers = 1; getOutputContainers = 1; getMatchingPipedContainers = 1; getContainerPath = 2 }
    },
    [pscustomobject]@{
        Kind = 'Refinery'; Relative = 'Survival\Scripts\game\interactables\Refinery.lua'
        Guards = @('function Refinery.server_onFixedUpdate', 'function Refinery.cl_n_finishProduction')
        Methods = [ordered]@{ getContainerShapeToCollectTo = 2; getContainerPath = 1 }
    },
    [pscustomobject]@{
        Kind = 'Vacuum'; Relative = 'Survival\Scripts\game\interactables\Vacuum.lua'
        Guards = @('function Vacuum.server_onFixedUpdate', 'function Vacuum.cl_n_onIncomingFire')
        Methods = [ordered]@{ getInputContainers = 8; getOutputContainers = 1; getContainerShapeToCollectTo = 11; getContainerShapeToSpendFrom = 2; getContainerPath = 2 }
    },
    [pscustomobject]@{
        Kind = 'Util'; Relative = 'Survival\Scripts\util.lua'
        Guards = @('function TrySpendFromConnectedContainer', 'function CanSpendFromConnectedContainer')
        Methods = [ordered]@{ getMatchingPipedContainers = 2 }
    },
    [pscustomobject]@{
        Kind = 'PipeEffects'; Relative = 'Survival\Scripts\game\util\pipes.lua'
        Guards = @('function PipeEffectPlayer.pushShapeEffectTask', 'local function ValidatePath')
        Methods = [ordered]@{}
    }
)

$gameDefinition = [pscustomobject]@{ Kind = 'Game'; Relative = 'Survival\Scripts\game\SurvivalGame.lua' }
$ownedFiles = @(
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua'; Relative = 'Survival\Scripts\ScrapLab\PipeSystem\ScrapLabPipeGraph.lua' },
    [pscustomobject]@{ Source = Join-Path $repoRoot 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipePhase3Harness.lua'; Relative = 'Survival\Scripts\ScrapLab\Experiments\WirelessVacuumPipePhase3\WirelessVacuumPipePhase3Harness.lua' }
)

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Get-BytesSha256([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '') } finally { $sha.Dispose() }
}
function Get-Count([string]$Text, [string]$Needle) {
    $count = 0; $offset = 0
    while (($offset = $Text.IndexOf($Needle, $offset, [StringComparison]::Ordinal)) -ge 0) { $count++; $offset += $Needle.Length }
    $count
}
function Get-TextNewline([string]$Text) { if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" } }
function Get-Utf8Document([string]$Path) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($bom) { 3 } else { 0 }
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes, $offset, $bytes.Length - $offset)
    $crlf = $text.Contains("`r`n"); $bareLf = $text.Replace("`r`n", '').Contains("`n")
    if ($crlf -and $bareLf) { throw "Mixed newlines are unsupported: $Path" }
    [pscustomobject]@{ Text = $text; HasBom = $bom; Newline = if ($crlf) { "`r`n" } else { "`n" } }
}
function ConvertTo-Utf8Bytes([string]$Text, [bool]$HasBom) {
    [byte[]]$payload = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    if (-not $HasBom) { return $payload }
    [byte[]]$bytes = New-Object byte[] ($payload.Length + 3)
    $bytes[0] = 0xEF; $bytes[1] = 0xBB; $bytes[2] = 0xBF
    [Array]::Copy($payload, 0, $bytes, 3, $payload.Length)
    $bytes
}
function Write-AtomicBytes([string]$Path, [byte[]]$Bytes) {
    $directory = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporary = Join-Path $directory ('.scraplab-phase3-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllBytes($temporary, $Bytes)
        if (Test-Path -LiteralPath $Path) {
            $old = Join-Path $directory ('.scraplab-phase3-old-' + [Guid]::NewGuid().ToString('N') + '.tmp')
            try { [IO.File]::Replace($temporary, $Path, $old) } finally { if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force } }
        } else { [IO.File]::Move($temporary, $Path) }
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}
function Write-Json([string]$Path, [object]$Value) {
    Write-AtomicBytes $Path ([Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 12)))
}
function Assert-GameClosed {
    if (-not $AllowRunningGame -and (Get-Process -Name ScrapMechanic -ErrorAction SilentlyContinue)) { throw 'Scrap Mechanic is running. Close it before changing Phase 3.' }
}
function Get-NativeCall([string]$Method) { "sm.pipeGraph.$Method" }
function Get-WrapperCall([string]$Method) {
    if ($Method -eq 'getContainerPath') { return 'ScrapLabPipeGraph.getVisualRoute' }
    "ScrapLabPipeGraph.$Method"
}
function Get-ConsumerBlock([string]$Nl) {
    $markerStart + $Nl + 'if ScrapLabPipeGraph == nil then' + $Nl + "`t" + $wrapperDofile + $Nl + 'end' + $Nl + $markerEnd
}
function Get-GameBlock([string]$Nl) { $gameMarkerStart + $Nl + $harnessDofile + $Nl + $gameMarkerEnd }
function Get-CrafterBridgeBlock([string]$Nl) {
    @(
        $crafterBridgeMarkerStart,
        'function Crafter.sv_n_requestScrapLabGuiContainers( self, _, player )',
        "`tlocal shapes = ScrapLabPipeGraph.getGuiInputContainers( self.shape )",
        "`tlocal containers = {}",
        "`tfor _, shape in ipairs( shapes ) do",
        "`t`tlocal ok, container = pcall( function() return GetPipeGraphObjectContainer( shape ) end )",
        "`t`tif ok and container then containers[#containers + 1] = container end",
        "`tend",
        ("`t" + 'print( "[ScrapLab Pipe Crafter] server GUI sync: shapes=" .. tostring( #shapes ) .. ", containers=" .. tostring( #containers ) )'),
        ("`t" + 'self.network:sendToClient( player, "cl_n_setScrapLabGuiContainers", containers )'),
        'end',
        '',
        'function Crafter.cl_n_setScrapLabGuiContainers( self, containers )',
        "`tif self.cl.guiInterface == nil then return end",
        "`tlocal guiContainers = {}",
        "`tfor _, container in ipairs( containers or {} ) do",
        "`t`tif container then guiContainers[#guiContainers + 1] = container end",
        "`tend",
        "`tguiContainers[#guiContainers + 1] = sm.localPlayer.getPlayer():getInventory()",
        ("`t" + 'print( "[ScrapLab Pipe Crafter] client GUI sync: linked=" .. tostring( #guiContainers - 1 ) )'),
        ("`t" + 'self.cl.guiInterface:setContainers( "", guiContainers )'),
        'end',
        $crafterBridgeMarkerEnd
    ) -join $Nl
}
function Get-CrafterBridgeBlockV4([string]$Nl) {
    @(
        $crafterBridgeMarkerStart,
        'function Crafter.sv_n_requestScrapLabGuiContainers( self, _, player )',
        "`tlocal shapes = ScrapLabPipeGraph.getGuiInputContainers( self.shape )",
        "`tlocal containers = {}",
        "`tfor _, shape in ipairs( shapes ) do",
        "`t`tlocal ok, container = pcall( function() return GetPipeGraphObjectContainer( shape ) end )",
        "`t`tif ok and container then containers[#containers + 1] = container end",
        "`tend",
        ('`tprint( "[ScrapLab Pipe Crafter] server GUI sync: shapes=" .. tostring( #shapes ) .. ", containers=" .. tostring( #containers ) )'),
        ("`t" + 'self.network:sendToClient( player, "cl_n_setScrapLabGuiContainers", containers )'),
        'end',
        '',
        'function Crafter.cl_n_setScrapLabGuiContainers( self, containers )',
        "`tif self.cl.guiInterface == nil then return end",
        "`tlocal guiContainers = {}",
        "`tfor _, container in ipairs( containers or {} ) do",
        "`t`tif container then guiContainers[#guiContainers + 1] = container end",
        "`tend",
        "`tguiContainers[#guiContainers + 1] = sm.localPlayer.getPlayer():getInventory()",
        ('`tprint( "[ScrapLab Pipe Crafter] client GUI sync: linked=" .. tostring( #guiContainers - 1 ) )'),
        ("`t" + 'self.cl.guiInterface:setContainers( "", guiContainers )'),
        'end',
        $crafterBridgeMarkerEnd
    ) -join $Nl
}
function Get-CrafterBridgeBlockV3([string]$Nl) {
    @(
        $crafterBridgeMarkerStart,
        'function Crafter.sv_n_requestScrapLabGuiContainers( self, _, player )',
        "`tlocal containers = {}",
        "`tfor _, shape in ipairs( ScrapLabPipeGraph.getGuiInputContainers( self.shape ) ) do",
        "`t`tlocal ok, container = pcall( function() return GetPipeGraphObjectContainer( shape ) end )",
        "`t`tif ok and container then containers[#containers + 1] = container end",
        "`tend",
        ("`t" + 'self.network:sendToClient( player, "cl_n_setScrapLabGuiContainers", containers )'),
        'end',
        '',
        'function Crafter.cl_n_setScrapLabGuiContainers( self, containers )',
        "`tif self.cl.guiInterface == nil then return end",
        "`tlocal guiContainers = {}",
        "`tfor _, container in ipairs( containers or {} ) do",
        "`t`tif container then guiContainers[#guiContainers + 1] = container end",
        "`tend",
        "`tguiContainers[#guiContainers + 1] = sm.localPlayer.getPlayer():getInventory()",
        ("`t" + 'self.cl.guiInterface:setContainers( "", guiContainers )'),
        'end',
        $crafterBridgeMarkerEnd
    ) -join $Nl
}
function Get-ConsumerDefinition([string]$Kind, [string]$Relative) {
    $consumerDefinitions | Where-Object { $_.Kind -eq $Kind -or $_.Relative -eq $Relative } | Select-Object -First 1
}
function Test-ConsumerCallPatch([object]$Definition, [string]$Text) {
    if ((Get-Count $Text $markerStart) -ne 1 -or (Get-Count $Text $wrapperDofile) -ne 1) { return $false }
    foreach ($entry in $Definition.Methods.GetEnumerator()) {
        if ((Get-Count $Text (Get-WrapperCall $entry.Key)) -ne [int]$entry.Value) { return $false }
        if ((Get-Count $Text (Get-NativeCall $entry.Key)) -ne 0) { return $false }
    }
    $true
}
function Test-ConsumerInstalled([object]$Definition, [string]$Text) {
    if (-not (Test-ConsumerCallPatch $Definition $Text)) { return $false }
    if ($Definition.Kind -eq 'Crafter') {
        $bridge = Get-CrafterBridgeBlock (Get-TextNewline $Text)
        $classAnchor = 'Workbench = class( Crafter )'
        return (Get-Count $Text $bridge) -eq 1 -and (Get-Count $Text $crafterGuiRequest) -eq 1 -and
            $Text.IndexOf($bridge, [StringComparison]::Ordinal) -lt $Text.IndexOf($classAnchor, [StringComparison]::Ordinal)
    }
    if ($Definition.Kind -eq 'PipeEffects') { return (Get-Count $Text $pipeEffectGuard) -eq 1 }
    $true
}
function Add-CrafterBridge([string]$Text, [string]$Nl) {
    if ((Get-Count $Text $crafterBridgeMarkerStart) -ne 0 -or (Get-Count $Text $crafterGuiRequest) -ne 0) { throw 'Crafter GUI bridge is partial or already installed.' }
    $anchor = 'if IsCraftBot( self.shape:getShapeUuid() ) or IsSawTable( self.shape:getShapeUuid() ) then'
    if ((Get-Count $Text $anchor) -ne 1) { throw 'Crafter GUI container anchor changed.' }
    $Text = $Text.Replace($anchor, $anchor + $Nl + "`t`t" + $crafterGuiRequest)
    $classAnchor = 'Workbench = class( Crafter )'
    if ((Get-Count $Text $classAnchor) -ne 1) { throw 'Crafter subclass anchor changed.' }
    $Text = $Text.Replace($classAnchor, (Get-CrafterBridgeBlock $Nl) + $Nl + $Nl + $classAnchor)
    $Text
}
function Move-CrafterBridgeBeforeSubclasses([string]$Text, [string]$Nl) {
    $legacy = Get-CrafterBridgeBlockV3 $Nl
    $classAnchor = 'Workbench = class( Crafter )'
    if ((Get-Count $Text $legacy) -ne 1 -or (Get-Count $Text $classAnchor) -ne 1) { throw 'Crafter definition 3 bridge is not intact.' }
    if ($Text.IndexOf($legacy, [StringComparison]::Ordinal) -lt $Text.IndexOf($classAnchor, [StringComparison]::Ordinal)) { throw 'Crafter definition 3 bridge has an unexpected location.' }
    $Text = $Text.Replace($legacy + $Nl, '')
    $Text = $Text.Replace($classAnchor, (Get-CrafterBridgeBlock $Nl) + $Nl + $Nl + $classAnchor)
    $Text
}
function New-ConsumerOutput([object]$Definition, [object]$Document) {
    $text = $Document.Text
    if ((Get-Count $text $markerStart) -ne 0 -or (Get-Count $text $wrapperDofile) -ne 0) { throw "$($Definition.Kind) already contains a Phase 3 or conflicting loader patch." }
    foreach ($guard in $Definition.Guards) {
        if ((Get-Count $text $guard) -ne 1) { throw "$($Definition.Kind) structural guard changed: $guard" }
    }
    foreach ($entry in $Definition.Methods.GetEnumerator()) {
        $native = Get-NativeCall $entry.Key; $wrapper = Get-WrapperCall $entry.Key
        if ((Get-Count $text $native) -ne [int]$entry.Value -or (Get-Count $text $wrapper) -ne 0) {
            throw "$($Definition.Kind) protected $($entry.Key) calls changed; Phase 3 will not patch it."
        }
        $text = $text.Replace($native, $wrapper)
    }
    if ($Definition.Kind -eq 'PipeEffects') {
        $anchor = 'function PipeEffectPlayer.pushShapeEffectTask( self, shapeList, item, delay, minimumDuration )'
        if ((Get-Count $text $anchor) -ne 1 -or (Get-Count $text $pipeEffectGuard) -ne 0) { throw 'Pipe effect visual-route guard anchor changed or is already patched.' }
        $text = $text.Replace($anchor, $anchor + $Document.Newline + $Document.Newline + "`t" + $pipeEffectGuard)
    }
    $text = (Get-ConsumerBlock $Document.Newline) + $Document.Newline + $Document.Newline + $text
    if ($Definition.Kind -eq 'Crafter') { $text = Add-CrafterBridge $text $Document.Newline }
    if (-not (Test-ConsumerInstalled $Definition $text)) { throw "$($Definition.Kind) generated output failed verification." }
    ConvertTo-Utf8Bytes $text $Document.HasBom
}
function New-ModifiedPlan([string]$Kind, [string]$Relative, [byte[]]$OutputBytes) {
    $path = Join-Path $GameRoot $Relative
    [pscustomobject]@{ Kind = $Kind; Relative = $Relative; Path = $path; OriginalBytes = [IO.File]::ReadAllBytes($path); OutputBytes = $OutputBytes; OutputHash = Get-BytesSha256 $OutputBytes }
}

function Get-Phase3Status {
    $signals = 0; $expected = $consumerDefinitions.Count + 1 + $ownedFiles.Count; $details = [ordered]@{}
    foreach ($definition in $consumerDefinitions) {
        $path = Join-Path $GameRoot $definition.Relative
        if (-not (Test-Path -LiteralPath $path)) { $details[$definition.Kind] = 'MISSING'; continue }
        $present = Test-ConsumerInstalled $definition (Get-Utf8Document $path).Text
        $details[$definition.Kind] = if ($present) { 'PRESENT' } else { 'ABSENT' }
        if ($present) { $signals++ }
    }
    $gamePath = Join-Path $GameRoot $gameDefinition.Relative
    if (Test-Path -LiteralPath $gamePath) {
        $gameText = (Get-Utf8Document $gamePath).Text
        $present = (Get-Count $gameText $gameMarkerStart) -eq 1 -and (Get-Count $gameText $harnessDofile) -eq 1
        $details.Game = if ($present) { 'PRESENT' } else { 'ABSENT' }
        if ($present) { $signals++ }
    } else { $details.Game = 'MISSING' }
    foreach ($file in $ownedFiles) {
        $path = Join-Path $GameRoot $file.Relative; $name = 'Owned:' + [IO.Path]::GetFileName($file.Relative)
        $same = (Test-Path -LiteralPath $path) -and (Get-Sha256 $path) -eq (Get-Sha256 $file.Source)
        $details[$name] = if ($same) { 'PRESENT' } elseif (Test-Path -LiteralPath $path) { 'CHANGED' } else { 'ABSENT' }
        if ($same) { $signals++ }
    }
    $state = if ($signals -eq 0) { 'NOT_INSTALLED' } elseif ($signals -eq $expected) { 'INSTALLED' } else { 'PARTIAL_OR_CONFLICT' }
    [pscustomobject]@{ State = $state; Installed = $state -eq 'INSTALLED'; DefinitionVersion = 10; GameRoot = $GameRoot; Receipt = $receiptPath; Details = $details }
}

function New-InstallPlans {
    if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'Survival\Scripts\ScrapLab\PipeSystem\WirelessPipeManager.lua'))) { throw 'Phase 2 manager is not installed.' }
    $plans = @()
    foreach ($definition in $consumerDefinitions) {
        $path = Join-Path $GameRoot $definition.Relative; $doc = Get-Utf8Document $path
        $plans += New-ModifiedPlan $definition.Kind $definition.Relative (New-ConsumerOutput $definition $doc)
    }
    $gamePath = Join-Path $GameRoot $gameDefinition.Relative; $gameDoc = Get-Utf8Document $gamePath; $gameText = $gameDoc.Text
    if ((Get-Count $gameText $gameMarkerStart) -ne 0 -or (Get-Count $gameText $harnessDofile) -ne 0) { throw 'SurvivalGame already contains a Phase 3 harness registration.' }
    if (-not $gameText.EndsWith($gameDoc.Newline, [StringComparison]::Ordinal)) { $gameText += $gameDoc.Newline }
    $gameText += (Get-GameBlock $gameDoc.Newline) + $gameDoc.Newline
    $plans += New-ModifiedPlan $gameDefinition.Kind $gameDefinition.Relative (ConvertTo-Utf8Bytes $gameText $gameDoc.HasBom)
    foreach ($file in $ownedFiles) {
        if (-not (Test-Path -LiteralPath $file.Source)) { throw "Owned source is missing: $($file.Source)" }
        $path = Join-Path $GameRoot $file.Relative
        if (Test-Path -LiteralPath $path) { throw "Owned Phase 3 target already exists: $path" }
        [byte[]]$bytes = [IO.File]::ReadAllBytes($file.Source)
        $plans += [pscustomobject]@{ Kind = 'Owned'; Relative = $file.Relative; Path = $path; OriginalBytes = $null; OutputBytes = $bytes; OutputHash = Get-BytesSha256 $bytes }
    }
    $plans
}
function Add-VerifiedBackup([object]$Plan, [string]$BackupDirectory) {
    if ($null -eq $Plan.OriginalBytes) { return $null }
    $backupPath = Join-Path $BackupDirectory ($Plan.Relative -replace '[\\/:*?"<>|]', '_')
    [IO.File]::WriteAllBytes($backupPath, $Plan.OriginalBytes)
    $hash = Get-BytesSha256 $Plan.OriginalBytes
    if ((Get-Sha256 $backupPath) -ne $hash) { throw "Backup verification failed: $($Plan.Relative)" }
    [pscustomobject]@{ Path = $backupPath; Hash = $hash }
}
function Invoke-Plans([object[]]$Plans, [object[]]$ExistingReceiptFiles, [int]$SchemaVersion) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'); $backupDirectory = Join-Path $BackupRoot $stamp
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    $written = @(); $receiptFiles = @($ExistingReceiptFiles)
    try {
        foreach ($plan in $Plans) {
            $backup = Add-VerifiedBackup $plan $backupDirectory
            Write-AtomicBytes $plan.Path $plan.OutputBytes
            if ((Get-Sha256 $plan.Path) -ne $plan.OutputHash) { throw "Output verification failed: $($plan.Relative)" }
            $written += $plan
            $existing = $receiptFiles | Where-Object { $_.Relative -eq $plan.Relative } | Select-Object -First 1
            if ($existing) {
                $existing.InstalledHash = $plan.OutputHash
            } else {
                $receiptFiles += [pscustomobject]@{ Kind = $plan.Kind; Relative = $plan.Relative; OriginalHash = if ($backup) { $backup.Hash } else { $null }; InstalledHash = $plan.OutputHash; BackupPath = if ($backup) { $backup.Path } else { $null } }
            }
        }
        [IO.Directory]::CreateDirectory($ReceiptRoot) | Out-Null
        Write-Json $receiptPath ([pscustomobject]@{ SchemaVersion = $SchemaVersion; DefinitionVersion = 10; InstalledUtc = [DateTime]::UtcNow.ToString('o'); Files = $receiptFiles })
        if ($Plans.Count -gt 0) {
            $cache = Join-Path $GameRoot $cacheRelative
            if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }
        }
    } catch {
        for ($index = $written.Count - 1; $index -ge 0; $index--) {
            $plan = $written[$index]
            if ($null -ne $plan.OriginalBytes) { Write-AtomicBytes $plan.Path $plan.OriginalBytes }
            elseif (Test-Path -LiteralPath $plan.Path) { Remove-Item -LiteralPath $plan.Path -Force }
        }
        throw
    }
}

function Install-Phase3 {
    Assert-GameClosed
    $status = Get-Phase3Status
    if ($status.State -eq 'INSTALLED') { return $status }
    if ($status.State -ne 'NOT_INSTALLED') { throw "Install blocked by state $($status.State). Use Update for a verified Phase 3 definition migration." }
    if (Test-Path -LiteralPath $receiptPath) { throw 'A stale Phase 3 receipt already exists.' }
    $plans = @(New-InstallPlans)
    try { Invoke-Plans $plans @() 2 } catch { if (Test-Path -LiteralPath $receiptPath) { Remove-Item -LiteralPath $receiptPath -Force }; throw }
    Get-Phase3Status
}

function Update-Phase3 {
    Assert-GameClosed
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'Phase 3 receipt is missing.' }
    [byte[]]$originalReceiptBytes = [IO.File]::ReadAllBytes($receiptPath)
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    foreach ($file in $receipt.Files) {
        $path = Join-Path $GameRoot $file.Relative
        if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $file.InstalledHash) { throw "Installed file no longer matches its receipt: $($file.Relative)" }
    }

    $plans = @()
    foreach ($definition in $consumerDefinitions) {
        $existing = $receipt.Files | Where-Object { $_.Relative -eq $definition.Relative } | Select-Object -First 1
        $path = Join-Path $GameRoot $definition.Relative
        if ($existing) {
            $doc = Get-Utf8Document $path
            if (Test-ConsumerInstalled $definition $doc.Text) {
                # Already current.
            } elseif ($definition.Kind -eq 'Crafter' -and (Test-ConsumerCallPatch $definition $doc.Text) -and (Get-Count $doc.Text (Get-CrafterBridgeBlockV4 $doc.Newline)) -eq 1 -and (Get-Count $doc.Text $crafterGuiRequest) -eq 1) {
                $output = $doc.Text.Replace((Get-CrafterBridgeBlockV4 $doc.Newline), (Get-CrafterBridgeBlock $doc.Newline))
                if (-not (Test-ConsumerInstalled $definition $output)) { throw 'Crafter definition 4 to 5 syntax repair failed generated-output verification.' }
                $plans += New-ModifiedPlan $definition.Kind $definition.Relative (ConvertTo-Utf8Bytes $output $doc.HasBom)
            } elseif ($definition.Kind -eq 'Crafter' -and (Test-ConsumerCallPatch $definition $doc.Text) -and (Get-Count $doc.Text (Get-CrafterBridgeBlockV3 $doc.Newline)) -eq 1 -and (Get-Count $doc.Text $crafterGuiRequest) -eq 1) {
                $output = Move-CrafterBridgeBeforeSubclasses $doc.Text $doc.Newline
                if (-not (Test-ConsumerInstalled $definition $output)) { throw 'Crafter definition 3 to 4 migration failed generated-output verification.' }
                $plans += New-ModifiedPlan $definition.Kind $definition.Relative (ConvertTo-Utf8Bytes $output $doc.HasBom)
            } elseif ($definition.Kind -eq 'Crafter' -and (Test-ConsumerCallPatch $definition $doc.Text) -and (Get-Count $doc.Text $crafterBridgeMarkerStart) -eq 0 -and (Get-Count $doc.Text $crafterGuiRequest) -eq 0) {
                $output = Add-CrafterBridge $doc.Text $doc.Newline
                $plans += New-ModifiedPlan $definition.Kind $definition.Relative (ConvertTo-Utf8Bytes $output $doc.HasBom)
            } else {
                $legacyBridgeCount = if ($definition.Kind -eq 'Crafter') { Get-Count $doc.Text (Get-CrafterBridgeBlockV3 $doc.Newline) } else { -1 }
                $legacyDetail = ''
                if ($definition.Kind -eq 'Crafter' -and $legacyBridgeCount -eq 0) {
                    $expectedLegacy = Get-CrafterBridgeBlockV3 $doc.Newline
                    $actualLegacy = [regex]::Match($doc.Text, '(?s)-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE\r?\n.*?-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE').Value
                    $difference = -1
                    for ($i = 0; $i -lt [Math]::Min($expectedLegacy.Length, $actualLegacy.Length); $i++) { if ($expectedLegacy[$i] -ne $actualLegacy[$i]) { $difference = $i; break } }
                    $legacyDetail = ", expectedLength=$($expectedLegacy.Length), actualLength=$($actualLegacy.Length), firstDifference=$difference"
                }
                throw "$($definition.Kind) receipt exists but its protected snippets are not a verified Phase 3 definition (calls=$(Test-ConsumerCallPatch $definition $doc.Text), legacyBridge=$legacyBridgeCount, request=$(Get-Count $doc.Text $crafterGuiRequest)$legacyDetail)."
            }
        } else {
            $doc = Get-Utf8Document $path
            $plans += New-ModifiedPlan $definition.Kind $definition.Relative (New-ConsumerOutput $definition $doc)
        }
    }
    $gameEntry = $receipt.Files | Where-Object { $_.Relative -eq $gameDefinition.Relative } | Select-Object -First 1
    if (-not $gameEntry) { throw 'Legacy Phase 3 receipt is missing its SurvivalGame harness entry.' }
    foreach ($owned in $ownedFiles) {
        $entry = $receipt.Files | Where-Object { $_.Relative -eq $owned.Relative } | Select-Object -First 1
        if (-not $entry) { throw "Legacy Phase 3 receipt is missing owned file: $($owned.Relative)" }
        [byte[]]$current = [IO.File]::ReadAllBytes((Join-Path $GameRoot $owned.Relative)); [byte[]]$output = [IO.File]::ReadAllBytes($owned.Source)
        $outputHash = Get-BytesSha256 $output
        if ((Get-BytesSha256 $current) -ne $outputHash) {
            $plans += [pscustomobject]@{ Kind = 'Owned'; Relative = $owned.Relative; Path = Join-Path $GameRoot $owned.Relative; OriginalBytes = $current; OutputBytes = $output; OutputHash = $outputHash }
        }
    }
    try {
        Invoke-Plans $plans @($receipt.Files) 2
    } catch {
        Write-AtomicBytes $receiptPath $originalReceiptBytes
        throw
    }
    Get-Phase3Status
}

function Get-SurgicalRemoval([object]$File) {
    $path = Join-Path $GameRoot $File.Relative
    if ($File.Kind -eq 'Owned') {
        if ((Get-Sha256 $path) -ne $File.InstalledHash) { throw "Owned Phase 3 file changed: $($File.Relative)" }
        return $null
    }
    $doc = Get-Utf8Document $path; $text = $doc.Text
    if ($File.Kind -eq 'Game') {
        $block = Get-GameBlock $doc.Newline
        if ((Get-Count $text $block) -ne 1) { throw 'Phase 3 harness registration is not intact.' }
        $text = $text.Replace($block + $doc.Newline, '')
        if ($text -eq $doc.Text) { $text = $text.Replace($block, '') }
    } else {
        $definition = Get-ConsumerDefinition $File.Kind $File.Relative
        if (-not $definition) { throw "Unknown Phase 3 consumer receipt entry: $($File.Relative)" }
        $block = Get-ConsumerBlock $doc.Newline
        if ((Get-Count $text $block) -ne 1 -or -not (Test-ConsumerInstalled $definition $text)) { throw "$($definition.Kind) Phase 3 snippets are not intact." }
        $text = $text.Replace($block + $doc.Newline + $doc.Newline, '')
        if ($definition.Kind -eq 'Crafter') {
            $bridgeBlock = Get-CrafterBridgeBlock $doc.Newline
            $text = $text.Replace("`t`t" + $crafterGuiRequest + $doc.Newline, '')
            $text = $text.Replace($bridgeBlock + $doc.Newline, '')
            if ((Get-Count $text $crafterBridgeMarkerStart) -ne 0 -or (Get-Count $text $crafterGuiRequest) -ne 0) { throw 'Crafter GUI bridge could not be removed surgically.' }
        }
        if ($definition.Kind -eq 'PipeEffects') {
            $text = $text.Replace("`t" + $pipeEffectGuard + $doc.Newline, '')
            if ((Get-Count $text $pipeEffectGuard) -ne 0) { throw 'Pipe effect visual-route guard could not be removed surgically.' }
        }
        foreach ($entry in $definition.Methods.GetEnumerator()) { $text = $text.Replace((Get-WrapperCall $entry.Key), (Get-NativeCall $entry.Key)) }
    }
    ConvertTo-Utf8Bytes $text $doc.HasBom
}

function Remove-Phase3 {
    Assert-GameClosed
    $status = Get-Phase3Status
    if ($status.State -eq 'NOT_INSTALLED') { return $status }
    if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'Phase 3 receipt is missing.' }
    # A later phase can legitimately replace an owned development file after
    # this receipt was written. The receipt-aware preflight below still
    # requires every owned hash or protected surgical snippet to be intact.
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $plans = @()
    foreach ($file in $receipt.Files) {
        $path = Join-Path $GameRoot $file.Relative
        if (-not (Test-Path -LiteralPath $path)) { throw "Installed file is missing: $($file.Relative)" }
        [byte[]]$current = [IO.File]::ReadAllBytes($path); [byte[]]$output = $null
        if ($file.Kind -ne 'Owned' -and (Get-Sha256 $path) -eq $file.InstalledHash) {
            if (-not (Test-Path -LiteralPath $file.BackupPath) -or (Get-Sha256 $file.BackupPath) -ne $file.OriginalHash) { throw "Backup verification failed: $($file.Relative)" }
            $output = [IO.File]::ReadAllBytes($file.BackupPath)
        } else { $output = Get-SurgicalRemoval $file }
        $plans += [pscustomobject]@{ Kind = $file.Kind; Path = $path; Current = $current; Output = $output }
    }
    $written = @()
    try {
        foreach ($plan in $plans) {
            if ($plan.Kind -eq 'Owned') { Remove-Item -LiteralPath $plan.Path -Force } else { Write-AtomicBytes $plan.Path $plan.Output }
            $written += $plan
        }
        Remove-Item -LiteralPath $receiptPath -Force
        $cache = Join-Path $GameRoot $cacheRelative
        if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }
    } catch {
        foreach ($plan in $written) { Write-AtomicBytes $plan.Path $plan.Current }
        throw
    }
    Get-Phase3Status
}

$result = switch ($Action) {
    'Install' { Install-Phase3 }
    'Update' { Update-Phase3 }
    'Remove' { Remove-Phase3 }
    default { Get-Phase3Status }
}
$result | ConvertTo-Json -Depth 10
