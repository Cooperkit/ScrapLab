using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ScrapLab.ToolForge
{
    internal static class SaplingPackageBuilder
    {
        internal const string ToolForgeVersion = "1.0.0";

        internal static ToolForgeBuildResult Build(ToolForgeProject project,
            string manifestPath, string outputBase)
        {
            ValidationReport validation = ToolForgeValidator.Validate(project,
                manifestPath, true);
            if (!validation.Valid)
                return new ToolForgeBuildResult
                {
                    Success = false,
                    Validation = validation,
                    PackagePath = String.Empty
                };

            string basePath = Path.GetFullPath(outputBase);
            Directory.CreateDirectory(basePath);
            string packageName = ToolForgeUtilities.SafeName(
                project.Output.PackageName, "TreeSaplingHeldTool");
            string finalPath = ToolForgeUtilities.ResolveInside(basePath,
                packageName);
            string stagePath = ToolForgeUtilities.ResolveInside(basePath,
                "." + packageName + ".stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagePath);
            try
            {
                string sourceFbx = ToolForgeProjectService.ResolveSourcePath(
                    project, manifestPath);
                string sourceRoot = Path.Combine(stagePath, "Assets", "Source");
                string packageRoot = Path.Combine(stagePath, "ScrapLabPackage",
                    "source", "Patching", "Parts", "TreeSaplings");
                string reportsRoot = Path.Combine(stagePath, "Reports");
                Directory.CreateDirectory(sourceRoot);
                Directory.CreateDirectory(packageRoot);
                Directory.CreateDirectory(reportsRoot);

                File.Copy(sourceFbx, Path.Combine(sourceRoot,
                    "tree_sapling_source.fbx"), true);
                ToolForgeProjectService.Save(Path.Combine(stagePath,
                    ToolForgeProjectService.ManifestFileName), project);

                FbxDocument sourceDocument = FbxDocument.Load(sourceFbx);
                FbxMeshData sourceMesh = sourceDocument.ExtractMesh(
                    project.SourceMesh.ModelName);
                double automaticScale = ColladaHeldToolGenerator
                    .AutomaticMeshScale(sourceMesh.Positions);
                GenerateProfile(sourceFbx, packageRoot, project,
                    project.FirstPersonTransform, automaticScale, "Fp", "fp");
                GenerateProfile(sourceFbx, packageRoot, project,
                    project.ThirdPersonTransform, automaticScale, "Tp", "tp");
                WriteText(Path.Combine(packageRoot,
                    "TreeSaplingVisual.generated.lua"),
                    TreeSaplingToolGenerator.GenerateVisualLua(project));
                string toolSource = File.ReadAllText(
                    project.IntegrationSourcePath);
                string integrated = TreeSaplingToolGenerator
                    .GenerateIntegratedTool(toolSource, project);
                WriteText(Path.Combine(packageRoot,
                    "TreeSaplingTool.generated.lua"), integrated);
                WriteText(Path.Combine(packageRoot,
                    "TreeSaplingTool.assets.json"),
                    TreeSaplingToolGenerator.GenerateAssetsManifest(project));

                ValidateGeneratedFiles(packageRoot, project);
                WriteText(Path.Combine(reportsRoot, "validation-report.json"),
                    ToolForgeUtilities.SerializePretty(validation) + "\n");
                WriteText(Path.Combine(reportsRoot, "INTEGRATION.md"),
                    GenerateIntegrationGuide(project));

                BuildManifest build = CreateManifest(stagePath, project,
                    validation);
                WriteText(Path.Combine(reportsRoot, "build-manifest.json"),
                    ToolForgeUtilities.SerializePretty(build) + "\n");
                VerifyArtifacts(stagePath, build);
                ReplacePackage(stagePath, finalPath);
                stagePath = null;
                return new ToolForgeBuildResult
                {
                    Success = true,
                    PackagePath = finalPath,
                    Validation = validation,
                    Manifest = build
                };
            }
            finally
            {
                if (!String.IsNullOrEmpty(stagePath) &&
                    Directory.Exists(stagePath))
                    Directory.Delete(stagePath, true);
            }
        }

        private static ToolTransform RuntimeTransform(ToolTransform source,
            double automaticScale)
        {
            if (source == null)
                throw new InvalidDataException(
                    "The attachment transform is missing.");
            return new ToolTransform
            {
                // The editor exposes centimetres. Static character-tool FBX
                // geometry is consumed by Scrap Mechanic in game units.
                PositionX = source.PositionX * 0.01,
                // Tool Forge uses positive Y for visible Up. Scrap Mechanic's
                // held-tool mesh importer consumes that translation inverted.
                PositionY = -source.PositionY * 0.01,
                PositionZ = source.PositionZ * 0.01,
                RotationX = source.RotationX,
                RotationY = source.RotationY,
                RotationZ = source.RotationZ,
                UniformScale = source.UniformScale * automaticScale,
                TranslationSnap = source.TranslationSnap,
                RotationSnap = source.RotationSnap,
                ScaleSnap = source.ScaleSnap
            };
        }

        private static BuildManifest CreateManifest(string root,
            ToolForgeProject project, ValidationReport validation)
        {
            GameBuildInfo game = ToolForgeUtilities.ReadGameBuild(project.GameRoot);
            BuildManifest manifest = new BuildManifest
            {
                SchemaVersion = 2,
                ToolForgeVersion = ToolForgeVersion,
                TemplateId = project.TemplateId,
                TemplateVersion = project.TemplateVersion,
                ProjectName = project.ProjectName,
                SourceSha256 = validation.SourceHash,
                SteamBuildId = game.SteamBuildId,
                GameVersion = game.GameVersion,
                Transform = null,
                FirstPersonTransform = project.FirstPersonTransform,
                ThirdPersonTransform = project.ThirdPersonTransform,
                Artifacts = new List<BuildArtifact>()
            };
            foreach (string file in Directory.GetFiles(root, "*",
                SearchOption.AllDirectories))
            {
                string relative = RelativePath(root, file);
                if (String.Equals(relative,
                    "Reports/build-manifest.json",
                    StringComparison.OrdinalIgnoreCase)) continue;
                FileInfo info = new FileInfo(file);
                manifest.Artifacts.Add(new BuildArtifact
                {
                    RelativePath = relative,
                    Sha256 = ToolForgeUtilities.Sha256File(file),
                    Length = info.Length,
                    Purpose = Purpose(relative)
                });
            }
            manifest.Artifacts.Sort(delegate(BuildArtifact a, BuildArtifact b)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(
                    a.RelativePath, b.RelativePath);
            });
            return manifest;
        }

        private static void ValidateGeneratedFiles(string packageRoot,
            ToolForgeProject project)
        {
            JavaScriptSerializer serializer = ToolForgeUtilities.CreateSerializer();
            serializer.DeserializeObject(File.ReadAllText(Path.Combine(
                packageRoot, "TreeSaplingHeldFp.rend")));
            serializer.DeserializeObject(File.ReadAllText(Path.Combine(
                packageRoot, "TreeSaplingHeldTp.rend")));
            serializer.DeserializeObject(File.ReadAllText(Path.Combine(
                packageRoot, "TreeSaplingTool.assets.json")));
            System.Xml.XmlDocument collada = new System.Xml.XmlDocument();
            collada.XmlResolver = null;
            collada.Load(Path.Combine(packageRoot, "TreeSaplingHeldFp.dae"));
            System.Xml.XmlNamespaceManager namespaces =
                new System.Xml.XmlNamespaceManager(collada.NameTable);
            namespaces.AddNamespace("c",
                "http://www.collada.org/2005/11/COLLADASchema");
            if (collada.SelectSingleNode(
                "//c:controller/c:skin/c:vertex_weights", namespaces) == null ||
                collada.SelectSingleNode(
                "//c:node[@id='jnt_right_weapon']/c:node[@id='root_bucket_jnt']",
                namespaces) == null ||
                collada.SelectSingleNode(
                "//c:instance_controller[@url='#TreeSaplingHeldController']",
                namespaces) == null)
                throw new InvalidDataException(
                    "The generated DAE is missing the Clay-style attachment skin.");
            string visual = File.ReadAllText(Path.Combine(packageRoot,
                "TreeSaplingVisual.generated.lua"));
            string tool = File.ReadAllText(Path.Combine(packageRoot,
                "TreeSaplingTool.generated.lua"));
            RequireOnce(visual, TreeSaplingToolGenerator.HeldRenderableFpGamePath);
            RequireOnce(visual, TreeSaplingToolGenerator.HeldRenderableTpGamePath);
            RequireOnce(tool,
                "function TreeSaplingToolBase.cl_updateRenderables");
            RequireOnce(tool,
                "ScrapLabTreeSaplingVisual.apply( self.tool, self.config.color )");
            foreach (ToolVariant variant in project.Variants)
                RequireOnce(tool, variant.ItemUuid);
        }

        private static void ReplacePackage(string stagePath, string finalPath)
        {
            string oldPath = finalPath + ".previous-" +
                Guid.NewGuid().ToString("N");
            bool movedOld = false;
            if (Directory.Exists(finalPath))
            {
                VerifyExistingPackage(finalPath);
                Directory.Move(finalPath, oldPath);
                movedOld = true;
            }
            try
            {
                Directory.Move(stagePath, finalPath);
            }
            catch
            {
                if (movedOld && !Directory.Exists(finalPath) &&
                    Directory.Exists(oldPath))
                    Directory.Move(oldPath, finalPath);
                throw;
            }
            if (movedOld && Directory.Exists(oldPath))
                Directory.Delete(oldPath, true);
        }

        private static void VerifyExistingPackage(string finalPath)
        {
            string manifestPath = Path.Combine(finalPath, "Reports",
                "build-manifest.json");
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException(
                    "The existing output was not created by Tool Forge. Choose another folder.");
            BuildManifest manifest = ToolForgeUtilities.CreateSerializer()
                .Deserialize<BuildManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.Artifacts == null)
                throw new InvalidDataException(
                    "The existing Tool Forge build manifest is invalid.");
            VerifyArtifacts(finalPath, manifest);
        }

        private static void VerifyArtifacts(string root, BuildManifest manifest)
        {
            foreach (BuildArtifact artifact in manifest.Artifacts)
            {
                string path = ToolForgeUtilities.ResolveInside(root,
                    artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) ||
                    !String.Equals(ToolForgeUtilities.Sha256File(path),
                        artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Generated file '" + artifact.RelativePath +
                        "' was edited. Tool Forge preserved it and stopped the rebuild.");
            }
        }

        private static string GenerateIntegrationGuide(ToolForgeProject project)
        {
            return "# Tree Saplings held-tool integration\n\n" +
                "This folder was generated by ScrapLab Tool Forge. It has not changed ScrapLab or Scrap Mechanic.\n\n" +
                "1. Review `TreeSaplingTool.generated.lua` against the current `TreeSaplingTool.lua`. Only the generated visual `dofile`, Clay/Bucket animation mapping, and held renderables should differ.\n" +
                "2. Copy the seven generated asset sources into `source/Patching/Parts/TreeSaplings/`.\n" +
                "3. Replace the reviewed source `TreeSaplingTool.lua` with the generated copy.\n" +
                "4. Embed the separate `TreeSaplingHeldFp.*` and `TreeSaplingHeldTp.*` assets plus `TreeSaplingVisual.generated.lua` in `ScrapLab.PatchHelper.exe`. Each camera mode receives its independently calibrated mesh.\n" +
                "5. Add the target paths from `TreeSaplingTool.assets.json` to `TreeSaplingsPatchService` so all assets participate in the existing atomic transaction and receipt.\n" +
                "6. Raise the Tree Saplings definition, keep the original uninstall receipt, rebuild, and test all three held colors in first and third person.\n\n" +
                "The package deliberately contains no vanilla textures, character meshes, or animations. The generated `.rend` and Lua reference the installed game copies.\n";
        }

        private static string Purpose(string relative)
        {
            if (relative.EndsWith(".fbx",
                StringComparison.OrdinalIgnoreCase)) return "mesh";
            if (relative.EndsWith(".dae",
                StringComparison.OrdinalIgnoreCase)) return "skinned-mesh";
            if (relative.EndsWith(".rend",
                StringComparison.OrdinalIgnoreCase)) return "renderable";
            if (relative.EndsWith(".lua",
                StringComparison.OrdinalIgnoreCase)) return "lua";
            if (relative.EndsWith(".json",
                StringComparison.OrdinalIgnoreCase)) return "manifest";
            return "documentation";
        }

        private static void GenerateProfile(string sourceFbx,
            string packageRoot, ToolForgeProject project,
            ToolTransform transform, double automaticScale,
            string suffix, string profile)
        {
            // Each parsed document is independent because transformed FBX
            // generation mutates its in-memory geometry.
            FbxDocument document = FbxDocument.Load(sourceFbx);
            string dae = ColladaHeldToolGenerator.Generate(document,
                project.SourceMesh.ModelName,
                project.SourceMesh.MaterialName, transform);
            ToolTransform runtime = RuntimeTransform(transform, automaticScale);
            byte[] transformed = document.CreateTransformedCopy(
                project.SourceMesh.ModelName, runtime);
            string fbxPath = Path.Combine(packageRoot,
                "TreeSaplingHeld" + suffix + ".fbx");
            File.WriteAllBytes(fbxPath, transformed);
            FbxDocument verification = FbxDocument.Load(fbxPath);
            verification.InspectMeshes();
            verification.RequireModel(project.SourceMesh.ModelName);
            verification.RequireMaterial(project.SourceMesh.MaterialName);
            WriteText(Path.Combine(packageRoot,
                "TreeSaplingHeld" + suffix + ".dae"), dae);
            WriteText(Path.Combine(packageRoot,
                "TreeSaplingHeld" + suffix + ".rend"),
                TreeSaplingToolGenerator.GenerateRenderable(project, profile));
        }

        private static string RelativePath(string root, string file)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(file);
            if (!full.StartsWith(fullRoot,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "A staged file escaped the output directory.");
            return ToolForgeUtilities.ToForwardSlashes(
                full.Substring(fullRoot.Length));
        }

        private static void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, ToolForgeUtilities.Utf8NoBom);
        }

        private static void RequireOnce(string text, string marker)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(marker, index,
                StringComparison.Ordinal)) >= 0)
            {
                count++; index += marker.Length;
            }
            if (count != 1)
                throw new InvalidDataException("Generated marker '" + marker +
                    "' must appear exactly once; found " + count + ".");
        }
    }
}
