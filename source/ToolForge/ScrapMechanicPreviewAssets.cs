using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ScrapLab.ToolForge
{
    internal static class ScrapMechanicPreviewAssets
    {
        private const string FirstPersonRend =
            "Survival/Character/Char_Male/Animations/char_male_fp_bucket.rend";
        private const string ThirdPersonRend =
            "Survival/Character/Char_Male/Animations/char_male_tp_bucket.rend";
        private const string FirstPersonToolRend =
            "Survival/Character/Char_bucket/char_bucket_fp_animlist.rend";
        private const string ThirdPersonToolRend =
            "Survival/Character/Char_bucket/char_bucket_tp_animlist.rend";

        internal static PreviewAssets Create(string gameRoot)
        {
            PreviewAssets assets = new PreviewAssets
            {
                FirstPersonMeshUrl = GameUrl(
                    "Survival/Character/Char_Male/Body/Male/char_male_body_fp_arms.dae"),
                ThirdPersonMeshUrl = GameUrl(
                    "Survival/Character/Char_Male/Body/Male/char_male_body.dae"),
                ClayReferenceUrl = GameUrl(
                    "Survival/Character/Char_Tools/Char_clay/char_claytool.dae")
            };
            assets.FirstPersonAnimations = ReadAnimations(gameRoot,
                FirstPersonRend);
            assets.ThirdPersonAnimations = ReadAnimations(gameRoot,
                ThirdPersonRend);
            assets.FirstPersonToolAnimations = ReadAnimations(gameRoot,
                FirstPersonToolRend);
            assets.ThirdPersonToolAnimations = ReadAnimations(gameRoot,
                ThirdPersonToolRend);
            assets.FirstPersonJointNames = ReadSkinJointNames(Path.Combine(
                gameRoot, "Survival", "Character", "Char_Male", "Body",
                "Male", "char_male_body_fp_arms.dae"));
            assets.ThirdPersonJointNames = ReadSkinJointNames(Path.Combine(
                gameRoot, "Survival", "Character", "Char_Male", "Body",
                "Male", "char_male_body.dae"));
            return assets;
        }

        private static List<string> ReadSkinJointNames(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "A character body mesh is missing.", path);
            string text = File.ReadAllText(path);
            MatchCollection arrays = Regex.Matches(text,
                "<Name_array[^>]*>(.*?)</Name_array>",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            List<string> result = new List<string>();
            foreach (Match array in arrays)
            {
                string[] values = Regex.Split(array.Groups[1].Value.Trim(),
                    "\\s+", RegexOptions.CultureInvariant);
                foreach (string value in values)
                    if (value.StartsWith("jnt_", StringComparison.Ordinal) &&
                        !result.Contains(value)) result.Add(value);
            }
            if (result.Count == 0)
                throw new InvalidDataException(
                    "The character body mesh contains no skin-joint order: " +
                    path);
            return result;
        }

        internal static string ResolveTokenPath(string gameRoot, string value)
        {
            string relative;
            if (value.StartsWith("$SURVIVAL_DATA/",
                StringComparison.OrdinalIgnoreCase))
                relative = "Survival/" + value.Substring(15);
            else if (value.StartsWith("$GAME_DATA/",
                StringComparison.OrdinalIgnoreCase))
                relative = "Data/" + value.Substring(11);
            else if (value.StartsWith("$CUSTOMIZATION_DATA/",
                StringComparison.OrdinalIgnoreCase))
                relative = "Data/" + value.Substring(20);
            else throw new InvalidDataException(
                "Unsupported Scrap Mechanic asset token: " + value);
            return Path.Combine(gameRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static List<PreviewAnimation> ReadAnimations(string gameRoot,
            string rendRelative)
        {
            string path = Path.Combine(gameRoot,
                rendRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "A Bucket animation renderable is missing.", path);
            string text = Regex.Replace(File.ReadAllText(path), "//.*$", "",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            Regex entry = new Regex(
                "\\{(?=[^{}]*\\\"file\\\")(?=[^{}]*\\\"name\\\")([^{}]*)\\}",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            List<PreviewAnimation> result = new List<PreviewAnimation>();
            foreach (Match match in entry.Matches(text))
            {
                Match file = Regex.Match(match.Groups[1].Value,
                    "\\\"file\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
                    RegexOptions.CultureInvariant);
                Match name = Regex.Match(match.Groups[1].Value,
                    "\\\"name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
                    RegexOptions.CultureInvariant);
                if (!file.Success || !name.Success) continue;
                string resolved = ResolveTokenPath(gameRoot, file.Groups[1].Value);
                if (!File.Exists(resolved))
                    throw new FileNotFoundException(
                        "A Bucket animation clip is missing.", resolved);
                result.Add(new PreviewAnimation
                {
                    Name = name.Groups[1].Value,
                    Url = GameUrl(RelativeGamePath(gameRoot, resolved)),
                    Looping = Regex.IsMatch(match.Groups[1].Value,
                        "\\\"looping\\\"\\s*:\\s*true",
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant)
                });
            }
            if (result.Count == 0)
                throw new InvalidDataException(
                    "No Bucket animations were found in " + path + ".");
            return result;
        }

        private static string RelativeGamePath(string gameRoot, string path)
        {
            string root = Path.GetFullPath(gameRoot).TrimEnd(
                Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "A preview asset escaped the game directory.");
            return ToolForgeUtilities.ToForwardSlashes(full.Substring(root.Length));
        }

        private static string GameUrl(string relative)
        {
            string[] parts = ToolForgeUtilities.ToForwardSlashes(relative)
                .Split('/');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = Uri.EscapeDataString(parts[i]);
            return "https://game.toolforge/" + String.Join("/", parts);
        }
    }
}
