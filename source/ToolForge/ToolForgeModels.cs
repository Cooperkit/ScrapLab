using System;
using System.Collections.Generic;

namespace ScrapLab.ToolForge
{
    internal sealed class ToolForgeProject
    {
        public int SchemaVersion { get; set; }
        public int TemplateVersion { get; set; }
        public string ProjectName { get; set; }
        public string TemplateId { get; set; }
        public string AnimationPreset { get; set; }
        public string GameRoot { get; set; }
        public string IntegrationSourcePath { get; set; }
        public SourceMeshSettings SourceMesh { get; set; }
        // Transform is retained only to migrate schema-1 projects. New builds
        // use independent profiles because Scrap Mechanic's FP and TP hand
        // rigs do not share the same attachment pose.
        public ToolTransform Transform { get; set; }
        public ToolTransform FirstPersonTransform { get; set; }
        public ToolTransform ThirdPersonTransform { get; set; }
        public List<ToolVariant> Variants { get; set; }
        public OutputSettings Output { get; set; }

        internal static ToolForgeProject CreateTreeSaplings(
            string projectName, string gameRoot)
        {
            return new ToolForgeProject
            {
                SchemaVersion = 2,
                TemplateVersion = 3,
                ProjectName = String.IsNullOrWhiteSpace(projectName)
                    ? "TreeSaplingHeldTool"
                    : projectName.Trim(),
                TemplateId = "tree-saplings",
                AnimationPreset = "clay-bucket",
                GameRoot = gameRoot ?? String.Empty,
                IntegrationSourcePath = String.Empty,
                SourceMesh = new SourceMeshSettings
                {
                    OriginalPath = String.Empty,
                    ProjectCopyPath = String.Empty,
                    Sha256 = String.Empty,
                    ModelName = String.Empty,
                    MaterialName = "leafplant",
                    TextureMode = "vanilla-leafplant",
                    OrientationAnalyzed = false,
                    SuggestedRotationX = 0.0,
                    SuggestedRotationY = 0.0,
                    SuggestedRotationZ = 0.0
                },
                Transform = null,
                FirstPersonTransform = ToolTransform.CreateDefault(),
                ThirdPersonTransform = ToolTransform.CreateDefault(),
                Variants = new List<ToolVariant>
                {
                    ToolVariant.Create("small", "Small", "7eed56",
                        "790d34b8-f006-47e4-9ebc-49a84a68ed16",
                        "6bf64453-2ec9-4d4b-a007-140ccf528cae"),
                    ToolVariant.Create("medium", "Medium", "e2db13",
                        "33511c78-354b-4a60-af6b-778c427c47d5",
                        "1af920ea-287b-45f9-865b-c99111c65772"),
                    ToolVariant.Create("large", "Large", "df7f00",
                        "c9413781-5a0e-4025-a2cb-bc2090803e50",
                        "96d39491-1209-40a9-b97c-3eed67861076")
                },
                Output = new OutputSettings
                {
                    BaseDirectory = String.Empty,
                    PackageName = "TreeSaplingHeldTool"
                }
            };
        }

        internal void Normalize()
        {
            if (SchemaVersion == 0) SchemaVersion = 1;
            if (TemplateVersion == 0) TemplateVersion = 1;
            if (ProjectName == null) ProjectName = "TreeSaplingHeldTool";
            if (TemplateId == null) TemplateId = "tree-saplings";
            if (AnimationPreset == null) AnimationPreset = "clay-bucket";
            if (GameRoot == null) GameRoot = String.Empty;
            if (IntegrationSourcePath == null) IntegrationSourcePath = String.Empty;
            if (SourceMesh == null) SourceMesh = new SourceMeshSettings();
            SourceMesh.Normalize();
            // Schema 1 stored one transform. Clone it into both profiles so
            // existing calibration work is preserved exactly.
            ToolTransform legacy = Transform ?? ToolTransform.CreateDefault();
            legacy.Normalize();
            if (FirstPersonTransform == null)
                FirstPersonTransform = legacy.Clone();
            if (ThirdPersonTransform == null)
                ThirdPersonTransform = legacy.Clone();
            FirstPersonTransform.Normalize();
            ThirdPersonTransform.Normalize();
            Transform = null;
            if (SchemaVersion < 2) SchemaVersion = 2;
            if (TemplateVersion < 3) TemplateVersion = 3;
            if (Variants == null || Variants.Count == 0)
            {
                ToolForgeProject defaults = CreateTreeSaplings(ProjectName, GameRoot);
                Variants = defaults.Variants;
            }
            foreach (ToolVariant variant in Variants)
                if (variant != null) variant.Normalize();
            if (Output == null) Output = new OutputSettings();
            Output.Normalize();
        }
    }

    internal sealed class SourceMeshSettings
    {
        public string OriginalPath { get; set; }
        public string ProjectCopyPath { get; set; }
        public string Sha256 { get; set; }
        public string ModelName { get; set; }
        public string MaterialName { get; set; }
        public string TextureMode { get; set; }
        public bool OrientationAnalyzed { get; set; }
        public double SuggestedRotationX { get; set; }
        public double SuggestedRotationY { get; set; }
        public double SuggestedRotationZ { get; set; }

        internal void Normalize()
        {
            if (OriginalPath == null) OriginalPath = String.Empty;
            if (ProjectCopyPath == null) ProjectCopyPath = String.Empty;
            if (Sha256 == null) Sha256 = String.Empty;
            if (ModelName == null) ModelName = String.Empty;
            if (MaterialName == null) MaterialName = "leafplant";
            if (TextureMode == null) TextureMode = "vanilla-leafplant";
        }
    }

    internal sealed class ToolTransform
    {
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public double RotationX { get; set; }
        public double RotationY { get; set; }
        public double RotationZ { get; set; }
        public double UniformScale { get; set; }
        public double TranslationSnap { get; set; }
        public double RotationSnap { get; set; }
        public double ScaleSnap { get; set; }

        internal static ToolTransform CreateDefault()
        {
            return new ToolTransform
            {
                UniformScale = 1.0,
                TranslationSnap = 1.0,
                RotationSnap = 5.0,
                ScaleSnap = 0.05
            };
        }

        internal ToolTransform Clone()
        {
            return new ToolTransform
            {
                PositionX = PositionX, PositionY = PositionY,
                PositionZ = PositionZ, RotationX = RotationX,
                RotationY = RotationY, RotationZ = RotationZ,
                UniformScale = UniformScale,
                TranslationSnap = TranslationSnap,
                RotationSnap = RotationSnap, ScaleSnap = ScaleSnap
            };
        }

        internal void Normalize()
        {
            if (UniformScale == 0.0) UniformScale = 1.0;
            if (TranslationSnap <= 0.0) TranslationSnap = 1.0;
            if (RotationSnap <= 0.0) RotationSnap = 5.0;
            if (ScaleSnap <= 0.0) ScaleSnap = 0.05;
        }
    }

    internal sealed class ToolVariant
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Color { get; set; }
        public string ItemUuid { get; set; }
        public string ToolUuid { get; set; }

        internal static ToolVariant Create(string key, string displayName,
            string color, string itemUuid, string toolUuid)
        {
            return new ToolVariant
            {
                Key = key,
                DisplayName = displayName,
                Color = color,
                ItemUuid = itemUuid,
                ToolUuid = toolUuid
            };
        }

        internal void Normalize()
        {
            if (Key == null) Key = String.Empty;
            if (DisplayName == null) DisplayName = Key;
            if (Color == null) Color = "ffffff";
            if (ItemUuid == null) ItemUuid = String.Empty;
            if (ToolUuid == null) ToolUuid = String.Empty;
        }
    }

    internal sealed class OutputSettings
    {
        public string BaseDirectory { get; set; }
        public string PackageName { get; set; }

        internal void Normalize()
        {
            if (BaseDirectory == null) BaseDirectory = String.Empty;
            if (PackageName == null) PackageName = "TreeSaplingHeldTool";
        }
    }

    internal sealed class ValidationIssue
    {
        public string Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string Path { get; set; }
    }

    internal sealed class ValidationReport
    {
        public bool Valid { get; set; }
        public string ProjectName { get; set; }
        public string SourceHash { get; set; }
        public string FbxFormat { get; set; }
        public string FbxVersion { get; set; }
        public int VertexCount { get; set; }
        public int PolygonCount { get; set; }
        public List<string> Models { get; set; }
        public List<string> Materials { get; set; }
        public List<ValidationIssue> Issues { get; set; }

        internal ValidationReport()
        {
            Models = new List<string>();
            Materials = new List<string>();
            Issues = new List<ValidationIssue>();
            FbxFormat = String.Empty;
        }

        internal void Add(string severity, string code, string message,
            string path)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                Path = path ?? String.Empty
            });
            if (String.Equals(severity, "ERROR",
                StringComparison.OrdinalIgnoreCase))
                Valid = false;
        }
    }

    internal sealed class BuildArtifact
    {
        public string RelativePath { get; set; }
        public string Sha256 { get; set; }
        public long Length { get; set; }
        public string Purpose { get; set; }
    }

    internal sealed class BuildManifest
    {
        public int SchemaVersion { get; set; }
        public string ToolForgeVersion { get; set; }
        public string TemplateId { get; set; }
        public int TemplateVersion { get; set; }
        public string ProjectName { get; set; }
        public string SourceSha256 { get; set; }
        public string SteamBuildId { get; set; }
        public string GameVersion { get; set; }
        public ToolTransform Transform { get; set; }
        public ToolTransform FirstPersonTransform { get; set; }
        public ToolTransform ThirdPersonTransform { get; set; }
        public List<BuildArtifact> Artifacts { get; set; }
    }

    internal sealed class ToolForgeBuildResult
    {
        public bool Success { get; set; }
        public string PackagePath { get; set; }
        public ValidationReport Validation { get; set; }
        public BuildManifest Manifest { get; set; }
    }

    internal sealed class GameBuildInfo
    {
        public string SteamBuildId { get; set; }
        public string GameVersion { get; set; }
    }

    internal sealed class PreviewAnimation
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public bool Looping { get; set; }
    }

    internal sealed class PreviewAssets
    {
        public string FirstPersonMeshUrl { get; set; }
        public string ThirdPersonMeshUrl { get; set; }
        public string ClayReferenceUrl { get; set; }
        public List<string> FirstPersonJointNames { get; set; }
        public List<string> ThirdPersonJointNames { get; set; }
        public List<PreviewAnimation> FirstPersonAnimations { get; set; }
        public List<PreviewAnimation> ThirdPersonAnimations { get; set; }
        public List<PreviewAnimation> FirstPersonToolAnimations { get; set; }
        public List<PreviewAnimation> ThirdPersonToolAnimations { get; set; }

        internal PreviewAssets()
        {
            FirstPersonJointNames = new List<string>();
            ThirdPersonJointNames = new List<string>();
            FirstPersonAnimations = new List<PreviewAnimation>();
            ThirdPersonAnimations = new List<PreviewAnimation>();
            FirstPersonToolAnimations = new List<PreviewAnimation>();
            ThirdPersonToolAnimations = new List<PreviewAnimation>();
        }
    }

    internal sealed class ToolPreviewGeometry
    {
        public double[] Positions { get; set; }
        public double[] Normals { get; set; }
        public double[] Texcoords { get; set; }
        public double AutomaticScale { get; set; }
        public int TriangleCount { get; set; }
    }
}
