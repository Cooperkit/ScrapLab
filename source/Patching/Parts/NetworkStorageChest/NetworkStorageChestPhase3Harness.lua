-- SCRAPLAB NETWORK STORAGE CHEST PHASE 3 AUTOMATIC HARNESS
-- Validates local deposit ranking, splitting, leftovers, conflict safety, and cleanup.

local SL3_TERMINAL = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL3_CHEST = sm.uuid.new( "4c474cff-3f6a-4306-93d1-c4c74578afd2" )
local SL3_WATER_CONTAINER = sm.uuid.new( "ea10d1af-b97a-46fb-8895-dfd1becb53bb" )
local SL3_WATER = sm.uuid.new( "869d4736-289a-4952-96cd-8a40117a2d28" )
local SL3_COMPONENT = sm.uuid.new( "5530e6a0-4748-4926-b134-50ca9ecb9dcf" )
local SL3_CIRCUIT = sm.uuid.new( "f152e4df-bc40-44fb-8d20-3b3ff70cdfe3" )
local SL3_BLOCKER = sm.uuid.new( "ac0b5b0a-14e1-4b31-8944-0a351fbfcc67" )
local SL3_PREFIX = "[ScrapLab Storage Phase 3 Auto] "

local function sl3Log( text ) sm.log.info( SL3_PREFIX .. tostring( text ) ) end
local function sl3Message( self, player, text )
	if self.sv_slstorage1Message then self:sv_slstorage1Message( player, text )
	elseif player then self.network:sendToClient( player, "cl_slstorage1Message", text ) end
end
local function sl3Container( shape )
	if not shape or not sm.exists( shape ) then return nil end
	local interactable = shape:getInteractable()
	return interactable and interactable:getContainer( 0 ) or nil
end
local function sl3Clear( container )
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
local function sl3Fill( entries )
	for _, entry in ipairs( entries or {} ) do
		local ok, size = pcall( function() return entry.container:getSize() end )
		if not ok or type( size ) ~= "number" then return false, "fixture entry is not an engine container" end
	end
	if not sm.container.beginTransaction() then return false end
	local ok, failure = pcall( function()
		for _, entry in ipairs( entries or {} ) do
			sm.container.collectToSlot( entry.container, entry.slot, entry.uuid, entry.quantity, true )
		end
	end )
	if not ok then pcall( sm.container.abortTransaction ); return false, tostring( failure ) end
	return sm.container.endTransaction()
end
local function sl3Count( container, uuid )
	local ok, count = pcall( sm.container.totalQuantity, container, uuid )
	return ok and count or -1
end
local function sl3Total( runtime, uuid )
	local total = sl3Count( runtime.buffer, uuid )
	for _, container in ipairs( runtime.destinations ) do total = total + sl3Count( container, uuid ) end
	return total
end
local function sl3Record( runtime, name, passed, detail )
	runtime.results[#runtime.results + 1] = { name = name, passed = passed, detail = tostring( detail ) }
	sl3Log( ( passed and "PASS " or "FAIL " ) .. name .. " - " .. tostring( detail ) )
end
local function sl3Destroy( runtime )
	if runtime.instance and runtime.player then pcall( function() runtime.instance:sv_endPhase1HarnessSession( runtime.player ) end ) end
	for _, container in ipairs( runtime.destinations or {} ) do sl3Clear( container ) end
	sl3Clear( runtime.buffer )
	for _, shape in ipairs( runtime.shapes or {} ) do
		if shape and sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
	end
end
local function sl3Finish( self, runtime, fatal )
	if fatal then sl3Record( runtime, "automatic-runtime", false, fatal ) end
	sl3Destroy( runtime )
	self.sv.saved.scrapLabStoragePhase3Cleanup = nil; self.storage:save( self.sv.saved )
	self.sv.scrapLabStoragePhase3Runtime = nil
	local passed, failed = 0, 0
	for _, result in ipairs( runtime.results ) do
		if result.passed then passed = passed + 1 else failed = failed + 1; sl3Message( self, runtime.player, "FAIL " .. result.name .. " - " .. result.detail ) end
	end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase3 = {
		complete = true, passed = passed, failed = failed, skipped = 0, results = runtime.results
	}
	sl3Message( self, runtime.player, "PHASE 3 AUTOMATIC TEST COMPLETE: " .. passed .. " passed, " .. failed .. " failed. Disposable station removed." )
end
local function sl3Descriptors( runtime )
	local result = {}
	for index, shape in ipairs( runtime.destinationShapes ) do
		local container = sl3Container( shape )
		local id = container and g_scrapLabNetworkInventoryIndex.getContainerId( container ) or nil
		if not id then return nil end
		result[index] = { id = id, shape = shape, container = container }
	end
	table.sort( result, function( a, b ) return a.id < b.id end )
	return result
end
local function sl3FullBlockers( container, startSlot, endSlot, blockerStack, entries )
	for slot = startSlot, endSlot do entries[#entries + 1] = { container = container, slot = slot, uuid = SL3_BLOCKER, quantity = blockerStack } end
end
local function sl3Tests( runtime )
	local componentStack = sm.item.getStackSize( SL3_COMPONENT )
	local circuitStack = sm.item.getStackSize( SL3_CIRCUIT )
	local blockerStack = sm.item.getStackSize( SL3_BLOCKER )
	return {
		{ name = "specialized-container-first", uuid = SL3_WATER, setup = function( r ) return sl3Fill( {
			{ container = r.buffer, slot = 0, uuid = SL3_WATER, quantity = 5 },
			{ container = r.chests[1], slot = 0, uuid = SL3_WATER, quantity = 3 }
		} ) end, verify = function( r, status, moved, remaining )
			return status == "SORTED" and moved == 5 and remaining == 0 and sl3Count( r.waterContainer, SL3_WATER ) == 5 and sl3Count( r.chests[1], SL3_WATER ) == 3,
				"status=" .. status .. ", specialized=" .. sl3Count( r.waterContainer, SL3_WATER )
		end },
		{ name = "fullest-partial-stack-first", uuid = SL3_COMPONENT, setup = function( r ) return sl3Fill( {
			{ container = r.buffer, slot = 0, uuid = SL3_COMPONENT, quantity = 3 },
			{ container = r.chests[1], slot = 0, uuid = SL3_COMPONENT, quantity = 2 },
			{ container = r.chests[2], slot = 0, uuid = SL3_COMPONENT, quantity = 5 }
		} ) end, verify = function( r, status, moved )
			return status == "SORTED" and moved == 3 and sl3Count( r.chests[2], SL3_COMPONENT ) == 8,
				"status=" .. status .. ", fullest=" .. sl3Count( r.chests[2], SL3_COMPONENT )
		end },
		{ name = "same-item-before-empty", uuid = SL3_CIRCUIT, setup = function( r ) return sl3Fill( {
			{ container = r.buffer, slot = 0, uuid = SL3_CIRCUIT, quantity = 1 },
			{ container = r.chests[1], slot = 0, uuid = SL3_CIRCUIT, quantity = circuitStack }
		} ) end, verify = function( r, status )
			return status == "SORTED" and sl3Count( r.chests[1], SL3_CIRCUIT ) == circuitStack + 1,
				"same-item quantity=" .. sl3Count( r.chests[1], SL3_CIRCUIT )
		end },
		{ name = "split-allocation", uuid = SL3_COMPONENT, setup = function( r )
			local entries = { { container = r.buffer, slot = 0, uuid = SL3_COMPONENT, quantity = 5 },
				{ container = r.chests[1], slot = 0, uuid = SL3_COMPONENT, quantity = componentStack - 2 },
				{ container = r.chests[2], slot = 0, uuid = SL3_COMPONENT, quantity = componentStack - 3 } }
			sl3FullBlockers( r.chests[1], 1, 9, blockerStack, entries ); sl3FullBlockers( r.chests[2], 1, 9, blockerStack, entries )
			sl3FullBlockers( r.chests[3], 0, 9, blockerStack, entries ); return sl3Fill( entries )
		end, verify = function( r, status, moved, remaining, destinations )
			return status == "SORTED" and moved == 5 and remaining == 0 and destinations == 2,
				"status=" .. status .. ", moved=" .. moved .. ", destinations=" .. destinations
		end },
		{ name = "partial-capacity-left-safe", uuid = SL3_COMPONENT, setup = function( r )
			local entries = { { container = r.buffer, slot = 0, uuid = SL3_COMPONENT, quantity = 5 },
				{ container = r.chests[1], slot = 0, uuid = SL3_COMPONENT, quantity = componentStack - 2 } }
			sl3FullBlockers( r.chests[1], 1, 9, blockerStack, entries ); sl3FullBlockers( r.chests[2], 0, 9, blockerStack, entries ); sl3FullBlockers( r.chests[3], 0, 9, blockerStack, entries ); return sl3Fill( entries )
		end, verify = function( r, status, moved, remaining )
			return status == "PARTIAL" and moved == 2 and remaining == 3 and sl3Count( r.buffer, SL3_COMPONENT ) == 3,
				"status=" .. status .. ", moved=" .. moved .. ", kept=" .. sl3Count( r.buffer, SL3_COMPONENT )
		end },
		{ name = "no-destination-keeps-items", uuid = SL3_COMPONENT, setup = function( r )
			local entries = { { container = r.buffer, slot = 0, uuid = SL3_COMPONENT, quantity = 4 } }
			for _, chest in ipairs( r.chests ) do sl3FullBlockers( chest, 0, 9, blockerStack, entries ) end
			return sl3Fill( entries )
		end, verify = function( r, status, moved, remaining )
			return status == "NO_VALID_DESTINATION" and moved == 0 and remaining == 4 and sl3Count( r.buffer, SL3_COMPONENT ) == 4,
				"status=" .. status .. ", buffer=" .. sl3Count( r.buffer, SL3_COMPONENT )
		end },
		{ name = "destination-revision-conflict", uuid = SL3_COMPONENT, conflict = true, setup = function( r ) return sl3Fill( {
			{ container = r.buffer, slot = 0, uuid = SL3_COMPONENT, quantity = 2 }
		} ) end, verify = function( r, status, moved )
			return status == "NETWORK_CHANGED" and moved == 0 and sl3Count( r.buffer, SL3_COMPONENT ) == 2,
				"status=" .. status .. ", buffer=" .. sl3Count( r.buffer, SL3_COMPONENT )
		end }
	}
end

function SurvivalGame.sv_slstorage3Start( self, player )
	if self.sv.scrapLabStoragePhase3Runtime then sl3Message( self, player, "A Phase 3 test is already running." ); return end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase3 = { complete = false }
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then sl3Message( self, player, "A live character is required." ); return end
	local world = character:getWorld(); local direction = character:getDirection(); direction.z = 0; direction = direction:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local lateral = sm.vec3.new( -direction.y, direction.x, 0 ); local origin = character:getWorldPosition() + direction * 6 + sm.vec3.new( 0, 0, 1.5 )
	local runtime = { player = player, world = world, shapes = {}, destinationShapes = {}, chestShapes = {}, chests = {}, destinations = {}, results = {}, stage = "WAIT", deadline = sm.game.getCurrentTick() + 600 }
	local specs = { { SL3_TERMINAL, origin }, { SL3_WATER_CONTAINER, origin + lateral * 2 }, { SL3_CHEST, origin - lateral * 2 }, { SL3_CHEST, origin + direction * 2 }, { SL3_CHEST, origin + direction * 2 + lateral * 2 } }
	for index, spec in ipairs( specs ) do
		local ok, shape = pcall( sm.shape.createPart, spec[1], spec[2], sm.quat.identity(), false, true, world )
		if not ok or not shape then sl3Destroy( runtime ); sl3Message( self, player, "PHASE 3 TEST FAILED: fixture creation failed." ); return end
		runtime.shapes[#runtime.shapes + 1] = shape
		if index == 1 then runtime.terminal = shape else runtime.destinationShapes[#runtime.destinationShapes + 1] = shape; if index > 2 then runtime.chestShapes[#runtime.chestShapes + 1] = shape end end
	end
	self.sv.scrapLabStoragePhase3Runtime = runtime
	sl3Message( self, player, "Phase 3 automatic routing station created. No building or item placement is required." )
end

function SurvivalGame.sv_slstorage3Process( self )
	local r = self.sv.scrapLabStoragePhase3Runtime; if not r then return end
	if sm.game.getCurrentTick() > r.deadline then sl3Finish( self, r, "test timed out during " .. r.stage ); return end
	if r.stage == "WAIT" then
		r.instance = g_scrapLabNetworkStorageChestInstances and g_scrapLabNetworkStorageChestInstances[tostring( r.terminal:getId() )] or nil
		r.buffer = sl3Container( r.terminal ); r.descriptors = sl3Descriptors( r )
		if not r.instance or not r.buffer or not r.descriptors then return end
		for _, descriptor in ipairs( r.descriptors ) do r.destinations[#r.destinations + 1] = descriptor.container end
		r.waterContainer = sl3Container( r.destinationShapes[1] )
		for _, shape in ipairs( r.chestShapes ) do r.chests[#r.chests + 1] = sl3Container( shape ) end
		local ok = r.instance:sv_beginPhase1QualificationSession( r.player, r.descriptors, "PHASE3" ); if not ok then sl3Finish( self, r, "session failed" ); return end
		r.tests = sl3Tests( r ); r.index = 1; r.stage = "RUN"; return
	end
	if r.stage == "RUN" then
		if r.instance.sv.indexing then return end
		local test = r.tests[r.index]
		if not test then
			local candidates = r.instance:sv_collectDepositContainers(); local bufferId = g_scrapLabNetworkInventoryIndex.getContainerId( r.buffer ); local excluded = true
			for _, candidate in ipairs( candidates or {} ) do if candidate.id == bufferId then excluded = false end end
			sl3Record( r, "terminal-buffer-excluded", excluded and #( candidates or {} ) == 4, "candidates=" .. tostring( #( candidates or {} ) ) ); sl3Finish( self, r ); return
		end
		for _, container in ipairs( r.destinations ) do if not sl3Clear( container ) then sl3Finish( self, r, "destination clear failed" ); return end end
		if not sl3Clear( r.buffer ) or not test.setup( r ) then sl3Finish( self, r, test.name .. " setup failed" ); return end
		local before = sl3Total( r, test.uuid)
		if test.conflict then r.instance.sv.phase3BeforeCommitHook = function( instance, allocation )
			instance.sv.phase3BeforeCommitHook = nil; local target = allocation[1].descriptor.container
			if sm.container.beginTransaction() then sm.container.collect( target, SL3_BLOCKER, 1, true ); sm.container.endTransaction() end
		end end
		local success, status, moved, remaining, touched = r.instance:sv_routeDepositSlot( r.buffer, 0 )
		local destinations = 0; for _ in pairs( touched or {} ) do destinations = destinations + 1 end
		local passed, detail = test.verify( r, status, moved, remaining, destinations, success ); sl3Record( r, test.name, passed, detail )
		sl3Record( r, test.name .. "-conservation", sl3Total( r, test.uuid ) == before, "before=" .. before .. ", after=" .. sl3Total( r, test.uuid ) )
		r.index = r.index + 1; return
	end
end

function SurvivalGame.sv_slstorage3Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "auto" ) )
	if action == "auto" then self:sv_slstorage3Start( player )
	elseif action == "debug" then
		local nearest, distance = nil, math.huge; local character = player and player:getCharacter()
		for _, instance in pairs( g_scrapLabNetworkStorageChestInstances or {} ) do local d = character and ( character:getWorldPosition() - instance.shape:getWorldPosition() ):length2() or math.huge; if d < distance then nearest, distance = instance, d end end
		if nearest and distance <= 24 * 24 then local enabled = nearest:sv_setDepositDebug( not nearest.sv.depositDebug ); sl3Message( self, player, "Deposit routing debug " .. ( enabled and "enabled" or "disabled" ) .. "." ) else sl3Message( self, player, "No Network Storage Chest found within 24 meters." ) end
	else sl3Message( self, player, "Commands: /slstorage3 auto | /slstorage3 debug" ) end
end
local SL3_BIND = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self ) if SL3_BIND then SL3_BIND( self ) end; sm.game.bindChatCommand( "/slstorage3", { { "string", "action", true } }, "cl_onChatCommand", "ScrapLab storage routing test" ) end
local SL3_CHAT = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params ) if params[1] == "/slstorage3" then self.network:sendToServer( "sv_slstorage3Command", { action = params[2] or "auto" } ); return end; if SL3_CHAT then SL3_CHAT( self, params ) end end
local SL3_FIXED = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, dt ) if SL3_FIXED then SL3_FIXED( self, dt ) end; local ok, failure = pcall( function() self:sv_slstorage3Process() end ); if not ok then local r = self.sv and self.sv.scrapLabStoragePhase3Runtime; sl3Log( "runtime error: " .. tostring( failure ) ); if r then sl3Finish( self, r, failure ) end end end
sl3Log( "automatic harness ready; use /slstorage3 auto" )
