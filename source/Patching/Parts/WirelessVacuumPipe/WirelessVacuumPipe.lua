if WirelessPipeManager == nil then
	dofile( "$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/WirelessPipeManager.lua" )
end
if PipeEffectPlayer == nil then
	dofile( "$SURVIVAL_DATA/Scripts/game/util/pipes.lua" )
end

WirelessVacuumPipe = class( nil )
WirelessVacuumPipe.maxParentCount = 1
WirelessVacuumPipe.maxChildCount = 0
WirelessVacuumPipe.connectionInput = sm.interactable.connectionType.logic
WirelessVacuumPipe.connectionOutput = sm.interactable.connectionType.none
WirelessVacuumPipe.colorNormal = sm.color.new( 0x777777ff )
WirelessVacuumPipe.colorHighlight = sm.color.new( 0xeeeeeeff )

local PART_UUID = "a34d9af0-4ba0-431d-b647-2d5435ecf138"
local ENDPOINT_STORAGE_VERSION = 2
local POLL_INTERVAL_TICKS = 20
local ACTIVITY_POLL_INTERVAL_TICKS = 2
local ACTIVITY_VISUAL_RANGE = 64
local MODE_ORDER = { "LINK", "SEND", "RECEIVE" }

local MODE_EXPLANATIONS = {
	LINK = "Joins enabled Link endpoints on this paint channel into one pipe network.",
	SEND = "Pushes items toward matching Receivers on this paint channel.",
	RECEIVE = "Lets local machines pull items from matching Send endpoints."
}

local STATUS_COLORS = {
	["LINKED"] = "65C466FF",
	["CROSS-WORLD LINKED"] = "48C6E8FF",
	["SENDING"] = "65C466FF",
	["READY TO RECEIVE"] = "65C466FF",
	["LOADING ROUTE"] = "48C6E8FF",
	["UNPAIRED"] = "F2B134FF",
	["CHANNEL EMPTY"] = "F2B134FF",
	["DISABLED BY LOGIC"] = "777777FF",
	["REMOTE CELL LOAD LIMIT"] = "E56C2FFF",
	["WIRELESS MANAGER UNAVAILABLE"] = "D94A3AFF"
}

local function formatMatchingWorlds( worlds )
	if type( worlds ) ~= "table" or #worlds == 0 then return "NO MATCHING WORLDS" end
	if #worlds <= 2 then return table.concat( worlds, "  |  " ) end
	return tostring( worlds[1] ) .. "  |  " .. tostring( worlds[2] ) .. "  |  +" .. tostring( #worlds - 2 ) .. " MORE"
end

local function shapeWorld( shape )
	local body = shape and shape:getBody() or nil
	return body and body:getWorld() or nil
end

local function channelFromShape( shape )
	local ok, value = pcall( function() return shape:getColor():getHexStr() end )
	if ok and value then
		value = string.upper( tostring( value ):gsub( "#", "" ) )
		if #value == 6 then value = value .. "FF" end
		if #value == 8 then return value end
	end
	return "DF7F01FF"
end

local function logicEnabled( interactable )
	local parents = interactable:getParents( sm.interactable.connectionType.logic )
	if #parents == 0 then return true end
	return parents[1]:isActive()
end

local function newEndpointId( shape )
	local world = shapeWorld( shape )
	return table.concat( {
		"slwp", tostring( world and world.id or 0 ), tostring( shape and shape.id or 0 ),
		tostring( sm.game.getCurrentTick() ), tostring( math.random( 100000, 999999999 ) )
	}, ":" )
end

local function positionsDiffer( a, b )
	return not a or not b or ( a - b ):length2() > 0.0001
end

function WirelessVacuumPipe.server_onCreate( self )
	self.sv = { pollTicks = 0, activityPollTicks = 0, generation = nil, registered = false, managerReason = nil, unloaded = false }
	self.sv.saved = self.storage:load()
	if type( self.sv.saved ) ~= "table" then
		self.sv.saved = {
			storageVersion = ENDPOINT_STORAGE_VERSION,
			endpointId = newEndpointId( self.shape ),
			mode = "LINK",
			directOnly = true
		}
	end
	self.sv.saved.endpointId = tostring( self.sv.saved.endpointId or newEndpointId( self.shape ) )
	if self.sv.saved.mode ~= "LINK" and self.sv.saved.mode ~= "SEND" and self.sv.saved.mode ~= "RECEIVE" then
		self.sv.saved.mode = "LINK"
	end
	-- Definition 2 keeps every existing endpoint identity and mode. Older
	-- placements adopt the new safe default instead of being recreated.
	self.sv.saved.storageVersion = ENDPOINT_STORAGE_VERSION
	if self.sv.saved.directOnly == nil then self.sv.saved.directOnly = true end
	self.sv.saved.directOnly = self.sv.saved.directOnly ~= false
	self.storage:save( self.sv.saved )
	self:sv_registerOrRefresh( true )
end

function WirelessVacuumPipe.server_onRefresh( self )
	self:server_onCreate()
end

function WirelessVacuumPipe.server_onFixedUpdate( self )
	self.sv.activityPollTicks = self.sv.activityPollTicks + 1
	if self.sv.registered and self.sv.activityPollTicks >= ACTIVITY_POLL_INTERVAL_TICKS then
		self.sv.activityPollTicks = 0
		local activity = WirelessPipeManager.Sv_ConsumeEndpointActivity( self.sv.saved.endpointId, self.sv.generation )
		if activity then
			self:sv_onDirectionalActivity( activity.role, activity.itemUuid, activity.containerShape, activity.crossWorld )
		end
	end
	self.sv.pollTicks = self.sv.pollTicks + 1
	if self.sv.pollTicks >= POLL_INTERVAL_TICKS then
		self.sv.pollTicks = 0
		self:sv_registerOrRefresh( false )
	end
end

function WirelessVacuumPipe.server_onWorldChanged( self )
	self:sv_registerOrRefresh( false )
end

function WirelessVacuumPipe.server_onUnload( self )
	if self.sv and self.sv.saved then
		-- Paint and logic are polled, so a player can leave the world before the
		-- next scheduled refresh. Capture one final authoritative state while the
		-- shape is still valid; otherwise the manager keeps the old channel until
		-- somebody visits this endpoint's world again.
		local ok, data = pcall( function() return self:sv_collectState() end )
		if ok and data then
			local result = WirelessPipeManager.Sv_RefreshEndpoint( data, self.shape, self, self.sv.generation )
			if result and result.ok then self.sv.generation = result.generation end
		end
		WirelessPipeManager.Sv_UnloadEndpoint( self.sv.saved.endpointId, self.shape, self.sv.generation )
	end
	self.sv.registered = false
	self.sv.unloaded = true
end

function WirelessVacuumPipe.server_onDestroy( self )
	-- Scrap Mechanic tears down a shape script after server_onUnload. That is
	-- not a player deleting the part. Match vanilla persistent-part lifecycle
	-- guards so only a real loaded-world destruction removes the registry row.
	if self.sv and self.sv.saved and not self.sv.unloaded then
		WirelessPipeManager.Sv_UnregisterEndpoint( self.sv.saved.endpointId, self.shape, self.sv.generation )
	end
end

function WirelessVacuumPipe.sv_collectState( self )
	local world = shapeWorld( self.shape )
	if not world then return nil end
	local position = self.shape.worldPosition
	return {
		endpointId = self.sv.saved.endpointId,
		partUuid = PART_UUID,
		world = world,
		position = position,
		cellX = math.floor( position.x / 64 ),
		cellY = math.floor( position.y / 64 ),
		mode = self.sv.saved.mode,
		directOnly = self.sv.saved.directOnly ~= false,
		channel = channelFromShape( self.shape ),
		enabled = logicEnabled( self.interactable ),
		shapeId = self.shape.id
	}
end

function WirelessVacuumPipe.sv_registerOrRefresh( self, forceRegister )
	local data = self:sv_collectState()
	if not data then return end
	local changed = forceRegister or not self.sv.lastState
	if self.sv.lastState then
		changed = changed or self.sv.lastState.worldId ~= data.world.id
		changed = changed or self.sv.lastState.cellX ~= data.cellX or self.sv.lastState.cellY ~= data.cellY
		changed = changed or self.sv.lastState.mode ~= data.mode or self.sv.lastState.channel ~= data.channel
		changed = changed or self.sv.lastState.directOnly ~= data.directOnly
		changed = changed or self.sv.lastState.enabled ~= data.enabled
		local positionRefreshDue = sm.game.getCurrentTick() >= ( self.sv.nextPositionRefreshTick or 0 )
		changed = changed or ( positionRefreshDue and positionsDiffer( self.sv.lastState.position, data.position ) )
	end
	if not changed and self.sv.registered then
		local status = WirelessPipeManager.Sv_GetEndpointStatus( data.endpointId )
		if status then self:sv_publishStatus( status ) end
		return
	end

	local result
	if self.sv.registered and not forceRegister then
		result = WirelessPipeManager.Sv_RefreshEndpoint( data, self.shape, self, self.sv.generation )
	else
		result = WirelessPipeManager.Sv_RegisterEndpoint( data, self.shape, self )
	end
	if result and not result.ok and result.reason == "DUPLICATE ENDPOINT ID" then
		self.sv.saved.endpointId = newEndpointId( self.shape )
		self.storage:save( self.sv.saved )
		data.endpointId = self.sv.saved.endpointId
		result = WirelessPipeManager.Sv_RegisterEndpoint( data, self.shape, self )
	end
	if result and result.ok then
		self.sv.registered = true
		self.sv.generation = result.generation
		self.sv.managerReason = nil
		self.sv.lastState = {
			worldId = data.world.id, cellX = data.cellX, cellY = data.cellY,
			position = sm.vec3.new( data.position.x, data.position.y, data.position.z ),
			mode = data.mode, channel = data.channel, enabled = data.enabled,
			directOnly = data.directOnly
		}
		self.sv.nextPositionRefreshTick = sm.game.getCurrentTick() + 200
		self:sv_publishStatus( result.status or WirelessPipeManager.Sv_GetEndpointStatus( data.endpointId ) )
	else
		self.sv.registered = false
		self.sv.managerReason = result and result.reason or "WIRELESS MANAGER UNAVAILABLE"
		self:sv_publishStatus( {
			state = self.sv.managerReason, mode = data.mode, channel = data.channel,
			enabled = data.enabled, matchingCount = 0, worlds = {}
		} )
	end
end

function WirelessVacuumPipe.sv_makeClientPayload( self, status )
	if not status then return end
	return {
		state = status.state or "WIRELESS MANAGER UNAVAILABLE",
		mode = status.mode or self.sv.saved.mode,
		directOnly = status.directOnly ~= false,
		channel = status.channel or channelFromShape( self.shape ),
		enabled = status.enabled ~= false,
		matchingCount = status.matchingCount or 0,
		worlds = status.worlds or {},
		worldLabel = status.worldLabel or "UNKNOWN WORLD",
		cellX = status.cellX,
		cellY = status.cellY,
		handleReady = status.handleReady == true,
		handleLimited = status.handleLimited == true,
		explanation = MODE_EXPLANATIONS[status.mode or self.sv.saved.mode]
	}
end

function WirelessVacuumPipe.sv_publishStatus( self, status )
	local payload = self:sv_makeClientPayload( status )
	if not payload then return end
	local signature = payload.state .. "|" .. payload.mode .. "|" .. payload.channel .. "|" .. tostring( payload.enabled ) .. "|" .. tostring( payload.directOnly ) .. "|" .. tostring( payload.matchingCount ) .. "|" .. table.concat( payload.worlds, "," )
	if signature ~= self.sv.lastClientSignature then
		self.sv.lastClientSignature = signature
		self.network:setClientData( payload )
	end
end

function WirelessVacuumPipe.sv_sendAuthoritativeStatus( self, player )
	if not player then return end
	local status = WirelessPipeManager.Sv_GetEndpointStatus( self.sv.saved.endpointId )
	if not status then
		status = {
			state = self.sv.managerReason or "WIRELESS MANAGER UNAVAILABLE",
			mode = self.sv.saved.mode,
			directOnly = self.sv.saved.directOnly ~= false,
			channel = channelFromShape( self.shape ),
			enabled = logicEnabled( self.interactable ),
			matchingCount = 0,
			worlds = {}
		}
	end
	local payload = self:sv_makeClientPayload( status )
	if payload then self.network:sendToClient( player, "cl_n_applyAuthoritativeStatus", payload ) end
end

function WirelessVacuumPipe.sv_canConfigure( self, player )
	local character = player and player:getCharacter() or nil
	local world = shapeWorld( self.shape )
	if not character or not sm.exists( character ) or character:getWorld() ~= world then return false end
	return ( character:getWorldPosition() - self.shape.worldPosition ):length2() <= 64
end

function WirelessVacuumPipe.sv_n_requestStatus( self, _, player )
	if not self:sv_canConfigure( player ) then return end
	self:sv_registerOrRefresh( false )
	self:sv_sendAuthoritativeStatus( player )
end

function WirelessVacuumPipe.sv_n_setMode( self, mode, player )
	mode = string.upper( tostring( mode or "" ) )
	if mode ~= "LINK" and mode ~= "SEND" and mode ~= "RECEIVE" then return end
	if not self:sv_canConfigure( player ) then return end
	if self.sv.saved.mode ~= mode then
		self.sv.saved.mode = mode
		self.storage:save( self.sv.saved )
		self:sv_registerOrRefresh( false )
	end
	self:sv_sendAuthoritativeStatus( player )
end

function WirelessVacuumPipe.sv_n_setDirectOnly( self, directOnly, player )
	if not self:sv_canConfigure( player ) then return end
	directOnly = directOnly ~= false
	if self.sv.saved.directOnly ~= directOnly then
		self.sv.saved.directOnly = directOnly
		self.storage:save( self.sv.saved )
		self:sv_registerOrRefresh( false )
	end
	self:sv_sendAuthoritativeStatus( player )
end

function WirelessVacuumPipe.sv_onDirectionalActivity( self, role, itemUuid, containerShape, crossWorld )
	if role ~= "SEND" and role ~= "RECEIVE" then return end
	if not containerShape or not sm.exists( containerShape ) then return end
	local data = {
		role = role,
		itemUuid = itemUuid,
		containerShape = containerShape,
		crossWorld = crossWorld == true
	}
	local shapeWorld = self.shape:getBody():getWorld()
	local shapePosition = self.shape.worldPosition
	for _, player in ipairs( sm.player.getAllPlayers() ) do
		local character = player and player:getCharacter() or nil
		if character and sm.exists( character ) and character:getWorld() == shapeWorld and
			( character.worldPosition - shapePosition ):length2() <= ACTIVITY_VISUAL_RANGE * ACTIVITY_VISUAL_RANGE then
			-- A server-loaded cross-world cell may have no client Shape script.
			-- Address only nearby clients that can actually own this visual instance.
			self.network:sendToClient( player, "cl_n_directionalActivity", data )
		end
	end
end

function WirelessVacuumPipe.client_onCreate( self )
	self.cl = {
		data = { state = "WIRELESS MANAGER UNAVAILABLE", mode = "LINK", directOnly = true, channel = "DF7F01FF", matchingCount = 0, worlds = {} },
		glow = 0,
		guiDirty = false,
		activityTicks = 0
	}
	-- Most endpoints are idle. Allocate the relatively heavy path-effect player
	-- only when an actual directional transfer needs it.
	self.cl.pipeEffectPlayer = nil
	self.cl.lastAppliedGlow = 0
	self.interactable:setUvFrameIndex( 0 )
	self.interactable:setGlowMultiplier( 0 )
end

function WirelessVacuumPipe.client_onDestroy( self )
	if self.cl and self.cl.pipeEffectPlayer then self.cl.pipeEffectPlayer:onDestroy() end
	if self.cl and self.cl.gui then
		self.cl.gui:close()
		self.cl.gui:destroy()
		self.cl.gui = nil
	end
end

function WirelessVacuumPipe.client_onClientDataUpdate( self, data )
	self.cl = self.cl or {}
	self.cl.data = data or self.cl.data
	self.cl.guiDirty = true
	self:cl_updateGui()
end

function WirelessVacuumPipe.client_onUpdate( self, deltaTime )
	if self.cl and self.cl.pipeEffectPlayer then self.cl.pipeEffectPlayer:update( deltaTime ) end
	if self.cl and self.cl.guiDirty then self:cl_updateGui() end
end

function WirelessVacuumPipe.client_onFixedUpdate( self )
	local state = self.cl.data and self.cl.data.state or ""
	if self.cl.activityTicks > 0 then self.cl.activityTicks = self.cl.activityTicks - 1 end
	local target = ( state == "LINKED" or state == "CROSS-WORLD LINKED" or state == "SENDING" or state == "READY TO RECEIVE" ) and 0.55 or 0
	if self.cl.activityTicks > 0 then target = self.cl.activityTicks % 4 < 2 and 1.0 or 0.35 end
	self.cl.glow = self.cl.glow + ( target - self.cl.glow ) * 0.2
	if target == 0 and self.cl.glow < 0.005 then self.cl.glow = 0 end
	if math.abs( self.cl.glow - ( self.cl.lastAppliedGlow or 0 ) ) >= 0.01 or
			( self.cl.glow == 0 and self.cl.lastAppliedGlow ~= 0 ) then
		self.cl.lastAppliedGlow = self.cl.glow
		self.interactable:setGlowMultiplier( self.cl.glow )
	end
end

function WirelessVacuumPipe.cl_ensurePipeEffectPlayer( self )
	if self.cl.pipeEffectPlayer then return self.cl.pipeEffectPlayer end
	self.cl.pipeEffectPlayer = PipeEffectPlayer()
	self.cl.pipeEffectPlayer:onCreate()
	return self.cl.pipeEffectPlayer
end

function WirelessVacuumPipe.cl_n_directionalActivity( self, data )
	if type( data ) ~= "table" or ( data.role ~= "SEND" and data.role ~= "RECEIVE" ) then return end
	self.cl.activityTicks = data.crossWorld and 16 or 10
	local containerShape = data.containerShape
	if not containerShape or not sm.exists( containerShape ) then return end
	if containerShape:getBody():getWorld() ~= self.shape:getBody():getWorld() then return end
	local direction = data.role == "SEND" and sm.pipeGraph.direction.incoming or sm.pipeGraph.direction.outgoing
	local ok, nativePath = pcall( function()
		return sm.pipeGraph.getContainerPath( self.shape, containerShape, direction )
	end )
	if not ok or type( nativePath ) ~= "table" then return end
	local path = {}
	if data.role == "SEND" then
		for index = #nativePath, 1, -1 do path[#path + 1] = nativePath[index] end
		path[#path + 1] = self.shape
	else
		path[#path + 1] = self.shape
		for _, shape in ipairs( nativePath ) do path[#path + 1] = shape end
	end
	if #path >= 2 then
		self:cl_ensurePipeEffectPlayer():pushShapeEffectTask( path, data.itemUuid )
	end
end

function WirelessVacuumPipe.client_canInteract( self )
	sm.gui.setInteractionText( "", sm.gui.getKeyBinding( "Use", true ), "CONFIGURE WIRELESS PIPE" )
	return true
end

function WirelessVacuumPipe.client_onInteract( self, character, state )
	if not state then return end
	if self.cl.gui then self.cl.gui:destroy(); self.cl.gui = nil end
	self.cl.gui = sm.gui.createGuiFromLayout( "$SURVIVAL_DATA/Gui/Layouts/ScrapLab/Parts/WirelessVacuumPipe.layout" )
	self.cl.gui:setButtonCallback( "ModeLink", "cl_onModeButton" )
	self.cl.gui:setButtonCallback( "ModeSend", "cl_onModeButton" )
	self.cl.gui:setButtonCallback( "ModeReceive", "cl_onModeButton" )
	self.cl.gui:setButtonCallback( "ScopeButton", "cl_onScopeButton" )
	self.cl.gui:setOnCloseCallback( "cl_onClose" )
	self.cl.guiDirty = true
	self.cl.gui:open()
	self:cl_updateGui()
	self.network:sendToServer( "sv_n_requestStatus" )
end

function WirelessVacuumPipe.cl_onScopeButton( self )
	local data = self.cl.data or {}
	if data.mode ~= "SEND" and data.mode ~= "RECEIVE" then return end
	data.directOnly = data.directOnly == false
	self.cl.data = data
	self.cl.guiDirty = true
	self:cl_updateGui()
	self.network:sendToServer( "sv_n_setDirectOnly", data.directOnly )
end

function WirelessVacuumPipe.cl_onClose( self )
	if self.cl.gui then self.cl.gui:destroy(); self.cl.gui = nil end
	self.cl.guiDirty = false
end

function WirelessVacuumPipe.cl_onModeButton( self, name )
	local modes = { ModeLink = "LINK", ModeSend = "SEND", ModeReceive = "RECEIVE" }
	local mode = modes[name]
	if mode then
		self.cl.data = self.cl.data or {}
		self.cl.data.mode = mode
		self.cl.data.explanation = MODE_EXPLANATIONS[mode]
		self.cl.guiDirty = true
		self:cl_updateGui()
		self.network:sendToServer( "sv_n_setMode", mode )
	end
end

function WirelessVacuumPipe.cl_n_applyAuthoritativeStatus( self, data )
	self.cl.data = data or self.cl.data
	self.cl.guiDirty = true
	self:cl_updateGui()
end

function WirelessVacuumPipe.cl_updateGui( self )
	if not self.cl.gui then self.cl.guiDirty = false; return end
	if not self.cl.gui:isActive() then self.cl.guiDirty = true; return end
	local data = self.cl.data or {}
	self.cl.gui:setButtonState( "ModeLink", data.mode == "LINK" )
	self.cl.gui:setButtonState( "ModeSend", data.mode == "SEND" )
	self.cl.gui:setButtonState( "ModeReceive", data.mode == "RECEIVE" )
	local directional = data.mode == "SEND" or data.mode == "RECEIVE"
	self.cl.gui:setVisible( "ScopeLabel", directional )
	self.cl.gui:setVisible( "ScopeButton", directional )
	self.cl.gui:setVisible( "LinkScopeHint", not directional )
	self.cl.gui:setButtonState( "ScopeButton", data.directOnly ~= false )
	self.cl.gui:setText( "ScopeButton", data.directOnly ~= false and "DIRECT CONTAINER ONLY" or "ENTIRE PIPE NETWORK" )
	self.cl.gui:setText( "ModeValue", data.mode or "LINK" )
	local status = data.state or "WIRELESS MANAGER UNAVAILABLE"
	self.cl.gui:setText( "StatusValue", status )
	self.cl.gui:setText( "ChannelValue", "#" .. tostring( data.channel or "DF7F01FF" ):sub( 1, 6 ) )
	self.cl.gui:setText( "EndpointValue", tostring( data.matchingCount or 0 ) )
	self.cl.gui:setText( "WorldValue", data.worldLabel or "UNKNOWN WORLD" )
	self.cl.gui:setText( "Explanation", data.explanation or MODE_EXPLANATIONS[data.mode or "LINK"] )
	self.cl.gui:setText( "RemoteWorlds", formatMatchingWorlds( data.worlds ) )
	local ok, color = pcall( function() return sm.color.new( tostring( data.channel or "DF7F01FF" ) ) end )
	if ok then self.cl.gui:setColor( "ChannelSwatch", color ) end
	local statusOk, statusColor = pcall( function() return sm.color.new( STATUS_COLORS[status] or "F2B134FF" ) end )
	if statusOk then self.cl.gui:setColor( "StatusLamp", statusColor ) end
	self.cl.guiDirty = false
end
