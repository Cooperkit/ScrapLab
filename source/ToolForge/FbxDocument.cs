using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScrapLab.ToolForge
{
    internal sealed class FbxMeshData
    {
        internal string Name;
        internal double[] Positions;
        internal int[] PolygonVertexIndices;
        internal double[] Normals;
        internal int[] NormalIndices;
        internal string NormalMapping;
        internal string NormalReference;
        internal double[] Texcoords;
        internal int[] TexcoordIndices;
        internal string TexcoordMapping;
        internal string TexcoordReference;
    }

    internal abstract class FbxDocument
    {
        internal abstract string Version { get; }
        internal abstract bool IsBinary { get; }
        internal abstract IList<string> ModelNames { get; }
        internal abstract IList<string> MaterialNames { get; }

        internal abstract List<FbxMeshSummary> InspectMeshes();
        internal abstract FbxMeshData ExtractMesh(string selectedModel);
        internal abstract byte[] CreateTransformedCopy(string selectedModel,
            ToolTransform transform);
        internal abstract void RequireModel(string name);
        internal abstract void RequireMaterial(string name);

        internal static FbxDocument Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The selected FBX was not found.",
                    path);
            byte[] header = new byte[23];
            using (FileStream stream = File.OpenRead(path))
            {
                int read = stream.Read(header, 0, header.Length);
                string value = Encoding.ASCII.GetString(header, 0, read);
                if (value.StartsWith("Kaydara FBX Binary",
                    StringComparison.Ordinal))
                    return BinaryFbxDocument.LoadBinary(path);
            }
            return AsciiFbxDocument.LoadAscii(path);
        }
    }

    internal static class FbxGeometryTransform
    {
        internal static double[] Positions(double[] source,
            ToolTransform transform)
        {
            RequireTriples(source, "positions");
            double[] rotation = RotationMatrix(transform);
            double scale = transform.UniformScale;
            double[] output = new double[source.Length];
            for (int i = 0; i < source.Length; i += 3)
            {
                double x = source[i] * scale;
                double y = source[i + 1] * scale;
                double z = source[i + 2] * scale;
                output[i] = rotation[0] * x + rotation[1] * y +
                    rotation[2] * z + transform.PositionX;
                output[i + 1] = rotation[3] * x + rotation[4] * y +
                    rotation[5] * z + transform.PositionY;
                output[i + 2] = rotation[6] * x + rotation[7] * y +
                    rotation[8] * z + transform.PositionZ;
            }
            ValidateFinite(output, "positions");
            return output;
        }

        internal static double[] Normals(double[] source,
            ToolTransform transform)
        {
            RequireTriples(source, "normals");
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
            if (transform == null)
                throw new InvalidDataException(
                    "The attachment transform is missing.");
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

        private static void RequireTriples(double[] values, string label)
        {
            if (values == null || values.Length == 0 ||
                values.Length % 3 != 0)
                throw new InvalidDataException(
                    "The held-tool mesh has invalid " + label + ".");
        }

        private static void ValidateFinite(double[] values, string label)
        {
            foreach (double value in values)
                if (!IsFinite(value) || Math.Abs(value) > 10000)
                    throw new InvalidDataException(
                        "The attachment transform produced invalid " +
                        label + ".");
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
