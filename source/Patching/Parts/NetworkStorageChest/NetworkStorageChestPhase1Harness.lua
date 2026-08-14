-- SCRAPLAB NETWORK STORAGE CHEST PHASE 1 AUTOMATIC HARNESS
-- Creates, validates, empties, and removes its own local pipe-network station.

local SL1A_TERMINAL_UUID = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL1A_CHEST_UUID = sm.uuid.new( "4c474cff-3f6a-4306-93d1-c4c74578afd2" )
local SL1A_T_PIPE_UUID = sm.uuid.new( "bab9d1d1-6131-4329-a6f2-7b2ddf1f25e1" )
local SL1A_WATER_UUID = sm.uuid.new( "869d4736-289a-4952-96cd-8a40117a2d28" )
local SL1A_COMPONENT_UUID = sm.uuid.new( "5530e6a0-4748-4926-b134-50ca9ecb9dcf" )
local SL1A_CIRCUIT_UUID = sm.uuid.new( "f152e4df-bc40-44fb-8d20-3b3ff70cdfe3" )
local SL1A_FERTILIZER_UUID = sm.uuid.new( "ac0b5b0a-14e1-4b31-8944-0a351fbfcc67" )
local SL1A_PREFIX = "[ScrapLab Storage Phase 1 Auto] "
local SL1A_TIMEOUT_TICKS = 600

local function sl1aLog( message )
	sm.log.info( SL1A_PREFIX .. tostring( message ) )
end

local function sl1aMessage( self, player, message )
	if self.sv_slstorage1Message then self:sv_slstorage1Message( player, message )
	else
		sl1aLog( message )
		if player then self.network:sendToClient( player, "cl_slstorage1Message", message ) end
	end
end

local function sl1aBlueprint()
	-- The terminal's +Y opening meets the T-pipe's -Y opening. The two
	-- rotated piped chests meet the T's +/-Z branches.
	return sm.json.writeJsonString( {
		version = 3,
		bodies = { {
			childs = {
				{ color = "00d8ff", controller = { id = 1 }, pos = { x = 0, y = 0, z = 0 }, shapeId = tostring( SL1A_TERMINAL_UUID ), xaxis = 1, zaxis = 3 },
				{ color = "df7f01", controller = { id = 2 }, pos = { x = 0, y = 2, z = 0 }, shapeId = tostring( SL1A_T_PIPE_UUID ), xaxis = 1, zaxis = 3 },
				-- Blueprint positions are rotation-aware local origins, not
				-- axis-aligned minimum corners. yAxis is zAxis cross xAxis.
				-- Positive branch: +Y opening faces -Z; its 2-block Y span
				-- starts at Z=5 and ends against the T at Z=3.
				{ color = "df7f01", controller = { id = 3 }, pos = { x = 0, y = 2, z = 5 }, shapeId = tostring( SL1A_CHEST_UUID ), xaxis = 1, zaxis = 2 },
				-- Negative branch: +Y opening faces +Z. Its -Y local Z axis
				-- also requires shifting the origin to Y=5.
				{ color = "df7f01", controller = { id = 4 }, pos = { x = 0, y = 5, z = -2 }, shapeId = tostring( SL1A_CHEST_UUID ), xaxis = 1, zaxis = -2 }
			}
		} }
	} )
end

local function sl1aShapeIds( bodies )
	local ids = {}
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do ids[#ids + 1] = shape:getId() end
	end
	return ids
end

local function sl1aResolveFixture( bodies )
	local terminal, tPipe = nil, nil
	local chests = {}
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do
			local uuid = shape:getShapeUuid()
			if uuid == SL1A_TERMINAL_UUID then terminal = shape
			elseif uuid == SL1A_T_PIPE_UUID then tPipe = shape
			elseif uuid == SL1A_CHEST_UUID then chests[#chests + 1] = shape end
		end
	end
	table.sort( chests, function( a, b ) return a:getWorldPosition().z < b:getWorldPosition().z end )
	return terminal, tPipe, chests
end

local function sl1aContainer( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end

local function sl1aClearContainer( container )
	if not container then return false end
	if container:isEmpty() then return true end
	if not sm.container.beginTransaction() then return false end
	for slot = 0, container:getSize() - 1 do
		local item = container:getItem( slot )
		if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
			sm.container.spendFromSlot( container, slot, item.uuid, item.quantity, true )
		end
	end
	return sm.container.endTransaction()
end

local function sl1aSetContents( container, entries )
	if not sl1aClearContainer( container ) then return false end
	if not sm.container.beginTransaction() then return false end
	for _, entry in ipairs( entries or {} ) do
		sm.container.collect( container, entry.uuid, entry.quantity, true )
	end
	return sm.container.endTransaction()
end

local function sl1aCount( container, uuid )
	local ok, quantity = pcall( sm.container.totalQuantity, container, uuid )
	return ok and quantity or -1
end

local function sl1aDestroyBodies( bodies )
	for _, body in ipairs( bodies or {} ) do
		if sm.exists( body ) then
			for _, shape in ipairs( body:getShapes() ) do
				if sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
			end
		end
	end
end

local function sl1aSaveCleanup( self, cleanup )
	self.sv.saved.scrapLabStoragePhase1AutoCleanup = cleanup
	self.storage:save( self.sv.saved )
end

local function sl1aRecord( runtime, name, passed, detail )
	runtime.results[#runtime.results + 1] = { name = name, passed = passed, detail = detail }
	sl1aLog( ( passed and "PASS " or "FAIL " ) .. name .. " - " .. tostring( detail ) )
end

local function sl1aSetStage( runtime, stage )
	runtime.stage = stage
	runtime.stageStartedTick = sm.game.getCurrentTick()
	runtime.deadlineTick = runtime.stageStartedTick + SL1A_TIMEOUT_TICKS
	sl1aLog( "stage " .. tostring( stage ) )
end

local function sl1aSnapshotQuantity( snapshot, uuid )
	local wanted = tostring( uuid )
	for _, entry in ipairs( snapshot and snapshot.entries or {} ) do
		if entry.uuid == wanted then return entry.quantity or 0, entry.stacks or 0, entry.sources or 0 end
	end
	return 0, 0, 0
end

local function sl1aTerminalInstance( shape )
	return shape and g_scrapLabNetworkStorageChestInstances and
		g_scrapLabNetworkStorageChestInstances[tostring( shape:getId() )] or nil
end

local function sl1aReadyDetail( runtime )
	local detail = runtime and runtime.readyDetail or nil
	if not detail then return "readiness telemetry unavailable" end
	return "terminalContainer=" .. tostring( detail.terminalContainer ) ..
		", chestContainers=" .. tostring( detail.chestContainers ) .. "/2" ..
		", terminalInstance=" .. tostring( detail.terminalInstance ) ..
		", neighbours=" .. tostring( detail.neighbours ) ..
		", tPipeById=" .. tostring( detail.tPipeById ) ..
		", tPipeNeighbours=" .. tostring( detail.tPipeNeighbours ) ..
		", chestNeighbours=" .. tostring( detail.chestNeighbours ) ..
		", localContainers=" .. tostring( detail.localContainers ) ..
		", pipeQuery=" .. tostring( detail.pipeQuery )
end

local function sl1aRuntimeDetail( runtime )
	if not runtime then return "runtime unavailable" end
	if runtime.stage == "WAIT_READY" then return sl1aReadyDetail( runtime ) end
	local parts = {
		"stage=" .. tostring( runtime.stage ),
		"elapsed=" .. tostring( sm.game.getCurrentTick() - ( runtime.stageStartedTick or sm.game.getCurrentTick() ) )
	}
	local instance = runtime.instance
	if not instance or not instance.sv then
		parts[#parts + 1] = "terminalInstance=false"
		return table.concat( parts, ", " )
	end
	local state = instance.sv
	local snapshot = state.snapshot
	parts[#parts + 1] = "indexing=" .. tostring( state.indexing )
	parts[#parts + 1] = "containers=" .. tostring( #( state.containers or {} ) )
	parts[#parts + 1] = "topology=" .. tostring( state.topologyGeneration ) .. "/" .. tostring( runtime.topologyGeneration )
	parts[#parts + 1] = "content=" .. tostring( state.contentGeneration ) .. "/" .. tostring( runtime.contentGeneration )
	parts[#parts + 1] = "snapshot=" .. tostring( snapshot and snapshot.status )
	parts[#parts + 1] = "snapshotContainers=" .. tostring( snapshot and snapshot.containerCount )
	parts[#parts + 1] = "snapshotQuantity=" .. tostring( snapshot and snapshot.totalQuantity )
	parts[#parts + 1] = "lastError=" .. tostring( state.lastError )
	return table.concat( parts, ", " )
end

local function sl1aNeighbourCount( shape )
	local ok, neighbours = pcall( function() return shape:getPipedNeighbours() end )
	if not ok or type( neighbours ) ~= "table" then return -1 end
	return #neighbours
end

local function sl1aFinish( self, runtime, fatalMessage )
	if fatalMessage then sl1aRecord( runtime, "automatic-fixture", false, fatalMessage ) end
	if runtime.instance and runtime.player then pcall( function() runtime.instance:sv_endPhase1HarnessSession( runtime.player ) end ) end
	for _, chest in ipairs( runtime.chests or {} ) do sl1aClearContainer( sl1aContainer( chest ) ) end
	sl1aClearContainer( sl1aContainer( runtime.terminal ) )
	sl1aDestroyBodies( runtime.bodies )
	sl1aSaveCleanup( self, nil )
	self.sv.scrapLabStoragePhase1AutoRuntime = nil

	local passed, failed = 0, 0
	for _, result in ipairs( runtime.results ) do
		if result.passed then passed = passed + 1 else
			failed = failed + 1
			sl1aMessage( self, runtime.player, "FAIL " .. result.name .. " - " .. tostring( result.detail ) )
		end
	end
	sl1aMessage( self, runtime.player, "AUTOMATIC TEST COMPLETE: " .. tostring( passed ) ..
		" passed, " .. tostring( failed ) .. " failed. Disposable station removed." )
end

local function sl1aDestroyStoredFixture( cleanup )
	if not cleanup or not cleanup.world or not sm.exists( cleanup.world ) then return false end
	local wanted = {}
	for _, id in ipairs( cleanup.shapeIds or {} ) do wanted[tostring( id )] = true end
	for _, body in ipairs( sm.body.getAllBodies( cleanup.world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if wanted[tostring( shape:getId() )] then
				wanted[tostring( shape:getId() )] = nil
				pcall( function() shape:destroyShape( 0 ) end )
			end
		end
	end
	-- The cell-ready callback makes this scan authoritative. IDs not found in
	-- the loaded cell were already removed before an interrupted cleanup.
	return true
end

function SurvivalGame.sv_slstorage1AutoRecoveryCellLoaded( self, _, _, _, params )
	local runtime = self.sv.scrapLabStoragePhase1AutoRuntime
	if runtime and runtime.stage == "RECOVERY" and params and params.token == runtime.token then runtime.recoveryReady = true end
end

function SurvivalGame.sv_slstorage1StartAuto( self, player )
	if self.sv.scrapLabStoragePhase1AutoRuntime then
		sl1aMessage( self, player, "An automatic Phase 1 test is already running." )
		return
	end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then sl1aMessage( self, player, "A live character is required." ); return end

	local cleanup = self.sv.saved.scrapLabStoragePhase1AutoCleanup
	if cleanup then
		local token = "slstorage1-recovery:" .. tostring( sm.game.getCurrentTick() ) .. ":" .. tostring( player.id )
		local runtime = { token = token, player = player, cleanup = cleanup, results = {} }
		sl1aSetStage( runtime, "RECOVERY" )
		self.sv.scrapLabStoragePhase1AutoRuntime = runtime
		if cleanup.world then
			if not sm.exists( cleanup.world ) then pcall( function() sm.world.loadWorld( cleanup.world ) end ) end
			local ok, handle = pcall( function()
				return cleanup.world:loadCellWithHandle( cleanup.cellX, cleanup.cellY,
					"sv_slstorage1AutoRecoveryCellLoaded", { token = token } )
			end )
			if ok then runtime.recoveryHandle = handle else runtime.recoveryReady = true end
		else runtime.recoveryReady = true end
		sl1aMessage( self, player, "Recovering an interrupted disposable station, then the test will start automatically." )
		return
	end

	local direction = character:getDirection(); direction.z = 0
	direction = direction:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local world = character:getWorld()
	local position = character:getWorldPosition() + direction * 7 + sm.vec3.new( 0, 0, 1.5 )
	local ok, bodies = pcall( function()
		return sm.creation.importFromString( world, sl1aBlueprint(), position, sm.quat.identity(), false, false )
	end )
	if not ok or type( bodies ) ~= "table" or #bodies == 0 then
		sl1aMessage( self, player, "AUTOMATIC TEST FAILED: the disposable station could not be imported." )
		return
	end
	local terminal, tPipe, chests = sl1aResolveFixture( bodies )
	if not terminal or not tPipe or #chests ~= 2 then
		sl1aDestroyBodies( bodies )
		sl1aMessage( self, player, "AUTOMATIC TEST FAILED: the imported station is incomplete." )
		return
	end

	local cleanupRecord = {
		world = world,
		cellX = math.floor( position.x / 64 ),
		cellY = math.floor( position.y / 64 ),
		shapeIds = sl1aShapeIds( bodies )
	}
	sl1aSaveCleanup( self, cleanupRecord )
	local runtime = {
		player = player,
		world = world,
		position = position,
		bodies = bodies,
		terminal = terminal,
		tPipe = tPipe,
		chests = chests,
		results = {}
	}
	sl1aSetStage( runtime, "WAIT_READY" )
	self.sv.scrapLabStoragePhase1AutoRuntime = runtime
	sl1aMessage( self, player, "Automatic local-network station created. No building or item placement is required." )
end

function SurvivalGame.sv_slstorage1ProcessAuto( self )
	local runtime = self.sv.scrapLabStoragePhase1AutoRuntime
	if not runtime then return end
	local tick = sm.game.getCurrentTick()
	if tick > runtime.deadlineTick then
		if runtime.stage == "RECOVERY" then
			if runtime.recoveryHandle then pcall( function() runtime.recoveryHandle:release() end ) end
			self.sv.scrapLabStoragePhase1AutoRuntime = nil
			sl1aMessage( self, runtime.player, "AUTOMATIC TEST FAILED: interrupted-station recovery timed out; cleanup record was preserved." )
		else
			local ok, detail = pcall( sl1aRuntimeDetail, runtime )
			sl1aFinish( self, runtime, "test timed out during " .. tostring( runtime.stage ) ..
				" (" .. tostring( ok and detail or "telemetry failed: " .. tostring( detail ) ) .. ")" )
		end
		return
	end

	if runtime.stage == "RECOVERY" then
		if not runtime.recoveryReady then return end
		if runtime.recoveryHandle then pcall( function() runtime.recoveryHandle:release() end ) end
		if not sl1aDestroyStoredFixture( runtime.cleanup ) then
			sl1aMessage( self, runtime.player, "AUTOMATIC TEST FAILED: interrupted fixture cleanup could not be verified." )
			self.sv.scrapLabStoragePhase1AutoRuntime = nil
			return
		end
		sl1aSaveCleanup( self, nil )
		local player = runtime.player
		self.sv.scrapLabStoragePhase1AutoRuntime = nil
		self:sv_slstorage1StartAuto( player )
		return
	end

	if runtime.stage == "WAIT_READY" then
		local terminalContainer = sl1aContainer( runtime.terminal )
		local chestA, chestB = sl1aContainer( runtime.chests[1] ), sl1aContainer( runtime.chests[2] )
		local instance = sl1aTerminalInstance( runtime.terminal )
		runtime.readyDetail = {
			terminalContainer = terminalContainer ~= nil,
			chestContainers = ( chestA and 1 or 0 ) + ( chestB and 1 or 0 ),
			terminalInstance = instance ~= nil,
			neighbours = 0,
			tPipeById = false,
			tPipeNeighbours = 0,
			chestNeighbours = "0/0",
			localContainers = 0,
			pipeQuery = "not-run"
		}
		if not terminalContainer or not chestA or not chestB or not instance then return end
		runtime.fixtureReadyTick = runtime.fixtureReadyTick or tick
		local neighbourOk, neighbours = pcall( function() return runtime.terminal:getPipedNeighbours() end )
		if not neighbourOk then runtime.readyDetail.pipeQuery = "neighbours-error: " .. tostring( neighbours ); return end
		runtime.readyDetail.neighbours = #( neighbours or {} )
		local hasT = false
		local tPipeId = runtime.tPipe:getId()
		for _, neighbour in ipairs( neighbours or {} ) do
			if neighbour and sm.exists( neighbour ) and neighbour:getId() == tPipeId then hasT = true; break end
		end
		runtime.readyDetail.tPipeById = hasT
		if not hasT then return end
		runtime.readyDetail.tPipeNeighbours = sl1aNeighbourCount( runtime.tPipe )
		runtime.readyDetail.chestNeighbours = tostring( sl1aNeighbourCount( runtime.chests[1] ) ) .. "/" ..
			tostring( sl1aNeighbourCount( runtime.chests[2] ) )
		local inputOk, inputShapes, inputFailure = pcall( function() return instance:sv_collectLocalContainers() end )
		if not inputOk then runtime.readyDetail.pipeQuery = "local-error: " .. tostring( inputShapes ); return end
		if not inputShapes then runtime.readyDetail.pipeQuery = "local-failure: " .. tostring( inputFailure ); return end
		runtime.readyDetail.pipeQuery = "ok"
		local inputCount = 0
		for _, descriptor in ipairs( inputShapes or {} ) do
			if descriptor.shape and descriptor.shape:getShapeUuid() == SL1A_CHEST_UUID then inputCount = inputCount + 1 end
		end
		runtime.readyDetail.localContainers = inputCount
		if inputCount ~= 2 then
			-- Import callbacks and pipe topology settle asynchronously, but a
			-- complete fixture must do so quickly. Fail with evidence instead of
			-- hiding a malformed blueprint behind a generic 15-second timeout.
			if tick - runtime.fixtureReadyTick >= 80 then
				sl1aFinish( self, runtime, "generated fixture did not form a complete T-pipe network (" ..
					sl1aReadyDetail( runtime ) .. ")" )
			end
			return
		end
		runtime.physicalTConnected = runtime.readyDetail.tPipeNeighbours == 3 and
			runtime.readyDetail.chestNeighbours == "1/1"

		if not sl1aSetContents( chestA, {
			{ uuid = SL1A_WATER_UUID, quantity = 10 }, { uuid = SL1A_COMPONENT_UUID, quantity = 5 }
		} ) or not sl1aSetContents( chestB, {
			{ uuid = SL1A_WATER_UUID, quantity = 7 }, { uuid = SL1A_CIRCUIT_UUID, quantity = 9 }
		} ) or not sl1aSetContents( terminalContainer, {
			{ uuid = SL1A_FERTILIZER_UUID, quantity = 3 }
		} ) then
			sl1aFinish( self, runtime, "could not initialize disposable inventories" )
			return
		end
		runtime.instance = instance
		local sessionOk, sessionStarted, sessionFailure = pcall( function()
			return instance:sv_beginPhase1HarnessSession( runtime.player )
		end )
		if not sessionOk or not sessionStarted then
			sl1aFinish( self, runtime, "could not begin isolated index session: " ..
				tostring( sessionOk and sessionFailure or sessionStarted ) )
			return
		end
		sl1aSetStage( runtime, "WAIT_BASELINE" )
		return
	end

	if runtime.stage == "WAIT_BASELINE" then
		local snapshot = runtime.instance.sv.snapshot
		if not snapshot or snapshot.status ~= "READY" or runtime.instance.sv.indexing then return end
		local water, waterStacks, waterSources = sl1aSnapshotQuantity( snapshot, SL1A_WATER_UUID )
		local components = sl1aSnapshotQuantity( snapshot, SL1A_COMPONENT_UUID )
		local circuits = sl1aSnapshotQuantity( snapshot, SL1A_CIRCUIT_UUID )
		local fertilizer = sl1aSnapshotQuantity( snapshot, SL1A_FERTILIZER_UUID )
		sl1aRecord( runtime, "physical-t-pipe-topology", runtime.physicalTConnected == true and snapshot.containerCount == 2,
			"containers=" .. tostring( snapshot.containerCount ) .. ", T/chests=" ..
			tostring( runtime.readyDetail.tPipeNeighbours ) .. "/" .. tostring( runtime.readyDetail.chestNeighbours ) )
		sl1aRecord( runtime, "deduplicated-container-index", #runtime.instance.sv.containers == 2, "descriptors=" .. tostring( #runtime.instance.sv.containers ) )
		sl1aRecord( runtime, "aggregate-item-totals", water == 17 and components == 5 and circuits == 9 and snapshot.totalQuantity == 31,
			"water=" .. water .. ", components=" .. components .. ", circuits=" .. circuits .. ", total=" .. tostring( snapshot.totalQuantity ) )
		sl1aRecord( runtime, "aggregate-stack-sources", waterStacks == 2 and waterSources == 2, "water stacks/sources=" .. waterStacks .. "/" .. waterSources )
		sl1aRecord( runtime, "terminal-buffer-excluded", fertilizer == 0 and sl1aCount( sl1aContainer( runtime.terminal ), SL1A_FERTILIZER_UUID ) == 3,
			"catalog fertilizer=" .. fertilizer .. ", buffer fertilizer=3" )
		sl1aRecord( runtime, "five-slot-real-buffer", sl1aContainer( runtime.terminal ):getSize() == 5, "slots=" .. tostring( sl1aContainer( runtime.terminal ):getSize() ) )

		local stats = g_scrapLabNetworkInventoryIndex.getStatistics()
		runtime.cacheBaseline = stats
		runtime.baselineSignature = runtime.instance.sv.lastSignature
		runtime.instance:sv_refreshTopology( true )
		sl1aSetStage( runtime, "WAIT_CACHE" )
		return
	end

	if runtime.stage == "WAIT_CACHE" then
		if runtime.instance.sv.indexing then return end
		local stats = g_scrapLabNetworkInventoryIndex.getStatistics()
		sl1aRecord( runtime, "warm-cache-reindex", stats.containerScans == runtime.cacheBaseline.containerScans and
			stats.slotsScanned == runtime.cacheBaseline.slotsScanned and stats.cacheHits >= runtime.cacheBaseline.cacheHits + 2,
			"hits +" .. tostring( stats.cacheHits - runtime.cacheBaseline.cacheHits ) ..
			", scans +" .. tostring( stats.containerScans - runtime.cacheBaseline.containerScans ) )
		local chestA = sl1aContainer( runtime.chests[1] )
		if not sm.container.beginTransaction() then sl1aFinish( self, runtime, "could not begin revision mutation" ); return end
		sm.container.collect( chestA, SL1A_WATER_UUID, 3, true )
		if not sm.container.endTransaction() then sl1aFinish( self, runtime, "could not commit revision mutation" ); return end
		runtime.contentGeneration = runtime.instance.sv.contentGeneration
		sl1aSetStage( runtime, "WAIT_REVISION" )
		return
	end

	if runtime.stage == "WAIT_REVISION" then
		if runtime.instance.sv.indexing or runtime.instance.sv.contentGeneration <= runtime.contentGeneration then return end
		local snapshot = runtime.instance.sv.snapshot
		local water = sl1aSnapshotQuantity( snapshot, SL1A_WATER_UUID )
		sl1aRecord( runtime, "single-container-revision-refresh", water == 20 and snapshot.totalQuantity == 34,
			"water=" .. water .. ", total=" .. tostring( snapshot.totalQuantity ) )
		local chestB = sl1aContainer( runtime.chests[2] )
		if not sl1aClearContainer( chestB ) then sl1aFinish( self, runtime, "could not empty removable topology branch" ); return end
		pcall( function() runtime.chests[2]:destroyShape( 0 ) end )
		runtime.topologyGeneration = runtime.instance.sv.topologyGeneration
		sl1aSetStage( runtime, "WAIT_TOPOLOGY" )
		return
	end

	if runtime.stage == "WAIT_TOPOLOGY" then
		if runtime.instance.sv.indexing or runtime.instance.sv.topologyGeneration <= runtime.topologyGeneration then return end
		local snapshot = runtime.instance.sv.snapshot
		local water = sl1aSnapshotQuantity( snapshot, SL1A_WATER_UUID )
		local components = sl1aSnapshotQuantity( snapshot, SL1A_COMPONENT_UUID )
		sl1aRecord( runtime, "topology-removal-refresh", snapshot.containerCount == 1 and water == 13 and components == 5 and snapshot.totalQuantity == 18,
			"containers=" .. tostring( snapshot.containerCount ) .. ", water=" .. water .. ", total=" .. tostring( snapshot.totalQuantity ) )

		local scalePassed, scaleDetails = true, {}
		for _, count in ipairs( { 1, 50, 100, 500 } ) do
			local records = {}
			for index = 1, count do records[index] = { items = { { uuid = tostring( SL1A_WATER_UUID ), quantity = 2, stacks = 1 } } } end
			local aggregate = g_scrapLabNetworkInventoryIndex.aggregate( records )
			local passed = aggregate.uniqueItems == 1 and aggregate.totalQuantity == count * 2 and aggregate.totalStacks == count
			scalePassed = scalePassed and passed
			scaleDetails[#scaleDetails + 1] = tostring( count ) .. "=" .. ( passed and "ok" or "bad" )
		end
		sl1aRecord( runtime, "aggregate-scale-1-50-100-500", scalePassed, table.concat( scaleDetails, ", " ) )

		runtime.instance:sv_endPhase1HarnessSession( runtime.player )
		local diagnostics = runtime.terminal:getInteractable().publicData.scrapLabStoragePhase1
		sl1aRecord( runtime, "zero-idle-viewer-work", diagnostics and diagnostics.status == "IDLE" and diagnostics.viewers == 0,
			"status=" .. tostring( diagnostics and diagnostics.status ) .. ", viewers=" .. tostring( diagnostics and diagnostics.viewers ) )
		sl1aFinish( self, runtime )
	end
end

local SL1A_PREVIOUS_COMMAND = SurvivalGame.sv_slstorage1Command
function SurvivalGame.sv_slstorage1Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "status" ) )
	if action == "auto" then self:sv_slstorage1StartAuto( player )
	elseif SL1A_PREVIOUS_COMMAND then SL1A_PREVIOUS_COMMAND( self, data, player ) end
end

local SL1A_ORIGINAL_FIXED_UPDATE = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	if SL1A_ORIGINAL_FIXED_UPDATE then SL1A_ORIGINAL_FIXED_UPDATE( self, timeStep ) end
	local ok, failure = pcall( function() self:sv_slstorage1ProcessAuto() end )
	if not ok then
		local runtime = self.sv and self.sv.scrapLabStoragePhase1AutoRuntime or nil
		local stage = runtime and runtime.stage or "UNKNOWN"
		sl1aLog( "runtime error during " .. tostring( stage ) .. ": " .. tostring( failure ) )
		if runtime and stage ~= "RECOVERY" then
			local finishOk, finishFailure = pcall( function()
				sl1aFinish( self, runtime, "runtime error during " .. tostring( stage ) .. ": " .. tostring( failure ) )
			end )
			if not finishOk then
				sl1aLog( "emergency cleanup failed: " .. tostring( finishFailure ) )
				self.sv.scrapLabStoragePhase1AutoRuntime = nil
				sl1aMessage( self, runtime.player, "AUTOMATIC TEST FAILED during " .. tostring( stage ) ..
					". Cleanup will be recovered on the next run." )
			end
		elseif runtime then
			if runtime.recoveryHandle then pcall( function() runtime.recoveryHandle:release() end ) end
			self.sv.scrapLabStoragePhase1AutoRuntime = nil
			sl1aMessage( self, runtime.player, "AUTOMATIC TEST FAILED during interrupted-station recovery. " ..
				"The cleanup record was preserved for the next run." )
		end
	end
end

sl1aLog( "automatic harness ready; use /slstorage1 auto" )
