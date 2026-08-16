using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ScrapLab.ToolForge
{
    internal sealed class FbxMeshSummary
    {
        public string Name { get; set; }
        public int VertexCount { get; set; }
        public int PolygonCount { get; set; }
    }

    internal sealed class AsciiFbxDocument : FbxDocument
    {
        private sealed class FbxBlock
        {
            internal string Name;
            internal int Start;
            internal int OpenBrace;
            internal int End;
        }

        private readonly string _text;
        private readonly bool _hadBom;
        private readonly string _newLine;
        private readonly List<FbxBlock> _modelBlocks;
        private readonly List<FbxBlock> _geometryBlocks;
        private readonly List<string> _materials;
        private readonly string _version;

        private AsciiFbxDocument(string text, bool hadBom)
        {
            _text = text;
            _hadBom = hadBom;
            _newLine = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                ? "\r\n" : "\n";
            _version = ReadVersion(text);
            _modelBlocks = FindBlocks(text,
                new Regex("Model\\s*:\\s*[-0-9]+\\s*,\\s*\"Model::([^\"]+)\"\\s*,\\s*\"Mesh\"\\s*\\{",
                    RegexOptions.CultureInvariant));
            _geometryBlocks = FindBlocks(text,
                new Regex("Geometry\\s*:\\s*[-0-9]+\\s*,\\s*\"Geometry::([^\"]*)\"\\s*,\\s*\"Mesh\"\\s*\\{",
                    RegexOptions.CultureInvariant));
            _materials = ReadNames(text,
                new Regex("Material\\s*:\\s*[-0-9]+\\s*,\\s*\"Material::([^\"]+)\"",
                    RegexOptions.CultureInvariant));
        }

        internal override string Version { get { return _version; } }
        internal override bool IsBinary { get { return false; } }
        internal bool HadBom { get { return _hadBom; } }
        internal string NewLine { get { return _newLine; } }
        internal override IList<string> ModelNames { get { return Names(_modelBlocks); } }
        internal override IList<string> MaterialNames { get { return _materials.AsReadOnly(); } }

        internal static AsciiFbxDocument LoadAscii(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xef &&
                bytes[1] == 0xbb && bytes[2] == 0xbf;
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes,
                    bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException(
                    "The FBX is not valid ASCII/UTF-8 text.", ex);
            }
            if (text.IndexOf("FBXHeaderExtension", StringComparison.Ordinal) < 0)
                throw new InvalidDataException(
                    "The file does not contain an ASCII FBX header.");
            AsciiFbxDocument document = new AsciiFbxDocument(text, bom);
            if (String.IsNullOrEmpty(document.Version) ||
                !document.Version.StartsWith("7", StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Tool Forge version 1 requires an ASCII FBX 7.x file.");
            return document;
        }

        internal override List<FbxMeshSummary> InspectMeshes()
        {
            List<FbxMeshSummary> result = new List<FbxMeshSummary>();
            foreach (FbxBlock geometry in _geometryBlocks)
            {
                double[] vertices = ReadDoubleArray(geometry, "Vertices");
                int[] indices = ReadIntArray(geometry, "PolygonVertexIndex");
                double[] normals = ReadOptionalDoubleArray(geometry, "Normals");
                double[] uv = ReadOptionalDoubleArray(geometry, "UV");
                int[] uvIndex = ReadOptionalIntArray(geometry, "UVIndex");
                if (vertices.Length == 0 || vertices.Length % 3 != 0)
                    throw new InvalidDataException("Mesh '" + geometry.Name +
                        "' has an invalid vertex array.");
                if (indices.Length == 0)
                    throw new InvalidDataException("Mesh '" + geometry.Name +
                        "' has no polygon indices.");
                int polygons = 0;
                foreach (int raw in indices)
                {
                    int index = raw < 0 ? -raw - 1 : raw;
                    if (index < 0 || index >= vertices.Length / 3)
                        throw new InvalidDataException("Mesh '" + geometry.Name +
                            "' contains an out-of-range polygon index.");
                    if (raw < 0) polygons++;
                }
                if (polygons == 0 || indices[indices.Length - 1] >= 0)
                    throw new InvalidDataException("Mesh '" + geometry.Name +
                        "' has an unterminated polygon list.");
                if (normals.Length == 0 || normals.Length % 3 != 0)
                    throw new InvalidDataException("Mesh '" + geometry.Name +
                        "' has missing or invalid normals.");
                if (uv.Length == 0 || uv.Length % 2 != 0)
                    throw new InvalidDataException("Mesh '" + geometry.Name +
                        "' has missing or invalid UV coordinates.");
                foreach (int current in uvIndex)
                    if (current < 0 || current >= uv.Length / 2)
                        throw new InvalidDataException("Mesh '" + geometry.Name +
                            "' contains an out-of-range UV index.");
                result.Add(new FbxMeshSummary
                {
                    Name = geometry.Name,
                    VertexCount = vertices.Length / 3,
                    PolygonCount = polygons
                });
            }
            if (result.Count == 0)
                throw new InvalidDataException("The FBX contains no mesh geometry.");
            return result;
        }

        internal override FbxMeshData ExtractMesh(string selectedModel)
        {
            RequireModel(selectedModel);
            FbxBlock geometry;
            if (_geometryBlocks.Count == 1) geometry = _geometryBlocks[0];
            else geometry = FindUnique(_geometryBlocks, selectedModel,
                "geometry");
            string source = _text.Substring(geometry.OpenBrace + 1,
                geometry.End - geometry.OpenBrace - 1);
            string normalLayer = ReadLayerBody(source, "LayerElementNormal");
            string uvLayer = ReadLayerBody(source, "LayerElementUV");
            return new FbxMeshData
            {
                Name = geometry.Name,
                Positions = ReadDoubleArray(geometry, "Vertices"),
                PolygonVertexIndices = ReadIntArray(geometry,
                    "PolygonVertexIndex"),
                Normals = ReadDoubleArrayFrom(normalLayer, "Normals", false),
                NormalIndices = ReadIntArrayFrom(normalLayer,
                    "NormalsIndex", true),
                NormalMapping = ReadLayerString(normalLayer,
                    "MappingInformationType"),
                NormalReference = ReadLayerString(normalLayer,
                    "ReferenceInformationType"),
                Texcoords = ReadDoubleArrayFrom(uvLayer, "UV", false),
                TexcoordIndices = ReadIntArrayFrom(uvLayer, "UVIndex", true),
                TexcoordMapping = ReadLayerString(uvLayer,
                    "MappingInformationType"),
                TexcoordReference = ReadLayerString(uvLayer,
                    "ReferenceInformationType")
            };
        }

        internal override byte[] CreateTransformedCopy(string selectedModel,
            ToolTransform transform)
        {
            FbxBlock model = FindUnique(_modelBlocks, selectedModel, "model");
            FbxBlock geometry = _geometryBlocks.Count == 1
                ? _geometryBlocks[0] : FindUnique(_geometryBlocks,
                    selectedModel, "geometry");
            string modelBlock = _text.Substring(model.Start,
                model.End - model.Start + 1);
            modelBlock = ReplaceTransform(modelBlock, selectedModel,
                "Lcl Translation", 0.0, 0.0, 0.0);
            modelBlock = ReplaceTransform(modelBlock, selectedModel,
                "Lcl Rotation", 0.0, 0.0, 0.0);
            modelBlock = ReplaceTransform(modelBlock, selectedModel,
                "Lcl Scaling", 1.0, 1.0, 1.0);

            string geometryBlock = _text.Substring(geometry.Start,
                geometry.End - geometry.Start + 1);
            geometryBlock = ReplaceDoubleArray(geometryBlock, "Vertices",
                FbxGeometryTransform.Positions(
                    ReadDoubleArray(geometry, "Vertices"), transform));
            double[] sourceNormals = ReadOptionalDoubleArray(geometry,
                "Normals");
            if (sourceNormals.Length == 0)
                throw new InvalidDataException("Mesh '" + geometry.Name +
                    "' has no normals to transform.");
            geometryBlock = ReplaceDoubleArray(geometryBlock, "Normals",
                FbxGeometryTransform.Normals(sourceNormals, transform));

            string output = _text;
            if (model.Start > geometry.Start)
            {
                output = ReplaceSpan(output, model.Start, model.End,
                    modelBlock);
                output = ReplaceSpan(output, geometry.Start, geometry.End,
                    geometryBlock);
            }
            else
            {
                output = ReplaceSpan(output, geometry.Start, geometry.End,
                    geometryBlock);
                output = ReplaceSpan(output, model.Start, model.End,
                    modelBlock);
            }
            byte[] body = ToolForgeUtilities.Utf8NoBom.GetBytes(output);
            if (!_hadBom) return body;
            byte[] bytes = new byte[body.Length + 3];
            bytes[0] = 0xef; bytes[1] = 0xbb; bytes[2] = 0xbf;
            Buffer.BlockCopy(body, 0, bytes, 3, body.Length);
            return bytes;
        }

        private string ReplaceDoubleArray(string block, string name,
            double[] values)
        {
            Match match = Regex.Match(block, "(?m)^\\s*" +
                Regex.Escape(name) + "\\s*:\\s*\\*?\\d*\\s*\\{",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                throw new InvalidDataException("The FBX geometry is missing " +
                    name + ".");
            int open = block.IndexOf('{', match.Index);
            int close = FindClosingBrace(block, open);
            if (close < 0)
                throw new InvalidDataException("The FBX " + name +
                    " array is unterminated.");
            string container = block.Substring(open + 1,
                close - open - 1);
            Match array = Regex.Match(container,
                "(?s)(\\ba\\s*:\\s*)(.*)",
                RegexOptions.CultureInvariant);
            if (!array.Success)
                throw new InvalidDataException("The FBX " + name +
                    " array has no values.");
            StringBuilder encoded = new StringBuilder(array.Groups[1].Value);
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) encoded.Append(',');
                encoded.Append(ToolForgeUtilities.Number(values[i]));
            }
            string replacement = container.Substring(0, array.Index) +
                encoded.ToString();
            return block.Substring(0, open + 1) + replacement +
                block.Substring(close);
        }

        private static string ReplaceSpan(string source, int start, int end,
            string replacement)
        {
            return source.Substring(0, start) + replacement +
                source.Substring(end + 1);
        }

        internal override void RequireModel(string name)
        {
            FindUnique(_modelBlocks, name, "model");
        }

        internal override void RequireMaterial(string name)
        {
            int count = 0;
            foreach (string material in _materials)
                if (String.Equals(material, name,
                    StringComparison.OrdinalIgnoreCase)) count++;
            if (count != 1)
                throw new InvalidDataException("The selected material '" + name +
                    "' must appear exactly once; found " + count + ".");
        }

        private string ReplaceTransform(string block, string modelName,
            string property, double x, double y, double z)
        {
            Regex regex = new Regex("(?m)^([ \\t]*)P\\s*:\\s*\"" +
                Regex.Escape(property) + "\"[^\\r\\n]*$",
                RegexOptions.CultureInvariant);
            MatchCollection matches = regex.Matches(block);
            if (matches.Count > 1)
                throw new InvalidDataException("The FBX " + property +
                    " property is duplicated in model '" + modelName + "'.");
            string value = "P: \"" +
                property + "\", \"" + property + "\", \"\", \"A\"," +
                ToolForgeUtilities.Number(x) + "," +
                ToolForgeUtilities.Number(y) + "," +
                ToolForgeUtilities.Number(z);
            if (matches.Count == 1)
            {
                string replacement = matches[0].Groups[1].Value + value;
                return block.Substring(0, matches[0].Index) + replacement +
                    block.Substring(matches[0].Index + matches[0].Length);
            }

            Regex properties = new Regex(
                "(?m)^([ \\t]*)Properties70\\s*:\\s*\\{",
                RegexOptions.CultureInvariant);
            MatchCollection containers = properties.Matches(block);
            if (containers.Count != 1)
                throw new InvalidDataException("Model '" + modelName +
                    "' must contain exactly one Properties70 block before Tool Forge can add " +
                    property + ".");
            Match container = containers[0];
            string insertion = _newLine + container.Groups[1].Value +
                "\t" + value;
            int offset = container.Index + container.Length;
            return block.Substring(0, offset) + insertion +
                block.Substring(offset);
        }

        private double[] ReadDoubleArray(FbxBlock block, string name)
        {
            string body = ReadArrayBody(block, name, false);
            MatchCollection matches = Regex.Matches(body,
                "[-+]?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][-+]?\\d+)?",
                RegexOptions.CultureInvariant);
            double[] output = new double[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                if (!Double.TryParse(matches[i].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out output[i]) ||
                    !ToolForgeUtilities.IsFinite(output[i]))
                    throw new InvalidDataException("The " + name +
                        " array contains an invalid number.");
            return output;
        }

        private int[] ReadIntArray(FbxBlock block, string name)
        {
            string body = ReadArrayBody(block, name, false);
            MatchCollection matches = Regex.Matches(body, "[-+]?\\d+",
                RegexOptions.CultureInvariant);
            int[] output = new int[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                if (!Int32.TryParse(matches[i].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out output[i]))
                    throw new InvalidDataException("The " + name +
                        " array contains an invalid integer.");
            return output;
        }

        private double[] ReadOptionalDoubleArray(FbxBlock block, string name)
        {
            string body = ReadArrayBody(block, name, true);
            if (body == null) return new double[0];
            MatchCollection matches = Regex.Matches(body,
                "[-+]?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][-+]?\\d+)?",
                RegexOptions.CultureInvariant);
            double[] output = new double[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                if (!Double.TryParse(matches[i].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out output[i]))
                    throw new InvalidDataException("The " + name +
                        " array contains an invalid number.");
            return output;
        }

        private int[] ReadOptionalIntArray(FbxBlock block, string name)
        {
            string body = ReadArrayBody(block, name, true);
            if (body == null) return new int[0];
            MatchCollection matches = Regex.Matches(body, "[-+]?\\d+",
                RegexOptions.CultureInvariant);
            int[] output = new int[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                if (!Int32.TryParse(matches[i].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out output[i]))
                    throw new InvalidDataException("The " + name +
                        " array contains an invalid integer.");
            return output;
        }

        private string ReadArrayBody(FbxBlock block, string name, bool optional)
        {
            string source = _text.Substring(block.OpenBrace + 1,
                block.End - block.OpenBrace - 1);
            Match match = Regex.Match(source, "(?m)^\\s*" +
                Regex.Escape(name) + "\\s*:\\s*\\*?\\d*\\s*\\{",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                if (optional) return null;
                throw new InvalidDataException("Mesh '" + block.Name +
                    "' is missing the " + name + " array.");
            }
            int open = source.IndexOf('{', match.Index);
            int close = FindClosingBrace(source, open);
            if (close < 0)
                throw new InvalidDataException("Mesh '" + block.Name +
                    "' has an unterminated " + name + " array.");
            string container = source.Substring(open + 1, close - open - 1);
            Match values = Regex.Match(container, "(?s)\\ba\\s*:\\s*(.*)",
                RegexOptions.CultureInvariant);
            if (!values.Success)
                throw new InvalidDataException("Mesh '" + block.Name +
                    "' has no values in its " + name + " array.");
            return values.Groups[1].Value;
        }

        private static string ReadLayerBody(string source, string name)
        {
            Match match = Regex.Match(source, "(?m)^\\s*" +
                Regex.Escape(name) + "\\s*:\\s*[^\\r\\n{]*\\{",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                throw new InvalidDataException("The FBX mesh is missing " +
                    name + ".");
            int open = source.IndexOf('{', match.Index);
            int close = FindClosingBrace(source, open);
            if (close < 0)
                throw new InvalidDataException("The FBX " + name +
                    " block is unterminated.");
            return source.Substring(open + 1, close - open - 1);
        }

        private static string ReadLayerString(string layer, string name)
        {
            Match match = Regex.Match(layer, "(?m)^\\s*" +
                Regex.Escape(name) + "\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                throw new InvalidDataException("The FBX layer is missing " +
                    name + ".");
            return match.Groups[1].Value;
        }

        private static double[] ReadDoubleArrayFrom(string source,
            string name, bool optional)
        {
            string body = ReadArrayBodyFrom(source, name, optional);
            if (body == null) return new double[0];
            MatchCollection matches = Regex.Matches(body,
                "[-+]?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][-+]?\\d+)?",
                RegexOptions.CultureInvariant);
            double[] output = new double[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                if (!Double.TryParse(matches[i].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out output[i]))
                    throw new InvalidDataException("The " + name +
                        " array contains an invalid number.");
            return output;
        }

        private static int[] ReadIntArrayFrom(string source, string name,
            bool optional)
        {
            string body = ReadArrayBodyFrom(source, name, optional);
            if (body == null) return new int[0];
            MatchCollection matches = Regex.Matches(body, "[-+]?\\d+",
                RegexOptions.CultureInvariant);
            int[] output = new int[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                if (!Int32.TryParse(matches[i].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out output[i]))
                    throw new InvalidDataException("The " + name +
                        " array contains an invalid integer.");
            return output;
        }

        private static string ReadArrayBodyFrom(string source, string name,
            bool optional)
        {
            Match match = Regex.Match(source, "(?m)^\\s*" +
                Regex.Escape(name) + "\\s*:\\s*\\*?\\d*\\s*\\{",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                if (optional) return null;
                throw new InvalidDataException("The FBX layer is missing " +
                    name + ".");
            }
            int open = source.IndexOf('{', match.Index);
            int close = FindClosingBrace(source, open);
            string container = source.Substring(open + 1, close - open - 1);
            Match values = Regex.Match(container, "(?s)\\ba\\s*:\\s*(.*)",
                RegexOptions.CultureInvariant);
            if (!values.Success)
                throw new InvalidDataException("The FBX " + name +
                    " array has no values.");
            return values.Groups[1].Value;
        }

        private static FbxBlock FindUnique(List<FbxBlock> blocks,
            string name, string kind)
        {
            FbxBlock found = null;
            int count = 0;
            foreach (FbxBlock block in blocks)
                if (String.Equals(block.Name, name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    found = block;
                    count++;
                }
            if (count != 1)
                throw new InvalidDataException("The selected FBX " + kind +
                    " '" + name + "' must appear exactly once; found " +
                    count + ".");
            return found;
        }

        private static IList<string> Names(List<FbxBlock> blocks)
        {
            List<string> output = new List<string>();
            foreach (FbxBlock block in blocks) output.Add(block.Name);
            return output.AsReadOnly();
        }

        private static List<string> ReadNames(string text, Regex regex)
        {
            List<string> output = new List<string>();
            foreach (Match match in regex.Matches(text))
                output.Add(match.Groups[1].Value);
            return output;
        }

        private static List<FbxBlock> FindBlocks(string text, Regex regex)
        {
            List<FbxBlock> output = new List<FbxBlock>();
            foreach (Match match in regex.Matches(text))
            {
                int open = text.IndexOf('{', match.Index);
                int close = FindClosingBrace(text, open);
                if (open < 0 || close < 0)
                    throw new InvalidDataException(
                        "The FBX contains an unterminated object block.");
                output.Add(new FbxBlock
                {
                    Name = match.Groups[1].Value,
                    Start = match.Index,
                    OpenBrace = open,
                    End = close
                });
            }
            return output;
        }

        private static int FindClosingBrace(string text, int open)
        {
            if (open < 0 || open >= text.Length || text[open] != '{') return -1;
            int depth = 0;
            bool quoted = false, escaped = false, comment = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (comment)
                {
                    if (c == '\r' || c == '\n') comment = false;
                    continue;
                }
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == ';') { comment = true; continue; }
                if (c == '"') { quoted = true; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return i;
            }
            return -1;
        }

        private static string ReadVersion(string text)
        {
            Match numeric = Regex.Match(text, "FBXVersion\\s*:\\s*(\\d+)",
                RegexOptions.CultureInvariant);
            if (numeric.Success)
            {
                string value = numeric.Groups[1].Value;
                if (value.Length >= 4)
                    return value.Substring(0, 1) + "." +
                        value.Substring(1, 1) + "." + value.Substring(2);
                return value;
            }
            Match comment = Regex.Match(text, "FBX\\s+(7\\.[0-9.]+)",
                RegexOptions.CultureInvariant);
            return comment.Success ? comment.Groups[1].Value : String.Empty;
        }

    }
}
