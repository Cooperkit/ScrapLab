using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal static class PatchHelperProgram
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args == null || args.Length != 2 &&
                    args.Length != 3)
                    return 2;
                if (String.Equals(
                    args[0], PatchHelperProtocol.StatusSwitch,
                    StringComparison.Ordinal))
                    return RunStatus(args);
                if (String.Equals(
                    args[0], PatchHelperProtocol.ElevatedSwitch,
                    StringComparison.Ordinal))
                    return RunElevated(args);
                return 2;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static int RunStatus(string[] args)
        {
            if (args.Length != 2 ||
                !PatchHelperProtocol.IsStatusAction(args[1]))
                return 2;
            GamePatchResult result = DispatchStatus(args[1]);
            Console.Out.WriteLine(Serializer().Serialize(result));
            return 0;
        }

        private static int RunElevated(string[] args)
        {
            if (args.Length != 3 || !IsSafeIdentifier(args[1], 96))
                return 2;
            int parentId;
            if (!Int32.TryParse(args[2], out parentId) || parentId <= 0)
                return 2;
            if (!IsAdministrator())
                throw new UnauthorizedAccessException(
                    "The patch helper requires administrator approval.");

            Process parent = ValidateParent(parentId);
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", args[1], PipeDirection.InOut, PipeOptions.None,
                TokenImpersonationLevel.Identification))
            {
                pipe.Connect(15000);
                uint serverProcess;
                if (!GetNamedPipeServerProcessId(
                    pipe.SafePipeHandle.DangerousGetHandle(),
                    out serverProcess) ||
                    serverProcess != (uint)parentId)
                    throw new UnauthorizedAccessException(
                        "The patch helper rejected an unexpected pipe owner.");
                StreamReader reader = new StreamReader(
                    pipe, new UTF8Encoding(false, true));
                StreamWriter writer = new StreamWriter(
                    pipe, new UTF8Encoding(false)) { AutoFlush = true };
                while (!HasExited(parent))
                {
                    string text = reader.ReadLine();
                    if (String.IsNullOrWhiteSpace(text))
                        return 0;
                    GamePatchResult result;
                    try
                    {
                        PatchHelperRequest request =
                            Serializer().Deserialize<PatchHelperRequest>(text);
                        result = ValidateAndDispatch(request);
                    }
                    catch (Exception exception)
                    {
                        result = Failure(exception.Message);
                    }
                    writer.WriteLine(Serializer().Serialize(result));
                }
            }
            return 0;
        }

        private static Process ValidateParent(int parentId)
        {
            Process parent = Process.GetProcessById(parentId);
            string parentPath = Path.GetFullPath(parent.MainModule.FileName);
            string expected = Path.Combine(
                Path.GetDirectoryName(
                    Path.GetFullPath(Assembly.GetExecutingAssembly().Location)),
                "ScrapLab.exe");
            if (!String.Equals(
                parentPath, expected, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException(
                    "The patch helper rejected an unexpected parent program.");
            CompanionSecurity.RequireMatchingSignerWhenSigned(
                Assembly.GetExecutingAssembly().Location, parentPath);
            return parent;
        }

        private static GamePatchResult ValidateAndDispatch(
            PatchHelperRequest request)
        {
            if (request == null ||
                request.ProtocolVersion != PatchHelperProtocol.Version ||
                !PatchHelperProtocol.IsKnownAction(request.Action) ||
                !PatchHelperProtocol.IsValidMode(
                    request.Action, request.Mode ?? ""))
                return Failure(
                    "The patch helper rejected invalid instructions.");

            switch (request.Action)
            {
                case PatchHelperProtocol.Hotfix:
                    return GamePatchService.Install();
                case PatchHelperProtocol.ResourceLocator:
                    return SecretModPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.RevivalBuffs:
                    return RevivalBuffPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.FullSpeedCarrying:
                    return CarrySprintPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.BetterEngines:
                    return BetterEnginesPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.BetterFreezerBeehive:
                    return BetterFreezerBeehivePatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.BetterPlasmaDrills:
                    return BetterPlasmaDrillsPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.RaidDetector:
                    return RaidDetectorPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.WirelessVacuumPipe:
                    return WirelessVacuumPipePatchService.SetEnabled(
                        request.Enabled);
                case PatchHelperProtocol.NetworkStorageChest:
                    return NetworkStorageChestPatchService.SetEnabled(
                        request.Enabled);
                case PatchHelperProtocol.TreeSaplings:
                    return TreeSaplingsPatchService.SetEnabled(request.Enabled);
                case PatchHelperProtocol.ChemicalFertilizer:
                    return DualFluidCannonPatchCoordinator.SetChemicalEnabled(
                        request.Enabled);
                case PatchHelperProtocol.DualFluidCannon:
                    return DualFluidCannonPatchCoordinator.SetCannonEnabled(
                        request.Enabled);
                case PatchHelperProtocol.DeveloperCommands:
                    return DeveloperCommandsPatchService.SetEnabled(
                        request.Enabled, request.Mode);
                case PatchHelperProtocol.AllGameplayMods:
                    return GameplayModsBatchCoordinator.SetEnabled(
                        request.Enabled);
                default:
                    return Failure("The patch helper rejected an unknown action.");
            }
        }

        private static GamePatchResult DispatchStatus(string action)
        {
            switch (action)
            {
                case PatchHelperProtocol.ResourceLocator:
                    return SecretModPatchService.GetStatus();
                case PatchHelperProtocol.RevivalBuffs:
                    return RevivalBuffPatchService.GetStatus();
                case PatchHelperProtocol.FullSpeedCarrying:
                    return CarrySprintPatchService.GetStatus();
                case PatchHelperProtocol.BetterEngines:
                    return BetterEnginesPatchService.GetStatus();
                case PatchHelperProtocol.BetterFreezerBeehive:
                    return BetterFreezerBeehivePatchService.GetStatus();
                case PatchHelperProtocol.BetterPlasmaDrills:
                    return BetterPlasmaDrillsPatchService.GetStatus();
                case PatchHelperProtocol.RaidDetector:
                    return RaidDetectorPatchService.GetStatus();
                case PatchHelperProtocol.WirelessVacuumPipe:
                    return WirelessVacuumPipePatchService.GetStatus();
                case PatchHelperProtocol.NetworkStorageChest:
                    return NetworkStorageChestPatchService.GetStatus();
                case PatchHelperProtocol.TreeSaplings:
                    return TreeSaplingsPatchService.GetStatus();
                case PatchHelperProtocol.ChemicalFertilizer:
                    return ChemicalFertilizerPatchService.GetStatus();
                case PatchHelperProtocol.DualFluidCannon:
                    return DualFluidCannonPatchService.GetStatus();
                case PatchHelperProtocol.DeveloperCommands:
                    return DeveloperCommandsPatchService.GetStatus();
                default:
                    return Failure("The patch helper rejected an unknown status.");
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool IsSafeIdentifier(string value, int maximumLength)
        {
            if (String.IsNullOrEmpty(value) ||
                value.Length > maximumLength)
                return false;
            foreach (char character in value)
            {
                if (!Char.IsLetterOrDigit(character) && character != '-')
                    return false;
            }
            return true;
        }

        private static bool HasExited(Process process)
        {
            try { return process == null || process.HasExited; }
            catch { return true; }
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = Int32.MaxValue
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

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeServerProcessId(
            IntPtr pipe, out uint serverProcessId);
    }
}
