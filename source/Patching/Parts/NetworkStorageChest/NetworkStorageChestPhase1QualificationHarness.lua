-- SCRAPLAB NETWORK STORAGE CHEST PHASE 1 QUALIFICATION HARNESS
-- Real engine-container scale, shared cache, client transport, idle, and cleanup.

local SLQ_TERMINAL_UUID = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SLQ_CHEST_UUID = sm.uuid.new( "4c474cff-3f6a-4306-93d1-c4c74578afd2" )
local SLQ_WATER_UUID = sm.uuid.new( "869d4736-289a-4952-96cd-8a40117a2d28" )
local SLQ_TOTAL_CHESTS = 500
local SLQ_SPAWN_BATCH = 25
local SLQ_WORK_BATCH = 25
local SLQ_IDLE_TICKS = 240
local SLQ_STAGE_TIMEOUT = 2400
local SLQ_PREFIX = "[ScrapLab Storage Phase 1 Qualify] "

local function slqLog( message ) sm.log.info( SLQ_PREFIX .. tostring( message ) ) end

local function slqMessage( self, player, message )
	if self.sv_slstorage1Message then self:sv_slstorage1Message( player, message )
	else
		slqLog( message )
		if player then self.network:sendToClient( player, "cl_slstorage1Message", message ) end
	end
end

local function slqSetStage( runtime, stage )
	runtime.stage = stage
	runtime.stageStartedTick = sm.game.getCurrentTick()
	runtime.deadlineTick = runtime.stageStartedTick + SLQ_STAGE_TIMEOUT
	slqLog( "stage " .. tostring( stage ) )
end

local function slqRecord( runtime, name, passed, detail )
	runtime.results[#runtime.results + 1] = { name = name, passed = passed, detail = tostring( detail ) }
	slqLog( ( passed and "PASS " or "FAIL " ) .. name .. " - " .. tostring( detail ) )
end

local function slqContainer( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end

local function slqInstance( shape )
	if not shape or not sm.exists( shape ) or not g_scrapLabNetworkStorageChestInstances then return nil end
	return g_scrapLabNetworkStorageChestInstances[tostring( shape:getId() )]
end

local function slqClearContainer( container )
	if not container then return true end
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

local function slqSaveCleanup( self, runtime )
	self.sv.saved.scrapLabStoragePhase1QualificationCleanup = runtime and {
		world = runtime.world,
		worldId = runtime.world and runtime.world.id or nil,
		shapeIds = runtime.shapeIds or {}
	} or nil
	self.storage:save( self.sv.saved )
end

local function slqDestroyStoredCleanup( self, cleanup, player )
	local character = player and player:getCharacter() or nil
	if not cleanup or not character or not sm.exists( character ) then return false, "live character unavailable" end
	local world = character:getWorld()
	if cleanup.worldId and tostring( cleanup.worldId ) ~= tostring( world.id ) then
		return false, "return to world " .. tostring( cleanup.worldId ) .. " to clean the interrupted qualification"
	end
	local wanted = {}
	for _, id in ipairs( cleanup.shapeIds or {} ) do wanted[tostring( id )] = true end
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if wanted[tostring( shape:getId() )] then
				wanted[tostring( shape:getId() )] = nil
				slqClearContainer( slqContainer( shape ) )
				pcall( function() shape:destroyShape( 0 ) end )
			end
		end
	end
	self.sv.saved.scrapLabStoragePhase1QualificationCleanup = nil
	self.storage:save( self.sv.saved )
	return true
end

local function slqShapePosition( origin, index )
	local zero = index - 1
	local x = zero % 10
	local y = math.floor( zero / 10 ) % 10
	local z = math.floor( zero / 100 )
	return origin + sm.vec3.new( ( x - 4.5 ) * 1.05, ( y - 4.5 ) * 1.05, 7.0 + z * 1.05 )
end

local function slqDescriptors( runtime, count )
	local result = {}
	for index = 1, count do
		local shape = runtime.chests[index]
		local container = slqContainer( shape )
		local id = g_scrapLabNetworkInventoryIndex.getContainerId( container )
		if not id then return nil, "container " .. tostring( index ) .. " has no stable id" end
		result[index] = { id = id, shape = shape, container = container }
	end
	return result
end

local function slqStats() return g_scrapLabNetworkInventoryIndex.getStatistics() end

local function slqStatsDelta( after, before )
	return {
		cacheHits = after.cacheHits - before.cacheHits,
		containerScans = after.containerScans - before.containerScans,
		slotsScanned = after.slotsScanned - before.slotsScanned
	}
end

local function slqCaptureTerminalActivity( runtime )
	local result = {}
	for index, terminal in ipairs( runtime.terminals or {} ) do
		local publicData = terminal:getInteractable().publicData
		local diagnostics = publicData and publicData.scrapLabStoragePhase1 or nil
		result[index] = diagnostics and diagnostics.activitySerial or -1
	end
	return result
end

local function slqEndSessions( runtime )
	for index, instance in ipairs( runtime.instances or {} ) do
		if instance then pcall( function() instance:sv_endPhase1HarnessSession( runtime.player ) end ) end
	end
end

local function slqBeginCleanup( self, runtime, failure )
	if runtime.cleanupStarted then return end
	runtime.cleanupStarted = true
	if failure then slqRecord( runtime, "qualification-runtime", false, failure ) end
	slqEndSessions( runtime )
	runtime.workIndex = 1
	slqSetStage( runtime, "CLEAR_ITEMS" )
end

local function slqFinish( self, runtime )
	slqSaveCleanup( self, nil )
	self.sv.scrapLabStoragePhase1QualificationRuntime = nil
	local passed, failed = 0, 0
	for _, result in ipairs( runtime.results ) do
		if result.passed then passed = passed + 1
		else
			failed = failed + 1
			slqMessage( self, runtime.player, "FAIL " .. result.name .. " - " .. result.detail )
		end
	end
	slqMessage( self, runtime.player, "PHASE 1 QUALIFICATION COMPLETE: " .. tostring( passed ) ..
		" passed, " .. tostring( failed ) .. " failed. All 502 test parts removed." )
end

function SurvivalGame.sv_slstorage1StartQualification( self, player )
	if self.sv.scrapLabStoragePhase1AutoRuntime or self.sv.scrapLabStoragePhase1QualificationRuntime then
		slqMessage( self, player, "A Network Storage Chest automatic test is already running." )
		return
	end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then slqMessage( self, player, "A live character is required." ); return end
	local oldCleanup = self.sv.saved.scrapLabStoragePhase1QualificationCleanup
	if oldCleanup then
		local ok, failure = slqDestroyStoredCleanup( self, oldCleanup, player )
		if not ok then slqMessage( self, player, "QUALIFICATION RECOVERY FAILED: " .. tostring( failure ) ); return end
		slqMessage( self, player, "Interrupted qualification cleaned up. Starting a fresh run." )
	end

	local direction = character:getDirection(); direction.z = 0
	direction = direction:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local lateral = sm.vec3.new( -direction.y, direction.x, 0 )
	local runtime = {
		player = player,
		world = character:getWorld(),
		origin = character:getWorldPosition(),
		direction = direction,
		lateral = lateral,
		chests = {}, terminals = {}, instances = {}, shapeIds = {}, results = {}, spawnIndex = 1
	}
	self.sv.scrapLabStoragePhase1QualificationRuntime = runtime
	slqSaveCleanup( self, runtime )
	slqSetStage( runtime, "SPAWN_TERMINALS" )
	slqMessage( self, player, "Phase 1 qualification started. Stay near this spot; ScrapLab will create and remove 502 temporary parts." )
end

local function slqProcessQualification( self )
	local runtime = self.sv.scrapLabStoragePhase1QualificationRuntime
	if not runtime then return end
	local tick = sm.game.getCurrentTick()
	if tick > runtime.deadlineTick and runtime.stage ~= "CLEAR_ITEMS" and runtime.stage ~= "DESTROY_PARTS" and runtime.stage ~= "VERIFY_CLEANUP" then
		if runtime.stage == "WAIT_CLIENT_ROUNDTRIP" then
			local probe = g_scrapLabStoragePhase1ClientQualification and g_scrapLabStoragePhase1ClientQualification[runtime.clientToken]
			slqRecord( runtime, "normal-client-catalog-roundtrip", false,
				"timed out after transport stage " .. tostring( probe and probe.stage or "EVENT_NOT_DELIVERED" ) )
			slqEndSessions( runtime )
			runtime.idleBefore = slqCaptureTerminalActivity( runtime )
			runtime.idleUntil = tick + SLQ_IDLE_TICKS
			slqSetStage( runtime, "WAIT_IDLE" )
			return
		end
		slqBeginCleanup( self, runtime, "stage timed out: " .. tostring( runtime.stage ) )
		return
	end

	if runtime.stage == "SPAWN_TERMINALS" then
		for index = 1, 2 do
			local position = runtime.origin + runtime.direction * 5 + runtime.lateral * ( index == 1 and -1.2 or 1.2 ) + sm.vec3.new( 0, 0, 1.2 )
			local ok, shape = pcall( sm.shape.createPart, SLQ_TERMINAL_UUID, position, sm.quat.identity(), false, true, runtime.world )
			if not ok or not shape then slqBeginCleanup( self, runtime, "could not spawn qualification terminal " .. tostring( index ) ); return end
			runtime.terminals[index] = shape
			runtime.shapeIds[#runtime.shapeIds + 1] = shape:getId()
		end
		slqSaveCleanup( self, runtime )
		slqSetStage( runtime, "SPAWN_CHESTS" )
		return
	end

	if runtime.stage == "SPAWN_CHESTS" then
		local last = math.min( runtime.spawnIndex + SLQ_SPAWN_BATCH - 1, SLQ_TOTAL_CHESTS )
		for index = runtime.spawnIndex, last do
			local position = slqShapePosition( runtime.origin, index )
			local ok, shape = pcall( sm.shape.createPart, SLQ_CHEST_UUID, position, sm.quat.identity(), false, true, runtime.world )
			if not ok or not shape then slqBeginCleanup( self, runtime, "could not spawn real chest " .. tostring( index ) ); return end
			runtime.chests[index] = shape
			runtime.shapeIds[#runtime.shapeIds + 1] = shape:getId()
		end
		runtime.spawnIndex = last + 1
		slqSaveCleanup( self, runtime )
		if runtime.spawnIndex > SLQ_TOTAL_CHESTS then slqSetStage( runtime, "WAIT_CONTAINERS" ) end
		return
	end

	if runtime.stage == "WAIT_CONTAINERS" then
		local ready = 0
		for _, shape in ipairs( runtime.chests ) do if slqContainer( shape ) then ready = ready + 1 end end
		runtime.instances[1], runtime.instances[2] = slqInstance( runtime.terminals[1] ), slqInstance( runtime.terminals[2] )
		if ready < SLQ_TOTAL_CHESTS or not runtime.instances[1] or not runtime.instances[2] then return end
		slqRecord( runtime, "real-500-container-fixture", #runtime.chests == SLQ_TOTAL_CHESTS and
			#runtime.terminals == 2 and ready == SLQ_TOTAL_CHESTS,
			"chests=" .. tostring( #runtime.chests ) .. ", containers=" .. tostring( ready ) .. ", terminals=2" )
		runtime.workIndex = 1
		slqSetStage( runtime, "POPULATE" )
		return
	end

	if runtime.stage == "POPULATE" then
		local last = math.min( runtime.workIndex + SLQ_WORK_BATCH - 1, SLQ_TOTAL_CHESTS )
		if not sm.container.beginTransaction() then slqBeginCleanup( self, runtime, "could not begin population transaction" ); return end
		for index = runtime.workIndex, last do sm.container.collect( slqContainer( runtime.chests[index] ), SLQ_WATER_UUID, 1, true ) end
		if not sm.container.endTransaction() then slqBeginCleanup( self, runtime, "could not populate real qualification containers" ); return end
		runtime.workIndex = last + 1
		if runtime.workIndex > SLQ_TOTAL_CHESTS then
			runtime.benchmarkCounts = { 1, 50, 100, 500 }
			runtime.benchmarkIndex = 1
			slqSetStage( runtime, "START_BENCHMARK" )
		end
		return
	end

	if runtime.stage == "START_BENCHMARK" then
		local count = runtime.benchmarkCounts[runtime.benchmarkIndex]
		local descriptors, failure = slqDescriptors( runtime, count )
		if not descriptors then slqBeginCleanup( self, runtime, failure ); return end
		for _, descriptor in ipairs( descriptors ) do g_scrapLabNetworkInventoryIndex.invalidate( descriptor.id ) end
		runtime.benchmarkRunId = "BENCHMARK:" .. tostring( count ) .. ":" .. tostring( tick )
		local ok, started, startFailure = pcall( function()
			return runtime.instances[1]:sv_beginPhase1QualificationSession(
				runtime.player, descriptors, runtime.benchmarkRunId )
		end )
		if not ok or not started then slqBeginCleanup( self, runtime, "could not start " .. count .. "-container scan: " .. tostring( ok and startFailure or started ) ); return end
		runtime.currentBenchmarkCount = count
		slqSetStage( runtime, "WAIT_BENCHMARK" )
		return
	end

	if runtime.stage == "WAIT_BENCHMARK" then
		local instance, count = runtime.instances[1], runtime.currentBenchmarkCount
		local snapshot = instance.sv.snapshot
		if instance.sv.indexing or not snapshot or snapshot.status ~= "READY" or
			snapshot.containerCount ~= count or snapshot.qualificationRunId ~= runtime.benchmarkRunId then return end
		local maxTicks = math.ceil( count / 12 ) + 2
		local passed = snapshot.totalQuantity == count and snapshot.uniqueItems == 1 and
			snapshot.scanContainerScans == count and snapshot.scanSlotsScanned == count * 10 and
			( snapshot.scanDurationTicks or 99999 ) <= maxTicks
		slqRecord( runtime, "real-engine-index-" .. tostring( count ), passed,
			"containers=" .. tostring( snapshot.containerCount ) .. ", quantity=" .. tostring( snapshot.totalQuantity ) ..
			", scans=" .. tostring( snapshot.scanContainerScans ) .. ", slots=" .. tostring( snapshot.scanSlotsScanned ) ..
			", ticks=" .. tostring( snapshot.scanDurationTicks ) .. "/" .. tostring( maxTicks ) )
		runtime.benchmarkIndex = runtime.benchmarkIndex + 1
		if runtime.benchmarkIndex <= #runtime.benchmarkCounts then slqSetStage( runtime, "START_BENCHMARK" )
		else
			runtime.sharedDescriptors = slqDescriptors( runtime, SLQ_TOTAL_CHESTS )
			runtime.sharedRunId = "SHARED:" .. tostring( tick )
			local ok, started = pcall( function()
				return runtime.instances[2]:sv_beginPhase1QualificationSession(
					runtime.player, runtime.sharedDescriptors, runtime.sharedRunId )
			end )
			if not ok or not started then slqBeginCleanup( self, runtime, "second terminal could not start shared-cache scan" ); return end
			slqSetStage( runtime, "WAIT_SHARED_CACHE" )
		end
		return
	end

	if runtime.stage == "WAIT_SHARED_CACHE" then
		local instance = runtime.instances[2]
		local snapshot = instance.sv.snapshot
		if instance.sv.indexing or not snapshot or snapshot.containerCount ~= SLQ_TOTAL_CHESTS or
			snapshot.qualificationRunId ~= runtime.sharedRunId then return end
		slqRecord( runtime, "two-terminal-shared-cache", snapshot.totalQuantity == SLQ_TOTAL_CHESTS and
			snapshot.scanContainerScans == 0 and snapshot.scanSlotsScanned == 0 and
			snapshot.scanCacheHits == SLQ_TOTAL_CHESTS,
			"hits=" .. tostring( snapshot.scanCacheHits ) .. ", scans=" .. tostring( snapshot.scanContainerScans ) ..
			", slots=" .. tostring( snapshot.scanSlotsScanned ) )
		runtime.clientToken = "slq:" .. tostring( tick ) .. ":" .. tostring( runtime.player.id )
		g_scrapLabStoragePhase1ClientQualification = g_scrapLabStoragePhase1ClientQualification or {}
		g_scrapLabStoragePhase1ClientQualification[runtime.clientToken] = nil
		local ok, failure = pcall( function()
			sm.event.sendToInteractable( runtime.terminals[1]:getInteractable(), "sv_e_startPhase1ClientQualification", {
				playerId = tostring( runtime.player.id ), token = runtime.clientToken,
				expectedContainers = SLQ_TOTAL_CHESTS, expectedQuantity = SLQ_TOTAL_CHESTS
			} )
		end )
		if not ok then slqBeginCleanup( self, runtime, "client qualification event failed: " .. tostring( failure ) ); return end
		slqSetStage( runtime, "WAIT_CLIENT_ROUNDTRIP" )
		runtime.deadlineTick = tick + 400
		return
	end

	if runtime.stage == "WAIT_CLIENT_ROUNDTRIP" then
		local probe = g_scrapLabStoragePhase1ClientQualification and g_scrapLabStoragePhase1ClientQualification[runtime.clientToken]
		if not probe or probe.stage ~= "COMPLETE" then return end
		slqRecord( runtime, "normal-client-catalog-roundtrip", probe.valid == true and
			probe.containerCount == SLQ_TOTAL_CHESTS and probe.totalQuantity == SLQ_TOTAL_CHESTS and probe.entryCount == 1,
			"containers=" .. tostring( probe.containerCount ) .. ", quantity=" .. tostring( probe.totalQuantity ) ..
			", entries=" .. tostring( probe.entryCount ) )
		g_scrapLabStoragePhase1ClientQualification[runtime.clientToken] = nil
		slqEndSessions( runtime )
		runtime.idleBefore = slqCaptureTerminalActivity( runtime )
		runtime.idleUntil = tick + SLQ_IDLE_TICKS
		slqSetStage( runtime, "WAIT_IDLE" )
		return
	end

	if runtime.stage == "WAIT_IDLE" then
		if tick < runtime.idleUntil then return end
		local idle = true
		local details = {}
		for index, terminal in ipairs( runtime.terminals ) do
			local publicData = terminal:getInteractable().publicData
			local diagnostics = publicData and publicData.scrapLabStoragePhase1 or nil
			local activity = diagnostics and diagnostics.activitySerial or -1
			local valid = diagnostics and diagnostics.status == "IDLE" and diagnostics.viewers == 0 and
				diagnostics.qualificationLocked ~= true and activity == runtime.idleBefore[index]
			idle = idle and valid
			details[#details + 1] = tostring( index ) .. "=" .. tostring( diagnostics and diagnostics.status ) ..
				"/" .. tostring( diagnostics and diagnostics.viewers ) .. "/work:" ..
				tostring( runtime.idleBefore[index] ) .. "->" .. tostring( activity )
		end
		slqRecord( runtime, "sustained-six-second-idle", idle, table.concat( details, "," ) )
		runtime.workIndex = 1
		slqSetStage( runtime, "CLEAR_ITEMS" )
		return
	end

	if runtime.stage == "CLEAR_ITEMS" then
		local last = math.min( runtime.workIndex + SLQ_WORK_BATCH - 1, #runtime.chests )
		for index = runtime.workIndex, last do
			if not slqClearContainer( slqContainer( runtime.chests[index] ) ) then
				slqRecord( runtime, "cleanup-item-safety", false, "could not empty test chest " .. tostring( index ) )
			end
		end
		runtime.workIndex = last + 1
		if runtime.workIndex > #runtime.chests then
			runtime.destroyList = {}
			for _, shape in ipairs( runtime.chests ) do runtime.destroyList[#runtime.destroyList + 1] = shape end
			for _, shape in ipairs( runtime.terminals ) do runtime.destroyList[#runtime.destroyList + 1] = shape end
			runtime.workIndex = 1
			slqSetStage( runtime, "DESTROY_PARTS" )
		end
		return
	end

	if runtime.stage == "DESTROY_PARTS" then
		local last = math.min( runtime.workIndex + SLQ_WORK_BATCH - 1, #runtime.destroyList )
		for index = runtime.workIndex, last do
			local shape = runtime.destroyList[index]
			if shape and sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
		end
		runtime.workIndex = last + 1
		if runtime.workIndex > #runtime.destroyList then
			runtime.verifyAfter = tick + 40
			slqSetStage( runtime, "VERIFY_CLEANUP" )
		end
		return
	end

	if runtime.stage == "VERIFY_CLEANUP" then
		if tick < runtime.verifyAfter then return end
		local remaining = 0
		for _, shape in ipairs( runtime.destroyList or {} ) do if shape and sm.exists( shape ) then remaining = remaining + 1 end end
		slqRecord( runtime, "verified-502-part-cleanup", remaining == 0, "remaining=" .. tostring( remaining ) )
		slqFinish( self, runtime )
	end
end

local SLQ_PREVIOUS_COMMAND = SurvivalGame.sv_slstorage1Command
function SurvivalGame.sv_slstorage1Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "status" ) )
	if action == "qualify" then self:sv_slstorage1StartQualification( player )
	elseif SLQ_PREVIOUS_COMMAND then SLQ_PREVIOUS_COMMAND( self, data, player ) end
end

local SLQ_ORIGINAL_FIXED_UPDATE = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	if SLQ_ORIGINAL_FIXED_UPDATE then SLQ_ORIGINAL_FIXED_UPDATE( self, timeStep ) end
	local ok, failure = pcall( function() slqProcessQualification( self ) end )
	if not ok then
		local runtime = self.sv and self.sv.scrapLabStoragePhase1QualificationRuntime or nil
		slqLog( "runtime error during " .. tostring( runtime and runtime.stage ) .. ": " .. tostring( failure ) )
		if runtime then slqBeginCleanup( self, runtime, "runtime error during " .. tostring( runtime.stage ) .. ": " .. tostring( failure ) ) end
	end
end

slqLog( "qualification harness ready; use /slstorage1 qualify" )
