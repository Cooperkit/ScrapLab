using System;
using System.Collections.Generic;
using System.IO;

namespace RaidRescue
{
    internal sealed class AtomicCustomPartFilePlan
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
        public bool ForceDeleteOnRemove;
        public bool IsAtlas;
    }

    internal static class AtomicCustomPartPatchSupport
    {
        internal static Action<string, string> PlanWriteCompletedForTest;

        internal static void Apply(
            string modKey, string displayName, string definitionVersion,
            List<AtomicCustomPartFilePlan> plans, GamePatchResult result,
            string gamePath, string backupRoot, SteamBuildInfo build,
            bool enabled,
            IList<ScrapLabIconAtlasCoordinator.IconAsset> iconCatalog)
        {
            AdaptivePatchReceipt prior =
                AdaptivePatchSupport.LoadReceipt(modKey);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(backupRoot,
                (enabled ? "Install-" : "Remove-") + modKey + "-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            string atlasBaseline =
                AdaptivePatchSupport.GetActiveSharedAtlasBaselinePath();
            string sharedStatePath =
                AdaptivePatchSupport.GetSharedStatePath(
                    "ScrapLab-Icon-Pack.json");
            bool baselineExisted = File.Exists(atlasBaseline);
            byte[] baselineBytes = baselineExisted
                ? File.ReadAllBytes(atlasBaseline) : null;
            bool sharedStateExisted = File.Exists(sharedStatePath);
            byte[] sharedStateBytes = sharedStateExisted
                ? File.ReadAllBytes(sharedStatePath) : null;
            List<AdaptivePatchReceiptFile> manifest =
                new List<AdaptivePatchReceiptFile>();

            foreach (AtomicCustomPartFilePlan plan in plans)
            {
                plan.BackupFile = plan.IsAtlas
                    ? atlasBaseline
                    : Path.Combine(backupPath, plan.RelativePath);
                if (plan.SourceExists && !plan.ReceiptSourceMissing)
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(plan.BackupFile));
                    if (!plan.IsAtlas)
                    {
                        File.WriteAllBytes(plan.BackupFile, plan.SourceBytes);
                        RequireHash(plan.BackupFile, plan.SourceHash,
                            plan.DisplayName + " backup");
                    }
                    else if (enabled && !File.Exists(plan.BackupFile))
                    {
                        if (ScrapLabIconAtlasCoordinator.
                            ContainsAnyCatalogPixels(plan.SourceBytes,
                                iconCatalog))
                            throw new InvalidOperationException(
                                "The shared ScrapLab icon baseline is missing while managed icon pixels are already present.");
                        WriteAtomic(plan.BackupFile, plan.SourceBytes,
                            modKey + "-atlas-baseline");
                        RequireHash(plan.BackupFile, plan.SourceHash,
                            "Shared icon atlas baseline");
                    }
                }
                manifest.Add(new AdaptivePatchReceiptFile
                {
                    RelativePath = plan.RelativePath,
                    SourceHash = plan.ReceiptSourceMissing
                        ? "MISSING" : plan.SourceHash,
                    OutputHash = plan.OutputHash,
                    Newline = "PRESERVED",
                    HasBom = false
                });
            }

            AdaptivePatchSupport.WriteBackupManifest(backupPath,
                displayName, enabled ? "Install" : "Remove", gamePath,
                build, definitionVersion, manifest);

            bool exactRestore = !enabled &&
                CanExactRestore(plans, prior);
            List<AtomicCustomPartFilePlan> changed =
                new List<AtomicCustomPartFilePlan>();
            try
            {
                foreach (AtomicCustomPartFilePlan plan in plans)
                {
                    byte[] output = plan.OutputBytes;
                    bool delete = output == null;
                    string expectedHash = plan.OutputHash;
                    if (exactRestore && !plan.IsAtlas &&
                        !plan.ForceDeleteOnRemove)
                    {
                        AdaptivePatchReceiptFile priorFile =
                            AdaptivePatchSupport.FindReceiptFile(
                                prior, plan.RelativePath);
                        if (String.Equals(priorFile.SourceHash, "MISSING",
                            StringComparison.Ordinal))
                        {
                            delete = true;
                            output = null;
                            expectedHash = "MISSING";
                        }
                        else
                        {
                            delete = false;
                            output = File.ReadAllBytes(priorFile.BackupPath);
                            expectedHash = priorFile.SourceHash;
                        }
                    }

                    bool noChange = !delete && plan.SourceExists &&
                        String.Equals(plan.SourceHash, expectedHash,
                            StringComparison.OrdinalIgnoreCase);
                    if (!noChange)
                    {
                        changed.Add(plan);
                        if (delete)
                        {
                            if (File.Exists(plan.Path)) File.Delete(plan.Path);
                        }
                        else
                        {
                            WriteAtomic(plan.Path, output,
                                modKey + (exactRestore
                                    ? "-exact-restore" : "-adaptive"));
                        }
                        Action<string, string> hook =
                            PlanWriteCompletedForTest;
                        if (hook != null)
                            hook(plan.Path, enabled ? "Install" : "Remove");
                    }
                    Verify(plan.Path, delete, expectedHash,
                        plan.DisplayName);
                }
                UpdateSharedAtlasState(gamePath, backupRoot,
                    atlasBaseline, iconCatalog);
                if (enabled)
                    SaveInstallReceipt(modKey, definitionVersion,
                        plans, result, build);
                else
                {
                    AdaptivePatchSupport.DeleteReceipt(modKey);
                    AdaptivePatchSupport.DeleteBuildActivation(modKey);
                }
            }
            catch
            {
                for (int index = changed.Count - 1; index >= 0; index--)
                {
                    AtomicCustomPartFilePlan plan = changed[index];
                    if (plan.SourceExists)
                        WriteAtomic(plan.Path, plan.SourceBytes,
                            modKey + "-rollback");
                    else if (File.Exists(plan.Path)) File.Delete(plan.Path);
                }
                foreach (AtomicCustomPartFilePlan plan in changed)
                    Verify(plan.Path, !plan.SourceExists,
                        plan.SourceExists ? plan.SourceHash : "MISSING",
                        plan.DisplayName + " rollback");
                RestoreSnapshot(sharedStatePath, sharedStateExisted,
                    sharedStateBytes, modKey + "-shared-state-rollback");
                RestoreSnapshot(atlasBaseline, baselineExisted,
                    baselineBytes, modKey + "-atlas-baseline-rollback");
                if (prior != null)
                {
                    AdaptivePatchSupport.SaveReceipt(modKey, prior);
                    AdaptivePatchSupport.PruneUnreferencedBaseBackups(
                        modKey, prior);
                }
                else AdaptivePatchSupport.
                    DeleteActiveReceiptPreservingSuperseded(modKey);
                throw;
            }

            result.FilesPatched = changed.Count;
        }

        private static void SaveInstallReceipt(
            string modKey, string definitionVersion,
            IList<AtomicCustomPartFilePlan> plans,
            GamePatchResult result, SteamBuildInfo build)
        {
            AdaptivePatchReceipt receipt = new AdaptivePatchReceipt
            {
                ModKey = modKey,
                DefinitionVersion = definitionVersion,
                SteamBuildId = build == null ? "" : build.BuildId,
                GameVersion = result.GameVersion,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                Files = new List<AdaptivePatchReceiptFile>()
            };
            foreach (AtomicCustomPartFilePlan plan in plans)
            {
                string basePath = "";
                if (plan.SourceExists && !plan.ReceiptSourceMissing)
                    basePath = plan.IsAtlas
                        ? plan.BackupFile
                        : AdaptivePatchSupport.CaptureBaseBackup(
                            modKey, plan.RelativePath, plan.BackupFile,
                            plan.SourceHash);
                receipt.Files.Add(new AdaptivePatchReceiptFile
                {
                    RelativePath = plan.RelativePath,
                    SourceHash = plan.ReceiptSourceMissing
                        ? "MISSING" : plan.SourceExists
                        ? plan.SourceHash : "MISSING",
                    OutputHash = plan.OutputHash,
                    BackupPath = plan.ReceiptSourceMissing ? "" : basePath,
                    Newline = "PRESERVED",
                    HasBom = false
                });
            }
            AdaptivePatchSupport.SaveReceipt(modKey, receipt);
            AdaptivePatchSupport.PruneUnreferencedBaseBackups(
                modKey, receipt);
        }

        private static bool CanExactRestore(
            IList<AtomicCustomPartFilePlan> plans,
            AdaptivePatchReceipt receipt)
        {
            if (receipt == null || receipt.Files == null) return false;
            foreach (AtomicCustomPartFilePlan plan in plans)
            {
                if (plan.IsAtlas) continue;
                AdaptivePatchReceiptFile file =
                    AdaptivePatchSupport.FindReceiptFile(
                        receipt, plan.RelativePath);
                if (file == null || !plan.SourceExists ||
                    !String.Equals(plan.SourceHash, file.OutputHash,
                        StringComparison.OrdinalIgnoreCase)) return false;
                if (!String.Equals(file.SourceHash, "MISSING",
                    StringComparison.Ordinal) &&
                    (!File.Exists(file.BackupPath) ||
                     !String.Equals(AdaptivePatchSupport.Sha256(
                        file.BackupPath), file.SourceHash,
                        StringComparison.OrdinalIgnoreCase))) return false;
            }
            return true;
        }

        internal static void PrepareSharedAtlasState(
            string gamePath, string backupRoot,
            IList<ScrapLabIconAtlasCoordinator.IconAsset> catalog)
        {
            string xmlPath = Path.Combine(gamePath, "Survival", "Gui",
                "IconMapSurvival.xml");
            string atlasPath = Path.Combine(gamePath, "Survival", "Gui",
                "IconMapSurvival.png");
            LuaTextDocument xml = AdaptivePatchSupport.ReadLua(xmlPath);
            string statePath = AdaptivePatchSupport.GetSharedStatePath(
                "ScrapLab-Icon-Pack.json");
            string activeBaseline =
                AdaptivePatchSupport.GetActiveSharedAtlasBaselinePath();
            string legacyDirectory = Path.Combine(backupRoot,
                "ScrapLab-Shared-Icon-Atlas");
            string legacyBaseline = Path.Combine(legacyDirectory,
                "IconMapSurvival.baseline.png");
            string legacyMirror = Path.Combine(legacyDirectory,
                "atlas-receipt.json");

            if (!ScrapLabIconAtlasCoordinator.AnyCatalogRegistration(
                xml.NormalizedText, catalog))
            {
                TryDelete(statePath);
                TryDelete(activeBaseline);
                TryDelete(legacyMirror);
                TryDelete(legacyBaseline);
                TryDeleteEmptyDirectory(legacyDirectory);
                return;
            }

            if (!File.Exists(atlasPath))
                throw new FileNotFoundException(
                    "IconMapSurvival.png was not found.", atlasPath);
            byte[] atlas = File.ReadAllBytes(atlasPath);
            ScrapLabIconAtlasCoordinator.SharedAtlasReceipt receipt =
                ScrapLabIconAtlasCoordinator.LoadReceipt(statePath) ??
                ScrapLabIconAtlasCoordinator.LoadReceipt(legacyMirror);
            byte[] baseline = ReadVerifiedBaseline(
                activeBaseline, receipt);
            if (baseline == null && receipt != null)
                baseline = ReadVerifiedBaseline(
                    receipt.BaselinePath, receipt);
            if (baseline == null)
                baseline = ReadVerifiedBaseline(
                    legacyBaseline, receipt);
            if (baseline == null)
            {
                // The live registrations and icon pixels are the authority.
                // Reconstruct only the managed transparent tiles instead of
                // making a stale or corrupt backup block every future mod.
                baseline = ScrapLabIconAtlasCoordinator.
                    RemoveCatalogWhenUnused("", atlas, catalog, null);
                if (ScrapLabIconAtlasCoordinator.ContainsAnyCatalogPixels(
                    baseline, catalog))
                    throw new InvalidDataException(
                        "The shared ScrapLab icon baseline could not be reconstructed safely.");
            }

            WriteAtomic(activeBaseline, baseline,
                "shared-atlas-active-baseline");
            ScrapLabIconAtlasCoordinator.CatalogPlan live =
                ScrapLabIconAtlasCoordinator.EnsureCatalog(
                    xml.NormalizedText, atlas, catalog);
            if (!live.AtlasChanged)
            {
                ScrapLabIconAtlasCoordinator.SharedAtlasReceipt rebuilt =
                    ScrapLabIconAtlasCoordinator.CreateReceipt(
                        xml.NormalizedText, atlas, baseline,
                        activeBaseline, xml.OriginalHash, catalog);
                WriteAtomic(statePath,
                    ScrapLabIconAtlasCoordinator.SerializeReceipt(rebuilt),
                    "shared-atlas-active-receipt");
            }
            TryDelete(legacyMirror);
            TryDelete(legacyBaseline);
            TryDeleteEmptyDirectory(legacyDirectory);
        }

        internal static byte[] ReadActiveAtlasBaseline()
        {
            string path =
                AdaptivePatchSupport.GetActiveSharedAtlasBaselinePath();
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        internal static void UpdateSharedAtlasState(
            string gamePath, string backupRoot, string baselinePath,
            IList<ScrapLabIconAtlasCoordinator.IconAsset> catalog)
        {
            string xmlPath = Path.Combine(gamePath, "Survival", "Gui",
                "IconMapSurvival.xml");
            string atlasPath = Path.Combine(gamePath, "Survival", "Gui",
                "IconMapSurvival.png");
            LuaTextDocument xml = AdaptivePatchSupport.ReadLua(xmlPath);
            string statePath = AdaptivePatchSupport.GetSharedStatePath(
                "ScrapLab-Icon-Pack.json");
            string legacyDirectory = Path.Combine(backupRoot,
                "ScrapLab-Shared-Icon-Atlas");
            string legacyMirror = Path.Combine(
                legacyDirectory, "atlas-receipt.json");
            string legacyBaseline = Path.Combine(
                legacyDirectory, "IconMapSurvival.baseline.png");
            if (!ScrapLabIconAtlasCoordinator.AnyCatalogRegistration(
                xml.NormalizedText, catalog))
            {
                TryDelete(statePath);
                TryDelete(baselinePath);
                TryDelete(legacyMirror);
                TryDelete(legacyBaseline);
                TryDeleteEmptyDirectory(legacyDirectory);
                return;
            }
            if (!File.Exists(baselinePath))
                throw new FileNotFoundException(
                    "The shared ScrapLab icon baseline is missing.",
                    baselinePath);
            byte[] baseline = File.ReadAllBytes(baselinePath);
            byte[] atlas = File.ReadAllBytes(atlasPath);
            ScrapLabIconAtlasCoordinator.SharedAtlasReceipt receipt =
                ScrapLabIconAtlasCoordinator.CreateReceipt(
                    xml.NormalizedText, atlas, baseline, baselinePath,
                    xml.OriginalHash, catalog);
            byte[] json = ScrapLabIconAtlasCoordinator.SerializeReceipt(
                receipt);
            WriteAtomic(statePath, json, "atlas-receipt");
            TryDelete(legacyMirror);
            TryDelete(legacyBaseline);
            TryDeleteEmptyDirectory(legacyDirectory);
        }

        private static byte[] ReadVerifiedBaseline(
            string path,
            ScrapLabIconAtlasCoordinator.SharedAtlasReceipt receipt)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            if (receipt == null ||
                String.IsNullOrEmpty(receipt.BaselineHash))
                return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (!String.Equals(
                        AdaptivePatchSupport.Sha256(bytes),
                        receipt.BaselineHash,
                        StringComparison.OrdinalIgnoreCase))
                    return null;
                return bytes;
            }
            catch { return null; }
        }

        internal static void WriteAtomic(
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
            finally { TryDelete(temporary); }
        }

        private static void RequireHash(
            string path, string hash, string display)
        {
            if (!File.Exists(path) || !String.Equals(
                AdaptivePatchSupport.Sha256(path), hash,
                StringComparison.OrdinalIgnoreCase))
                throw new IOException(
                    display + " failed checksum verification.");
        }

        private static void Verify(
            string path, bool deleted, string hash, string display)
        {
            if (deleted)
            {
                if (File.Exists(path))
                    throw new IOException(display + " could not be removed.");
                return;
            }
            RequireHash(path, hash, display);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        private static void TryDeleteEmptyDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path) &&
                    Directory.GetFileSystemEntries(path).Length == 0)
                    Directory.Delete(path);
            }
            catch { }
        }

        private static void RestoreSnapshot(
            string path, bool existed, byte[] bytes, string operation)
        {
            if (existed)
                WriteAtomic(path, bytes, operation);
            else
                TryDelete(path);
        }
    }
}
