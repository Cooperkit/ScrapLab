param(
    [string]$RaidRescueExe = (Join-Path $PSScriptRoot '..\dist\RaidRescue.exe')
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
    $originalGate = Get-StaticField $commandsType 'OriginalGate'
    $hostGate = Get-StaticField $commandsType 'HostOnlyGate'
    $everyoneGate = Get-StaticField $commandsType 'EveryoneGate'
    $originalClientData = Get-StaticField $commandsType 'OriginalClientData'
    $everyoneClientData = Get-StaticField $commandsType 'EveryoneClientData'
    Copy-CleanFixture $commandsRelative {
        param($text, $source)
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

    $resourceInstall = Invoke-Static $resourceType 'SetEnabledAt' `
        $fakeGame $backupRoot $true
    Assert-True $resourceInstall.Success (
        'Resource Locator adaptive install failed: ' + $resourceInstall.Error)
    Assert-True $resourceInstall.Adaptive 'Resource Locator did not report adaptive mode.'
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
    $commandsEveryone = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $true 'everyone'
    Assert-True $commandsEveryone.Success 'Developer Commands mode switch failed.'
    $commandsRemove = Invoke-Static $commandsType 'SetEnabledAt' `
        $fakeGame $backupRoot $false 'host'
    Assert-True $commandsRemove.Success 'Developer Commands adaptive removal failed.'
    Assert-True (
        [Linq.Enumerable]::SequenceEqual(
            [byte[]]$baseline[$commandsRelative],
            [byte[]][IO.File]::ReadAllBytes((Join-Path $fakeGame $commandsRelative)))) `
        'Developer Commands exact adaptive restore failed.'

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
