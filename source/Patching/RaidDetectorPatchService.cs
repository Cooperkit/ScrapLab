using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace RaidRescue
{
    internal static class RaidDetectorPatchService
    {
        private const string ModKey = "RaidDetector";
        private const string DefinitionVersion = "2";
        internal const string PartUuid =
            "a638a8aa-6f4f-41c2-9e31-702687066092";
        internal const string VerifiedSteamBuildId = "24529696";
        internal const string VerifiedGameVersion = "1.0.5.876";

        private const string ShapeSetPath =
            "$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/RaidDetector.shapeset";
        private const string ScriptResource =
            "RaidRescue.Parts.RaidDetector.RaidDetector.lua";
        private const string LegacyScriptResource =
            "RaidRescue.Parts.RaidDetector.RaidDetectorLegacyV1.lua";
        private const string ShapeResource =
            "RaidRescue.Parts.RaidDetector.RaidDetector.shapeset";
        private const string IconResource =
            "RaidRescue.Parts.RaidDetector.RaidDetectorIcon.png";

        private static readonly string ShapesIndexRelative = Path.Combine(
            "Survival", "Objects", "Database", "shapesets.json");
        private static readonly string ItemsRelative = Path.Combine(
            "Survival", "Scripts", "game", "survival_items.lua");
        private static readonly string TradesRelative = Path.Combine(
            "Survival", "CraftingRecipes", "hideout.json");
        private static readonly string TraderRelative = Path.Combine(
            "Survival", "Scripts", "game", "interactables", "HideoutTrader.lua");
        private static readonly string IconXmlRelative = Path.Combine(
            "Survival", "Gui", "IconMapSurvival.xml");
        private static readonly string IconPngRelative = Path.Combine(
            "Survival", "Gui", "IconMapSurvival.png");
        private static readonly string ScriptRelative = Path.Combine(
            "Survival", "Scripts", "ScrapLab", "Parts", "RaidDetector",
            "RaidDetector.lua");
        private static readonly string ShapeRelative = Path.Combine(
            "Survival", "Objects", "Database", "ShapeSets", "ScrapLab",
            "Parts", "RaidDetector.shapeset");

        private const string ShapesHash =
            "FF30F988FCDF775604AA54E1AF3E97CBCC4AE45F7EDCAB7B528694933D7E2511";
        private const string ItemsHash =
            "ACDAD2CF9163655F87796D996A58DDE381AC1221B1337AEF049E38066B199789";
        private const string TradesHash =
            "69E355B255975BA9AD3F20DB7FD568F1A57AC21D92DF14618C4A558383015068";
        private const string TraderHash =
            "6C5EB46FB1E7C950E365E98413D5BA24F5642A90BD3B6D5186E884DEE2AEE7E6";
        private const string IconXmlHash =
            "5DA34EF427C912BDF64BD1993834A78DBD86F11DFF16FD63B61F3FA9C1ECDDDB";
        private const string IconPngHash =
            "4288CAA081C8674E8D69640C717802C3883E1AA53181C6A9ABA86BBCFE7D9146";
        private const string CurrentIconPngHash =
            "C33A5A5DE6E7B11B7F9319BA928383E5DDF02E78C35BBCF25CA789AEF627A4D5";

        private static readonly string[,] Languages = new string[,]
        {
            { "Brazilian", "3C6EAC82C2B49E9215196883FCB8B74AD749CBE82EFEA151D01D734A98592440", "Detector de Incursões", "Emite um sinal lógico enquanto uma incursão programada ou ativa estiver dentro de 256 metros." },
            { "Chinese", "03C675DF2720E7148E94140226A62BA3F7F96AA266851D5DA3B793CBC90D636D", "突袭探测器", "当256米范围内存在已计划或正在进行的突袭时，输出逻辑信号。" },
            { "English", "BA935E110D8B0A5FC4AEFAAB0E76A7AA4A26ACEE6B6AD093F0F7E801B05AF3EE", "Raid Detector", "Outputs a logic signal while a scheduled or active raid is within 256 meters." },
            { "French", "4A56BC8A64C378DED64A40EEDC05FDA35DC9EB3F837E1F6850EEA05E0EEC2F4F", "Détecteur de raid", "Émet un signal logique lorsqu'un raid planifié ou actif se trouve dans un rayon de 256 mètres." },
            { "German", "7D48F80C66A21F555003EB287D2E3E35031F05528305825498906D783DB286F8", "Raid-Detektor", "Gibt ein Logiksignal aus, solange sich ein geplanter oder aktiver Raid im Umkreis von 256 Metern befindet." },
            { "Italian", "C8DE862CFB7F8A2B6833B0BAA92195C4D4A09B1BB16D959B0344DDB6D19D836B", "Rilevatore di incursioni", "Emette un segnale logico quando un'incursione programmata o attiva si trova entro 256 metri." },
            { "Japanese", "98D81E337E4EC87B2BE2D5B5DE0209BEDFC54096AC153FB0F1BBE23D85E1B7C0", "レイド探知機", "256メートル以内に予定中または進行中のレイドがある間、ロジック信号を出力します。" },
            { "Korean", "579693B35D7D9F95A997944667CDE15ECAB9C4740C55375ED3087FCA5C719BDD", "습격 감지기", "256미터 안에 예정되었거나 진행 중인 습격이 있으면 논리 신호를 출력합니다." },
            { "Polish", "B45802E61196CECEAB7BC51A0075F3051ED069EB4D1571010A8999CB79C544E9", "Wykrywacz najazdów", "Wysyła sygnał logiczny, gdy zaplanowany lub aktywny najazd znajduje się w promieniu 256 metrów." },
            { "Russian", "8DC2C7D60D2E7756D18123588596F7108F1C657FFF99C9924B089DA8EA3BE855", "Детектор рейдов", "Выдаёт логический сигнал, пока запланированный или активный рейд находится в радиусе 256 метров." },
            { "Spanish", "ECA2E0850CC3DC56AE811B2EB4043B0C657D407D9E8F813F9CBC7E3BF7EB6704", "Detector de incursiones", "Emite una señal lógica mientras haya una incursión programada o activa a menos de 256 metros." }
        };

        private sealed class FilePlan
        {
            public string RelativePath;
            public string DisplayName;
            public string Path;
            public byte[] SourceBytes;
            public byte[] OutputBytes;
            public string SourceHash;
            public string OutputHash;
            public string BackupFile;
            public bool SourceExists;
            public bool ReceiptSourceMissing;
        }

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
        }

        private sealed class ProbeState
        {
            public List<TextState> Texts = new List<TextState>();
            public byte[] AtlasBytes;
            public string AtlasPath;
            public string AtlasHash;
            public byte[] IconBytes;
            public List<ScrapLabIconAtlasCoordinator.IconAsset> IconCatalog;
            public ScrapLabIconAtlasCoordinator.CatalogPlan CatalogPlan;
            public ScrapLabIconAtlasCoordinator.SharedAtlasReceipt
                SharedAtlasReceipt;
            public ScrapLabIconAtlasCoordinator.AtlasInfo AtlasInfo;
            public byte[] ScriptBytes;
            public byte[] LegacyScriptBytes;
            public byte[] ShapeBytes;
            public string ScriptPath;
            public string ShapePath;
            public bool OwnedClean;
            public bool OwnedInstalled;
            public bool AtlasClean;
            public bool AtlasInstalled;
            public bool AtlasKnown;
            public bool IconUpdateAvailable;
            public bool LogicUpdateAvailable;
            public bool OrphanedOwnedAssets;
            public bool AllClean;
            public bool AllInstalled;
            public bool AllKnownClean;
        }

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
                    result.NeedsUpdate = UpdateAvailable(state);
                    AdaptivePatchSupport.FillResult(result, build,
                        UpdateAvailable(state)
                            ? PatchCompatibilityState.DefinitionUpdate
                            : PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        UpdateAvailable(state)
                            ? UpdateReason(state)
                            : "The Raid Detector part, trade, logic script, localization, and icon are intact.");
                    return result;
                }
                if (state.AllClean)
                {
                    string reason;
                    bool canApply = CanApplyClean(state, build,
                        out reason);
                    result.Success = true;
                    result.Installed = false;
                    if (AdaptivePatchSupport.
                        HasReceiptOrSupersededState(ModKey) &&
                        canApply)
                    {
                        AdaptivePatchSupport.FillResult(result, build,
                            "REINSTALL REQUIRED - SAVE PART AT RISK",
                            true, true,
                            "Steam replaced the Raid Detector registrations. Reinstall before loading a world that may contain the part.");
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
                if (receipt != null && build != null && build.Valid &&
                    !String.Equals(receipt.SteamBuildId, build.BuildId,
                        StringComparison.Ordinal))
                {
                    AdaptivePatchSupport.FillResult(result, build,
                        "REINSTALL REQUIRED - SAVE PART AT RISK",
                        true, false,
                        "Steam replaced only part of the Raid Detector installation. Verify the game files if needed, then reinstall before loading an affected save.");
                    return result;
                }
                AdaptivePatchSupport.FillResult(result, build,
                    PatchCompatibilityState.PartialConflict,
                    false, false,
                    "A Raid Detector registration, owned file, or inventory-icon tile is missing, duplicated, or edited.");
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
                    "Scrap Mechanic is running. Close the game completely before changing the Raid Detector mod.");
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
                    !UpdateAvailable(state))
                {
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(
                        gamePath, backupRoot, state.IconCatalog);
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build,
                        PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        "Raid Detector is already installed.");
                    return result;
                }
                if (enabled && state.AllInstalled &&
                    UpdateAvailable(state))
                {
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(
                        gamePath, backupRoot, state.IconCatalog);
                    List<FilePlan> updatePlans =
                        BuildDefinitionUpdatePlans(state);
                    ApplyPlans(updatePlans, result, gamePath, backupRoot,
                        build, true, true);
                    result.Success = true;
                    result.Installed = true;
                    result.NeedsUpdate = false;
                    result.FilesPatched = updatePlans.Count;
                    if (state.LogicUpdateAvailable)
                        result.Changes.Add(
                            "Fixed the Raid Detector world lookup so scheduled and active raids drive its logic output.");
                    if (state.IconUpdateAvailable)
                        result.Changes.Add(
                            "Replaced the verified legacy Raid Detector icon with its transparent-background version.");
                    AdaptivePatchSupport.FillResult(result, build,
                        PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        "The Raid Detector definition update was installed and verified.");
                    SecretModBackupRetention.Prune(
                        backupRoot, ModKey, result.BackupPath, result);
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
                        "Raid Detector is already removed.");
                    return result;
                }
                bool retiredSupersededState = false;
                if (enabled)
                {
                    if (!state.AllClean)
                        throw new InvalidOperationException(
                            "Raid Detector cannot be installed because a registration, owned file, or atlas tile is partial or conflicting.");
                    string reason;
                    if (!CanApplyClean(state, build, out reason))
                        throw new InvalidOperationException(
                            "Raid Detector cannot be installed: " + reason);
                    retiredSupersededState =
                        AdaptivePatchSupport.RetireVerifiedSupersededReceipt(
                            ModKey,
                            "Steam Verify removed the Raid Detector registrations while leaving its old install receipt behind.");
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(
                        gamePath, backupRoot, state.IconCatalog);
                }
                else if (!state.AllInstalled)
                {
                    throw new InvalidOperationException(
                        "Raid Detector cannot be removed because its protected files or icon were edited.");
                }
                else
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(
                        gamePath, backupRoot, state.IconCatalog);

                List<FilePlan> plans = enabled
                    ? BuildInstallPlans(state, build, backupRoot)
                    : BuildRemovePlans(state, backupRoot);
                ApplyPlans(plans, result, gamePath, backupRoot,
                    build, enabled, false);
                result.Success = true;
                result.Installed = enabled;
                result.FilesPatched = plans.Count;
                result.Changes.Add(enabled
                    ? "Added the beacon-based Raid Detector and its 256-meter logic output."
                    : "Removed the Raid Detector registrations and owned part files.");
                result.Changes.Add(enabled
                    ? "Added a repeatable Hideout trade costing four Caged Farmers."
                    : "Removed the Raid Detector Hideout trade and inventory description.");
                result.Changes.Add(enabled
                    ? "Installed the shared ScrapLab icon pack into verified bottom-of-atlas tiles and registered the Raid Detector icon."
                    : "Removed the Raid Detector icon registration and restored the shared atlas only when no custom-part mods remained.");
                if (retiredSupersededState)
                    result.Changes.Add(
                        "Automatically retired the Steam-overwritten Raid Detector receipt before creating a fresh uninstall state.");
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
                        ? "Raid Detector was installed and verified."
                        : "Raid Detector was removed and verified.");
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
                PatchShapesIndex, UnpatchShapesIndex, null));
            state.Texts.Add(ReadText(gamePath, ItemsRelative,
                "survival_items.lua", ItemsHash, PartUuid,
                PatchItems, UnpatchItems,
                BetterPlasmaDrillsPatchService.HasIntactItemsPatch));
            state.Texts.Add(ReadText(gamePath, TradesRelative,
                "hideout.json", TradesHash, PartUuid,
                PatchTrades, UnpatchTrades, null));
            state.Texts.Add(ReadText(gamePath, TraderRelative,
                "HideoutTrader.lua", TraderHash,
                "-- SCRAPLAB PART: Raid Detector trade.",
                PatchTrader, UnpatchTrader, null));
            state.Texts.Add(ReadText(gamePath, IconXmlRelative,
                "IconMapSurvival.xml", IconXmlHash, PartUuid,
                delegate(string text) { return text; },
                delegate(string text) { return text; },
                BetterPlasmaDrillsPatchService.HasIntactIconsPatch,
                true));
            for (int index = 0; index < Languages.GetLength(0); index++)
            {
                string language = Languages[index, 0];
                state.Texts.Add(ReadLanguage(gamePath,
                    Path.Combine("Survival", "Gui", "Language", language,
                        "inventoryDescriptions.json"),
                    language + " inventory descriptions",
                    Languages[index, 1], Languages[index, 2],
                    Languages[index, 3]));
            }

            TextState iconXml = FindText(state.Texts, IconXmlRelative);
            state.AtlasPath = Path.Combine(gamePath, IconPngRelative);
            if (!File.Exists(state.AtlasPath))
                throw new FileNotFoundException(
                    "IconMapSurvival.png was not found.", state.AtlasPath);
            state.AtlasBytes = File.ReadAllBytes(state.AtlasPath);
            state.AtlasHash = AdaptivePatchSupport.Sha256(state.AtlasBytes);
            state.IconCatalog = ScrapLabIconAtlasCoordinator.LoadCatalog();
            state.IconBytes = ScrapLabIconAtlasCoordinator.FindCatalogIcon(
                state.IconCatalog, PartUuid).Bytes;
            state.AtlasInfo = ScrapLabIconAtlasCoordinator.Inspect(
                iconXml.Document.NormalizedText, state.AtlasBytes,
                state.IconBytes, PartUuid);
            state.AtlasInstalled = state.AtlasInfo.EntryPresent &&
                state.AtlasInfo.IconPresent;
            if (!state.AtlasInfo.EntryPresent)
            {
                try
                {
                    state.CatalogPlan =
                        ScrapLabIconAtlasCoordinator.EnsureCatalog(
                        iconXml.Document.NormalizedText, state.AtlasBytes,
                        state.IconCatalog);
                    state.AtlasClean = true;
                }
                catch { state.AtlasClean = false; }
            }
            else
            {
                state.CatalogPlan =
                    ScrapLabIconAtlasCoordinator.EnsureCatalog(
                        iconXml.Document.NormalizedText, state.AtlasBytes,
                        state.IconCatalog);
            }
            if (state.AtlasInfo.EntryPresent &&
                state.CatalogPlan != null &&
                state.CatalogPlan.Placements.ContainsKey(PartUuid))
            {
                state.AtlasInstalled = true;
                state.IconUpdateAvailable =
                    state.CatalogPlan.AtlasChanged;
            }
            state.SharedAtlasReceipt =
                ScrapLabIconAtlasCoordinator.LoadReceipt(
                    AdaptivePatchSupport.GetSharedStatePath(
                        "ScrapLab-Icon-Pack.json"));
            state.AtlasKnown = (String.Equals(
                state.AtlasHash, IconPngHash,
                    StringComparison.OrdinalIgnoreCase) ||
                String.Equals(state.AtlasHash, CurrentIconPngHash,
                    StringComparison.OrdinalIgnoreCase) ||
                ScrapLabIconAtlasCoordinator.IsTrustedReceipt(
                    state.SharedAtlasReceipt, state.AtlasHash,
                    state.IconCatalog));
            if (state.SharedAtlasReceipt != null &&
                String.Equals(state.SharedAtlasReceipt.IconXmlHash,
                    iconXml.Document.OriginalHash,
                    StringComparison.OrdinalIgnoreCase))
                iconXml.Known = true;

            state.ScriptBytes = GetResource(ScriptResource);
            state.LegacyScriptBytes = GetResource(LegacyScriptResource);
            state.ShapeBytes = GetResource(ShapeResource);
            state.ScriptPath = Path.Combine(gamePath, ScriptRelative);
            state.ShapePath = Path.Combine(gamePath, ShapeRelative);
            bool scriptMissing = !File.Exists(state.ScriptPath);
            bool shapeMissing = !File.Exists(state.ShapePath);
            bool scriptExact = !scriptMissing && BytesEqual(
                File.ReadAllBytes(state.ScriptPath), state.ScriptBytes);
            bool scriptLegacy = !scriptMissing && BytesEqual(
                File.ReadAllBytes(state.ScriptPath), state.LegacyScriptBytes);
            bool shapeExact = !shapeMissing && BytesEqual(
                File.ReadAllBytes(state.ShapePath), state.ShapeBytes);
            state.LogicUpdateAvailable = scriptLegacy;
            state.OwnedInstalled = (scriptExact || scriptLegacy) && shapeExact;
            state.OwnedClean = (scriptMissing || scriptExact || scriptLegacy) &&
                (shapeMissing || shapeExact);

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
            state.AllClean = textsClean && state.AtlasClean && state.OwnedClean;
            state.AllInstalled = textsInstalled && state.AtlasInstalled &&
                state.OwnedInstalled;
            state.OrphanedOwnedAssets = textsClean && state.AtlasClean &&
                state.OwnedInstalled;
            state.AllKnownClean = known && state.AtlasKnown;
            return state;
        }

        private static TextState ReadText(
            string gamePath, string relative, string display,
            string knownHash, string marker,
            Func<string, string> patch, Func<string, string> unpatch,
            Func<string, bool> trusted, bool atlasXml = false)
        {
            string path = Path.Combine(gamePath, relative);
            if (!File.Exists(path))
                throw new FileNotFoundException(display + " was not found.", path);
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
                    TreeSaplingsPatchService.IsTrustedOutput(
                        relative, document.OriginalHash) ||
                    TreeSaplingsPatchService.HasIntactSharedPatch(
                        relative, document.NormalizedText) ||
                    (trusted != null && trusted(document.NormalizedText))
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
                // Shared append-only files can gain later verified ScrapLab
                // blocks after this one. Exact unpatching proves our block is
                // intact; re-patching proves the protected insertion anchor is
                // still compatible. Sibling block order is not part of this
                // mod's integrity contract.
                state.Installed = AdaptivePatchSupport.Count(
                    state.CleanText, marker) == 0;
            }
            return state;
        }

        private static TextState ReadLanguage(
            string gamePath, string relative, string display,
            string knownHash, string title, string description)
        {
            string path = Path.Combine(gamePath, relative);
            if (!File.Exists(path))
                throw new FileNotFoundException(display + " was not found.", path);
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
                    TreeSaplingsPatchService.IsTrustedOutput(
                        relative, document.OriginalHash) ||
                    TreeSaplingsPatchService.HasIntactSharedPatch(
                        relative, text) ||
                    BetterPlasmaDrillsPatchService.HasIntactLanguagePatch(text)
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
                // The exact entry and its uniqueness are the protected contract;
                // its position in the JSON object is deliberately irrelevant.
                state.CleanText = UnpatchLanguage(text, title, description);
                state.PatchedText = text;
                state.Installed =
                    AdaptivePatchSupport.Count(state.CleanText, PartUuid) == 0;
            }
            return state;
        }

        private static bool CanApplyClean(
            ProbeState state, SteamBuildInfo build, out string reason)
        {
            if (state.AllKnownClean)
            {
                reason = "Verified Steam build 24529696 Raid Detector targets.";
                return true;
            }
            if (build != null && build.Valid &&
                String.Equals(build.BuildId, VerifiedSteamBuildId,
                    StringComparison.Ordinal) &&
                String.Equals(build.GameVersion, VerifiedGameVersion,
                    StringComparison.Ordinal))
            {
                reason = "A protected Raid Detector target differs from the verified current Steam build.";
                return false;
            }
            List<string> unknown = new List<string>();
            foreach (TextState text in state.Texts)
                if (!text.Known) unknown.Add(text.Path);
            if (!state.AtlasKnown)
                unknown.Add(state.AtlasPath);
            return AdaptivePatchSupport.CanAdaptCleanFiles(
                build, unknown, out reason);
        }

        private static List<FilePlan> BuildInstallPlans(
            ProbeState state, SteamBuildInfo build, string backupRoot)
        {
            List<FilePlan> plans = new List<FilePlan>();
            foreach (TextState text in state.Texts)
            {
                if (String.Equals(text.RelativePath, IconXmlRelative,
                    StringComparison.OrdinalIgnoreCase))
                    continue;
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
            int x = placement.X;
            int y = placement.Y;
            string xmlOutput = PatchIconXml(
                iconXml.Document.NormalizedText, x, y);
            AddTextPlan(plans, iconXml, xmlOutput);
            if (catalogPlan.AtlasChanged)
                AddBinaryPlan(plans, IconPngRelative,
                    "IconMapSurvival.png", state.AtlasPath,
                    state.AtlasBytes, catalogPlan.AtlasBytes);
            AddOwnedPlan(plans, ScriptRelative, "RaidDetector.lua",
                state.ScriptPath, state.ScriptBytes,
                state.OrphanedOwnedAssets);
            AddOwnedPlan(plans, ShapeRelative, "RaidDetector.shapeset",
                state.ShapePath, state.ShapeBytes,
                state.OrphanedOwnedAssets);
            return plans;
        }

        private static List<FilePlan> BuildRemovePlans(
            ProbeState state, string backupRoot)
        {
            List<FilePlan> plans = new List<FilePlan>();
            foreach (TextState text in state.Texts)
            {
                if (String.Equals(text.RelativePath, IconXmlRelative,
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                AddTextPlan(plans, text, text.CleanText);
            }
            TextState iconXml = FindText(state.Texts, IconXmlRelative);
            int x;
            int y;
            if (!ScrapLabIconAtlasCoordinator.TryGetEntry(
                iconXml.Document.NormalizedText, PartUuid, out x, out y))
                throw new InvalidDataException(
                    "The Raid Detector icon registration is missing.");
            string xmlOutput = UnpatchIconXml(
                iconXml.Document.NormalizedText, x, y);
            AddTextPlan(plans, iconXml, xmlOutput);
            byte[] baseline =
                AtomicCustomPartPatchSupport.ReadActiveAtlasBaseline();
            byte[] atlasOutput =
                ScrapLabIconAtlasCoordinator.RemoveCatalogWhenUnused(
                    xmlOutput, state.AtlasBytes, state.IconCatalog,
                    baseline);
            if (!HashEquals(AdaptivePatchSupport.Sha256(atlasOutput),
                state.AtlasHash))
                AddBinaryPlan(plans, IconPngRelative,
                    "IconMapSurvival.png", state.AtlasPath,
                    state.AtlasBytes, atlasOutput);
            AddDeletePlan(plans, ScriptRelative,
                "RaidDetector.lua", state.ScriptPath);
            AddDeletePlan(plans, ShapeRelative,
                "RaidDetector.shapeset", state.ShapePath);
            return plans;
        }

        private static List<FilePlan> BuildDefinitionUpdatePlans(
            ProbeState state)
        {
            if (!UpdateAvailable(state))
                throw new InvalidOperationException(
                    "The Raid Detector definition update is not available.");
            List<FilePlan> plans = new List<FilePlan>();
            if (state.IconUpdateAvailable)
            {
                if (state.CatalogPlan == null ||
                    !state.CatalogPlan.AtlasChanged)
                    throw new InvalidOperationException(
                        "The Raid Detector icon update is incomplete.");
                AddBinaryPlan(plans, IconPngRelative,
                    "IconMapSurvival.png", state.AtlasPath,
                    state.AtlasBytes, state.CatalogPlan.AtlasBytes);
            }
            if (state.LogicUpdateAvailable)
                AddOwnedPlan(plans, ScriptRelative, "RaidDetector.lua",
                    state.ScriptPath, state.ScriptBytes, false);
            return plans;
        }

        private static bool UpdateAvailable(ProbeState state)
        {
            return state != null &&
                (state.IconUpdateAvailable || state.LogicUpdateAvailable);
        }

        private static string UpdateReason(ProbeState state)
        {
            if (state.LogicUpdateAvailable && state.IconUpdateAvailable)
                return "Verified Raid Detector logic and transparent-icon updates are ready.";
            if (state.LogicUpdateAvailable)
                return "A verified Raid Detector logic fix is ready for scheduled and active raids.";
            return "A verified transparent-background Raid Detector icon update is ready.";
        }

        private static void ApplyPlans(
            List<FilePlan> plans, GamePatchResult result,
            string gamePath, string backupRoot,
            SteamBuildInfo build, bool enabled,
            bool preserveModReceipt)
        {
            if (!preserveModReceipt)
            {
                List<AtomicCustomPartFilePlan> atomicPlans =
                    new List<AtomicCustomPartFilePlan>();
                foreach (FilePlan plan in plans)
                {
                    atomicPlans.Add(new AtomicCustomPartFilePlan
                    {
                        RelativePath = plan.RelativePath,
                        DisplayName = plan.DisplayName,
                        Path = plan.Path,
                        SourceBytes = plan.SourceBytes,
                        OutputBytes = plan.OutputBytes,
                        SourceHash = plan.SourceHash,
                        OutputHash = plan.OutputHash,
                        SourceExists = plan.SourceExists,
                        ReceiptSourceMissing = plan.ReceiptSourceMissing,
                        ForceDeleteOnRemove = plan.OutputBytes == null,
                        IsAtlas = IsAtlasPlan(plan)
                    });
                }
                AtomicCustomPartPatchSupport.Apply(
                    ModKey, "Raid Detector", DefinitionVersion,
                    atomicPlans, result, gamePath, backupRoot, build,
                    enabled, ScrapLabIconAtlasCoordinator.LoadCatalog());
                return;
            }

            AdaptivePatchReceipt prior =
                AdaptivePatchSupport.LoadReceipt(ModKey);
            if (prior == null || prior.Files == null)
                throw new InvalidOperationException(
                    "The original Raid Detector receipt is missing, so its definition cannot be updated safely.");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(backupRoot,
                (enabled ? "Install-" : "Remove-") + ModKey + "-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            string sharedAtlasBaseline =
                AdaptivePatchSupport.GetActiveSharedAtlasBaselinePath();
            string sharedStatePath = AdaptivePatchSupport.GetSharedStatePath(
                "ScrapLab-Icon-Pack.json");
            bool sharedStateExisted = File.Exists(sharedStatePath);
            byte[] sharedStateBytes = sharedStateExisted
                ? File.ReadAllBytes(sharedStatePath) : null;
            bool baselineExisted = File.Exists(sharedAtlasBaseline);
            byte[] baselineBytes = baselineExisted
                ? File.ReadAllBytes(sharedAtlasBaseline) : null;
            List<AdaptivePatchReceiptFile> manifest =
                new List<AdaptivePatchReceiptFile>();
            foreach (FilePlan plan in plans)
            {
                bool atlas = IsAtlasPlan(plan);
                plan.BackupFile = atlas
                    ? sharedAtlasBaseline
                    : Path.Combine(backupPath, plan.RelativePath);
                if (plan.SourceExists)
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(plan.BackupFile));
                    if (!atlas)
                    {
                        File.WriteAllBytes(plan.BackupFile, plan.SourceBytes);
                    }
                    else if (enabled && !File.Exists(plan.BackupFile))
                    {
                        List<ScrapLabIconAtlasCoordinator.IconAsset> catalog =
                            ScrapLabIconAtlasCoordinator.LoadCatalog();
                        if (ScrapLabIconAtlasCoordinator.
                            ContainsAnyCatalogPixels(plan.SourceBytes, catalog))
                            throw new InvalidOperationException(
                                "The shared ScrapLab icon pack baseline is missing while managed icon pixels are already present.");
                        WriteAtomic(plan.BackupFile, plan.SourceBytes,
                            ModKey + "-shared-atlas-baseline");
                    }
                    if (!atlas && !HashEquals(
                        AdaptivePatchSupport.Sha256(plan.BackupFile),
                        plan.SourceHash))
                        throw new IOException(
                            plan.DisplayName + " backup failed checksum verification.");
                    if (atlas && enabled &&
                        !File.Exists(plan.BackupFile))
                        throw new IOException(
                            "The shared ScrapLab icon atlas baseline could not be created.");
                }
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
                "Raid Detector", enabled ? "Install" : "Remove",
                gamePath, build, DefinitionVersion, manifest);

            bool exactRestore = !enabled && CanExactRestore(plans, prior);
            List<FilePlan> changed = new List<FilePlan>();
            try
            {
                foreach (FilePlan plan in plans)
                {
                    byte[] output = plan.OutputBytes;
                    bool delete = output == null;
                    if (exactRestore && !IsAtlasPlan(plan))
                    {
                        AdaptivePatchReceiptFile receiptFile =
                            AdaptivePatchSupport.FindReceiptFile(
                                prior, plan.RelativePath);
                        if (String.Equals(receiptFile.SourceHash,
                            "MISSING", StringComparison.Ordinal))
                        {
                            delete = true;
                            output = null;
                        }
                        else
                        {
                            delete = false;
                            output = File.ReadAllBytes(receiptFile.BackupPath);
                            plan.OutputHash = receiptFile.SourceHash;
                        }
                    }
                    changed.Add(plan);
                    if (delete)
                    {
                        if (File.Exists(plan.Path)) File.Delete(plan.Path);
                    }
                    else
                    {
                        WriteAtomic(plan.Path, output,
                            ModKey + (exactRestore
                                ? "-exact-restore" : "-adaptive"));
                    }
                    VerifyPlanOutput(plan, delete);
                }
                UpdateSharedAtlasState(
                    gamePath, backupRoot, sharedAtlasBaseline);
                foreach (FilePlan plan in plans)
                {
                    AdaptivePatchReceiptFile file =
                        AdaptivePatchSupport.FindReceiptFile(
                            prior, plan.RelativePath);
                    if (file == null)
                        throw new InvalidOperationException(
                            plan.DisplayName +
                            " is missing from the Raid Detector install receipt.");
                    file.OutputHash = plan.OutputHash;
                }
                prior.DefinitionVersion = DefinitionVersion;
                AdaptivePatchSupport.SaveReceipt(ModKey, prior);
                AdaptivePatchSupport.PruneUnreferencedBaseBackups(
                    ModKey, prior);
            }
            catch
            {
                foreach (FilePlan plan in changed)
                {
                    if (plan.SourceExists)
                        WriteAtomic(plan.Path, plan.SourceBytes,
                            ModKey + "-rollback");
                    else if (File.Exists(plan.Path))
                        File.Delete(plan.Path);
                }
                foreach (FilePlan plan in changed)
                {
                    if (plan.SourceExists && (!File.Exists(plan.Path) ||
                        !HashEquals(AdaptivePatchSupport.Sha256(plan.Path),
                            plan.SourceHash)))
                        throw new IOException(
                            "Raid Detector rollback could not restore " +
                            plan.DisplayName + ".");
                    if (!plan.SourceExists && File.Exists(plan.Path))
                        throw new IOException(
                            "Raid Detector rollback could not remove " +
                            plan.DisplayName + ".");
                }
                RestoreSnapshot(sharedStatePath, sharedStateExisted,
                    sharedStateBytes, ModKey + "-shared-state-rollback");
                RestoreSnapshot(sharedAtlasBaseline, baselineExisted,
                    baselineBytes, ModKey + "-atlas-baseline-rollback");
                throw;
            }
        }

        private static bool IsAtlasPlan(FilePlan plan)
        {
            return plan != null && String.Equals(
                plan.RelativePath, IconPngRelative,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void UpdateSharedAtlasState(
            string gamePath, string backupRoot, string baselinePath)
        {
            List<ScrapLabIconAtlasCoordinator.IconAsset> catalog =
                ScrapLabIconAtlasCoordinator.LoadCatalog();
            AtomicCustomPartPatchSupport.UpdateSharedAtlasState(
                gamePath, backupRoot, baselinePath, catalog);
        }

        private static bool CanExactRestore(
            List<FilePlan> plans, AdaptivePatchReceipt receipt)
        {
            if (receipt == null || receipt.Files == null)
                return false;
            foreach (FilePlan plan in plans)
            {
                if (IsAtlasPlan(plan)) continue;
                AdaptivePatchReceiptFile file =
                    AdaptivePatchSupport.FindReceiptFile(
                        receipt, plan.RelativePath);
                if (file == null || !plan.SourceExists ||
                    !HashEquals(plan.SourceHash, file.OutputHash))
                    return false;
                if (!String.Equals(file.SourceHash, "MISSING",
                    StringComparison.Ordinal) &&
                    (!File.Exists(file.BackupPath) ||
                     !HashEquals(AdaptivePatchSupport.Sha256(
                        file.BackupPath), file.SourceHash)))
                    return false;
            }
            return true;
        }

        private static void VerifyPlanOutput(FilePlan plan, bool deleted)
        {
            if (deleted)
            {
                if (File.Exists(plan.Path))
                    throw new IOException(
                        plan.DisplayName + " could not be removed.");
                return;
            }
            if (!File.Exists(plan.Path) ||
                !HashEquals(AdaptivePatchSupport.Sha256(plan.Path),
                    plan.OutputHash))
                throw new IOException(
                    plan.DisplayName + " failed final checksum verification.");
        }

        private static void WriteAtomic(
            string path, byte[] bytes, string operation)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                AdaptivePatchSupport.ReplaceFile(path, bytes, operation);
                return;
            }
            string temporary = path + ".scraplab-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }

        private static void RestoreSnapshot(
            string path, bool existed, byte[] bytes, string operation)
        {
            if (existed)
                WriteAtomic(path, bytes, operation);
            else
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
            }
        }

        private static void AddTextPlan(
            List<FilePlan> plans, TextState state, string output)
        {
            if (output == null)
                throw new InvalidDataException(
                    state.DisplayName + " has no verified output.");
            AddBinaryPlan(plans, state.RelativePath,
                state.DisplayName, state.Path,
                state.Document.OriginalBytes,
                state.Document.Render(output));
        }

        private static void AddBinaryPlan(
            List<FilePlan> plans, string relative, string display,
            string path, byte[] source, byte[] output)
        {
            plans.Add(new FilePlan
            {
                RelativePath = relative,
                DisplayName = display,
                Path = path,
                SourceExists = true,
                SourceBytes = source,
                OutputBytes = output,
                SourceHash = AdaptivePatchSupport.Sha256(source),
                OutputHash = AdaptivePatchSupport.Sha256(output)
            });
        }

        private static void AddOwnedPlan(
            List<FilePlan> plans, string relative, string display,
            string path, byte[] output, bool restoreAsMissing)
        {
            bool exists = File.Exists(path);
            byte[] source = exists ? File.ReadAllBytes(path) : null;
            plans.Add(new FilePlan
            {
                RelativePath = relative,
                DisplayName = display,
                Path = path,
                SourceExists = exists,
                ReceiptSourceMissing = restoreAsMissing,
                SourceBytes = source,
                OutputBytes = output,
                SourceHash = exists
                    ? AdaptivePatchSupport.Sha256(source) : "MISSING",
                OutputHash = AdaptivePatchSupport.Sha256(output)
            });
        }

        private static void AddDeletePlan(
            List<FilePlan> plans, string relative,
            string display, string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    display + " is missing.", path);
            byte[] source = File.ReadAllBytes(path);
            plans.Add(new FilePlan
            {
                RelativePath = relative,
                DisplayName = display,
                Path = path,
                SourceExists = true,
                SourceBytes = source,
                OutputBytes = null,
                SourceHash = AdaptivePatchSupport.Sha256(source),
                OutputHash = "MISSING"
            });
        }

        private static TextState FindText(
            List<TextState> texts, string relative)
        {
            foreach (TextState text in texts)
                if (String.Equals(text.RelativePath, relative,
                    StringComparison.OrdinalIgnoreCase)) return text;
            throw new InvalidOperationException(
                "Raid Detector text target was not prepared: " + relative);
        }

        private static string PatchShapesIndex(string text)
        {
            const string anchor =
                "\t\t\"$SURVIVAL_DATA/Objects/Database/ShapeSets/beacon.shapeset\",";
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
                "\tobj_interactive_beacon = sm.uuid.new( \"a5985971-1f95-4373-a5d9-4ce0a3e74851\" ),";
            return InsertAfterUnique(text, anchor,
                "\n\t-- SCRAPLAB PART: Raid Detector UUID.\n" +
                "\tobj_interactive_raid_detector = sm.uuid.new( \"" +
                PartUuid + "\" ),");
        }

        private static string UnpatchItems(string text)
        {
            return RemoveUnique(text,
                "\n\t-- SCRAPLAB PART: Raid Detector UUID.\n" +
                "\tobj_interactive_raid_detector = sm.uuid.new( \"" +
                PartUuid + "\" ),");
        }

        private static string TradeEntry
        {
            get
            {
                return "\t{\n" +
                    "\t\t\"itemId\": \"" + PartUuid + "\",\n" +
                    "\t\t\"quantity\": 1,\n" +
                    "\t\t\"craftTime\": 0,\n" +
                    "\t\t\"ingredientList\": [\n" +
                    "\t\t\t{\n" +
                    "\t\t\t\t\"quantity\": 4,\n" +
                    "\t\t\t\t\"itemId\": \"8d601982-4608-4d5e-bb9e-e4041486f7c7\"\n" +
                    "\t\t\t}\n" +
                    "\t\t]\n" +
                    "\t}";
            }
        }

        private static string PatchTrades(string text)
        {
            int end = text.LastIndexOf("\n]", StringComparison.Ordinal);
            if (end < 0 || AdaptivePatchSupport.Count(text, PartUuid) != 0)
                throw new InvalidDataException(
                    "The Hideout trade list ending is missing or conflicting.");
            return text.Substring(0, end) + ",\n" + TradeEntry +
                text.Substring(end);
        }

        private static string UnpatchTrades(string text)
        {
            return RemoveUnique(text, ",\n" + TradeEntry);
        }

        private static string PatchTrader(string text)
        {
            const string anchor = "\tITEMS.obj_seed_tomato\n}";
            return ReplaceUnique(text, anchor,
                "\tITEMS.obj_seed_tomato,\n" +
                "\t-- SCRAPLAB PART: Raid Detector trade.\n" +
                "\tITEMS.obj_interactive_raid_detector\n}");
        }

        private static string UnpatchTrader(string text)
        {
            return ReplaceUnique(text,
                "\tITEMS.obj_seed_tomato,\n" +
                "\t-- SCRAPLAB PART: Raid Detector trade.\n" +
                "\tITEMS.obj_interactive_raid_detector\n}",
                "\tITEMS.obj_seed_tomato\n}");
        }

        private static string PatchIconXml(string text, int x, int y)
        {
            const string anchor = "        </Group>";
            string entry =
                "            <!-- SCRAPLAB PART: Raid Detector icon. -->\n" +
                "            <Index name=\"" + PartUuid + "\">\n" +
                "                <Frame point=\"" + x + " " + y + "\"/>\n" +
                "            </Index>\n";
            return InsertBeforeUnique(text, anchor, entry);
        }

        private static string UnpatchIconXml(
            string text, int x, int y)
        {
            return RemoveUnique(text,
                "            <!-- SCRAPLAB PART: Raid Detector icon. -->\n" +
                "            <Index name=\"" + PartUuid + "\">\n" +
                "                <Frame point=\"" + x + " " + y + "\"/>\n" +
                "            </Index>\n");
        }

        private static string PatchLanguage(
            string text, string title, string description)
        {
            int end = text.LastIndexOf("\n}", StringComparison.Ordinal);
            if (end < 0 || AdaptivePatchSupport.Count(text, PartUuid) != 0)
                throw new InvalidDataException(
                    "The inventory-description object ending is missing or conflicting.");
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

        private static string InsertAfterUnique(
            string text, string anchor, string addition)
        {
            if (AdaptivePatchSupport.Count(text, anchor) != 1)
                throw new InvalidDataException(
                    "A protected Raid Detector insertion anchor changed.");
            return text.Replace(anchor, anchor + addition);
        }

        private static string InsertBeforeUnique(
            string text, string anchor, string addition)
        {
            if (AdaptivePatchSupport.Count(text, anchor) != 1)
                throw new InvalidDataException(
                    "A protected Raid Detector insertion anchor changed.");
            return text.Replace(anchor, addition + anchor);
        }

        private static string ReplaceUnique(
            string text, string before, string after)
        {
            if (AdaptivePatchSupport.Count(text, before) != 1)
                throw new InvalidDataException(
                    "A protected Raid Detector code snippet changed.");
            return text.Replace(before, after);
        }

        private static string RemoveUnique(string text, string value)
        {
            if (AdaptivePatchSupport.Count(text, value) != 1)
                throw new InvalidDataException(
                    "A Raid Detector patch snippet is missing, duplicated, or edited.");
            return text.Replace(value, "");
        }

        private static byte[] GetResource(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "The embedded Raid Detector asset is missing: " + name);
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
            if (File.Exists(state.ScriptPath) && BytesEqual(
                File.ReadAllBytes(state.ScriptPath), state.ScriptBytes))
                File.Delete(state.ScriptPath);
            else if (File.Exists(state.ScriptPath) && BytesEqual(
                File.ReadAllBytes(state.ScriptPath), state.LegacyScriptBytes))
                File.Delete(state.ScriptPath);
            if (File.Exists(state.ShapePath) && BytesEqual(
                File.ReadAllBytes(state.ShapePath), state.ShapeBytes))
                File.Delete(state.ShapePath);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static bool HashEquals(string left, string right)
        {
            return String.Equals(left, right,
                StringComparison.OrdinalIgnoreCase);
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
