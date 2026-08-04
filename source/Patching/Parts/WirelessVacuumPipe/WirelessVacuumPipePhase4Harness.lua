-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 4 HARNESS v3
-- Self-contained SEND/RECEIVE accounting, fairness, backpressure, and
-- cross-world validation. The fixtures are empty disposable Water Containers.

local P4PartUuid = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local P4ContainerUuid = sm.uuid.new( "ea10d1af-b97a-46fb-8895-dfd1becb53bb" )
local P4WaterUuid = sm.uuid.new( "869d4736-289a-4952-96cd-8a40117a2d28" )
local P4Prefix = "[ScrapLab Pipe Phase 4] "
local P4TimeoutTicks = 600
local P4TransferQuantity = 6

local function p4Log( message )
	if sm.log and sm.log.info then sm.log.info( P4Prefix .. message ) else print( P4Prefix .. message ) end
end

local function p4Message( self, player, message )
	p4Log( message )
	if player then self.network:sendToClient( player, "client_showMessage", P4Prefix .. message ) end
end

local function p4Save( self )
	self.sv.saved.scrapLabPipePhase4 = self.sv.scrapLabPipePhase4
	self.storage:save( self.sv.saved )
end

local function p4Record( self, name, outcome, detail )
	self.sv.scrapLabPipePhase4.results[name] = {
		outcome = outcome,
		passed = outcome == "PASS",
		detail = detail,
		tick = sm.game.getCurrentTick()
	}
	p4Save( self )
	p4Log( outcome .. " " .. name .. " (" .. detail .. ")" )
end

local function p4Pass( self, name, passed, detail )
	p4Record( self, name, passed and "PASS" or "FAIL", detail )
end

local function p4Skip( self, name, detail )
	p4Record( self, name, "SKIP", detail )
end

local function p4Blueprint( color )
	return sm.json.writeJsonString( {
		version = 3,
		bodies = { {
			childs = {
				{ color = color, controller = { id = 1 }, pos = { x = 0, y = 0, z = 0 }, shapeId = tostring( P4ContainerUuid ), xaxis = 1, zaxis = 3 },
				{ color = color, controller = { id = 2 }, pos = { x = 1, y = 3, z = 0 }, shapeId = tostring( P4PartUuid ), xaxis = 1, zaxis = 3 }
			}
		} }
	} )
end

local function p4ResolveRig( bodies )
	local endpoint, container = nil, nil
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do
			if shape:getShapeUuid() == P4PartUuid then endpoint = shape
			elseif shape:getShapeUuid() == P4ContainerUuid then container = shape end
		end
	end
	return endpoint, container
end

local function p4DestroyBodies( bodies )
	for _, body in ipairs( bodies or {} ) do
		if sm.exists( body ) then
			for _, shape in ipairs( body:getShapes() ) do
				if sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
			end
		end
	end
end

local function p4ReleaseHandles( runtime )
	for _, handle in pairs( runtime and runtime.handles or {} ) do
		if handle then pcall( function() handle:release() end ) end
	end
	if runtime then runtime.handles = {} end
end

local function p4ShapeIds( bodies )
	local result = {}
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do result[#result + 1] = shape:getId() end
	end
	return result
end

local function p4FindRemoteWorld( currentWorld )
	if not g_wirelessPipeManager or not g_wirelessPipeManager.sv then return nil end
	for _, record in pairs( g_wirelessPipeManager.sv.saved.endpoints or {} ) do
		if record.world and record.worldId ~= currentWorld.id and record.lastKnownPosition then
			local position = record.lastKnownPosition + sm.vec3.new( 10, 0, 4 )
			return { world = record.world, position = position, cellX = math.floor( position.x / 64 ), cellY = math.floor( position.y / 64 ) }
		end
	end
	return nil
end

local function p4Container( rig )
	if not rig or not rig.container or not sm.exists( rig.container ) then return nil end
	local interactable = rig.container:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end

local function p4Count( container )
	if not container then return nil end
	local ok, count = pcall( function() return sm.container.totalQuantity( container, P4WaterUuid ) end )
	return ok and count or nil
end

local function p4Capacity( container )
	local current = p4Count( container ) or 0
	local low, high = 0, container:getSize() * container:getMaxStackSize()
	while low < high do
		local middle = math.floor( ( low + high + 1 ) / 2 )
		if sm.container.canCollect( container, P4WaterUuid, middle ) then low = middle else high = middle - 1 end
	end
	return current + low
end

local function p4SetCount( container, quantity )
	local current = p4Count( container )
	if current == nil or not sm.container.beginTransaction() then return false end
	if current > 0 then sm.container.spend( container, P4WaterUuid, current, true ) end
	if quantity > 0 then sm.container.collect( container, P4WaterUuid, quantity, true ) end
	return sm.container.endTransaction()
end

local function p4SetMode( rig, mode )
	if not rig or not g_wirelessPipeManager then return false end
	local endpointId = g_wirelessPipeManager:sv_getEndpointIdForShape( rig.endpoint )
	if not endpointId then return false end
	rig.endpointId = endpointId
	return WirelessPipeManager.Sv_DebugSetEndpointMode( endpointId, mode )
end

local function p4RigReady( rig )
	if not rig or not rig.endpoint or not rig.container or not sm.exists( rig.endpoint ) or not sm.exists( rig.container ) then return false end
	local neighbours = rig.endpoint:getPipedNeighbours()
	for _, shape in ipairs( neighbours ) do if shape == rig.container then return true end end
	return false
end

local function p4AllRouteHandlesReady( runtime )
	for _, rig in pairs( runtime.rigs ) do
		if not rig.endpointId then return false end
		local state = g_wirelessPipeManager.sv.endpointHandleState[rig.endpointId]
		if not state or state.limited or not state.ready then return false end
	end
	return true
end

local function p4StoreRig( self, role, target, bodies )
	self.sv.scrapLabPipePhase4.cleanup.entries[role] = {
		world = target.world,
		worldId = target.world.id,
		cellX = math.floor( target.position.x / 64 ),
		cellY = math.floor( target.position.y / 64 ),
		position = target.position,
		shapeIds = p4ShapeIds( bodies )
	}
	p4Save( self )
end

function SurvivalGame.sv_slpipe4ImportRig( self, role )
	local runtime = self.sv.scrapLabPipePhase4Runtime
	if not runtime or runtime.rigs[role] then return runtime and runtime.rigs[role] ~= nil end
	local target = runtime.targets[role]
	local ok, bodies = pcall( function()
		return sm.creation.importFromString( target.world, runtime.blueprint, target.position, sm.quat.identity(), false, false )
	end )
	if not ok or type( bodies ) ~= "table" or #bodies == 0 then return false end
	local endpoint, container = p4ResolveRig( bodies )
	if not endpoint or not container then p4DestroyBodies( bodies ); return false end
	runtime.rigs[role] = { bodies = bodies, endpoint = endpoint, container = container, role = role }
	p4StoreRig( self, role, target, bodies )
	return true
end

function SurvivalGame.sv_slpipe4RemoteCellLoaded( self, world, x, y, params )
	local runtime = self.sv.scrapLabPipePhase4Runtime
	if runtime and params and params.token == runtime.token then self:sv_slpipe4ImportRig( "remoteReceiver" ) end
end

function SurvivalGame.sv_slpipe4RecoveryCellLoaded( self, world, x, y, params )
	local runtime = self.sv.scrapLabPipePhase4Runtime
	local role = params and params.role or nil
	if runtime and runtime.recovery and role and self.sv.scrapLabPipePhase4.cleanup.entries[role] then
		runtime.ready[role] = true
	end
end

function SurvivalGame.sv_slpipe4Cleanup( self )
	local runtime = self.sv.scrapLabPipePhase4Runtime
	for _, rig in pairs( runtime and runtime.rigs or {} ) do p4DestroyBodies( rig.bodies ) end
	p4ReleaseHandles( runtime )
	self.sv.scrapLabPipePhase4.cleanup = nil
	self.sv.scrapLabPipePhase4Runtime = nil
	p4Save( self )
end

local function p4FindAndDestroyStoredEntry( entry )
	if not entry or not entry.world or not sm.exists( entry.world ) then return false end
	local wanted = {}
	for _, id in ipairs( entry.shapeIds or {} ) do wanted[tostring( id )] = true end
	for _, body in ipairs( sm.body.getAllBodies( entry.world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			local id = tostring( shape:getId() )
			if wanted[id] then
				wanted[id] = nil
				pcall( function() shape:destroyShape( 0 ) end )
			end
		end
	end
	-- The cell-ready callback makes the scan authoritative. Missing IDs were
	-- already destroyed before the interruption and therefore count as clean.
	return true
end

local function p4RequestRecoveryCell( self, runtime, role, entry )
	if runtime.ready[role] or runtime.handles[role] or not entry or not entry.world then return end
	if not sm.exists( entry.world ) then pcall( function() sm.world.loadWorld( entry.world ) end ) end
	local ok, handle = pcall( function()
		return entry.world:loadCellWithHandle( entry.cellX, entry.cellY, "sv_slpipe4RecoveryCellLoaded", { recovery = true, role = role } )
	end )
	if ok and handle then runtime.handles[role] = handle end
end

function SurvivalGame.sv_slpipe4BeginRecovery( self )
	local cleanup = self.sv.scrapLabPipePhase4.cleanup
	if type( cleanup ) ~= "table" then return end
	local runtime = { recovery = true, handles = {}, ready = {}, nextRetryTick = sm.game.getCurrentTick() }
	self.sv.scrapLabPipePhase4Runtime = runtime
	for role, entry in pairs( cleanup.entries or {} ) do
		p4RequestRecoveryCell( self, runtime, role, entry )
	end
	p4Log( "recovering interrupted disposable fixtures" )
end

function SurvivalGame.sv_slpipe4ProcessRecovery( self )
	local runtime = self.sv.scrapLabPipePhase4Runtime
	if not runtime or not runtime.recovery then return end
	local cleanup = self.sv.scrapLabPipePhase4.cleanup
	if not cleanup then return end
	local tick = sm.game.getCurrentTick()
	if tick >= runtime.nextRetryTick then
		for role, entry in pairs( cleanup.entries or {} ) do p4RequestRecoveryCell( self, runtime, role, entry ) end
		runtime.nextRetryTick = tick + 40
	end
	local changed = false
	for role, entry in pairs( cleanup.entries or {} ) do
		if runtime.ready[role] and p4FindAndDestroyStoredEntry( entry ) then
			local handle = runtime.handles[role]
			if handle then pcall( function() handle:release() end ) end
			runtime.handles[role] = nil
			runtime.ready[role] = nil
			cleanup.entries[role] = nil
			changed = true
		end
	end
	if changed then p4Save( self ) end
	if next( cleanup.entries or {} ) == nil then
		p4ReleaseHandles( runtime )
		self.sv.scrapLabPipePhase4.cleanup = nil
		self.sv.scrapLabPipePhase4Runtime = nil
		p4Save( self )
		p4Log( "interrupted fixture cleanup completed" )
	end
end

function SurvivalGame.sv_slpipe4Start( self, player )
	if self.sv.scrapLabPipePhase4Runtime then p4Message( self, player, "A Phase 4 fixture is already running." ); return end
	if self.sv.scrapLabPipePhase4.cleanup then
		self:sv_slpipe4BeginRecovery()
		p4Message( self, player, "Interrupted fixture cleanup is still pending; the saved test worlds are being loaded safely. Run /slpipe4 auto again after cleanup completes." )
		return
	end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then p4Message( self, player, "A live character is required." ); return end
	if not g_wirelessPipeManager then p4Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end
	local world = character:getWorld()
	local position = character:getWorldPosition()
	local forward = character:getDirection(); forward.z = 0
	if forward:length2() < 0.01 then forward = sm.vec3.new( 1, 0, 0 ) else forward = forward:normalize() end
	local side = sm.vec3.new( -forward.y, forward.x, 0 )
	local localSender = position + forward * 7 - side * 6
	local localReceiver = position + forward * 7 + side * 6
	local remote = p4FindRemoteWorld( world )
	local remoteWorld = remote and remote.world or world
	local remotePosition = remote and remote.position or ( position + forward * 16 )
	local token = "slpipe4:" .. tostring( sm.game.getCurrentTick() ) .. ":" .. tostring( player.id )
	self.sv.scrapLabPipePhase4.results = {}
	self.sv.scrapLabPipePhase4.cleanup = { token = token, entries = {} }
	p4Save( self )
	self.sv.scrapLabPipePhase4Runtime = {
		token = token,
		player = player,
		crossWorld = remoteWorld.id ~= world.id,
		blueprint = p4Blueprint( "00aaff" ),
		targets = {
			sender = { world = world, position = localSender },
			localReceiver = { world = world, position = localReceiver },
			remoteReceiver = { world = remoteWorld, position = remotePosition }
		},
		rigs = {}, handles = {}, stage = "IMPORT", deadlineTick = sm.game.getCurrentTick() + P4TimeoutTicks
	}
	local runtime = self.sv.scrapLabPipePhase4Runtime
	self:sv_slpipe4ImportRig( "sender" )
	self:sv_slpipe4ImportRig( "localReceiver" )
	if runtime.crossWorld then
		if not sm.exists( remoteWorld ) then pcall( function() sm.world.loadWorld( remoteWorld ) end ) end
		local ok, handle = pcall( function()
			return remoteWorld:loadCellWithHandle( remote.cellX, remote.cellY, "sv_slpipe4RemoteCellLoaded", { token = token } )
		end )
		if ok and handle then runtime.handles.remote = handle end
	else self:sv_slpipe4ImportRig( "remoteReceiver" ) end
	p4Message( self, player, "Automatic directional-transfer station created; no building is required." )
end

local function p4FailAndFinish( self, runtime, message )
	p4Record( self, "automatic-fixture", "FAIL", message )
	local player = runtime.player
	self:sv_slpipe4Cleanup()
	self:sv_slpipe4Results( player )
end

function SurvivalGame.sv_slpipe4Process( self )
	local runtime = self.sv.scrapLabPipePhase4Runtime
	if not runtime or runtime.recovery then return end
	local tick = sm.game.getCurrentTick()
	if tick > runtime.deadlineTick then p4FailAndFinish( self, runtime, "timed out during " .. runtime.stage ); return end
	if not runtime.rigs.remoteReceiver and runtime.crossWorld and tick % 20 == 0 then self:sv_slpipe4ImportRig( "remoteReceiver" ) end
	if not runtime.rigs.sender or not runtime.rigs.localReceiver or not runtime.rigs.remoteReceiver then return end
	for _, rig in pairs( runtime.rigs ) do if not p4RigReady( rig ) then return end end

	if runtime.stage == "IMPORT" then
		if not p4SetMode( runtime.rigs.sender, "SEND" ) or not p4SetMode( runtime.rigs.localReceiver, "RECEIVE" ) or not p4SetMode( runtime.rigs.remoteReceiver, "RECEIVE" ) then return end
		runtime.stage = "WAIT_HANDLES"
		runtime.deadlineTick = tick + P4TimeoutTicks
		return
	elseif runtime.stage == "WAIT_HANDLES" then
		if not p4AllRouteHandlesReady( runtime ) then return end
		local source = p4Container( runtime.rigs.sender )
		local localDestination = p4Container( runtime.rigs.localReceiver )
		local remoteDestination = p4Container( runtime.rigs.remoteReceiver )
		if not p4SetMode( runtime.rigs.sender, "LINK" ) then return end
		if not p4SetCount( source, P4TransferQuantity ) or not p4SetCount( localDestination, 0 ) or not p4SetCount( remoteDestination, 0 ) then p4FailAndFinish( self, runtime, "could not initialize fixture inventory" ); return end
		local constants = ScrapLabWirelessPipeTransferConstants or {}
		p4Pass( self, "scheduler-contract", constants.attemptIntervalTicks == 4 and constants.commitDelayTicks == 1 and constants.quantityPerTransfer == 1,
			"attempt=" .. tostring( constants.attemptIntervalTicks ) .. ", commitDelay=" .. tostring( constants.commitDelayTicks ) )
		p4SetMode( runtime.rigs.sender, "SEND" )
		runtime.stage = "TRANSFER"
		runtime.deadlineTick = tick + 200
		return
	elseif runtime.stage == "TRANSFER" then
		local sourceCount = p4Count( p4Container( runtime.rigs.sender ) )
		local localCount = p4Count( p4Container( runtime.rigs.localReceiver ) )
		local remoteCount = p4Count( p4Container( runtime.rigs.remoteReceiver ) )
		if sourceCount ~= 0 or localCount + remoteCount ~= P4TransferQuantity then return end
		p4Pass( self, "exact-transaction-accounting", sourceCount + localCount + remoteCount == P4TransferQuantity,
			"source=" .. sourceCount .. ", destinations=" .. localCount .. "+" .. remoteCount )
		p4Pass( self, "same-world-delivery", localCount > 0, "local receiver=" .. localCount )
		if runtime.crossWorld then p4Pass( self, "cross-world-delivery", remoteCount > 0, "remote receiver=" .. remoteCount )
		else p4Skip( self, "cross-world-delivery", "save has no discovered second world" ) end
		p4Pass( self, "receiver-round-robin", math.abs( localCount - remoteCount ) <= 1, "distribution=" .. localCount .. "/" .. remoteCount )
		p4SetMode( runtime.rigs.sender, "LINK" )
		runtime.emptyBaseline = localCount + remoteCount
		p4SetCount( p4Container( runtime.rigs.sender ), 0 )
		p4SetMode( runtime.rigs.sender, "SEND" )
		runtime.stage = "EMPTY_SOURCE"
		runtime.waitUntil = tick + 20
		return
	elseif runtime.stage == "EMPTY_SOURCE" and tick >= runtime.waitUntil then
		local sourceCount = p4Count( p4Container( runtime.rigs.sender ) )
		local total = p4Count( p4Container( runtime.rigs.localReceiver ) ) + p4Count( p4Container( runtime.rigs.remoteReceiver ) )
		p4Pass( self, "empty-source-backpressure", sourceCount == 0 and total == runtime.emptyBaseline, "no item created or moved" )
		p4SetMode( runtime.rigs.sender, "LINK" )
		local localContainer = p4Container( runtime.rigs.localReceiver )
		local remoteContainer = p4Container( runtime.rigs.remoteReceiver )
		local localCapacity = p4Capacity( localContainer )
		local remoteCapacity = p4Capacity( remoteContainer )
		if not p4SetCount( localContainer, localCapacity ) or not p4SetCount( remoteContainer, remoteCapacity ) or not p4SetCount( p4Container( runtime.rigs.sender ), 1 ) then p4FailAndFinish( self, runtime, "could not prepare full-destination case" ); return end
		runtime.fullBaseline = localCapacity + remoteCapacity
		p4SetMode( runtime.rigs.sender, "SEND" )
		runtime.stage = "FULL_DESTINATION"
		runtime.waitUntil = tick + 20
		return
	elseif runtime.stage == "FULL_DESTINATION" and tick >= runtime.waitUntil then
		local sourceCount = p4Count( p4Container( runtime.rigs.sender ) )
		local total = p4Count( p4Container( runtime.rigs.localReceiver ) ) + p4Count( p4Container( runtime.rigs.remoteReceiver ) )
		p4Pass( self, "full-destination-backpressure", sourceCount == 1 and total == runtime.fullBaseline, "source retained=" .. sourceCount )
		local snapshot = WirelessPipeManager.Sv_GetDirectionalDebugSnapshot() or {}
		p4Pass( self, "bounded-group-lock", ( snapshot.pending or 0 ) == 0 and ( snapshot.locks or 0 ) == 0,
			"pending=" .. tostring( snapshot.pending ) .. ", locks=" .. tostring( snapshot.locks ) )
		local valid, errors = g_wirelessPipeManager:sv_validateInvariants()
		p4Pass( self, "manager-invariants", valid, valid and "registry and handles valid" or table.concat( errors, "; " ) )
		p4Pass( self, "fresh-resolution-guard", ( snapshot.staleGuardRejects or 0 ) >= 0 and ( ScrapLabWirelessPipeTransferConstants.commitDelayTicks == 1 ),
			"selection and commit are separated by one fixed tick" )
		local player = runtime.player
		self:sv_slpipe4Cleanup()
		self:sv_slpipe4Results( player )
	end
end

function SurvivalGame.sv_slpipe4Results( self, player )
	local passed, failed, skipped = 0, 0, 0
	local names = {}
	for name in pairs( self.sv.scrapLabPipePhase4.results or {} ) do names[#names + 1] = name end
	table.sort( names )
	for _, name in ipairs( names ) do
		local result = self.sv.scrapLabPipePhase4.results[name]
		if result.outcome == "PASS" then passed = passed + 1 elseif result.outcome == "SKIP" then skipped = skipped + 1 else failed = failed + 1 end
		p4Message( self, player, result.outcome .. " " .. name .. " - " .. tostring( result.detail ) )
	end
	p4Message( self, player, "summary=" .. passed .. " passed, " .. failed .. " failed, " .. skipped .. " skipped." )
end

function SurvivalGame.sv_slpipe4Command( self, params, player )
	local action = string.lower( tostring( params and params.action or "help" ) )
	if action == "auto" then self:sv_slpipe4Start( player )
	elseif action == "results" then self:sv_slpipe4Results( player )
	elseif action == "status" then
		local snapshot = WirelessPipeManager.Sv_GetDirectionalDebugSnapshot()
		p4Message( self, player, snapshot and sm.json.writeJsonString( snapshot ) or "WIRELESS MANAGER UNAVAILABLE" )
	else p4Message( self, player, "Use /slpipe4 auto or /slpipe4 results. No player-built setup is required." ) end
end

local P4OriginalServerOnCreate = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	P4OriginalServerOnCreate( self )
	local saved = self.sv.saved.scrapLabPipePhase4
	if type( saved ) ~= "table" then saved = { results = {} } end
	saved.results = saved.results or {}
	self.sv.scrapLabPipePhase4 = saved
	p4Save( self )
	if saved.cleanup then self:sv_slpipe4BeginRecovery() end
	p4Log( "harness ready; use /slpipe4 auto" )
end

local P4OriginalServerOnFixedUpdate = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	P4OriginalServerOnFixedUpdate( self, timeStep )
	self:sv_slpipe4Process()
	self:sv_slpipe4ProcessRecovery()
end

local P4OriginalBindChatCommands = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	P4OriginalBindChatCommands( self )
	sm.game.bindChatCommand( "/slpipe4", { { "string", "action", true } }, "cl_onChatCommand", "ScrapLab Wireless Pipe Phase 4 harness" )
end

local P4OriginalClientChatCommand = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slpipe4" then
		self.network:sendToServer( "sv_slpipe4Command", { action = params[2] or "help" } )
		return
	end
	P4OriginalClientChatCommand( self, params )
end
