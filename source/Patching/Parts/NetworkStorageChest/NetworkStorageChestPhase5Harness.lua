-- SCRAPLAB NETWORK STORAGE CHEST PHASE 5 AUTOMATIC HARNESS
-- Final GUI, localization, icon-atlas, client rendering, and cleanup qualification.

local SL5_TERMINAL_UUID = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL5_PREFIX = "[ScrapLab Storage Phase 5] "
local SL5_LANGUAGES = { "Brazilian", "Chinese", "English", "French", "German", "Italian",
	"Japanese", "Korean", "Polish", "Russian", "Spanish" }

local function sl5Log( message ) sm.log.info( SL5_PREFIX .. tostring( message ) ) end

local function sl5Message( self, player, message )
	if player then self.network:sendToClient( player, "cl_slstorage5Message", tostring( message ) ) end
	sl5Log( message )
end

local function sl5Record( runtime, name, passed, detail )
	runtime.results[#runtime.results + 1] = {
		name = tostring( name ), passed = passed == true, detail = tostring( detail or "" )
	}
end

local function sl5DestroyShape( shape )
	if shape and sm.exists( shape ) then pcall( function() shape:destroyShape( 0 ) end ) end
end

local function sl5Finish( self, runtime )
	sl5DestroyShape( runtime.terminal )
	g_scrapLabStoragePhase5Qualification[runtime.token] = nil
	self.sv.scrapLabStoragePhase5Runtime = nil
	local passed, failed = 0, 0
	for _, result in ipairs( runtime.results ) do
		if result.passed then passed = passed + 1
		else
			failed = failed + 1
			sl5Message( self, runtime.player, "FAIL " .. result.name .. " - " .. result.detail )
		end
	end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase5 = {
		complete = true, passed = passed, failed = failed, skipped = 0, results = runtime.results
	}
	sl5Message( self, runtime.player, "PHASE 5 COMPLETE: " .. tostring( passed ) ..
		" passed, " .. tostring( failed ) .. " failed. Temporary terminal removed." )
end

local function sl5ValidateInstalledLanguages( runtime )
	local source = sm.json.open( "$SURVIVAL_DATA/Scripts/ScrapLab/Parts/NetworkStorageChest/NetworkStorageChest.localization.json" )
	for _, language in ipairs( SL5_LANGUAGES ) do
		local expected = source and source[language]
		local installed = sm.json.open( "$SURVIVAL_DATA/Gui/Language/" .. language .. "/inventoryDescriptions.json" )
		local entry = installed and installed[tostring( SL5_TERMINAL_UUID)]
		local valid = expected and entry and entry.title == expected.inventoryTitle and
			entry.upperCaseTitle == expected.inventoryUpper and entry.description == expected.inventoryDescription
		sl5Record( runtime, "inventory-language-" .. string.lower( language ), valid,
			valid and entry.title or "missing or mismatched installed description" )
	end
end

function SurvivalGame.sv_slstorage5Start( self, player )
	if self.sv.scrapLabStoragePhase5Runtime then
		sl5Message( self, player, "A Phase 5 test is already running." )
		return
	end
	g_scrapLabStorageQualificationResults = g_scrapLabStorageQualificationResults or {}
	g_scrapLabStorageQualificationResults.phase5 = { complete = false }
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then
		sl5Message( self, player, "A live character is required." )
		return
	end
	local direction = character:getDirection(); direction.z = 0
	direction = direction:safeNormalize( sm.vec3.new( 1, 0, 0 ) )
	local token = "sl5:" .. tostring( sm.game.getCurrentTick() ) .. ":" .. tostring( player.id )
	local runtime = {
		player = player, world = character:getWorld(), token = token, results = {}, stage = "SPAWN",
		position = character:getWorldPosition() + direction * 4 + sm.vec3.new( 0, 0, 1.25 ),
		deadline = sm.game.getCurrentTick() + 800
	}
	self.sv.scrapLabStoragePhase5Runtime = runtime
	g_scrapLabStoragePhase5Qualification = g_scrapLabStoragePhase5Qualification or {}
	g_scrapLabStoragePhase5Qualification[token] = { complete = false }
	sl5ValidateInstalledLanguages( runtime )
	sl5Message( self, player, "Phase 5 automatic UI, localization, and icon qualification started." )
end

local function sl5Process( self )
	local runtime = self.sv.scrapLabStoragePhase5Runtime
	if not runtime then return end
	local tick = sm.game.getCurrentTick()
	if tick > runtime.deadline then
		sl5Record( runtime, "automatic-runtime", false, "timed out in " .. tostring( runtime.stage ) )
		sl5Finish( self, runtime )
		return
	end
	if runtime.stage == "SPAWN" then
		local ok, shape = pcall( sm.shape.createPart, SL5_TERMINAL_UUID, runtime.position,
			sm.quat.identity(), false, true, runtime.world )
		if not ok or not shape then
			sl5Record( runtime, "temporary-terminal", false, tostring( shape ) )
			sl5Finish( self, runtime )
			return
		end
		runtime.terminal = shape
		runtime.stage = "WAIT_INSTANCE"
		return
	end
	if runtime.stage == "WAIT_INSTANCE" then
		local instance = runtime.terminal and g_scrapLabNetworkStorageChestInstances and
			g_scrapLabNetworkStorageChestInstances[tostring( runtime.terminal:getId() )] or nil
		if not instance then return end
		runtime.instance = instance
		sl5Record( runtime, "temporary-terminal", true, "real server and client script instance ready" )
		local sent, failure = pcall( sm.event.sendToInteractable, runtime.terminal:getInteractable(),
			"sv_e_startPhase5ClientQualification", { playerId = tostring( runtime.player.id), token = runtime.token } )
		if not sent then
			sl5Record( runtime, "client-qualification-event", false, tostring( failure ) )
			sl5Finish( self, runtime )
			return
		end
		runtime.stage = "WAIT_CLIENT"
		return
	end
	if runtime.stage == "WAIT_CLIENT" then
		local response = g_scrapLabStoragePhase5Qualification[runtime.token]
		if not response or response.complete ~= true then return end
		for _, result in ipairs( response.results or {} ) do
			sl5Record( runtime, "client-" .. tostring( result.name ), result.passed == true, result.detail )
		end
		-- cl_destroyGui closes the JSON GUI asynchronously. Keep the temporary
		-- terminal alive until its queued cl_onGuiClosed callback has run; deleting
		-- the shape immediately leaves an invalid client script reference behind.
		runtime.stage = "WAIT_GUI_CLOSE"
		runtime.cleanupTick = tick + 20
		return
	end
	if runtime.stage == "WAIT_GUI_CLOSE" then
		if tick < runtime.cleanupTick then return end
		sl5Finish( self, runtime )
	end
end

function SurvivalGame.sv_slstorage5Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "auto" ) )
	if action == "auto" then self:sv_slstorage5Start( player )
	else sl5Message( self, player, "Use /slstorage5 auto. Nothing needs to be built." ) end
end

function SurvivalGame.cl_slstorage5Message( self, message )
	sm.gui.chatMessage( "#55DFFF" .. tostring( message ) )
end

local SL5_SERVER_CREATE = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	SL5_SERVER_CREATE( self )
	self.sv.scrapLabStoragePhase5Runtime = nil
	g_scrapLabStoragePhase5Qualification = g_scrapLabStoragePhase5Qualification or {}
end

local SL5_FIXED_UPDATE = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	SL5_FIXED_UPDATE( self, timeStep )
	sl5Process( self )
end

local SL5_CLIENT_CREATE = SurvivalGame.client_onCreate
function SurvivalGame.client_onCreate( self )
	SL5_CLIENT_CREATE( self )
	sm.game.bindChatCommand( "/slstorage5", { { "string", "action", true } },
		"cl_onChatCommand", "ScrapLab Network Storage Chest Phase 5 automatic test" )
end

local SL5_CHAT = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slstorage5" then
		self.network:sendToServer( "sv_slstorage5Command", { action = params[2] or "auto" } )
		return
	end
	SL5_CHAT( self, params )
end

sl5Log( "automatic harness ready; use /slstorage5 auto" )
