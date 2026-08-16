using System;
using System.Collections.Generic;

namespace RaidRescue
{
    internal sealed class GameplayModBatchOperation
    {
        internal string Key;
        internal string Name;
        internal Func<GamePatchResult> Probe;
        internal Func<bool, GamePatchResult> Apply;
    }

    internal static class GameplayModsBatchCoordinator
    {
        internal const string InstalledOutcome = "INSTALLED";
        internal const string UpdatedOutcome = "UPDATED";
        internal const string AlreadyActiveOutcome = "ALREADY ACTIVE";
        internal const string SkippedOutcome = "SKIPPED";
        internal const string FailedOutcome = "FAILED";
        internal const string RemovedOutcome = "REMOVED";
        internal const string NotAttemptedOutcome = "NOT ATTEMPTED";

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing gameplay mods.");

            GamePatchResult result = NewResult(enabled);
            try
            {
                if (enabled)
                {
                    EnableAll(result, CreateInstallBeforeDependencyOperations());
                    EnableDependencyPair(result);
                    EnableAll(result, CreateInstallAfterDependencyOperations());
                }
                else
                    DisableProduction(result);
                FinalizeResult(result, enabled);
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                result.CompatibilityState = "BATCH FAILED";
                return result;
            }
        }

        internal static GamePatchResult RunForTest(
            bool enabled, IList<GameplayModBatchOperation> operations)
        {
            GamePatchResult result = NewResult(enabled);
            if (enabled)
                EnableAll(result, operations);
            else
                DisableAll(result, operations);
            FinalizeResult(result, enabled);
            return result;
        }

        private static IList<GameplayModBatchOperation>
            CreateInstallBeforeDependencyOperations()
        {
            return new List<GameplayModBatchOperation>
            {
                Operation("resource-locator", "Resource Locator Dots",
                    SecretModPatchService.GetStatus,
                    SecretModPatchService.SetEnabled),
                Operation("revival-buffs", "Revival Buff Recovery",
                    RevivalBuffPatchService.GetStatus,
                    delegate(bool enabled) {
                        return RevivalBuffPatchService.SetEnabled(enabled);
                    }),
                Operation("full-speed-carrying", "Full-Speed Carrying",
                    CarrySprintPatchService.GetStatus,
                    CarrySprintPatchService.SetEnabled),
                Operation("better-engines", "Better Engines",
                    BetterEnginesPatchService.GetStatus,
                    BetterEnginesPatchService.SetEnabled),
                Operation("better-freezer-beehive", "Better Freezer & Beehive",
                    BetterFreezerBeehivePatchService.GetStatus,
                    BetterFreezerBeehivePatchService.SetEnabled),
                Operation("better-plasma-drills", "Better Plasma Drills",
                    BetterPlasmaDrillsPatchService.GetStatus,
                    BetterPlasmaDrillsPatchService.SetEnabled)
            };
        }

        private static IList<GameplayModBatchOperation>
            CreateInstallAfterDependencyOperations()
        {
            return new List<GameplayModBatchOperation>
            {
                Operation("raid-detector", "Raid Detector",
                    RaidDetectorPatchService.GetStatus,
                    RaidDetectorPatchService.SetEnabled),
                Operation("tree-saplings", "Tree Saplings",
                    TreeSaplingsPatchService.GetStatus,
                    TreeSaplingsPatchService.SetEnabled),
                Operation("wireless-vacuum-pipe", "Wireless Vacuum Pipe",
                    WirelessVacuumPipePatchService.GetStatus,
                    WirelessVacuumPipePatchService.SetEnabled),
                Operation("network-storage-chest", "Network Storage Chest",
                    NetworkStorageChestPatchService.GetStatus,
                    NetworkStorageChestPatchService.SetEnabled)
            };
        }

        private static IList<GameplayModBatchOperation>
            CreateRemovalBeforeDependencyOperations()
        {
            return new List<GameplayModBatchOperation>
            {
                Operation("network-storage-chest", "Network Storage Chest",
                    NetworkStorageChestPatchService.GetStatus,
                    NetworkStorageChestPatchService.SetEnabled),
                Operation("wireless-vacuum-pipe", "Wireless Vacuum Pipe",
                    WirelessVacuumPipePatchService.GetStatus,
                    WirelessVacuumPipePatchService.SetEnabled),
                Operation("raid-detector", "Raid Detector",
                    RaidDetectorPatchService.GetStatus,
                    RaidDetectorPatchService.SetEnabled),
                Operation("tree-saplings", "Tree Saplings",
                    TreeSaplingsPatchService.GetStatus,
                    TreeSaplingsPatchService.SetEnabled),
                Operation("better-plasma-drills", "Better Plasma Drills",
                    BetterPlasmaDrillsPatchService.GetStatus,
                    BetterPlasmaDrillsPatchService.SetEnabled)
            };
        }

        private static IList<GameplayModBatchOperation>
            CreateRemovalAfterDependencyOperations()
        {
            return new List<GameplayModBatchOperation>
            {
                Operation("revival-buffs", "Revival Buff Recovery",
                    RevivalBuffPatchService.GetStatus,
                    delegate(bool enabled) {
                        return RevivalBuffPatchService.SetEnabled(enabled);
                    }),
                Operation("full-speed-carrying", "Full-Speed Carrying",
                    CarrySprintPatchService.GetStatus,
                    CarrySprintPatchService.SetEnabled),
                Operation("better-freezer-beehive", "Better Freezer & Beehive",
                    BetterFreezerBeehivePatchService.GetStatus,
                    BetterFreezerBeehivePatchService.SetEnabled),
                Operation("better-engines", "Better Engines",
                    BetterEnginesPatchService.GetStatus,
                    BetterEnginesPatchService.SetEnabled),
                Operation("resource-locator", "Resource Locator Dots",
                    SecretModPatchService.GetStatus,
                    SecretModPatchService.SetEnabled)
            };
        }

        private static GameplayModBatchOperation Operation(
            string key, string name, Func<GamePatchResult> probe,
            Func<bool, GamePatchResult> apply)
        {
            return new GameplayModBatchOperation
            {
                Key = key,
                Name = name,
                Probe = probe,
                Apply = apply
            };
        }

        private static readonly GameplayModBatchOperation ChemicalOperation =
            Operation("chemical-fertilizer", "Chemical Fertilizer Splash",
                ChemicalFertilizerPatchService.GetStatus,
                delegate(bool enabled) {
                    return DualFluidCannonPatchCoordinator.SetChemicalEnabled(enabled);
                });

        private static readonly GameplayModBatchOperation CannonOperation =
            Operation("dual-fluid-cannon", "Dual-Fluid Water Cannon",
                DualFluidCannonPatchService.GetStatus,
                delegate(bool enabled) {
                    return DualFluidCannonPatchCoordinator.SetCannonEnabled(enabled);
                });

        private static void EnableDependencyPair(GamePatchResult batch)
        {
            GamePatchResult cannon = SafeProbe(CannonOperation);
            GamePatchResult fertilizer = SafeProbe(ChemicalOperation);

            if (cannon != null && cannon.Success &&
                cannon.Installed && !cannon.NeedsUpdate)
            {
                AddItem(batch, CannonOperation, AlreadyActiveOutcome,
                    "Already installed and current.");
                EnableFertilizerOnly(batch, fertilizer);
                return;
            }

            if (cannon != null && cannon.Success && cannon.CanApply)
            {
                GamePatchResult applied = SafeApply(CannonOperation, true);
                if (applied != null && applied.Success)
                {
                    Merge(batch, applied);
                    AddItem(batch, CannonOperation,
                        cannon.Installed ? UpdatedOutcome : InstalledOutcome,
                        "The dependency-aware cannon operation completed.");
                    AddItem(batch, ChemicalOperation,
                        fertilizer != null && fertilizer.Installed
                            ? (fertilizer.NeedsUpdate ? UpdatedOutcome : AlreadyActiveOutcome)
                            : InstalledOutcome,
                        "Required fertilizer support is active.");
                    return;
                }

                AddItem(batch, CannonOperation, FailedOutcome,
                    StatusReason(applied));
                AddItem(batch, ChemicalOperation, FailedOutcome,
                    "The combined dependency transaction was rolled back.");
                return;
            }

            AddItem(batch, CannonOperation, SkippedOutcome,
                StatusReason(cannon));
            EnableFertilizerOnly(batch, fertilizer);
        }

        private static void EnableFertilizerOnly(
            GamePatchResult batch, GamePatchResult fertilizer)
        {
            if (fertilizer == null || !fertilizer.Success)
            {
                AddItem(batch, ChemicalOperation, SkippedOutcome,
                    StatusReason(fertilizer));
                return;
            }
            if (fertilizer.Installed && !fertilizer.NeedsUpdate)
            {
                AddItem(batch, ChemicalOperation, AlreadyActiveOutcome,
                    "Already installed and current.");
                return;
            }
            if (!fertilizer.CanApply)
            {
                AddItem(batch, ChemicalOperation, SkippedOutcome,
                    StatusReason(fertilizer));
                return;
            }

            GamePatchResult applied = SafeApply(ChemicalOperation, true);
            if (applied != null && applied.Success)
            {
                Merge(batch, applied);
                AddItem(batch, ChemicalOperation,
                    fertilizer.Installed ? UpdatedOutcome : InstalledOutcome,
                    "Verified fertilizer patch operation completed.");
            }
            else
            {
                AddItem(batch, ChemicalOperation, FailedOutcome,
                    StatusReason(applied));
            }
        }

        private static void DisableProduction(GamePatchResult batch)
        {
            IList<GameplayModBatchOperation> before =
                CreateRemovalBeforeDependencyOperations();
            IList<GameplayModBatchOperation> after =
                CreateRemovalAfterDependencyOperations();
            if (!DisableAll(batch, before))
            {
                AddItem(batch, CannonOperation, NotAttemptedOutcome,
                    "Not attempted because an earlier removal failed.");
                AddItem(batch, ChemicalOperation, NotAttemptedOutcome,
                    "Not attempted because an earlier removal failed.");
                AddNotAttempted(batch, after, 0);
                return;
            }
            if (!DisableDependencyPair(batch))
            {
                AddNotAttempted(batch, after, 0);
                return;
            }
            DisableAll(batch, after);
        }

        private static bool DisableDependencyPair(GamePatchResult batch)
        {
            GamePatchResult cannon = SafeProbe(CannonOperation);
            GamePatchResult fertilizer = SafeProbe(ChemicalOperation);
            if (cannon == null || !cannon.Success ||
                fertilizer == null || !fertilizer.Success)
            {
                AddItem(batch, CannonOperation, FailedOutcome,
                    FirstText(StatusReason(cannon), StatusReason(fertilizer)));
                return false;
            }
            if (!cannon.Installed && !fertilizer.Installed)
                return true;

            GamePatchResult applied = fertilizer.Installed
                ? SafeApply(ChemicalOperation, false)
                : SafeApply(CannonOperation, false);
            if (applied == null || !applied.Success)
            {
                GameplayModBatchOperation failedOperation = cannon.Installed
                    ? CannonOperation : ChemicalOperation;
                AddItem(batch, failedOperation, FailedOutcome,
                    StatusReason(applied));
                return false;
            }

            Merge(batch, applied);
            if (cannon.Installed)
                AddItem(batch, CannonOperation, RemovedOutcome,
                    "Removed before its fertilizer dependency.");
            if (fertilizer.Installed)
                AddItem(batch, ChemicalOperation, RemovedOutcome,
                    "Verified original state restored.");
            return true;
        }

        private static void EnableAll(
            GamePatchResult batch,
            IList<GameplayModBatchOperation> operations)
        {
            foreach (GameplayModBatchOperation operation in operations)
            {
                GamePatchResult status = SafeProbe(operation);
                if (status == null || !status.Success)
                {
                    AddItem(batch, operation, SkippedOutcome,
                        StatusReason(status));
                    continue;
                }
                if (status.Installed && !status.NeedsUpdate)
                {
                    AddItem(batch, operation, AlreadyActiveOutcome,
                        "Already installed and current.");
                    continue;
                }
                if (!status.CanApply)
                {
                    AddItem(batch, operation, SkippedOutcome,
                        StatusReason(status));
                    continue;
                }

                GamePatchResult applied = SafeApply(operation, true);
                if (applied != null && applied.Success)
                {
                    Merge(batch, applied);
                    AddItem(batch, operation,
                        status.Installed ? UpdatedOutcome : InstalledOutcome,
                        FirstText(applied.CompatibilityReason,
                            "Verified patch operation completed."));
                }
                else
                {
                    AddItem(batch, operation, FailedOutcome,
                        StatusReason(applied));
                }
            }
        }

        private static bool DisableAll(
            GamePatchResult batch,
            IList<GameplayModBatchOperation> operations)
        {
            for (int index = 0; index < operations.Count; index++)
            {
                GameplayModBatchOperation operation = operations[index];
                GamePatchResult status = SafeProbe(operation);
                if (status == null || !status.Success)
                {
                    AddItem(batch, operation, FailedOutcome,
                        StatusReason(status));
                    AddNotAttempted(batch, operations, index + 1);
                    return false;
                }
                if (!status.Installed)
                    continue;

                GamePatchResult applied = SafeApply(operation, false);
                if (applied != null && applied.Success)
                {
                    Merge(batch, applied);
                    AddItem(batch, operation, RemovedOutcome,
                        "Verified original state restored.");
                    continue;
                }

                AddItem(batch, operation, FailedOutcome,
                    StatusReason(applied));
                AddNotAttempted(batch, operations, index + 1);
                return false;
            }
            return true;
        }

        private static GamePatchResult SafeProbe(
            GameplayModBatchOperation operation)
        {
            try { return operation.Probe(); }
            catch (Exception exception) { return Failure(exception.Message); }
        }

        private static GamePatchResult SafeApply(
            GameplayModBatchOperation operation, bool enabled)
        {
            try { return operation.Apply(enabled); }
            catch (Exception exception) { return Failure(exception.Message); }
        }

        private static void AddNotAttempted(
            GamePatchResult batch,
            IList<GameplayModBatchOperation> operations, int start)
        {
            for (int index = start; index < operations.Count; index++)
                AddItem(batch, operations[index], NotAttemptedOutcome,
                    "Not attempted because an earlier removal failed.");
        }

        private static void AddItem(
            GamePatchResult batch, GameplayModBatchOperation operation,
            string outcome, string reason)
        {
            batch.BatchItems.Add(new GamePatchBatchItem
            {
                Key = operation.Key,
                Name = operation.Name,
                Outcome = outcome,
                Reason = reason ?? ""
            });
        }

        private static void Merge(
            GamePatchResult batch, GamePatchResult item)
        {
            batch.FilesPatched += item.FilesPatched;
            if (String.IsNullOrEmpty(batch.GamePath))
                batch.GamePath = item.GamePath;
            if (String.IsNullOrEmpty(batch.GameVersion))
                batch.GameVersion = item.GameVersion;
            if (String.IsNullOrEmpty(batch.SteamBuildId))
                batch.SteamBuildId = item.SteamBuildId;
            if (String.IsNullOrEmpty(batch.BackupPath))
                batch.BackupPath = item.BackupPath;
            if (item.Changes != null)
                batch.Changes.AddRange(item.Changes);
        }

        private static void FinalizeResult(
            GamePatchResult result, bool enabled)
        {
            int failed = CountOutcome(result, FailedOutcome);
            int installed = CountOutcome(result, InstalledOutcome);
            int updated = CountOutcome(result, UpdatedOutcome);
            int removed = CountOutcome(result, RemovedOutcome);
            int skipped = CountOutcome(result, SkippedOutcome);

            result.Success = failed == 0;
            result.Installed = enabled && failed == 0;
            result.CanApply = true;
            result.CompatibilityState = failed > 0
                ? "BATCH PARTIAL"
                : (enabled ? "ALL COMPATIBLE ENABLED" : "GAMEPLAY MODS REMOVED");
            if (failed > 0)
                result.Error = "One or more gameplay mods could not be changed. Review the batch results.";
            result.Changes.Insert(0,
                enabled
                    ? String.Format(
                        "Gameplay batch: {0} installed, {1} updated, {2} skipped, {3} failed.",
                        installed, updated, skipped, failed)
                    : String.Format(
                        "Gameplay batch: {0} removed, {1} failed.",
                        removed, failed));
        }

        private static int CountOutcome(
            GamePatchResult result, string outcome)
        {
            int count = 0;
            foreach (GamePatchBatchItem item in result.BatchItems)
                if (String.Equals(item.Outcome, outcome,
                    StringComparison.Ordinal)) count++;
            return count;
        }

        private static GamePatchResult NewResult(bool enabled)
        {
            return new GamePatchResult
            {
                Installed = enabled,
                Changes = new List<string>(),
                BatchItems = new List<GamePatchBatchItem>()
            };
        }

        private static GamePatchResult Failure(string message)
        {
            return new GamePatchResult
            {
                Success = false,
                Error = message,
                Changes = new List<string>(),
                BatchItems = new List<GamePatchBatchItem>()
            };
        }

        private static string StatusReason(GamePatchResult result)
        {
            if (result == null) return "The patch service returned no result.";
            return FirstText(
                result.CompatibilityReason,
                result.Error,
                result.CompatibilityState,
                "The patch is not currently available.");
        }

        private static string FirstText(params string[] values)
        {
            foreach (string value in values)
                if (!String.IsNullOrWhiteSpace(value)) return value.Trim();
            return "";
        }
    }
}
