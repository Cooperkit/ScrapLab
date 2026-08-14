using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal static class NetworkStorageChestPatchService
    {
        private const string ModKey = "NetworkStorageChest";
        private const string DefinitionVersion = "1";
        internal const string PartUuid = "bc7576a7-f226-459a-883c-e8460e955d63";
        internal const string VerifiedSteamBuildId = "24529696";
        internal const string VerifiedGameVersion = "1.0.5.876";
        private const string ShapeSetPath = "$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/NetworkStorageChest.shapeset";
        private const string ResourcePrefix = "RaidRescue.Parts.NetworkStorageChest.";

        private static readonly string ShapesIndexRelative = Path.Combine("Survival", "Objects", "Database", "shapesets.json");
        private static readonly string ItemsRelative = Path.Combine("Survival", "Scripts", "game", "survival_items.lua");
        private static readonly string PipesRelative = Path.Combine("Survival", "Scripts", "game", "util", "pipes.lua");
        private static readonly string RecipesRelative = Path.Combine("Survival", "CraftingRecipes", "craftbot", "craftbot_core.json");
        private static readonly string RecipeManagerRelative = Path.Combine("Survival", "Scripts", "game", "managers", "RecipeManager.lua");
        private static readonly string IconXmlRelative = Path.Combine("Survival", "Gui", "IconMapSurvival.xml");
        private static readonly string IconPngRelative = Path.Combine("Survival", "Gui", "IconMapSurvival.png");

        private static readonly string[,] Languages = new string[,]
        {
            { "Brazilian", "3C6EAC82C2B49E9215196883FCB8B74AD749CBE82EFEA151D01D734A98592440" },
            { "Chinese", "03C675DF2720E7148E94140226A62BA3F7F96AA266851D5DA3B793CBC90D636D" },
            { "English", "BA935E110D8B0A5FC4AEFAAB0E76A7AA4A26ACEE6B6AD093F0F7E801B05AF3EE" },
            { "French", "4A56BC8A64C378DED64A40EEDC05FDA35DC9EB3F837E1F6850EEA05E0EEC2F4F" },
            { "German", "7D48F80C66A21F555003EB287D2E3E35031F05528305825498906D783DB286F8" },
            { "Italian", "C8DE862CFB7F8A2B6833B0BAA92195C4D4A09B1BB16D959B0344DDB6D19D836B" },
            { "Japanese", "98D81E337E4EC87B2BE2D5B5DE0209BEDFC54096AC153FB0F1BBE23D85E1B7C0" },
            { "Korean", "579693B35D7D9F95A997944667CDE15ECAB9C4740C55375ED3087FCA5C719BDD" },
            { "Polish", "B45802E61196CECEAB7BC51A0075F3051ED069EB4D1571010A8999CB79C544E9" },
            { "Russian", "8DC2C7D60D2E7756D18123588596F7108F1C657FFF99C9924B089DA8EA3BE855" },
            { "Spanish", "ECA2E0850CC3DC56AE811B2EB4043B0C657D407D9E8F813F9CBC7E3BF7EB6704" }
        };

        private sealed class LocalizedEntry
        {
            public string inventoryTitle { get; set; }
            public string inventoryUpper { get; set; }
            public string inventoryDescription { get; set; }
        }

        private sealed class TextState
        {
            public string RelativePath, DisplayName, KnownHash, Path, PatchedText, CleanText;
            public LuaTextDocument Document;
            public bool Clean, Installed, Known, IsIconXml;
        }

        private sealed class OwnedAsset
        {
            public string RelativePath, DisplayName, ResourceName, Path;
            public byte[] Bytes;
            public bool Missing, Exact;
        }

        private sealed class ProbeState
        {
            public readonly List<TextState> Texts = new List<TextState>();
            public readonly List<OwnedAsset> Owned = new List<OwnedAsset>();
            public byte[] AtlasBytes;
            public string AtlasPath, AtlasHash;
            public List<ScrapLabIconAtlasCoordinator.IconAsset> IconCatalog;
            public ScrapLabIconAtlasCoordinator.CatalogPlan CatalogPlan;
            public ScrapLabIconAtlasCoordinator.AtlasInfo AtlasInfo;
            public ScrapLabIconAtlasCoordinator.SharedAtlasReceipt AtlasReceipt;
            public bool AtlasClean, AtlasInstalled, AtlasKnown, OwnedClean, OwnedInstalled, AllClean, AllInstalled, AllKnownClean;
        }

        public static GamePatchResult GetStatus()
        {
            try
            {
                string gamePath = GamePatchService.FindGameInstall();
                if (String.IsNullOrEmpty(gamePath)) return Failure("Scrap Mechanic was not found.");
                return GetStatusAt(gamePath);
            }
            catch (Exception exception) { return Failure(exception.Message); }
        }

        internal static GamePatchResult GetStatusAt(string gamePath)
        {
            GamePatchResult result = NewResult(gamePath, false);
            try
            {
                SteamBuildInfo build = ReadBuild(gamePath, result);
                ProbeState state = Probe(gamePath);
                AdaptivePatchReceipt receipt = AdaptivePatchSupport.LoadReceipt(ModKey);
                result.Success = true;
                if (state.AllInstalled)
                {
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build,
                        state.AllKnownClean ? PatchCompatibilityState.KnownInstalled : PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        "The Network Storage Chest part, recipe, runtime, localization, and icon are intact.");
                    return result;
                }
                if (state.AllClean)
                {
                    string reason;
                    bool canApply = CanApplyClean(state, build, out reason);
                    if (receipt != null && canApply)
                        AdaptivePatchSupport.FillResult(result, build, "REINSTALL REQUIRED - SAVE PART AT RISK", true, true,
                            "Steam removed the Network Storage Chest registrations. Reinstall before loading a world that may contain the part.");
                    else
                        AdaptivePatchSupport.FillResult(result, build,
                            state.AllKnownClean ? PatchCompatibilityState.KnownClean : canApply ? PatchCompatibilityState.CompatibleUpdate : PatchCompatibilityState.OtherModification,
                            !state.AllKnownClean, canApply, reason);
                    return result;
                }
                AdaptivePatchSupport.FillResult(result, build, PatchCompatibilityState.PartialConflict, false, false,
                    "A Network Storage Chest registration, owned file, recipe, language entry, or icon is missing, duplicated, or edited.");
                return result;
            }
            catch (Exception exception) { result.Error = exception.Message; return result; }
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning()) return Failure("Scrap Mechanic is running. Close the game completely before changing Network Storage Chest.");
            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath)) return Failure("Scrap Mechanic was not found.");
            GamePatchResult result = SetEnabledAt(gamePath,
                ProductPaths.LocalDataPath("Game Backups", "Scrap Mechanic", "Secret Mods"), enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(string gamePath, string backupRoot, bool enabled)
        {
            GamePatchResult result = NewResult(gamePath, enabled);
            try
            {
                SteamBuildInfo build = ReadBuild(gamePath, result);
                ProbeState state = Probe(gamePath);
                if (enabled && state.AllInstalled)
                {
                    result.Success = true; result.Installed = true; result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build, PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true, "Network Storage Chest is already installed.");
                    return result;
                }
                if (!enabled && state.AllClean)
                {
                    CleanupOwnedFiles(state);
                    AdaptivePatchSupport.DeleteReceipt(ModKey);
                    AdaptivePatchSupport.DeleteBuildActivation(ModKey);
                    result.Success = true; result.Installed = false; result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build,
                        state.AllKnownClean ? PatchCompatibilityState.KnownClean : PatchCompatibilityState.CompatibleUpdate,
                        !state.AllKnownClean, true, "Network Storage Chest is already removed.");
                    return result;
                }
                if (enabled)
                {
                    if (!state.AllClean) throw new InvalidOperationException("Network Storage Chest cannot be installed because its protected state is partial or conflicting.");
                    string reason;
                    if (!CanApplyClean(state, build, out reason)) throw new InvalidOperationException("Network Storage Chest cannot be installed: " + reason);
                }
                else if (!state.AllInstalled)
                    throw new InvalidOperationException("Network Storage Chest cannot be removed because a protected registration, file, or icon was edited.");

                List<AtomicCustomPartFilePlan> plans = enabled ? BuildInstallPlans(state) : BuildRemovePlans(state, backupRoot);
                AtomicCustomPartPatchSupport.Apply(ModKey, "Network Storage Chest", DefinitionVersion,
                    plans, result, gamePath, backupRoot, build, enabled, state.IconCatalog);
                result.Success = true; result.Installed = enabled;
                result.Changes.Add(enabled
                    ? "Installed the Network Storage Chest, five-slot deposit tray, catalog, and server-authoritative transfers."
                    : "Removed the Network Storage Chest registrations, recipe, runtime, localization, and icon entry.");
                result.Changes.Add(enabled
                    ? "Added the default-unlocked Craftbot recipe and optional Wireless Vacuum Pipe integration."
                    : "Preserved Wireless Vacuum Pipe and every other shared ScrapLab icon and patch.");
                AdaptivePatchSupport.FillResult(result, build,
                    enabled ? (state.AllKnownClean ? PatchCompatibilityState.KnownInstalled : PatchCompatibilityState.AdaptiveInstalled)
                            : (state.AllKnownClean ? PatchCompatibilityState.KnownClean : PatchCompatibilityState.CompatibleUpdate),
                    !state.AllKnownClean, true, enabled ? "Network Storage Chest was installed and verified." : "Network Storage Chest was removed and verified.");
                AdaptivePatchSupport.QueueBuildActivation(result, ModKey, enabled);
                SecretModBackupRetention.Prune(backupRoot, ModKey, result.BackupPath, result);
                return result;
            }
            catch (Exception exception) { result.Success = false; result.Error = exception.Message; return result; }
        }

        private static ProbeState Probe(string gamePath)
        {
            ProbeState state = new ProbeState();
            state.Texts.Add(ReadText(gamePath, ShapesIndexRelative, "shapesets.json", "FF30F988FCDF775604AA54E1AF3E97CBCC4AE45F7EDCAB7B528694933D7E2511", ShapeSetPath, PatchShapes, UnpatchShapes, false));
            state.Texts.Add(ReadText(gamePath, ItemsRelative, "survival_items.lua", "ACDAD2CF9163655F87796D996A58DDE381AC1221B1337AEF049E38066B199789", PartUuid, PatchItems, UnpatchItems, false));
            state.Texts.Add(ReadText(gamePath, PipesRelative, "pipes.lua", "9E494D72BE3CDB8E666F4B1B2AFD34C2105CA2E653468251ABE8D302180F8146", "obj_container_network_storage_chest", PatchPipes, UnpatchPipes, false));
            state.Texts.Add(ReadText(gamePath, RecipesRelative, "craftbot_core.json", "7AE14EA8224965276835A3E1C7FCFA7366EC91810F8FEE339C7E584A0022157E", PartUuid, PatchRecipe, UnpatchRecipe, false));
            state.Texts.Add(ReadText(gamePath, RecipeManagerRelative, "RecipeManager.lua", "4290B7B0FF9370B5C6E4D3E98DD3AC62B3934A80DAB36A6EA7EE18D2C62400B5", "Network Storage Chest default unlock", PatchRecipeManager, UnpatchRecipeManager, false));
            state.Texts.Add(ReadText(gamePath, IconXmlRelative, "IconMapSurvival.xml", "5DA34EF427C912BDF64BD1993834A78DBD86F11DFF16FD63B61F3FA9C1ECDDDB", PartUuid, delegate(string text) { return text; }, delegate(string text) { return text; }, true));

            Dictionary<string, LocalizedEntry> localization = LoadLocalization();
            for (int index = 0; index < Languages.GetLength(0); index++)
            {
                string language = Languages[index, 0];
                LocalizedEntry entry = localization[language];
                state.Texts.Add(ReadLanguage(gamePath, language, Languages[index, 1], entry));
            }

            TextState iconXml = FindText(state, IconXmlRelative);
            state.AtlasPath = Path.Combine(gamePath, IconPngRelative);
            if (!File.Exists(state.AtlasPath)) throw new FileNotFoundException("IconMapSurvival.png was not found.", state.AtlasPath);
            state.AtlasBytes = File.ReadAllBytes(state.AtlasPath);
            state.AtlasHash = AdaptivePatchSupport.Sha256(state.AtlasBytes);
            state.IconCatalog = ScrapLabIconAtlasCoordinator.LoadCatalog();
            byte[] icon = ScrapLabIconAtlasCoordinator.FindCatalogIcon(state.IconCatalog, PartUuid).Bytes;
            state.AtlasInfo = ScrapLabIconAtlasCoordinator.Inspect(iconXml.Document.NormalizedText, state.AtlasBytes, icon, PartUuid);
            state.AtlasInstalled = state.AtlasInfo.EntryPresent && state.AtlasInfo.IconPresent;
            try
            {
                state.CatalogPlan = ScrapLabIconAtlasCoordinator.EnsureCatalog(iconXml.Document.NormalizedText, state.AtlasBytes, state.IconCatalog);
                state.AtlasClean = !state.AtlasInfo.EntryPresent;
            }
            catch { state.AtlasClean = false; }
            state.AtlasReceipt = ScrapLabIconAtlasCoordinator.LoadReceipt(AdaptivePatchSupport.GetSharedStatePath("ScrapLab-Icon-Pack.json"));
            state.AtlasKnown = String.Equals(state.AtlasHash, "4288CAA081C8674E8D69640C717802C3883E1AA53181C6A9ABA86BBCFE7D9146", StringComparison.OrdinalIgnoreCase)
                || ScrapLabIconAtlasCoordinator.IsTrustedReceipt(state.AtlasReceipt, state.AtlasHash, state.IconCatalog);
            if (state.AtlasReceipt != null && String.Equals(state.AtlasReceipt.IconXmlHash, iconXml.Document.OriginalHash, StringComparison.OrdinalIgnoreCase)) iconXml.Known = true;

            AddOwned(state, gamePath, Path.Combine("Survival", "Scripts", "ScrapLab", "Parts", "NetworkStorageChest", "NetworkStorageChest.lua"), "Network Storage Chest script", ResourcePrefix + "NetworkStorageChest.lua");
            AddOwned(state, gamePath, Path.Combine("Survival", "Scripts", "ScrapLab", "Storage", "NetworkInventoryIndex.lua"), "Network inventory index", ResourcePrefix + "NetworkInventoryIndex.lua");
            AddOwned(state, gamePath, Path.Combine("Survival", "Gui", "JsonGuis", "ScrapLab", "Parts", "NetworkStorageChest.gui"), "Network Storage Chest GUI", ResourcePrefix + "NetworkStorageChest.gui");
            AddOwned(state, gamePath, Path.Combine("Survival", "Gui", "JsonGuis", "ScrapLab", "Parts", "NetworkStorageChestItem.gui"), "Network Storage Chest item card", ResourcePrefix + "NetworkStorageChestItem.gui");
            AddOwned(state, gamePath, Path.Combine("Survival", "Objects", "Database", "ShapeSets", "ScrapLab", "Parts", "NetworkStorageChest.shapeset"), "Network Storage Chest shape set", ResourcePrefix + "NetworkStorageChest.shapeset");
            AddOwned(state, gamePath, Path.Combine("Survival", "Scripts", "ScrapLab", "Parts", "NetworkStorageChest", "NetworkStorageChest.localization.json"), "Network Storage Chest localization", ResourcePrefix + "NetworkStorageChest.localization.json");

            state.OwnedClean = true; state.OwnedInstalled = true;
            foreach (OwnedAsset owned in state.Owned) { state.OwnedClean &= owned.Missing; state.OwnedInstalled &= owned.Exact; }
            bool textsClean = true, textsInstalled = true, known = state.AtlasKnown;
            foreach (TextState text in state.Texts)
            {
                if (text.IsIconXml) { textsClean &= state.AtlasClean; textsInstalled &= state.AtlasInstalled; }
                else { textsClean &= text.Clean; textsInstalled &= text.Installed; }
                known &= text.Known;
            }
            state.AllClean = textsClean && state.OwnedClean;
            state.AllInstalled = textsInstalled && state.OwnedInstalled && state.AtlasInstalled;
            state.AllKnownClean = known;
            return state;
        }

        private static TextState ReadText(string gamePath, string relative, string display, string knownHash, string marker,
            Func<string, string> patch, Func<string, string> unpatch, bool iconXml)
        {
            string path = Path.Combine(gamePath, relative);
            if (!File.Exists(path)) throw new FileNotFoundException(display + " was not found.", path);
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            AdaptivePatchSupport.RequireAdaptiveFormat(document, display);
            int count = AdaptivePatchSupport.Count(document.NormalizedText, marker);
            TextState state = new TextState { RelativePath = relative, DisplayName = display, KnownHash = knownHash, Path = path, Document = document, IsIconXml = iconXml,
                Known = String.Equals(document.OriginalHash, knownHash, StringComparison.OrdinalIgnoreCase) || IsTrustedExistingOutput(relative, document.OriginalHash) };
            if (iconXml) { state.Clean = count == 0; state.Installed = count == 1; return state; }
            if (count == 0) { state.PatchedText = patch(document.NormalizedText); state.Clean = true; }
            else if (count == 1)
            {
                state.CleanText = unpatch(document.NormalizedText);
                state.PatchedText = patch(state.CleanText);
                state.Installed = String.Equals(state.PatchedText, document.NormalizedText, StringComparison.Ordinal);
            }
            return state;
        }

        private static TextState ReadLanguage(string gamePath, string language, string knownHash, LocalizedEntry entry)
        {
            string relative = Path.Combine("Survival", "Gui", "Language", language, "inventoryDescriptions.json");
            return ReadText(gamePath, relative, language + " inventory descriptions", knownHash, PartUuid,
                delegate(string text) { return PatchLanguage(text, entry); }, delegate(string text) { return UnpatchLanguage(text, entry); }, false);
        }

        private static bool CanApplyClean(ProbeState state, SteamBuildInfo build, out string reason)
        {
            if (state.AllKnownClean) { reason = "Verified Steam build 24529696 Network Storage Chest targets."; return true; }
            if (build != null && build.Valid && String.Equals(build.BuildId, VerifiedSteamBuildId, StringComparison.Ordinal)
                && String.Equals(build.GameVersion, VerifiedGameVersion, StringComparison.Ordinal))
            { reason = "A protected Network Storage Chest target differs from the verified current Steam build."; return false; }
            List<string> unknown = new List<string>();
            foreach (TextState text in state.Texts) if (!text.Known) unknown.Add(text.Path);
            if (!state.AtlasKnown) unknown.Add(state.AtlasPath);
            return AdaptivePatchSupport.CanAdaptCleanFiles(build, unknown, out reason);
        }

        private static List<AtomicCustomPartFilePlan> BuildInstallPlans(ProbeState state)
        {
            List<AtomicCustomPartFilePlan> plans = new List<AtomicCustomPartFilePlan>();
            foreach (TextState text in state.Texts) if (!text.IsIconXml) AddTextPlan(plans, text, text.PatchedText);
            TextState xml = FindText(state, IconXmlRelative);
            ScrapLabIconAtlasCoordinator.CatalogPlan catalog = state.CatalogPlan ?? ScrapLabIconAtlasCoordinator.EnsureCatalog(xml.Document.NormalizedText, state.AtlasBytes, state.IconCatalog);
            ScrapLabIconAtlasCoordinator.IconPlacement placement = catalog.Placements[PartUuid];
            AddTextPlan(plans, xml, PatchIconXml(xml.Document.NormalizedText, placement.X, placement.Y));
            if (catalog.AtlasChanged) AddBinaryPlan(plans, IconPngRelative, "IconMapSurvival.png", state.AtlasPath, state.AtlasBytes, catalog.AtlasBytes, true, false);
            foreach (OwnedAsset owned in state.Owned) AddOwnedPlan(plans, owned, owned.Bytes);
            return plans;
        }

        private static List<AtomicCustomPartFilePlan> BuildRemovePlans(ProbeState state, string backupRoot)
        {
            List<AtomicCustomPartFilePlan> plans = new List<AtomicCustomPartFilePlan>();
            foreach (TextState text in state.Texts) if (!text.IsIconXml) AddTextPlan(plans, text, text.CleanText);
            TextState xml = FindText(state, IconXmlRelative);
            int x, y;
            if (!ScrapLabIconAtlasCoordinator.TryGetEntry(xml.Document.NormalizedText, PartUuid, out x, out y)) throw new InvalidDataException("The Network Storage Chest icon entry is missing.");
            string xmlOutput = UnpatchIconXml(xml.Document.NormalizedText, x, y);
            AddTextPlan(plans, xml, xmlOutput);
            string baselinePath = Path.Combine(backupRoot, "ScrapLab-Shared-Icon-Atlas", "IconMapSurvival.baseline.png");
            byte[] baseline = File.Exists(baselinePath) ? File.ReadAllBytes(baselinePath) : null;
            byte[] atlasOutput = ScrapLabIconAtlasCoordinator.RemoveCatalogWhenUnused(xmlOutput, state.AtlasBytes, state.IconCatalog, baseline);
            if (!BytesEqual(atlasOutput, state.AtlasBytes)) AddBinaryPlan(plans, IconPngRelative, "IconMapSurvival.png", state.AtlasPath, state.AtlasBytes, atlasOutput, true, false);
            foreach (OwnedAsset owned in state.Owned) AddOwnedPlan(plans, owned, null);
            return plans;
        }

        private static void AddOwned(ProbeState state, string gamePath, string relative, string display, string resource)
        {
            byte[] bytes = GetResource(resource); string path = Path.Combine(gamePath, relative); bool missing = !File.Exists(path);
            state.Owned.Add(new OwnedAsset { RelativePath = relative, DisplayName = display, ResourceName = resource, Path = path, Bytes = bytes,
                Missing = missing, Exact = !missing && BytesEqual(File.ReadAllBytes(path), bytes) });
        }

        private static void AddTextPlan(List<AtomicCustomPartFilePlan> plans, TextState state, string output)
        {
            if (output == null) throw new InvalidDataException(state.DisplayName + " has no verified output.");
            AddBinaryPlan(plans, state.RelativePath, state.DisplayName, state.Path, state.Document.OriginalBytes, state.Document.Render(output), false, false);
        }

        private static void AddOwnedPlan(List<AtomicCustomPartFilePlan> plans, OwnedAsset owned, byte[] output)
        {
            bool exists = File.Exists(owned.Path); byte[] source = exists ? File.ReadAllBytes(owned.Path) : null;
            AddBinaryPlan(plans, owned.RelativePath, owned.DisplayName, owned.Path, source, output, false, output == null);
        }

        private static void AddBinaryPlan(List<AtomicCustomPartFilePlan> plans, string relative, string display, string path, byte[] source, byte[] output, bool atlas, bool forceDelete)
        {
            bool exists = source != null;
            plans.Add(new AtomicCustomPartFilePlan { RelativePath = relative, DisplayName = display, Path = path, SourceExists = exists,
                SourceBytes = source, OutputBytes = output, SourceHash = exists ? AdaptivePatchSupport.Sha256(source) : "MISSING",
                OutputHash = output == null ? "MISSING" : AdaptivePatchSupport.Sha256(output), IsAtlas = atlas, ForceDeleteOnRemove = forceDelete });
        }

        private static string PatchShapes(string text) { return InsertAfterUnique(text, "\t\t\"$SURVIVAL_DATA/Objects/Database/ShapeSets/interactive_shared.shapeset\",", "\n\t\t\"" + ShapeSetPath + "\","); }
        private static string UnpatchShapes(string text) { return RemoveUnique(text, "\n\t\t\"" + ShapeSetPath + "\","); }
        private static string PatchItems(string text) { return InsertAfterUnique(text, "\tobj_container_smallchest_pipe = sm.uuid.new( \"4c474cff-3f6a-4306-93d1-c4c74578afd2\" ),", "\n\t-- SCRAPLAB PART: Network Storage Chest UUID.\n\tobj_container_network_storage_chest = sm.uuid.new( \"" + PartUuid + "\" ),"); }
        private static string UnpatchItems(string text) { return RemoveUnique(text, "\n\t-- SCRAPLAB PART: Network Storage Chest UUID.\n\tobj_container_network_storage_chest = sm.uuid.new( \"" + PartUuid + "\" ),"); }
        private static string PatchPipes(string text) { return ReplaceUnique(text, "\tobj_container_smallchest_pipe\n}", "\tobj_container_smallchest_pipe,\n\t-- SCRAPLAB PART: Network Storage Chest pipe container.\n\tobj_container_network_storage_chest\n}"); }
        private static string UnpatchPipes(string text) { return ReplaceUnique(text, "\tobj_container_smallchest_pipe,\n\t-- SCRAPLAB PART: Network Storage Chest pipe container.\n\tobj_container_network_storage_chest\n}", "\tobj_container_smallchest_pipe\n}"); }

        private static string RecipeEntry { get { return "\t{\n\t\t\"itemId\": \"" + PartUuid + "\",\n\t\t\"quantity\": 1,\n\t\t\"craftTime\": 30,\n\t\t\"ingredientList\": [\n\t\t\t{ \"quantity\": 1, \"itemId\": \"4c474cff-3f6a-4306-93d1-c4c74578afd2\" },\n\t\t\t{ \"quantity\": 10, \"itemId\": \"5530e6a0-4748-4926-b134-50ca9ecb9dcf\" },\n\t\t\t{ \"quantity\": 20, \"itemId\": \"f152e4df-bc40-44fb-8d20-3b3ff70cdfe3\" }\n\t\t]\n\t}"; } }
        private static string PatchRecipe(string text) { int end = text.LastIndexOf("\n]", StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("craftbot_core.json ending changed."); return text.Substring(0, end) + ",\n" + RecipeEntry + text.Substring(end); }
        private static string UnpatchRecipe(string text) { return RemoveUnique(text, ",\n" + RecipeEntry); }
        private static string PatchRecipeManager(string text) { return InsertAfterUnique(text, "\tITEMS.obj_container_smallchest_pipe,", "\n\t-- SCRAPLAB PART: Network Storage Chest default unlock.\n\tITEMS.obj_container_network_storage_chest,"); }
        private static string UnpatchRecipeManager(string text) { return RemoveUnique(text, "\n\t-- SCRAPLAB PART: Network Storage Chest default unlock.\n\tITEMS.obj_container_network_storage_chest,"); }

        private static string PatchIconXml(string text, int x, int y)
        {
            string entry = "            <!-- SCRAPLAB PART: Network Storage Chest icon. -->\n            <Index name=\"" + PartUuid + "\">\n                <Frame point=\"" + x + " " + y + "\"/>\n            </Index>\n";
            return InsertBeforeUnique(text, "        </Group>", entry);
        }
        private static string UnpatchIconXml(string text, int x, int y) { return RemoveUnique(text, "            <!-- SCRAPLAB PART: Network Storage Chest icon. -->\n            <Index name=\"" + PartUuid + "\">\n                <Frame point=\"" + x + " " + y + "\"/>\n            </Index>\n"); }

        private static string LanguageEntry(LocalizedEntry entry)
        {
            return "\t\"" + PartUuid + "\": {\n\t\t\"description\": \"" + JsonEscape(entry.inventoryDescription) + "\",\n\t\t\"title\": \"" + JsonEscape(entry.inventoryTitle) + "\",\n\t\t\"upperCaseTitle\": \"" + JsonEscape(entry.inventoryUpper) + "\"\n\t}";
        }
        private static string PatchLanguage(string text, LocalizedEntry entry) { int end = text.LastIndexOf("\n}", StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("Inventory descriptions ending changed."); return text.Substring(0, end) + ",\n" + LanguageEntry(entry) + text.Substring(end); }
        private static string UnpatchLanguage(string text, LocalizedEntry entry) { return RemoveUnique(text, ",\n" + LanguageEntry(entry)); }

        private static string InsertAfterUnique(string text, string anchor, string addition) { RequireCount(text, anchor, 1); return text.Replace(anchor, anchor + addition); }
        private static string InsertBeforeUnique(string text, string anchor, string addition) { RequireCount(text, anchor, 1); return text.Replace(anchor, addition + anchor); }
        private static string ReplaceUnique(string text, string from, string to) { RequireCount(text, from, 1); return text.Replace(from, to); }
        private static string RemoveUnique(string text, string value) { RequireCount(text, value, 1); return text.Replace(value, ""); }
        private static void RequireCount(string text, string value, int expected) { int count = AdaptivePatchSupport.Count(text, value); if (count != expected) throw new InvalidDataException("A protected Network Storage Chest snippet changed or appears " + count + " times."); }
        private static string JsonEscape(string value) { return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }

        private static Dictionary<string, LocalizedEntry> LoadLocalization()
        {
            string json = Encoding.UTF8.GetString(GetResource(ResourcePrefix + "NetworkStorageChest.localization.json"));
            Dictionary<string, LocalizedEntry> result = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<Dictionary<string, LocalizedEntry>>(json);
            if (result == null || result.Count != 11) throw new InvalidDataException("The embedded Network Storage Chest localization catalog is incomplete.");
            return result;
        }
        private static TextState FindText(ProbeState state, string relative) { foreach (TextState text in state.Texts) if (String.Equals(text.RelativePath, relative, StringComparison.OrdinalIgnoreCase)) return text; throw new InvalidOperationException("A prepared Network Storage Chest target is missing: " + relative); }
        private static byte[] GetResource(string name) { using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) { if (stream == null) throw new InvalidOperationException("The embedded Network Storage Chest asset is missing: " + name); using (MemoryStream output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); } } }
        private static bool IsTrustedExistingOutput(string relative, string hash)
        {
            string[] keys = { "RaidDetector", "WirelessVacuumPipe", "BetterPlasmaDrills", "FullSpeedCarrying", "BetterFreezerBeehive", "BetterEngines", "ResourceLocator", "ChemicalFertilizerSplash", "DualFluidCannon", "DeveloperCommands", "RevivalBuffRecovery" };
            foreach (string key in keys) { AdaptivePatchReceiptFile file = AdaptivePatchSupport.FindReceiptFile(AdaptivePatchSupport.LoadReceipt(key), relative); if (file != null && String.Equals(file.OutputHash, hash, StringComparison.OrdinalIgnoreCase)) return true; }
            return false;
        }
        private static SteamBuildInfo ReadBuild(string gamePath, GamePatchResult result) { string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe"); if (!File.Exists(executable)) throw new FileNotFoundException("ScrapMechanic.exe was not found.", executable); result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion; return AdaptivePatchSupport.GetSteamBuild(gamePath, result.GameVersion); }
        private static void CleanupOwnedFiles(ProbeState state) { foreach (OwnedAsset owned in state.Owned) if (File.Exists(owned.Path) && BytesEqual(File.ReadAllBytes(owned.Path), owned.Bytes)) File.Delete(owned.Path); }
        private static bool BytesEqual(byte[] left, byte[] right) { if (left == null || right == null || left.Length != right.Length) return false; int difference = 0; for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0; }
        private static GamePatchResult NewResult(string gamePath, bool installed) { return new GamePatchResult { GamePath = gamePath, Installed = installed, Changes = new List<string>() }; }
        private static GamePatchResult Failure(string error) { return new GamePatchResult { Success = false, Error = error, Changes = new List<string>() }; }
    }
}
