using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace ScrapLab.ToolForge
{
    internal static class ColladaHeldToolGenerator
    {
        // Position controls are expressed in centimetres, while the DAE's
        // geometry must match the roughly 1-2 unit bounds of vanilla Clay.
        // Mesh normalization already performs the required source-size
        // conversion; applying another 0.01 to vertices makes the held mesh
        // effectively invisible.
        private const double CentimetersToGameUnits = 0.01;
        // Build-only normalization keeps wildly different FBX authoring scales
        // usable without changing the source-size editing view. Powers of ten
        // preserve predictable Blender conventions: a roughly 100-unit prop
        // becomes 0.01x, while a tiny prop is enlarged by the inverse rule.
        private const double MinimumNormalizedSpan = 0.1;
        private const double MaximumNormalizedSpan = 10.0;
        private const string VanillaClayBindShape =
            "0.96581 0 -0.259251 0.003468 0 1 0 0 " +
            "0.259251 0 0.96581 -0.206261 0 0 0 1";

        private sealed class Corner
        {
            internal int Position;
            internal int Normal;
            internal int Texcoord;
        }

        internal static string Generate(FbxDocument document,
            string selectedModel, string material, ToolTransform transform)
        {
            FbxMeshData mesh = document.ExtractMesh(selectedModel);
            Validate(mesh);
            List<Corner> triangles = Triangulate(mesh);
            double automaticScale = AutomaticMeshScale(mesh.Positions);
            double[] positions = TransformPositions(mesh.Positions, transform,
                automaticScale);
            double[] normals = TransformNormals(mesh.Normals, transform);
            ValidateTransformedGeometry(positions, normals, triangles,
                mesh.Texcoords.Length / 2);
            string materialId = XmlName(material, "poleplant");
            int vertexCount = mesh.Positions.Length / 3;
            StringBuilder text = new StringBuilder(32768);
            text.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
                .Append("<COLLADA xmlns=\"http://www.collada.org/2005/11/COLLADASchema\" version=\"1.4.1\">\n")
                .Append("  <asset><contributor><authoring_tool>ScrapLab Tool Forge</authoring_tool></contributor><unit meter=\"0.010000\" name=\"centimeter\"/><up_axis>Y_UP</up_axis></asset>\n")
                .Append("  <library_effects><effect id=\"").Append(materialId)
                .Append("-fx\"><profile_COMMON><technique sid=\"common\"><phong><diffuse><color>1 1 1 1</color></diffuse></phong></technique></profile_COMMON></effect></library_effects>\n")
                .Append("  <library_materials><material id=\"").Append(materialId)
                .Append("\" name=\"").Append(XmlEscape(material))
                .Append("\"><instance_effect url=\"#").Append(materialId)
                .Append("-fx\"/></material></library_materials>\n")
                .Append("  <library_geometries><geometry id=\"TreeSaplingHeld-lib\" name=\"TreeSaplingHeld\"><mesh>\n");
            AppendFloatSource(text, "TreeSaplingHeld-POSITION", positions,
                3, new string[] { "X", "Y", "Z" });
            AppendFloatSource(text, "TreeSaplingHeld-Normal0", normals,
                3, new string[] { "X", "Y", "Z" });
            AppendFloatSource(text, "TreeSaplingHeld-UV0", mesh.Texcoords,
                2, new string[] { "S", "T" });
            text.Append("        <vertices id=\"TreeSaplingHeld-VERTEX\"><input semantic=\"POSITION\" source=\"#TreeSaplingHeld-POSITION\"/></vertices>\n")
                .Append("        <polylist count=\"").Append(triangles.Count / 3)
                .Append("\" material=\"").Append(materialId)
                .Append("\"><input semantic=\"VERTEX\" offset=\"0\" source=\"#TreeSaplingHeld-VERTEX\"/><input semantic=\"NORMAL\" offset=\"1\" source=\"#TreeSaplingHeld-Normal0\"/><input semantic=\"TEXCOORD\" offset=\"2\" set=\"0\" source=\"#TreeSaplingHeld-UV0\"/><vcount>");
            for (int i = 0; i < triangles.Count / 3; i++)
                text.Append(i == 0 ? "3" : " 3");
            text.Append("</vcount><p>");
            for (int i = 0; i < triangles.Count; i++)
            {
                Corner corner = triangles[i];
                if (i > 0) text.Append(' ');
                text.Append(corner.Position).Append(' ')
                    .Append(corner.Normal).Append(' ')
                    .Append(corner.Texcoord);
            }
            text.Append("</p></polylist>\n")
                .Append("      </mesh></geometry></library_geometries>\n")
                .Append("  <library_controllers><controller id=\"TreeSaplingHeldController\"><skin source=\"#TreeSaplingHeld-lib\">\n")
                .Append("    <bind_shape_matrix>").Append(VanillaClayBindShape)
                .Append("</bind_shape_matrix>\n")
                .Append("    <source id=\"TreeSaplingHeldController-Joints\"><Name_array id=\"TreeSaplingHeldController-Joints-array\" count=\"1\">root_bucket_jnt</Name_array><technique_common><accessor source=\"#TreeSaplingHeldController-Joints-array\" count=\"1\" stride=\"1\"><param name=\"JOINT\" type=\"Name\"/></accessor></technique_common></source>\n")
                .Append("    <source id=\"TreeSaplingHeldController-Matrices\"><float_array id=\"TreeSaplingHeldController-Matrices-array\" count=\"16\">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</float_array><technique_common><accessor source=\"#TreeSaplingHeldController-Matrices-array\" count=\"1\" stride=\"16\"><param name=\"TRANSFORM\" type=\"float4x4\"/></accessor></technique_common></source>\n")
                .Append("    <source id=\"TreeSaplingHeldController-Weights\"><float_array id=\"TreeSaplingHeldController-Weights-array\" count=\"").Append(vertexCount).Append("\">");
            for (int i = 0; i < vertexCount; i++)
                text.Append(i == 0 ? "1" : " 1");
            text.Append("</float_array><technique_common><accessor source=\"#TreeSaplingHeldController-Weights-array\" count=\"")
                .Append(vertexCount).Append("\" stride=\"1\"><param name=\"WEIGHT\" type=\"float\"/></accessor></technique_common></source>\n")
                .Append("    <joints><input semantic=\"JOINT\" source=\"#TreeSaplingHeldController-Joints\"/><input semantic=\"INV_BIND_MATRIX\" source=\"#TreeSaplingHeldController-Matrices\"/></joints>\n")
                .Append("    <vertex_weights count=\"").Append(vertexCount)
                .Append("\"><input semantic=\"JOINT\" offset=\"0\" source=\"#TreeSaplingHeldController-Joints\"/><input semantic=\"WEIGHT\" offset=\"1\" source=\"#TreeSaplingHeldController-Weights\"/><vcount>");
            for (int i = 0; i < vertexCount; i++)
                text.Append(i == 0 ? "1" : " 1");
            text.Append("</vcount><v>");
            for (int i = 0; i < vertexCount; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append("0 ").Append(i);
            }
            text.Append("</v></vertex_weights>\n")
                .Append("  </skin></controller></library_controllers>\n")
                .Append("  <library_visual_scenes><visual_scene id=\"TreeSaplingHeld\" name=\"TreeSaplingHeld\">\n")
                .Append("    <node name=\"jnt_right_weapon\" id=\"jnt_right_weapon\" sid=\"jnt_right_weapon\" type=\"JOINT\"><matrix sid=\"matrix\">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix><node name=\"root_bucket_jnt\" id=\"root_bucket_jnt\" sid=\"root_bucket_jnt\" type=\"JOINT\"><matrix sid=\"matrix\">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix></node></node>\n")
                .Append("    <node name=\"TreeSaplingHeld\" id=\"TreeSaplingHeld-node\"><instance_controller url=\"#TreeSaplingHeldController\"><bind_material><technique_common><instance_material symbol=\"")
                .Append(materialId).Append("\" target=\"#").Append(materialId)
                .Append("\"/></technique_common></bind_material></instance_controller></node>\n")
                .Append("  </visual_scene></library_visual_scenes><scene><instance_visual_scene url=\"#TreeSaplingHeld\"/></scene>\n")
                .Append("</COLLADA>\n");
            string output = text.ToString();
            using (StringReader reader = new StringReader(output))
            using (XmlReader xml = XmlReader.Create(reader,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
                while (xml.Read()) { }
            return output;
        }

        // The editor must render the same normalized vertex stream that is
        // written into the runtime DAE. Loading the source FBX in Three.js is
        // not equivalent: FBXLoader also applies Blender model transforms,
        // pivots, units, and axis metadata that Scrap Mechanic never sees in
        // the generated Clay-bound controller.
        internal static ToolPreviewGeometry CreatePreviewGeometry(
            FbxDocument document, string selectedModel)
        {
            FbxMeshData mesh = document.ExtractMesh(selectedModel);
            Validate(mesh);
            List<Corner> triangles = Triangulate(mesh);
            double automaticScale = AutomaticMeshScale(mesh.Positions);
            ToolTransform identity = new ToolTransform
            {
                UniformScale = 1.0
            };
            double[] normalizedPositions = TransformPositions(mesh.Positions,
                identity, automaticScale);
            double[] normalizedNormals = TransformNormals(mesh.Normals,
                identity);
            double[] positions = new double[triangles.Count * 3];
            double[] normals = new double[triangles.Count * 3];
            double[] texcoords = new double[triangles.Count * 2];
            for (int i = 0; i < triangles.Count; i++)
            {
                Corner corner = triangles[i];
                Array.Copy(normalizedPositions, corner.Position * 3,
                    positions, i * 3, 3);
                Array.Copy(normalizedNormals, corner.Normal * 3,
                    normals, i * 3, 3);
                Array.Copy(mesh.Texcoords, corner.Texcoord * 2,
                    texcoords, i * 2, 2);
            }
            return new ToolPreviewGeometry
            {
                Positions = positions,
                Normals = normals,
                Texcoords = texcoords,
                AutomaticScale = automaticScale,
                TriangleCount = triangles.Count / 3
            };
        }

        private static void Validate(FbxMeshData mesh)
        {
            if (mesh == null || mesh.Positions == null ||
                mesh.Positions.Length == 0 || mesh.Positions.Length % 3 != 0)
                throw new InvalidDataException("The held-tool mesh has invalid positions.");
            if (mesh.PolygonVertexIndices == null ||
                mesh.PolygonVertexIndices.Length < 3)
                throw new InvalidDataException("The held-tool mesh has no polygons.");
            if (mesh.Normals == null || mesh.Normals.Length == 0 ||
                mesh.Normals.Length % 3 != 0)
                throw new InvalidDataException("The held-tool mesh has invalid normals.");
            if (mesh.Texcoords == null || mesh.Texcoords.Length == 0 ||
                mesh.Texcoords.Length % 2 != 0)
                throw new InvalidDataException("The held-tool mesh has invalid UVs.");
        }

        private static double[] TransformPositions(double[] source,
            ToolTransform transform, double automaticScale)
        {
            double[] rotation = RotationMatrix(transform);
            double scale = transform.UniformScale * automaticScale;
            double tx = transform.PositionX * CentimetersToGameUnits;
            // Scrap Mechanic's held-tool importer presents the character-tool
            // Y translation opposite to Three.js/Tool Forge's visible Up axis.
            // Keep the editor intuitive and perform the basis conversion once
            // at the generated runtime boundary.
            double ty = -transform.PositionY * CentimetersToGameUnits;
            double tz = transform.PositionZ * CentimetersToGameUnits;
            double[] output = new double[source.Length];
            for (int i = 0; i < source.Length; i += 3)
            {
                double x = source[i] * scale;
                double y = source[i + 1] * scale;
                double z = source[i + 2] * scale;
                output[i] = rotation[0] * x + rotation[1] * y +
                    rotation[2] * z + tx;
                output[i + 1] = rotation[3] * x + rotation[4] * y +
                    rotation[5] * z + ty;
                output[i + 2] = rotation[6] * x + rotation[7] * y +
                    rotation[8] * z + tz;
            }
            return output;
        }

        internal static double AutomaticMeshScale(double[] positions)
        {
            if (positions == null || positions.Length < 3 ||
                positions.Length % 3 != 0)
                throw new InvalidDataException(
                    "The held-tool mesh has invalid positions.");
            double minimumX = Double.MaxValue, minimumY = Double.MaxValue,
                minimumZ = Double.MaxValue;
            double maximumX = Double.MinValue, maximumY = Double.MinValue,
                maximumZ = Double.MinValue;
            for (int i = 0; i < positions.Length; i += 3)
            {
                double x = positions[i], y = positions[i + 1],
                    z = positions[i + 2];
                if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
                    throw new InvalidDataException(
                        "The held-tool mesh contains a non-finite position.");
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                minimumZ = Math.Min(minimumZ, z);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
                maximumZ = Math.Max(maximumZ, z);
            }
            double span = Math.Max(maximumX - minimumX,
                Math.Max(maximumY - minimumY, maximumZ - minimumZ));
            if (!IsFinite(span) || span <= 0.000000001)
                throw new InvalidDataException(
                    "The held-tool mesh has no measurable size.");
            double scale = 1.0;
            while (span * scale > MaximumNormalizedSpan) scale *= 0.1;
            while (span * scale < MinimumNormalizedSpan) scale *= 10.0;
            return scale;
        }

        private static double[] TransformNormals(double[] source,
            ToolTransform transform)
        {
            double[] rotation = RotationMatrix(transform);
            double[] output = new double[source.Length];
            for (int i = 0; i < source.Length; i += 3)
            {
                double x = rotation[0] * source[i] +
                    rotation[1] * source[i + 1] +
                    rotation[2] * source[i + 2];
                double y = rotation[3] * source[i] +
                    rotation[4] * source[i + 1] +
                    rotation[5] * source[i + 2];
                double z = rotation[6] * source[i] +
                    rotation[7] * source[i + 1] +
                    rotation[8] * source[i + 2];
                double length = Math.Sqrt(x * x + y * y + z * z);
                if (!IsFinite(length) || length < 0.000000001)
                    throw new InvalidDataException(
                        "The held-tool mesh contains an invalid normal.");
                output[i] = x / length;
                output[i + 1] = y / length;
                output[i + 2] = z / length;
            }
            return output;
        }

        private static double[] RotationMatrix(ToolTransform transform)
        {
            double rx = transform.RotationX * Math.PI / 180.0;
            double ry = transform.RotationY * Math.PI / 180.0;
            double rz = transform.RotationZ * Math.PI / 180.0;
            double cx = Math.Cos(rx), sx = Math.Sin(rx);
            double cy = Math.Cos(ry), sy = Math.Sin(ry);
            double cz = Math.Cos(rz), sz = Math.Sin(rz);
            return new double[] {
                cz * cy, cz * sy * sx - sz * cx,
                cz * sy * cx + sz * sx,
                sz * cy, sz * sy * sx + cz * cx,
                sz * sy * cx - cz * sx,
                -sy, cy * sx, cy * cx
            };
        }

        private static void ValidateTransformedGeometry(double[] positions,
            double[] normals, List<Corner> triangles, int texcoordCount)
        {
            for (int i = 0; i < positions.Length; i++)
                if (!IsFinite(positions[i]) || Math.Abs(positions[i]) > 10000)
                    throw new InvalidDataException(
                        "The attachment transform produced invalid geometry.");
            for (int i = 0; i < normals.Length; i++)
                if (!IsFinite(normals[i]) || Math.Abs(normals[i]) > 1.000001)
                    throw new InvalidDataException(
                        "The attachment transform produced invalid normals.");
            int positionCount = positions.Length / 3;
            int normalCount = normals.Length / 3;
            foreach (Corner corner in triangles)
                if (corner.Position < 0 || corner.Position >= positionCount ||
                    corner.Normal < 0 || corner.Normal >= normalCount ||
                    corner.Texcoord < 0 || corner.Texcoord >= texcoordCount)
                    throw new InvalidDataException(
                        "The attachment transform produced an invalid triangle index.");
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        private static List<Corner> Triangulate(FbxMeshData mesh)
        {
            List<Corner> output = new List<Corner>();
            List<Corner> polygon = new List<Corner>();
            int polygonIndex = 0;
            for (int polygonVertex = 0;
                polygonVertex < mesh.PolygonVertexIndices.Length;
                polygonVertex++)
            {
                int raw = mesh.PolygonVertexIndices[polygonVertex];
                int position = raw < 0 ? -raw - 1 : raw;
                polygon.Add(new Corner
                {
                    Position = position,
                    Normal = ResolveLayerIndex(mesh.NormalMapping,
                        mesh.NormalReference, mesh.NormalIndices,
                        polygonVertex, position, polygonIndex,
                        mesh.Normals.Length / 3, "normal"),
                    Texcoord = ResolveLayerIndex(mesh.TexcoordMapping,
                        mesh.TexcoordReference, mesh.TexcoordIndices,
                        polygonVertex, position, polygonIndex,
                        mesh.Texcoords.Length / 2, "UV")
                });
                if (raw >= 0) continue;
                if (polygon.Count < 3)
                    throw new InvalidDataException(
                        "The held-tool mesh contains a polygon with fewer than three corners.");
                for (int i = 1; i < polygon.Count - 1; i++)
                {
                    output.Add(polygon[0]);
                    output.Add(polygon[i]);
                    output.Add(polygon[i + 1]);
                }
                polygon.Clear();
                polygonIndex++;
            }
            if (polygon.Count != 0)
                throw new InvalidDataException(
                    "The held-tool mesh has an unterminated polygon.");
            return output;
        }

        private static int ResolveLayerIndex(string mapping, string reference,
            int[] indices, int polygonVertex, int position, int polygon,
            int directCount, string label)
        {
            int mapped;
            if (EqualsMode(mapping, "ByPolygonVertex")) mapped = polygonVertex;
            else if (EqualsMode(mapping, "ByVertice") ||
                EqualsMode(mapping, "ByVertex")) mapped = position;
            else if (EqualsMode(mapping, "ByPolygon")) mapped = polygon;
            else if (EqualsMode(mapping, "AllSame")) mapped = 0;
            else throw new InvalidDataException("Unsupported FBX " + label +
                " mapping mode '" + mapping + "'.");
            int direct;
            if (EqualsMode(reference, "Direct")) direct = mapped;
            else if (EqualsMode(reference, "IndexToDirect"))
            {
                if (indices == null || mapped < 0 || mapped >= indices.Length)
                    throw new InvalidDataException("The FBX " + label +
                        " index array is incomplete.");
                direct = indices[mapped];
            }
            else throw new InvalidDataException("Unsupported FBX " + label +
                " reference mode '" + reference + "'.");
            if (direct < 0 || direct >= directCount)
                throw new InvalidDataException("The FBX " + label +
                    " index is outside its direct array.");
            return direct;
        }

        private static bool EqualsMode(string value, string expected)
        {
            return String.Equals(value, expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendFloatSource(StringBuilder text, string id,
            double[] values, int stride, string[] names)
        {
            text.Append("        <source id=\"").Append(id)
                .Append("\"><float_array id=\"").Append(id)
                .Append("-array\" count=\"").Append(values.Length)
                .Append("\">");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(Number(values[i]));
            }
            text.Append("</float_array><technique_common><accessor source=\"#")
                .Append(id).Append("-array\" count=\"")
                .Append(values.Length / stride).Append("\" stride=\"")
                .Append(stride).Append("\">");
            foreach (string name in names)
                text.Append("<param name=\"").Append(name)
                    .Append("\" type=\"float\"/>");
            text.Append("</accessor></technique_common></source>\n");
        }

        private static string Number(double value)
        {
            if (Math.Abs(value) < 0.0000000001) value = 0;
            return value.ToString("0.#########", CultureInfo.InvariantCulture);
        }

        private static string XmlName(string value, string fallback)
        {
            string candidate = String.IsNullOrWhiteSpace(value) ? fallback : value;
            return XmlConvert.EncodeLocalName(candidate);
        }

        private static string XmlEscape(string value)
        {
            return SecurityElementEscape(value ?? String.Empty);
        }

        private static string SecurityElementEscape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
