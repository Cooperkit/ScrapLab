using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ScrapLab.ToolForge
{
    internal static class ToolForgeUtilities
    {
        internal static readonly UTF8Encoding Utf8NoBom =
            new UTF8Encoding(false, true);

        internal static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
        }

        internal static string Serialize(object value)
        {
            return CreateSerializer().Serialize(value);
        }

        internal static string SerializePretty(object value)
        {
            string compact = Serialize(value);
            StringBuilder output = new StringBuilder(compact.Length + 128);
            bool quoted = false;
            bool escaped = false;
            int indent = 0;
            for (int i = 0; i < compact.Length; i++)
            {
                char c = compact[i];
                if (quoted)
                {
                    output.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"')
                {
                    quoted = true;
                    output.Append(c);
                }
                else if (c == '{' || c == '[')
                {
                    output.Append(c).Append('\n');
                    indent++;
                    AppendIndent(output, indent);
                }
                else if (c == '}' || c == ']')
                {
                    output.Append('\n');
                    indent--;
                    AppendIndent(output, indent);
                    output.Append(c);
                }
                else if (c == ',')
                {
                    output.Append(c).Append('\n');
                    AppendIndent(output, indent);
                }
                else if (c == ':') output.Append(": ");
                else output.Append(c);
            }
            return output.ToString();
        }

        private static void AppendIndent(StringBuilder builder, int count)
        {
            for (int i = 0; i < count; i++) builder.Append("  ");
        }

        internal static string Sha256File(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return ToHex(hash.ComputeHash(stream));
        }

        internal static string Sha256Bytes(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return ToHex(hash.ComputeHash(bytes));
        }

        private static string ToHex(byte[] value)
        {
            StringBuilder output = new StringBuilder(value.Length * 2);
            foreach (byte current in value)
                output.Append(current.ToString("X2", CultureInfo.InvariantCulture));
            return output.ToString();
        }

        internal static void WriteTextAtomic(string path, string text)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory,
                "." + Path.GetFileName(path) + ".toolforge-" +
                Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temporary, text, Utf8NoBom);
            try
            {
                if (File.Exists(path))
                {
                    string backup = temporary + ".old";
                    File.Replace(temporary, path, backup, true);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        internal static string SafeName(string value, string fallback)
        {
            string input = String.IsNullOrWhiteSpace(value) ? fallback : value;
            StringBuilder output = new StringBuilder();
            foreach (char c in input)
            {
                if (Char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    output.Append(c);
            }
            return output.Length == 0 ? fallback : output.ToString();
        }

        internal static string ResolveInside(string root, string relative)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "A generated path escaped the selected output folder.");
            return full;
        }

        internal static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        internal static string Number(double value)
        {
            if (Math.Abs(value) < 0.000000001) value = 0.0;
            return value.ToString("0.#########", CultureInfo.InvariantCulture);
        }

        internal static string ToForwardSlashes(string path)
        {
            return (path ?? String.Empty).Replace('\\', '/');
        }

        internal static GameBuildInfo ReadGameBuild(string gameRoot)
        {
            GameBuildInfo info = new GameBuildInfo
            {
                SteamBuildId = String.Empty,
                GameVersion = String.Empty
            };
            try
            {
                string executable = Path.Combine(gameRoot, "Release",
                    "ScrapMechanic.exe");
                if (File.Exists(executable))
                {
                    FileVersionInfo version =
                        FileVersionInfo.GetVersionInfo(executable);
                    info.GameVersion = version.FileVersion ?? String.Empty;
                }
                DirectoryInfo common = Directory.GetParent(gameRoot);
                DirectoryInfo steamApps = common == null ? null : common.Parent;
                string manifest = steamApps == null ? null : Path.Combine(
                    steamApps.FullName, "appmanifest_387990.acf");
                if (manifest != null && File.Exists(manifest))
                {
                    Match match = Regex.Match(File.ReadAllText(manifest),
                        "\\\"buildid\\\"\\s+\\\"([^\\\"]+)\\\"",
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant);
                    if (match.Success) info.SteamBuildId = match.Groups[1].Value;
                }
            }
            catch { }
            return info;
        }

        internal static string FindIntegrationSource(string startDirectory)
        {
            List<string> starts = new List<string>();
            if (!String.IsNullOrWhiteSpace(startDirectory))
                starts.Add(startDirectory);
            starts.Add(Environment.CurrentDirectory);
            starts.Add(AppDomain.CurrentDomain.BaseDirectory);
            foreach (string start in starts)
            {
                DirectoryInfo current;
                try { current = new DirectoryInfo(Path.GetFullPath(start)); }
                catch { continue; }
                for (int i = 0; current != null && i < 8; i++, current = current.Parent)
                {
                    string candidate = Path.Combine(current.FullName, "source",
                        "Patching", "Parts", "TreeSaplings",
                        "TreeSaplingTool.lua");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return String.Empty;
        }
    }
}
