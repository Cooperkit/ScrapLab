using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RaidRescue
{
    internal static class ScrapLabIconAtlasCoordinator
    {
        internal const int CellSize = 96;
        internal const int CurrentBuildX = 3936;
        internal const int CurrentBuildY = 3936;
        internal const string CatalogVersion = "4";

        internal sealed class IconAsset
        {
            public string ModKey;
            public string Uuid;
            public string ResourceName;
            public byte[] Bytes;
            public string Hash;
            public string[] LegacyResourceNames;
            public List<byte[]> LegacyBytes;
            public List<string> LegacyHashes;
        }

        internal sealed class IconPlacement
        {
            public string ModKey { get; set; }
            public string Uuid { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public string IconHash { get; set; }
        }

        internal sealed class SharedAtlasReceipt
        {
            public string CatalogVersion { get; set; }
            public string BaselinePath { get; set; }
            public string BaselineHash { get; set; }
            public string AtlasOutputHash { get; set; }
            public string IconXmlHash { get; set; }
            public string UpdatedUtc { get; set; }
            public List<string> ActiveMods { get; set; }
            public List<IconPlacement> Icons { get; set; }
        }

        internal sealed class CatalogPlan
        {
            public byte[] AtlasBytes;
            public bool AtlasChanged;
            public Dictionary<string, IconPlacement> Placements;
        }

        internal sealed class AtlasInfo
        {
            public int X;
            public int Y;
            public bool EntryPresent;
            public bool IconPresent;
            public bool CellTransparent;
        }

        private sealed class RawImage
        {
            public int Width;
            public int Height;
            public int Stride;
            public byte[] Pixels;
        }

        internal static List<IconAsset> LoadCatalog()
        {
            List<IconAsset> catalog = new List<IconAsset>
            {
                new IconAsset
                {
                    ModKey = "RaidDetector",
                    Uuid = "a638a8aa-6f4f-41c2-9e31-702687066092",
                    ResourceName =
                        "RaidRescue.Parts.RaidDetector.RaidDetectorIcon.png",
                    LegacyResourceNames = new string[]
                    {
                        "RaidRescue.Parts.RaidDetector.RaidDetectorIconLegacyOpaque.png"
                    }
                },
                new IconAsset
                {
                    ModKey = "WirelessVacuumPipe",
                    Uuid = "a34d9af0-4ba0-431d-b647-2d5435ecf138",
                    ResourceName =
                        "RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipeIcon.png",
                    LegacyResourceNames = new string[0]
                },
                new IconAsset
                {
                    ModKey = "NetworkStorageChest",
                    Uuid = "bc7576a7-f226-459a-883c-e8460e955d63",
                    ResourceName =
                        "RaidRescue.Parts.NetworkStorageChest.NetworkStorageChestIcon.png",
                    LegacyResourceNames = new string[0]
                },
                new IconAsset
                {
                    ModKey = "TreeSaplings",
                    Uuid = "790d34b8-f006-47e4-9ebc-49a84a68ed16",
                    ResourceName = "RaidRescue.Parts.TreeSaplings.SmallTreeSaplingIcon.png",
                    LegacyResourceNames = new string[0]
                },
                new IconAsset
                {
                    ModKey = "TreeSaplings",
                    Uuid = "33511c78-354b-4a60-af6b-778c427c47d5",
                    ResourceName = "RaidRescue.Parts.TreeSaplings.MediumTreeSaplingIcon.png",
                    LegacyResourceNames = new string[0]
                },
                new IconAsset
                {
                    ModKey = "TreeSaplings",
                    Uuid = "c9413781-5a0e-4025-a2cb-bc2090803e50",
                    ResourceName = "RaidRescue.Parts.TreeSaplings.LargeTreeSaplingIcon.png",
                    LegacyResourceNames = new string[0]
                }
            };

            Assembly assembly = Assembly.GetExecutingAssembly();
            HashSet<string> uuids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> hashes = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (IconAsset asset in catalog)
            {
                if (String.IsNullOrEmpty(asset.ModKey) ||
                    String.IsNullOrEmpty(asset.Uuid) ||
                    String.IsNullOrEmpty(asset.ResourceName) ||
                    !uuids.Add(asset.Uuid))
                {
                    throw new InvalidDataException(
                        "The embedded ScrapLab icon catalog contains a duplicate or incomplete entry.");
                }
                using (Stream stream = assembly.GetManifestResourceStream(
                    asset.ResourceName))
                {
                    if (stream == null)
                        throw new InvalidOperationException(
                            "The embedded ScrapLab part icon is missing: " +
                            asset.ResourceName);
                    using (MemoryStream output = new MemoryStream())
                    {
                        stream.CopyTo(output);
                        asset.Bytes = output.ToArray();
                    }
                }
                asset.Hash = AdaptivePatchSupport.Sha256(asset.Bytes);
                if (!hashes.Add(asset.Hash))
                    throw new InvalidDataException(
                        "Every ScrapLab custom part must have a distinct inventory icon.");
                RawImage icon = LoadImage(asset.Bytes,
                    asset.ModKey + " inventory icon");
                if (icon.Width != CellSize || icon.Height != CellSize)
                    throw new InvalidDataException(
                        asset.ModKey + " inventory icon is not 96 by 96 pixels.");
                asset.LegacyBytes = new List<byte[]>();
                asset.LegacyHashes = new List<string>();
                foreach (string legacyName in asset.LegacyResourceNames ??
                    new string[0])
                {
                    using (Stream legacyStream =
                        assembly.GetManifestResourceStream(legacyName))
                    {
                        if (legacyStream == null)
                            throw new InvalidOperationException(
                                "The embedded legacy ScrapLab icon is missing: " +
                                legacyName);
                        using (MemoryStream output = new MemoryStream())
                        {
                            legacyStream.CopyTo(output);
                            asset.LegacyBytes.Add(output.ToArray());
                        }
                    }
                    byte[] legacy = asset.LegacyBytes[
                        asset.LegacyBytes.Count - 1];
                    string legacyHash =
                        AdaptivePatchSupport.Sha256(legacy);
                    if (String.Equals(legacyHash, asset.Hash,
                        StringComparison.OrdinalIgnoreCase) ||
                        asset.LegacyHashes.Contains(legacyHash))
                        throw new InvalidDataException(
                            asset.ModKey + " has a duplicated legacy icon definition.");
                    RawImage legacyIcon = LoadImage(legacy,
                        asset.ModKey + " legacy inventory icon");
                    if (legacyIcon.Width != CellSize ||
                        legacyIcon.Height != CellSize)
                        throw new InvalidDataException(
                            asset.ModKey + " legacy inventory icon is not 96 by 96 pixels.");
                    asset.LegacyHashes.Add(legacyHash);
                }
            }
            return catalog;
        }

        internal static IconAsset FindCatalogIcon(
            IList<IconAsset> catalog, string uuid)
        {
            foreach (IconAsset asset in catalog)
                if (String.Equals(asset.Uuid, uuid,
                    StringComparison.OrdinalIgnoreCase)) return asset;
            throw new InvalidOperationException(
                "The ScrapLab icon catalog does not contain " + uuid + ".");
        }

        internal static CatalogPlan EnsureCatalog(
            string xml, byte[] atlasBytes, IList<IconAsset> catalog)
        {
            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RequireAtlasDimensions(atlas);
            HashSet<string> used = ReadUsedCells(xml);
            HashSet<string> owned = new HashSet<string>(
                StringComparer.Ordinal);
            Dictionary<string, IconPlacement> placements =
                new Dictionary<string, IconPlacement>(
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, RawImage> icons = LoadIcons(catalog);
            List<IconAsset> missing = new List<IconAsset>();
            bool changed = false;

            foreach (IconAsset asset in catalog)
            {
                int x;
                int y;
                if (TryGetEntry(xml, asset.Uuid, out x, out y))
                {
                    if (!TileEquals(atlas, icons[asset.Uuid], x, y))
                    {
                        if (!LegacyTileEquals(atlas, asset, x, y))
                            throw new InvalidDataException(
                                asset.ModKey + " has an icon registration, but its atlas pixels are missing or edited.");
                        WriteIcon(atlas, icons[asset.Uuid], x, y);
                        changed = true;
                    }
                    AddPlacement(placements, owned, asset, x, y);
                    continue;
                }

                List<IconPlacement> matches = FindIconTiles(
                    atlas, icons[asset.Uuid], asset);
                if (matches.Count > 1)
                    throw new InvalidDataException(
                        asset.ModKey + " inventory icon appears in more than one atlas cell.");
                if (matches.Count == 1)
                {
                    IconPlacement placement = matches[0];
                    string cell = CellKey(placement.X, placement.Y);
                    if (used.Contains(cell) || !owned.Add(cell))
                        throw new InvalidDataException(
                            asset.ModKey + " inventory icon overlaps another registered atlas cell.");
                    placements.Add(asset.Uuid, placement);
                    continue;
                }
                List<IconPlacement> legacyMatches =
                    FindLegacyIconTiles(atlas, asset);
                if (legacyMatches.Count > 1)
                    throw new InvalidDataException(
                        asset.ModKey + " legacy inventory icon appears in more than one atlas cell.");
                if (legacyMatches.Count == 1)
                {
                    IconPlacement placement = legacyMatches[0];
                    string cell = CellKey(placement.X, placement.Y);
                    if (used.Contains(cell) || !owned.Add(cell))
                        throw new InvalidDataException(
                            asset.ModKey + " legacy inventory icon overlaps another registered atlas cell.");
                    WriteIcon(atlas, icons[asset.Uuid],
                        placement.X, placement.Y);
                    placements.Add(asset.Uuid, placement);
                    changed = true;
                    continue;
                }
                missing.Add(asset);
            }

            foreach (IconAsset asset in missing)
            {
                int x;
                int y;
                SelectBottomCell(atlas, used, owned, out x, out y);
                WriteIcon(atlas, icons[asset.Uuid], x, y);
                AddPlacement(placements, owned, asset, x, y);
                changed = true;
            }

            byte[] output = changed ? SavePng(atlas) : atlasBytes;
            if (changed)
                VerifyOnlyTilesChanged(atlasBytes, output,
                    PlacementList(placements));
            return new CatalogPlan
            {
                AtlasBytes = output,
                AtlasChanged = changed,
                Placements = placements
            };
        }

        internal static byte[] RemoveCatalogWhenUnused(
            string xmlAfterRemoval, byte[] atlasBytes,
            IList<IconAsset> catalog, byte[] baselineBytes)
        {
            if (AnyCatalogRegistration(xmlAfterRemoval, catalog))
                return atlasBytes;

            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RequireAtlasDimensions(atlas);
            Dictionary<string, RawImage> icons = LoadIcons(catalog);
            List<IconPlacement> present = new List<IconPlacement>();
            foreach (IconAsset asset in catalog)
            {
                List<IconPlacement> matches = FindIconTiles(
                    atlas, icons[asset.Uuid], asset);
                if (matches.Count == 0)
                    matches = FindLegacyIconTiles(atlas, asset);
                if (matches.Count > 1)
                    throw new InvalidDataException(
                        asset.ModKey + " inventory icon appears in more than one atlas cell.");
                if (matches.Count == 1) present.Add(matches[0]);
            }
            if (present.Count == 0)
                return atlasBytes;

            if (baselineBytes != null)
            {
                RawImage baseline = LoadImage(
                    baselineBytes, "shared ScrapLab atlas baseline");
                RequireAtlasDimensions(baseline);
                bool originalTiles = true;
                foreach (IconPlacement placement in present)
                    originalTiles &= IsTransparent(
                        baseline, placement.X, placement.Y);
                if (originalTiles && FindOutsideTilesDifference(
                    baseline, atlas, present) == null)
                    return baselineBytes;
            }

            foreach (IconPlacement placement in present)
                ClearTile(atlas, placement.X, placement.Y);
            byte[] output = SavePng(atlas);
            VerifyOnlyTilesChanged(atlasBytes, output, present);
            return output;
        }

        internal static bool AnyCatalogRegistration(
            string xml, IList<IconAsset> catalog)
        {
            foreach (IconAsset asset in catalog)
            {
                int x;
                int y;
                if (TryGetEntry(xml, asset.Uuid, out x, out y)) return true;
            }
            return false;
        }

        internal static bool ContainsAnyCatalogPixels(
            byte[] atlasBytes, IList<IconAsset> catalog)
        {
            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RequireAtlasDimensions(atlas);
            Dictionary<string, RawImage> icons = LoadIcons(catalog);
            foreach (IconAsset asset in catalog)
                if (FindIconTiles(atlas, icons[asset.Uuid], asset).Count > 0 ||
                    FindLegacyIconTiles(atlas, asset).Count > 0)
                    return true;
            return false;
        }

        internal static SharedAtlasReceipt CreateReceipt(
            string xml, byte[] atlasBytes, byte[] baselineBytes,
            string baselinePath, string iconXmlHash,
            IList<IconAsset> catalog)
        {
            CatalogPlan state = EnsureCatalog(xml, atlasBytes, catalog);
            if (state.AtlasChanged)
                throw new InvalidDataException(
                    "The shared ScrapLab icon pack is incomplete after installation.");
            List<string> active = new List<string>();
            foreach (IconAsset asset in catalog)
            {
                int x;
                int y;
                if (TryGetEntry(xml, asset.Uuid, out x, out y) &&
                    !active.Contains(asset.ModKey)) active.Add(asset.ModKey);
            }
            active.Sort(StringComparer.Ordinal);
            List<IconPlacement> icons = PlacementList(state.Placements);
            icons.Sort(delegate(IconPlacement left, IconPlacement right)
            {
                return String.Compare(left.ModKey, right.ModKey,
                    StringComparison.Ordinal);
            });
            return new SharedAtlasReceipt
            {
                CatalogVersion = CatalogVersion,
                BaselinePath = baselinePath ?? "",
                BaselineHash = baselineBytes == null ? "" :
                    AdaptivePatchSupport.Sha256(baselineBytes),
                AtlasOutputHash = AdaptivePatchSupport.Sha256(atlasBytes),
                IconXmlHash = iconXmlHash ?? "",
                UpdatedUtc = DateTime.UtcNow.ToString("O"),
                ActiveMods = active,
                Icons = icons
            };
        }

        internal static byte[] SerializeReceipt(SharedAtlasReceipt receipt)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer
            {
                MaxJsonLength = Int32.MaxValue
            };
            return new UTF8Encoding(false).GetBytes(
                serializer.Serialize(receipt));
        }

        internal static SharedAtlasReceipt LoadReceipt(string path)
        {
            try
            {
                if (String.IsNullOrEmpty(path) || !File.Exists(path))
                    return null;
                JavaScriptSerializer serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = Int32.MaxValue
                };
                return serializer.Deserialize<SharedAtlasReceipt>(
                    File.ReadAllText(path, new UTF8Encoding(false, true)));
            }
            catch { return null; }
        }

        internal static bool IsTrustedReceipt(
            SharedAtlasReceipt receipt, string atlasHash,
            IList<IconAsset> catalog)
        {
            if (receipt == null || receipt.Icons == null ||
                receipt.Icons.Count == 0 ||
                !String.Equals(receipt.AtlasOutputHash, atlasHash,
                    StringComparison.OrdinalIgnoreCase) ||
                String.IsNullOrEmpty(receipt.BaselinePath) ||
                !File.Exists(receipt.BaselinePath)) return false;
            try
            {
                if (!String.Equals(
                    AdaptivePatchSupport.Sha256(receipt.BaselinePath),
                    receipt.BaselineHash,
                    StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch { return false; }
            HashSet<string> uuids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> cells = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (IconPlacement placement in receipt.Icons)
            {
                int count = 0;
                foreach (IconAsset asset in catalog)
                    if (String.Equals(placement.Uuid, asset.Uuid,
                        StringComparison.OrdinalIgnoreCase) &&
                        IsKnownIconHash(asset,
                            placement.IconHash)) count++;
                if (count != 1 || !uuids.Add(placement.Uuid) ||
                    placement.X % CellSize != 0 ||
                    placement.Y % CellSize != 0 ||
                    !cells.Add(CellKey(placement.X, placement.Y)))
                    return false;
            }
            return true;
        }

        private static bool IsKnownIconHash(
            IconAsset asset, string hash)
        {
            if (String.Equals(asset.Hash, hash,
                StringComparison.OrdinalIgnoreCase)) return true;
            foreach (string legacyHash in asset.LegacyHashes ??
                new List<string>())
                if (String.Equals(legacyHash, hash,
                    StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static AtlasInfo Inspect(
            string xml, byte[] atlasBytes, byte[] iconBytes,
            string uuid)
        {
            int x;
            int y;
            bool entry = TryGetEntry(xml, uuid, out x, out y);
            if (!entry)
            {
                x = CurrentBuildX;
                y = CurrentBuildY;
            }
            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RawImage icon = LoadImage(iconBytes, "ScrapLab part icon");
            RequireDimensions(atlas, icon);
            return new AtlasInfo
            {
                X = x,
                Y = y,
                EntryPresent = entry,
                IconPresent = entry && TileEquals(atlas, icon, x, y),
                CellTransparent = IsTransparent(atlas, x, y)
            };
        }

        internal static void SelectInstallCell(
            string xml, byte[] atlasBytes, byte[] iconBytes,
            bool currentKnownBuild, out int x, out int y)
        {
            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RawImage icon = LoadImage(iconBytes, "ScrapLab part icon");
            RequireDimensions(atlas, icon);
            HashSet<string> used = ReadUsedCells(xml);
            SelectBottomCell(atlas, used,
                new HashSet<string>(StringComparer.Ordinal), out x, out y);
        }

        internal static byte[] InstallIcon(
            byte[] atlasBytes, byte[] iconBytes, int x, int y)
        {
            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RawImage icon = LoadImage(iconBytes, "ScrapLab part icon");
            RequireDimensions(atlas, icon);
            if (!IsTransparent(atlas, x, y) &&
                !TileEquals(atlas, icon, x, y))
                throw new InvalidDataException(
                    "The selected inventory-icon atlas cell is no longer empty.");
            WriteIcon(atlas, icon, x, y);
            byte[] output = SavePng(atlas);
            VerifyOnlyTilesChanged(atlasBytes, output,
                new List<IconPlacement> { new IconPlacement { X = x, Y = y } });
            return output;
        }

        internal static byte[] RemoveIcon(
            byte[] atlasBytes, byte[] iconBytes, int x, int y)
        {
            RawImage atlas = LoadImage(atlasBytes, "item icon atlas");
            RawImage icon = LoadImage(iconBytes, "ScrapLab part icon");
            RequireDimensions(atlas, icon);
            if (!TileEquals(atlas, icon, x, y))
                throw new InvalidDataException(
                    "The ScrapLab inventory-icon pixels were edited or replaced.");
            ClearTile(atlas, x, y);
            byte[] output = SavePng(atlas);
            VerifyOnlyTilesChanged(atlasBytes, output,
                new List<IconPlacement> { new IconPlacement { X = x, Y = y } });
            return output;
        }

        internal static bool TryGetEntry(
            string xml, string uuid, out int x, out int y)
        {
            x = 0;
            y = 0;
            MatchCollection uuidMatches = Regex.Matches(
                xml ?? "", "<Index\\s+name=\"" +
                Regex.Escape(uuid) + "\"\\s*>",
                RegexOptions.CultureInvariant);
            if (uuidMatches.Count == 0)
                return false;
            if (uuidMatches.Count != 1)
                throw new InvalidDataException(
                    "The ScrapLab icon registration for " + uuid +
                    " is duplicated.");

            int start = uuidMatches[0].Index;
            int end = xml.IndexOf("</Index>", start,
                StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidDataException(
                    "The ScrapLab icon registration for " + uuid +
                    " is incomplete.");
            string block = xml.Substring(start, end - start);
            Match frame = Regex.Match(block,
                "<Frame\\s+point=\"([0-9]+)\\s+([0-9]+)\"\\s*/>",
                RegexOptions.CultureInvariant);
            if (!frame.Success ||
                !Int32.TryParse(frame.Groups[1].Value, out x) ||
                !Int32.TryParse(frame.Groups[2].Value, out y) ||
                x % CellSize != 0 || y % CellSize != 0)
                throw new InvalidDataException(
                    "The ScrapLab icon atlas coordinate for " + uuid +
                    " is invalid.");
            return true;
        }

        internal static bool PixelsOutsideTileEqual(
            byte[] before, byte[] after, int x, int y)
        {
            return FindOutsideTilesDifference(
                LoadImage(before, "source atlas"),
                LoadImage(after, "output atlas"),
                new List<IconPlacement>
                {
                    new IconPlacement { X = x, Y = y }
                }) == null;
        }

        private static Dictionary<string, RawImage> LoadIcons(
            IList<IconAsset> catalog)
        {
            Dictionary<string, RawImage> icons =
                new Dictionary<string, RawImage>(
                    StringComparer.OrdinalIgnoreCase);
            foreach (IconAsset asset in catalog)
            {
                RawImage icon = LoadImage(
                    asset.Bytes, asset.ModKey + " inventory icon");
                if (icon.Width != CellSize || icon.Height != CellSize)
                    throw new InvalidDataException(
                        asset.ModKey + " inventory icon is not 96 by 96 pixels.");
                icons.Add(asset.Uuid, icon);
            }
            return icons;
        }

        private static void AddPlacement(
            Dictionary<string, IconPlacement> placements,
            HashSet<string> owned, IconAsset asset, int x, int y)
        {
            string cell = CellKey(x, y);
            if (!owned.Add(cell))
                throw new InvalidDataException(
                    "Two ScrapLab icons were assigned to atlas cell " + cell + ".");
            placements.Add(asset.Uuid, new IconPlacement
            {
                ModKey = asset.ModKey,
                Uuid = asset.Uuid,
                X = x,
                Y = y,
                IconHash = asset.Hash
            });
        }

        private static List<IconPlacement> FindIconTiles(
            RawImage atlas, RawImage icon, IconAsset asset)
        {
            List<IconPlacement> matches = new List<IconPlacement>();
            int maxX = LastAlignedCell(atlas.Width);
            int maxY = LastAlignedCell(atlas.Height);
            for (int y = maxY; y >= 0; y -= CellSize)
                for (int x = maxX; x >= 0; x -= CellSize)
                    if (TileEquals(atlas, icon, x, y))
                        matches.Add(new IconPlacement
                        {
                            ModKey = asset.ModKey,
                            Uuid = asset.Uuid,
                            X = x,
                            Y = y,
                            IconHash = asset.Hash
                        });
            return matches;
        }

        private static bool LegacyTileEquals(
            RawImage atlas, IconAsset asset, int x, int y)
        {
            foreach (byte[] legacy in asset.LegacyBytes ??
                new List<byte[]>())
                if (TileEquals(atlas, LoadImage(legacy,
                    asset.ModKey + " legacy inventory icon"), x, y))
                    return true;
            return false;
        }

        private static List<IconPlacement> FindLegacyIconTiles(
            RawImage atlas, IconAsset asset)
        {
            List<IconPlacement> matches = new List<IconPlacement>();
            HashSet<string> cells = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (byte[] legacy in asset.LegacyBytes ??
                new List<byte[]>())
            {
                RawImage icon = LoadImage(legacy,
                    asset.ModKey + " legacy inventory icon");
                int maxX = LastAlignedCell(atlas.Width);
                int maxY = LastAlignedCell(atlas.Height);
                for (int y = maxY; y >= 0; y -= CellSize)
                    for (int x = maxX; x >= 0; x -= CellSize)
                        if (TileEquals(atlas, icon, x, y) &&
                            cells.Add(CellKey(x, y)))
                            matches.Add(new IconPlacement
                            {
                                ModKey = asset.ModKey,
                                Uuid = asset.Uuid,
                                X = x,
                                Y = y,
                                IconHash = asset.Hash
                            });
            }
            return matches;
        }

        private static void SelectBottomCell(
            RawImage atlas, HashSet<string> used, HashSet<string> owned,
            out int selectedX, out int selectedY)
        {
            int maxX = LastAlignedCell(atlas.Width);
            int maxY = LastAlignedCell(atlas.Height);
            for (int y = maxY; y >= 0; y -= CellSize)
            {
                for (int x = maxX; x >= 0; x -= CellSize)
                {
                    string cell = CellKey(x, y);
                    if (!used.Contains(cell) && !owned.Contains(cell) &&
                        IsTransparent(atlas, x, y))
                    {
                        selectedX = x;
                        selectedY = y;
                        return;
                    }
                }
            }
            throw new InvalidDataException(
                "IconMapSurvival.png has no verified transparent 96-pixel cell for the ScrapLab icon pack.");
        }

        private static int LastAlignedCell(int dimension)
        {
            return ((dimension - CellSize) / CellSize) * CellSize;
        }

        private static List<IconPlacement> PlacementList(
            Dictionary<string, IconPlacement> placements)
        {
            return new List<IconPlacement>(placements.Values);
        }

        private static string FindOutsideTilesDifference(
            RawImage left, RawImage right, IList<IconPlacement> tiles)
        {
            if (left.Width != right.Width || left.Height != right.Height)
                return "image dimensions";
            for (int py = 0; py < left.Height; py++)
            {
                for (int px = 0; px < left.Width; px++)
                {
                    if (IsInsideAnyTile(px, py, tiles)) continue;
                    int li = py * left.Stride + px * 4;
                    int ri = py * right.Stride + px * 4;
                    if (!PixelEquals(left.Pixels, li, right.Pixels, ri))
                        return px + "," + py;
                }
            }
            return null;
        }

        private static bool IsInsideAnyTile(
            int x, int y, IList<IconPlacement> tiles)
        {
            foreach (IconPlacement tile in tiles)
                if (x >= tile.X && x < tile.X + CellSize &&
                    y >= tile.Y && y < tile.Y + CellSize) return true;
            return false;
        }

        private static void VerifyOnlyTilesChanged(
            byte[] before, byte[] after, IList<IconPlacement> tiles)
        {
            string difference = FindOutsideTilesDifference(
                LoadImage(before, "source atlas"),
                LoadImage(after, "output atlas"), tiles);
            if (difference != null)
                throw new IOException(
                    "The generated icon atlas changed pixels outside ScrapLab's assigned bottom tiles at " +
                    difference + ".");
        }

        private static HashSet<string> ReadUsedCells(string xml)
        {
            HashSet<string> used = new HashSet<string>(
                StringComparer.Ordinal);
            MatchCollection frames = Regex.Matches(
                xml ?? "", "<Frame\\s+point=\"([0-9]+)\\s+([0-9]+)\"\\s*/>",
                RegexOptions.CultureInvariant);
            foreach (Match frame in frames)
                used.Add(CellKey(
                    Int32.Parse(frame.Groups[1].Value),
                    Int32.Parse(frame.Groups[2].Value)));
            return used;
        }

        private static string CellKey(int x, int y)
        {
            return x + "," + y;
        }

        private static bool IsTransparent(RawImage atlas, int x, int y)
        {
            if (x < 0 || y < 0 || x + CellSize > atlas.Width ||
                y + CellSize > atlas.Height)
                return false;
            for (int py = y; py < y + CellSize; py++)
                for (int px = x; px < x + CellSize; px++)
                    if (atlas.Pixels[py * atlas.Stride + px * 4 + 3] != 0)
                        return false;
            return true;
        }

        private static bool TileEquals(
            RawImage atlas, RawImage icon, int x, int y)
        {
            if (x < 0 || y < 0 || x + CellSize > atlas.Width ||
                y + CellSize > atlas.Height)
                return false;
            for (int py = 0; py < CellSize; py++)
                for (int px = 0; px < CellSize; px++)
                {
                    int ai = (y + py) * atlas.Stride + (x + px) * 4;
                    int ii = py * icon.Stride + px * 4;
                    if (!PixelEquals(atlas.Pixels, ai, icon.Pixels, ii))
                        return false;
                }
            return true;
        }

        private static void WriteIcon(
            RawImage atlas, RawImage icon, int x, int y)
        {
            for (int py = 0; py < CellSize; py++)
                Buffer.BlockCopy(icon.Pixels, py * icon.Stride,
                    atlas.Pixels, (y + py) * atlas.Stride + x * 4,
                    CellSize * 4);
        }

        private static void ClearTile(RawImage atlas, int x, int y)
        {
            byte[] clear = new byte[CellSize * 4];
            for (int py = 0; py < CellSize; py++)
                Buffer.BlockCopy(clear, 0, atlas.Pixels,
                    (y + py) * atlas.Stride + x * 4, clear.Length);
        }

        private static bool PixelEquals(
            byte[] left, int li, byte[] right, int ri)
        {
            return left[li] == right[ri] &&
                left[li + 1] == right[ri + 1] &&
                left[li + 2] == right[ri + 2] &&
                left[li + 3] == right[ri + 3];
        }

        private static void RequireAtlasDimensions(RawImage atlas)
        {
            if (atlas.Width != 4096 || atlas.Height != 4096)
                throw new InvalidDataException(
                    "IconMapSurvival.png is not the supported 4096 by 4096 atlas.");
        }

        private static void RequireDimensions(
            RawImage atlas, RawImage icon)
        {
            RequireAtlasDimensions(atlas);
            if (icon.Width != CellSize || icon.Height != CellSize)
                throw new InvalidDataException(
                    "The embedded ScrapLab icon is not 96 by 96 pixels.");
        }

        private static RawImage LoadImage(byte[] bytes, string name)
        {
            try
            {
                using (MemoryStream input = new MemoryStream(bytes, false))
                {
                    PngBitmapDecoder decoder = new PngBitmapDecoder(input,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count != 1)
                        throw new InvalidDataException(
                            name + " must contain one PNG frame.");
                    BitmapSource source = decoder.Frames[0];
                    if (source.Format != PixelFormats.Bgra32)
                        source = new FormatConvertedBitmap(source,
                            PixelFormats.Bgra32, null, 0);
                    int stride = source.PixelWidth * 4;
                    byte[] pixels = new byte[stride * source.PixelHeight];
                    source.CopyPixels(pixels, stride, 0);
                    return new RawImage
                    {
                        Width = source.PixelWidth,
                        Height = source.PixelHeight,
                        Stride = stride,
                        Pixels = pixels
                    };
                }
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    name + " is not a readable PNG image.", exception);
            }
        }

        private static byte[] SavePng(RawImage image)
        {
            BitmapSource source = BitmapSource.Create(
                image.Width, image.Height, 96, 96,
                PixelFormats.Bgra32, null, image.Pixels, image.Stride);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (MemoryStream output = new MemoryStream())
            {
                encoder.Save(output);
                return output.ToArray();
            }
        }
    }
}
