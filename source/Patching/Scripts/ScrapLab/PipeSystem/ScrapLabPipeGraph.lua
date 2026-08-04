-- SCRAPLAB WIRELESS PIPE GRAPH v6
-- Phase 3: conservative virtual Link traversal layered over the native pipe graph.
-- Native local results always come first. If wireless discovery is unavailable or
-- fails, callers receive the exact native result without a partial virtual graph.

ScrapLabPipeGraph = ScrapLabPipeGraph or {}
ScrapLabPipeGraph.DEFINITION_VERSION = 6

local WIRELESS_PIPE_UUID = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local MAX_PHYSICAL_SHAPES = 4096
local MAX_WIRELESS_ENDPOINTS = 256

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
	return getWorldId( shape ) .. ":" .. getShapeId( shape )
end

local function isWirelessEndpoint( shape )
	if not shapeExists( shape ) then return false end
	local ok, uuid = pcall( function() return shape:getShapeUuid() end )
	return ok and uuid == WIRELESS_PIPE_UUID
end

local function sortedNeighbours( shape )
	local neighbours = shape:getPipedNeighbours()
	table.sort( neighbours, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
	return neighbours
end

local function walkPhysicalGraph( rootShape, visitor, initiallyVisited )
	local queue = { rootShape }
	local head = 1
	local visited = {}
	for key, value in pairs( initiallyVisited or {} ) do visited[key] = value end
	local count = 0
	while head <= #queue do
		local shape = queue[head]
		head = head + 1
		if shapeExists( shape ) then
			local key = shapeKey( shape )
			if not visited[key] then
				visited[key] = true
				count = count + 1
				if count > MAX_PHYSICAL_SHAPES then error( "physical pipe graph safety limit exceeded" ) end
				local recurse = visitor( shape ) ~= false
				if recurse then
					for _, neighbour in ipairs( sortedNeighbours( shape ) ) do
						local neighbourKey = shapeKey( neighbour )
						if not visited[neighbourKey] then queue[#queue + 1] = neighbour end
					end
				end
			end
		end
	end
end

local function openingDirections( shape )
	if not shapeExists( shape ) then return nil end
	return PIPE_OPENING_DIRECTIONS[string.lower( tostring( shape:getShapeUuid() ) )]
end

local function directionalNeighbours( shape, requestedDirection )
	local directions = openingDirections( shape )
	if not directions or not requestedDirection then return sortedNeighbours( shape ) end
	local offsets = shape:getPipeOffsets()
	if #offsets ~= #directions then error( "directional pipe opening catalog mismatch" ) end
	local openingPositions = {}
	for index, offset in ipairs( offsets ) do openingPositions[index] = shape:transformLocalPoint( offset ) end
	local result = {}
	for _, neighbour in ipairs( sortedNeighbours( shape ) ) do
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

local function managerAvailable()
	return g_wirelessPipeManager ~= nil
		and WirelessPipeManager ~= nil
		and WirelessPipeManager.Sv_GetEndpointIdForShape ~= nil
		and WirelessPipeManager.Sv_GetLinkPeerEntries ~= nil
		and WirelessPipeManager.Sv_GetDirectionalSourceEntries ~= nil
		and WirelessPipeManager.Sv_GetTopologyRevision ~= nil
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
			if not shapeExists( entry.shape ) then
				cached = nil
				peerCache.entries[endpointId] = nil
				break
			end
		end
	end
	if not cached then
		cached = WirelessPipeManager.Sv_GetLinkPeerEntries( endpointId )
		table.sort( cached, function( a, b ) return tostring( a.endpointId ) < tostring( b.endpointId ) end )
		peerCache.entries[endpointId] = cached
	end
	return cached
end

local function appendUniqueShapes( target, additions )
	local seen = {}
	for _, shape in ipairs( target ) do seen[shapeKey( shape )] = true end
	for _, shape in ipairs( additions ) do
		local key = shapeKey( shape )
		if not seen[key] then
			seen[key] = true
			target[#target + 1] = shape
		end
	end
	return target
end

-- Returns remote Link endpoint shapes in deterministic breadth-first order.
-- A physical graph may contain more than one Link endpoint, so every newly
-- reached remote network is scanned for additional Link groups as well.
local function discoverRemoteEndpoints( startShape, requestedDirection )
	if not managerAvailable() or not shapeExists( startShape ) then return {} end
	local endpointQueue = {}
	local endpointHead = 1
	local visitedEndpointIds = {}
	local emittedShapeKeys = {}
	local remote = {}

	local function visitPhysicalShape( shape )
			if isWirelessEndpoint( shape ) then
				local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( shape )
				if endpointId and not visitedEndpointIds[endpointId] then
					visitedEndpointIds[endpointId] = true
					endpointQueue[#endpointQueue + 1] = { endpointId = endpointId, shape = shape }
					if #endpointQueue > MAX_WIRELESS_ENDPOINTS then error( "wireless endpoint safety limit exceeded" ) end
				end
			end
			-- Directional interactables terminate a pipe branch. Walking through one
			-- would incorrectly join its input side to its output side.
			if shape ~= startShape and openingDirections( shape ) then return false end
			return true
	end

	local function enqueuePhysicalLinks( rootShape, direction )
		if rootShape == startShape and openingDirections( rootShape ) and direction then
			local visitedRoot = { [shapeKey( rootShape )] = true }
			-- Endpoints on the requester's opposite port may share the same paint
			-- channel. Mark them before following peers so a wireless cycle cannot
			-- re-enter the machine through the wrong side.
			local oppositeDirection = direction == "input" and "output" or "input"
			for _, neighbour in ipairs( directionalNeighbours( rootShape, oppositeDirection ) ) do
				walkPhysicalGraph( neighbour, function( shape )
					if isWirelessEndpoint( shape ) then
						local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( shape )
						if endpointId then visitedEndpointIds[endpointId] = true end
					end
					if openingDirections( shape ) then return false end
					return true
				end, visitedRoot )
			end
			for _, neighbour in ipairs( directionalNeighbours( rootShape, direction ) ) do
				walkPhysicalGraph( neighbour, visitPhysicalShape, visitedRoot )
			end
		else
			walkPhysicalGraph( rootShape, visitPhysicalShape )
		end
	end

	enqueuePhysicalLinks( startShape, requestedDirection )
	while endpointHead <= #endpointQueue do
		local origin = endpointQueue[endpointHead]
		endpointHead = endpointHead + 1
		for _, peer in ipairs( getPeerEntries( origin.endpointId ) ) do
			if peer.endpointId and not visitedEndpointIds[peer.endpointId] and shapeExists( peer.shape ) then
				visitedEndpointIds[peer.endpointId] = true
				endpointQueue[#endpointQueue + 1] = peer
				if #endpointQueue > MAX_WIRELESS_ENDPOINTS then error( "wireless endpoint safety limit exceeded" ) end
				local key = shapeKey( peer.shape )
				if not emittedShapeKeys[key] then
					emittedShapeKeys[key] = true
					remote[#remote + 1] = peer.shape
				end
				enqueuePhysicalLinks( peer.shape )
			end
		end
	end
	return remote
end

-- Native pipe queries are authoritative for the machine's ordinary local
-- network, but a neutral Link pipe is not itself an input/output consumer. On a
-- bus with several matching Links, relying on the native result alone can omit
-- a container attached to an origin-side Link network. Enumerate those Link
-- roots explicitly and merge them with every peer root so the color channel is
-- exposed as one conjoined container network.
local function discoverOriginEndpoints( startShape, requestedDirection )
	if not managerAvailable() or not shapeExists( startShape ) then return {} end
	local result, seen = {}, {}
	local function visit( shape )
		if isWirelessEndpoint( shape ) then
			local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( shape )
			if endpointId and not seen[endpointId] then
				seen[endpointId] = true
				result[#result + 1] = shape
			end
		end
		if shape ~= startShape and openingDirections( shape ) then return false end
		return true
	end
	if openingDirections( startShape ) and requestedDirection then
		local visitedRoot = { [shapeKey( startShape )] = true }
		for _, neighbour in ipairs( directionalNeighbours( startShape, requestedDirection ) ) do
			walkPhysicalGraph( neighbour, visit, visitedRoot )
		end
	else
		walkPhysicalGraph( startShape, visit )
	end
	table.sort( result, function( a, b ) return shapeKey( a ) < shapeKey( b ) end )
	return result
end

local function discoverLinkedRoots( startShape, requestedDirection )
	local remote = discoverRemoteEndpoints( startShape, requestedDirection )
	-- An unpaired Link must retain exact vanilla behavior.
	if #remote == 0 then return {}, remote end
	local roots = {}
	appendUniqueShapes( roots, discoverOriginEndpoints( startShape, requestedDirection ) )
	appendUniqueShapes( roots, remote )
	return roots, remote
end

local function isRegisteredContainerShape( shape )
	if not shapeExists( shape ) then return false end
	return REGISTERED_CONTAINER_UUIDS[string.lower( tostring( shape:getShapeUuid() ) )] == true
end

local function getPhysicalContainerShapes( rootShape )
	local containers = {}
	walkPhysicalGraph( rootShape, function( shape )
		if isRegisteredContainerShape( shape ) then containers[#containers + 1] = shape end
		if shape ~= rootShape and openingDirections( shape ) then return false end
		return true
	end )
	return containers
end

local function getDirectContainerShapes( rootShape )
	local containers = {}
	if not shapeExists( rootShape ) then return containers end
	for _, shape in ipairs( sortedNeighbours( rootShape ) ) do
		if isRegisteredContainerShape( shape ) then containers[#containers + 1] = shape end
	end
	return containers
end

local function discoverDirectionalSourceEntries( startShape, requestedDirection )
	if not managerAvailable() or not shapeExists( startShape ) then return {} end
	if requestedDirection ~= nil and requestedDirection ~= "input" then return {} end
	local result, seen = {}, {}
	for _, endpoint in ipairs( discoverOriginEndpoints( startShape, requestedDirection ) ) do
		local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( endpoint )
		if endpointId then
			for _, entry in ipairs( WirelessPipeManager.Sv_GetDirectionalSourceEntries( endpointId ) ) do
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

local function getDirectionalSourceContainerShapes( entry )
	if not entry or not shapeExists( entry.shape ) then return {} end
	if entry.directOnly ~= false then return getDirectContainerShapes( entry.shape ) end
	return getPhysicalContainerShapes( entry.shape )
end

local function extendNativeShapeList( nativeFunction, startShape, requestedDirection )
	local localResults = nativeFunction( startShape )
	if not managerAvailable() then return localResults end
	local ok, extended = pcall( function()
		local result = {}
		for _, shape in ipairs( localResults ) do result[#result + 1] = shape end
		local linkedRoots = discoverLinkedRoots( startShape, requestedDirection )
		for _, remoteEndpoint in ipairs( linkedRoots ) do
			-- A neutral Pipe root has no input/output direction, so vanilla returns
			-- zero containers here. Continue through its physical network and append
			-- only shapes registered by the game as pipe containers.
			appendUniqueShapes( result, getPhysicalContainerShapes( remoteEndpoint ) )
		end
		if requestedDirection == "input" then
			for _, entry in ipairs( discoverDirectionalSourceEntries( startShape, requestedDirection ) ) do
				appendUniqueShapes( result, getDirectionalSourceContainerShapes( entry ) )
			end
		end
		return result
	end )
	return ok and extended or localResults
end

function ScrapLabPipeGraph.getInputContainers( shape )
	return extendNativeShapeList( sm.pipeGraph.getInputContainers, shape, "input" )
end

-- Dedicated name keeps protected vanilla call counts stable while Crafter asks
-- the server for authoritative GUI containers.
function ScrapLabPipeGraph.getGuiInputContainers( shape )
	return ScrapLabPipeGraph.getInputContainers( shape )
end

function ScrapLabPipeGraph.getOutputContainers( shape )
	return extendNativeShapeList( sm.pipeGraph.getOutputContainers, shape, "output" )
end

-- Phase 4 uses this local-only physical view for active SEND/RECEIVE routing.
-- It never follows a wireless peer and therefore cannot accidentally turn a
-- directional channel into Link mode. Neutral pipe roots need this fallback
-- because the engine's input/output queries legitimately return no containers.
function ScrapLabPipeGraph.getLocalPhysicalContainerShapes( shape )
	if not shapeExists( shape ) then return {} end
	local ok, containers = pcall( function() return getPhysicalContainerShapes( shape ) end )
	return ok and containers or {}
end

local function validateCollectRequest( container, items, quantities )
	if type( items ) ~= "table" or type( quantities ) ~= "table" or #items == 0 or #items ~= #quantities then
		return false
	end

	-- The native pipe selector accepts arrays and evaluates the complete output
	-- together. The public container validator accepts one UUID at a time, so
	-- duplicate UUIDs must be combined before checking capacity and filters.
	local requests = {}
	local requestOrder = {}
	for index, itemUuid in ipairs( items ) do
		local quantity = quantities[index]
		if itemUuid == nil or type( quantity ) ~= "number" or quantity <= 0 or quantity ~= math.floor( quantity ) then
			return false
		end
		local key = tostring( itemUuid )
		if requests[key] == nil then
			requests[key] = { uuid = itemUuid, quantity = 0 }
			requestOrder[#requestOrder + 1] = key
		end
		requests[key].quantity = requests[key].quantity + quantity
	end

	for _, key in ipairs( requestOrder ) do
		local request = requests[key]
		if not sm.container.canCollect( container, request.uuid, request.quantity ) then return false end
	end
	if #requestOrder == 1 then return true end

	-- Separate canCollect calls can otherwise overbook the same empty slots when
	-- a Craftbot recipe has multiple distinct outputs. This conservative slot
	-- simulation never mutates inventory and therefore remains safe even when a
	-- vanilla caller already has an active transaction (Refinery does this).
	local emptySlots = 0
	local existingCapacity = {}
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
	return itemUuid ~= nil
		and type( quantity ) == "number"
		and quantity > 0
		and quantity == math.floor( quantity )
		and sm.container.canSpend( container, itemUuid, quantity )
end

local function extendNativeSelection( selectionKind, nativeFunction, shape, itemUuid, quantity )
	local localResult = nativeFunction( shape, itemUuid, quantity )
	if localResult ~= nil or not managerAvailable() then return localResult end
	local ok, selected = pcall( function()
		local requestedDirection = selectionKind == "collect" and "output" or "input"
		local linkedRoots = discoverLinkedRoots( shape, requestedDirection )
		for _, remoteEndpoint in ipairs( linkedRoots ) do
			for _, candidate in ipairs( getPhysicalContainerShapes( remoteEndpoint ) ) do
				local container = candidate:getInteractable():getContainer( 0 )
				if container then
					local accepted = false
					if selectionKind == "collect" then
						accepted = validateCollectRequest( container, itemUuid, quantity )
					else
						accepted = validateSpendRequest( container, itemUuid, quantity )
					end
					if accepted then return candidate end
				end
			end
		end
		if selectionKind == "spend" then
			for _, entry in ipairs( discoverDirectionalSourceEntries( shape, "input" ) ) do
				for _, candidate in ipairs( getDirectionalSourceContainerShapes( entry ) ) do
					local container = candidate:getInteractable():getContainer( 0 )
					if container and validateSpendRequest( container, itemUuid, quantity ) then
						return candidate
					end
				end
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
	-- util.lua is also loaded by terrain-generation Lua states, where the
	-- interactable API is intentionally absent. Resolve these constants only in
	-- a game state that actually supports pipe/container queries.
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
	if not managerAvailable() or not connectedInteractable then return localResults end
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
		local linkedRoots = discoverLinkedRoots( startShape )
		for _, remoteEndpoint in ipairs( linkedRoots ) do
			walkPhysicalGraph( remoteEndpoint, function( shape )
				local interactable = shape:getInteractable()
				if interactable and matchesAnyOutputType( interactable, outputTypes ) then
					local container = interactable:getContainer( 0 )
					if container then
						local id = tostring( sm.container.getId( container ) )
						if not seen[id] then
							seen[id] = true
							result[#result + 1] = container
						end
					end
				end
				if shape ~= remoteEndpoint and openingDirections( shape ) then return false end
				return true
			end )
		end
		for _, entry in ipairs( discoverDirectionalSourceEntries( startShape ) ) do
			for _, shape in ipairs( getDirectionalSourceContainerShapes( entry ) ) do
				local interactable = shape:getInteractable()
				if interactable and matchesAnyOutputType( interactable, outputTypes ) then
					local container = interactable:getContainer( 0 )
					if container then
						local id = tostring( sm.container.getId( container ) )
						if not seen[id] then seen[id] = true; result[#result + 1] = container end
					end
				end
			end
		end
		return result
	end )
	return ok and extended or localResults
end

-- Client effects must never draw a single impossible line between worlds.
-- This returns the native route for a same-world transfer and an empty route
-- for a cross-world transfer. Consumer-specific endpoint effects come later.
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
	local ok, result = pcall( function()
		local containers = {}
		local linkedRoots = discoverLinkedRoots( shape, requestedDirection )
		for _, root in ipairs( linkedRoots ) do appendUniqueShapes( containers, getPhysicalContainerShapes( root ) ) end
		return containers
	end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetPhysicalContainerShapes( shape )
	local ok, result = pcall( function() return getPhysicalContainerShapes( shape ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetDirectContainerShapes( shape )
	local ok, result = pcall( function() return getDirectContainerShapes( shape ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugGetDirectionalSourceEntries( shape, requestedDirection )
	local ok, result = pcall( function() return discoverDirectionalSourceEntries( shape, requestedDirection ) end )
	return ok and result or {}
end

function ScrapLabPipeGraph.debugClearTopologyCache()
	peerCache = { revision = nil, entries = {} }
end

-- Future ScrapLab container parts can opt into the virtual graph explicitly.
function ScrapLabPipeGraph.registerContainerUuid( uuid )
	REGISTERED_CONTAINER_UUIDS[string.lower( tostring( uuid ) )] = true
end
