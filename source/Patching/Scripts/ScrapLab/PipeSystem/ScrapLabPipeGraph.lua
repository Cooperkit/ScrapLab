-- SCRAPLAB WIRELESS PIPE GRAPH v15
-- Bounded native-plus-wireless container selection layered over the native
-- pipe graph. Native ordering and live container transactions stay
-- authoritative. Physical components persist only while their bodies and the
-- wireless topology remain valid.

ScrapLabPipeGraph = ScrapLabPipeGraph or {}
ScrapLabPipeGraph.DEFINITION_VERSION = 15

local WIRELESS_PIPE_UUID = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local MAX_PHYSICAL_SHAPES = 4096
local MAX_WIRELESS_ENDPOINTS = 256
local CACHE_INTERVAL_TICKS = 20
local CACHE_PRUNE_INTERVAL_TICKS = 400
local CACHE_MAX_IDLE_TICKS = 1200
local GRAPH_LEASE_TICKS = 80
local MAX_QUERY_CACHE_ENTRIES = 2048
local QUERY_CACHE_TRIM_TARGET = 1792

-- Shape:getPipeOffsets returns openings in the same order as the official
-- shape-set definition. Preserve the engine's input/output boundary at every
-- directional machine instead of treating its two physical sides as one pipe.
local PIPE_OPENING_DIRECTIONS = {
	["b63c6440-dfc2-4da7-acdb-3c385080b2e4"] = { "output", "input" }, -- Craftbot 1
	["b7571f6f-9d53-44ba-99d2-3b4f05e6fd0f"] = { "output", "input" }, -- Craftbot 2
	["1c83675f-7c77-4cbb-875b-79d4bd46100d"] = { "output", "input" }, -- Craftbot 3
	["c69a7855-d915-4784-af81-d0a8849e458f"] = { "output", "input" }, -- Craftbot 4
	["4fcb4cb8-7623-11ea-bc55-0242ac130003"] = { "output", "input" }, -- Craftbot 5
	["0a0cc4ee-bdd7-41b1-b5cb-f34a0e6de46e"] = { "input", "output" }, -- Saw table
	["b46c3271-6288-4b74-a6b1-9ea946cf072b"] = { "output", "input" }, -- Prospector
	["5cb15c93-4fa9-48da-9974-2e95ca6c9e1c"] = { "output" }, -- Refinery
	["b593a935-802a-4715-b27f-739a091a8977"] = { "output" }, -- Ore crusher
	["6c450f8e-7fe5-43ad-9391-c429e83310d2"] = { "input" } -- Garage chest
}

-- Mirrors the game's ContainerUuids pipe registry without depending on
-- game/util/pipes.lua having been loaded by a particular consumer first.
local REGISTERED_CONTAINER_UUIDS = {
	["056e5ff1-f030-40df-946a-b830bf494c92"] = true, -- gas
	["da4833fd-f981-4e08-a9f7-48e630a7c146"] = true, -- battery
	["ea10d1af-b97a-46fb-8895-dfd1becb53bb"] = true, -- water
	["38ec258d-c644-4f08-8635-3f7434c884dd"] = true, -- seed
	["76331bbf-abbd-4b8d-bb54-f721a5b6193b"] = true, -- fertilizer
	["096d4daf-639e-4947-a1a6-1890eaa94464"] = true, -- ammo
	["ad35f7e6-af8f-40fa-aef4-77d827ac8a8a"] = true, -- chest
	["e9efc008-8fae-4391-9ad1-6a62dbab5760"] = true, -- looting chest
	["be29592a-ef58-4b1d-b18c-895023abd27f"] = true, -- chemical
	["5cb15c93-4fa9-48da-9974-2e95ca6c9e1c"] = true, -- refinery
	["9601f2ca-9552-48b0-afc1-b0f200461114"] = true, -- XXL chest
	["4c474cff-3f6a-4306-93d1-c4c74578afd2"] = true  -- piped small chest
}

local peerCache = { revision = nil, entries = {} }
local physicalCache = {
	revision = nil,
	lastPruneTick = 0,
	-- Scrap Mechanic's restricted Lua runtime does not expose setmetatable, so
	-- bounded age pruning prevents retained Shape keys from growing forever.
	shapeKeys = {},
	componentsByShape = {},
	directByShape = {},
	virtualQueries = {},
	terminalQueries = {},
	nativeQueries = {},
	resolvedQueries = {},
	virtualQueryCount = 0,
	terminalQueryCount = 0,
	nativeQueryCount = 0,
	resolvedQueryCount = 0,
	nextComponentId = 0
}
local performance = {
	nativeCalls = 0,
	nativeCacheHits = 0,
	nativeQueryInvalidations = 0,
	resolvedQueryHits = 0,
	resolvedQueryInvalidations = 0,
	fastPathReturns = 0,
	physicalScans = 0,
	physicalNodes = 0,
	componentCacheHits = 0,
	directCacheHits = 0,
	virtualQueryHits = 0,
	negativeVirtualCacheHits = 0,
	terminalCacheHits = 0,
	componentInvalidations = 0,
	cachePrunes = 0,
	queryCacheTrims = 0
}

local function shapeExists( shape )
	if not shape then return false end
	local ok, exists = pcall( function() return sm.exists( shape ) end )
	return ok and exists == true
end

local function getShapeId( shape )
	local ok, value = pcall( function() return shape:getId() end )
	if ok and value ~= nil then return tostring( value ) end
	return tostring( shape.id or shape )
end

local function getWorldId( shape )
	local ok, world = pcall( function() return shape:getBody():getWorld() end )
	if ok and world then return tostring( world.id or world ) end
	return "?"
end

local function shapeKey( shape )
	local cached = physicalCache.shapeKeys[shape]
	if cached then return cached end
	local key = getWorldId( shape ) .. ":" .. getShapeId( shape )
	physicalCache.shapeKeys[shape] = key
	return key
end

local function managerAvailable()
	return g_wirelessPipeManager ~= nil
		and WirelessPipeManager ~= nil
		and WirelessPipeManager.Sv_GetEndpointIdForShape ~= nil
		and WirelessPipeManager.Sv_GetLinkPeerEntries ~= nil
		and WirelessPipeManager.Sv_GetDirectionalSourceEntries ~= nil
		and WirelessPipeManager.Sv_GetDirectionalDestinationEntries ~= nil
		and WirelessPipeManager.Sv_GetTerminalPeerEntries ~= nil
		and WirelessPipeManager.Sv_GetTopologyRevision ~= nil
		and WirelessPipeManager.Sv_RequestEndpointLeases ~= nil
		and WirelessPipeManager.Sv_AreEndpointLeasesSettled ~= nil
		and WirelessPipeManager.Sv_HasVirtualRoute ~= nil
end

local function currentTick()
	local ok, tick = pcall( function() return sm.game.getCurrentTick() end )
	return ok and tick or 0
end

local function clearQueryEntries( field, countField )
	physicalCache[field] = {}
	physicalCache[countField] = 0
end

local function removeQueryEntry( entries, countField, key )
	if entries[key] == nil then return end
	entries[key] = nil
	physicalCache[countField] = math.max( 0, ( physicalCache[countField] or 0 ) - 1 )
end

local function trimQueryEntries( entries, countField )
	local count = physicalCache[countField] or 0
	if count <= MAX_QUERY_CACHE_ENTRIES then return end
	local ordered = {}
	for key, entry in pairs( entries ) do
		ordered[#ordered + 1] = { key = key, tick = entry.lastAccessTick or entry.createdTick or 0 }
	end
	table.sort( ordered, function( a, b )
		if a.tick ~= b.tick then return a.tick < b.tick end
		return tostring( a.key ) < tostring( b.key )
	end )
	local removeCount = math.max( 0, count - QUERY_CACHE_TRIM_TARGET )
	for index = 1, removeCount do
		local value = ordered[index]
		if value and entries[value.key] ~= nil then
			entries[value.key] = nil
			count = count - 1
		end
	end
	physicalCache[countField] = math.max( 0, count )
	performance.queryCacheTrims = performance.queryCacheTrims + 1
end

local function storeQueryEntry( entries, countField, key, entry )
	if entries[key] == nil then
		physicalCache[countField] = ( physicalCache[countField] or 0 ) + 1
	end
	entries[key] = entry
	trimQueryEntries( entries, countField )
end

local function resetPhysicalEntries()
	physicalCache.shapeKeys = {}
	physicalCache.componentsByShape = {}
	physicalCache.directByShape = {}
	physicalCache.virtualQueries = {}
	physicalCache.terminalQueries = {}
	physicalCache.nativeQueries = {}
	physicalCache.resolvedQueries = {}
	physicalCache.virtualQueryCount = 0
	physicalCache.terminalQueryCount = 0
	physicalCache.nativeQueryCount = 0
	physicalCache.resolvedQueryCount = 0
	physicalCache.nextComponentId = 0
end

local prunePhysicalEntries

local function ensureCacheState()
	local tick = currentTick()
	local revision = managerAvailable() and WirelessPipeManager.Sv_GetTopologyRevision() or nil
	if physicalCache.revision ~= revision then
		physicalCache.revision = revision
		-- Wireless peers and handle readiness changed. Physical pipe bodies did
		-- not necessarily change, so preserve their expensive component scans.
		clearQueryEntries( "virtualQueries", "virtualQueryCount" )
		clearQueryEntries( "terminalQueries", "terminalQueryCount" )
		clearQueryEntries( "resolvedQueries", "resolvedQueryCount" )
	end
	if prunePhysicalEntries and tick - ( physicalCache.lastPruneTick or 0 ) >= CACHE_PRUNE_INTERVAL_TICKS then
		prunePhysicalEntries( tick )
	end
	return tick
end

local function invalidatePhysicalEntries()
	resetPhysicalEntries()
end

local function isWirelessEndpoint( shape )
	if not shapeExists( shape ) then return false end
	local ok, uuid = pcall( function() return shape:getShapeUuid() end )
	return ok and uuid == WIRELESS_PIPE_UUID
end

local function unsortedNeighbours( shape )
	local ok, neighbours = pcall( function() return shape:getPipedNeighbours() end )
	if not ok or type( neighbours ) ~= "table" then
		error( "physical pipe neighbours are unavailable" )
	end
	return neighbours
end

local function openingDirections( shape )
	if not shapeExists( shape ) then return nil end
	return PIPE_OPENING_DIRECTIONS[string.lower( tostring( shape:getShapeUuid() ) )]
end

local function directionalNeighbours( shape, requestedDirection )
	local directions = openingDirections( shape )
	local neighbours = unsortedNeighbours( shape )
	if not directions or not requestedDirection then return neighbours end
	local offsets = shape:getPipeOffsets()
	if #offsets ~= #directions then error( "directional pipe opening catalog mismatch" ) end
	local openingPositions = {}
	for index, offset in ipairs( offsets ) do openingPositions[index] = shape:transformLocalPoint( offset ) end
	local result = {}
	for _, neighbour in ipairs( neighbours ) do
		local neighbourPosition = neighbour.worldPosition
		local closestIndex = nil
		local closestDistance = math.huge
		for index, openingPosition in ipairs( openingPositions ) do
			local distance = ( openingPosition - neighbourPosition ):length2()
			if distance < closestDistance then closestDistance = distance; closestIndex = index end
		end
		if closestIndex and directions[closestIndex] == requestedDirection then result[#result + 1] = neighbour end
	end
	return result
end

local function isRegisteredContainerShape( shape )
	if not shapeExists( shape ) then return false end
	return REGISTERED_CONTAINER_UUIDS[string.lower( tostring( shape:getShapeUuid() ) )] == true
end

local function shapeHasContainer( shape )
	if not shapeExists( shape ) then return false end
	local ok, container = pcall( function()
		local interactable = shape:getInteractable()
		return interactable and interactable:getContainer( 0 ) or nil
	end )
	return ok and container ~= nil
end

local function addBody( bodies, bodyKeys, shape )
	local ok, body = pcall( function() return shape:getBody() end )
	if not ok or not body then return end
	local key = tostring( body.id or body )
	if not bodyKeys[key] then
		bodyKeys[key] = true
		bodies[#bodies + 1] = body
	end
end

local function bodiesStillValid( bodies, createdTick, validationTick )
	for _, body in ipairs( bodies or {} ) do
		local ok, valid = pcall( function()
			return sm.exists( body ) and not body:hasChanged( createdTick )
		end )
		if not ok or not valid then return false end
	end
	return true
end

local function componentStillValid( component, tick )
	if component.lastValidationTick and tick - component.lastValidationTick < CACHE_INTERVAL_TICKS then
		return component.lastValidationValid ~= false
	end
	component.lastValidationTick = tick
	component.lastValidationValid = bodiesStillValid( component.bodies, component.createdTick, tick )
	return component.lastValidationValid
end

local function discardComponent( component )
	if not component then return end
	for _, member in ipairs( component.members or {} ) do
		if physicalCache.componentsByShape[member.key] == component then
			physicalCache.componentsByShape[member.key] = nil
		end
	end
	performance.componentInvalidations = performance.componentInvalidations + 1
end

local function buildPhysicalComponent( rootShape, tick )
	performance.physicalScans = performance.physicalScans + 1
	physicalCache.nextComponentId = physicalCache.nextComponentId + 1
	local component = {
		id = physicalCache.nextComponentId,
		createdTick = tick,
		lastValidationTick = tick,
		lastValidationValid = true,
		lastAccessTick = tick,
		shapes = {}, containers = {}, endpoints = {}, bodies = {}, members = {}
	}
	local bodyKeys = {}
	local queue, head, visited = { rootShape }, 1, {}
	while head <= #queue do
		local shape = queue[head]
		head = head + 1
		if shapeExists( shape ) then
			local key = shapeKey( shape )
			if not visited[key] then
				visited[key] = true
				performance.physicalNodes = performance.physicalNodes + 1
				if #component.shapes >= MAX_PHYSICAL_SHAPES then error( "physical pipe graph safety limit exceeded" ) end
				local boundary = shape ~= rootShape and openingDirections( shape ) ~= nil
				component.shapes[#component.shapes + 1] = shape
				component.members[#component.members + 1] = { key = key, boundary = boundary }
				addBody( component.bodies, bodyKeys, shape )
				if isRegisteredContainerShape( shape ) then component.containers[#component.containers + 1] = shape end
				if isWirelessEndpoint( shape ) then
					local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( shape )
					if endpointId then component.endpoints[#component.endpoints + 1] = { endpointId = endpointId, shape = shape } end
				end
				if not boundary then
					for _, neighbour in ipairs( unsortedNeighbours( shape ) ) do
						if shapeExists( neighbour ) and not visited[shapeKey( neighbour )] then queue[#queue + 1] = neighbour end
					end
				end
			end
		end
	end
	table.sort( component.shapes, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
	table.sort( component.containers, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
	table.sort( component.endpoints, function( a, b ) return tostring( a.endpointId ) < tostring( b.endpointId ) end )
	for _, member in ipairs( component.members ) do
		if not member.boundary then physicalCache.componentsByShape[member.key] = component end
	end
	return component
end

local function getPhysicalComponent( rootShape, tracker )
	if not shapeExists( rootShape ) then return nil end
	local tick = ensureCacheState()
	local key = shapeKey( rootShape )
	local component = physicalCache.componentsByShape[key]
	if component and componentStillValid( component, tick ) then
		performance.componentCacheHits = performance.componentCacheHits + 1
	else
		if component then discardComponent( component ) end
		component = buildPhysicalComponent( rootShape, tick )
	end
	component.lastAccessTick = tick
	if tracker and not tracker.componentIds[component.id] then
		tracker.componentIds[component.id] = true
		tracker.components[#tracker.components + 1] = component
	end
	return component
end

local function getStartComponents( startShape, requestedDirection, tracker )
	local result, seen = {}, {}
	if not shapeExists( startShape ) then return result end
	local roots
	if openingDirections( startShape ) and requestedDirection then
		roots = directionalNeighbours( startShape, requestedDirection )
	else
		roots = { startShape }
	end
	for _, root in ipairs( roots ) do
		local component = getPhysicalComponent( root, tracker )
		if component and not seen[component.id] then
			seen[component.id] = true
			result[#result + 1] = component
		end
	end
	return result
end

local function getPeerEntries( endpointId )
	local revision = WirelessPipeManager.Sv_GetTopologyRevision()
	if revision == nil then error( "wireless manager has no topology revision" ) end
	if peerCache.revision ~= revision then
		peerCache.revision = revision
		peerCache.entries = {}
	end
	local cached = peerCache.entries[endpointId]
	if cached then
		for _, entry in ipairs( cached ) do
			if entry.shape and not shapeExists( entry.shape ) then
				cached = nil
				peerCache.entries[endpointId] = nil
				break
			end
		end
	end
	if not cached then
		cached = WirelessPipeManager.Sv_GetLinkPeerEntries( endpointId, "GRAPH" )
		table.sort( cached, function( a, b ) return tostring( a.endpointId ) < tostring( b.endpointId ) end )
		peerCache.entries[endpointId] = cached
	end
	return cached
end

local function appendUniqueShapes( target, additions )
	local seen = {}
	for _, shape in ipairs( target ) do if shapeExists( shape ) then seen[shapeKey( shape )] = true end end
	for _, shape in ipairs( additions or {} ) do
		if shapeExists( shape ) then
			local key = shapeKey( shape )
			if not seen[key] then seen[key] = true; target[#target + 1] = shape end
		end
	end
	return target
end

local function appendComponentEndpoints( queue, seen, components )
	for _, component in ipairs( components or {} ) do
		for _, endpoint in ipairs( component.endpoints ) do
			if endpoint.endpointId and not seen[endpoint.endpointId] then
				seen[endpoint.endpointId] = true
				queue[#queue + 1] = endpoint
				if #queue > MAX_WIRELESS_ENDPOINTS then error( "wireless endpoint safety limit exceeded" ) end
			end
		end
	end
end

local function trackLeaseEndpoint( tracker, endpointId )
	if not tracker or not endpointId or tracker.leaseEndpointSet[endpointId] then return end
	tracker.leaseEndpointSet[endpointId] = true
	tracker.leaseEndpointIds[#tracker.leaseEndpointIds + 1] = endpointId
end

local function renewTrackerLeases( tracker, purpose )
	if not tracker or #tracker.leaseEndpointIds == 0 or not managerAvailable() then return end
	local tick = currentTick()
	local interval = purpose == "TERMINAL" and 30 or 20
	tracker.leaseRenewedAt = tracker.leaseRenewedAt or {}
	local previous = tracker.leaseRenewedAt[purpose or "GRAPH"]
	if previous ~= nil and tick - previous < interval then return end
	tracker.leaseRenewedAt[purpose or "GRAPH"] = tick
	WirelessPipeManager.Sv_RequestEndpointLeases(
		tracker.leaseEndpointIds, purpose == "TERMINAL" and 120 or GRAPH_LEASE_TICKS,
		purpose or "GRAPH", purpose == "TERMINAL" and 2 or 3 )
end

-- Returns remote Link endpoint shapes. Component caching means a bus with many
-- endpoints is still physically scanned once, rather than once per endpoint.
local function discoverRemoteEndpoints( startShape, requestedDirection, tracker )
	if not managerAvailable() or not shapeExists( startShape ) or
		not WirelessPipeManager.Sv_HasVirtualRoute( "link" ) then return {} end
	local endpointQueue, visitedEndpointIds, emittedShapeKeys, remote = {}, {}, {}, {}
	-- Output routing must not follow a wireless peer back into the same
	-- directional machine's input network. Input discovery is intentionally
	-- allowed to follow a peer located on the output-side storage network: that
	-- lets a Craftbot craft from the complete linked chest system while its
	-- finished items still remain on the output side.
	if requestedDirection == "output" and openingDirections( startShape ) then
		for _, component in ipairs( getStartComponents( startShape, "input", tracker ) ) do
			for _, endpoint in ipairs( component.endpoints ) do visitedEndpointIds[endpoint.endpointId] = true end
		end
	end
	appendComponentEndpoints( endpointQueue, visitedEndpointIds,
		getStartComponents( startShape, requestedDirection, tracker ) )
	local endpointHead = 1
	while endpointHead <= #endpointQueue do
		local origin = endpointQueue[endpointHead]
		endpointHead = endpointHead + 1
		for _, peer in ipairs( getPeerEntries( origin.endpointId ) ) do
			trackLeaseEndpoint( tracker, peer.endpointId )
			if peer.endpointId and not visitedEndpointIds[peer.endpointId] and shapeExists( peer.shape ) then
				visitedEndpointIds[peer.endpointId] = true
				local key = shapeKey( peer.shape )
				if not emittedShapeKeys[key] then emittedShapeKeys[key] = true; remote[#remote + 1] = peer.shape end
				local component = getPhysicalComponent( peer.shape, tracker )
				appendComponentEndpoints( endpointQueue, visitedEndpointIds, component and { component } or {} )
			end
		end
	end
	table.sort( remote, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
	return remote
end

local function discoverOriginEndpoints( startShape, requestedDirection, tracker )
	if not managerAvailable() or not shapeExists( startShape ) then return {} end
	local result, seen = {}, {}
	for _, component in ipairs( getStartComponents( startShape, requestedDirection, tracker ) ) do
		for _, endpoint in ipairs( component.endpoints ) do
			if endpoint.endpointId and not seen[endpoint.endpointId] then
				seen[endpoint.endpointId] = true
				result[#result + 1] = endpoint.shape
			end
		end
	end
	table.sort( result, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
	return result
end

local function discoverLinkedRoots( startShape, requestedDirection, tracker )
	local remote = discoverRemoteEndpoints( startShape, requestedDirection, tracker )
	-- An unpaired Link must retain exact vanilla behavior.
	if #remote == 0 then return {}, remote end
	local roots = {}
	appendUniqueShapes( roots, discoverOriginEndpoints( startShape, requestedDirection, tracker ) )
	appendUniqueShapes( roots, remote )
	return roots, remote
end

local function getPhysicalContainerShapes( rootShape, tracker )
	local component = getPhysicalComponent( rootShape, tracker )
	local containers = {}
	if component then appendUniqueShapes( containers, component.containers ) end
	return containers
end

local function directEntryStillValid( entry, tick )
	if entry.lastValidationTick and tick - entry.lastValidationTick < CACHE_INTERVAL_TICKS then
		return entry.lastValidationValid ~= false
	end
	entry.lastValidationTick = tick
	entry.lastValidationValid = bodiesStillValid( entry.bodies, entry.createdTick, tick )
	return entry.lastValidationValid
end

local function getDirectContainerShapes( rootShape, tracker )
	if not shapeExists( rootShape ) then return {} end
	local tick = ensureCacheState()
	local key = shapeKey( rootShape )
	local entry = physicalCache.directByShape[key]
	if entry and directEntryStillValid( entry, tick ) then
		performance.directCacheHits = performance.directCacheHits + 1
	else
		if entry then physicalCache.directByShape[key] = nil end
		entry = { createdTick = tick, lastValidationTick = tick, lastValidationValid = true, lastAccessTick = tick,
			rootShape = rootShape, shapes = {}, bodies = {} }
		local bodyKeys, seen = {}, {}
		addBody( entry.bodies, bodyKeys, rootShape )
		for _, shape in ipairs( unsortedNeighbours( rootShape ) ) do
			if shapeHasContainer( shape ) then
				local id = shapeKey( shape )
				if not seen[id] then seen[id] = true; entry.shapes[#entry.shapes + 1] = shape end
				addBody( entry.bodies, bodyKeys, shape )
			end
		end
		table.sort( entry.shapes, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
		physicalCache.directByShape[key] = entry
	end
	entry.lastAccessTick = tick
	if tracker and not tracker.directKeys[key] then
		tracker.directKeys[key] = true
		tracker.directEntries[#tracker.directEntries + 1] = entry
	end
	local result = {}
	appendUniqueShapes( result, entry.shapes )
	return result
end

prunePhysicalEntries = function( tick )
	physicalCache.lastPruneTick = tick
	performance.cachePrunes = performance.cachePrunes + 1
	local components = {}
	for _, component in pairs( physicalCache.componentsByShape ) do
		components[component.id] = component
	end
	for _, component in pairs( components ) do
		if tick - ( component.lastAccessTick or component.createdTick or tick ) > CACHE_MAX_IDLE_TICKS or
			not componentStillValid( component, tick ) then
			discardComponent( component )
		end
	end
	for key, entry in pairs( physicalCache.directByShape ) do
		if tick - ( entry.lastAccessTick or entry.createdTick or tick ) > CACHE_MAX_IDLE_TICKS or
			not directEntryStillValid( entry, tick ) then
			physicalCache.directByShape[key] = nil
		end
	end
	local function pruneIdleQueries( entries, countField )
		for key, entry in pairs( entries ) do
			if tick - ( entry.lastAccessTick or entry.createdTick or tick ) > CACHE_MAX_IDLE_TICKS then
				removeQueryEntry( entries, countField, key )
			end
		end
	end
	pruneIdleQueries( physicalCache.virtualQueries, "virtualQueryCount" )
	pruneIdleQueries( physicalCache.terminalQueries, "terminalQueryCount" )
	pruneIdleQueries( physicalCache.nativeQueries, "nativeQueryCount" )
	pruneIdleQueries( physicalCache.resolvedQueries, "resolvedQueryCount" )
	-- Shape objects are ordinary table keys in the restricted runtime. Clear
	-- the auxiliary map periodically; active objects are repopulated lazily.
	physicalCache.shapeKeys = {}
end

local function discoverDirectionalSourceEntries( startShape, requestedDirection, tracker )
	if not managerAvailable() or not shapeExists( startShape ) then return {} end
	if requestedDirection ~= nil and requestedDirection ~= "input" then return {} end
	if not WirelessPipeManager.Sv_HasVirtualRoute( "directional" ) then return {} end
	local result, seen = {}, {}
	for _, endpoint in ipairs( discoverOriginEndpoints( startShape, requestedDirection, tracker ) ) do
		local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( endpoint )
		if endpointId then
			for _, entry in ipairs( WirelessPipeManager.Sv_GetDirectionalSourceEntries( endpointId, "GRAPH" ) ) do
				trackLeaseEndpoint( tracker, entry.endpointId )
				if entry.endpointId and not seen[entry.endpointId] and shapeExists( entry.shape ) then
					seen[entry.endpointId] = true
					result[#result + 1] = entry
				end
			end
		end
	end
	table.sort( result, function( a, b ) return tostring( a.endpointId ) < tostring( b.endpointId ) end )
	return result
end

local function getDirectionalSourceContainerShapes( entry, tracker )
	if not entry or not shapeExists( entry.shape ) then return {} end
	if entry.directOnly ~= false then return getDirectContainerShapes( entry.shape, tracker ) end
	return getPhysicalContainerShapes( entry.shape, tracker )
end

local function discoverDirectionalDestinationEntries( startShape, requestedDirection, tracker )
	if not managerAvailable() or not shapeExists( startShape ) then return {} end
	if requestedDirection ~= "output" then return {} end
	if not WirelessPipeManager.Sv_HasVirtualRoute( "directional" ) then return {} end
	local result, seen = {}, {}
	for _, endpoint in ipairs( discoverOriginEndpoints( startShape, requestedDirection, tracker ) ) do
		local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( endpoint )
		if endpointId then
			for _, entry in ipairs( WirelessPipeManager.Sv_GetDirectionalDestinationEntries( endpointId, "GRAPH" ) ) do
				trackLeaseEndpoint( tracker, entry.endpointId )
				if entry.endpointId and not seen[entry.endpointId] and shapeExists( entry.shape ) then
					seen[entry.endpointId] = true
					result[#result + 1] = entry
				end
			end
		end
	end
	table.sort( result, function( a, b ) return tostring( a.endpointId ) < tostring( b.endpointId ) end )
	return result
end

local function getDirectionalDestinationContainerShapes( entry, tracker )
	if not entry or not shapeExists( entry.shape ) then return {} end
	if entry.directOnly ~= false then return getDirectContainerShapes( entry.shape, tracker ) end
	return getPhysicalContainerShapes( entry.shape, tracker )
end

local function trackerStillValid( tracker, tick )
	for _, component in ipairs( tracker.components or {} ) do
		if not componentStillValid( component, tick ) then return false end
	end
	for _, entry in ipairs( tracker.directEntries or {} ) do
		if not directEntryStillValid( entry, tick ) then return false end
	end
	return true
end

local function newTracker()
	return {
		components = {}, componentIds = {}, directEntries = {}, directKeys = {},
		leaseEndpointIds = {}, leaseEndpointSet = {}
	}
end

local function getVirtualContainerShapes( startShape, requestedDirection )
	if not managerAvailable() or not shapeExists( startShape ) or
		not WirelessPipeManager.Sv_HasVirtualRoute( requestedDirection ) then return {}, nil end
	local tick = ensureCacheState()
	local key = requestedDirection .. "|" .. shapeKey( startShape )
	local cached = physicalCache.virtualQueries[key]
	if cached and trackerStillValid( cached.tracker, tick ) then
		performance.virtualQueryHits = performance.virtualQueryHits + 1
		if #cached.shapes == 0 then
			performance.negativeVirtualCacheHits = performance.negativeVirtualCacheHits + 1
		end
		cached.lastAccessTick = tick
		renewTrackerLeases( cached.tracker, "GRAPH" )
		local result = {}
		appendUniqueShapes( result, cached.shapes )
		return result, cached.tracker
	elseif cached then
		removeQueryEntry( physicalCache.virtualQueries, "virtualQueryCount", key )
	end

	local tracker, result = newTracker(), {}
	local linkedRoots = discoverLinkedRoots( startShape, requestedDirection, tracker )
	for _, root in ipairs( linkedRoots ) do appendUniqueShapes( result, getPhysicalContainerShapes( root, tracker ) ) end
	if requestedDirection == "input" then
		for _, entry in ipairs( discoverDirectionalSourceEntries( startShape, requestedDirection, tracker ) ) do
			appendUniqueShapes( result, getDirectionalSourceContainerShapes( entry, tracker ) )
		end
	elseif requestedDirection == "output" then
		for _, entry in ipairs( discoverDirectionalDestinationEntries( startShape, requestedDirection, tracker ) ) do
			appendUniqueShapes( result, getDirectionalDestinationContainerShapes( entry, tracker ) )
		end
	end
	storeQueryEntry( physicalCache.virtualQueries, "virtualQueryCount", key,
		{ shapes = result, tracker = tracker, createdTick = tick, lastAccessTick = tick } )
	renewTrackerLeases( tracker, "GRAPH" )
	local output = {}
	appendUniqueShapes( output, result )
	return output, tracker
end

local function copyShapeList( shapes )
	local result = {}
	appendUniqueShapes( result, shapes )
	return result
end

local function nativeEntryStillValid( entry, tick )
	if not entry or not shapeExists( entry.rootShape ) then return false end
	if entry.lastValidationTick and tick - entry.lastValidationTick < CACHE_INTERVAL_TICKS then
		return entry.lastValidationValid ~= false
	end
	entry.lastValidationTick = tick
	if entry.tracker == nil or not trackerStillValid( entry.tracker, tick ) then
		entry.lastValidationValid = false
		return false
	end
	for _, shape in ipairs( entry.shapes or {} ) do
		if not shapeExists( shape ) then
			entry.lastValidationValid = false
			return false
		end
	end
	entry.lastValidationValid = true
	return true
end

local function getNativeShapeList( nativeFunction, startShape, requestedDirection )
	local tick = ensureCacheState()
	local key = requestedDirection .. "|" .. shapeKey( startShape )
	local cached = physicalCache.nativeQueries[key]
	if cached and nativeEntryStillValid( cached, tick ) then
		cached.lastAccessTick = tick
		performance.nativeCacheHits = performance.nativeCacheHits + 1
		return copyShapeList( cached.shapes ), cached
	elseif cached then
		performance.nativeQueryInvalidations = performance.nativeQueryInvalidations + 1
		removeQueryEntry( physicalCache.nativeQueries, "nativeQueryCount", key )
	end

	performance.nativeCalls = performance.nativeCalls + 1
	local nativeShapes = nativeFunction( startShape )
	local stored = copyShapeList( nativeShapes )
	local tracker = newTracker()
	local trackerOk = pcall( function()
		getStartComponents( startShape, requestedDirection, tracker )
	end )
	if not trackerOk then tracker = nil end
	local entry = {
		rootShape = startShape,
		shapes = stored,
		tracker = tracker,
		createdTick = tick,
		lastValidationTick = tick,
		lastValidationValid = tracker ~= nil,
		lastAccessTick = tick
	}
	storeQueryEntry( physicalCache.nativeQueries, "nativeQueryCount", key, entry )
	return copyShapeList( stored ), entry
end

local function resolvedEntryStillValid( entry, tick )
	if not entry or not shapeExists( entry.rootShape ) then return false end
	if entry.lastValidationTick and tick - entry.lastValidationTick < CACHE_INTERVAL_TICKS then
		return entry.lastValidationValid ~= false
	end
	entry.lastValidationTick = tick
	if not nativeEntryStillValid( entry.nativeEntry, tick ) or
		( entry.virtualTracker ~= nil and not trackerStillValid( entry.virtualTracker, tick ) ) then
		entry.lastValidationValid = false
		return false
	end
	for _, shape in ipairs( entry.shapes or {} ) do
		if not shapeExists( shape ) then
			entry.lastValidationValid = false
			return false
		end
	end
	entry.lastValidationValid = true
	return true
end

local function extendNativeShapeList( nativeFunction, startShape, requestedDirection )
	local tick = ensureCacheState()
	local key = requestedDirection .. "|" .. shapeKey( startShape )
	local cached = physicalCache.resolvedQueries[key]
	if cached and resolvedEntryStillValid( cached, tick ) then
		cached.lastAccessTick = tick
		performance.resolvedQueryHits = performance.resolvedQueryHits + 1
		if cached.virtualTracker == nil then
			performance.fastPathReturns = performance.fastPathReturns + 1
		else
			renewTrackerLeases( cached.virtualTracker, "GRAPH" )
		end
		return copyShapeList( cached.shapes ), cached.virtualTracker
	elseif cached then
		performance.resolvedQueryInvalidations = performance.resolvedQueryInvalidations + 1
		removeQueryEntry( physicalCache.resolvedQueries, "resolvedQueryCount", key )
	end

	local localResults, nativeEntry = getNativeShapeList( nativeFunction, startShape, requestedDirection )
	local tracker = nil
	if not managerAvailable() or not WirelessPipeManager.Sv_HasVirtualRoute( requestedDirection ) then
		performance.fastPathReturns = performance.fastPathReturns + 1
	else
		local ok, virtualShapes, queryTracker = pcall( function()
			local shapes, value = getVirtualContainerShapes( startShape, requestedDirection )
			return shapes, value
		end )
		if not ok then return localResults, nil end
		appendUniqueShapes( localResults, virtualShapes )
		tracker = queryTracker
	end
	local entry = {
		rootShape = startShape,
		shapes = localResults,
		nativeEntry = nativeEntry,
		virtualTracker = tracker,
		createdTick = tick,
		lastValidationTick = tick,
		lastValidationValid = nativeEntry ~= nil and nativeEntry.lastValidationValid ~= false,
		lastAccessTick = tick
	}
	storeQueryEntry( physicalCache.resolvedQueries, "resolvedQueryCount", key, entry )
	return copyShapeList( localResults ), tracker
end

function ScrapLabPipeGraph.getInputContainers( shape )
	return extendNativeShapeList( sm.pipeGraph.getInputContainers, shape, "input" )
end

-- Dedicated name keeps protected vanilla call counts stable while Crafter asks
-- the server for authoritative GUI containers.
function ScrapLabPipeGraph.getGuiInputContainers( shape )
	local shapes, tracker = extendNativeShapeList( sm.pipeGraph.getInputContainers, shape, "input" )
	local pending = tracker ~= nil and #tracker.leaseEndpointIds > 0 and
		not WirelessPipeManager.Sv_AreEndpointLeasesSettled( tracker.leaseEndpointIds )
	return shapes, pending
end

function ScrapLabPipeGraph.getOutputContainers( shape )
	return extendNativeShapeList( sm.pipeGraph.getOutputContainers, shape, "output" )
end

-- Consumers that retain a container list (rather than querying every action)
-- can cheaply rebuild only when wireless topology or remote-cell readiness
-- changes. No graph scan or lease request is performed here.
function ScrapLabPipeGraph.getTopologyRevision()
	return managerAvailable() and WirelessPipeManager.Sv_GetTopologyRevision() or nil
end

-- Local-only physical view for SEND/RECEIVE routing. It never follows a
-- wireless peer and shares the same bounded component cache as Link queries.
function ScrapLabPipeGraph.getLocalPhysicalContainerShapes( shape )
	if not shapeExists( shape ) then return {} end
	local ok, containers = pcall( function() return getPhysicalContainerShapes( shape ) end )
	return ok and containers or {}
end

function ScrapLabPipeGraph.getDirectContainerShapes( shape )
	if not shapeExists( shape ) then return {} end
	local ok, containers = pcall( function() return getDirectContainerShapes( shape ) end )
	return ok and containers or {}
end

local function getContainerDescriptor( shape, route )
	if not shapeExists( shape ) then return nil end
	local ok, descriptor = pcall( function()
		local interactable = shape:getInteractable()
		local container = interactable and interactable:getContainer( 0 ) or nil
		if not container then return nil end
		local id = sm.container.getId( container )
		if id == nil then return nil end
		return {
			id = tostring( id ),
			shape = shape,
			container = container,
			worldId = getWorldId( shape ),
			worldLabel = route.worldLabel,
			wireless = route.wireless == true,
			crossWorld = route.crossWorld == true,
			routeKind = route.routeKind or "LOCAL",
			routePriority = route.wireless and 1 or 0,
			routeDistance = route.routeDistance or 0,
			endpointId = route.endpointId,
			directOnly = route.directOnly
		}
	end )
	return ok and descriptor or nil
end

local function appendTerminalShapes( descriptors, byId, shapes, route )
	for _, shape in ipairs( shapes or {} ) do
		local descriptor = getContainerDescriptor( shape, route )
		if descriptor then
			local previous = byId[descriptor.id]
			if not previous or descriptor.routePriority < previous.routePriority or
				( descriptor.routePriority == previous.routePriority and descriptor.routeDistance < previous.routeDistance ) then
				if previous then
					for index, value in ipairs( descriptors ) do
						if value == previous then descriptors[index] = descriptor; break end
					end
				else descriptors[#descriptors + 1] = descriptor end
				byId[descriptor.id] = descriptor
			end
		end
	end
end

local function terminalLocalWorld( shape )
	local ok, world = pcall( function() return shape:getBody():getWorld() end )
	if not ok or not world then return "?", "LOCAL WORLD" end
	local publicData = world.publicData or {}
	local kind = tostring( publicData.type or "" )
	local label = kind == "Overworld" and "OVERWORLD"
		or ( kind == "UndergroundWorld" and ( "UNDERGROUND - DEPTH " .. tostring( publicData.depth or "?" ) ) )
		or ( kind ~= "" and string.upper( kind:gsub( "_", " " ) ) )
		or ( "WORLD " .. tostring( world.id or "?" ) )
	return tostring( world.id or world ), label
end

local function buildTerminalContainersUncached( startShape, requestedDirection )
	local descriptors, byId, tracker = {}, {}, newTracker()
	if not shapeExists( startShape ) then
		return descriptors, {
			wirelessInstalled = true, managerAvailable = managerAvailable(),
			wirelessState = "OFFLINE", compatibilityReason = "TERMINAL SHAPE UNAVAILABLE",
			topologyGeneration = managerAvailable() and WirelessPipeManager.Sv_GetTopologyRevision() or 0,
			worlds = {}, reachableWorlds = 0
		}, tracker
	end

	local localWorldId, localWorldLabel = terminalLocalWorld( startShape )
	for _, component in ipairs( getStartComponents( startShape, requestedDirection, tracker ) ) do
		appendTerminalShapes( descriptors, byId, component.containers, {
			wireless = false, routeKind = "LOCAL", routeDistance = 0,
			worldLabel = localWorldLabel
		} )
	end

	local state = {
		wirelessInstalled = true,
		managerAvailable = managerAvailable(),
		wirelessState = "LOCAL_ONLY",
		compatibilityReason = nil,
		topologyGeneration = managerAvailable() and WirelessPipeManager.Sv_GetTopologyRevision() or 0,
		matchingEndpoints = 0,
		readyEndpoints = 0,
		limitedEndpoints = 0,
		offlineEndpoints = 0,
		worlds = { localWorldLabel },
		reachableWorlds = 1,
		crossWorld = false,
		localOnly = true
	}
	if not state.managerAvailable then
		state.wirelessState = "OFFLINE"
		state.compatibilityReason = "WIRELESS MANAGER UNAVAILABLE"
		return descriptors, state, tracker
	end

	local worldLabels, worldIds = { [localWorldLabel] = true }, { [localWorldId] = true }
	local peerIds = {}
	for _, endpoint in ipairs( discoverOriginEndpoints( startShape, requestedDirection, tracker ) ) do
		local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( endpoint )
		if endpointId then
			for _, peer in ipairs( WirelessPipeManager.Sv_GetTerminalPeerEntries( endpointId, requestedDirection ) ) do
				if peer.endpointId and not peerIds[peer.endpointId] then
					trackLeaseEndpoint( tracker, peer.endpointId )
					peerIds[peer.endpointId] = true
					state.matchingEndpoints = state.matchingEndpoints + 1
					if peer.limited then state.limitedEndpoints = state.limitedEndpoints + 1 end
					if peer.ready and peer.shape then
						state.readyEndpoints = state.readyEndpoints + 1
						local peerWorldId = tostring( peer.worldId or "?" )
						local peerWorldLabel = peer.worldLabel or ( "WORLD " .. peerWorldId )
						worldIds[peerWorldId] = true
						worldLabels[peerWorldLabel] = true
						if peerWorldId ~= localWorldId then state.crossWorld = true end
						local shapes = peer.directOnly and getDirectContainerShapes( peer.shape, tracker )
							or getPhysicalContainerShapes( peer.shape, tracker )
						appendTerminalShapes( descriptors, byId, shapes, {
							wireless = true,
							crossWorld = peerWorldId ~= localWorldId,
							routeKind = peer.mode == "LINK" and "LINK" or peer.mode,
							routeDistance = 1,
							endpointId = peer.endpointId,
							directOnly = peer.directOnly,
							worldLabel = peerWorldLabel
						} )
					else state.offlineEndpoints = state.offlineEndpoints + 1 end
				end
			end
		end
	end

	state.worlds = {}
	for label in pairs( worldLabels ) do state.worlds[#state.worlds + 1] = label end
	table.sort( state.worlds )
	state.reachableWorlds = 0
	for _ in pairs( worldIds ) do state.reachableWorlds = state.reachableWorlds + 1 end
	if state.matchingEndpoints == 0 then state.wirelessState = "LOCAL_ONLY"
	elseif state.limitedEndpoints > 0 then state.wirelessState = "LIMITED"
	elseif state.readyEndpoints == 0 then state.wirelessState = "OFFLINE"
	elseif state.offlineEndpoints > 0 then state.wirelessState = "LIMITED"
	else state.wirelessState = "READY" end
	state.localOnly = state.readyEndpoints == 0
	table.sort( descriptors, function( a, b )
		if a.routePriority ~= b.routePriority then return a.routePriority < b.routePriority end
		if a.routeDistance ~= b.routeDistance then return a.routeDistance < b.routeDistance end
		return a.id < b.id
	end )
	return descriptors, state, tracker
end

local function terminalEntryStillValid( entry, tick )
	if not entry then return false end
	if entry.lastValidationTick and tick - entry.lastValidationTick < CACHE_INTERVAL_TICKS then
		return entry.lastValidationValid ~= false
	end
	entry.lastValidationTick = tick
	if not trackerStillValid( entry.tracker, tick ) then
		entry.lastValidationValid = false
		return false
	end
	for _, descriptor in ipairs( entry.descriptors or {} ) do
		if not shapeExists( descriptor.shape ) then
			entry.lastValidationValid = false
			return false
		end
		local ok, exists = pcall( function()
			return descriptor.container ~= nil and sm.exists( descriptor.container )
		end )
		if not ok or not exists then
			entry.lastValidationValid = false
			return false
		end
	end
	entry.lastValidationValid = true
	return true
end

local function getTerminalContainers( shape, requestedDirection )
	local tick = ensureCacheState()
	local key = "terminal|" .. requestedDirection .. "|" .. shapeKey( shape )
	local cached = physicalCache.terminalQueries[key]
	if cached and terminalEntryStillValid( cached, tick ) then
		cached.lastAccessTick = tick
		performance.terminalCacheHits = performance.terminalCacheHits + 1
		renewTrackerLeases( cached.tracker, "TERMINAL" )
		-- Borrowed read-only views avoid rebuilding the same descriptor graph for
		-- every terminal poll. Callers must never mutate either table.
		return cached.descriptors, cached.state
	elseif cached then
		removeQueryEntry( physicalCache.terminalQueries, "terminalQueryCount", key )
	end
	local descriptors, state, tracker = buildTerminalContainersUncached( shape, requestedDirection )
	storeQueryEntry( physicalCache.terminalQueries, "terminalQueryCount", key, {
		descriptors = descriptors,
		state = state,
		tracker = tracker,
		createdTick = tick,
		lastValidationTick = tick,
		lastValidationValid = true,
		lastAccessTick = tick
	} )
	renewTrackerLeases( tracker, "TERMINAL" )
	return descriptors, state
end

-- Storage terminals use these descriptor queries instead of duplicating pipe
-- traversal. Spend follows local/Link plus RECEIVE <- SEND routes; collect
-- follows local/Link plus SEND -> RECEIVE routes.
function ScrapLabPipeGraph.getTerminalSpendContainers( shape )
	local ok, descriptors, state = pcall( getTerminalContainers, shape, "input" )
	if ok then return descriptors, state end
	return {}, { wirelessInstalled = true, managerAvailable = managerAvailable(), wirelessState = "OFFLINE",
		compatibilityReason = tostring( descriptors ), topologyGeneration = 0, worlds = {}, reachableWorlds = 0, localOnly = true }
end

function ScrapLabPipeGraph.getTerminalCollectContainers( shape )
	local ok, descriptors, state = pcall( getTerminalContainers, shape, "output" )
	if ok then return descriptors, state end
	return {}, { wirelessInstalled = true, managerAvailable = managerAvailable(), wirelessState = "OFFLINE",
		compatibilityReason = tostring( descriptors ), topologyGeneration = 0, worlds = {}, reachableWorlds = 0, localOnly = true }
end

local function validateCollectRequest( container, items, quantities )
	if type( items ) ~= "table" or type( quantities ) ~= "table" or #items == 0 or #items ~= #quantities then return false end
	local requests, requestOrder = {}, {}
	for index, itemUuid in ipairs( items ) do
		local quantity = quantities[index]
		if itemUuid == nil or type( quantity ) ~= "number" or quantity <= 0 or quantity ~= math.floor( quantity ) then return false end
		local key = tostring( itemUuid )
		if requests[key] == nil then requests[key] = { uuid = itemUuid, quantity = 0 }; requestOrder[#requestOrder + 1] = key end
		requests[key].quantity = requests[key].quantity + quantity
	end
	for _, key in ipairs( requestOrder ) do
		local request = requests[key]
		if not sm.container.canCollect( container, request.uuid, request.quantity ) then return false end
	end
	if #requestOrder == 1 then return true end
	local emptySlots, existingCapacity = 0, {}
	local maxStackSize = container:getMaxStackSize()
	if type( maxStackSize ) ~= "number" or maxStackSize < 1 then return false end
	for slot = 0, container:getSize() - 1 do
		local item = container:getItem( slot )
		if item == nil or item.uuid == nil or item.uuid:isNil() then
			emptySlots = emptySlots + 1
		else
			local key = tostring( item.uuid )
			if requests[key] ~= nil then
				local stackSize = sm.item.isTool( item.uuid ) and 1 or maxStackSize
				existingCapacity[key] = ( existingCapacity[key] or 0 ) + math.max( 0, stackSize - ( item.quantity or 0 ) )
			end
		end
	end
	local slotsRequired = 0
	for _, key in ipairs( requestOrder ) do
		local request = requests[key]
		local remaining = math.max( 0, request.quantity - ( existingCapacity[key] or 0 ) )
		local stackSize = sm.item.isTool( request.uuid ) and 1 or maxStackSize
		slotsRequired = slotsRequired + math.ceil( remaining / stackSize )
	end
	return slotsRequired <= emptySlots
end

local function validateSpendRequest( container, itemUuid, quantity )
	return itemUuid ~= nil and type( quantity ) == "number" and quantity > 0
		and quantity == math.floor( quantity ) and sm.container.canSpend( container, itemUuid, quantity )
end

local function extendNativeSelection( selectionKind, nativeFunction, shape, itemUuid, quantity )
	local localResult = nativeFunction( shape, itemUuid, quantity )
	if localResult ~= nil or not managerAvailable() then return localResult end
	local requestedDirection = selectionKind == "collect" and "output" or "input"
	if not WirelessPipeManager.Sv_HasVirtualRoute( requestedDirection ) then return localResult end
	local ok, selected = pcall( function()
		for _, candidate in ipairs( getVirtualContainerShapes( shape, requestedDirection ) ) do
			local interactable = candidate:getInteractable()
			local container = interactable and interactable:getContainer( 0 ) or nil
			if container then
				local accepted = selectionKind == "collect"
					and validateCollectRequest( container, itemUuid, quantity )
					or validateSpendRequest( container, itemUuid, quantity )
				if accepted then return candidate end
			end
		end
		return nil
	end )
	return ok and selected or localResult
end

function ScrapLabPipeGraph.getContainerShapeToCollectTo( shape, itemUuid, quantity )
	return extendNativeSelection( "collect", sm.pipeGraph.getContainerShapeToCollectTo, shape, itemUuid, quantity )
end

function ScrapLabPipeGraph.getContainerShapeToSpendFrom( shape, itemUuid, quantity )
	return extendNativeSelection( "spend", sm.pipeGraph.getContainerShapeToSpendFrom, shape, itemUuid, quantity )
end

local function getResourceConnectionTypes()
	if sm.interactable == nil or sm.interactable.connectionType == nil then return {} end
	return {
		sm.interactable.connectionType.water,
		sm.interactable.connectionType.gasoline,
		sm.interactable.connectionType.electricity,
		sm.interactable.connectionType.ammo,
		sm.interactable.connectionType.chemical
	}
end

local function outputTypesOf( interactable )
	local types = {}
	for _, connectionType in ipairs( getResourceConnectionTypes() ) do
		local ok, matches = pcall( function() return interactable:hasOutputType( connectionType ) end )
		if ok and matches then types[#types + 1] = connectionType end
	end
	return types
end

local function matchesAnyOutputType( interactable, outputTypes )
	for _, connectionType in ipairs( outputTypes ) do
		local ok, matches = pcall( function() return interactable:hasOutputType( connectionType ) end )
		if ok and matches then return true end
	end
	return false
end

function ScrapLabPipeGraph.getMatchingPipedContainers( connectedInteractable )
	local localResults = sm.pipeGraph.getMatchingPipedContainers( connectedInteractable )
	if not managerAvailable() or not connectedInteractable or
		not WirelessPipeManager.Sv_HasVirtualRoute( "input" ) then return localResults end
	local ok, extended = pcall( function()
		local startShape = connectedInteractable.shape
		if not shapeExists( startShape ) then return localResults end
		local outputTypes = outputTypesOf( connectedInteractable )
		if #outputTypes == 0 then return localResults end
		local result, seen = {}, {}
		for _, container in ipairs( localResults ) do
			local id = tostring( sm.container.getId( container ) )
			seen[id] = true
			result[#result + 1] = container
		end
		for _, shape in ipairs( getVirtualContainerShapes( startShape, "input" ) ) do
			local interactable = shape:getInteractable()
			if interactable and matchesAnyOutputType( interactable, outputTypes ) then
				local container = interactable:getContainer( 0 )
				if container then
					local id = tostring( sm.container.getId( container ) )
					if not seen[id] then seen[id] = true; result[#result + 1] = container end
				end
			end
		end
		return result
	end )
	return ok and extended or localResults
end

function ScrapLabPipeGraph.getVisualRoute( fromShape, toShape, direction )
	if not shapeExists( fromShape ) or not shapeExists( toShape ) then return {} end
	if getWorldId( fromShape ) ~= getWorldId( toShape ) then return {} end
	return sm.pipeGraph.getContainerPath( fromShape, toShape, direction )
end

function ScrapLabPipeGraph.debugDiscoverRemoteEndpoints( shape )
	local ok, result = pcall( function() return discoverRemoteEndpoints( shape ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugDiscoverOriginEndpoints( shape, requestedDirection )
	local ok, result = pcall( function() return discoverOriginEndpoints( shape, requestedDirection ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugDiscoverLinkedRoots( shape, requestedDirection )
	local ok, result = pcall( function() return discoverLinkedRoots( shape, requestedDirection ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetLinkedContainerShapes( shape, requestedDirection )
	local ok, result = pcall( function() return getVirtualContainerShapes( shape, requestedDirection or "input" ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetPhysicalContainerShapes( shape )
	local ok, result = pcall( function() return getPhysicalContainerShapes( shape ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetDirectContainerShapes( shape )
	return ScrapLabPipeGraph.getDirectContainerShapes( shape )
end

function ScrapLabPipeGraph.debugGetDirectionalSourceEntries( shape, requestedDirection )
	local ok, result = pcall( function() return discoverDirectionalSourceEntries( shape, requestedDirection ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetDirectionalDestinationEntries( shape, requestedDirection )
	local ok, result = pcall( function() return discoverDirectionalDestinationEntries( shape, requestedDirection or "output" ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetPerformanceSnapshot()
	return {
		cacheIntervalTicks = CACHE_INTERVAL_TICKS,
		queryCacheLimit = MAX_QUERY_CACHE_ENTRIES,
		queryCacheTrimTarget = QUERY_CACHE_TRIM_TARGET,
		nativeQueryEntries = physicalCache.nativeQueryCount,
		resolvedQueryEntries = physicalCache.resolvedQueryCount,
		nativeCalls = performance.nativeCalls,
		nativeCacheHits = performance.nativeCacheHits,
		nativeQueryInvalidations = performance.nativeQueryInvalidations,
		resolvedQueryHits = performance.resolvedQueryHits,
		resolvedQueryInvalidations = performance.resolvedQueryInvalidations,
		fastPathReturns = performance.fastPathReturns,
		physicalScans = performance.physicalScans,
		physicalNodes = performance.physicalNodes,
		componentCacheHits = performance.componentCacheHits,
		directCacheHits = performance.directCacheHits,
		virtualQueryHits = performance.virtualQueryHits,
		negativeVirtualCacheHits = performance.negativeVirtualCacheHits,
		terminalCacheHits = performance.terminalCacheHits,
		componentInvalidations = performance.componentInvalidations,
		cachePrunes = performance.cachePrunes,
		queryCacheTrims = performance.queryCacheTrims
	}
end

function ScrapLabPipeGraph.debugResetPerformance()
	for key in pairs( performance ) do performance[key] = 0 end
end

function ScrapLabPipeGraph.debugClearTopologyCache()
	peerCache = { revision = nil, entries = {} }
	physicalCache.revision = nil
	physicalCache.lastPruneTick = 0
	resetPhysicalEntries()
end

-- Future ScrapLab container parts can opt into the virtual graph explicitly.
function ScrapLabPipeGraph.registerContainerUuid( uuid )
	REGISTERED_CONTAINER_UUIDS[string.lower( tostring( uuid ) )] = true
	ScrapLabPipeGraph.debugClearTopologyCache()
end
