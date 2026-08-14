-- SCRAPLAB NETWORK STORAGE CHEST PHASE 4 AUTOMATIC HARNESS
-- Builds disposable same-world and cross-world wireless storage networks and
-- validates terminal Link, Send, Receive, scope, status, and item movement.

local SL4_TERMINAL = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL4_ENDPOINT = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local SL4_PIPE = sm.uuid.new( "59ea6ce8-239b-4eed-8847-a51b907d9b42" )
local SL4_CONTAINER = sm.uuid.new( "ea10d1af-b97a-46fb-8895-dfd1becb53bb" )
local SL4_WATER = sm.uuid.new( "869d4736-289a-4952-96cd-8a40117a2d28" )
local SL4_PREFIX = "[ScrapLab Storage Phase 4 Auto] "
local SL4_TIMEOUT = 900

local function sl4Log( text ) sm.log.info( SL4_PREFIX .. tostring( text ) ) end
local function sl4Message( self, player, text )
	sl4Log( text )
	if player then self.network:sendToClient( player, "client_showMessage", SL4_PREFIX .. text ) end
end
local function sl4Record( runtime, name, passed, detail, skipped )
	runtime.results[#runtime.results + 1] = { name = name, passed = passed, skipped = skipped == true, detail = tostring( detail ) }
	sl4Log( ( skipped and "SKIP " or ( passed and "PASS " or "FAIL " ) ) .. name .. " - " .. tostring( detail ) )
end
local function sl4Container( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end
local function sl4ContainerId( shape )
	local container = sl4Container( shape )
	return container and tostring( sm.container.getId( container ) ) or nil
end
local function sl4Count( container )
	if not container then return -1 end
	local ok, quantity = pcall( sm.container.totalQuantity, container, SL4_WATER )
	return ok and quantity or -1
end
local function sl4SetCount( container, quantity )
	local current = sl4Count( container )
	if current < 0 or not sm.container.beginTransaction() then return false end
	if current > 0 then sm.container.spend( container, SL4_WATER, current, true ) end
	if quantity > 0 then sm.container.collect( container, SL4_WATER, quantity, true ) end
	return sm.container.endTransaction()
end
local function sl4DestroyBodies( bodies )
	for _, body in ipairs( bodies or {} ) do
		if sm.exists( body ) then
			for _, shape in ipairs( body:getShapes() ) do
				if sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
			end
		end
	end
end
local function sl4Release( runtime )
	for _, handle in pairs( runtime.handles or {} ) do if handle then pcall( function() handle:release() end ) end end
	runtime.handles = {}
end
local function sl4Blueprint( role, color )
	local children
	if role == "origin" then
		children = {
			{ color = color, controller = { id = 1 }, pos = { x = 0, y = 0, z = 0 }, shapeId = tostring( SL4_TERMINAL ), xaxis = 1, zaxis = 3 },
			-- The terminal is 3 x 2. Its +Y opening is centered at x=1.5,
			-- so the 3-wide endpoint begins at x=0 rather than the x=1 used
			-- by the 4-wide Water Container fixture.
			{ color = color, controller = { id = 2 }, pos = { x = 0, y = 2, z = 0 }, shapeId = tostring( SL4_ENDPOINT ), xaxis = 1, zaxis = 3 }
		}
	else
		children = {
			{ color = color, controller = { id = 1 }, pos = { x = 0, y = 0, z = 0 }, shapeId = tostring( SL4_CONTAINER ), xaxis = 1, zaxis = 3 },
			{ color = color, controller = { id = 2 }, pos = { x = 1, y = 3, z = 0 }, shapeId = tostring( SL4_ENDPOINT ), xaxis = 1, zaxis = 3 },
			-- Pipe 1 is one block long: it follows immediately at y=4.
			{ color = color, pos = { x = 1, y = 4, z = 0 }, shapeId = tostring( SL4_PIPE ), xaxis = 1, zaxis = 3 },
			-- Rotating the 4 x 3 container 180 degrees makes its +Y port face
			-- the pipe. Its transformed origin must move by its full x/y size.
			{ color = color, controller = { id = 3 }, pos = { x = 5, y = 8, z = 0 }, shapeId = tostring( SL4_CONTAINER ), xaxis = -1, zaxis = 3 }
		}
	end
	return sm.json.writeJsonString( { version = 3, bodies = { { childs = children } } } )
end
local function sl4ResolveRig( role, bodies )
	local rig = { role = role, bodies = bodies, containers = {} }
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do
			local uuid = shape:getShapeUuid()
			if uuid == SL4_ENDPOINT then rig.endpoint = shape
			elseif uuid == SL4_TERMINAL then rig.terminal = shape
			elseif uuid == SL4_CONTAINER then rig.containers[#rig.containers + 1] = shape end
		end
	end
	table.sort( rig.containers, function( a, b ) return a:getWorldPosition().y < b:getWorldPosition().y end )
	return rig
end
local function sl4FindRemoteWorld( currentWorld )
	if not g_wirelessPipeManager or not g_wirelessPipeManager.sv then return nil end
	for _, record in pairs( g_wirelessPipeManager.sv.saved.endpoints or {} ) do
		if record.world and record.worldId ~= currentWorld.id and record.lastKnownPosition then
			local position = record.lastKnownPosition + sm.vec3.new( 14, 0, 4 )
			return { world = record.world, position = position, cellX = math.floor( position.x / 64 ), cellY = math.floor( position.y / 64 ) }
		end
	end
	return nil
end
local function sl4Import( runtime, role )
	if runtime.rigs[role] then return true end
	local target = runtime.targets[role]
	local blueprint = sl4Blueprint( role == "origin" and "origin" or "remote", runtime.color )
	local ok, bodies = pcall( function()
		return sm.creation.importFromString( target.world, blueprint, target.position, sm.quat.identity(), false, false )
	end )
	if not ok or type( bodies ) ~= "table" or #bodies == 0 then return false end
	local rig = sl4ResolveRig( role, bodies )
	if not rig.endpoint or ( role == "origin" and not rig.terminal ) or ( role ~= "origin" and #rig.containers ~= 2 ) then
		sl4DestroyBodies( bodies ); return false
	end
	runtime.rigs[role] = rig
	return true
end
local function sl4EndpointReady( rig )
	if not rig or not rig.endpoint or not sm.exists( rig.endpoint ) then return false end
	local endpointId = WirelessPipeManager.Sv_GetEndpointIdForShape( rig.endpoint )
	if not endpointId then return false end
	rig.endpointId = endpointId
	local state = g_wirelessPipeManager.sv.endpointHandleState[endpointId]
	return state and state.ready == true and state.limited ~= true
end
local function sl4SetRoute( rig, mode, directOnly )
	return rig and rig.endpointId
		and WirelessPipeManager.Sv_DebugSetEndpointMode( rig.endpointId, mode )
		and WirelessPipeManager.Sv_DebugSetEndpointScope( rig.endpointId, directOnly )
end
local function sl4Ids( descriptors )
	local ids = {}
	for _, descriptor in ipairs( descriptors or {} ) do ids[tostring( descriptor.id )] = descriptor end
	return ids
end
local function sl4Contains( descriptors, shape )
	local id = sl4ContainerId( shape )
	return id and sl4Ids( descriptors )[id] ~= nil
end
local function sl4RemoteCount( runtime )
	local total = 0
	for _, role in ipairs( { "same", "cross" } ) do
		for _, shape in ipairs( runtime.rigs[role].containers ) do total = total + sl4Count( sl4Container( shape ) ) end
	end
	return total
end
local function sl4SetRemoteCounts( runtime, sameDirect, sameIndirect, crossDirect, crossIndirect )
	local counts = { sameDirect, sameIndirect, crossDirect, crossIndirect }
	local index = 1
	for _, role in ipairs( { "same", "cross" } ) do
		for _, shape in ipairs( runtime.rigs[role].containers ) do
			if not sl4SetCount( sl4Container( shape ), counts[index] ) then return false end
			index = index + 1
		end
	end
	return true
end
local function sl4Finish( self, runtime, fatal )
	if fatal then sl4Record( runtime, "automatic-runtime", false, fatal ) end
	if runtime.instance and runtime.player then pcall( function() runtime.instance:sv_endPhase1HarnessSession( runtime.player ) end ) end
	for _, rig in pairs( runtime.rigs or {} ) do
		for _, shape in ipairs( rig.containers or {} ) do sl4SetCount( sl4Container( shape ), 0 ) end
		if rig.terminal then sl4SetCount( sl4Container( rig.terminal ), 0 ) end
		sl4DestroyBodies( rig.bodies )
	end
	sl4Release( runtime )
	self.sv.scrapLabStoragePhase4Runtime = nil
	local passed, failed, skipped = 0, 0, 0
	for _, result in ipairs( runtime.results ) do
		if result.skipped then skipped = skipped + 1 elseif result.passed then passed = passed + 1 else failed = failed + 1 end
		if not result.passed and not result.skipped then sl4Message( self, runtime.player, "FAIL " .. result.name .. " - " .. result.detail ) end
	end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase4 = {
		complete = true, passed = passed, failed = failed, skipped = skipped, results = runtime.results
	}
	sl4Message( self, runtime.player, "PHASE 4 AUTOMATIC TEST COMPLETE: " .. passed .. " passed, " .. failed .. " failed, " .. skipped .. " skipped. Disposable networks removed." )
end

function SurvivalGame.sv_slstorage4RemoteCellLoaded( self, world, x, y, params )
	local runtime = self.sv.scrapLabStoragePhase4Runtime
	if runtime and params and params.token == runtime.token then sl4Import( runtime, "cross" ) end
end

function SurvivalGame.sv_slstorage4Start( self, player )
	if self.sv.scrapLabStoragePhase4Runtime then sl4Message( self, player, "A Phase 4 test is already running." ); return end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase4 = { complete = false }
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then sl4Message( self, player, "A live character is required." ); return end
	if not g_wirelessPipeManager or not ScrapLabPipeGraph or not ScrapLabPipeGraph.getTerminalSpendContainers then
		sl4Message( self, player, "Phase 4 terminal route APIs are unavailable." ); return
	end
	local world = character:getWorld(); local position = character:getWorldPosition()
	local forward = character:getDirection(); forward.z = 0
	if forward:length2() < 0.01 then forward = sm.vec3.new( 1, 0, 0 ) else forward = forward:normalize() end
	local side = sm.vec3.new( -forward.y, forward.x, 0 )
	local remote = sl4FindRemoteWorld( world )
	local crossWorld = remote and remote.world or world
	local crossPosition = remote and remote.position or ( position + forward * 18 )
	local runtime = {
		player = player, world = world, color = "16a6d9", token = "slstorage4:" .. tostring( sm.game.getCurrentTick() ),
		rigs = {}, handles = {}, results = {}, stage = "IMPORT", deadline = sm.game.getCurrentTick() + SL4_TIMEOUT,
		crossWorld = crossWorld.id ~= world.id,
		targets = {
			origin = { world = world, position = position + forward * 7 },
			same = { world = world, position = position + forward * 7 + side * 10 },
			cross = { world = crossWorld, position = crossPosition }
		}
	}
	self.sv.scrapLabStoragePhase4Runtime = runtime
	if not sl4Import( runtime, "origin" ) or not sl4Import( runtime, "same" ) then sl4Finish( self, runtime, "same-world fixture creation failed" ); return end
	if runtime.crossWorld then
		if not sm.exists( crossWorld ) then pcall( function() sm.world.loadWorld( crossWorld ) end ) end
		local ok, handle = pcall( function()
			return crossWorld:loadCellWithHandle( remote.cellX, remote.cellY, "sv_slstorage4RemoteCellLoaded", { token = runtime.token } )
		end )
		if ok and handle then runtime.handles.cross = handle end
	else sl4Import( runtime, "cross" ) end
	sl4Message( self, player, "Phase 4 wireless terminal laboratory created. No building or item placement is required." )
end

local function sl4Query( runtime )
	local spend, spendState = ScrapLabPipeGraph.getTerminalSpendContainers( runtime.rigs.origin.terminal )
	local collect, collectState = ScrapLabPipeGraph.getTerminalCollectContainers( runtime.rigs.origin.terminal )
	return spend or {}, spendState or {}, collect or {}, collectState or {}
end

function SurvivalGame.sv_slstorage4Process( self )
	local r = self.sv.scrapLabStoragePhase4Runtime; if not r then return end
	local tick = sm.game.getCurrentTick()
	if tick > r.deadline then sl4Finish( self, r, "timed out during " .. tostring( r.stage ) ); return end
	if not r.rigs.cross and r.crossWorld and tick % 20 == 0 then sl4Import( r, "cross" ) end
	if not r.rigs.origin or not r.rigs.same or not r.rigs.cross then return end
	for _, role in ipairs( { "origin", "same", "cross" } ) do if not sl4EndpointReady( r.rigs[role] ) then return end end
	if r.stage == "IMPORT" then
		r.instance = g_scrapLabNetworkStorageChestInstances and g_scrapLabNetworkStorageChestInstances[tostring( r.rigs.origin.terminal:getId() )] or nil
		if not r.instance then return end
		local originNeighbours = r.rigs.origin.endpoint:getPipedNeighbours()
		local sameDirect = ScrapLabPipeGraph.getDirectContainerShapes( r.rigs.same.endpoint )
		local sameWhole = ScrapLabPipeGraph.getLocalPhysicalContainerShapes( r.rigs.same.endpoint )
		local connected = false; for _, shape in ipairs( originNeighbours ) do if shape == r.rigs.origin.terminal then connected = true end end
		sl4Record( r, "fixture-terminal-port", connected, "origin terminal and wireless endpoint connected" )
		sl4Record( r, "fixture-direct-versus-whole", #sameDirect == 1 and #sameWhole == 2, "direct=" .. #sameDirect .. ", whole=" .. #sameWhole )
		if not connected or #sameDirect ~= 1 or #sameWhole ~= 2 then sl4Finish( self, r ); return end
		for _, role in ipairs( { "origin", "same", "cross" } ) do sl4SetRoute( r.rigs[role], "LINK", true ) end
		r.stage = "LINK"; r.waitUntil = tick + 12; return
	elseif tick < ( r.waitUntil or 0 ) then return end
	if r.stage == "LINK" then
		local spend, spendState, collect, collectState = sl4Query( r )
		sl4Record( r, "link-spend-union", #spend == 4, "containers=" .. #spend )
		sl4Record( r, "link-collect-union", #collect == 4, "containers=" .. #collect )
		sl4Record( r, "link-ignores-directional-scope", sl4Contains( spend, r.rigs.same.containers[2] ) and sl4Contains( collect, r.rigs.same.containers[2] ), "indirect storage remains reachable" )
		sl4Record( r, "wireless-ready-state", spendState.wirelessState == "READY" and collectState.wirelessState == "READY", tostring( spendState.wirelessState ) .. "/" .. tostring( collectState.wirelessState ) )
		if r.crossWorld then sl4Record( r, "cross-world-link", spendState.crossWorld == true and ( spendState.reachableWorlds or 0 ) >= 2, "worlds=" .. tostring( spendState.reachableWorlds ) )
		else sl4Record( r, "cross-world-link", true, "no saved second world was available", true ) end
		sl4SetRoute( r.rigs.origin, "RECEIVE", true ); sl4SetRoute( r.rigs.same, "SEND", true ); sl4SetRoute( r.rigs.cross, "SEND", true )
		r.stage = "RECEIVE_DIRECT"; r.waitUntil = tick + 12; return
	elseif r.stage == "RECEIVE_DIRECT" then
		local spend, _, collect = sl4Query( r )
		sl4Record( r, "receive-sees-send-sources", #spend == 2, "direct source containers=" .. #spend )
		sl4Record( r, "receive-does-not-export", #collect == 0, "wireless destinations=" .. #collect )
		sl4SetRoute( r.rigs.same, "SEND", false ); sl4SetRoute( r.rigs.cross, "SEND", false )
		r.stage = "RECEIVE_WHOLE"; r.waitUntil = tick + 12; return
	elseif r.stage == "RECEIVE_WHOLE" then
		local spend = sl4Query( r )
		sl4Record( r, "receive-whole-network", #spend == 4, "source containers=" .. #spend )
		sl4SetRoute( r.rigs.origin, "SEND", true ); sl4SetRoute( r.rigs.same, "RECEIVE", true ); sl4SetRoute( r.rigs.cross, "RECEIVE", true )
		r.stage = "SEND_DIRECT"; r.waitUntil = tick + 12; return
	elseif r.stage == "SEND_DIRECT" then
		local spend, _, collect = sl4Query( r )
		sl4Record( r, "send-keeps-local-sources", #spend == 0, "wireless sources=" .. #spend )
		sl4Record( r, "send-sees-receive-destinations", #collect == 2, "direct destinations=" .. #collect )
		sl4SetRoute( r.rigs.same, "RECEIVE", false ); sl4SetRoute( r.rigs.cross, "RECEIVE", false )
		r.stage = "SEND_WHOLE"; r.waitUntil = tick + 12; return
	elseif r.stage == "SEND_WHOLE" then
		local _, _, collect = sl4Query( r )
		sl4Record( r, "send-whole-network", #collect == 4, "destination containers=" .. #collect )
		for _, role in ipairs( { "origin", "same", "cross" } ) do sl4SetRoute( r.rigs[role], "LINK", true ) end
		if not sl4SetRemoteCounts( r, 0, 1, 1, 0 ) then sl4Finish( self, r, "could not initialize transfer inventory" ); return end
		local ok, failure = r.instance:sv_beginPhase1HarnessSession( r.player )
		if not ok then sl4Finish( self, r, failure ); return end
		r.stage = "WITHDRAW_INDEX"; r.waitUntil = tick + 12; return
	elseif r.stage == "WITHDRAW_INDEX" then
		if r.instance.sv.indexing then return end
		local snapshot = r.instance.sv.snapshot or {}
		sl4Record( r, "terminal-wireless-catalog", snapshot.totalQuantity == 2 and snapshot.wirelessState == "READY", "quantity=" .. tostring( snapshot.totalQuantity ) .. ", state=" .. tostring( snapshot.wirelessState ) )
		local buffer = sl4Container( r.rigs.origin.terminal ); local before = sl4RemoteCount( r ) + sl4Count( buffer )
		local success, status, moved = r.instance:sv_executeLocalWithdrawal( SL4_WATER, "TAKE_ALL", buffer )
		local after = sl4RemoteCount( r ) + sl4Count( buffer )
		sl4Record( r, "wireless-withdrawal", success and status == "SUCCESS" and moved == 2 and sl4Count( buffer ) == 2, "status=" .. tostring( status ) .. ", moved=" .. tostring( moved ) )
		sl4Record( r, "withdrawal-conservation", before == after, "before=" .. before .. ", after=" .. after )
		r.stage = "DEPOSIT"; r.waitUntil = tick + 4; return
	elseif r.stage == "DEPOSIT" then
		if r.instance.sv.indexing then return end
		local buffer = sl4Container( r.rigs.origin.terminal ); local before = sl4RemoteCount( r ) + sl4Count( buffer )
		local success, status, moved, remaining = r.instance:sv_routeDepositSlot( buffer, 0 )
		local after = sl4RemoteCount( r ) + sl4Count( buffer )
		sl4Record( r, "wireless-deposit", success and status == "SORTED" and moved == 2 and remaining == 0 and sl4RemoteCount( r ) == 2, "status=" .. tostring( status ) .. ", moved=" .. tostring( moved ) )
		sl4Record( r, "deposit-conservation", before == after, "before=" .. before .. ", after=" .. after )
		local valid, errors = g_wirelessPipeManager:sv_validateInvariants()
		sl4Record( r, "manager-invariants", valid, valid and "registry and handles valid" or table.concat( errors, "; " ) )
		sl4Finish( self, r ); return
	end
end

function SurvivalGame.sv_slstorage4Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "auto" ) )
	if action == "auto" then self:sv_slstorage4Start( player )
	else sl4Message( self, player, "Use /slstorage4 auto. The complete Phase 4 test builds and removes itself." ) end
end

local SL4_BIND = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	if SL4_BIND then SL4_BIND( self ) end
	sm.game.bindChatCommand( "/slstorage4", { { "string", "action", true } }, "cl_onChatCommand", "ScrapLab wireless storage terminal test" )
end
local SL4_CHAT = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slstorage4" then self.network:sendToServer( "sv_slstorage4Command", { action = params[2] or "auto" } ); return end
	if SL4_CHAT then SL4_CHAT( self, params ) end
end
local SL4_FIXED = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, dt )
	if SL4_FIXED then SL4_FIXED( self, dt ) end
	local ok, failure = pcall( function() self:sv_slstorage4Process() end )
	if not ok then local runtime = self.sv and self.sv.scrapLabStoragePhase4Runtime; sl4Log( "runtime error: " .. tostring( failure ) ); if runtime then sl4Finish( self, runtime, failure ) end end
end
sl4Log( "automatic harness ready; use /slstorage4 auto" )
