dofile( "$SURVIVAL_DATA/Scripts/game/managers/RaidManager.lua" )

RaidDetector = class( nil )
RaidDetector.maxParentCount = 0
RaidDetector.maxChildCount = 255
RaidDetector.connectionInput = sm.interactable.connectionType.none
RaidDetector.connectionOutput = sm.interactable.connectionType.logic
RaidDetector.colorNormal = sm.color.new( 0x777777ff )
RaidDetector.colorHighlight = sm.color.new( 0xeeeeeeff )
RaidDetector.poseWeightCount = 1

local DetectionRadiusSquared = 256 * 256
local ScanIntervalTicks = 10
local UnfoldSpeed = 15
local UVSpeed = 5

local function HasRaidInRange( shape )
	if not g_raidManager or not g_raidManager.sv or not g_raidManager.sv.saved then
		return false
	end

	local worldRaids = g_raidManager.sv.saved.worldRaids
	if not worldRaids then
		return false
	end

	local world = shape:getWorld()
	local raids = world and worldRaids[world.id] or nil
	if not raids then
		return false
	end

	local position = shape.worldPosition
	for _, raid in pairs( raids ) do
		if raid.center and raid.attackData then
			local offset = raid.center - position
			if offset:length2() <= DetectionRadiusSquared then
				return true
			end
		end
	end

	return false
end

function RaidDetector.server_onCreate( self )
	self.sv = { scanTicks = 0, active = false }
	self.interactable.active = false
	self:sv_updateOutput()
end

function RaidDetector.server_onFixedUpdate( self )
	self.sv.scanTicks = self.sv.scanTicks + 1
	if self.sv.scanTicks >= ScanIntervalTicks then
		self.sv.scanTicks = 0
		self:sv_updateOutput()
	end
end

function RaidDetector.sv_updateOutput( self )
	local active = HasRaidInRange( self.shape )
	if active ~= self.sv.active then
		self.sv.active = active
		self.interactable.active = active
	end
end

function RaidDetector.client_onCreate( self )
	self.cl = { unfoldWeight = 0, uvFrame = 0 }
	self.interactable:setUvFrameIndex( 0 )
end

function RaidDetector.client_onUpdate( self, dt )
	if self.cl.unfoldWeight < 1 then
		self.cl.unfoldWeight = math.min(
			self.cl.unfoldWeight + dt * UnfoldSpeed, 1 )
		self.interactable:setPoseWeight( 0, self.cl.unfoldWeight )
	end

	if self.interactable.active then
		self.cl.uvFrame = ( self.cl.uvFrame + dt * UVSpeed ) % 4
		self.interactable:setUvFrameIndex( math.floor( self.cl.uvFrame ) )
	elseif self.cl.uvFrame ~= 0 then
		self.cl.uvFrame = 0
		self.interactable:setUvFrameIndex( 0 )
	end
end
