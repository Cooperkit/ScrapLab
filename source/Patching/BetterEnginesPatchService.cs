using System;
using System.Collections.Generic;
using System.IO;

namespace RaidRescue
{
    internal static class BetterEnginesPatchService
    {
        private const string ModKey = "BetterEngines";
        private const string DefinitionVersion = "1";
        private const string ElectricCleanHash =
            "14AD6B98D31F4CB6FF836BD8310DAD3679B6B8C8898600AC0A0E58907D5D9E6A";
        private const string GasCleanHash =
            "54A6D3733156ABD2780AA1BDC67965D121E2D2B6404389F8CEE7479721CC5AC2";

        internal static readonly string ElectricEngineRelativePath =
            Path.Combine(
                "Survival", "Scripts", "game", "interactables",
                "ElectricEngine.lua");
        internal static readonly string GasEngineRelativePath =
            Path.Combine(
                "Survival", "Scripts", "game", "interactables",
                "GasEngine.lua");

        internal const string ElectricMarker =
            "-- SCRAPLAB SECRET MOD: Better Engines electric power and efficiency.";
        internal const string GasMarker =
            "-- SCRAPLAB SECRET MOD: Better Engines gas efficiency.";

        internal const string OriginalElectricGears =
            "local Gears = {\n" +
            "\t{ power = 1000, velocity = math.rad( 0 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 30 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 60 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 90 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 150 ) }, -- 1\n" +
            "\t{ power = 1000, velocity = math.rad( 240 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 390 ) }, -- 2\n" +
            "\t{ power = 1000, velocity = math.rad( 630 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 1020 ) }, -- 3\n" +
            "\t{ power = 1000, velocity = math.rad( 1650 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 2670 ) }, -- 4\n" +
            "\t{ power = 1000, velocity = math.rad( 4320 ) },\n" +
            "\t{ power = 1000, velocity = math.rad( 6990 ) }, -- 5\n" +
            "}";
        internal const string PatchedElectricGears =
            ElectricMarker + "\n" +
            "local Gears = {\n" +
            "\t{ power = 10000, velocity = math.rad( 0 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 30 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 60 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 90 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 150 ) }, -- 1\n" +
            "\t{ power = 10000, velocity = math.rad( 240 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 390 ) }, -- 2\n" +
            "\t{ power = 10000, velocity = math.rad( 630 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 1020 ) }, -- 3\n" +
            "\t{ power = 10000, velocity = math.rad( 1650 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 2670 ) }, -- 4\n" +
            "\t{ power = 10000, velocity = math.rad( 4320 ) },\n" +
            "\t{ power = 10000, velocity = math.rad( 6990 ) }, -- 5\n" +
            "}";

        internal const string OriginalElectricLevelFive =
            "\t[tostring(ITEMS.obj_interactive_electricengine_05)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"ElectricEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerBattery = 20250,\n" +
            "\t\tallowAdjustingJoints = true\n" +
            "\t},\n" +
            "\t[tostring(obj_survivalobject_electricengine)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"ElectricEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerBattery = 20250,\n" +
            "\t\tallowAdjustingJoints = false,\n" +
            "\t\tcreativeBattery = true\n" +
            "\t}";
        internal const string PatchedElectricLevelFive =
            "\t[tostring(ITEMS.obj_interactive_electricengine_05)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"ElectricEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerBattery = 40250,\n" +
            "\t\tallowAdjustingJoints = true\n" +
            "\t},\n" +
            "\t[tostring(obj_survivalobject_electricengine)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"ElectricEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerBattery = 40250,\n" +
            "\t\tallowAdjustingJoints = false,\n" +
            "\t\tcreativeBattery = true\n" +
            "\t}";

        internal const string OriginalGasLevelFive =
            "\t[tostring(ITEMS.obj_interactive_gasengine_05)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"GasEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerFuel = 20250\n" +
            "\t},\n" +
            "\t[tostring(obj_survivalobject_gasengine)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"GasEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerFuel = 20250,\n" +
            "\t\tcreativeFuel = true\n" +
            "\t}";
        internal const string PatchedGasLevelFive =
            GasMarker + "\n" +
            "\t[tostring(ITEMS.obj_interactive_gasengine_05)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"GasEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerFuel = 40250\n" +
            "\t},\n" +
            "\t[tostring(obj_survivalobject_gasengine)] = {\n" +
            "\t\tgears = Gears,\n" +
            "\t\teffect = \"GasEngine - Level 5\",\n" +
            "\t\ttitle = \"#{LEVEL} 5\",\n" +
            "\t\tgearCount = #Gears,\n" +
            "\t\tbearingCount = 10,\n" +
            "\t\tpointsPerFuel = 40250,\n" +
            "\t\tcreativeFuel = true\n" +
            "\t}";

        public static GamePatchResult GetStatus()
        {
            return AdaptiveMultiFileModService.GetStatus(
                GetDefinition());
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            return AdaptiveMultiFileModService.SetEnabled(
                GetDefinition(), enabled);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot,
            bool enabled)
        {
            return AdaptiveMultiFileModService.SetEnabledAt(
                GetDefinition(), gamePath, backupRoot, enabled);
        }

        internal static string PatchElectricText(string text)
        {
            string transformed = ReplaceUnique(
                text, OriginalElectricGears,
                PatchedElectricGears,
                "Electric Engine gear table");
            return ReplaceUnique(
                transformed, OriginalElectricLevelFive,
                PatchedElectricLevelFive,
                "Electric Engine level-5 efficiency records");
        }

        internal static string UnpatchElectricText(string text)
        {
            string transformed = ReplaceUnique(
                text, PatchedElectricLevelFive,
                OriginalElectricLevelFive,
                "Electric Engine level-5 efficiency records");
            return ReplaceUnique(
                transformed, PatchedElectricGears,
                OriginalElectricGears,
                "Electric Engine gear table");
        }

        internal static string PatchGasText(string text)
        {
            return ReplaceUnique(
                text, OriginalGasLevelFive,
                PatchedGasLevelFive,
                "Gas Engine level-5 efficiency records");
        }

        internal static string UnpatchGasText(string text)
        {
            return ReplaceUnique(
                text, PatchedGasLevelFive,
                OriginalGasLevelFive,
                "Gas Engine level-5 efficiency records");
        }

        private static AdaptiveMultiFileModDefinition GetDefinition()
        {
            return new AdaptiveMultiFileModDefinition
            {
                ModKey = ModKey,
                DisplayName = "Better Engines",
                DefinitionVersion = DefinitionVersion,
                InstalledReason =
                    "Installed with exact protected engine-table matching.",
                RemovedReason =
                    "Restored the original engine tables while preserving unrelated script changes.",
                InstallChanges = new List<string>
                {
                    "Electric Engine gear power increased from 1,000 to 10,000 across all 13 gears.",
                    "Level-5 Electric and Gas Engine efficiency increased from 20,250 to 40,250 points per battery or fuel item."
                },
                RemoveChanges = new List<string>
                {
                    "Restored Electric Engine gear power to 1,000.",
                    "Restored level-5 Electric and Gas Engine efficiency to 20,250."
                },
                Files = new List<AdaptiveModFileDefinition>
                {
                    new AdaptiveModFileDefinition
                    {
                        RelativePath = ElectricEngineRelativePath,
                        DisplayName = "ElectricEngine.lua",
                        KnownCleanHash = ElectricCleanHash,
                        Marker = ElectricMarker,
                        Patch = PatchElectricText,
                        Unpatch = UnpatchElectricText,
                        Guard = RequireElectricGuards
                    },
                    new AdaptiveModFileDefinition
                    {
                        RelativePath = GasEngineRelativePath,
                        DisplayName = "GasEngine.lua",
                        KnownCleanHash = GasCleanHash,
                        Marker = GasMarker,
                        Patch = PatchGasText,
                        Unpatch = UnpatchGasText,
                        Guard = RequireGasGuards
                    }
                }
            };
        }

        private static void RequireElectricGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text, "ElectricEngine = class()",
                "Electric Engine class declaration");
            AdaptivePatchSupport.RequireUnique(
                text, "function ElectricEngine.server_onCreate( self )",
                "Electric Engine creation callback");
            AdaptivePatchSupport.RequireUnique(
                text, "local Gears = {",
                "Electric Engine gear table");
        }

        private static void RequireGasGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text, "GasEngine = class()",
                "Gas Engine class declaration");
            AdaptivePatchSupport.RequireUnique(
                text, "function GasEngine.server_onCreate( self )",
                "Gas Engine creation callback");
            AdaptivePatchSupport.RequireUnique(
                text, "local EngineLevels = {",
                "Gas Engine level table");
        }

        private static string ReplaceUnique(
            string text, string oldText,
            string newText, string description)
        {
            int first = text.IndexOf(
                oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(
                    oldText, first + oldText.Length,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description +
                    " code was not found exactly once.");
            }
            return text.Substring(0, first) +
                newText +
                text.Substring(first + oldText.Length);
        }
    }
}
