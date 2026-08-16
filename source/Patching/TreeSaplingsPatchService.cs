using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal static class TreeSaplingsPatchService
    {
        private const string ModKey = "TreeSaplings";
        private const string DefinitionVersion = "33";
        private const string LegacyV31HeldFpFbxHash = "746697F56E1D66CF6B6A1D144B36D22FEEBD8D1A9BCA0CBE761DC53F05C040B9";
        private const string LegacyV31HeldFpDaeHash = "DC1EA19FB1EA3DFE8FF04EBEC14EC4D56C0B5A26D5F5B34451A68F69A276BC3F";
        private const string LegacyV31HeldFpRendHash = "CD0CD32572380EA84C4BC869B28E90E41264345EB9D98E8D729AC6D830EF08BF";
        private const string LegacyV31HeldTpFbxHash = "746697F56E1D66CF6B6A1D144B36D22FEEBD8D1A9BCA0CBE761DC53F05C040B9";
        private const string LegacyV31HeldTpDaeHash = "DC1EA19FB1EA3DFE8FF04EBEC14EC4D56C0B5A26D5F5B34451A68F69A276BC3F";
        private const string LegacyV31HeldTpRendHash = "93A63C960A14E532AED30F009FE388F82EC848A189EC6421A373D4E99CA4BFA7";
        private const string LegacyV30HeldFpFbxHash = "BCDD6DC0130C7F74265731118F0FD3F94211F4E8E1A44BAAB3AE3EA47DE39837";
        private const string LegacyV30HeldFpDaeHash = "8EA18B529D4ED3E1C92E1034558FDB3AB9AE2E00E1A4876741DCEF1F23F9A2EC";
        private const string LegacyV30HeldFpRendHash = "CD0CD32572380EA84C4BC869B28E90E41264345EB9D98E8D729AC6D830EF08BF";
        private const string LegacyV30HeldTpFbxHash = "0C22E9B66D0240504E38EB4D23B616F1FAD91FECF38F648E535CB26946254BB4";
        private const string LegacyV30HeldTpDaeHash = "12437DC998E4C749D734D11AF0B3DE4F5B830FF2D2194911CDF55AD53D00226C";
        private const string LegacyV30HeldTpRendHash = "93A63C960A14E532AED30F009FE388F82EC848A189EC6421A373D4E99CA4BFA7";
        private const string LegacyV29HeldFpFbxHash = "8B9A51FEAC6D31F1A435BE247E80EFE43FD0EEFB7181856AEA3A73D93360943F";
        private const string LegacyV29HeldFpDaeHash = "1FF95467099B5B88F3BEE95B6B6FE24D36E2973B74DCC2CBD1A44CC313C6370B";
        private const string LegacyV29HeldFpRendHash = "CD0CD32572380EA84C4BC869B28E90E41264345EB9D98E8D729AC6D830EF08BF";
        private const string LegacyV29HeldTpFbxHash = "8B9A51FEAC6D31F1A435BE247E80EFE43FD0EEFB7181856AEA3A73D93360943F";
        private const string LegacyV29HeldTpDaeHash = "1FF95467099B5B88F3BEE95B6B6FE24D36E2973B74DCC2CBD1A44CC313C6370B";
        private const string LegacyV29HeldTpRendHash = "93A63C960A14E532AED30F009FE388F82EC848A189EC6421A373D4E99CA4BFA7";
        private const string LegacyV28HeldFpFbxHash = "F4CF789886CF940BE922797C8C8EFF35EBA4B9A32CC669169AC55E7474AB5A04";
        private const string LegacyV28HeldFpDaeHash = "FEF7D585A9ACD4EF74059AEDD79F616CE5FEF9C847417178C9541ED1AB634107";
        private const string LegacyV28HeldFpRendHash = "CD0CD32572380EA84C4BC869B28E90E41264345EB9D98E8D729AC6D830EF08BF";
        private const string LegacyV28HeldTpFbxHash = "F4CF789886CF940BE922797C8C8EFF35EBA4B9A32CC669169AC55E7474AB5A04";
        private const string LegacyV28HeldTpDaeHash = "FEF7D585A9ACD4EF74059AEDD79F616CE5FEF9C847417178C9541ED1AB634107";
        private const string LegacyV28HeldTpRendHash = "93A63C960A14E532AED30F009FE388F82EC848A189EC6421A373D4E99CA4BFA7";
        private const string LegacyV27VisualHash = "50C39318C233D36A97272FA22F995572C911FB24AB1912E8462C47F35DEAFD1D";
        private const string LegacyV24VisualHash = "CFEA7B462468DCDC637DD5DCC3FD7716DB371A78AC71BEE9CCB31AD7DC275DC2";
        private const string LegacyV25HeldFbxHash = "E5542EFD3CC0EBD416FE3EA8FFEFFC77DDF92C1367438D190F1C7532D996E233";
        private const string LegacyV25HeldDaeHash = "80C49E838755DB94BEC3EF79172F45ED6EA75EDA8D106D7308896F66030BBC32";
        private const string LegacyV26HeldFbxHash = "726F3DE0B34EFD96AA68061C17F0D9EF54C68BCA6BA22379148FC07F39498ACD";
        private const string LegacyV26HeldDaeHash = "730F2BF9CB5DF8CBFA0F4E5C5234313DA025280BDD5B859777DDC3E0C6F931F9";
        private const string LegacyV1ToolHash = "79DDB1FD2D4A23E14DF2FD2B4BA67A3D049E4C55344A2CDF73106A4A2FB73900";
        private const string LegacyV1HarvestableSetHash = "27C2F71C270737B009F5FDC1704CD0012A326E38BED9EFE99BE80F20BD35A640";
        private const string LegacyV2ToolHash = "6C9E33E552F78B042F65BFB19F0AEEF1D464F846C831411059BB80F8B2D73EBC";
        private const string LegacyV2GrowthHash = "37AD2123FE4328A4F621054375CFC366A721E81E46DD49A3BB530394CDD699F2";
        private const string LegacyV2HarvestableSetHash = "DF8397CF9D15F7BEFAC891BA512E48028596E1AF8849DB091095032E31BC8706";
        private const string LegacyV3ToolHash = "BF6774065D5A43DD1F662ABB7635B4A7E43C48E2E0C54E33F606F043A948D715";
        private const string LegacyV3GrowthHash = "ADE8AF83CAC175338CC627C20B08486B019B4CF5122E353F2563533222DC36CE";
        private const string LegacyV3HarvestableSetHash = "4896E1CE8A9A868CEFE816364A1DC4066186488F563E4C9DFCDFE97722DB689C";
        private const string LegacyV4ToolHash = "BF3A88FB2601B6B726625452EEFBDEEB4234202D50767946D6A1707079AE3B35";
        private const string LegacyV4GrowthHash = "D7F6A69E8C8BCDFF4CD82A513C1B0D7D24AF7271E2D207BF20C19D353C2CE99C";
        private const string LegacyV4HarvestableSetHash = "57CB60B452DF55EAEBC50BC84BB4C1B7B8F6AEDF9770BEE325B243C65AFBB5DE";
        private const string LegacyV6ToolHash = "F2A09C7126C1F4E618976E4730BEF995AFF915894EEC061A7E87560210CF2FB4";
        private const string LegacyV7HeldRenderableHash = "1B10FDC0AD1F13AF81C9BCEA72F91ACE9335DEB674550A700C3E7BFF3D0ACD52";
        private const string LegacyV8HeldDaeHash = "6D156D854112AC64661BEDB0ADFEE35995B4EA75BE2B7EE8BFFCB8136D24D4B8";
        private const string LegacyV9HeldDaeHash = "90D85F144661DE859A77903BC6C9A478144E486C50FC793D89E8048B2F6AB10F";
        private const string LegacyV10HeldFbxHash = "9A3F04FA3D96A3D1CFE9F473E1BCB31B99A984B6E2EC075B04C271E91BB4EB80";
        private const string LegacyV10HeldDaeHash = "87CC15B6206701FFDE2DDE2FA6272BFAD22554B06473C3C94CC07A312A3850D9";
        private const string LegacyV11HeldFbxHash = "EA82DDC4C5654CC9E359FFFE226A342DED29BF25A8E8E71ACC6DA450FD47D076";
        private const string LegacyV11HeldDaeHash = "3CA5B81E524CD3678D3E0E557D813384BC225487C5FAE0BC3AACF8513525A105";
        private const string LegacyV12HeldFbxHash = "1D4075AB3605DB98A0D467F18C373FBB069CDE84E2B2627EB2AF3C4224B2B299";
        private const string LegacyV12HeldDaeHash = "E605D2AC334FDD932F58924B6141AB612806D0740098719584C45DE7D9F665B6";
        private const string LegacyV13HeldDaeHash = "8F07A384710C312E31AD0C41938A91C2A4A9804CA3C05AF6FC0C15A078753030";
        private const string LegacyV14HeldFbxHash = "235E7B60EA488C53AB6CD0B8507BB6CE0BA224D183A1AFFCEC83EFB43C931F17";
        private const string LegacyV14HeldDaeHash = "6D3E7B2336E48D36D8BD0EE4F405C0F004399A2EF5C6823E5283C2860A9C399B";
        private const string LegacyV15HeldFbxHash = "053E7B8C263511BF23838B2C75CA2779D206FE317C390005EBBAA277E4BB3390";
        private const string LegacyV15HeldDaeHash = "7A15C636D1B154F3129E8614338617D09C350D9707BA5E7D380D6CC5851A732F";
        private const string LegacyV16HeldFbxHash = "4BBC78967D17B7F62A69EFB0E4A9B8F957F9D9C8B372DFF945D5B3B04BD39075";
        private const string LegacyV16HeldDaeHash = "635C99503588228C075BACF9FE243547536D25BBBE419862AD8D8BF87B8F5CD3";
        private const string LegacyV17ToolHash = "ABE5EC5A12BDD140E4AC9DC1CCDB884816BD2BB2207CFD0ECDE00CAC572618CB";
        private const string LegacyV17HeldRenderableHash = "7AE48A5766A6F607C388AB2A7ED79AEDC1275E7AACA004F372C9C6E823EEB333";
        private const string LegacyV17HeldFbxHash = "1CC0AC3063DA20BC4F344034D9D7E107BA8A6F79E7F8788CAC4C74028BF00B2F";
        private const string LegacyV18HeldFbxHash = "C257EFA58043D87B45732851FB1FCA4285E28C55801EC2003F2C223C6A6C9949";
        private const string LegacyV18HeldDaeHash = "2284BC60303BD2CF9959DA3ABAA41E0D5451AAF8ECE1E18D3E56E5380FED68CF";
        private const string LegacyV19HeldFbxHash = "35E84A6382DE058D470A6ADDD33DEB090C5C3C8C4434F1F11089FF7BBFB600BB";
        private const string LegacyV19HeldDaeHash = "CF81AF444977DFE1E9C208D063F340927C8D3906969A4FDC83776A3E584492ED";
        private const string LegacyV19HeldRenderableHash = "4BD2D610F76914C55B988EB00668FF6575BF1CA822404469041650C0B3F06D1F";
        private const string LegacyV20HeldDaeHash = "C3B5A43B73F34CE73977D386D1BD6892BDDC0B11E5523C7D4A3B24EF30756A7F";
        private const string LegacyV20HeldRenderableHash = "852247B17F4C90DF8A3D1ECD113B8890D3F01ACAA5A4328DF0A9275F8DB41DF6";
        private const string LegacyV21HeldFbxHash = "B9A03ADCDB7624B36E9F7D7EA497882DAC8F0C078AC495C48383C4A5595D3271";
        private const string LegacyV21HeldDaeHash = "81AD7E4722D532D691BC2EE50A1270EFE0C94B0260AA3B41946DF3EE5EAC5B1E";
        private const string LegacyV22HeldFbxHash = "962EA32ECC3C3DCB5C5A717311D7BBDBA9F0FEFFB62D3EB98226C8856710D686";
        private const string LegacyV22HeldDaeHash = "69C0F0ACDF18848CF344082C9896C054DA7BD522CA12D889547F6825E898F6EF";
        private const string LegacyV23HeldFbxHash = "746697F56E1D66CF6B6A1D144B36D22FEEBD8D1A9BCA0CBE761DC53F05C040B9";
        private const string LegacyV23HeldDaeHash = "DC1EA19FB1EA3DFE8FF04EBEC14EC4D56C0B5A26D5F5B34451A68F69A276BC3F";
        internal const string VerifiedSteamBuildId = "24529696";
        internal const string VerifiedGameVersion = "1.0.5.876";
        internal static readonly string[] ItemUuids = {
            "790d34b8-f006-47e4-9ebc-49a84a68ed16",
            "33511c78-354b-4a60-af6b-778c427c47d5",
            "c9413781-5a0e-4025-a2cb-bc2090803e50" };
        private static readonly string[] HarvestableUuids = {
            "26427da8-4848-4f18-9786-2f75db6fd772",
            "99a01345-f1e6-404e-b669-7e6d805bae3a",
            "e5e3d28f-bdff-4862-92b9-7fc9ce688643" };
        private const string ShapesSetPath = "$SURVIVAL_DATA/Objects/Database/ShapeSets/ScrapLab/Parts/TreeSaplings.shapeset";
        private const string ToolsSetPath = "$SURVIVAL_DATA/Tools/ToolSets/ScrapLab/TreeSaplings.tools.json";
        private const string HarvestablesSetPath = "$SURVIVAL_DATA/Harvestables/Database/HarvestableSets/ScrapLab/TreeSaplings.harvestableset";
        private const string ResourcePrefix = "RaidRescue.Parts.TreeSaplings.";

        private static readonly string ShapesIndexRelative = Path.Combine("Survival", "Objects", "Database", "shapesets.json");
        private static readonly string ToolsIndexRelative = Path.Combine("Survival", "Tools", "toolsets.json");
        private static readonly string HarvestablesIndexRelative = Path.Combine("Survival", "Harvestables", "Database", "harvestablesets.json");
        private static readonly string ItemsRelative = Path.Combine("Survival", "Scripts", "game", "survival_items.lua");
        private static readonly string HarvestableIdsRelative = Path.Combine("Survival", "Scripts", "game", "survival_harvestable.lua");
        private static readonly string TreeTrunkRelative = Path.Combine("Survival", "Scripts", "game", "harvestable", "TreeTrunk.lua");
        private static readonly string TradesRelative = Path.Combine("Survival", "CraftingRecipes", "hideout.json");
        private static readonly string TraderRelative = Path.Combine("Survival", "Scripts", "game", "interactables", "HideoutTrader.lua");
        private static readonly string IconXmlRelative = Path.Combine("Survival", "Gui", "IconMapSurvival.xml");
        private static readonly string IconPngRelative = Path.Combine("Survival", "Gui", "IconMapSurvival.png");

        private static readonly string[,] Languages = {
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

        private sealed class LocalizedEntry { public string title { get; set; } public string description { get; set; } }
        private sealed class TextState
        {
            public string RelativePath, DisplayName, Path, PatchedText, CleanText;
            public LuaTextDocument Document;
            public bool Clean, Installed, Known, IsIconXml, LegacyInstalled;
        }
        private sealed class OwnedAsset
        {
            public string RelativePath, DisplayName, Path, ResourceName;
            public byte[] Bytes;
            public bool Missing, Exact, LegacyExact, AdditiveAsset;
        }
        private sealed class ProbeState
        {
            public readonly List<TextState> Texts = new List<TextState>();
            public readonly List<OwnedAsset> Owned = new List<OwnedAsset>();
            public string AtlasPath, AtlasHash;
            public byte[] AtlasBytes;
            public List<ScrapLabIconAtlasCoordinator.IconAsset> IconCatalog;
            public ScrapLabIconAtlasCoordinator.CatalogPlan CatalogPlan;
            public bool AtlasClean, AtlasInstalled, AtlasKnown, OwnedClean, OwnedInstalled, DefinitionUpdateAvailable;
            public bool AllClean, AllInstalled, AllKnownClean, OrphanedOwnedAssets;
        }

        public static GamePatchResult GetStatus()
        {
            try { string path = GamePatchService.FindGameInstall(); return String.IsNullOrEmpty(path) ? Failure("Scrap Mechanic was not found.") : GetStatusAt(path); }
            catch (Exception exception) { return Failure(exception.Message); }
        }

        internal static GamePatchResult GetStatusAt(string gamePath)
        {
            GamePatchResult result = NewResult(gamePath, false);
            try
            {
                SteamBuildInfo build = ReadBuild(gamePath, result);
                ProbeState state = Probe(gamePath);
                result.Success = true;
                if (state.AllInstalled)
                {
                    result.Installed = true; result.AlreadyPatched = true;
                    result.NeedsUpdate = state.DefinitionUpdateAvailable;
                    AdaptivePatchSupport.FillResult(result, build,
                        state.DefinitionUpdateAvailable ? PatchCompatibilityState.DefinitionUpdate :
                            state.AllKnownClean ? PatchCompatibilityState.KnownInstalled : PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true,
                        state.DefinitionUpdateAvailable
                            ? "A verified Tree Saplings dual-view held-tool update is ready."
                            : "Tree Saplings items, growth scripts, trades, drops, languages, and icons are intact.");
                }
                else if (state.AllClean)
                {
                    string reason; bool canApply = CanApplyClean(state, build, out reason);
                    if (AdaptivePatchSupport.HasReceiptOrSupersededState(ModKey) && canApply)
                        AdaptivePatchSupport.FillResult(result, build, "REINSTALL REQUIRED - SAPLINGS AT RISK", true, true,
                            "Steam removed Tree Saplings registrations. Reinstall before loading worlds that contain sapling items or planted pots.");
                    else
                        AdaptivePatchSupport.FillResult(result, build,
                            state.AllKnownClean ? PatchCompatibilityState.KnownClean : canApply ? PatchCompatibilityState.CompatibleUpdate : PatchCompatibilityState.OtherModification,
                            !state.AllKnownClean, canApply, reason);
                }
                else AdaptivePatchSupport.FillResult(result, build, PatchCompatibilityState.PartialConflict, false, false,
                    "A Tree Saplings registration, owned file, trade, tree-drop hook, language entry, or icon is missing, duplicated, or edited.");
                return result;
            }
            catch (Exception exception) { result.Error = exception.Message; return result; }
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning()) return Failure("Scrap Mechanic is running. Close the game completely before changing Tree Saplings.");
            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath)) return Failure("Scrap Mechanic was not found.");
            GamePatchResult result = SetEnabledAt(gamePath, ProductPaths.LocalDataPath("Game Backups", "Scrap Mechanic", "Secret Mods"), enabled);
            if (result != null && result.FilesPatched > 0)
            {
                GameScriptCacheInvalidator.QueueMeshCachePrefix(
                    result, "TreeSaplingHeld");
                GameScriptCacheInvalidator.QueueMeshCachePrefix(
                    result, "TreeSaplingHeldFp");
                GameScriptCacheInvalidator.QueueMeshCachePrefix(
                    result, "TreeSaplingHeldTp");
            }
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(string gamePath, string backupRoot, bool enabled)
        {
            GamePatchResult result = NewResult(gamePath, enabled);
            try
            {
                SteamBuildInfo build = ReadBuild(gamePath, result);
                ProbeState state = Probe(gamePath);
                if (enabled && state.AllInstalled && state.DefinitionUpdateAvailable)
                {
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(gamePath, backupRoot, state.IconCatalog);
                    ApplyDefinitionUpdate(BuildDefinitionUpdatePlans(state), result, gamePath, backupRoot, build);
                    result.Success = true; result.Installed = true; result.NeedsUpdate = false;
                    result.Changes.Add("Updated Tree Saplings with independent first-person and third-person held-tool profiles, then invalidated both compiled mesh caches.");
                    AdaptivePatchSupport.FillResult(result, build, PatchCompatibilityState.AdaptiveInstalled,
                        !state.AllKnownClean, true, "Tree Saplings definition 33 was installed and verified.");
                    SecretModBackupRetention.Prune(backupRoot, ModKey, result.BackupPath, result);
                    return result;
                }
                if (enabled && state.AllInstalled)
                {
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(gamePath, backupRoot, state.IconCatalog);
                    result.Success = true; result.Installed = true; result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build, PatchCompatibilityState.AdaptiveInstalled, !state.AllKnownClean, true, "Tree Saplings is already installed.");
                    return result;
                }
                if (!enabled && state.AllClean)
                {
                    CleanupOwnedFiles(state); AdaptivePatchSupport.DeleteReceipt(ModKey); AdaptivePatchSupport.DeleteBuildActivation(ModKey);
                    result.Success = true; result.Installed = false; result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(result, build, PatchCompatibilityState.KnownClean, !state.AllKnownClean, true, "Tree Saplings is already removed.");
                    return result;
                }
                bool retired = false;
                if (enabled)
                {
                    if (!state.AllClean) throw new InvalidOperationException("Tree Saplings cannot be installed because its protected state is partial or conflicting.");
                    string reason; if (!CanApplyClean(state, build, out reason)) throw new InvalidOperationException("Tree Saplings cannot be installed: " + reason);
                    retired = AdaptivePatchSupport.RetireVerifiedSupersededReceipt(ModKey, "Steam Verify removed Tree Saplings registrations while leaving its old receipt behind.");
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(gamePath, backupRoot, state.IconCatalog);
                }
                else
                {
                    if (!state.AllInstalled) throw new InvalidOperationException("Tree Saplings cannot be removed because protected code, assets, or icons were edited.");
                    AtomicCustomPartPatchSupport.PrepareSharedAtlasState(gamePath, backupRoot, state.IconCatalog);
                }
                List<AtomicCustomPartFilePlan> plans = enabled ? BuildInstallPlans(state) : BuildRemovePlans(state);
                AtomicCustomPartPatchSupport.Apply(ModKey, "Tree Saplings", DefinitionVersion, plans, result, gamePath, backupRoot, build, enabled, state.IconCatalog);
                result.Success = true; result.Installed = enabled;
                result.Changes.Add(enabled ? "Installed Small, Medium, and Large Tree Saplings with protected placement, growth, fertilizing, uprooting, native-tree conversion, drops, and Hideout trades." : "Removed every Tree Saplings registration and owned file while leaving fully grown native trees safe.");
                if (retired) result.Changes.Add("Retired the Steam-overwritten Tree Saplings receipt before creating a fresh verified uninstall state.");
                AdaptivePatchSupport.FillResult(result, build,
                    enabled ? (state.AllKnownClean ? PatchCompatibilityState.KnownInstalled : PatchCompatibilityState.AdaptiveInstalled) : (state.AllKnownClean ? PatchCompatibilityState.KnownClean : PatchCompatibilityState.CompatibleUpdate),
                    !state.AllKnownClean, true, enabled ? "Tree Saplings was installed and verified." : "Tree Saplings was removed and verified.");
                AdaptivePatchSupport.QueueBuildActivation(result, ModKey, enabled);
                SecretModBackupRetention.Prune(backupRoot, ModKey, result.BackupPath, result);
                return result;
            }
            catch (Exception exception) { result.Success = false; result.Error = exception.Message; return result; }
        }

        private static ProbeState Probe(string gamePath)
        {
            ProbeState s = new ProbeState();
            s.Texts.Add(ReadText(gamePath, ShapesIndexRelative, "shapesets.json", "FF30F988FCDF775604AA54E1AF3E97CBCC4AE45F7EDCAB7B528694933D7E2511", ShapesSetPath, PatchShapes, UnpatchShapes, false));
            s.Texts.Add(ReadText(gamePath, ToolsIndexRelative, "toolsets.json", "681162D0E66527ADC866545A531B069F5DF631B7DD73360D48A8D87736AD4BA4", ToolsSetPath, PatchTools, UnpatchTools, false));
            s.Texts.Add(ReadText(gamePath, HarvestablesIndexRelative, "harvestablesets.json", "1B229630C5ED4563C8573F53118B00B178356818AB44C3B47BB083EE46A72F4A", HarvestablesSetPath, PatchHarvestableIndex, UnpatchHarvestableIndex, false));
            s.Texts.Add(ReadText(gamePath, ItemsRelative, "survival_items.lua", "ACDAD2CF9163655F87796D996A58DDE381AC1221B1337AEF049E38066B199789", ItemUuids[0], PatchItems, UnpatchItems, false));
            s.Texts.Add(ReadText(gamePath, HarvestableIdsRelative, "survival_harvestable.lua", "372DBE3693F305E80A36D044EE4398F35ECD062CAFD603ADCE6287588B2F46E3", HarvestableUuids[0], PatchHarvestableIds, UnpatchHarvestableIds, false));
            TextState treeTrunk = ReadText(gamePath, TreeTrunkRelative, "TreeTrunk.lua", "1B8F388A568C30E77A5C5C16D3309CC8085CEC94AA3CE01D0C83305A772A6ED8", "-- SCRAPLAB MOD: Tree Sapling crown drop.", PatchTreeTrunk, UnpatchTreeTrunk, false);
            treeTrunk.LegacyInstalled = treeTrunk.Installed &&
                AdaptivePatchSupport.Count(treeTrunk.Document.NormalizedText, "math.random() <= 0.15") == 1;
            s.Texts.Add(treeTrunk);
            s.Texts.Add(ReadText(gamePath, TradesRelative, "hideout.json", "69E355B255975BA9AD3F20DB7FD568F1A57AC21D92DF14618C4A558383015068", ItemUuids[0], PatchTrades, UnpatchTrades, false));
            s.Texts.Add(ReadText(gamePath, TraderRelative, "HideoutTrader.lua", "6C5EB46FB1E7C950E365E98413D5BA24F5642A90BD3B6D5186E884DEE2AEE7E6", "-- SCRAPLAB PART: Tree Saplings trades.", PatchTrader, UnpatchTrader, false));
            s.Texts.Add(ReadText(gamePath, IconXmlRelative, "IconMapSurvival.xml", "5DA34EF427C912BDF64BD1993834A78DBD86F11DFF16FD63B61F3FA9C1ECDDDB", ItemUuids[0], delegate(string t) { return t; }, delegate(string t) { return t; }, true));
            Dictionary<string, List<LocalizedEntry>> loc = LoadLocalization();
            for (int i = 0; i < Languages.GetLength(0); i++)
            {
                string language = Languages[i, 0];
                s.Texts.Add(ReadText(gamePath, Path.Combine("Survival", "Gui", "Language", language, "inventoryDescriptions.json"), language + " inventory descriptions", Languages[i, 1], ItemUuids[0],
                    delegate(string t) { return PatchLanguage(t, loc[language]); }, delegate(string t) { return UnpatchLanguage(t, loc[language]); }, false));
            }
            TextState xml = FindText(s, IconXmlRelative);
            s.AtlasPath = Path.Combine(gamePath, IconPngRelative); if (!File.Exists(s.AtlasPath)) throw new FileNotFoundException("IconMapSurvival.png was not found.", s.AtlasPath);
            s.AtlasBytes = File.ReadAllBytes(s.AtlasPath); s.AtlasHash = AdaptivePatchSupport.Sha256(s.AtlasBytes); s.IconCatalog = ScrapLabIconAtlasCoordinator.LoadCatalog();
            s.AtlasInstalled = true; s.AtlasClean = true;
            foreach (string uuid in ItemUuids)
            {
                byte[] icon = ScrapLabIconAtlasCoordinator.FindCatalogIcon(s.IconCatalog, uuid).Bytes;
                ScrapLabIconAtlasCoordinator.AtlasInfo info = ScrapLabIconAtlasCoordinator.Inspect(xml.Document.NormalizedText, s.AtlasBytes, icon, uuid);
                s.AtlasInstalled &= info.EntryPresent && info.IconPresent;
                // Shared ScrapLab catalog tiles are intentionally preloaded by
                // any custom-part mod. An absent XML registration is the clean
                // state even when the managed tile is already present.
                s.AtlasClean &= !info.EntryPresent;
            }
            try { s.CatalogPlan = ScrapLabIconAtlasCoordinator.EnsureCatalog(xml.Document.NormalizedText, s.AtlasBytes, s.IconCatalog); }
            catch { if (!s.AtlasInstalled) s.AtlasClean = false; }
            ScrapLabIconAtlasCoordinator.SharedAtlasReceipt receipt = ScrapLabIconAtlasCoordinator.LoadReceipt(AdaptivePatchSupport.GetSharedStatePath("ScrapLab-Icon-Pack.json"));
            s.AtlasKnown = String.Equals(s.AtlasHash, "4288CAA081C8674E8D69640C717802C3883E1AA53181C6A9ABA86BBCFE7D9146", StringComparison.OrdinalIgnoreCase) || String.Equals(s.AtlasHash, "C33A5A5DE6E7B11B7F9319BA928383E5DDF02E78C35BBCF25CA789AEF627A4D5", StringComparison.OrdinalIgnoreCase) || ScrapLabIconAtlasCoordinator.IsTrustedReceipt(receipt, s.AtlasHash, s.IconCatalog);
            if (receipt != null && String.Equals(receipt.IconXmlHash, xml.Document.OriginalHash, StringComparison.OrdinalIgnoreCase)) xml.Known = true;
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeld.fbx"), "Tree Sapling held mesh", ResourcePrefix + "TreeSaplingHeld.fbx", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeld.dae"), "Tree Sapling skinned held mesh", ResourcePrefix + "TreeSaplingHeld.dae", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeld.rend"), "Tree Sapling held renderable", ResourcePrefix + "TreeSaplingHeld.rend");
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeldFp.fbx"), "Tree Sapling first-person held mesh", ResourcePrefix + "TreeSaplingHeldFp.fbx", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeldFp.dae"), "Tree Sapling first-person skinned mesh", ResourcePrefix + "TreeSaplingHeldFp.dae", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeldFp.rend"), "Tree Sapling first-person renderable", ResourcePrefix + "TreeSaplingHeldFp.rend", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeldTp.fbx"), "Tree Sapling third-person held mesh", ResourcePrefix + "TreeSaplingHeldTp.fbx", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeldTp.dae"), "Tree Sapling third-person skinned mesh", ResourcePrefix + "TreeSaplingHeldTp.dae", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Character", "Char_Tools", "ScrapLab", "TreeSaplings", "TreeSaplingHeldTp.rend"), "Tree Sapling third-person renderable", ResourcePrefix + "TreeSaplingHeldTp.rend", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Scripts", "ScrapLab", "Parts", "TreeSaplings", "TreeSaplingVisual.generated.lua"), "Tree Sapling held visual setup", ResourcePrefix + "TreeSaplingVisual.generated.lua", true);
            AddOwned(s, gamePath, Path.Combine("Survival", "Scripts", "ScrapLab", "Parts", "TreeSaplings", "TreeSaplingTool.lua"), "Tree Sapling placement tools", ResourcePrefix + "TreeSaplingTool.lua");
            AddOwned(s, gamePath, Path.Combine("Survival", "Scripts", "ScrapLab", "Parts", "TreeSaplings", "TreeSaplingHarvestable.lua"), "Tree Sapling growth script", ResourcePrefix + "TreeSaplingHarvestable.lua");
            AddOwned(s, gamePath, Path.Combine("Survival", "Objects", "Database", "ShapeSets", "ScrapLab", "Parts", "TreeSaplings.shapeset"), "Tree Saplings shape set", ResourcePrefix + "TreeSaplings.shapeset");
            AddOwned(s, gamePath, Path.Combine("Survival", "Tools", "ToolSets", "ScrapLab", "TreeSaplings.tools.json"), "Tree Saplings tool set", ResourcePrefix + "TreeSaplings.tools.json");
            AddOwned(s, gamePath, Path.Combine("Survival", "Harvestables", "Database", "HarvestableSets", "ScrapLab", "TreeSaplings.harvestableset"), "Tree Saplings harvestable set", ResourcePrefix + "TreeSaplings.harvestableset");
            AddOwned(s, gamePath, Path.Combine("Survival", "Harvestables", "Collision", "ScrapLab", "TreeSaplings", "TreeSaplingPotCollision.obj"), "Tree Sapling pot interaction collision", ResourcePrefix + "TreeSaplingPotCollision.obj", true);
            s.OwnedClean = true;
            bool coreInstalled = true;
            bool addedAssetsInstalled = true;
            bool anyLegacyOwned = false;
            foreach (OwnedAsset owned in s.Owned)
            {
                s.OwnedClean &= owned.Missing;
                if (owned.AdditiveAsset)
                {
                    addedAssetsInstalled &= owned.Exact || owned.LegacyExact;
                    anyLegacyOwned |= owned.LegacyExact;
                }
                else
                {
                    coreInstalled &= owned.Exact || owned.LegacyExact;
                    anyLegacyOwned |= owned.LegacyExact;
                }
            }
            bool canAddAdditiveAssets = coreInstalled && anyLegacyOwned;
            if (canAddAdditiveAssets)
            {
                // Recompute the complete additive set. Missing files are valid
                // only for a verified older definition; an existing modified
                // file must never be hidden by a different missing file.
                addedAssetsInstalled = true;
                foreach (OwnedAsset owned in s.Owned)
                    if (owned.AdditiveAsset)
                    {
                        if (owned.Missing) anyLegacyOwned = true;
                        else addedAssetsInstalled &= owned.Exact || owned.LegacyExact;
                    }
            }
            s.OwnedInstalled = coreInstalled && addedAssetsInstalled;
            bool legacyTextInstalled = false;
            foreach (TextState text in s.Texts) legacyTextInstalled |= text.LegacyInstalled;
            s.DefinitionUpdateAvailable = s.OwnedInstalled && (anyLegacyOwned || legacyTextInstalled);
            bool textsClean = true, textsInstalled = true, known = s.AtlasKnown;
            foreach (TextState text in s.Texts) { if (text.IsIconXml) { textsClean &= s.AtlasClean; textsInstalled &= s.AtlasInstalled; } else { textsClean &= text.Clean; textsInstalled &= text.Installed; } known &= text.Known; }
            s.OrphanedOwnedAssets = textsClean && s.OwnedInstalled;
            s.AllClean = textsClean && (s.OwnedClean || s.OwnedInstalled);
            s.AllInstalled = textsInstalled && s.OwnedInstalled && s.AtlasInstalled;
            s.AllKnownClean = known;
            return s;
        }

        private static TextState ReadText(string gamePath, string relative, string display, string knownHash, string marker, Func<string, string> patch, Func<string, string> unpatch, bool icon)
        {
            string path = Path.Combine(gamePath, relative); if (!File.Exists(path)) throw new FileNotFoundException(display + " was not found.", path);
            LuaTextDocument doc = AdaptivePatchSupport.ReadLua(path); AdaptivePatchSupport.RequireAdaptiveFormat(doc, display);
            int count = AdaptivePatchSupport.Count(doc.NormalizedText, marker);
            TextState s = new TextState { RelativePath = relative, DisplayName = display, Path = path, Document = doc, IsIconXml = icon,
                Known = String.Equals(doc.OriginalHash, knownHash, StringComparison.OrdinalIgnoreCase) || IsTrustedExistingOutput(relative, doc.OriginalHash) };
            if (icon) { s.Clean = count == 0; s.Installed = count == 1; return s; }
            if (count == 0) { s.PatchedText = patch(doc.NormalizedText); s.Clean = true; }
            else if (count == 1) { s.CleanText = unpatch(doc.NormalizedText); s.PatchedText = patch(s.CleanText); s.Installed = AdaptivePatchSupport.Count(s.CleanText, marker) == 0; }
            return s;
        }

        private static bool CanApplyClean(ProbeState state, SteamBuildInfo build, out string reason)
        {
            if (state.AllKnownClean) { reason = "Verified Steam build 24529696 Tree Saplings targets."; return true; }
            if (build != null && build.Valid && String.Equals(build.BuildId, VerifiedSteamBuildId, StringComparison.Ordinal) && String.Equals(build.GameVersion, VerifiedGameVersion, StringComparison.Ordinal)) { reason = "A protected Tree Saplings target differs from the verified current Steam build."; return false; }
            List<string> unknown = new List<string>(); foreach (TextState t in state.Texts) if (!t.Known) unknown.Add(t.Path); if (!state.AtlasKnown) unknown.Add(state.AtlasPath);
            return AdaptivePatchSupport.CanAdaptCleanFiles(build, unknown, out reason);
        }

        private static List<AtomicCustomPartFilePlan> BuildInstallPlans(ProbeState state)
        {
            List<AtomicCustomPartFilePlan> plans = new List<AtomicCustomPartFilePlan>(); foreach (TextState t in state.Texts) if (!t.IsIconXml) AddTextPlan(plans, t, t.PatchedText);
            TextState xml = FindText(state, IconXmlRelative); ScrapLabIconAtlasCoordinator.CatalogPlan catalog = state.CatalogPlan ?? ScrapLabIconAtlasCoordinator.EnsureCatalog(xml.Document.NormalizedText, state.AtlasBytes, state.IconCatalog);
            string xmlOutput = xml.Document.NormalizedText; foreach (string uuid in ItemUuids) { ScrapLabIconAtlasCoordinator.IconPlacement p = catalog.Placements[uuid]; xmlOutput = PatchIconXml(xmlOutput, uuid, p.X, p.Y); }
            AddTextPlan(plans, xml, xmlOutput); if (catalog.AtlasChanged) AddBinaryPlan(plans, IconPngRelative, "IconMapSurvival.png", state.AtlasPath, state.AtlasBytes, catalog.AtlasBytes, true, false);
            foreach (OwnedAsset o in state.Owned) AddOwnedPlan(plans, o, o.Bytes, state.OrphanedOwnedAssets); return plans;
        }

        private static List<AtomicCustomPartFilePlan> BuildRemovePlans(ProbeState state)
        {
            List<AtomicCustomPartFilePlan> plans = new List<AtomicCustomPartFilePlan>(); foreach (TextState t in state.Texts) if (!t.IsIconXml) AddTextPlan(plans, t, t.CleanText);
            TextState xml = FindText(state, IconXmlRelative); string xmlOutput = xml.Document.NormalizedText;
            foreach (string uuid in ItemUuids) { int x, y; if (!ScrapLabIconAtlasCoordinator.TryGetEntry(xmlOutput, uuid, out x, out y)) throw new InvalidDataException("A Tree Saplings icon registration is missing."); xmlOutput = UnpatchIconXml(xmlOutput, uuid, x, y); }
            AddTextPlan(plans, xml, xmlOutput); byte[] atlasOutput = ScrapLabIconAtlasCoordinator.RemoveCatalogWhenUnused(xmlOutput, state.AtlasBytes, state.IconCatalog, AtomicCustomPartPatchSupport.ReadActiveAtlasBaseline());
            if (!BytesEqual(atlasOutput, state.AtlasBytes)) AddBinaryPlan(plans, IconPngRelative, "IconMapSurvival.png", state.AtlasPath, state.AtlasBytes, atlasOutput, true, false);
            foreach (OwnedAsset o in state.Owned) AddOwnedPlan(plans, o, null, false); return plans;
        }

        private static string PatchShapes(string t) { return InsertAfterUnique(t, "\t\t\"$SURVIVAL_DATA/Objects/Database/ShapeSets/plantables.shapeset\",", "\n\t\t\"" + ShapesSetPath + "\","); }
        private static string UnpatchShapes(string t) { return RemoveUnique(t, "\n\t\t\"" + ShapesSetPath + "\","); }
        private static string PatchTools(string t) { return ReplaceUnique(t, "\t\t\"$SURVIVAL_DATA/Tools/ToolSets/carry.json\"\n\t]", "\t\t\"$SURVIVAL_DATA/Tools/ToolSets/carry.json\",\n\t\t\"" + ToolsSetPath + "\"\n\t]"); }
        private static string UnpatchTools(string t) { return ReplaceUnique(t, "\t\t\"$SURVIVAL_DATA/Tools/ToolSets/carry.json\",\n\t\t\"" + ToolsSetPath + "\"\n\t]", "\t\t\"$SURVIVAL_DATA/Tools/ToolSets/carry.json\"\n\t]"); }
        private static string HarvestableSetEntry { get { return "\t\t{\n\t\t\t\"categories\" : [ \"HVS/Plantables\" ],\n\t\t\t\"name\" : \"" + HarvestablesSetPath + "\"\n\t\t}"; } }
        private static string PatchHarvestableIndex(string t) { int end = t.LastIndexOf("\n\t]", StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("The harvestable set list ending changed."); return t.Substring(0, end) + ",\n" + HarvestableSetEntry + t.Substring(end); }
        private static string UnpatchHarvestableIndex(string t) { return RemoveUnique(t, ",\n" + HarvestableSetEntry); }
        private static string ItemBlock { get { return "\n\t-- SCRAPLAB PART: Tree Sapling item UUIDs.\n\tobj_scraplab_tree_sapling_small = sm.uuid.new( \"" + ItemUuids[0] + "\" ),\n\tobj_scraplab_tree_sapling_medium = sm.uuid.new( \"" + ItemUuids[1] + "\" ),\n\tobj_scraplab_tree_sapling_large = sm.uuid.new( \"" + ItemUuids[2] + "\" ),"; } }
        private static string PatchItems(string t) { return InsertAfterUnique(t, "\tobj_consumable_soilbag = sm.uuid.new( \"9a3e478c-2224-44fa-887c-239965bd05ad\" ),", ItemBlock); }
        private static string UnpatchItems(string t) { return RemoveUnique(t, ItemBlock); }
        private static string HarvestableBlock { get { return "\n-- SCRAPLAB PART: Planted Tree Saplings.\nhvs_scraplab_tree_sapling_small = sm.uuid.new( \"" + HarvestableUuids[0] + "\" )\nhvs_scraplab_tree_sapling_medium = sm.uuid.new( \"" + HarvestableUuids[1] + "\" )\nhvs_scraplab_tree_sapling_large = sm.uuid.new( \"" + HarvestableUuids[2] + "\" )\n"; } }
        private static string PatchHarvestableIds(string t) { return InsertBeforeUnique(t, "\n-- Farmables", HarvestableBlock); }
        private static string UnpatchHarvestableIds(string t) { return RemoveUnique(t, HarvestableBlock); }
        private static string TreeHelperForChance(string chance) { return "\n-- SCRAPLAB MOD: Tree Sapling crown drop.\nlocal ScrapLabTreeSaplingDrops = { small = sm.uuid.new( \"" + ItemUuids[0] + "\" ), medium = sm.uuid.new( \"" + ItemUuids[1] + "\" ), large = sm.uuid.new( \"" + ItemUuids[2] + "\" ) }\n\nfunction TreeTrunk.sv_scrapLabMarkFallen( self )\n\tif self.sv.fallen then return end\n\tself.sv.fallen = true\n\tif self.sv.crown and self.data and ScrapLabTreeSaplingDrops[self.data.treeType] and math.random() <= " + chance + " then\n\t\tSpawnLoot( self.shape, { { uuid = ScrapLabTreeSaplingDrops[self.data.treeType], quantity = 1 } }, self.shape.worldPosition + sm.vec3.new( 0, 0, 0.5 ) )\n\tend\n\tself.network:setClientData( { fallen = self.sv.fallen } )\nend\n"; }
        private static string TreeHelper { get { return TreeHelperForChance("0.30"); } }
        private static string LegacyTreeHelper { get { return TreeHelperForChance("0.15"); } }
        private const string FallenBlockTwo = "\t\tself.sv.fallen = true\n\t\tself.network:setClientData( { fallen = self.sv.fallen } )";
        private const string FallenBlockThree = "\t\t\tself.sv.fallen = true\n\t\t\tself.network:setClientData( { fallen = self.sv.fallen } )";
        private static string PatchTreeTrunk(string t) { t = InsertAfterUnique(t, "dofile( \"$SURVIVAL_DATA/Scripts/game/survival_constants.lua\" )", "\ndofile( \"$SURVIVAL_DATA/Scripts/game/survival_loot.lua\" )"); t = InsertBeforeUnique(t, "\nfunction TreeTrunk.server_onFixedUpdate", TreeHelper); RequireCount(t, FallenBlockTwo, 2); RequireCount(t, FallenBlockThree, 2); t = t.Replace(FallenBlockTwo, "\t\tself:sv_scrapLabMarkFallen()"); return t.Replace(FallenBlockThree, "\t\t\tself:sv_scrapLabMarkFallen()"); }
        private static string UnpatchTreeTrunk(string t) { RequireCount(t, "\t\t\tself:sv_scrapLabMarkFallen()", 2); t = t.Replace("\t\t\tself:sv_scrapLabMarkFallen()", FallenBlockThree); RequireCount(t, "\t\tself:sv_scrapLabMarkFallen()", 2); t = t.Replace("\t\tself:sv_scrapLabMarkFallen()", FallenBlockTwo); int current = AdaptivePatchSupport.Count(t, TreeHelper); int legacy = AdaptivePatchSupport.Count(t, LegacyTreeHelper); if (current + legacy != 1) throw new InvalidDataException("The Tree Saplings crown-drop helper is missing, duplicated, or edited."); t = RemoveUnique(t, current == 1 ? TreeHelper : LegacyTreeHelper); return RemoveUnique(t, "\ndofile( \"$SURVIVAL_DATA/Scripts/game/survival_loot.lua\" )"); }
        private static string TradeEntry(string uuid, int cost) { return "\t{\n\t\t\"itemId\": \"" + uuid + "\",\n\t\t\"quantity\": 5,\n\t\t\"craftTime\": 0,\n\t\t\"ingredientList\": [\n\t\t\t{\n\t\t\t\t\"quantity\": " + cost + ",\n\t\t\t\t\"itemId\": \"8d601982-4608-4d5e-bb9e-e4041486f7c7\"\n\t\t\t}\n\t\t]\n\t}"; }
        private static string TradeBlock { get { return TradeEntry(ItemUuids[0], 1) + ",\n" + TradeEntry(ItemUuids[1], 2) + ",\n" + TradeEntry(ItemUuids[2], 3); } }
        private static string PatchTrades(string t) { int end = t.LastIndexOf("\n]", StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("The Hideout trade list ending changed."); return t.Substring(0, end) + ",\n" + TradeBlock + t.Substring(end); }
        private static string UnpatchTrades(string t) { return RemoveUnique(t, ",\n" + TradeBlock); }
        private static string TraderBlock { get { return "\n\t-- SCRAPLAB PART: Tree Saplings trades.\n\tITEMS.obj_scraplab_tree_sapling_small,\n\tITEMS.obj_scraplab_tree_sapling_medium,\n\tITEMS.obj_scraplab_tree_sapling_large,"; } }
        private static string PatchTrader(string t) { return InsertAfterUnique(t, "\tITEMS.obj_consumable_soilbag,", TraderBlock); }
        private static string UnpatchTrader(string t) { return RemoveUnique(t, TraderBlock); }
        private static string PatchIconXml(string t, string uuid, int x, int y) { return InsertBeforeUnique(t, "        </Group>", "            <!-- SCRAPLAB PART: Tree Sapling icon. -->\n            <Index name=\"" + uuid + "\">\n                <Frame point=\"" + x + " " + y + "\"/>\n            </Index>\n"); }
        private static string UnpatchIconXml(string t, string uuid, int x, int y) { return RemoveUnique(t, "            <!-- SCRAPLAB PART: Tree Sapling icon. -->\n            <Index name=\"" + uuid + "\">\n                <Frame point=\"" + x + " " + y + "\"/>\n            </Index>\n"); }
        private static string LanguageEntry(string uuid, LocalizedEntry e) { return "\t\"" + uuid + "\": {\n\t\t\"description\": \"" + JsonEscape(e.description) + "\",\n\t\t\"title\": \"" + JsonEscape(e.title) + "\",\n\t\t\"upperCaseTitle\": \"" + JsonEscape(e.title.ToUpperInvariant()) + "\"\n\t}"; }
        private static string LanguageBlock(List<LocalizedEntry> entries) { return LanguageEntry(ItemUuids[0], entries[0]) + ",\n" + LanguageEntry(ItemUuids[1], entries[1]) + ",\n" + LanguageEntry(ItemUuids[2], entries[2]); }
        private static string PatchLanguage(string t, List<LocalizedEntry> e) { int end = t.LastIndexOf("\n}", StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("Inventory descriptions ending changed."); return t.Substring(0, end) + ",\n" + LanguageBlock(e) + t.Substring(end); }
        private static string UnpatchLanguage(string t, List<LocalizedEntry> e) { return RemoveUnique(t, ",\n" + LanguageBlock(e)); }

        private static void AddOwned(ProbeState s, string gamePath, string relative, string display, string resource, bool additiveAsset = false)
        {
            byte[] bytes = GetResource(resource);
            string path = Path.Combine(gamePath, relative);
            bool missing = !File.Exists(path);
            byte[] current = missing ? null : File.ReadAllBytes(path);
            bool exact = !missing && BytesEqual(current, bytes);
            s.Owned.Add(new OwnedAsset
            {
                RelativePath = relative,
                DisplayName = display,
                Path = path,
                ResourceName = resource,
                Bytes = bytes,
                Missing = missing,
                Exact = exact,
                AdditiveAsset = additiveAsset,
                LegacyExact = !missing && !exact && IsLegacyOwnedHash(relative, AdaptivePatchSupport.Sha256(current))
            });
        }

        private static bool IsLegacyOwnedHash(string relative, string hash)
        {
            string file = Path.GetFileName(relative);
            if (String.Equals(file, "TreeSaplingHeldFp.fbx", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV31HeldFpFbxHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV30HeldFpFbxHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV29HeldFpFbxHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV28HeldFpFbxHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeldFp.dae", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV31HeldFpDaeHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV30HeldFpDaeHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV29HeldFpDaeHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV28HeldFpDaeHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeldFp.rend", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV31HeldFpRendHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV30HeldFpRendHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV29HeldFpRendHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV28HeldFpRendHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeldTp.fbx", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV31HeldTpFbxHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV30HeldTpFbxHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV29HeldTpFbxHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV28HeldTpFbxHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeldTp.dae", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV31HeldTpDaeHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV30HeldTpDaeHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV29HeldTpDaeHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV28HeldTpDaeHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeldTp.rend", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV31HeldTpRendHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV30HeldTpRendHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV29HeldTpRendHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV28HeldTpRendHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingTool.lua", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV1ToolHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV2ToolHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV3ToolHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV4ToolHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV6ToolHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV17ToolHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHarvestable.lua", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV2GrowthHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV3GrowthHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV4GrowthHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplings.harvestableset", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV1HarvestableSetHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV2HarvestableSetHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV3HarvestableSetHash, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV4HarvestableSetHash, StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeld.rend", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV7HeldRenderableHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV17HeldRenderableHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV19HeldRenderableHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV20HeldRenderableHash,
                        StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeld.fbx", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV10HeldFbxHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV11HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV12HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV14HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV15HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV16HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV17HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV18HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV19HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV21HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV22HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV23HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV25HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV26HeldFbxHash,
                        StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingHeld.dae", StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV8HeldDaeHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV9HeldDaeHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV10HeldDaeHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV11HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV12HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV13HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV14HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV15HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV16HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV18HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV19HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV20HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV21HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV22HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV23HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV25HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV26HeldDaeHash,
                        StringComparison.OrdinalIgnoreCase);
            if (String.Equals(file, "TreeSaplingVisual.generated.lua",
                StringComparison.OrdinalIgnoreCase))
                return String.Equals(hash, LegacyV24VisualHash,
                    StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(hash, LegacyV27VisualHash,
                        StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static List<AtomicCustomPartFilePlan> BuildDefinitionUpdatePlans(ProbeState state)
        {
            if (state == null || !state.DefinitionUpdateAvailable)
                throw new InvalidOperationException("The Tree Saplings definition update is not available.");
            List<AtomicCustomPartFilePlan> plans = new List<AtomicCustomPartFilePlan>();
            foreach (OwnedAsset owned in state.Owned)
                if (owned.LegacyExact) AddOwnedPlan(plans, owned, owned.Bytes, false);
                else if (owned.AdditiveAsset && owned.Missing)
                    AddOwnedPlan(plans, owned, owned.Bytes, true);
            foreach (TextState text in state.Texts)
                if (text.LegacyInstalled) AddTextPlan(plans, text, text.PatchedText);
            if (plans.Count == 0)
                throw new InvalidOperationException("No verified Tree Saplings definition targets were found.");
            return plans;
        }

        private static void ApplyDefinitionUpdate(List<AtomicCustomPartFilePlan> plans,
            GamePatchResult result, string gamePath, string backupRoot, SteamBuildInfo build)
        {
            AdaptivePatchReceipt receipt = AdaptivePatchSupport.LoadReceipt(ModKey);
            if (receipt == null || receipt.Files == null)
                throw new InvalidOperationException("The original Tree Saplings receipt is missing, so its placement files cannot be updated safely.");
            foreach (AtomicCustomPartFilePlan plan in plans)
            {
                AdaptivePatchReceiptFile file = AdaptivePatchSupport.FindReceiptFile(receipt, plan.RelativePath);
                if (plan.ReceiptSourceMissing && String.Equals(plan.SourceHash, "MISSING", StringComparison.Ordinal))
                {
                    if (file != null)
                        throw new InvalidOperationException(plan.DisplayName + " has an unexpected existing install receipt.");
                }
                else if (file == null || !String.Equals(file.OutputHash, plan.SourceHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(plan.DisplayName + " no longer matches its verified install receipt.");
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(backupRoot, "Update-" + ModKey + "-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            List<AdaptivePatchReceiptFile> manifest = new List<AdaptivePatchReceiptFile>();
            foreach (AtomicCustomPartFilePlan plan in plans)
            {
                if (plan.SourceExists)
                {
                    string backup = Path.Combine(backupPath, plan.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.WriteAllBytes(backup, plan.SourceBytes);
                    if (!String.Equals(AdaptivePatchSupport.Sha256(backup), plan.SourceHash, StringComparison.OrdinalIgnoreCase))
                        throw new IOException(plan.DisplayName + " update backup failed checksum verification.");
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
            AdaptivePatchSupport.WriteBackupManifest(backupPath, "Tree Saplings",
                "Gameplay Definition Update", gamePath, build, DefinitionVersion, manifest);

            List<AtomicCustomPartFilePlan> changed = new List<AtomicCustomPartFilePlan>();
            try
            {
                foreach (AtomicCustomPartFilePlan plan in plans)
                {
                    WriteDefinitionOutput(plan, ModKey + "-definition-update");
                    changed.Add(plan);
                    if (!File.Exists(plan.Path) || !String.Equals(AdaptivePatchSupport.Sha256(plan.Path), plan.OutputHash, StringComparison.OrdinalIgnoreCase))
                        throw new IOException(plan.DisplayName + " failed final update verification.");
                }
                foreach (AtomicCustomPartFilePlan plan in plans)
                {
                    AdaptivePatchReceiptFile file = AdaptivePatchSupport.FindReceiptFile(receipt, plan.RelativePath);
                    if (file == null)
                    {
                        file = new AdaptivePatchReceiptFile
                        {
                            RelativePath = plan.RelativePath,
                            SourceHash = "MISSING",
                            BackupPath = "",
                            Newline = "PRESERVED",
                            HasBom = false
                        };
                        receipt.Files.Add(file);
                    }
                    file.OutputHash = plan.OutputHash;
                }
                receipt.DefinitionVersion = DefinitionVersion;
                AdaptivePatchSupport.SaveReceipt(ModKey, receipt);
            }
            catch
            {
                for (int index = changed.Count - 1; index >= 0; index--)
                {
                    AtomicCustomPartFilePlan plan = changed[index];
                    if (plan.SourceExists)
                        AdaptivePatchSupport.ReplaceFile(plan.Path, plan.SourceBytes, ModKey + "-definition-update-rollback");
                    else if (File.Exists(plan.Path)) File.Delete(plan.Path);
                }
                foreach (AtomicCustomPartFilePlan plan in changed)
                {
                    bool restored = plan.SourceExists
                        ? File.Exists(plan.Path) && String.Equals(AdaptivePatchSupport.Sha256(plan.Path), plan.SourceHash, StringComparison.OrdinalIgnoreCase)
                        : !File.Exists(plan.Path);
                    if (!restored)
                        throw new IOException("Tree Saplings update rollback could not restore " + plan.DisplayName + ".");
                }
                throw;
            }
            result.FilesPatched = plans.Count;
        }
        private static void WriteDefinitionOutput(AtomicCustomPartFilePlan plan, string operation)
        {
            if (plan.SourceExists)
            {
                AdaptivePatchSupport.ReplaceFile(plan.Path, plan.OutputBytes, operation);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(plan.Path));
            string temporary = plan.Path + ".scraplab-" + operation + "-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, plan.OutputBytes);
                File.Move(temporary, plan.Path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void AddTextPlan(List<AtomicCustomPartFilePlan> p, TextState s, string output) { if (output == null) throw new InvalidDataException(s.DisplayName + " has no verified output."); AddBinaryPlan(p, s.RelativePath, s.DisplayName, s.Path, s.Document.OriginalBytes, s.Document.Render(output), false, false); }
        private static void AddOwnedPlan(List<AtomicCustomPartFilePlan> p, OwnedAsset o, byte[] output, bool restoreAsMissing) { byte[] source = File.Exists(o.Path) ? File.ReadAllBytes(o.Path) : null; AtomicCustomPartFilePlan plan = AddBinaryPlan(p, o.RelativePath, o.DisplayName, o.Path, source, output, false, output == null); plan.ReceiptSourceMissing = restoreAsMissing; }
        private static AtomicCustomPartFilePlan AddBinaryPlan(List<AtomicCustomPartFilePlan> p, string relative, string display, string path, byte[] source, byte[] output, bool atlas, bool forceDelete) { AtomicCustomPartFilePlan plan = new AtomicCustomPartFilePlan { RelativePath = relative, DisplayName = display, Path = path, SourceExists = source != null, SourceBytes = source, OutputBytes = output, SourceHash = source == null ? "MISSING" : AdaptivePatchSupport.Sha256(source), OutputHash = output == null ? "MISSING" : AdaptivePatchSupport.Sha256(output), IsAtlas = atlas, ForceDeleteOnRemove = forceDelete }; p.Add(plan); return plan; }
        private static Dictionary<string, List<LocalizedEntry>> LoadLocalization() { Dictionary<string, List<LocalizedEntry>> result = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Deserialize<Dictionary<string, List<LocalizedEntry>>>(Encoding.UTF8.GetString(GetResource(ResourcePrefix + "TreeSaplings.localization.json"))); if (result == null || result.Count != 11) throw new InvalidDataException("The embedded Tree Saplings localization catalog is incomplete."); foreach (List<LocalizedEntry> e in result.Values) if (e == null || e.Count != 3) throw new InvalidDataException("Every Tree Saplings language needs three entries."); return result; }
        private static byte[] GetResource(string name) { using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) { if (s == null) throw new InvalidOperationException("The embedded Tree Saplings asset is missing: " + name); using (MemoryStream m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); } } }
        private static TextState FindText(ProbeState s, string relative) { foreach (TextState t in s.Texts) if (String.Equals(t.RelativePath, relative, StringComparison.OrdinalIgnoreCase)) return t; throw new InvalidOperationException("A prepared Tree Saplings target is missing: " + relative); }
        private static bool IsTrustedExistingOutput(string relative, string hash) { string[] keys = { "RaidDetector", "WirelessVacuumPipe", "NetworkStorageChest", "BetterPlasmaDrills", "FullSpeedCarrying", "BetterFreezerBeehive", "BetterEngines", "ResourceLocator", "ChemicalFertilizerSplash", "DualFluidCannon", "DeveloperCommands", "RevivalBuffRecovery" }; foreach (string key in keys) { AdaptivePatchReceiptFile f = AdaptivePatchSupport.FindReceiptFile(AdaptivePatchSupport.LoadReceipt(key), relative); if (f != null && String.Equals(f.OutputHash, hash, StringComparison.OrdinalIgnoreCase)) return true; } return false; }
        internal static bool IsTrustedOutput(string relative, string hash) { AdaptivePatchReceiptFile f = AdaptivePatchSupport.FindReceiptFile(AdaptivePatchSupport.LoadReceipt(ModKey), relative); return f != null && String.Equals(f.OutputHash, hash, StringComparison.OrdinalIgnoreCase); }
        internal static bool HasIntactSharedPatch(string relative, string text)
        {
            try
            {
                string clean;
                if (String.Equals(relative, ShapesIndexRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchShapes(text);
                else if (String.Equals(relative, ToolsIndexRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchTools(text);
                else if (String.Equals(relative, HarvestablesIndexRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchHarvestableIndex(text);
                else if (String.Equals(relative, ItemsRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchItems(text);
                else if (String.Equals(relative, HarvestableIdsRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchHarvestableIds(text);
                else if (String.Equals(relative, TradesRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchTrades(text);
                else if (String.Equals(relative, TraderRelative, StringComparison.OrdinalIgnoreCase)) clean = UnpatchTrader(text);
                else if (String.Equals(relative, IconXmlRelative, StringComparison.OrdinalIgnoreCase))
                {
                    clean = text;
                    int[] xs = new int[ItemUuids.Length]; int[] ys = new int[ItemUuids.Length];
                    for (int i = 0; i < ItemUuids.Length; i++)
                    {
                        if (!ScrapLabIconAtlasCoordinator.TryGetEntry(clean, ItemUuids[i], out xs[i], out ys[i])) return false;
                        clean = UnpatchIconXml(clean, ItemUuids[i], xs[i], ys[i]);
                    }
                    string rebuilt = clean;
                    for (int i = 0; i < ItemUuids.Length; i++) rebuilt = PatchIconXml(rebuilt, ItemUuids[i], xs[i], ys[i]);
                    return String.Equals(rebuilt, text, StringComparison.Ordinal);
                }
                else if (relative.EndsWith("inventoryDescriptions.json", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string uuid in ItemUuids)
                    {
                        if (AdaptivePatchSupport.Count(text, "\"" + uuid + "\": {") != 1) return false;
                    }
                    return true;
                }
                else return false;
                foreach (string uuid in ItemUuids) if (AdaptivePatchSupport.Count(clean, uuid) != 0) return false;
                return true;
            }
            catch (InvalidDataException) { return false; }
        }
        private static SteamBuildInfo ReadBuild(string gamePath, GamePatchResult result) { string exe = Path.Combine(gamePath, "Release", "ScrapMechanic.exe"); if (!File.Exists(exe)) throw new FileNotFoundException("ScrapMechanic.exe was not found.", exe); result.GameVersion = FileVersionInfo.GetVersionInfo(exe).FileVersion; return AdaptivePatchSupport.GetSteamBuild(gamePath, result.GameVersion); }
        private static void CleanupOwnedFiles(ProbeState s) { foreach (OwnedAsset o in s.Owned) if (File.Exists(o.Path) && BytesEqual(File.ReadAllBytes(o.Path), o.Bytes)) File.Delete(o.Path); }
        private static string InsertAfterUnique(string t, string a, string b) { RequireCount(t, a, 1); return t.Replace(a, a + b); }
        private static string InsertBeforeUnique(string t, string a, string b) { RequireCount(t, a, 1); return t.Replace(a, b + a); }
        private static string ReplaceUnique(string t, string a, string b) { RequireCount(t, a, 1); return t.Replace(a, b); }
        private static string RemoveUnique(string t, string a) { RequireCount(t, a, 1); return t.Replace(a, ""); }
        private static void RequireCount(string t, string a, int n) { int count = AdaptivePatchSupport.Count(t, a); if (count != n) throw new InvalidDataException("A protected Tree Saplings snippet changed or appears " + count + " times."); }
        private static string JsonEscape(string v) { return (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
        private static bool BytesEqual(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0; }
        private static GamePatchResult NewResult(string path, bool installed) { return new GamePatchResult { GamePath = path, Installed = installed, Changes = new List<string>() }; }
        private static GamePatchResult Failure(string e) { return new GamePatchResult { Success = false, Error = e, Changes = new List<string>() }; }
    }
}
