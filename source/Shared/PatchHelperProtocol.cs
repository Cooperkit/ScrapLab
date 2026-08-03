using System;

namespace RaidRescue
{
    internal sealed class PatchHelperRequest
    {
        public int ProtocolVersion { get; set; }
        public string Action { get; set; }
        public bool Enabled { get; set; }
        public string Mode { get; set; }
    }

    internal static class PatchHelperProtocol
    {
        internal const int Version = 1;
        internal const string HelperFileName = "ScrapLab.PatchHelper.exe";
        internal const string ElevatedSwitch = "--elevated-session";
        internal const string StatusSwitch = "--status";

        internal const string Hotfix = "hotfix";
        internal const string ResourceLocator = "resource-locator";
        internal const string RevivalBuffs = "revival-buffs";
        internal const string FullSpeedCarrying = "full-speed-carrying";
        internal const string BetterEngines = "better-engines";
        internal const string BetterFreezerBeehive = "better-freezer-beehive";
        internal const string BetterPlasmaDrills = "better-plasma-drills";
        internal const string ChemicalFertilizer = "chemical-fertilizer";
        internal const string DualFluidCannon = "dual-fluid-cannon";
        internal const string DeveloperCommands = "developer-commands";

        internal static bool IsKnownAction(string action)
        {
            return String.Equals(action, Hotfix, StringComparison.Ordinal) ||
                String.Equals(action, ResourceLocator, StringComparison.Ordinal) ||
                String.Equals(action, RevivalBuffs, StringComparison.Ordinal) ||
                String.Equals(action, FullSpeedCarrying, StringComparison.Ordinal) ||
                String.Equals(action, BetterEngines, StringComparison.Ordinal) ||
                String.Equals(action, BetterFreezerBeehive, StringComparison.Ordinal) ||
                String.Equals(action, BetterPlasmaDrills, StringComparison.Ordinal) ||
                String.Equals(action, ChemicalFertilizer, StringComparison.Ordinal) ||
                String.Equals(action, DualFluidCannon, StringComparison.Ordinal) ||
                String.Equals(action, DeveloperCommands, StringComparison.Ordinal);
        }

        internal static bool IsStatusAction(string action)
        {
            return IsKnownAction(action) &&
                !String.Equals(action, Hotfix, StringComparison.Ordinal);
        }

        internal static bool IsValidMode(string action, string mode)
        {
            if (!String.Equals(
                action, DeveloperCommands, StringComparison.Ordinal))
                return String.IsNullOrEmpty(mode);
            return String.Equals(mode, "host", StringComparison.Ordinal) ||
                String.Equals(mode, "everyone", StringComparison.Ordinal);
        }
    }
}
