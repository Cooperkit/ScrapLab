using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal static class PatchHelperClient
    {
        private const string HelperProduct =
            "Raid Rescue Patch Helper for Scrap Mechanic";
        private static readonly object Sync = new object();
        private static Process brokerProcess;
        private static NamedPipeServerStream brokerServer;
        private static StreamWriter brokerWriter;
        private static StreamReader brokerReader;

        internal static GamePatchResult GetStatus(string action)
        {
            if (!PatchHelperProtocol.IsStatusAction(action))
                return Failure("The patch helper rejected an unknown status action.");
            try
            {
                string helper = GetValidatedHelper();
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = helper,
                    Arguments = Quote(PatchHelperProtocol.StatusSwitch) + " " +
                        Quote(action),
                    WorkingDirectory = Path.GetDirectoryName(helper),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(start))
                {
                    if (process == null)
                        throw new InvalidOperationException(
                            "Windows could not start the patch helper.");
                    string response = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException(
                            "The patch helper status check timed out.");
                    }
                    if (process.ExitCode != 0 || String.IsNullOrWhiteSpace(response))
                        throw new InvalidDataException(
                            String.IsNullOrWhiteSpace(errors)
                                ? "The patch helper returned no status."
                                : errors.Trim());
                    return Serializer().Deserialize<GamePatchResult>(
                        response.Trim());
                }
            }
            catch (Exception exception)
            {
                return Failure(
                    "Patch Bay is unavailable. " + exception.Message);
            }
        }

        internal static GamePatchResult Execute(
            string action, bool enabled, string mode)
        {
            if (!PatchHelperProtocol.IsKnownAction(action) ||
                !PatchHelperProtocol.IsValidMode(action, mode ?? ""))
                return Failure("The patch helper rejected invalid instructions.");

            lock (Sync)
            {
                try
                {
                    EnsureBroker();
                    PatchHelperRequest request = new PatchHelperRequest
                    {
                        ProtocolVersion = PatchHelperProtocol.Version,
                        Action = action,
                        Enabled = enabled,
                        Mode = mode ?? ""
                    };
                    brokerWriter.WriteLine(Serializer().Serialize(request));
                    string response = brokerReader.ReadLine();
                    if (String.IsNullOrWhiteSpace(response))
                        throw new IOException(
                            "The elevated patch helper returned no result.");
                    return Serializer().Deserialize<GamePatchResult>(response);
                }
                catch (Win32Exception exception)
                {
                    Reset();
                    if (exception.NativeErrorCode == 1223)
                    {
                        return new GamePatchResult
                        {
                            Success = false,
                            Cancelled = true,
                            Changes = new List<string>()
                        };
                    }
                    return Failure(exception.Message);
                }
                catch (Exception exception)
                {
                    Reset();
                    return Failure(
                        "The elevated patch helper could not complete the request. " +
                        exception.Message);
                }
            }
        }

        private static void EnsureBroker()
        {
            if (brokerProcess != null)
            {
                try
                {
                    if (!brokerProcess.HasExited &&
                        brokerServer != null && brokerServer.IsConnected)
                        return;
                }
                catch { }
                Reset();
            }

            string helper = GetValidatedHelper();
            string pipeName = "RaidRescue-Patch-" +
                Guid.NewGuid().ToString("N");
            brokerServer = CreateCurrentUserPipe(pipeName);
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = helper,
                Arguments =
                    Quote(PatchHelperProtocol.ElevatedSwitch) + " " +
                    Quote(pipeName) + " " +
                    Quote(Process.GetCurrentProcess().Id.ToString()),
                WorkingDirectory = Path.GetDirectoryName(helper),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            brokerProcess = Process.Start(start);
            if (brokerProcess == null)
                throw new InvalidOperationException(
                    "Windows did not start the patch helper.");

            IAsyncResult waiting =
                brokerServer.BeginWaitForConnection(null, null);
            if (!waiting.AsyncWaitHandle.WaitOne(15000))
                throw new TimeoutException(
                    "The elevated patch helper did not connect in time.");
            brokerServer.EndWaitForConnection(waiting);

            uint connectedProcess;
            if (!GetNamedPipeClientProcessId(
                brokerServer.SafePipeHandle.DangerousGetHandle(),
                out connectedProcess) ||
                connectedProcess != (uint)brokerProcess.Id)
                throw new InvalidDataException(
                    "The patch helper connection failed identity verification.");

            brokerWriter = new StreamWriter(
                brokerServer, new UTF8Encoding(false)) { AutoFlush = true };
            brokerReader = new StreamReader(
                brokerServer, new UTF8Encoding(false, true));
        }

        private static NamedPipeServerStream CreateCurrentUserPipe(
            string pipeName)
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (identity == null || identity.User == null)
                throw new InvalidOperationException(
                    "Windows could not identify the current user.");
            PipeSecurity security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(
                identity.User, PipeAccessRights.FullControl,
                AccessControlType.Allow));
            security.SetOwner(identity.User);
            return new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                4096, 4096, security);
        }

        private static string GetValidatedHelper()
        {
            string helper = CompanionSecurity.GetSibling(
                PatchHelperProtocol.HelperFileName);
            CompanionSecurity.ValidateCompanion(
                helper, HelperProduct, true);
            return helper;
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = Int32.MaxValue
            };
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static void Reset()
        {
            try { if (brokerWriter != null) brokerWriter.Dispose(); } catch { }
            try { if (brokerReader != null) brokerReader.Dispose(); } catch { }
            try { if (brokerServer != null) brokerServer.Dispose(); } catch { }
            try { if (brokerProcess != null) brokerProcess.Dispose(); } catch { }
            brokerWriter = null;
            brokerReader = null;
            brokerServer = null;
            brokerProcess = null;
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
        private static extern bool GetNamedPipeClientProcessId(
            IntPtr pipe, out uint clientProcessId);
    }
}
