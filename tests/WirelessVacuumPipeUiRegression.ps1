$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$layoutPath = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.layout'
$luaPath = Join-Path $root 'source\Patching\Parts\WirelessVacuumPipe\WirelessVacuumPipe.lua'
$servicePath = Join-Path $root 'source\Patching\WirelessVacuumPipePatchService.cs'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    Assert-True ($Text.IndexOf($Needle, [StringComparison]::Ordinal) -ge 0) $Message
}

[xml]$layout = Get-Content -LiteralPath $layoutPath -Raw
$layoutText = Get-Content -LiteralPath $layoutPath -Raw
$lua = Get-Content -LiteralPath $luaPath -Raw
$service = Get-Content -LiteralPath $servicePath -Raw

$widgets = @{}
foreach ($widget in $layout.SelectNodes('//Widget[@name]')) {
    $name = [string]$widget.name
    Assert-True (-not $widgets.ContainsKey($name)) "Duplicate GUI widget name: $name"
    $widgets[$name] = $widget
}

foreach ($name in @(
    'ContentPanel','MachineBackground','Title','SystemLabel','ModeValue',
    'ModeLink','ModeSend','ModeReceive','StatusLamp','StatusValue',
    'EndpointValue','ChannelSwatch','ChannelValue','WorldValue','RemoteWorlds',
    'ScopeLabel','ScopeButton','LinkScopeHint','Explanation','Hint'
)) {
    Assert-True $widgets.ContainsKey($name) "Required GUI widget is missing: $name"
}

Assert-True ([string]$widgets.ContentPanel.position_real -eq '0.323437 0.127778 0.35625 0.638889') 'The panel no longer uses the centered native Sensor footprint.'
Assert-True ([string]$widgets.MachineBackground.skin -eq 'BackgroundSensor') 'The panel is not using the native machine background.'
foreach ($name in @('ModeLink','ModeSend','ModeReceive','ScopeButton')) {
    Assert-True ([string]$widgets[$name].skin -eq 'SettingsButton') "$name is not using the native selected-control skin."
    $caption = $widgets[$name].Property | Where-Object { $_.key -eq 'Caption' }
    Assert-True ($null -ne $caption) "$name must use a direct caption instead of a fragile nested label."
}

Assert-True ($layoutText.IndexOf('key="WordWrap"', [StringComparison]::Ordinal) -lt 0) 'Unsupported WordWrap returned to the layout.'
Assert-Contains $lua 'setVisible( "LinkScopeHint", not directional )' 'Link mode does not replace the directional scope area.'
Assert-Contains $lua 'setText( "ScopeButton",' 'The scope caption is not bound directly to its native button.'
Assert-Contains $lua 'setColor( "StatusLamp", statusColor )' 'The live status lamp is not updated.'
Assert-Contains $lua 'if #worlds <= 2 then return table.concat( worlds, "  |  " ) end' 'Short matching-world lists are not formatted cleanly.'
Assert-Contains $lua 'tostring( #worlds - 2 ) .. " MORE"' 'Long matching-world lists are not bounded for the panel.'
foreach ($state in @('LINKED','CROSS-WORLD LINKED','SENDING','READY TO RECEIVE','UNPAIRED','CHANNEL EMPTY','DISABLED BY LOGIC','REMOTE CELL LOAD LIMIT','WIRELESS MANAGER UNAVAILABLE')) {
    Assert-Contains $lua ('["' + $state + '"]') "Status color is missing for: $state"
}

Assert-Contains $service 'private const string DefinitionVersion = "8";' 'Wireless Vacuum Pipe was not advanced to definition 8.'
Assert-Contains $service '338FAB44E130D36A51D90EC5EC8079DA472C67A4C51900E92B36C3727FD67BED' 'The v2.6.0 endpoint UI script is not recognized for an in-place update.'
Assert-Contains $service 'F5D5ADCC354E1CCA7001E68B17507B0657B84AAF80AEF05C4B159C551439A48B' 'The v2.6.0 layout is not recognized for an in-place UI update.'

Write-Output 'Wireless Vacuum Pipe UI regression passed.'
