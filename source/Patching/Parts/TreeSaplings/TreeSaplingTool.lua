dofile "$GAME_DATA/Scripts/game/AnimationUtil.lua"
dofile "$SURVIVAL_DATA/Scripts/util.lua"
dofile "$SURVIVAL_DATA/Scripts/game/survival_items.lua"
dofile "$SURVIVAL_DATA/Scripts/ScrapLab/Parts/TreeSaplings/TreeSaplingVisual.generated.lua"

local PotUuid = sm.uuid.new( "cec1d36c-b48a-43d2-9667-5e53b62dd4c5" )
local InvalidColor = sm.color.new( 0xd02525ff )
local MaxSaplingRadius = 1.25
local ServerPlacementRetryTicks = 2
local PlacementMask = bit.bor(
	sm.physics.filter.dynamicBody,
	sm.physics.filter.waterArea,
	sm.physics.filter.terrainAsset,
	sm.physics.filter.harvestable,
	sm.physics.filter.staticBody,
	sm.physics.filter.voxelTerrain )

local Config = {
	small = {
		item = sm.uuid.new( "790d34b8-f006-47e4-9ebc-49a84a68ed16" ),
		harvestable = sm.uuid.new( "26427da8-4848-4f18-9786-2f75db6fd772" ),
		spacingRadius = 0.75,
		height = 14.0,
		scale = 0.85,
		color = sm.color.new( 0x7eed56ff ),
		label = "SMALL"
	},
	medium = {
		item = sm.uuid.new( "33511c78-354b-4a60-af6b-778c427c47d5" ),
		harvestable = sm.uuid.new( "99a01345-f1e6-404e-b669-7e6d805bae3a" ),
		spacingRadius = 1.0,
		height = 20.0,
		scale = 1.0,
		color = sm.color.new( 0xe2db13ff ),
		label = "MEDIUM"
	},
	large = {
		item = sm.uuid.new( "c9413781-5a0e-4025-a2cb-bc2090803e50" ),
		harvestable = sm.uuid.new( "e5e3d28f-bdff-4862-92b9-7fc9ce688643" ),
		spacingRadius = 1.25,
		height = 24.0,
		scale = 1.15,
		color = sm.color.new( 0xdf7f00ff ),
		label = "LARGE"
	}
}

local SaplingHarvestables = {
	["26427da8-4848-4f18-9786-2f75db6fd772"] = 0.75,
	["99a01345-f1e6-404e-b669-7e6d805bae3a"] = 1.0,
	["e5e3d28f-bdff-4862-92b9-7fc9ce688643"] = 1.25
}

local LooseLootHarvestables = {
	["97fe0cf2-0591-4e98-9beb-9186f4fd83c8"] = true,
	["d1d56712-a3f0-4af8-bb53-7ad6cb37d34b"] = true,
	["282f332e-eb95-4553-b711-4a027e92391d"] = true
}

local function hasMajorPlacementContact( config, position, world )
	local contacts = sm.physics.getSphereContacts( position + sm.vec3.new( 0, 0, 0.75 ),
		config.spacingRadius, world, bit.bor( sm.physics.filter.dynamicBody,
			sm.physics.filter.staticBody, sm.physics.filter.terrainAsset,
			sm.physics.filter.harvestable ) )
	if contacts and ( next( contacts.bodies or {} ) or next( contacts.terrainAssets or {} ) ) then
		return true
	end
	for _, harvestable in ipairs( contacts and contacts.harvestables or {} ) do
		local uuid = tostring( harvestable.uuid )
		local otherRadius = SaplingHarvestables[uuid]
		if not otherRadius and not LooseLootHarvestables[uuid] then
			return true
		end
	end
	-- Query the complete possible mixed-size spacing envelope separately.
	-- The former client query used only this sapling's radius, so a green
	-- preview could miss a larger neighbour that the server later rejected.
	local saplingContacts = sm.physics.getSphereContacts( position,
		config.spacingRadius + MaxSaplingRadius + 0.25, world,
		sm.physics.filter.harvestable )
	for _, harvestable in ipairs( saplingContacts and saplingContacts.harvestables or {} ) do
		local otherRadius = SaplingHarvestables[tostring( harvestable.uuid )]
		if otherRadius and ( harvestable.worldPosition - position ):length() <
			config.spacingRadius + otherRadius then return true end
	end
	return false
end

-- Client preview and server commit both call this exact function. Keeping the
-- physics masks, origins, and clearance sizes in one place prevents a green
-- preview from being judged by a subtly different set of rules on the server.
local function getPlacementBlockReason( config, position, world )
	if sm.physics.sphereHasContact( position + sm.vec3.new( 0, 0, 0.25 ), 0.45,
		nil, nil, PlacementMask ) then
		return "THE PLANTING SPOT IS BLOCKED"
	end
	if hasMajorPlacementContact( config, position, world ) then
		return "KEEP THIS TREE AWAY FROM BUILDINGS AND OTHER PLANTS"
	end
	local roofHit = sm.physics.raycast( position + sm.vec3.new( 0, 0, 0.25 ),
		position + sm.vec3.new( 0, 0, config.height ), nil,
		bit.bor( sm.physics.filter.dynamicBody, sm.physics.filter.staticBody,
			sm.physics.filter.terrainAsset, sm.physics.filter.voxelTerrain ), world )
	if roofHit then return "NOT ENOUGH ROOM FOR THIS TREE" end
	return nil
end

TreeSaplingToolBase = class()

function TreeSaplingToolBase.cl_initialize( self, size )
	self.config = Config[size]
	self.effect = sm.effect.createEffect( "ShapeRenderable" )
	self.effect:setParameter( "uuid", PotUuid )
	self.effect:setParameter( "visualization", true )
	self.effect:setParameter( "Color", self.config.color )
	self.effect:setScale( sm.vec3.one() * 0.25 * self.config.scale )
	self:client_onRefresh()
end

function TreeSaplingToolBase.client_onRefresh( self )
	if self.tool:isEquipped() then
		-- Renderables must be installed before AnimationUtil queries the Bucket
		-- clips. Loading animations first produces missing FP animation info and
		-- can leave Scrap Mechanic showing the auto-tool item's world mesh.
		self:cl_updateRenderables()
		self:cl_loadAnimations()
	end
end

function TreeSaplingToolBase.cl_loadAnimations( self )
	self.tpAnimations = createTpAnimations( self.tool, {
		idle = { "bucket_idle", { looping = true } },
		use = { "bucket_use_full", { nextAnimation = "idle" } },
		pickup = { "bucket_pickup", { nextAnimation = "idle" } },
		putdown = { "bucket_putdown" }
	} )
	local movement = {
		idle = "bucket_idle", runFwd = "bucket_run",
		runBwd = "bucket_runbwd", sprint = "bucket_sprint_idle",
		sprintLeft = "bucket_sprint_left", sprintRight = "bucket_sprint_right",
		jump = "bucket_jump", jumpUp = "bucket_jump_up",
		jumpDown = "bucket_jump_down", land = "bucket_jump_land",
		landFwd = "bucket_jump_land_fwd", landBwd = "bucket_jump_land_bwd",
		landLeft = "bucket_jump_land_left", landRight = "bucket_jump_land_right",
		crouchIdle = "bucket_crouch_idle", crouchFwd = "bucket_crouch_run",
		crouchBwd = "bucket_crouch_runbwd"
	}
	for name, animation in pairs( movement ) do
		self.tool:setMovementAnimation( name, animation )
	end
	if self.tool:isLocal() then
		self.fpAnimations = createFpAnimations( self.tool, {
			idle = { "bucket_idle", { looping = true } },
			use = { "bucket_use_full", { nextAnimation = "idle" } },
			sprintInto = { "bucket_sprint_into", { nextAnimation = "sprintIdle", blendNext = 0.2 } },
			sprintIdle = { "bucket_sprint_idle", { looping = true } },
			sprintExit = { "bucket_sprint_exit", { nextAnimation = "idle", blendNext = 0 } },
			jump = { "bucket_jump", { nextAnimation = "idle" } },
			land = { "bucket_jump_land", { nextAnimation = "idle" } },
			equip = { "bucket_pickup", { nextAnimation = "idle" } },
			unequip = { "bucket_putdown" }
		} )
	end
	setTpAnimation( self.tpAnimations, "idle", 5.0 )
	self.blendTime = 0.2
end

function TreeSaplingToolBase.cl_updateRenderables( self )
	ScrapLabTreeSaplingVisual.apply( self.tool, self.config.color )
end

function TreeSaplingToolBase.client_onDestroy( self )
	if self.effect then self.effect:stop(); self.effect:destroy() end
end

function TreeSaplingToolBase.client_onUpdate( self, dt )
	if self.tool:isLocal() and self.fpAnimations then
		updateFpAnimations( self.fpAnimations, self.equipped, dt )
	end
	if not self.equipped then
		if self.wantEquipped then self.wantEquipped = false; self.equipped = true end
		return
	end
	local crouchWeight = self.tool:isCrouching() and 1.0 or 0.0
	local totalWeight = 0.0
	for name, animation in pairs( self.tpAnimations.animations ) do
		animation.time = animation.time + dt
		if name == self.tpAnimations.currentAnimation then
			animation.weight = math.min( animation.weight + self.tpAnimations.blendSpeed * dt, 1.0 )
			if animation.time >= animation.info.duration - self.blendTime then
				if name == "use" then setTpAnimation( self.tpAnimations, "idle", 10.0 )
				elseif name == "pickup" then setTpAnimation( self.tpAnimations, "idle", 0.001 )
				elseif animation.nextAnimation ~= "" then setTpAnimation( self.tpAnimations, animation.nextAnimation, 0.001 ) end
			end
		else
			animation.weight = math.max( animation.weight - self.tpAnimations.blendSpeed * dt, 0.0 )
		end
		totalWeight = totalWeight + animation.weight
	end
	totalWeight = totalWeight == 0 and 1.0 or totalWeight
	for name, animation in pairs( self.tpAnimations.animations ) do
		local weight = animation.weight / totalWeight
		if name == "idle" then self.tool:updateMovementAnimation( animation.time, weight )
		elseif animation.crouch then
			self.tool:updateAnimation( animation.info.name, animation.time, weight * ( 1.0 - crouchWeight ) )
			self.tool:updateAnimation( animation.crouch.name, animation.time, weight * crouchWeight )
		else self.tool:updateAnimation( animation.info.name, animation.time, weight ) end
	end
end

function TreeSaplingToolBase.cl_raycast( self )
	local world = sm.localPlayer.getWorld()
	if not world or not world.clientPublicData or not world.clientPublicData.allowSoilPlacement then
		return false, nil, nil, "SAPLINGS CANNOT BE PLANTED IN THIS WORLD"
	end
	local valid, result = sm.localPlayer.getLatestRaycast()
	if not valid or result.type ~= "terrainSurface" then
		return false, nil, nil, "AIM AT OPEN TERRAIN"
	end
	local point = result.pointWorld + result.normalWorld * 0.04
	if result.normalWorld.z < 0.96592583 then
		return false, point, result.normalWorld, "GROUND IS TOO STEEP"
	end
	local blockedReason = getPlacementBlockReason( self.config, point, world )
	if blockedReason then return false, point, result.normalWorld, blockedReason end
	return true, point, result.normalWorld, nil
end

function TreeSaplingToolBase.client_onEquippedUpdate( self, primaryState, secondaryState, forceBuildActive )
	if not self.tool:isLocal() then return false, false end
	if forceBuildActive then self.effect:stop(); return false, false end
	local valid, position, normal, reason = self:cl_raycast()
	if not position then self.effect:stop(); sm.gui.setInteractionText( reason or "AIM AT OPEN TERRAIN" ); return false, false end
	self.effect:setPosition( position )
	self.effect:setRotation( sm.quat.angleAxis( math.pi * 0.5, sm.vec3.new( 1, 0, 0 ) ) )
	self.effect:setParameter( "Color", valid and self.config.color or InvalidColor )
	self.effect:setParameter( "visualizationColor", valid and "Lift Valid" or "Lift Invalid" )
	if not self.effect:isPlaying() then self.effect:start() end
	if valid then
		sm.gui.setInteractionText( "", sm.gui.getKeyBinding( "Create", true ), "PLANT " .. self.config.label .. " TREE SAPLING" )
		if primaryState == sm.tool.interactState.start then
			-- Re-run the exact shared preview checks on the click frame. This is
			-- intentionally click-only, so it closes stale-preview gaps without
			-- adding continuous physics work.
			local confirmed, confirmedPosition, confirmedNormal, confirmedReason = self:cl_raycast()
			if confirmed and confirmedPosition and
				( confirmedPosition - position ):length() <= 0.05 then
				self.network:sendToServer( "sv_n_plant", {
					pos = confirmedPosition, normal = confirmedNormal,
					slot = sm.localPlayer.getSelectedHotbarSlot()
				} )
				self:cl_playUse()
			else
				self.effect:setParameter( "Color", InvalidColor )
				self.effect:setParameter( "visualizationColor", "Lift Invalid" )
				sm.gui.setInteractionText( confirmedReason or "THE PLANTING SPOT CHANGED" )
			end
		end
	else sm.gui.setInteractionText( reason or "THE PLANTING SPOT IS BLOCKED" ) end
	return true, false
end

function TreeSaplingToolBase.client_onEquip( self )
	self:cl_updateRenderables(); self:cl_loadAnimations(); self.wantEquipped = true
	setTpAnimation( self.tpAnimations, "pickup", 0.0001 )
	if self.tool:isLocal() then swapFpAnimation( self.fpAnimations, "unequip", "equip", 0.2 ) end
end

function TreeSaplingToolBase.client_onUnequip( self )
	self.effect:stop(); self.wantEquipped = false; self.equipped = false
	if sm.exists( self.tool ) then
		setTpAnimation( self.tpAnimations, "putdown" )
		if self.tool:isLocal() and self.fpAnimations.currentAnimation ~= "unequip" then
			swapFpAnimation( self.fpAnimations, "equip", "unequip", 0.2 )
		end
	end
end

function TreeSaplingToolBase.cl_playUse( self )
	if self.tool:isLocal() then setFpAnimation( self.fpAnimations, "use", 0.25 ) end
	setTpAnimation( self.tpAnimations, "use", 10.0 )
end

local function isServerPlacementClear( config, world, position, player )
	if not player or not player.character or player.character:getWorld() ~= world then return false end
	if ( player.character.worldPosition - position ):length() > 8.0 then return false end
	if not world.publicData or world.publicData.type ~= "Overworld" then return false end
	local groundHit, ground = sm.physics.raycast(
		position + sm.vec3.new( 0, 0, 0.75 ),
		position - sm.vec3.new( 0, 0, 1.25 ), nil,
		sm.physics.filter.terrainSurface, world )
	if not groundHit or ground.type ~= "terrainSurface" or ground.normalWorld.z < 0.96592583 then return false end
	local groundPosition = ground.pointWorld + ground.normalWorld * 0.04
	if ( groundPosition - position ):length() > 0.2 then return false end
	return getPlacementBlockReason( config, groundPosition, world ) == nil, groundPosition
end

function TreeSaplingToolBase.sv_n_plant( self, params, player )
	if self.svPendingPlant or type( params ) ~= "table" or
		type( params.pos ) ~= "Vec3" or type( params.slot ) ~= "number" then return end
	local config = self.svConfig
	local world = player and player.character and player.character:getWorld()
	local clear, groundPosition = false, nil
	if world then clear, groundPosition = isServerPlacementClear( config, world, params.pos, player ) end
	if not clear then
		-- Physics contacts can be one simulation step behind the client's green
		-- preview (especially immediately after another sapling changes size).
		-- Revalidate briefly without consuming anything. Permanent obstructions
		-- still fail every check and are safely rejected.
		self.svPendingPlant = {
			params = params, player = player,
			retries = ServerPlacementRetryTicks
		}
		return
	end
	self:sv_commitPlant( params, player, groundPosition )
end

function TreeSaplingToolBase.server_onFixedUpdate( self )
	local pending = self.svPendingPlant
	if not pending then return end
	local player = pending.player
	local world = player and player.character and player.character:getWorld()
	local clear, groundPosition = false, nil
	if world then
		clear, groundPosition = isServerPlacementClear(
			self.svConfig, world, pending.params.pos, player )
	end
	if clear then
		self.svPendingPlant = nil
		self:sv_commitPlant( pending.params, player, groundPosition )
		return
	end
	pending.retries = pending.retries - 1
	if pending.retries <= 0 then
		self.svPendingPlant = nil
		if player then self.network:sendToClient( player, "cl_n_rejected" ) end
	end
end

function TreeSaplingToolBase.sv_commitPlant( self, params, player, groundPosition )
	local config = self.svConfig
	sm.container.beginTransaction()
	sm.container.spendFromSlot( player:getInventory(), params.slot, config.item, 1, true )
	if not sm.container.endTransaction() then return end
	local yaw = math.random( 0, 359 ) * math.pi / 180
	local rotation = sm.quat.angleAxis( yaw, sm.vec3.new( 0, 0, 1 ) ) *
		sm.quat.new( 0.70710678, 0, 0, 0.70710678 )
	local created = sm.harvestable.createHarvestable( config.harvestable, groundPosition, rotation )
	if not created then
		sm.container.beginTransaction()
		sm.container.collect( player:getInventory(), config.item, 1, true )
		sm.container.endTransaction()
		self.network:sendToClient( player, "cl_n_creationFailed" )
		return
	end
	sm.effect.playEffect( "Plants - SoilbagUse", groundPosition, nil, rotation )
	self.network:sendToClients( "cl_n_planted" )
end

function TreeSaplingToolBase.cl_n_planted( self )
	if not self.tool:isLocal() and self.tool:isEquipped() then self:cl_playUse() end
end

function TreeSaplingToolBase.cl_n_rejected( self )
	if self.tool:isLocal() then sm.gui.displayAlertText( "PLANTING SPOT IS NO LONGER CLEAR", 2.0 ) end
end

function TreeSaplingToolBase.cl_n_creationFailed( self )
	if self.tool:isLocal() then sm.gui.displayAlertText( "TREE SAPLING DATA DID NOT LOAD - RESTART SCRAP MECHANIC", 3.0 ) end
end

TreeSaplingSmallTool = class( TreeSaplingToolBase )
function TreeSaplingSmallTool.client_onCreate( self ) self:cl_initialize( "small" ) end
function TreeSaplingSmallTool.server_onCreate( self ) self.svConfig = Config.small end

TreeSaplingMediumTool = class( TreeSaplingToolBase )
function TreeSaplingMediumTool.client_onCreate( self ) self:cl_initialize( "medium" ) end
function TreeSaplingMediumTool.server_onCreate( self ) self.svConfig = Config.medium end

TreeSaplingLargeTool = class( TreeSaplingToolBase )
function TreeSaplingLargeTool.client_onCreate( self ) self:cl_initialize( "large" ) end
function TreeSaplingLargeTool.server_onCreate( self ) self.svConfig = Config.large end
