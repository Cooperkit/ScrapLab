-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 1 PROBE v3
-- Developer-only cross-world container transaction safety gate.
-- Use only in a disposable Survival test world. This is not the shipped part.

local ProbeSchemaVersion = 1
local ProbeItem = sm.uuid.new( "f152e4df-bc40-44fb-8d20-3b3ff70cdfe3" ) -- Circuit Board
local ProbeChest = sm.uuid.new( "4c474cff-3f6a-4306-93d1-c4c74578afd2" ) -- Piped Small Chest
local ProbePrefix = "[ScrapLab Pipe Phase 1] "

local function probeLog( message )
	if sm.log and sm.log.info then
		sm.log.info( ProbePrefix .. message )
	else
		print( ProbePrefix .. message )
	end
end

local function probeMessage( self, player, message )
	probeLog( "player " .. tostring( player and player.id or "?") .. ": " .. message )
	if player then
		self.network:sendToClient( player, "client_showMessage", ProbePrefix .. message )
	end
end

local function probeIsHost( player )
	local host = sm.player.getHostPlayer()
	return host and player and host.id == player.id
end

local function probeReleaseHandle( handle )
	if handle then
		pcall( function() handle:release() end )
	end
end

local function probeCountItem( container )
	if not container then return nil end
	local total = 0
	for slot = 0, container:getSize() - 1 do
		local item = container:getItem( slot )
		if item and item.uuid == ProbeItem then
			total = total + item.quantity
		end
	end
	return total
end

local function probeContainerCapacity( container )
	-- getMaxStackSize is the container's technical ceiling (65535 for a
	-- chest), not necessarily this item's stack limit. Find the real total
	-- capacity with the engine's own all-or-nothing admission check.
	local low = 0
	local high = container:getSize() * container:getMaxStackSize()
	while low < high do
		local middle = math.floor( ( low + high + 1 ) / 2 )
		if container:canCollect( ProbeItem, middle ) then
			low = middle
		else
			high = middle - 1
		end
	end
	return low
end

local function probeSetItemCount( container, quantity )
	local current = probeCountItem( container )
	if current == nil then return false end
	if not sm.container.beginTransaction() then return false end
	if current > 0 then
		sm.container.spend( container, ProbeItem, current, true )
	end
	if quantity > 0 then
		sm.container.collect( container, ProbeItem, quantity, true )
	end
	return sm.container.endTransaction()
end

local function probeShapeContainer( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape.interactable or shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end

local function probeShapeMatches( shape, endpoint )
	if not shape or not sm.exists( shape ) then return false end
	if shape.id ~= endpoint.shapeId then return false end
	if tostring( shape:getShapeUuid() ) ~= endpoint.shapeUuid then return false end
	local delta = shape:getWorldPosition() - endpoint.position
	return delta:length2() < 0.25
end

local function probeFindShape( endpoint )
	if not endpoint or not endpoint.world then return nil end
	local world = endpoint.world
	if type( world ) == "boolean" then return nil end
	if not sm.exists( world ) then
		sm.world.loadWorld( world )
		endpoint.world = world
	end
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if probeShapeMatches( shape, endpoint ) then
				return shape
			end
		end
	end
	return nil
end

local function probeResultSummary( result )
	if result.pending then
		return "PENDING " .. result.name .. " (" .. result.detail .. ")"
	end
	local outcome = result.passed and "PASS" or "FAIL"
	return outcome .. " " .. result.name .. " (" .. result.detail .. ")"
end

local function probeMakeResult( name, passed, detail, beforeSource, beforeDestination, afterSource, afterDestination, committed )
	return {
		name = name,
		passed = passed,
		detail = detail,
		beforeSource = beforeSource,
		beforeDestination = beforeDestination,
		afterSource = afterSource,
		afterDestination = afterDestination,
		committed = committed == true,
		tick = sm.game.getCurrentTick()
	}
end

local function probeSave( self )
	self.sv.saved.scrapLabPipePhase1Probe = self.sv.scrapLabPipePhase1Probe
	self.storage:save( self.sv.saved )
end

function SurvivalGame.sv_slppAcquireHandle( self, role )
	local runtime = self.sv.scrapLabPipePhase1Runtime
	local endpoint = self.sv.scrapLabPipePhase1Probe.endpoints[role]
	if not endpoint then return false end
	if runtime.handles[role] then return true end
	local world = endpoint.world
	if not world or type( world ) == "boolean" then return false end
	if not sm.exists( world ) then
		sm.world.loadWorld( world )
		endpoint.world = world
	end
	local ok, handle = pcall( function()
		return world:loadCellWithHandle( endpoint.cellX, endpoint.cellY, nil )
	end )
	if not ok or not handle then
		probeLog( "failed to acquire " .. role .. " cell handle" )
		return false
	end
	runtime.handles[role] = handle
	return true
end

function SurvivalGame.sv_slppReleaseHandle( self, role )
	local runtime = self.sv.scrapLabPipePhase1Runtime
	probeReleaseHandle( runtime.handles[role] )
	runtime.handles[role] = nil
end

function SurvivalGame.sv_slppResolveEndpoint( self, role )
	local runtime = self.sv.scrapLabPipePhase1Runtime
	local endpoint = self.sv.scrapLabPipePhase1Probe.endpoints[role]
	if not endpoint then return nil, nil end
	self:sv_slppAcquireHandle( role )
	local shape = runtime.shapes[role]
	if not probeShapeMatches( shape, endpoint ) then
		shape = probeFindShape( endpoint )
		runtime.shapes[role] = shape
	end
	return shape, probeShapeContainer( shape )
end

function SurvivalGame.sv_slppGetContainers( self )
	local _, source = self:sv_slppResolveEndpoint( "source" )
	local _, destination = self:sv_slppResolveEndpoint( "destination" )
	return source, destination
end

function SurvivalGame.sv_slppRecordResult( self, result )
	local state = self.sv.scrapLabPipePhase1Probe
	state.results[result.name] = result
	state.lastResult = result.name
	probeSave( self )
	probeLog( probeResultSummary( result ) )
	return result
end

function SurvivalGame.sv_slppPrepareCounts( self, sourceQuantity, destinationQuantity )
	local source, destination = self:sv_slppGetContainers()
	if not source or not destination then return nil, nil, "both endpoint containers must be loaded" end
	if not probeSetItemCount( source, sourceQuantity ) then return nil, nil, "could not prepare source" end
	if not probeSetItemCount( destination, destinationQuantity ) then return nil, nil, "could not prepare destination" end
	return source, destination, nil
end

function SurvivalGame.sv_slppTransfer( self, source, destination, quantity )
	if not source or not destination then return false end
	if not sm.container.beginTransaction() then return false end
	sm.container.spend( source, ProbeItem, quantity, true )
	sm.container.collect( destination, ProbeItem, quantity, true )
	return sm.container.endTransaction()
end

function SurvivalGame.sv_slppRunNormal( self )
	local source, destination, err = self:sv_slppPrepareCounts( 10, 0 )
	if err then return probeMakeResult( "normal", false, err ) end
	local committed = self:sv_slppTransfer( source, destination, 1 )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "normal", committed and a == 9 and b == 1,
		"commit=" .. tostring( committed ) .. ", counts=" .. tostring( a ) .. "/" .. tostring( b ), 10, 0, a, b, committed )
end

function SurvivalGame.sv_slppRunExactFull( self )
	local source, destination, err = self:sv_slppPrepareCounts( 1, 0 )
	if err then return probeMakeResult( "exact-full", false, err ) end
	local capacity = probeContainerCapacity( destination )
	if not probeSetItemCount( destination, capacity - 1 ) then
		return probeMakeResult( "exact-full", false, "could not prepare exact-full destination" )
	end
	local committed = self:sv_slppTransfer( source, destination, 1 )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "exact-full", committed and a == 0 and b == capacity,
		"commit=" .. tostring( committed ) .. ", counts=" .. tostring( a ) .. "/" .. tostring( b ), 1, capacity - 1, a, b, committed )
end

function SurvivalGame.sv_slppRunAlreadyFull( self )
	local source, destination, err = self:sv_slppPrepareCounts( 1, 0 )
	if err then return probeMakeResult( "already-full", false, err ) end
	local capacity = probeContainerCapacity( destination )
	if not probeSetItemCount( destination, capacity ) then
		return probeMakeResult( "already-full", false, "could not fill destination" )
	end
	local committed = self:sv_slppTransfer( source, destination, 1 )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "already-full", not committed and a == 1 and b == capacity,
		"commit=" .. tostring( committed ) .. ", counts=" .. tostring( a ) .. "/" .. tostring( b ), 1, capacity, a, b, committed )
end

function SurvivalGame.sv_slppRunSourceChanged( self )
	local source, destination, err = self:sv_slppPrepareCounts( 2, 0 )
	if err then return probeMakeResult( "source-changed", false, err ) end
	local snapshot = probeCountItem( source )
	if not sm.container.beginTransaction() then
		return probeMakeResult( "source-changed", false, "could not begin independent source mutation" )
	end
	sm.container.spend( source, ProbeItem, 1, true )
	if not sm.container.endTransaction() then
		return probeMakeResult( "source-changed", false, "independent source mutation failed" )
	end
	local committed = self:sv_slppTransfer( source, destination, snapshot )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "source-changed", not committed and a == 1 and b == 0,
		"stale=" .. tostring( snapshot ) .. ", commit=" .. tostring( committed ) .. ", counts=" .. tostring( a ) .. "/" .. tostring( b ), 1, 0, a, b, committed )
end

function SurvivalGame.sv_slppRunReceiverUnload( self )
	local source, destination, err = self:sv_slppPrepareCounts( 2, 0 )
	if err then return probeMakeResult( "receiver-unload", false, err ) end
	if not sm.container.beginTransaction() then
		return probeMakeResult( "receiver-unload", false, "could not begin transaction" )
	end
	sm.container.spend( source, ProbeItem, 1, true )
	sm.container.collect( destination, ProbeItem, 1, true )
	self:sv_slppReleaseHandle( "destination" )
	local committed = sm.container.endTransaction()
	self:sv_slppAcquireHandle( "destination" )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "receiver-unload", committed and a == 1 and b == 1,
		"handle released before commit; commit=" .. tostring( committed ) .. ", counts=" .. tostring( a ) .. "/" .. tostring( b ), 2, 0, a, b, committed )
end

function SurvivalGame.sv_slppRunErrorBefore( self )
	local source, destination, err = self:sv_slppPrepareCounts( 2, 0 )
	if err then return probeMakeResult( "error-before-commit", false, err ) end
	if not sm.container.beginTransaction() then
		return probeMakeResult( "error-before-commit", false, "could not begin transaction" )
	end
	local ok = pcall( function()
		sm.container.spend( source, ProbeItem, 1, true )
		sm.container.collect( destination, ProbeItem, 1, true )
		error( "intentional Phase 1 error before endTransaction" )
	end )
	sm.container.abortTransaction()
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "error-before-commit", not ok and a == 2 and b == 0,
		"error caught and transaction aborted; counts=" .. tostring( a ) .. "/" .. tostring( b ), 2, 0, a, b, false )
end

function SurvivalGame.sv_slppRunErrorAfter( self )
	local source, destination, err = self:sv_slppPrepareCounts( 2, 0 )
	if err then return probeMakeResult( "error-after-commit", false, err ) end
	local committed = self:sv_slppTransfer( source, destination, 1 )
	local ok = pcall( function()
		error( "intentional Phase 1 error after endTransaction" )
	end )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	return probeMakeResult( "error-after-commit", committed and not ok and a == 1 and b == 1,
		"error caught after commit; counts=" .. tostring( a ) .. "/" .. tostring( b ), 2, 0, a, b, committed )
end

function SurvivalGame.sv_slppSpawnEndpointAt( self, role, world, position )
	-- Static probe endpoints cannot fall away from their persisted position
	-- while the host travels between worlds.
	local shape = sm.shape.createPart( ProbeChest, position, sm.quat.identity(), false, true, world )
	if not shape or not sm.exists( shape ) then return nil end
	shape.color = role == "source" and sm.color.new( "ff8c00" ) or sm.color.new( "00c8ff" )
	local endpoint = {
		role = role,
		world = world,
		worldId = world.id,
		worldType = world.publicData and world.publicData.type or "Unknown",
		cellX = math.floor( position.x / 64 ),
		cellY = math.floor( position.y / 64 ),
		position = shape:getWorldPosition(),
		shapeId = shape.id,
		shapeUuid = tostring( shape:getShapeUuid() )
	}
	self.sv.scrapLabPipePhase1Probe.endpoints[role] = endpoint
	self.sv.scrapLabPipePhase1Runtime.shapes[role] = shape
	self:sv_slppAcquireHandle( role )
	probeSave( self )
	return shape
end

function SurvivalGame.sv_slppCreateEndpoint( self, role, player )
	local character = player and player:getCharacter()
	if not character or not sm.exists( character ) then
		probeMessage( self, player, "A live character is required." )
		return
	end
	local world = character:getWorld()
	local worldType = world.publicData and world.publicData.type or "Unknown"
	if role == "source" and worldType ~= "Overworld" then
		probeMessage( self, player, "Create SOURCE in the overworld." )
		return
	end
	if role == "destination" and worldType ~= "UndergroundWorld" then
		probeMessage( self, player, "Create DESTINATION in an underground world." )
		return
	end
	local oldShape = self.sv.scrapLabPipePhase1Runtime.shapes[role]
	if oldShape and sm.exists( oldShape ) then oldShape:destroyShape( 0 ) end
	self:sv_slppReleaseHandle( role )
	self.sv.scrapLabPipePhase1Probe.endpoints[role] = nil
	local direction = character:getDirection()
	local position = character:getWorldPosition() + direction * 3 + sm.vec3.new( 0, 0, 0.5 )
	local shape = self:sv_slppSpawnEndpointAt( role, world, position )
	if not shape then
		probeMessage( self, player, "Failed to create the " .. role .. " chest." )
		return
	end
	probeMessage( self, player, string.upper( role ) .. " chest created in " .. worldType .. " (world " .. tostring( world.id ) .. ")." )
end

function SurvivalGame.sv_slppRunDestroyedEndpoint( self, player )
	local source, destination, err = self:sv_slppPrepareCounts( 1, 0 )
	if err then return probeMakeResult( "endpoint-destroyed", false, err ) end
	local state = self.sv.scrapLabPipePhase1Probe
	local endpoint = state.endpoints.destination
	local shape = self.sv.scrapLabPipePhase1Runtime.shapes.destination
	shape:destroyShape( 0 )
	self.sv.scrapLabPipePhase1Runtime.shapes.destination = nil
	self.sv.scrapLabPipePhase1Runtime.pendingDestroyedCase = {
		player = player,
		source = source,
		endpoint = endpoint,
		dueTick = sm.game.getCurrentTick() + 2,
		deadlineTick = sm.game.getCurrentTick() + 40
	}
	local pending = probeMakeResult( "endpoint-destroyed", false,
		"waiting one scheduler tick to reject the stale endpoint", 1, 0, nil, nil, false )
	pending.pending = true
	return pending
end

function SurvivalGame.sv_slppUpdateDestroyedEndpointCase( self )
	local runtime = self.sv.scrapLabPipePhase1Runtime
	local pending = runtime.pendingDestroyedCase
	if not pending or sm.game.getCurrentTick() < pending.dueTick then return end
	local staleShape = probeFindShape( pending.endpoint )
	if staleShape and sm.game.getCurrentTick() < pending.deadlineTick then return end

	local sourceAfter = probeCountItem( pending.source )
	self:sv_slppReleaseHandle( "destination" )
	self.sv.scrapLabPipePhase1Probe.endpoints.destination = nil
	runtime.shapes.destination = nil
	local recreated = nil
	if not staleShape then
		recreated = self:sv_slppSpawnEndpointAt( "destination", pending.endpoint.world, pending.endpoint.position )
	end
	local _, recreatedContainer = self:sv_slppResolveEndpoint( "destination" )
	if recreatedContainer then probeSetItemCount( recreatedContainer, 0 ) end
	local passed = staleShape == nil and sourceAfter == 1 and recreated ~= nil and recreatedContainer ~= nil
	local result = probeMakeResult( "endpoint-destroyed", passed,
		"fresh resolve=" .. tostring( staleShape ~= nil ) .. ", commit skipped=true, source=" .. tostring( sourceAfter ) .. ", endpoint recreated=" .. tostring( recreated ~= nil ),
		1, 0, sourceAfter, 0, false )
	runtime.pendingDestroyedCase = nil
	self:sv_slppRecordResult( result )
	probeMessage( self, pending.player, probeResultSummary( result ) )
end

function SurvivalGame.sv_slppRunCase( self, name, player )
	local runners = {
		["normal"] = self.sv_slppRunNormal,
		["exact-full"] = self.sv_slppRunExactFull,
		["already-full"] = self.sv_slppRunAlreadyFull,
		["source-changed"] = self.sv_slppRunSourceChanged,
		["receiver-unload"] = self.sv_slppRunReceiverUnload,
		["endpoint-destroyed"] = self.sv_slppRunDestroyedEndpoint,
		["error-before-commit"] = self.sv_slppRunErrorBefore,
		["error-after-commit"] = self.sv_slppRunErrorAfter
	}
	local runner = runners[name]
	if not runner then return probeMakeResult( name or "missing", false, "unknown test case" ) end
	local result = runner( self, player )
	if result.pending then return result end
	return self:sv_slppRecordResult( result )
end

function SurvivalGame.sv_slppRunAllAutomatic( self, player )
	local synchronousOrder = {
		"normal", "exact-full", "already-full", "source-changed",
		"receiver-unload", "error-before-commit", "error-after-commit"
	}
	local passed = 0
	for _, name in ipairs( synchronousOrder ) do
		local result = self:sv_slppRunCase( name, player )
		if result.passed then passed = passed + 1 end
		probeMessage( self, player, probeResultSummary( result ) )
	end
	local destroyed = self:sv_slppRunCase( "endpoint-destroyed", player )
	probeMessage( self, player, probeResultSummary( destroyed ) )
	probeMessage( self, player, "Synchronous matrix: " .. tostring( passed ) .. "/" .. tostring( #synchronousOrder ) .. " passed; destruction guard completes after its fresh-resolve tick." )
end

function SurvivalGame.sv_slppArmReloadGate( self, player )
	local source, destination, err = self:sv_slppPrepareCounts( 3, 0 )
	if err then probeMessage( self, player, err ); return end
	local state = self.sv.scrapLabPipePhase1Probe
	state.journalSequence = ( state.journalSequence or 0 ) + 1
	state.journal = {
		sequence = state.journalSequence,
		status = "prepared",
		beforeSource = 3,
		beforeDestination = 0,
		quantity = 1
	}
	probeSave( self )
	local committed = self:sv_slppTransfer( source, destination, 1 )
	local a, b = probeCountItem( source ), probeCountItem( destination )
	state.journal.status = committed and "committed-awaiting-reload" or "failed-before-reload"
	state.journal.afterSource = a
	state.journal.afterDestination = b
	state.pendingReload = committed and { sequence = state.journalSequence, source = a, destination = b } or nil
	probeSave( self )
	if committed and a == 2 and b == 1 then
		probeMessage( self, player, "RELOAD GATE ARMED: save now, exit immediately, then reopen this world. The probe will verify 2/1 exactly once." )
	else
		self:sv_slppRecordResult( probeMakeResult( "save-reload", false, "arming transfer failed", 3, 0, a, b, committed ) )
		probeMessage( self, player, "Reload gate could not be armed." )
	end
end

function SurvivalGame.sv_slppTryVerifyReload( self )
	local state = self.sv.scrapLabPipePhase1Probe
	local pending = state.pendingReload
	if not pending then return end
	local source, destination = self:sv_slppGetContainers()
	if not source or not destination then return end
	local a, b = probeCountItem( source ), probeCountItem( destination )
	local passed = a == pending.source and b == pending.destination and a == 2 and b == 1
	self:sv_slppRecordResult( probeMakeResult( "save-reload", passed,
		"expected 2/1 after process reload; counts=" .. tostring( a ) .. "/" .. tostring( b ), 3, 0, a, b, true ) )
	state.pendingReload = nil
	state.journal.status = passed and "reload-verified" or "reload-mismatch"
	probeSave( self )
	probeLog( passed and "PASS save-reload gate" or "FAIL save-reload gate" )
end

function SurvivalGame.sv_slppObserve( self, player )
	local source, destination = self:sv_slppGetContainers()
	local a, b = probeCountItem( source ), probeCountItem( destination )
	local state = self.sv.scrapLabPipePhase1Probe
	state.observers[tostring( player.id )] = {
		source = a,
		destination = b,
		tick = sm.game.getCurrentTick()
	}
	probeSave( self )
	probeMessage( self, player, "AUTHORITATIVE COUNTS source=" .. tostring( a ) .. ", destination=" .. tostring( b ) .. "." )
end

function SurvivalGame.sv_slppCheckObservers( self, player )
	local source, destination = self:sv_slppGetContainers()
	local a, b = probeCountItem( source ), probeCountItem( destination )
	local matches, total = 0, 0
	for _, observer in pairs( self.sv.scrapLabPipePhase1Probe.observers ) do
		total = total + 1
		if observer.source == a and observer.destination == b then matches = matches + 1 end
	end
	local passed = total >= 2 and matches == total
	local result = probeMakeResult( "host-client-observation", passed,
		"observers=" .. tostring( total ) .. ", matching=" .. tostring( matches ) .. ", counts=" .. tostring( a ) .. "/" .. tostring( b ), a, b, a, b, false )
	self:sv_slppRecordResult( result )
	probeMessage( self, player, probeResultSummary( result ) )
end

function SurvivalGame.sv_slppStartLoopback( self, player )
	local source, destination = self:sv_slppGetContainers()
	local a, b = probeCountItem( source ), probeCountItem( destination )
	if a == nil or b == nil then
		probeMessage( self, player, "Both endpoint containers must be loaded before the loopback check." )
		return
	end
	local token = tostring( sm.game.getCurrentTick() ) .. ":" .. tostring( player.id ) .. ":" .. tostring( a ) .. ":" .. tostring( b )
	self.sv.scrapLabPipePhase1Runtime.loopback = {
		token = token,
		playerId = player.id,
		source = a,
		destination = b
	}
	self.network:sendToClient( player, "cl_slppLoopbackProbe", {
		token = token,
		source = a,
		destination = b
	} )
end

function SurvivalGame.cl_slppLoopbackProbe( self, data )
	self.network:sendToServer( "sv_slppLoopbackAck", data )
end

function SurvivalGame.sv_slppLoopbackAck( self, data, player )
	local expected = self.sv.scrapLabPipePhase1Runtime.loopback
	local source, destination = self:sv_slppGetContainers()
	local a, b = probeCountItem( source ), probeCountItem( destination )
	local passed = expected ~= nil and data ~= nil
		and expected.playerId == player.id
		and expected.token == data.token
		and expected.source == data.source and expected.destination == data.destination
		and expected.source == a and expected.destination == b
	local detail = "server-client-server round trip; counts=" .. tostring( a ) .. "/" .. tostring( b )
	local result = probeMakeResult( "host-client-loopback", passed, detail, a, b, a, b, false )
	self.sv.scrapLabPipePhase1Runtime.loopback = nil
	self:sv_slppRecordResult( result )
	probeMessage( self, player, probeResultSummary( result ) )
end

function SurvivalGame.sv_slppStatus( self, player )
	local state = self.sv.scrapLabPipePhase1Probe
	local source, destination = self:sv_slppGetContainers()
	probeMessage( self, player, "source=" .. tostring( source ~= nil ) .. " (" .. tostring( probeCountItem( source ) ) .. "), destination=" .. tostring( destination ~= nil ) .. " (" .. tostring( probeCountItem( destination ) ) .. ")." )
	local passed, failed = 0, 0
	for _, result in pairs( state.results ) do
		if result.passed then passed = passed + 1 else failed = failed + 1 end
	end
	probeMessage( self, player, "recorded gates: " .. tostring( passed ) .. " passed, " .. tostring( failed ) .. " failed; pending reload=" .. tostring( state.pendingReload ~= nil ) .. "." )
end

function SurvivalGame.sv_slppCleanup( self, player )
	for _, role in ipairs( { "source", "destination" } ) do
		local shape = self.sv.scrapLabPipePhase1Runtime.shapes[role]
		if shape and sm.exists( shape ) then shape:destroyShape( 0 ) end
		self:sv_slppReleaseHandle( role )
	end
	self.sv.scrapLabPipePhase1Runtime.shapes = {}
	self.sv.scrapLabPipePhase1Probe = {
		schemaVersion = ProbeSchemaVersion,
		endpoints = {}, results = {}, observers = {}, journalSequence = 0
	}
	probeSave( self )
	probeMessage( self, player, "Probe chests, handles, journal, observers, and results cleared." )
end

function SurvivalGame.sv_slppCommand( self, params, player )
	local action = string.lower( tostring( params and params.action or "help" ) )
	local readOnly = action == "help" or action == "status" or action == "observe"
	if not readOnly and not probeIsHost( player ) then
		probeMessage( self, player, "Only the host may mutate the Phase 1 probe." )
		return
	end
	if action == "source" or action == "destination" then
		self:sv_slppCreateEndpoint( action, player )
	elseif action == "run" then
		local result = self:sv_slppRunCase( string.lower( tostring( params.caseName or "" ) ), player )
		probeMessage( self, player, probeResultSummary( result ) )
	elseif action == "runall" then
		self:sv_slppRunAllAutomatic( player )
	elseif action == "reload" then
		self:sv_slppArmReloadGate( player )
	elseif action == "observe" then
		self:sv_slppObserve( player )
	elseif action == "observercheck" then
		self:sv_slppCheckObservers( player )
	elseif action == "loopback" then
		self:sv_slppStartLoopback( player )
	elseif action == "status" then
		self:sv_slppStatus( player )
	elseif action == "cleanup" then
		self:sv_slppCleanup( player )
	else
		probeMessage( self, player, "Commands: source, destination, runall, run <case>, reload, loopback, observe, observercheck, status, cleanup." )
	end
end

local ProbeOriginalServerOnCreate = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	ProbeOriginalServerOnCreate( self )
	local saved = self.sv.saved.scrapLabPipePhase1Probe
	if type( saved ) ~= "table" or saved.schemaVersion ~= ProbeSchemaVersion then
		saved = { schemaVersion = ProbeSchemaVersion, endpoints = {}, results = {}, observers = {}, journalSequence = 0 }
	end
	saved.endpoints = saved.endpoints or {}
	saved.results = saved.results or {}
	saved.observers = saved.observers or {}
	for _, role in ipairs( { "source", "destination" } ) do
		local endpoint = saved.endpoints[role]
		if endpoint and ( not endpoint.world or type( endpoint.world ) == "boolean" ) then
			saved.endpoints[role] = nil
			probeLog( "discarded " .. role .. " endpoint damaged by the Phase 1 v1 reload bug; recreate it" )
		end
	end
	self.sv.scrapLabPipePhase1Probe = saved
	self.sv.scrapLabPipePhase1Runtime = { handles = {}, shapes = {}, verifyTicks = 0 }
	self:sv_slppAcquireHandle( "source" )
	self:sv_slppAcquireHandle( "destination" )
	probeSave( self )
	probeLog( "probe loaded; use /slpipeprobe help" )
end

local ProbeOriginalServerOnDestroy = SurvivalGame.server_onDestroy
function SurvivalGame.server_onDestroy( self )
	if self.sv and self.sv.scrapLabPipePhase1Runtime then
		self:sv_slppReleaseHandle( "source" )
		self:sv_slppReleaseHandle( "destination" )
	end
	ProbeOriginalServerOnDestroy( self )
end

local ProbeOriginalServerFixedUpdate = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	ProbeOriginalServerFixedUpdate( self, timeStep )
	local runtime = self.sv.scrapLabPipePhase1Runtime
	if runtime then
		runtime.verifyTicks = runtime.verifyTicks + 1
		self:sv_slppUpdateDestroyedEndpointCase()
		if runtime.verifyTicks % 20 == 0 then
			self:sv_slppResolveEndpoint( "source" )
			self:sv_slppResolveEndpoint( "destination" )
			self:sv_slppTryVerifyReload()
		end
	end
end

local ProbeOriginalBindChatCommands = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	ProbeOriginalBindChatCommands( self )
	sm.game.bindChatCommand( "/slpipeprobe", {
		{ "string", "action", true },
		{ "string", "case", true }
	}, "cl_onChatCommand", "ScrapLab cross-world pipe transaction safety probe" )
end

local ProbeOriginalClientChatCommand = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slpipeprobe" then
		self.network:sendToServer( "sv_slppCommand", {
			action = params[2] or "help",
			caseName = params[3]
		} )
		return
	end
	ProbeOriginalClientChatCommand( self, params )
end
