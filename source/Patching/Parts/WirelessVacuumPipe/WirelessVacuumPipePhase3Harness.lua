-- SCRAPLAB WIRELESS VACUUM PIPE PHASE 3 HARNESS v4
-- Developer-only Link graph discovery, ordering, fallback, and cross-world checks.

if ScrapLabPipeGraph == nil then
	dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/ScrapLabPipeGraph.lua" )
end

local Phase3PartUuid = sm.uuid.new( "a34d9af0-4ba0-431d-b647-2d5435ecf138" )
local Phase3WaterContainerUuid = sm.uuid.new( "ea10d1af-b97a-46fb-8895-dfd1becb53bb" )
local Phase3SchemaVersion = 2
local Phase3Prefix = "[ScrapLab Pipe Phase 3] "
local Phase3FixtureTimeoutTicks = 400

local function phase3Log( message )
	if sm.log and sm.log.info then sm.log.info( Phase3Prefix .. message ) else print( Phase3Prefix .. message ) end
end

local function phase3Message( self, player, message )
	phase3Log( message )
	if player then self.network:sendToClient( player, "client_showMessage", Phase3Prefix .. message ) end
end

local function phase3WorldId( shape )
	return tostring( shape:getBody():getWorld().id )
end

local function phase3ShapeId( shape )
	return phase3WorldId( shape ) .. ":" .. tostring( shape:getId() )
end

local function phase3ShapeSignature( shapes )
	local ids = {}
	for _, shape in ipairs( shapes ) do ids[#ids + 1] = phase3ShapeId( shape ) end
	return table.concat( ids, "," )
end

local function phase3HasDuplicates( shapes )
	local seen = {}
	for _, shape in ipairs( shapes ) do
		local id = phase3ShapeId( shape )
		if seen[id] then return true end
		seen[id] = true
	end
	return false
end

local function phase3HasNativePrefix( native, virtual )
	if #virtual < #native then return false end
	for index, shape in ipairs( native ) do
		if virtual[index] ~= shape then return false end
	end
	return true
end

local function phase3ContainsAll( available, expected )
	local seen = {}
	for _, shape in ipairs( available ) do seen[phase3ShapeId( shape )] = true end
	for _, shape in ipairs( expected ) do
		if not seen[phase3ShapeId( shape )] then return false end
	end
	return true
end

local function phase3LogLinkedRoots( roots )
	for _, root in ipairs( roots ) do
		local containers = ScrapLabPipeGraph.debugGetPhysicalContainerShapes( root )
		local containerIds = {}
		for _, container in ipairs( containers ) do
			containerIds[#containerIds + 1] = phase3ShapeId( container ) .. "/" .. tostring( container:getShapeUuid() )
		end
		phase3Log( "linked root " .. phase3ShapeId( root ) .. " exposes " .. #containers .. " container(s): " .. table.concat( containerIds, "," ) )
	end
end

local function phase3ResourceConnectionTypes()
	return {
		sm.interactable.connectionType.water,
		sm.interactable.connectionType.gasoline,
		sm.interactable.connectionType.electricity,
		sm.interactable.connectionType.ammo,
		sm.interactable.connectionType.chemical
	}
end

local function phase3OutputTypes( interactable )
	local types = {}
	for _, connectionType in ipairs( phase3ResourceConnectionTypes() ) do
		local ok, matches = pcall( function() return interactable:hasOutputType( connectionType ) end )
		if ok and matches then types[#types + 1] = connectionType end
	end
	return types
end

local function phase3MatchesAnyOutputType( interactable, outputTypes )
	for _, connectionType in ipairs( outputTypes ) do
		local ok, matches = pcall( function() return interactable:hasOutputType( connectionType ) end )
		if ok and matches then return true end
	end
	return false
end

local function phase3ContainerId( container )
	local ok, id = pcall( function() return sm.container.getId( container ) end )
	return ok and tostring( id ) or nil
end

-- Exercises the resource-container query used by Prospector and the shared
-- resource helpers. It is read-only: no inventory count is changed.
local function phase3TestResourceContainerUnion( linkedRoots )
	local resourceShape, outputTypes = nil, nil
	local physicalContainers = {}
	local seenShapes = {}
	for _, root in ipairs( linkedRoots ) do
		for _, shape in ipairs( ScrapLabPipeGraph.debugGetPhysicalContainerShapes( root ) ) do
			local shapeId = phase3ShapeId( shape )
			if not seenShapes[shapeId] then
				seenShapes[shapeId] = true
				physicalContainers[#physicalContainers + 1] = shape
				local interactable = shape:getInteractable()
				local types = interactable and phase3OutputTypes( interactable ) or {}
				if not resourceShape and #types > 0 then
					resourceShape, outputTypes = shape, types
				end
			end
		end
	end
	if not resourceShape then
		return nil, "no connected Water, Gas, Battery, Ammo, or Chemical Container"
	end

	local expected = {}
	for _, shape in ipairs( physicalContainers ) do
		local interactable = shape:getInteractable()
		if interactable and phase3MatchesAnyOutputType( interactable, outputTypes ) then
			local container = interactable:getContainer( 0 )
			local id = container and phase3ContainerId( container ) or nil
			if id then expected[id] = true end
		end
	end

	local actual = ScrapLabPipeGraph.getMatchingPipedContainers( resourceShape:getInteractable() )
	local actualIds, duplicates = {}, false
	for _, container in ipairs( actual ) do
		local id = phase3ContainerId( container )
		if id then
			if actualIds[id] then duplicates = true end
			actualIds[id] = true
		end
	end
	local expectedCount, foundCount = 0, 0
	for id in pairs( expected ) do
		expectedCount = expectedCount + 1
		if actualIds[id] then foundCount = foundCount + 1 end
	end
	return not duplicates and foundCount == expectedCount,
		"resource containers exposed " .. foundCount .. "/" .. expectedCount .. ", returned=" .. #actual
end

local function phase3FindEndpoints( world )
	local shapes = {}
	if not world or not sm.exists( world ) then return shapes end
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if shape:getShapeUuid() == Phase3PartUuid then shapes[#shapes + 1] = shape end
		end
	end
	table.sort( shapes, function( a, b ) return a:getId() < b:getId() end )
	return shapes
end

local function phase3NearestEndpoint( player )
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then return nil end
	local nearest, nearestDistance = nil, math.huge
	for _, shape in ipairs( phase3FindEndpoints( character:getWorld() ) ) do
		local distance = ( shape.worldPosition - character:getWorldPosition() ):length2()
		if distance < nearestDistance then nearest, nearestDistance = shape, distance end
	end
	return nearest, nearestDistance
end

local function phase3Save( self )
	self.sv.saved.scrapLabPipePhase3 = self.sv.scrapLabPipePhase3
	self.storage:save( self.sv.saved )
end

local function phase3RecordOutcome( self, name, outcome, detail )
	self.sv.scrapLabPipePhase3.results[name] = {
		passed = outcome == "PASS",
		outcome = outcome,
		detail = detail,
		tick = sm.game.getCurrentTick()
	}
	phase3Save( self )
	phase3Log( outcome .. " " .. name .. " (" .. detail .. ")" )
end

local function phase3Record( self, name, passed, detail )
	phase3RecordOutcome( self, name, passed and "PASS" or "FAIL", detail )
end

local function phase3Skip( self, name, detail )
	phase3RecordOutcome( self, name, "SKIP", detail )
end

local function phase3FixtureBlueprint( color )
	return sm.json.writeJsonString( {
		version = 3,
		bodies = { {
			childs = {
				{
					color = color,
					controller = { id = 1 },
					pos = { x = 0, y = 0, z = 0 },
					shapeId = tostring( Phase3WaterContainerUuid ),
					xaxis = 1,
					zaxis = 3
				},
				{
					color = color,
					controller = { id = 2 },
					pos = { x = 1, y = 3, z = 0 },
					shapeId = tostring( Phase3PartUuid ),
					xaxis = 1,
					zaxis = 3
				}
			}
		} }
	} )
end

local function phase3FixtureResolveRig( bodies )
	local endpoint, container = nil, nil
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do
			local uuid = shape:getShapeUuid()
			if uuid == Phase3PartUuid then endpoint = shape
			elseif uuid == Phase3WaterContainerUuid then container = shape end
		end
	end
	return endpoint, container
end

local function phase3FixtureShapeIds( bodies )
	local ids = {}
	for _, body in ipairs( bodies or {} ) do
		for _, shape in ipairs( body:getShapes() ) do ids[#ids + 1] = shape:getId() end
	end
	return ids
end

local function phase3FixtureHasPipeConnection( endpoint, container )
	if not endpoint or not container or not sm.exists( endpoint ) or not sm.exists( container ) then return false end
	for _, neighbour in ipairs( endpoint:getPipedNeighbours() ) do
		if neighbour == container then return true end
	end
	return false
end

local function phase3FixtureDestroyBodies( bodies )
	for _, body in ipairs( bodies or {} ) do
		if sm.exists( body ) then
			for _, shape in ipairs( body:getShapes() ) do
				if sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
			end
		end
	end
end

local function phase3FixtureReleaseHandles( runtime )
	for _, handle in pairs( runtime and runtime.handles or {} ) do
		if handle then pcall( function() handle:release() end ) end
	end
	if runtime then runtime.handles = {} end
end

local function phase3FixtureFindRemoteWorld( currentWorld )
	if not g_wirelessPipeManager or not g_wirelessPipeManager.sv or not g_wirelessPipeManager.sv.saved then return nil end
	for _, record in pairs( g_wirelessPipeManager.sv.saved.endpoints or {} ) do
		if record.world and record.worldId ~= currentWorld.id and record.lastKnownPosition then
			local position = record.lastKnownPosition + sm.vec3.new( 5, 0, 2 )
			return {
				world = record.world,
				worldId = record.worldId,
				position = position,
				cellX = math.floor( position.x / 64 ),
				cellY = math.floor( position.y / 64 )
			}
		end
	end
	return nil
end

local function phase3FixtureStoreEntry( self, role, world, position, bodies )
	local cleanup = self.sv.scrapLabPipePhase3.fixtureCleanup
	cleanup.entries[role] = {
		world = world,
		worldId = world.id,
		cellX = math.floor( position.x / 64 ),
		cellY = math.floor( position.y / 64 ),
		position = position,
		shapeIds = phase3FixtureShapeIds( bodies )
	}
	phase3Save( self )
end

function SurvivalGame.sv_slpipe3ImportFixtureRig( self, role )
	local runtime = self.sv.scrapLabPipePhase3Runtime
	if not runtime or not runtime.fixture or runtime.fixture.token ~= self.sv.scrapLabPipePhase3.fixtureCleanup.token then return false end
	if runtime.rigs[role] then return true end
	local target = runtime.targets[role]
	local ok, bodies = pcall( function()
		return sm.creation.importFromString( target.world, runtime.blueprint, target.position, sm.quat.identity(), false, false )
	end )
	if not ok or type( bodies ) ~= "table" or #bodies == 0 then
		runtime.lastImportError = "could not import the disposable " .. role .. " rig"
		return false
	end
	local endpoint, container = phase3FixtureResolveRig( bodies )
	if not endpoint or not container then
		phase3FixtureDestroyBodies( bodies )
		runtime.error = "the disposable " .. role .. " rig is missing its endpoint or Water Container"
		return false
	end
	runtime.rigs[role] = { bodies = bodies, endpoint = endpoint, container = container }
	phase3FixtureStoreEntry( self, role, target.world, target.position, bodies )
	return true
end

function SurvivalGame.sv_slpipe3FixtureCellLoaded( self, world, x, y, params )
	local runtime = self.sv.scrapLabPipePhase3Runtime
	if not runtime or not runtime.fixture or not params or params.token ~= runtime.fixture.token then return end
	self:sv_slpipe3ImportFixtureRig( "remote" )
end

function SurvivalGame.sv_slpipe3CleanupFixture( self )
	local runtime = self.sv.scrapLabPipePhase3Runtime
	for _, rig in pairs( runtime and runtime.rigs or {} ) do phase3FixtureDestroyBodies( rig.bodies ) end
	phase3FixtureReleaseHandles( runtime )
	self.sv.scrapLabPipePhase3.fixtureCleanup = nil
	self.sv.scrapLabPipePhase3Runtime = nil
	phase3Save( self )
end

function SurvivalGame.sv_slpipe3StartAutomaticFixture( self, player )
	if self.sv.scrapLabPipePhase3Runtime and self.sv.scrapLabPipePhase3Runtime.fixture then
		phase3Message( self, player, "An automatic fixture is already running." )
		return
	end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then phase3Message( self, player, "A live character is required." ); return end
	if not g_wirelessPipeManager then phase3Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end

	local currentWorld = character:getWorld()
	local direction = character:getDirection()
	direction.z = 0
	direction = direction:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local side = sm.vec3.new( -direction.y, direction.x, 0 )
	local localPosition = character:getWorldPosition() + direction * 8 + sm.vec3.new( 0, 0, 2 )
	local remoteTarget = phase3FixtureFindRemoteWorld( currentWorld )
	local remotePosition = remoteTarget and remoteTarget.position or ( localPosition + side * 8 )
	local remoteWorld = remoteTarget and remoteTarget.world or currentWorld
	local token = "slpipe3-auto:" .. tostring( sm.game.getCurrentTick() ) .. ":" .. tostring( player.id )
	local color = "7f00ff"

	self.sv.scrapLabPipePhase3.results = {}
	self.sv.scrapLabPipePhase3.fixtureCleanup = {
		token = token,
		color = color,
		entries = {
			localRig = {
				world = currentWorld, worldId = currentWorld.id,
				cellX = math.floor( localPosition.x / 64 ), cellY = math.floor( localPosition.y / 64 ),
				position = localPosition, shapeIds = {}
			},
			remote = {
				world = remoteWorld, worldId = remoteWorld.id,
				cellX = math.floor( remotePosition.x / 64 ), cellY = math.floor( remotePosition.y / 64 ),
				position = remotePosition, shapeIds = {}
			}
		}
	}
	phase3Save( self )
	self.sv.scrapLabPipePhase3Runtime = {
		fixture = { token = token, player = player, crossWorld = remoteWorld.id ~= currentWorld.id },
		blueprint = phase3FixtureBlueprint( color ),
		targets = {
			localRig = { world = currentWorld, position = localPosition },
			remote = { world = remoteWorld, position = remotePosition }
		},
		rigs = {},
		handles = {},
		deadlineTick = sm.game.getCurrentTick() + Phase3FixtureTimeoutTicks
	}
	local runtime = self.sv.scrapLabPipePhase3Runtime
	if not self:sv_slpipe3ImportFixtureRig( "localRig" ) then
		runtime.error = runtime.lastImportError or "could not import the disposable local rig"
		return
	end
	if runtime.fixture.crossWorld then
		if not sm.exists( remoteWorld ) then pcall( function() sm.world.loadWorld( remoteWorld ) end ) end
		local cellX = remoteTarget.cellX or math.floor( remotePosition.x / 64 )
		local cellY = remoteTarget.cellY or math.floor( remotePosition.y / 64 )
		local ok, handle = pcall( function()
			return remoteWorld:loadCellWithHandle( cellX, cellY, "sv_slpipe3FixtureCellLoaded", { token = token } )
		end )
		if ok and handle then runtime.handles.remote = handle else runtime.error = "could not load the saved remote world cell" end
	else
		self:sv_slpipe3ImportFixtureRig( "remote" )
	end
	phase3Message( self, player, "Automatic disposable test station created; no player-built setup is required." )
end

function SurvivalGame.sv_slpipe3ProcessAutomaticFixture( self )
	local runtime = self.sv.scrapLabPipePhase3Runtime
	if not runtime or not runtime.fixture then return end
	local player = runtime.fixture.player
	if runtime.error then
		phase3Record( self, "automatic-fixture", false, runtime.error )
		self:sv_slpipe3CleanupFixture()
		self:sv_slpipe3Results( player )
		return
	end
	if sm.game.getCurrentTick() > runtime.deadlineTick then
		phase3Record( self, "automatic-fixture", false, "timed out while the game initialized the disposable parts" )
		self:sv_slpipe3CleanupFixture()
		self:sv_slpipe3Results( player )
		return
	end
	local localRig, remoteRig = runtime.rigs.localRig, runtime.rigs.remote
	if not remoteRig and runtime.fixture.crossWorld then
		if not runtime.nextRemoteAttemptTick or sm.game.getCurrentTick() >= runtime.nextRemoteAttemptTick then
			runtime.nextRemoteAttemptTick = sm.game.getCurrentTick() + 20
			self:sv_slpipe3ImportFixtureRig( "remote" )
			remoteRig = runtime.rigs.remote
		end
	end
	if not localRig or not remoteRig then return end
	if not phase3FixtureHasPipeConnection( localRig.endpoint, localRig.container )
		or not phase3FixtureHasPipeConnection( remoteRig.endpoint, remoteRig.container ) then return end
	local localId = WirelessPipeManager.Sv_GetEndpointIdForShape( localRig.endpoint )
	local remoteId = WirelessPipeManager.Sv_GetEndpointIdForShape( remoteRig.endpoint )
	if not localId or not remoteId then return end
	local peers = ScrapLabPipeGraph.debugDiscoverRemoteEndpoints( localRig.endpoint )
	if #peers == 0 then return end

	phase3Log( "automatic fixture ready; running read-only graph and resource tests" )
	self:sv_slpipe3Status( player, localRig.endpoint )
	self:sv_slpipe3Run( player, localRig.endpoint, not runtime.fixture.crossWorld )
	self:sv_slpipe3CleanupFixture()
	self:sv_slpipe3Results( player )
end

local function phase3FixtureDestroyStoredShapes( cleanup )
	local remaining = 0
	for _, entry in pairs( cleanup and cleanup.entries or {} ) do
		local wanted = {}
		for _, id in ipairs( entry.shapeIds or {} ) do wanted[tostring( id )] = true end
		if entry.world and sm.exists( entry.world ) then
			for _, body in ipairs( sm.body.getAllBodies( entry.world ) ) do
				for _, shape in ipairs( body:getShapes() ) do
					local id = tostring( shape:getId() )
					local uuid = shape:getShapeUuid()
					local exactId = wanted[id] == true
					local fallbackMatch = false
					if next( wanted ) == nil and entry.position and ( uuid == Phase3PartUuid or uuid == Phase3WaterContainerUuid ) then
						local color = string.lower( tostring( shape.color or "" ) )
						fallbackMatch = color:sub( 1, 6 ) == string.lower( tostring( cleanup.color or "" ) ):sub( 1, 6 )
							and ( shape.worldPosition - entry.position ):length2() < 16
					end
					if exactId or fallbackMatch then
						wanted[id] = nil
						pcall( function() shape:destroyShape( 0 ) end )
					end
				end
			end
		else
			remaining = remaining + math.max( 1, #( entry.shapeIds or {} ) )
		end
		for _ in pairs( wanted ) do remaining = remaining + 1 end
	end
	return remaining
end

function SurvivalGame.sv_slpipe3BeginFixtureRecovery( self )
	local cleanup = self.sv.scrapLabPipePhase3.fixtureCleanup
	if type( cleanup ) ~= "table" then return end
	local runtime = {
		recovery = true,
		handles = {},
		readyTick = sm.game.getCurrentTick() + 40,
		deadlineTick = sm.game.getCurrentTick() + Phase3FixtureTimeoutTicks
	}
	self.sv.scrapLabPipePhase3Runtime = runtime
	for role, entry in pairs( cleanup.entries or {} ) do
		if entry.world then
			if not sm.exists( entry.world ) then pcall( function() sm.world.loadWorld( entry.world ) end ) end
			local ok, handle = pcall( function()
				return entry.world:loadCellWithHandle( entry.cellX, entry.cellY, nil )
			end )
			if ok and handle then runtime.handles[role] = handle end
		end
	end
	phase3Log( "recovering a disposable fixture left by an interrupted automatic test" )
end

function SurvivalGame.sv_slpipe3ProcessFixtureRecovery( self )
	local runtime = self.sv.scrapLabPipePhase3Runtime
	if not runtime or not runtime.recovery then return end
	if sm.game.getCurrentTick() < runtime.readyTick then return end
	local cleanup = self.sv.scrapLabPipePhase3.fixtureCleanup
	if not cleanup then phase3FixtureReleaseHandles( runtime ); self.sv.scrapLabPipePhase3Runtime = nil; return end
	local remaining = phase3FixtureDestroyStoredShapes( cleanup )
	if remaining == 0 then
		phase3FixtureReleaseHandles( runtime )
		self.sv.scrapLabPipePhase3.fixtureCleanup = nil
		self.sv.scrapLabPipePhase3Runtime = nil
		phase3Save( self )
		phase3Log( "interrupted disposable fixture cleanup completed" )
	elseif sm.game.getCurrentTick() > runtime.deadlineTick then
		phase3FixtureReleaseHandles( runtime )
		self.sv.scrapLabPipePhase3Runtime = nil
		phase3Log( "fixture recovery deferred; cleanup record retained for the next load" )
	end
end

function SurvivalGame.sv_slpipe3Status( self, player, endpointOverride )
	local endpoint, distance = endpointOverride, 0
	if not endpoint then endpoint, distance = phase3NearestEndpoint( player ) end
	if not endpoint then phase3Message( self, player, "No loaded Wireless Vacuum Pipe exists in this world." ); return end
	local nativeInputs = sm.pipeGraph.getInputContainers( endpoint )
	local nativeOutputs = sm.pipeGraph.getOutputContainers( endpoint )
	local virtualInputs = ScrapLabPipeGraph.getInputContainers( endpoint )
	local virtualOutputs = ScrapLabPipeGraph.getOutputContainers( endpoint )
	local remote = ScrapLabPipeGraph.debugDiscoverRemoteEndpoints( endpoint )
	local linkedRoots = ScrapLabPipeGraph.debugDiscoverLinkedRoots( endpoint )
	local linkedContainers = ScrapLabPipeGraph.debugGetLinkedContainerShapes( endpoint )
	local crossWorld = 0
	for _, shape in ipairs( remote ) do if phase3WorldId( shape ) ~= phase3WorldId( endpoint ) then crossWorld = crossWorld + 1 end end
	phase3Message( self, player, "nearest=" .. string.format( "%.1fm", math.sqrt( distance ) ) .. ", remote Link endpoints=" .. #remote .. " (cross-world=" .. crossWorld .. ")." )
	phase3Message( self, player, "containers: input " .. #nativeInputs .. " native / " .. #virtualInputs .. " virtual; output " .. #nativeOutputs .. " native / " .. #virtualOutputs .. " virtual." )
	phase3Message( self, player, "conjoined Link bus: " .. #linkedRoots .. " root(s), " .. #linkedContainers .. " registered container(s)." )
	phase3LogLinkedRoots( linkedRoots )
end

function SurvivalGame.sv_slpipe3Run( self, player, endpointOverride, allowCrossWorldSkip )
	local endpoint = endpointOverride or phase3NearestEndpoint( player )
	if not endpoint then phase3Message( self, player, "Place a Wireless Vacuum Pipe in this world first." ); return end
	if not g_wirelessPipeManager then phase3Message( self, player, "WIRELESS MANAGER UNAVAILABLE" ); return end

	local nativeInputs = sm.pipeGraph.getInputContainers( endpoint )
	local nativeOutputs = sm.pipeGraph.getOutputContainers( endpoint )
	local virtualInputs = ScrapLabPipeGraph.getInputContainers( endpoint )
	local virtualOutputs = ScrapLabPipeGraph.getOutputContainers( endpoint )
	local virtualInputsAgain = ScrapLabPipeGraph.getInputContainers( endpoint )
	local virtualOutputsAgain = ScrapLabPipeGraph.getOutputContainers( endpoint )
	local remote = ScrapLabPipeGraph.debugDiscoverRemoteEndpoints( endpoint )
	local remoteAgain = ScrapLabPipeGraph.debugDiscoverRemoteEndpoints( endpoint )
	local linkedInputContainers = ScrapLabPipeGraph.debugGetLinkedContainerShapes( endpoint, "input" )
	local linkedOutputContainers = ScrapLabPipeGraph.debugGetLinkedContainerShapes( endpoint, "output" )
	local originWorld = phase3WorldId( endpoint )
	local crossWorldPeer = nil
	for _, shape in ipairs( remote ) do
		if phase3WorldId( shape ) ~= originWorld then crossWorldPeer = shape; break end
	end

	phase3Record( self, "manager-topology-contract",
		WirelessPipeManager.Sv_GetTopologyRevision() ~= nil,
		"manager exposes an event-driven topology revision" )
	phase3Record( self, "remote-link-discovery", #remote > 0,
		"discovered " .. #remote .. " reachable remote Link endpoint(s)" )
	if crossWorldPeer or not allowCrossWorldSkip then
		phase3Record( self, "cross-world-link-discovery", crossWorldPeer ~= nil,
			crossWorldPeer and ( "reached world " .. phase3WorldId( crossWorldPeer ) ) or "no cross-world peer is currently reachable" )
	else
		phase3Skip( self, "cross-world-link-discovery", "no previously discovered remote world is available in this save" )
	end
	phase3Record( self, "native-results-first",
		phase3HasNativePrefix( nativeInputs, virtualInputs ) and phase3HasNativePrefix( nativeOutputs, virtualOutputs ),
		"vanilla local ordering is preserved before wireless results" )
	phase3Record( self, "deterministic-order",
		phase3ShapeSignature( virtualInputs ) == phase3ShapeSignature( virtualInputsAgain )
			and phase3ShapeSignature( virtualOutputs ) == phase3ShapeSignature( virtualOutputsAgain )
			and phase3ShapeSignature( remote ) == phase3ShapeSignature( remoteAgain ),
		"repeated graph queries returned the same order" )
	phase3Record( self, "cycle-and-duplicate-guards",
		not phase3HasDuplicates( virtualInputs ) and not phase3HasDuplicates( virtualOutputs ) and not phase3HasDuplicates( remote ),
		"no duplicate containers or endpoints escaped traversal guards" )
	local virtualFoundMore = #virtualInputs > #nativeInputs or #virtualOutputs > #nativeOutputs
	phase3Record( self, "remote-container-discovery", virtualFoundMore,
		"input " .. #nativeInputs .. "->" .. #virtualInputs .. ", output " .. #nativeOutputs .. "->" .. #virtualOutputs )
	local conjoined = phase3ContainsAll( virtualInputs, linkedInputContainers )
		and phase3ContainsAll( virtualOutputs, linkedOutputContainers )
	phase3Record( self, "multi-link-container-union", conjoined,
		"input exposes " .. #linkedInputContainers .. "/" .. #virtualInputs .. ", output exposes " .. #linkedOutputContainers .. "/" .. #virtualOutputs .. " linked/native container(s)" )
	local resourcePassed, resourceDetail = phase3TestResourceContainerUnion( ScrapLabPipeGraph.debugDiscoverLinkedRoots( endpoint ) )
	if resourcePassed ~= nil then
		phase3Record( self, "resource-container-union", resourcePassed, resourceDetail )
	else phase3Skip( self, "resource-container-union", resourceDetail ) end
	local visualSafe = true
	if crossWorldPeer then visualSafe = #ScrapLabPipeGraph.getVisualRoute( endpoint, crossWorldPeer, sm.pipeGraph.direction.outgoing ) == 0 end
	phase3Record( self, "cross-world-visual-safety", visualSafe,
		"no impossible direct cross-world pipe route is emitted" )

	local savedManager = g_wirelessPipeManager
	local fallbackInputs, fallbackOutputs
	local fallbackOk = pcall( function()
		g_wirelessPipeManager = nil
		fallbackInputs = ScrapLabPipeGraph.getInputContainers( endpoint )
		fallbackOutputs = ScrapLabPipeGraph.getOutputContainers( endpoint )
	end )
	g_wirelessPipeManager = savedManager
	local fallbackExact = fallbackOk
		and phase3ShapeSignature( nativeInputs ) == phase3ShapeSignature( fallbackInputs )
		and phase3ShapeSignature( nativeOutputs ) == phase3ShapeSignature( fallbackOutputs )
	phase3Record( self, "exact-native-fallback", fallbackExact,
		fallbackExact and "manager-unavailable queries matched vanilla exactly" or "fallback differed from vanilla" )
	phase3Message( self, player, "Phase 3 Link graph checks recorded. Use /slpipe3 results for the full summary." )
end

function SurvivalGame.sv_slpipe3Results( self, player )
	local passed, failed, skipped = 0, 0, 0
	local names = {}
	for name in pairs( self.sv.scrapLabPipePhase3.results ) do names[#names + 1] = name end
	table.sort( names )
	for _, name in ipairs( names ) do
		local result = self.sv.scrapLabPipePhase3.results[name]
		local outcome = result.outcome or ( result.passed and "PASS" or "FAIL" )
		if outcome == "PASS" then passed = passed + 1 elseif outcome == "SKIP" then skipped = skipped + 1 else failed = failed + 1 end
		phase3Message( self, player, outcome .. " " .. name .. " - " .. result.detail )
	end
	phase3Message( self, player, "summary=" .. passed .. " passed, " .. failed .. " failed, " .. skipped .. " skipped." )
end

function SurvivalGame.sv_slpipe3Command( self, params, player )
	local action = string.lower( tostring( params and params.action or "help" ) )
	if action == "status" then self:sv_slpipe3Status( player )
	elseif action == "run" then self:sv_slpipe3Run( player )
	elseif action == "results" then self:sv_slpipe3Results( player )
	elseif action == "auto" then self:sv_slpipe3StartAutomaticFixture( player )
	elseif action == "reset" then
		self.sv.scrapLabPipePhase3.results = {}
		phase3Save( self )
		phase3Message( self, player, "Phase 3 results reset." )
	else phase3Message( self, player, "Commands: auto, status, run, results, reset." ) end
end

local Phase3OriginalServerOnCreate = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	Phase3OriginalServerOnCreate( self )
	local saved = self.sv.saved.scrapLabPipePhase3
	if type( saved ) ~= "table" then saved = { results = {} } end
	saved.schemaVersion = Phase3SchemaVersion
	saved.results = saved.results or {}
	self.sv.scrapLabPipePhase3 = saved
	phase3Save( self )
	if saved.fixtureCleanup then self:sv_slpipe3BeginFixtureRecovery() end
	phase3Log( "harness ready; use /slpipe3 help" )
end

local Phase3OriginalServerOnFixedUpdate = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	Phase3OriginalServerOnFixedUpdate( self, timeStep )
	self:sv_slpipe3ProcessAutomaticFixture()
	self:sv_slpipe3ProcessFixtureRecovery()
end

local Phase3OriginalBindChatCommands = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	Phase3OriginalBindChatCommands( self )
	sm.game.bindChatCommand( "/slpipe3", {
		{ "string", "action", true }
	}, "cl_onChatCommand", "ScrapLab Wireless Vacuum Pipe Phase 3 harness" )
end

local Phase3OriginalClientChatCommand = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slpipe3" then
		self.network:sendToServer( "sv_slpipe3Command", { action = params[2] or "help" } )
		return
	end
	Phase3OriginalClientChatCommand( self, params )
end
