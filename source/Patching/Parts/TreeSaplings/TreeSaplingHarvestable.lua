dofile "$SURVIVAL_DATA/Scripts/util.lua"
dofile "$SURVIVAL_DATA/Scripts/game/survival_items.lua"

local PotUuid = sm.uuid.new( "cec1d36c-b48a-43d2-9667-5e53b62dd4c5" )
local FertilizerUuid = sm.uuid.new( "ac0b5b0a-14e1-4b31-8944-0a351fbfcc67" )
local SaveIntervalTicks = 40
local MatureRecheckTicks = 400
local FertilizedGrowthMultiplier = 2.5
local LooseLootHarvestables = {
	["97fe0cf2-0591-4e98-9beb-9186f4fd83c8"] = true,
	["d1d56712-a3f0-4af8-bb53-7ad6cb37d34b"] = true,
	["282f332e-eb95-4553-b711-4a027e92391d"] = true
}

-- Saplings are deliberately allowed to mature as a grove. Native tree
-- harvestables and sibling planted pots are vegetation, not roof/creation
-- obstructions. Placement spacing still protects their trunk centers.
local GrowthFriendlyHarvestables = {
	["26427da8-4848-4f18-9786-2f75db6fd772"] = true,
	["99a01345-f1e6-404e-b669-7e6d805bae3a"] = true,
	["e5e3d28f-bdff-4862-92b9-7fc9ce688643"] = true,
	["c4ea19d3-2469-4059-9f13-3ddb4f7e0b79"] = true,
	["711c3e72-7ba1-4424-ae70-c13d23afe818"] = true,
	["a7aa52af-4276-4b2d-af44-36bc41864e04"] = true,
	["91ec04ea-9bf7-4a9d-bb7f-3d0125ff78c7"] = true,
	["4d482999-98b7-4023-a149-d47be709b8f7"] = true,
	["3db0a60d-8668-4c8a-8dd2-f5ceb294977e"] = true,
	["73f968f0-d3a3-4334-86a8-a90203a3a56d"] = true,
	["86324c5b-e97a-41f6-aa2c-7c6462f1f2e7"] = true,
	["27aa53ea-1e09-4251-a284-437f93850409"] = true,
	["8411caba-63db-4b93-ad67-7ae8e350d360"] = true,
	["1cb503a4-9306-412f-9e13-371bc634af60"] = true,
	["fa864e51-67db-4ac9-823b-cfbdf523375d"] = true
}

-- This token survives cell unloads but changes when a new game session starts.
-- Persisting it prevents closed-game time from being mistaken for active growth.
ScrapLabTreeSaplingSessionId = ScrapLabTreeSaplingSessionId or tostring( {} )

ScrapLabChemicalFertilizableHarvestables = ScrapLabChemicalFertilizableHarvestables or {}
ScrapLabChemicalFertilizableHarvestables["26427da8-4848-4f18-9786-2f75db6fd772"] = true
ScrapLabChemicalFertilizableHarvestables["99a01345-f1e6-404e-b669-7e6d805bae3a"] = true
ScrapLabChemicalFertilizableHarvestables["e5e3d28f-bdff-4862-92b9-7fc9ce688643"] = true

TreeSaplingHarvestable = class( nil )

local function chooseVariant( variants )
	return variants[math.random( 1, #variants )]
end

local function anyEntries( value )
	return value and next( value ) ~= nil
end

function TreeSaplingHarvestable.server_onCreate( self )
	self.sv = {}
	local now = sm.game.getServerTick()
	self.sv.saved = self.storage:load() or {}
	self.sv.saved.progress = math.max( 0, tonumber( self.sv.saved.progress ) or 0 )
	self.sv.saved.fertilized = self.sv.saved.fertilized == true
	self.sv.saved.treeUuid = self.sv.saved.treeUuid or chooseVariant( self.data.treeVariants )
	self.sv.saved.yaw = tonumber( self.sv.saved.yaw ) or math.random() * math.pi * 2
	if self.sv.saved.sessionId ~= ScrapLabTreeSaplingSessionId then
		self.sv.saved.lastTick = now
	else
		self.sv.saved.lastTick = tonumber( self.sv.saved.lastTick ) or now
		if now < self.sv.saved.lastTick then self.sv.saved.lastTick = now end
	end
	self.sv.saved.sessionId = ScrapLabTreeSaplingSessionId
	self.sv.nextSave = now + SaveIntervalTicks
	self.sv.nextMatureCheck = now + ( self.harvestable.id % MatureRecheckTicks )
	self.sv.blocked = false
	self.sv.converting = self.sv.saved.converting == true
	if self.sv.converting and self:sv_recoverConversion() then return end
	self:sv_sync( false )
end

function TreeSaplingHarvestable.sv_recoverConversion( self )
	local contacts = sm.physics.getSphereContacts( self.harvestable.worldPosition, 1.0,
		self.harvestable:getWorld(), sm.physics.filter.harvestable )
	for _, harvestable in ipairs( contacts.harvestables or {} ) do
		if harvestable.id ~= self.harvestable.id and
			tostring( harvestable.uuid ) == self.sv.saved.treeUuid then
			self.harvestable:destroy()
			return true
		end
	end
	-- The process stopped after recording intent but before the native tree was
	-- created. Clearing the receipt makes the mature sapling safe to retry.
	self.sv.converting = false
	self.sv.saved.converting = false
	self:sv_save()
	return false
end

function TreeSaplingHarvestable.sv_save( self )
	self.storage:save( self.sv.saved )
end

function TreeSaplingHarvestable.sv_sync( self, blocked )
	local remaining = math.max( 0, self.data.growthTicks - self.sv.saved.progress )
	if remaining > 0 then
		self.sv.blocked = false
	elseif blocked ~= nil then
		self.sv.blocked = blocked == true
	end
	self.network:setClientData( {
		size = self.data.size,
		itemUuid = self.data.itemUuid,
		color = self.data.color,
		scale = self.data.scale,
		fertilized = self.sv.saved.fertilized,
		growthTicks = self.data.growthTicks,
		remainingTicks = remaining,
		mature = remaining <= 0,
		blocked = self.sv.blocked == true
	} )
end

function TreeSaplingHarvestable.server_onFixedUpdate( self, timeStep )
	if self.sv.converting then return end
	local now = sm.game.getServerTick()
	local elapsed = math.max( 0, now - self.sv.saved.lastTick )
	self.sv.saved.lastTick = now
	if self.sv.saved.progress < self.data.growthTicks and elapsed > 0 then
		self.sv.saved.progress = math.min( self.data.growthTicks,
			self.sv.saved.progress + elapsed *
			( self.sv.saved.fertilized and FertilizedGrowthMultiplier or 1 ) )
	end
	if now >= self.sv.nextSave then
		self.sv.nextSave = now + SaveIntervalTicks
		self:sv_save()
		self:sv_sync( nil )
	end
	if self.sv.saved.progress >= self.data.growthTicks and now >= self.sv.nextMatureCheck then
		self.sv.nextMatureCheck = now + MatureRecheckTicks
		local clear = self:sv_hasGrowthClearance()
		if clear then self:sv_growTree() else self:sv_sync( true ); self:sv_save() end
	end
end

function TreeSaplingHarvestable.server_onDestroy( self )
	if self.sv and self.sv.saved and not self.sv.converting then self:sv_save() end
end

function TreeSaplingHarvestable.sv_hasGrowthClearance( self )
	local world = self.harvestable:getWorld()
	local base = self.harvestable.worldPosition
	local mask = bit.bor( sm.physics.filter.dynamicBody, sm.physics.filter.staticBody,
		sm.physics.filter.terrainAsset, sm.physics.filter.voxelTerrain,
		sm.physics.filter.character )
	local rayHit = sm.physics.raycast( base + sm.vec3.new( 0, 0, 0.35 ),
		base + sm.vec3.new( 0, 0, self.data.clearanceHeight ),
		self.harvestable, mask, world )
	if rayHit then return false end
	for index = 0, 5 do
		local fraction = index / 5
		local center = base + sm.vec3.new( 0, 0, self.data.clearanceHeight * fraction )
		local contacts = sm.physics.getSphereContacts( center,
			math.max( 0.75, self.data.clearanceRadius * math.sin( fraction * math.pi ) ), world )
		if anyEntries( contacts.bodies ) or anyEntries( contacts.characters ) or
			anyEntries( contacts.units ) or anyEntries( contacts.terrainAssets ) then
			return false
		end
		for _, harvestable in ipairs( contacts.harvestables or {} ) do
			local uuid = tostring( harvestable.uuid )
			if harvestable.id ~= self.harvestable.id and
				not LooseLootHarvestables[uuid] and
				not GrowthFriendlyHarvestables[uuid] then return false end
		end
	end
	return true
end

function TreeSaplingHarvestable.sv_growTree( self )
	if self.sv.converting then return end
	self.sv.converting = true
	self.sv.saved.converting = true
	self:sv_save()
	local position = self.harvestable.worldPosition
	-- Native tree harvestables are authored Y-up. Match the proven
	-- /spawntree convention by standing that axis on world Z before yawing.
	local standUpRotation = sm.vec3.getRotation(
		sm.vec3.new( 0, 1, 0 ), sm.vec3.new( 0, 0, 1 ) )
	local yawRotation = sm.quat.angleAxis(
		self.sv.saved.yaw, sm.vec3.new( 0, 1, 0 ) )
	local rotation = standUpRotation * yawRotation
	local tree = sm.harvestable.createHarvestable( sm.uuid.new( self.sv.saved.treeUuid ), position, rotation )
	if tree then
		sm.effect.playEffect( "Plants - SoilbagUse", position, nil, rotation )
		self.harvestable:destroy()
	else
		self.sv.converting = false
		self.sv.saved.converting = false
		self:sv_save()
		self:sv_sync( true )
	end
end

function TreeSaplingHarvestable.sv_e_fertilize( self, params )
	if self.sv.saved.fertilized or type( params ) ~= "table" or not params.playerInventory then return end
	sm.container.beginTransaction()
	sm.container.spendFromSlot( params.playerInventory, params.slot, FertilizerUuid, 1, true )
	if sm.container.endTransaction() then self:sv_applyFertilizer() end
end

function TreeSaplingHarvestable.sv_e_raidRescueChemicalFertilize( self )
	if not self.sv.saved.fertilized then self:sv_applyFertilizer() end
end

-- BaseWorld also dispatches ordinary water events through the shared target
-- set. Saplings deliberately ignore water; only the chemical event fertilizes.
function TreeSaplingHarvestable.sv_e_onWatered( self )
end

function TreeSaplingHarvestable.sv_applyFertilizer( self )
	self.sv.saved.fertilized = true
	self:sv_save()
	self:sv_sync( nil )
	sm.effect.playEffect( "Plants - Fertilizer_impact", self.harvestable.worldPosition )
end

function TreeSaplingHarvestable.sv_n_uproot( self, _, player )
	if not player or not player.character or
		( player.character.worldPosition - self.harvestable.worldPosition ):length() > 6.0 then return end
	sm.container.beginTransaction()
	sm.container.collect( player:getInventory(), sm.uuid.new( self.data.itemUuid ), 1, true )
	if sm.container.endTransaction() then
		self.sv.converting = true
		self.harvestable:destroy()
	else self.network:sendToClient( player, "cl_n_inventoryFull" ) end
end

function TreeSaplingHarvestable.client_onCreate( self )
	self.cl = { growthFraction = 0, appliedVisualScale = -1 }
	self.harvestable.clientPublicData = self.harvestable.clientPublicData or {}
	self.harvestable.clientPublicData.fertilizer = false
	self.cl.effect = sm.effect.createEffect( "ShapeRenderable" )
	self.cl.effect:setParameter( "uuid", PotUuid )
	self.cl.effect:setPosition( self.harvestable.worldPosition )
	self.cl.effect:setRotation( self.harvestable.worldRotation )
end

function TreeSaplingHarvestable.client_onClientDataUpdate( self, data )
	self.cl.data = data
	local growthTicks = math.max( 1, tonumber( data.growthTicks ) or 1 )
	self.cl.growthFraction = math.max( 0, math.min( 1,
		1 - ( tonumber( data.remainingTicks ) or growthTicks ) / growthTicks ) )
	self.harvestable.clientPublicData.fertilizer = data.fertilized == true
	self.cl.effect:setParameter( "Color", sm.color.new( data.color ) )
	self:cl_updateVisualGrowth( true )
	if not self.cl.effect:isPlaying() then self.cl.effect:start() end
end

function TreeSaplingHarvestable.cl_updateVisualGrowth( self, force )
	if not self.cl or not self.cl.data then return end
	local progress = math.max( 0, math.min( 1, self.cl.growthFraction or 0 ) )
	local eased = progress * progress * ( 3 - 2 * progress )
	-- Start clearly young but recognizable, then smoothly reach the authored
	-- pot size as server-backed progress advances.
	local visualScale = ( 0.45 + 0.55 * eased ) * self.cl.data.scale
	if force or math.abs( visualScale - self.cl.appliedVisualScale ) >= 0.002 then
		self.cl.appliedVisualScale = visualScale
		self.cl.effect:setScale( sm.vec3.one() * 0.25 * visualScale )
	end
end

function TreeSaplingHarvestable.client_onUpdate( self, deltaTime )
	if not self.cl or not self.cl.data or self.cl.data.mature then return end
	local growthTicks = math.max( 1, tonumber( self.cl.data.growthTicks ) or 1 )
	local speed = self.cl.data.fertilized and FertilizedGrowthMultiplier or 1
	self.cl.growthFraction = math.min( 1,
		( self.cl.growthFraction or 0 ) + deltaTime * 40 * speed / growthTicks )
	self:cl_updateVisualGrowth( false )
end

function TreeSaplingHarvestable.client_onDestroy( self )
	if self.cl and self.cl.effect then self.cl.effect:stop(); self.cl.effect:destroy() end
end

function TreeSaplingHarvestable.client_canInteract( self )
	if not self.cl or not self.cl.data then return false end
	local data = self.cl.data
	local status
	if data.mature and data.blocked then status = "MATURE - NEEDS CLEAR SPACE"
	elseif data.mature then status = "READY TO GROW"
	else
		local seconds = math.ceil( data.remainingTicks /
			( data.fertilized and 40 * FertilizedGrowthMultiplier or 40 ) )
		status = string.format( "%d:%02d REMAINING%s", math.floor( seconds / 60 ), seconds % 60,
			data.fertilized and " - FERTILIZED" or "" )
	end
	sm.gui.setInteractionText( data.size:upper() .. " TREE SAPLING - " .. status,
		sm.gui.getKeyBinding( "Use", true ), "UPROOT SAPLING" )
	return true
end

function TreeSaplingHarvestable.client_onInteract( self, character, state )
	if state then self.network:sendToServer( "sv_n_uproot" ) end
end

function TreeSaplingHarvestable.cl_n_inventoryFull( self )
	sm.gui.displayAlertText( "INVENTORY FULL - SAPLING WAS NOT REMOVED", 2.5 )
end
