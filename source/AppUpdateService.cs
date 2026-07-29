using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    public sealed class AppUpdateResult
    {
        public bool Success { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool CanAutoUpdate { get; set; }
        public bool ReadyToRestart { get; set; }
        public string Error { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string TagName { get; set; }
        public string ReleaseUrl { get; set; }
        public string AssetUrl { get; set; }
        public string AssetDigest { get; set; }
        public long AssetSize { get; set; }
    }

    public sealed class AppUpdateStartupStatus
    {
        public bool HasStatus { get; set; }
        public bool Success { get; set; }
        public string Version { get; set; }
        public string Error { get; set; }
    }

    internal sealed class GitHubReleaseAsset
    {
        public string name { get; set; }
        public string state { get; set; }
        public string browser_download_url { get; set; }
        public string digest { get; set; }
        public long size { get; set; }
    }

    internal sealed class GitHubRelease
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public List<GitHubReleaseAsset> assets { get; set; }
    }

    internal sealed class TimedWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 10000;
            HttpWebRequest http = request as HttpWebRequest;
            if (http != null)
            {
                http.ReadWriteTimeout = 15000;
                http.AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            return request;
        }
    }

    internal static class AppUpdateService
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/Cooperkit/Raid-Rescue/releases/latest";
        private const string ReleasePrefix =
            "https://github.com/Cooperkit/Raid-Rescue/releases/";
        private const string DownloadPathPrefix =
            "/Cooperkit/Raid-Rescue/releases/download/";
        private const string AssetName = "RaidRescue.exe";
        private const string HelperArgument = "--raid-rescue-apply-update";
        private const string HelperPrefix = "RaidRescue-Updater-";
        private const string StagePrefix = ".RaidRescue.Update.";
        private static Timer cleanupTimer;

        public static string CurrentVersion
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                return FormatVersion(version);
            }
        }

        public static AppUpdateResult CheckForUpdates()
        {
            AppUpdateResult result = new AppUpdateResult
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = "",
                TagName = "",
                ReleaseUrl = ReleasePrefix + "latest",
                AssetUrl = "",
                AssetDigest = ""
            };

            try
            {
                EnableTls12();
                string json;
                using (TimedWebClient client = CreateWebClient())
                    json = client.DownloadString(LatestReleaseApi);

                JavaScriptSerializer serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = 1024 * 1024
                };
                GitHubRelease release =
                    serializer.Deserialize<GitHubRelease>(json);
                if (release == null || release.draft || release.prerelease)
                    throw new InvalidDataException(
                        "GitHub did not return a stable Raid Rescue release.");

                Version latest;
                if (!TryParseReleaseVersion(release.tag_name, out latest))
                    throw new InvalidDataException(
                        "The latest GitHub release has an unreadable version.");
                if (!IsOfficialReleaseUrl(release.html_url))
                    throw new InvalidDataException(
                        "GitHub returned an unexpected release address.");

                result.TagName = release.tag_name;
                result.LatestVersion = FormatVersion(latest);
                result.ReleaseUrl = release.html_url;
                result.UpdateAvailable =
                    CompareVersions(latest, CurrentAssemblyVersion()) > 0;
                result.Success = true;

                GitHubReleaseAsset asset = FindReleaseAsset(release.assets);
                if (asset != null)
                {
                    result.AssetUrl = asset.browser_download_url ?? "";
                    result.AssetDigest = NormalizeDigest(asset.digest);
                    result.AssetSize = asset.size;
                    result.CanAutoUpdate =
                        IsOfficialDownloadUrl(result.AssetUrl) &&
                        IsSha256(result.AssetDigest) &&
                        asset.size > 0;
                }

                if (result.UpdateAvailable && !result.CanAutoUpdate)
                {
                    result.Error =
                        "The release is available, but its verified Windows " +
                        "download is not ready. Open GitHub to update manually.";
                }
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error =
                    "Raid Rescue could not reach GitHub right now. " +
                    FriendlyMessage(exception);
                return result;
            }
        }

        public static AppUpdateResult PrepareAndLaunchUpdate(
            string assetUrl, string digest, string latestVersion)
        {
            AppUpdateResult result = new AppUpdateResult
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = latestVersion ?? "",
                AssetUrl = assetUrl ?? "",
                AssetDigest = NormalizeDigest(digest)
            };
            string stagePath = null;
            string helperPath = null;

            try
            {
                Version expected;
                if (!TryParseReleaseVersion(latestVersion, out expected) ||
                    CompareVersions(expected, CurrentAssemblyVersion()) <= 0)
                    throw new InvalidDataException(
                        "The selected release is not newer than this copy.");
                if (!IsOfficialDownloadUrl(assetUrl))
                    throw new InvalidDataException(
                        "The update download is not an official Raid Rescue asset.");
                if (!IsSha256(result.AssetDigest))
                    throw new InvalidDataException(
                        "GitHub did not provide a valid SHA-256 digest.");

                string targetPath = Path.GetFullPath(
                    Assembly.GetExecutingAssembly().Location);
                string targetDirectory = Path.GetDirectoryName(targetPath);
                stagePath = Path.Combine(
                    targetDirectory,
                    StagePrefix + Guid.NewGuid().ToString("N") + ".tmp");

                EnableTls12();
                using (TimedWebClient client = CreateWebClient())
                    client.DownloadFile(assetUrl, stagePath);

                VerifyDownloadedExecutable(
                    stagePath, result.AssetDigest, expected);

                helperPath = Path.Combine(
                    Path.GetTempPath(),
                    HelperPrefix + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(targetPath, helperPath, false);
                if (!HashesEqual(
                    ComputeSha256(helperPath), ComputeSha256(targetPath)))
                    throw new IOException(
                        "The temporary update helper failed verification.");

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = helperPath,
                    UseShellExecute = false,
                    WorkingDirectory = targetDirectory,
                    CreateNoWindow = true,
                    Arguments =
                        HelperArgument + " " +
                        Process.GetCurrentProcess().Id.ToString() + " " +
                        QuoteArgument(stagePath) + " " +
                        QuoteArgument(targetPath) + " " +
                        QuoteArgument(result.AssetDigest) + " " +
                        QuoteArgument(FormatVersion(expected))
                };
                Process helper = Process.Start(start);
                if (helper == null)
                    throw new InvalidOperationException(
                        "Windows could not start the update helper.");

                result.Success = true;
                result.ReadyToRestart = true;
                return result;
            }
            catch (Exception exception)
            {
                TryDelete(stagePath);
                TryDelete(helperPath);
                result.Success = false;
                result.Error =
                    "The update was not installed. " +
                    FriendlyMessage(exception);
                return result;
            }
        }

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(
                    args[0], HelperArgument,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            string targetPath = null;
            string backupPath = null;
            string expectedDigest = "";
            string expectedVersion = "";
            try
            {
                if (args.Length != 6)
                    throw new InvalidDataException(
                        "The update helper received incomplete instructions.");

                int parentId;
                if (!Int32.TryParse(args[1], out parentId) || parentId <= 0)
                    throw new InvalidDataException(
                        "The update helper received an invalid process ID.");

                string stagePath = Path.GetFullPath(args[2]);
                targetPath = Path.GetFullPath(args[3]);
                expectedDigest = NormalizeDigest(args[4]);
                expectedVersion = args[5] ?? "";

                ValidateHelperPaths(stagePath, targetPath);
                Version expected;
                if (!TryParseReleaseVersion(expectedVersion, out expected))
                    throw new InvalidDataException(
                        "The update helper received an invalid version.");
                if (!IsSha256(expectedDigest))
                    throw new InvalidDataException(
                        "The update helper received an invalid SHA-256 digest.");

                WaitForParent(parentId);
                VerifyDownloadedExecutable(stagePath, expectedDigest, expected);

                string updateDirectory = GetUpdateDataDirectory();
                Directory.CreateDirectory(updateDirectory);
                backupPath = Path.Combine(updateDirectory, "previous.exe");
                File.Copy(targetPath, backupPath, true);
                string previousDigest = ComputeSha256(targetPath);
                if (!HashesEqual(
                    previousDigest, ComputeSha256(backupPath)))
                    throw new IOException(
                        "The previous executable backup failed verification.");

                ReplaceExecutable(stagePath, targetPath);
                if (!HashesEqual(
                    expectedDigest, ComputeSha256(targetPath)))
                    throw new IOException(
                        "The installed executable failed final verification.");

                WriteStartupStatus(true, expectedVersion, "");
                try
                {
                    StartUpdatedApplication(targetPath);
                }
                catch
                {
                    RestorePreviousExecutable(
                        backupPath, targetPath, previousDigest);
                    throw;
                }
            }
            catch (Exception exception)
            {
                string failure =
                    "The automatic update was rolled back. " +
                    FriendlyMessage(exception);
                if (!String.IsNullOrEmpty(targetPath) &&
                    !String.IsNullOrEmpty(backupPath) &&
                    File.Exists(backupPath))
                {
                    try
                    {
                        string backupDigest = ComputeSha256(backupPath);
                        RestorePreviousExecutable(
                            backupPath, targetPath, backupDigest);
                    }
                    catch { }
                }
                WriteStartupStatus(false, expectedVersion, failure);
                try
                {
                    if (!String.IsNullOrEmpty(targetPath) &&
                        File.Exists(targetPath))
                        StartUpdatedApplication(targetPath);
                }
                catch { }
            }
            return true;
        }

        public static AppUpdateStartupStatus ConsumeStartupStatus()
        {
            AppUpdateStartupStatus result = new AppUpdateStartupStatus();
            string path = GetStartupStatusPath();
            try
            {
                if (!File.Exists(path))
                    return result;
                string[] lines = File.ReadAllLines(path);
                TryDelete(path);
                foreach (string line in lines)
                {
                    int split = line.IndexOf('=');
                    if (split <= 0)
                        continue;
                    string key = line.Substring(0, split);
                    string value = line.Substring(split + 1);
                    if (String.Equals(key, "Success",
                        StringComparison.OrdinalIgnoreCase))
                        result.Success = String.Equals(
                            value, "1", StringComparison.Ordinal);
                    else if (String.Equals(key, "Version",
                        StringComparison.OrdinalIgnoreCase))
                        result.Version = value;
                    else if (String.Equals(key, "Error",
                        StringComparison.OrdinalIgnoreCase))
                        result.Error = DecodeStatusValue(value);
                }
                result.HasStatus = true;
            }
            catch
            {
                // A missing update notice must never block startup.
            }
            return result;
        }

        public static void ScheduleCleanup()
        {
            cleanupTimer = new Timer(
                delegate { CleanupStaleFiles(); },
                null, 5000, Timeout.Infinite);
        }

        public static bool OpenOfficialRelease(string url)
        {
            try
            {
                if (!IsOfficialReleaseUrl(url))
                    return false;
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static TimedWebClient CreateWebClient()
        {
            TimedWebClient client = new TimedWebClient();
            client.Headers[HttpRequestHeader.UserAgent] =
                "RaidRescue/" + CurrentVersion;
            client.Headers[HttpRequestHeader.Accept] =
                "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            return client;
        }

        private static GitHubReleaseAsset FindReleaseAsset(
            List<GitHubReleaseAsset> assets)
        {
            if (assets == null)
                return null;
            foreach (GitHubReleaseAsset asset in assets)
            {
                if (asset != null &&
                    String.Equals(
                        asset.name, AssetName,
                        StringComparison.OrdinalIgnoreCase) &&
                    (String.IsNullOrEmpty(asset.state) ||
                     String.Equals(
                        asset.state, "uploaded",
                        StringComparison.OrdinalIgnoreCase)))
                    return asset;
            }
            return null;
        }

        private static void VerifyDownloadedExecutable(
            string path, string digest, Version expected)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length < 100000 || file.Length > 8 * 1024 * 1024)
                throw new InvalidDataException(
                    "The downloaded executable has an unexpected size.");
            if (!HashesEqual(digest, ComputeSha256(path)))
                throw new InvalidDataException(
                    "The downloaded executable does not match GitHub's SHA-256 digest.");

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            Version downloaded;
            if (!Version.TryParse(info.FileVersion, out downloaded) ||
                CompareVersions(downloaded, expected) != 0)
                throw new InvalidDataException(
                    "The downloaded executable version does not match the release.");
            if (!String.Equals(
                info.ProductName,
                "Raid Rescue for Scrap Mechanic",
                StringComparison.Ordinal))
                throw new InvalidDataException(
                    "The downloaded file is not a Raid Rescue executable.");
        }

        private static void ValidateHelperPaths(
            string stagePath, string targetPath)
        {
            if (!File.Exists(stagePath) || !File.Exists(targetPath))
                throw new FileNotFoundException(
                    "An update file disappeared before installation.");
            if (!String.Equals(
                Path.GetDirectoryName(stagePath),
                Path.GetDirectoryName(targetPath),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The staged update is not beside the installed executable.");
            if (!Path.GetFileName(stagePath).StartsWith(
                StagePrefix, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetExtension(targetPath), ".exe",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The update paths did not pass validation.");
        }

        private static void WaitForParent(int parentId)
        {
            try
            {
                Process parent = Process.GetProcessById(parentId);
                if (!parent.WaitForExit(60000))
                    throw new TimeoutException(
                        "Raid Rescue did not close in time.");
            }
            catch (ArgumentException)
            {
                // The main app already exited.
            }
        }

        private static void RestorePreviousExecutable(
            string backupPath, string targetPath, string expectedDigest)
        {
            File.Copy(backupPath, targetPath, true);
            if (!HashesEqual(
                expectedDigest, ComputeSha256(targetPath)))
                throw new IOException(
                    "The previous executable could not be restored.");
        }

        private static void ReplaceExecutable(
            string stagePath, string targetPath)
        {
            try
            {
                File.Replace(stagePath, targetPath, null, true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
                // Portable copies may be launched from a non-NTFS drive.
            }
            catch (IOException)
            {
                // Some Windows filesystems do not implement File.Replace.
                // The same-directory move below retains rollback safety.
            }

            string displaced = Path.Combine(
                Path.GetDirectoryName(targetPath),
                StagePrefix + "previous-" +
                Guid.NewGuid().ToString("N") + ".tmp");
            File.Move(targetPath, displaced);
            try
            {
                File.Move(stagePath, targetPath);
                TryDelete(displaced);
            }
            catch
            {
                try
                {
                    if (!File.Exists(targetPath) && File.Exists(displaced))
                        File.Move(displaced, targetPath);
                }
                catch { }
                throw;
            }
        }

        private static void StartUpdatedApplication(string targetPath)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                WorkingDirectory = Path.GetDirectoryName(targetPath),
                UseShellExecute = true
            });
            if (process == null)
                throw new InvalidOperationException(
                    "Windows could not reopen Raid Rescue.");
        }

        private static void WriteStartupStatus(
            bool success, string version, string error)
        {
            try
            {
                string directory = GetUpdateDataDirectory();
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    GetStartupStatusPath(),
                    "Success=" + (success ? "1" : "0") + Environment.NewLine +
                    "Version=" + (version ?? "") + Environment.NewLine +
                    "Error=" + EncodeStatusValue(error ?? "") + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private static string GetUpdateDataDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Raid Rescue", "Updates");
        }

        private static string GetStartupStatusPath()
        {
            return Path.Combine(GetUpdateDataDirectory(), "update-status.ini");
        }

        private static void CleanupStaleFiles()
        {
            try
            {
                string current = Path.GetFullPath(
                    Assembly.GetExecutingAssembly().Location);
                string[] helpers = Directory.GetFiles(
                    Path.GetTempPath(), HelperPrefix + "*.exe");
                foreach (string helper in helpers)
                {
                    if (!String.Equals(
                        Path.GetFullPath(helper), current,
                        StringComparison.OrdinalIgnoreCase))
                        TryDelete(helper);
                }
            }
            catch { }

            try
            {
                string directory = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);
                string[] stages = Directory.GetFiles(
                    directory, StagePrefix + "*.tmp");
                foreach (string stage in stages)
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(stage) <
                            DateTime.UtcNow.AddDays(-1))
                            TryDelete(stage);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static Version CurrentAssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private static bool TryParseReleaseVersion(
            string value, out Version version)
        {
            version = null;
            if (String.IsNullOrWhiteSpace(value))
                return false;
            string clean = value.Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(1);
            Version parsed;
            if (!Version.TryParse(clean, out parsed))
                return false;
            version = NormalizeVersion(parsed);
            return true;
        }

        private static Version NormalizeVersion(Version version)
        {
            if (version == null)
                return new Version(0, 0, 0, 0);
            return new Version(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build),
                Math.Max(0, version.Revision));
        }

        private static int CompareVersions(Version left, Version right)
        {
            return NormalizeVersion(left).CompareTo(NormalizeVersion(right));
        }

        private static string FormatVersion(Version version)
        {
            Version normalized = NormalizeVersion(version);
            return normalized.Major + "." + normalized.Minor + "." +
                normalized.Build;
        }

        private static bool IsOfficialReleaseUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                String.Equals(
                    uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.StartsWith(
                    "/Cooperkit/Raid-Rescue/releases/",
                    StringComparison.Ordinal);
        }

        private static bool IsOfficialDownloadUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                String.Equals(
                    uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.StartsWith(
                    DownloadPathPrefix, StringComparison.Ordinal) &&
                uri.AbsolutePath.EndsWith(
                    "/" + AssetName, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDigest(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "";
            string clean = value.Trim();
            if (clean.StartsWith(
                "sha256:", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(7);
            return clean.ToUpperInvariant();
        }

        private static bool IsSha256(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length != 64)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'A' && c <= 'F') ||
                      (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    text.Append(value.ToString("X2"));
                return text.ToString();
            }
        }

        private static bool HashesEqual(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            int different = 0;
            for (int i = 0; i < left.Length; i++)
                different |= Char.ToUpperInvariant(left[i]) ^
                    Char.ToUpperInvariant(right[i]);
            return different == 0;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "") + "\"";
        }

        private static string FriendlyMessage(Exception exception)
        {
            if (exception == null || String.IsNullOrWhiteSpace(exception.Message))
                return "Try again in a moment.";
            return exception.Message.Trim();
        }

        private static void EnableTls12()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!String.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static string EncodeStatusValue(string value)
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value ?? ""));
        }

        private static string DecodeStatusValue(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(
                    Convert.FromBase64String(value ?? ""));
            }
            catch
            {
                return "";
            }
        }
    }
}
