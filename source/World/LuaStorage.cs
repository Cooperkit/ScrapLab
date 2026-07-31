using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RaidRescue
{
    internal sealed class LuaEntry
    {
        public object Key;
        public object Value;
    }

    internal sealed class LuaTable
    {
        public bool IsArray;
        public long ArrayOffset;
        public readonly List<LuaEntry> Entries = new List<LuaEntry>();

        public object Get(string name)
        {
            foreach (LuaEntry entry in Entries)
            {
                string key = entry.Key as string;
                if (key != null && String.Equals(key, name, StringComparison.Ordinal))
                    return entry.Value;
            }
            return null;
        }

        public int Count
        {
            get { return Entries.Count; }
        }
    }

    internal sealed class LuaUserData
    {
        public string Type;
        public long Id;
        public string Uuid;
        public float X;
        public float Y;
        public float Z;
        public float W;
    }

    internal sealed class ScriptPayload
    {
        public byte[] Key;
        public int WorldId;
        public int Flags;
        public byte[] Compressed;
        public byte[] Decompressed;
        public int LuaVersion;
        public object Value;
    }

    internal sealed class BitReader
    {
        private readonly byte[] data;
        private long bitIndex;

        public BitReader(byte[] source)
        {
            data = source;
        }

        public long RemainingBits
        {
            get { return ((long)data.Length * 8L) - bitIndex; }
        }

        public long PositionBits
        {
            get { return bitIndex; }
        }

        public ulong ReadUnsigned(int count)
        {
            if (count < 1 || count > 64 || RemainingBits < count)
                throw new InvalidDataException("Unexpected end of the serialized Lua data.");

            ulong value = 0;
            for (int i = 0; i < count; i++)
            {
                int current = data[bitIndex / 8];
                int bit = (current >> (7 - (int)(bitIndex % 8))) & 1;
                value = (value << 1) | (uint)bit;
                bitIndex++;
            }
            return value;
        }

        public long ReadSigned(int count)
        {
            ulong value = ReadUnsigned(count);
            ulong sign = 1UL << (count - 1);
            if ((value & sign) == 0)
                return (long)value;
            return (long)(value - (1UL << count));
        }

        public byte[] ReadBytes(int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = (byte)ReadUnsigned(8);
            return result;
        }

        public void Align()
        {
            long remainder = bitIndex % 8;
            if (remainder != 0)
                bitIndex += 8 - remainder;
        }
    }

    internal static class LuaStorage
    {
        public static ScriptPayload ParseScriptData(byte[] blob)
        {
            if (blob == null || blob.Length < 29)
                throw new InvalidDataException("The ScriptData record is too short.");

            int keyLength = ReadBigUInt16(blob, 16);
            int position = 18 + keyLength;
            if (position + 7 > blob.Length)
                throw new InvalidDataException("The ScriptData header is truncated.");

            byte[] key = Slice(blob, 18, keyLength);
            int worldId = ReadBigUInt16(blob, position);
            int flags = blob[position + 2];
            uint compressedLength = ReadBigUInt32(blob, position + 3);
            if (compressedLength > Int32.MaxValue || position + 7L + compressedLength > blob.Length)
                throw new InvalidDataException("The ScriptData compressed payload is truncated.");

            byte[] compressed = Slice(blob, position + 7, (int)compressedLength);
            byte[] decompressed = DecompressLz4Block(compressed);
            BitReader reader = new BitReader(decompressed);
            byte[] magic = reader.ReadBytes(3);
            if (magic[0] != (byte)'L' || magic[1] != (byte)'U' || magic[2] != (byte)'A')
                throw new InvalidDataException("The raid record does not contain a serialized Lua object.");

            int version = checked((int)reader.ReadUnsigned(32));
            object value = ParseValue(reader, 0);

            return new ScriptPayload
            {
                Key = key,
                WorldId = worldId,
                Flags = flags,
                Compressed = compressed,
                Decompressed = decompressed,
                LuaVersion = version,
                Value = value
            };
        }

        public static byte[] SetRootBoolean(
            byte[] blob, string fieldName, bool value,
            out bool found, out bool originalValue)
        {
            if (String.IsNullOrEmpty(fieldName))
                throw new ArgumentException(
                    "A Lua storage field name is required.",
                    "fieldName");

            ScriptPayload payload = ParseScriptData(blob);
            long bitOffset;
            found = TryFindRootBoolean(
                payload.Decompressed, fieldName,
                out bitOffset, out originalValue);
            if (!found || originalValue == value)
                return blob;

            byte[] decompressed =
                (byte[])payload.Decompressed.Clone();
            SetBit(decompressed, bitOffset, value);
            byte[] compressed =
                CompressLz4LiteralBlock(decompressed);
            byte[] rewritten =
                ReplaceCompressedPayload(blob, compressed);

            ScriptPayload verification =
                ParseScriptData(rewritten);
            long verifiedOffset;
            bool verifiedValue;
            bool verified = TryFindRootBoolean(
                verification.Decompressed, fieldName,
                out verifiedOffset, out verifiedValue);
            if (!verified || verifiedValue != value)
            {
                throw new InvalidDataException(
                    "The rewritten Lua storage field could not be verified.");
            }

            if (payload.WorldId != verification.WorldId ||
                payload.Flags != verification.Flags ||
                !BytesEqual(payload.Key, verification.Key) ||
                payload.LuaVersion != verification.LuaVersion)
            {
                throw new InvalidDataException(
                    "The rewritten Lua storage header changed unexpectedly.");
            }
            return rewritten;
        }

        private static bool TryFindRootBoolean(
            byte[] decompressed, string fieldName,
            out long bitOffset, out bool value)
        {
            bitOffset = -1;
            value = false;
            BitReader reader = new BitReader(decompressed);
            byte[] magic = reader.ReadBytes(3);
            if (magic[0] != (byte)'L' ||
                magic[1] != (byte)'U' ||
                magic[2] != (byte)'A')
            {
                throw new InvalidDataException(
                    "The ScriptData record does not contain a serialized Lua object.");
            }

            reader.ReadUnsigned(32);
            int rootType = checked((int)reader.ReadUnsigned(8));
            if (rootType != 5)
                throw new InvalidDataException(
                    "The ScriptData root value is not a Lua table.");

            uint count = checked((uint)reader.ReadUnsigned(32));
            if (count > 10000000)
                throw new InvalidDataException(
                    "The serialized Lua table is unreasonably large.");
            bool isArray = reader.ReadUnsigned(1) != 0;
            if (isArray)
                throw new InvalidDataException(
                    "The ScriptData root table is unexpectedly an array.");

            bool found = false;
            for (uint index = 0; index < count; index++)
            {
                object key = ParseValue(reader, 1);
                string name = key as string;
                if (String.Equals(
                    name, fieldName, StringComparison.Ordinal))
                {
                    if (found)
                        throw new InvalidDataException(
                            "The Lua storage field appears more than once.");
                    int valueType =
                        checked((int)reader.ReadUnsigned(8));
                    if (valueType != 2)
                        throw new InvalidDataException(
                            "The Lua storage field is not a boolean.");
                    bitOffset = reader.PositionBits;
                    value = reader.ReadUnsigned(1) != 0;
                    found = true;
                }
                else
                {
                    ParseValue(reader, 1);
                }
            }
            return found;
        }

        private static void SetBit(
            byte[] data, long bitOffset, bool value)
        {
            if (bitOffset < 0 ||
                bitOffset >= (long)data.Length * 8L)
            {
                throw new ArgumentOutOfRangeException("bitOffset");
            }
            int byteIndex = checked((int)(bitOffset / 8L));
            int mask = 1 << (7 - checked((int)(bitOffset % 8L)));
            if (value)
                data[byteIndex] = (byte)(data[byteIndex] | mask);
            else
                data[byteIndex] = (byte)(data[byteIndex] & ~mask);
        }

        internal static byte[] CompressLz4LiteralBlock(
            byte[] source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            List<byte> output =
                new List<byte>(source.Length + 8);
            int literalLength = source.Length;
            output.Add((byte)(Math.Min(literalLength, 15) << 4));
            if (literalLength >= 15)
            {
                int remaining = literalLength - 15;
                while (remaining >= 255)
                {
                    output.Add(255);
                    remaining -= 255;
                }
                output.Add((byte)remaining);
            }
            output.AddRange(source);
            return output.ToArray();
        }

        private static byte[] ReplaceCompressedPayload(
            byte[] blob, byte[] compressed)
        {
            int keyLength = ReadBigUInt16(blob, 16);
            int headerPosition = checked(18 + keyLength);
            uint oldLength = ReadBigUInt32(blob, headerPosition + 3);
            int oldPayloadStart = checked(headerPosition + 7);
            int oldPayloadEnd =
                checked(oldPayloadStart + checked((int)oldLength));
            if (oldPayloadEnd > blob.Length)
                throw new InvalidDataException(
                    "The ScriptData compressed payload is truncated.");

            int suffixLength = blob.Length - oldPayloadEnd;
            byte[] result = new byte[
                checked(oldPayloadStart + compressed.Length + suffixLength)];
            Buffer.BlockCopy(
                blob, 0, result, 0, oldPayloadStart);
            WriteBigUInt32(
                result, headerPosition + 3,
                checked((uint)compressed.Length));
            Buffer.BlockCopy(
                compressed, 0, result,
                oldPayloadStart, compressed.Length);
            if (suffixLength > 0)
            {
                Buffer.BlockCopy(
                    blob, oldPayloadEnd, result,
                    oldPayloadStart + compressed.Length,
                    suffixLength);
            }
            return result;
        }

        private static object ParseValue(BitReader reader, int depth)
        {
            if (depth > 128)
                throw new InvalidDataException("The serialized Lua object is nested too deeply.");

            int type = (int)reader.ReadUnsigned(8);
            switch (type)
            {
                case 1:
                    return null;
                case 2:
                    return reader.ReadUnsigned(1) != 0;
                case 3:
                    return ReadFloat(reader);
                case 4:
                {
                    uint length = (uint)reader.ReadUnsigned(32);
                    if (length > 64 * 1024 * 1024)
                        throw new InvalidDataException("A serialized string is unreasonably large.");
                    reader.Align();
                    return Encoding.UTF8.GetString(reader.ReadBytes((int)length));
                }
                case 5:
                {
                    uint count = (uint)reader.ReadUnsigned(32);
                    if (count > 10000000)
                        throw new InvalidDataException("A serialized table is unreasonably large.");

                    LuaTable table = new LuaTable();
                    table.IsArray = reader.ReadUnsigned(1) != 0;
                    if (table.IsArray)
                    {
                        table.ArrayOffset = (long)reader.ReadUnsigned(32);
                        for (uint i = 0; i < count; i++)
                        {
                            table.Entries.Add(new LuaEntry
                            {
                                Key = table.ArrayOffset + i,
                                Value = ParseValue(reader, depth + 1)
                            });
                        }
                    }
                    else
                    {
                        for (uint i = 0; i < count; i++)
                        {
                            table.Entries.Add(new LuaEntry
                            {
                                Key = ParseValue(reader, depth + 1),
                                Value = ParseValue(reader, depth + 1)
                            });
                        }
                    }
                    return table;
                }
                case 6:
                    return reader.ReadSigned(32);
                case 7:
                    return reader.ReadSigned(16);
                case 8:
                    return reader.ReadSigned(8);
                case 100:
                    return ParseUserData(reader);
                default:
                    throw new InvalidDataException(
                        "Unsupported serialized Lua value type " + type.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static LuaUserData ParseUserData(BitReader reader)
        {
            int typeId = checked((int)reader.ReadUnsigned(32));
            if (typeId == 10001)
            {
                byte[] bytes = reader.ReadBytes(16);
                Array.Reverse(bytes);
                string hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                string uuid =
                    hex.Substring(0, 8) + "-" +
                    hex.Substring(8, 4) + "-" +
                    hex.Substring(12, 4) + "-" +
                    hex.Substring(16, 4) + "-" +
                    hex.Substring(20, 12);
                return new LuaUserData { Type = "Uuid", Uuid = uuid };
            }
            if (typeId == 10003)
            {
                return new LuaUserData
                {
                    Type = "Vec3",
                    X = ReadFloat(reader),
                    Y = ReadFloat(reader),
                    Z = ReadFloat(reader)
                };
            }
            if (typeId == 10004)
            {
                return new LuaUserData
                {
                    Type = "Quat",
                    X = ReadFloat(reader),
                    Y = ReadFloat(reader),
                    Z = ReadFloat(reader),
                    W = ReadFloat(reader)
                };
            }
            if (typeId == 10005)
            {
                return new LuaUserData
                {
                    Type = "Color",
                    X = ReadFloat(reader),
                    Y = ReadFloat(reader),
                    Z = ReadFloat(reader),
                    W = ReadFloat(reader)
                };
            }

            string referenceType = ReferenceTypeName(typeId);
            if (referenceType != null)
                return new LuaUserData { Type = referenceType, Id = (long)reader.ReadUnsigned(32) };

            throw new InvalidDataException(
                "Unsupported serialized userdata type " + typeId.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static string ReferenceTypeName(int typeId)
        {
            switch (typeId)
            {
                case 10021: return "Shape";
                case 10022: return "Body";
                case 10023: return "Interactable";
                case 10024: return "Container";
                case 10025: return "Harvestable";
                case 10027: return "World";
                case 10028: return "Unit";
                case 10030: return "Player";
                case 10031: return "Character";
                case 10032: return "Joint";
                case 10036: return "Portal";
                case 10037: return "PathNode";
                case 10038: return "Lift";
                case 10039: return "ScriptableObject";
                case 20002: return "Tool";
                default: return null;
            }
        }

        private static float ReadFloat(BitReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        internal static byte[] DecompressLz4Block(byte[] source)
        {
            List<byte> output = new List<byte>(Math.Max(source.Length * 2, 256));
            int index = 0;
            while (index < source.Length)
            {
                int token = source[index++];
                int literalLength = token >> 4;
                if (literalLength == 15)
                {
                    int extension;
                    do
                    {
                        if (index >= source.Length)
                            throw new InvalidDataException("Invalid LZ4 literal length.");
                        extension = source[index++];
                        literalLength += extension;
                    } while (extension == 255);
                }

                if (literalLength < 0 || index + literalLength > source.Length)
                    throw new InvalidDataException("Invalid LZ4 literal data.");
                for (int i = 0; i < literalLength; i++)
                    output.Add(source[index++]);

                if (index >= source.Length)
                    break;
                if (index + 2 > source.Length)
                    throw new InvalidDataException("Invalid LZ4 match offset.");

                int offset = source[index] | (source[index + 1] << 8);
                index += 2;
                if (offset == 0 || offset > output.Count)
                    throw new InvalidDataException("Invalid LZ4 back-reference.");

                int matchLength = token & 15;
                if (matchLength == 15)
                {
                    int extension;
                    do
                    {
                        if (index >= source.Length)
                            throw new InvalidDataException("Invalid LZ4 match length.");
                        extension = source[index++];
                        matchLength += extension;
                    } while (extension == 255);
                }
                matchLength += 4;

                for (int i = 0; i < matchLength; i++)
                    output.Add(output[output.Count - offset]);

                if (output.Count > 128 * 1024 * 1024)
                    throw new InvalidDataException("The decompressed raid data is unreasonably large.");
            }
            return output.ToArray();
        }

        private static int ReadBigUInt16(byte[] value, int offset)
        {
            return (value[offset] << 8) | value[offset + 1];
        }

        private static uint ReadBigUInt32(byte[] value, int offset)
        {
            return ((uint)value[offset] << 24) |
                   ((uint)value[offset + 1] << 16) |
                   ((uint)value[offset + 2] << 8) |
                   value[offset + 3];
        }

        private static void WriteBigUInt32(
            byte[] value, int offset, uint number)
        {
            value[offset] = (byte)(number >> 24);
            value[offset + 1] = (byte)(number >> 16);
            value[offset + 2] = (byte)(number >> 8);
            value[offset + 3] = (byte)number;
        }

        private static bool BytesEqual(
            byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null ||
                left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static byte[] Slice(byte[] value, int offset, int count)
        {
            byte[] result = new byte[count];
            Buffer.BlockCopy(value, offset, result, 0, count);
            return result;
        }
    }
}
