param(
    [string]$RaidRescueExe
)

$ErrorActionPreference = 'Stop'

if ([String]::IsNullOrWhiteSpace($RaidRescueExe)) {
    $RaidRescueExe = Join-Path $PSScriptRoot '..\dist\RaidRescue.exe'
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Reset-BitWriter {
    $script:LuaBits = [Collections.Generic.List[int]]::new()
}

function Write-UnsignedBits {
    param([UInt64]$Value, [int]$Count)
    for ($bit = $Count - 1; $bit -ge 0; $bit--) {
        $script:LuaBits.Add([int](($Value -shr $bit) -band 1))
    }
}

function Write-ByteBits {
    param([byte]$Value)
    Write-UnsignedBits ([UInt64]$Value) 8
}

function Align-BitWriter {
    while (($script:LuaBits.Count % 8) -ne 0) {
        $script:LuaBits.Add(0)
    }
}

function Write-LuaString {
    param([string]$Value)
    [byte[]]$bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    Write-ByteBits 4
    Write-UnsignedBits ([UInt64]$bytes.Length) 32
    Align-BitWriter
    foreach ($valueByte in $bytes) {
        Write-ByteBits $valueByte
    }
}

function Write-LuaInteger {
    param([int]$Value)
    Write-ByteBits 6
    Write-UnsignedBits ([UInt64][UInt32]$Value) 32
}

function Write-LuaBoolean {
    param([bool]$Value)
    Write-ByteBits 2
    Write-UnsignedBits ([UInt64]($(if ($Value) { 1 } else { 0 }))) 1
}

function Write-LuaHarvestableReference {
    param([UInt32]$Id)
    Write-ByteBits 100
    Write-UnsignedBits 10025 32
    Write-UnsignedBits ([UInt64]$Id) 32
}

function Write-LuaMapStart {
    param([UInt32]$Count)
    Write-ByteBits 5
    Write-UnsignedBits ([UInt64]$Count) 32
    Write-UnsignedBits 0 1
}

function Complete-LuaPayload {
    [int]$byteCount = [Math]::Ceiling($script:LuaBits.Count / 8.0)
    [byte[]]$result = New-Object byte[] $byteCount
    for ($index = 0; $index -lt $script:LuaBits.Count; $index++) {
        if ($script:LuaBits[$index] -eq 0) {
            continue
        }
        $byteIndex = [Math]::Floor($index / 8)
        $mask = 1 -shl (7 - ($index % 8))
        $result[$byteIndex] = [byte]($result[$byteIndex] -bor $mask)
    }
    return $result
}

function Begin-LuaPayload {
    Reset-BitWriter
    foreach ($magicByte in [Text.Encoding]::ASCII.GetBytes('LUA')) {
        Write-ByteBits $magicByte
    }
    Write-UnsignedBits 1 32
}

function New-CropPayload {
    param(
        [ValidateSet('false', 'true', 'missing', 'invalid')]
        [string]$FlagState = 'false'
    )

    Begin-LuaPayload
    $fieldCount = if ($FlagState -eq 'missing') { 2 } else { 3 }
    Write-LuaMapStart $fieldCount

    Write-LuaString 'waterTick'
    Write-LuaInteger 80
    Write-LuaString 'growStartTick'
    Write-LuaInteger 40

    if ($FlagState -ne 'missing') {
        Write-LuaString 'hasSurvivedRaid'
        if ($FlagState -eq 'invalid') {
            Write-LuaString 'false'
        }
        else {
            Write-LuaBoolean ($FlagState -eq 'true')
        }
    }
    return Complete-LuaPayload
}

function New-RaidPayload {
    param([UInt32]$CropId)

    Begin-LuaPayload
    Write-LuaMapStart 2

    Write-LuaString 'version'
    Write-LuaInteger 1
    Write-LuaString 'worldRaids'
    Write-LuaMapStart 1
    Write-LuaInteger 1
    Write-LuaMapStart 1
    Write-LuaString '1|0|0|0'
    Write-LuaMapStart 5

    Write-LuaString 'key'
    Write-LuaString '1|0|0|0'
    Write-LuaString 'level'
    Write-LuaInteger 1
    Write-LuaString 'value'
    Write-LuaInteger 10
    Write-LuaString 'maxValue'
    Write-LuaInteger 10
    Write-LuaString 'existingCrops'
    Write-LuaMapStart 1
    Write-LuaInteger ([int]$CropId)
    Write-LuaHarvestableReference $CropId

    return Complete-LuaPayload
}

function Compress-LiteralLz4 {
    param([byte[]]$Source)
    $result = [Collections.Generic.List[byte]]::new()
    $length = $Source.Length
    $result.Add([byte]([Math]::Min($length, 15) -shl 4))
    if ($length -ge 15) {
        $remaining = $length - 15
        while ($remaining -ge 255) {
            $result.Add([byte]255)
            $remaining -= 255
        }
        $result.Add([byte]$remaining)
    }
    $result.AddRange($Source)
    return $result.ToArray()
}

function Write-BigUInt16 {
    param([byte[]]$Target, [int]$Offset, [UInt16]$Value)
    $Target[$Offset] = [byte]($Value -shr 8)
    $Target[$Offset + 1] = [byte]$Value
}

function Write-BigUInt32 {
    param([byte[]]$Target, [int]$Offset, [UInt32]$Value)
    $Target[$Offset] = [byte]($Value -shr 24)
    $Target[$Offset + 1] = [byte]($Value -shr 16)
    $Target[$Offset + 2] = [byte]($Value -shr 8)
    $Target[$Offset + 3] = [byte]$Value
}

function New-ScriptBlob {
    param(
        [byte[]]$Uid,
        [byte[]]$Key,
        [int]$WorldId,
        [int]$Flags,
        [byte[]]$LuaPayload
    )

    [byte[]]$compressed = Compress-LiteralLz4 $LuaPayload
    $headerPosition = 18 + $Key.Length
    [byte[]]$blob = New-Object byte[] ($headerPosition + 7 + $compressed.Length)
    [Array]::Copy($Uid, 0, $blob, 0, 16)
    Write-BigUInt16 $blob 16 ([UInt16]$Key.Length)
    [Array]::Copy($Key, 0, $blob, 18, $Key.Length)
    Write-BigUInt16 $blob $headerPosition ([UInt16]$WorldId)
    $blob[$headerPosition + 2] = [byte]$Flags
    Write-BigUInt32 $blob ($headerPosition + 3) ([UInt32]$compressed.Length)
    [Array]::Copy(
        $compressed, 0, $blob, $headerPosition + 7, $compressed.Length)
    return $blob
}

function Convert-UUIDBytes {
    param([string]$Uuid)
    $hex = $Uuid.Replace('-', '')
    [byte[]]$bytes = New-Object byte[] 16
    for ($index = 0; $index -lt 16; $index++) {
        $bytes[$index] = [Convert]::ToByte(
            $hex.Substring($index * 2, 2), 16)
    }
    return $bytes
}

function Convert-HexBytes {
    param([string]$Hex)
    [byte[]]$bytes = New-Object byte[] ($Hex.Length / 2)
    for ($index = 0; $index -lt $bytes.Length; $index++) {
        $bytes[$index] = [Convert]::ToByte(
            $Hex.Substring($index * 2, 2), 16)
    }
    return $bytes
}

function New-HarvestableBlob {
    param([string]$Uuid)
    [byte[]]$blob = New-Object byte[] 64
    [byte[]]$uuidBytes = Convert-UUIDBytes $Uuid
    [Array]::Reverse($uuidBytes)
    [Array]::Copy($uuidBytes, 0, $blob, 20, 16)
    return $blob
}

function New-CropKey {
    param([UInt32]$Id)
    return [byte[]]@(
        [byte]($Id -band 0xff),
        [byte](($Id -shr 8) -band 0xff),
        [byte](($Id -shr 16) -band 0xff),
        [byte](($Id -shr 24) -band 0xff))
}

function Add-ScriptRecord {
    param(
        $Database,
        [byte[]]$Uid,
        [byte[]]$Key,
        [int]$WorldId,
        [int]$Flags,
        [byte[]]$Data
    )
    $statement = $Database.Prepare(
        'INSERT INTO ScriptData(uid,key,worldId,flags,data) VALUES(?1,?2,?3,?4,?5)')
    try {
        $statement.BindBlob(1, $Uid)
        $statement.BindBlob(2, $Key)
        $statement.BindInt64(3, $WorldId)
        $statement.BindInt64(4, $Flags)
        $statement.BindBlob(5, $Data)
        $statement.ExecuteNonQuery()
    }
    finally {
        $statement.Dispose()
    }
}

function Add-HarvestableRecord {
    param(
        $Database,
        [UInt32]$Id,
        [byte[]]$Data
    )
    $statement = $Database.Prepare(
        'INSERT INTO Harvestable(id,worldId,x,y,size,data) VALUES(?1,1,0,0,1,?2)')
    try {
        $statement.BindInt64(1, $Id)
        $statement.BindBlob(2, $Data)
        $statement.ExecuteNonQuery()
    }
    finally {
        $statement.Dispose()
    }
}

function New-Fixture {
    param(
        [string]$Path,
        [UInt32]$CropId,
        [bool]$IncludeRaid,
        [string]$FlagState = 'false',
        [bool]$IncludeCrop = $true
    )

    $database = $script:OpenReadWrite.Invoke(
        $null, [object[]]@([string]$Path, [bool]$true))
    try {
        $database.Execute(
            'CREATE TABLE Game(savegameversion INTEGER, gametick INTEGER);' +
            'CREATE TABLE ScriptData(uid BLOB, key BLOB, worldId INTEGER, flags INTEGER, data BLOB);' +
            'CREATE TABLE Harvestable(id INTEGER PRIMARY KEY, worldId INTEGER, x INTEGER, y INTEGER, size INTEGER, data BLOB);' +
            'CREATE TABLE GenericData(uid BLOB, worldId INTEGER, data BLOB);' +
            'INSERT INTO Game(savegameversion,gametick) VALUES(28,4000);')

        if ($IncludeCrop) {
            [byte[]]$cropUid = [byte[]](1..16)
            [byte[]]$cropKey = New-CropKey $CropId
            [byte[]]$cropData = New-ScriptBlob `
                $cropUid $cropKey 1 0 (New-CropPayload $FlagState)
            Add-HarvestableRecord `
                $database $CropId `
                (New-HarvestableBlob 'c6f80a93-5b16-45ef-a478-ca56a50f61ae')
            Add-ScriptRecord $database $cropUid $cropKey 1 0 $cropData
        }

        if ($IncludeRaid) {
            [byte[]]$raidUid = Convert-UUIDBytes `
                '2c3699b2-fd9c-503e-a405-cf73434e2e88'
            [byte[]]$raidKey = Convert-HexBytes `
                '4C554100000001082D'
            [byte[]]$raidData = New-ScriptBlob `
                $raidUid $raidKey 0 0 (New-RaidPayload $CropId)
            Add-ScriptRecord $database $raidUid $raidKey 0 0 $raidData
        }
    }
    finally {
        $database.Dispose()
    }
}

function Read-CropFlag {
    param([string]$Path, [UInt32]$CropId)
    $database = $script:OpenReadOnly.Invoke(
        $null, [object[]]@([string]$Path))
    try {
        [byte[]]$key = New-CropKey $CropId
        $records = $database.ReadScriptRecords($key, 1)
        Assert-True ($records.Count -eq 1) `
            'The fixture crop storage row could not be read.'
        [object[]]$parseArguments = New-Object object[] 1
        $parseArguments[0] = [byte[]]$records[0].Data
        $payload = $script:ParseScriptData.Invoke(
            $null, $parseArguments)
        $root = $payload.Value
        return $root.Get('hasSurvivedRaid')
    }
    finally {
        $database.Dispose()
    }
}

$resolvedExe = [IO.Path]::GetFullPath($RaidRescueExe)
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'raid-rescue-crop-release-' + [Guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    $assembly = [Reflection.Assembly]::LoadFrom($resolvedExe)
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static'
    $databaseType = $assembly.GetType('RaidRescue.SqliteDatabase', $true)
    $script:OpenReadWrite = $databaseType.GetMethod('OpenReadWrite', $flags)
    $script:OpenReadOnly = $databaseType.GetMethod('OpenReadOnly', $flags)
    $luaType = $assembly.GetType('RaidRescue.LuaStorage', $true)
    $script:ParseScriptData = $luaType.GetMethod('ParseScriptData', $flags)
    $setRootBoolean = $luaType.GetMethod('SetRootBoolean', $flags)
    $service = $assembly.GetType('RaidRescue.RaidService', $true)
    $analyze = $service.GetMethod('AnalyzeCore', $flags)
    $clearRaids = $service.GetMethod('ClearRaidsCore', $flags)
    $repairOrphans = $service.GetMethod(
        'RepairOrphanedRaidCropsCore', $flags)

    [byte[]]$uid = [byte[]](1..16)
    [byte[]]$key = New-CropKey 101
    [byte[]]$originalBlob = New-ScriptBlob `
        $uid $key 1 0 (New-CropPayload 'false')
    [object[]]$rewriteArguments = @(
        [byte[]]$originalBlob,
        [string]'hasSurvivedRaid',
        [bool]$true,
        [bool]$false,
        [bool]$false)
    [byte[]]$rewrittenBlob = $setRootBoolean.Invoke(
        $null, $rewriteArguments)
    Assert-True ([bool]$rewriteArguments[3]) `
        'The Lua mutator did not find hasSurvivedRaid.'
    Assert-True (-not [bool]$rewriteArguments[4]) `
        'The Lua mutator did not report the original false value.'
    [object[]]$originalParseArguments = New-Object object[] 1
    $originalParseArguments[0] = $originalBlob
    $originalPayload = $script:ParseScriptData.Invoke(
        $null, $originalParseArguments)
    [object[]]$rewrittenParseArguments = New-Object object[] 1
    $rewrittenParseArguments[0] = $rewrittenBlob
    $rewrittenPayload = $script:ParseScriptData.Invoke(
        $null, $rewrittenParseArguments)
    Assert-True (
        $rewrittenPayload.Value.Get('hasSurvivedRaid') -eq $true) `
        'The rewritten crop flag was not true.'
    Assert-True (
        $rewrittenPayload.Value.Get('waterTick') -eq 80) `
        'An unrelated crop field changed during rewriting.'
    $differentBytes = 0
    $differentBits = 0
    for ($index = 0;
         $index -lt $originalPayload.Decompressed.Length;
         $index++) {
        $xor = $originalPayload.Decompressed[$index] -bxor
            $rewrittenPayload.Decompressed[$index]
        if ($xor -ne 0) {
            $differentBytes++
            for ($bit = 0; $bit -lt 8; $bit++) {
                if (($xor -band (1 -shl $bit)) -ne 0) {
                    $differentBits++
                }
            }
        }
    }
    Assert-True (
        $differentBytes -eq 1 -and $differentBits -eq 1) `
        'The crop rewrite changed more than the one boolean bit.'

    $activePath = Join-Path $fixtureRoot 'active-raid.db'
    New-Fixture $activePath 101 $true 'false'
    $activeBefore = $analyze.Invoke(
        $null, [object[]]@(
            [string]$activePath, [bool]$false, [bool]$false))
    Assert-True $activeBefore.Success `
        ("Active fixture analysis failed: " + $activeBefore.Error)
    Assert-True ($activeBefore.RaidCount -eq 1) `
        'The active fixture raid was not decoded.'
    Assert-True ($activeBefore.OrphanedRaidCropCount -eq 0) `
        'A crop referenced by an active raid was incorrectly orphaned.'
    Assert-True $activeBefore.CanClear `
        'A safely decodable active raid was not clearable.'

    $clearResult = $clearRaids.Invoke(
        $null, [object[]]@(
            [string]$activePath, [bool]$false))
    Assert-True $clearResult.Success `
        ("Resolve-and-clear failed: " + $clearResult.Error)
    Assert-True ($clearResult.CropsReleased -eq 1) `
        'Resolve-and-clear did not release the registered crop.'
    Assert-True ($clearResult.RecordsRemoved -eq 1) `
        'Resolve-and-clear did not remove the raid-manager record.'
    Assert-True ($clearResult.After.RaidCount -eq 0) `
        'A raid remained after resolve-and-clear.'
    Assert-True ($clearResult.After.OrphanedRaidCropCount -eq 0) `
        'Resolve-and-clear stranded its registered crop.'
    Assert-True ((Read-CropFlag $activePath 101) -eq $true) `
        'The registered crop survival flag was not persisted.'

    $orphanPath = Join-Path $fixtureRoot 'orphaned-crop.db'
    New-Fixture $orphanPath 202 $false 'false'
    $orphanBefore = $analyze.Invoke(
        $null, [object[]]@(
            [string]$orphanPath, [bool]$false, [bool]$false))
    Assert-True $orphanBefore.Success `
        ("Orphan fixture analysis failed: " + $orphanBefore.Error)
    Assert-True ($orphanBefore.RaidCount -eq 0) `
        'The orphan fixture unexpectedly contained a raid.'
    Assert-True ($orphanBefore.OrphanedRaidCropCount -eq 1) `
        'The stranded crop was not detected.'
    Assert-True $orphanBefore.CanRepairOrphanedCrops `
        'The stranded crop repair was not enabled.'

    $orphanResult = $repairOrphans.Invoke(
        $null, [object[]]@(
            [string]$orphanPath, [bool]$false))
    Assert-True $orphanResult.Success `
        ("Orphan repair failed: " + $orphanResult.Error)
    Assert-True ($orphanResult.CropsReleased -eq 1) `
        'The orphan repair did not release exactly one crop.'
    Assert-True ($orphanResult.After.OrphanedRaidCropCount -eq 0) `
        'The orphan remained after repair.'
    Assert-True ((Read-CropFlag $orphanPath 202) -eq $true) `
        'The orphan crop survival flag was not persisted.'

    $safePath = Join-Path $fixtureRoot 'already-safe-crop.db'
    New-Fixture $safePath 303 $true 'true'
    $safeResult = $clearRaids.Invoke(
        $null, [object[]]@(
            [string]$safePath, [bool]$false))
    Assert-True $safeResult.Success `
        ("Already-safe resolve-and-clear failed: " + $safeResult.Error)
    Assert-True ($safeResult.CropsReleased -eq 0) `
        'Resolve-and-clear rewrote a crop that was already safe.'
    Assert-True ($safeResult.CropsAlreadySafe -eq 1) `
        'The already-safe crop was not counted correctly.'

    $stalePath = Join-Path $fixtureRoot 'stale-crop-reference.db'
    New-Fixture $stalePath 404 $true 'false' $false
    $staleBefore = $analyze.Invoke(
        $null, [object[]]@(
            [string]$stalePath, [bool]$false, [bool]$false))
    Assert-True $staleBefore.CanClear `
        'A stale, already-missing crop reference incorrectly locked clearing.'
    $staleResult = $clearRaids.Invoke(
        $null, [object[]]@(
            [string]$stalePath, [bool]$false))
    Assert-True $staleResult.Success `
        ("Stale-reference resolve-and-clear failed: " + $staleResult.Error)
    Assert-True ($staleResult.MissingCropReferences -eq 1) `
        'The stale crop reference was not counted.'
    Assert-True ($staleResult.RecordsRemoved -eq 1) `
        'The raid-manager record remained after skipping a stale crop reference.'

    $unsafePath = Join-Path $fixtureRoot 'unsafe-active-crop.db'
    New-Fixture $unsafePath 505 $true 'invalid'
    $unsafe = $analyze.Invoke(
        $null, [object[]]@(
            [string]$unsafePath, [bool]$false, [bool]$false))
    Assert-True $unsafe.Success `
        ("Unsafe fixture analysis failed: " + $unsafe.Error)
    Assert-True ($unsafe.UnreleasableRaidCropCount -eq 1) `
        'The invalid active crop storage was not reported.'
    Assert-True (-not $unsafe.CanClear) `
        'Raid clearing remained enabled with an unsafe live crop.'

    Write-Host (
        'Crop release regression passed: one-bit Lua rewrite, active crop ' +
        'release, already-safe and stale references, orphan detection and ' +
        'repair, post-write verification, and fail-closed invalid storage.')
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
