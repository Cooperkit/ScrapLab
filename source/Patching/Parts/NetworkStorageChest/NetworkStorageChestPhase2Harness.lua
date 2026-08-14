-- SCRAPLAB NETWORK STORAGE CHEST PHASE 2 AUTOMATIC HARNESS
-- Creates disposable engine containers and validates secure local withdrawals.

local SL2_TERMINAL_UUID = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL2_CHEST_UUID = sm.uuid.new( "4c474cff-3f6a-4306-93d1-c4c74578afd2" )
local SL2_WATER_UUID = sm.uuid.new( "869d4736-289a-4952-96cd-8a40117a2d28" )
local SL2_COMPONENT_UUID = sm.uuid.new( "5530e6a0-4748-4926-b134-50ca9ecb9dcf" )
local SL2_CIRCUIT_UUID = sm.uuid.new( "f152e4df-bc40-44fb-8d20-3b3ff70cdfe3" )
local SL2_FERTILIZER_UUID = sm.uuid.new( "ac0b5b0a-14e1-4b31-8944-0a351fbfcc67" )
local SL2_PREFIX = "[ScrapLab Storage Phase 2 Auto] "
local SL2_TIMEOUT_TICKS = 600

local function sl2Log( message ) sm.log.info( SL2_PREFIX .. tostring( message ) ) end

local function sl2Message( self, player, message )
	if self.sv_slstorage1Message then self:sv_slstorage1Message( player, message )
	elseif player then self.network:sendToClient( player, "cl_slstorage1Message", message )
	else sl2Log( message ) end
end

local function sl2Container( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end

local function sl2Instance( shape )
	return shape and g_scrapLabNetworkStorageChestInstances and
		g_scrapLabNetworkStorageChestInstances[tostring( shape:getId() )] or nil
end

local function sl2Count( container, uuid )
	local ok, quantity = pcall( sm.container.totalQuantity, container, uuid )
	return ok and quantity or -1
end

local function sl2Total( runtime, uuid )
	local total = sl2Count( runtime.destination, uuid )
	for _, container in ipairs( runtime.sources or {} ) do total = total + sl2Count( container, uuid ) end
	return total
end

local function sl2Clear( container )
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

local function sl2Populate( assignments )
	if not sm.container.beginTransaction() then return false end
	for _, entry in ipairs( assignments or {} ) do
		sm.container.collectToSlot( entry.container, entry.slot, entry.uuid, entry.quantity, true )
	end
	return sm.container.endTransaction()
end

local function sl2SlotQuantity( container, slot, uuid )
	local item = container and container:getItem( slot ) or nil
	if not item or tostring( item.uuid ) ~= tostring( uuid ) then return 0 end
	return item.quantity or 0
end

local function sl2Record( runtime, name, passed, detail )
	runtime.results[#runtime.results + 1] = { name = name, passed = passed, detail = tostring( detail ) }
	sl2Log( ( passed and "PASS " or "FAIL " ) .. name .. " - " .. tostring( detail ) )
end

local function sl2SetStage( runtime, stage )
	runtime.stage = stage
	runtime.stageStartedTick = sm.game.getCurrentTick()
	runtime.deadlineTick = runtime.stageStartedTick + SL2_TIMEOUT_TICKS
	sl2Log( "stage " .. tostring( stage ) )
end

local function sl2Destroy( runtime )
	if runtime.instance and runtime.player then
		pcall( function() runtime.instance:sv_endPhase1HarnessSession( runtime.player ) end )
	end
	for _, container in ipairs( runtime.sources or {} ) do sl2Clear( container ) end
	sl2Clear( runtime.destination )
	for _, shape in ipairs( runtime.shapes or {} ) do
		if shape and sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
	end
end

local function sl2RecoverInterruptedFixture( self, player, cleanup )
	if not cleanup then return true end
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then return false, "a live character is required for cleanup" end
	local world = character:getWorld()
	if cleanup.worldId and tostring( cleanup.worldId ) ~= tostring( world.id ) then
		return false, "return to world " .. tostring( cleanup.worldId ) .. " to remove the interrupted test station"
	end
	local wanted = {}
	for _, id in ipairs( cleanup.shapeIds or {} ) do wanted[tostring( id )] = true end
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if wanted[tostring( shape:getId() )] then
				wanted[tostring( shape:getId() )] = nil
				sl2Clear( sl2Container( shape ) )
				pcall( function() shape:destroyShape( 0 ) end )
			end
		end
	end
	self.sv.saved.scrapLabStoragePhase2Cleanup = nil
	self.storage:save( self.sv.saved )
	return true
end

local function sl2Finish( self, runtime, fatal )
	if fatal then sl2Record( runtime, "automatic-runtime", false, fatal ) end
	sl2Destroy( runtime )
	self.sv.saved.scrapLabStoragePhase2Cleanup = nil
	self.storage:save( self.sv.saved )
	self.sv.scrapLabStoragePhase2Runtime = nil
	local passed, failed = 0, 0
	for _, result in ipairs( runtime.results ) do
		if result.passed then passed = passed + 1
		else
			failed = failed + 1
			sl2Message( self, runtime.player, "FAIL " .. result.name .. " - " .. result.detail )
		end
	end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase2 = {
		complete = true, passed = passed, failed = failed, skipped = 0, results = runtime.results
	}
	sl2Message( self, runtime.player, "PHASE 2 AUTOMATIC TEST COMPLETE: " .. tostring( passed ) ..
		" passed, " .. tostring( failed ) .. " failed. Disposable station removed." )
end

local function sl2Descriptors( runtime )
	local result = {}
	for index, shape in ipairs( runtime.chests ) do
		local container = sl2Container( shape )
		local id = g_scrapLabNetworkInventoryIndex.getContainerId( container )
		if not id then return nil end
		result[index] = { id = id, shape = shape, container = container }
	end
	table.sort( result, function( a, b ) return a.id < b.id end )
	return result
end

local function sl2SnapshotQuantity( snapshot, uuid )
	for _, entry in ipairs( snapshot and snapshot.entries or {} ) do
		if entry.uuid == tostring( uuid ) then return entry.quantity or 0 end
	end
	return 0
end

local function sl2ResetIndex( runtime, name )
	for _, descriptor in ipairs( runtime.descriptors ) do
		g_scrapLabNetworkInventoryIndex.invalidate( descriptor.id )
	end
	runtime.instance.sv.testHarnessDescriptors = runtime.descriptors
	-- sv_beginPhase1QualificationSession owns the canonical spend+collect route
	-- key. The descriptor set does not change between cases, so replacing that
	-- key with a plain ID list makes the real withdrawal safety check correctly
	-- reject every request as NETWORK_CHANGED.
	runtime.instance.sv.containers = runtime.descriptors
	runtime.instance.sv.records = {}
	runtime.instance:sv_startScan( runtime.descriptors, "PHASE2:" .. name )
end

local function sl2PrepareTest( runtime, test )
	for _, container in ipairs( runtime.sources ) do if not sl2Clear( container ) then return false, "source clear failed" end end
	if not sl2Clear( runtime.destination ) then return false, "destination clear failed" end
	local ok, failure = test.setup( runtime )
	if not ok then return false, failure or "test population failed" end
	runtime.beforeTotal = sl2Total( runtime, test.uuid )
	sl2ResetIndex( runtime, test.name )
	return true
end

local function sl2BuildTests( runtime )
	local componentStack = math.max( 4, sm.item.getStackSize( SL2_COMPONENT_UUID ) )
	local waterStack = math.max( 4, sm.item.getStackSize( SL2_WATER_UUID ) )
	local blockerStack = math.max( 1, sm.item.getStackSize( SL2_FERTILIZER_UUID ) )
	runtime.componentStack, runtime.waterStack, runtime.blockerStack = componentStack, waterStack, blockerStack
	return {
		{
			name = "take-one-smallest-stack-first", uuid = SL2_WATER_UUID, action = "TAKE_ONE",
			setup = function( r ) return sl2Populate( {
				{ container = r.sources[1], slot = 0, uuid = SL2_WATER_UUID, quantity = 4 },
				{ container = r.sources[2], slot = 0, uuid = SL2_WATER_UUID, quantity = 7 }
			} ) end,
			verify = function( r, moved, status )
				return status == "SUCCESS" and moved == 1 and sl2Count( r.destination, SL2_WATER_UUID ) == 1 and
					sl2SlotQuantity( r.sources[1], 0, SL2_WATER_UUID ) == 3,
				"status=" .. status .. ", moved=" .. moved .. ", small-source=" .. sl2SlotQuantity( r.sources[1], 0, SL2_WATER_UUID )
			end
		},
		{
			name = "take-stack-across-containers", uuid = SL2_COMPONENT_UUID, action = "TAKE_STACK",
			setup = function( r ) return sl2Populate( {
				{ container = r.sources[1], slot = 0, uuid = SL2_COMPONENT_UUID, quantity = 2 },
				{ container = r.sources[2], slot = 0, uuid = SL2_COMPONENT_UUID, quantity = r.componentStack - 2 },
				{ container = r.sources[3], slot = 0, uuid = SL2_COMPONENT_UUID, quantity = 3 }
			} ) end,
			verify = function( r, moved, status )
				return status == "SUCCESS" and moved == r.componentStack and
					sl2Count( r.destination, SL2_COMPONENT_UUID ) == r.componentStack,
				"status=" .. status .. ", moved=" .. moved .. "/" .. r.componentStack
			end
		},
		{
			name = "take-all-multi-container", uuid = SL2_CIRCUIT_UUID, action = "TAKE_ALL",
			setup = function( r ) return sl2Populate( {
				{ container = r.sources[1], slot = 0, uuid = SL2_CIRCUIT_UUID, quantity = 2 },
				{ container = r.sources[2], slot = 0, uuid = SL2_CIRCUIT_UUID, quantity = 3 },
				{ container = r.sources[3], slot = 0, uuid = SL2_CIRCUIT_UUID, quantity = 4 }
			} ) end,
			verify = function( r, moved, status )
				return status == "SUCCESS" and moved == 9 and sl2Total( r, SL2_CIRCUIT_UUID ) == 9 and
					sl2Count( r.destination, SL2_CIRCUIT_UUID ) == 9,
				"status=" .. status .. ", moved=" .. moved .. ", destination=" .. sl2Count( r.destination, SL2_CIRCUIT_UUID )
			end
		},
		{
			name = "take-all-capacity-clamp", uuid = SL2_WATER_UUID, action = "TAKE_ALL",
			setup = function( r )
				local entries = { { container = r.sources[1], slot = 0, uuid = SL2_WATER_UUID, quantity = r.waterStack } }
				for slot = 1, 4 do entries[#entries + 1] = { container = r.destination, slot = slot, uuid = SL2_FERTILIZER_UUID, quantity = r.blockerStack } end
				entries[#entries + 1] = { container = r.destination, slot = 0, uuid = SL2_WATER_UUID, quantity = r.waterStack - 2 }
				return sl2Populate( entries )
			end,
			verify = function( r, moved, status )
				return status == "SUCCESS" and moved == 2 and sl2Count( r.destination, SL2_WATER_UUID ) == r.waterStack,
				"status=" .. status .. ", moved=" .. moved .. ", destination=" .. sl2Count( r.destination, SL2_WATER_UUID )
			end
		},
		{
			name = "full-destination-no-spend", uuid = SL2_WATER_UUID, action = "TAKE_ALL",
			setup = function( r )
				local entries = { { container = r.sources[1], slot = 0, uuid = SL2_WATER_UUID, quantity = 3 } }
				for slot = 0, 4 do entries[#entries + 1] = { container = r.destination, slot = slot, uuid = SL2_FERTILIZER_UUID, quantity = r.blockerStack } end
				return sl2Populate( entries )
			end,
			verify = function( r, moved, status )
				return status == "INVENTORY_FULL" and moved == 0 and sl2Count( r.sources[1], SL2_WATER_UUID ) == 3,
				"status=" .. status .. ", moved=" .. moved .. ", source=" .. sl2Count( r.sources[1], SL2_WATER_UUID )
			end
		},
		{
			name = "stale-revision-aborts", uuid = SL2_WATER_UUID, action = "TAKE_ONE", mutate = true,
			setup = function( r ) return sl2Populate( {
				{ container = r.sources[1], slot = 0, uuid = SL2_WATER_UUID, quantity = 4 }
			} ) end,
			verify = function( r, moved, status )
				return status == "NETWORK_CHANGED" and moved == 0 and sl2Count( r.destination, SL2_WATER_UUID ) == 0,
				"status=" .. status .. ", moved=" .. moved .. ", destination=" .. sl2Count( r.destination, SL2_WATER_UUID )
			end
		},
		{
			name = "concurrent-final-item", uuid = SL2_CIRCUIT_UUID, action = "TAKE_ONE", concurrent = true,
			setup = function( r ) return sl2Populate( {
				{ container = r.sources[1], slot = 0, uuid = SL2_CIRCUIT_UUID, quantity = 1 }
			} ) end,
			verify = function( r, moved, status )
				return status == "SUCCESS" and moved == 1 and r.secondMoved == 0 and r.secondStatus == "INDEXING" and
					sl2Count( r.destination, SL2_CIRCUIT_UUID ) == 1,
				"first=" .. status .. "/" .. moved .. ", second=" .. tostring( r.secondStatus ) .. "/" .. tostring( r.secondMoved )
			end
		},
		{
			name = "missing-item-no-spend", uuid = SL2_COMPONENT_UUID, action = "TAKE_ONE",
			setup = function( r ) return sl2Populate( {
				{ container = r.sources[1], slot = 0, uuid = SL2_WATER_UUID, quantity = 2 }
			} ) end,
			verify = function( r, moved, status )
				return status == "ITEM_UNAVAILABLE" and moved == 0 and sl2Count( r.sources[1], SL2_WATER_UUID ) == 2,
				"status=" .. status .. ", moved=" .. moved
			end
		}
	}
end

function SurvivalGame.sv_slstorage2StartAuto( self, player )
	if self.sv.scrapLabStoragePhase2Runtime then sl2Message( self, player, "A Phase 2 test is already running." ); return end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase2 = { complete = false }
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then sl2Message( self, player, "A live character is required." ); return end
	local recovered, recoveryFailure = sl2RecoverInterruptedFixture( self, player,
		self.sv.saved.scrapLabStoragePhase2Cleanup )
	if not recovered then sl2Message( self, player, "PHASE 2 RECOVERY FAILED: " .. tostring( recoveryFailure ) ); return end
	local world = character:getWorld()
	local direction = character:getDirection(); direction.z = 0
	direction = direction:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local lateral = sm.vec3.new( -direction.y, direction.x, 0 )
	local origin = character:getWorldPosition() + direction * 6 + sm.vec3.new( 0, 0, 1.5 )
	local runtime = { player = player, world = world, shapes = {}, chests = {}, sources = {}, results = {} }
	local positions = { origin, origin + lateral * 2, origin - lateral * 2, origin + direction * 2 }
	for index, position in ipairs( positions ) do
		local uuid = index == 1 and SL2_TERMINAL_UUID or SL2_CHEST_UUID
		local ok, shape = pcall( sm.shape.createPart, uuid, position, sm.quat.identity(), false, true, world )
		if not ok or not shape then sl2Destroy( runtime ); sl2Message( self, player, "PHASE 2 TEST FAILED: disposable parts could not be created." ); return end
		runtime.shapes[#runtime.shapes + 1] = shape
		if index == 1 then runtime.terminal = shape else runtime.chests[#runtime.chests + 1] = shape end
	end
	self.sv.saved.scrapLabStoragePhase2Cleanup = { worldId = tostring( world.id ), shapeIds = ( function()
		local ids = {}; for _, shape in ipairs( runtime.shapes ) do ids[#ids + 1] = shape:getId() end; return ids
	end )() }
	self.storage:save( self.sv.saved )
	self.sv.scrapLabStoragePhase2Runtime = runtime
	sl2SetStage( runtime, "WAIT_FIXTURE" )
	sl2Message( self, player, "Phase 2 automatic withdrawal station created. No building or item placement is required." )
end

function SurvivalGame.sv_slstorage2Process( self )
	local runtime = self.sv.scrapLabStoragePhase2Runtime
	if not runtime then return end
	local tick = sm.game.getCurrentTick()
	if tick > runtime.deadlineTick then sl2Finish( self, runtime, "timed out during " .. tostring( runtime.stage ) ); return end

	if runtime.stage == "WAIT_FIXTURE" then
		runtime.instance = sl2Instance( runtime.terminal )
		runtime.destination = sl2Container( runtime.terminal )
		runtime.descriptors = sl2Descriptors( runtime )
		if not runtime.instance or not runtime.destination or not runtime.descriptors then return end
		for _, descriptor in ipairs( runtime.descriptors ) do runtime.sources[#runtime.sources + 1] = descriptor.container end
		local ok, started, failure = pcall( function()
			return runtime.instance:sv_beginPhase1QualificationSession( runtime.player, runtime.descriptors, "PHASE2:BOOT" )
		end )
		if not ok or not started then sl2Finish( self, runtime, "could not start isolated terminal session: " .. tostring( failure or started ) ); return end
		runtime.tests = sl2BuildTests( runtime)
		runtime.testIndex = 1
		sl2SetStage( runtime, "PREPARE_TEST" )
		return
	end

	if runtime.stage == "PREPARE_TEST" then
		local test = runtime.tests[runtime.testIndex]
		if not test then
			local tokenA = runtime.instance:sv_createSession( runtime.player )
			local tokenB = runtime.instance:sv_createSession( runtime.player )
			runtime.instance.sv.viewers[tostring( runtime.player.id )] = runtime.player
			local base = { token = tokenB, action = "TAKE_ONE", uuid = tostring( SL2_WATER_UUID ),
				topologyGeneration = runtime.instance.sv.topologyGeneration,
				contentGeneration = runtime.instance.sv.contentGeneration }
			local _, _, badToken = runtime.instance:sv_validateWithdrawalRequest( {
				token = "wrong", action = base.action, uuid = base.uuid,
				topologyGeneration = base.topologyGeneration, contentGeneration = base.contentGeneration
			}, runtime.player )
			local stale = {}; for key, value in pairs( base ) do stale[key] = value end; stale.contentGeneration = stale.contentGeneration - 1
			local _, _, staleStatus = runtime.instance:sv_validateWithdrawalRequest( stale, runtime.player )
			local _, _, validStatus = runtime.instance:sv_validateWithdrawalRequest( base, runtime.player )
			local _, _, rateStatus = runtime.instance:sv_validateWithdrawalRequest( base, runtime.player )
			sl2Record( runtime, "session-token-rotation", tokenA ~= tokenB, "tokens are unique=" .. tostring( tokenA ~= tokenB ) )
			sl2Record( runtime, "expired-session-rejected", badToken == "SESSION_EXPIRED", "status=" .. tostring( badToken ) )
			sl2Record( runtime, "stale-generation-rejected", staleStatus == "STALE_CATALOG", "status=" .. tostring( staleStatus ) )
			sl2Record( runtime, "request-rate-limited", validStatus == nil and rateStatus == "RATE_LIMITED",
				"first=" .. tostring( validStatus ) .. ", second=" .. tostring( rateStatus ) )
			sl2Finish( self, runtime )
			return
		end
		local ok, failure = sl2PrepareTest( runtime, test )
		if not ok then sl2Finish( self, runtime, test.name .. " setup failed: " .. tostring( failure ) ); return end
		sl2SetStage( runtime, "WAIT_INDEX" )
		return
	end

	if runtime.stage == "WAIT_INDEX" then
		local test = runtime.tests[runtime.testIndex]
		local snapshot = runtime.instance.sv.snapshot
		if runtime.instance.sv.indexing or not snapshot or snapshot.status ~= "READY" or
			snapshot.scanReason ~= "PHASE2:" .. test.name then return end
		if test.mutate then
			if not sm.container.beginTransaction() then sl2Finish( self, runtime, "stale mutation transaction unavailable" ); return end
			sm.container.collect( runtime.sources[1], test.uuid, 1, true )
			if not sm.container.endTransaction() then sl2Finish( self, runtime, "stale mutation failed" ); return end
			runtime.beforeTotal = sl2Total( runtime, test.uuid )
		end
		local _, status, moved = runtime.instance:sv_executeLocalWithdrawal( test.uuid, test.action, runtime.destination )
		runtime.status, runtime.moved = status, moved
		if test.concurrent then
			local _, secondStatus, secondMoved = runtime.instance:sv_executeLocalWithdrawal( test.uuid, test.action, runtime.destination )
			runtime.secondStatus, runtime.secondMoved = secondStatus, secondMoved
		else runtime.secondStatus, runtime.secondMoved = nil, nil end
		sl2SetStage( runtime, "WAIT_RESULT" )
		return
	end

	if runtime.stage == "WAIT_RESULT" then
		if runtime.instance.sv.indexing then return end
		local test = runtime.tests[runtime.testIndex]
		local passed, detail = test.verify( runtime, runtime.moved, runtime.status )
		sl2Record( runtime, test.name, passed, detail )
		local afterTotal = sl2Total( runtime, test.uuid )
		sl2Record( runtime, test.name .. "-conservation", afterTotal == runtime.beforeTotal,
			"before=" .. tostring( runtime.beforeTotal ) .. ", after=" .. tostring( afterTotal ) )
		runtime.testIndex = runtime.testIndex + 1
		sl2SetStage( runtime, "PREPARE_TEST" )
	end
end

function SurvivalGame.sv_slstorage2Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "auto" ) )
	if action == "auto" then self:sv_slstorage2StartAuto( player )
	else sl2Message( self, player, "Command: /slstorage2 auto" ) end
end

local SL2_ORIGINAL_BIND = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	if SL2_ORIGINAL_BIND then SL2_ORIGINAL_BIND( self ) end
	sm.game.bindChatCommand( "/slstorage2", { { "string", "action", true } },
		"cl_onChatCommand", "ScrapLab Network Storage Chest Phase 2 automatic test" )
end

local SL2_ORIGINAL_CHAT = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slstorage2" then
		self.network:sendToServer( "sv_slstorage2Command", { action = params[2] or "auto" } )
		return
	end
	if SL2_ORIGINAL_CHAT then SL2_ORIGINAL_CHAT( self, params ) end
end

local SL2_ORIGINAL_FIXED_UPDATE = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	if SL2_ORIGINAL_FIXED_UPDATE then SL2_ORIGINAL_FIXED_UPDATE( self, timeStep ) end
	local ok, failure = pcall( function() self:sv_slstorage2Process() end )
	if not ok then
		local runtime = self.sv and self.sv.scrapLabStoragePhase2Runtime or nil
		sl2Log( "runtime error during " .. tostring( runtime and runtime.stage ) .. ": " .. tostring( failure ) )
		if runtime then sl2Finish( self, runtime, "runtime error during " .. tostring( runtime.stage ) .. ": " .. tostring( failure ) ) end
	end
end

sl2Log( "automatic harness ready; use /slstorage2 auto" )
