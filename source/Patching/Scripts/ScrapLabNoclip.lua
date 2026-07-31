-- SCRAPLAB DEVELOPER COMMANDS NOCLIP MODULE v7
-- Damped flight with capsule-swept collision bypass.

local ScrapLabNoclipUp = sm.vec3.new( 0, 0, 1 )
local ScrapLabNoclipNormalSpeed = 20
local ScrapLabNoclipSprintSpeed = 36
local ScrapLabNoclipMaximumSpeed = ScrapLabNoclipSprintSpeed
local ScrapLabNoclipInputInterval = 0.025
local ScrapLabNoclipSweepScale = 0.86
local ScrapLabNoclipTargetResponse = 7.5
local ScrapLabNoclipPhysicsResponse = 12.0
local ScrapLabNoclipMaximumDeltaVelocity = 2.5
local ScrapLabNoclipGravity = GRAVITY or 10.0

g_scrapLabNoclipActivePlayers = g_scrapLabNoclipActivePlayers or {}
g_scrapLabNoclipEntries = g_scrapLabNoclipEntries or {}

local function scrapLabHasNoclipPlayers( players )
	return players ~= nil and next( players ) ~= nil
end

local function scrapLabSafeDirection( direction, fallback )
	if direction and direction:length2() > 0.0001 then
		return direction:normalize()
	end
	return fallback or sm.vec3.new( 0, 1, 0 )
end

-- Compatibility fallback for builds that do not instantiate the hidden
-- ScrapLab auto-tool. It is deliberately secondary; normal installations use
-- ScrapLabNoclipInputTool.lua and do not depend on the Lift implementation.
local function scrapLabInstallLiftInputFallback()
	if g_scrapLabNoclipToolInput then return true end
	if SurvivalLift == nil or SurvivalLift.client_onUpdate == nil then return false end
	if g_scrapLabNoclipLiftClass == SurvivalLift and SurvivalLift.client_onUpdate == g_scrapLabNoclipLiftWrapper then return true end

	local originalUpdate = SurvivalLift.client_onUpdate
	local wrapper = function( self, dt )
		originalUpdate( self, dt )
		if self.tool and self.tool:isLocal() then
			g_scrapLabNoclipToolInput = {
				move = self.tool:getRelativeMoveDirection(),
				direction = self.tool:getDirection(),
				speed = self.tool:getMovementSpeedFraction(),
				sprinting = self.tool:isSprinting()
			}
		end
	end

	g_scrapLabNoclipLiftClass = SurvivalLift
	g_scrapLabNoclipLiftWrapper = wrapper
	SurvivalLift.client_onUpdate = wrapper
	return true
end

local function scrapLabInstallTumbleGuard()
	if BasePlayer == nil or BasePlayer.sv_startTumble == nil then return false end
	if g_scrapLabOriginalStartTumble then return true end

	g_scrapLabOriginalStartTumble = BasePlayer.sv_startTumble
	function BasePlayer.sv_startTumble( self, tumbleTickTime )
		if self.player and g_scrapLabNoclipActivePlayers[self.player.id] then
			if self.player.character and self.player.character:isTumbling() then
				self.player.character:setTumbling( false )
			end
			return false
		end
		return g_scrapLabOriginalStartTumble( self, tumbleTickTime )
	end
	return true
end

-- Physics impulses must run from a world-bound script. SurvivalGame is a
-- GameClass ("no world"), while BasePlayer is the PlayerClass that owns the
-- character and is therefore allowed to drive its physics.
local ScrapLabOriginalBasePlayerFixedUpdate = BasePlayer.server_onFixedUpdate
function BasePlayer.server_onFixedUpdate( self, timeStep )
	ScrapLabOriginalBasePlayerFixedUpdate( self, timeStep )
	local player = self.player
	local entry = player and g_scrapLabNoclipEntries[player.id]
	if not entry then return end

	local character = player:getCharacter()
	if not character or not sm.exists( character ) or character ~= entry.character or character:getWorld() ~= entry.world then
		return
	end

	g_scrapLabNoclipActivePlayers[player.id] = true
	if character:isTumbling() then character:setTumbling( false ) end

	local targetVelocity = entry.velocity or sm.vec3.zero()
	local targetAlpha = math.min( ScrapLabNoclipTargetResponse * timeStep, 1.0 )
	local velocity = entry.smoothedVelocity or sm.vec3.zero()
	velocity = velocity + ( targetVelocity - velocity ) * targetAlpha
	entry.smoothedVelocity = velocity
	local position = character:getWorldPosition()
	local nextPosition = position + velocity * timeStep
	local blocked = false
	if velocity:length2() > 0.0001 then
		-- A slightly inset movement probe avoids treating ordinary floor or
		-- ceiling contact as an obstruction. Safe exit uses the full capsule.
		local radius = math.max( character:getRadius() * ScrapLabNoclipSweepScale, 0.1 )
		local height = character:getHeight()
		local castHeight = math.max( height * ScrapLabNoclipSweepScale - 2 * radius, 0.1 )
		local centerOffset = ScrapLabNoclipUp * ( height * 0.5 )
		blocked = sm.physics.capsulecast( position + centerOffset, nextPosition + centerOffset, radius, castHeight, character, sm.physics.filter.default, entry.world )
	end

	if blocked then
		-- Cross only the obstructed slice with a short authoritative jump.
		character:setWorldPosition( nextPosition )
	else
		-- A damped controller cannot reverse past its target in one tick. The
		-- gravity feed-forward holds altitude without enabling the character's
		-- spring-like climbing controller.
		local physicsAlpha = math.min( ScrapLabNoclipPhysicsResponse * timeStep, 1.0 )
		local correction = ( velocity - character:getVelocity() ) * physicsAlpha
		correction = correction + ScrapLabNoclipUp * ( ScrapLabNoclipGravity * timeStep )
		if correction:length2() > ScrapLabNoclipMaximumDeltaVelocity * ScrapLabNoclipMaximumDeltaVelocity then
			correction = correction:normalize() * ScrapLabNoclipMaximumDeltaVelocity
		end
		if correction:length2() > 0.000001 then
			sm.physics.applyImpulse( character, correction * character.mass )
		end
	end
end

local ScrapLabOriginalServerOnCreate = SurvivalGame.server_onCreate
function SurvivalGame.server_onCreate( self )
	ScrapLabOriginalServerOnCreate( self )
	g_scrapLabNoclipEntries = {}
	self.sv.scrapLabNoclipPlayers = g_scrapLabNoclipEntries
	self.sv.scrapLabNoclipGodBase = nil
	scrapLabInstallTumbleGuard()
end

local ScrapLabOriginalClientOnCreate = SurvivalGame.client_onCreate
function SurvivalGame.client_onCreate( self )
	ScrapLabOriginalClientOnCreate( self )
	self.cl.scrapLabNoclip = nil
	self.cl.scrapLabNoclipInputWait = 0
end

local ScrapLabOriginalBindChatCommands = SurvivalGame.bindChatCommands
function SurvivalGame.bindChatCommands( self )
	ScrapLabOriginalBindChatCommands( self )
	if sm.isHost or g_survivalDev then
		sm.game.bindChatCommand( "/fly", {}, "cl_onChatCommand", "Toggle collision-free flight and temporary god mode" )
	end
end

local ScrapLabOriginalClientChatCommand = SurvivalGame.cl_onChatCommand
function SurvivalGame.cl_onChatCommand( self, params )
	if params[1] == "/fly" then
		self.network:sendToServer( "sv_scrapLabToggleNoclip" )
		return
	end
	ScrapLabOriginalClientChatCommand( self, params )
end

function SurvivalGame.sv_scrapLabRestoreGodMode( self )
	if not scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) then
		if self.sv.scrapLabNoclipGodBase ~= nil then
			g_godMode = self.sv.scrapLabNoclipGodBase
		end
		self.sv.scrapLabNoclipGodBase = nil
	end
end

function SurvivalGame.sv_scrapLabStopNoclip( self, player, notifyClient )
	local entries = self.sv.scrapLabNoclipPlayers
	local entry = entries and entries[player.id]
	if not entry then return end

	local character = player:getCharacter()
	if character and sm.exists( character ) and character == entry.character then
		if character:isTumbling() then character:setTumbling( false ) end
	end
	g_scrapLabNoclipActivePlayers[player.id] = nil
	entries[player.id] = nil
	self:sv_scrapLabRestoreGodMode()
	if notifyClient then
		self.network:sendToClient( player, "cl_scrapLabNoclipState", false )
	end
end

function SurvivalGame.sv_scrapLabToggleNoclip( self, _, player )
	local character = player and player:getCharacter()
	if not character or not sm.exists( character ) then return end
	self.sv.scrapLabNoclipPlayers = self.sv.scrapLabNoclipPlayers or {}
	local entry = self.sv.scrapLabNoclipPlayers[player.id]

	if entry then
		if character ~= entry.character or character:getWorld() ~= entry.world then
			self:sv_scrapLabStopNoclip( player, true )
			return
		end
		local position = character:getWorldPosition()
		local radius = character:getRadius()
		local height = character:getHeight()
		local castHeight = math.max( height - 2 * radius, 0.1 )
		local castCenter = position + ScrapLabNoclipUp * ( height * 0.5 )
		local blocked = sm.physics.capsulecast( castCenter, castCenter, radius, castHeight, character, sm.physics.filter.default, entry.world )
		if blocked then
			self.network:sendToClient( player, "client_showMessage", "FLIGHT: Move clear of solid objects before disabling" )
			return
		end
		self:sv_scrapLabStopNoclip( player, true )
		self.network:sendToClient( player, "client_showMessage", "FLIGHT: Off" )
		return
	end

	if character:isSeated() or character:isDowned() then
		self.network:sendToClient( player, "client_showMessage", "FLIGHT: Leave the seat or revive before enabling" )
		return
	end
	if not scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) then
		self.sv.scrapLabNoclipGodBase = g_godMode == true
	end
	g_godMode = true
	if character:isTumbling() then character:setTumbling( false ) end
	g_scrapLabNoclipActivePlayers[player.id] = true
	self.sv.scrapLabNoclipPlayers[player.id] = {
		player = player,
		character = character,
		world = character:getWorld(),
		velocity = sm.vec3.zero(),
		smoothedVelocity = sm.vec3.zero()
	}
	self.network:sendToClient( player, "cl_scrapLabNoclipState", true )
	self.network:sendToClient( player, "client_showMessage", "FLIGHT: On - hold Shift for high speed" )
end

function SurvivalGame.sv_scrapLabNoclipInput( self, velocity, player )
	local entry = self.sv.scrapLabNoclipPlayers and self.sv.scrapLabNoclipPlayers[player.id]
	if entry and velocity then
		if velocity:length2() > ScrapLabNoclipMaximumSpeed * ScrapLabNoclipMaximumSpeed then
			velocity = velocity:normalize() * ScrapLabNoclipMaximumSpeed
		end
		entry.velocity = velocity
	end
end

function SurvivalGame.sv_scrapLabUpdateNoclip( self, timeStep )
	local entries = self.sv.scrapLabNoclipPlayers
	if not scrapLabHasNoclipPlayers( entries ) then return end
	g_godMode = true
	scrapLabInstallTumbleGuard()
	local stopped = {}

	for _, entry in pairs( entries ) do
		local player = entry.player
		local character = player and player:getCharacter()
		if not character or not sm.exists( character ) or character ~= entry.character or character:getWorld() ~= entry.world then
			stopped[#stopped + 1] = player
		else
			g_scrapLabNoclipActivePlayers[player.id] = true
		end
	end

	for _, player in ipairs( stopped ) do
		if player then self:sv_scrapLabStopNoclip( player, true ) end
	end
end

local ScrapLabOriginalServerFixedUpdate = SurvivalGame.server_onFixedUpdate
function SurvivalGame.server_onFixedUpdate( self, timeStep )
	ScrapLabOriginalServerFixedUpdate( self, timeStep )
	self:sv_scrapLabUpdateNoclip( timeStep )
end

function SurvivalGame.cl_scrapLabNoclipState( self, enabled )
	local player = sm.localPlayer.getPlayer()
	local character = player and player:getCharacter()
	if character and sm.exists( character ) then
		character:setMovementWeights( enabled and 0 or 1, enabled and 0 or 1 )
	end
	self.cl.scrapLabNoclip = enabled and { sendTimer = 0 } or nil
end

function SurvivalGame.cl_scrapLabUpdateNoclip( self, dt )
	local state = self.cl.scrapLabNoclip
	if not state then return end
	local input = g_scrapLabNoclipToolInput
	if not input or not input.move then
		self.cl.scrapLabNoclipInputWait = self.cl.scrapLabNoclipInputWait + dt
		if self.cl.scrapLabNoclipInputWait >= 0.5 then
			scrapLabInstallLiftInputFallback()
		end
		return
	end
	self.cl.scrapLabNoclipInputWait = 0

	local move = input.move
	if move:length2() > 1 then move = move:normalize() end
	local direction = scrapLabSafeDirection( input.direction, sm.localPlayer.getDirection() )
	local flatForward = scrapLabSafeDirection( sm.vec3.new( direction.x, direction.y, 0 ), sm.vec3.new( 0, 1, 0 ) )
	local right = sm.vec3.new( flatForward.y, -flatForward.x, 0 )
	local desired = direction * move.y + right * move.x
	if desired:length2() > 1 then desired = desired:normalize() end
	local speedFraction = math.max( math.min( input.speed or 0.5, 1.0 ), 0.25 )
	local flightSpeed = input.sprinting
		and ScrapLabNoclipSprintSpeed
		or ( ScrapLabNoclipNormalSpeed * speedFraction )
	local velocity = desired * flightSpeed

	state.sendTimer = state.sendTimer + dt
	if state.sendTimer >= ScrapLabNoclipInputInterval then
		state.sendTimer = 0
		self.network:sendToServer( "sv_scrapLabNoclipInput", velocity )
	end
end

local ScrapLabOriginalClientUpdate = SurvivalGame.client_onUpdate
function SurvivalGame.client_onUpdate( self, dt )
	ScrapLabOriginalClientUpdate( self, dt )
	self:cl_scrapLabUpdateNoclip( dt )
end

local ScrapLabOriginalServerPlayerLeft = SurvivalGame.server_onPlayerLeft
function SurvivalGame.server_onPlayerLeft( self, player )
	if self.sv.scrapLabNoclipPlayers and self.sv.scrapLabNoclipPlayers[player.id] then
		self:sv_scrapLabStopNoclip( player, false )
	end
	ScrapLabOriginalServerPlayerLeft( self, player )
end

local ScrapLabOriginalServerOnDestroy = SurvivalGame.server_onDestroy
function SurvivalGame.server_onDestroy( self )
	for playerId, _ in pairs( self.sv.scrapLabNoclipPlayers or {} ) do
		g_scrapLabNoclipActivePlayers[playerId] = nil
	end
	if scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) and self.sv.scrapLabNoclipGodBase ~= nil then
		g_godMode = self.sv.scrapLabNoclipGodBase
	end
	ScrapLabOriginalServerOnDestroy( self )
end
