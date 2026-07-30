using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal sealed class WorldDescriptor
    {
        public int WorldId;
        public string ScriptPath;
        public string ClassName;
        public string Parameters;
        public string DisplayName;
    }

    internal static class WorldStorage
    {
        private const int MaximumFieldLength = 1024 * 1024;

        public static Dictionary<int, string> ReadWorldNames(
            SqliteDatabase database)
        {
            Dictionary<int, string> names =
                new Dictionary<int, string>();
            try
            {
                foreach (WorldMetadataRecord record in
                    database.ReadWorldMetadata())
                {
                    WorldDescriptor descriptor;
                    if (!TryParse(record, out descriptor) ||
                        String.IsNullOrWhiteSpace(descriptor.DisplayName))
                        continue;

                    string current;
                    if (!names.TryGetValue(record.WorldId, out current))
                    {
                        names[record.WorldId] = descriptor.DisplayName;
                    }
                }
            }
            catch (SqliteException)
            {
                // Older saves do not contain Chapter 2 world descriptors.
            }

            // Standard Survival creates the Overworld first. This safe
            // fallback keeps older saves readable when GenericData is absent.
            if (!names.ContainsKey(1))
                names[1] = "Overworld";
            return names;
        }

        public static string ResolveName(
            IDictionary<int, string> names, int worldId)
        {
            string name;
            if (names != null &&
                names.TryGetValue(worldId, out name) &&
                !String.IsNullOrWhiteSpace(name))
                return name;
            return "World " +
                worldId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParse(
            WorldMetadataRecord record, out WorldDescriptor descriptor)
        {
            descriptor = null;
            try
            {
                byte[] blob = record.Data;
                if (blob == null || blob.Length < 29)
                    return false;

                int keyLength = ReadBigUInt16(blob, 16);
                int position = checked(18 + keyLength);
                if (position + 7 > blob.Length)
                    return false;

                int storedWorldId = ReadBigUInt16(blob, position);
                uint compressedLength = ReadBigUInt32(blob, position + 3);
                if (storedWorldId != record.WorldId ||
                    compressedLength > Int32.MaxValue ||
                    position + 7L + compressedLength > blob.Length)
                    return false;

                byte[] compressed = Slice(
                    blob, position + 7, (int)compressedLength);
                byte[] data = LuaStorage.DecompressLz4Block(compressed);
                if (data.Length < 10)
                    return false;

                int cursor = 4;
                string scriptPath = ReadString(data, ref cursor);
                string className = ReadString(data, ref cursor);
                string parameters = ReadString(data, ref cursor);
                if (String.IsNullOrWhiteSpace(className))
                    return false;

                descriptor = new WorldDescriptor
                {
                    WorldId = record.WorldId,
                    ScriptPath = scriptPath,
                    ClassName = className,
                    Parameters = parameters,
                    DisplayName = BuildDisplayName(
                        className, scriptPath, parameters)
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildDisplayName(
            string className, string scriptPath, string parameters)
        {
            Dictionary<string, object> settings =
                ParseSettings(parameters);
            string explicitName = GetString(
                settings, "worldName", "displayName", "name");
            if (!String.IsNullOrWhiteSpace(explicitName))
                return Humanize(explicitName);

            if (String.Equals(
                className, "Overworld", StringComparison.OrdinalIgnoreCase))
                return "Overworld";

            if (String.Equals(
                className, "WarehouseWorld",
                StringComparison.OrdinalIgnoreCase))
            {
                int warehouse = GetInteger(settings, "warehouseIndex");
                int floor = GetInteger(settings, "level");
                bool quest = GetBoolean(settings, "isQuestWarehouse");
                StringBuilder result = new StringBuilder();
                if (quest)
                    result.Append("Quest ");
                result.Append("Warehouse");
                if (warehouse > 0)
                    result.Append(" ").Append(warehouse);
                if (floor > 0)
                    result.Append(" - Floor ").Append(floor);
                return result.ToString();
            }

            string worldPath = GetString(settings, "path");
            if (!String.IsNullOrWhiteSpace(worldPath))
            {
                string normalized = worldPath.Replace('\\', '/');
                int slash = normalized.LastIndexOf('/');
                string fileName = slash >= 0
                    ? normalized.Substring(slash + 1)
                    : normalized;
                int dot = fileName.LastIndexOf('.');
                if (dot > 0)
                    fileName = fileName.Substring(0, dot);
                if (!String.IsNullOrWhiteSpace(fileName))
                    return Humanize(fileName);
            }

            string fallback = className;
            if (fallback.EndsWith(
                "World", StringComparison.OrdinalIgnoreCase) &&
                fallback.Length > 5)
                fallback = fallback.Substring(0, fallback.Length - 5);
            if (String.IsNullOrWhiteSpace(fallback))
            {
                fallback = Path.GetFileNameWithoutExtension(
                    scriptPath ?? String.Empty);
            }
            return Humanize(fallback);
        }

        private static Dictionary<string, object> ParseSettings(string json)
        {
            if (String.IsNullOrWhiteSpace(json) ||
                String.Equals(
                    json.Trim(), "null",
                    StringComparison.OrdinalIgnoreCase))
                return new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);
            try
            {
                Dictionary<string, object> parsed =
                    new JavaScriptSerializer()
                        .Deserialize<Dictionary<string, object>>(json);
                return parsed ??
                    new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string GetString(
            IDictionary<string, object> values, params string[] keys)
        {
            foreach (string key in keys)
            {
                object value;
                if (TryGet(values, key, out value) && value != null)
                    return Convert.ToString(
                        value, CultureInfo.InvariantCulture);
            }
            return String.Empty;
        }

        private static int GetInteger(
            IDictionary<string, object> values, string key)
        {
            object value;
            if (!TryGet(values, key, out value) || value == null)
                return 0;
            try
            {
                return checked((int)Math.Round(
                    Convert.ToDouble(
                        value, CultureInfo.InvariantCulture)));
            }
            catch
            {
                return 0;
            }
        }

        private static bool GetBoolean(
            IDictionary<string, object> values, string key)
        {
            object value;
            if (!TryGet(values, key, out value) || value == null)
                return false;
            try
            {
                return Convert.ToBoolean(
                    value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGet(
            IDictionary<string, object> values,
            string key, out object value)
        {
            if (values.TryGetValue(key, out value))
                return true;
            foreach (KeyValuePair<string, object> pair in values)
            {
                if (String.Equals(
                    pair.Key, key,
                    StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        private static string Humanize(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "Unknown World";
            string text = value.Trim()
                .Replace('_', ' ')
                .Replace('-', ' ');
            text = Regex.Replace(
                text, "([a-z0-9])([A-Z])", "$1 $2");
            text = Regex.Replace(
                text, "([A-Za-z])([0-9])", "$1 $2");
            text = Regex.Replace(
                text, "([0-9])([A-Za-z])", "$1 $2");
            text = Regex.Replace(text, "\\s+", " ").Trim();
            text = Regex.Replace(
                text, "\\bgrowlab\\b", "grow lab",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(
                text, "\\bminidungeon\\b", "mini dungeon",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(
                text, "\\b0+([0-9]+)\\b", "$1");

            TextInfo title = CultureInfo.InvariantCulture.TextInfo;
            return title.ToTitleCase(text.ToLowerInvariant());
        }

        private static string ReadString(
            byte[] data, ref int position)
        {
            if (position + 2 > data.Length)
                throw new InvalidDataException(
                    "World metadata string length is truncated.");
            int length = ReadBigUInt16(data, position);
            position += 2;
            if (length < 0 || length > MaximumFieldLength ||
                position + length > data.Length)
            {
                throw new InvalidDataException(
                    "World metadata string is truncated.");
            }
            string value = Encoding.UTF8.GetString(
                data, position, length);
            position += length;
            return value;
        }

        private static int ReadBigUInt16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static uint ReadBigUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        private static byte[] Slice(
            byte[] source, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }
    }
}
