using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RaidRescue
{
    internal static class InspectWirelessPipeSave
    {
        private static string Scalar(object value)
        {
            if (value == null) return "nil";
            if (value is string) return "\"" + value + "\"";
            if (value is bool) return (bool)value ? "true" : "false";
            LuaUserData data = value as LuaUserData;
            if (data != null)
            {
                if (!String.IsNullOrEmpty(data.Uuid)) return data.Type + "(" + data.Uuid + ")";
                return data.Type + "(" + data.Id.ToString(CultureInfo.InvariantCulture) + ")";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void DumpTable(LuaTable table, string indent, int depth)
        {
            if (depth > 8) { Console.WriteLine(indent + "<depth limit>"); return; }
            foreach (LuaEntry entry in table.Entries)
            {
                LuaTable nested = entry.Value as LuaTable;
                Console.WriteLine(indent + Scalar(entry.Key) + " = " +
                    (nested == null ? Scalar(entry.Value) : "table[" + nested.Count + "]"));
                if (nested != null) DumpTable(nested, indent + "  ", depth + 1);
            }
        }

        private static string Hex(byte[] value)
        {
            return value == null ? "" : BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
        }

        private static LuaTable FindNamedTable(LuaTable table, string name, int depth)
        {
            if (table == null || depth > 12) return null;
            LuaTable direct = table.Get(name) as LuaTable;
            if (direct != null) return direct;
            foreach (LuaEntry entry in table.Entries)
            {
                LuaTable found = FindNamedTable(entry.Value as LuaTable, name, depth + 1);
                if (found != null) return found;
            }
            return null;
        }

        public static int Main(string[] args)
        {
            if (args.Length != 1) { Console.Error.WriteLine("Usage: Inspect-WirelessPipeSave.exe <save.db>"); return 2; }
            int parsed = 0, candidates = 0, phase4Candidates = 0;
            using (SqliteDatabase database = SqliteDatabase.OpenReadOnly(args[0]))
            using (SqliteDatabase.SqliteStatement statement = database.Prepare(
                "SELECT rowid, uid, key, worldId, flags, data FROM ScriptData ORDER BY rowid"))
            {
                while (statement.Read())
                {
                    long rowId = statement.GetInt64(0);
                    byte[] uid = statement.GetBlob(1);
                    byte[] key = statement.GetBlob(2);
                    int worldId = checked((int)statement.GetInt64(3));
                    byte[] blob = statement.GetBlob(5);
                    try
                    {
                        ScriptPayload payload = LuaStorage.ParseScriptData(blob);
                        parsed++;
                        LuaTable root = payload.Value as LuaTable;
                        if (root == null) continue;
                        if (root.Get("schemaVersion") != null && root.Get("endpoints") != null)
                        {
                            candidates++;
                            Console.WriteLine("WIRELESS MANAGER CANDIDATE rowid=" + rowId +
                                " tableWorld=" + worldId + " payloadWorld=" + payload.WorldId +
                                " uid=" + Hex(uid) + " key=" + Hex(key));
                            DumpTable(root, "  ", 0);
                        }
                        LuaTable phase4 = FindNamedTable(root, "scrapLabPipePhase4", 0);
                        if (phase4 != null)
                        {
                            phase4Candidates++;
                            Console.WriteLine("PHASE 4 HARNESS STATE rowid=" + rowId +
                                " cleanup=" + (phase4.Get("cleanup") == null ? "ABSENT" : "PRESENT"));
                            DumpTable(phase4, "  ", 0);
                        }
                    }
                    catch { }
                }
            }
            Console.WriteLine("SUMMARY parsed=" + parsed + " managerCandidates=" + candidates +
                " phase4Candidates=" + phase4Candidates);
            return candidates > 0 ? 0 : 1;
        }
    }
}
