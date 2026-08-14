-- ScrapLab Network Storage Chest
-- Shared, server-only, in-memory container revision cache.

if g_scrapLabNetworkInventoryIndex then
	return g_scrapLabNetworkInventoryIndex
end

local NetworkInventoryIndex = {
	cache = {},
	itemProfiles = {},
	catalogReady = false,
	statistics = {
		cacheHits = 0,
		containerScans = 0,
		slotsScanned = 0,
		expiredEntries = 0,
		catalogItems = 0,
		catalogFiles = 0,
		catalogFailures = 0
	}
}

local SHAPESET_REGISTRY = "$SURVIVAL_DATA/Objects/Database/shapesets.json"

local PART_FAMILIES = {
	interactive = true, beacon = true, bucket = true, containers = true,
	cookbot = true, craftbot = true, fittings = true, industrial = true,
	lights = true, mounted_guns = true, powertools = true, tool_parts = true,
	vacumpipe = true, vehicle = true
}

local BUILDING_FAMILIES = {
	building = true, construction = true, decor = true, manmade = true,
	spaceship = true, structure = true
}

local RESOURCE_FAMILIES = {
	castedpart = true, component = true, harvests = true, jewel = true,
	moltenorb = true, resource = true, robotparts = true, voxelmaterial = true,
	voxelmaterialchunk = true
}

local SPECIAL_FAMILIES = {
	artifact = true, character_shape = true, characterobject = true,
	effect_proxies = true, outfitpackage = true, packingcrates = true,
	quest_items = true, rewards = true, shoprewards = true
}

local function startsWith( value, prefix )
	return string.sub( value, 1, string.len( prefix ) ) == prefix
end

local function shapeSetStem( path )
	local normalized = string.lower( tostring( path or "" ) ):gsub( "\\", "/" )
	local stem = normalized:match( "([^/]+)%.shapeset$" ) or "unknown"
	for _, suffix in ipairs( { "_survivalobject", "_challenge", "_creative", "_shared", "_blueprint", "_upgradeable" } ) do
		if stem:sub( -#suffix ) == suffix then stem = stem:sub( 1, #stem - #suffix ) end
	end
	if startsWith( stem, "interactive" ) then return "interactive" end
	if startsWith( stem, "blocks" ) or startsWith( stem, "wedges" ) then return "blocks" end
	if startsWith( stem, "consumable" ) then return "consumable" end
	if startsWith( stem, "harvests" ) then return "harvests" end
	if startsWith( stem, "plantables" ) then return "plantables" end
	if startsWith( stem, "resource" ) then return "resource" end
	return stem
end

local function broadCategory( family, listName, entry )
	if listName == "blockList" or family == "blocks" then return "BLOCK" end
	if entry and entry.edible then return "FOOD" end
	if family == "plantables" then return "FARMING" end
	if family == "consumable" then return "CONSUMABLE" end
	if PART_FAMILIES[family] then return "INTERACTIVE_PART" end
	if BUILDING_FAMILIES[family] then return "BUILDING_PART" end
	if RESOURCE_FAMILIES[family] then return "RESOURCE" end
	if SPECIAL_FAMILIES[family] then return "SPECIAL" end
	return listName == "partList" and "PART" or "UNKNOWN"
end

local function rememberItemProfile( uuid, family, category )
	if not uuid then return end
	local key = tostring( uuid )
	if key == "" or key == "00000000-0000-0000-0000-000000000000" then return end
	if not NetworkInventoryIndex.itemProfiles[key] then
		NetworkInventoryIndex.itemProfiles[key] = { family = family, category = category }
		NetworkInventoryIndex.statistics.catalogItems = NetworkInventoryIndex.statistics.catalogItems + 1
	end
end

local function loadShapeSet( path )
	local ok, shapeSet = pcall( sm.json.open, path )
	if not ok or type( shapeSet ) ~= "table" then
		NetworkInventoryIndex.statistics.catalogFailures = NetworkInventoryIndex.statistics.catalogFailures + 1
		return
	end
	local family = shapeSetStem( path )
	for _, listName in ipairs( { "blockList", "partList" } ) do
		for _, entry in ipairs( shapeSet[listName] or {} ) do
			rememberItemProfile( entry.uuid, family, broadCategory( family, listName, entry ) )
		end
	end
	NetworkInventoryIndex.statistics.catalogFiles = NetworkInventoryIndex.statistics.catalogFiles + 1
end

local function ensureItemCatalog()
	if NetworkInventoryIndex.catalogReady then return end
	NetworkInventoryIndex.catalogReady = true
	local ok, registry = pcall( sm.json.open, SHAPESET_REGISTRY )
	if not ok or type( registry ) ~= "table" or type( registry.shapeSetList ) ~= "table" then
		NetworkInventoryIndex.statistics.catalogFailures = NetworkInventoryIndex.statistics.catalogFailures + 1
		return
	end
	for _, path in ipairs( registry.shapeSetList ) do loadShapeSet( path ) end
	sm.log.info( "[ScrapLab Storage] routing catalog: " ..
		tostring( NetworkInventoryIndex.statistics.catalogItems ) .. " items from " ..
		tostring( NetworkInventoryIndex.statistics.catalogFiles ) .. " shape sets" )
end

local function safeItemCall( name, uuid )
	local api = sm.item and sm.item[name]
	if type( api ) ~= "function" then return nil end
	local ok, value = pcall( api, uuid )
	return ok and value or nil
end

local function fallbackItemProfile( uuid )
	if safeItemCall( "isBlock", uuid ) == true then return { family = "blocks", category = "BLOCK" } end
	if safeItemCall( "isTool", uuid ) == true then return { family = "tool", category = "TOOL" } end
	if safeItemCall( "getEdible", uuid ) then return { family = "food", category = "FOOD" } end
	if safeItemCall( "getPlantable", uuid ) then return { family = "plantables", category = "FARMING" } end
	if safeItemCall( "isPart", uuid ) == true then return { family = "part", category = "PART" } end
	return { family = "unknown", category = "UNKNOWN" }
end

function NetworkInventoryIndex.getItemProfile( uuid )
	ensureItemCatalog()
	local key = tostring( uuid )
	local profile = NetworkInventoryIndex.itemProfiles[key]
	if profile then return profile end
	profile = fallbackItemProfile( uuid )
	NetworkInventoryIndex.itemProfiles[key] = profile
	return profile
end

local function currentTick()
	local ok, tick = pcall( sm.game.getCurrentTick )
	return ok and tick or 0
end

local function containerId( container )
	if not container then return nil end
	local ok, id = pcall( sm.container.getId, container )
	if not ok or id == nil then return nil end
	return tostring( id )
end

local function containerRevision( container )
	if not container then return -1 end
	local ok, revision = pcall( function() return container:getRevision() end )
	return ok and revision or -1
end

local function decodeContainer( container, id, revision, tick )
	local byUuid = {}
	local categoryStacks = {}
	local familyStacks = {}
	local size = container:getSize()
	local occupiedStacks = 0
	local totalQuantity = 0

	for slot = 0, size - 1 do
		local item = container:getItem( slot )
		if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
			local uuid = tostring( item.uuid )
			local entry = byUuid[uuid]
			if not entry then
				entry = { uuid = uuid, quantity = 0, stacks = 0, fullestPartial = 0 }
				byUuid[uuid] = entry
			end
			entry.quantity = entry.quantity + item.quantity
			entry.stacks = entry.stacks + 1
			local ok, stackSize = pcall( sm.item.getStackSize, item.uuid )
			stackSize = ok and math.max( 1, stackSize or 1 ) or 1
			if item.quantity < stackSize then entry.fullestPartial = math.max( entry.fullestPartial, item.quantity ) end
			local profile = NetworkInventoryIndex.getItemProfile( item.uuid )
			categoryStacks[profile.category] = ( categoryStacks[profile.category] or 0 ) + 1
			familyStacks[profile.family] = ( familyStacks[profile.family] or 0 ) + 1
			occupiedStacks = occupiedStacks + 1
			totalQuantity = totalQuantity + item.quantity
		end
	end

	local items = {}
	for _, entry in pairs( byUuid ) do items[#items + 1] = entry end
	table.sort( items, function( a, b ) return a.uuid < b.uuid end )

	NetworkInventoryIndex.statistics.containerScans = NetworkInventoryIndex.statistics.containerScans + 1
	NetworkInventoryIndex.statistics.slotsScanned = NetworkInventoryIndex.statistics.slotsScanned + size

	return {
		id = id,
		revision = revision,
		size = size,
		occupiedStacks = occupiedStacks,
		totalQuantity = totalQuantity,
		byUuid = byUuid,
		categoryStacks = categoryStacks,
		familyStacks = familyStacks,
		items = items,
		lastUsedTick = tick
	}
end

function NetworkInventoryIndex.getContainerId( container )
	return containerId( container )
end

function NetworkInventoryIndex.getRevision( container )
	return containerRevision( container )
end

function NetworkInventoryIndex.read( container, tick )
	tick = tick or currentTick()
	local id = containerId( container )
	if not id then return nil, false, "CONTAINER ID UNAVAILABLE" end
	local revision = containerRevision( container )
	if revision < 0 then return nil, false, "CONTAINER REVISION UNAVAILABLE" end

	local cached = NetworkInventoryIndex.cache[id]
	if cached and cached.revision == revision then
		cached.lastUsedTick = tick
		NetworkInventoryIndex.statistics.cacheHits = NetworkInventoryIndex.statistics.cacheHits + 1
		return cached, true
	end

	local ok, decoded = pcall( decodeContainer, container, id, revision, tick )
	if not ok then return nil, false, tostring( decoded ) end
	NetworkInventoryIndex.cache[id] = decoded
	return decoded, false
end

function NetworkInventoryIndex.invalidate( containerOrId )
	local id = type( containerOrId ) == "string" and containerOrId or containerId( containerOrId )
	if id then NetworkInventoryIndex.cache[id] = nil end
end

function NetworkInventoryIndex.aggregate( records )
	local byUuid = {}
	local totalQuantity = 0
	local totalStacks = 0

	for _, record in ipairs( records or {} ) do
		for _, source in ipairs( record.items or {} ) do
			local entry = byUuid[source.uuid]
			if not entry then
				entry = { uuid = source.uuid, quantity = 0, stacks = 0, sources = 0 }
				byUuid[source.uuid] = entry
			end
			entry.quantity = entry.quantity + source.quantity
			entry.stacks = entry.stacks + source.stacks
			entry.sources = entry.sources + 1
			totalQuantity = totalQuantity + source.quantity
			totalStacks = totalStacks + source.stacks
		end
	end

	local entries = {}
	for _, entry in pairs( byUuid ) do entries[#entries + 1] = entry end
	table.sort( entries, function( a, b ) return a.uuid < b.uuid end )

	local signatureParts = {}
	for _, entry in ipairs( entries ) do
		signatureParts[#signatureParts + 1] = entry.uuid .. ":" .. tostring( entry.quantity ) ..
			":" .. tostring( entry.stacks ) .. ":" .. tostring( entry.sources )
	end

	return {
		entries = entries,
		uniqueItems = #entries,
		totalQuantity = totalQuantity,
		totalStacks = totalStacks,
		signature = table.concat( signatureParts, "|" )
	}
end

function NetworkInventoryIndex.prune( tick, maxAgeTicks )
	tick = tick or currentTick()
	maxAgeTicks = maxAgeTicks or 2400
	for id, record in pairs( NetworkInventoryIndex.cache ) do
		if tick - ( record.lastUsedTick or 0 ) > maxAgeTicks then
			NetworkInventoryIndex.cache[id] = nil
			NetworkInventoryIndex.statistics.expiredEntries = NetworkInventoryIndex.statistics.expiredEntries + 1
		end
	end
end

function NetworkInventoryIndex.getStatistics()
	local cachedEntries = 0
	for _ in pairs( NetworkInventoryIndex.cache ) do cachedEntries = cachedEntries + 1 end
	return {
		cachedEntries = cachedEntries,
		cacheHits = NetworkInventoryIndex.statistics.cacheHits,
		containerScans = NetworkInventoryIndex.statistics.containerScans,
		slotsScanned = NetworkInventoryIndex.statistics.slotsScanned,
		expiredEntries = NetworkInventoryIndex.statistics.expiredEntries,
		catalogItems = NetworkInventoryIndex.statistics.catalogItems,
		catalogFiles = NetworkInventoryIndex.statistics.catalogFiles,
		catalogFailures = NetworkInventoryIndex.statistics.catalogFailures
	}
end

g_scrapLabNetworkInventoryIndex = NetworkInventoryIndex
return NetworkInventoryIndex
