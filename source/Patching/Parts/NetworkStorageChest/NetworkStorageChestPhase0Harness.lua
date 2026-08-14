local SL_STORAGE_PHASE0_UUID = sm.uuid.new( "bc7576a7-f226-459a-883c-e8460e955d63" )
local SL_STORAGE_PHASE0_PREFIX = "[ScrapLab Storage Phase 0] "
local SL_STORAGE_PHASE1_PREFIX = "[ScrapLab Storage Phase 1] "

local function slStoragePhase0Log( message )
	sm.log.info( SL_STORAGE_PHASE0_PREFIX .. tostring( message ) )
end

local function slStoragePhase0ContainerSummary( container )
	if not container then return 0, 0, 0, -1 end
	local occupied, total = 0, 0
	for slot = 0, container:getSize() - 1 do
		local item = container:getItem( slot )
		if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
			occupied = occupied + 1
			total = total + item.quantity
		end
	end
	return container:getSize(), occupied, total, container:getRevision()
end

local function slStoragePhase0FindNearest( player, radius )
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then return nil end
	local world = character:getWorld()
	local position = character:getWorldPosition()
	local best, bestDistance = nil, ( radius or 16 ) * ( radius or 16 )
	for _, body in ipairs( sm.body.getAllBodies( world ) ) do
		for _, shape in ipairs( body:getShapes() ) do
			if shape:getShapeUuid() == SL_STORAGE_PHASE0_UUID then
				local distance = ( shape:getWorldPosition() - position ):length2()
				if distance <= bestDistance then
					best = shape
					bestDistance = distance
				end
			end
		end
	end
	return best, math.sqrt( bestDistance )
end

function SurvivalGame.cl_slstorage0Message( self, message )
	sm.gui.chatMessage( SL_STORAGE_PHASE0_PREFIX .. tostring( message ) )
end

function SurvivalGame.sv_slstorage0Message( self, player, message )
	slStoragePhase0Log( message )
	if player then self.network:sendToClient( player, "cl_slstorage0Message", message ) end
end

function SurvivalGame.cl_slstorage1Message( self, message )
	sm.gui.chatMessage( SL_STORAGE_PHASE1_PREFIX .. tostring( message ) )
end

function SurvivalGame.sv_slstorage1Message( self, player, message )
	sm.log.info( SL_STORAGE_PHASE1_PREFIX .. tostring( message ) )
	if player then self.network:sendToClient( player, "cl_slstorage1Message", message ) end
end

function SurvivalGame.sv_slstorage0Spawn( self, player )
	local character = player and player:getCharacter() or nil
	if not character or not sm.exists( character ) then
		self:sv_slstorage0Message( player, "A live character is required." )
		return
	end
	local direction = character:getDirection()
	local position = character:getWorldPosition() + direction * 3 + sm.vec3.new( 0, 0, 0.5 )
	local shape = sm.shape.createPart(
		SL_STORAGE_PHASE0_UUID, position, sm.quat.identity(), false, true,
		character:getWorld() )
	if not shape or not sm.exists( shape ) then
		self:sv_slstorage0Message( player, "The probe part could not be created. Check the game log for registration errors." )
		return
	end
	shape.color = sm.color.new( "df7f01" )
	self:sv_slstorage0Message( player, "Network Storage Chest probe spawned in front of you. Interact with it to open the Phase 0 GUI." )
end

function SurvivalGame.sv_slstorage0Status( self, player )
	local shape, distance = slStoragePhase0FindNearest( player, 24 )
	if not shape then
		self:sv_slstorage0Message( player, "No Network Storage Chest probe was found within 24 meters." )
		return
	end
	local interactable = shape:getInteractable()
	local container = interactable and interactable:getContainer( 0 ) or nil
	local size, occupied, total, revision = slStoragePhase0ContainerSummary( container )
	local passed = size == 5
	self:sv_slstorage0Message( player,
		( passed and "PASS" or "FAIL" ) ..
		" — nearest probe is " .. string.format( "%.1f", distance or 0 ) ..
		"m away; slots=" .. tostring( size ) ..
		", occupied=" .. tostring( occupied ) ..
		", items=" .. tostring( total ) ..
		", revision=" .. tostring( revision ) .. "." )
end

function SurvivalGame.sv_slstorage0Cleanup( self, player )
	local shape = slStoragePhase0FindNearest( player, 24 )
	if not shape then
		self:sv_slstorage0Message( player, "No Network Storage Chest probe was found within 24 meters." )
		return
	end
	local interactable = shape:getInteractable()
	local container = interactable and interactable:getContainer( 0 ) or nil
	local _, _, total = slStoragePhase0ContainerSummary( container )
	if total > 0 then
		self:sv_slstorage0Message( player, "Cleanup refused: empty the 3-slot deposit buffer first." )
		return
	end
	shape:destroyShape( 0 )
	self:sv_slstorage0Message( player, "The nearest empty Phase 0 probe was removed." )
end

function SurvivalGame.sv_slstorage0Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "help" ) )
	if action == "spawn" then
		self:sv_slstorage0Spawn( player )
	elseif action == "status" then
		self:sv_slstorage0Status( player )
	elseif action == "cleanup" then
		self:sv_slstorage0Cleanup( player )
	else
		self:sv_slstorage0Message( player, "Commands: /slstorage0 spawn, /slstorage0 status, /slstorage0 cleanup." )
	end
end

function SurvivalGame.sv_slstorage1Status( self, player )
	local shape, distance = slStoragePhase0FindNearest( player, 24 )
	if not shape then
		self:sv_slstorage1Message( player, "No Network Storage Chest was found within 24 meters." )
		return
	end
	local interactable = shape:getInteractable()
	local data = interactable and interactable.publicData and
		interactable.publicData.scrapLabStoragePhase1 or nil
	if not data then
		self:sv_slstorage1Message( player, "FAIL — Phase 1 diagnostics are unavailable; check the game log." )
		return
	end
	self:sv_slstorage1Message( player,
		"PASS — " .. string.format( "%.1f", distance or 0 ) .. "m; status=" .. tostring( data.status ) ..
		", viewers=" .. tostring( data.viewers ) ..
		", containers=" .. tostring( data.containers ) ..
		", unique=" .. tostring( data.uniqueItems ) ..
		", quantity=" .. tostring( data.totalQuantity ) ..
		", topologyGen=" .. tostring( data.topologyGeneration ) ..
		", contentGen=" .. tostring( data.contentGeneration ) ..
		", scanTicks=" .. tostring( data.durationTicks ) ..
		", cacheHits=" .. tostring( data.cacheHits ) ..
		", rescans=" .. tostring( data.containerScans ) ..
		", slotsScanned=" .. tostring( data.slotsScanned ) .. "." )
end

function SurvivalGame.sv_slstorage1Command( self, data, player )
	local action = string.lower( tostring( data and data.action or "status" ) )
	if action == "status" then
		self:sv_slstorage1Status( player )
	else
		self:sv_slstorage1Message( player, "Command: /slstorage1 status" )
	end
end

local SLStoragePhase0OriginalBindChatCommands = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	if SLStoragePhase0OriginalBindChatCommands then SLStoragePhase0OriginalBindChatCommands( self ) end
	sm.game.bindChatCommand( "/slstorage0", {
		{ "string", "action", true }
	}, "cl_onChatCommand", "ScrapLab Network Storage Chest Phase 0 probe" )
	sm.game.bindChatCommand( "/slstorage1", {
		{ "string", "action", true }
	}, "cl_onChatCommand", "ScrapLab Network Storage Chest Phase 1 diagnostics" )
end

local SLStoragePhase0OriginalClientChatCommand = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/slstorage0" then
		self.network:sendToServer( "sv_slstorage0Command", { action = params[2] or "help" } )
		return
	end
	if params[1] == "/slstorage1" then
		self.network:sendToServer( "sv_slstorage1Command", { action = params[2] or "status" } )
		return
	end
	if SLStoragePhase0OriginalClientChatCommand then
		SLStoragePhase0OriginalClientChatCommand( self, params )
	end
end

slStoragePhase0Log( "harness loaded; use /slstorage0 help and /slstorage1 status" )
