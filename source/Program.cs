using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;
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
            if (GamePatchLauncher.TryRunHelper(args))
                return;

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
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                {
                    if (key != null)
                        key.SetValue(executable, 11001, RegistryValueKind.DWord);
                }
            }
            catch
            {
                // The interface still works with the default browser mode.
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

    internal sealed class MainForm : Form
    {
        private const int WsMaximizeBox = 0x00010000;
        private const int WsThickFrame = 0x00040000;
        private readonly WebBrowser browser;

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
