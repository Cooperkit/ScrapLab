dofile( "$SURVIVAL_DATA/Scripts/util.lua" )
dofile( "$GAME_DATA/Scripts/gui/GridScrollView.lua" )

dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/Storage/NetworkInventoryIndex.lua" )
local NetworkInventoryIndex = g_scrapLabNetworkInventoryIndex
assert( NetworkInventoryIndex, "[ScrapLab Storage Phase 1] shared network inventory index failed to initialize" )

NetworkStorageChest = class( nil )
NetworkStorageChest.maxParentCount = 0
NetworkStorageChest.maxChildCount = 0
NetworkStorageChest.connectionInput = sm.interactable.connectionType.none
NetworkStorageChest.connectionOutput = sm.interactable.connectionType.none
NetworkStorageChest.colorNormal = sm.color.new( 0x777777ff )
NetworkStorageChest.colorHighlight = sm.color.new( 0xeeeeeeff )

local PART_UUID = "bc7576a7-f226-459a-883c-e8460e955d63"
local EXPECTED_BUFFER_SIZE = 3
local LEGACY_BUFFER_SIZE = 5
local PHASE1_PREFIX = "[ScrapLab Storage Phase 1] "
local GUI_PATH = "$SURVIVAL_DATA/Gui/JsonGuis/ScrapLab/Parts/NetworkStorageChest.gui"
local ITEM_PATH = "$SURVIVAL_DATA/Gui/JsonGuis/ScrapLab/Parts/NetworkStorageChestItem.gui"
local LOCALIZATION_PATH = "$SURVIVAL_DATA/Scripts/ScrapLab/Parts/NetworkStorageChest/NetworkStorageChest.localization.json"

local GUI_TEMPLATE = dofile( GUI_PATH )
local ITEM_TEMPLATE = dofile( ITEM_PATH )
local LOCALIZATION = sm.json.open( LOCALIZATION_PATH )

local SORT_MODES = { "TYPE", "NAME", "COUNT", "STACKS" }
local ITEM_FILTERS = { "ALL", "BLOCK", "INTERACTIVE", "PART", "TOOL", "CONSUMABLE" }
local ITEM_TYPE_ORDER = {
	block = 1,
	interactive = 2,
	part = 3,
	tool = 4,
	consumable = 5,
	other = 6
}
local SCAN_BUDGET_PER_TICK = 12
local REVISION_POLL_INTERVAL_TICKS = 4
local REVISION_POLL_BUDGET = 32
-- Machine-driven inventories may change every fixed tick. Keep those changes
-- authoritative internally, but never rebuild every open terminal more than a
-- twice per second.
local CATALOG_PUBLISH_INTERVAL_TICKS = 20
local TOPOLOGY_POLL_INTERVAL_TICKS = 40
local VIEW_DISTANCE = 16
local MAX_LOCAL_PIPE_SHAPES = 4096
local WITHDRAW_RATE_LIMIT_TICKS = 6
local WITHDRAW_RETRY_LIMIT = 4
local WITHDRAW_RETRY_WINDOW_TICKS = 20
local WITHDRAW_RETRY_DELAYS = { 1, 2, 4, 4 }
local INVENTORY_DEPOSIT_RATE_LIMIT_TICKS = 3
local ROUTING_MODE_RATE_LIMIT_TICKS = 10
local WITHDRAW_ACTIONS = { TAKE_ONE = true, TAKE_STACK = true, TAKE_ALL = true }
local DEPOSIT_RETRY_TICKS = 8
local NIL_UUID_STRING = "00000000-0000-0000-0000-000000000000"

local function playerSlotIsEmpty( item )
	return not item or not item.uuid or tostring( item.uuid ) == NIL_UUID_STRING or ( tonumber( item.quantity ) or 0 ) <= 0
end

local SPECIALIZED_DEPOSIT_UUIDS = {
	["056e5ff1-f030-40df-946a-b830bf494c92"] = true, -- gas
	["da4833fd-f981-4e08-a9f7-48e630a7c146"] = true, -- battery
	["ea10d1af-b97a-46fb-8895-dfd1becb53bb"] = true, -- water
	["38ec258d-c644-4f08-8635-3f7434c884dd"] = true, -- seed
	["76331bbf-abbd-4b8d-bb54-f721a5b6193b"] = true, -- fertilizer
	["096d4daf-639e-4947-a1a6-1890eaa94464"] = true, -- ammunition
	["be29592a-ef58-4b1d-b18c-895023abd27f"] = true  -- chemical
}

local BLOCKED_DEPOSIT_UUIDS = {
	["5cb15c93-4fa9-48da-9974-2e95ca6c9e1c"] = true -- refinery output is machine-owned
}

-- Mirrors the vanilla ContainerUuids registry. The terminal is deliberately
-- absent so its deposit buffer can never enter its own catalog.
local LOCAL_STORAGE_UUIDS = {
	["056e5ff1-f030-40df-946a-b830bf494c92"] = true, -- Gas Container
	["da4833fd-f981-4e08-a9f7-48e630a7c146"] = true, -- Battery Container
	["ea10d1af-b97a-46fb-8895-dfd1becb53bb"] = true, -- Water Container
	["38ec258d-c644-4f08-8635-3f7434c884dd"] = true, -- Seed Container
	["76331bbf-abbd-4b8d-bb54-f721a5b6193b"] = true, -- Fertilizer Container
	["096d4daf-639e-4947-a1a6-1890eaa94464"] = true, -- Ammo Container
	["ad35f7e6-af8f-40fa-aef4-77d827ac8a8a"] = true, -- Chest
	["e9efc008-8fae-4391-9ad1-6a62dbab5760"] = true, -- Looting Chest
	["be29592a-ef58-4b1d-b18c-895023abd27f"] = true, -- Chemical Container
	["5cb15c93-4fa9-48da-9974-2e95ca6c9e1c"] = true, -- Refinery output
	["9601f2ca-9552-48b0-afc1-b0f200461114"] = true, -- XXL Chest
	["4c474cff-3f6a-4306-93d1-c4c74578afd2"] = true  -- Piped Small Chest
}

-- A directional machine is a graph boundary. Traversing through one would
-- incorrectly merge its input and output storage systems into one catalog.
local DIRECTIONAL_PIPE_BOUNDARIES = {
	["b63c6440-dfc2-4da7-acdb-3c385080b2e4"] = true, -- Craftbot 1
	["b7571f6f-9d53-44ba-99d2-3b4f05e6fd0f"] = true, -- Craftbot 2
	["1c83675f-7c77-4cbb-875b-79d4bd46100d"] = true, -- Craftbot 3
	["c69a7855-d915-4784-af81-d0a8849e458f"] = true, -- Craftbot 4
	["4fcb4cb8-7623-11ea-bc55-0242ac130003"] = true, -- Craftbot 5
	["0a0cc4ee-bdd7-41b1-b5cb-f34a0e6de46e"] = true, -- Saw table
	["b46c3271-6288-4b74-a6b1-9ea946cf072b"] = true, -- Prospector
	["5cb15c93-4fa9-48da-9974-2e95ca6c9e1c"] = true, -- Refinery
	["b593a935-802a-4715-b27f-739a091a8977"] = true, -- Ore crusher
	["6c450f8e-7fe5-43ad-9391-c429e83310d2"] = true  -- Garage chest
}

local function normalizeSearch( value )
	return string.lower( tostring( value or "" ) ):gsub( "^%s+", "" ):gsub( "%s+$", "" )
end

local function currentLanguage()
	local ok, language = pcall( sm.gui.getCurrentLanguage )
	return ok and type( language ) == "string" and language or "English"
end

local function localizedText( key, ... )
	local language = LOCALIZATION[currentLanguage()] or LOCALIZATION.English or {}
	local fallback = LOCALIZATION.English or {}
	local value = language[key] or fallback[key] or tostring( key )
	if select( "#", ... ) > 0 then
		local ok, formatted = pcall( string.format, value, ... )
		if ok then return formatted end
	end
	return value
end

local function sourceKind( entry )
	local localCount = tonumber( entry and entry.localSources ) or 0
	local wirelessCount = tonumber( entry and entry.wirelessSources ) or 0
	if localCount > 0 and wirelessCount > 0 then return localizedText( "mixedSource" ) end
	if wirelessCount > 0 then return localizedText( "remoteSource" ) end
	return localizedText( "localSource" )
end

local function normalizeItemType( value )
	local normalized = string.lower( tostring( value or "" ) )
	return ITEM_TYPE_ORDER[normalized] and normalized or "other"
end

local function itemTypeRank( item )
	return ITEM_TYPE_ORDER[normalizeItemType( item and item.itemType )] or ITEM_TYPE_ORDER.other
end

local function buildVisibleCatalog( catalog, searchValue, sortMode, filterType )
	local search = normalizeSearch( searchValue )
	local filter = string.lower( tostring( filterType or "ALL" ) )
	local entries = {}
	for index, item in ipairs( catalog or {} ) do
		local typeMatches = filter == "all" or normalizeItemType( item.itemType ) == filter
		if typeMatches and ( search == "" or string.find( item.searchTitle, search, 1, true ) ~= nil ) then
			entries[#entries + 1] = { index = index, item = item }
		end
	end
	table.sort( entries, function( a, b )
		if sortMode == "TYPE" and itemTypeRank( a.item ) ~= itemTypeRank( b.item ) then
			return itemTypeRank( a.item ) < itemTypeRank( b.item )
		elseif sortMode == "COUNT" and a.item.quantity ~= b.item.quantity then
			return a.item.quantity > b.item.quantity
		elseif sortMode == "STACKS" and a.item.stacks ~= b.item.stacks then
			return a.item.stacks > b.item.stacks
		end
		if sortMode ~= "NAME" and sortMode ~= "TYPE" and itemTypeRank( a.item ) ~= itemTypeRank( b.item ) then
			return itemTypeRank( a.item ) < itemTypeRank( b.item )
		end
		if a.item.searchTitle ~= b.item.searchTitle then
			return a.item.searchTitle < b.item.searchTitle
		end
		return a.item.uuid < b.item.uuid
	end )
	return entries
end

local function findRequiredWidget( root, name )
	local widget = FindWidget( root, name )
	assert( widget, PHASE1_PREFIX .. "missing JSON GUI widget: " .. name )
	return widget
end

local function hasEntries( tableValue )
	return tableValue and next( tableValue ) ~= nil
end

local function validShape( shape )
	if not shape then return false end
	local ok, exists = pcall( function() return sm.exists( shape ) end )
	return ok and exists == true
end

local function shapeId( shape )
	local ok, id = pcall( function() return shape:getId() end )
	return ok and id ~= nil and tostring( id ) or nil
end

local function shapeUuid( shape )
	local ok, uuid = pcall( function() return shape:getShapeUuid() end )
	return ok and uuid and string.lower( tostring( uuid ) ) or nil
end

local function collectPhysicalStorageShapes( rootShape )
	if not validShape( rootShape ) then return nil, "TERMINAL SHAPE UNAVAILABLE" end
	local rootId = shapeId( rootShape )
	if not rootId then return nil, "TERMINAL SHAPE ID UNAVAILABLE" end
	local queue, head, visited, visitedCount, storage = { rootShape }, 1, {}, 0, {}
	while head <= #queue do
		local shape = queue[head]
		head = head + 1
		if validShape( shape ) then
			local id = shapeId( shape )
			if not id then return nil, "PIPE SHAPE ID UNAVAILABLE" end
			if not visited[id] then
				visited[id] = true
				visitedCount = visitedCount + 1
				if visitedCount > MAX_LOCAL_PIPE_SHAPES then
					return nil, "LOCAL PIPE GRAPH SAFETY LIMIT EXCEEDED"
				end
				local uuid = shapeUuid( shape )
				if id ~= rootId and uuid and LOCAL_STORAGE_UUIDS[uuid] then
					storage[#storage + 1] = { id = id, shape = shape }
				end
				if id == rootId or not ( uuid and DIRECTIONAL_PIPE_BOUNDARIES[uuid] ) then
					local ok, neighbours = pcall( function() return shape:getPipedNeighbours() end )
					if not ok or type( neighbours ) ~= "table" then return nil, "PHYSICAL PIPE NEIGHBOURS UNAVAILABLE" end
					for _, neighbour in ipairs( neighbours ) do
						local neighbourId = validShape( neighbour ) and shapeId( neighbour ) or nil
						if neighbourId and not visited[neighbourId] then queue[#queue + 1] = neighbour end
					end
				end
			end
		end
	end
	table.sort( storage, function( a, b ) return a.id < b.id end )
	local shapes = {}
	for _, entry in ipairs( storage ) do shapes[#shapes + 1] = entry.shape end
	return shapes
end

local function validViewer( player, shape )
	if not player or not validShape( shape ) then return false end
	local character = player:getCharacter()
	if not character or not sm.exists( character ) then return false end
	local worldOk, shapeWorld = pcall( function() return shape:getBody():getWorld() end )
	if not worldOk or not shapeWorld or character:getWorld() ~= shapeWorld then return false end
	return ( character:getWorldPosition() - shape:getWorldPosition() ):length2() <= VIEW_DISTANCE * VIEW_DISTANCE
end

local function countTable( value )
	local count = 0
	for _ in pairs( value or {} ) do count = count + 1 end
	return count
end

local function playerKey( player )
	return player and tostring( player.id ) or nil
end

local function safeItemUuid( value )
	if type( value ) ~= "string" then return nil end
	local ok, uuid = pcall( sm.uuid.new, value )
	if not ok or not uuid or uuid:isNil() then return nil end
	return uuid
end

local function descriptorKey( descriptors, routeState )
	local ids = {}
	for _, descriptor in ipairs( descriptors or {} ) do
		ids[#ids + 1] = table.concat( {
			tostring( descriptor.id or "?" ),
			tostring( descriptor.routeKind or "LOCAL" ),
			tostring( descriptor.worldId or "?" ),
			tostring( descriptor.routeDistance or 0 ),
			descriptor.wireless and "W" or "L"
		}, ":" )
	end
	ids[#ids + 1] = "G:" .. tostring( routeState and routeState.topologyGeneration or 0 )
	ids[#ids + 1] = "S:" .. tostring( routeState and routeState.wirelessState or "LOCAL_ONLY" )
	return table.concat( ids, "|" )
end

local function localRouteState( wirelessInstalled, reason )
	return {
		wirelessInstalled = wirelessInstalled == true,
		managerAvailable = false,
		wirelessState = wirelessInstalled and "OFFLINE" or "LOCAL_ONLY",
		compatibilityReason = reason,
		topologyGeneration = 0,
		matchingEndpoints = 0,
		readyEndpoints = 0,
		limitedEndpoints = 0,
		offlineEndpoints = 0,
		worlds = {},
		reachableWorlds = 1,
		crossWorld = false,
		localOnly = true
	}
end

local function mergeRouteStates( spend, collect )
	spend = spend or localRouteState( false )
	collect = collect or localRouteState( false )
	local labels, worlds = {}, {}
	for _, state in ipairs( { spend, collect } ) do
		for _, label in ipairs( state.worlds or {} ) do
			if not labels[label] then labels[label] = true; worlds[#worlds + 1] = label end
		end
	end
	table.sort( worlds )
	local wirelessState = "LOCAL_ONLY"
	if spend.wirelessState == "LIMITED" or collect.wirelessState == "LIMITED" then wirelessState = "LIMITED"
	elseif spend.wirelessState == "READY" or collect.wirelessState == "READY" then wirelessState = "READY"
	elseif spend.wirelessState == "OFFLINE" or collect.wirelessState == "OFFLINE" then wirelessState = "OFFLINE" end
	return {
		wirelessInstalled = spend.wirelessInstalled == true or collect.wirelessInstalled == true,
		managerAvailable = spend.managerAvailable == true or collect.managerAvailable == true,
		wirelessState = wirelessState,
		compatibilityReason = spend.compatibilityReason or collect.compatibilityReason,
		topologyGeneration = math.max( spend.topologyGeneration or 0, collect.topologyGeneration or 0 ),
		matchingEndpoints = math.max( spend.matchingEndpoints or 0, collect.matchingEndpoints or 0 ),
		readyEndpoints = math.max( spend.readyEndpoints or 0, collect.readyEndpoints or 0 ),
		limitedEndpoints = math.max( spend.limitedEndpoints or 0, collect.limitedEndpoints or 0 ),
		offlineEndpoints = math.max( spend.offlineEndpoints or 0, collect.offlineEndpoints or 0 ),
		worlds = worlds,
		reachableWorlds = math.max( spend.reachableWorlds or 1, collect.reachableWorlds or 1 ),
		crossWorld = spend.crossWorld == true or collect.crossWorld == true,
		localOnly = spend.localOnly ~= false and collect.localOnly ~= false,
		spendState = spend.wirelessState or "LOCAL_ONLY",
		collectState = collect.wirelessState or "LOCAL_ONLY"
	}
end

local function maxCollectableQuantity( container, itemUuid, wanted )
	if not container or wanted <= 0 then return 0 end
	local function canCollect( quantity )
		if quantity <= 0 then return true end
		local ok, accepted = pcall( sm.container.canCollect, container, itemUuid, quantity )
		return ok and accepted == true
	end
	if canCollect( wanted ) then return wanted end
	local low, high = 0, wanted
	while low < high do
		local middle = math.floor( ( low + high + 1 ) / 2 )
		if canCollect( middle ) then low = middle else high = middle - 1 end
	end
	return low
end

local function inspectDestinationContents( container, itemUuid )
	local wanted = tostring( itemUuid )
	local stackSize = math.max( 1, sm.item.getStackSize( itemUuid ) or 1 )
	local hasItem, fullestPartial, occupiedStacks = false, 0, 0
	for slot = 0, container:getSize() - 1 do
		local item = container:getItem( slot )
		if item and item.uuid and ( item.quantity or 0 ) > 0 then
			occupiedStacks = occupiedStacks + 1
			if tostring( item.uuid ) == wanted then
				hasItem = true
				if item.quantity < stackSize then fullestPartial = math.max( fullestPartial, item.quantity ) end
			end
		end
	end
	return hasItem, fullestPartial, occupiedStacks
end

local function destinationHasEmptySlot( container, record )
	local size = record and tonumber( record.size ) or container:getSize()
	local occupied = record and tonumber( record.occupiedStacks ) or nil
	if occupied == nil then
		occupied = 0
		for slot = 0, size - 1 do
			local item = container:getItem( slot )
			if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
				occupied = occupied + 1
			end
		end
	end
	return occupied < size
end

local function destinationProximity( originShape, targetShape, fallbackDistance )
	local ok, tier, distance = pcall( function()
		local originWorld = originShape:getBody():getWorld()
		local targetWorld = targetShape:getBody():getWorld()
		if originWorld == targetWorld then
			return 0, ( targetShape:getWorldPosition() - originShape:getWorldPosition() ):length2()
		end
		return 1, tonumber( fallbackDistance ) or 0
	end )
	if ok then return tier, distance end
	return 2, tonumber( fallbackDistance ) or math.huge
end

local function contentRoutingRank( descriptor, hasItem, fullestPartial, occupiedStacks,
		familyStacks, categoryStacks, knownProfile )
	if descriptor.specialized then return 1, "DEDICATED CONTAINER" end
	if fullestPartial > 0 then return 2, "FULLEST PARTIAL STACK" end
	if hasItem then return 3, "EXACT ITEM CHEST" end
	if knownProfile and familyStacks > 0 and familyStacks * 2 >= occupiedStacks then
		return 4, "DOMINANT ITEM FAMILY"
	end
	if knownProfile and categoryStacks > 0 and categoryStacks * 2 >= occupiedStacks then
		return 5, "DOMINANT ITEM CATEGORY"
	end
	if knownProfile and familyStacks > 0 then return 6, "MATCHING ITEM FAMILY" end
	if knownProfile and categoryStacks > 0 then return 7, "MATCHING ITEM CATEGORY" end
	if occupiedStacks == 0 then return 8, "EMPTY CHEST" end
	return 9, "UNRELATED CHEST"
end

-- Server --------------------------------------------------------------------

function NetworkStorageChest.sv_updateClientData( self )
	local buffer = self.interactable:getContainer( 0 )
	local size = buffer and buffer:getSize() or 0
	self.network:setClientData( {
		phase = 2,
		bufferReady = size == EXPECTED_BUFFER_SIZE,
		bufferSize = size,
		smartRouting = not self.sv or self.sv.smartRouting ~= false
	} )
end

function NetworkStorageChest.sv_tryMigrateDepositBuffer( self )
	local buffer = self.interactable:getContainer( 0 )
	if not buffer or buffer:getSize() == EXPECTED_BUFFER_SIZE then return buffer end
	if not buffer:isEmpty() or ( self.sv and hasEntries( self.sv.viewers ) ) then return buffer end
	self.interactable:removeContainer( 0 )
	buffer = self.interactable:addContainer( 0, EXPECTED_BUFFER_SIZE )
	if self.sv then
		self.sv.lastBufferRevision = buffer and buffer:getRevision() or -1
		self.sv.depositDirty = false
		self:sv_updateClientData()
	end
	sm.log.info( PHASE1_PREFIX .. "empty legacy deposit buffer migrated to three slots" )
	return buffer
end

function NetworkStorageChest.server_onCreate( self )
	local buffer = self.interactable:getContainer( 0 )
	-- Safely migrate empty five/ten-slot versions. A non-empty legacy
	-- buffer is never removed, because resizing must not destroy player items.
	if buffer and buffer:getSize() ~= EXPECTED_BUFFER_SIZE and buffer:isEmpty() then
		self.interactable:removeContainer( 0 )
		buffer = self.interactable:addContainer( 0, EXPECTED_BUFFER_SIZE )
		sm.log.info( PHASE1_PREFIX .. "empty legacy deposit buffer migrated to three slots" )
	end
	if not buffer then buffer = self.interactable:addContainer( 0, EXPECTED_BUFFER_SIZE ) end

	local size = buffer and buffer:getSize() or 0
	if size ~= EXPECTED_BUFFER_SIZE then
		sm.log.warning( PHASE1_PREFIX .. "expected a 3-slot engine container for " .. PART_UUID ..
			", got " .. tostring( size ) .. "; its items remain intact and it will migrate after routing empties it" )
	else
		sm.log.info( PHASE1_PREFIX .. "persistent 3-slot deposit buffer ready" )
	end
	local stored = self.storage:load()
	if type( stored ) ~= "table" then stored = {} end

	self.sv = {
		tick = 0,
		viewers = {},
		sessions = {},
		sessionSerial = 0,
		topologyInitialized = false,
		topologyKey = "",
		topologyGeneration = 0,
		contentGeneration = 0,
		containers = {},
		records = {},
		scanQueue = {},
		scanCursor = 1,
		scanReason = "",
		scanStartedTick = 0,
		indexing = false,
		scanBlocking = false,
		pendingScanDescriptors = {},
		pendingScanReason = nil,
		needsRescan = true,
		revisionCursor = 1,
		lastSignature = nil,
		snapshot = nil,
		pendingCatalogSnapshot = nil,
		lastCatalogPublishTick = -CATALOG_PUBLISH_INTERVAL_TICKS,
		lastPublishedSignature = nil,
		lastPublishedTopologyGeneration = -1,
		lastError = nil,
		pendingWithdrawals = {},
		withdrawalSerial = 0,
		withdrawalStats = {
			requests = 0, retries = 0, successes = 0, failures = 0,
			topologyWaits = 0, transactionBusy = 0, slotConflicts = 0
		},
		activitySerial = 0,
		lastBufferRevision = buffer and buffer:getRevision() or -1,
		depositDirty = buffer and not buffer:isEmpty() or false,
		depositRetryTick = 20,
		depositStatus = "READY",
		depositDebug = false,
		depositRoutingSuspended = false,
		stored = stored,
		smartRouting = stored.smartRouting ~= false,
		routeState = localRouteState( false )
	}
	g_scrapLabNetworkStorageChestInstances = g_scrapLabNetworkStorageChestInstances or {}
	self.sv.registryId = tostring( self.shape:getId() )
	g_scrapLabNetworkStorageChestInstances[self.sv.registryId] = self
	self.interactable.publicData = self.interactable.publicData or {}
	self:sv_publishDiagnostics( "IDLE", 0, 0, 0 )

	self:sv_updateClientData()
end

function NetworkStorageChest.server_onRefresh( self )
	self:server_onCreate()
end

function NetworkStorageChest.server_onDestroy( self )
	if self.sv and self.sv.registryId and g_scrapLabNetworkStorageChestInstances then
		g_scrapLabNetworkStorageChestInstances[self.sv.registryId] = nil
	end
	-- The engine may invalidate the interactable before this callback runs.
	-- Its public data dies with it, so no explicit cleanup is necessary here.
end

function NetworkStorageChest.sv_publishDiagnostics( self, status, scanned, total, durationTicks )
	local stats = NetworkInventoryIndex.getStatistics()
	self.interactable.publicData = self.interactable.publicData or {}
	self.interactable.publicData.scrapLabStoragePhase1 = {
		status = status,
		viewers = countTable( self.sv and self.sv.viewers ),
		containers = #( self.sv and self.sv.containers or {} ),
		uniqueItems = self.sv and self.sv.snapshot and self.sv.snapshot.uniqueItems or 0,
		totalQuantity = self.sv and self.sv.snapshot and self.sv.snapshot.totalQuantity or 0,
		topologyGeneration = self.sv and self.sv.topologyGeneration or 0,
		wirelessState = self.sv and self.sv.routeState and self.sv.routeState.wirelessState or "LOCAL_ONLY",
		reachableWorlds = self.sv and self.sv.routeState and self.sv.routeState.reachableWorlds or 1,
		contentGeneration = self.sv and self.sv.contentGeneration or 0,
		scanned = scanned or 0,
		scanTotal = total or 0,
		durationTicks = durationTicks or 0,
		cachedEntries = stats.cachedEntries,
		cacheHits = stats.cacheHits,
		containerScans = stats.containerScans,
		slotsScanned = stats.slotsScanned,
		activitySerial = self.sv and self.sv.activitySerial or 0,
		pendingWithdrawals = countTable( self.sv and self.sv.pendingWithdrawals ),
		withdrawalRequests = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.requests or 0,
		withdrawalRetries = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.retries or 0,
		withdrawalSuccesses = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.successes or 0,
		withdrawalFailures = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.failures or 0,
		withdrawalTopologyWaits = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.topologyWaits or 0,
		withdrawalTransactionBusy = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.transactionBusy or 0,
		withdrawalSlotConflicts = self.sv and self.sv.withdrawalStats and self.sv.withdrawalStats.slotConflicts or 0,
		smartRouting = not self.sv or self.sv.smartRouting ~= false,
		bufferSize = self.interactable:getContainer( 0 ) and self.interactable:getContainer( 0 ):getSize() or 0,
		qualificationLocked = self.sv and self.sv.qualificationLocked == true or false,
		sessions = countTable( self.sv and self.sv.sessions ),
		lastError = self.sv and self.sv.lastError or nil
	}
	self.interactable.publicData.scrapLabStoragePhase1.depositStatus = self.sv and self.sv.depositStatus or "READY"
	self.interactable.publicData.scrapLabStoragePhase1.depositDebug = self.sv and self.sv.depositDebug == true or false
end

function NetworkStorageChest.sv_sendToViewers( self, callback, payload )
	-- The automatic server harness exercises the real index directly from the
	-- SurvivalGame script. Scrap Mechanic correctly forbids that scriptRef from
	-- sending through this shape's network object, so harness sessions suppress
	-- client transport while retaining all topology/cache/revision behavior.
	if self.sv and self.sv.testHarnessSilent then return end
	for _, player in pairs( self.sv.viewers ) do
		if validViewer( player, self.shape ) then
			self.network:sendToClient( player, callback, payload )
		end
	end
end

function NetworkStorageChest.sv_beginPhase1HarnessSession( self, player )
	if not player or not self.sv then return false, "TERMINAL INSTANCE NOT READY" end
	if not validViewer( player, self.shape ) then return false, "TEST PLAYER IS OUT OF RANGE" end
	self.sv.testHarnessSilent = true
	self.sv.depositRoutingSuspended = true
	self.sv.viewers[playerKey( player )] = player
	self:sv_publishDiagnostics( self.sv.indexing and "INDEXING" or "READY", 0, 0, 0 )
	self:sv_refreshTopology( true )
	return true
end

function NetworkStorageChest.sv_beginPhase1QualificationSession( self, player, descriptors, runId )
	if not player or not self.sv or type( descriptors ) ~= "table" then
		return false, "QUALIFICATION SESSION DATA INVALID"
	end
	self.sv.testHarnessSilent = true
	self.sv.depositRoutingSuspended = true
	self.sv.testHarnessDescriptors = descriptors
	self.sv.qualificationLocked = true
	self.sv.qualificationRunId = tostring( runId or "QUALIFICATION" )
	self.sv.viewers[playerKey( player )] = player
	self.sv.topologyInitialized = true
	local qualificationState = localRouteState( false )
	self.sv.routeState = mergeRouteStates( qualificationState, qualificationState )
	self.sv.topologyKey = descriptorKey( descriptors, qualificationState ) .. "||" ..
		descriptorKey( descriptors, qualificationState )
	self.sv.topologyGeneration = self.sv.topologyGeneration + 1
	self.sv.containers = descriptors
	self.sv.records = {}
	self.sv.revisionCursor = 1
	self.sv.lastError = nil
	self:sv_startScan( descriptors, "QUALIFICATION" )
	return true
end

function NetworkStorageChest.sv_endPhase1HarnessSession( self, player )
	if not self.sv then return end
	local wasQualification = self.sv.qualificationLocked == true
	self.sv.testHarnessSilent = nil
	self.sv.depositRoutingSuspended = false
	self.sv.testHarnessDescriptors = nil
	if wasQualification then
		self.sv.viewers = {}
		self.sv.sessions = {}
		self.sv.pendingWithdrawals = {}
		self.sv.indexing = false
		self.sv.scanQueue = {}
		self.sv.pendingScanDescriptors = {}
		self.sv.pendingScanReason = nil
		self.sv.pendingCatalogSnapshot = nil
		self.sv.qualificationLocked = nil
		self.sv.qualificationRunId = nil
		self.sv.phase1QualificationToken = nil
		self.sv.topologyInitialized = false
		self.sv.topologyKey = ""
		self.sv.containers = {}
		self.sv.records = {}
		self.sv.revisionCursor = 1
		self.sv.needsRescan = true
	elseif player then
		local key = playerKey( player )
		self.sv.viewers[key] = nil
		self.sv.pendingWithdrawals[key] = nil
	end
	if not hasEntries( self.sv.viewers ) then
		self.sv.pendingWithdrawals = {}
		self.sv.indexing = false
		self.sv.scanQueue = {}
		self.sv.pendingScanDescriptors = {}
		self.sv.pendingScanReason = nil
		self.sv.pendingCatalogSnapshot = nil
		self:sv_publishDiagnostics( "IDLE", 0, 0, 0 )
	end
end

function NetworkStorageChest.sv_indexingPayload( self )
	local previous = self.sv.snapshot or {}
	return {
		status = "INDEXING",
		phase = 2,
		entries = previous.entries or {},
		uniqueItems = previous.uniqueItems or 0,
		totalQuantity = previous.totalQuantity or 0,
		totalStacks = previous.totalStacks or 0,
		containerCount = #self.sv.containers,
		scannedContainers = math.min( self.sv.scanCursor - 1, #self.sv.scanQueue ),
		scanTotal = #self.sv.scanQueue,
		topologyGeneration = self.sv.topologyGeneration,
		contentGeneration = self.sv.contentGeneration,
		contentSignature = previous.contentSignature or self.sv.lastSignature,
		localOnly = self.sv.routeState == nil or self.sv.routeState.localOnly ~= false,
		wirelessInstalled = self.sv.routeState and self.sv.routeState.wirelessInstalled == true or false,
		wirelessState = self.sv.routeState and self.sv.routeState.wirelessState or "LOCAL_ONLY",
		reachableWorlds = self.sv.routeState and self.sv.routeState.reachableWorlds or 1,
		worlds = self.sv.routeState and self.sv.routeState.worlds or {},
		crossWorld = self.sv.routeState and self.sv.routeState.crossWorld == true or false,
		compatibilityReason = self.sv.routeState and self.sv.routeState.compatibilityReason or nil
	}
end

function NetworkStorageChest.sv_publishCatalogSnapshot( self, snapshot, force )
	if not snapshot then return end
	local signatureChanged = snapshot.contentSignature ~= self.sv.lastPublishedSignature
	local topologyChanged = snapshot.topologyGeneration ~= self.sv.lastPublishedTopologyGeneration
	if not force and not signatureChanged and not topologyChanged then
		self.sv.pendingCatalogSnapshot = nil
		return
	end
	if not force and self.sv.tick - self.sv.lastCatalogPublishTick < CATALOG_PUBLISH_INTERVAL_TICKS then
		-- Replace, rather than append, so a busy machine produces one latest-state
		-- update instead of a transport backlog.
		self.sv.pendingCatalogSnapshot = snapshot
		return
	end
	self.sv.pendingCatalogSnapshot = nil
	self.sv.lastCatalogPublishTick = self.sv.tick
	self.sv.lastPublishedSignature = snapshot.contentSignature
	self.sv.lastPublishedTopologyGeneration = snapshot.topologyGeneration
	self:sv_sendToViewers( "cl_n_catalogSnapshot", snapshot )
end

function NetworkStorageChest.sv_flushCatalogSnapshot( self )
	if self.sv.pendingCatalogSnapshot and
			self.sv.tick - self.sv.lastCatalogPublishTick >= CATALOG_PUBLISH_INTERVAL_TICKS then
		self:sv_publishCatalogSnapshot( self.sv.pendingCatalogSnapshot, true )
	end
end

function NetworkStorageChest.sv_collectLocalContainers( self )
	local shapes, traversalFailure = collectPhysicalStorageShapes( self.shape )
	if not shapes then return nil, traversalFailure or "LOCAL PHYSICAL PIPE QUERY FAILED" end

	local buffer = self.interactable:getContainer( 0 )
	local excludedId = NetworkInventoryIndex.getContainerId( buffer )
	local seen = {}
	local descriptors = {}
	for _, shape in ipairs( shapes or {} ) do
		if validShape( shape ) and shape ~= self.shape then
			local interactable = shape:getInteractable()
			local container = interactable and interactable:getContainer( 0 ) or nil
			local id = NetworkInventoryIndex.getContainerId( container )
			if id and id ~= excludedId and not seen[id] then
				seen[id] = true
				descriptors[#descriptors + 1] = {
					id = id, shape = shape, container = container,
					wireless = false, crossWorld = false, routeKind = "LOCAL",
					routePriority = 0, routeDistance = 0
				}
			end
		end
	end
	table.sort( descriptors, function( a, b ) return a.id < b.id end )
	return descriptors
end

function NetworkStorageChest.sv_collectNetworkContainers( self, kind )
	if self.sv.testHarnessDescriptors then
		return self.sv.testHarnessDescriptors, nil, localRouteState( false )
	end
	local graph = ScrapLabPipeGraph
	local methodName = kind == "collect" and "getTerminalCollectContainers" or "getTerminalSpendContainers"
	if graph and type( graph[methodName] ) == "function" then
		local ok, descriptors, routeState = pcall( graph[methodName], self.shape )
		if ok and type( descriptors ) == "table" and type( routeState ) == "table" then
			local bufferId = NetworkInventoryIndex.getContainerId( self.interactable:getContainer( 0 ) )
			local seen, safe = {}, {}
			for _, descriptor in ipairs( descriptors ) do
				local id = descriptor.id or NetworkInventoryIndex.getContainerId( descriptor.container )
				local uuid = descriptor.shape and shapeUuid( descriptor.shape ) or nil
				local existsOk, containerExists = pcall( function() return descriptor.container and sm.exists( descriptor.container ) end )
				if id and id ~= bufferId and not seen[id] and uuid and LOCAL_STORAGE_UUIDS[uuid] and existsOk and containerExists then
					seen[id] = true
					descriptor.id = tostring( id )
					descriptor.routePriority = descriptor.routePriority or ( descriptor.wireless and 1 or 0 )
					descriptor.routeDistance = descriptor.routeDistance or 0
					safe[#safe + 1] = descriptor
				end
			end
			table.sort( safe, function( a, b )
				if a.routePriority ~= b.routePriority then return a.routePriority < b.routePriority end
				if a.routeDistance ~= b.routeDistance then return a.routeDistance < b.routeDistance end
				return a.id < b.id
			end )
			return safe, nil, routeState
		end
		local localDescriptors, failure = self:sv_collectLocalContainers()
		return localDescriptors, failure, localRouteState( true, ok and "WIRELESS DESCRIPTOR QUERY INVALID" or tostring( descriptors ) )
	end
	local descriptors, failure = self:sv_collectLocalContainers()
	return descriptors, failure, localRouteState( false )
end

function NetworkStorageChest.sv_collectTopologySnapshot( self )
	local spend, spendFailure, spendState = self:sv_collectNetworkContainers( "spend" )
	if not spend then return nil, nil, nil, spendFailure end
	local collect, collectFailure, collectState = self:sv_collectNetworkContainers( "collect" )
	if not collect then return nil, nil, nil, collectFailure end
	local state = mergeRouteStates( spendState, collectState )
	local key = descriptorKey( spend, spendState ) .. "||" .. descriptorKey( collect, collectState )
	return spend, collect, state, nil, key
end

function NetworkStorageChest.sv_startScan( self, descriptors, reason )
	reason = reason or "REFRESH"
	local blocking = self.sv.snapshot == nil or reason == "TOPOLOGY" or reason == "QUALIFICATION"
	if self.sv.indexing and not blocking then
		-- A container may be touched again while an earlier chunked scan is still
		-- running. Coalesce by container id and scan its latest revision once more
		-- after the current pass instead of restarting the index.
		for _, descriptor in ipairs( descriptors or {} ) do
			if descriptor and descriptor.id then
				self.sv.pendingScanDescriptors[descriptor.id] = descriptor
			end
		end
		self.sv.pendingScanReason = reason
		return
	end

	if blocking then
		-- A real topology replacement invalidates the old scan queue.
		self.sv.pendingScanDescriptors = {}
		self.sv.pendingScanReason = nil
	end
	self.sv.scanQueue = descriptors or {}
	self.sv.scanCursor = 1
	self.sv.scanReason = reason
	self.sv.scanStartedTick = self.sv.tick
	self.sv.scanCacheHits = 0
	self.sv.scanContainerScans = 0
	self.sv.scanSlotsScanned = 0
	self.sv.indexing = true
	self.sv.scanBlocking = blocking
	self.sv.needsRescan = blocking
	self:sv_publishDiagnostics( blocking and "INDEXING" or "REFRESHING", 0, #self.sv.scanQueue, 0 )
	if blocking then
		self:sv_sendToViewers( "cl_n_catalogSnapshot", self:sv_indexingPayload() )
	end
	if #self.sv.scanQueue == 0 then self:sv_finishScan() end
end

function NetworkStorageChest.sv_refreshTopology( self, force )
	local descriptors, _, routeState, failure, topologyKey = self:sv_collectTopologySnapshot()
	if not descriptors then
		self.sv.lastError = failure or "PIPE NETWORK QUERY FAILED"
		self.sv.indexing = false
		self:sv_publishDiagnostics( "OFFLINE", 0, 0, 0 )
		self:sv_sendToViewers( "cl_n_catalogSnapshot", {
			status = "OFFLINE", phase = 2, entries = {}, uniqueItems = 0,
			totalQuantity = 0, totalStacks = 0, containerCount = 0,
			compatibilityReason = self.sv.lastError, localOnly = true,
			wirelessState = "OFFLINE", reachableWorlds = 1
		} )
		return
	end

	local key = topologyKey
	if not force and self.sv.topologyInitialized and key == self.sv.topologyKey then return end

	self.sv.topologyInitialized = true
	self.sv.topologyKey = key
	self.sv.topologyGeneration = self.sv.topologyGeneration + 1
	self.sv.containers = descriptors
	self.sv.routeState = routeState or localRouteState( false )
	self.sv.revisionCursor = 1
	self.sv.lastError = nil

	local retained = {}
	for _, descriptor in ipairs( descriptors ) do
		if self.sv.records[descriptor.id] then retained[descriptor.id] = self.sv.records[descriptor.id] end
	end
	self.sv.records = retained
	self:sv_startScan( descriptors, "TOPOLOGY" )
	sm.log.info( PHASE1_PREFIX .. "local topology generation " .. tostring( self.sv.topologyGeneration ) ..
		" contains " .. tostring( #descriptors ) .. " deduplicated local storage containers" )
end

function NetworkStorageChest.sv_processScan( self )
	local processed = 0
	while self.sv.scanCursor <= #self.sv.scanQueue and processed < SCAN_BUDGET_PER_TICK do
		local descriptor = self.sv.scanQueue[self.sv.scanCursor]
		self.sv.scanCursor = self.sv.scanCursor + 1
		processed = processed + 1
		local record, cached, failure = NetworkInventoryIndex.read( descriptor.container, self.sv.tick )
		self.sv.activitySerial = ( self.sv.activitySerial or 0 ) + 1
		if record then
			if cached then
				self.sv.scanCacheHits = self.sv.scanCacheHits + 1
			else
				self.sv.scanContainerScans = self.sv.scanContainerScans + 1
				self.sv.scanSlotsScanned = self.sv.scanSlotsScanned + ( descriptor.container:getSize() or 0 )
			end
			self.sv.records[descriptor.id] = record
		else
			self.sv.records[descriptor.id] = nil
			self.sv.lastError = failure or "CONTAINER SCAN FAILED"
			sm.log.warning( PHASE1_PREFIX .. "container " .. descriptor.id .. " scan skipped: " .. self.sv.lastError )
		end
	end

	self:sv_publishDiagnostics( self.sv.scanBlocking and "INDEXING" or "REFRESHING",
		self.sv.scanCursor - 1, #self.sv.scanQueue,
		self.sv.tick - self.sv.scanStartedTick )
	if self.sv.scanCursor > #self.sv.scanQueue then self:sv_finishScan() end
end

function NetworkStorageChest.sv_finishScan( self )
	local completedReason = self.sv.scanReason
	local wasBlocking = self.sv.scanBlocking == true
	local records = {}
	for _, descriptor in ipairs( self.sv.containers ) do
		local record = self.sv.records[descriptor.id]
		if record then records[#records + 1] = record end
	end
	local aggregate = NetworkInventoryIndex.aggregate( records )
	local aggregateByUuid = {}
	for _, entry in ipairs( aggregate.entries ) do
		entry.localSources = 0
		entry.wirelessSources = 0
		entry.crossWorldSources = 0
		aggregateByUuid[entry.uuid] = entry
	end
	for _, descriptor in ipairs( self.sv.containers ) do
		local record = self.sv.records[descriptor.id]
		for _, item in ipairs( record and record.items or {} ) do
			local entry = aggregateByUuid[item.uuid]
			if entry then
				if descriptor.wireless then entry.wirelessSources = entry.wirelessSources + 1
				else entry.localSources = entry.localSources + 1 end
				if descriptor.crossWorld then entry.crossWorldSources = entry.crossWorldSources + 1 end
			end
		end
	end
	local aggregateChanged = aggregate.signature ~= self.sv.lastSignature
	if aggregateChanged then
		self.sv.contentGeneration = self.sv.contentGeneration + 1
		self.sv.lastSignature = aggregate.signature
	end

	local duration = self.sv.tick - self.sv.scanStartedTick
	self.sv.indexing = false
	self.sv.scanBlocking = false
	self.sv.needsRescan = false
	self.sv.snapshot = {
		status = "READY",
		phase = 2,
		entries = aggregate.entries,
		uniqueItems = aggregate.uniqueItems,
		totalQuantity = aggregate.totalQuantity,
		totalStacks = aggregate.totalStacks,
		containerCount = #self.sv.containers,
		topologyGeneration = self.sv.topologyGeneration,
		contentGeneration = self.sv.contentGeneration,
		contentSignature = aggregate.signature,
		scanDurationTicks = duration,
		scanReason = self.sv.scanReason,
		qualificationRunId = self.sv.scanReason == "QUALIFICATION" and self.sv.qualificationRunId or nil,
		scanCacheHits = self.sv.scanCacheHits or 0,
		scanContainerScans = self.sv.scanContainerScans or 0,
		scanSlotsScanned = self.sv.scanSlotsScanned or 0,
		localOnly = self.sv.routeState.localOnly ~= false,
		wirelessInstalled = self.sv.routeState.wirelessInstalled == true,
		wirelessState = self.sv.routeState.wirelessState,
		reachableWorlds = self.sv.routeState.reachableWorlds or 1,
		worlds = self.sv.routeState.worlds or {},
		crossWorld = self.sv.routeState.crossWorld == true,
		matchingEndpoints = self.sv.routeState.matchingEndpoints or 0,
		readyEndpoints = self.sv.routeState.readyEndpoints or 0,
		limitedEndpoints = self.sv.routeState.limitedEndpoints or 0,
		offlineEndpoints = self.sv.routeState.offlineEndpoints or 0,
		spendState = self.sv.routeState.spendState,
		collectState = self.sv.routeState.collectState,
		compatibilityReason = self.sv.routeState.compatibilityReason
	}
	self:sv_publishDiagnostics( "READY", #self.sv.scanQueue, #self.sv.scanQueue, duration )
	local forcePublish = wasBlocking or completedReason == "WITHDRAWAL" or completedReason == "QUALIFICATION"
	if aggregateChanged or forcePublish then
		self:sv_publishCatalogSnapshot( self.sv.snapshot, forcePublish )
	end
	if aggregateChanged or wasBlocking then
		sm.log.info( PHASE1_PREFIX .. "index ready: containers=" .. tostring( #self.sv.containers ) ..
			", unique=" .. tostring( aggregate.uniqueItems ) ..
			", quantity=" .. tostring( aggregate.totalQuantity ) ..
			", stacks=" .. tostring( aggregate.totalStacks ) ..
			", ticks=" .. tostring( duration ) ..
			", reason=" .. tostring( completedReason ) )
	end

	local pending = {}
	for _, descriptor in pairs( self.sv.pendingScanDescriptors or {} ) do
		pending[#pending + 1] = descriptor
	end
	table.sort( pending, function( a, b ) return a.id < b.id end )
	local pendingReason = self.sv.pendingScanReason or "COALESCED"
	self.sv.pendingScanDescriptors = {}
	self.sv.pendingScanReason = nil
	if #pending > 0 then self:sv_startScan( pending, pendingReason ) end
end

function NetworkStorageChest.sv_pollRevisions( self )
	local count = #self.sv.containers
	if count == 0 then return end
	local changed = {}
	local changedIds = {}
	local topologyInvalid = false
	local checks = math.min( REVISION_POLL_BUDGET, count )
	for _ = 1, checks do
		self.sv.activitySerial = ( self.sv.activitySerial or 0 ) + 1
		if self.sv.revisionCursor > count then self.sv.revisionCursor = 1 end
		local descriptor = self.sv.containers[self.sv.revisionCursor]
		self.sv.revisionCursor = self.sv.revisionCursor + 1
		local record = self.sv.records[descriptor.id]
		local revision = NetworkInventoryIndex.getRevision( descriptor.container )
		if revision < 0 then
			topologyInvalid = true
			break
		elseif not record or revision ~= record.revision then
			if not changedIds[descriptor.id] then
				changedIds[descriptor.id] = true
				changed[#changed + 1] = descriptor
			end
		end
	end
	if topologyInvalid then
		if not self.sv.testHarnessDescriptors then self:sv_refreshTopology( true ) end
		return
	end
	if #changed > 0 then
		self:sv_startScan( changed, "REVISION" )
		local buffer = self.interactable:getContainer( 0 )
		if buffer and not buffer:isEmpty() and not self.sv.depositRoutingSuspended then
			self.sv.depositDirty = true
			self.sv.depositRetryTick = self.sv.tick + 1
		end
	end
end

function NetworkStorageChest.sv_validateViewers( self )
	for id, player in pairs( self.sv.viewers ) do
		if not validViewer( player, self.shape ) then
			self.sv.viewers[id] = nil
			self.sv.sessions[id] = nil
			self.sv.pendingWithdrawals[id] = nil
		end
	end
	if not hasEntries( self.sv.viewers ) then
		self.sv.pendingWithdrawals = {}
		self.sv.indexing = false
		self.sv.scanQueue = {}
		self.sv.pendingScanDescriptors = {}
		self.sv.pendingScanReason = nil
		self.sv.pendingCatalogSnapshot = nil
		self:sv_publishDiagnostics( "IDLE", 0, 0, 0 )
	end
end

function NetworkStorageChest.sv_sendDepositStatus( self, status, moved, remaining, destinations, detail )
	self.sv.depositStatus = status
	self:sv_publishDiagnostics( self.sv.indexing and "INDEXING" or "READY", 0, 0, 0 )
	local payload = {
		status = status,
		moved = moved or 0,
		remaining = remaining or 0,
		destinations = destinations or 0,
		detail = self.sv.depositDebug and detail or nil
	}
	self:sv_sendToViewers( "cl_n_depositStatus", payload )
	if self.sv.depositDebug and detail then sm.log.info( PHASE1_PREFIX .. "deposit route: " .. tostring( detail ) ) end
end

function NetworkStorageChest.sv_collectDepositContainers( self )
	local descriptors, failure, routeState
	if self.sv.testHarnessDescriptors then descriptors = self.sv.testHarnessDescriptors
	else descriptors, failure, routeState = self:sv_collectNetworkContainers( "collect" ) end
	if not descriptors then return nil, failure or "LOCAL PIPE QUERY FAILED" end
	local buffer = self.interactable:getContainer( 0 )
	local bufferId = NetworkInventoryIndex.getContainerId( buffer )
	local seen, result = {}, {}
	for _, descriptor in ipairs( descriptors ) do
		local uuid = descriptor.shape and shapeUuid( descriptor.shape ) or nil
		local id = descriptor.id or NetworkInventoryIndex.getContainerId( descriptor.container )
		if id and id ~= bufferId and not seen[id] and not BLOCKED_DEPOSIT_UUIDS[uuid] and
			descriptor.container and sm.exists( descriptor.container ) then
			seen[id] = true
			local proximityTier, proximityDistance = destinationProximity(
				self.shape, descriptor.shape, descriptor.routeDistance )
			result[#result + 1] = {
				id = id, shape = descriptor.shape, container = descriptor.container,
				specialized = SPECIALIZED_DEPOSIT_UUIDS[uuid] == true,
				wireless = descriptor.wireless == true,
				routePriority = descriptor.routePriority or ( descriptor.wireless and 1 or 0 ),
				routeDistance = descriptor.routeDistance or 0,
				proximityTier = proximityTier,
				proximityDistance = proximityDistance,
				original = descriptor
			}
		end
	end
	return result, nil, descriptorKey( descriptors, routeState )
end

function NetworkStorageChest.sv_planDepositSlot( self, itemUuid, quantity, descriptors )
	local candidates = {}
	local smartRouting = not self.sv or self.sv.smartRouting ~= false
	local itemProfile = NetworkInventoryIndex.getItemProfile( itemUuid )
	local knownProfile = itemProfile and itemProfile.category ~= "UNKNOWN"
	for _, descriptor in ipairs( descriptors or {} ) do
		local capacity = maxCollectableQuantity( descriptor.container, itemUuid, quantity )
		if capacity > 0 then
			local record = NetworkInventoryIndex.read( descriptor.container, self.sv and self.sv.tick or nil )
			local emptySlot = destinationHasEmptySlot( descriptor.container, record )
			local hasItem, fullestPartial, occupiedStacks = false, 0, 0
			local familyStacks, categoryStacks = 0, 0
			if record then
				local exact = record.byUuid and record.byUuid[tostring( itemUuid )] or nil
				hasItem = exact ~= nil
				fullestPartial = exact and ( exact.fullestPartial or 0 ) or 0
				occupiedStacks = record.occupiedStacks or 0
				if knownProfile then
					familyStacks = record.familyStacks and ( record.familyStacks[itemProfile.family] or 0 ) or 0
					categoryStacks = record.categoryStacks and ( record.categoryStacks[itemProfile.category] or 0 ) or 0
				end
			else
				hasItem, fullestPartial, occupiedStacks = inspectDestinationContents( descriptor.container, itemUuid )
			end
			if smartRouting or emptySlot then
				local rank, reason
				if smartRouting then
					rank, reason = contentRoutingRank( descriptor, hasItem, fullestPartial,
						occupiedStacks, familyStacks, categoryStacks, knownProfile )
				else
					rank, reason = 1, "NEAREST EMPTY CHEST"
				end
				candidates[#candidates + 1] = {
					descriptor = descriptor,
					capacity = capacity,
					revision = descriptor.container:getRevision(),
					rank = rank,
					reason = reason,
					fullestPartial = fullestPartial,
					hasItem = hasItem,
					occupiedStacks = occupiedStacks,
					familyStacks = familyStacks,
					categoryStacks = categoryStacks
				}
			end
		end
	end
	table.sort( candidates, function( a, b )
		if not smartRouting then
			if a.descriptor.proximityTier ~= b.descriptor.proximityTier then
				return a.descriptor.proximityTier < b.descriptor.proximityTier
			end
			if a.descriptor.proximityDistance ~= b.descriptor.proximityDistance then
				return a.descriptor.proximityDistance < b.descriptor.proximityDistance
			end
			if a.descriptor.routePriority ~= b.descriptor.routePriority then
				return a.descriptor.routePriority < b.descriptor.routePriority
			end
			return a.descriptor.id < b.descriptor.id
		end
		if a.rank ~= b.rank then return a.rank < b.rank end
		if a.fullestPartial ~= b.fullestPartial then return a.fullestPartial > b.fullestPartial end
		local aOccupied, bOccupied = math.max( 1, a.occupiedStacks ), math.max( 1, b.occupiedStacks )
		local aFamilyPurity, bFamilyPurity = a.familyStacks * bOccupied, b.familyStacks * aOccupied
		if aFamilyPurity ~= bFamilyPurity then return aFamilyPurity > bFamilyPurity end
		local aCategoryPurity, bCategoryPurity = a.categoryStacks * bOccupied, b.categoryStacks * aOccupied
		if aCategoryPurity ~= bCategoryPurity then return aCategoryPurity > bCategoryPurity end
		if a.familyStacks ~= b.familyStacks then return a.familyStacks > b.familyStacks end
		if a.categoryStacks ~= b.categoryStacks then return a.categoryStacks > b.categoryStacks end
		if a.capacity ~= b.capacity then return a.capacity > b.capacity end
		if a.descriptor.routePriority ~= b.descriptor.routePriority then
			return a.descriptor.routePriority < b.descriptor.routePriority
		end
		if a.descriptor.routeDistance ~= b.descriptor.routeDistance then
			return a.descriptor.routeDistance < b.descriptor.routeDistance
		end
		return a.descriptor.id < b.descriptor.id
	end )

	local allocation, remaining = {}, quantity
	for _, candidate in ipairs( candidates ) do
		if remaining <= 0 then break end
		local amount = math.min( candidate.capacity, remaining )
		if amount > 0 then
			allocation[#allocation + 1] = {
				descriptor = candidate.descriptor,
				quantity = amount,
				revision = candidate.revision,
				rank = candidate.rank,
				reason = candidate.reason,
				fullestPartial = candidate.fullestPartial
			}
			remaining = remaining - amount
		end
	end
	return allocation, quantity - remaining, remaining
end

function NetworkStorageChest.sv_routeDepositSlot( self, buffer, slot )
	local item = buffer:getItem( slot )
	if not item or not item.uuid or item.uuid:isNil() or ( item.quantity or 0 ) <= 0 then
		return true, "EMPTY", 0, 0, {}, nil
	end
	local itemUuid, originalQuantity = item.uuid, item.quantity
	local descriptors, failure, routeKey = self:sv_collectDepositContainers()
	if not descriptors then return false, "NETWORK_CHANGED", 0, originalQuantity, {}, failure end
	local allocation, routed, remaining = self:sv_planDepositSlot( itemUuid, originalQuantity, descriptors )
	if routed <= 0 then return true, "NO_VALID_DESTINATION", 0, originalQuantity, {}, "no accepting destination" end

	local sourceRevision = buffer:getRevision()
	if self.sv.phase3BeforeCommitHook then self.sv.phase3BeforeCommitHook( self, allocation ) end
	if buffer:getRevision() ~= sourceRevision then
		return false, "NETWORK_CHANGED", 0, originalQuantity, {}, "deposit tray changed before commit"
	end
	for _, entry in ipairs( allocation ) do
		if entry.descriptor.container:getRevision() ~= entry.revision then
			return false, "NETWORK_CHANGED", 0, originalQuantity, {},
				"destination " .. tostring( entry.descriptor.id ) .. " changed before commit"
		end
	end
	if not self.sv.testHarnessDescriptors then
		local fresh, freshFailure, freshState = self:sv_collectNetworkContainers( "collect" )
		if not fresh or descriptorKey( fresh, freshState ) ~= routeKey then
			return false, "NETWORK_CHANGED", 0, originalQuantity, {},
				freshFailure or "wireless deposit route changed before commit"
		end
	end

	if not sm.container.beginTransaction() then return false, "TRANSACTION_BUSY", 0, originalQuantity, {}, "transaction busy" end
	sm.container.spendFromSlot( buffer, slot, itemUuid, routed, true )
	for _, entry in ipairs( allocation ) do
		sm.container.collect( entry.descriptor.container, itemUuid, entry.quantity )
	end
	if not sm.container.endTransaction() then
		return false, "NETWORK_CHANGED", 0, originalQuantity, {}, "atomic deposit transaction rejected"
	end

	local touched, explanations = {}, {}
	for _, entry in ipairs( allocation ) do
		NetworkInventoryIndex.invalidate( entry.descriptor.id )
		touched[entry.descriptor.id] = entry.descriptor.original
		explanations[#explanations + 1] = tostring( entry.reason or ( "rank " .. tostring( entry.rank ) ) ) .. " -> container " ..
			tostring( entry.descriptor.id ) .. " x" .. tostring( entry.quantity )
	end
	return true, remaining > 0 and "PARTIAL" or "SORTED", routed, remaining, touched,
		table.concat( explanations, ", " )
end

function NetworkStorageChest.sv_processDepositBuffer( self )
	local buffer = self.interactable:getContainer( 0 )
	if not buffer or not sm.exists( buffer ) then
		self.sv.depositDirty = false
		self:sv_sendDepositStatus( "BUFFER_UNAVAILABLE", 0, 0, 0 )
		return
	end
	local totalMoved, totalRemaining, destinationIds = 0, 0, {}
	local touched, explanations = {}, {}
	local sawItem, sawNoDestination, conflict = false, false, nil
	for slot = 0, buffer:getSize() - 1 do
		local item = buffer:getItem( slot )
		if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
			sawItem = true
			local success, status, moved, remaining, slotTouched, detail = self:sv_routeDepositSlot( buffer, slot )
			if not success then conflict = status; explanations[#explanations + 1] = detail or status; break end
			totalMoved = totalMoved + moved
			totalRemaining = totalRemaining + remaining
			if status == "NO_VALID_DESTINATION" then sawNoDestination = true end
			for id, descriptor in pairs( slotTouched or {} ) do touched[id] = descriptor; destinationIds[id] = true end
			if detail then explanations[#explanations + 1] = detail end
		end
	end
	self.sv.lastBufferRevision = buffer:getRevision()
	if conflict then
		self.sv.depositDirty = true
		self.sv.depositRetryTick = self.sv.tick + DEPOSIT_RETRY_TICKS
		self:sv_sendDepositStatus( "NETWORK_CHANGED_RETRYING", 0, totalRemaining, 0, table.concat( explanations, "; " ) )
		return
	end
	self.sv.depositDirty = false
	local rescans = {}
	for _, descriptor in pairs( touched ) do rescans[#rescans + 1] = descriptor end
	table.sort( rescans, function( a, b ) return a.id < b.id end )
	if #rescans > 0 and hasEntries( self.sv.viewers ) then
		self:sv_startScan( rescans, "DEPOSIT" )
	end
	local status = "READY"
	if sawItem and totalRemaining == 0 then status = "SORTED"
	elseif totalMoved > 0 then status = "PARTIAL_DESTINATIONS_FULL"
	elseif sawNoDestination then status = "NO_VALID_DESTINATION" end
	self:sv_sendDepositStatus( status, totalMoved, totalRemaining, countTable( destinationIds ),
		table.concat( explanations, "; " ) )
end

function NetworkStorageChest.server_onFixedUpdate( self )
	if not self.sv then return end
	self.sv.tick = self.sv.tick + 1
	local buffer = self:sv_tryMigrateDepositBuffer()
	local bufferRevision = buffer and buffer:getRevision() or -1
	if not self.sv.depositRoutingSuspended and bufferRevision ~= self.sv.lastBufferRevision then
		self.sv.lastBufferRevision = bufferRevision
		self.sv.depositDirty = true
		self.sv.depositRetryTick = self.sv.tick + 1
	end
	if not self.sv.depositRoutingSuspended and self.sv.depositDirty and self.sv.tick >= self.sv.depositRetryTick then
		self:sv_processDepositBuffer()
	end
	if not hasEntries( self.sv.viewers ) and not self.sv.qualificationLocked then return end

	if self.sv.qualificationLocked then
		if self.sv.indexing then self:sv_processScan() end
		return
	end

	if not self.sv.testHarnessDescriptors and self.sv.tick % TOPOLOGY_POLL_INTERVAL_TICKS == 0 then
		self:sv_validateViewers()
		if not hasEntries( self.sv.viewers ) then return end
		self:sv_refreshTopology( false )
	end

	if self.sv.indexing then
		self:sv_processScan()
	elseif self.sv.tick % REVISION_POLL_INTERVAL_TICKS == 0 then
		self:sv_pollRevisions()
	end
	self:sv_processPendingWithdrawals()
	self:sv_flushCatalogSnapshot()

	if self.sv.tick % 2400 == 0 then NetworkInventoryIndex.prune( self.sv.tick, 2400 ) end
end

function NetworkStorageChest.sv_createSession( self, player )
	local key = playerKey( player )
	if not key then return nil end
	self.sv.sessionSerial = self.sv.sessionSerial + 1
	local token = table.concat( {
		tostring( self.shape:getId() ), key, tostring( self.sv.tick ),
		tostring( self.sv.sessionSerial ), tostring( math.random( 100000, 999999 ) )
	}, ":" )
	self.sv.sessions[key] = {
		token = token,
		lastRequestTick = -WITHDRAW_RATE_LIMIT_TICKS,
		lastDepositTick = -INVENTORY_DEPOSIT_RATE_LIMIT_TICKS,
		lastRoutingTick = -ROUTING_MODE_RATE_LIMIT_TICKS
	}
	return token
end

function NetworkStorageChest.sv_sendWithdrawalResult( self, player, status, moved, detail )
	if not player then return end
	self.network:sendToClient( player, "cl_n_withdrawResult", {
		status = status,
		moved = moved or 0,
		detail = detail,
		topologyGeneration = self.sv.topologyGeneration,
		contentGeneration = self.sv.contentGeneration
	} )
end

function NetworkStorageChest.sv_sendInventoryDepositResult( self, player, status, moved )
	if not player then return end
	self.network:sendToClient( player, "cl_n_inventoryDepositResult", {
		status = status,
		moved = moved or 0
	} )
	if self.sv and self.sv.depositDebug then
		sm.log.info( PHASE1_PREFIX .. "inventory deposit result: player=" ..
			tostring( playerKey( player ) or "UNKNOWN" ) .. ", status=" ..
			tostring( status ) .. ", moved=" .. tostring( moved or 0 ) )
	end
end

function NetworkStorageChest.sv_n_stageInventorySlot( self, data, player )
	local key = playerKey( player )
	local session = key and self.sv.sessions[key] or nil
	if type( data ) ~= "table" or not session or data.token ~= session.token or
		not self.sv.viewers[key] or not validViewer( player, self.shape ) then
		self:sv_sendInventoryDepositResult( player, "SESSION_EXPIRED", 0 )
		return
	end
	if self.sv.tick - ( session.lastDepositTick or -INVENTORY_DEPOSIT_RATE_LIMIT_TICKS ) <
		INVENTORY_DEPOSIT_RATE_LIMIT_TICKS then
		self:sv_sendInventoryDepositResult( player, "RATE_LIMITED", 0 )
		return
	end
	local slot = tonumber( data.slot )
	if not slot or slot ~= math.floor( slot ) then
		self:sv_sendInventoryDepositResult( player, "INVALID_REQUEST", 0 )
		return
	end
	local inventory = player:getInventory()
	local buffer = self.interactable:getContainer( 0 )
	if not inventory or not sm.exists( inventory ) or not buffer or not sm.exists( buffer ) or
		buffer:getSize() ~= EXPECTED_BUFFER_SIZE or slot < 0 or slot >= inventory:getSize() then
		self:sv_sendInventoryDepositResult( player, "INVENTORY_UNAVAILABLE", 0 )
		return
	end
	-- A connected client's replicated container revision can legitimately lag
	-- behind the server, especially while another world is being kept alive by
	-- a wireless route. Never use that client-side revision as transaction
	-- authority. The slot and UUID are only an intent hint; the server reads the
	-- current slot and the native transaction performs the final concurrency
	-- check without trusting a client-provided quantity or revision.
	local item = inventory:getItem( slot )
	local requestedUuid = safeItemUuid( data.uuid )
	if not requestedUuid or playerSlotIsEmpty( item ) or tostring( item.uuid ) ~= tostring( requestedUuid ) then
		self:sv_sendInventoryDepositResult( player, "INVENTORY_CHANGED", 0 )
		return
	end
	local authoritativeQuantity = math.max( 0, math.floor( tonumber( item.quantity ) or 0 ) )
	local moved = maxCollectableQuantity( buffer, item.uuid, authoritativeQuantity )
	if moved <= 0 then
		self:sv_sendInventoryDepositResult( player, "BUFFER_FULL", 0 )
		return
	end
	session.lastDepositTick = self.sv.tick
	if not sm.container.beginTransaction() then
		self:sv_sendInventoryDepositResult( player, "TRANSACTION_BUSY", 0 )
		return
	end
	sm.container.spendFromSlot( inventory, slot, item.uuid, moved, true )
	sm.container.collect( buffer, item.uuid, moved )
	if not sm.container.endTransaction() then
		self:sv_sendInventoryDepositResult( player, "INVENTORY_CHANGED", 0 )
		return
	end
	self.sv.depositDirty = true
	self.sv.depositRetryTick = self.sv.tick + 1
	self:sv_sendInventoryDepositResult( player, "SUCCESS", moved )
end

function NetworkStorageChest.sv_findSnapshotEntry( self, uuidString )
	for _, entry in ipairs( self.sv.snapshot and self.sv.snapshot.entries or {} ) do
		if entry.uuid == uuidString then return entry end
	end
	return nil
end

function NetworkStorageChest.sv_refreshAfterWithdrawalConflict( self, descriptors, reason )
	for _, descriptor in ipairs( descriptors or {} ) do NetworkInventoryIndex.invalidate( descriptor.id ) end
	self:sv_startScan( descriptors or self.sv.containers, reason or "WITHDRAWAL_RETRY" )
end

function NetworkStorageChest.sv_orderWithdrawalDescriptors( self, descriptors, itemUuid )
	local wanted = tostring( itemUuid )
	local indexed, fallback = {}, {}
	for _, descriptor in ipairs( descriptors or {} ) do
		local record = self.sv.records[descriptor.id]
		local entry = record and record.byUuid and record.byUuid[wanted] or nil
		local candidate = { descriptor = descriptor, indexedQuantity = entry and entry.quantity or 0 }
		if candidate.indexedQuantity > 0 then indexed[#indexed + 1] = candidate
		else fallback[#fallback + 1] = candidate end
	end
	local function less( a, b )
		local aPriority = a.descriptor.routePriority or ( a.descriptor.wireless and 1 or 0 )
		local bPriority = b.descriptor.routePriority or ( b.descriptor.wireless and 1 or 0 )
		if aPriority ~= bPriority then return aPriority < bPriority end
		if a.indexedQuantity ~= b.indexedQuantity then return a.indexedQuantity > b.indexedQuantity end
		if ( a.descriptor.routeDistance or 0 ) ~= ( b.descriptor.routeDistance or 0 ) then
			return ( a.descriptor.routeDistance or 0 ) < ( b.descriptor.routeDistance or 0 )
		end
		return a.descriptor.id < b.descriptor.id
	end
	table.sort( indexed, less )
	table.sort( fallback, less )
	local ordered = {}
	for _, candidate in ipairs( indexed ) do ordered[#ordered + 1] = candidate.descriptor end
	for _, candidate in ipairs( fallback ) do ordered[#ordered + 1] = candidate.descriptor end
	return ordered
end

function NetworkStorageChest.sv_collectWithdrawalSources( self, descriptors, itemUuid, required, scanAll )
	local wanted = tostring( itemUuid )
	local sources, total, scanFailed = {}, 0, false
	for _, descriptor in ipairs( descriptors or {} ) do
		local ok, descriptorSources = pcall( function()
			local found = {}
			local container = descriptor.container
			if not container or not sm.exists( container ) then return found end
			for slot = 0, container:getSize() - 1 do
				local item = container:getItem( slot )
				if item and item.uuid and tostring( item.uuid ) == wanted and ( item.quantity or 0 ) > 0 then
					found[#found + 1] = {
						container = container, descriptor = descriptor, slot = slot,
						quantity = item.quantity
					}
				end
			end
			table.sort( found, function( a, b )
				if a.quantity ~= b.quantity then return a.quantity > b.quantity end
				return a.slot < b.slot
			end )
			return found
		end )
		if ok then
			for _, source in ipairs( descriptorSources ) do
				sources[#sources + 1] = source
				total = total + source.quantity
			end
		else
			scanFailed = true
		end
		if not scanAll and total >= required then break end
	end
	return sources, total, scanFailed
end

function NetworkStorageChest.sv_executeLocalWithdrawal( self, itemUuid, action, destination, descriptors )
	if ( self.sv.indexing and self.sv.scanBlocking ) or
			not self.sv.snapshot or self.sv.snapshot.status ~= "READY" then
		return false, "INDEXING", 0, nil, true, false
	end

	local wanted = 1
	if action == "TAKE_STACK" then
		local ok, stackSize = pcall( sm.item.getStackSize, itemUuid )
		wanted = ok and type( stackSize ) == "number" and math.max( 1, stackSize ) or 1
	end
	local scanAll = action == "TAKE_ALL"
	if not scanAll then
		wanted = maxCollectableQuantity( destination, itemUuid, wanted )
		if wanted <= 0 then return false, "INVENTORY_FULL", 0, nil, false, true end
	end
	local ordered = self:sv_orderWithdrawalDescriptors( descriptors, itemUuid )
	local sources, total, scanFailed = self:sv_collectWithdrawalSources( ordered, itemUuid, wanted, scanAll )
	if scanFailed and ( scanAll or total <= 0 ) then
		self.sv.withdrawalStats.topologyWaits = self.sv.withdrawalStats.topologyWaits + 1
		self:sv_refreshTopology( true )
		return false, "NETWORK_REFRESHING", 0, "SOURCE CONTAINER CHANGED", true, false
	end
	if total <= 0 then return false, "ITEM_UNAVAILABLE", 0, nil, false, true end
	if scanAll then wanted = total end
	wanted = math.min( wanted, total )
	local quantity = maxCollectableQuantity( destination, itemUuid, wanted )
	if quantity <= 0 then return false, "INVENTORY_FULL", 0, nil, false, true end

	local allocation, remaining, touched = {}, quantity, {}
	for _, source in ipairs( sources ) do
		if remaining <= 0 then break end
		local take = math.min( source.quantity, remaining )
		allocation[#allocation + 1] = { source = source, quantity = take }
		touched[source.descriptor.id] = source.descriptor
		remaining = remaining - take
	end
	if remaining ~= 0 then
		return false, "ITEM_MOVED", 0, nil, true, true
	end

	if not sm.container.beginTransaction() then
		return false, "TRANSACTION_BUSY", 0, nil, true, true
	end
	for _, entry in ipairs( allocation ) do
		sm.container.spendFromSlot( entry.source.container, entry.source.slot, itemUuid, entry.quantity, true )
	end
	sm.container.collect( destination, itemUuid, quantity )
	if not sm.container.endTransaction() then
		local retry = {}
		for _, descriptor in pairs( touched ) do retry[#retry + 1] = descriptor end
		table.sort( retry, function( a, b ) return a.id < b.id end )
		self:sv_refreshAfterWithdrawalConflict( retry, "WITHDRAWAL_RETRY" )
		return false, "ITEM_MOVED", 0, nil, true, true
	end

	local rescans = {}
	for _, descriptor in pairs( touched ) do
		NetworkInventoryIndex.invalidate( descriptor.id )
		rescans[#rescans + 1] = descriptor
	end
	table.sort( rescans, function( a, b ) return a.id < b.id end )
	self:sv_startScan( rescans, "WITHDRAWAL" )
	return true, "SUCCESS", quantity, nil, false, true
end

function NetworkStorageChest.sv_sendWithdrawalProgress( self, job, reason )
	if not job or not job.player then return end
	self.network:sendToClient( job.player, "cl_n_withdrawProgress", {
		status = "RETRYING", reason = reason,
		attempt = job.attempts, limit = WITHDRAW_RETRY_LIMIT
	} )
end

function NetworkStorageChest.sv_completeWithdrawalJob( self, job, status, moved, detail )
	if not job or self.sv.pendingWithdrawals[job.playerKey] ~= job then return end
	self.sv.pendingWithdrawals[job.playerKey] = nil
	if status == "SUCCESS" then self.sv.withdrawalStats.successes = self.sv.withdrawalStats.successes + 1
	else self.sv.withdrawalStats.failures = self.sv.withdrawalStats.failures + 1 end
	self:sv_sendWithdrawalResult( job.player, status, moved, detail )
	self:sv_publishDiagnostics( self.sv.indexing and
		( self.sv.scanBlocking and "INDEXING" or "REFRESHING" ) or "READY", 0, 0, 0 )
end

function NetworkStorageChest.sv_finalWithdrawalStatus( self, reason )
	if reason == "NETWORK_REFRESHING" or reason == "NETWORK_OFFLINE" or reason == "INDEXING" then
		return "ROUTE_UNAVAILABLE"
	end
	if reason == "TRANSACTION_BUSY" then return "STORAGE_BUSY" end
	return "ITEM_IN_USE"
end

function NetworkStorageChest.sv_tryWithdrawalJob( self, job )
	if self.sv.indexing and self.sv.scanBlocking then
		return false, "INDEXING", 0, nil, true, false
	end
	local descriptors, _, _, topologyFailure, currentTopologyKey = self:sv_collectTopologySnapshot()
	if not descriptors then
		return false, "NETWORK_OFFLINE", 0, topologyFailure, true, false
	end
	if currentTopologyKey ~= self.sv.topologyKey then
		self.sv.withdrawalStats.topologyWaits = self.sv.withdrawalStats.topologyWaits + 1
		self:sv_refreshTopology( true )
		return false, "NETWORK_REFRESHING", 0, nil, true, false
	end
	return self:sv_executeLocalWithdrawal( job.itemUuid, job.action, job.destination, descriptors )
end

function NetworkStorageChest.sv_runWithdrawalJob( self, job )
	if not job or self.sv.pendingWithdrawals[job.playerKey] ~= job or
			self.sv.tick < ( job.nextAttemptTick or 0 ) then return end
	local session = self.sv.sessions[job.playerKey]
	if not session or session.token ~= job.sessionToken or
			not self.sv.viewers[job.playerKey] or not validViewer( job.player, self.shape ) then
		self:sv_completeWithdrawalJob( job, "SESSION_EXPIRED", 0 )
		return
	end
	local inventory = job.player:getInventory()
	if not inventory or not sm.exists( inventory ) then
		self:sv_completeWithdrawalJob( job, "INVENTORY_UNAVAILABLE", 0 )
		return
	end
	job.destination = inventory
	local success, status, moved, detail, retryable, consumesAttempt = self:sv_tryWithdrawalJob( job )
	if success then
		self:sv_completeWithdrawalJob( job, "SUCCESS", moved, detail )
		return
	end
	if consumesAttempt then job.attempts = job.attempts + 1 end
	job.lastReason = status
	if status == "TRANSACTION_BUSY" then
		self.sv.withdrawalStats.transactionBusy = self.sv.withdrawalStats.transactionBusy + 1
	elseif status == "ITEM_MOVED" then
		self.sv.withdrawalStats.slotConflicts = self.sv.withdrawalStats.slotConflicts + 1
	end
	if not retryable then
		self:sv_completeWithdrawalJob( job, status, moved, detail )
		return
	end
	-- WITHDRAW_RETRY_LIMIT counts retries after the initial attempt. A successful
	-- commit always completes the job above, so it is never replayed.
	if self.sv.tick >= job.deadlineTick or job.attempts > WITHDRAW_RETRY_LIMIT then
		self:sv_completeWithdrawalJob( job, self:sv_finalWithdrawalStatus( status ), 0, detail )
		return
	end
	self.sv.withdrawalStats.retries = self.sv.withdrawalStats.retries + 1
	local delayIndex = math.max( 1, math.min( #WITHDRAW_RETRY_DELAYS, job.attempts ) )
	job.nextAttemptTick = self.sv.tick + WITHDRAW_RETRY_DELAYS[delayIndex]
	if not job.progressSent then
		job.progressSent = true
		self:sv_sendWithdrawalProgress( job, status )
	end
end

function NetworkStorageChest.sv_processPendingWithdrawals( self )
	local jobs = {}
	for _, job in pairs( self.sv.pendingWithdrawals or {} ) do jobs[#jobs + 1] = job end
	table.sort( jobs, function( a, b ) return a.serial < b.serial end )
	for _, job in ipairs( jobs ) do self:sv_runWithdrawalJob( job ) end
end

function NetworkStorageChest.sv_validateWithdrawalRequest( self, data, player )
	local key = playerKey( player )
	local session = key and self.sv.sessions[key] or nil
	if type( data ) ~= "table" or not session or data.token ~= session.token then
		return nil, nil, "SESSION_EXPIRED"
	end
	if not self.sv.viewers[key] or not validViewer( player, self.shape ) then
		self.sv.sessions[key], self.sv.viewers[key] = nil, nil
		return nil, nil, "SESSION_EXPIRED"
	end
	local tick = self.sv.tick
	if tick - session.lastRequestTick < WITHDRAW_RATE_LIMIT_TICKS then
		return nil, nil, "RATE_LIMITED"
	end
	if not WITHDRAW_ACTIONS[data.action] then return nil, nil, "INVALID_REQUEST" end
	local itemUuid = safeItemUuid( data.uuid )
	if not itemUuid then return nil, nil, "INVALID_REQUEST" end
	-- Catalog and topology generations are display hints. The client requests
	-- only an item UUID and action; the server resolves the current reachable
	-- sources and native slot transactions remain the sole transfer authority.
	session.lastRequestTick = tick
	local inventory = player:getInventory()
	if not inventory or not sm.exists( inventory ) then return nil, nil, "INVENTORY_UNAVAILABLE" end
	return itemUuid, inventory, nil
end

function NetworkStorageChest.sv_n_withdraw( self, data, player )
	local itemUuid, inventory, rejection = self:sv_validateWithdrawalRequest( data, player )
	if rejection then
		self:sv_sendWithdrawalResult( player, rejection, 0 )
		if rejection == "STALE_CATALOG" and self.sv.snapshot then
			self.network:sendToClient( player, "cl_n_catalogSnapshot", self.sv.snapshot )
		end
		return
	end
	local key = playerKey( player )
	local existing = key and self.sv.pendingWithdrawals[key] or nil
	if existing then
		self:sv_sendWithdrawalProgress( existing, existing.lastReason or "RETRYING" )
		return
	end
	self.sv.withdrawalSerial = self.sv.withdrawalSerial + 1
	self.sv.withdrawalStats.requests = self.sv.withdrawalStats.requests + 1
	local session = self.sv.sessions[key]
	local job = {
		serial = self.sv.withdrawalSerial,
		player = player, playerKey = key, sessionToken = session.token,
		itemUuid = itemUuid, action = data.action, destination = inventory,
		startedTick = self.sv.tick, deadlineTick = self.sv.tick + WITHDRAW_RETRY_WINDOW_TICKS,
		nextAttemptTick = self.sv.tick, attempts = 0, progressSent = false
	}
	self.sv.pendingWithdrawals[key] = job
	self:sv_runWithdrawalJob( job )
end

function NetworkStorageChest.sv_n_openCatalog( self, _, player )
	if not validViewer( player, self.shape ) then
		if self.sv.phase1QualificationToken then
			g_scrapLabStoragePhase1ClientQualification = g_scrapLabStoragePhase1ClientQualification or {}
			g_scrapLabStoragePhase1ClientQualification[self.sv.phase1QualificationToken] = {
				stage = "OPEN_REJECTED", playerId = player and tostring( player.id ) or "MISSING"
			}
			sm.log.warning( PHASE1_PREFIX .. "qualification client open request rejected: player is missing or too far from terminal" )
		end
		return
	end
	local key = playerKey( player )
	self.sv.viewers[key] = player
	local buffer = self.interactable:getContainer( 0 )
	if buffer and not buffer:isEmpty() and not self.sv.depositRoutingSuspended then
		self.sv.depositDirty = true
		self.sv.depositRetryTick = self.sv.tick + 1
	end
	local sessionToken = self:sv_createSession( player )
	self.network:sendToClient( player, "cl_n_sessionState", {
		token = sessionToken, phase = 2, smartRouting = self.sv.smartRouting ~= false
	} )
	self.network:sendToClient( player, "cl_n_depositStatus", {
		status = self.sv.depositStatus or "READY", moved = 0, remaining = 0, destinations = 0
	} )
	self:sv_publishDiagnostics( self.sv.indexing and "INDEXING" or "READY", 0, 0, 0 )
	if self.sv.phase1QualificationToken then
		g_scrapLabStoragePhase1ClientQualification = g_scrapLabStoragePhase1ClientQualification or {}
		g_scrapLabStoragePhase1ClientQualification[self.sv.phase1QualificationToken] = {
			stage = "OPEN_REQUEST", playerId = tostring( player.id )
		}
		sm.log.info( PHASE1_PREFIX .. "qualification client open request received" )
	end
	if self.sv.snapshot then self.network:sendToClient( player, "cl_n_catalogSnapshot", self.sv.snapshot ) end
	if self.sv.testHarnessDescriptors then return end
	if not self.sv.topologyInitialized or not self.sv.snapshot or self.sv.needsRescan then
		self:sv_refreshTopology( true )
	else
		self:sv_refreshTopology( false )
		if self.sv.indexing and self.sv.scanBlocking then
			self.network:sendToClient( player, "cl_n_catalogSnapshot", self:sv_indexingPayload() )
		end
	end
end

function NetworkStorageChest.sv_n_setRoutingMode( self, data, player )
	local key = playerKey( player )
	local session = key and self.sv.sessions[key] or nil
	if type( data ) ~= "table" or not session or data.token ~= session.token or
		not self.sv.viewers[key] or not validViewer( player, self.shape ) then
		if player then self.network:sendToClient( player, "cl_n_routingModeState", {
			success = false, status = "SESSION_EXPIRED", smartRouting = self.sv.smartRouting ~= false
		} ) end
		return
	end
	local enabled = data.smartRouting == true
	if self.sv.tick - ( session.lastRoutingTick or -ROUTING_MODE_RATE_LIMIT_TICKS ) <
		ROUTING_MODE_RATE_LIMIT_TICKS then
		self.network:sendToClient( player, "cl_n_routingModeState", {
			success = false, status = "RATE_LIMITED", smartRouting = self.sv.smartRouting ~= false
		} )
		return
	end
	session.lastRoutingTick = self.sv.tick
	if enabled == ( self.sv.smartRouting ~= false ) then
		self.network:sendToClient( player, "cl_n_routingModeState", {
			success = true, status = "READY", smartRouting = enabled
		} )
		return
	end
	self.sv.smartRouting = enabled
	self.sv.stored = self.sv.stored or {}
	self.sv.stored.smartRouting = enabled
	self.storage:save( self.sv.stored )
	local buffer = self.interactable:getContainer( 0 )
	if buffer and not buffer:isEmpty() and not self.sv.depositRoutingSuspended then
		self.sv.depositDirty = true
		self.sv.depositRetryTick = self.sv.tick + 1
	end
	self:sv_updateClientData()
	self.network:sendToClient( player, "cl_n_routingModeState", {
		success = true, status = "READY", smartRouting = enabled
	} )
	self:sv_publishDiagnostics( self.sv.indexing and "INDEXING" or "READY", 0, 0, 0 )
end

function NetworkStorageChest.sv_setDepositDebug( self, enabled )
	self.sv.depositDebug = enabled == true
	self:sv_publishDiagnostics( self.sv.indexing and "INDEXING" or "READY", 0, 0, 0 )
	return self.sv.depositDebug
end

function NetworkStorageChest.sv_e_startPhase1ClientQualification( self, params )
	if not params or not params.playerId or not params.token then return end
	local targetPlayer = nil
	for _, candidate in ipairs( sm.player.getAllPlayers() ) do
		if tostring( candidate.id ) == tostring( params.playerId ) then targetPlayer = candidate; break end
	end
	if not targetPlayer then
		sm.log.warning( PHASE1_PREFIX .. "qualification target player could not be resolved" )
		return
	end
	local token = tostring( params.token )
	self.sv.phase1QualificationToken = token
	g_scrapLabStoragePhase1ClientQualification = g_scrapLabStoragePhase1ClientQualification or {}
	g_scrapLabStoragePhase1ClientQualification[token] = { stage = "SERVER_EVENT", playerId = tostring( targetPlayer.id ) }
	sm.log.info( PHASE1_PREFIX .. "qualification server event reached terminal" )
	self.network:sendToClient( targetPlayer, "cl_n_startPhase1ClientQualification", {
		token = token,
		expectedContainers = params.expectedContainers or 0,
		expectedQuantity = params.expectedQuantity or 0
	} )
end

function NetworkStorageChest.sv_n_phase1ClientQualificationAck( self, data, player )
	if not data or not data.token or not player then return end
	g_scrapLabStoragePhase1ClientQualification = g_scrapLabStoragePhase1ClientQualification or {}
	g_scrapLabStoragePhase1ClientQualification[tostring( data.token )] = {
		stage = "COMPLETE",
		playerId = tostring( player.id ),
		valid = data.valid == true,
		containerCount = data.containerCount or -1,
		totalQuantity = data.totalQuantity or -1,
		entryCount = data.entryCount or -1
	}
	self.sv.phase1QualificationToken = nil
	sm.log.info( PHASE1_PREFIX .. "qualification catalog snapshot acknowledged by client" )
end

function NetworkStorageChest.sv_e_startPhase5ClientQualification( self, params )
	if not params or not params.playerId or not params.token then return end
	local targetPlayer = nil
	for _, candidate in ipairs( sm.player.getAllPlayers() ) do
		if tostring( candidate.id ) == tostring( params.playerId ) then targetPlayer = candidate; break end
	end
	if not targetPlayer then return end
	self.network:sendToClient( targetPlayer, "cl_n_runPhase5UiQualification", {
		token = tostring( params.token )
	} )
end

function NetworkStorageChest.sv_n_phase5ClientQualificationAck( self, data, player )
	if not data or not data.token or not player then return end
	g_scrapLabStoragePhase5Qualification = g_scrapLabStoragePhase5Qualification or {}
	g_scrapLabStoragePhase5Qualification[tostring( data.token )] = {
		complete = true, playerId = tostring( player.id ), results = data.results or {}
	}
end

function NetworkStorageChest.sv_n_closeCatalog( self, _, player )
	if player then
		local key = playerKey( player )
		self.sv.viewers[key] = nil
		self.sv.sessions[key] = nil
		self.sv.pendingWithdrawals[key] = nil
	end
	if not hasEntries( self.sv.viewers ) then
		self.sv.pendingWithdrawals = {}
		self.sv.indexing = false
		self.sv.scanQueue = {}
		self.sv.pendingScanDescriptors = {}
		self.sv.pendingScanReason = nil
		self.sv.pendingCatalogSnapshot = nil
		self:sv_publishDiagnostics( "IDLE", 0, 0, 0 )
	end
end

-- Client --------------------------------------------------------------------

function NetworkStorageChest.client_onCreate( self )
	sm.log.info( PHASE1_PREFIX .. "real local-network catalog client ready" )
	self.cl = {
		gui = nil,
		guiData = nil,
		scrollView = nil,
		inventoryScrollView = nil,
		playerInventory = nil,
		playerInventorySlots = {},
		playerInventoryVisibleCount = 0,
		serverState = { phase = 2, bufferReady = false, bufferSize = 0 },
		catalog = {},
		catalogSignature = nil,
		catalogTopologyGeneration = -1,
		catalogState = {
			status = "OFFLINE", uniqueItems = 0, totalQuantity = 0,
			totalStacks = 0, containerCount = 0, topologyGeneration = 0,
			contentGeneration = 0, localOnly = true, wirelessState = "LOCAL_ONLY",
			reachableWorlds = 1, worlds = {}
		},
		search = "",
		sortMode = 1,
		typeFilter = 1,
		selected = nil,
		visibleCount = 0,
		guiDirty = false,
		lastBufferRevision = -1,
		lastInventoryRevision = -1,
		sessionOpen = false,
		sessionToken = nil,
		withdrawBusy = false,
		withdrawStatus = nil,
		inventoryDepositBusy = false,
		inventoryDepositStatus = nil,
		inventoryDepositStatusUntil = 0,
		routingBusy = false,
		legacyBuffer = false,
		depositState = { status = "READY", moved = 0, remaining = 0 }
	}
end

function NetworkStorageChest.client_onClientDataUpdate( self, data )
	self.cl = self.cl or {}
	self.cl.serverState = data or self.cl.serverState
	if self.cl.guiData then
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
end

function NetworkStorageChest.client_onDestroy( self )
	self:cl_destroyGui()
end

function NetworkStorageChest.client_onUpdate( self )
	if not self.cl or not self.cl.gui then return end
	local buffer = self.interactable:getContainer( 0 )
	local revision = buffer and buffer:getRevision() or -1
	if revision ~= self.cl.lastBufferRevision then
		self.cl.lastBufferRevision = revision
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
	local inventory = self.cl.playerInventory
	local inventoryRevision = inventory and inventory:getRevision() or -1
	if inventoryRevision ~= self.cl.lastInventoryRevision then
		self.cl.lastInventoryRevision = inventoryRevision
		self:cl_rebuildPlayerInventory( false )
	end
	if self.cl.inventoryDepositStatus and ( self.cl.inventoryDepositStatusUntil or 0 ) > 0 and
			sm.game.getCurrentTick() >= self.cl.inventoryDepositStatusUntil then
		self.cl.inventoryDepositStatus = nil
		self.cl.inventoryDepositStatusUntil = 0
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
	if self.cl.guiDirty then
		self.cl.guiDirty = false
		self.cl.gui:render( self.cl.guiData )
		self:cl_applyRoutingButtonState()
	end
end

function NetworkStorageChest.client_canInteract( self )
	sm.gui.setInteractionText( "", sm.gui.getKeyBinding( "Use", true ), localizedText( "openInteraction" ) )
	return true
end

function NetworkStorageChest.client_onInteract( self, _, state )
	if state then self:cl_openGui() end
end

function NetworkStorageChest.cl_applyLocalization( self )
	if not self.cl or not self.cl.guiData then return end
	local labels = {
		Title = "title", ClearSearch = "clear", SelectedLabel = "selectedItem",
		InventoryLabel = "playerInventory",
		DepositHelp = "depositHelp", TakeOneButton = "takeOne",
		TakeStackButton = "takeStack", TakeAllButton = "takeAll"
	}
	for widgetName, key in pairs( labels ) do
		findRequiredWidget( self.cl.guiData, widgetName ).Caption = localizedText( key )
	end
	findRequiredWidget( self.cl.guiData, "DepositLabel" ).Caption = localizedText( "depositBuffer3" )
	findRequiredWidget( self.cl.guiData, "SelectedName" ).Caption = localizedText( "none" )
	findRequiredWidget( self.cl.guiData, "SelectionStatus" ).Caption = localizedText( "selectPrompt" )
	findRequiredWidget( self.cl.guiData, "TypeFilterButton" ).ToolTip.Text = localizedText( "filterHint" )
end

function NetworkStorageChest.cl_openGui( self )
	self:cl_destroyGui()
	local buffer = self.interactable:getContainer( 0 )
	local inventory = sm.localPlayer.getInventory()
	if not buffer or not inventory then
		sm.gui.displayAlertText( localizedText( "bufferUnavailable" ), 4 )
		return
	end
	if buffer:getSize() ~= EXPECTED_BUFFER_SIZE and buffer:getSize() ~= LEGACY_BUFFER_SIZE then
		sm.gui.displayAlertText( localizedText( "legacyBuffer" ), 5 )
		return
	end

	local guiData = DeepCopy( GUI_TEMPLATE )
	ReplaceSubLayouts( guiData )
	local depositBox = findRequiredWidget( guiData, "DepositBox" )
	depositBox.ContainerData.ContainerId = buffer.id
	depositBox.ContainerData.DropContainerIds = { inventory.id }
	local legacyBuffer = buffer:getSize() ~= EXPECTED_BUFFER_SIZE
	depositBox.width = buffer:getSize() * 64
	depositBox.ContainerData.ContainerWidth = buffer:getSize() * 64
	findRequiredWidget( guiData, "RoutingModeButton" ).Visible = not legacyBuffer

	local gui = sm.jsonGui.createGui( { isInteractive = true, bNeedsCursor = true, hidesHotbar = true } )
	self.cl.gui = gui
	self.cl.guiData = guiData
	self:cl_applyLocalization()
	self.cl.scrollView = GridScrollView()
	self.cl.scrollView:setup( findRequiredWidget( guiData, "CatalogScrollHost" ), gui )
	self.cl.scrollView.widgets.scrollView.x = 0
	self.cl.scrollView.widgets.scrollView.width = 660
	self.cl.scrollView.widgets.scrollBar.x = 662
	self.cl.scrollView:setGridItemSize( 66, 66 )
	self.cl.scrollView:setScrollStrength( 1 )
	self.cl.inventoryScrollView = GridScrollView()
	self.cl.inventoryScrollView:setup( findRequiredWidget( guiData, "PlayerInventoryScrollHost" ), gui )
	local inventoryWidgets = self.cl.inventoryScrollView.widgets
	inventoryWidgets.mainPanel.Name = "PlayerInventoryScrollMain"
	inventoryWidgets.scrollView.Name = "PlayerInventoryScrollGrid"
	inventoryWidgets.scrollBar.Name = "PlayerInventoryScrollBar"
	inventoryWidgets.scrollButton.Name = "PlayerInventoryScrollButton"
	inventoryWidgets.mainPanel.onMouseWheel = "cl_onPlayerInventoryMouseWheel"
	inventoryWidgets.scrollView.onMouseWheel = "cl_onPlayerInventoryMouseWheel"
	inventoryWidgets.scrollBar.onPressed = "cl_onPlayerInventoryScrollBarPressed"
	inventoryWidgets.scrollButton.onMouseWheel = "cl_onPlayerInventoryMouseWheel"
	inventoryWidgets.scrollButton.onPressed = "cl_onPlayerInventoryScrollButtonPressed"
	inventoryWidgets.scrollButton.onReleased = "cl_onPlayerInventoryScrollButtonReleased"
	inventoryWidgets.scrollButton.onDrag = "cl_onPlayerInventoryScrollButtonDrag"
	inventoryWidgets.mainPanel.x, inventoryWidgets.mainPanel.y = 0, 0
	inventoryWidgets.mainPanel.width, inventoryWidgets.mainPanel.height = 342, 256
	inventoryWidgets.scrollView.x, inventoryWidgets.scrollView.y = 0, 0
	inventoryWidgets.scrollView.width, inventoryWidgets.scrollView.height = 320, 256
	inventoryWidgets.scrollBar.x, inventoryWidgets.scrollBar.y = 324, 0
	inventoryWidgets.scrollBar.width, inventoryWidgets.scrollBar.height = 18, 256
	self.cl.inventoryScrollView.scrollBarLength = 256 - inventoryWidgets.scrollButton.height
	self.cl.inventoryScrollView:setGridItemSize( 64, 64 )
	self.cl.inventoryScrollView:setScrollStrength( 1 )
	self.cl.playerInventory = inventory
	self.cl.playerInventorySlots = {}
	self.cl.playerInventoryVisibleCount = 0
	self.cl.search = ""
	self.cl.sortMode = 1
	self.cl.typeFilter = 1
	self.cl.selected = nil
	self.cl.sessionToken = nil
	self.cl.withdrawBusy = false
	self.cl.inventoryDepositBusy = false
	self.cl.inventoryDepositStatus = nil
	self.cl.inventoryDepositStatusUntil = 0
	self.cl.routingBusy = false
	self.cl.legacyBuffer = legacyBuffer
	self.cl.withdrawStatus = nil
	self.cl.catalogState.status = "INDEXING"
	self.cl.lastBufferRevision = buffer:getRevision()
	self.cl.lastInventoryRevision = inventory:getRevision()
	self.cl.sessionOpen = true

	self:cl_rebuildCatalog( true )
	self:cl_rebuildPlayerInventory( true )
	self:cl_refreshStatus()
	gui:render( guiData )
	self:cl_applyRoutingButtonState()
	self.network:sendToServer( "sv_n_openCatalog" )
end

function NetworkStorageChest.cl_endServerSession( self )
	if self.cl and self.cl.sessionOpen then
		self.cl.sessionOpen = false
		self.network:sendToServer( "sv_n_closeCatalog" )
	end
end

function NetworkStorageChest.cl_destroyGui( self )
	if not self.cl then return end
	self:cl_endServerSession()
	local gui = self.cl.gui
	self.cl.gui = nil
	self.cl.guiData = nil
	self.cl.scrollView = nil
	self.cl.inventoryScrollView = nil
	self.cl.playerInventory = nil
	self.cl.playerInventorySlots = {}
	self.cl.playerInventoryVisibleCount = 0
	self.cl.guiDirty = false
	self.cl.sessionToken = nil
	self.cl.withdrawBusy = false
	self.cl.inventoryDepositBusy = false
	self.cl.inventoryDepositStatus = nil
	self.cl.inventoryDepositStatusUntil = 0
	self.cl.routingBusy = false
	self.cl.legacyBuffer = false
	if gui and sm.exists( gui ) and gui:isActive() then gui:close() end
end

function NetworkStorageChest.cl_onGuiClosed( self )
	if not self.cl then return end
	self:cl_endServerSession()
	self.cl.gui = nil
	self.cl.guiData = nil
	self.cl.scrollView = nil
	self.cl.inventoryScrollView = nil
	self.cl.playerInventory = nil
	self.cl.playerInventorySlots = {}
	self.cl.playerInventoryVisibleCount = 0
	self.cl.guiDirty = false
	self.cl.sessionToken = nil
	self.cl.withdrawBusy = false
	self.cl.inventoryDepositBusy = false
	self.cl.inventoryDepositStatus = nil
	self.cl.inventoryDepositStatusUntil = 0
	self.cl.routingBusy = false
	self.cl.legacyBuffer = false
end

function NetworkStorageChest.cl_n_sessionState( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	self.cl.sessionToken = data.token
	if data.smartRouting ~= nil then self.cl.serverState.smartRouting = data.smartRouting == true end
	self.cl.withdrawBusy = false
	self.cl.routingBusy = false
	self:cl_refreshStatus()
	if self.cl.guiData then self.cl.guiDirty = true end
end

function NetworkStorageChest.cl_n_depositStatus( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	self.cl.depositState = data
	if self.cl.guiData then
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
end

function NetworkStorageChest.cl_n_catalogSnapshot( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	local sameCatalog = data.status == "READY" and data.contentSignature ~= nil and
		data.contentSignature == self.cl.catalogSignature and
		data.topologyGeneration == self.cl.catalogTopologyGeneration
	local rebuildCatalog = data.status ~= "INDEXING" and not sameCatalog
	local selectedUuid = self.cl.selected and self.cl.catalog[self.cl.selected]
		and self.cl.catalog[self.cl.selected].uuid or nil
	if rebuildCatalog then
		local catalog = {}
		for _, source in ipairs( data.entries or {} ) do
			if source.uuid and ( source.quantity or 0 ) > 0 then
				local itemUuid = sm.uuid.new( source.uuid )
				local title = sm.shape.getShapeTitle( itemUuid ) or source.uuid
				local _, _, _, _, itemType = sm.gui.getItemIconFromUuid( itemUuid )
				catalog[#catalog + 1] = {
					uuid = source.uuid,
					itemUuid = itemUuid,
					title = title,
					searchTitle = string.lower( title ),
					itemType = normalizeItemType( itemType ),
					quantity = source.quantity,
					stacks = source.stacks or 0,
					sources = source.sources or 0,
					localSources = source.localSources or 0,
					wirelessSources = source.wirelessSources or 0,
					crossWorldSources = source.crossWorldSources or 0
				}
			end
		end
		self.cl.catalog = catalog
		self.cl.selected = nil
		if selectedUuid then
			for index, entry in ipairs( catalog ) do
				if entry.uuid == selectedUuid then self.cl.selected = index; break end
			end
		end
	end
	self.cl.catalogState = data
	if data.status == "READY" then
		self.cl.catalogSignature = data.contentSignature
		self.cl.catalogTopologyGeneration = data.topologyGeneration or -1
	end
	if self.cl.guiData and self.cl.scrollView then
		if rebuildCatalog then self:cl_rebuildCatalog( false )
		else self:cl_refreshWithdrawalControls() end
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
	if self.cl.phase1Qualification and data.status == "READY" then
		local probe = self.cl.phase1Qualification
		self.cl.phase1Qualification = nil
		self.network:sendToServer( "sv_n_phase1ClientQualificationAck", {
			token = probe.token,
			valid = ( data.containerCount or -1 ) == probe.expectedContainers and
				( data.totalQuantity or -1 ) == probe.expectedQuantity,
			containerCount = data.containerCount or -1,
			totalQuantity = data.totalQuantity or -1,
			entryCount = #( data.entries or {} )
		} )
	end
end

function NetworkStorageChest.cl_getCatalogScrollOffset( self )
	local scrollView = self.cl and self.cl.scrollView
	local widget = scrollView and scrollView.widgets and scrollView.widgets.scrollView
	return widget and tonumber( widget.GridScrollOffset.top ) or 0
end

function NetworkStorageChest.cl_resetCatalogScroll( self )
	local scrollView = self.cl.scrollView
	scrollView:clearGrid()
	scrollView:resetScroll()
	scrollView.allowedScrollRange = 0
	scrollView.widgets.scrollBar.Visible = false
	scrollView.widgets.scrollButton.Visible = false
end

function NetworkStorageChest.cl_restoreCatalogScroll( self, offset )
	local scrollView = self.cl.scrollView
	local top = clamp( tonumber( offset ) or 0, scrollView.allowedScrollRange, 0 )
	scrollView.widgets.scrollView.GridScrollOffset.top = top
	if scrollView.allowedScrollRange < 0 then
		scrollView.widgets.scrollButton.y = math.floor( lerp( 0, scrollView.scrollBarLength,
			top / scrollView.allowedScrollRange ) )
	else
		scrollView.widgets.scrollButton.y = 0
	end
end

local function setMouseWheelCallback( widget, callbackName )
	if type( widget ) ~= "table" then return end
	if widget.onMouseWheel then widget.onMouseWheel = callbackName end
	for _, child in ipairs( widget.Childs or {} ) do setMouseWheelCallback( child, callbackName ) end
end

local function hotbarBinding( slot )
	if slot < 0 or slot > 9 then return "" end
	return slot == 9 and "0" or tostring( slot + 1 )
end

function NetworkStorageChest.cl_getPlayerInventoryScrollOffset( self )
	local view = self.cl and self.cl.inventoryScrollView
	local widget = view and view.widgets and view.widgets.scrollView
	return widget and tonumber( widget.GridScrollOffset.top ) or 0
end

function NetworkStorageChest.cl_resetPlayerInventoryScroll( self )
	local view = self.cl.inventoryScrollView
	view:clearGrid()
	view:resetScroll()
	view.allowedScrollRange = 0
	view.widgets.scrollBar.Visible = false
	view.widgets.scrollButton.Visible = false
end

function NetworkStorageChest.cl_restorePlayerInventoryScroll( self, offset )
	local view = self.cl.inventoryScrollView
	local top = clamp( tonumber( offset ) or 0, view.allowedScrollRange, 0 )
	view.widgets.scrollView.GridScrollOffset.top = top
	if view.allowedScrollRange < 0 then
		view.widgets.scrollButton.y = math.floor( lerp( 0, view.scrollBarLength,
			top / view.allowedScrollRange ) )
	else
		view.widgets.scrollButton.y = 0
	end
end

function NetworkStorageChest.cl_makePlayerInventorySlot( self, slot, containerItem )
	if playerSlotIsEmpty( containerItem ) then return nil, nil end
	local card = DeepCopy( ITEM_TEMPLATE )
	local root = findRequiredWidget( card, "CatalogItem" )
	local image = findRequiredWidget( card, "CatalogItemImage" )
	local quantity = findRequiredWidget( card, "CatalogItemQuantity" )
	local keyItemWidget = findRequiredWidget( card, "CatalogItemKey" )
	local typeWidget = findRequiredWidget( card, "CatalogItemType" )
	local slotWidget = findRequiredWidget( card, "CatalogItemRoute" )
	local binding = hotbarBinding( slot )
	root.Name = "PlayerInventoryItem_" .. tostring( slot )
	root.onClick = "cl_onPlayerInventoryItemClick"
	setMouseWheelCallback( root, "cl_onPlayerInventoryMouseWheel" )
	slotWidget.Caption = binding
	slotWidget.TextColour = binding ~= "" and "1 0.78 0.12 1" or "0.35 0.85 1 0"
	local resource, group, imageName, keyItem, itemType = sm.gui.getItemIconFromUuid( containerItem.uuid )
	local title = sm.shape.getShapeTitle( containerItem.uuid ) or tostring( containerItem.uuid )
	root.ToolTip.Text = title .. "  x" .. tostring( containerItem.quantity ) ..
		"  |  " .. localizedText( "inventoryDepositClick" )
	image.ImageResource, image.ImageGroup, image.ImageName = resource, group, imageName
	quantity.Caption = ( tonumber( containerItem.quantity ) or 0 ) > 1 and
		( "x" .. tostring( containerItem.quantity ) ) or ""
	keyItemWidget.ImageTexture = keyItem
	typeWidget.ImageName = itemType
	return card, {
		slot = slot,
		uuid = tostring( containerItem.uuid ),
		quantity = tonumber( containerItem.quantity ) or 0,
		hotbar = slot < 10,
		binding = binding
	}
end

function NetworkStorageChest.cl_rebuildPlayerInventory( self, resetScroll )
	local view = self.cl and self.cl.inventoryScrollView
	local inventory = self.cl and self.cl.playerInventory
	if not view or not inventory or not sm.exists( inventory ) then return end
	local previousOffset = resetScroll and 0 or self:cl_getPlayerInventoryScrollOffset()
	self:cl_resetPlayerInventoryScroll()
	self.cl.playerInventorySlots = {}
	self.cl.playerInventoryVisibleCount = 0
	local size = math.max( tonumber( inventory:getSize() ) or 0, 0 )
	for slot = 0, size - 1 do
		local item = inventory:getItem( slot )
		if not playerSlotIsEmpty( item ) then
			local card, snapshot = self:cl_makePlayerInventorySlot( slot, item )
			view:addGridItem( card )
			self.cl.playerInventorySlots[slot + 1] = snapshot
			self.cl.playerInventoryVisibleCount = self.cl.playerInventoryVisibleCount + 1
		end
	end
	self:cl_restorePlayerInventoryScroll( previousOffset )
	self.cl.guiDirty = true
end

function NetworkStorageChest.cl_makeCatalogItem( self, entry )
	local item = DeepCopy( ITEM_TEMPLATE )
	local root = findRequiredWidget( item, "CatalogItem" )
	local image = findRequiredWidget( item, "CatalogItemImage" )
	local quantity = findRequiredWidget( item, "CatalogItemQuantity" )
	local keyItemWidget = findRequiredWidget( item, "CatalogItemKey" )
	local typeWidget = findRequiredWidget( item, "CatalogItemType" )
	local routeWidget = findRequiredWidget( item, "CatalogItemRoute" )
	local resource, group, imageName, keyItem, itemType = sm.gui.getItemIconFromUuid( entry.item.itemUuid )
	root.Name = "CatalogItem_" .. tostring( entry.index )
	root.ToolTip.Text = localizedText( "tooltip", entry.item.title, entry.item.quantity,
		entry.item.stacks, entry.item.sources, sourceKind( entry.item ) )
	image.ImageResource = resource
	image.ImageGroup = group
	image.ImageName = imageName
	quantity.Caption = "x" .. tostring( entry.item.quantity )
	keyItemWidget.ImageTexture = keyItem
	typeWidget.ImageName = itemType
	routeWidget.Caption = entry.item.crossWorldSources > 0 and "X"
		or ( entry.item.localSources > 0 and entry.item.wirelessSources > 0 and "M" )
		or ( entry.item.wirelessSources > 0 and "W" ) or "L"
	return item
end

function NetworkStorageChest.cl_rebuildCatalog( self, resetScroll )
	if not self.cl.scrollView or not self.cl.guiData then return end
	local search = normalizeSearch( self.cl.search )
	local sortMode = SORT_MODES[self.cl.sortMode]
	local filterType = ITEM_FILTERS[self.cl.typeFilter] or "ALL"
	local entries = buildVisibleCatalog( self.cl.catalog, search, sortMode, filterType )
	local previousOffset = resetScroll and 0 or self:cl_getCatalogScrollOffset()

	self:cl_resetCatalogScroll()
	for _, entry in ipairs( entries ) do self.cl.scrollView:addGridItem( self:cl_makeCatalogItem( entry ) ) end
	self:cl_restoreCatalogScroll( previousOffset )
	self.cl.visibleCount = #entries
	local sortLabels = { TYPE = localizedText( "sortType" ), NAME = localizedText( "sortName" ), COUNT = localizedText( "sortCount" ),
		STACKS = localizedText( "sortStacks" ) }
	findRequiredWidget( self.cl.guiData, "SortButton" ).Caption =
		localizedText( "sort", sortLabels[sortMode] or sortMode )
	local filterLabels = {
		ALL = localizedText( "filterAll" ), BLOCK = localizedText( "filterBlocks" ),
		INTERACTIVE = localizedText( "filterInteractive" ), PART = localizedText( "filterParts" ),
		TOOL = localizedText( "filterTools" ), CONSUMABLE = localizedText( "filterConsumables" )
	}
	local filterButton = findRequiredWidget( self.cl.guiData, "TypeFilterButton" )
	filterButton.Caption = localizedText( "filter", filterLabels[filterType] or filterType )
	local filterDot = findRequiredWidget( self.cl.guiData, "TypeFilterDot" )
	filterDot.Visible = filterType ~= "ALL"
	filterDot.ImageName = filterType ~= "ALL" and string.lower( filterType ) or ""

	local selection = findRequiredWidget( self.cl.guiData, "SelectionStatus" )
	if self.cl.withdrawStatus then
		selection.Caption = self.cl.withdrawStatus
	elseif self.cl.selected and self.cl.catalog[self.cl.selected] then
		local selected = self.cl.catalog[self.cl.selected]
		selection.Caption = localizedText( "selectedSummary", string.upper( selected.title ),
			selected.quantity, selected.stacks )
	elseif self.cl.catalogState.status == "INDEXING" then
		selection.Caption = localizedText( "indexingPrompt" )
	elseif search ~= "" then
		selection.Caption = #entries == 1 and localizedText( "searchMatch", self.cl.search )
			or localizedText( "searchMatches", #entries, self.cl.search )
	elseif #entries == 0 then
		selection.Caption = localizedText( "noItems" )
	else
		selection.Caption = localizedText( "selectPrompt" )
	end
	self:cl_refreshWithdrawalControls()
	self.cl.guiDirty = true
end

function NetworkStorageChest.cl_refreshWithdrawalControls( self )
	if not self.cl or not self.cl.guiData then return end
	local ready = self.cl.catalogState and self.cl.catalogState.status == "READY"
	local enabled = ready and self.cl.sessionToken ~= nil and self.cl.selected ~= nil and not self.cl.withdrawBusy
	findRequiredWidget( self.cl.guiData, "TakeOneButton" ).Enabled = enabled
	findRequiredWidget( self.cl.guiData, "TakeStackButton" ).Enabled = enabled
	findRequiredWidget( self.cl.guiData, "TakeAllButton" ).Enabled = enabled
end

function NetworkStorageChest.cl_refreshRoutingControl( self )
	if not self.cl or not self.cl.guiData then return end
	local button = findRequiredWidget( self.cl.guiData, "RoutingModeButton" )
	local smartRouting = not self.cl.serverState or self.cl.serverState.smartRouting ~= false
	button.Visible = not self.cl.legacyBuffer
	button.Enabled = not self.cl.legacyBuffer and self.cl.sessionToken ~= nil and not self.cl.routingBusy
	button.Caption = self.cl.routingBusy and localizedText( "routingApplying" )
		or ( smartRouting and localizedText( "routingSmartOn" ) or localizedText( "routingSmartOff" ) )
	button.TextColour = smartRouting and "1 0.80 0.25 1" or "0.55 0.85 0.95 1"
	button.ToolTip = button.ToolTip or {}
	button.ToolTip.Text = smartRouting and localizedText( "routingSmartHint" ) or localizedText( "routingNearestHint" )
end

function NetworkStorageChest.cl_applyRoutingButtonState( self )
	if not self.cl or not self.cl.gui or self.cl.legacyBuffer then return end
	local smartRouting = not self.cl.serverState or self.cl.serverState.smartRouting ~= false
	pcall( function() self.cl.gui:setButtonState( "RoutingModeButton", smartRouting ) end )
end

function NetworkStorageChest.cl_refreshStatus( self )
	if not self.cl or not self.cl.guiData then return end
	local state = self.cl.catalogState or {}
	local deposit = self.cl.depositState or { status = "READY" }
	local depositCaptions = {
		SORTED = localizedText( "depositSorted", deposit.moved or 0 ),
		PARTIAL_DESTINATIONS_FULL = localizedText( "depositPartial", deposit.remaining or 0 ),
		NO_VALID_DESTINATION = localizedText( "depositNoDestination" ),
		NETWORK_CHANGED_RETRYING = localizedText( "depositRetrying" ),
		BUFFER_UNAVAILABLE = localizedText( "depositUnavailable" )
	}
	local depositHelp = findRequiredWidget( self.cl.guiData, "DepositHelp" )
	depositHelp.Caption = self.cl.legacyBuffer and localizedText( "legacyBufferHelp" )
		or self.cl.inventoryDepositStatus or depositCaptions[deposit.status]
		or localizedText( "inventoryDepositHelp" )
	depositHelp.TextColour = deposit.status == "READY" and "0.92 0.92 0.92 1" or "1 0.72 0.24 1"
	findRequiredWidget( self.cl.guiData, "DepositLabel" ).Caption = self.cl.legacyBuffer
		and localizedText( "legacyBufferLabel" ) or localizedText( "depositBuffer3" )

	local progressHolder = findRequiredWidget( self.cl.guiData, "IndexProgressHolder" )
	local progressFill = findRequiredWidget( self.cl.guiData, "IndexProgressFill" )
	local scanTotal = math.max( tonumber( state.scanTotal or state.containerCount ) or 0, 0 )
	local scanned = math.max( tonumber( state.scannedContainers ) or 0, 0 )
	local fraction = scanTotal > 0 and math.min( scanned / scanTotal, 1 ) or 0
	progressHolder.Visible = state.status == "INDEXING"
	progressFill.width = math.max( 4, math.floor( 326 * fraction ) )

	local selectedName = findRequiredWidget( self.cl.guiData, "SelectedName" )
	local selectedDetail = findRequiredWidget( self.cl.guiData, "SelectedDetail" )
	local selectedImage = findRequiredWidget( self.cl.guiData, "SelectedIconImage" )
	local selectedKey = findRequiredWidget( self.cl.guiData, "SelectedIconKey" )
	local selectedType = findRequiredWidget( self.cl.guiData, "SelectedIconType" )
	if self.cl.selected and self.cl.catalog[self.cl.selected] then
		local selected = self.cl.catalog[self.cl.selected]
		selectedName.Caption = string.upper( selected.title ) .. "  x" .. tostring( selected.quantity )
		selectedDetail.Caption = localizedText( "selectedDetail", selected.sources, sourceKind( selected ) )
		local resource, group, imageName, keyItem, itemType = sm.gui.getItemIconFromUuid( selected.itemUuid )
		selectedImage.ImageResource = resource
		selectedImage.ImageGroup = group
		selectedImage.ImageName = imageName
		selectedKey.ImageTexture = keyItem
		selectedType.ImageName = itemType
		selectedImage.Visible, selectedKey.Visible, selectedType.Visible = true, true, true
	else
		selectedName.Caption = localizedText( "none" )
		selectedDetail.Caption = localizedText( "selectPrompt" )
		selectedImage.Visible, selectedKey.Visible, selectedType.Visible = false, false, false
	end
	findRequiredWidget( self.cl.guiData, "SelectionStatus" ).TextColour =
		self.cl.withdrawStatus and "1 0.72 0.24 1" or "0.92 0.92 0.92 1"
	self:cl_refreshWithdrawalControls()
	self:cl_refreshRoutingControl()
end

function NetworkStorageChest.cl_onCatalogItemClick( self, widgetName )
	local index = tonumber( tostring( widgetName or "" ):match( "(%d+)$" ) )
	if not index or not self.cl.catalog[index] then return end
	if self.cl.selected == index and not self.cl.withdrawStatus then return end
	self.cl.selected = index
	self.cl.withdrawStatus = nil
	self:cl_refreshStatus()
	self.cl.guiDirty = true
end

function NetworkStorageChest.cl_onTextEdit( self, _, text )
	self.cl.search = tostring( text or "" )
	self.cl.selected = nil
	self.cl.withdrawStatus = nil
	self:cl_rebuildCatalog( true )
	self:cl_refreshStatus()
end

function NetworkStorageChest.cl_n_startPhase1ClientQualification( self, data )
	if not self.cl or not data or not data.token then return end
	sm.log.info( PHASE1_PREFIX .. "qualification request reached local terminal client" )
	self.cl.phase1Qualification = {
		token = tostring( data.token ),
		expectedContainers = data.expectedContainers or 0,
		expectedQuantity = data.expectedQuantity or 0
	}
	self.network:sendToServer( "sv_n_openCatalog" )
end

function NetworkStorageChest.cl_n_runPhase5UiQualification( self, data )
	if not data or not data.token then return end
	local results = {}
	local function record( name, passed, detail )
		results[#results + 1] = { name = name, passed = passed == true, detail = tostring( detail or "" ) }
	end
	local function protected( name, callback )
		local ok, passed, detail = pcall( callback )
		record( name, ok and passed == true, ok and detail or passed )
	end

	protected( "eleven-language-catalog", function()
		local required = { "Brazilian", "Chinese", "English", "French", "German", "Italian",
			"Japanese", "Korean", "Polish", "Russian", "Spanish" }
		for _, language in ipairs( required ) do
			local entry = LOCALIZATION[language]
			if not entry or not entry.inventoryTitle or not entry.inventoryDescription or
				not entry.title or not entry.catalog or not entry.takeAll or
				not entry.sortType or not entry.filterAll then
				return false, "missing required text for " .. language
			end
		end
		return true, tostring( #required ) .. " languages"
	end )

	protected( "gui-widget-contract", function()
		local guiData = DeepCopy( GUI_TEMPLATE )
		ReplaceSubLayouts( guiData )
		local required = { "SearchInput", "TypeFilterButton", "TypeFilterDot", "SortButton", "ClearSearch", "CatalogScrollHost",
			"IndexProgressHolder", "IndexProgressFill", "SelectionStatus", "TakeOneButton",
			"TakeStackButton", "TakeAllButton", "SelectedIconImage", "SelectedDetail",
			"PlayerInventoryScrollHost", "DepositBox", "DepositHelp" }
		for _, widgetName in ipairs( required ) do findRequiredWidget( guiData, widgetName ) end
		return guiData.width == 1120 and guiData.height == 540,
			"widgets=" .. tostring( #required ) .. ", size=" .. tostring( guiData.width ) .. "x" .. tostring( guiData.height )
	end )

	protected( "native-progress-and-focus", function()
		local guiData = DeepCopy( GUI_TEMPLATE )
		local progress = findRequiredWidget( guiData, "IndexProgressFill" )
		local focus = findRequiredWidget( guiData, "SearchInput" ).NeedKey and
			findRequiredWidget( guiData, "TypeFilterButton" ).NeedKey and
			findRequiredWidget( guiData, "SortButton" ).NeedKey and
			findRequiredWidget( guiData, "ClearSearch" ).NeedKey and
			findRequiredWidget( guiData, "TakeOneButton" ).NeedKey and
			findRequiredWidget( ITEM_TEMPLATE, "CatalogItem" ).NeedKey
		local playerHost = findRequiredWidget( guiData, "PlayerInventoryScrollHost" )
		return progress.Skin == "DressbotProgress" and focus == true and GUI_TEMPLATE.Hotbar == nil and
			FindWidget( guiData, "InventoryBox" ) == nil and FindWidget( guiData, "HotbarBox" ) == nil and
			playerHost.Type == "Widget" and playerHost.height == 256,
			"native progress + ordered focus + one unified slot grid"
	end )

	protected( "compact-filter-reflow", function()
		local sample = {}
		for index = 1, 60 do
			local title = index % 3 == 0 and ( "metal " .. tostring( index ) ) or ( "part " .. tostring( index ) )
			sample[#sample + 1] = { uuid = string.format( "%04d", index ), title = title,
				searchTitle = title, quantity = 61 - index, stacks = index % 7 + 1,
				itemType = index % 2 == 0 and "block" or "interactive" }
		end
		local filtered = buildVisibleCatalog( sample, "metal", "NAME", "ALL" )
		for index, entry in ipairs( filtered ) do
			if not entry or not entry.item or string.find( entry.item.searchTitle, "metal", 1, true ) == nil then
				return false, "gap or unrelated result at " .. tostring( index )
			end
		end
		return #filtered == 20, "20 compact results from 60 items"
	end )

	protected( "all-sort-modes", function()
		local sample = {
			{ uuid = "b", searchTitle = "beta", quantity = 4, stacks = 8, itemType = "interactive" },
			{ uuid = "a", searchTitle = "alpha", quantity = 9, stacks = 2, itemType = "block" }
		}
		return buildVisibleCatalog( sample, "", "TYPE", "ALL" )[1].item.itemType == "block" and
			buildVisibleCatalog( sample, "", "NAME", "ALL" )[1].item.uuid == "a" and
			buildVisibleCatalog( sample, "", "COUNT", "ALL" )[1].item.quantity == 9 and
			buildVisibleCatalog( sample, "", "STACKS", "ALL" )[1].item.stacks == 8 and
			#buildVisibleCatalog( sample, "", "TYPE", "interactive" ) == 1,
			"type/name grouping, type filter, quantity, and stacks"
	end )

	protected( "catalog-card-detail", function()
		local knownUuid = sm.uuid.new( "ad35f7e6-af8f-40fa-aef4-77d827ac8a8a" )
		local card = self:cl_makeCatalogItem( { index = 1, item = {
			uuid = tostring( knownUuid ), itemUuid = knownUuid, title = "Test Item", quantity = 42,
			stacks = 3, sources = 2, localSources = 1, wirelessSources = 1,
			crossWorldSources = 0, itemType = "interactive"
		} } )
		return card.Name == "CatalogItem_1" and card.NeedMouse == true and
			findRequiredWidget( card, "CatalogItemRoute" ).Caption == "M" and
			string.find( card.ToolTip.Text, "42", 1, true ) ~= nil,
			"renamed clickable card + localized tooltip + mixed route marker"
	end )

	protected( "atlas-icon-registration", function()
		local resource, group, imageName = sm.gui.getItemIconFromUuid( sm.uuid.new( PART_UUID ) )
		return resource ~= nil and resource ~= "" and group ~= nil and imageName ~= nil and imageName ~= "",
			tostring( resource ) .. " | " .. tostring( imageName )
	end )

	protected( "real-json-gui-render", function()
		self:cl_openGui()
		local rendered = self.cl and self.cl.gui ~= nil and self.cl.guiData ~= nil and self.cl.scrollView ~= nil
		local knownUuid = sm.uuid.new( "ad35f7e6-af8f-40fa-aef4-77d827ac8a8a" )
		self.cl.catalog = {}
		for index = 1, 60 do
			self.cl.catalog[index] = {
				uuid = tostring( knownUuid ) .. tostring( index ), itemUuid = knownUuid,
				title = "Test Item " .. tostring( index ), searchTitle = string.format( "test item %03d", index ),
				quantity = index, stacks = 1, sources = 1, localSources = 1,
				wirelessSources = 0, crossWorldSources = 0,
				itemType = index % 2 == 0 and "block" or "interactive"
			}
		end
		self.cl.catalogState.status = "READY"
		self:cl_rebuildCatalog( true )
		self:cl_restoreCatalogScroll( -132 )
		local beforeSelection = self:cl_getCatalogScrollOffset()
		self:cl_onCatalogItemClick( "CatalogItem_25" )
		local selectionPreservedScroll = beforeSelection < 0 and
			self:cl_getCatalogScrollOffset() == beforeSelection and self.cl.selected == 25
		local inventory = sm.localPlayer.getInventory()
		local size = inventory:getSize()
		local visibleSlots = 0
		local slotAccurate = self.cl.inventoryScrollView ~= nil
		for slot = 0, size - 1 do
			local actual = inventory:getItem( slot )
			local shown = self.cl.playerInventorySlots[slot + 1]
			local empty = playerSlotIsEmpty( actual )
			if empty then
				if shown ~= nil then slotAccurate = false break end
			else
				visibleSlots = visibleSlots + 1
				if not shown or shown.slot ~= slot or shown.hotbar ~= ( slot < 10 ) or
					shown.binding ~= hotbarBinding( slot ) or
					shown.quantity ~= ( tonumber( actual.quantity ) or 0 ) or
					shown.uuid ~= tostring( actual.uuid ) then
					slotAccurate = false
					break
				end
			end
		end
		slotAccurate = slotAccurate and self.cl.playerInventoryVisibleCount == visibleSlots
		self:cl_destroyGui()
		return rendered and selectionPreservedScroll and slotAccurate,
			"real containers + stable catalog scroll + compact occupied-slot inventory grid"
	end )

	self.network:sendToServer( "sv_n_phase5ClientQualificationAck", {
		token = tostring( data.token ), results = results
	} )
end

function NetworkStorageChest.cl_onTextEnter( self, _, text )
	self:cl_onTextEdit( nil, text )
end

function NetworkStorageChest.cl_requestWithdrawal( self, action )
	if not self.cl or self.cl.withdrawBusy or not self.cl.sessionToken then return end
	local selected = self.cl.selected and self.cl.catalog[self.cl.selected] or nil
	local state = self.cl.catalogState or {}
	if not selected or state.status ~= "READY" then return end
	self.cl.withdrawBusy = true
	self.cl.withdrawStatus = localizedText( "verifying" )
	self:cl_refreshStatus()
	self.cl.guiDirty = true
	self.network:sendToServer( "sv_n_withdraw", {
		token = self.cl.sessionToken,
		action = action,
		uuid = selected.uuid,
		topologyGeneration = state.topologyGeneration,
		contentGeneration = state.contentGeneration
	} )
end

function NetworkStorageChest.cl_n_withdrawProgress( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	self.cl.withdrawBusy = true
	self.cl.withdrawStatus = localizedText( "withdrawRetrying" )
	if self.cl.guiData then
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
end

function NetworkStorageChest.cl_n_withdrawResult( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	self.cl.withdrawBusy = false
	local moved = tonumber( data.moved ) or 0
	local messages = {
		SUCCESS = localizedText( "success", moved, moved == 1 and "" or "S" ),
		INVENTORY_FULL = localizedText( "inventoryFull" ),
		ITEM_UNAVAILABLE = localizedText( "itemUnavailable" ),
		STALE_CATALOG = localizedText( "staleCatalog" ),
		NETWORK_CHANGED = localizedText( "networkChanged" ),
		ITEM_IN_USE = localizedText( "itemInUse" ),
		ROUTE_UNAVAILABLE = localizedText( "routeUnavailable" ),
		STORAGE_BUSY = localizedText( "storageBusy" ),
		INDEXING = localizedText( "indexingError" ),
		NETWORK_OFFLINE = localizedText( "offlineError" ),
		TRANSACTION_BUSY = localizedText( "busyError" ),
		RATE_LIMITED = localizedText( "rateError" ),
		SESSION_EXPIRED = localizedText( "sessionError" ),
		INVENTORY_UNAVAILABLE = localizedText( "inventoryError" ),
		INVALID_REQUEST = localizedText( "invalidError" )
	}
	self.cl.withdrawStatus = messages[data.status] or localizedText( "stopped" )
	if data.status == "SESSION_EXPIRED" then self.cl.sessionToken = nil end
	if self.cl.guiData then
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
end

function NetworkStorageChest.cl_onControlClick( self, widgetName )
	if widgetName == "ClearSearch" then
		self.cl.search = ""
		self.cl.selected = nil
		self.cl.withdrawStatus = nil
		findRequiredWidget( self.cl.guiData, "SearchInput" ).Caption = ""
	elseif widgetName == "SortButton" then
		self.cl.sortMode = self.cl.sortMode % #SORT_MODES + 1
		self.cl.selected = nil
		self.cl.withdrawStatus = nil
	elseif widgetName == "TypeFilterButton" then
		self.cl.typeFilter = self.cl.typeFilter % #ITEM_FILTERS + 1
		self.cl.selected = nil
		self.cl.withdrawStatus = nil
	elseif widgetName == "TakeOneButton" then
		self:cl_requestWithdrawal( "TAKE_ONE" )
		return
	elseif widgetName == "TakeStackButton" then
		self:cl_requestWithdrawal( "TAKE_STACK" )
		return
	elseif widgetName == "TakeAllButton" then
		self:cl_requestWithdrawal( "TAKE_ALL" )
		return
	elseif widgetName == "RoutingModeButton" then
		if self.cl.legacyBuffer or self.cl.routingBusy or not self.cl.sessionToken then return end
		self.cl.routingBusy = true
		self:cl_refreshRoutingControl()
		self.cl.guiDirty = true
		self.network:sendToServer( "sv_n_setRoutingMode", {
			token = self.cl.sessionToken,
			smartRouting = self.cl.serverState and self.cl.serverState.smartRouting == false
		} )
		return
	else
		return
	end
	self:cl_rebuildCatalog( true )
	self:cl_refreshStatus()
end

function NetworkStorageChest.cl_n_routingModeState( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	self.cl.routingBusy = false
	if data.smartRouting ~= nil then self.cl.serverState.smartRouting = data.smartRouting == true end
	if data.success ~= true and data.status == "SESSION_EXPIRED" then
		self.cl.sessionToken = nil
		sm.gui.displayAlertText( localizedText( "sessionError" ), 3 )
	end
	if self.cl.guiData then
		self:cl_refreshStatus()
		self.cl.guiDirty = true
	end
end

function NetworkStorageChest.cl_onHandleMouseWheel( self, _, scrollValue )
	if self.cl.scrollView then
		local before = self:cl_getCatalogScrollOffset()
		self.cl.scrollView:handleScroll( scrollValue )
		if self:cl_getCatalogScrollOffset() ~= before then self.cl.guiDirty = true end
	end
end

function NetworkStorageChest.cl_onScrollButtonPressed( self, _, x, y )
	if self.cl.scrollView then self.cl.scrollView:handleScrollButtonPressed( x, y ) end
end

function NetworkStorageChest.cl_onScrollButtonReleased( self, _, x, y )
	if self.cl.scrollView then self.cl.scrollView:handleScrollButtonReleased( x, y ) end
end

function NetworkStorageChest.cl_onScrollButtonDrag( self, _, x, y )
	if self.cl.scrollView then
		local before = self:cl_getCatalogScrollOffset()
		self.cl.scrollView:handleScrollButtonDrag( x, y )
		if self:cl_getCatalogScrollOffset() ~= before then self.cl.guiDirty = true end
	end
end

function NetworkStorageChest.cl_onPlayerInventoryMouseWheel( self, _, scrollValue )
	local view = self.cl and self.cl.inventoryScrollView
	if not view then return end
	local before = self:cl_getPlayerInventoryScrollOffset()
	view:handleScroll( scrollValue )
	if self:cl_getPlayerInventoryScrollOffset() ~= before then self.cl.guiDirty = true end
end

function NetworkStorageChest.cl_onPlayerInventoryScrollButtonPressed( self, _, _, y )
	local view = self.cl and self.cl.inventoryScrollView
	if not view or not self.cl.gui then return end
	local _, absoluteY = self.cl.gui:getWidgetAbsolutePosition( "PlayerInventoryScrollButton" )
	view.buttonPressOffset = y - absoluteY
end

function NetworkStorageChest.cl_onPlayerInventoryScrollButtonReleased( self )
end

function NetworkStorageChest.cl_onPlayerInventoryScrollBarPressed( self, _, x, y )
	local view = self.cl and self.cl.inventoryScrollView
	if not view then return end
	view.buttonPressOffset = view.widgets.scrollButton.height / 2
	self:cl_onPlayerInventoryScrollButtonDrag( nil, x, y )
end

function NetworkStorageChest.cl_onPlayerInventoryScrollButtonDrag( self, _, x, y )
	local view = self.cl and self.cl.inventoryScrollView
	if not view or not self.cl.gui or view.allowedScrollRange == 0 then return end
	local absoluteX, absoluteY = self.cl.gui:getWidgetAbsolutePosition( "PlayerInventoryScrollBar" )
	local newY = math.floor( clamp( y - absoluteY - ( view.buttonPressOffset or 0 ), 0, view.scrollBarLength ) )
	view.widgets.scrollButton.y = newY
	view.widgets.scrollView.GridScrollOffset.top = math.floor( lerp( 0, view.allowedScrollRange,
		newY / view.scrollBarLength ) )
	self.cl.guiDirty = true
end

function NetworkStorageChest.cl_onPlayerInventoryItemClick( self, widgetName )
	local slot = tonumber( tostring( widgetName or "" ):match( "(%d+)$" ) )
	if slot == nil then return end
	local snapshot = self.cl and self.cl.playerInventorySlots and self.cl.playerInventorySlots[slot + 1]
	local inventory = self.cl and self.cl.playerInventory
	if not snapshot or not snapshot.uuid or not inventory or self.cl.inventoryDepositBusy or
		not self.cl.sessionToken then return end
	self.cl.inventoryDepositBusy = true
	self.cl.inventoryDepositStatus = localizedText( "inventoryDepositWorking" )
	self.cl.inventoryDepositStatusUntil = 0
	self:cl_refreshStatus()
	self.cl.guiDirty = true
	self.network:sendToServer( "sv_n_stageInventorySlot", {
		token = self.cl.sessionToken,
		slot = slot,
		uuid = snapshot.uuid
	} )
end

function NetworkStorageChest.cl_n_inventoryDepositResult( self, data )
	if not self.cl or type( data ) ~= "table" then return end
	self.cl.inventoryDepositBusy = false
	local messages = {
		SUCCESS = localizedText( "inventoryDepositSuccess", tonumber( data.moved ) or 0 ),
		BUFFER_FULL = localizedText( "inventoryDepositFull" ),
		INVENTORY_CHANGED = localizedText( "inventoryDepositChanged" ),
		TRANSACTION_BUSY = localizedText( "inventoryDepositRetry" ),
		RATE_LIMITED = localizedText( "inventoryDepositRetry" ),
		INVALID_REQUEST = localizedText( "inventoryDepositChanged" ),
		INVENTORY_UNAVAILABLE = localizedText( "inventoryError" ),
		SESSION_EXPIRED = localizedText( "sessionError" )
	}
	self.cl.inventoryDepositStatus = messages[data.status] or localizedText( "inventoryDepositChanged" )
	self.cl.inventoryDepositStatusUntil = sm.game.getCurrentTick() + 100
	if data.status == "SESSION_EXPIRED" then self.cl.sessionToken = nil end
	self:cl_refreshStatus()
	self.cl.guiDirty = true
end

function NetworkStorageChest.cl_onScrollBarPressed( self, _, x, y )
	if self.cl.scrollView then
		local before = self:cl_getCatalogScrollOffset()
		self.cl.scrollView:handleScrollBarPressed( x, y )
		if self:cl_getCatalogScrollOffset() ~= before then self.cl.guiDirty = true end
	end
end
