using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RaidRescue
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (AppUpdateService.TryRunHelper(args) ||
                ElevatedPatchBroker.TryRunHelper(args) ||
                GamePatchLauncher.TryRunHelper(args) ||
                SecretModPatchLauncher.TryRunHelper(args) ||
                ChemicalFertilizerPatchLauncher.TryRunHelper(args) ||
                DualFluidCannonPatchLauncher.TryRunHelper(args) ||
                DeveloperCommandsPatchLauncher.TryRunHelper(args))
                return;

            AppUpdateService.ScheduleCleanup();
            ConfigureBrowserMode();
            GameFonts.TryLoad();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string initialPath = null;
            if (args != null && args.Length > 0)
            {
                try
                {
                    if (File.Exists(args[0]))
                        initialPath = Path.GetFullPath(args[0]);
                }
                catch { }
            }
            Application.Run(new MainForm(initialPath));
        }

        private static void ConfigureBrowserMode()
        {
            try
            {
                string executable = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                SetBrowserFeature(
                    executable,
                    "FEATURE_BROWSER_EMULATION",
                    11001);
                SetBrowserFeature(
                    executable,
                    "FEATURE_GPU_RENDERING",
                    1);
            }
            catch
            {
                // The interface still works with the default browser mode.
            }
        }

        private static void SetBrowserFeature(
            string executable, string feature, int value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Internet Explorer\Main\FeatureControl\" +
                feature))
            {
                if (key != null)
                    key.SetValue(executable, value, RegistryValueKind.DWord);
            }
        }
    }

    internal static class GameFonts
    {
        private const uint PrivateFont = 0x10;

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(
            string fileName, uint flags, IntPtr reserved);

        public static void TryLoad()
        {
            try
            {
                string install = FindInstall();
                if (String.IsNullOrEmpty(install))
                    return;
                string fonts = Path.Combine(install, "Data", "Gui", "Fonts");
                Load(Path.Combine(fonts, "Shentox_Medium.otf"));
                Load(Path.Combine(fonts, "Shentox_SemiBold.otf"));
                Load(Path.Combine(fonts, "Inter_Medium.otf"));
            }
            catch
            {
                // System font fallbacks keep the interface fully usable.
            }
        }

        private static void Load(string path)
        {
            if (File.Exists(path))
                AddFontResourceEx(path, PrivateFont, IntPtr.Zero);
        }

        private static string FindInstall()
        {
            List<string> candidates = new List<string>();
            AddRegistryInstall(candidates, RegistryView.Registry32);
            AddRegistryInstall(candidates, RegistryView.Registry64);

            string programFilesX86 = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);
            string programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(
                programFilesX86, "Steam", "steamapps", "common", "Scrap Mechanic"));
            candidates.Add(Path.Combine(
                programFiles, "Steam", "steamapps", "common", "Scrap Mechanic"));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                    continue;
                candidates.Add(Path.Combine(
                    drive.RootDirectory.FullName,
                    "SteamLibrary", "steamapps", "common", "Scrap Mechanic"));
                candidates.Add(Path.Combine(
                    drive.RootDirectory.FullName,
                    "Steam", "steamapps", "common", "Scrap Mechanic"));
            }

            foreach (string candidate in candidates)
            {
                if (!String.IsNullOrEmpty(candidate) &&
                    File.Exists(Path.Combine(
                        candidate, "Data", "Gui", "Fonts", "Shentox_SemiBold.otf")))
                    return candidate;
            }
            return null;
        }

        private static void AddRegistryInstall(
            List<string> candidates, RegistryView view)
        {
            try
            {
                using (RegistryKey machine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine, view))
                using (RegistryKey app = machine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 387990"))
                {
                    if (app == null)
                        return;
                    string location = app.GetValue("InstallLocation") as string;
                    if (!String.IsNullOrEmpty(location))
                        candidates.Add(location);
                }
            }
            catch { }
        }
    }

    internal static class TutorialPreferences
    {
        private const string CompletedValue = "TutorialVersion=1";
        private static readonly object Sync = new object();

        public static bool ShouldOfferTutorial()
        {
            lock (Sync)
            {
                try
                {
                    string path = GetSettingsPath();
                    if (!File.Exists(path))
                        return true;
                    return !String.Equals(
                        File.ReadAllText(path).Trim(),
                        CompletedValue,
                        StringComparison.Ordinal);
                }
                catch
                {
                    // If the preference cannot be read, offering help is safer
                    // than silently hiding onboarding from a new user.
                    return true;
                }
            }
        }

        public static void CompleteTutorialPrompt()
        {
            WriteValue(CompletedValue);
        }

        public static void ResetTutorialPrompt()
        {
            WriteValue("TutorialVersion=0");
        }

        private static void WriteValue(string value)
        {
            lock (Sync)
            {
                try
                {
                    string path = GetSettingsPath();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, value);
                }
                catch
                {
                    // The UI remains usable even if Windows blocks preferences.
                }
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Raid Rescue",
                "preferences.ini");
        }
    }

    internal static class SecretModPreferences
    {
        private const string EnabledValue = "Enabled=1";
        private static readonly object Sync = new object();

        public static bool GetEnabled()
        {
            lock (Sync)
            {
                try
                {
                    string path = GetSettingsPath();
                    return File.Exists(path) &&
                        String.Equals(
                            File.ReadAllText(path).Trim(),
                            EnabledValue,
                            StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            lock (Sync)
            {
                try
                {
                    string path = GetSettingsPath();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, enabled ? EnabledValue : "Enabled=0");
                }
                catch
                {
                    // Experimental controls safely default to off if preferences
                    // cannot be written.
                }
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Raid Rescue",
                "secret-mods.ini");
        }
    }

    internal sealed class MainForm : Form
    {
        private const int WsMaximizeBox = 0x00010000;
        private const int WsThickFrame = 0x00040000;
        private readonly WebBrowser browser;
        private int updateCheckActive;
        private int updateInstallActive;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.Style &= ~WsMaximizeBox;
                parameters.Style &= ~WsThickFrame;
                return parameters;
            }
        }

        public MainForm(string initialPath)
        {
            Text = "Raid Rescue for Scrap Mechanic";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1080, 760);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(9, 14, 28);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(
                    Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                // The embedded executable icon remains available to Windows.
            }

            browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = true,
                ScriptErrorsSuppressed = true,
                ObjectForScripting = new BrowserBridge(this)
            };
            Controls.Add(browser);
            if (!String.IsNullOrEmpty(initialPath))
            {
                browser.DocumentCompleted += delegate
                {
                    try
                    {
                        browser.Document.InvokeScript("loadPath", new object[] { initialPath });
                    }
                    catch { }
                };
            }
            browser.DocumentText = UiHtml.Content;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing &&
                Interlocked.CompareExchange(
                    ref updateInstallActive, 0, 0) != 0)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        internal bool BeginUpdateCheck(bool manual)
        {
            if (Interlocked.CompareExchange(
                ref updateCheckActive, 1, 0) != 0)
                return false;

            ThreadPool.QueueUserWorkItem(delegate
            {
                AppUpdateResult result = AppUpdateService.CheckForUpdates();
                NotifyUpdateScript(
                    "receiveUpdateCheck", result, manual,
                    delegate
                    {
                        Interlocked.Exchange(ref updateCheckActive, 0);
                    });
            });
            return true;
        }

        internal bool BeginUpdateInstall(
            string assetUrl, string digest, string latestVersion)
        {
            if (Interlocked.CompareExchange(
                ref updateInstallActive, 1, 0) != 0)
                return false;

            ThreadPool.QueueUserWorkItem(delegate
            {
                AppUpdateResult result =
                    AppUpdateService.PrepareAndLaunchUpdate(
                        assetUrl, digest, latestVersion);
                NotifyUpdateScript(
                    "receiveUpdateInstall", result, false,
                    delegate
                    {
                        Interlocked.Exchange(ref updateInstallActive, 0);
                    });
            });
            return true;
        }

        private void NotifyUpdateScript(
            string functionName, object result, bool manual,
            MethodInvoker completed)
        {
            string json;
            try
            {
                json = new JavaScriptSerializer
                {
                    MaxJsonLength = Int32.MaxValue
                }.Serialize(result);
            }
            catch (Exception exception)
            {
                json = "{\"Success\":false,\"Error\":\"" +
                    EscapeJson(exception.Message) + "\"}";
            }

            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                BeginInvoke((MethodInvoker)delegate
                {
                    try
                    {
                        if (browser.Document != null)
                            browser.Document.InvokeScript(
                                functionName,
                                new object[] { json, manual });
                    }
                    catch { }
                    finally
                    {
                        if (completed != null)
                            completed();
                    }
                });
            }
            catch
            {
                if (completed != null)
                    completed();
            }
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
                return String.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public sealed class BrowserBridge
    {
        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;
        private readonly MainForm owner;
        private readonly JavaScriptSerializer serializer;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        internal BrowserBridge(MainForm window)
        {
            owner = window;
            serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
        }

        public void BeginDrag()
        {
            try
            {
                ReleaseCapture();
                SendMessage(
                    owner.Handle, WmNcLButtonDown,
                    new IntPtr(HtCaption), IntPtr.Zero);
            }
            catch { }
        }

        public void Minimize()
        {
            try { owner.WindowState = FormWindowState.Minimized; }
            catch { }
        }

        public void CloseWindow()
        {
            try { owner.Close(); }
            catch { }
        }

        public string Discover()
        {
            return Serialize(RaidService.Discover());
        }

        public string Analyze(string path)
        {
            return Serialize(RaidService.Analyze(path));
        }

        public bool IsGameRunning()
        {
            return RaidService.IsGameRunning();
        }

        public bool ShouldOfferTutorial()
        {
            return TutorialPreferences.ShouldOfferTutorial();
        }

        public void CompleteTutorialPrompt()
        {
            TutorialPreferences.CompleteTutorialPrompt();
        }

        public void ResetTutorialPrompt()
        {
            TutorialPreferences.ResetTutorialPrompt();
        }

        public bool GetSecretModsEnabled()
        {
            return SecretModPreferences.GetEnabled();
        }

        public void SetSecretModsEnabled(bool enabled)
        {
            SecretModPreferences.SetEnabled(enabled);
        }

        public string GetAppVersion()
        {
            return AppUpdateService.CurrentVersion;
        }

        public bool CheckForUpdates(bool manual)
        {
            return owner.BeginUpdateCheck(manual);
        }

        public bool InstallAppUpdate(
            string assetUrl, string digest, string latestVersion)
        {
            return owner.BeginUpdateInstall(
                assetUrl, digest, latestVersion);
        }

        public string ConsumeUpdateStartupStatus()
        {
            return Serialize(AppUpdateService.ConsumeStartupStatus());
        }

        public bool OpenUpdateRelease(string url)
        {
            return AppUpdateService.OpenOfficialRelease(url);
        }

        public string Browse()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose a Scrap Mechanic survival save";
                dialog.Filter = "Scrap Mechanic saves (*.db)|*.db|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.Multiselect = false;

                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string saveRoot = Path.Combine(
                    roaming, "Axolot Games", "Scrap Mechanic", "User");
                if (Directory.Exists(saveRoot))
                    dialog.InitialDirectory = saveRoot;

                return dialog.ShowDialog(owner) == DialogResult.OK
                    ? dialog.FileName
                    : String.Empty;
            }
        }

        public string ClearRaids(string path)
        {
            DialogResult answer = MessageBox.Show(
                owner,
                "Raid Rescue will first create and verify a timestamped backup beside the save.\r\n\r\n" +
                "It will then remove the saved raid-manager state. Inventories, builds, quests, " +
                "players, and other world data are not edited.\r\n\r\n" +
                "Scrap Mechanic must be completely closed.\r\n\r\nContinue?",
                "Clear every stored raid?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return Serialize(new RepairResult
                {
                    Success = false,
                    Cancelled = true,
                    Path = path
                });
            }
            return Serialize(RaidService.ClearRaids(path));
        }

        public string InstallRaidHotfix()
        {
            return Serialize(GamePatchLauncher.Install());
        }

        public string GetResourceLocatorModStatus()
        {
            return Serialize(SecretModPatchService.GetStatus());
        }

        public string SetResourceLocatorMod(bool enabled)
        {
            return Serialize(SecretModPatchLauncher.SetEnabled(enabled));
        }

        public string GetChemicalFertilizerModStatus()
        {
            return Serialize(ChemicalFertilizerPatchService.GetStatus());
        }

        public string SetChemicalFertilizerMod(bool enabled)
        {
            return Serialize(DualFluidCannonPatchLauncher.SetChemicalEnabled(enabled));
        }

        public string GetDualFluidCannonModStatus()
        {
            return Serialize(DualFluidCannonPatchService.GetStatus());
        }

        public string SetDualFluidCannonMod(bool enabled)
        {
            return Serialize(DualFluidCannonPatchLauncher.SetCannonEnabled(enabled));
        }

        public string GetDeveloperCommandsModStatus()
        {
            return Serialize(DeveloperCommandsPatchService.GetStatus());
        }

        public string SetDeveloperCommandsMod(bool enabled, string mode)
        {
            return Serialize(DeveloperCommandsPatchLauncher.SetEnabled(enabled, mode));
        }

        public void OpenFolder(string path)
        {
            try
            {
                string directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    owner, exception.Message, "Could not open folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string Serialize(object value)
        {
            try
            {
                return serializer.Serialize(value);
            }
            catch (Exception exception)
            {
                return "{\"Success\":false,\"Error\":\"" +
                    EscapeJson(exception.Message) + "\"}";
            }
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
                return String.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
