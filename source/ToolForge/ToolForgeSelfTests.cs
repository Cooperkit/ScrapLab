using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ScrapLab.ToolForge
{
    internal static class ToolForgeSelfTests
    {
        internal static bool Run(TextWriter output)
        {
            int passed = 0, failed = 0;
            string root = Path.Combine(Path.GetTempPath(),
                "ScrapLabToolForgeTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string game = RaidRescue.GameInstallLocator.Find();
                RunCase(output, "game discovery", delegate
                {
                    if (String.IsNullOrWhiteSpace(game))
                        throw new InvalidOperationException(
                            "Scrap Mechanic was not found.");
                }, ref passed, ref failed);
                if (String.IsNullOrWhiteSpace(game)) return false;
                string source = Path.Combine(game, "Data", "Objects", "Mesh",
                    "plants", "obj_plants_leafplant.fbx");
                string projectRoot = Path.Combine(root, "Project");
                string projectPath = Path.Combine(projectRoot,
                    ToolForgeProjectService.ManifestFileName);
                ToolForgeProject project = null;
                RunCase(output, "ASCII FBX project import", delegate
                {
                    project = ToolForgeProjectService.CreateTreeSaplingsProject(
                        source, projectPath, game);
                    project.IntegrationSourcePath =
                        ToolForgeUtilities.FindIntegrationSource(
                            Environment.CurrentDirectory);
                    if (!project.SourceMesh.OrientationAnalyzed)
                        throw new InvalidOperationException(
                            "The source orientation was not analyzed.");
                    ToolForgeProjectService.Save(projectPath, project);
                }, ref passed, ref failed);
                if (project == null) return false;
                RunCase(output, "schema-1 dual-profile migration", delegate
                {
                    ToolForgeProject legacy = ToolForgeProject.CreateTreeSaplings(
                        "Legacy", game);
                    legacy.SchemaVersion = 1;
                    legacy.TemplateVersion = 2;
                    legacy.FirstPersonTransform = null;
                    legacy.ThirdPersonTransform = null;
                    legacy.Transform = ToolTransform.CreateDefault();
                    legacy.Transform.PositionX = 19.5;
                    legacy.Transform.RotationY = 33.0;
                    legacy.Normalize();
                    if (legacy.SchemaVersion != 2 || legacy.Transform != null ||
                        legacy.FirstPersonTransform.PositionX != 19.5 ||
                        legacy.ThirdPersonTransform.PositionX != 19.5 ||
                        legacy.FirstPersonTransform.RotationY != 33.0 ||
                        legacy.ThirdPersonTransform.RotationY != 33.0 ||
                        Object.ReferenceEquals(legacy.FirstPersonTransform,
                            legacy.ThirdPersonTransform))
                        throw new InvalidOperationException(
                            "The legacy attachment transform was not cloned safely.");
                }, ref passed, ref failed);
                RunCase(output, "project validation", delegate
                {
                    ValidationReport report = ToolForgeValidator.Validate(project,
                        projectPath, true);
                    if (!report.Valid)
                        throw new InvalidOperationException(
                            ToolForgeUtilities.Serialize(report.Issues));
                }, ref passed, ref failed);
                RunCase(output, "player and tool animation rigs", delegate
                {
                    PreviewAssets assets = ScrapMechanicPreviewAssets.Create(game);
                    if (assets.FirstPersonAnimations.Count == 0 ||
                        assets.ThirdPersonAnimations.Count == 0 ||
                        assets.FirstPersonToolAnimations.Count == 0 ||
                        assets.ThirdPersonToolAnimations.Count == 0)
                        throw new InvalidOperationException(
                            "The Bucket player/tool animation pairing is incomplete.");
                    if (!String.Equals(
                        assets.FirstPersonToolAnimations[0].Name,
                        "bucket_idle", StringComparison.Ordinal) ||
                        !String.Equals(
                        assets.ThirdPersonToolAnimations[0].Name,
                        "bucket_idle", StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "The Bucket tool rig does not start from its idle pose.");
                }, ref passed, ref failed);
                project.FirstPersonTransform.PositionX = 12.5;
                project.FirstPersonTransform.PositionY = 9.75;
                project.FirstPersonTransform.RotationZ = 35.0;
                project.FirstPersonTransform.UniformScale = 0.42;
                project.ThirdPersonTransform.PositionX = -18.0;
                project.ThirdPersonTransform.PositionY = 4.5;
                project.ThirdPersonTransform.RotationY = 22.0;
                project.ThirdPersonTransform.UniformScale = 0.38;
                ToolForgeProjectService.Save(projectPath, project);
                string outputRoot = Path.Combine(root, "Output");
                ToolForgeBuildResult first = null;
                RunCase(output, "staged sapling package", delegate
                {
                    first = SaplingPackageBuilder.Build(project, projectPath,
                        outputRoot);
                    if (!first.Success) throw new InvalidOperationException(
                        ToolForgeUtilities.Serialize(first.Validation.Issues));
                }, ref passed, ref failed);
                if (first != null && first.Success)
                {
                    RunCase(output, "independent FP and TP exports", delegate
                    {
                        string assets = Path.Combine(first.PackagePath,
                            "ScrapLabPackage", "source", "Patching", "Parts",
                            "TreeSaplings");
                        string fp = Path.Combine(assets,
                            "TreeSaplingHeldFp.dae");
                        string tp = Path.Combine(assets,
                            "TreeSaplingHeldTp.dae");
                        if (!File.Exists(fp) || !File.Exists(tp) ||
                            String.Equals(ToolForgeUtilities.Sha256File(fp),
                                ToolForgeUtilities.Sha256File(tp),
                                StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException(
                                "FP and TP transforms did not produce independent meshes.");
                        string visual = File.ReadAllText(Path.Combine(assets,
                            "TreeSaplingVisual.generated.lua"));
                        if (!visual.Contains("TreeSaplingHeldFp.rend") ||
                            !visual.Contains("TreeSaplingHeldTp.rend") ||
                            visual.Contains("local HeldRenderable ="))
                            throw new InvalidOperationException(
                                "The runtime visual did not separate FP and TP assets.");
                    }, ref passed, ref failed);
                    RunCase(output, "safe baked attachment transform", delegate
                    {
                        string dae = Path.Combine(first.PackagePath,
                            "ScrapLabPackage", "source", "Patching", "Parts",
                            "TreeSaplings", "TreeSaplingHeldFp.dae");
                        string daeText = File.ReadAllText(dae);
                        string rendText = File.ReadAllText(Path.Combine(
                            first.PackagePath, "ScrapLabPackage", "source",
                            "Patching", "Parts", "TreeSaplings",
                            "TreeSaplingHeldFp.rend"));
                        if (!rendText.Contains("TreeSaplingHeldFp.dae") ||
                            rendText.Contains("TreeSaplingHeldFp.fbx"))
                            throw new InvalidOperationException(
                                "The runtime renderable does not use the Clay-bound DAE.");
                        if (!rendText.Contains("\"subMeshMap\"") ||
                            rendText.Contains("\"subMeshList\""))
                            throw new InvalidOperationException(
                                "The runtime renderable lost its named material binding.");
                        string runtimeFbx = Path.Combine(first.PackagePath,
                            "ScrapLabPackage", "source", "Patching", "Parts",
                            "TreeSaplings", "TreeSaplingHeldFp.fbx");
                        FbxDocument runtimeDocument = FbxDocument.Load(runtimeFbx);
                        FbxMeshData runtimeMesh = runtimeDocument.ExtractMesh(
                            project.SourceMesh.ModelName);
                        FbxDocument sourceDocument = FbxDocument.Load(
                            ToolForgeProjectService.ResolveSourcePath(project,
                                projectPath));
                        FbxMeshData sourceMesh = sourceDocument.ExtractMesh(
                            project.SourceMesh.ModelName);
                        ToolTransform expectedTransform = new ToolTransform
                        {
                            PositionX = project.FirstPersonTransform.PositionX * 0.01,
                            PositionY = -project.FirstPersonTransform.PositionY * 0.01,
                            PositionZ = project.FirstPersonTransform.PositionZ * 0.01,
                            RotationX = project.FirstPersonTransform.RotationX,
                            RotationY = project.FirstPersonTransform.RotationY,
                            RotationZ = project.FirstPersonTransform.RotationZ,
                            UniformScale = project.FirstPersonTransform.UniformScale *
                                ColladaHeldToolGenerator.AutomaticMeshScale(
                                    sourceMesh.Positions)
                        };
                        double[] expectedRuntime =
                            FbxGeometryTransform.Positions(
                                sourceMesh.Positions, expectedTransform);
                        if (runtimeMesh.Positions.Length !=
                            expectedRuntime.Length)
                            throw new InvalidOperationException(
                                "The runtime FBX changed the mesh vertex count.");
                        for (int i = 0; i < expectedRuntime.Length; i++)
                            if (Math.Abs(runtimeMesh.Positions[i] -
                                expectedRuntime[i]) > 0.000001)
                                throw new InvalidOperationException(
                                    "The runtime FBX did not bake the attachment transform into its vertices.");
                        double runtimeSpan = Math.Max(
                            AxisSpan(runtimeMesh.Positions, 0), Math.Max(
                            AxisSpan(runtimeMesh.Positions, 1),
                            AxisSpan(runtimeMesh.Positions, 2)));
                        if (runtimeSpan < 0.1 || runtimeSpan > 10.0)
                            throw new InvalidOperationException(
                                "The runtime FBX is outside character-tool scale.");
                        Match match = Regex.Match(daeText,
                            "<bind_shape_matrix>([^<]+)</bind_shape_matrix>");
                        if (!match.Success)
                            throw new InvalidOperationException(
                                "The generated bind matrix is missing.");
                        string bind = Regex.Replace(match.Groups[1].Value,
                            "\\s+", " ").Trim();
                        const string clay = "0.96581 0 -0.259251 0.003468 " +
                            "0 1 0 0 0.259251 0 0.96581 -0.206261 0 0 0 1";
                        if (!String.Equals(bind, clay,
                            StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "The editable transform leaked into the skin bind.");
                        double[] positions = FloatArray(daeText,
                            "TreeSaplingHeld-POSITION-array");
                        double[] normals = FloatArray(daeText,
                            "TreeSaplingHeld-Normal0-array");
                        if (positions.Length == 0 || positions.Length % 3 != 0 ||
                            normals.Length == 0 || normals.Length % 3 != 0)
                            throw new InvalidOperationException(
                                "The transformed geometry arrays are malformed.");
                        if (positions.Length != runtimeMesh.Positions.Length)
                            throw new InvalidOperationException(
                                "The DAE and FBX vertex counts differ.");
                        for (int i = 0; i < positions.Length; i++)
                            if (Math.Abs(positions[i] -
                                runtimeMesh.Positions[i]) > 0.000001)
                                throw new InvalidOperationException(
                                    "The DAE received a duplicate attachment transform.");
                        foreach (double value in positions)
                            if (Double.IsNaN(value) || Double.IsInfinity(value) ||
                                Math.Abs(value) > 10000)
                                throw new InvalidOperationException(
                                    "The baked attachment geometry is not finite.");
                        double xSpan = AxisSpan(positions, 0);
                        double ySpan = AxisSpan(positions, 1);
                        double zSpan = AxisSpan(positions, 2);
                        double maximumSpan = Math.Max(xSpan,
                            Math.Max(ySpan, zSpan));
                        if (maximumSpan < 0.1 || maximumSpan > 10.0)
                            throw new InvalidOperationException(
                                "The Clay-bound held mesh is outside character-tool scale.");
                        for (int i = 0; i < normals.Length; i += 3)
                        {
                            double length = Math.Sqrt(normals[i] * normals[i] +
                                normals[i + 1] * normals[i + 1] +
                                normals[i + 2] * normals[i + 2]);
                            if (Math.Abs(length - 1.0) > 0.00001)
                                throw new InvalidOperationException(
                                    "A transformed normal was not normalized.");
                        }
                        Match polylist = Regex.Match(daeText,
                            "<polylist count=\"(\\d+)\"[^>]*>.*?<p>([^<]+)</p>",
                            RegexOptions.Singleline);
                        if (!polylist.Success)
                            throw new InvalidOperationException(
                                "The generated triangle list is missing.");
                        int triangleCount = Int32.Parse(
                            polylist.Groups[1].Value,
                            CultureInfo.InvariantCulture);
                        string[] indices = polylist.Groups[2].Value.Split(
                            new char[] { ' ', '\r', '\n', '\t' },
                            StringSplitOptions.RemoveEmptyEntries);
                        if (indices.Length != triangleCount * 9)
                            throw new InvalidOperationException(
                                "The generated triangle indices are incomplete.");
                        ToolPreviewGeometry previewGeometry =
                            ColladaHeldToolGenerator.CreatePreviewGeometry(
                                sourceDocument, project.SourceMesh.ModelName);
                        if (previewGeometry.TriangleCount != triangleCount ||
                            previewGeometry.Positions.Length !=
                                triangleCount * 9)
                            throw new InvalidOperationException(
                                "The live preview did not use the runtime triangle stream.");
                        ToolTransform previewTransform = new ToolTransform
                        {
                            PositionX = project.FirstPersonTransform.PositionX * 0.01,
                            PositionY = -project.FirstPersonTransform.PositionY * 0.01,
                            PositionZ = project.FirstPersonTransform.PositionZ * 0.01,
                            RotationX = project.FirstPersonTransform.RotationX,
                            RotationY = project.FirstPersonTransform.RotationY,
                            RotationZ = project.FirstPersonTransform.RotationZ,
                            UniformScale = project.FirstPersonTransform.UniformScale
                        };
                        double[] previewBaked = FbxGeometryTransform.Positions(
                            previewGeometry.Positions, previewTransform);
                        for (int corner = 0; corner < triangleCount * 3;
                            corner++)
                        {
                            int positionIndex = Int32.Parse(
                                indices[corner * 3],
                                CultureInfo.InvariantCulture);
                            for (int axis = 0; axis < 3; axis++)
                                if (Math.Abs(previewBaked[corner * 3 + axis] -
                                    positions[positionIndex * 3 + axis]) >
                                    0.000001)
                                    throw new InvalidOperationException(
                                        "The live preview transform differs from the runtime DAE.");
                        }
                    }, ref passed, ref failed);
                    RunCase(output, "adaptive mesh scale normalization", delegate
                    {
                        double large = ColladaHeldToolGenerator.AutomaticMeshScale(
                            new double[] { 0, 0, 0, 125, 1, 1 });
                        double small = ColladaHeldToolGenerator.AutomaticMeshScale(
                            new double[] { 0, 0, 0, 0.005, 0.001, 0.001 });
                        double normal = ColladaHeldToolGenerator.AutomaticMeshScale(
                            new double[] { 0, 0, 0, 2, 1, 1 });
                        if (Math.Abs(large - 0.01) > 0.0000001 ||
                            Math.Abs(small - 100.0) > 0.0000001 ||
                            Math.Abs(normal - 1.0) > 0.0000001)
                            throw new InvalidOperationException(
                                "Large, small, and normal meshes were not normalized predictably.");
                    }, ref passed, ref failed);
                    Dictionary<string, string> firstHashes =
                        Hashes(first.Manifest);
                    RunCase(output, "deterministic rebuild", delegate
                    {
                        ToolForgeBuildResult second = SaplingPackageBuilder.Build(
                            project, projectPath, outputRoot);
                        Dictionary<string, string> secondHashes =
                            Hashes(second.Manifest);
                        if (firstHashes.Count != secondHashes.Count)
                            throw new InvalidOperationException(
                                "The artifact count changed between identical builds.");
                        foreach (KeyValuePair<string, string> pair in firstHashes)
                            if (!secondHashes.ContainsKey(pair.Key) ||
                                !String.Equals(secondHashes[pair.Key], pair.Value,
                                    StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException(
                                    "Artifact changed: " + pair.Key);
                    }, ref passed, ref failed);
                    RunCase(output, "manual edit protection", delegate
                    {
                        string rend = Path.Combine(first.PackagePath,
                            "ScrapLabPackage", "source", "Patching", "Parts",
                            "TreeSaplings", "TreeSaplingHeldFp.rend");
                        File.AppendAllText(rend, "\nmanual edit\n");
                        bool blocked = false;
                        try
                        {
                            SaplingPackageBuilder.Build(project, projectPath,
                                outputRoot);
                        }
                        catch (InvalidOperationException) { blocked = true; }
                        if (!blocked) throw new InvalidOperationException(
                            "A manually edited output was overwritten.");
                    }, ref passed, ref failed);
                }
                RunCase(output, "Blender 5 binary FBX import and build", delegate
                {
                    string binary = Path.Combine(root, "blender5-binary.fbx");
                    using (Stream resource = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "ScrapLab.ToolForge.Tests.Blender5Binary.fbx"))
                    {
                        if (resource == null)
                            throw new InvalidOperationException(
                                "The embedded Blender 5 FBX fixture is missing.");
                        using (FileStream file = File.Create(binary))
                            resource.CopyTo(file);
                    }
                    FbxDocument document = FbxDocument.Load(binary);
                    if (!document.IsBinary || document.Version != "7.4.0")
                        throw new InvalidOperationException(
                            "The Blender binary FBX format was not recognized.");
                    document.InspectMeshes();
                    if (!ContainsName(document.ModelNames, "leafplant3"))
                        throw new InvalidOperationException(
                            "Blender model names: " + String.Join(", ",
                                new List<string>(document.ModelNames).ToArray()));
                    document.RequireModel("leafplant3");
                    document.RequireMaterial("leafplant");
                    string binaryProjectRoot = Path.Combine(root,
                        "BinaryProject");
                    string binaryProjectPath = Path.Combine(binaryProjectRoot,
                        ToolForgeProjectService.ManifestFileName);
                    ToolForgeProject binaryProject = ToolForgeProjectService
                        .CreateTreeSaplingsProject(binary, binaryProjectPath,
                            game);
                    binaryProject.IntegrationSourcePath =
                        ToolForgeUtilities.FindIntegrationSource(
                            Environment.CurrentDirectory);
                    binaryProject.FirstPersonTransform.PositionY = -8.25;
                    binaryProject.FirstPersonTransform.RotationX = 17.0;
                    binaryProject.FirstPersonTransform.UniformScale = 0.65;
                    ToolForgeProjectService.Save(binaryProjectPath,
                        binaryProject);
                    ToolForgeBuildResult result = SaplingPackageBuilder.Build(
                        binaryProject, binaryProjectPath,
                        Path.Combine(root, "BinaryOutput"));
                    if (!result.Success)
                        throw new InvalidOperationException(
                            ToolForgeUtilities.Serialize(
                                result.Validation.Issues));
                    string generated = Path.Combine(result.PackagePath,
                        "ScrapLabPackage", "source", "Patching", "Parts",
                        "TreeSaplings", "TreeSaplingHeldFp.fbx");
                    FbxDocument generatedDocument = FbxDocument.Load(generated);
                    if (!generatedDocument.IsBinary)
                        throw new InvalidOperationException(
                            "The generated FBX did not preserve binary format.");
                    generatedDocument.InspectMeshes();
                }, ref passed, ref failed);
                RunCase(output, "material-matched plant texture atlas", delegate
                {
                    ToolForgeProject poleProject = ToolForgeProject
                        .CreateTreeSaplings("PoleTexture", game);
                    poleProject.SourceMesh.ModelName = "poleplant1";
                    poleProject.SourceMesh.MaterialName = "poleplant";
                    poleProject.SourceMesh.TextureMode = "vanilla-poleplant";
                    string rend = TreeSaplingToolGenerator.GenerateRenderable(
                        poleProject, "fp");
                    if (rend.IndexOf("obj_plants_poleplant_dif.tga",
                        StringComparison.Ordinal) < 0 ||
                        rend.IndexOf("obj_plants_leafplant_dif.tga",
                        StringComparison.Ordinal) >= 0)
                        throw new InvalidOperationException(
                            "The poleplant UV layout was paired with the wrong texture atlas.");
                }, ref passed, ref failed);
                RunCase(output, "truncated binary FBX rejection", delegate
                {
                    string binary = Path.Combine(root, "binary.fbx");
                    File.WriteAllBytes(binary, Encoding.ASCII.GetBytes(
                        "Kaydara FBX Binary  "));
                    bool blocked = false;
                    try { FbxDocument.Load(binary); }
                    catch (InvalidDataException) { blocked = true; }
                    if (!blocked) throw new InvalidOperationException(
                        "A truncated binary FBX was accepted.");
                }, ref passed, ref failed);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch { }
            }
            output.WriteLine("Tool Forge self-test: " + passed + " passed, " +
                failed + " failed.");
            return failed == 0;
        }

        private static void RunCase(TextWriter output, string name,
            Action action, ref int passed, ref int failed)
        {
            try
            {
                action(); passed++;
                output.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                failed++;
                output.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        private static double AxisSpan(double[] values, int axis)
        {
            double minimum = Double.MaxValue, maximum = Double.MinValue;
            for (int i = axis; i < values.Length; i += 3)
            {
                minimum = Math.Min(minimum, values[i]);
                maximum = Math.Max(maximum, values[i]);
            }
            return maximum - minimum;
        }

        private static Dictionary<string, string> Hashes(BuildManifest manifest)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (BuildArtifact artifact in manifest.Artifacts)
                result[artifact.RelativePath] = artifact.Sha256;
            return result;
        }

        private static bool ContainsName(IList<string> values, string expected)
        {
            foreach (string value in values)
                if (String.Equals(value, expected,
                    StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static double[] FloatArray(string dae, string id)
        {
            Match match = Regex.Match(dae,
                "<float_array id=\"" + Regex.Escape(id) +
                "\"[^>]*>([^<]+)</float_array>");
            if (!match.Success) return new double[0];
            string[] fields = match.Groups[1].Value.Split(
                new char[] { ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            double[] values = new double[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                values[i] = Double.Parse(fields[i],
                    CultureInfo.InvariantCulture);
            return values;
        }
    }
}
