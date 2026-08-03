using System;
using System.Collections.Generic;
using System.IO;

namespace RaidRescue
{
    internal static class BetterPlasmaDrillsPatchService
    {
        private const string ModKey = "BetterPlasmaDrills";
        private const string DefinitionVersion = "1";
        internal const string VerifiedSteamBuildId = "24499589";
        internal const string VerifiedGameVersion = "1.0.4.874";
        internal const string Level4Uuid =
            "02b5a169-d892-48cf-bdb3-c994347cbfa7";
        internal const string Level5Uuid =
            "53dbc8c3-522d-4fa9-b50b-3af1c955b397";
        private const string Level3Uuid =
            "4a3d40d4-ce86-4a68-b042-8d107ea39d78";

        private static readonly string PlasmaPath = Path.Combine(
            "Survival", "Scripts", "game", "interactables", "PlasmaDrill.lua");
        private static readonly string ItemsPath = Path.Combine(
            "Survival", "Scripts", "game", "survival_items.lua");
        private static readonly string CollectionsPath = Path.Combine(
            "Survival", "Scripts", "game", "survival_collections.lua");
        private static readonly string CarryPath = Path.Combine(
            "Survival", "Scripts", "game", "tools", "CarryTool.lua");
        private static readonly string ShapesPath = Path.Combine(
            "Survival", "Objects", "Database", "ShapeSets", "powertools.shapeset");
        private static readonly string IconsPath = Path.Combine(
            "Survival", "Gui", "IconMapSurvival.xml");

        private const string PlasmaHash = "3E32DBF0671E176A270BD040866DDBDF1EF58F53C470534022E7AF2060E42AE9";
        private const string ItemsHash = "ACDAD2CF9163655F87796D996A58DDE381AC1221B1337AEF049E38066B199789";
        private const string CollectionsHash = "E3128D58619EFABB9E0A1927E249A5300E42BCB5C9ACEC01FB4A67AB6112F1C0";
        private const string CarryHash = "BF08DEB38238C34B1C3A884AF10C4FA153846E21BA287E2F8D983BC7DB908200";
        private const string ShapesHash = "BC03D84C625D2573C1A2371AB2EF5FEA7D6D91B93514E624A768C1A7972CE4D0";
        private const string IconsHash = "5DA34EF427C912BDF64BD1993834A78DBD86F11DFF16FD63B61F3FA9C1ECDDDB";

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

        public static GamePatchResult GetStatus()
        {
            bool wasInstalled = AdaptivePatchSupport.LoadReceipt(ModKey) != null;
            GamePatchResult result = AdaptiveMultiFileModService.GetStatus(GetDefinition());
            if (wasInstalled && result.Success && !result.Installed && result.CanApply)
            {
                result.CompatibilityState = "REINSTALL REQUIRED - SAVE PARTS AT RISK";
                result.CompatibilityReason =
                    "Steam replaced Better Plasma Drills. Reinstall it before loading worlds that may contain level-4 or level-5 drills.";
            }
            return result;
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            return AdaptiveMultiFileModService.SetEnabled(GetDefinition(), enabled);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot, bool enabled)
        {
            return AdaptiveMultiFileModService.SetEnabledAt(
                GetDefinition(), gamePath, backupRoot, enabled);
        }

        internal static string PatchPlasmaText(string text)
        {
            string value = ReplaceUnique(text, Level1Cost,
                Level1Cost + "\n\t\tupgradeInfo = { Speed = \"+50%\", Range = \"+50%\", Settings = 3 },",
                "level-1 upgrade data");
            value = ReplaceUnique(value, Level2Cost,
                Level2Cost + "\n\t\tupgradeInfo = { Speed = \"+50%\", Range = \"+50%\", Settings = 3 },",
                "level-2 upgrade data");
            value = ReplaceUnique(value, OriginalLevel3, PatchedLevel3,
                "level-3 drill record");
            value = ReplaceUnique(value, OriginalRadiusEnd, PatchedRadiusEnd,
                "drill radius settings");
            value = ReplaceUnique(value, OriginalInteractUpgradeInfo,
                PatchedInteractUpgradeInfo, "open-GUI upgrade information");
            return ReplaceUnique(value, OriginalRefreshUpgradeInfo,
                PatchedRefreshUpgradeInfo, "post-upgrade GUI information");
        }

        internal static string UnpatchPlasmaText(string text)
        {
            string value = ReplaceUnique(text, PatchedRefreshUpgradeInfo,
                OriginalRefreshUpgradeInfo, "post-upgrade GUI information");
            value = ReplaceUnique(value, PatchedInteractUpgradeInfo,
                OriginalInteractUpgradeInfo, "open-GUI upgrade information");
            value = ReplaceUnique(value, PatchedRadiusEnd, OriginalRadiusEnd,
                "drill radius settings");
            value = ReplaceUnique(value, PatchedLevel3, OriginalLevel3,
                "level-3 drill record");
            value = ReplaceUnique(value,
                Level2Cost + "\n\t\tupgradeInfo = { Speed = \"+50%\", Range = \"+50%\", Settings = 3 },",
                Level2Cost, "level-2 upgrade data");
            return ReplaceUnique(value,
                Level1Cost + "\n\t\tupgradeInfo = { Speed = \"+50%\", Range = \"+50%\", Settings = 3 },",
                Level1Cost, "level-1 upgrade data");
        }

        internal static string PatchItemsText(string text)
        {
            return InsertAfterUnique(text,
                "\tobj_interactive_plasmadrill_lvl3 = sm.uuid.new( \"" + Level3Uuid + "\" ),",
                "\n\t-- SCRAPLAB SECRET MOD: Better Plasma Drills UUIDs.\n" +
                "\tobj_interactive_plasmadrill_lvl4 = sm.uuid.new( \"" + Level4Uuid + "\" ),\n" +
                "\tobj_interactive_plasmadrill_lvl5 = sm.uuid.new( \"" + Level5Uuid + "\" ),",
                "Plasma Drill UUID declarations");
        }

        internal static string UnpatchItemsText(string text)
        {
            return RemoveUnique(text,
                "\n\t-- SCRAPLAB SECRET MOD: Better Plasma Drills UUIDs.\n" +
                "\tobj_interactive_plasmadrill_lvl4 = sm.uuid.new( \"" + Level4Uuid + "\" ),\n" +
                "\tobj_interactive_plasmadrill_lvl5 = sm.uuid.new( \"" + Level5Uuid + "\" ),",
                "Plasma Drill UUID declarations");
        }

        internal static string PatchCollectionsText(string text)
        {
            return ReplaceUnique(text,
                "\tobj_interactive_plasmadrill_lvl3\n}",
                "\tobj_interactive_plasmadrill_lvl3,\n" +
                "\t-- SCRAPLAB SECRET MOD: Better Plasma Drills dangerous objects.\n" +
                "\tobj_interactive_plasmadrill_lvl4,\n" +
                "\tobj_interactive_plasmadrill_lvl5\n}",
                "dangerous Plasma Drill collection");
        }

        internal static string UnpatchCollectionsText(string text)
        {
            return ReplaceUnique(text,
                "\tobj_interactive_plasmadrill_lvl3,\n" +
                "\t-- SCRAPLAB SECRET MOD: Better Plasma Drills dangerous objects.\n" +
                "\tobj_interactive_plasmadrill_lvl4,\n" +
                "\tobj_interactive_plasmadrill_lvl5\n}",
                "\tobj_interactive_plasmadrill_lvl3\n}",
                "dangerous Plasma Drill collection");
        }

        internal static string PatchCarryText(string text)
        {
            const string level3 =
                "\t[tostring(ITEMS.obj_interactive_plasmadrill_lvl3)]     = { ITEMS.obj_resource_drillcasingmixedt1, ITEMS.obj_resource_drillcasingmixedt3, ITEMS.obj_resource_drillcasingmixedt4, ITEMS.obj_resource_drillcasingmixedrich\t },";
            return InsertAfterUnique(text, level3,
                "\n\t-- SCRAPLAB SECRET MOD: Better Plasma Drills casing insertion.\n" +
                "\t[tostring(ITEMS.obj_interactive_plasmadrill_lvl4)]     = { ITEMS.obj_resource_drillcasingmixedt1, ITEMS.obj_resource_drillcasingmixedt3, ITEMS.obj_resource_drillcasingmixedt4, ITEMS.obj_resource_drillcasingmixedrich\t },\n" +
                "\t[tostring(ITEMS.obj_interactive_plasmadrill_lvl5)]     = { ITEMS.obj_resource_drillcasingmixedt1, ITEMS.obj_resource_drillcasingmixedt3, ITEMS.obj_resource_drillcasingmixedt4, ITEMS.obj_resource_drillcasingmixedrich\t },",
                "Carry Tool Plasma Drill targets");
        }

        internal static string UnpatchCarryText(string text)
        {
            return RemoveUnique(text,
                "\n\t-- SCRAPLAB SECRET MOD: Better Plasma Drills casing insertion.\n" +
                "\t[tostring(ITEMS.obj_interactive_plasmadrill_lvl4)]     = { ITEMS.obj_resource_drillcasingmixedt1, ITEMS.obj_resource_drillcasingmixedt3, ITEMS.obj_resource_drillcasingmixedt4, ITEMS.obj_resource_drillcasingmixedrich\t },\n" +
                "\t[tostring(ITEMS.obj_interactive_plasmadrill_lvl5)]     = { ITEMS.obj_resource_drillcasingmixedt1, ITEMS.obj_resource_drillcasingmixedt3, ITEMS.obj_resource_drillcasingmixedt4, ITEMS.obj_resource_drillcasingmixedrich\t },",
                "Carry Tool Plasma Drill targets");
        }

        internal static string PatchShapesText(string text)
        {
            string suffix = "\t\t\t\"uuid\" : \"" + Level3Uuid + "\"\n\t\t}\n\t]\n}";
            string replacement = "\t\t\t\"uuid\" : \"" + Level3Uuid + "\"\n\t\t},\n" +
                ShapeEntry("obj_interactive_plasmadrill_lvl4", Level4Uuid) + ",\n" +
                ShapeEntry("obj_interactive_plasmadrill_lvl5", Level5Uuid) + "\n\t]\n}";
            return ReplaceUnique(text, suffix, replacement,
                "Plasma Drill shape-set ending");
        }

        internal static string UnpatchShapesText(string text)
        {
            string installed = "\t\t\t\"uuid\" : \"" + Level3Uuid + "\"\n\t\t},\n" +
                ShapeEntry("obj_interactive_plasmadrill_lvl4", Level4Uuid) + ",\n" +
                ShapeEntry("obj_interactive_plasmadrill_lvl5", Level5Uuid) + "\n\t]\n}";
            string clean = "\t\t\t\"uuid\" : \"" + Level3Uuid + "\"\n\t\t}\n\t]\n}";
            return ReplaceUnique(text, installed, clean,
                "Plasma Drill shape-set registrations");
        }

        internal static string PatchIconsText(string text)
        {
            string anchor =
                "            <Index name=\"" + Level3Uuid + "\">\n" +
                "                <Frame point=\"2208 384\"/>\n" +
                "            </Index>";
            return InsertAfterUnique(text, anchor,
                "\n            <!-- SCRAPLAB SECRET MOD: Better Plasma Drills icons. -->\n" +
                "            <Index name=\"" + Level4Uuid + "\">\n" +
                "                <Frame point=\"2208 384\"/>\n" +
                "            </Index>\n" +
                "            <Index name=\"" + Level5Uuid + "\">\n" +
                "                <Frame point=\"2208 384\"/>\n" +
                "            </Index>", "Plasma Drill icon registrations");
        }

        internal static string UnpatchIconsText(string text)
        {
            return RemoveUnique(text,
                "\n            <!-- SCRAPLAB SECRET MOD: Better Plasma Drills icons. -->\n" +
                "            <Index name=\"" + Level4Uuid + "\">\n" +
                "                <Frame point=\"2208 384\"/>\n" +
                "            </Index>\n" +
                "            <Index name=\"" + Level5Uuid + "\">\n" +
                "                <Frame point=\"2208 384\"/>\n" +
                "            </Index>", "Plasma Drill icon registrations");
        }

        internal static string PatchLanguageText(string text)
        {
            string original = FindJsonEntry(text, Level3Uuid);
            string level4 = ChangeLocalizedLevel(
                original.Replace(Level3Uuid, Level4Uuid), "4");
            string level5 = ChangeLocalizedLevel(
                original.Replace(Level3Uuid, Level5Uuid), "5");
            return ReplaceUnique(text, original, original + ",\n" + level4 + ",\n" + level5,
                "localized Plasma Drill level-3 entry");
        }

        internal static string UnpatchLanguageText(string text)
        {
            string original = FindJsonEntry(text, Level3Uuid);
            string level4 = ChangeLocalizedLevel(
                original.Replace(Level3Uuid, Level4Uuid), "4");
            string level5 = ChangeLocalizedLevel(
                original.Replace(Level3Uuid, Level5Uuid), "5");
            return ReplaceUnique(text, original + ",\n" + level4 + ",\n" + level5,
                original, "localized advanced Plasma Drill entries");
        }

        private static AdaptiveMultiFileModDefinition GetDefinition()
        {
            List<AdaptiveModFileDefinition> files = new List<AdaptiveModFileDefinition>
            {
                File(PlasmaPath, "PlasmaDrill.lua", PlasmaHash,
                    "-- SCRAPLAB SECRET MOD: Better Plasma Drills levels 4 and 5.",
                    PatchPlasmaText, UnpatchPlasmaText, GuardPlasma),
                File(ItemsPath, "survival_items.lua", ItemsHash, Level4Uuid,
                    PatchItemsText, UnpatchItemsText, GuardItems),
                File(CollectionsPath, "survival_collections.lua", CollectionsHash,
                    "-- SCRAPLAB SECRET MOD: Better Plasma Drills dangerous objects.",
                    PatchCollectionsText, UnpatchCollectionsText, GuardCollections),
                File(CarryPath, "CarryTool.lua", CarryHash,
                    "-- SCRAPLAB SECRET MOD: Better Plasma Drills casing insertion.",
                    PatchCarryText, UnpatchCarryText, GuardCarry,
                    CarrySprintPatchService.HasIntactCarryPatch),
                File(ShapesPath, "powertools.shapeset", ShapesHash, Level4Uuid,
                    PatchShapesText, UnpatchShapesText, GuardShapes),
                File(IconsPath, "IconMapSurvival.xml", IconsHash, Level4Uuid,
                    PatchIconsText, UnpatchIconsText, GuardIcons)
            };
            for (int index = 0; index < Languages.GetLength(0); index++)
            {
                string language = Languages[index, 0];
                files.Add(File(Path.Combine("Survival", "Gui", "Language", language,
                    "inventoryDescriptions.json"), language + " inventory descriptions",
                    Languages[index, 1], Level4Uuid, PatchLanguageText,
                    UnpatchLanguageText, GuardLanguage));
            }
            return new AdaptiveMultiFileModDefinition
            {
                ModKey = ModKey,
                DisplayName = "Better Plasma Drills",
                DefinitionVersion = DefinitionVersion,
                InstalledReason = "Added verified Plasma Drill levels 4 and 5.",
                RemovedReason = "Removed the advanced drill registrations and restored the original upgrade chain.",
                InstallChanges = new List<string>
                {
                    "Added Plasma Drill levels 4 and 5 with permanent UUIDs.",
                    "Added six larger beam settings, faster voxel updates, longer range, and improved battery capacity.",
                    "Registered advanced drills for placement, inventory icons, casing insertion, and all shipped languages."
                },
                RemoveChanges = new List<string>
                {
                    "Restored the original three-level Plasma Drill upgrade chain.",
                    "Removed level-4 and level-5 item registrations."
                },
                Files = files
            };
        }

        private static AdaptiveModFileDefinition File(string path, string name,
            string hash, string marker, Func<string, string> patch,
            Func<string, string> unpatch, Action<string> guard,
            Func<string, bool> trustedVariant = null)
        {
            return new AdaptiveModFileDefinition
            {
                RelativePath = path, DisplayName = name,
                KnownCleanHash = hash, Marker = marker,
                Patch = patch, Unpatch = unpatch, Guard = guard,
                TrustedCleanVariant = trustedVariant
            };
        }

        internal static bool HasIntactCarryPatch(string text)
        {
            if (AdaptivePatchSupport.Count(text, Level4Uuid) != 1 ||
                AdaptivePatchSupport.Count(text, Level5Uuid) != 1)
                return false;
            try
            {
                string clean = UnpatchCarryText(text);
                return AdaptivePatchSupport.Count(clean, Level4Uuid) == 0 &&
                    AdaptivePatchSupport.Count(clean, Level5Uuid) == 0;
            }
            catch (InvalidDataException) { return false; }
        }

        private static void GuardPlasma(string text)
        {
            AdaptivePatchSupport.RequireUnique(text, "PlasmaDrill = class( nil )", "Plasma Drill class");
            AdaptivePatchSupport.RequireUnique(text, "function PlasmaDrill.sv_n_tryUpgrade( self, _, player )", "Plasma Drill upgrade callback");
            AdaptivePatchSupport.RequireUnique(text, "function PlasmaDrill.sv_drillVoxels( self, drillSettings, range, direction, point )", "Plasma Drill voxel callback");
        }

        private static void GuardItems(string text) { AdaptivePatchSupport.RequireUnique(text, Level3Uuid, "level-3 Plasma Drill UUID"); }
        private static void GuardCollections(string text) { AdaptivePatchSupport.RequireUnique(text, "g_dangerousObjects = {", "dangerous-object collection"); }
        private static void GuardCarry(string text) { AdaptivePatchSupport.RequireUnique(text, "local GenericInsertTargets = {", "Carry Tool insert targets"); }
        private static void GuardShapes(string text) { AdaptivePatchSupport.RequireUnique(text, "\"name\" : \"obj_interactive_plasmadrill_lvl3\"", "level-3 Plasma Drill shape"); }
        private static void GuardIcons(string text) { AdaptivePatchSupport.RequireUnique(text, "<Index name=\"" + Level3Uuid + "\">", "level-3 Plasma Drill icon"); }
        private static void GuardLanguage(string text)
        {
            AdaptivePatchSupport.RequireUnique(text, "\"" + Level3Uuid + "\"", "localized level-3 Plasma Drill entry");
            FindJsonEntry(text, Level3Uuid);
        }

        private static string ShapeEntry(string name, string uuid)
        {
            return "\t\t{\n" +
                "\t\t\t\"color\" : \"df7f01ff\",\n" +
                "\t\t\t\"flammable\" : false,\n" +
                "\t\t\t\"hull\" : \n\t\t\t{\n" +
                "\t\t\t\t\"col\" : \"$SURVIVAL_DATA/Objects/Collision/obj_powertools_plasmadrill_lvl1.obj\",\n" +
                "\t\t\t\t\"x\" : 3,\n\t\t\t\t\"y\" : 4,\n\t\t\t\t\"z\" : 3\n\t\t\t},\n" +
                "\t\t\t\"name\" : \"" + name + "\",\n" +
                "\t\t\t\"physicsMaterial\" : \"Metal\",\n" +
                "\t\t\t\"previewRenderable\" : \"$SURVIVAL_DATA/Objects/Renderable/powertools/obj_powertools_plasmadrill_lvl3_preview.rend\",\n" +
                "\t\t\t\"ratings\" : \n\t\t\t{\n" +
                "\t\t\t\t\"buoyancy\" : 3,\n\t\t\t\t\"density\" : 3,\n\t\t\t\t\"durability\" : 8,\n\t\t\t\t\"friction\" : 2\n\t\t\t},\n" +
                "\t\t\t\"renderable\" : \"$SURVIVAL_DATA/Objects/Renderable/powertools/obj_powertools_plasmadrill_lvl3.rend\",\n" +
                "\t\t\t\"rotationSet\" : \"PropY\",\n" +
                "\t\t\t\"scripted\" : \n\t\t\t{\n" +
                "\t\t\t\t\"classname\" : \"PlasmaDrill\",\n" +
                "\t\t\t\t\"data\" : {},\n" +
                "\t\t\t\t\"filename\" : \"$SURVIVAL_DATA/Scripts/game/interactables/PlasmaDrill.lua\"\n\t\t\t},\n" +
                "\t\t\t\"stackSize\" : 5,\n\t\t\t\"sticky\" : \"-Y\",\n" +
                "\t\t\t\"uuid\" : \"" + uuid + "\"\n\t\t}";
        }

        private static string FindJsonEntry(string text, string uuid)
        {
            string key = "\t\"" + uuid + "\"";
            int start = text.IndexOf(key, StringComparison.Ordinal);
            if (start < 0 || text.IndexOf(key, start + key.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("The localized Plasma Drill entry was not found exactly once.");
            int open = text.IndexOf('{', start);
            int depth = 0;
            bool quoted = false;
            bool escape = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                    return text.Substring(start, i - start + 1);
            }
            throw new InvalidDataException("The localized Plasma Drill entry is incomplete.");
        }

        private static string ChangeLocalizedLevel(string entry, string level)
        {
            string value = entry;
            foreach (string field in new[] { "\"title\":", "\"upperCaseTitle\":" })
            {
                int fieldIndex = value.IndexOf(field, StringComparison.Ordinal);
                if (fieldIndex < 0) throw new InvalidDataException("A localized Plasma Drill title is missing.");
                int open = value.IndexOf('"', fieldIndex + field.Length);
                int end = open < 0 ? -1 : value.IndexOf('"', open + 1);
                if (end < 0)
                    throw new InvalidDataException("A localized Plasma Drill title is incomplete.");
                int three = value.LastIndexOf('3', end);
                if (three < fieldIndex || three > end)
                    throw new InvalidDataException("A localized Plasma Drill title does not end in level 3.");
                value = value.Substring(0, three) + level + value.Substring(three + 1);
            }
            return value;
        }

        private static string InsertAfterUnique(string text, string anchor,
            string addition, string description)
        {
            return ReplaceUnique(text, anchor, anchor + addition, description);
        }

        private static string RemoveUnique(string text, string value, string description)
        {
            return ReplaceUnique(text, value, "", description);
        }

        private static string ReplaceUnique(string text, string oldText,
            string newText, string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 || text.IndexOf(oldText, first + oldText.Length,
                StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("The expected " + description + " was not found exactly once.");
            return text.Substring(0, first) + newText + text.Substring(first + oldText.Length);
        }

        private const string Level1Cost = "\t\tcost = 5,";
        private const string Level2Cost = "\t\tcost = 20,";
        private const string OriginalLevel3 =
            "\t[tostring( ITEMS.obj_interactive_plasmadrill_lvl3 )] = {\n" +
            "\t\ttitle = \"#{LEVEL} 3\",\n\t\tdrillSpeed = 2.25,\n" +
            "\t\tvoxelDrillIntervalTicks = 4,\n\t\tpointsPerBattery = 2400,\n" +
            "\t\tlevel = 3,\n\t\tmaxSetting = 9,\n\t\trange = 22.5,\n\t}";
        private const string PatchedLevel3 =
            "\t[tostring( ITEMS.obj_interactive_plasmadrill_lvl3 )] = {\n" +
            "\t\ttitle = \"#{LEVEL} 3\",\n\t\tupgrade = tostring( ITEMS.obj_interactive_plasmadrill_lvl4 ),\n" +
            "\t\tdrillSpeed = 2.25,\n\t\tvoxelDrillIntervalTicks = 4,\n\t\tpointsPerBattery = 2400,\n" +
            "\t\tlevel = 3,\n\t\tmaxSetting = 9,\n\t\trange = 22.5,\n\t\tcost = 25,\n" +
            "\t\tupgradeInfo = { Speed = \"+122%\", Range = \"+78%\", Settings = 3 },\n\t},\n" +
            "\t-- SCRAPLAB SECRET MOD: Better Plasma Drills levels 4 and 5.\n" +
            "\t[tostring( ITEMS.obj_interactive_plasmadrill_lvl4 )] = {\n\t\ttitle = \"#{LEVEL} 4\",\n" +
            "\t\tupgrade = tostring( ITEMS.obj_interactive_plasmadrill_lvl5 ),\n\t\tdrillSpeed = 5,\n" +
            "\t\tvoxelDrillIntervalTicks = 3,\n\t\tpointsPerBattery = 6000,\n\t\tlevel = 3,\n" +
            "\t\tmaxSetting = 12,\n\t\trange = 40,\n\t\tcost = 50,\n" +
            "\t\tupgradeInfo = { Speed = \"+100%\", Range = \"+88%\", Settings = 3 },\n\t},\n" +
            "\t[tostring( ITEMS.obj_interactive_plasmadrill_lvl5 )] = {\n\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tdrillSpeed = 10,\n\t\tvoxelDrillIntervalTicks = 2,\n\t\tpointsPerBattery = 12000,\n" +
            "\t\tlevel = 3,\n\t\tmaxSetting = 15,\n\t\trange = 75,\n\t}";
        private const string OriginalRadiusEnd = "\t[9] = {\n\t\tradius = 4,\n\t}\n}";
        private const string PatchedRadiusEnd =
            "\t[9] = {\n\t\tradius = 4,\n\t},\n\t[10] = {\n\t\tradius = 5,\n\t},\n" +
            "\t[11] = {\n\t\tradius = 6,\n\t},\n\t[12] = {\n\t\tradius = 7,\n\t},\n" +
            "\t[13] = {\n\t\tradius = 8,\n\t},\n\t[14] = {\n\t\tradius = 9,\n\t},\n" +
            "\t[15] = {\n\t\tradius = 10,\n\t}\n}";
        private const string OriginalInteractUpgradeInfo =
            "\t\t\t\tself.gui:setData( \"UpgradeInfo\", { Speed = \"+50%\", Range = \"+50%\", Settings = 3 } )";
        private const string PatchedInteractUpgradeInfo =
            "\t\t\t\tself.gui:setData( \"UpgradeInfo\", drillLevel.upgradeInfo )";
        private const string OriginalRefreshUpgradeInfo =
            "\t\t\tself.gui:setData( \"UpgradeInfo\", { Speed = \"+50%\", Range = \"+50%\", Settings = 3 } )";
        private const string PatchedRefreshUpgradeInfo =
            "\t\t\tself.gui:setData( \"UpgradeInfo\", nextLevel.upgradeInfo )";
    }
}
