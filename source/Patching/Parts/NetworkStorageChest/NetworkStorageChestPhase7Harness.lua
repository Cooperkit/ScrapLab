-- SCRAPLAB NETWORK STORAGE CHEST PHASE 7 RELEASE QUALIFICATION
-- Temporary, self-building coordinator for local, wireless, and 500-container
-- soak tests. The Phase 7 deployer removes this file after qualification.

local SL7_PREFIX = "[ScrapLab Storage Phase 7] "
local SL7_TERMINAL = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL7_CHEST = sm.uuid.new( "4c474cff-3f6a-4306-93d1-c4c74578afd2" )
local SL7_COMPONENT = sm.uuid.new( "5530e6a0-4748-4926-b134-50ca9ecb9dcf" )
local SL7_SPAWN_COUNT = 500
local SL7_SPAWN_BUDGET = 10
local SL7_DESTROY_BUDGET = 30

local function sl7Log( text ) sm.log.info( SL7_PREFIX .. tostring( text ) ) end
local function sl7Message( self, player, text )
	sl7Log( text )
	if player then self.network:sendToClient( player, "cl_slstorage7Message", tostring( text ) ) end
end
local function sl7CountTable( value )
	local count = 0
	for _ in pairs( value or {} ) do count = count + 1 end
	return count
end
local function sl7Container( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end
local function sl7Instance( shape )
	return shape and g_scrapLabNetworkStorageChestInstances and
		g_scrapLabNetworkStorageChestInstances[tostring( shape:getId() )] or nil
end
local function sl7Clear( container )
	if not container or container:isEmpty() then return true end
	if not sm.container.beginTransaction() then return false end
	for slot = 0, container:getSize() - 1 do
		local item = container:getItem( slot )
		if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
			sm.container.spendFromSlot( container, slot, item.uuid, item.quantity, true )
		end
	end
	return sm.container.endTransaction()
end
local function sl7Record( runtime, name, passed, detail, skipped )
	runtime.results[#runtime.results + 1] = {
		name = tostring( name ), passed = passed == true, skipped = skipped == true,
		detail = tostring( detail or "" )
	}
	sl7Log( ( skipped and "SKIP " or ( passed and "PASS " or "FAIL " ) ) ..
		tostring( name ) .. " - " .. tostring( detail ) )
end
local function sl7Summarize( results )
	local passed, failed, skipped = 0, 0, 0
	for _, result in ipairs( results or {} ) do
		if result.skipped then skipped = skipped + 1
		elseif result.passed then passed = passed + 1
		else failed = failed + 1 end
	end
	return passed, failed, skipped
end
local function sl7SaveCleanup( self, runtime )
	local ids = {}
	for _, shape in ipairs( runtime.shapes or {} ) do
		if shape and sm.exists( shape ) then ids[#ids + 1] = shape:getId() end
	end
	self.sv.saved.scrapLabStoragePhase7Cleanup = {
		worldId = tostring( runtime.world.id ), shapeIds = ids
	}
	self.storage:save( self.sv.saved )
end
local function sl7Recover( self, player )
	local cleanup = self.sv.saved.scrapLabStoragePhase7Cleanup
	if not cleanup then return true end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then return false, "a live character is required for recovery" end
	local world = character:getWorld()
	if tostring( world.id ) ~= tostring( cleanup.worldId ) then
		return false, "return to world " .. tostring( cleanup.worldId ) .. " to clean the interrupted fixture"
	end
	local wanted = {}
	for _, id in ipairs( cleanup.shapeIds or {} ) do wanted[tostring( id )] = true end
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if wanted[tostring( shape:getId() )] then
				wanted[tostring( shape:getId() )] = nil
				sl7Clear( sl7Container( shape ) )
				pcall( function() shape:destroyShape( 0 ) end )
			end
		end
	end
	self.sv.saved.scrapLabStoragePhase7Cleanup = nil
	self.storage:save( self.sv.saved )
	return true
end

local SL7_PHASES = {
	phase2 = function( self, player ) self:sv_slstorage2StartAuto( player ) end,
	phase3 = function( self, player ) self:sv_slstorage3Start( player ) end,
	phase4 = function( self, player ) self:sv_slstorage4Start( player ) end,
	phase5 = function( self, player ) self:sv_slstorage5Start( player ) end
}

local function sl7StartSuitePhase( self, runtime )
	local name = runtime.queue[runtime.index]
	if not name then
		g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
		local key = runtime.mode == "wireless" and "phase7wireless" or "phase7local"
		g_scrapLabStorageQualificationResults[key] = {
			complete = true, passed = runtime.passed, failed = runtime.failed,
			skipped = runtime.skipped, results = runtime.results
		}
		local label = string.upper( runtime.mode )
		sl7Message( self, runtime.player, label .. " SUMMARY: " .. runtime.passed ..
			" passed, " .. runtime.failed .. " failed, " .. runtime.skipped .. " skipped." )
		self.sv.scrapLabStoragePhase7Suite = nil
		return
	end
	local starter = SL7_PHASES[name]
	if not starter then
		runtime.failed = runtime.failed + 1
		runtime.results[#runtime.results + 1] = { name = name, passed = false, detail = "starter missing" }
		runtime.index = runtime.index + 1
		sl7StartSuitePhase( self, runtime )
		return
	end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults[name] = nil
	runtime.current = name
	runtime.deadline = sm.game.getCurrentTick() + 1800
	sl7Message( self, runtime.player, "Starting " .. name .. " automatic qualification." )
	starter( self, runtime.player )
end

function SurvivalGame.sv_slstorage7StartSuite( self, player, mode )
	if self.sv.scrapLabStoragePhase7Suite or self.sv.scrapLabStoragePhase7Soak then
		sl7Message( self, player, "A Phase 7 qualification is already running." ); return
	end
	local queues = {
		["local"] = { "phase2", "phase3", "phase5" },
		wireless = { "phase4" },
		all = { "phase2", "phase3", "phase5", "phase4" }
	}
	local queue = queues[mode]
	if not queue then sl7Message( self, player, "Use /slstorage auto local, wireless, or all." ); return end
	local runtime = { player = player, mode = mode, queue = queue, index = 1,
		passed = 0, failed = 0, skipped = 0, results = {} }
	self.sv.scrapLabStoragePhase7Suite = runtime
	sl7StartSuitePhase( self, runtime )
end

local function sl7ProcessSuite( self )
	local runtime = self.sv.scrapLabStoragePhase7Suite
	if not runtime then return end
	if sm.game.getCurrentTick() > runtime.deadline then
		runtime.failed = runtime.failed + 1
		runtime.results[#runtime.results + 1] = {
			name = runtime.current, passed = false, detail = "coordinator timeout"
		}
		runtime.index = runtime.index + 1
		sl7StartSuitePhase( self, runtime )
		return
	end
	local result = g_scrapLabStorageQualificationResults and
		g_scrapLabStorageQualificationResults[runtime.current] or nil
	if not result or result.complete ~= true then return end
	runtime.passed = runtime.passed + ( tonumber( result.passed ) or 0 )
	runtime.failed = runtime.failed + ( tonumber( result.failed ) or 0 )
	runtime.skipped = runtime.skipped + ( tonumber( result.skipped ) or 0 )
	for _, entry in ipairs( result.results or {} ) do
		runtime.results[#runtime.results + 1] = {
			name = runtime.current .. ":" .. tostring( entry.name ),
			passed = entry.passed == true, skipped = entry.skipped == true,
			detail = tostring( entry.detail or "" )
		}
	end
	runtime.index = runtime.index + 1
	sl7StartSuitePhase( self, runtime )
end

local function sl7ChestPosition( runtime, index )
	local zero = index - 1
	local column = zero % 10
	local row = math.floor( zero / 10 ) % 10
	local layer = math.floor( zero / 100 )
	return runtime.origin + runtime.side * ( ( column - 4.5 ) * 4 ) +
		runtime.forward * ( row * 4 ) + sm.vec3.new( 0, 0, layer * 4 )
end
local function sl7Descriptors( shapes )
	local result = {}
	for _, shape in ipairs( shapes or {} ) do
		local container = sl7Container( shape )
		local id = container and g_scrapLabNetworkInventoryIndex.getContainerId( container ) or nil
		if not id then return nil end
		result[#result + 1] = { id = id, shape = shape, container = container }
	end
	table.sort( result, function( left, right ) return left.id < right.id end )
	return result
end
local function sl7Stats()
	return g_scrapLabNetworkInventoryIndex and g_scrapLabNetworkInventoryIndex.getStatistics() or {}
end
local function sl7StartQualification( instance, player, descriptors, runId )
	local ok, started, failure = pcall( function()
		return instance:sv_beginPhase1QualificationSession( player, descriptors, runId )
	end )
	return ok and started == true, failure or started
end
local function sl7SetStage( runtime, stage, timeout )
	runtime.stage = stage
	runtime.stageTick = sm.game.getCurrentTick()
	runtime.deadline = runtime.stageTick + ( timeout or 1200 )
	sl7Log( "soak stage " .. stage )
end
local function sl7BeginCleanup( self, runtime, fatal )
	if fatal then sl7Record( runtime, "automatic-runtime", false, fatal ) end
	for _, instance in ipairs( runtime.instances or {} ) do
		pcall( function() instance:sv_endPhase1HarnessSession( runtime.player ) end )
	end
	-- Only these containers can receive generated qualification items. Avoid a
	-- one-tick walk over all 500 empty fixtures during cleanup.
	for _, shape in ipairs( runtime.terminals or {} ) do sl7Clear( sl7Container( shape ) ) end
	if runtime.chests and runtime.chests[1] then sl7Clear( sl7Container( runtime.chests[1] ) ) end
	runtime.destroyIndex = #runtime.shapes
	sl7SetStage( runtime, "CLEANUP", 1200 )
end
local function sl7FinishSoak( self, runtime )
	self.sv.saved.scrapLabStoragePhase7Cleanup = nil
	self.storage:save( self.sv.saved )
	self.sv.scrapLabStoragePhase7Soak = nil
	local passed, failed, skipped = sl7Summarize( runtime.results )
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase7soak = {
		complete = true, passed = passed, failed = failed, skipped = skipped,
		results = runtime.results
	}
	sl7Message( self, runtime.player, "SOAK SUMMARY: " .. passed .. " passed, " ..
		failed .. " failed, " .. skipped .. " skipped. 502 temporary parts removed." )
end

function SurvivalGame.sv_slstorage7StartSoak( self, player )
	if self.sv.scrapLabStoragePhase7Suite or self.sv.scrapLabStoragePhase7Soak then
		sl7Message( self, player, "A Phase 7 qualification is already running." ); return
	end
	local recovered, failure = sl7Recover( self, player )
	if not recovered then sl7Message( self, player, "SOAK RECOVERY FAILED: " .. tostring( failure ) ); return end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then sl7Message( self, player, "A live character is required." ); return end
	local forward = character:getDirection(); forward.z = 0
	forward = forward:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local side = sm.vec3.new( -forward.y, forward.x, 0 )
	local runtime = {
		player = player, world = character:getWorld(), shapes = {}, chests = {}, terminals = {},
		instances = {}, results = {}, forward = forward, side = side,
		origin = character:getWorldPosition() + forward * 12 + sm.vec3.new( 0, 0, 8 ), spawnIndex = 1
	}
	self.sv.scrapLabStoragePhase7Soak = runtime
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase7soak = { complete = false }
	sl7SetStage( runtime, "SPAWN_TERMINALS", 2400 )
	sl7Message( self, player, "500-container soak fixture is building itself. Do not leave this world until cleanup completes." )
end

local function sl7ProcessSoak( self )
	local r = self.sv.scrapLabStoragePhase7Soak
	if not r then return end
	local tick = sm.game.getCurrentTick()
	if tick > r.deadline and r.stage ~= "CLEANUP" then sl7BeginCleanup( self, r, "timed out during " .. tostring( r.stage ) ); return end
	if r.stage == "SPAWN_TERMINALS" then
		for index = 1, 2 do
			local position = r.origin + r.side * ( index == 1 and -3 or 3 )
			local ok, shape = pcall( sm.shape.createPart, SL7_TERMINAL, position,
				sm.quat.identity(), false, true, r.world )
			if not ok or not shape then sl7BeginCleanup( self, r, "terminal creation failed" ); return end
			r.shapes[#r.shapes + 1] = shape; r.terminals[#r.terminals + 1] = shape
		end
		sl7SaveCleanup( self, r ); sl7SetStage( r, "SPAWN_CHESTS", 2400 ); return
	end
	if r.stage == "SPAWN_CHESTS" then
		local created = 0
		while r.spawnIndex <= SL7_SPAWN_COUNT and created < SL7_SPAWN_BUDGET do
			local ok, shape = pcall( sm.shape.createPart, SL7_CHEST,
				sl7ChestPosition( r, r.spawnIndex ), sm.quat.identity(), false, true, r.world )
			if not ok or not shape then sl7BeginCleanup( self, r, "chest " .. r.spawnIndex .. " creation failed" ); return end
			r.shapes[#r.shapes + 1] = shape; r.chests[#r.chests + 1] = shape
			r.spawnIndex = r.spawnIndex + 1; created = created + 1
		end
		if #r.chests % 50 == 0 then sl7SaveCleanup( self, r ) end
		if r.spawnIndex > SL7_SPAWN_COUNT then sl7SaveCleanup( self, r ); sl7SetStage( r, "WAIT_INSTANCES", 600 ) end
		return
	end
	if r.stage == "WAIT_INSTANCES" then
		r.instances = { sl7Instance( r.terminals[1] ), sl7Instance( r.terminals[2] ) }
		if not r.instances[1] or not r.instances[2] then return end
		r.descriptors = sl7Descriptors( r.chests )
		if not r.descriptors or #r.descriptors ~= SL7_SPAWN_COUNT then sl7BeginCleanup( self, r, "container descriptors unavailable" ); return end
		r.idleStats = sl7Stats(); r.idleActivity = r.instances[1].sv.activitySerial or 0
		sl7SetStage( r, "IDLE", 200 ); r.waitUntil = tick + 80; return
	end
	if r.stage == "IDLE" then
		if tick < r.waitUntil then return end
		local stats = sl7Stats()
		local idle = not r.instances[1].sv.indexing and #r.instances[1].sv.scanQueue == 0 and
			( r.instances[1].sv.activitySerial or 0 ) == r.idleActivity and
			( stats.containerScans or 0 ) == ( r.idleStats.containerScans or 0 )
		sl7Record( r, "closed-terminal-idle-cost", idle,
			"activity=" .. tostring( r.instances[1].sv.activitySerial ) .. ", scans=" .. tostring( stats.containerScans ) )
		local first100 = {}; for index = 1, 100 do first100[index] = r.descriptors[index] end
		r.first100 = first100; r.before = sl7Stats()
		local started, failure = sl7StartQualification( r.instances[1], r.player, first100, "PHASE7:COLD100" )
		if not started then sl7BeginCleanup( self, r, "100-container scan failed to start: " .. tostring( failure ) ); return end
		sl7SetStage( r, "COLD100", 600 ); return
	end
	if r.stage == "COLD100" then
		if r.instances[1].sv.indexing then return end
		local snap = r.instances[1].sv.snapshot or {}
		sl7Record( r, "cold-100-container-index", snap.containerCount == 100 and
			snap.scanContainerScans == 100 and snap.scanCacheHits == 0 and ( snap.scanDurationTicks or 999 ) <= 12,
			"containers=" .. tostring( snap.containerCount ) .. ", scans=" .. tostring( snap.scanContainerScans ) ..
			", hits=" .. tostring( snap.scanCacheHits ) .. ", ticks=" .. tostring( snap.scanDurationTicks ) )
		r.instances[1]:sv_startScan( r.first100, "PHASE7:WARM100" ); sl7SetStage( r, "WARM100", 300 ); return
	end
	if r.stage == "WARM100" then
		if r.instances[1].sv.indexing then return end
		local snap = r.instances[1].sv.snapshot or {}
		sl7Record( r, "warm-cache-100", snap.scanContainerScans == 0 and snap.scanCacheHits == 100,
			"scans=" .. tostring( snap.scanContainerScans ) .. ", hits=" .. tostring( snap.scanCacheHits ) )
		local changed = sl7Container( r.chests[1] )
		if not sm.container.beginTransaction() then sl7BeginCleanup( self, r, "revision transaction unavailable" ); return end
		sm.container.collect( changed, SL7_COMPONENT, 1, true )
		if not sm.container.endTransaction() then sl7BeginCleanup( self, r, "revision transaction failed" ); return end
		r.instances[1]:sv_startScan( r.first100, "PHASE7:REVISION100" ); sl7SetStage( r, "REVISION100", 300 ); return
	end
	if r.stage == "REVISION100" then
		if r.instances[1].sv.indexing then return end
		local snap = r.instances[1].sv.snapshot or {}
		sl7Record( r, "single-revision-rescan", snap.scanContainerScans == 1 and snap.scanCacheHits == 99 and snap.totalQuantity == 1,
			"scans=" .. tostring( snap.scanContainerScans ) .. ", hits=" .. tostring( snap.scanCacheHits ) .. ", quantity=" .. tostring( snap.totalQuantity ) )
		local started, failure = sl7StartQualification( r.instances[1], r.player, r.descriptors, "PHASE7:COLD500" )
		if not started then sl7BeginCleanup( self, r, "500-container scan failed to start: " .. tostring( failure ) ); return end
		sl7SetStage( r, "COLD500", 900 ); return
	end
	if r.stage == "COLD500" then
		if r.instances[1].sv.indexing then return end
		local snap = r.instances[1].sv.snapshot or {}
		sl7Record( r, "incremental-500-container-index", snap.containerCount == 500 and
			snap.scanContainerScans == 400 and snap.scanCacheHits == 100 and ( snap.scanDurationTicks or 999 ) <= 45,
			"containers=" .. tostring( snap.containerCount ) .. ", scans=" .. tostring( snap.scanContainerScans ) ..
			", hits=" .. tostring( snap.scanCacheHits ) .. ", ticks=" .. tostring( snap.scanDurationTicks ) )
		local started, failure = sl7StartQualification( r.instances[2], r.player, r.descriptors, "PHASE7:OVERLAP500" )
		if not started then sl7BeginCleanup( self, r, "overlap scan failed to start: " .. tostring( failure ) ); return end
		sl7SetStage( r, "OVERLAP500", 900 ); return
	end
	if r.stage == "OVERLAP500" then
		if r.instances[2].sv.indexing then return end
		local snap = r.instances[2].sv.snapshot or {}
		sl7Record( r, "shared-cache-overlapping-terminal", snap.scanContainerScans == 0 and snap.scanCacheHits == 500,
			"scans=" .. tostring( snap.scanContainerScans ) .. ", hits=" .. tostring( snap.scanCacheHits ) )
		local buffer = sl7Container( r.terminals[1] )
		if not sm.container.beginTransaction() then sl7BeginCleanup( self, r, "buffer transaction unavailable" ); return end
		sm.container.collect( buffer, SL7_COMPONENT, 3, true )
		if not sm.container.endTransaction() then sl7BeginCleanup( self, r, "buffer transaction failed" ); return end
		local id = tostring( sm.container.getId( buffer ) )
		pcall( function() r.instances[1]:server_onRefresh() end )
		local refreshed = sl7Container( r.terminals[1] )
		local quantity = refreshed and sm.container.totalQuantity( refreshed, SL7_COMPONENT ) or -1
		sl7Record( r, "five-slot-buffer-refresh-persistence", refreshed and refreshed:getSize() == 5 and
			tostring( sm.container.getId( refreshed ) ) == id and quantity == 3,
			"size=" .. tostring( refreshed and refreshed:getSize() ) .. ", quantity=" .. tostring( quantity ) )
		local beforePrune = sl7Stats()
		g_scrapLabNetworkInventoryIndex.prune( tick + 3000, 2400 )
		local afterPrune = sl7Stats()
		sl7Record( r, "bounded-unused-index-cache", ( beforePrune.cachedEntries or 0 ) >= 500 and
			( afterPrune.cachedEntries or 0 ) <= ( beforePrune.cachedEntries or 0 ) - 500,
			"before=" .. tostring( beforePrune.cachedEntries ) .. ", after=" .. tostring( afterPrune.cachedEntries ) )
		sl7BeginCleanup( self, r ); return
	end
	if r.stage == "CLEANUP" then
		local removed = 0
		while r.destroyIndex > 0 and removed < SL7_DESTROY_BUDGET do
			local shape = r.shapes[r.destroyIndex]
			if shape and sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
			r.destroyIndex = r.destroyIndex - 1; removed = removed + 1
		end
		if r.destroyIndex <= 0 then sl7FinishSoak( self, r ) end
	end
end

function SurvivalGame.sv_slstorage7Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "status" ) )
	local mode = string.lower( tostring( data and data.mode or "" ) )
	if action == "auto" then self:sv_slstorage7StartSuite( player, mode == "" and "all" or mode )
	elseif action == "soak" then self:sv_slstorage7StartSoak( player )
	elseif action == "status" then
		local active = self.sv.scrapLabStoragePhase7Suite and "suite" or
			( self.sv.scrapLabStoragePhase7Soak and ( "soak:" .. tostring( self.sv.scrapLabStoragePhase7Soak.stage ) ) or "idle" )
		sl7Message( self, player, "Phase 7 status: " .. active )
	else sl7Message( self, player, "Use /slstorage auto local, /slstorage auto wireless, /slstorage auto all, or /slstorage soak." ) end
end

function SurvivalGame.cl_slstorage7Message( self, message )
	sm.gui.chatMessage( "#55DFFF" .. tostring( message ) )
end

local SL7_SERVER_CREATE = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	SL7_SERVER_CREATE( self )
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	self.sv.scrapLabStoragePhase7Suite = nil
	self.sv.scrapLabStoragePhase7Soak = nil
end
local SL7_FIXED = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	SL7_FIXED( self, timeStep )
	local ok, failure = pcall( function()
		sl7ProcessSuite( self )
		sl7ProcessSoak( self )
	end )
	if not ok then
		sl7Log( "runtime error: " .. tostring( failure ) )
		local runtime = self.sv and self.sv.scrapLabStoragePhase7Soak
		if runtime and runtime.stage ~= "CLEANUP" then sl7BeginCleanup( self, runtime, failure ) end
	end
end
local SL7_CLIENT_CREATE = SurvivalGame.client_onCreate
function SurvivalGame.client_onCreate( self )
	SL7_CLIENT_CREATE( self )
	sm.game.bindChatCommand( "/slstorage", {
		{ "string", "action", true }, { "string", "mode", true }
	}, "cl_onChatCommand", "ScrapLab Network Storage release qualification" )
end
local SL7_CHAT = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slstorage" then
		self.network:sendToServer( "sv_slstorage7Command", {
			action = params[2] or "status", mode = params[3] or ""
		} )
		return
	end
	SL7_CHAT( self, params )
end

sl7Log( "release qualification ready: /slstorage auto all and /slstorage soak" )
