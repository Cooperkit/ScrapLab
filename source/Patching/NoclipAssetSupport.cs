using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace RaidRescue
{
    internal static class NoclipAssetSupport
    {
        internal static readonly string ToolsRelativePath = Path.Combine(
            "Survival", "Tools", "ToolSets", "tools.json");
        internal static readonly string ModuleRelativePath = Path.Combine(
            "Survival", "Scripts", "ScrapLab", "Noclip.lua");
        internal static readonly string InputToolRelativePath = Path.Combine(
            "Survival", "Scripts", "ScrapLab", "NoclipInputTool.lua");

        private const string ToolUuid =
            "79f8b9c7-7738-4cf8-94ee-8aeb5cc45d3d";
        private const string LegacyV4ModuleHash =
            "FF5D42E9070BC1D9272E395E0EC9F7EFCF9B30D565687DDA7631DF61EEAEED37";
        private const string LegacyV5ModuleHash =
            "770484BCB555F17748E98E2F3EF85D1D987F3987466C8245ED7FDD86621A7E00";
        private const string LegacyV6ModuleHash =
            "AA1E06A6FF92A1AC773230C5CB094318F4C33DC7D19059467A5B0DCBCF27B9AC";
        private const string LegacyInputToolHash =
            "92B9D5559244E5DF68A690EFC7B6D5BA0BC3266410BA253ABA1BDBD3979D9795";
        private const string CleanTail =
            "\n\t\t}\n\t]\n}\n//[ 1, 0, 0, 0, 0, -1, 0, 1, 0] points to right down";
        private static readonly string InstalledEntry =
            String.Join("\n", new[]
            {
                "\t\t{",
                "\t\t\t\"autoTool\" : true,",
                "\t\t\t\"previewRenderable\" : \"$GAME_DATA/Character/Char_Tools/char_lift_preview.rend\",",
                "\t\t\t\"previewRotation\" : [ 1, 0, 0, 0, 0, -1, 0, 1, 0 ],",
                "\t\t\t\"script\" :",
                "\t\t\t{",
                "\t\t\t\t\"class\" : \"ScrapLabNoclipInputTool\",",
                "\t\t\t\t\"file\" : \"$SURVIVAL_DATA/Scripts/ScrapLab/NoclipInputTool.lua\"",
                "\t\t\t},",
                "\t\t\t\"showInInventory\" : false,",
                "\t\t\t\"uuid\" : \"" + ToolUuid + "\"",
                "\t\t}"
            });
        private static readonly string InstalledTail =
            "\n\t\t},\n" + InstalledEntry + "\n" + String.Join("\n", new[]
            {
                "\t]",
                "}",
                "//[ 1, 0, 0, 0, 0, -1, 0, 1, 0] points to right down"
            });

        internal static bool IsInstalled(string gamePath, out string reason)
        {
            try
            {
                AssetState state = ReadState(gamePath);
                if (!state.ToolsInstalled)
                {
                    reason = state.ToolsClean
                        ? "ScrapLab's hidden noclip input tool is not registered."
                        : "tools.json contains a partial or conflicting ScrapLab input-tool entry.";
                    return false;
                }
                if (!state.ModuleExact)
                {
                    reason = state.ModuleOwned
                        ? "A verified older flight controller is installed and ready to upgrade."
                        : "Scripts/ScrapLab/Noclip.lua is missing or edited.";
                    return false;
                }
                if (!state.InputToolExact)
                {
                    reason = state.InputToolOwned
                        ? "A verified older flight input tool is installed and ready to upgrade."
                        : "Scripts/ScrapLab/NoclipInputTool.lua is missing or edited.";
                    return false;
                }
                reason = "ScrapLab's isolated noclip modules and hidden input tool are verified.";
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        internal static bool CanApply(string gamePath, out string reason)
        {
            try
            {
                AssetState state = ReadState(gamePath);
                if (!state.ToolsClean && !state.ToolsInstalled)
                {
                    reason = "tools.json contains a partial or conflicting ScrapLab input-tool entry.";
                    return false;
                }
                if ((state.ModuleExists && !state.ModuleOwned) ||
                    (state.InputToolExists && !state.InputToolOwned))
                {
                    reason = "Scripts/ScrapLab contains an unknown or edited noclip module.";
                    return false;
                }
                reason = "ScrapLab's isolated noclip assets can be installed safely.";
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        internal static NoclipAssetTransaction Prepare(
            string gamePath, string backupRoot, bool enabled)
        {
            AssetState state = ReadState(gamePath);
            if (!state.ToolsClean && !state.ToolsInstalled)
                throw new InvalidDataException(
                    "tools.json contains a partial, duplicated, or edited ScrapLab noclip input-tool entry " +
                    "(clean=" + state.ToolsClean + ", installed=" + state.ToolsInstalled + ").");
            if (state.ModuleExists && !state.ModuleOwned)
                throw new InvalidDataException(
                    "Scripts/ScrapLab/Noclip.lua already exists with unknown contents.");
            if (state.InputToolExists && !state.InputToolOwned)
                throw new InvalidDataException(
                    "Scripts/ScrapLab/NoclipInputTool.lua already exists with unknown contents.");

            return new NoclipAssetTransaction(
                gamePath, backupRoot, enabled, state,
                GetResource("RaidRescue.ScrapLabNoclip.lua"),
                GetResource("RaidRescue.ScrapLabNoclipInputTool.lua"));
        }

        private static AssetState ReadState(string gamePath)
        {
            string toolsPath = Path.Combine(gamePath, ToolsRelativePath);
            if (!File.Exists(toolsPath))
                throw new FileNotFoundException("Survival tools.json was not found.", toolsPath);

            byte[] toolsBytes = File.ReadAllBytes(toolsPath);
            TextFile tools = TextFile.Read(toolsBytes, "tools.json");
            int cleanCount = Count(tools.Normalized, CleanTail);
            int installedCount = Count(tools.Normalized, InstalledEntry);
            bool marker = tools.Normalized.IndexOf(
                ToolUuid, StringComparison.OrdinalIgnoreCase) >= 0 ||
                tools.Normalized.IndexOf(
                    "ScrapLabNoclipInputTool", StringComparison.Ordinal) >= 0;

            byte[] module = GetResource("RaidRescue.ScrapLabNoclip.lua");
            byte[] input = GetResource("RaidRescue.ScrapLabNoclipInputTool.lua");
            string modulePath = Path.Combine(gamePath, ModuleRelativePath);
            string inputPath = Path.Combine(gamePath, InputToolRelativePath);
            bool moduleExists = File.Exists(modulePath);
            bool inputExists = File.Exists(inputPath);
            byte[] moduleBytes = moduleExists
                ? File.ReadAllBytes(modulePath) : null;
            byte[] inputBytes = inputExists
                ? File.ReadAllBytes(inputPath) : null;
            bool moduleExact = moduleExists && BytesEqual(moduleBytes, module);
            bool inputExact = inputExists && BytesEqual(inputBytes, input);

            return new AssetState
            {
                ToolsPath = toolsPath,
                ToolsBytes = toolsBytes,
                ToolsDocument = tools,
                ToolsClean = cleanCount == 1 && installedCount == 0 && !marker,
                ToolsInstalled = installedCount == 1 && marker,
                ModulePath = modulePath,
                ModuleExists = moduleExists,
                ModuleBytes = moduleBytes,
                ModuleExact = moduleExact,
                ModuleOwned = moduleExact ||
                    (moduleExists &&
                        (HashEquals(moduleBytes, LegacyV4ModuleHash) ||
                         HashEquals(moduleBytes, LegacyV5ModuleHash) ||
                         HashEquals(moduleBytes, LegacyV6ModuleHash))),
                InputToolPath = inputPath,
                InputToolExists = inputExists,
                InputToolBytes = inputBytes,
                InputToolExact = inputExact,
                InputToolOwned = inputExact ||
                    (inputExists && HashEquals(inputBytes, LegacyInputToolHash))
            };
        }

        private static byte[] GetResource(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "The embedded ScrapLab noclip module is missing: " + name);
                using (MemoryStream output = new MemoryStream())
                {
                    stream.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        private static int Count(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static bool HashEquals(byte[] bytes, string expected)
        {
            if (bytes == null) return false;
            using (SHA256 sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(
                    sha.ComputeHash(bytes)).Replace("-", "");
                return String.Equals(
                    actual, expected, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal sealed class AssetState
        {
            internal string ToolsPath;
            internal byte[] ToolsBytes;
            internal TextFile ToolsDocument;
            internal bool ToolsClean;
            internal bool ToolsInstalled;
            internal string ModulePath;
            internal bool ModuleExists;
            internal byte[] ModuleBytes;
            internal bool ModuleExact;
            internal bool ModuleOwned;
            internal string InputToolPath;
            internal bool InputToolExists;
            internal byte[] InputToolBytes;
            internal bool InputToolExact;
            internal bool InputToolOwned;
        }

        internal sealed class TextFile
        {
            internal bool Bom;
            internal string Newline;
            internal string Normalized;

            internal static TextFile Read(byte[] bytes, string name)
            {
                bool bom = bytes.Length >= 3 && bytes[0] == 0xef &&
                    bytes[1] == 0xbb && bytes[2] == 0xbf;
                string text = new UTF8Encoding(false, true).GetString(
                    bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
                int crlf = Count(text, "\r\n");
                int loneLf = 0;
                for (int index = 0; index < text.Length; index++)
                    if (text[index] == '\n' && (index == 0 || text[index - 1] != '\r')) loneLf++;
                if (crlf > 0 && loneLf > 0)
                    throw new InvalidDataException(name + " uses mixed newline styles.");
                return new TextFile
                {
                    Bom = bom,
                    Newline = crlf > 0 ? "\r\n" : "\n",
                    Normalized = text.Replace("\r\n", "\n").Replace("\r", "\n")
                };
            }

            internal byte[] Render(string normalized)
            {
                string text = Newline == "\n"
                    ? normalized
                    : normalized.Replace("\n", Newline);
                byte[] payload = new UTF8Encoding(false).GetBytes(text);
                if (!Bom) return payload;
                byte[] output = new byte[payload.Length + 3];
                output[0] = 0xef; output[1] = 0xbb; output[2] = 0xbf;
                Buffer.BlockCopy(payload, 0, output, 3, payload.Length);
                return output;
            }
        }

        internal sealed class NoclipAssetTransaction
        {
            private readonly string backupRoot;
            private readonly bool enabled;
            private readonly AssetState before;
            private readonly byte[] module;
            private readonly byte[] inputTool;
            private bool applied;

            internal int FilesChanged { get; private set; }
            internal string BackupPath { get; private set; }

            internal NoclipAssetTransaction(
                string gamePath, string backupRoot, bool enabled,
                AssetState before, byte[] module, byte[] inputTool)
            {
                this.backupRoot = backupRoot;
                this.enabled = enabled;
                this.before = before;
                this.module = module;
                this.inputTool = inputTool;
            }

            internal void Apply()
            {
                string normalized = before.ToolsDocument.Normalized;
                string transformed = normalized;
                if (enabled && before.ToolsClean)
                    transformed = ReplaceUnique(normalized, CleanTail, InstalledTail);
                else if (!enabled && before.ToolsInstalled)
                    transformed = ReplaceUnique(normalized, InstalledTail, CleanTail);

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                BackupPath = Path.Combine(
                    backupRoot, (enabled ? "Install-" : "Remove-") +
                    "DeveloperCommandsAssets-" + stamp);
                Directory.CreateDirectory(BackupPath);
                WriteVerifiedBackup(
                    Path.Combine(BackupPath, "tools.json"), before.ToolsBytes);
                if (before.ModuleExists)
                    WriteVerifiedBackup(
                        Path.Combine(BackupPath, "Noclip.lua"), before.ModuleBytes);
                if (before.InputToolExists)
                    WriteVerifiedBackup(
                        Path.Combine(BackupPath, "NoclipInputTool.lua"), before.InputToolBytes);

                try
                {
                    if (!String.Equals(transformed, normalized, StringComparison.Ordinal))
                    {
                        AdaptivePatchSupport.ReplaceFile(
                            before.ToolsPath,
                            before.ToolsDocument.Render(transformed),
                            "noclip-toolset");
                        FilesChanged++;
                    }

                    if (enabled)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(before.ModulePath));
                        if (!before.ModuleExact)
                        {
                            WriteOwnedFile(before.ModulePath, module, "noclip-module");
                            FilesChanged++;
                        }
                        if (!before.InputToolExact)
                        {
                            WriteOwnedFile(before.InputToolPath, inputTool, "noclip-input-tool");
                            FilesChanged++;
                        }
                    }
                    else
                    {
                        if (before.ModuleOwned) { File.Delete(before.ModulePath); FilesChanged++; }
                        if (before.InputToolOwned) { File.Delete(before.InputToolPath); FilesChanged++; }
                        string folder = Path.GetDirectoryName(before.ModulePath);
                        if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                            Directory.Delete(folder, false);
                    }
                    applied = true;
                }
                catch
                {
                    Rollback();
                    throw;
                }
            }

            internal void Rollback()
            {
                if (!applied && FilesChanged == 0) return;
                AdaptivePatchSupport.ReplaceFile(
                    before.ToolsPath, before.ToolsBytes, "noclip-assets-rollback");
                RestoreOwned(before.ModulePath, before.ModuleExists, before.ModuleBytes);
                RestoreOwned(before.InputToolPath, before.InputToolExists, before.InputToolBytes);
                applied = false;
            }

            private static void WriteOwnedFile(
                string path, byte[] bytes, string label)
            {
                if (File.Exists(path)) File.Delete(path);
                File.WriteAllBytes(path, bytes);
                if (!BytesEqual(File.ReadAllBytes(path), bytes))
                    throw new IOException(label + " failed checksum verification.");
            }

            private static void WriteVerifiedBackup(string path, byte[] bytes)
            {
                File.WriteAllBytes(path, bytes);
                if (!BytesEqual(File.ReadAllBytes(path), bytes))
                    throw new IOException(
                        "The noclip asset backup failed checksum verification: " +
                        Path.GetFileName(path));
            }

            private static void RestoreOwned(
                string path, bool existed, byte[] bytes)
            {
                if (existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            private static string ReplaceUnique(
                string text, string oldText, string newText)
            {
                int first = text.IndexOf(oldText, StringComparison.Ordinal);
                if (first < 0 || text.IndexOf(
                    oldText, first + oldText.Length,
                    StringComparison.Ordinal) >= 0)
                    throw new InvalidDataException(
                        "The protected tools.json tail was not found exactly once.");
                return text.Substring(0, first) + newText +
                    text.Substring(first + oldText.Length);
            }
        }
    }
}
