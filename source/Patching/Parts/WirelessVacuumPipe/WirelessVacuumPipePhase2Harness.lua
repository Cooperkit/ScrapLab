-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 2 HARNESS v1
-- Developer-only registration, grouping, handle, reconciliation, and movement checks.

local Phase2PartUuid = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local Phase2SchemaVersion = 1
local Phase2Prefix = "[ScrapLab Pipe Phase 2] "

local function phase2Log( message )
	if sm.log and sm.log.info then sm.log.info( Phase2Prefix .. message ) else print( Phase2Prefix .. message ) end
end

local function phase2Message( self, player, message )
	phase2Log( message )
	if player then self.network:sendToClient( player, "client_showMessage", Phase2Prefix .. message ) end
end

local function phase2IsHost( player )
	local host = sm.player.getHostPlayer()
	return host and player and host.id == player.id
end

local function phase2WorldLabel( world )
	local publicData = world and world.publicData or {}
	if publicData.type == "Overworld" then return "OVERWORLD" end
	if publicData.type == "UndergroundWorld" then return "UNDERGROUND - DEPTH " .. tostring( publicData.depth or "?") end
	if publicData.type == "WarehouseWorld" then return "WAREHOUSE - LEVEL " .. tostring( publicData.level or "?") end
	return string.upper( tostring( publicData.type or ( "WORLD " .. tostring( world and world.id or "?") ) ) )
end

local function phase2FindShapes( world )
	local shapes = {}
	if not world or not sm.exists( world ) then return shapes end
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if tostring( shape:getShapeUuid() ) == tostring( Phase2PartUuid ) then shapes[#shapes + 1] = shape end
		end
	end
	return shapes
end

local function phase2NearestShape( player )
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then return nil end
	local position = character:getWorldPosition()
	local nearest, nearestDistance = nil, math.huge
	for _, shape in ipairs( phase2FindShapes( character:getWorld() ) ) do
		local distance = ( shape.worldPosition - position ):length2()
		if distance < nearestDistance then nearest, nearestDistance = shape, distance end
	end
	return nearest, nearestDistance
end

local function phase2Save( self )
	self.sv.saved.scrapLabPipePhase2 = self.sv.scrapLabPipePhase2
	self.storage:save( self.sv.saved )
end

local function phase2RecordResult( self, name, passed, detail )
	local result = { passed = passed == true, detail = detail, tick = sm.game.getCurrentTick() }
	self.sv.scrapLabPipePhase2.results[name] = result
	phase2Save( self )
	phase2Log( ( passed and "PASS " or "FAIL " ) .. name .. " (" .. detail .. ")" )
	return result
end

function SurvivalGame.sv_slpipe2Spawn( self, player, dynamic )
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then phase2Message( self, player, "A live character is required." ); return end
	local direction = character:getDirection()
	local position = character:getWorldPosition() + direction * 3 + sm.vec3.new( 0, 0, 0.75 )
	local shape = sm.shape.createPart( Phase2PartUuid, position, sm.quat.identity(), dynamic == true, true, character:getWorld() )
	if not shape or not sm.exists( shape ) then phase2Message( self, player, "Could not create the endpoint." ); return end
	shape.color = sm.color.new( "df7f01" )
	self.sv.scrapLabPipePhase2.spawned[tostring( shape.id )] = {
		world = character:getWorld(), worldId = character:getWorld().id, shapeId = shape.id,
		position = shape.worldPosition
	}
	phase2Save( self )
	phase2Message( self, player, ( dynamic and "Dynamic" or "static" ) .. " endpoint created in " .. phase2WorldLabel( character:getWorld() ) .. "." )
end

function SurvivalGame.sv_slpipe2Status( self, player )
	if not g_wirelessPipeManager then phase2Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end
	local snapshot = g_wirelessPipeManager:sv_getDebugSnapshot()
	phase2Message( self, player, "manager endpoints=" .. snapshot.endpoints .. ", live=" .. snapshot.liveEndpoints .. ", cells=" .. snapshot.handles .. "/" .. snapshot.maxHandles .. ", ready=" .. snapshot.readyHandles .. ", reconciling=" .. snapshot.reconciling .. "." )
	local shape, distance = phase2NearestShape( player )
	if not shape then phase2Message( self, player, "No Wireless Vacuum Pipe is loaded in this world." ); return end
	local endpointId = g_wirelessPipeManager:sv_getEndpointIdForShape( shape )
	local status = endpointId and g_wirelessPipeManager:sv_getEndpointStatus( endpointId ) or nil
	if not status then phase2Message( self, player, "Nearest part is waiting to register." ); return end
	phase2Message( self, player, "nearest=" .. endpointId .. ", distance=" .. string.format( "%.1f", math.sqrt( distance ) ) .. ", mode=" .. status.mode .. ", state=" .. status.state .. ", channel=#" .. status.channel:sub( 1, 6 ) .. ", matches=" .. status.matchingCount .. ", world=" .. status.worldLabel .. "." )
end

function SurvivalGame.sv_slpipe2RunChecks( self, player )
	if not g_wirelessPipeManager then phase2Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end
	local ok, errors = g_wirelessPipeManager:sv_validateInvariants()
	local snapshot = g_wirelessPipeManager:sv_getDebugSnapshot()
	phase2RecordResult( self, "manager-invariants", ok, ok and "registry/group/handle invariants hold" or table.concat( errors, "; " ) )
	local handlesPass = snapshot.handles <= snapshot.maxHandles
	phase2RecordResult( self, "bounded-handle-cap", handlesPass, "active cell handles=" .. snapshot.handles .. "/" .. snapshot.maxHandles )

	local duplicateCells = {}
	for endpointId, record in pairs( g_wirelessPipeManager.sv.saved.endpoints ) do
		local key = tostring( record.worldId ) .. ":" .. tostring( record.cellX ) .. ":" .. tostring( record.cellY )
		duplicateCells[key] = ( duplicateCells[key] or 0 ) + 1
		if record.endpointId ~= endpointId then
			ok = false
			errors[#errors + 1] = "record key mismatch " .. tostring( endpointId )
		end
	end
	local sharedCellFound, sharedCellUsesOneHandle = false, true
	for key, count in pairs( duplicateCells ) do
		if count > 1 then
			sharedCellFound = true
			local handle = g_wirelessPipeManager.sv.handles[key]
			if handle and ( handle.refCount or 0 ) < count then sharedCellUsesOneHandle = false end
		end
	end
	phase2RecordResult( self, "cell-handle-sharing", sharedCellUsesOneHandle, sharedCellFound and "multiple endpoints share one keyed cell handle" or "no duplicate endpoint cell yet; invariant path is clean" )
	phase2Message( self, player, "Automatic Phase 2 checks recorded. Use /slpipe2 results for the summary." )
end

function SurvivalGame.sv_slpipe2Track( self, player )
	if not g_wirelessPipeManager then phase2Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end
	local shape = phase2NearestShape( player )
	if not shape then phase2Message( self, player, "Place or spawn an endpoint first." ); return end
	local endpointId = g_wirelessPipeManager:sv_getEndpointIdForShape( shape )
	local record = endpointId and g_wirelessPipeManager.sv.saved.endpoints[endpointId] or nil
	if not record then phase2Message( self, player, "Nearest endpoint has not registered yet." ); return end
	self.sv.scrapLabPipePhase2.tracker = {
		endpointId = endpointId,
		worldId = record.worldId,
		cellX = record.cellX,
		cellY = record.cellY,
		position = sm.vec3.new( record.lastKnownPosition.x, record.lastKnownPosition.y, record.lastKnownPosition.z ),
		bodyId = shape:getBody():getId(),
		armedTick = sm.game.getCurrentTick(),
		positionPassed = false,
		cellPassed = false,
		worldPassed = false
	}
	phase2Save( self )
	phase2Message( self, player, "Tracking nearest endpoint. Move its creation, cross a cell boundary, or take it through an elevator; changes are recorded automatically." )
end

function SurvivalGame.sv_slpipe2UpdateTracker( self )
	local tracker = self.sv.scrapLabPipePhase2.tracker
	if not tracker or not g_wirelessPipeManager then return end
	local record = g_wirelessPipeManager.sv.saved.endpoints[tracker.endpointId]
	if not record then return end
	local changed = false
	if not tracker.positionPassed and ( record.lastKnownPosition - tracker.position ):length2() >= 1 then
		tracker.positionPassed = true
		changed = true
		phase2RecordResult( self, "moving-creation-position", true, "endpoint position refreshed without changing identity" )
	end
	if not tracker.cellPassed and ( record.cellX ~= tracker.cellX or record.cellY ~= tracker.cellY ) then
		tracker.cellPassed = true
		changed = true
		phase2RecordResult( self, "moving-creation-cell", true, "endpoint moved from cell " .. tracker.cellX .. "," .. tracker.cellY .. " to " .. record.cellX .. "," .. record.cellY )
	end
	if not tracker.worldPassed and record.worldId ~= tracker.worldId then
		tracker.worldPassed = true
		changed = true
		phase2RecordResult( self, "elevator-world-change", true, "endpoint retained identity across world " .. tostring( tracker.worldId ) .. " -> " .. tostring( record.worldId ) )
	end
	if changed then phase2Save( self ) end
end

function SurvivalGame.sv_slpipe2Results( self, player )
	local passed, failed = 0, 0
	for name, result in pairs( self.sv.scrapLabPipePhase2.results ) do
		if result.passed then passed = passed + 1 else failed = failed + 1 end
		phase2Message( self, player, ( result.passed and "PASS " or "FAIL " ) .. name .. " - " .. result.detail )
	end
	local tracker = self.sv.scrapLabPipePhase2.tracker
	phase2Message( self, player, "recorded=" .. passed .. " passed, " .. failed .. " failed; movement tracker=" .. tostring( tracker ~= nil ) .. "." )
end

function SurvivalGame.sv_slpipe2Stale( self, player )
	if not g_wirelessPipeManager then phase2Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then phase2Message( self, player, "A live character is required." ); return end
	local endpointId = g_wirelessPipeManager:sv_debugInjectStaleRecord( character:getWorld(), character:getWorldPosition() )
	self.sv.scrapLabPipePhase2.pendingStale = { endpointId = endpointId, deadlineTick = sm.game.getCurrentTick() + 160, player = player }
	phase2Save( self )
	phase2Message( self, player, "Injected one harmless stale registry record in this loaded cell. Reconciliation should remove it in about two seconds." )
end

function SurvivalGame.sv_slpipe2UpdateStale( self )
	local pending = self.sv.scrapLabPipePhase2.pendingStale
	if not pending or not g_wirelessPipeManager then return end
	if not g_wirelessPipeManager.sv.saved.endpoints[pending.endpointId] then
		phase2RecordResult( self, "startup-reconciliation", true, "loaded stale endpoint record was removed only after confirmation timeout" )
		phase2Message( self, pending.player, "PASS startup reconciliation - stale record removed safely." )
		self.sv.scrapLabPipePhase2.pendingStale = nil
		phase2Save( self )
	elseif sm.game.getCurrentTick() >= pending.deadlineTick then
		phase2RecordResult( self, "startup-reconciliation", false, "stale record remained after loaded-cell confirmation timeout" )
		phase2Message( self, pending.player, "FAIL startup reconciliation - stale record was not removed." )
		self.sv.scrapLabPipePhase2.pendingStale = nil
		phase2Save( self )
	end
end

function SurvivalGame.sv_slpipe2Cleanup( self, player )
	local character = player and player:getCharacter() or nil
	local removed = 0
	if character then
		for _, shape in ipairs( phase2FindShapes( character:getWorld() ) ) do
			shape:destroyShape( 0 )
			removed = removed + 1
		end
	end
	self.sv.scrapLabPipePhase2.spawned = {}
	self.sv.scrapLabPipePhase2.tracker = nil
	phase2Save( self )
	phase2Message( self, player, "Removed " .. removed .. " loaded test endpoint(s) from this world. Endpoints in other worlds must be cleaned there." )
end

function SurvivalGame.sv_slpipe2Command( self, params, player )
	local action = string.lower( tostring( params and params.action or "help" ) )
	local mutating = action == "spawn" or action == "track" or action == "stale" or action == "cleanup" or action == "reset"
	if mutating and not phase2IsHost( player ) then phase2Message( self, player, "Only the host may change the Phase 2 harness." ); return end
	if action == "spawn" then
		self:sv_slpipe2Spawn( player, string.lower( tostring( params.option or "static" ) ) == "dynamic" )
	elseif action == "status" then self:sv_slpipe2Status( player )
	elseif action == "run" then self:sv_slpipe2RunChecks( player )
	elseif action == "track" then self:sv_slpipe2Track( player )
	elseif action == "stale" then self:sv_slpipe2Stale( player )
	elseif action == "results" then self:sv_slpipe2Results( player )
	elseif action == "cleanup" then self:sv_slpipe2Cleanup( player )
	elseif action == "reset" then
		self.sv.scrapLabPipePhase2.results = {}
		self.sv.scrapLabPipePhase2.tracker = nil
		phase2Save( self )
		phase2Message( self, player, "Phase 2 results and tracker reset." )
	else
		phase2Message( self, player, "Commands: spawn [static|dynamic], status, run, track, stale, results, cleanup, reset." )
	end
end

local Phase2OriginalServerOnCreate = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	Phase2OriginalServerOnCreate( self )
	local saved = self.sv.saved.scrapLabPipePhase2
	if type( saved ) ~= "table" or saved.schemaVersion ~= Phase2SchemaVersion then
		saved = { schemaVersion = Phase2SchemaVersion, results = {}, spawned = {} }
	end
	saved.results = saved.results or {}
	saved.spawned = saved.spawned or {}
	self.sv.scrapLabPipePhase2 = saved
	self.sv.scrapLabPipePhase2Ticks = 0
	phase2Save( self )
	phase2Log( "harness ready; use /slpipe2 help" )
end

local Phase2OriginalServerFixedUpdate = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	Phase2OriginalServerFixedUpdate( self, timeStep )
	self.sv.scrapLabPipePhase2Ticks = ( self.sv.scrapLabPipePhase2Ticks or 0 ) + 1
	if self.sv.scrapLabPipePhase2Ticks >= 10 then
		self.sv.scrapLabPipePhase2Ticks = 0
		self:sv_slpipe2UpdateTracker()
		self:sv_slpipe2UpdateStale()
	end
end

local Phase2OriginalBindChatCommands = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	Phase2OriginalBindChatCommands( self )
	sm.game.bindChatCommand( "/slpipe2", {
		{ "string", "action", true },
		{ "string", "option", true }
	}, "cl_onChatCommand", "ScrapLab Wireless Vacuum Pipe Phase 2 harness" )
end

local Phase2OriginalClientChatCommand = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slpipe2" then
		self.network:sendToServer( "sv_slpipe2Command", { action = params[2] or "help", option = params[3] } )
		return
	end
	Phase2OriginalClientChatCommand( self, params )
end
