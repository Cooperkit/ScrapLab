-- ScrapLab Network Storage Chest
-- Shared, server-only, in-memory container revision cache.

if g_scrapLabNetworkInventoryIndex then
	return g_scrapLabNetworkInventoryIndex
end

local NetworkInventoryIndex = {
	cache = {},
	statistics = {
		cacheHits = 0,
		containerScans = 0,
		slotsScanned = 0,
		expiredEntries = 0
	}
}

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
	local size = container:getSize()
	local occupiedStacks = 0
	local totalQuantity = 0

	for slot = 0, size - 1 do
		local item = container:getItem( slot )
		if item and item.uuid and not item.uuid:isNil() and ( item.quantity or 0 ) > 0 then
			local uuid = tostring( item.uuid )
			local entry = byUuid[uuid]
			if not entry then
				entry = { uuid = uuid, quantity = 0, stacks = 0 }
				byUuid[uuid] = entry
			end
			entry.quantity = entry.quantity + item.quantity
			entry.stacks = entry.stacks + 1
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
		expiredEntries = NetworkInventoryIndex.statistics.expiredEntries
	}
end

g_scrapLabNetworkInventoryIndex = NetworkInventoryIndex
return NetworkInventoryIndex
