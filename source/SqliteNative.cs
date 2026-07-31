using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RaidRescue
{
    internal sealed class HarvestableRecord
    {
        public long Id;
        public int WorldId;
        public int CellX;
        public int CellY;
        public int Size;
        public byte[] Data;
    }

    internal sealed class StoredScriptRecord
    {
        public long RowId;
        public byte[] Uid;
        public byte[] Key;
        public int WorldId;
        public int Flags;
        public byte[] Data;
    }

    internal sealed class WorldMetadataRecord
    {
        public long RowId;
        public int WorldId;
        public byte[] Data;
    }

    internal sealed class SqliteStorageStatistics
    {
        public long PageSizeBytes;
        public long PageCount;
        public long FreePageCount;
    }

    internal sealed class SupportedSchema
    {
        public bool CanReadGame;
        public bool CanReadHarvestableCells;
        public bool CanReadUnitCells;
        public bool UnitTablePresent;
        public bool CanReadWorldMetadata;
        public bool CanReadScriptTotals;
        public string GenericDataLayout;
        public string ScriptDataLayout;
    }

    internal sealed class UnsupportedTableSummary
    {
        public int Count;
        public List<string> Names;
    }

    internal sealed class WorldPayloadTotal
    {
        public int WorldId;
        public long RecordCount;
        public long PayloadBytes;
    }

    internal enum SupportedTableKind
    {
        GenericData,
        ScriptData
    }

    internal sealed class SqliteException : Exception
    {
        public int ResultCode { get; private set; }

        public SqliteException(int resultCode, string message)
            : base(message)
        {
            ResultCode = resultCode;
        }
    }

    internal sealed class SqliteDatabase : IDisposable
    {
        private const int SqliteOk = 0;
        private const int SqliteRow = 100;
        private const int SqliteDone = 101;
        private const int FlagOpenReadOnly = 0x00000001;
        private const int FlagOpenReadWrite = 0x00000002;
        private const int FlagOpenCreate = 0x00000004;
        private const int FlagOpenFullMutex = 0x00010000;

        private IntPtr handle;

        private SqliteDatabase(IntPtr database)
        {
            handle = database;
            Native.sqlite3_busy_timeout(handle, 5000);
        }

        public static SqliteDatabase OpenReadOnly(string path)
        {
            return Open(path, FlagOpenReadOnly | FlagOpenFullMutex);
        }

        public static SqliteDatabase OpenReadWrite(string path, bool create)
        {
            int flags = FlagOpenReadWrite | FlagOpenFullMutex;
            if (create)
                flags |= FlagOpenCreate;
            return Open(path, flags);
        }

        private static SqliteDatabase Open(string path, int flags)
        {
            IntPtr database;
            int result = Native.sqlite3_open_v2(Utf8(path), out database, flags, IntPtr.Zero);
            if (result != SqliteOk)
            {
                string message = database == IntPtr.Zero
                    ? "SQLite could not open the file."
                    : PtrToStringUtf8(Native.sqlite3_errmsg(database));
                if (database != IntPtr.Zero)
                    Native.sqlite3_close_v2(database);
                throw new SqliteException(result, message);
            }
            return new SqliteDatabase(database);
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Native.sqlite3_close_v2(handle);
                handle = IntPtr.Zero;
            }
        }

        public string QuickCheck()
        {
            using (SqliteStatement statement = Prepare("PRAGMA quick_check"))
            {
                if (!statement.Read())
                    throw new InvalidDataException("SQLite did not return an integrity-check result.");
                return statement.GetString(0);
            }
        }

        public void ReadGameInfo(out long saveVersion, out long gameTick)
        {
            using (SqliteStatement statement = Prepare(
                "SELECT savegameversion, gametick FROM Game LIMIT 1"))
            {
                if (!statement.Read())
                    throw new InvalidDataException("The Game table is empty.");
                saveVersion = statement.GetInt64(0);
                gameTick = statement.GetInt64(1);
            }
        }

        public SqliteStorageStatistics ReadStorageStatistics()
        {
            return new SqliteStorageStatistics
            {
                PageSizeBytes = ReadPragmaInt64("PRAGMA page_size"),
                PageCount = ReadPragmaInt64("PRAGMA page_count"),
                FreePageCount = ReadPragmaInt64(
                    "PRAGMA freelist_count")
            };
        }

        public SupportedSchema ReadSupportedSchema()
        {
            List<SchemaColumn> game =
                ReadAllowlistedTableColumns("Game");
            List<SchemaColumn> harvestable =
                ReadAllowlistedTableColumns("Harvestable");
            List<SchemaColumn> unit =
                ReadAllowlistedTableColumns("Unit");
            List<SchemaColumn> generic =
                ReadAllowlistedTableColumns("GenericData");
            List<SchemaColumn> script =
                ReadAllowlistedTableColumns("ScriptData");

            string genericLayout = RecognizeStorageLayout(generic);
            string scriptLayout = RecognizeStorageLayout(script);
            return new SupportedSchema
            {
                CanReadGame = HasColumns(
                    game,
                    Column("savegameversion", "INTEGER"),
                    Column("gametick", "INTEGER")),
                CanReadHarvestableCells = HasColumns(
                    harvestable,
                    Column("id", "INTEGER"),
                    Column("worldId", "INTEGER"),
                    Column("x", "INTEGER"),
                    Column("y", "INTEGER"),
                    Column("size", "INTEGER"),
                    Column("data", "BLOB")),
                CanReadUnitCells = HasColumns(
                    unit,
                    Column("id", "INTEGER"),
                    Column("worldId", "INTEGER"),
                    Column("x", "INTEGER"),
                    Column("y", "INTEGER"),
                    Column("data", "BLOB")),
                UnitTablePresent = unit.Count > 0,
                CanReadWorldMetadata =
                    !String.IsNullOrEmpty(genericLayout),
                CanReadScriptTotals =
                    !String.IsNullOrEmpty(scriptLayout),
                GenericDataLayout = genericLayout,
                ScriptDataLayout = scriptLayout
            };
        }

        public UnsupportedTableSummary ReadUnsupportedTables()
        {
            HashSet<string> supported = new HashSet<string>(
                StringComparer.Ordinal)
            {
                "Game",
                "Harvestable",
                "Unit",
                "GenericData",
                "ScriptData"
            };
            UnsupportedTableSummary result =
                new UnsupportedTableSummary
                {
                    Names = new List<string>()
                };
            using (SqliteStatement statement = Prepare(
                "SELECT name FROM sqlite_master " +
                "WHERE type='table' AND name NOT LIKE 'sqlite_%' " +
                "ORDER BY name COLLATE BINARY"))
            {
                while (statement.Read())
                {
                    string name = statement.GetString(0);
                    if (supported.Contains(name))
                        continue;
                    result.Count++;
                    if (result.Names.Count < 32)
                        result.Names.Add(name);
                }
            }
            return result;
        }

        public List<WorldPayloadTotal> CountSupportedRows(
            SupportedTableKind table, CancellationToken cancellation)
        {
            string sql;
            switch (table)
            {
                case SupportedTableKind.GenericData:
                    sql =
                        "SELECT worldId, COUNT(*), " +
                        "COALESCE(SUM(length(data)), 0) " +
                        "FROM GenericData GROUP BY worldId ORDER BY worldId";
                    break;
                case SupportedTableKind.ScriptData:
                    sql =
                        "SELECT worldId, COUNT(*), " +
                        "COALESCE(SUM(length(data)), 0) " +
                        "FROM ScriptData GROUP BY worldId ORDER BY worldId";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("table");
            }

            List<WorldPayloadTotal> totals =
                new List<WorldPayloadTotal>();
            using (SqliteStatement statement = Prepare(sql))
            {
                while (statement.Read())
                {
                    cancellation.ThrowIfCancellationRequested();
                    totals.Add(new WorldPayloadTotal
                    {
                        WorldId = checked(
                            (int)statement.GetInt64(0)),
                        RecordCount = statement.GetInt64(1),
                        PayloadBytes = statement.GetInt64(2)
                    });
                }
            }
            return totals;
        }

        public long CountHarvestableRows(
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (SqliteStatement statement = Prepare(
                "SELECT COUNT(*) FROM Harvestable"))
            {
                if (!statement.Read())
                    throw new InvalidDataException(
                        "SQLite did not return a Harvestable count.");
                long count = statement.GetInt64(0);
                cancellation.ThrowIfCancellationRequested();
                return count;
            }
        }

        public long CountUnitRows(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (SqliteStatement statement = Prepare(
                "SELECT COUNT(*) FROM Unit"))
            {
                if (!statement.Read())
                    throw new InvalidDataException(
                        "SQLite did not return a Unit count.");
                long count = statement.GetInt64(0);
                cancellation.ThrowIfCancellationRequested();
                return count;
            }
        }

        public void StreamHarvestableCells(
            Action<int, int, int, long> onRecord,
            CancellationToken cancellation)
        {
            if (onRecord == null)
                throw new ArgumentNullException("onRecord");
            using (SqliteStatement statement = Prepare(
                "SELECT worldId, x, y, COALESCE(length(data), 0) " +
                "FROM Harvestable ORDER BY id"))
            {
                while (statement.Read())
                {
                    cancellation.ThrowIfCancellationRequested();
                    onRecord(
                        checked((int)statement.GetInt64(0)),
                        checked((int)statement.GetInt64(1)),
                        checked((int)statement.GetInt64(2)),
                        statement.GetInt64(3));
                }
            }
        }

        public void StreamUnitCells(
            Action<int, int, int, long, byte[]> onRecord,
            CancellationToken cancellation)
        {
            if (onRecord == null)
                throw new ArgumentNullException("onRecord");
            using (SqliteStatement statement = Prepare(
                "SELECT worldId, x, y, " +
                "COALESCE(length(data), 0), data " +
                "FROM Unit ORDER BY id"))
            {
                while (statement.Read())
                {
                    cancellation.ThrowIfCancellationRequested();
                    onRecord(
                        checked((int)statement.GetInt64(0)),
                        checked((int)statement.GetInt64(1)),
                        checked((int)statement.GetInt64(2)),
                        statement.GetInt64(3),
                        statement.GetBlob(4));
                }
            }
        }

        public byte[] ReadRaidRecord(out long rowId)
        {
            const string sql =
                "SELECT rowid, data FROM ScriptData " +
                "WHERE uid=x'2C3699B2FD9C503EA405CF73434E2E88' " +
                "AND key=x'4C554100000001082D' LIMIT 1";
            using (SqliteStatement statement = Prepare(sql))
            {
                if (!statement.Read())
                {
                    rowId = 0;
                    return null;
                }
                rowId = statement.GetInt64(0);
                return statement.GetBlob(1);
            }
        }

        public bool HasColumn(string table, string column)
        {
            if (!String.Equals(table, "ScriptData", StringComparison.Ordinal))
                throw new ArgumentException("Unsupported schema lookup.", "table");
            using (SqliteStatement statement = Prepare("PRAGMA table_info(ScriptData)"))
            {
                while (statement.Read())
                {
                    if (String.Equals(statement.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        public bool RowExists(string table, long id)
        {
            if (!String.Equals(table, "Harvestable", StringComparison.Ordinal))
                throw new ArgumentException("Unsupported table lookup.", "table");
            using (SqliteStatement statement = Prepare(
                "SELECT 1 FROM Harvestable WHERE id=?1 LIMIT 1"))
            {
                statement.BindInt64(1, id);
                return statement.Read();
            }
        }

        public List<HarvestableRecord> ReadHarvestables()
        {
            List<HarvestableRecord> records =
                new List<HarvestableRecord>();
            using (SqliteStatement statement = Prepare(
                "SELECT id, worldId, x, y, size, data " +
                "FROM Harvestable ORDER BY id"))
            {
                while (statement.Read())
                {
                    records.Add(new HarvestableRecord
                    {
                        Id = statement.GetInt64(0),
                        WorldId = checked((int)statement.GetInt64(1)),
                        CellX = checked((int)statement.GetInt64(2)),
                        CellY = checked((int)statement.GetInt64(3)),
                        Size = checked((int)statement.GetInt64(4)),
                        Data = statement.GetBlob(5)
                    });
                }
            }
            return records;
        }

        public HarvestableRecord ReadHarvestable(long id)
        {
            using (SqliteStatement statement = Prepare(
                "SELECT id, worldId, x, y, size, data " +
                "FROM Harvestable WHERE id=?1 LIMIT 1"))
            {
                statement.BindInt64(1, id);
                if (!statement.Read())
                    return null;
                return new HarvestableRecord
                {
                    Id = statement.GetInt64(0),
                    WorldId = checked((int)statement.GetInt64(1)),
                    CellX = checked((int)statement.GetInt64(2)),
                    CellY = checked((int)statement.GetInt64(3)),
                    Size = checked((int)statement.GetInt64(4)),
                    Data = statement.GetBlob(5)
                };
            }
        }

        public List<StoredScriptRecord> ReadScriptRecords(
            byte[] key, int worldId)
        {
            List<StoredScriptRecord> records =
                new List<StoredScriptRecord>();
            using (SqliteStatement statement = Prepare(
                "SELECT rowid, uid, key, worldId, flags, data " +
                "FROM ScriptData WHERE key=?1 AND worldId=?2 " +
                "ORDER BY rowid"))
            {
                statement.BindBlob(1, key);
                statement.BindInt64(2, worldId);
                while (statement.Read())
                {
                    records.Add(new StoredScriptRecord
                    {
                        RowId = statement.GetInt64(0),
                        Uid = statement.GetBlob(1),
                        Key = statement.GetBlob(2),
                        WorldId = checked((int)statement.GetInt64(3)),
                        Flags = checked((int)statement.GetInt64(4)),
                        Data = statement.GetBlob(5)
                    });
                }
            }
            return records;
        }

        public StoredScriptRecord ReadScriptRecord(long rowId)
        {
            using (SqliteStatement statement = Prepare(
                "SELECT rowid, uid, key, worldId, flags, data " +
                "FROM ScriptData WHERE rowid=?1 LIMIT 1"))
            {
                statement.BindInt64(1, rowId);
                if (!statement.Read())
                    return null;
                return new StoredScriptRecord
                {
                    RowId = statement.GetInt64(0),
                    Uid = statement.GetBlob(1),
                    Key = statement.GetBlob(2),
                    WorldId = checked((int)statement.GetInt64(3)),
                    Flags = checked((int)statement.GetInt64(4)),
                    Data = statement.GetBlob(5)
                };
            }
        }

        public List<WorldMetadataRecord> ReadWorldMetadata()
        {
            List<WorldMetadataRecord> records =
                new List<WorldMetadataRecord>();
            SupportedSchema schema = ReadSupportedSchema();
            string sql;
            if (String.Equals(
                schema.GenericDataLayout,
                "legacy-v26", StringComparison.Ordinal))
            {
                sql =
                    "SELECT rowid, worldId, data FROM GenericData " +
                    "WHERE uid=x'5297769DF4514E5E9A388B0F95E2EDAD' " +
                    "ORDER BY worldId, rowid";
            }
            else if (String.Equals(
                schema.GenericDataLayout,
                "current-v28", StringComparison.Ordinal))
            {
                sql =
                    "SELECT rowid, worldId, data FROM GenericData " +
                    "ORDER BY worldId, rowid";
            }
            else
            {
                return records;
            }
            using (SqliteStatement statement = Prepare(sql))
            {
                while (statement.Read())
                {
                    records.Add(new WorldMetadataRecord
                    {
                        RowId = statement.GetInt64(0),
                        WorldId = checked((int)statement.GetInt64(1)),
                        Data = statement.GetBlob(2)
                    });
                }
            }
            return records;
        }

        private long ReadPragmaInt64(string sql)
        {
            using (SqliteStatement statement = Prepare(sql))
            {
                if (!statement.Read())
                    throw new InvalidDataException(
                        "SQLite did not return a storage statistic.");
                return statement.GetInt64(0);
            }
        }

        private List<SchemaColumn> ReadAllowlistedTableColumns(
            string table)
        {
            string sql;
            switch (table)
            {
                case "Game":
                    sql = "PRAGMA table_info(Game)";
                    break;
                case "Harvestable":
                    sql = "PRAGMA table_info(Harvestable)";
                    break;
                case "Unit":
                    sql = "PRAGMA table_info(Unit)";
                    break;
                case "GenericData":
                    sql = "PRAGMA table_info(GenericData)";
                    break;
                case "ScriptData":
                    sql = "PRAGMA table_info(ScriptData)";
                    break;
                default:
                    throw new ArgumentException(
                        "Unsupported schema table.", "table");
            }

            List<SchemaColumn> columns = new List<SchemaColumn>();
            using (SqliteStatement statement = Prepare(sql))
            {
                while (statement.Read())
                {
                    columns.Add(new SchemaColumn
                    {
                        Name = statement.GetString(1),
                        Type = statement.GetString(2)
                    });
                }
            }
            return columns;
        }

        private static string RecognizeStorageLayout(
            IList<SchemaColumn> columns)
        {
            if (HasColumns(
                columns,
                Column("uid", "BLOB"),
                Column("key", "BLOB"),
                Column("worldId", "INTEGER"),
                Column("flags", "INTEGER"),
                Column("data", "BLOB")))
                return "legacy-v26";
            if (HasColumns(
                columns,
                Column("id", "INTEGER"),
                Column("channel", "INTEGER"),
                Column("worldId", "INTEGER"),
                Column("flags", "INTEGER"),
                Column("data", "BLOB")))
                return "current-v28";
            return String.Empty;
        }

        private static bool HasColumns(
            IList<SchemaColumn> actual,
            params SchemaColumn[] required)
        {
            foreach (SchemaColumn expected in required)
            {
                bool found = false;
                foreach (SchemaColumn column in actual)
                {
                    if (String.Equals(
                            column.Name, expected.Name,
                            StringComparison.Ordinal) &&
                        String.Equals(
                            column.Type, expected.Type,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static SchemaColumn Column(
            string name, string type)
        {
            return new SchemaColumn { Name = name, Type = type };
        }

        private sealed class SchemaColumn
        {
            public string Name;
            public string Type;
        }

        public int DeleteScriptDataRow(long rowId)
        {
            using (SqliteStatement statement = Prepare(
                "DELETE FROM ScriptData WHERE rowid=?1"))
            {
                statement.BindInt64(1, rowId);
                statement.ExecuteNonQuery();
            }
            return Native.sqlite3_changes(handle);
        }

        public int UpdateScriptData(
            long rowId, byte[] key, int worldId,
            byte[] expectedData, byte[] replacementData)
        {
            using (SqliteStatement statement = Prepare(
                "UPDATE ScriptData SET data=?1 " +
                "WHERE rowid=?2 AND key=?3 AND worldId=?4 AND data=?5"))
            {
                statement.BindBlob(1, replacementData);
                statement.BindInt64(2, rowId);
                statement.BindBlob(3, key);
                statement.BindInt64(4, worldId);
                statement.BindBlob(5, expectedData);
                statement.ExecuteNonQuery();
            }
            return Native.sqlite3_changes(handle);
        }

        public int DeleteHarvestable(long id)
        {
            using (SqliteStatement statement = Prepare(
                "DELETE FROM Harvestable WHERE id=?1"))
            {
                statement.BindInt64(1, id);
                statement.ExecuteNonQuery();
            }
            return Native.sqlite3_changes(handle);
        }

        public int DeleteRaidRecord()
        {
            Execute(
                "DELETE FROM ScriptData " +
                "WHERE uid=x'2C3699B2FD9C503EA405CF73434E2E88' " +
                "AND key=x'4C554100000001082D'");
            return Native.sqlite3_changes(handle);
        }

        public void Execute(string sql)
        {
            IntPtr error;
            int result = Native.sqlite3_exec(handle, Utf8(sql), IntPtr.Zero, IntPtr.Zero, out error);
            if (result != SqliteOk)
            {
                string message = error == IntPtr.Zero
                    ? ErrorMessage
                    : PtrToStringUtf8(error);
                if (error != IntPtr.Zero)
                    Native.sqlite3_free(error);
                throw new SqliteException(result, message);
            }
        }

        public SqliteStatement Prepare(string sql)
        {
            IntPtr statement;
            int result = Native.sqlite3_prepare_v2(handle, Utf8(sql), -1, out statement, IntPtr.Zero);
            if (result != SqliteOk)
                throw new SqliteException(result, ErrorMessage);
            return new SqliteStatement(this, statement);
        }

        public static void Backup(string sourcePath, string destinationPath)
        {
            using (SqliteDatabase source = OpenReadOnly(sourcePath))
            using (SqliteDatabase destination = OpenReadWrite(destinationPath, true))
            {
                IntPtr backup = Native.sqlite3_backup_init(
                    destination.handle, Utf8("main"), source.handle, Utf8("main"));
                if (backup == IntPtr.Zero)
                    throw new SqliteException(-1, destination.ErrorMessage);

                int result = -1;
                try
                {
                    int attempts = 0;
                    do
                    {
                        result = Native.sqlite3_backup_step(backup, -1);
                        if (result == 5 || result == 6)
                        {
                            attempts++;
                            if (attempts > 20)
                                break;
                            Thread.Sleep(50);
                        }
                    } while (result == 5 || result == 6);
                }
                finally
                {
                    int finishResult = Native.sqlite3_backup_finish(backup);
                    if (result == SqliteDone && finishResult != SqliteOk)
                        result = finishResult;
                }

                if (result != SqliteDone)
                    throw new SqliteException(result, destination.ErrorMessage);
            }
        }

        internal string ErrorMessage
        {
            get { return PtrToStringUtf8(Native.sqlite3_errmsg(handle)); }
        }

        internal static byte[] Utf8(string value)
        {
            byte[] content = Encoding.UTF8.GetBytes(value);
            byte[] terminated = new byte[content.Length + 1];
            Buffer.BlockCopy(content, 0, terminated, 0, content.Length);
            return terminated;
        }

        internal static string PtrToStringUtf8(IntPtr value)
        {
            if (value == IntPtr.Zero)
                return String.Empty;
            int count = 0;
            while (Marshal.ReadByte(value, count) != 0)
                count++;
            byte[] bytes = new byte[count];
            Marshal.Copy(value, bytes, 0, count);
            return Encoding.UTF8.GetString(bytes);
        }

        internal static class Native
        {
            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_open_v2(
                byte[] filename, out IntPtr database, int flags, IntPtr vfs);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_close_v2(IntPtr database);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr sqlite3_errmsg(IntPtr database);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_prepare_v2(
                IntPtr database, byte[] sql, int byteCount, out IntPtr statement, IntPtr tail);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_step(IntPtr statement);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_finalize(IntPtr statement);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern long sqlite3_column_int64(IntPtr statement, int column);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr sqlite3_column_blob(IntPtr statement, int column);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_column_bytes(IntPtr statement, int column);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_bind_blob(
                IntPtr statement, int index, byte[] value, int byteCount, IntPtr destructor);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_exec(
                IntPtr database, byte[] sql, IntPtr callback, IntPtr state, out IntPtr error);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void sqlite3_free(IntPtr value);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_changes(IntPtr database);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr sqlite3_backup_init(
                IntPtr destination, byte[] destinationName, IntPtr source, byte[] sourceName);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_backup_step(IntPtr backup, int pages);

            [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern int sqlite3_backup_finish(IntPtr backup);
        }

        internal sealed class SqliteStatement : IDisposable
        {
            private readonly SqliteDatabase owner;
            private IntPtr statement;

            internal SqliteStatement(SqliteDatabase database, IntPtr value)
            {
                owner = database;
                statement = value;
            }

            public void Dispose()
            {
                if (statement != IntPtr.Zero)
                {
                    Native.sqlite3_finalize(statement);
                    statement = IntPtr.Zero;
                }
            }

            public bool Read()
            {
                int result = Native.sqlite3_step(statement);
                if (result == SqliteRow)
                    return true;
                if (result == SqliteDone)
                    return false;
                throw new SqliteException(result, owner.ErrorMessage);
            }

            public void ExecuteNonQuery()
            {
                int result = Native.sqlite3_step(statement);
                if (result != SqliteDone)
                    throw new SqliteException(result, owner.ErrorMessage);
            }

            public long GetInt64(int column)
            {
                return Native.sqlite3_column_int64(statement, column);
            }

            public string GetString(int column)
            {
                return PtrToStringUtf8(Native.sqlite3_column_text(statement, column));
            }

            public byte[] GetBlob(int column)
            {
                int count = Native.sqlite3_column_bytes(statement, column);
                if (count == 0)
                    return new byte[0];
                IntPtr value = Native.sqlite3_column_blob(statement, column);
                byte[] result = new byte[count];
                Marshal.Copy(value, result, 0, count);
                return result;
            }

            public void BindInt64(int index, long value)
            {
                int result = Native.sqlite3_bind_int64(statement, index, value);
                if (result != SqliteOk)
                    throw new SqliteException(result, owner.ErrorMessage);
            }

            public void BindBlob(int index, byte[] value)
            {
                byte[] content = value ?? new byte[0];
                int result = Native.sqlite3_bind_blob(
                    statement, index, content, content.Length,
                    new IntPtr(-1));
                if (result != SqliteOk)
                    throw new SqliteException(result, owner.ErrorMessage);
            }
        }
    }
}
