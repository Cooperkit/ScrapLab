-- SCRAPLAB WIRELESS VACUUM PIPE MANAGER v6
-- Persistent endpoint registry, Link topology, and the Phase 4 directional
-- scheduler host. Inventory authority remains inside native transactions.

if WirelessPipeTransfer == nil then
	dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/WirelessPipeTransfer.lua" )
end

WirelessPipeManager = class( nil )
WirelessPipeManager.isSaveObject = true

local MANAGER_SCHEMA_VERSION = 3
local ENDPOINT_RECORD_VERSION = 2
local WIRELESS_PIPE_UUID = "a34d9af0-4ba0-431d-b647-2d5435ecf138"
local MAX_ACTIVE_ENDPOINT_CELLS = 64
local MANAGER_UPDATE_TICKS = 10
local HANDLE_IDLE_GRACE_TICKS = 200
local RECONCILE_CONFIRM_TICKS = 80
local RECONCILE_RETRY_TICKS = 400

local VALID_MODES = { LINK = true, SEND = true, RECEIVE = true }
local MODE_ORDER = { "LINK", "SEND", "RECEIVE" }

local function copyPosition( position )
	return sm.vec3.new( position.x, position.y, position.z )
end

local function cellKey( worldId, x, y )
	return tostring( worldId ) .. ":" .. tostring( x ) .. ":" .. tostring( y )
end

local function groupKey( mode, channel )
	return mode .. "|" .. channel
end

local function sortedKeys( values )
	local keys = {}
	for key in pairs( values ) do
		keys[#keys + 1] = key
	end
	table.sort( keys )
	return keys
end

local function arrayContains( values, needle )
	for _, value in ipairs( values ) do
		if value == needle then return true end
	end
	return false
end

local function normalizeMode( mode )
	mode = string.upper( tostring( mode or "LINK" ) )
	return VALID_MODES[mode] and mode or "LINK"
end

local function normalizeChannel( channel )
	local value = string.upper( tostring( channel or "DF7F01FF" ) )
	value = value:gsub( "#", "" )
	if #value == 6 then value = value .. "FF" end
	if #value ~= 8 or not value:match( "^[0-9A-F]+$" ) then
		return "DF7F01FF"
	end
	return value
end

local function worldLabel( world )
	if not world then return "UNKNOWN WORLD" end
	local publicData = world.publicData or {}
	local worldType = tostring( publicData.type or "" )
	if worldType == "Overworld" then
		return "OVERWORLD"
	elseif worldType == "UndergroundWorld" then
		return "UNDERGROUND - DEPTH " .. tostring( publicData.depth or "?")
	elseif worldType == "WarehouseWorld" then
		return "WAREHOUSE - LEVEL " .. tostring( publicData.level or "?")
	elseif worldType == "DungeonWorld" then
		local poi = tostring( publicData.poiType or "DUNGEON" ):gsub( "_", " " )
		return string.upper( poi )
	elseif worldType == "TrashdomeWorld" then
		return "TRASHDOME"
	elseif worldType == "DrillbotWorld" then
		return "DRILLBOT WORLD"
	elseif worldType ~= "" then
		return string.upper( worldType:gsub( "_", " " ) )
	end
	return "WORLD " .. tostring( world.id or "?")
end

local function persistentRecordChanged( a, b )
	if not a then return true end
	if a.partUuid ~= b.partUuid or a.worldId ~= b.worldId then return true end
	if a.cellX ~= b.cellX or a.cellY ~= b.cellY then return true end
	if a.mode ~= b.mode or a.channel ~= b.channel or a.enabled ~= b.enabled then return true end
	if a.directOnly ~= b.directOnly then return true end
	if a.shapeId ~= b.shapeId or a.worldLabel ~= b.worldLabel then return true end
	local pa, pb = a.lastKnownPosition, b.lastKnownPosition
	if not pa or not pb then return pa ~= pb end
	return ( pa - pb ):length2() > 0.0001
end

function WirelessPipeManager.server_onCreate( self )
	g_wirelessPipeManager = self
	self.sv = {}
	self.sv.saved = self.storage:load()
	if type( self.sv.saved ) ~= "table" then
		self.sv.saved = { schemaVersion = MANAGER_SCHEMA_VERSION, endpoints = {} }
	end
	self.sv.saved.schemaVersion = MANAGER_SCHEMA_VERSION
	self.sv.saved.endpoints = self.sv.saved.endpoints or {}
	self.sv.saved.directionalCursors = self.sv.saved.directionalCursors or {}
	self.sv.live = {}
	-- Scrap Mechanic's restricted Lua runtime does not expose setmetatable.
	-- Endpoint unload/delete paths explicitly remove these live shape keys.
	self.sv.endpointIdByShape = {}
	self.sv.recentlyUnloaded = {}
	self.sv.groups = { LINK = {}, SEND = {}, RECEIVE = {} }
	self.sv.handles = {}
	self.sv.endpointHandleState = {}
	self.sv.reconcile = {}
	self.sv.updateTicks = 0
	self.sv.saveDirty = false
	self.sv.groupsDirty = true
	self.sv.generation = 0
	self.sv.topologyRevision = 1
	self.sv.statusRevision = 1
	self.sv.statusCache = { signature = nil, entries = {} }
	self.sv.routeCapabilities = { link = false, directional = false, input = false, output = false }
	self.sv.lastHandleTopologySignature = nil
	self.sv.directionalDebugModes = {}
	WirelessPipeTransfer.Sv_ServerOnCreate( self )

	for endpointId, record in pairs( self.sv.saved.endpoints ) do
		if type( record ) == "table" and record.world and record.worldId ~= nil then
			record.endpointId = tostring( endpointId )
			record.recordVersion = ENDPOINT_RECORD_VERSION
			record.mode = normalizeMode( record.mode )
			record.channel = normalizeChannel( record.channel )
			record.enabled = record.enabled ~= false
			record.directOnly = record.directOnly ~= false
			record.worldLabel = record.worldLabel or worldLabel( record.world )
			self.sv.reconcile[record.endpointId] = {
				state = "UNCONFIRMED",
				nextAttemptTick = sm.game.getCurrentTick()
			}
		else
			self.sv.saved.endpoints[endpointId] = nil
			self.sv.saveDirty = true
		end
	end

	self:sv_rebuildGroups()
	self:sv_saveIfDirty( true )
	sm.log.info( "[ScrapLab Wireless Pipe] manager ready; saved endpoints=" .. tostring( self:sv_getEndpointCount() ) )
end

function WirelessPipeManager.server_onDestroy( self )
	for _, entry in pairs( self.sv and self.sv.handles or {} ) do
		if entry.handle then pcall( function() entry.handle:release() end ) end
	end
	if g_wirelessPipeManager == self then g_wirelessPipeManager = nil end
end

function WirelessPipeManager.server_onFixedUpdate( self )
	WirelessPipeTransfer.Sv_ServerOnFixedUpdate( self )
	self.sv.updateTicks = self.sv.updateTicks + 1
	if self.sv.updateTicks < MANAGER_UPDATE_TICKS then return end
	self.sv.updateTicks = 0
	if self.sv.groupsDirty then self:sv_rebuildGroups() end
	self:sv_updateReconciliation()
	self:sv_updateHandleOwnership()
	self:sv_saveIfDirty( false )
end

function WirelessPipeManager.sv_saveIfDirty( self, force )
	if force or self.sv.saveDirty then
		self.storage:save( self.sv.saved )
		self.sv.saveDirty = false
	end
end

function WirelessPipeManager.sv_getEndpointCount( self )
	local count = 0
	for _ in pairs( self.sv.saved.endpoints ) do count = count + 1 end
	return count
end

function WirelessPipeManager.sv_bumpTopologyRevision( self )
	self.sv.topologyRevision = ( self.sv.topologyRevision or 0 ) + 1
	if self.sv.topologyRevision > 2147483000 then self.sv.topologyRevision = 1 end
	if self.sv.statusCache then self.sv.statusCache.signature = nil end
end

function WirelessPipeManager.sv_invalidateStatusCache( self )
	self.sv.statusRevision = ( self.sv.statusRevision or 0 ) + 1
	if self.sv.statusRevision > 2147483000 then self.sv.statusRevision = 1 end
	if self.sv.statusCache then self.sv.statusCache.signature = nil end
end

function WirelessPipeManager.sv_makeRecord( self, data )
	return {
		recordVersion = ENDPOINT_RECORD_VERSION,
		endpointId = tostring( data.endpointId ),
		partUuid = tostring( data.partUuid ),
		world = data.world,
		worldId = data.world.id,
		cellX = math.floor( data.cellX ),
		cellY = math.floor( data.cellY ),
		lastKnownPosition = copyPosition( data.position ),
		mode = normalizeMode( data.mode ),
		channel = normalizeChannel( data.channel ),
		enabled = data.enabled ~= false,
		directOnly = data.directOnly ~= false,
		shapeId = data.shapeId,
		worldLabel = worldLabel( data.world ),
		lastConfirmedSaveTick = sm.game.getCurrentTick()
	}
end

function WirelessPipeManager.sv_registerEndpoint( self, data, shape, owner )
	if type( data ) ~= "table" or not data.endpointId or not data.world or not data.position then
		return { ok = false, reason = "INVALID ENDPOINT DATA" }
	end
	if tostring( data.partUuid ) ~= WIRELESS_PIPE_UUID then
		return { ok = false, reason = "INVALID PART UUID" }
	end
	local endpointId = tostring( data.endpointId )
	local currentLive = self.sv.live[endpointId]
	if currentLive and currentLive.shape ~= shape and currentLive.shape and sm.exists( currentLive.shape ) then
		return { ok = false, reason = "DUPLICATE ENDPOINT ID" }
	end
	local existing = self.sv.saved.endpoints[endpointId]
	if existing and not currentLive then
		local sameShape = existing.shapeId == data.shapeId
		local sameCell = existing.worldId == data.world.id and existing.cellX == math.floor( data.cellX ) and existing.cellY == math.floor( data.cellY )
		local samePlace = sameCell and existing.lastKnownPosition and ( existing.lastKnownPosition - data.position ):length2() < 1
		local recent = self.sv.recentlyUnloaded[endpointId]
		local validTransition = recent and sm.game.getCurrentTick() - recent.tick <= 400
		if not sameShape and not samePlace and not validTransition then
			return { ok = false, reason = "DUPLICATE ENDPOINT ID" }
		end
	end

	self.sv.generation = self.sv.generation + 1
	local generation = self.sv.generation
	local record = self:sv_makeRecord( data )
	if self.sv.directionalDebugModes[endpointId] then record.mode = self.sv.directionalDebugModes[endpointId] end
	if persistentRecordChanged( self.sv.saved.endpoints[endpointId], record ) then
		self.sv.saved.endpoints[endpointId] = record
		self.sv.saveDirty = true
		self.sv.groupsDirty = true
	else
		record = self.sv.saved.endpoints[endpointId]
	end

	self.sv.live[endpointId] = {
		shape = shape,
		interactable = shape and shape.interactable or nil,
		owner = owner,
		generation = generation
	}
	if shape then self.sv.endpointIdByShape[shape] = endpointId end
	self.sv.recentlyUnloaded[endpointId] = nil
	self.sv.reconcile[endpointId] = nil
	WirelessPipeTransfer.Sv_OnEndpointTopologyChanged( self, endpointId )
	self:sv_rebuildGroups()
	return { ok = true, generation = generation, status = self:sv_getEndpointStatus( endpointId ) }
end

function WirelessPipeManager.sv_refreshEndpoint( self, data, shape, owner, generation )
	local endpointId = data and tostring( data.endpointId ) or nil
	local live = endpointId and self.sv.live[endpointId] or nil
	if not live or live.shape ~= shape or live.owner ~= owner or live.generation ~= generation then
		return self:sv_registerEndpoint( data, shape, owner )
	end
	local updated = self:sv_makeRecord( data )
	if self.sv.directionalDebugModes[endpointId] then updated.mode = self.sv.directionalDebugModes[endpointId] end
	local previous = self.sv.saved.endpoints[endpointId]
	local changed = persistentRecordChanged( previous, updated )
	if changed then
		local topologyChanged = not previous
			or previous.worldId ~= updated.worldId
			or previous.cellX ~= updated.cellX or previous.cellY ~= updated.cellY
			or previous.mode ~= updated.mode or previous.channel ~= updated.channel
			or previous.enabled ~= updated.enabled
			or previous.directOnly ~= updated.directOnly
		self.sv.saved.endpoints[endpointId] = updated
		self.sv.saveDirty = true
		if topologyChanged then
			WirelessPipeTransfer.Sv_OnEndpointTopologyChanged( self, endpointId )
			self.sv.groupsDirty = true
			self:sv_rebuildGroups()
		end
	end
	return { ok = true, generation = generation, changed = changed, status = self:sv_getEndpointStatus( endpointId ) }
end

function WirelessPipeManager.sv_unloadEndpoint( self, endpointId, shape, generation )
	endpointId = tostring( endpointId or "" )
	local live = self.sv.live[endpointId]
	if live and live.shape == shape and ( generation == nil or live.generation == generation ) then
		if live.shape then self.sv.endpointIdByShape[live.shape] = nil end
		self.sv.live[endpointId] = nil
		self.sv.recentlyUnloaded[endpointId] = { tick = sm.game.getCurrentTick() }
		WirelessPipeTransfer.Sv_OnEndpointTopologyChanged( self, endpointId )
		self.sv.groupsDirty = true
		self:sv_bumpTopologyRevision()
	end
end

function WirelessPipeManager.sv_unregisterEndpoint( self, endpointId, shape, generation )
	endpointId = tostring( endpointId or "" )
	local live = self.sv.live[endpointId]
	if live and live.shape ~= shape then return false end
	if live and generation and live.generation ~= generation then return false end
	if live and live.shape then self.sv.endpointIdByShape[live.shape] = nil end
	self.sv.live[endpointId] = nil
	self.sv.recentlyUnloaded[endpointId] = nil
	self.sv.saved.endpoints[endpointId] = nil
	self.sv.reconcile[endpointId] = nil
	self.sv.endpointHandleState[endpointId] = nil
	self.sv.directionalDebugModes[endpointId] = nil
	WirelessPipeTransfer.Sv_OnEndpointTopologyChanged( self, endpointId )
	self.sv.saveDirty = true
	self.sv.groupsDirty = true
	self:sv_rebuildGroups()
	return true
end

function WirelessPipeManager.sv_rebuildGroups( self )
	self.sv.groups = { LINK = {}, SEND = {}, RECEIVE = {} }
	for endpointId, record in pairs( self.sv.saved.endpoints ) do
		if record.enabled then
			local mode = normalizeMode( record.mode )
			local key = groupKey( mode, normalizeChannel( record.channel ) )
			self.sv.groups[mode][key] = self.sv.groups[mode][key] or {}
			table.insert( self.sv.groups[mode][key], endpointId )
		end
	end
	for _, mode in ipairs( MODE_ORDER ) do
		for _, members in pairs( self.sv.groups[mode] ) do table.sort( members ) end
	end
	local hasLink = false
	for _, members in pairs( self.sv.groups.LINK ) do
		if #members > 1 then hasLink = true; break end
	end
	local hasDirectional = false
	for key, senders in pairs( self.sv.groups.SEND ) do
		local channel = tostring( key ):match( "^[^|]+|(.+)$" )
		local receivers = channel and self.sv.groups.RECEIVE[groupKey( "RECEIVE", channel )] or nil
		if #senders > 0 and receivers and #receivers > 0 then hasDirectional = true; break end
	end
	self.sv.routeCapabilities = {
		link = hasLink,
		directional = hasDirectional,
		input = hasLink or hasDirectional,
		output = hasLink
	}
	self.sv.groupsDirty = false
	self:sv_bumpTopologyRevision()
end

function WirelessPipeManager.sv_hasVirtualRoute( self, requestedDirection )
	if self.sv.groupsDirty then self:sv_rebuildGroups() end
	local capabilities = self.sv.routeCapabilities or {}
	requestedDirection = string.lower( tostring( requestedDirection or "input" ) )
	if requestedDirection == "link" then return capabilities.link == true end
	if requestedDirection == "directional" then return capabilities.directional == true end
	if requestedDirection == "output" then return capabilities.output == true end
	return capabilities.input == true
end

function WirelessPipeManager.sv_getMatchingIds( self, record )
	if not record or not record.enabled then return {} end
	local channel = normalizeChannel( record.channel )
	local result = {}
	if record.mode == "LINK" then
		local members = self.sv.groups.LINK[groupKey( "LINK", channel )] or {}
		for _, endpointId in ipairs( members ) do
			if endpointId ~= record.endpointId then result[#result + 1] = endpointId end
		end
	elseif record.mode == "SEND" then
		local members = self.sv.groups.RECEIVE[groupKey( "RECEIVE", channel )] or {}
		for _, endpointId in ipairs( members ) do result[#result + 1] = endpointId end
	elseif record.mode == "RECEIVE" then
		local members = self.sv.groups.SEND[groupKey( "SEND", channel )] or {}
		for _, endpointId in ipairs( members ) do result[#result + 1] = endpointId end
	end
	return result
end

function WirelessPipeManager.sv_getMatchingCount( self, record )
	if not record or not record.enabled then return 0 end
	local channel = normalizeChannel( record.channel )
	if record.mode == "LINK" then
		local members = self.sv.groups.LINK[groupKey( "LINK", channel )] or {}
		return math.max( 0, #members - 1 )
	elseif record.mode == "SEND" then
		return #( self.sv.groups.RECEIVE[groupKey( "RECEIVE", channel )] or {} )
	elseif record.mode == "RECEIVE" then
		return #( self.sv.groups.SEND[groupKey( "SEND", channel )] or {} )
	end
	return 0
end

function WirelessPipeManager.sv_isActiveEndpoint( self, record )
	return self:sv_getMatchingCount( record ) > 0
end

function WirelessPipeManager.sv_buildDesiredCells( self )
	local desired = {}
	local activeIds = {}
	for endpointId, record in pairs( self.sv.saved.endpoints ) do
		if self:sv_isActiveEndpoint( record ) then activeIds[#activeIds + 1] = endpointId end
	end
	table.sort( activeIds )

	local function add( endpointId, purpose, priority )
		local record = self.sv.saved.endpoints[endpointId]
		if not record or not record.world then return end
		local key = cellKey( record.worldId, record.cellX, record.cellY )
		local value = desired[key]
		if not value then
			value = {
				key = key, world = record.world, worldId = record.worldId,
				cellX = record.cellX, cellY = record.cellY, refCount = 0,
				endpointIds = {}, active = false, reconcile = false,
				priority = priority
			}
			desired[key] = value
		end
		value.refCount = value.refCount + 1
		value.endpointIds[endpointId] = true
		value.active = value.active or purpose == "ACTIVE"
		value.reconcile = value.reconcile or purpose == "RECONCILE"
		value.priority = math.min( value.priority, priority )
	end

	for _, endpointId in ipairs( activeIds ) do add( endpointId, "ACTIVE", 1 ) end
	for _, endpointId in ipairs( sortedKeys( self.sv.reconcile ) ) do
		local state = self.sv.reconcile[endpointId]
		if state and state.state ~= "CONFIRMED" and sm.game.getCurrentTick() >= ( state.nextAttemptTick or 0 ) then
			add( endpointId, "RECONCILE", 2 )
		end
	end
	return desired
end

function WirelessPipeManager.sv_updateHandleOwnership( self )
	local tick = sm.game.getCurrentTick()
	local desired = self:sv_buildDesiredCells()
	local ordered = {}
	for _, entry in pairs( desired ) do ordered[#ordered + 1] = entry end
	table.sort( ordered, function( a, b )
		if a.priority ~= b.priority then return a.priority < b.priority end
		return a.key < b.key
	end )

	local admitted = {}
	for index, entry in ipairs( ordered ) do
		if index <= MAX_ACTIVE_ENDPOINT_CELLS then admitted[entry.key] = entry end
	end

	for endpointId in pairs( self.sv.saved.endpoints ) do
		self.sv.endpointHandleState[endpointId] = { limited = false, ready = false, key = nil }
	end
	for _, entry in ipairs( ordered ) do
		for endpointId in pairs( entry.endpointIds ) do
			local state = self.sv.endpointHandleState[endpointId]
			state.key = entry.key
			state.limited = admitted[entry.key] == nil
		end
	end

	for key, wanted in pairs( admitted ) do
		local current = self.sv.handles[key]
		if not current then
			current = {
				key = key, world = wanted.world, worldId = wanted.worldId,
				cellX = wanted.cellX, cellY = wanted.cellY,
				handle = nil, ready = false, loading = false,
				nextAttemptTick = tick, refCount = wanted.refCount
			}
			self.sv.handles[key] = current
		end
		current.refCount = wanted.refCount
		current.endpointIds = wanted.endpointIds
		current.releaseAt = nil
		if not current.handle and not current.loading and tick >= ( current.nextAttemptTick or 0 ) then
			self:sv_tryAcquireHandle( current )
		end
		for endpointId in pairs( wanted.endpointIds ) do
			self.sv.endpointHandleState[endpointId].ready = current.ready == true
			local reconcile = self.sv.reconcile[endpointId]
			if current.ready and reconcile and reconcile.state ~= "CELL_LOADED" then
				reconcile.state = "CELL_LOADED"
				reconcile.confirmDeadlineTick = tick + RECONCILE_CONFIRM_TICKS
			end
		end
	end

	for key, current in pairs( self.sv.handles ) do
		if not admitted[key] then
			current.releaseAt = current.releaseAt or ( tick + HANDLE_IDLE_GRACE_TICKS )
			if tick >= current.releaseAt then
				if current.handle then pcall( function() current.handle:release() end ) end
				self.sv.handles[key] = nil
			end
		end
	end

	local handleSignature = {}
	for _, endpointId in ipairs( sortedKeys( self.sv.endpointHandleState ) ) do
		local state = self.sv.endpointHandleState[endpointId]
		handleSignature[#handleSignature + 1] = endpointId .. ":" .. tostring( state.key or "" ) .. ":" .. tostring( state.ready == true ) .. ":" .. tostring( state.limited == true )
	end
	local signature = table.concat( handleSignature, "|" )
	if signature ~= self.sv.lastHandleTopologySignature then
		self.sv.lastHandleTopologySignature = signature
		self:sv_bumpTopologyRevision()
	end
end

function WirelessPipeManager.sv_tryAcquireHandle( self, entry )
	local world = entry.world
	if not world or type( world ) == "boolean" then
		entry.nextAttemptTick = sm.game.getCurrentTick() + RECONCILE_RETRY_TICKS
		return
	end
	if not sm.exists( world ) then
		local loaded = pcall( function() sm.world.loadWorld( world ) end )
		if not loaded or not sm.exists( world ) then
			entry.nextAttemptTick = sm.game.getCurrentTick() + RECONCILE_RETRY_TICKS
			for endpointId in pairs( entry.endpointIds or {} ) do
				local reconcile = self.sv.reconcile[endpointId]
				if reconcile then
					reconcile.state = "LOAD_ERROR"
					reconcile.nextAttemptTick = entry.nextAttemptTick
				end
			end
			return
		end
	end
	entry.loading = true
	local ok, handle = pcall( function()
		return world:loadCellWithHandle( entry.cellX, entry.cellY, "sv_onEndpointCellLoaded", { key = entry.key } )
	end )
	entry.loading = false
	if ok and handle then
		entry.handle = handle
		entry.nextAttemptTick = nil
	else
		entry.nextAttemptTick = sm.game.getCurrentTick() + RECONCILE_RETRY_TICKS
		for endpointId in pairs( entry.endpointIds or {} ) do
			local reconcile = self.sv.reconcile[endpointId]
			if reconcile then
				reconcile.state = "LOAD_ERROR"
				reconcile.nextAttemptTick = entry.nextAttemptTick
			end
		end
	end
end

function WirelessPipeManager.sv_onEndpointCellLoaded( self, world, x, y, params )
	local key = params and params.key or cellKey( world.id, x, y )
	local entry = self.sv.handles[key]
	if not entry then return end
	entry.ready = true
	entry.readyTick = sm.game.getCurrentTick()
	self:sv_bumpTopologyRevision()
	for endpointId in pairs( entry.endpointIds or {} ) do
		local reconcile = self.sv.reconcile[endpointId]
		if reconcile and not self.sv.live[endpointId] then
			reconcile.state = "CELL_LOADED"
			reconcile.confirmDeadlineTick = sm.game.getCurrentTick() + RECONCILE_CONFIRM_TICKS
		end
	end
end

function WirelessPipeManager.sv_updateReconciliation( self )
	local tick = sm.game.getCurrentTick()
	for endpointId, state in pairs( self.sv.reconcile ) do
		if self.sv.live[endpointId] then
			self.sv.reconcile[endpointId] = nil
		elseif state.state == "CELL_LOADED" and state.confirmDeadlineTick and tick >= state.confirmDeadlineTick then
			self.sv.saved.endpoints[endpointId] = nil
			self.sv.reconcile[endpointId] = nil
			self.sv.endpointHandleState[endpointId] = nil
			self.sv.saveDirty = true
			self.sv.groupsDirty = true
			self:sv_bumpTopologyRevision()
			sm.log.info( "[ScrapLab Wireless Pipe] removed stale endpoint record " .. endpointId )
		elseif state.state == "LOAD_ERROR" and tick >= ( state.nextAttemptTick or 0 ) then
			state.state = "UNCONFIRMED"
		end
	end
	if self.sv.groupsDirty then self:sv_rebuildGroups() end
end

function WirelessPipeManager.sv_getEndpointStatus( self, endpointId )
	endpointId = tostring( endpointId or "" )
	local record = self.sv.saved.endpoints[endpointId]
	if not record then
		return { state = "WIRELESS MANAGER UNAVAILABLE", matchingCount = 0, worlds = {} }
	end
	local cacheSignature = tostring( self.sv.topologyRevision or 0 ) .. ":" .. tostring( self.sv.statusRevision or 0 )
	self.sv.statusCache = self.sv.statusCache or { signature = nil, entries = {} }
	if self.sv.statusCache.signature ~= cacheSignature then
		self.sv.statusCache.signature = cacheSignature
		self.sv.statusCache.entries = {}
	end
	local cached = self.sv.statusCache.entries[endpointId]
	if cached then return cached end
	local matches = self:sv_getMatchingIds( record )
	local labels, seenLabels = {}, {}
	local crossWorld = false
	for _, peerId in ipairs( matches ) do
		local peer = self.sv.saved.endpoints[peerId]
		if peer then
			if peer.worldId ~= record.worldId then crossWorld = true end
			if not seenLabels[peer.worldLabel] then
				seenLabels[peer.worldLabel] = true
				labels[#labels + 1] = peer.worldLabel
			end
		end
	end
	table.sort( labels )
	local handleState = self.sv.endpointHandleState[endpointId] or {}
	local groupLimited = handleState.limited == true
	local groupReady = handleState.ready == true
	for _, peerId in ipairs( matches ) do
		local peerHandle = self.sv.endpointHandleState[peerId] or {}
		groupLimited = groupLimited or peerHandle.limited == true
		groupReady = groupReady and peerHandle.ready == true
	end
	local state
	if not record.enabled then state = "DISABLED BY LOGIC"
	elseif #matches == 0 then state = record.mode == "LINK" and "UNPAIRED" or "CHANNEL EMPTY"
	elseif groupLimited then state = "REMOTE CELL LOAD LIMIT"
	elseif record.mode == "LINK" then state = crossWorld and "CROSS-WORLD LINKED" or "LINKED"
	elseif record.mode == "SEND" then state = WirelessPipeTransfer.Sv_GetEndpointState( self, endpointId ) or "SENDING"
	else state = "READY TO RECEIVE" end
	local status = {
		state = state,
		mode = record.mode,
		directOnly = record.directOnly ~= false,
		channel = record.channel,
		enabled = record.enabled,
		matchingCount = #matches,
		worlds = labels,
		worldLabel = record.worldLabel,
		cellX = record.cellX,
		cellY = record.cellY,
		handleReady = groupReady,
		handleLimited = groupLimited
	}
	self.sv.statusCache.entries[endpointId] = status
	return status
end

function WirelessPipeManager.sv_getDebugSnapshot( self )
	local handles, ready, limited = 0, 0, 0
	for _, entry in pairs( self.sv.handles ) do
		handles = handles + 1
		if entry.ready then ready = ready + 1 end
	end
	for _, state in pairs( self.sv.endpointHandleState ) do
		if state.limited then limited = limited + 1 end
	end
	local reconciling = 0
	for _ in pairs( self.sv.reconcile ) do reconciling = reconciling + 1 end
	local snapshot = {
		schemaVersion = MANAGER_SCHEMA_VERSION,
		endpoints = self:sv_getEndpointCount(),
		liveEndpoints = #sortedKeys( self.sv.live ),
		handles = handles,
		readyHandles = ready,
		limitedEndpoints = limited,
		reconciling = reconciling,
		maxHandles = MAX_ACTIVE_ENDPOINT_CELLS
	}
	snapshot.directional = WirelessPipeTransfer.Sv_GetDebugSnapshot( self )
	return snapshot
end

function WirelessPipeManager.sv_getEndpointIdForShape( self, shape )
	if not shape then return nil end
	local indexed = self.sv.endpointIdByShape and self.sv.endpointIdByShape[shape] or nil
	if indexed then return indexed end
	for endpointId, live in pairs( self.sv.live ) do
		if live.shape == shape then
			if self.sv.endpointIdByShape then self.sv.endpointIdByShape[shape] = endpointId end
			return endpointId
		end
	end
	return nil
end

function WirelessPipeManager.sv_getLinkPeerEntries( self, endpointId )
	endpointId = tostring( endpointId or "" )
	local record = self.sv.saved.endpoints[endpointId]
	if not record or record.mode ~= "LINK" or not record.enabled then return {} end
	local result = {}
	for _, peerId in ipairs( self:sv_getMatchingIds( record ) ) do
		local peer = self.sv.saved.endpoints[peerId]
		local live = self.sv.live[peerId]
		-- A valid live shape proves that its world cell is loaded. Do not reject it
		-- merely because the independently-updated handle snapshot has not caught
		-- up yet; that produced a false delay until a player revisited the world.
		if peer and live and live.shape and sm.exists( live.shape ) then
			result[#result + 1] = {
				endpointId = peerId,
				shape = live.shape,
				worldId = peer.worldId,
				cellX = peer.cellX,
				cellY = peer.cellY,
				channel = peer.channel
			}
		end
	end
	table.sort( result, function( a, b ) return a.endpointId < b.endpointId end )
	return result
end

-- RECEIVE acts as a pull gateway for local machines. Only matching SEND
-- endpoints are exposed, and each source carries its own safe transfer scope.
-- This is intentionally one hop: directional channels never recurse into Link
-- buses or another directional channel.
function WirelessPipeManager.sv_getDirectionalSourceEntries( self, endpointId )
	endpointId = tostring( endpointId or "" )
	local record = self.sv.saved.endpoints[endpointId]
	if not record or record.mode ~= "RECEIVE" or not record.enabled then return {} end
	local result = {}
	for _, peerId in ipairs( self:sv_getMatchingIds( record ) ) do
		local peer = self.sv.saved.endpoints[peerId]
		local live = self.sv.live[peerId]
		if peer and peer.mode == "SEND" and peer.enabled and live and live.shape and sm.exists( live.shape ) then
			result[#result + 1] = {
				endpointId = peerId,
				shape = live.shape,
				worldId = peer.worldId,
				cellX = peer.cellX,
				cellY = peer.cellY,
				channel = peer.channel,
				directOnly = peer.directOnly ~= false
			}
		end
	end
	table.sort( result, function( a, b ) return a.endpointId < b.endpointId end )
	return result
end

function WirelessPipeManager.sv_validateInvariants( self )
	local errors = {}
	local handleCount = 0
	for key, handle in pairs( self.sv.handles ) do
		handleCount = handleCount + 1
		if key ~= cellKey( handle.worldId, handle.cellX, handle.cellY ) then
			errors[#errors + 1] = "handle key mismatch: " .. tostring( key )
		end
		if ( handle.refCount or 0 ) < 1 and not handle.releaseAt then
			errors[#errors + 1] = "unreferenced handle without grace: " .. tostring( key )
		end
	end
	if handleCount > MAX_ACTIVE_ENDPOINT_CELLS then
		errors[#errors + 1] = "handle cap exceeded: " .. tostring( handleCount )
	end
	for endpointId, live in pairs( self.sv.live ) do
		if not self.sv.saved.endpoints[endpointId] then
			errors[#errors + 1] = "live endpoint missing record: " .. endpointId
		end
		if not live.shape or not sm.exists( live.shape ) then
			errors[#errors + 1] = "live endpoint has stale shape: " .. endpointId
		end
	end
	return #errors == 0, errors
end

function WirelessPipeManager.sv_debugInjectStaleRecord( self, world, position )
	local endpointId = "slwp-stale:" .. tostring( world.id ) .. ":" .. tostring( sm.game.getCurrentTick() ) .. ":" .. tostring( math.random( 100000, 999999999 ) )
	self.sv.saved.endpoints[endpointId] = {
		recordVersion = ENDPOINT_RECORD_VERSION,
		endpointId = endpointId,
		partUuid = WIRELESS_PIPE_UUID,
		world = world,
		worldId = world.id,
		cellX = math.floor( position.x / 64 ),
		cellY = math.floor( position.y / 64 ),
		lastKnownPosition = copyPosition( position ),
		mode = "LINK",
		directOnly = true,
		channel = "010101FF",
		enabled = false,
		shapeId = -1,
		worldLabel = worldLabel( world ),
		lastConfirmedSaveTick = sm.game.getCurrentTick()
	}
	self.sv.reconcile[endpointId] = { state = "UNCONFIRMED", nextAttemptTick = sm.game.getCurrentTick() }
	self.sv.saveDirty = true
	self.sv.groupsDirty = true
	return endpointId
end

function WirelessPipeManager.sv_debugSetEndpointMode( self, endpointId, mode )
	endpointId = tostring( endpointId or "" )
	mode = normalizeMode( mode )
	local record = self.sv.saved.endpoints[endpointId]
	local live = self.sv.live[endpointId]
	if not record or not live or not live.shape or not sm.exists( live.shape ) then return false end
	self.sv.directionalDebugModes[endpointId] = mode
	record.mode = mode
	self.sv.saveDirty = true
	self.sv.groupsDirty = true
	WirelessPipeTransfer.Sv_OnEndpointTopologyChanged( self, endpointId )
	self:sv_rebuildGroups()
	return true
end

function WirelessPipeManager.Sv_RegisterEndpoint( data, shape, owner )
	if not g_wirelessPipeManager then return { ok = false, reason = "WIRELESS MANAGER UNAVAILABLE" } end
	return g_wirelessPipeManager:sv_registerEndpoint( data, shape, owner )
end

function WirelessPipeManager.Sv_RefreshEndpoint( data, shape, owner, generation )
	if not g_wirelessPipeManager then return { ok = false, reason = "WIRELESS MANAGER UNAVAILABLE" } end
	return g_wirelessPipeManager:sv_refreshEndpoint( data, shape, owner, generation )
end

function WirelessPipeManager.Sv_UnloadEndpoint( endpointId, shape, generation )
	if g_wirelessPipeManager then g_wirelessPipeManager:sv_unloadEndpoint( endpointId, shape, generation ) end
end

function WirelessPipeManager.Sv_UnregisterEndpoint( endpointId, shape, generation )
	if not g_wirelessPipeManager then return false end
	return g_wirelessPipeManager:sv_unregisterEndpoint( endpointId, shape, generation )
end

function WirelessPipeManager.Sv_GetEndpointStatus( endpointId )
	if not g_wirelessPipeManager then return nil end
	return g_wirelessPipeManager:sv_getEndpointStatus( endpointId )
end

function WirelessPipeManager.Sv_GetEndpointIdForShape( shape )
	if not g_wirelessPipeManager then return nil end
	return g_wirelessPipeManager:sv_getEndpointIdForShape( shape )
end

function WirelessPipeManager.Sv_GetLinkPeerEntries( endpointId )
	if not g_wirelessPipeManager then return {} end
	return g_wirelessPipeManager:sv_getLinkPeerEntries( endpointId )
end

function WirelessPipeManager.Sv_GetDirectionalSourceEntries( endpointId )
	if not g_wirelessPipeManager then return {} end
	return g_wirelessPipeManager:sv_getDirectionalSourceEntries( endpointId )
end

function WirelessPipeManager.Sv_GetTopologyRevision()
	if not g_wirelessPipeManager then return nil end
	return g_wirelessPipeManager.sv.topologyRevision
end

function WirelessPipeManager.Sv_HasVirtualRoute( requestedDirection )
	if not g_wirelessPipeManager then return false end
	return g_wirelessPipeManager:sv_hasVirtualRoute( requestedDirection )
end

function WirelessPipeManager.Sv_GetDirectionalDebugSnapshot()
	if not g_wirelessPipeManager then return nil end
	return WirelessPipeTransfer.Sv_GetDebugSnapshot( g_wirelessPipeManager )
end

function WirelessPipeManager.Sv_ConsumeEndpointActivity( endpointId, generation )
	if not g_wirelessPipeManager then return nil end
	return WirelessPipeTransfer.Sv_ConsumeEndpointActivity( g_wirelessPipeManager, endpointId, generation )
end

function WirelessPipeManager.Sv_DebugSetEndpointMode( endpointId, mode )
	if not g_wirelessPipeManager then return false end
	return g_wirelessPipeManager:sv_debugSetEndpointMode( endpointId, mode )
end

ScrapLabWirelessPipeManagerConstants = {
	managerSchemaVersion = MANAGER_SCHEMA_VERSION,
	recordVersion = ENDPOINT_RECORD_VERSION,
	partUuid = WIRELESS_PIPE_UUID,
	maxActiveEndpointCells = MAX_ACTIVE_ENDPOINT_CELLS,
	modeOrder = MODE_ORDER
}
