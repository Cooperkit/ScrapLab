using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace RaidRescue
{
    internal static class CompanionSecurity
    {
        internal static string GetSibling(string fileName)
        {
            string current = Path.GetFullPath(
                Assembly.GetExecutingAssembly().Location);
            return Path.Combine(Path.GetDirectoryName(current), fileName);
        }

        internal static void ValidateCompanion(
            string path, string expectedProduct, bool requireSameVersion)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException(
                    "A required Raid Rescue companion is missing.", path);

            string currentDirectory = Path.GetDirectoryName(Path.GetFullPath(
                Assembly.GetExecutingAssembly().Location));
            string companionDirectory =
                Path.GetDirectoryName(Path.GetFullPath(path));
            if (!String.Equals(
                currentDirectory, companionDirectory,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "A companion program must be beside RaidRescue.exe.");

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            if (!String.Equals(
                info.ProductName, expectedProduct, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "A companion program has an unexpected product identity.");

            if (requireSameVersion)
            {
                Version current =
                    Assembly.GetExecutingAssembly().GetName().Version;
                Version companion;
                if (!Version.TryParse(info.FileVersion, out companion) ||
                    companion.Major != current.Major ||
                    companion.Minor != current.Minor ||
                    companion.Build != current.Build)
                    throw new InvalidDataException(
                        "The patch helper does not match this Raid Rescue version. " +
                        "Install the complete release bundle.");
            }

            RequireMatchingSignerWhenSigned(
                Assembly.GetExecutingAssembly().Location, path);
        }

        internal static void RequireMatchingSignerWhenSigned(
            string trustedPath, string candidatePath)
        {
            X509Certificate2 trusted = TryGetSigner(trustedPath);
            if (trusted == null)
                return;
            if (!HasValidAuthenticodeSignature(trustedPath))
                throw new InvalidDataException(
                    "The trusted Raid Rescue program has an invalid Authenticode signature.");
            X509Certificate2 candidate = TryGetSigner(candidatePath);
            if (candidate == null ||
                !HasValidAuthenticodeSignature(candidatePath) ||
                !String.Equals(
                    trusted.Thumbprint, candidate.Thumbprint,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The companion program is not signed by the same publisher.");
        }

        internal static X509Certificate2 TryGetSigner(string path)
        {
            try
            {
                X509Certificate certificate =
                    X509Certificate.CreateFromSignedFile(path);
                return new X509Certificate2(certificate);
            }
            catch
            {
                return null;
            }
        }

        internal static bool HasValidAuthenticodeSignature(string path)
        {
            WinTrustFileInfo file = new WinTrustFileInfo(path);
            IntPtr filePointer = IntPtr.Zero;
            try
            {
                filePointer = Marshal.AllocCoTaskMem(
                    Marshal.SizeOf(typeof(WinTrustFileInfo)));
                Marshal.StructureToPtr(file, filePointer, false);
                WinTrustData data = new WinTrustData(filePointer);
                Guid action = new Guid(
                    "00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
                return WinVerifyTrust(
                    new IntPtr(-1), ref action, data) == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (filePointer != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(
                        filePointer, typeof(WinTrustFileInfo));
                    Marshal.FreeCoTaskMem(filePointer);
                }
                file.Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class WinTrustFileInfo : IDisposable
        {
            public int StructSize =
                Marshal.SizeOf(typeof(WinTrustFileInfo));
            public IntPtr FilePath;
            public IntPtr FileHandle = IntPtr.Zero;
            public IntPtr KnownSubject = IntPtr.Zero;

            internal WinTrustFileInfo(string path)
            {
                FilePath = Marshal.StringToCoTaskMemUni(path);
            }

            public void Dispose()
            {
                if (FilePath != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(FilePath);
                    FilePath = IntPtr.Zero;
                }
                GC.SuppressFinalize(this);
            }

            ~WinTrustFileInfo()
            {
                Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class WinTrustData
        {
            public int StructSize =
                Marshal.SizeOf(typeof(WinTrustData));
            public IntPtr PolicyCallbackData = IntPtr.Zero;
            public IntPtr SipClientData = IntPtr.Zero;
            public int UiChoice = 2;
            public int RevocationChecks = 0;
            public int UnionChoice = 1;
            public IntPtr FileInfo;
            public int StateAction = 0;
            public IntPtr StateData = IntPtr.Zero;
            public string UrlReference = null;
            public int ProviderFlags = 0x00001000;
            public int UiContext = 0;

            internal WinTrustData(IntPtr fileInfo)
            {
                FileInfo = fileInfo;
            }
        }

        [DllImport(
            "wintrust.dll",
            ExactSpelling = true,
            SetLastError = false,
            PreserveSig = true,
            CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(
            IntPtr window,
            [MarshalAs(UnmanagedType.LPStruct)] ref Guid action,
            [In] [MarshalAs(UnmanagedType.LPStruct)] WinTrustData data);
    }
}
