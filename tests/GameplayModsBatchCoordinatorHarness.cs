using System;
using System.Collections.Generic;

namespace RaidRescue
{
    public sealed class GamePatchResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public bool Installed { get; set; }
        public bool NeedsUpdate { get; set; }
        public string CompatibilityState { get; set; }
        public string SteamBuildId { get; set; }
        public bool CanApply { get; set; }
        public string CompatibilityReason { get; set; }
        public string Error { get; set; }
        public string GamePath { get; set; }
        public string GameVersion { get; set; }
        public string BackupPath { get; set; }
        public int FilesPatched { get; set; }
        public List<string> Changes { get; set; }
        public List<GamePatchBatchItem> BatchItems { get; set; }
    }

    public sealed class GamePatchBatchItem
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Outcome { get; set; }
        public string Reason { get; set; }
    }

    internal static class BatchHarness
    {
        private static int afterFailureApplies;
        private static int removalTailApplies;

        private static GamePatchResult Status(
            bool installed, bool update, bool canApply)
        {
            return new GamePatchResult
            {
                Success = true,
                Installed = installed,
                NeedsUpdate = update,
                CanApply = canApply,
                Changes = new List<string>()
            };
        }

        private static GamePatchResult Applied(bool enabled)
        {
            return new GamePatchResult
            {
                Success = true,
                Installed = enabled,
                CanApply = true,
                FilesPatched = 1,
                Changes = new List<string> { "changed" }
            };
        }

        private static GamePatchResult Failed()
        {
            return new GamePatchResult
            {
                Success = false,
                Error = "simulated failure",
                Changes = new List<string>()
            };
        }

        private static GameplayModBatchOperation Operation(
            string key, Func<GamePatchResult> probe,
            Func<bool, GamePatchResult> apply)
        {
            return new GameplayModBatchOperation
            {
                Key = key,
                Name = key,
                Probe = probe,
                Apply = apply
            };
        }

        private static int Count(GamePatchResult result, string outcome)
        {
            int count = 0;
            foreach (GamePatchBatchItem item in result.BatchItems)
                if (item.Outcome == outcome) count++;
            return count;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static void Main()
        {
            List<GameplayModBatchOperation> install =
                new List<GameplayModBatchOperation>
                {
                    Operation("active", delegate { return Status(true, false, true); },
                        delegate(bool enabled) { throw new Exception("active applied"); }),
                    Operation("install", delegate { return Status(false, false, true); },
                        delegate(bool enabled) { return Applied(enabled); }),
                    Operation("update", delegate { return Status(true, true, true); },
                        delegate(bool enabled) { return Applied(enabled); }),
                    Operation("blocked", delegate { return Status(false, false, false); },
                        delegate(bool enabled) { throw new Exception("blocked applied"); }),
                    Operation("failure", delegate { return Status(false, false, true); },
                        delegate(bool enabled) { return Failed(); }),
                    Operation("after-failure", delegate { return Status(false, false, true); },
                        delegate(bool enabled) { afterFailureApplies++; return Applied(enabled); })
                };
            GamePatchResult enabledResult =
                GameplayModsBatchCoordinator.RunForTest(true, install);
            Require(!enabledResult.Success, "partial install did not report failure");
            Require(Count(enabledResult, GameplayModsBatchCoordinator.AlreadyActiveOutcome) == 1,
                "already-active result missing");
            Require(Count(enabledResult, GameplayModsBatchCoordinator.InstalledOutcome) == 2,
                "install results missing");
            Require(Count(enabledResult, GameplayModsBatchCoordinator.UpdatedOutcome) == 1,
                "update result missing");
            Require(Count(enabledResult, GameplayModsBatchCoordinator.SkippedOutcome) == 1,
                "blocked result was not skipped");
            Require(Count(enabledResult, GameplayModsBatchCoordinator.FailedOutcome) == 1,
                "runtime failure result missing");
            Require(afterFailureApplies == 1,
                "independent install did not continue after failure");

            List<GameplayModBatchOperation> removal =
                new List<GameplayModBatchOperation>
                {
                    Operation("first", delegate { return Status(true, false, true); },
                        delegate(bool enabled) { return Applied(enabled); }),
                    Operation("failure", delegate { return Status(true, false, true); },
                        delegate(bool enabled) { return Failed(); }),
                    Operation("tail", delegate { return Status(true, false, true); },
                        delegate(bool enabled) { removalTailApplies++; return Applied(enabled); })
                };
            GamePatchResult disabledResult =
                GameplayModsBatchCoordinator.RunForTest(false, removal);
            Require(!disabledResult.Success, "failed removal reported success");
            Require(Count(disabledResult, GameplayModsBatchCoordinator.RemovedOutcome) == 1,
                "verified removal result missing");
            Require(Count(disabledResult, GameplayModsBatchCoordinator.FailedOutcome) == 1,
                "removal failure result missing");
            Require(Count(disabledResult, GameplayModsBatchCoordinator.NotAttemptedOutcome) == 1,
                "later removal was not marked unattempted");
            Require(removalTailApplies == 0,
                "removal continued after the first failure");
        }
    }

    internal static class GamePatchService { internal static bool IsGameRunning() { return false; } }
    internal static class SecretModPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class RevivalBuffPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class CarrySprintPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class BetterEnginesPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class BetterFreezerBeehivePatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class BetterPlasmaDrillsPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class RaidDetectorPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class WirelessVacuumPipePatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class NetworkStorageChestPatchService { internal static GamePatchResult GetStatus() { return null; } internal static GamePatchResult SetEnabled(bool value) { return null; } }
    internal static class ChemicalFertilizerPatchService { internal static GamePatchResult GetStatus() { return null; } }
    internal static class DualFluidCannonPatchService { internal static GamePatchResult GetStatus() { return null; } }
    internal static class DualFluidCannonPatchCoordinator
    {
        internal static GamePatchResult SetChemicalEnabled(bool value) { return null; }
        internal static GamePatchResult SetCannonEnabled(bool value) { return null; }
    }
}
