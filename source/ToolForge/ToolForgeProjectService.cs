using System;
using System.IO;

namespace ScrapLab.ToolForge
{
    internal static class ToolForgeProjectService
    {
        internal const string ManifestFileName = "project.scraptool.json";

        internal static ToolForgeProject CreateTreeSaplingsProject(
            string sourceFbx, string manifestPath, string gameRoot)
        {
            if (!File.Exists(sourceFbx))
                throw new FileNotFoundException("The selected FBX was not found.",
                    sourceFbx);
            string projectRoot = Path.GetDirectoryName(
                Path.GetFullPath(manifestPath));
            Directory.CreateDirectory(projectRoot);
            string sourceDirectory = Path.Combine(projectRoot, "Assets", "Source");
            Directory.CreateDirectory(sourceDirectory);
            string projectCopy = Path.Combine(sourceDirectory,
                "tree_sapling_source.fbx");
            if (!String.Equals(Path.GetFullPath(sourceFbx),
                Path.GetFullPath(projectCopy),
                StringComparison.OrdinalIgnoreCase))
                File.Copy(sourceFbx, projectCopy, true);

            FbxDocument fbx = FbxDocument.Load(projectCopy);
            fbx.InspectMeshes();
            ToolForgeProject project = ToolForgeProject.CreateTreeSaplings(
                Path.GetFileName(projectRoot), gameRoot);
            project.SourceMesh.OriginalPath = Path.GetFullPath(sourceFbx);
            project.SourceMesh.ProjectCopyPath = Path.Combine("Assets", "Source",
                "tree_sapling_source.fbx");
            project.SourceMesh.Sha256 = ToolForgeUtilities.Sha256File(projectCopy);
            if (fbx.ModelNames.Count == 1)
                project.SourceMesh.ModelName = fbx.ModelNames[0];
            else
                project.SourceMesh.ModelName = FindPreferred(
                    fbx.ModelNames, "leafplant3");
            project.SourceMesh.MaterialName = FindPreferred(
                fbx.MaterialNames, "leafplant");
            project.SourceMesh.TextureMode = InferTextureMode(
                project.SourceMesh.ModelName, project.SourceMesh.MaterialName);
            AnalyzeOrientation(project, fbx, true);
            project.IntegrationSourcePath =
                ToolForgeUtilities.FindIntegrationSource(projectRoot);
            project.Output.BaseDirectory = projectRoot;
            Save(manifestPath, project);
            return project;
        }

        internal static ToolForgeProject Load(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("The Tool Forge project was not found.",
                    manifestPath);
            ToolForgeProject project = ToolForgeUtilities.CreateSerializer()
                .Deserialize<ToolForgeProject>(File.ReadAllText(manifestPath));
            if (project == null)
                throw new InvalidDataException("The Tool Forge project is empty.");
            project.Normalize();
            project.SourceMesh.TextureMode = InferTextureMode(
                project.SourceMesh.ModelName, project.SourceMesh.MaterialName);
            string source = ResolveSourcePath(project, manifestPath);
            if (!project.SourceMesh.OrientationAnalyzed && File.Exists(source))
            {
                FbxDocument fbx = FbxDocument.Load(source);
                AnalyzeOrientation(project, fbx, false);
                if (project.TemplateVersion < 2) project.TemplateVersion = 2;
            }
            return project;
        }

        internal static void Save(string manifestPath, ToolForgeProject project)
        {
            if (project == null) throw new ArgumentNullException("project");
            project.Normalize();
            ToolForgeUtilities.WriteTextAtomic(manifestPath,
                ToolForgeUtilities.SerializePretty(project) + "\n");
        }

        internal static string ResolveSourcePath(ToolForgeProject project,
            string manifestPath)
        {
            if (project == null || project.SourceMesh == null)
                return String.Empty;
            string path = project.SourceMesh.ProjectCopyPath;
            if (String.IsNullOrWhiteSpace(path)) return String.Empty;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(manifestPath)), path));
        }

        private static string FindPreferred(System.Collections.Generic.IList<string> values,
            string preferred)
        {
            foreach (string current in values)
                if (String.Equals(current, preferred,
                    StringComparison.OrdinalIgnoreCase)) return current;
            return values.Count == 1 ? values[0] : String.Empty;
        }

        private static string InferTextureMode(string model, string material)
        {
            string identity = (model ?? String.Empty) + " " +
                (material ?? String.Empty);
            if (identity.IndexOf("poleplant",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return "vanilla-poleplant";
            if (identity.IndexOf("leafplant",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return "vanilla-leafplant";
            throw new InvalidDataException("Tool Forge cannot identify the vanilla texture atlas for model '" +
                model + "' and material '" + material + "'.");
        }

        private static void AnalyzeOrientation(ToolForgeProject project,
            FbxDocument document, bool applyToTransform)
        {
            if (String.IsNullOrWhiteSpace(project.SourceMesh.ModelName))
                return;
            FbxMeshData mesh = document.ExtractMesh(
                project.SourceMesh.ModelName);
            double[] minimum =
            {
                Double.PositiveInfinity,
                Double.PositiveInfinity,
                Double.PositiveInfinity
            };
            double[] maximum =
            {
                Double.NegativeInfinity,
                Double.NegativeInfinity,
                Double.NegativeInfinity
            };
            for (int index = 0; index < mesh.Positions.Length; index += 3)
                for (int axis = 0; axis < 3; axis++)
                {
                    double value = mesh.Positions[index + axis];
                    minimum[axis] = Math.Min(minimum[axis], value);
                    maximum[axis] = Math.Max(maximum[axis], value);
                }
            double x = maximum[0] - minimum[0];
            double y = maximum[1] - minimum[1];
            double z = maximum[2] - minimum[2];
            project.SourceMesh.SuggestedRotationX = 0.0;
            project.SourceMesh.SuggestedRotationY = 0.0;
            project.SourceMesh.SuggestedRotationZ = 0.0;
            if (z > y && z >= x)
                project.SourceMesh.SuggestedRotationX = -90.0;
            else if (x > y && x > z)
                project.SourceMesh.SuggestedRotationZ = 90.0;
            project.SourceMesh.OrientationAnalyzed = true;
            if (applyToTransform)
            {
                ApplySuggestedOrientation(project.FirstPersonTransform,
                    project.SourceMesh);
                ApplySuggestedOrientation(project.ThirdPersonTransform,
                    project.SourceMesh);
            }
        }

        private static void ApplySuggestedOrientation(ToolTransform transform,
            SourceMeshSettings source)
        {
            transform.RotationX = source.SuggestedRotationX;
            transform.RotationY = source.SuggestedRotationY;
            transform.RotationZ = source.SuggestedRotationZ;
        }
    }
}
