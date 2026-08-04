param(
    [string]$RaidRescueExe,
    [Parameter(Mandatory = $true)]
    [string]$SourceSave
)

$ErrorActionPreference = 'Stop'

if ([String]::IsNullOrWhiteSpace($RaidRescueExe)) {
    $RaidRescueExe = Join-Path $PSScriptRoot '..\dist\ScrapLab.exe'
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

$resolvedExe = [IO.Path]::GetFullPath($RaidRescueExe)
$resolvedSave = [IO.Path]::GetFullPath($SourceSave)
$fixtureRoot = Join-Path $PSScriptRoot (
    'dropped-item-fixture-' + [Guid]::NewGuid().ToString('N'))
$fixtureSave = Join-Path $fixtureRoot 'DroppedItemFixture.db'
$expiredFixtureSave = Join-Path $fixtureRoot 'ExpiredDroppedItemFixture.db'
$sourceHash = (Get-FileHash -LiteralPath $resolvedSave -Algorithm SHA256).Hash

try {
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    Copy-Item -LiteralPath $resolvedSave -Destination $fixtureSave
    Copy-Item -LiteralPath $resolvedSave -Destination $expiredFixtureSave

    $assembly = [Reflection.Assembly]::LoadFrom($resolvedExe)
    $service = $assembly.GetType('RaidRescue.RaidService', $true)
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static'
    $analyze = $service.GetMethod('Analyze', $flags)
    $analyzeRaidsOnly = $service.GetMethod('AnalyzeRaidsOnly', $flags)
    $clear = $service.GetMethod('ClearDroppedItems', $flags)
    $clearExpired = $service.GetMethod('ClearExpiredDroppedItems', $flags)

    $raidOnly = $analyzeRaidsOnly.Invoke(
        $null, [object[]]@([string]$fixtureSave))
    Assert-True $raidOnly.Success `
        ("Raid-only analysis failed: " + $raidOnly.Error)
    Assert-True (-not $raidOnly.DroppedItemsScanned) `
        'Raid-only analysis incorrectly marked loose items as scanned.'
    Assert-True ($raidOnly.DroppedItemCount -eq 0) `
        'Raid-only analysis decoded loose items before the optional scan.'

    $before = $analyze.Invoke($null, [object[]]@([string]$fixtureSave))
    Assert-True $before.Success ("Initial analysis failed: " + $before.Error)
    foreach ($raid in $before.Raids) {
        Assert-True ($raid.WorldName -eq 'Overworld') `
            'The save world descriptor did not resolve raid world 1 to Overworld.'
    }
    Assert-True $before.DroppedItemsScanned `
        'The explicit loose-item scan did not report completion.'
    Assert-True ($before.DroppedItemCount -ge 4) `
        'The source fixture did not contain enough decoded loose-item stacks.'
    Assert-True ($before.DroppedItemQuantity -ge $before.DroppedItemCount) `
        'The fixture loose-item quantity was not decoded correctly.'
    foreach ($droppedItem in $before.DroppedItems) {
        Assert-True ($droppedItem.WorldName -eq 'Overworld') `
            'The save world descriptor did not resolve world 1 to Overworld.'
    }

    $expectedNames = @(
        'Big Wheel',
        'Broccoli',
        'Crude Oil',
        'Warehouse Key'
    )
    foreach ($name in $expectedNames) {
        $item = @($before.DroppedItems | Where-Object { $_.Name -eq $name })
        Assert-True ($item.Count -eq 1) ("Expected one decoded " + $name + '.')
        Assert-True (-not [String]::IsNullOrEmpty(
            $before.DroppedItemIcons[$item[0].Uuid])) `
            ($name + ' did not receive its Scrap Mechanic icon.')
    }

    for ($index = 1; $index -lt $before.DroppedItems.Count; $index++) {
        Assert-True (
            $before.DroppedItems[$index - 1].ValueScore -ge
                $before.DroppedItems[$index].ValueScore) `
            'Dropped pickups were not ordered from highest to lowest value.'
    }

    $catalogType = $assembly.GetType('RaidRescue.ItemCatalog', $true)
    $findCatalogItem = $catalogType.GetMethod('Find', $flags)
    $componentKit = $findCatalogItem.Invoke(
        $null,
        [object[]]@(
            [string]'5530e6a0-4748-4926-b134-50ca9ecb9dcf'))
    $broccoliDrop = @(
        $before.DroppedItems |
            Where-Object { $_.Name -eq 'Broccoli' } |
            Select-Object -First 1
    )[0]
    $broccoliCatalog = $findCatalogItem.Invoke(
        $null, [object[]]@([string]$broccoliDrop.Uuid))
    Assert-True (
        $componentKit.RecoveryValue -gt $broccoliCatalog.RecoveryValue) `
        'Component Kits did not rank above an ordinary crop.'
    Assert-True ($componentKit.RecoveryTier -eq 'CRITICAL VALUE') `
        'Component Kits did not receive the expected critical-value label.'

    $databaseType = $assembly.GetType('RaidRescue.SqliteDatabase', $true)
    $openReadWrite = $databaseType.GetMethod('OpenReadWrite', $flags)
    $expiredDatabase = $openReadWrite.Invoke(
        $null,
        [object[]]@([string]$expiredFixtureSave, [bool]$false))
    try {
        $expiredDatabase.Execute('UPDATE Game SET gametick = 173500')
    }
    finally {
        $expiredDatabase.Dispose()
    }

    $expiredBefore = $analyze.Invoke(
        $null, [object[]]@([string]$expiredFixtureSave))
    Assert-True $expiredBefore.Success `
        ("Expired fixture analysis failed: " + $expiredBefore.Error)
    $expiredItems = @($expiredBefore.DroppedItems | Where-Object { $_.Expired })
    $activeItems = @($expiredBefore.DroppedItems | Where-Object { -not $_.Expired })
    $expiredQuantity = [long](($expiredItems | Measure-Object -Property Quantity -Sum).Sum)
    Assert-True ($expiredItems.Count -gt 0) `
        'The mixed fixture did not identify an expired loose stack.'
    Assert-True ($activeItems.Count -gt 0) `
        'The mixed fixture did not retain an active loose stack.'
    Assert-True ($expiredBefore.ExpiredDroppedItemCount -eq $expiredItems.Count) `
        'The aggregate expired count does not match the decoded item records.'
    Assert-True $expiredBefore.CanClearExpiredDroppedItems `
        'Expired-only cleanup was not enabled for the mixed fixture.'

    $expiredResult = $clearExpired.Invoke(
        $null, [object[]]@([string]$expiredFixtureSave))
    Assert-True $expiredResult.Success `
        ("Expired-only removal failed: " + $expiredResult.Error)
    Assert-True ($expiredResult.ItemsRemoved -eq $expiredItems.Count) `
        'Expired-only removal reported the wrong stack count.'
    Assert-True ($expiredResult.QuantityRemoved -eq $expiredQuantity) `
        'Expired-only removal reported the wrong quantity.'
    Assert-True ($expiredResult.After.DroppedItemCount -eq $activeItems.Count) `
        'Expired-only removal changed an active loose pickup.'
    Assert-True ($expiredResult.After.ExpiredDroppedItemCount -eq 0) `
        'Expired-only removal left a pending-cleanup pickup behind.'
    foreach ($activeItem in $activeItems) {
        Assert-True (@(
            $expiredResult.After.DroppedItems |
                Where-Object { $_.EntityId -eq $activeItem.EntityId }
        ).Count -eq 1) `
            ($activeItem.Name + ' should remain after expired-only cleanup.')
    }
    Assert-True ($expiredResult.After.RaidCount -eq $before.RaidCount) `
        'Expired-only removal changed the decoded raid count.'
    Assert-True ($expiredResult.DatabaseStatus -eq 'ok') `
        'The expired-only fixture failed its final integrity check.'

    $bigWheel = @(
        $before.DroppedItems |
            Where-Object { $_.Name -eq 'Big Wheel' } |
            Select-Object -First 1
    )[0]
    $single = $clear.Invoke(
        $null,
        [object[]]@([string]$fixtureSave, [long]$bigWheel.EntityId))
    Assert-True $single.Success ("Single removal failed: " + $single.Error)
    Assert-True ($single.ItemsRemoved -eq 1) `
        'Single removal did not remove exactly one stack.'
    Assert-True ($single.QuantityRemoved -eq 1) `
        'Single removal reported the wrong item quantity.'
    Assert-True ($single.After.DroppedItemCount -eq ($before.DroppedItemCount - 1)) `
        'Single removal changed more than the selected loose stack.'
    Assert-True ($single.After.RaidCount -eq $before.RaidCount) `
        'Single removal changed the decoded raid count.'
    Assert-True (Test-Path -LiteralPath $single.BackupPath) `
        'Single removal did not leave its verified backup.'

    $all = $clear.Invoke(
        $null,
        [object[]]@([string]$fixtureSave, [long]0))
    Assert-True $all.Success ("Clear-all failed: " + $all.Error)
    Assert-True ($all.ItemsRemoved -eq ($before.DroppedItemCount - 1)) `
        'Clear-all did not remove every remaining loose stack.'
    Assert-True ($all.QuantityRemoved -eq ($before.DroppedItemQuantity - $bigWheel.Quantity)) `
        'Clear-all reported the wrong remaining quantity.'
    Assert-True ($all.After.DroppedItemCount -eq 0) `
        'Clear-all left decoded loose items in the fixture.'
    Assert-True ($all.After.RaidCount -eq $before.RaidCount) `
        'Clear-all changed the decoded raid count.'
    Assert-True ($all.DatabaseStatus -eq 'ok') `
        'The edited fixture failed its final SQLite integrity check.'

    $finalSourceHash =
        (Get-FileHash -LiteralPath $resolvedSave -Algorithm SHA256).Hash
    Assert-True ($finalSourceHash -eq $sourceHash) `
        'The source save changed during isolated regression testing.'

    Write-Host (
        'Dropped-item regression passed: opt-in scan, icons, value ordering, ' +
        'Component Kit priority, expired-only cleanup, individual removal, ' +
        'clear-all, backups, raid preservation, and final integrity.')
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
