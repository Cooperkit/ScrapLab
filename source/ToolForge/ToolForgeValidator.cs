using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ScrapLab.ToolForge
{
    internal static class ToolForgeValidator
    {
        private static readonly string[] ExpectedItemUuids =
        {
            "790d34b8-f006-47e4-9ebc-49a84a68ed16",
            "33511c78-354b-4a60-af6b-778c427c47d5",
            "c9413781-5a0e-4025-a2cb-bc2090803e50"
        };
        private static readonly string[] ExpectedToolUuids =
        {
            "6bf64453-2ec9-4d4b-a007-140ccf528cae",
            "1af920ea-287b-45f9-865b-c99111c65772",
            "96d39491-1209-40a9-b97c-3eed67861076"
        };

        internal static ValidationReport Validate(ToolForgeProject project,
            string manifestPath, bool requireIntegrationSource)
        {
            ValidationReport report = new ValidationReport
            {
                Valid = true,
                ProjectName = project == null ? String.Empty : project.ProjectName,
                SourceHash = String.Empty,
                FbxVersion = String.Empty
            };
            if (project == null)
            {
                report.Add("ERROR", "PROJECT_EMPTY",
                    "The Tool Forge project is empty.", manifestPath);
                return report;
            }
            project.Normalize();
            if (project.SchemaVersion != 2)
                report.Add("ERROR", "SCHEMA_UNSUPPORTED",
                    "Only Tool Forge project schema 2 is supported.", manifestPath);
            if (!String.Equals(project.TemplateId, "tree-saplings",
                StringComparison.Ordinal))
                report.Add("ERROR", "TEMPLATE_UNSUPPORTED",
                    "Version 1 supports only the Tree Saplings template.",
                    manifestPath);
            if (!String.Equals(project.AnimationPreset, "clay-bucket",
                StringComparison.Ordinal))
                report.Add("ERROR", "ANIMATION_PRESET",
                    "The Tree Saplings template must use the Clay/Bucket preset.",
                    manifestPath);
            ValidateVariants(project, report);
            ValidateTransform(project.FirstPersonTransform, report);
            ValidateTransform(project.ThirdPersonTransform, report);

            string source = ToolForgeProjectService.ResolveSourcePath(project,
                manifestPath);
            if (!File.Exists(source))
            {
                report.Add("ERROR", "SOURCE_MISSING",
                    "The immutable project FBX copy is missing.", source);
            }
            else
            {
                try
                {
                    string hash = ToolForgeUtilities.Sha256File(source);
                    report.SourceHash = hash;
                    if (!String.Equals(hash, project.SourceMesh.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                        report.Add("ERROR", "SOURCE_CHANGED",
                            "The source FBX no longer matches the project hash. Re-import it instead of building an unknown edit.",
                            source);
                    FbxDocument fbx = FbxDocument.Load(source);
                    report.FbxFormat = fbx.IsBinary ? "BINARY" : "ASCII";
                    report.FbxVersion = fbx.Version;
                    foreach (string name in fbx.ModelNames) report.Models.Add(name);
                    foreach (string name in fbx.MaterialNames)
                        report.Materials.Add(name);
                    List<FbxMeshSummary> meshes = fbx.InspectMeshes();
                    foreach (FbxMeshSummary mesh in meshes)
                    {
                        report.VertexCount += mesh.VertexCount;
                        report.PolygonCount += mesh.PolygonCount;
                    }
                    fbx.RequireModel(project.SourceMesh.ModelName);
                    fbx.RequireMaterial(project.SourceMesh.MaterialName);
                }
                catch (Exception ex)
                {
                    report.Add("ERROR", "FBX_INVALID", ex.Message, source);
                }
            }
            ValidateGameAssets(project, report);
            ValidateIntegration(project, report, requireIntegrationSource);
            report.Valid = !HasErrors(report.Issues);
            return report;
        }

        private static void ValidateVariants(ToolForgeProject project,
            ValidationReport report)
        {
            if (project.Variants == null || project.Variants.Count != 3)
            {
                report.Add("ERROR", "VARIANTS",
                    "The Tree Saplings template requires exactly three variants.",
                    String.Empty);
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                ToolVariant variant = project.Variants[i];
                if (variant == null ||
                    !String.Equals(variant.ItemUuid, ExpectedItemUuids[i],
                        StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(variant.ToolUuid, ExpectedToolUuids[i],
                        StringComparison.OrdinalIgnoreCase))
                    report.Add("ERROR", "UUID_CHANGED",
                        "The permanent " + (i + 1) +
                        " Tree Sapling item or tool UUID changed.", String.Empty);
                if (variant == null || !Regex.IsMatch(variant.Color ?? String.Empty,
                    "^[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant))
                    report.Add("ERROR", "COLOR_INVALID",
                        "Every sapling color must contain exactly six hexadecimal digits.",
                        String.Empty);
            }
        }

        private static void ValidateTransform(ToolTransform transform,
            ValidationReport report)
        {
            if (transform == null)
            {
                report.Add("ERROR", "TRANSFORM_MISSING",
                    "The project transform is missing.", String.Empty);
                return;
            }
            double[] values = { transform.PositionX, transform.PositionY,
                transform.PositionZ, transform.RotationX, transform.RotationY,
                transform.RotationZ, transform.UniformScale };
            foreach (double value in values)
                if (!ToolForgeUtilities.IsFinite(value))
                {
                    report.Add("ERROR", "TRANSFORM_NOT_FINITE",
                        "Position, rotation, and scale must be finite numbers.",
                        String.Empty);
                    return;
                }
            if (Math.Abs(transform.PositionX) > 10000.0 ||
                Math.Abs(transform.PositionY) > 10000.0 ||
                Math.Abs(transform.PositionZ) > 10000.0)
                report.Add("ERROR", "POSITION_RANGE",
                    "Tool position must stay within 10,000 centimeters of the attachment joint.",
                    String.Empty);
            if (transform.UniformScale < 0.001 ||
                transform.UniformScale > 100.0)
                report.Add("ERROR", "SCALE_RANGE",
                    "Uniform scale must be between 0.001 and 100.",
                    String.Empty);
        }

        private static void ValidateGameAssets(ToolForgeProject project,
            ValidationReport report)
        {
            string gameRoot = project.GameRoot;
            if (String.IsNullOrWhiteSpace(gameRoot) ||
                !File.Exists(Path.Combine(gameRoot, "Release",
                    "ScrapMechanic.exe")))
            {
                report.Add("ERROR", "GAME_NOT_FOUND",
                    "Select a valid Scrap Mechanic installation.", gameRoot);
                return;
            }
            string textureStem;
            try
            {
                textureStem = TreeSaplingToolGenerator.ResolveTextureStem(project);
            }
            catch (Exception ex)
            {
                report.Add("ERROR", "TEXTURE_MODE", ex.Message, String.Empty);
                return;
            }
            string[] required =
            {
                "Data/Objects/Textures/plants/" + textureStem + "_dif.tga",
                "Data/Objects/Textures/plants/" + textureStem + "_asg.tga",
                "Data/Objects/Textures/plants/" + textureStem + "_nor.tga",
                "Survival/Character/Char_Male/Body/Male/char_male_body_fp_arms.dae",
                "Survival/Character/Char_Male/Body/Male/char_male_body.dae",
                "Survival/Character/Char_Tools/Char_clay/char_claytool.dae",
                "Survival/Character/Char_Male/Animations/char_male_fp_bucket.rend",
                "Survival/Character/Char_Male/Animations/char_male_tp_bucket.rend"
            };
            foreach (string relative in required)
            {
                string path = Path.Combine(gameRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    report.Add("ERROR", "GAME_ASSET_MISSING",
                        "A required vanilla preview or material asset is missing.",
                        path);
            }
            try
            {
                PreviewAssets assets = ScrapMechanicPreviewAssets.Create(gameRoot);
                bool firstPersonJoint = ContainsJointInAnimations(gameRoot,
                    assets.FirstPersonAnimations);
                bool thirdPersonJoint = ContainsJointInAnimations(gameRoot,
                    assets.ThirdPersonAnimations);
                if (!firstPersonJoint || !thirdPersonJoint)
                    report.Add("ERROR", "ATTACHMENT_JOINT_MISSING",
                        "The installed Bucket animation rigs do not contain jnt_right_weapon.",
                        gameRoot);
            }
            catch (Exception ex)
            {
                report.Add("ERROR", "ANIMATION_ASSETS", ex.Message, gameRoot);
            }
        }

        private static bool ContainsJointInAnimations(string gameRoot,
            IList<PreviewAnimation> animations)
        {
            if (animations == null || animations.Count == 0) return false;
            foreach (PreviewAnimation animation in animations)
                if (animation != null && ContainsJoint(gameRoot, animation.Url))
                    return true;
            return false;
        }

        private static bool ContainsJoint(string gameRoot, string url)
        {
            const string prefix = "https://game.toolforge/";
            if (String.IsNullOrEmpty(url) || !url.StartsWith(prefix,
                StringComparison.OrdinalIgnoreCase)) return false;
            string relative = Uri.UnescapeDataString(url.Substring(prefix.Length))
                .Replace('/', Path.DirectorySeparatorChar);
            string path = Path.Combine(gameRoot, relative);
            if (!File.Exists(path)) return false;
            using (StreamReader reader = new StreamReader(path))
            {
                char[] buffer = new char[65536];
                int read;
                string carry = String.Empty;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string text = carry + new string(buffer, 0, read);
                    if (text.IndexOf("jnt_right_weapon",
                        StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    carry = text.Length > 64
                        ? text.Substring(text.Length - 64) : text;
                }
            }
            return false;
        }

        private static void ValidateIntegration(ToolForgeProject project,
            ValidationReport report, bool required)
        {
            string path = project.IntegrationSourcePath;
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                report.Add(required ? "ERROR" : "WARNING",
                    "INTEGRATION_SOURCE_MISSING",
                    "Select the current ScrapLab TreeSaplingTool.lua to generate its review copy.",
                    path);
                return;
            }
            string text = File.ReadAllText(path);
            string[] markers =
            {
                "function TreeSaplingToolBase.cl_loadAnimations",
                "function TreeSaplingToolBase.cl_updateRenderables",
                "790d34b8-f006-47e4-9ebc-49a84a68ed16",
                "33511c78-354b-4a60-af6b-778c427c47d5",
                "c9413781-5a0e-4025-a2cb-bc2090803e50"
            };
            foreach (string marker in markers)
                if (Count(text, marker) != 1)
                    report.Add("ERROR", "INTEGRATION_SOURCE_CHANGED",
                        "The integration source is missing or duplicates protected marker: " + marker,
                        path);
        }

        private static int Count(string text, string value)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(value, index,
                StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static bool HasErrors(IEnumerable<ValidationIssue> issues)
        {
            foreach (ValidationIssue issue in issues)
                if (String.Equals(issue.Severity, "ERROR",
                    StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
