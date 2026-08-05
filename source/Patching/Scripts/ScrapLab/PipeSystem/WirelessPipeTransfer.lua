-- SCRAPLAB WIRELESS VACUUM PIPE DIRECTIONAL TRANSFER v4
-- Server-authoritative SEND -> RECEIVE scheduling. Selection and commit are
-- deliberately separated by one fixed tick so no cached Container reference
-- can survive an endpoint destruction or world-unload boundary.

WirelessPipeTransfer = WirelessPipeTransfer or {}

if ScrapLabPipeGraph == nil then
	dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/ScrapLabPipeGraph.lua" )
end

local WIRELESS_PIPE_UUID = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local ATTEMPT_INTERVAL_TICKS = 4
local COMMIT_DELAY_TICKS = 1
local MAX_GROUPS_PER_TICK = 64
local MAX_IDLE_BACKOFF_TICKS = 40

local function shapeExists( shape )
	return shape ~= nil and sm.exists( shape )
end

local function shapeId( shape )
	if not shapeExists( shape ) then return nil end
	local ok, id = pcall( function() return shape:getId() end )
	return ok and id or shape.id
end

local function containerAt( shape, index )
	if not shapeExists( shape ) then return nil end
	local ok, container = pcall( function()
		local interactable = shape:getInteractable()
		return interactable and interactable:getContainer( index or 0 ) or nil
	end )
	return ok and container or nil
end

local function directContainerShapes( endpointShape )
	if not shapeExists( endpointShape ) then return {} end
	if ScrapLabPipeGraph and type( ScrapLabPipeGraph.getDirectContainerShapes ) == "function" then
		return ScrapLabPipeGraph.getDirectContainerShapes( endpointShape )
	end
	local result, seen = {}, {}
	local ok, neighbours = pcall( function() return endpointShape:getPipedNeighbours() end )
	if not ok or type( neighbours ) ~= "table" then return result end
	table.sort( neighbours, function( a, b ) return tostring( shapeId( a ) ) < tostring( shapeId( b ) ) end )
	for _, shape in ipairs( neighbours ) do
		local id = shapeId( shape )
		if id and not seen[id] and containerAt( shape, 0 ) then
			seen[id] = true
			result[#result + 1] = shape
		end
	end
	return result
end

local function nativeContainerShapes( endpointShape, mode, directOnly )
	if not shapeExists( endpointShape ) then return {} end
	if directOnly ~= false then return directContainerShapes( endpointShape ) end
	local shapes = {}
	local query = sm.pipeGraph and ( mode == "SEND" and sm.pipeGraph.getInputContainers or sm.pipeGraph.getOutputContainers ) or nil
	if type( query ) == "function" then
		local ok, nativeShapes = pcall( query, endpointShape )
		if ok and type( nativeShapes ) == "table" then shapes = nativeShapes end
	end
	local result, seen = {}, {}
	for _, shape in ipairs( shapes ) do
		local id = shapeId( shape )
		if id and not seen[id] then seen[id] = true; result[#result + 1] = shape end
	end
	-- Neutral Wireless Pipe roots have no engine input/output direction, so the
	-- native list can be empty. Supplement it with the proven Phase 3 local
	-- physical traversal; this does not follow any wireless endpoint peer.
	local physicalShapes = {}
	if ScrapLabPipeGraph and type( ScrapLabPipeGraph.getLocalPhysicalContainerShapes ) == "function" then
		physicalShapes = ScrapLabPipeGraph.getLocalPhysicalContainerShapes( endpointShape )
	end
	for _, shape in ipairs( physicalShapes ) do
		local id = shapeId( shape )
		if id and not seen[id] then seen[id] = true; result[#result + 1] = shape end
	end
	return result
end

local function canSpend( container, itemUuid, quantity )
	if not container then return false end
	local ok, accepted = pcall( function() return sm.container.canSpend( container, itemUuid, quantity ) end )
	return ok and accepted == true
end

local function canCollect( container, itemUuid, quantity )
	if not container then return false end
	local ok, accepted = pcall( function() return sm.container.canCollect( container, itemUuid, quantity ) end )
	return ok and accepted == true
end

local function sortedKeys( values )
	local keys = {}
	for key in pairs( values or {} ) do keys[#keys + 1] = key end
	table.sort( keys )
	return keys
end

local function nextIndexAfter( values, previous )
	if #values == 0 then return nil end
	if previous then
		for index, value in ipairs( values ) do
			if value == previous then return index % #values + 1 end
		end
	end
	return 1
end

local function orderedFromCursor( values, previous )
	local result = {}
	local first = nextIndexAfter( values, previous )
	if not first then return result end
	for offset = 0, #values - 1 do
		result[#result + 1] = values[( first + offset - 1 ) % #values + 1]
	end
	return result
end

local function channelFromGroupKey( key )
	return tostring( key or "" ):match( "^[^|]+|(.+)$" )
end

local function directionalGroupKey( channel )
	return "DIRECTIONAL|" .. tostring( channel or "" )
end

local function ensureRuntime( manager )
	manager.sv.directional = manager.sv.directional or {
		pending = {},
		locks = {},
		status = {},
		activity = {},
		nextAttempt = {},
		idleDelay = {},
		metrics = {
			attempts = 0, selected = 0, committed = 0, rejected = 0,
			transactionFailures = 0, staleGuardRejects = 0,
			idleBackoffs = 0, backoffSkips = 0
		}
	}
	manager.sv.directional.nextAttempt = manager.sv.directional.nextAttempt or {}
	manager.sv.directional.idleDelay = manager.sv.directional.idleDelay or {}
	manager.sv.directional.metrics.idleBackoffs = manager.sv.directional.metrics.idleBackoffs or 0
	manager.sv.directional.metrics.backoffSkips = manager.sv.directional.metrics.backoffSkips or 0
	manager.sv.saved.directionalCursors = manager.sv.saved.directionalCursors or {}
	return manager.sv.directional
end

local function setStatus( manager, endpointId, state )
	local runtime = ensureRuntime( manager )
	local previous = runtime.status[endpointId]
	if state then runtime.status[endpointId] = state else runtime.status[endpointId] = nil end
	if previous ~= state and manager.sv_invalidateStatusCache then manager:sv_invalidateStatusCache() end
end

local function resetBackoff( runtime, channel, tick )
	runtime.idleDelay[channel] = ATTEMPT_INTERVAL_TICKS
	runtime.nextAttempt[channel] = ( tick or sm.game.getCurrentTick() ) + ATTEMPT_INTERVAL_TICKS
end

local function applyIdleBackoff( runtime, channel, tick )
	local previous = runtime.idleDelay[channel] or ATTEMPT_INTERVAL_TICKS
	local delay = math.min( MAX_IDLE_BACKOFF_TICKS, math.max( ATTEMPT_INTERVAL_TICKS, previous * 2 ) )
	runtime.idleDelay[channel] = delay
	runtime.nextAttempt[channel] = ( tick or sm.game.getCurrentTick() ) + delay
	runtime.metrics.idleBackoffs = runtime.metrics.idleBackoffs + 1
end

local function liveEndpoint( manager, endpointId, expectedMode, expectedChannel, expectedGeneration, expectedDirectOnly )
	local record = manager.sv.saved.endpoints[endpointId]
	local live = manager.sv.live[endpointId]
	local handle = manager.sv.endpointHandleState[endpointId]
	if not record or not live or not record.enabled then return nil, nil, "endpoint unavailable" end
	if record.mode ~= expectedMode or record.channel ~= expectedChannel then return nil, nil, "endpoint route changed" end
	if expectedDirectOnly ~= nil and ( record.directOnly ~= false ) ~= expectedDirectOnly then return nil, nil, "endpoint scope changed" end
	if expectedGeneration and live.generation ~= expectedGeneration then return nil, nil, "endpoint generation changed" end
	if not handle or handle.limited or not handle.ready then return nil, nil, "endpoint cell not ready" end
	if not shapeExists( live.shape ) or live.shape:getShapeUuid() ~= WIRELESS_PIPE_UUID then return nil, nil, "endpoint shape unavailable" end
	return record, live, nil
end

local function findSourceCandidate( endpointShape, directOnly )
	for _, candidateShape in ipairs( nativeContainerShapes( endpointShape, "SEND", directOnly ) ) do
		local container = containerAt( candidateShape, 0 )
		if container then
			local ok, size = pcall( function() return container:getSize() end )
			if ok then
				for slot = 0, size - 1 do
					local itemOk, item = pcall( function() return container:getItem( slot ) end )
					local quantity = itemOk and item and tonumber( item.quantity ) or 0
					if quantity > 0 and item.uuid and canSpend( container, item.uuid, 1 ) then
						return {
							shapeId = shapeId( candidateShape ),
							containerIndex = 0,
							itemUuid = tostring( item.uuid ),
							quantity = 1
						}
					end
				end
			end
		end
	end
	return nil
end

local function findDestinationCandidate( endpointShape, itemUuid, quantity, directOnly )
	for _, candidateShape in ipairs( nativeContainerShapes( endpointShape, "RECEIVE", directOnly ) ) do
		local container = containerAt( candidateShape, 0 )
		if container and canCollect( container, itemUuid, quantity ) then
			return { shapeId = shapeId( candidateShape ), containerIndex = 0 }
		end
	end
	return nil
end

local function findFreshContainerShape( endpointShape, mode, wantedShapeId, directOnly )
	for _, candidateShape in ipairs( nativeContainerShapes( endpointShape, mode, directOnly ) ) do
		if shapeId( candidateShape ) == wantedShapeId then return candidateShape end
	end
	return nil
end

local function recordSuccessfulCursor( manager, channel, senderId, receiverId )
	manager.sv.saved.directionalCursors[channel] = {
		senderId = senderId,
		receiverId = receiverId
	}
	manager.sv.saveDirty = true
end

local function queueActivity( manager, endpointId, generation, role, itemUuid, containerShape, crossWorld )
	local runtime = ensureRuntime( manager )
	runtime.activity[endpointId] = {
		generation = generation,
		role = role,
		itemUuid = itemUuid,
		containerShape = containerShape,
		crossWorld = crossWorld == true
	}
end

local function releaseGroup( runtime, key )
	runtime.pending[key] = nil
	runtime.locks[key] = nil
end

local function rejectPending( manager, key, pending, reason, stale )
	local runtime = ensureRuntime( manager )
	runtime.metrics.rejected = runtime.metrics.rejected + 1
	if stale then runtime.metrics.staleGuardRejects = runtime.metrics.staleGuardRejects + 1 end
	setStatus( manager, pending.senderId, reason == "destination unavailable" and "DESTINATION FULL" or "CHANNEL EMPTY" )
	applyIdleBackoff( runtime, pending.channel, sm.game.getCurrentTick() )
	releaseGroup( runtime, key )
end

local function commitPending( manager, key, pending )
	local runtime = ensureRuntime( manager )
	local senderRecord, senderLive = liveEndpoint( manager, pending.senderId, "SEND", pending.channel, pending.senderGeneration, pending.senderDirectOnly )
	local receiverRecord, receiverLive = liveEndpoint( manager, pending.receiverId, "RECEIVE", pending.channel, pending.receiverGeneration, pending.receiverDirectOnly )
	if not senderRecord or not receiverRecord then
		rejectPending( manager, key, pending, "route changed", true )
		return
	end

	-- Freshly repeat both native graph queries and reacquire both Containers.
	-- Nothing retained from selection is used as transaction authority.
	local sourceShape = findFreshContainerShape( senderLive.shape, "SEND", pending.sourceShapeId, pending.senderDirectOnly )
	local destinationShape = findFreshContainerShape( receiverLive.shape, "RECEIVE", pending.destinationShapeId, pending.receiverDirectOnly )
	if not sourceShape or not destinationShape then
		rejectPending( manager, key, pending, "route changed", true )
		return
	end
	local source = containerAt( sourceShape, pending.sourceContainerIndex )
	local destination = containerAt( destinationShape, pending.destinationContainerIndex )
	local itemUuid = sm.uuid.new( pending.itemUuid )
	if not canSpend( source, itemUuid, pending.quantity ) then
		rejectPending( manager, key, pending, "source unavailable", false )
		return
	end
	if not canCollect( destination, itemUuid, pending.quantity ) then
		rejectPending( manager, key, pending, "destination unavailable", false )
		return
	end

	local beganOk, began = pcall( function() return sm.container.beginTransaction() end )
	if not beganOk or not began then
		runtime.metrics.transactionFailures = runtime.metrics.transactionFailures + 1
		applyIdleBackoff( runtime, pending.channel, sm.game.getCurrentTick() )
		releaseGroup( runtime, key )
		return
	end
	local queued, queueError = pcall( function()
		sm.container.spend( source, itemUuid, pending.quantity, true )
		sm.container.collect( destination, itemUuid, pending.quantity, true )
	end )
	if not queued then
		-- Do not call endTransaction after a queueing exception. The engine drops
		-- the uncommitted transaction at the callback boundary; Phase 1 proved
		-- this path changes neither container.
		runtime.metrics.transactionFailures = runtime.metrics.transactionFailures + 1
		applyIdleBackoff( runtime, pending.channel, sm.game.getCurrentTick() )
		releaseGroup( runtime, key )
		sm.log.warning( "[ScrapLab Pipe Transfer] transaction queue failed: " .. tostring( queueError ) )
		return
	end
	local commitOk, committed = pcall( function() return sm.container.endTransaction() end )
	if not commitOk or not committed then
		runtime.metrics.transactionFailures = runtime.metrics.transactionFailures + 1
		applyIdleBackoff( runtime, pending.channel, sm.game.getCurrentTick() )
		releaseGroup( runtime, key )
		return
	end

	runtime.metrics.committed = runtime.metrics.committed + 1
	resetBackoff( runtime, pending.channel, sm.game.getCurrentTick() )
	recordSuccessfulCursor( manager, pending.channel, pending.senderId, pending.receiverId )
	setStatus( manager, pending.senderId, "SENDING" )
	setStatus( manager, pending.receiverId, "READY TO RECEIVE" )
	local crossWorld = senderRecord.worldId ~= receiverRecord.worldId
	queueActivity( manager, pending.senderId, senderLive.generation, "SEND", itemUuid, sourceShape, crossWorld )
	queueActivity( manager, pending.receiverId, receiverLive.generation, "RECEIVE", itemUuid, destinationShape, crossWorld )
	releaseGroup( runtime, key )
end

local function scheduleGroup( manager, channel, senders, receivers, tick )
	local runtime = ensureRuntime( manager )
	local key = directionalGroupKey( channel )
	if runtime.locks[key] or runtime.pending[key] then return end
	runtime.metrics.attempts = runtime.metrics.attempts + 1
	local cursor = manager.sv.saved.directionalCursors[channel] or {}
	local orderedSenders = orderedFromCursor( senders, cursor.senderId )
	local orderedReceivers = orderedFromCursor( receivers, cursor.receiverId )
	local sawSource, sawDestination = false, false

	for _, senderId in ipairs( orderedSenders ) do
		local senderRecord, senderLive = liveEndpoint( manager, senderId, "SEND", channel, nil )
		if senderLive then
			local senderDirectOnly = senderRecord.directOnly ~= false
			local source = findSourceCandidate( senderLive.shape, senderDirectOnly )
			if source then
				sawSource = true
				local itemUuid = sm.uuid.new( source.itemUuid )
				for _, receiverId in ipairs( orderedReceivers ) do
					local receiverRecord, receiverLive = liveEndpoint( manager, receiverId, "RECEIVE", channel, nil )
					if receiverLive then
						local receiverDirectOnly = receiverRecord.directOnly ~= false
						local destination = findDestinationCandidate( receiverLive.shape, itemUuid, source.quantity, receiverDirectOnly )
						if destination then
							sawDestination = true
							resetBackoff( runtime, channel, tick )
							runtime.locks[key] = true
							runtime.pending[key] = {
								selectedTick = tick,
								commitTick = tick + COMMIT_DELAY_TICKS,
								channel = channel,
								senderId = senderId,
								senderGeneration = senderLive.generation,
								senderDirectOnly = senderDirectOnly,
								receiverId = receiverId,
								receiverGeneration = receiverLive.generation,
								receiverDirectOnly = receiverDirectOnly,
								sourceShapeId = source.shapeId,
								sourceContainerIndex = source.containerIndex,
								destinationShapeId = destination.shapeId,
								destinationContainerIndex = destination.containerIndex,
								itemUuid = source.itemUuid,
								quantity = source.quantity
							}
							runtime.metrics.selected = runtime.metrics.selected + 1
							return
						end
					end
				end
			end
		end
	end

	for _, senderId in ipairs( senders ) do
		setStatus( manager, senderId, not sawSource and "CHANNEL EMPTY" or ( not sawDestination and "DESTINATION FULL" or "SENDING" ) )
	end
	applyIdleBackoff( runtime, channel, tick )
end

function WirelessPipeTransfer.Sv_ServerOnCreate( manager )
	ensureRuntime( manager )
end

function WirelessPipeTransfer.Sv_ServerOnFixedUpdate( manager )
	local runtime = ensureRuntime( manager )
	local tick = sm.game.getCurrentTick()
	for _, key in ipairs( sortedKeys( runtime.pending ) ) do
		local pending = runtime.pending[key]
		if pending and tick >= pending.commitTick then commitPending( manager, key, pending ) end
	end
	if tick % ATTEMPT_INTERVAL_TICKS ~= 0 then return end
	if manager.sv.groupsDirty then manager:sv_rebuildGroups() end
	local processed = 0
	for key, senders in pairs( manager.sv.groups.SEND or {} ) do
		local channel = channelFromGroupKey( key )
		local receivers = manager.sv.groups.RECEIVE["RECEIVE|" .. tostring( channel )] or {}
		if channel and #senders > 0 and #receivers > 0 then
			if tick >= ( runtime.nextAttempt[channel] or 0 ) then
				scheduleGroup( manager, channel, senders, receivers, tick )
			else
				runtime.metrics.backoffSkips = runtime.metrics.backoffSkips + 1
			end
			processed = processed + 1
			if processed >= MAX_GROUPS_PER_TICK then break end
		end
	end
end

function WirelessPipeTransfer.Sv_OnEndpointTopologyChanged( manager, endpointId )
	local runtime = ensureRuntime( manager )
	local record = manager.sv.saved.endpoints[tostring( endpointId or "" )]
	if record and record.channel then resetBackoff( runtime, record.channel, sm.game.getCurrentTick() - ATTEMPT_INTERVAL_TICKS ) end
	setStatus( manager, endpointId, nil )
	runtime.activity[endpointId] = nil
	for key, pending in pairs( runtime.pending ) do
		if pending.senderId == endpointId or pending.receiverId == endpointId then
			resetBackoff( runtime, pending.channel, sm.game.getCurrentTick() - ATTEMPT_INTERVAL_TICKS )
			releaseGroup( runtime, key )
		end
	end
end

function WirelessPipeTransfer.Sv_ConsumeEndpointActivity( manager, endpointId, generation )
	local runtime = ensureRuntime( manager )
	endpointId = tostring( endpointId or "" )
	local activity = runtime.activity[endpointId]
	if not activity then return nil end
	runtime.activity[endpointId] = nil
	if generation and activity.generation ~= generation then return nil end
	return activity
end

function WirelessPipeTransfer.Sv_GetEndpointState( manager, endpointId )
	local runtime = ensureRuntime( manager )
	return runtime.status[tostring( endpointId or "" )]
end

function WirelessPipeTransfer.Sv_GetDebugSnapshot( manager )
	local runtime = ensureRuntime( manager )
	local pending, locks = 0, 0
	for _ in pairs( runtime.pending ) do pending = pending + 1 end
	for _ in pairs( runtime.locks ) do locks = locks + 1 end
	return {
		attemptIntervalTicks = ATTEMPT_INTERVAL_TICKS,
		commitDelayTicks = COMMIT_DELAY_TICKS,
		pending = pending,
		locks = locks,
		attempts = runtime.metrics.attempts,
		selected = runtime.metrics.selected,
		committed = runtime.metrics.committed,
		rejected = runtime.metrics.rejected,
		transactionFailures = runtime.metrics.transactionFailures,
		staleGuardRejects = runtime.metrics.staleGuardRejects,
		idleBackoffs = runtime.metrics.idleBackoffs,
		backoffSkips = runtime.metrics.backoffSkips,
		maxIdleBackoffTicks = MAX_IDLE_BACKOFF_TICKS
	}
end

ScrapLabWirelessPipeTransferConstants = {
	attemptIntervalTicks = ATTEMPT_INTERVAL_TICKS,
	commitDelayTicks = COMMIT_DELAY_TICKS,
	maxIdleBackoffTicks = MAX_IDLE_BACKOFF_TICKS,
	quantityPerTransfer = 1
}
