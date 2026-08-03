using System;
using System.Collections.Generic;
using System.IO;

namespace RaidRescue
{
    internal static class BetterFreezerBeehivePatchService
    {
        private const string ModKey = "BetterFreezerBeehive";
        private const string DefinitionVersion = "1";
        internal const string VerifiedSteamBuildId = "24529696";
        internal const string VerifiedGameVersion = "1.0.5.876";
        private const string FreezerCleanHash =
            "D0469B31C50D7A9196633748B4C1458FEDAAF7CEF56CADE40A33FEDD6D7A2328";
        private const string BeehiveCleanHash =
            "9698C7846C625C5455BB75021300801BB5F5544B38ECEF09BDCFC14333896B4D";

        internal static readonly string FreezerRelativePath = Path.Combine(
            "Survival", "Scripts", "game", "interactables", "Freezer.lua");
        internal static readonly string BeehiveRelativePath = Path.Combine(
            "Survival", "Scripts", "game", "interactables",
            "InteractableBeehive.lua");

        internal const string FreezerMarker =
            "-- SCRAPLAB SECRET MOD: Better Freezer & Beehive freezer automation.";
        internal const string BeehiveMarker =
            "-- SCRAPLAB SECRET MOD: Better Freezer & Beehive beehive production.";

        internal const string OriginalFreezerClass =
            "Freezer = class( nil )\n" +
            "Freezer.poseWeightCount = 1";
        internal const string PatchedFreezerClass =
            FreezerMarker + "\n" +
            "Freezer = class( nil )\n" +
            "Freezer.poseWeightCount = 1\n" +
            "Freezer.maxParentCount = 1\n" +
            "Freezer.maxChildCount = 0\n" +
            "Freezer.connectionInput = sm.interactable.connectionType.water\n" +
            "Freezer.connectionOutput = sm.interactable.connectionType.none\n" +
            "Freezer.colorNormal = sm.color.new( 0x00acfcff )\n" +
            "Freezer.colorHighlight = sm.color.new( 0x00acfcff )\n" +
            "Freezer.connectIcon = \"water\"\n" +
            "Freezer.connectIconScale = 0.75";

        internal const string OriginalFreezerBalance =
            "local ProduceTickTime = DAYCYCLE_TIME_TICKS * 0.06\n\n" +
            "local NumConsumed = 1\n" +
            "local NumProduced = 20\n" +
            "local MaximumStored = 500";
        internal const string PatchedFreezerBalance =
            "local ProduceTickTime = DAYCYCLE_TIME_TICKS * 0.015\n\n" +
            "local NumConsumed = 1\n" +
            "local NumProduced = 20\n" +
            "local MaximumStored = 2500";

        internal const string OriginalFreezerContainer =
            "self.sv.container = self.shape:getInteractable():addContainer( 0, 1, 20 )";
        internal const string PatchedFreezerContainer =
            "self.sv.container = self.shape:getInteractable():addContainer( 0, 5, 20 )";

        internal const string OriginalFreezerClientData =
            "function Freezer.sv_setClientData( self )\n" +
            "    self.network:setClientData( { active = self.sv.container:canSpend( obj_consumable_water, NumConsumed ), ice = self.sv.saved.ice } )\n" +
            "end";
        internal const string PatchedFreezerClientData =
            "function Freezer.sv_getConnectedWaterContainer( self )\n" +
            "    local parents = self.interactable:getParents( sm.interactable.connectionType.water )\n" +
            "    if #parents == 1 then\n" +
            "        return parents[1]:getContainer( 0 )\n" +
            "    end\n" +
            "    return nil\n" +
            "end\n\n" +
            "function Freezer.sv_getWaterSource( self )\n" +
            "    local connected = self:sv_getConnectedWaterContainer()\n" +
            "    if connected and connected:canSpend( obj_consumable_water, NumConsumed ) then\n" +
            "        return connected\n" +
            "    end\n" +
            "    if self.sv.container and self.sv.container:canSpend( obj_consumable_water, NumConsumed ) then\n" +
            "        return self.sv.container\n" +
            "    end\n" +
            "    return nil\n" +
            "end\n\n" +
            "function Freezer.sv_setClientData( self )\n" +
            "    self.network:setClientData( { active = self:sv_getWaterSource() ~= nil, ice = self.sv.saved.ice } )\n" +
            "end";

        internal const string OriginalFreezerCanProduce =
            "    local canProduce = function()\n" +
            "        return container:canSpend( obj_consumable_water, NumConsumed ) and NumProduced <= MaximumStored - self.sv.saved.ice\n" +
            "    end";
        internal const string PatchedFreezerCanProduce =
            "    local waterSource = nil\n" +
            "    local canProduce = function()\n" +
            "        waterSource = self:sv_getWaterSource()\n" +
            "        return waterSource ~= nil and NumProduced <= MaximumStored - self.sv.saved.ice\n" +
            "    end";

        internal const string OriginalFreezerSpend =
            "        sm.container.spend( container, obj_consumable_water, NumConsumed, true )";
        internal const string PatchedFreezerSpend =
            "        sm.container.spend( waterSource, obj_consumable_water, NumConsumed, true )";

        internal const string OriginalFreezerReceiveUpdate =
            "function Freezer.server_onReceiveUpdate( self )\n" +
            "    self:sv_updateProgress()\n" +
            "end";
        internal const string PatchedFreezerReceiveUpdate =
            OriginalFreezerReceiveUpdate + "\n\n" +
            "function Freezer.client_getAvailableParentConnectionCount( self, connectionType )\n" +
            "    if bit.band( connectionType, sm.interactable.connectionType.water ) ~= 0 then\n" +
            "        return 1 - #self.interactable:getParents( sm.interactable.connectionType.water )\n" +
            "    end\n" +
            "    return 0\n" +
            "end";

        internal const string OriginalBeehiveClass =
            "InteractableBeehive = class( nil )";
        internal const string PatchedBeehiveClass =
            BeehiveMarker + "\n" + OriginalBeehiveClass;

        internal const string OriginalBeehiveBalance =
            "local ProduceTickTime = DAYCYCLE_TIME_TICKS * 0.12\n\n" +
            "local NumConsumed = 1\n" +
            "local NumProduced = 1\n" +
            "local MaximumStored = 20";
        internal const string PatchedBeehiveBalance =
            "local ProduceTickTime = DAYCYCLE_TIME_TICKS * 0.03\n\n" +
            "local NumConsumed = 1\n" +
            "local NumProduced = 1\n" +
            "local MaximumStored = 100";

        internal const string OriginalBeehiveContainer =
            "self.sv.container = self.shape:getInteractable():addContainer( 0, 1, 20 )";
        internal const string PatchedBeehiveContainer =
            "self.sv.container = self.shape:getInteractable():addContainer( 0, 5, 20 )";

        public static GamePatchResult GetStatus()
        {
            return AdaptiveMultiFileModService.GetStatus(GetDefinition());
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            return AdaptiveMultiFileModService.SetEnabled(
                GetDefinition(), enabled);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot, bool enabled)
        {
            return AdaptiveMultiFileModService.SetEnabledAt(
                GetDefinition(), gamePath, backupRoot, enabled);
        }

        internal static string PatchFreezerText(string text)
        {
            string transformed = ReplaceUnique(
                text, OriginalFreezerClass, PatchedFreezerClass,
                "Freezer connection declaration");
            transformed = ReplaceUnique(
                transformed, OriginalFreezerBalance, PatchedFreezerBalance,
                "Freezer production balance");
            transformed = ReplaceUnique(
                transformed, OriginalFreezerContainer, PatchedFreezerContainer,
                "Freezer new-machine input container");
            transformed = ReplaceUnique(
                transformed, OriginalFreezerClientData,
                PatchedFreezerClientData,
                "Freezer water-source helpers");
            transformed = ReplaceUnique(
                transformed, OriginalFreezerCanProduce,
                PatchedFreezerCanProduce,
                "Freezer production guard");
            transformed = ReplaceUnique(
                transformed, OriginalFreezerSpend, PatchedFreezerSpend,
                "Freezer water transaction");
            return ReplaceUnique(
                transformed, OriginalFreezerReceiveUpdate,
                PatchedFreezerReceiveUpdate,
                "Freezer connection-count callback");
        }

        internal static string UnpatchFreezerText(string text)
        {
            string transformed = ReplaceUnique(
                text, PatchedFreezerReceiveUpdate,
                OriginalFreezerReceiveUpdate,
                "Freezer connection-count callback");
            transformed = ReplaceUnique(
                transformed, PatchedFreezerSpend, OriginalFreezerSpend,
                "Freezer water transaction");
            transformed = ReplaceUnique(
                transformed, PatchedFreezerCanProduce,
                OriginalFreezerCanProduce,
                "Freezer production guard");
            transformed = ReplaceUnique(
                transformed, PatchedFreezerClientData,
                OriginalFreezerClientData,
                "Freezer water-source helpers");
            transformed = ReplaceUnique(
                transformed, PatchedFreezerContainer,
                OriginalFreezerContainer,
                "Freezer new-machine input container");
            transformed = ReplaceUnique(
                transformed, PatchedFreezerBalance,
                OriginalFreezerBalance,
                "Freezer production balance");
            return ReplaceUnique(
                transformed, PatchedFreezerClass, OriginalFreezerClass,
                "Freezer connection declaration");
        }

        internal static string PatchBeehiveText(string text)
        {
            string transformed = ReplaceUnique(
                text, OriginalBeehiveClass, PatchedBeehiveClass,
                "Beehive class declaration");
            transformed = ReplaceUnique(
                transformed, OriginalBeehiveBalance,
                PatchedBeehiveBalance,
                "Beehive production balance");
            return ReplaceUnique(
                transformed, OriginalBeehiveContainer,
                PatchedBeehiveContainer,
                "Beehive new-machine input container");
        }

        internal static string UnpatchBeehiveText(string text)
        {
            string transformed = ReplaceUnique(
                text, PatchedBeehiveContainer,
                OriginalBeehiveContainer,
                "Beehive new-machine input container");
            transformed = ReplaceUnique(
                transformed, PatchedBeehiveBalance,
                OriginalBeehiveBalance,
                "Beehive production balance");
            return ReplaceUnique(
                transformed, PatchedBeehiveClass,
                OriginalBeehiveClass,
                "Beehive class declaration");
        }

        private static AdaptiveMultiFileModDefinition GetDefinition()
        {
            return new AdaptiveMultiFileModDefinition
            {
                ModKey = ModKey,
                DisplayName = "Better Freezer & Beehive",
                DefinitionVersion = DefinitionVersion,
                InstalledReason =
                    "Installed with exact protected freezer and beehive matching.",
                RemovedReason =
                    "Restored vanilla production scripts while preserving unrelated changes.",
                InstallChanges = new List<string>
                {
                    "Freezers now accept one Water Container and prefer its supply before internal water.",
                    "New Freezers and Beehives receive five filtered input slots.",
                    "Freezer and Beehive production is four times faster.",
                    "Finished storage increased to 2,500 ice and 100 beeswax."
                },
                RemoveChanges = new List<string>
                {
                    "Restored vanilla production timing and finished-storage limits.",
                    "Removed the Freezer Water Container connection behavior.",
                    "Restored one input slot for newly placed machines; existing five-slot containers remain save-persistent."
                },
                Files = new List<AdaptiveModFileDefinition>
                {
                    new AdaptiveModFileDefinition
                    {
                        RelativePath = FreezerRelativePath,
                        DisplayName = "Freezer.lua",
                        KnownCleanHash = FreezerCleanHash,
                        Marker = FreezerMarker,
                        Patch = PatchFreezerText,
                        Unpatch = UnpatchFreezerText,
                        Guard = RequireFreezerGuards
                    },
                    new AdaptiveModFileDefinition
                    {
                        RelativePath = BeehiveRelativePath,
                        DisplayName = "InteractableBeehive.lua",
                        KnownCleanHash = BeehiveCleanHash,
                        Marker = BeehiveMarker,
                        Patch = PatchBeehiveText,
                        Unpatch = UnpatchBeehiveText,
                        Guard = RequireBeehiveGuards
                    }
                }
            };
        }

        private static void RequireFreezerGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text, "Freezer = class( nil )", "Freezer class declaration");
            AdaptivePatchSupport.RequireUnique(
                text, "function Freezer.server_onCreate( self )",
                "Freezer creation callback");
            AdaptivePatchSupport.RequireUnique(
                text, "function Freezer.sv_updateProgress( self )",
                "Freezer production callback");
            AdaptivePatchSupport.RequireUnique(
                text, "function Freezer.sv_n_collect( self, args, player )",
                "Freezer collection callback");
        }

        private static void RequireBeehiveGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text, "InteractableBeehive = class( nil )",
                "Beehive class declaration");
            AdaptivePatchSupport.RequireUnique(
                text, "function InteractableBeehive.server_onCreate( self )",
                "Beehive creation callback");
            AdaptivePatchSupport.RequireUnique(
                text, "function InteractableBeehive.sv_updateProgress( self )",
                "Beehive production callback");
            AdaptivePatchSupport.RequireUnique(
                text, "function InteractableBeehive.sv_n_collect( self, args, player )",
                "Beehive collection callback");
        }

        private static string ReplaceUnique(
            string text, string oldText, string newText,
            string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 || text.IndexOf(
                oldText, first + oldText.Length,
                StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description +
                    " code was not found exactly once.");
            }
            return text.Substring(0, first) + newText +
                text.Substring(first + oldText.Length);
        }
    }
}
