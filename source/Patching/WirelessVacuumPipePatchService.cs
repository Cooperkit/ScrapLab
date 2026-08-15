using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace RaidRescue
{
    internal static class WirelessVacuumPipePatchService
    {
        private const string ModKey = "WirelessVacuumPipe";
        private const string DefinitionVersion = "9";
        internal const string PartUuid =
            "a34d9af0-4ba0-431d-b647-2d5435ecf138";
        internal const string ManagerUuid =
            "8a6e31c4-575f-40fa-96f3-85bd23eb34ce";
        internal const string VerifiedSteamBuildId = "24529696";
        internal const string VerifiedGameVersion = "1.0.5.876";

        private const string ShapeSetPath =
            "$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/WirelessVacuumPipe.shapeset";
        private const string ManagerScriptPath =
            "$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/WirelessPipeManager.lua";
        private const string LoaderStart =
            "-- SCRAPLAB WIRELESS VACUUM PIPE LINK GRAPH";
        private const string LoaderEnd =
            "-- END SCRAPLAB WIRELESS VACUUM PIPE LINK GRAPH";
        private const string WrapperDofile =
            "dofile( \"$SURVIVAL_DATA/Scripts/ScrapLab/PipeSystem/ScrapLabPipeGraph.lua\" )";
        private const string CrafterBridgeStart =
            "-- SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE";
        private const string CrafterBridgeEnd =
            "-- END SCRAPLAB WIRELESS PIPE CRAFTER GUI BRIDGE";
        private const string CrafterGuiRequest =
            "self.network:sendToServer( \"sv_n_requestScrapLabGuiContainers\" )";
        private const string PipeEffectGuard =
            "if type( shapeList ) ~= \"table\" or #shapeList < 2 then return end -- SCRAPLAB WIRELESS PIPE VISUAL ROUTE GUARD";

        private static readonly string ShapesIndexRelative = Path.Combine(
            "Survival", "Objects", "Database", "shapesets.json");
        private static readonly string ItemsRelative = Path.Combine(
            "Survival", "Scripts", "game", "survival_items.lua");
        private static readonly string ManagersRelative = Path.Combine(
            "Survival", "ScriptableObjects", "scriptableObjectSets",
            "sob_managers.sobset");
        private static readonly string RecipesRelative = Path.Combine(
            "Survival", "CraftingRecipes", "craftbot",
            "craftbot_core.json");
        private static readonly string RecipeManagerRelative = Path.Combine(
            "Survival", "Scripts", "game", "managers",
            "RecipeManager.lua");
        private static readonly string IconXmlRelative = Path.Combine(
            "Survival", "Gui", "IconMapSurvival.xml");
        private static readonly string IconPngRelative = Path.Combine(
            "Survival", "Gui", "IconMapSurvival.png");

        private const string ShapesHash =
            "FF30F988FCDF775604AA54E1AF3E97CBCC4AE45F7EDCAB7B528694933D7E2511";
        private const string ItemsHash =
            "ACDAD2CF9163655F87796D996A58DDE381AC1221B1337AEF049E38066B199789";
        private const string ManagersHash =
            "2CFF5DF5D86ACD101914E0C3D3B1A2A25EB715A37A33AE5AE5F90E72B84C2B04";
        private const string RecipesHash =
            "7AE14EA8224965276835A3E1C7FCFA7366EC91810F8FEE339C7E584A0022157E";
        private const string RecipeManagerHash =
            "4290B7B0FF9370B5C6E4D3E98DD3AC62B3934A80DAB36A6EA7EE18D2C62400B5";
        private const string IconXmlHash =
            "5DA34EF427C912BDF64BD1993834A78DBD86F11DFF16FD63B61F3FA9C1ECDDDB";
        private const string IconPngHash =
            "4288CAA081C8674E8D69640C717802C3883E1AA53181C6A9ABA86BBCFE7D9146";

        private static readonly string[,] Languages = new string[,]
        {
            { "Brazilian", "3C6EAC82C2B49E9215196883FCB8B74AD749CBE82EFEA151D01D734A98592440", "Tubo de V\u00e1cuo sem Fio", "Conecta redes de tubos pela cor da tinta, mesmo entre mundos. Interaja para escolher o modo Link, Enviar ou Receber." },
            { "Chinese", "03C675DF2720E7148E94140226A62BA3F7F96AA266851D5DA3B793CBC90D636D", "\u65e0\u7ebf\u771f\u7a7a\u7ba1", "\u6309\u6d82\u88c5\u989c\u8272\u8fde\u63a5\u771f\u7a7a\u7ba1\u7f51\u7edc\uff0c\u751a\u81f3\u53ef\u8de8\u4e16\u754c\u3002\u4ea4\u4e92\u53ef\u9009\u62e9\u8fde\u63a5\u3001\u53d1\u9001\u6216\u63a5\u6536\u6a21\u5f0f\u3002" },
            { "English", "BA935E110D8B0A5FC4AEFAAB0E76A7AA4A26ACEE6B6AD093F0F7E801B05AF3EE", "Wireless Vacuum Pipe", "Links vacuum pipe networks by paint color, even between worlds. Interact to choose Link, Send, or Receive mode." },
            { "French", "4A56BC8A64C378DED64A40EEDC05FDA35DC9EB3F837E1F6850EEA05E0EEC2F4F", "Tuyau d'aspiration sans fil", "Relie les r\u00e9seaux de tuyaux selon leur couleur, m\u00eame entre les mondes. Interagissez pour choisir Lien, Envoi ou R\u00e9ception." },
            { "German", "7D48F80C66A21F555003EB287D2E3E35031F05528305825498906D783DB286F8", "Kabelloses Vakuumrohr", "Verbindet Rohrnetze nach Lackfarbe, sogar zwischen Welten. Interagieren, um Verbinden, Senden oder Empfangen zu w\u00e4hlen." },
            { "Italian", "C8DE862CFB7F8A2B6833B0BAA92195C4D4A09B1BB16D959B0344DDB6D19D836B", "Tubo aspirante wireless", "Collega reti di tubi in base al colore, anche tra mondi. Interagisci per scegliere Collega, Invia o Ricevi." },
            { "Japanese", "98D81E337E4EC87B2BE2D5B5DE0209BEDFC54096AC153FB0F1BBE23D85E1B7C0", "\u30ef\u30a4\u30e4\u30ec\u30b9\u771f\u7a7a\u30d1\u30a4\u30d7", "\u5857\u88c5\u8272\u3067\u771f\u7a7a\u30d1\u30a4\u30d7\u7db2\u3092\u63a5\u7d9a\u3057\u3001\u7570\u306a\u308b\u30ef\u30fc\u30eb\u30c9\u9593\u3067\u3082\u52d5\u4f5c\u3057\u307e\u3059\u3002\u30a4\u30f3\u30bf\u30e9\u30af\u30c8\u3067\u30ea\u30f3\u30af\u3001\u9001\u4fe1\u3001\u53d7\u4fe1\u3092\u9078\u629e\u3067\u304d\u307e\u3059\u3002" },
            { "Korean", "579693B35D7D9F95A997944667CDE15ECAB9C4740C55375ED3087FCA5C719BDD", "\ubb34\uc120 \uc9c4\uacf5 \ud30c\uc774\ud504", "\ud398\uc778\ud2b8 \uc0c9\uc0c1\uc73c\ub85c \uc9c4\uacf5 \ud30c\uc774\ud504 \ub124\ud2b8\uc6cc\ud06c\ub97c \uc5f0\uacb0\ud558\uba70 \uc11c\ub85c \ub2e4\ub978 \uc6d4\ub4dc\uc5d0\uc11c\ub3c4 \uc791\ub3d9\ud569\ub2c8\ub2e4. \uc0c1\ud638\uc791\uc6a9\ud558\uc5ec \uc5f0\uacb0, \uc1a1\uc2e0, \uc218\uc2e0 \ubaa8\ub4dc\ub97c \uc120\ud0dd\ud558\uc138\uc694." },
            { "Polish", "B45802E61196CECEAB7BC51A0075F3051ED069EB4D1571010A8999CB79C544E9", "Bezprzewodowa rura pr\u00f3\u017cniowa", "\u0141\u0105czy sieci rur wed\u0142ug koloru farby, nawet mi\u0119dzy \u015bwiatami. U\u017cyj interakcji, aby wybra\u0107 tryb Po\u0142\u0105cz, Wy\u015blij lub Odbierz." },
            { "Russian", "8DC2C7D60D2E7756D18123588596F7108F1C657FFF99C9924B089DA8EA3BE855", "\u0411\u0435\u0441\u043f\u0440\u043e\u0432\u043e\u0434\u043d\u0430\u044f \u0432\u0430\u043a\u0443\u0443\u043c\u043d\u0430\u044f \u0442\u0440\u0443\u0431\u0430", "\u0421\u043e\u0435\u0434\u0438\u043d\u044f\u0435\u0442 \u0441\u0435\u0442\u0438 \u0442\u0440\u0443\u0431 \u043f\u043e \u0446\u0432\u0435\u0442\u0443, \u0434\u0430\u0436\u0435 \u043c\u0435\u0436\u0434\u0443 \u043c\u0438\u0440\u0430\u043c\u0438. \u0412\u0437\u0430\u0438\u043c\u043e\u0434\u0435\u0439\u0441\u0442\u0432\u0443\u0439\u0442\u0435, \u0447\u0442\u043e\u0431\u044b \u0432\u044b\u0431\u0440\u0430\u0442\u044c \u0440\u0435\u0436\u0438\u043c \u0441\u0432\u044f\u0437\u0438, \u043e\u0442\u043f\u0440\u0430\u0432\u043a\u0438 \u0438\u043b\u0438 \u043f\u0440\u0438\u0451\u043c\u0430." },
            { "Spanish", "ECA2E0850CC3DC56AE811B2EB4043B0C657D407D9E8F813F9CBC7E3BF7EB6704", "Tubo de vac\u00edo inal\u00e1mbrico", "Conecta redes de tuber\u00edas por color, incluso entre mundos. Interact\u00faa para elegir el modo Enlace, Enviar o Recibir." }
        };

        private sealed class TextState
        {
            public string RelativePath;
            public string DisplayName;
            public string KnownHash;
            public string Path;
            public LuaTextDocument Document;
            public string PatchedText;
            public string CleanText;
            public bool Clean;
            public bool Installed;
            public bool Known;
            public bool NeedsDefinitionUpdate;
        }

        private sealed class OwnedAsset
        {
            public string RelativePath;
            public string DisplayName;
            public string ResourceName;
            public string Path;
            public byte[] Bytes;
            public bool Missing;
            public bool Exact;
            public bool LegacyExact;
        }

        private sealed class ConsumerDefinition
        {
            public string Kind;
            public string RelativePath;
            public string KnownHash;
            public string[] Guards;
            public Dictionary<string, int> Methods;
        }

        private sealed class ProbeState
        {
            public List<TextState> Texts = new List<TextState>();
            public List<OwnedAsset> Owned = new List<OwnedAsset>();
            public byte[] AtlasBytes;
            public string AtlasPath;
            public string AtlasHash;
            public byte[] IconBytes;
            public List<ScrapLabIconAtlasCoordinator.IconAsset> IconCatalog;
            public ScrapLabIconAtlasCoordinator.CatalogPlan CatalogPlan;
            public ScrapLabIconAtlasCoordinator.AtlasInfo AtlasInfo;
            public ScrapLabIconAtlasCoordinator.SharedAtlasReceipt AtlasReceipt;
            public bool AtlasClean;
            public bool AtlasInstalled;
            public bool AtlasKnown;
            public bool OwnedClean;
            public bool OwnedInstalled;
            public bool RegistrationsClean;
            public bool OrphanedOwnedAssets;
            public bool DefinitionUpdateAvailable;
            public bool AllClean;
            public bool AllInstalled;
            public bool AllKnownClean;
        }

        private static readonly ConsumerDefinition[] Consumers =
            CreateConsumers();

        public static GamePatchResult GetStatus()
        {
            try
            {
                string gamePath = GamePatchService.FindGameInstall();
                if (String.IsNullOrEmpty(gamePath))
                    return Failure("Scrap Mechanic was not found.");
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
                AdaptivePatchReceipt receipt =
                    AdaptivePatchSupport.LoadReceipt(ModKey);
                if (state.AllInstalled)
                {
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    result.NeedsUpdate = state.DefinitionUpdateAvailable;
                    AdaptivePatchSupport.FillResult(result, build,
                        state.DefinitionUpdateAvailable
                            ? PatchCompatibilityState.DefinitionUpdate
                            : PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        state.DefinitionUpdateAvailable
                            ? "A verified Wireless Vacuum Pipe update is ready."
                            : "Wireless Vacuum Pipe registrations, runtime, recipes, languages, and icon are intact.");
                    return result;
                }
                if (state.AllClean)
                {
                    string reason;
                    bool canApply = CanApplyClean(state, build, out reason);
                    result.Success = true;
                    result.Installed = false;
                    if (receipt != null && canApply)
                    {
                        AdaptivePatchSupport.FillResult(result, build,
                            "REINSTALL REQUIRED - SAVE PART AT RISK",
                            true, true,
                            "Steam removed the Wireless Vacuum Pipe registrations. Reinstall before loading a save that may contain the part.");
                    }
                    else
                    {
                        AdaptivePatchSupport.FillResult(result, build,
                            state.AllKnownClean
                                ? PatchCompatibilityState.KnownClean
                                : canApply
                                    ? PatchCompatibilityState.CompatibleUpdate
                                    : PatchCompatibilityState.OtherModification,
                            !state.AllKnownClean, canApply, reason);
                    }
                    return result;
                }
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(result, build,
                    PatchCompatibilityState.PartialConflict,
                    false, false,
                    DescribeConflict(state));
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing Wireless Vacuum Pipe.");
            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");
            GamePatchResult result = SetEnabledAt(gamePath,
                ProductPaths.LocalDataPath("Game Backups", "Scrap Mechanic",
                    "Secret Mods"), enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(
                gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot, bool enabled)
        {
            GamePatchResult result = NewResult(gamePath, enabled);
            try
            {
                SteamBuildInfo build = ReadBuild(gamePath, result);
                ProbeState state = Probe(gamePath);
                if (enabled && state.AllInstalled &&
                    state.DefinitionUpdateAvailable)
                {
                    List<AtomicCustomPartFilePlan> updatePlans =
                        BuildDefinitionUpdatePlans(state);
                    ApplyDefinitionUpdate(updatePlans, result, gamePath,
                        backupRoot, build);
                    result.Success = true;
                    result.Installed = true;
                    result.NeedsUpdate = false;
                    result.Changes.Add(
                        "Grouped every installed ScrapLab custom-part recipe beside the vanilla Vacuum Pipe recipe.");
                    AdaptivePatchSupport.FillResult(result, build,
                        PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        "Wireless Vacuum Pipe definition 9 recipe ordering was installed and verified.");
                    SecretModBackupRetention.Prune(
                        backupRoot, ModKey, result.BackupPath, result);
                    return result;
                }
                if (enabled && state.AllInstalled)
                {
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        ModKey, build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, ModKey, build,
                            "Wireless Vacuum Pipe was reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build,
                        PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        "Wireless Vacuum Pipe is already installed.");
                    return result;
                }
                if (!enabled && state.AllClean)
                {
                    CleanupOwnedFiles(state);
                    AdaptivePatchSupport.DeleteReceipt(ModKey);
                    AdaptivePatchSupport.DeleteBuildActivation(ModKey);
                    result.Success = true;
                    result.Installed = false;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build,
                        state.AllKnownClean
                            ? PatchCompatibilityState.KnownClean
                            : PatchCompatibilityState.CompatibleUpdate,
                        !state.AllKnownClean, true,
                        "Wireless Vacuum Pipe is already removed.");
                    return result;
                }
                if (enabled)
                {
                    if (!state.AllClean)
                        throw new InvalidOperationException(
                            "Wireless Vacuum Pipe cannot be installed because one or more targets are partial or conflicting.");
                    string reason;
                    if (!CanApplyClean(state, build, out reason))
                        throw new InvalidOperationException(
                            "Wireless Vacuum Pipe cannot be installed: " + reason);
                }
                else if (!state.AllInstalled)
                {
                    throw new InvalidOperationException(
                        "Wireless Vacuum Pipe cannot be removed because a protected patch or owned file was edited.");
                }

                List<AtomicCustomPartFilePlan> plans = enabled
                    ? BuildInstallPlans(state)
                    : BuildRemovePlans(state, backupRoot);
                AtomicCustomPartPatchSupport.Apply(ModKey,
                    "Wireless Vacuum Pipe", DefinitionVersion, plans,
                    result, gamePath, backupRoot, build, enabled,
                    state.IconCatalog);
                result.Success = true;
                result.Installed = enabled;
                result.Changes.Add(enabled
                    ? "Installed the Wireless Vacuum Pipe runtime and all protected Link/Send/Receive pipe integrations."
                    : "Removed the Wireless Vacuum Pipe runtime and registrations.");
                result.Changes.Add(enabled
                    ? "Added the default-unlocked Craftbot recipe producing two pipes and all 11 inventory descriptions."
                    : "Removed the Craftbot recipe, default unlock, and inventory descriptions.");
                result.Changes.Add(enabled
                    ? "Registered the transparent icon through the shared bottom-of-atlas ScrapLab catalog."
                    : "Removed only the Wireless Vacuum Pipe icon registration while preserving other ScrapLab icons.");
                AdaptivePatchSupport.FillResult(result, build,
                    enabled
                        ? (state.AllKnownClean
                            ? PatchCompatibilityState.KnownInstalled
                            : PatchCompatibilityState.AdaptiveInstalled)
                        : (state.AllKnownClean
                            ? PatchCompatibilityState.KnownClean
                            : PatchCompatibilityState.CompatibleUpdate),
                    !state.AllKnownClean, true,
                    enabled
                        ? "Wireless Vacuum Pipe was installed and verified."
                        : "Wireless Vacuum Pipe was removed and verified.");
                AdaptivePatchSupport.QueueBuildActivation(
                    result, ModKey, enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, ModKey, result.BackupPath, result);
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        private static ProbeState Probe(string gamePath)
        {
            ProbeState state = new ProbeState();
            state.Texts.Add(ReadText(gamePath, ShapesIndexRelative,
                "shapesets.json", ShapesHash, ShapeSetPath,
                PatchShapesIndex, UnpatchShapesIndex, false));
            state.Texts.Add(ReadText(gamePath, ItemsRelative,
                "survival_items.lua", ItemsHash, PartUuid,
                PatchItems, UnpatchItems, false));
            state.Texts.Add(ReadText(gamePath, ManagersRelative,
                "sob_managers.sobset", ManagersHash, ManagerUuid,
                PatchManagers, UnpatchManagers, false));
            state.Texts.Add(ReadRecipe(gamePath));
            state.Texts.Add(ReadText(gamePath, RecipeManagerRelative,
                "RecipeManager.lua", RecipeManagerHash,
                "ITEMS.obj_pneumatic_pipe_wireless",
                PatchRecipeManager, UnpatchRecipeManager, false));

            foreach (ConsumerDefinition consumer in Consumers)
            {
                ConsumerDefinition captured = consumer;
                state.Texts.Add(ReadText(gamePath,
                    consumer.RelativePath, consumer.Kind,
                    consumer.KnownHash,
                    consumer.Kind == "PipeEffects"
                        ? PipeEffectGuard : LoaderStart,
                    delegate(string text)
                    {
                        return PatchConsumer(captured, text);
                    },
                    delegate(string text)
                    {
                        return UnpatchConsumer(captured, text);
                    }, false));
            }

            state.Texts.Add(ReadText(gamePath, IconXmlRelative,
                "IconMapSurvival.xml", IconXmlHash, PartUuid,
                delegate(string text) { return text; },
                delegate(string text) { return text; }, true));
            for (int index = 0; index < Languages.GetLength(0); index++)
            {
                string relative = Path.Combine("Survival", "Gui",
                    "Language", Languages[index, 0],
                    "inventoryDescriptions.json");
                state.Texts.Add(ReadLanguage(gamePath, relative,
                    Languages[index, 0] + " inventory descriptions",
                    Languages[index, 1], Languages[index, 2],
                    Languages[index, 3]));
            }

            TextState iconXml = FindText(state.Texts, IconXmlRelative);
            state.AtlasPath = Path.Combine(gamePath, IconPngRelative);
            if (!File.Exists(state.AtlasPath))
                throw new FileNotFoundException(
                    "IconMapSurvival.png was not found.", state.AtlasPath);
            state.AtlasBytes = File.ReadAllBytes(state.AtlasPath);
            state.AtlasHash = AdaptivePatchSupport.Sha256(
                state.AtlasBytes);
            state.IconCatalog = ScrapLabIconAtlasCoordinator.LoadCatalog();
            state.IconBytes = ScrapLabIconAtlasCoordinator.FindCatalogIcon(
                state.IconCatalog, PartUuid).Bytes;
            state.AtlasInfo = ScrapLabIconAtlasCoordinator.Inspect(
                iconXml.Document.NormalizedText, state.AtlasBytes,
                state.IconBytes, PartUuid);
            state.AtlasInstalled = state.AtlasInfo.EntryPresent &&
                state.AtlasInfo.IconPresent;
            try
            {
                state.CatalogPlan =
                    ScrapLabIconAtlasCoordinator.EnsureCatalog(
                        iconXml.Document.NormalizedText,
                        state.AtlasBytes, state.IconCatalog);
                state.AtlasClean = !state.AtlasInfo.EntryPresent;
            }
            catch { state.AtlasClean = false; }
            state.AtlasReceipt =
                ScrapLabIconAtlasCoordinator.LoadReceipt(
                    AdaptivePatchSupport.GetSharedStatePath(
                        "ScrapLab-Icon-Pack.json"));
            state.AtlasKnown = String.Equals(state.AtlasHash,
                IconPngHash, StringComparison.OrdinalIgnoreCase) ||
                ScrapLabIconAtlasCoordinator.IsTrustedReceipt(
                    state.AtlasReceipt, state.AtlasHash,
                    state.IconCatalog);
            if (state.AtlasReceipt != null && String.Equals(
                state.AtlasReceipt.IconXmlHash,
                iconXml.Document.OriginalHash,
                StringComparison.OrdinalIgnoreCase)) iconXml.Known = true;

            AddOwned(state, gamePath,
                Path.Combine("Survival", "Scripts", "ScrapLab",
                    "PipeSystem", "WirelessPipeManager.lua"),
                "WirelessPipeManager.lua",
                "RaidRescue.Parts.WirelessVacuumPipe.WirelessPipeManager.lua");
            AddOwned(state, gamePath,
                Path.Combine("Survival", "Scripts", "ScrapLab",
                    "PipeSystem", "ScrapLabPipeGraph.lua"),
                "ScrapLabPipeGraph.lua",
                "RaidRescue.Parts.WirelessVacuumPipe.ScrapLabPipeGraph.lua");
            AddOwned(state, gamePath,
                Path.Combine("Survival", "Scripts", "ScrapLab",
                    "PipeSystem", "WirelessPipeTransfer.lua"),
                "WirelessPipeTransfer.lua",
                "RaidRescue.Parts.WirelessVacuumPipe.WirelessPipeTransfer.lua");
            AddOwned(state, gamePath,
                Path.Combine("Survival", "Scripts", "ScrapLab", "Parts",
                    "WirelessVacuumPipe", "WirelessVacuumPipe.lua"),
                "WirelessVacuumPipe.lua",
                "RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.lua");
            AddOwned(state, gamePath,
                Path.Combine("Survival", "Objects", "Database",
                    "ShapeSets", "ScrapLab", "Parts",
                    "WirelessVacuumPipe.shapeset"),
                "WirelessVacuumPipe.shapeset",
                "RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.shapeset");
            AddOwned(state, gamePath,
                Path.Combine("Survival", "Gui", "Layouts", "ScrapLab",
                    "Parts", "WirelessVacuumPipe.layout"),
                "WirelessVacuumPipe.layout",
                "RaidRescue.Parts.WirelessVacuumPipe.WirelessVacuumPipe.layout");

            bool textsClean = true;
            bool textsInstalled = true;
            bool known = true;
            foreach (TextState text in state.Texts)
            {
                if (String.Equals(text.RelativePath, IconXmlRelative,
                    StringComparison.OrdinalIgnoreCase))
                {
                    textsClean &= !state.AtlasInfo.EntryPresent;
                    textsInstalled &= state.AtlasInfo.EntryPresent;
                }
                else
                {
                    textsClean &= text.Clean;
                    textsInstalled &= text.Installed;
                }
                known &= text.Known;
            }
            state.OwnedClean = true;
            state.OwnedInstalled = true;
            bool anyLegacyOwned = false;
            foreach (OwnedAsset owned in state.Owned)
            {
                state.OwnedClean &= owned.Missing;
                state.OwnedInstalled &= owned.Exact || owned.LegacyExact;
                anyLegacyOwned |= owned.LegacyExact;
            }
            bool anyTextUpdate = false;
            foreach (TextState text in state.Texts)
                anyTextUpdate |= text.NeedsDefinitionUpdate;
            state.DefinitionUpdateAvailable =
                state.OwnedInstalled && (anyLegacyOwned || anyTextUpdate);
            state.RegistrationsClean = textsClean && state.AtlasClean;
            state.OrphanedOwnedAssets = state.RegistrationsClean &&
                state.OwnedInstalled;
            state.AllClean = state.RegistrationsClean &&
                (state.OwnedClean || state.OwnedInstalled);
            state.AllInstalled = textsInstalled && state.AtlasInstalled &&
                state.OwnedInstalled;
            state.AllKnownClean = known && state.AtlasKnown;
            return state;
        }

        private static string DescribeConflict(ProbeState state)
        {
            List<string> issues = new List<string>();
            int cleanTexts = 0;
            int installedTexts = 0;
            foreach (TextState text in state.Texts)
            {
                if (String.Equals(text.RelativePath, IconXmlRelative,
                    StringComparison.OrdinalIgnoreCase)) continue;
                if (text.Clean) cleanTexts++;
                if (text.Installed) installedTexts++;
                if (!text.Clean && !text.Installed)
                    issues.Add(text.DisplayName + " is edited or duplicated");
            }
            if (!state.AtlasClean && !state.AtlasInstalled)
                issues.Add("the icon XML or managed atlas tile conflicts");
            bool anyOwnedMissing = false;
            bool anyOwnedExact = false;
            foreach (OwnedAsset owned in state.Owned)
            {
                anyOwnedMissing |= owned.Missing;
                anyOwnedExact |= owned.Exact;
                if (!owned.Missing && !owned.Exact)
                    issues.Add(owned.DisplayName + " was edited");
            }
            if (anyOwnedMissing && anyOwnedExact)
                issues.Add("the owned runtime file set is incomplete");
            if (issues.Count == 0 && cleanTexts > 0 && installedTexts > 0)
                issues.Add("protected registrations are mixed between clean and installed states");
            if (issues.Count == 0)
                issues.Add("the installed components do not form one complete clean or installed state");
            return "Wireless Vacuum Pipe needs repair: " +
                String.Join("; ", issues.ToArray()) + ".";
        }

        private static TextState ReadText(
            string gamePath, string relative, string display,
            string knownHash, string marker,
            Func<string, string> patch, Func<string, string> unpatch,
            bool atlasXml)
        {
            string path = Path.Combine(gamePath, relative);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    display + " was not found.", path);
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            AdaptivePatchSupport.RequireAdaptiveFormat(document, display);
            int count = AdaptivePatchSupport.Count(
                document.NormalizedText, marker);
            TextState state = new TextState
            {
                RelativePath = relative,
                DisplayName = display,
                KnownHash = knownHash,
                Path = path,
                Document = document,
                Known = String.Equals(document.OriginalHash, knownHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    IsTrustedExistingOutput(relative,
                        document.OriginalHash)
            };
            if (atlasXml)
            {
                state.Clean = count == 0;
                state.Installed = count == 1;
                return state;
            }
            if (count == 0)
            {
                state.PatchedText = patch(document.NormalizedText);
                state.Clean = true;
            }
            else if (count == 1)
            {
                state.CleanText = unpatch(document.NormalizedText);
                state.PatchedText = patch(state.CleanText);
                state.Installed = String.Equals(state.PatchedText,
                    document.NormalizedText, StringComparison.Ordinal);
            }
            return state;
        }

        private static TextState ReadLanguage(
            string gamePath, string relative, string display,
            string knownHash, string title, string description)
        {
            string path = Path.Combine(gamePath, relative);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    display + " was not found.", path);
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            AdaptivePatchSupport.RequireAdaptiveFormat(document, display);
            string text = document.NormalizedText;
            int markerCount = AdaptivePatchSupport.Count(text, PartUuid);
            string exactEntry = LanguageEntry(title, description);
            TextState state = new TextState
            {
                RelativePath = relative,
                DisplayName = display,
                KnownHash = knownHash,
                Path = path,
                Document = document,
                Known = String.Equals(document.OriginalHash, knownHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    IsTrustedExistingOutput(relative, document.OriginalHash)
            };

            if (markerCount == 0)
            {
                state.PatchedText = PatchLanguage(text, title, description);
                state.Clean = true;
            }
            else if (markerCount == 1 &&
                AdaptivePatchSupport.Count(text, exactEntry) == 1)
            {
                // Shared localization files can contain later ScrapLab entries.
                // Preserve those entries and validate this exact block in place.
                state.CleanText = UnpatchLanguage(text, title, description);
                state.PatchedText = text;
                state.Installed =
                    AdaptivePatchSupport.Count(state.CleanText, PartUuid) == 0;
            }
            return state;
        }

        private static TextState ReadRecipe(string gamePath)
        {
            string path = Path.Combine(gamePath, RecipesRelative);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "craftbot_core.json was not found.", path);
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            AdaptivePatchSupport.RequireAdaptiveFormat(
                document, "craftbot_core.json");
            string text = document.NormalizedText;
            int markerCount = AdaptivePatchSupport.Count(text, PartUuid);
            TextState state = new TextState
            {
                RelativePath = RecipesRelative,
                DisplayName = "craftbot_core.json",
                KnownHash = RecipesHash,
                Path = path,
                Document = document,
                Known = String.Equals(document.OriginalHash, RecipesHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    IsTrustedExistingOutput(RecipesRelative,
                        document.OriginalHash)
            };
            if (markerCount == 0)
            {
                state.PatchedText = PatchRecipe(text);
                state.Clean = true;
            }
            else if (markerCount == 1 &&
                AdaptivePatchSupport.Count(text, RecipeEntry) == 1)
            {
                state.CleanText = UnpatchRecipe(text);
                state.PatchedText = PatchRecipe(state.CleanText);
                state.Installed = AdaptivePatchSupport.Count(
                    state.CleanText, PartUuid) == 0;
                state.NeedsDefinitionUpdate = state.Installed &&
                    !String.Equals(state.PatchedText, text,
                        StringComparison.Ordinal);
            }
            return state;
        }

        private static void AddOwned(
            ProbeState state, string gamePath, string relative,
            string display, string resource)
        {
            OwnedAsset owned = new OwnedAsset
            {
                RelativePath = relative,
                DisplayName = display,
                ResourceName = resource,
                Path = Path.Combine(gamePath, relative),
                Bytes = GetResource(resource)
            };
            owned.Missing = !File.Exists(owned.Path);
            owned.Exact = !owned.Missing && BytesEqual(
                File.ReadAllBytes(owned.Path), owned.Bytes);
            owned.LegacyExact = !owned.Missing && !owned.Exact &&
                IsLegacyOwnedHash(relative,
                    AdaptivePatchSupport.Sha256(owned.Path));
            state.Owned.Add(owned);
        }

        private static bool IsLegacyOwnedHash(
            string relative, string candidate)
        {
            string file = Path.GetFileName(relative);
            string[] hashes = null;
            if (String.Equals(file, "WirelessPipeManager.lua",
                StringComparison.OrdinalIgnoreCase))
                hashes = new string[]
                {
                    "0CB52D246E313967CE35BC1CF38C73094F02BF18B1F7B9847EB17EA939AEE0DB",
                    "7BBCB858591D3903FEF650626B3B0BBE58F0C1D9E28A9551888DAC7FC3730AF4",
                    "C1F0FA66477AB6189A47F40BEA377991A13E3FE2E99BB077D0CE6A6665E43B57",
                    "2EE306FA1303FDA36CC2CE64964CCD4E567CC27EA7D82D4F47B5B6CCE31BC321",
                    "3411D6804F6D874C4B9BD8D8C80C4109BF3CECFB0F44F31EDF49C0DF4F3D8DC8"
                };
            else if (String.Equals(file, "ScrapLabPipeGraph.lua",
                StringComparison.OrdinalIgnoreCase))
                hashes = new string[]
                {
                    "4A225B56FB87F108C4987FC4EC6F1C8AA45ED466452A856BF3F5775EE2C11CD9",
                    "2F19CA25EC83596931C369624A691C09DF7E9A8903736171AE4192311B21813A",
                    "7EC649701A334452B8E4CD6B96403C977B1E6EB3AE5D7057B46B506D79537F4D",
                    "D1E9A24346530DFA8344451475C9F35F8BA2F141A6A19130E8EFBBE408A1C9AC",
                    "8C8641F1069968D0750ABCDCB0C56261616D44B11E2C1814C4664222BED2BD2A"
                };
            else if (String.Equals(file, "WirelessPipeTransfer.lua",
                StringComparison.OrdinalIgnoreCase))
                hashes = new string[]
                {
                    "ED9507FEFFA91C280C5B6AAEC720EE773993B6E9E56A47F7CE274606AFC680BA",
                    "CC64EEFFFA602B4A6CC670A15ECF9DE99805C3AC394C4D46149FFEA00CE6B561"
                };
            else if (String.Equals(file, "WirelessVacuumPipe.lua",
                StringComparison.OrdinalIgnoreCase))
                hashes = new string[]
                {
                    "25F6D11E19C3514FE2E06DA72FC5A60C45BE21471B49D35198F2E143EF8377D6",
                    "338FAB44E130D36A51D90EC5EC8079DA472C67A4C51900E92B36C3727FD67BED"
                };
            else if (String.Equals(file, "WirelessVacuumPipe.layout",
                StringComparison.OrdinalIgnoreCase))
                hashes = new string[]
                {
                    "2F800DD9E1A29679182B7649D378F401D1D3E561E50A7894859D337157B18B12",
                    "F5D5ADCC354E1CCA7001E68B17507B0657B84AAF80AEF05C4B159C551439A48B"
                };
            if (hashes == null) return false;
            foreach (string hash in hashes)
                if (String.Equals(candidate, hash,
                    StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static List<AtomicCustomPartFilePlan>
            BuildDefinitionUpdatePlans(ProbeState state)
        {
            if (state == null || !state.DefinitionUpdateAvailable)
                throw new InvalidOperationException(
                    "The Wireless Vacuum Pipe definition update is not available.");
            List<AtomicCustomPartFilePlan> plans =
                new List<AtomicCustomPartFilePlan>();
            foreach (OwnedAsset owned in state.Owned)
                if (owned.LegacyExact)
                    AddOwnedPlan(plans, owned, false);
            foreach (TextState text in state.Texts)
                if (text.NeedsDefinitionUpdate)
                    AddTextPlan(plans, text, text.PatchedText);
            if (plans.Count == 0)
                throw new InvalidOperationException(
                    "No verified Wireless Vacuum Pipe definition targets were found.");
            return plans;
        }

        private static void ApplyDefinitionUpdate(
            List<AtomicCustomPartFilePlan> plans,
            GamePatchResult result, string gamePath,
            string backupRoot, SteamBuildInfo build)
        {
            AdaptivePatchReceipt receipt =
                AdaptivePatchSupport.LoadReceipt(ModKey);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(backupRoot,
                "Update-" + ModKey + "-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            List<AdaptivePatchReceiptFile> manifest =
                new List<AdaptivePatchReceiptFile>();
            foreach (AtomicCustomPartFilePlan plan in plans)
            {
                string backup = Path.Combine(backupPath, plan.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.WriteAllBytes(backup, plan.SourceBytes);
                if (!String.Equals(AdaptivePatchSupport.Sha256(backup),
                    plan.SourceHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(plan.DisplayName +
                        " update backup failed checksum verification.");
                manifest.Add(new AdaptivePatchReceiptFile
                {
                    RelativePath = plan.RelativePath,
                    SourceHash = plan.SourceHash,
                    OutputHash = plan.OutputHash,
                    Newline = "PRESERVED",
                    HasBom = false
                });
            }
            AdaptivePatchSupport.WriteBackupManifest(backupPath,
                "Wireless Vacuum Pipe", "Definition Update",
                gamePath, build, DefinitionVersion, manifest);

            List<AtomicCustomPartFilePlan> changed =
                new List<AtomicCustomPartFilePlan>();
            try
            {
                foreach (AtomicCustomPartFilePlan plan in plans)
                {
                    AdaptivePatchSupport.ReplaceFile(plan.Path,
                        plan.OutputBytes,
                        ModKey + "-definition-update");
                    changed.Add(plan);
                    if (!File.Exists(plan.Path) || !String.Equals(
                        AdaptivePatchSupport.Sha256(plan.Path),
                        plan.OutputHash,
                        StringComparison.OrdinalIgnoreCase))
                        throw new IOException(plan.DisplayName +
                            " failed final update verification.");
                }

                if (receipt != null)
                {
                    foreach (AtomicCustomPartFilePlan plan in plans)
                    {
                        AdaptivePatchReceiptFile file =
                            AdaptivePatchSupport.FindReceiptFile(
                                receipt, plan.RelativePath);
                        if (file != null) file.OutputHash = plan.OutputHash;
                    }
                    receipt.DefinitionVersion = DefinitionVersion;
                    AdaptivePatchSupport.SaveReceipt(ModKey, receipt);
                }
            }
            catch
            {
                for (int index = changed.Count - 1; index >= 0; index--)
                {
                    AtomicCustomPartFilePlan plan = changed[index];
                    AdaptivePatchSupport.ReplaceFile(plan.Path,
                        plan.SourceBytes,
                        ModKey + "-definition-update-rollback");
                }
                foreach (AtomicCustomPartFilePlan plan in changed)
                    if (!File.Exists(plan.Path) || !String.Equals(
                        AdaptivePatchSupport.Sha256(plan.Path),
                        plan.SourceHash,
                        StringComparison.OrdinalIgnoreCase))
                        throw new IOException(
                            "Wireless Vacuum Pipe update rollback could not restore " +
                            plan.DisplayName + ".");
                throw;
            }
            result.FilesPatched = plans.Count;
        }

        private static bool CanApplyClean(
            ProbeState state, SteamBuildInfo build, out string reason)
        {
            if (state.AllKnownClean)
            {
                reason = "Verified Steam build 24529696 Wireless Vacuum Pipe targets.";
                return true;
            }
            if (build != null && build.Valid &&
                String.Equals(build.BuildId, VerifiedSteamBuildId,
                    StringComparison.Ordinal) &&
                String.Equals(build.GameVersion, VerifiedGameVersion,
                    StringComparison.Ordinal))
            {
                reason = "A protected Wireless Vacuum Pipe target differs from the verified current Steam build.";
                return false;
            }
            List<string> unknown = new List<string>();
            foreach (TextState text in state.Texts)
                if (!text.Known) unknown.Add(text.Path);
            if (!state.AtlasKnown) unknown.Add(state.AtlasPath);
            return AdaptivePatchSupport.CanAdaptCleanFiles(
                build, unknown, out reason);
        }

        private static List<AtomicCustomPartFilePlan> BuildInstallPlans(
            ProbeState state)
        {
            List<AtomicCustomPartFilePlan> plans =
                new List<AtomicCustomPartFilePlan>();
            foreach (TextState text in state.Texts)
            {
                if (String.Equals(text.RelativePath, IconXmlRelative,
                    StringComparison.OrdinalIgnoreCase)) continue;
                AddTextPlan(plans, text, text.PatchedText);
            }
            TextState iconXml = FindText(state.Texts, IconXmlRelative);
            ScrapLabIconAtlasCoordinator.CatalogPlan catalogPlan =
                state.CatalogPlan ??
                ScrapLabIconAtlasCoordinator.EnsureCatalog(
                    iconXml.Document.NormalizedText, state.AtlasBytes,
                    state.IconCatalog);
            ScrapLabIconAtlasCoordinator.IconPlacement placement =
                catalogPlan.Placements[PartUuid];
            AddTextPlan(plans, iconXml,
                PatchIconXml(iconXml.Document.NormalizedText,
                    placement.X, placement.Y));
            if (catalogPlan.AtlasChanged)
                AddBinaryPlan(plans, IconPngRelative,
                    "IconMapSurvival.png", state.AtlasPath,
                    state.AtlasBytes, catalogPlan.AtlasBytes, true);
            foreach (OwnedAsset owned in state.Owned)
                AddOwnedPlan(plans, owned,
                    state.OrphanedOwnedAssets);
            return plans;
        }

        private static List<AtomicCustomPartFilePlan> BuildRemovePlans(
            ProbeState state, string backupRoot)
        {
            List<AtomicCustomPartFilePlan> plans =
                new List<AtomicCustomPartFilePlan>();
            foreach (TextState text in state.Texts)
            {
                if (String.Equals(text.RelativePath, IconXmlRelative,
                    StringComparison.OrdinalIgnoreCase)) continue;
                AddTextPlan(plans, text, text.CleanText);
            }
            TextState iconXml = FindText(state.Texts, IconXmlRelative);
            int x;
            int y;
            if (!ScrapLabIconAtlasCoordinator.TryGetEntry(
                iconXml.Document.NormalizedText, PartUuid, out x, out y))
                throw new InvalidDataException(
                    "The Wireless Vacuum Pipe icon registration is missing.");
            string xmlOutput = UnpatchIconXml(
                iconXml.Document.NormalizedText, x, y);
            AddTextPlan(plans, iconXml, xmlOutput);
            string baselinePath = Path.Combine(backupRoot,
                "ScrapLab-Shared-Icon-Atlas",
                "IconMapSurvival.baseline.png");
            byte[] baseline = File.Exists(baselinePath)
                ? File.ReadAllBytes(baselinePath) : null;
            byte[] atlasOutput =
                ScrapLabIconAtlasCoordinator.RemoveCatalogWhenUnused(
                    xmlOutput, state.AtlasBytes, state.IconCatalog,
                    baseline);
            if (!String.Equals(AdaptivePatchSupport.Sha256(atlasOutput),
                state.AtlasHash, StringComparison.OrdinalIgnoreCase))
                AddBinaryPlan(plans, IconPngRelative,
                    "IconMapSurvival.png", state.AtlasPath,
                    state.AtlasBytes, atlasOutput, true);
            foreach (OwnedAsset owned in state.Owned)
                AddDeletePlan(plans, owned);
            return plans;
        }

        private static ConsumerDefinition[] CreateConsumers()
        {
            return new ConsumerDefinition[]
            {
                Consumer("Crafter",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "Crafter.lua"),
                    "486A95F37EF37878296BC776F10D47991E2B6075FDEE777DC531C816855F2D1B",
                    new string[] { "function Crafter.server_onFixedUpdate", "function Crafter.sv_getContainerShapeForRecipe" },
                    Pair("getInputContainers", 3), Pair("getOutputContainers", 2), Pair("getContainerShapeToCollectTo", 1), Pair("getContainerPath", 2)),
                Consumer("FlatVacuum",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "FlatVacuum.lua"),
                    "70E674AE4DB6247C23327DFB826DA87BFC72DD87378A09160D338D1CE2638F2D",
                    new string[] { "function FlatVacuum.server_onFixedUpdate", "function FlatVacuum.cl_n_onIncomingFire" },
                    Pair("getInputContainers", 4), Pair("getOutputContainers", 1), Pair("getContainerShapeToCollectTo", 2), Pair("getContainerPath", 2)),
                Consumer("GarageChest",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "GarageChest.lua"),
                    "D868B7C9D06D776DBF4A037F067C232EE951317C1761D40FD873EC42B4D5C722",
                    new string[] { "function GarageChest.server_onCreate", "function GarageChest.server_onFixedUpdate" },
                    Pair("getInputContainers", 2)),
                Consumer("OreCrusher",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "OreCrusher.lua"),
                    "74B237181DDE8D68CBE15685B73F2375969F10FD7E155D56DC4E7F3151F7CE85",
                    new string[] { "function OreCrusher.server_onFixedUpdate", "function OreCrusher.cl_n_finishProduction" },
                    Pair("getContainerShapeToCollectTo", 2), Pair("getContainerPath", 1)),
                Consumer("Prospector",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "Prospector.lua"),
                    "BC1C078D77D82C4A55D620787F3C0832AD7CB72A16A64CF27C53811C44AE4279",
                    new string[] { "function Prospector.server_onFixedUpdate", "function Prospector.cl_n_depositToChest" },
                    Pair("getInputContainers", 1), Pair("getOutputContainers", 1), Pair("getMatchingPipedContainers", 1), Pair("getContainerPath", 2)),
                Consumer("Refinery",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "Refinery.lua"),
                    "75F008423BC451E3AFB93F0DD1063FEB0015D3B3A5F80DC3C34DC135B8DFF0BE",
                    new string[] { "function Refinery.server_onFixedUpdate", "function Refinery.cl_n_finishProduction" },
                    Pair("getContainerShapeToCollectTo", 2), Pair("getContainerPath", 1)),
                Consumer("Vacuum",
                    Path.Combine("Survival", "Scripts", "game",
                        "interactables", "Vacuum.lua"),
                    "C4272F5FE215F703EC3F91B2DEFF2729E6F549ABC851D80591163FD06955C446",
                    new string[] { "function Vacuum.server_onFixedUpdate", "function Vacuum.cl_n_onIncomingFire" },
                    Pair("getInputContainers", 8), Pair("getOutputContainers", 1), Pair("getContainerShapeToCollectTo", 11), Pair("getContainerShapeToSpendFrom", 2), Pair("getContainerPath", 2)),
                Consumer("Util",
                    Path.Combine("Survival", "Scripts", "util.lua"),
                    "0F768A843C92003AB6AE722C8475F1C4ED586E48634DE44F309648356F0C0B99",
                    new string[] { "function TrySpendFromConnectedContainer", "function CanSpendFromConnectedContainer" },
                    Pair("getMatchingPipedContainers", 2)),
                Consumer("PipeEffects",
                    Path.Combine("Survival", "Scripts", "game", "util",
                        "pipes.lua"),
                    "9E494D72BE3CDB8E666F4B1B2AFD34C2105CA2E653468251ABE8D302180F8146",
                    new string[] { "function PipeEffectPlayer.pushShapeEffectTask", "local function ValidatePath" })
            };
        }

        private static KeyValuePair<string, int> Pair(
            string name, int count)
        {
            return new KeyValuePair<string, int>(name, count);
        }

        private static ConsumerDefinition Consumer(
            string kind, string relative, string hash, string[] guards,
            params KeyValuePair<string, int>[] methods)
        {
            Dictionary<string, int> map =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> method in methods)
                map.Add(method.Key, method.Value);
            return new ConsumerDefinition
            {
                Kind = kind,
                RelativePath = relative,
                KnownHash = hash,
                Guards = guards,
                Methods = map
            };
        }

        private static string NativeCall(string method)
        {
            return "sm.pipeGraph." + method;
        }

        private static string WrapperCall(string method)
        {
            return method == "getContainerPath"
                ? "ScrapLabPipeGraph.getVisualRoute"
                : "ScrapLabPipeGraph." + method;
        }

        private static string LoaderBlock
        {
            get
            {
                return LoaderStart + "\n" +
                    "if ScrapLabPipeGraph == nil then\n" +
                    "\t" + WrapperDofile + "\n" +
                    "end\n" + LoaderEnd;
            }
        }

        private static string CrafterBridge
        {
            get
            {
                return CrafterBridgeStart + "\n" +
                    "function Crafter.sv_n_requestScrapLabGuiContainers( self, _, player )\n" +
                    "\tlocal shapes = ScrapLabPipeGraph.getGuiInputContainers( self.shape )\n" +
                    "\tlocal containers = {}\n" +
                    "\tfor _, shape in ipairs( shapes ) do\n" +
                    "\t\tlocal ok, container = pcall( function() return GetPipeGraphObjectContainer( shape ) end )\n" +
                    "\t\tif ok and container then containers[#containers + 1] = container end\n" +
                    "\tend\n" +
                    "\tself.network:sendToClient( player, \"cl_n_setScrapLabGuiContainers\", containers )\n" +
                    "end\n\n" +
                    "function Crafter.cl_n_setScrapLabGuiContainers( self, containers )\n" +
                    "\tif self.cl.guiInterface == nil then return end\n" +
                    "\tlocal guiContainers = {}\n" +
                    "\tfor _, container in ipairs( containers or {} ) do\n" +
                    "\t\tif container then guiContainers[#guiContainers + 1] = container end\n" +
                    "\tend\n" +
                    "\tguiContainers[#guiContainers + 1] = sm.localPlayer.getPlayer():getInventory()\n" +
                    "\tself.cl.guiInterface:setContainers( \"\", guiContainers )\n" +
                    "end\n" + CrafterBridgeEnd;
            }
        }

        private static string PatchConsumer(
            ConsumerDefinition definition, string text)
        {
            if (AdaptivePatchSupport.Count(text, LoaderStart) != 0 ||
                AdaptivePatchSupport.Count(text, WrapperDofile) != 0)
                throw new InvalidDataException(
                    definition.Kind + " already contains a conflicting pipe wrapper loader.");
            foreach (string guard in definition.Guards)
                RequireCount(text, guard, 1,
                    definition.Kind + " structural guard changed");
            foreach (KeyValuePair<string, int> method in definition.Methods)
            {
                string native = NativeCall(method.Key);
                string wrapper = WrapperCall(method.Key);
                RequireCount(text, native, method.Value,
                    definition.Kind + " protected " + method.Key + " calls changed");
                RequireCount(text, wrapper, 0,
                    definition.Kind + " already contains wrapper calls");
                text = text.Replace(native, wrapper);
            }
            if (definition.Kind == "PipeEffects")
            {
                const string anchor =
                    "function PipeEffectPlayer.pushShapeEffectTask( self, shapeList, item, delay, minimumDuration )";
                RequireCount(text, anchor, 1,
                    "Pipe effect route guard anchor changed");
                RequireCount(text, PipeEffectGuard, 0,
                    "Pipe effect route guard already exists");
                text = text.Replace(anchor, anchor + "\n\n\t" +
                    PipeEffectGuard);
            }
            text = LoaderBlock + "\n\n" + text;
            if (definition.Kind == "Crafter")
            {
                const string requestAnchor =
                    "if IsCraftBot( self.shape:getShapeUuid() ) or IsSawTable( self.shape:getShapeUuid() ) then";
                RequireCount(text, requestAnchor, 1,
                    "Crafter GUI request anchor changed");
                RequireCount(text, CrafterGuiRequest, 0,
                    "Crafter GUI request already exists");
                text = text.Replace(requestAnchor, requestAnchor +
                    "\n\t\t" + CrafterGuiRequest);
                const string classAnchor = "Workbench = class( Crafter )";
                RequireCount(text, classAnchor, 1,
                    "Crafter subclass anchor changed");
                text = text.Replace(classAnchor,
                    CrafterBridge + "\n\n" + classAnchor);
            }
            if (!IsConsumerInstalled(definition, text))
                throw new InvalidDataException(
                    definition.Kind + " generated wrapper output failed verification.");
            return text;
        }

        private static string UnpatchConsumer(
            ConsumerDefinition definition, string text)
        {
            if (!IsConsumerInstalled(definition, text))
                throw new InvalidDataException(
                    definition.Kind + " pipe wrapper is missing, duplicated, or edited.");
            if (definition.Kind == "Crafter")
            {
                text = RemoveUnique(text, CrafterBridge + "\n\n");
                text = RemoveUnique(text, "\n\t\t" + CrafterGuiRequest);
            }
            if (definition.Kind == "PipeEffects")
                text = RemoveUnique(text, "\n\n\t" + PipeEffectGuard);
            text = RemoveUnique(text, LoaderBlock + "\n\n");
            foreach (KeyValuePair<string, int> method in definition.Methods)
                text = text.Replace(WrapperCall(method.Key),
                    NativeCall(method.Key));
            foreach (KeyValuePair<string, int> method in definition.Methods)
                RequireCount(text, NativeCall(method.Key), method.Value,
                    definition.Kind + " native call restoration failed");
            return text;
        }

        private static bool IsConsumerInstalled(
            ConsumerDefinition definition, string text)
        {
            if (AdaptivePatchSupport.Count(text, LoaderBlock) != 1)
                return false;
            foreach (KeyValuePair<string, int> method in definition.Methods)
                if (AdaptivePatchSupport.Count(text,
                        WrapperCall(method.Key)) != method.Value ||
                    AdaptivePatchSupport.Count(text,
                        NativeCall(method.Key)) != 0) return false;
            if (definition.Kind == "Crafter")
            {
                if (AdaptivePatchSupport.Count(text, CrafterBridge) != 1 ||
                    AdaptivePatchSupport.Count(text,
                        CrafterGuiRequest) != 1) return false;
                int bridge = text.IndexOf(CrafterBridge,
                    StringComparison.Ordinal);
                int subclasses = text.IndexOf(
                    "Workbench = class( Crafter )",
                    StringComparison.Ordinal);
                if (bridge < 0 || subclasses < 0 || bridge > subclasses)
                    return false;
            }
            if (definition.Kind == "PipeEffects" &&
                AdaptivePatchSupport.Count(text, PipeEffectGuard) != 1)
                return false;
            return true;
        }

        private static string PatchShapesIndex(string text)
        {
            const string anchor =
                "\t\t\"$SURVIVAL_DATA/Objects/Database/ShapeSets/vacumpipe.shapeset\",";
            return InsertAfterUnique(text, anchor,
                "\n\t\t\"" + ShapeSetPath + "\",");
        }

        private static string UnpatchShapesIndex(string text)
        {
            return RemoveUnique(text,
                "\n\t\t\"" + ShapeSetPath + "\",");
        }

        private static string PatchItems(string text)
        {
            const string anchor =
                "\tobj_pneumatic_pipe_01 = sm.uuid.new( \"59ea6ce8-239b-4eed-8847-a51b907d9b42\" ),";
            return InsertAfterUnique(text, anchor,
                "\n\t-- SCRAPLAB PART: Wireless Vacuum Pipe UUID.\n" +
                "\tobj_pneumatic_pipe_wireless = sm.uuid.new( \"" +
                PartUuid + "\" ),");
        }

        private static string UnpatchItems(string text)
        {
            return RemoveUnique(text,
                "\n\t-- SCRAPLAB PART: Wireless Vacuum Pipe UUID.\n" +
                "\tobj_pneumatic_pipe_wireless = sm.uuid.new( \"" +
                PartUuid + "\" ),");
        }

        private static string ManagerObject
        {
            get
            {
                return "\t\t{\n" +
                    "\t\t\t\"filename\" : \"" + ManagerScriptPath + "\",\n" +
                    "\t\t\t\"classname\" : \"WirelessPipeManager\",\n" +
                    "\t\t\t\"name\" : \"scraplab_wireless_pipe_manager\",\n" +
                    "\t\t\t\"uuid\" : \"" + ManagerUuid + "\",\n" +
                    "\t\t\t\"singleton\" : true\n" +
                    "\t\t}";
            }
        }

        private static string PatchManagers(string text)
        {
            return AppendJsonArrayEntry(text, ManagerObject,
                "The manager registration list ending changed");
        }

        private static string UnpatchManagers(string text)
        {
            return RemoveUnique(text, ",\n" + ManagerObject);
        }

        private static string RecipeEntry
        {
            get
            {
                return ScrapLabCraftbotRecipeOrder.WirelessVacuumPipeRecipe;
            }
        }

        private static string PatchRecipe(string text)
        {
            return ScrapLabCraftbotRecipeOrder.PlaceRecipe(text, PartUuid);
        }

        private static string UnpatchRecipe(string text)
        {
            return ScrapLabCraftbotRecipeOrder.RemoveRecipe(text, PartUuid);
        }

        private static string PatchRecipeManager(string text)
        {
            const string anchor = "\tITEMS.obj_pneumatic_pipe_t,";
            return InsertAfterUnique(text, anchor,
                "\n\t-- SCRAPLAB PART: Wireless Vacuum Pipe default unlock.\n" +
                "\tITEMS.obj_pneumatic_pipe_wireless,");
        }

        private static string UnpatchRecipeManager(string text)
        {
            return RemoveUnique(text,
                "\n\t-- SCRAPLAB PART: Wireless Vacuum Pipe default unlock.\n" +
                "\tITEMS.obj_pneumatic_pipe_wireless,");
        }

        private static string PatchIconXml(string text, int x, int y)
        {
            const string anchor = "        </Group>";
            string entry =
                "            <!-- SCRAPLAB PART: Wireless Vacuum Pipe icon. -->\n" +
                "            <Index name=\"" + PartUuid + "\">\n" +
                "                <Frame point=\"" + x + " " + y + "\"/>\n" +
                "            </Index>\n";
            return InsertBeforeUnique(text, anchor, entry);
        }

        private static string UnpatchIconXml(
            string text, int x, int y)
        {
            return RemoveUnique(text,
                "            <!-- SCRAPLAB PART: Wireless Vacuum Pipe icon. -->\n" +
                "            <Index name=\"" + PartUuid + "\">\n" +
                "                <Frame point=\"" + x + " " + y + "\"/>\n" +
                "            </Index>\n");
        }

        private static string PatchLanguage(
            string text, string title, string description)
        {
            if (AdaptivePatchSupport.Count(text, PartUuid) != 0)
                throw new InvalidDataException(
                    "A Wireless Vacuum Pipe language entry already exists or conflicts.");
            int end = text.LastIndexOf("\n}", StringComparison.Ordinal);
            if (end < 0 || !String.IsNullOrWhiteSpace(
                text.Substring(end + 2)))
                throw new InvalidDataException(
                    "An inventory-description object has an unexpected ending.");
            return text.Substring(0, end) + ",\n" +
                LanguageEntry(title, description) + text.Substring(end);
        }

        private static string UnpatchLanguage(
            string text, string title, string description)
        {
            return RemoveUnique(text, ",\n" +
                LanguageEntry(title, description));
        }

        private static string LanguageEntry(
            string title, string description)
        {
            return "\t\"" + PartUuid + "\": {\n" +
                "\t\t\"description\": \"" + JsonEscape(description) + "\",\n" +
                "\t\t\"title\": \"" + JsonEscape(title) + "\",\n" +
                "\t\t\"upperCaseTitle\": \"" +
                JsonEscape(title.ToUpperInvariant()) + "\"\n" +
                "\t}";
        }

        private static string AppendJsonArrayEntry(
            string text, string entry, string error)
        {
            const string suffix = "\n\t]\n}";
            int index = text.LastIndexOf(suffix,
                StringComparison.Ordinal);
            if (index < 0 || !String.IsNullOrWhiteSpace(
                text.Substring(index + suffix.Length)))
                throw new InvalidDataException(error + ".");
            return text.Insert(index, ",\n" + entry);
        }

        private static string InsertAfterUnique(
            string text, string anchor, string addition)
        {
            RequireCount(text, anchor, 1,
                "A protected Wireless Vacuum Pipe insertion anchor changed");
            return text.Replace(anchor, anchor + addition);
        }

        private static string InsertBeforeUnique(
            string text, string anchor, string addition)
        {
            RequireCount(text, anchor, 1,
                "A protected Wireless Vacuum Pipe insertion anchor changed");
            return text.Replace(anchor, addition + anchor);
        }

        private static string RemoveUnique(string text, string value)
        {
            RequireCount(text, value, 1,
                "A Wireless Vacuum Pipe patch snippet is missing, duplicated, or edited");
            return text.Replace(value, "");
        }

        private static void RequireCount(
            string text, string value, int expected, string error)
        {
            int count = AdaptivePatchSupport.Count(text, value);
            if (count != expected)
                throw new InvalidDataException(
                    error + " (expected " + expected + ", found " +
                    count + ").");
        }

        private static string JsonEscape(string value)
        {
            StringBuilder output = new StringBuilder();
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': output.Append("\\\\"); break;
                    case '"': output.Append("\\\""); break;
                    case '\r': output.Append("\\r"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\t': output.Append("\\t"); break;
                    default: output.Append(character); break;
                }
            }
            return output.ToString();
        }

        private static void AddTextPlan(
            List<AtomicCustomPartFilePlan> plans,
            TextState state, string output)
        {
            if (output == null)
                throw new InvalidDataException(
                    state.DisplayName + " has no verified output.");
            AddBinaryPlan(plans, state.RelativePath,
                state.DisplayName, state.Path,
                state.Document.OriginalBytes,
                state.Document.Render(output), false);
        }

        private static void AddBinaryPlan(
            List<AtomicCustomPartFilePlan> plans,
            string relative, string display, string path,
            byte[] source, byte[] output, bool atlas)
        {
            plans.Add(new AtomicCustomPartFilePlan
            {
                RelativePath = relative,
                DisplayName = display,
                Path = path,
                SourceExists = true,
                SourceBytes = source,
                OutputBytes = output,
                SourceHash = AdaptivePatchSupport.Sha256(source),
                OutputHash = AdaptivePatchSupport.Sha256(output),
                IsAtlas = atlas
            });
        }

        private static void AddOwnedPlan(
            List<AtomicCustomPartFilePlan> plans, OwnedAsset owned,
            bool restoreAsMissing)
        {
            byte[] source = owned.Missing
                ? null : File.ReadAllBytes(owned.Path);
            plans.Add(new AtomicCustomPartFilePlan
            {
                RelativePath = owned.RelativePath,
                DisplayName = owned.DisplayName,
                Path = owned.Path,
                SourceExists = !owned.Missing,
                ReceiptSourceMissing = restoreAsMissing,
                SourceBytes = source,
                OutputBytes = owned.Bytes,
                SourceHash = owned.Missing ? "MISSING" :
                    AdaptivePatchSupport.Sha256(source),
                OutputHash = AdaptivePatchSupport.Sha256(owned.Bytes),
                IsAtlas = false
            });
        }

        private static void AddDeletePlan(
            List<AtomicCustomPartFilePlan> plans, OwnedAsset owned)
        {
            if (!File.Exists(owned.Path))
                throw new FileNotFoundException(
                    owned.DisplayName + " is missing.", owned.Path);
            byte[] source = File.ReadAllBytes(owned.Path);
            plans.Add(new AtomicCustomPartFilePlan
            {
                RelativePath = owned.RelativePath,
                DisplayName = owned.DisplayName,
                Path = owned.Path,
                SourceExists = true,
                SourceBytes = source,
                OutputBytes = null,
                SourceHash = AdaptivePatchSupport.Sha256(source),
                OutputHash = "MISSING",
                ForceDeleteOnRemove = true,
                IsAtlas = false
            });
        }

        private static TextState FindText(
            List<TextState> texts, string relative)
        {
            foreach (TextState text in texts)
                if (String.Equals(text.RelativePath, relative,
                    StringComparison.OrdinalIgnoreCase)) return text;
            throw new InvalidOperationException(
                "Wireless Vacuum Pipe target was not prepared: " +
                relative);
        }

        private static bool IsTrustedExistingOutput(
            string relative, string hash)
        {
            string[] modKeys = new string[]
            {
                "RaidDetector", "BetterPlasmaDrills",
                "FullSpeedCarrying", "BetterFreezerBeehive",
                "BetterEngines", "ResourceLocator",
                "ChemicalFertilizerSplash", "DualFluidCannon",
                "DeveloperCommands", "RevivalBuffRecovery",
                "NetworkStorageChest"
            };
            foreach (string modKey in modKeys)
            {
                AdaptivePatchReceipt receipt =
                    AdaptivePatchSupport.LoadReceipt(modKey);
                AdaptivePatchReceiptFile file =
                    AdaptivePatchSupport.FindReceiptFile(
                        receipt, relative);
                if (file != null && String.Equals(file.OutputHash, hash,
                    StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static byte[] GetResource(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "The embedded Wireless Vacuum Pipe asset is missing: " + name);
                using (MemoryStream output = new MemoryStream())
                {
                    stream.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        private static SteamBuildInfo ReadBuild(
            string gamePath, GamePatchResult result)
        {
            string executable = Path.Combine(
                gamePath, "Release", "ScrapMechanic.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException(
                    "ScrapMechanic.exe was not found.", executable);
            result.GameVersion = FileVersionInfo.GetVersionInfo(
                executable).FileVersion;
            return AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);
        }

        private static void CleanupOwnedFiles(ProbeState state)
        {
            foreach (OwnedAsset owned in state.Owned)
                if (File.Exists(owned.Path) && BytesEqual(
                    File.ReadAllBytes(owned.Path), owned.Bytes))
                    File.Delete(owned.Path);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null ||
                left.Length != right.Length) return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static GamePatchResult NewResult(
            string gamePath, bool installed)
        {
            return new GamePatchResult
            {
                GamePath = gamePath,
                Installed = installed,
                Changes = new List<string>()
            };
        }

        private static GamePatchResult Failure(string message)
        {
            return new GamePatchResult
            {
                Success = false,
                Error = message,
                Changes = new List<string>()
            };
        }
    }
}
