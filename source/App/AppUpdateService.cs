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
        public string PatchAssetUrl { get; set; }
        public string PatchAssetDigest { get; set; }
        public long PatchAssetSize { get; set; }
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
            "https://api.github.com/repos/Cooperkit/ScrapLab/releases/latest";
        private const string ReleasePrefix =
            "https://github.com/Cooperkit/ScrapLab/releases/";
        private const string LegacyDownloadPathPrefix =
            "/Cooperkit/Raid-Rescue/releases/download/";
        private const string ScrapLabDownloadPathPrefix =
            "/Cooperkit/ScrapLab/releases/download/";
        private const string LegacyReleasePathPrefix =
            "/Cooperkit/Raid-Rescue/releases";
        private const string ScrapLabReleasePathPrefix =
            "/Cooperkit/ScrapLab/releases";
        private const string MainAssetName = "ScrapLab.exe";
        private const string PatchAssetName = "ScrapLab.PatchHelper.exe";
        private const string UpdaterFileName = "ScrapLab.Updater.exe";
        private const string UpdaterProduct =
            "ScrapLab Updater for Scrap Mechanic";
        private const string StagePrefix = ".ScrapLab.Update.";
        private static Timer cleanupTimer;

        public static string CurrentVersion
        {
            get
            {
                return FormatVersion(
                    Assembly.GetExecutingAssembly().GetName().Version);
            }
        }

        public static AppUpdateResult CheckForUpdates()
        {
            AppUpdateResult result = NewResult();
            try
            {
                EnableTls12();
                string json;
                using (TimedWebClient client = CreateWebClient())
                    json = client.DownloadString(LatestReleaseApi);
                GitHubRelease release =
                    Serializer().Deserialize<GitHubRelease>(json);
                if (release == null || release.draft || release.prerelease)
                    throw new InvalidDataException(
                        "GitHub did not return a stable ScrapLab release.");

                Version latest;
                if (!TryParseReleaseVersion(release.tag_name, out latest))
                    throw new InvalidDataException(
                        "The latest GitHub release has an unreadable version.");
                if (!IsOfficialReleaseUrl(release.html_url))
                    throw new InvalidDataException(
                        "GitHub returned an unexpected release address.");

                result.TagName = release.tag_name ?? "";
                result.LatestVersion = FormatVersion(latest);
                result.ReleaseUrl = release.html_url;
                result.UpdateAvailable =
                    CompareVersions(latest, CurrentAssemblyVersion()) > 0;
                result.Success = true;

                GitHubReleaseAsset main =
                    FindReleaseAsset(release.assets, MainAssetName);
                GitHubReleaseAsset patch =
                    FindReleaseAsset(release.assets, PatchAssetName);
                if (main != null)
                {
                    result.AssetUrl = main.browser_download_url ?? "";
                    result.AssetDigest = NormalizeDigest(main.digest);
                    result.AssetSize = main.size;
                }
                if (patch != null)
                {
                    result.PatchAssetUrl =
                        patch.browser_download_url ?? "";
                    result.PatchAssetDigest =
                        NormalizeDigest(patch.digest);
                    result.PatchAssetSize = patch.size;
                }

                result.CanAutoUpdate =
                    IsOfficialDownloadUrl(result.AssetUrl) &&
                    IsSha256(result.AssetDigest) &&
                    result.AssetSize > 0 &&
                    IsOfficialDownloadUrl(result.PatchAssetUrl) &&
                    IsSha256(result.PatchAssetDigest) &&
                    result.PatchAssetSize > 0 &&
                    HasValidUpdater();

                if (result.UpdateAvailable && !result.CanAutoUpdate)
                {
                    result.Error =
                        "This release needs the complete verified Windows bundle. " +
                        "Open GitHub and keep ScrapLab.exe with both companion programs.";
                }
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error =
                    "ScrapLab could not reach GitHub right now. " +
                    FriendlyMessage(exception);
                return result;
            }
        }

        public static AppUpdateResult PrepareAndLaunchUpdate(
            string assetUrl,
            string digest,
            string patchAssetUrl,
            string patchDigest,
            string latestVersion)
        {
            AppUpdateResult result = NewResult();
            result.LatestVersion = latestVersion ?? "";
            result.AssetUrl = assetUrl ?? "";
            result.AssetDigest = NormalizeDigest(digest);
            result.PatchAssetUrl = patchAssetUrl ?? "";
            result.PatchAssetDigest = NormalizeDigest(patchDigest);
            string mainStage = null;
            string patchStage = null;

            try
            {
                Version expected;
                if (!TryParseReleaseVersion(latestVersion, out expected) ||
                    CompareVersions(expected, CurrentAssemblyVersion()) <= 0)
                    throw new InvalidDataException(
                        "The selected release is not newer than this copy.");
                ValidateAsset(result.AssetUrl, result.AssetDigest);
                ValidateAsset(
                    result.PatchAssetUrl, result.PatchAssetDigest);

                string targetMain = Path.GetFullPath(
                    Assembly.GetExecutingAssembly().Location);
                string directory = Path.GetDirectoryName(targetMain);
                string targetPatch = Path.Combine(
                    directory, PatchHelperProtocol.HelperFileName);
                if (!File.Exists(targetPatch))
                    throw new FileNotFoundException(
                        "The patch companion is missing. Install the complete release bundle.",
                        targetPatch);
                string updater = Path.Combine(
                    directory, UpdaterFileName);
                CompanionSecurity.ValidateCompanion(
                    updater, UpdaterProduct, false);

                string suffix = Guid.NewGuid().ToString("N");
                mainStage = Path.Combine(
                    directory, StagePrefix + "Main." + suffix + ".tmp");
                patchStage = Path.Combine(
                    directory, StagePrefix + "Patch." + suffix + ".tmp");

                EnableTls12();
                using (TimedWebClient client = CreateWebClient())
                {
                    client.DownloadFile(result.AssetUrl, mainStage);
                    client.DownloadFile(result.PatchAssetUrl, patchStage);
                }
                VerifyDownloadedExecutable(
                    mainStage, result.AssetDigest, expected,
                    "ScrapLab Survival World Toolkit");
                VerifyDownloadedExecutable(
                    patchStage, result.PatchAssetDigest, expected,
                    "ScrapLab Patch Helper for Scrap Mechanic");
                CompanionSecurity.RequireMatchingSignerWhenSigned(
                    targetMain, mainStage);
                CompanionSecurity.RequireMatchingSignerWhenSigned(
                    targetMain, patchStage);

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = updater,
                    UseShellExecute = false,
                    WorkingDirectory = directory,
                    CreateNoWindow = true,
                    Arguments =
                        "--apply-update " +
                        Process.GetCurrentProcess().Id.ToString() + " " +
                        QuoteArgument(mainStage) + " " +
                        QuoteArgument(targetMain) + " " +
                        QuoteArgument(result.AssetDigest) + " " +
                        QuoteArgument(patchStage) + " " +
                        QuoteArgument(targetPatch) + " " +
                        QuoteArgument(result.PatchAssetDigest) + " " +
                        QuoteArgument(FormatVersion(expected))
                };
                Process helper = Process.Start(start);
                if (helper == null)
                    throw new InvalidOperationException(
                        "Windows could not start the fixed update helper.");

                result.Success = true;
                result.ReadyToRestart = true;
                return result;
            }
            catch (Exception exception)
            {
                TryDelete(mainStage);
                TryDelete(patchStage);
                result.Success = false;
                result.Error =
                    "The update was not installed. " +
                    FriendlyMessage(exception);
                return result;
            }
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
                    if (String.Equals(
                        key, "Success", StringComparison.OrdinalIgnoreCase))
                        result.Success = value == "1";
                    else if (String.Equals(
                        key, "Version", StringComparison.OrdinalIgnoreCase))
                        result.Version = value;
                    else if (String.Equals(
                        key, "Error", StringComparison.OrdinalIgnoreCase))
                        result.Error = DecodeStatusValue(value);
                }
                result.HasStatus = true;
            }
            catch { }
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
            catch { return false; }
        }

        private static AppUpdateResult NewResult()
        {
            return new AppUpdateResult
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = "",
                TagName = "",
                ReleaseUrl = ReleasePrefix + "latest",
                AssetUrl = "",
                AssetDigest = "",
                PatchAssetUrl = "",
                PatchAssetDigest = ""
            };
        }

        private static bool HasValidUpdater()
        {
            try
            {
                CompanionSecurity.ValidateCompanion(
                    CompanionSecurity.GetSibling(UpdaterFileName),
                    UpdaterProduct, false);
                return true;
            }
            catch { return false; }
        }

        private static void ValidateAsset(string url, string digest)
        {
            if (!IsOfficialDownloadUrl(url))
                throw new InvalidDataException(
                    "The update download is not an official ScrapLab asset.");
            if (!IsSha256(digest))
                throw new InvalidDataException(
                    "GitHub did not provide a valid SHA-256 digest.");
        }

        private static TimedWebClient CreateWebClient()
        {
            TimedWebClient client = new TimedWebClient();
            client.Headers[HttpRequestHeader.UserAgent] =
                "ScrapLab/" + CurrentVersion;
            client.Headers[HttpRequestHeader.Accept] =
                "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            return client;
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = 1024 * 1024
            };
        }

        private static GitHubReleaseAsset FindReleaseAsset(
            List<GitHubReleaseAsset> assets, string expectedName)
        {
            if (assets == null)
                return null;
            foreach (GitHubReleaseAsset asset in assets)
            {
                if (asset != null &&
                    String.Equals(
                        asset.name, expectedName,
                        StringComparison.OrdinalIgnoreCase) &&
                    (String.IsNullOrEmpty(asset.state) ||
                     String.Equals(
                        asset.state, "uploaded",
                        StringComparison.OrdinalIgnoreCase)))
                    return asset;
            }
            return null;
        }

        internal static void VerifyDownloadedExecutable(
            string path,
            string digest,
            Version expected,
            string expectedProduct)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length < 50000 ||
                file.Length > 12 * 1024 * 1024)
                throw new InvalidDataException(
                    "A downloaded executable has an unexpected size.");
            if (!HashesEqual(digest, ComputeSha256(path)))
                throw new InvalidDataException(
                    "A downloaded executable does not match GitHub's SHA-256 digest.");
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            Version downloaded;
            if (!Version.TryParse(info.FileVersion, out downloaded) ||
                CompareVersions(downloaded, expected) != 0)
                throw new InvalidDataException(
                    "A downloaded executable version does not match the release.");
            if (!String.Equals(
                info.ProductName, expectedProduct, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "A downloaded file has an unexpected product identity.");
        }

        internal static bool IsOfficialDownloadUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                String.Equals(
                    uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) &&
                (uri.AbsolutePath.StartsWith(
                    LegacyDownloadPathPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                 uri.AbsolutePath.StartsWith(
                    ScrapLabDownloadPathPrefix,
                    StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsOfficialReleaseUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                String.Equals(
                    uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) &&
                (uri.AbsolutePath.StartsWith(
                    LegacyReleasePathPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                 uri.AbsolutePath.StartsWith(
                    ScrapLabReleasePathPrefix,
                    StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsSha256(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length != 64)
                return false;
            foreach (char character in value)
            {
                if (!Uri.IsHexDigit(character))
                    return false;
            }
            return true;
        }

        internal static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
            {
                StringBuilder result = new StringBuilder(64);
                foreach (byte value in algorithm.ComputeHash(stream))
                    result.Append(value.ToString("X2"));
                return result.ToString();
            }
        }

        private static string NormalizeDigest(string value)
        {
            string digest = (value ?? "").Trim();
            int split = digest.IndexOf(':');
            if (split >= 0)
            {
                if (!String.Equals(
                    digest.Substring(0, split), "sha256",
                    StringComparison.OrdinalIgnoreCase))
                    return "";
                digest = digest.Substring(split + 1);
            }
            return digest.ToUpperInvariant();
        }

        private static bool HashesEqual(string left, string right)
        {
            return String.Equals(
                left, right, StringComparison.OrdinalIgnoreCase);
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
            return new Version(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build),
                Math.Max(0, version.Revision));
        }

        private static int CompareVersions(Version left, Version right)
        {
            return NormalizeVersion(left).CompareTo(
                NormalizeVersion(right));
        }

        private static string FormatVersion(Version version)
        {
            Version normalized = NormalizeVersion(version);
            return normalized.Major + "." +
                normalized.Minor + "." + normalized.Build;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static void EnableTls12()
        {
            ServicePointManager.SecurityProtocol |=
                (SecurityProtocolType)3072;
        }

        private static string FriendlyMessage(Exception exception)
        {
            if (exception == null)
                return "An unknown error occurred.";
            WebException web = exception as WebException;
            if (web != null && web.Response is HttpWebResponse)
            {
                return "GitHub returned HTTP " +
                    ((int)((HttpWebResponse)web.Response).StatusCode) + ".";
            }
            return exception.Message;
        }

        private static string GetStartupStatusPath()
        {
            return ProductPaths.LocalDataPath(
                "Updates", "update-status.ini");
        }

        private static string DecodeStatusValue(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(
                    Convert.FromBase64String(value ?? ""));
            }
            catch { return ""; }
        }

        private static void CleanupStaleFiles()
        {
            try
            {
                string directory = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);
                foreach (string stage in Directory.GetFiles(
                    directory, StagePrefix + "*.tmp"))
                {
                    if (File.GetLastWriteTimeUtc(stage) <
                        DateTime.UtcNow.AddDays(-1))
                        TryDelete(stage);
                }
            }
            catch { }
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
    }
}
