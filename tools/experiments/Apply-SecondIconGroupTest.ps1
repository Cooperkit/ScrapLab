param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Scrap Mechanic',
    [string]$TestIcon = (Join-Path $PSScriptRoot '..\..\source\Patching\Parts\RaidDetector\RaidDetectorIcon.png')
)

$ErrorActionPreference = 'Stop'
$soilBagUuid = '9a3e478c-2224-44fa-887c-239965bd05ad'
$guiPath = Join-Path $GamePath 'Survival\Gui'
$xmlPath = Join-Path $guiPath 'IconMapSurvival.xml'
$backupPath = Join-Path $guiPath 'IconMapSurvival.scraplab-group-test-backup.xml'
$testTexturePath = Join-Path $guiPath 'ScrapLabIconGroupTest.png'
$cachePath = Join-Path $GamePath 'Cache\Bundle\core_data.cbo'
$cacheBackupPath = $cachePath + '.scraplab-icon-group-test-original'

if (Get-Process -Name ScrapMechanic,ScrapMechanicServer -ErrorAction SilentlyContinue) {
    throw 'Close Scrap Mechanic completely before applying the icon-group experiment.'
}
if (-not (Test-Path -LiteralPath $xmlPath -PathType Leaf)) {
    throw "IconMapSurvival.xml was not found: $xmlPath"
}
if (-not (Test-Path -LiteralPath $TestIcon -PathType Leaf)) {
    throw "The 96x96 test icon was not found: $TestIcon"
}

$currentBytes = [IO.File]::ReadAllBytes($xmlPath)
if (Test-Path -LiteralPath $backupPath) {
    $backupBytes = [IO.File]::ReadAllBytes($backupPath)
    if (-not [Linq.Enumerable]::SequenceEqual(
        [byte[]]$currentBytes, [byte[]]$backupBytes)) {
        throw 'The dedicated experiment backup exists but does not match the current XML.'
    }
}
else {
    [IO.File]::WriteAllBytes($backupPath, $currentBytes)
}

$utf8 = [Text.UTF8Encoding]::new($false, $true)
$text = $utf8.GetString($currentBytes)
$hasBom = $currentBytes.Length -ge 3 -and
    $currentBytes[0] -eq 0xEF -and $currentBytes[1] -eq 0xBB -and
    $currentBytes[2] -eq 0xBF
if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
    $text = $text.Substring(1)
}
$newline = "`n"
if ($text.Contains("`r`n")) { $newline = "`r`n" }
if ([regex]::Matches($text, '<Group name="ItemIcons"').Count -ne 1) {
    throw 'The source XML does not contain exactly one ItemIcons group.'
}

$escapedUuid = [regex]::Escape($soilBagUuid)
$pattern = '(?m)^[ \t]*<Index name="' + $escapedUuid + '">\r?\n' +
    '[ \t]*<Frame point="1344 1728"/>\r?\n' +
    '[ \t]*</Index>\r?\n'
$matches = [regex]::Matches($text, $pattern)
if ($matches.Count -ne 1) {
    throw 'The protected Soil Bag icon entry was not found exactly once.'
}
$withoutSoilBag = [regex]::Replace($text, $pattern, '', 1)
$resourceEnd = '    </Resource>'
if (($withoutSoilBag.Split(@($resourceEnd), [StringSplitOptions]::None).Count - 1) -ne 1) {
    throw 'The ResourceImageSet closing tag was not found exactly once.'
}
$secondGroup =
    '        <!-- SCRAPLAB TEMPORARY SECOND ICON GROUP TEST -->' + $newline +
    '        <Group name="ItemIcons" texture="ScrapLabIconGroupTest.png" size="96 96">' + $newline +
    '            <Index name="' + $soilBagUuid + '">' + $newline +
    '                <Frame point="0 0"/>' + $newline +
    '            </Index>' + $newline +
    '        </Group>' + $newline
$output = $withoutSoilBag.Replace($resourceEnd, $secondGroup + $resourceEnd)

if ([regex]::Matches($output, '<Group name="ItemIcons"').Count -ne 2 -or
    [regex]::Matches($output, [regex]::Escape($soilBagUuid)).Count -ne 1) {
    throw 'The generated experiment XML failed its group or UUID checks.'
}
$xmlDocument = [xml]$output
if ($null -eq $xmlDocument.MyGUI.Resource.Group) {
    throw 'The generated experiment XML did not parse as a ResourceImageSet.'
}

Copy-Item -LiteralPath $TestIcon -Destination $testTexturePath -Force
$temporary = $xmlPath + '.scraplab-group-test.tmp'
$replaceBackup = $xmlPath + '.scraplab-group-test-swap-backup.tmp'
try {
    if (Test-Path -LiteralPath $replaceBackup) {
        Remove-Item -LiteralPath $replaceBackup -Force
    }
    [IO.File]::WriteAllText($temporary, $output,
        [Text.UTF8Encoding]::new($hasBom))
    [IO.File]::Replace($temporary, $xmlPath, $replaceBackup, $true)
    Remove-Item -LiteralPath $replaceBackup -Force
}
finally {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }
}

if (Test-Path -LiteralPath $cacheBackupPath) {
    throw 'A previous icon-group cache backup still exists; restore that experiment first.'
}
if (Test-Path -LiteralPath $cachePath) {
    Move-Item -LiteralPath $cachePath -Destination $cacheBackupPath
}

Write-Host 'Second ItemIcons group experiment applied.'
Write-Host 'Test item: Soil Bag (UUID 9a3e478c-2224-44fa-887c-239965bd05ad).'
Write-Host 'Expected icon: the Raid Detector concept icon.'
