param(
    [string]$PatchHelperExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe')
)

$ErrorActionPreference = 'Stop'
$binding = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERTION FAILED: $Message" }
}

function Invoke-Static([Type]$Type, [string]$Name, [object[]]$Arguments) {
    $method = $Type.GetMethods($binding) | Where-Object {
        $_.Name -eq $Name -and $_.GetParameters().Count -eq $Arguments.Count
    }
    if (@($method).Count -ne 1) {
        throw "Expected one $($Type.FullName).$Name overload."
    }
    $parameters = $method.GetParameters()
    [object[]]$invokeArguments = @(
        for ($index = 0; $index -lt $Arguments.Count; $index++) {
            if ($parameters[$index].ParameterType -eq [string]) {
                [string]$Arguments[$index]
            }
            elseif ($parameters[$index].ParameterType -eq [bool]) {
                [bool]$Arguments[$index]
            }
            else { $Arguments[$index] }
        }
    )
    try { return $method.Invoke($null, $invokeArguments) }
    catch [Reflection.TargetInvocationException] { throw $_.Exception.InnerException }
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

$assembly = [Reflection.Assembly]::LoadFrom(
    (Resolve-Path -LiteralPath $PatchHelperExe).Path)
$gameType = $assembly.GetType('RaidRescue.GamePatchService', $true)
$supportType = $assembly.GetType('RaidRescue.AdaptivePatchSupport', $true)
$detectorType = $assembly.GetType('RaidRescue.RaidDetectorPatchService', $true)
$liveGame = Invoke-Static $gameType 'FindGameInstall' @()
Assert-True (-not [String]::IsNullOrWhiteSpace($liveGame)) `
    'Scrap Mechanic install was not found.'

$fixtureRoot = Join-Path $PSScriptRoot (
    '.raid-detector-fixture-' + [Guid]::NewGuid().ToString('N'))
$fakeGame = Join-Path $fixtureRoot 'steamapps\common\Scrap Mechanic'
$backupRoot = Join-Path $fixtureRoot 'backups'
$receiptRoot = Join-Path $fixtureRoot 'receipts'
$languages = @(
    'Brazilian','Chinese','English','French','German','Italian',
    'Japanese','Korean','Polish','Russian','Spanish')
$targets = @(
    'Survival\Objects\Database\shapesets.json',
    'Survival\Scripts\game\survival_items.lua',
    'Survival\CraftingRecipes\hideout.json',
    'Survival\Scripts\game\interactables\HideoutTrader.lua',
    'Survival\Gui\IconMapSurvival.xml',
    'Survival\Gui\IconMapSurvival.png')
$targets += $languages | ForEach-Object {
    "Survival\Gui\Language\$_\inventoryDescriptions.json"
}
$liveReceiptPath = Join-Path $env:LOCALAPPDATA `
    'ScrapLab\Patch State\Active\RaidDetector.json'
$liveReceipt = $null
if (Test-Path -LiteralPath $liveReceiptPath) {
    $liveReceipt = Get-Content -LiteralPath $liveReceiptPath -Raw |
        ConvertFrom-Json
}

try {
    [IO.Directory]::CreateDirectory((Join-Path $fakeGame 'Release')) | Out-Null
    Copy-Item -LiteralPath (Join-Path $liveGame 'Release\ScrapMechanic.exe') `
        -Destination (Join-Path $fakeGame 'Release\ScrapMechanic.exe')
    foreach ($relative in $targets) {
        $destination = Join-Path $fakeGame $relative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) |
            Out-Null
        $source = Join-Path $liveGame $relative
        if ($null -ne $liveReceipt) {
            $receiptFile = $liveReceipt.Files | Where-Object {
                $_.RelativePath -eq $relative -and
                $_.SourceHash -ne 'MISSING' -and
                (Test-Path -LiteralPath $_.BackupPath)
            } | Select-Object -First 1
            if ($null -ne $receiptFile) { $source = $receiptFile.BackupPath }
        }
        Copy-Item -LiteralPath $source -Destination $destination
    }

    $manifest = Join-Path $fixtureRoot 'steamapps\appmanifest_387990.acf'
    $updated = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Utf8NoBom $manifest (
        '"AppState"' + "`n{" +
        "`n`t" + '"appid"' + "`t`t" + '"387990"' +
        "`n`t" + '"buildid"' + "`t`t" + '"24529696"' +
        "`n`t" + '"LastUpdated"' + "`t`t" + '"' + $updated + '"' +
        "`n}`n")
    $supportType.GetField('PatchStateRootOverride', $binding).SetValue(
        $null, $receiptRoot)

    $baseline = @{}
    foreach ($relative in $targets) {
        $baseline[$relative] = [IO.File]::ReadAllBytes(
            (Join-Path $fakeGame $relative))
    }

    $cleanStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True $cleanStatus.Success ('Clean probe failed: ' + $cleanStatus.Error)
    Assert-True (-not $cleanStatus.Installed) 'Clean fixture reported installed.'
    Assert-True $cleanStatus.CanApply 'Verified current build was not installable.'

    $replaceHookField = $supportType.GetField(
        'ReplaceFileCompletedForTest', $binding)
    $replaceFailure = [Action[string,string]]{
        param($path, $operation)
        if ($operation.Contains('RaidDetector-adaptive')) {
            throw 'Injected Raid Detector transaction failure.'
        }
    }
    $replaceHookField.SetValue($null, $replaceFailure)
    try {
        $failedInstall = Invoke-Static $detectorType 'SetEnabledAt' `
            @($fakeGame, $backupRoot, $true)
    }
    finally { $replaceHookField.SetValue($null, $null) }
    Assert-True (-not $failedInstall.Success) `
        'An injected detector write failure was incorrectly accepted.'
    foreach ($relative in $targets) {
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$relative],
            [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $relative)))) `
            "Rollback failed for $relative."
    }

    $install = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $true)
    Assert-True $install.Success ('Install failed: ' + $install.Error)
    Assert-True $install.Installed 'Install did not report installed.'
    Assert-True ($install.FilesPatched -eq 19) `
        "Install changed $($install.FilesPatched) files instead of 19."
    $sharedAtlasBaseline = Join-Path $backupRoot `
        'ScrapLab-Shared-Icon-Atlas\IconMapSurvival.baseline.png'
    $sharedAtlasReceipt = Join-Path $backupRoot `
        'ScrapLab-Shared-Icon-Atlas\atlas-receipt.json'
    $sharedStateReceipt = Join-Path $receiptRoot `
        'ScrapLab-Icon-Pack.json'
    Assert-True (Test-Path -LiteralPath $sharedAtlasBaseline) `
        'The bounded shared atlas baseline was not created.'
    Assert-True (Test-Path -LiteralPath $sharedAtlasReceipt) `
        'The shared atlas ownership receipt was not created.'
    Assert-True (Test-Path -LiteralPath $sharedStateReceipt) `
        'The authoritative shared icon-pack state was not created.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path `
        $install.BackupPath 'Survival\Gui\IconMapSurvival.png'))) `
        'The transaction duplicated the 11 MB atlas in its timestamped backup.'

    $uuid = 'a638a8aa-6f4f-41c2-9e31-702687066092'
    $script = Join-Path $fakeGame `
        'Survival\Scripts\ScrapLab\Parts\RaidDetector\RaidDetector.lua'
    $shape = Join-Path $fakeGame `
        'Survival\Objects\Database\ShapeSets\ScrapLab\Parts\RaidDetector.shapeset'
    Assert-True (Test-Path -LiteralPath $script) 'Owned detector script missing.'
    Assert-True (Test-Path -LiteralPath $shape) 'Owned shape set missing.'
    $scriptText = Get-Content -LiteralPath $script -Raw -Encoding UTF8
    Assert-True ($scriptText.Contains(
        'DetectionRadiusSquared = 256 * 256')) 'Detector range guard missing.'
    Assert-True ($scriptText.Contains(
        'ScanIntervalTicks = 10')) 'Detector scan interval guard missing.'
    Assert-True ($scriptText.Contains(
        'worldRaids[world.id]')) 'Detector did not isolate the current world.'
    Assert-True ($scriptText.Contains(
        'local body = shape and shape.body or nil')) `
        'Detector did not guard its interactable body lookup.'
    Assert-True ($scriptText.Contains(
        'body and body:getWorld() or nil')) `
        'Detector still calls the unsupported Shape.getWorld API.'
    Assert-True (-not $scriptText.Contains('shape:getWorld()')) `
        'Detector still contains the crashing Shape.getWorld call.'
    Assert-True ($scriptText.Contains(
        'raid.center and raid.attackData')) `
        'Detector did not require a scheduled or active raid record.'
    Assert-True ($scriptText.Contains(
        'offset:length2() <= DetectionRadiusSquared')) `
        'Detector did not include the exact 256-meter boundary.'
    $iconXml = Get-Content -LiteralPath (Join-Path $fakeGame `
        'Survival\Gui\IconMapSurvival.xml') -Raw -Encoding UTF8
    Assert-True ($iconXml.Contains($uuid)) 'Icon registration missing.'
    Assert-True ($iconXml.Contains('<Frame point="3936 3936"/>')) `
        'The icon pack did not allocate from the bottom-right atlas cell.'
    $atlasReceipt = Get-Content -LiteralPath $sharedAtlasReceipt -Raw |
        ConvertFrom-Json
    Assert-True ($atlasReceipt.CatalogVersion -eq '3') `
        'The shared icon-pack catalog version was not recorded.'
    Assert-True ($atlasReceipt.ActiveMods -contains 'RaidDetector') `
        'The shared icon-pack receipt did not record the active mod.'
    $raidIconReceipt = @($atlasReceipt.Icons | Where-Object {
        $_.Uuid -eq $uuid
    })
    Assert-True ($raidIconReceipt.Count -eq 1 -and
        $raidIconReceipt[0].X -eq 3936 -and
        $raidIconReceipt[0].Y -eq 3936) `
        'The shared receipt did not retain the bottom-atlas assignment.'
    Assert-True ((Get-Content -LiteralPath (
        Join-Path $fakeGame 'Survival\CraftingRecipes\hideout.json') `
        -Raw -Encoding UTF8).Contains($uuid)) 'Hideout trade missing.'
    Get-Content -LiteralPath (Join-Path $fakeGame `
        'Survival\Objects\Database\shapesets.json') -Raw -Encoding UTF8 |
        ConvertFrom-Json | Out-Null
    $tradeJson = Get-Content -LiteralPath (Join-Path $fakeGame `
        'Survival\CraftingRecipes\hideout.json') -Raw -Encoding UTF8
    $tradeJson.Substring($tradeJson.IndexOf('[')) |
        ConvertFrom-Json | Out-Null
    Get-Content -LiteralPath $shape -Raw -Encoding UTF8 |
        ConvertFrom-Json | Out-Null
    [xml](Get-Content -LiteralPath (Join-Path $fakeGame `
        'Survival\Gui\IconMapSurvival.xml') -Raw -Encoding UTF8) | Out-Null
    foreach ($language in $languages) {
        $languageJson = Get-Content -LiteralPath (Join-Path $fakeGame `
            "Survival\Gui\Language\$language\inventoryDescriptions.json") `
            -Raw -Encoding UTF8
        $languageJson.Substring($languageJson.IndexOf('{')) |
            ConvertFrom-Json | Out-Null
    }

    $installedStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True ($installedStatus.Success -and $installedStatus.Installed) `
        'Restart probe did not recognize the complete installation.'
    Assert-True (-not $installedStatus.NeedsUpdate) `
        'A new transparent-icon installation incorrectly requested an update.'

    # Reproduce the real install-order bug: another ScrapLab part appends its
    # own localization after Raid Detector in the same shared JSON object.
    $englishRelative = `
        'Survival\Gui\Language\English\inventoryDescriptions.json'
    $englishPath = Join-Path $fakeGame $englishRelative
    $englishText = [IO.File]::ReadAllText($englishPath)
    $englishNewline = if ($englishText.Contains("`r`n")) { "`r`n" } else { "`n" }
    $foreignUuid = '11111111-2222-4333-8444-555555555555'
    $foreignEntry = `
        "`t`"$foreignUuid`": {$englishNewline" +
        "`t`t`"description`": `"Shared localization regression.`",$englishNewline" +
        "`t`t`"title`": `"Other ScrapLab Part`",$englishNewline" +
        "`t`t`"upperCaseTitle`": `"OTHER SCRAPLAB PART`"$englishNewline" +
        "`t}"
    $objectEnd = $englishText.LastIndexOf($englishNewline + '}')
    Assert-True ($objectEnd -ge 0) 'English localization fixture has no object ending.'
    Write-Utf8NoBom $englishPath (
        $englishText.Substring(0, $objectEnd) + ',' + $englishNewline + $foreignEntry +
        $englishText.Substring($objectEnd))
    $composedStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True ($composedStatus.Success -and $composedStatus.Installed) `
        ('A later shared localization entry disabled Raid Detector: ' +
            $composedStatus.CompatibilityState + ' / ' +
            $composedStatus.CompatibilityReason + ' / ' +
            $composedStatus.Error)
    $composedRemove = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $false)
    Assert-True $composedRemove.Success `
        ('Composed localization removal failed: ' + $composedRemove.Error)
    Assert-True ([IO.File]::ReadAllText($englishPath).Contains($foreignUuid)) `
        'Raid Detector removal deleted another mod localization entry.'
    [IO.File]::WriteAllBytes($englishPath, $baseline[$englishRelative])
    $composedReinstall = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $true)
    Assert-True ($composedReinstall.Success -and $composedReinstall.Installed) `
        ('Reinstall after composed localization test failed: ' +
            $composedReinstall.Error)

    # Recreate the exact definition-1 script and legacy opaque tile to prove
    # existing installations migrate atomically without replacing their clean
    # uninstall receipt.
    $legacyScriptPath = Join-Path $PSScriptRoot `
        '..\source\Patching\Parts\RaidDetector\RaidDetectorLegacyV1.lua'
    [IO.File]::WriteAllBytes($script,
        [IO.File]::ReadAllBytes($legacyScriptPath))
    Add-Type -AssemblyName System.Drawing
    $legacyStream = $assembly.GetManifestResourceStream(
        'RaidRescue.Parts.RaidDetector.RaidDetectorIconLegacyOpaque.png')
    Assert-True ($null -ne $legacyStream) 'Legacy icon fixture is missing.'
    try {
        $legacyMemory = [IO.MemoryStream]::new()
        $legacyStream.CopyTo($legacyMemory)
        [byte[]]$legacyBytes = $legacyMemory.ToArray()
    }
    finally {
        if ($null -ne $legacyMemory) { $legacyMemory.Dispose() }
        $legacyStream.Dispose()
    }
    $atlasPath = Join-Path $fakeGame 'Survival\Gui\IconMapSurvival.png'
    $atlasBitmap = [Drawing.Bitmap]::new($atlasPath)
    $legacyBitmapStream = [IO.MemoryStream]::new($legacyBytes, $false)
    $legacyBitmap = [Drawing.Bitmap]::new($legacyBitmapStream)
    try {
        for ($pixelY = 0; $pixelY -lt 96; $pixelY++) {
            for ($pixelX = 0; $pixelX -lt 96; $pixelX++) {
                $atlasBitmap.SetPixel(
                    3936 + $pixelX, 3936 + $pixelY,
                    $legacyBitmap.GetPixel($pixelX, $pixelY))
            }
        }
        $legacyAtlasPath = $atlasPath + '.legacy.png'
        $atlasBitmap.Save($legacyAtlasPath, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $legacyBitmap.Dispose()
        $legacyBitmapStream.Dispose()
        $atlasBitmap.Dispose()
    }
    Move-Item -LiteralPath $legacyAtlasPath -Destination $atlasPath -Force
    $legacyIconHash = (Get-FileHash -LiteralPath (
        Join-Path $PSScriptRoot `
            '..\source\Patching\Parts\RaidDetector\RaidDetectorIconLegacyOpaque.png') `
        -Algorithm SHA256).Hash
    $legacyAtlasHash = (Get-FileHash -LiteralPath $atlasPath `
        -Algorithm SHA256).Hash
    foreach ($sharedReceiptPath in @(
        $sharedStateReceipt, $sharedAtlasReceipt)) {
        $legacyReceipt = Get-Content -LiteralPath $sharedReceiptPath -Raw |
            ConvertFrom-Json
        $legacyReceipt.AtlasOutputHash = $legacyAtlasHash
        $legacyReceipt.Icons[0].IconHash = $legacyIconHash
        Write-Utf8NoBom $sharedReceiptPath (
            $legacyReceipt | ConvertTo-Json -Depth 10 -Compress)
    }
    $modReceiptPath = Join-Path $receiptRoot 'RaidDetector.json'
    [byte[]]$modReceiptBeforeDefinitionUpdate = [IO.File]::ReadAllBytes(
        $modReceiptPath)
    $legacyStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True ($legacyStatus.Success -and $legacyStatus.Installed -and
        $legacyStatus.NeedsUpdate) `
        'The verified definition-1 detector did not expose a safe update.'
    $definitionUpdate = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $true)
    Assert-True ($definitionUpdate.Success -and $definitionUpdate.Installed -and
        -not $definitionUpdate.NeedsUpdate -and
        $definitionUpdate.FilesPatched -eq 2) `
        ('Detector definition migration failed: ' + $definitionUpdate.Error)
    Assert-True ([Linq.Enumerable]::SequenceEqual(
        $modReceiptBeforeDefinitionUpdate,
        [byte[]][IO.File]::ReadAllBytes($modReceiptPath))) `
        'The definition update replaced the original clean uninstall receipt.'
    $updatedScriptText = Get-Content -LiteralPath $script -Raw -Encoding UTF8
    Assert-True ($updatedScriptText.Contains(
        'body and body:getWorld() or nil') -and
        -not $updatedScriptText.Contains('shape:getWorld()')) `
        'The definition update did not install the corrected world lookup.'
    $updatedStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True ($updatedStatus.Success -and $updatedStatus.Installed -and
        -not $updatedStatus.NeedsUpdate) `
        'The Raid Detector definition update was not recognized after restart.'

    # Users who already accepted the transparent-icon migration must also be
    # able to apply the logic fix by itself.
    [IO.File]::WriteAllBytes($script,
        [IO.File]::ReadAllBytes($legacyScriptPath))
    $logicOnlyStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True ($logicOnlyStatus.Success -and $logicOnlyStatus.Installed -and
        $logicOnlyStatus.NeedsUpdate -and
        $logicOnlyStatus.CompatibilityReason.Contains('logic fix')) `
        'The standalone definition-1 logic fix was not offered.'
    $logicOnlyUpdate = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $true)
    Assert-True ($logicOnlyUpdate.Success -and
        $logicOnlyUpdate.FilesPatched -eq 1) `
        ('Standalone detector logic migration failed: ' +
            $logicOnlyUpdate.Error)
    Assert-True ((Get-Content -LiteralPath $script -Raw -Encoding UTF8).Contains(
        'body and body:getWorld() or nil')) `
        'Standalone detector logic migration restored the wrong script.'

    $shapeIndexPath = Join-Path $fakeGame `
        'Survival\Objects\Database\shapesets.json'
    $shapeIndexInstalled = [IO.File]::ReadAllBytes($shapeIndexPath)
    $shapeIndexText = [IO.File]::ReadAllText($shapeIndexPath)
    $shapeNewline = "`n"
    if ($shapeIndexText.Contains("`r`n")) { $shapeNewline = "`r`n" }
    $shapeRegistration = $shapeNewline +
        "`t`t`"`$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/RaidDetector.shapeset`","
    $shapeIndexText = $shapeIndexText.Replace(
        $shapeRegistration, '')
    Write-Utf8NoBom $shapeIndexPath $shapeIndexText
    $knownManifest = [IO.File]::ReadAllText($manifest)
    Write-Utf8NoBom $manifest ($knownManifest.Replace(
        '"24529696"', '"99999999"'))
    $updateStatus = Invoke-Static $detectorType 'GetStatusAt' @($fakeGame)
    Assert-True ($updateStatus.CompatibilityState -eq
        'REINSTALL REQUIRED - SAVE PART AT RISK') `
        ('A Steam partial overwrite did not report the save-part risk: ' +
            $updateStatus.CompatibilityState + ' / build ' +
            $updateStatus.SteamBuildId + ' / receipt ' +
            (Test-Path -LiteralPath (Join-Path $receiptRoot 'RaidDetector.json')) +
            ' / ' + $updateStatus.Error)
    [IO.File]::WriteAllBytes($shapeIndexPath, $shapeIndexInstalled)
    Write-Utf8NoBom $manifest $knownManifest

    $scriptOriginal = [IO.File]::ReadAllBytes($script)
    [IO.File]::AppendAllText($script, "`n-- TAMPER TEST`n",
        [Text.UTF8Encoding]::new($false))
    $blocked = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $false)
    Assert-True (-not $blocked.Success) `
        'Removal accepted an edited owned detector script.'
    [IO.File]::WriteAllBytes($script, $scriptOriginal)

    $remove = Invoke-Static $detectorType 'SetEnabledAt' `
        @($fakeGame, $backupRoot, $false)
    Assert-True $remove.Success ('Removal failed: ' + $remove.Error)
    Assert-True (-not $remove.Installed) 'Removal still reported installed.'
    Assert-True (-not (Test-Path -LiteralPath $script)) `
        'Owned detector script remained after removal.'
    Assert-True (-not (Test-Path -LiteralPath $shape)) `
        'Owned detector shape set remained after removal.'
    foreach ($relative in $targets) {
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$relative],
            [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $relative)))) `
            "Byte-exact restore failed for $relative."
    }
    Assert-True (-not (Test-Path -LiteralPath $sharedAtlasReceipt)) `
        'The inactive shared icon-pack receipt was not removed.'
    Assert-True (-not (Test-Path -LiteralPath $sharedAtlasBaseline)) `
        'The final custom-part removal left a stale atlas baseline.'
    Assert-True (-not (Test-Path -LiteralPath $sharedStateReceipt)) `
        'The final custom-part removal left stale icon-pack state.'

    Write-Host 'Raid Detector regression tests passed.'
}
finally {
    $supportType.GetField('PatchStateRootOverride', $binding).SetValue(
        $null, $null)
    $resolvedTests = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot).TrimEnd('\')
    if ($resolvedFixture.StartsWith(
        $resolvedTests + '\', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Directory]::Exists($resolvedFixture)) {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
