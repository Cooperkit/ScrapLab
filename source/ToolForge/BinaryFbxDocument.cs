using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ScrapLab.ToolForge
{
    internal sealed class BinaryFbxDocument : FbxDocument
    {
        private sealed class Property
        {
            internal char Type;
            internal object Value;
            internal byte[] Raw;

            internal static Property String(string value)
            {
                return new Property { Type = 'S', Value = value ?? System.String.Empty };
            }

            internal static Property Double(double value)
            {
                return new Property { Type = 'D', Value = value };
            }
        }

        private sealed class Node
        {
            internal string Name;
            internal List<Property> Properties = new List<Property>();
            internal List<Node> Children = new List<Node>();
            internal bool HasChildSentinel;
        }

        private const string BinarySignature = "Kaydara FBX Binary  \0\x1a\0";
        private readonly byte[] _header;
        private readonly byte[] _footer;
        private readonly uint _numericVersion;
        private readonly bool _wideOffsets;
        private readonly List<Node> _nodes;
        private readonly List<Node> _modelNodes;
        private readonly List<Node> _geometryNodes;
        private readonly List<Node> _materialNodes;

        private BinaryFbxDocument(byte[] header, byte[] footer, uint version,
            List<Node> nodes)
        {
            _header = header;
            _footer = footer;
            _numericVersion = version;
            _wideOffsets = version >= 7500;
            _nodes = nodes;
            _modelNodes = FindObjectNodes(nodes, "Model", "Mesh");
            _geometryNodes = FindObjectNodes(nodes, "Geometry", "Mesh");
            _materialNodes = FindObjectNodes(nodes, "Material", null);
        }

        internal override string Version
        {
            get
            {
                return (_numericVersion / 1000).ToString(
                    CultureInfo.InvariantCulture) + "." +
                    ((_numericVersion % 1000) / 100).ToString(
                        CultureInfo.InvariantCulture) + "." +
                    (_numericVersion % 100).ToString(
                        CultureInfo.InvariantCulture);
            }
        }

        internal override bool IsBinary { get { return true; } }
        internal override IList<string> ModelNames
        {
            get { return Names(_modelNodes, "Model::"); }
        }
        internal override IList<string> MaterialNames
        {
            get { return Names(_materialNodes, "Material::"); }
        }

        internal static BinaryFbxDocument LoadBinary(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 27)
                throw new InvalidDataException("The binary FBX header is truncated.");
            string signature = Encoding.ASCII.GetString(bytes, 0, 23);
            if (!String.Equals(signature, BinarySignature,
                StringComparison.Ordinal))
                throw new InvalidDataException("The binary FBX signature is invalid.");
            uint version = BitConverter.ToUInt32(bytes, 23);
            if (version < 7100 || version >= 8000)
                throw new InvalidDataException(
                    "Tool Forge supports binary FBX 7.1 through 7.x; this file reports version " +
                    version.ToString(CultureInfo.InvariantCulture) + ".");
            bool wide = version >= 7500;
            List<Node> nodes = new List<Node>();
            byte[] footer;
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream,
                Encoding.UTF8, true))
            {
                stream.Position = 27;
                while (stream.Position < stream.Length)
                {
                    bool isNull;
                    Node node = ReadNode(reader, wide, stream.Length, 0,
                        out isNull);
                    if (isNull)
                    {
                        footer = reader.ReadBytes(checked((int)(
                            stream.Length - stream.Position)));
                        byte[] header = new byte[27];
                        Buffer.BlockCopy(bytes, 0, header, 0, header.Length);
                        BinaryFbxDocument document = new BinaryFbxDocument(
                            header, footer, version, nodes);
                        document.ValidateObjectTable();
                        return document;
                    }
                    nodes.Add(node);
                }
            }
            throw new InvalidDataException(
                "The binary FBX is missing its top-level terminator.");
        }

        internal override List<FbxMeshSummary> InspectMeshes()
        {
            List<FbxMeshSummary> output = new List<FbxMeshSummary>();
            foreach (Node geometry in _geometryNodes)
            {
                double[] vertices = ReadDoubleArray(RequireDescendant(
                    geometry, "Vertices"));
                int[] indices = ReadIntArray(RequireDescendant(
                    geometry, "PolygonVertexIndex"));
                double[] normals = ReadDoubleArray(RequireDescendant(
                    geometry, "Normals"));
                double[] uv = ReadDoubleArray(RequireDescendant(geometry, "UV"));
                Node uvIndexNode = FindDescendant(geometry, "UVIndex");
                int[] uvIndices = uvIndexNode == null
                    ? new int[0] : ReadIntArray(uvIndexNode);
                string name = ObjectName(geometry, "Geometry::");
                if (vertices.Length == 0 || vertices.Length % 3 != 0)
                    throw new InvalidDataException("Mesh '" + name +
                        "' has an invalid vertex array.");
                if (indices.Length == 0)
                    throw new InvalidDataException("Mesh '" + name +
                        "' has no polygon indices.");
                int polygons = 0;
                foreach (int raw in indices)
                {
                    int index = raw < 0 ? -raw - 1 : raw;
                    if (index < 0 || index >= vertices.Length / 3)
                        throw new InvalidDataException("Mesh '" + name +
                            "' contains an out-of-range polygon index.");
                    if (raw < 0) polygons++;
                }
                if (polygons == 0 || indices[indices.Length - 1] >= 0)
                    throw new InvalidDataException("Mesh '" + name +
                        "' has an unterminated polygon list.");
                if (normals.Length == 0 || normals.Length % 3 != 0)
                    throw new InvalidDataException("Mesh '" + name +
                        "' has missing or invalid normals.");
                if (uv.Length == 0 || uv.Length % 2 != 0)
                    throw new InvalidDataException("Mesh '" + name +
                        "' has missing or invalid UV coordinates.");
                foreach (int current in uvIndices)
                    if (current < 0 || current >= uv.Length / 2)
                        throw new InvalidDataException("Mesh '" + name +
                            "' contains an out-of-range UV index.");
                output.Add(new FbxMeshSummary
                {
                    Name = name,
                    VertexCount = vertices.Length / 3,
                    PolygonCount = polygons
                });
            }
            if (output.Count == 0)
                throw new InvalidDataException("The FBX contains no mesh geometry.");
            return output;
        }

        internal override FbxMeshData ExtractMesh(string selectedModel)
        {
            RequireModel(selectedModel);
            Node geometry = SelectGeometry(selectedModel);
            Node normalLayer = RequireDescendant(geometry,
                "LayerElementNormal");
            Node uvLayer = RequireDescendant(geometry, "LayerElementUV");
            Node normalIndex = FindDescendant(normalLayer, "NormalsIndex");
            Node uvIndex = FindDescendant(uvLayer, "UVIndex");
            return new FbxMeshData
            {
                Name = ObjectName(geometry, "Geometry::"),
                Positions = ReadDoubleArray(RequireDescendant(geometry,
                    "Vertices")),
                PolygonVertexIndices = ReadIntArray(RequireDescendant(
                    geometry, "PolygonVertexIndex")),
                Normals = ReadDoubleArray(RequireDescendant(normalLayer,
                    "Normals")),
                NormalIndices = normalIndex == null ? new int[0] :
                    ReadIntArray(normalIndex),
                NormalMapping = ReadLayerString(normalLayer,
                    "MappingInformationType"),
                NormalReference = ReadLayerString(normalLayer,
                    "ReferenceInformationType"),
                Texcoords = ReadDoubleArray(RequireDescendant(uvLayer, "UV")),
                TexcoordIndices = uvIndex == null ? new int[0] :
                    ReadIntArray(uvIndex),
                TexcoordMapping = ReadLayerString(uvLayer,
                    "MappingInformationType"),
                TexcoordReference = ReadLayerString(uvLayer,
                    "ReferenceInformationType")
            };
        }

        internal override byte[] CreateTransformedCopy(string selectedModel,
            ToolTransform transform)
        {
            Node model = FindUnique(_modelNodes, selectedModel, "Model::",
                "model");
            Node geometry = SelectGeometry(selectedModel);
            Node vertices = RequireDescendant(geometry, "Vertices");
            Node normals = RequireDescendant(
                RequireDescendant(geometry, "LayerElementNormal"),
                "Normals");
            WriteDoubleArray(vertices, FbxGeometryTransform.Positions(
                ReadDoubleArray(vertices), transform));
            WriteDoubleArray(normals, FbxGeometryTransform.Normals(
                ReadDoubleArray(normals), transform));

            // Scrap Mechanic's character-tool compiler consumes geometry
            // coordinates but does not preserve arbitrary FBX model-node
            // transforms. Keep the node at identity after baking.
            Node properties = FindDirectUnique(model, "Properties70");
            SetVectorProperty(properties, "Lcl Translation",
                0.0, 0.0, 0.0);
            SetVectorProperty(properties, "Lcl Rotation",
                0.0, 0.0, 0.0);
            SetVectorProperty(properties, "Lcl Scaling",
                1.0, 1.0, 1.0);
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream,
                Encoding.UTF8, true))
            {
                writer.Write(_header);
                foreach (Node node in _nodes) WriteNode(writer, node);
                WriteNullRecord(writer);
                writer.Write(_footer);
                return stream.ToArray();
            }
        }

        internal override void RequireModel(string name)
        {
            FindUnique(_modelNodes, name, "Model::", "model");
        }

        internal override void RequireMaterial(string name)
        {
            FindUnique(_materialNodes, name, "Material::", "material");
        }

        private Node SelectGeometry(string selectedModel)
        {
            if (_geometryNodes.Count == 1) return _geometryNodes[0];
            return FindUnique(_geometryNodes, selectedModel, "Geometry::",
                "geometry");
        }

        private static string ReadLayerString(Node layer, string name)
        {
            Node value = FindDescendant(layer, name);
            if (value == null || value.Properties.Count != 1)
                throw new InvalidDataException("Binary FBX layer is missing " +
                    name + ".");
            string text = PropertyString(value.Properties[0]);
            if (String.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("Binary FBX layer has an invalid " +
                    name + ".");
            return text;
        }

        private void ValidateObjectTable()
        {
            if (_geometryNodes.Count == 0)
                throw new InvalidDataException(
                    "The binary FBX contains no mesh geometry records.");
            if (_modelNodes.Count == 0)
                throw new InvalidDataException(
                    "The binary FBX contains no mesh model records.");
            if (_materialNodes.Count == 0)
                throw new InvalidDataException(
                    "The binary FBX contains no material records.");
        }

        private static Node ReadNode(BinaryReader reader, bool wide,
            long streamLength, int depth, out bool isNull)
        {
            if (depth > 128)
                throw new InvalidDataException(
                    "The binary FBX object tree is nested too deeply.");
            long recordStart = reader.BaseStream.Position;
            ulong endOffset = wide ? reader.ReadUInt64() : reader.ReadUInt32();
            ulong propertyCount = wide ? reader.ReadUInt64() : reader.ReadUInt32();
            ulong propertyLength = wide ? reader.ReadUInt64() : reader.ReadUInt32();
            byte nameLength = reader.ReadByte();
            if (endOffset == 0 && propertyCount == 0 && propertyLength == 0 &&
                nameLength == 0)
            {
                isNull = true;
                return null;
            }
            isNull = false;
            if (endOffset <= (ulong)reader.BaseStream.Position ||
                endOffset > (ulong)streamLength)
                throw new InvalidDataException(
                    "A binary FBX node has an invalid end offset.");
            if (propertyCount > 1000000 || propertyLength > Int32.MaxValue)
                throw new InvalidDataException(
                    "A binary FBX node declares an unreasonable property list.");
            byte[] nameBytes = reader.ReadBytes(nameLength);
            if (nameBytes.Length != nameLength)
                throw new EndOfStreamException(
                    "The binary FBX node name is truncated.");
            Node node = new Node
            {
                Name = Encoding.UTF8.GetString(nameBytes)
            };
            long propertyStart = reader.BaseStream.Position;
            for (ulong i = 0; i < propertyCount; i++)
                node.Properties.Add(ReadProperty(reader));
            if ((ulong)(reader.BaseStream.Position - propertyStart) !=
                propertyLength)
                throw new InvalidDataException("Binary FBX node '" + node.Name +
                    "' has a mismatched property-list length.");
            int nullLength = wide ? 25 : 13;
            while ((ulong)reader.BaseStream.Position < endOffset)
            {
                long remaining = checked((long)endOffset -
                    reader.BaseStream.Position);
                if (remaining == nullLength)
                {
                    bool childNull;
                    ReadNode(reader, wide, streamLength, depth + 1,
                        out childNull);
                    if (!childNull)
                        throw new InvalidDataException("Binary FBX node '" +
                            node.Name + "' has an invalid child terminator.");
                    node.HasChildSentinel = true;
                    break;
                }
                bool isChildNull;
                Node child = ReadNode(reader, wide, streamLength, depth + 1,
                    out isChildNull);
                if (isChildNull)
                    throw new InvalidDataException("Binary FBX node '" +
                        node.Name + "' ended before its declared offset.");
                node.Children.Add(child);
            }
            if ((ulong)reader.BaseStream.Position != endOffset)
                throw new InvalidDataException("Binary FBX node '" + node.Name +
                    "' did not end at its declared offset (record " +
                    recordStart.ToString(CultureInfo.InvariantCulture) + ").");
            return node;
        }

        private static Property ReadProperty(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            char type = (char)reader.ReadByte();
            object value = null;
            switch (type)
            {
                case 'Y': value = reader.ReadInt16(); break;
                case 'C': value = reader.ReadByte() != 0; break;
                case 'I': value = reader.ReadInt32(); break;
                case 'F': value = reader.ReadSingle(); break;
                case 'D': value = reader.ReadDouble(); break;
                case 'L': value = reader.ReadInt64(); break;
                case 'S':
                {
                    uint length = reader.ReadUInt32();
                    value = Encoding.UTF8.GetString(ReadExact(reader, length));
                    break;
                }
                case 'R':
                {
                    uint length = reader.ReadUInt32();
                    ReadExact(reader, length);
                    break;
                }
                case 'f': case 'd': case 'l': case 'i': case 'b': case 'c':
                {
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    uint compressedLength = reader.ReadUInt32();
                    ReadExact(reader, compressedLength);
                    break;
                }
                default:
                    throw new InvalidDataException(
                        "Unsupported binary FBX property type '" + type + "'.");
            }
            long end = reader.BaseStream.Position;
            reader.BaseStream.Position = start;
            byte[] raw = reader.ReadBytes(checked((int)(end - start)));
            reader.BaseStream.Position = end;
            return new Property { Type = type, Value = value, Raw = raw };
        }

        private static byte[] ReadExact(BinaryReader reader, uint length)
        {
            if (length > Int32.MaxValue)
                throw new InvalidDataException(
                    "A binary FBX property is unreasonably large.");
            byte[] value = reader.ReadBytes((int)length);
            if (value.Length != (int)length)
                throw new EndOfStreamException(
                    "A binary FBX property is truncated.");
            return value;
        }

        private static void WriteNode(BinaryWriter writer, Node node)
        {
            byte[] name = Encoding.UTF8.GetBytes(node.Name ?? String.Empty);
            if (name.Length > Byte.MaxValue)
                throw new InvalidDataException(
                    "A binary FBX node name exceeds 255 bytes.");
            List<byte[]> encoded = new List<byte[]>();
            ulong propertyLength = 0;
            foreach (Property property in node.Properties)
            {
                byte[] current = EncodeProperty(property);
                encoded.Add(current);
                propertyLength += (ulong)current.Length;
            }
            long offsetPosition = writer.BaseStream.Position;
            if (writer.BaseStream.Position < 27)
                throw new InvalidDataException(
                    "The binary FBX writer lost its header position.");
            bool wide = BitConverter.ToUInt32(
                ((MemoryStream)writer.BaseStream).GetBuffer(), 23) >= 7500;
            if (wide)
            {
                writer.Write((ulong)0);
                writer.Write((ulong)encoded.Count);
                writer.Write(propertyLength);
            }
            else
            {
                if (propertyLength > UInt32.MaxValue)
                    throw new InvalidDataException(
                        "A binary FBX property list exceeds the 32-bit format limit.");
                writer.Write((uint)0);
                writer.Write((uint)encoded.Count);
                writer.Write((uint)propertyLength);
            }
            writer.Write((byte)name.Length);
            writer.Write(name);
            foreach (byte[] property in encoded) writer.Write(property);
            foreach (Node child in node.Children) WriteNode(writer, child);
            if (node.HasChildSentinel || node.Children.Count > 0)
                WriteNullRecord(writer);
            ulong endOffset = checked((ulong)writer.BaseStream.Position);
            long endPosition = writer.BaseStream.Position;
            writer.BaseStream.Position = offsetPosition;
            if (wide) writer.Write(endOffset);
            else
            {
                if (endOffset > UInt32.MaxValue)
                    throw new InvalidDataException(
                        "The generated FBX exceeds the 32-bit offset limit.");
                writer.Write((uint)endOffset);
            }
            writer.BaseStream.Position = endPosition;
        }

        private static void WriteNullRecord(BinaryWriter writer)
        {
            bool wide = BitConverter.ToUInt32(
                ((MemoryStream)writer.BaseStream).GetBuffer(), 23) >= 7500;
            writer.Write(new byte[wide ? 25 : 13]);
        }

        private static byte[] EncodeProperty(Property property)
        {
            if (property.Raw != null) return property.Raw;
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream,
                Encoding.UTF8, true))
            {
                writer.Write((byte)property.Type);
                if (property.Type == 'S')
                {
                    byte[] value = Encoding.UTF8.GetBytes(
                        Convert.ToString(property.Value,
                            CultureInfo.InvariantCulture) ?? String.Empty);
                    writer.Write((uint)value.Length);
                    writer.Write(value);
                }
                else if (property.Type == 'D')
                    writer.Write(Convert.ToDouble(property.Value,
                        CultureInfo.InvariantCulture));
                else throw new InvalidDataException(
                    "Tool Forge cannot generate binary FBX property type '" +
                    property.Type + "'.");
                return stream.ToArray();
            }
        }

        private static void SetVectorProperty(Node properties, string name,
            double x, double y, double z)
        {
            List<Node> matches = new List<Node>();
            foreach (Node child in properties.Children)
                if (String.Equals(child.Name, "P", StringComparison.Ordinal) &&
                    child.Properties.Count > 0 &&
                    String.Equals(PropertyString(child.Properties[0]), name,
                        StringComparison.Ordinal))
                    matches.Add(child);
            if (matches.Count > 1)
                throw new InvalidDataException("The binary FBX " + name +
                    " property is duplicated.");
            Node property;
            if (matches.Count == 0)
            {
                property = new Node { Name = "P" };
                property.Properties.Add(Property.String(name));
                property.Properties.Add(Property.String(name));
                property.Properties.Add(Property.String(String.Empty));
                property.Properties.Add(Property.String("A"));
                property.Properties.Add(Property.Double(x));
                property.Properties.Add(Property.Double(y));
                property.Properties.Add(Property.Double(z));
                properties.Children.Insert(0, property);
                properties.HasChildSentinel = true;
                return;
            }
            property = matches[0];
            if (property.Properties.Count < 7)
                throw new InvalidDataException("The binary FBX " + name +
                    " property does not contain three values.");
            SetNumeric(property.Properties[property.Properties.Count - 3], x);
            SetNumeric(property.Properties[property.Properties.Count - 2], y);
            SetNumeric(property.Properties[property.Properties.Count - 1], z);
        }

        private static void SetNumeric(Property property, double value)
        {
            if (property.Type != 'D' && property.Type != 'F' &&
                property.Type != 'I' && property.Type != 'L')
                throw new InvalidDataException(
                    "A binary FBX transform value is not numeric.");
            property.Type = 'D';
            property.Value = value;
            property.Raw = null;
        }

        private static double[] ReadDoubleArray(Node node)
        {
            Property property = RequireArray(node);
            uint count;
            byte[] data = DecodeArray(property, out count);
            double[] result = new double[checked((int)count)];
            using (BinaryReader reader = new BinaryReader(
                new MemoryStream(data, false)))
            {
                if (property.Type == 'd')
                    for (int i = 0; i < result.Length; i++)
                        result[i] = reader.ReadDouble();
                else if (property.Type == 'f')
                    for (int i = 0; i < result.Length; i++)
                        result[i] = reader.ReadSingle();
                else throw new InvalidDataException("Binary FBX node '" +
                    node.Name + "' does not contain a floating-point array.");
            }
            return result;
        }

        private static int[] ReadIntArray(Node node)
        {
            Property property = RequireArray(node);
            uint count;
            byte[] data = DecodeArray(property, out count);
            int[] result = new int[checked((int)count)];
            using (BinaryReader reader = new BinaryReader(
                new MemoryStream(data, false)))
            {
                if (property.Type == 'i')
                    for (int i = 0; i < result.Length; i++)
                        result[i] = reader.ReadInt32();
                else if (property.Type == 'l')
                    for (int i = 0; i < result.Length; i++)
                    {
                        long value = reader.ReadInt64();
                        if (value < Int32.MinValue || value > Int32.MaxValue)
                            throw new InvalidDataException(
                                "A binary FBX index exceeds the 32-bit range.");
                        result[i] = (int)value;
                    }
                else throw new InvalidDataException("Binary FBX node '" +
                    node.Name + "' does not contain an integer array.");
            }
            return result;
        }

        private static void WriteDoubleArray(Node node, double[] values)
        {
            Property property = RequireArray(node);
            if (property.Type != 'd' && property.Type != 'f')
                throw new InvalidDataException("Binary FBX node '" +
                    node.Name + "' does not contain a floating-point array.");
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream,
                Encoding.UTF8, true))
            {
                writer.Write((byte)property.Type);
                writer.Write(checked((uint)values.Length));
                writer.Write((uint)0); // uncompressed, deterministic payload
                int width = property.Type == 'd' ? 8 : 4;
                writer.Write(checked((uint)(values.Length * width)));
                foreach (double value in values)
                    if (property.Type == 'd') writer.Write(value);
                    else writer.Write(checked((float)value));
                property.Raw = stream.ToArray();
                property.Value = null;
            }
        }

        private static Property RequireArray(Node node)
        {
            if (node.Properties.Count != 1 ||
                "fdlibc".IndexOf(node.Properties[0].Type) < 0)
                throw new InvalidDataException("Binary FBX node '" + node.Name +
                    "' must contain exactly one array property.");
            return node.Properties[0];
        }

        private static byte[] DecodeArray(Property property, out uint count)
        {
            using (BinaryReader reader = new BinaryReader(
                new MemoryStream(property.Raw, false)))
            {
                reader.ReadByte();
                count = reader.ReadUInt32();
                uint encoding = reader.ReadUInt32();
                uint compressedLength = reader.ReadUInt32();
                byte[] payload = ReadExact(reader, compressedLength);
                int width = property.Type == 'd' || property.Type == 'l' ? 8 :
                    property.Type == 'f' || property.Type == 'i' ? 4 : 1;
                long expected = checked((long)count * width);
                if (expected > Int32.MaxValue)
                    throw new InvalidDataException(
                        "A binary FBX array is too large to validate.");
                byte[] data;
                if (encoding == 0) data = payload;
                else if (encoding == 1) data = InflateZlib(payload,
                    checked((int)expected));
                else throw new InvalidDataException(
                    "A binary FBX array uses an unsupported encoding.");
                if (data.Length != (int)expected)
                    throw new InvalidDataException(
                        "A binary FBX array has an unexpected decoded length.");
                return data;
            }
        }

        private static byte[] InflateZlib(byte[] payload, int expected)
        {
            if (payload.Length < 6)
                throw new InvalidDataException(
                    "A compressed binary FBX array is truncated.");
            using (MemoryStream input = new MemoryStream(payload, 2,
                payload.Length - 6, false))
            using (DeflateStream deflate = new DeflateStream(input,
                CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream(expected))
            {
                deflate.CopyTo(output);
                byte[] data = output.ToArray();
                uint expectedAdler = ((uint)payload[payload.Length - 4] << 24) |
                    ((uint)payload[payload.Length - 3] << 16) |
                    ((uint)payload[payload.Length - 2] << 8) |
                    payload[payload.Length - 1];
                if (Adler32(data) != expectedAdler)
                    throw new InvalidDataException(
                        "A compressed binary FBX array failed its Adler-32 check.");
                return data;
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint modulus = 65521;
            uint a = 1, b = 0;
            foreach (byte value in data)
            {
                a = (a + value) % modulus;
                b = (b + a) % modulus;
            }
            return (b << 16) | a;
        }

        private static List<Node> FindObjectNodes(IEnumerable<Node> nodes,
            string nodeName, string objectType)
        {
            List<Node> result = new List<Node>();
            Visit(nodes, delegate(Node node)
            {
                if (!String.Equals(node.Name, nodeName,
                    StringComparison.Ordinal)) return;
                if (objectType != null && (node.Properties.Count < 3 ||
                    !String.Equals(PropertyString(node.Properties[2]), objectType,
                        StringComparison.OrdinalIgnoreCase))) return;
                result.Add(node);
            });
            return result;
        }

        private static void Visit(IEnumerable<Node> nodes, Action<Node> action)
        {
            foreach (Node node in nodes)
            {
                action(node);
                Visit(node.Children, action);
            }
        }

        private static IList<string> Names(IEnumerable<Node> nodes,
            string prefix)
        {
            List<string> names = new List<string>();
            foreach (Node node in nodes) names.Add(ObjectName(node, prefix));
            return names.AsReadOnly();
        }

        private static string ObjectName(Node node, string prefix)
        {
            if (node.Properties.Count < 2)
                throw new InvalidDataException("Binary FBX node '" + node.Name +
                    "' is missing its object name.");
            string name = PropertyString(node.Properties[1]);
            int typedSuffix = name.IndexOf("\0\x01",
                StringComparison.Ordinal);
            if (typedSuffix >= 0) name = name.Substring(0, typedSuffix);
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return name.Substring(prefix.Length);
            return name;
        }

        private static string PropertyString(Property property)
        {
            if (property == null || property.Type != 'S') return String.Empty;
            return property.Value as string ?? String.Empty;
        }

        private static Node FindUnique(List<Node> nodes, string name,
            string prefix, string kind)
        {
            Node found = null;
            int count = 0;
            foreach (Node node in nodes)
                if (String.Equals(ObjectName(node, prefix), name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    found = node;
                    count++;
                }
            if (count != 1)
                throw new InvalidDataException("The selected FBX " + kind +
                    " '" + name + "' must appear exactly once; found " +
                    count + ".");
            return found;
        }

        private static Node FindDirectUnique(Node parent, string name)
        {
            Node found = null;
            int count = 0;
            foreach (Node child in parent.Children)
                if (String.Equals(child.Name, name,
                    StringComparison.Ordinal))
                {
                    found = child;
                    count++;
                }
            if (count != 1)
                throw new InvalidDataException("Binary FBX model '" +
                    ObjectName(parent, "Model::") + "' must contain exactly one " +
                    name + " node; found " + count + ".");
            return found;
        }

        private static Node RequireDescendant(Node parent, string name)
        {
            Node node = FindDescendant(parent, name);
            if (node == null)
                throw new InvalidDataException("Binary FBX geometry '" +
                    ObjectName(parent, "Geometry::") + "' is missing " + name +
                    ".");
            return node;
        }

        private static Node FindDescendant(Node parent, string name)
        {
            Node found = null;
            int count = 0;
            Visit(parent.Children, delegate(Node node)
            {
                if (String.Equals(node.Name, name,
                    StringComparison.Ordinal))
                {
                    found = node;
                    count++;
                }
            });
            if (count > 1)
                throw new InvalidDataException("Binary FBX geometry '" +
                    ObjectName(parent, "Geometry::") + "' contains duplicate " +
                    name + " arrays.");
            return found;
        }
    }
}
