param(
    [string]$RaidRescueExe = (Join-Path $PSScriptRoot '..\dist\ScrapLab.PatchHelper.exe')
)

$ErrorActionPreference = 'Stop'
$binding = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
}

function Get-StaticField([Type]$Type, [string]$Name) {
    $field = $Type.GetField($Name, $binding)
    if ($null -eq $field) {
        throw "Field $($Type.FullName).$Name was not found."
    }
    if ($field.IsLiteral) {
        return $field.GetRawConstantValue()
    }
    return $field.GetValue($null)
}

function Invoke-Static(
    [Type]$Type,
    [string]$Name,
    [Parameter(ValueFromRemainingArguments = $true)]
    [object[]]$Arguments
) {
    $methods = $Type.GetMethods($binding) | Where-Object {
        $_.Name -eq $Name -and
        $_.GetParameters().Count -eq $Arguments.Count
    }
    if ($methods.Count -ne 1) {
        throw "Expected one $($Type.FullName).$Name overload, found $($methods.Count)."
    }
    $parameters = $methods[0].GetParameters()
    [object[]]$invokeArguments = @(
        for ($index = 0; $index -lt $Arguments.Count; $index++) {
            if ($parameters[$index].ParameterType -eq [string]) {
                [string]$Arguments[$index]
            }
            elseif ($parameters[$index].ParameterType -eq [bool]) {
                [bool]$Arguments[$index]
            }
            else {
                $Arguments[$index]
            }
        }
    )
    try {
        return $methods[0].Invoke($null, $invokeArguments)
    }
    catch [Reflection.TargetInvocationException] {
        throw $_.Exception.InnerException
    }
    catch {
        $argumentSummary = ($invokeArguments | ForEach-Object {
            if ($null -eq $_) { '(null)' }
            else { $_.GetType().FullName + '=' + [string]$_ }
        }) -join '; '
        throw "$($Type.FullName).$Name invocation failed: $($_.Exception.Message) Arguments: $argumentSummary"
    }
}

function Normalize-Lua([string]$Text) {
    return $Text.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    $parent = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Copy-CleanFixture(
    [string]$RelativePath,
    [scriptblock]$CleanTransform
) {
    $source = Join-Path $liveGame $RelativePath
    $destination = Join-Path $fakeGame $RelativePath
    $text = Normalize-Lua ([IO.File]::ReadAllText(
        $source, [Text.UTF8Encoding]::new($false, $true)))
    $clean = & $CleanTransform $text $source
    Write-Utf8NoBom $destination (
        $clean.TrimEnd("`n") +
        "`n-- FUTURE BUILD: unrelated compatibility-test line`n")
}

$exePath = (Resolve-Path -LiteralPath $RaidRescueExe).Path
$assembly = [Reflection.Assembly]::LoadFrom($exePath)
$gameService = $assembly.GetType('RaidRescue.GamePatchService', $true)
$supportType = $assembly.GetType('RaidRescue.AdaptivePatchSupport', $true)
$resourceType = $assembly.GetType('RaidRescue.SecretModPatchService', $true)
$chemicalType = $assembly.GetType('RaidRescue.ChemicalFertilizerPatchService', $true)
$cannonType = $assembly.GetType('RaidRescue.DualFluidCannonPatchService', $true)
$coordinatorType = $assembly.GetType('RaidRescue.DualFluidCannonPatchCoordinator', $true)
$commandsType = $assembly.GetType('RaidRescue.DeveloperCommandsPatchService', $true)
$noclipAssetsType = $assembly.GetType('RaidRescue.NoclipAssetSupport', $true)
$revivalType = $assembly.GetType('RaidRescue.RevivalBuffPatchService', $true)
$carryType = $assembly.GetType('RaidRescue.CarrySprintPatchService', $true)
$enginesType = $assembly.GetType('RaidRescue.BetterEnginesPatchService', $true)
$plasmaType = $assembly.GetType('RaidRescue.BetterPlasmaDrillsPatchService', $true)

$liveGame = Invoke-Static $gameService 'FindGameInstall'
Assert-True (-not [String]::IsNullOrWhiteSpace($liveGame)) 'Scrap Mechanic install was not found.'

$fixtureRoot = Join-Path $PSScriptRoot (
    '.adaptive-fixture-' + [Guid]::NewGuid().ToString('N'))
$fakeGame = Join-Path $fixtureRoot 'steamapps\common\Scrap Mechanic'
$backupRoot = Join-Path $fixtureRoot 'backups'
$receiptRoot = Join-Path $fixtureRoot 'receipts'

try {
    [IO.Directory]::CreateDirectory((Join-Path $fakeGame 'Release')) | Out-Null
    Copy-Item -LiteralPath (Join-Path $liveGame 'Release\ScrapMechanic.exe') `
        -Destination (Join-Path $fakeGame 'Release\ScrapMechanic.exe')

    $manifestPath = Join-Path $fixtureRoot 'steamapps\appmanifest_387990.acf'
    $updated = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Utf8NoBom $manifestPath (
        '"AppState"' + "`n{" +
        "`n`t" + '"appid"' + "`t`t" + '"387990"' +
        "`n`t" + '"buildid"' + "`t`t" + '"99999999"' +
        "`n`t" + '"LastUpdated"' + "`t`t" + '"' + $updated + '"' +
        "`n}`n")

    $supportType.GetField('PatchStateRootOverride', $binding).SetValue(
        $null, $receiptRoot)

    $resourceRelative = Get-StaticField $resourceType 'HarvestCoreRelativePath'
    $resourceOriginal = Get-StaticField $resourceType 'OriginalDeclaration'
    $resourceV1 = Get-StaticField $resourceType 'LocatorV1Declaration'
    $resourceV2 = Get-StaticField $resourceType 'LocatorV2Declaration'
    Copy-CleanFixture $resourceRelative {
        param($text, $source)
        if ($text.Contains($resourceV2)) {
            return $text.Replace($resourceV2, $resourceOriginal)
        }
        if ($text.Contains($resourceV1)) {
            return $text.Replace($resourceV1, $resourceOriginal)
        }
        return $text
    }
    $resourceFixturePath = Join-Path $fakeGame $resourceRelative
    $resourceCrLf = (Normalize-Lua ([IO.File]::ReadAllText(
        $resourceFixturePath))).Replace("`n", "`r`n")
    [IO.File]::WriteAllText(
        $resourceFixturePath, $resourceCrLf,
        [Text.UTF8Encoding]::new($true))

    $commandsRelative = Get-StaticField $commandsType 'SurvivalGameRelativePath'
    $noclipToolsRelative = Get-StaticField $noclipAssetsType 'ToolsRelativePath'
    $noclipModuleRelative = Get-StaticField $noclipAssetsType 'ModuleRelativePath'
    $noclipInputRelative = Get-StaticField $noclipAssetsType 'InputToolRelativePath'
    $noclipToolsPath = Join-Path $fakeGame $noclipToolsRelative
    $noclipCleanTail = Get-StaticField $noclipAssetsType 'CleanTail'
    $noclipInstalledTail = Get-StaticField $noclipAssetsType 'InstalledTail'
    [IO.Directory]::CreateDirectory(
        (Split-Path -Parent $noclipToolsPath)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $liveGame $noclipToolsRelative) `
        -Destination $noclipToolsPath
    $noclipToolsFixtureText = Normalize-Lua ([IO.File]::ReadAllText(
        $noclipToolsPath))
    if ($noclipToolsFixtureText.Contains($noclipInstalledTail)) {
        $noclipToolsFixtureText = $noclipToolsFixtureText.Replace(
            $noclipInstalledTail, $noclipCleanTail)
        Write-Utf8NoBom $noclipToolsPath $noclipToolsFixtureText
    }
    $noclipToolsBaseline = [IO.File]::ReadAllBytes($noclipToolsPath)
    $originalGate = Get-StaticField $commandsType 'OriginalGate'
    $hostGate = Get-StaticField $commandsType 'HostOnlyGate'
    $everyoneGate = Get-StaticField $commandsType 'EveryoneGate'
    $originalClientData = Get-StaticField $commandsType 'OriginalClientData'
    $everyoneClientData = Get-StaticField $commandsType 'EveryoneClientData'
    $noclipRuntime = Get-StaticField $commandsType 'NoclipRuntime'
    $noclipMarker = Get-StaticField $commandsType 'NoclipRuntimeMarker'
    $noclipModuleStream = $assembly.GetManifestResourceStream(
        'RaidRescue.ScrapLabNoclip.lua')
    $noclipModuleReader = [IO.StreamReader]::new(
        $noclipModuleStream, [Text.UTF8Encoding]::new($false, $true))
    $noclipModuleText = $noclipModuleReader.ReadToEnd()
    $noclipModuleReader.Dispose()
    $knownCommandsOriginalHash = Get-StaticField $commandsType 'SurvivalGameOriginal'
    $knownCommandsHostHash = Get-StaticField $commandsType 'SurvivalGameHostCommandsWithNoclip'
    $knownCommandsEveryoneHash = Get-StaticField $commandsType 'SurvivalGameEveryoneCommandsWithNoclip'
    Copy-CleanFixture $commandsRelative {
        param($text, $source)
        $text = Invoke-Static $commandsType 'RemoveNoclipRuntime' $text
        if ($text.Contains($hostGate)) {
            return $text.Replace($hostGate, $originalGate)
        }
        if ($text.Contains($everyoneGate)) {
            return $text.Replace(
                $everyoneGate, $originalGate).Replace(
                $everyoneClientData, $originalClientData)
        }
        return $text
    }

    $revivalRelative = Get-StaticField $revivalType 'SurvivalPlayerRelativePath'
    $revivalMarker = Get-StaticField $revivalType 'PatchMarker'
    Copy-CleanFixture $revivalRelative {
        param($text, $source)
        if ($text.Contains($revivalMarker)) {
            return Invoke-Static $revivalType 'UnpatchText' $text
        }
        return $text
    }
    $revivalFixturePath = Join-Path $fakeGame $revivalRelative

    $carryRelative = Get-StaticField $carryType 'CarryToolRelativePath'
    $liftRelative = Get-StaticField $carryType 'SurvivalLiftRelativePath'
    $carryMarker = Get-StaticField $carryType 'CarryPatchMarker'
    $liftMarker = Get-StaticField $carryType 'LiftPatchMarker'
    Copy-CleanFixture $carryRelative {
        param($text, $source)
        if ($text.Contains($carryMarker)) {
            return Invoke-Static $carryType 'UnpatchCarryText' $text
        }
        return $text
    }
    Copy-CleanFixture $liftRelative {
        param($text, $source)
        if ($text.Contains($liftMarker)) {
            return Invoke-Static $carryType 'UnpatchLiftText' $text
        }
        return $text
    }
    $carryFixturePath = Join-Path $fakeGame $carryRelative
    $liftFixturePath = Join-Path $fakeGame $liftRelative

    $electricRelative = Get-StaticField $enginesType 'ElectricEngineRelativePath'
    $gasRelative = Get-StaticField $enginesType 'GasEngineRelativePath'
    $electricMarker = Get-StaticField $enginesType 'ElectricMarker'
    $gasMarker = Get-StaticField $enginesType 'GasMarker'
    Copy-CleanFixture $electricRelative {
        param($text, $source)
        if ($text.Contains($electricMarker)) {
            return Invoke-Static $enginesType 'UnpatchElectricText' $text
        }
        return $text
    }
    Copy-CleanFixture $gasRelative {
        param($text, $source)
        if ($text.Contains($gasMarker)) {
            return Invoke-Static $enginesType 'UnpatchGasText' $text
        }
        return $text
    }
    $electricFixturePath = Join-Path $fakeGame $electricRelative
    $gasFixturePath = Join-Path $fakeGame $gasRelative

    $plasmaDefinition = Invoke-Static $plasmaType 'GetDefinition'
    $plasmaTargets = $plasmaDefinition.GetType().GetField(
        'Files', $binding).GetValue($plasmaDefinition)
    $plasmaRelatives = @()
    foreach ($target in $plasmaTargets) {
        $targetType = $target.GetType()
        $relative = [string]$targetType.GetField(
            'RelativePath', $binding).GetValue($target)
        $marker = [string]$targetType.GetField(
            'Marker', $binding).GetValue($target)
        $unpatch = $targetType.GetField(
            'Unpatch', $binding).GetValue($target)
        $isCarryTarget = $relative -eq $carryRelative
        $plasmaRelatives += $relative
        Copy-CleanFixture $relative {
            param($text, $source)
            $clean = $text
            if ($clean.Contains($marker)) {
                $clean = $unpatch.DynamicInvoke($clean)
            }
            if ($isCarryTarget -and $clean.Contains($carryMarker)) {
                $clean = Invoke-Static $carryType 'UnpatchCarryText' $clean
            }
            return $clean
        }
    }

    $chemicalTargets = Invoke-Static $chemicalType 'GetTargets'
    foreach ($target in $chemicalTargets) {
        $targetType = $target.GetType()
        $relative = $targetType.GetField('RelativePath', $binding).GetValue($target)
        $unpatch = $targetType.GetField('Unpatch', $binding).GetValue($target)
        $variants = $targetType.GetField('Variants', $binding).GetValue($target)
        Copy-CleanFixture $relative {
            param($text, $source)
            $hash = Get-Sha256 $source
            foreach ($variant in $variants) {
                $variantType = $variant.GetType()
                $patchedHash = $variantType.GetField(
                    'PatchedHash', $binding).GetValue($variant)
                if ($hash -eq $patchedHash) {
                    return $unpatch.DynamicInvoke($text)
                }
            }
            return $text
        }
    }

    $cannonRelative = Get-StaticField $cannonType 'MountedWaterGunRelativePath'
    $cannonPatchedHash = Get-StaticField $cannonType 'MountedWaterGunPatched'
    $cannonUnpatch = $cannonType.GetMethod('Unpatch', $binding)
    Copy-CleanFixture $cannonRelative {
        param($text, $source)
        if ((Get-Sha256 $source) -eq $cannonPatchedHash) {
            return $cannonUnpatch.Invoke($null, @($text))
        }
        return $text
    }

    $baseline = @{}
    Get-ChildItem -LiteralPath $fakeGame -Recurse -Filter '*.lua' | ForEach-Object {
        $relative = $_.FullName.Substring($fakeGame.Length + 1)
        $baseline[$relative] = [IO.File]::ReadAllBytes($_.FullName)
    }
    foreach ($relative in $plasmaRelatives) {
        if (-not $baseline.ContainsKey($relative)) {
            $baseline[$relative] = [IO.File]::ReadAllBytes(
                (Join-Path $fakeGame $relative))
        }
    }

    $resourceInstall = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $resourceInstall.Success (
        'Resource Locator adaptive install failed: ' + $resourceInstall.Error)
    Assert-True $resourceInstall.Adaptive 'Resource Locator did not report adaptive mode.'
    Invoke-Static $supportType 'CommitBuildActivations' `
        $resourceInstall $fakeGame
    $firstBuild = Invoke-Static $supportType 'GetSteamBuild' `
        $fakeGame $resourceInstall.GameVersion
    Assert-True (-not (Invoke-Static $supportType 'RequiresBuildRefresh' `
        'ResourceLocator' $firstBuild)) `
        'A freshly activated mod was incorrectly marked stale.'

    $nextManifest = [IO.File]::ReadAllText($manifestPath).Replace(
        '"99999999"', '"99999998"')
    Write-Utf8NoBom $manifestPath $nextManifest
    $nextBuild = Invoke-Static $supportType 'GetSteamBuild' `
        $fakeGame $resourceInstall.GameVersion
    Assert-True (Invoke-Static $supportType 'RequiresBuildRefresh' `
        'ResourceLocator' $nextBuild) `
        'A Steam build change did not mark the installed mod inactive.'
    $beforeRefreshHash = Get-Sha256 $resourceFixturePath
    $resourceRefresh = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $resourceRefresh.Success `
        'A cache-only mod reactivation failed.'
    Assert-True ($resourceRefresh.FilesPatched -eq 1) `
        'Cache-only reactivation did not request bundle invalidation.'
    Assert-True ((Get-Sha256 $resourceFixturePath) -eq $beforeRefreshHash) `
        'Cache-only reactivation rewrote unchanged Lua.'
    Invoke-Static $supportType 'CommitBuildActivations' `
        $resourceRefresh $fakeGame
    Assert-True (-not (Invoke-Static $supportType 'RequiresBuildRefresh' `
        'ResourceLocator' $nextBuild)) `
        'The refreshed build activation was not recorded.'
    Write-Utf8NoBom $manifestPath (
        $nextManifest.Replace('"99999998"', '"99999999"'))

    $resourceRemove = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $resourceRemove.Success 'Resource Locator adaptive removal failed.'
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$resourceRelative],
            [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $resourceRelative)))) `
        'Resource Locator exact adaptive restore failed.'

    $resourceSurgicalInstall = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $resourceSurgicalInstall.Success `
        'Resource Locator surgical-removal setup failed.'
    [IO.File]::AppendAllText(
        (Join-Path $fakeGame $resourceRelative),
        "-- POST-INSTALL UNRELATED EDIT`r`n",
        [Text.UTF8Encoding]::new($false))
    $resourceSurgicalRemove = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $resourceSurgicalRemove.Success `
        'Resource Locator surgical adaptive removal failed.'
    $resourceAfterSurgery = [IO.File]::ReadAllText(
        (Join-Path $fakeGame $resourceRelative))
    Assert-True (
        $resourceAfterSurgery.Contains('-- POST-INSTALL UNRELATED EDIT')) `
        'Surgical removal discarded an unrelated later edit.'
    Assert-True (-not $resourceAfterSurgery.Contains(
        'one inactive output slot makes the locator dot visible')) `
        'Surgical removal left the Resource Locator patch installed.'
    [IO.File]::WriteAllBytes(
        (Join-Path $fakeGame $resourceRelative),
        [byte[]]$baseline[$resourceRelative])

    $resourceTamperInstall = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $resourceTamperInstall.Success `
        'Resource Locator tamper-removal setup failed.'
    $tamperedResource = [IO.File]::ReadAllText(
        (Join-Path $fakeGame $resourceRelative)).Replace(
        'HarvestCore.maxChildCount = 1',
        'HarvestCore.maxChildCount = 2')
    [IO.File]::WriteAllText(
        (Join-Path $fakeGame $resourceRelative),
        $tamperedResource,
        [Text.UTF8Encoding]::new($true))
    $tamperedHash = Get-Sha256 (Join-Path $fakeGame $resourceRelative)
    $tamperedRemove = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True (-not $tamperedRemove.Success) `
        'Removal accepted an edited Raid Rescue snippet.'
    Assert-True (
        (Get-Sha256 (Join-Path $fakeGame $resourceRelative)) -eq
        $tamperedHash) `
        'Rejected tampered removal still wrote the target file.'
    [IO.File]::WriteAllBytes(
        (Join-Path $fakeGame $resourceRelative),
        [byte[]]$baseline[$resourceRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'ResourceLocator'

    $commandsHost = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'host'
    Assert-True $commandsHost.Success 'Developer Commands host install failed.'
    $commandsHostText = Normalize-Lua ([IO.File]::ReadAllText(
        (Join-Path $fakeGame $commandsRelative)))
    Assert-True ($commandsHostText.Contains($noclipRuntime)) `
        'Developer Commands host install omitted the /noclip runtime.'
    Assert-True (
        [regex]::Matches(
            $commandsHostText,
            [regex]::Escape($noclipMarker)).Count -eq 2) `
        'Developer Commands host install duplicated or truncated the /noclip marker pair.'
    Assert-True ($noclipModuleText.Contains(
        'sv_scrapLabNoclipInput')) `
        'Developer Commands host install omitted synchronized noclip movement input.'
    Assert-True ($noclipModuleText.Contains(
        'getRelativeMoveDirection()')) `
        'Developer Commands noclip does not use the engine movement-input vector.'
    Assert-True (-not $noclipModuleText.Contains(
        'character:setClimbing( true )')) `
        'Developer Commands noclip still enables the spring-like climbing controller.'
    Assert-True ($noclipModuleText.Contains(
        'character:setTumbling( false )')) `
        'Developer Commands noclip does not prevent ragdoll state.'
    Assert-True ($noclipModuleText.Contains(
        'sm.physics.applyImpulse')) `
        'Developer Commands noclip does not use smooth free-space flight.'
    Assert-True ($noclipModuleText.Contains(
        'function BasePlayer.server_onFixedUpdate')) `
        'Developer Commands noclip physics is not hosted by the world-bound player script.'
    $gameClassUpdate = [regex]::Match(
        $noclipModuleText,
        '(?s)function SurvivalGame\.sv_scrapLabUpdateNoclip.*?local ScrapLabOriginalServerFixedUpdate').Value
    Assert-True (-not $gameClassUpdate.Contains('sm.physics.applyImpulse')) `
        'Developer Commands still calls world-dependent physics from SurvivalGame.'
    Assert-True ($noclipModuleText.Contains(
        'height * ScrapLabNoclipSweepScale')) `
        'Developer Commands noclip movement sweep can still catch ordinary floor contact.'
    Assert-True ($noclipModuleText.Contains(
        'ScrapLabNoclipTargetResponse')) `
        'Developer Commands noclip does not smooth target acceleration.'
    Assert-True ($noclipModuleText.Contains(
        'ScrapLabNoclipMaximumDeltaVelocity')) `
        'Developer Commands noclip does not cap per-tick physics correction.'
    Assert-True (-not $noclipModuleText.Contains(
        'sm.localPlayer.getMouseDelta()')) `
        'Developer Commands noclip still contains custom inverted mouse math.'
    Assert-True (-not $noclipModuleText.Contains(
        'ScrapLabNoclipProbeHeight')) `
        'Developer Commands noclip still uses the airborne input probe.'
    Assert-True (-not $noclipModuleText.Contains(
        'sm.camera.setDirection')) `
        'Developer Commands noclip still overrides the normal mouse camera.'
    Assert-True (-not $noclipModuleText.Contains(
        'sm.localPlayer.setLockedControls')) `
        'Developer Commands noclip still locks the normal player controls.'
    Assert-True (Test-Path -LiteralPath (
        Join-Path $fakeGame $noclipModuleRelative)) `
        'Developer Commands did not install Scripts/ScrapLab/Noclip.lua.'
    Assert-True (Test-Path -LiteralPath (
        Join-Path $fakeGame $noclipInputRelative)) `
        'Developer Commands did not install the isolated input ToolClass.'
    Assert-True ([IO.File]::ReadAllText(
        (Join-Path $fakeGame $noclipInputRelative)).Contains(
            'isSprinting()')) `
        'Developer Commands input tool does not report the Shift sprint state.'
    Assert-True ([IO.File]::ReadAllText($noclipToolsPath).Contains(
        'ScrapLabNoclipInputTool')) `
        'Developer Commands did not register the hidden ScrapLab input tool.'
    $toolJsonWithoutComments = ([IO.File]::ReadAllLines(
        $noclipToolsPath) | Where-Object {
            -not $_.TrimStart().StartsWith('//')
        }) -join "`n"
    try {
        $null = $toolJsonWithoutComments | ConvertFrom-Json
    }
    catch {
        throw 'Developer Commands produced invalid tools.json: ' +
            $_.Exception.Message
    }
    $noclipInstalledEntry = Get-StaticField $noclipAssetsType 'InstalledEntry'
    Assert-True ((Normalize-Lua ([IO.File]::ReadAllText(
        $noclipToolsPath))).Contains($noclipInstalledEntry)) `
        'Developer Commands input-tool registration does not match its protected descriptor.'
    $commandsEveryone = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'everyone'
    Assert-True $commandsEveryone.Success (
        'Developer Commands mode switch failed: ' + $commandsEveryone.Error)
    $commandsEveryoneText = Normalize-Lua ([IO.File]::ReadAllText(
        (Join-Path $fakeGame $commandsRelative)))
    Assert-True ($commandsEveryoneText.Contains($noclipRuntime)) `
        'Developer Commands mode switch removed the /noclip runtime.'
    Assert-True ($noclipModuleText.Contains('/fly')) `
        'Developer Commands installed runtime does not bind /fly.'
    Assert-True (-not $noclipModuleText.Contains('"/noclip"')) `
        'Developer Commands still exposes the old /noclip command.'
    Assert-True ($noclipModuleText.Contains(
        'ScrapLabNoclipSprintSpeed = 36')) `
        'Developer Commands does not provide the faster Shift flight speed.'
    $commandsRemove = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $false 'host'
    Assert-True $commandsRemove.Success 'Developer Commands adaptive removal failed.'
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$commandsRelative],
            [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $commandsRelative)))) `
        'Developer Commands exact adaptive restore failed.'
    Assert-True ([Linq.Enumerable]::SequenceEqual(
        [byte[]]$noclipToolsBaseline,
        [byte[]][IO.File]::ReadAllBytes($noclipToolsPath))) `
        'Developer Commands removal did not restore tools.json exactly.'
    Assert-True (-not (Test-Path -LiteralPath (
        Join-Path $fakeGame $noclipModuleRelative))) `
        'Developer Commands removal left Noclip.lua installed.'
    Assert-True (-not (Test-Path -LiteralPath (
        Join-Path $fakeGame $noclipInputRelative))) `
        'Developer Commands removal left NoclipInputTool.lua installed.'

    $liveLegacyModule = Join-Path $liveGame $noclipModuleRelative
    $liveLegacyInput = Join-Path $liveGame $noclipInputRelative
    if (Test-Path -LiteralPath $liveLegacyModule) {
        $legacyModuleHash = Get-Sha256 $liveLegacyModule
        $knownLegacyModuleHashes = @(
            (Get-StaticField $noclipAssetsType 'LegacyV4ModuleHash'),
            (Get-StaticField $noclipAssetsType 'LegacyV5ModuleHash'),
            (Get-StaticField $noclipAssetsType 'LegacyV6ModuleHash')
        )
        if ($knownLegacyModuleHashes -contains $legacyModuleHash) {
            $legacyAssetInstall = Invoke-Static $commandsType 'SetEnabledAt' `
                $fakeGame $backupRoot $true 'host'
            Assert-True $legacyAssetInstall.Success `
                'Developer Commands legacy-module setup failed.'
            Copy-Item -LiteralPath $liveLegacyModule `
                -Destination (Join-Path $fakeGame $noclipModuleRelative) -Force
            if (Test-Path -LiteralPath $liveLegacyInput) {
                Copy-Item -LiteralPath $liveLegacyInput `
                    -Destination (Join-Path $fakeGame $noclipInputRelative) -Force
            }
            $legacyAssetUpgrade = Invoke-Static $commandsType 'SetEnabledAt' `
                $fakeGame $backupRoot $true 'host'
            Assert-True $legacyAssetUpgrade.Success `
                'Developer Commands did not accept the verified legacy module upgrade.'
            Assert-True ([IO.File]::ReadAllText(
                (Join-Path $fakeGame $noclipModuleRelative)).Contains(
                    'NOCLIP MODULE v7')) `
                'Developer Commands did not replace the legacy module with v7.'
            Assert-True ([IO.File]::ReadAllText(
                (Join-Path $fakeGame $noclipInputRelative)).Contains(
                    'isSprinting()')) `
                'Developer Commands did not upgrade the legacy input tool.'
            $legacyAssetRemove = Invoke-Static $commandsType 'SetEnabledAt' `
                $fakeGame $backupRoot $false 'host'
            Assert-True $legacyAssetRemove.Success `
                'Developer Commands v7 migration cleanup failed.'
        }
    }

    $assetTamperInstall = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'host'
    Assert-True $assetTamperInstall.Success `
        'Developer Commands asset-tamper setup failed.'
    $assetTamperModulePath = Join-Path $fakeGame $noclipModuleRelative
    $verifiedModuleBytes = [IO.File]::ReadAllBytes($assetTamperModulePath)
    [IO.File]::AppendAllText(
        $assetTamperModulePath,
        "`n-- third-party edit`n",
        [Text.UTF8Encoding]::new($false))
    $assetTamperMainHash = Get-Sha256 (
        Join-Path $fakeGame $commandsRelative)
    $assetTamperToolsHash = Get-Sha256 $noclipToolsPath
    $assetTamperModuleHash = Get-Sha256 $assetTamperModulePath
    $assetTamperRemove = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $false 'host'
    Assert-True (-not $assetTamperRemove.Success) `
        'Developer Commands removal accepted an edited ScrapLab noclip module.'
    Assert-True ((Get-Sha256 (
        Join-Path $fakeGame $commandsRelative)) -eq $assetTamperMainHash) `
        'Rejected noclip-module removal still wrote SurvivalGame.lua.'
    Assert-True ((Get-Sha256 $noclipToolsPath) -eq $assetTamperToolsHash) `
        'Rejected noclip-module removal still wrote tools.json.'
    Assert-True ((Get-Sha256 $assetTamperModulePath) -eq $assetTamperModuleHash) `
        'Rejected noclip-module removal still wrote the edited module.'
    [IO.File]::WriteAllBytes($assetTamperModulePath, $verifiedModuleBytes)
    $assetTamperCleanup = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $false 'host'
    Assert-True $assetTamperCleanup.Success `
        'Developer Commands asset-tamper cleanup failed.'

    $commandsTamperInstall = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'host'
    Assert-True $commandsTamperInstall.Success `
        'Developer Commands noclip-tamper setup failed.'
    $commandsFixturePath = Join-Path $fakeGame $commandsRelative
    $tamperedCommands = [IO.File]::ReadAllText($commandsFixturePath).Replace(
        '$SURVIVAL_DATA/Scripts/ScrapLab/Noclip.lua',
        '$SURVIVAL_DATA/Scripts/ScrapLab/Noclip-edited.lua')
    [IO.File]::WriteAllText(
        $commandsFixturePath,
        $tamperedCommands,
        [Text.UTF8Encoding]::new($false))
    $tamperedCommandsHash = Get-Sha256 $commandsFixturePath
    $commandsTamperRemove = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $false 'host'
    Assert-True (-not $commandsTamperRemove.Success) `
        'Developer Commands removal accepted an edited /noclip runtime.'
    Assert-True ((Get-Sha256 $commandsFixturePath) -eq $tamperedCommandsHash) `
        'Rejected /noclip runtime removal still wrote SurvivalGame.lua.'
    [IO.File]::WriteAllBytes(
        $commandsFixturePath,
        [byte[]]$baseline[$commandsRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'DeveloperCommands'

    $knownCommandsText = Normalize-Lua ([IO.File]::ReadAllText(
        (Join-Path $liveGame $commandsRelative)))
    $knownCommandsText = Invoke-Static $commandsType `
        'RemoveNoclipRuntime' $knownCommandsText
    $knownCommandsText = $knownCommandsText.Replace(
        $hostGate, $originalGate).Replace(
        $everyoneGate, $originalGate).Replace(
        $everyoneClientData, $originalClientData)
    Write-Utf8NoBom $commandsFixturePath $knownCommandsText
    Assert-True ((Get-Sha256 $commandsFixturePath) -eq $knownCommandsOriginalHash) `
        'Known-file Developer Commands fixture is not the verified original.'
    $knownHostInstall = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'host'
    Assert-True $knownHostInstall.Success `
        'Known-file Developer Commands host install failed.'
    Assert-True ((Get-Sha256 $commandsFixturePath) -eq $knownCommandsHostHash) `
        'Known-file Developer Commands host output hash is wrong.'
    $knownEveryoneInstall = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'everyone'
    Assert-True $knownEveryoneInstall.Success `
        'Known-file Developer Commands Every Player switch failed.'
    Assert-True ((Get-Sha256 $commandsFixturePath) -eq $knownCommandsEveryoneHash) `
        'Known-file Developer Commands Every Player output hash is wrong.'
    $knownCommandsRemove = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $false 'host'
    Assert-True $knownCommandsRemove.Success `
        'Known-file Developer Commands removal failed.'
    Assert-True ((Get-Sha256 $commandsFixturePath) -eq $knownCommandsOriginalHash) `
        'Known-file Developer Commands removal did not restore the verified original.'
    [IO.File]::WriteAllBytes(
        $commandsFixturePath,
        [byte[]]$baseline[$commandsRelative])

    $legacyCommandsText = Normalize-Lua ([IO.File]::ReadAllText(
        (Join-Path $liveGame $commandsRelative)))
    $legacyMarker = if ($legacyCommandsText.Contains(
        'SCRAPLAB DEVELOPER COMMANDS NOCLIP v3')) {
        'SCRAPLAB DEVELOPER COMMANDS NOCLIP v3'
    }
    elseif ($legacyCommandsText.Contains(
        'SCRAPLAB DEVELOPER COMMANDS NOCLIP v2')) {
        'SCRAPLAB DEVELOPER COMMANDS NOCLIP v2'
    }
    elseif ($legacyCommandsText.Contains(
        'SCRAPLAB DEVELOPER COMMANDS NOCLIP v1')) {
        'SCRAPLAB DEVELOPER COMMANDS NOCLIP v1'
    }
    else { $null }
    if ($null -ne $legacyMarker) {
        Write-Utf8NoBom $commandsFixturePath $legacyCommandsText
        $legacyCommandsUpgrade = Invoke-Static $commandsType 'SetEnabledAt' `
            $fakeGame $backupRoot $true 'host'
        Assert-True $legacyCommandsUpgrade.Success `
            'Legacy Developer Commands noclip upgrade failed.'
        $legacyUpgradedText = Normalize-Lua ([IO.File]::ReadAllText(
            $commandsFixturePath))
        Assert-True ($legacyUpgradedText.Contains($noclipRuntime)) `
            'Legacy Developer Commands upgrade did not install noclip v4.'
        Assert-True (-not $legacyUpgradedText.Contains(
            $legacyMarker)) `
            'Legacy Developer Commands upgrade left the old noclip runtime installed.'
        Assert-True ((Get-Sha256 $commandsFixturePath) -eq $knownCommandsHostHash) `
            'Legacy Developer Commands upgrade produced the wrong host hash.'
        $legacyCommandsRemove = Invoke-Static $commandsType 'SetEnabledAt' `
            $fakeGame $backupRoot $false 'host'
        Assert-True $legacyCommandsRemove.Success `
            'Upgraded legacy Developer Commands removal failed.'
        Assert-True ((Get-Sha256 $commandsFixturePath) -eq $knownCommandsOriginalHash) `
            'Upgraded legacy Developer Commands removal did not restore the original.'
    }
    else {
        Write-Output 'Legacy noclip live fixture unavailable; migration fixture skipped.'
    }
    [IO.File]::WriteAllBytes(
        $commandsFixturePath,
        [byte[]]$baseline[$commandsRelative])

    $revivalInstall = Invoke-Static $revivalType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $revivalInstall.Success (
        'Revival Buff Recovery adaptive install failed: ' + $revivalInstall.Error)
    Assert-True $revivalInstall.Adaptive `
        'Revival Buff Recovery did not report adaptive mode.'
    $revivalText = Normalize-Lua ([IO.File]::ReadAllText($revivalFixturePath))
    Assert-True $revivalText.Contains($revivalMarker) `
        'Revival Buff Recovery marker was not installed.'
    Assert-True (
        [regex]::Matches(
            $revivalText,
            [regex]::Escape('self:sv_raidRescueCaptureRevivalPerks()')).Count -eq 3) `
        'Every real knockout transition was not patched exactly once.'
    Assert-True $revivalText.Contains(
        'local raidRescueUsedBaguette = self.sv.saved.hasRevivalItem and not params.skipRevivalItem') `
        'Real Revival Baguette detection was not installed.'
    Assert-True $revivalText.Contains(
        'self:sv_raidRescueRestoreRevivalPerks()') `
        'The baguette revival callback does not restore captured buffs.'
    Assert-True $revivalText.Contains(
        'self.sv.saved.raidRescueRevivalPerks = nil') `
        'Normal respawn and forced-revival snapshot clearing is missing.'
    Assert-True $revivalText.Contains(
        'for _, perk in pairs( SurvivalPlayer.Perks ) do') `
        'Snapshots are not constrained to known food perks.'

    $revivalRemove = Invoke-Static $revivalType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $revivalRemove.Success (
        'Revival Buff Recovery adaptive removal failed: ' + $revivalRemove.Error)
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$revivalRelative],
            [byte[]][IO.File]::ReadAllBytes($revivalFixturePath))) `
        'Revival Buff Recovery exact restore failed.'

    $revivalSurgicalInstall = Invoke-Static $revivalType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $revivalSurgicalInstall.Success `
        'Revival Buff Recovery surgical-removal setup failed.'
    [IO.File]::AppendAllText(
        $revivalFixturePath,
        "-- POST-INSTALL UNRELATED REVIVAL EDIT`n",
        [Text.UTF8Encoding]::new($false))
    $revivalSurgicalRemove = Invoke-Static $revivalType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $revivalSurgicalRemove.Success (
        'Revival Buff Recovery surgical removal failed: ' +
        $revivalSurgicalRemove.Error)
    $revivalAfterSurgery = [IO.File]::ReadAllText($revivalFixturePath)
    Assert-True $revivalAfterSurgery.Contains(
        '-- POST-INSTALL UNRELATED REVIVAL EDIT') `
        'Revival Buff Recovery removal discarded an unrelated later edit.'
    Assert-True (-not $revivalAfterSurgery.Contains($revivalMarker)) `
        'Revival Buff Recovery removal left its patch marker behind.'
    [IO.File]::WriteAllBytes(
        $revivalFixturePath,
        [byte[]]$baseline[$revivalRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'RevivalBuffRecovery'

    $revivalTamperInstall = Invoke-Static $revivalType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $revivalTamperInstall.Success `
        'Revival Buff Recovery tamper-test setup failed.'
    $revivalTamperedText = [IO.File]::ReadAllText($revivalFixturePath).Replace(
        'local restoredPerks = {}',
        'local restoredPerks = { false }')
    Write-Utf8NoBom $revivalFixturePath $revivalTamperedText
    $revivalTamperedHash = Get-Sha256 $revivalFixturePath
    $revivalTamperedRemove = Invoke-Static $revivalType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True (-not $revivalTamperedRemove.Success) `
        'Revival Buff Recovery removal accepted an edited protected helper.'
    Assert-True ((Get-Sha256 $revivalFixturePath) -eq $revivalTamperedHash) `
        'Rejected Revival Buff Recovery removal still wrote the game script.'
    [IO.File]::WriteAllBytes(
        $revivalFixturePath,
        [byte[]]$baseline[$revivalRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'RevivalBuffRecovery'

    $carryInstall = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $carryInstall.Success (
        'Full-Speed Carrying adaptive install failed: ' + $carryInstall.Error)
    Assert-True ($carryInstall.FilesPatched -eq 2) `
        'Full-Speed Carrying did not patch both carrying scripts.'
    $carryText = [IO.File]::ReadAllText($carryFixturePath)
    $liftText = [IO.File]::ReadAllText($liftFixturePath)
    Assert-True $carryText.Contains($carryMarker) `
        'CarryTool patch marker was not installed.'
    Assert-True $carryText.Contains('local sprintPrefix = prefix') `
        'Each carry type is not using its own native sprint animation prefix.'
    Assert-True $carryText.Contains(
        'sprintLeft = sprintPrefix == "bucket" and "bucket_sprint_left" or sprintMovement') `
        'Native third-person carry sprint animations were not wired.'
    Assert-True $carryText.Contains(
        'swapFpAnimation( self.cl.fpAnimations, "sprintExit", "sprintInto", 0.0 )') `
        'Native first-person carry sprint transitions were not wired.'
    Assert-True $carryText.Contains(
        'self.tool:setBlockSprint( false )') `
        'Hand-carry sprint was not unblocked.'
    Assert-True $liftText.Contains($liftMarker) `
        'Survival Lift patch marker was not installed.'
    Assert-True $liftText.Contains(
        "`t`tself.tool:setBlockSprint( false )") `
        'Lift-carry sprint was not unblocked.'

    $carryRemove = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $carryRemove.Success (
        'Full-Speed Carrying exact removal failed: ' + $carryRemove.Error)
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$carryRelative],
            [byte[]][IO.File]::ReadAllBytes($carryFixturePath))) `
        'CarryTool exact restore failed.'
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$liftRelative],
            [byte[]][IO.File]::ReadAllBytes($liftFixturePath))) `
        'SurvivalLift exact restore failed.'

    $carrySurgicalInstall = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $carrySurgicalInstall.Success `
        'Full-Speed Carrying surgical-removal setup failed.'
    [IO.File]::AppendAllText(
        $carryFixturePath,
        "-- POST-INSTALL UNRELATED CARRY EDIT`n",
        [Text.UTF8Encoding]::new($false))
    $carrySurgicalRemove = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $carrySurgicalRemove.Success (
        'Full-Speed Carrying surgical removal failed: ' +
        $carrySurgicalRemove.Error)
    $carryAfterSurgery = [IO.File]::ReadAllText($carryFixturePath)
    Assert-True $carryAfterSurgery.Contains(
        '-- POST-INSTALL UNRELATED CARRY EDIT') `
        'Full-Speed Carrying removal discarded an unrelated later edit.'
    Assert-True (-not $carryAfterSurgery.Contains($carryMarker)) `
        'Full-Speed Carrying removal left its CarryTool marker behind.'
    [IO.File]::WriteAllBytes(
        $carryFixturePath, [byte[]]$baseline[$carryRelative])
    [IO.File]::WriteAllBytes(
        $liftFixturePath, [byte[]]$baseline[$liftRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'FullSpeedCarrying'

    $carryTamperInstall = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $carryTamperInstall.Success `
        'Full-Speed Carrying tamper-test setup failed.'
    $carryTamperedText = [IO.File]::ReadAllText($carryFixturePath).Replace(
        'local sprinting = self.tool:isSprinting()',
        'local sprinting = false')
    Write-Utf8NoBom $carryFixturePath $carryTamperedText
    $carryTamperedHash = Get-Sha256 $carryFixturePath
    $liftBeforeRejectedRemove = Get-Sha256 $liftFixturePath
    $carryTamperedRemove = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True (-not $carryTamperedRemove.Success) `
        'Full-Speed Carrying removal accepted edited protected animation code.'
    Assert-True ((Get-Sha256 $carryFixturePath) -eq $carryTamperedHash) `
        'Rejected Full-Speed Carrying removal still wrote CarryTool.lua.'
    Assert-True ((Get-Sha256 $liftFixturePath) -eq $liftBeforeRejectedRemove) `
        'Rejected Full-Speed Carrying removal still wrote SurvivalLift.lua.'
    [IO.File]::WriteAllBytes(
        $carryFixturePath, [byte[]]$baseline[$carryRelative])
    [IO.File]::WriteAllBytes(
        $liftFixturePath, [byte[]]$baseline[$liftRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'FullSpeedCarrying'

    $carryProtectedEdit = [IO.File]::ReadAllText($carryFixturePath).Replace(
        'self.tool:setBlockSprint( true )',
        'self.tool:setBlockSprint(true)')
    Write-Utf8NoBom $carryFixturePath $carryProtectedEdit
    $carryProtectedHash = Get-Sha256 $carryFixturePath
    $liftProtectedHash = Get-Sha256 $liftFixturePath
    $carryBlockedInstall = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True (-not $carryBlockedInstall.Success) `
        'Full-Speed Carrying accepted a changed protected sprint block.'
    Assert-True ((Get-Sha256 $carryFixturePath) -eq $carryProtectedHash) `
        'Rejected Full-Speed Carrying install still wrote CarryTool.lua.'
    Assert-True ((Get-Sha256 $liftFixturePath) -eq $liftProtectedHash) `
        'Rejected Full-Speed Carrying install still wrote SurvivalLift.lua.'
    [IO.File]::WriteAllBytes(
        $carryFixturePath, [byte[]]$baseline[$carryRelative])

    $enginesInstall = Invoke-Static $enginesType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $enginesInstall.Success (
        'Better Engines adaptive install failed: ' + $enginesInstall.Error)
    Assert-True ($enginesInstall.FilesPatched -eq 2) `
        'Better Engines did not patch both engine scripts.'
    $electricText = [IO.File]::ReadAllText($electricFixturePath)
    $gasText = [IO.File]::ReadAllText($gasFixturePath)
    Assert-True $electricText.Contains($electricMarker) `
        'Better Engines electric marker was not installed.'
    Assert-True $gasText.Contains($gasMarker) `
        'Better Engines gas marker was not installed.'
    Assert-True (([regex]::Matches(
        $electricText, 'power = 10000, velocity =')).Count -eq 13) `
        'Better Engines did not set all 13 Electric Engine gears to 10,000 power.'
    Assert-True (([regex]::Matches(
        $electricText, 'pointsPerBattery = 40250')).Count -eq 2) `
        'Better Engines did not update both level-5 Electric Engine records.'
    Assert-True (([regex]::Matches(
        $gasText, 'pointsPerFuel = 40250')).Count -eq 2) `
        'Better Engines did not update both level-5 Gas Engine records.'
    Assert-True $gasText.Contains('pointsPerFuel = 13500') `
        'Better Engines unexpectedly changed lower-level Gas Engine efficiency.'

    $enginesRemove = Invoke-Static $enginesType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $enginesRemove.Success (
        'Better Engines exact removal failed: ' + $enginesRemove.Error)
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$electricRelative],
            [byte[]][IO.File]::ReadAllBytes($electricFixturePath))) `
        'Better Engines exact ElectricEngine restore failed.'
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$gasRelative],
            [byte[]][IO.File]::ReadAllBytes($gasFixturePath))) `
        'Better Engines exact GasEngine restore failed.'

    $enginesSurgicalInstall = Invoke-Static $enginesType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $enginesSurgicalInstall.Success `
        'Better Engines surgical-removal setup failed.'
    [IO.File]::AppendAllText(
        $gasFixturePath,
        "-- POST-INSTALL UNRELATED GAS ENGINE EDIT`n",
        [Text.UTF8Encoding]::new($false))
    $enginesSurgicalRemove = Invoke-Static $enginesType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $enginesSurgicalRemove.Success (
        'Better Engines surgical removal failed: ' +
        $enginesSurgicalRemove.Error)
    Assert-True ([IO.File]::ReadAllText($gasFixturePath).Contains(
        '-- POST-INSTALL UNRELATED GAS ENGINE EDIT')) `
        'Better Engines removal discarded an unrelated later edit.'
    [IO.File]::WriteAllBytes(
        $electricFixturePath, [byte[]]$baseline[$electricRelative])
    [IO.File]::WriteAllBytes(
        $gasFixturePath, [byte[]]$baseline[$gasRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'BetterEngines'

    $plasmaInstall = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $plasmaInstall.Success (
        'Better Plasma Drills install failed: ' + $plasmaInstall.Error)
    Assert-True ($plasmaInstall.FilesPatched -eq 17) `
        'Better Plasma Drills did not patch all 17 protected files.'
    $plasmaScript = [IO.File]::ReadAllText((Join-Path $fakeGame `
        'Survival\Scripts\game\interactables\PlasmaDrill.lua'))
    Assert-True $plasmaScript.Contains('pointsPerBattery = 12000') `
        'Plasma Drill level 5 battery capacity is missing.'
    Assert-True $plasmaScript.Contains('radius = 10') `
        'Plasma Drill radius 10 setting is missing.'
    Assert-True $plasmaScript.Contains('voxelDrillIntervalTicks = 2') `
        'Plasma Drill level 5 voxel interval is missing.'

    $plasmaRemove = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $plasmaRemove.Success (
        'Better Plasma Drills removal failed: ' + $plasmaRemove.Error)
    foreach ($relative in $plasmaRelatives) {
        Assert-True ([Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$relative],
            [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $relative)))) `
            "Better Plasma Drills exact restore failed for $relative."
    }

    $plasmaFirst = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $plasmaFirst.Success 'Plasma-first shared CarryTool install failed.'
    $carryAfterPlasma = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $carryAfterPlasma.Success (
        'Full-Speed Carrying rejected intact Plasma Drill registrations: ' +
        $carryAfterPlasma.Error)
    $carryRemoveAfterPlasma = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $carryRemoveAfterPlasma.Success `
        'Full-Speed Carrying removal damaged Plasma Drill composition.'
    Assert-True ([IO.File]::ReadAllText($carryFixturePath).Contains(
        'obj_interactive_plasmadrill_lvl4')) `
        'Full-Speed Carrying removal discarded Plasma Drill registrations.'
    $plasmaFinalRemove = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $plasmaFinalRemove.Success `
        'Plasma Drill removal after shared-file composition failed.'

    $carryFirst = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $carryFirst.Success 'Carry-first shared CarryTool install failed.'
    $plasmaAfterCarry = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $plasmaAfterCarry.Success (
        'Better Plasma Drills rejected intact Full-Speed Carrying code: ' +
        $plasmaAfterCarry.Error)
    $plasmaRemoveAfterCarry = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $plasmaRemoveAfterCarry.Success `
        'Plasma Drill removal damaged Full-Speed Carrying composition.'
    Assert-True ([IO.File]::ReadAllText($carryFixturePath).Contains($carryMarker)) `
        'Plasma Drill removal discarded Full-Speed Carrying code.'
    $carryFinalRemove = Invoke-Static $carryType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $carryFinalRemove.Success `
        'Final Full-Speed Carrying removal failed.'

    $plasmaTamperInstall = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $plasmaTamperInstall.Success `
        'Better Plasma Drills tamper-test setup failed.'
    $plasmaScriptPath = Join-Path $fakeGame `
        'Survival\Scripts\game\interactables\PlasmaDrill.lua'
    $tamperedPlasma = [IO.File]::ReadAllText($plasmaScriptPath).Replace(
        'pointsPerBattery = 12000', 'pointsPerBattery = 11999')
    Write-Utf8NoBom $plasmaScriptPath $tamperedPlasma
    $tamperedPlasmaHash = Get-Sha256 $plasmaScriptPath
    $shapePath = Join-Path $fakeGame `
        'Survival\Objects\Database\ShapeSets\powertools.shapeset'
    $shapeBeforeRejectedRemove = Get-Sha256 $shapePath
    $plasmaTamperedRemove = Invoke-Static $plasmaType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True (-not $plasmaTamperedRemove.Success) `
        'Better Plasma Drills removal accepted edited protected level data.'
    Assert-True ((Get-Sha256 $plasmaScriptPath) -eq $tamperedPlasmaHash) `
        'Rejected Plasma Drill removal still wrote PlasmaDrill.lua.'
    Assert-True ((Get-Sha256 $shapePath) -eq $shapeBeforeRejectedRemove) `
        'Rejected Plasma Drill removal still wrote the shape set.'
    foreach ($relative in $plasmaRelatives) {
        [IO.File]::WriteAllBytes((Join-Path $fakeGame $relative),
            [byte[]]$baseline[$relative])
    }
    Invoke-Static $supportType 'DeleteReceipt' 'BetterPlasmaDrills'

    $enginesTamperInstall = Invoke-Static $enginesType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $enginesTamperInstall.Success `
        'Better Engines tamper-test setup failed.'
    $electricTamperedText = [IO.File]::ReadAllText(
        $electricFixturePath).Replace(
            'power = 10000, velocity = math.rad( 0 )',
            'power = 9999, velocity = math.rad( 0 )')
    Write-Utf8NoBom $electricFixturePath $electricTamperedText
    $electricTamperedHash = Get-Sha256 $electricFixturePath
    $gasBeforeRejectedRemove = Get-Sha256 $gasFixturePath
    $enginesTamperedRemove = Invoke-Static $enginesType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True (-not $enginesTamperedRemove.Success) `
        'Better Engines removal accepted an edited protected gear table.'
    Assert-True ((Get-Sha256 $electricFixturePath) -eq $electricTamperedHash) `
        'Rejected Better Engines removal still wrote ElectricEngine.lua.'
    Assert-True ((Get-Sha256 $gasFixturePath) -eq $gasBeforeRejectedRemove) `
        'Rejected Better Engines removal still wrote GasEngine.lua.'
    [IO.File]::WriteAllBytes(
        $electricFixturePath, [byte[]]$baseline[$electricRelative])
    [IO.File]::WriteAllBytes(
        $gasFixturePath, [byte[]]$baseline[$gasRelative])
    Invoke-Static $supportType 'DeleteReceipt' 'BetterEngines'

    $chemicalInstall = Invoke-Static $chemicalType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $chemicalInstall.Success 'Chemical Fertilizer adaptive install failed.'
    $chemicalRemove = Invoke-Static $chemicalType 'SetEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $chemicalRemove.Success (
        'Chemical Fertilizer adaptive removal failed: ' + $chemicalRemove.Error)
    foreach ($target in $chemicalTargets) {
        $targetType = $target.GetType()
        $relative = $targetType.GetField('RelativePath', $binding).GetValue($target)
        Assert-True (
            [Linq.Enumerable]::SequenceEqual(
                [byte[]]$baseline[$relative],
                [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $relative)))) `
            "Chemical Fertilizer exact restore failed for $relative."
    }

    $linkedInstall = Invoke-Static $coordinatorType 'SetCannonEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $linkedInstall.Success 'Linked fertilizer and cannon install failed.'
    Assert-True ($linkedInstall.FilesPatched -eq 5) `
        'Linked install did not report all five changed files.'
    $cannonRemove = Invoke-Static $coordinatorType 'SetCannonEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $cannonRemove.Success 'Cannon adaptive removal failed.'
    $dependencyRemove = Invoke-Static $coordinatorType 'SetChemicalEnabledAt' `
        $fakeGame $backupRoot $false
    Assert-True $dependencyRemove.Success 'Fertilizer dependency removal failed.'

    foreach ($relative in $baseline.Keys) {
        Assert-True (
            [Linq.Enumerable]::SequenceEqual(
                [byte[]]$baseline[$relative],
                [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $relative)))) `
            "Final byte-exact restore failed for $relative."
    }

    $resourcePath = Join-Path $fakeGame $resourceRelative
    $cleanResource = [IO.File]::ReadAllBytes($resourcePath)
    $changedText = [IO.File]::ReadAllText($resourcePath).Replace(
        'HarvestCore = class( nil )',
        'HarvestCore = class(nil)')
    Write-Utf8NoBom $resourcePath $changedText
    $protectedChange = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True (-not $protectedChange.Success) `
        'A protected formatting change was incorrectly accepted.'
    Assert-True (
        [IO.File]::ReadAllText($resourcePath).Contains(
            'HarvestCore = class(nil)')) `
        'Rejected protected-code test unexpectedly wrote the file.'
    [IO.File]::WriteAllBytes($resourcePath, $cleanResource)

    [IO.File]::AppendAllText(
        $resourcePath, "-- MIXED NEWLINE TEST`n",
        [Text.UTF8Encoding]::new($false))
    $mixedHash = Get-Sha256 $resourcePath
    $mixedNewline = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True (-not $mixedNewline.Success) `
        'Adaptive patching accepted mixed newline styles.'
    Assert-True ((Get-Sha256 $resourcePath) -eq $mixedHash) `
        'Rejected mixed-newline test wrote the file.'
    [IO.File]::WriteAllBytes($resourcePath, $cleanResource)

    $knownManifest = [IO.File]::ReadAllText($manifestPath).Replace(
        '"99999999"', '"24417028"')
    Write-Utf8NoBom $manifestPath $knownManifest
    $sameBuildText = [IO.File]::ReadAllText($resourcePath) +
        '-- SAME BUILD MANUAL EDIT TEST' + "`n"
    Write-Utf8NoBom $resourcePath $sameBuildText
    $sameBuild = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True (-not $sameBuild.Success) `
        'An unknown hash on the known Steam build was incorrectly accepted.'

    [IO.File]::WriteAllBytes($resourcePath, $cleanResource)
    $futureManifest = $knownManifest.Replace(
        '"24417028"', '"99999999"')
    Write-Utf8NoBom $manifestPath $futureManifest
    Remove-Item -LiteralPath $manifestPath
    $missingManifestHash = Get-Sha256 $resourcePath
    $missingManifest = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True (-not $missingManifest.Success) `
        'Adaptive patching accepted missing Steam manifest data.'
    Assert-True ((Get-Sha256 $resourcePath) -eq $missingManifestHash) `
        'Missing-manifest rejection still wrote the target file.'

    Write-Host 'Adaptive patch regression tests passed.'
}
finally {
    $supportType.GetField('PatchStateRootOverride', $binding).SetValue(
        $null, $null)
    $resolvedTests = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot).TrimEnd('\')
    if ($resolvedFixture.StartsWith(
        $resolvedTests + '\',
        [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Directory]::Exists($resolvedFixture)) {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
