using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ScrapLab.ToolForge
{
    internal sealed class ToolForgeMainForm : Form
    {
        private const int WmNcButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message,
            IntPtr wParam, IntPtr lParam);

        private readonly WebView2 _webView;
        private readonly Label _fallback;
        private readonly JavaScriptSerializer _serializer;
        private ToolForgeProject _project;
        private string _projectPath;
        private string _lastBuiltPackage;
        private string _appHost;
        private string _mappedGameRoot;
        private string _mappedSourceRoot;
        private bool _ready;

        internal ToolForgeMainForm(string startupProjectPath)
        {
            Text = "ScrapLab Tool Forge";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1440, 900);
            MinimumSize = new Size(1160, 700);
            BackColor = Color.FromArgb(12, 17, 19);
            FormBorderStyle = FormBorderStyle.None;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            _serializer = ToolForgeUtilities.CreateSerializer();
            _projectPath = String.Empty;
            _lastBuiltPackage = String.Empty;
            _appHost = "app.toolforge";
            _mappedGameRoot = String.Empty;
            _mappedSourceRoot = String.Empty;
            if (!String.IsNullOrWhiteSpace(startupProjectPath))
            {
                _projectPath = Path.GetFullPath(startupProjectPath);
                _project = ToolForgeProjectService.Load(_projectPath);
            }
            _webView = new WebView2 { Dock = DockStyle.Fill };
            _fallback = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(255, 195, 45),
                BackColor = Color.FromArgb(18, 24, 26),
                Font = new Font("Segoe UI", 12.0f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "STARTING SCRAPLAB TOOL FORGE..."
            };
            Controls.Add(_webView);
            Controls.Add(_fallback);
            Shown += delegate { BeginInitialize(); };
        }

        private async void BeginInitialize()
        {
            try
            {
                string data = Environment.GetEnvironmentVariable(
                    "SCRAPLAB_TOOLFORGE_WEBVIEW_DATA");
                if (String.IsNullOrWhiteSpace(data))
                    data = Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                        "ScrapLab", "ToolForge", "WebView2");
                data = Path.GetFullPath(data);
                Directory.CreateDirectory(data);
                CoreWebView2EnvironmentOptions options = null;
                string debugPort = Environment.GetEnvironmentVariable(
                    "SCRAPLAB_TOOLFORGE_DEBUG_PORT");
                int parsedPort;
                if (Int32.TryParse(debugPort, out parsedPort) &&
                    parsedPort >= 1024 && parsedPort <= 65535)
                    options = new CoreWebView2EnvironmentOptions(
                        "--remote-debugging-port=" + parsedPort);
                CoreWebView2Environment environment = await
                    CoreWebView2Environment.CreateAsync(null, data, options);
                await _webView.EnsureCoreWebView2Async(environment);
                ConfigureWebView();
                SetMappings();
                _fallback.Visible = false;
                _webView.Visible = true;
            }
            catch (Exception ex)
            {
                _fallback.Visible = true;
                _fallback.Text = "WEBVIEW2 COULD NOT START\r\n\r\n" +
                    ex.Message + "\r\n\r\nInstall or repair the Microsoft Edge WebView2 Runtime.";
            }
        }

        private void ConfigureWebView()
        {
            string webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Web");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new FileNotFoundException(
                    "Tool Forge's local interface files are missing.",
                    Path.Combine(webRoot, "index.html"));
            string indexPath = Path.Combine(webRoot, "index.html");
            string appPath = Path.Combine(webRoot, "app.js");
            string previewPath = Path.Combine(webRoot, "preview.js");
            if (!File.Exists(appPath) || !File.Exists(previewPath))
                throw new FileNotFoundException(
                    "Tool Forge's local preview scripts are missing.");
            string stamp = (ToolForgeUtilities.Sha256File(indexPath) +
                ToolForgeUtilities.Sha256File(appPath) +
                ToolForgeUtilities.Sha256File(previewPath)).Substring(0, 16)
                .ToLowerInvariant();
            _appHost = "app-" + stamp + ".toolforge";
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                _appHost, webRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
            _webView.CoreWebView2.NavigationStarting += delegate(
                object sender, CoreWebView2NavigationStartingEventArgs args)
            {
                Uri uri;
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out uri) ||
                    (!String.Equals(uri.Host, _appHost,
                        StringComparison.OrdinalIgnoreCase) &&
                     !String.Equals(uri.Host, "game.toolforge",
                        StringComparison.OrdinalIgnoreCase) &&
                     !String.Equals(uri.Host, "source.toolforge",
                        StringComparison.OrdinalIgnoreCase)))
                    args.Cancel = true;
            };
            _webView.CoreWebView2.NewWindowRequested += delegate(
                object sender, CoreWebView2NewWindowRequestedEventArgs args)
            {
                args.Handled = true;
            };
            _webView.Source = new Uri("https://" + _appHost + "/index.html");
        }

        private void OnWebMessage(object sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                Dictionary<string, object> message = _serializer
                    .Deserialize<Dictionary<string, object>>(
                        args.WebMessageAsJson);
                string type = Value(message, "type");
                if (type == "ready")
                {
                    _ready = true;
                    SendState();
                }
                else if (type == "newProject") CreateProject();
                else if (type == "openProject") OpenProject();
                else if (type == "saveProject") SaveFromMessage(message,
                    !String.Equals(Value(message, "silent"), "true",
                        StringComparison.OrdinalIgnoreCase));
                else if (type == "validate") ValidateFromMessage(message);
                else if (type == "build") BuildFromMessage(message);
                else if (type == "pickGame") PickGameDirectory();
                else if (type == "pickIntegration") PickIntegrationSource();
                else if (type == "pickOutput") PickOutputDirectory();
                else if (type == "reveal") RevealLastBuild();
                else if (type == "previewStatus") LogPreviewStatus(message);
                else if (type == "window") HandleWindow(Value(message, "action"));
            }
            catch (Exception ex)
            {
                Send(new { type = "operation", ok = false,
                    title = "OPERATION FAILED", message = ex.Message });
            }
        }

        private void CreateProject()
        {
            using (OpenFileDialog mesh = new OpenFileDialog())
            {
                mesh.Title = "Select the modified FBX 7.x model";
                mesh.Filter = "FBX model (*.fbx)|*.fbx|All files (*.*)|*.*";
                if (mesh.ShowDialog(this) != DialogResult.OK) return;
                using (SaveFileDialog project = new SaveFileDialog())
                {
                    project.Title = "Create the Tool Forge project";
                    project.Filter = "Tool Forge project (*.scraptool.json)|*.scraptool.json";
                    project.FileName = ToolForgeProjectService.ManifestFileName;
                    if (project.ShowDialog(this) != DialogResult.OK) return;
                    string game = RaidRescue.GameInstallLocator.Find() ?? String.Empty;
                    _projectPath = Path.GetFullPath(project.FileName);
                    _project = ToolForgeProjectService.CreateTreeSaplingsProject(
                        mesh.FileName, _projectPath, game);
                    RefreshAfterMappingChange(SetMappings());
                    Send(new { type = "operation", ok = true,
                        title = "PROJECT CREATED",
                        message = "The source FBX was copied and checksum-locked." });
                }
            }
        }

        private void OpenProject()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Open a Tool Forge project";
                dialog.Filter = "Tool Forge project (*.scraptool.json)|*.scraptool.json|JSON files (*.json)|*.json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _projectPath = Path.GetFullPath(dialog.FileName);
                _project = ToolForgeProjectService.Load(_projectPath);
                RefreshAfterMappingChange(SetMappings());
                Send(new { type = "operation", ok = true,
                    title = "PROJECT OPEN",
                    message = "The mesh hash and game assets are ready to validate." });
            }
        }

        private void SaveFromMessage(Dictionary<string, object> message,
            bool notify)
        {
            RequireProject();
            string json = Value(message, "projectJson");
            ToolForgeProject updated = _serializer
                .Deserialize<ToolForgeProject>(json);
            if (updated == null)
                throw new InvalidDataException("The editor returned an empty project.");
            updated.Normalize();
            _project = updated;
            ToolForgeProjectService.Save(_projectPath, _project);
            if (notify)
                Send(new { type = "operation", ok = true,
                    title = "PROJECT SAVED",
                    message = "Transform and preview settings were saved." });
        }

        private void ValidateFromMessage(Dictionary<string, object> message)
        {
            SaveFromMessage(message, false);
            ValidationReport report = ToolForgeValidator.Validate(_project,
                _projectPath, false);
            Send(new { type = "validation", report = report });
        }

        private void BuildFromMessage(Dictionary<string, object> message)
        {
            SaveFromMessage(message, false);
            if (String.IsNullOrWhiteSpace(_project.Output.BaseDirectory))
            {
                if (!ChooseOutput()) return;
                ToolForgeProjectService.Save(_projectPath, _project);
                SendState();
            }
            Cursor = Cursors.WaitCursor;
            Send(new { type = "busy", busy = true,
                message = "VALIDATING AND BUILDING PACKAGE" });
            try
            {
                ToolForgeBuildResult result = SaplingPackageBuilder.Build(
                    _project, _projectPath, _project.Output.BaseDirectory);
                if (result.Success) _lastBuiltPackage = result.PackagePath;
                Send(new { type = "buildResult", result = result });
            }
            finally
            {
                Cursor = Cursors.Default;
                Send(new { type = "busy", busy = false, message = String.Empty });
            }
        }

        private void PickGameDirectory()
        {
            RequireProject();
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the Scrap Mechanic installation folder";
                dialog.SelectedPath = _project.GameRoot;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _project.GameRoot = Path.GetFullPath(dialog.SelectedPath);
                ToolForgeProjectService.Save(_projectPath, _project);
                RefreshAfterMappingChange(SetMappings());
            }
        }

        private void PickIntegrationSource()
        {
            RequireProject();
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select ScrapLab's current TreeSaplingTool.lua";
                dialog.Filter = "Lua source (*.lua)|*.lua|All files (*.*)|*.*";
                dialog.FileName = _project.IntegrationSourcePath;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _project.IntegrationSourcePath = Path.GetFullPath(dialog.FileName);
                ToolForgeProjectService.Save(_projectPath, _project);
                SendState();
            }
        }

        private void PickOutputDirectory()
        {
            RequireProject();
            if (!ChooseOutput()) return;
            ToolForgeProjectService.Save(_projectPath, _project);
            SendState();
        }

        private bool ChooseOutput()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the base folder for generated packages";
                dialog.SelectedPath = _project.Output.BaseDirectory;
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                _project.Output.BaseDirectory = Path.GetFullPath(dialog.SelectedPath);
                return true;
            }
        }

        private bool SetMappings()
        {
            if (_webView.CoreWebView2 == null || _project == null) return false;
            bool changed = false;
            if (!String.IsNullOrWhiteSpace(_project.GameRoot) &&
                Directory.Exists(_project.GameRoot))
            {
                string gameRoot = Path.GetFullPath(_project.GameRoot);
                if (!String.Equals(gameRoot, _mappedGameRoot,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "game.toolforge", gameRoot,
                        CoreWebView2HostResourceAccessKind.Allow);
                    _mappedGameRoot = gameRoot;
                    changed = true;
                }
            }
            string source = ToolForgeProjectService.ResolveSourcePath(_project,
                _projectPath);
            if (File.Exists(source))
            {
                string sourceRoot = Path.GetFullPath(
                    Path.GetDirectoryName(source));
                if (!String.Equals(sourceRoot, _mappedSourceRoot,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "source.toolforge", sourceRoot,
                        CoreWebView2HostResourceAccessKind.Allow);
                    _mappedSourceRoot = sourceRoot;
                    changed = true;
                }
            }
            return changed;
        }

        private void RefreshAfterMappingChange(bool changed)
        {
            if (!changed)
            {
                SendState();
                return;
            }

            // A WebView2 host-folder remap can race an immediate fetch from an
            // already-loaded document. Reloading makes the new mapping active
            // before the editor requests the character rig or source FBX.
            _ready = false;
            _webView.Reload();
        }

        private void SendState()
        {
            if (!_ready || _webView.CoreWebView2 == null) return;
            string sourceUrl = String.Empty;
            PreviewAssets preview = null;
            ToolPreviewGeometry previewGeometry = null;
            string previewError = String.Empty;
            if (_project != null)
            {
                string source = ToolForgeProjectService.ResolveSourcePath(
                    _project, _projectPath);
                if (File.Exists(source))
                    sourceUrl = "https://source.toolforge/" +
                        Uri.EscapeDataString(Path.GetFileName(source));
                try
                {
                    if (Directory.Exists(_project.GameRoot))
                        preview = ScrapMechanicPreviewAssets.Create(
                            _project.GameRoot);
                    FbxDocument document = FbxDocument.Load(source);
                    previewGeometry = ColladaHeldToolGenerator
                        .CreatePreviewGeometry(document,
                            _project.SourceMesh.ModelName);
                }
                catch (Exception ex) { previewError = ex.Message; }
            }
            Send(new { type = "state", project = _project,
                projectPath = _projectPath, sourceUrl = sourceUrl,
                preview = preview, previewGeometry = previewGeometry,
                previewError = previewError,
                lastBuiltPackage = _lastBuiltPackage });
        }

        private void RevealLastBuild()
        {
            if (String.IsNullOrWhiteSpace(_lastBuiltPackage) ||
                !Directory.Exists(_lastBuiltPackage))
                throw new InvalidOperationException(
                    "Build a package before opening its folder.");
            Process.Start("explorer.exe", "/select,\"" + _lastBuiltPackage + "\"");
        }

        private static void LogPreviewStatus(
            Dictionary<string, object> message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "ScrapLab", "ToolForge");
                Directory.CreateDirectory(directory);
                string line = DateTime.UtcNow.ToString("o") + " loaded=" +
                    Value(message, "loaded") + " rig=" +
                    Value(message, "rig") + " joint=" +
                    Value(message, "joint") + " message=" +
                    Value(message, "error") + Environment.NewLine;
                File.AppendAllText(Path.Combine(directory,
                    "preview.log"), line, ToolForgeUtilities.Utf8NoBom);
            }
            catch { }
        }

        private void HandleWindow(string action)
        {
            if (action == "close") Close();
            else if (action == "minimize") WindowState = FormWindowState.Minimized;
            else if (action == "maximize")
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            else if (action == "drag")
            {
                ReleaseCapture();
                SendMessage(Handle, WmNcButtonDown,
                    new IntPtr(HtCaption), IntPtr.Zero);
            }
        }

        private void Send(object value)
        {
            if (_webView.CoreWebView2 == null) return;
            _webView.CoreWebView2.PostWebMessageAsJson(
                _serializer.Serialize(value));
        }

        private static string Value(Dictionary<string, object> value,
            string key)
        {
            object result;
            return value != null && value.TryGetValue(key, out result) &&
                result != null ? Convert.ToString(result) : String.Empty;
        }

        private void RequireProject()
        {
            if (_project == null || String.IsNullOrWhiteSpace(_projectPath))
                throw new InvalidOperationException(
                    "Create or open a Tool Forge project first.");
        }
    }
}
