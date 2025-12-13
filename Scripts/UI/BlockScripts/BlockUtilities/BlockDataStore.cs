using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    /// <summary>
    /// Centralized helper for block-level persistence. Handles table creation,
    /// lock states, and drag/drop order storage so individual blocks stay lean.
    /// </summary>
    internal static class BlockDataStore
    {
        private const string LockRowKey = "BlockLock";
        private const string LegacyLockRowKey = "__block_lock__";
        private const string TablePrefix = "Block";
        private const string LegacyTablePrefix = "Block_";
        private const string RowDataColumnName = "RowData";

        private static readonly Dictionary<string, bool> _tableReady = new(StringComparer.OrdinalIgnoreCase);

        public static void ResetCache()
        {
            _tableReady.Clear();
        }

        public static void EnsureTables(SQLiteConnection connection, params DockBlockKind[] blockKinds)
        {
            if (blockKinds == null || blockKinds.Length == 0)
            {
                return;
            }

            foreach (DockBlockKind kind in blockKinds)
            {
                EnsureTable(kind, connection);
            }
        }

        public static Dictionary<string, int> LoadRowOrders(DockBlockKind blockKind)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                EnsureTable(blockKind, connection);

                string tableName = GetTableName(blockKind);
                string sql = $"SELECT RowKey, RenderOrder FROM {tableName} WHERE RowKey <> @lockKey ORDER BY RenderOrder ASC, RowKey ASC;";

                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);

                using SQLiteDataReader reader = command.ExecuteReader();
                Dictionary<string, int> orders = new(StringComparer.OrdinalIgnoreCase);

                while (reader.Read())
                {
                    string rowKey = NormalizeRowKey(blockKind, reader["RowKey"]?.ToString());
                    int order = DecodeInt(reader["RenderOrder"], orders.Count + 1);

                    if (!string.IsNullOrWhiteSpace(rowKey) && order > 0)
                    {
                        orders[rowKey] = order;
                    }
                }

                return orders;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load block rows for {blockKind}: {ex.Message}");
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static void SaveRowOrders(DockBlockKind blockKind, IReadOnlyCollection<(string RowKey, int Order)> rows)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return;
            }

            try
            {
                EnsureTable(blockKind, connection);
                string tableName = GetTableName(blockKind);

                using SQLiteTransaction transaction = connection.BeginTransaction();

                if (rows != null && rows.Count > 0)
                {
                    HashSet<string> normalizedKeys = new(StringComparer.OrdinalIgnoreCase);
                    foreach ((string RowKey, int Order) row in rows)
                    {
                        string normalizedKey = NormalizeRowKey(blockKind, row.RowKey);
                        if (string.IsNullOrWhiteSpace(normalizedKey))
                        {
                            continue;
                        }

                        normalizedKeys.Add(normalizedKey);
                    }

                    if (normalizedKeys.Count > 0)
                    {
                        // Remove rows that are no longer present, but keep the lock row intact.
                        string placeholders = string.Join(", ", normalizedKeys.Select((_, i) => $"@row{i}"));
                        using var delete = new SQLiteCommand($"DELETE FROM {tableName} WHERE RowKey <> @lockKey AND RowKey NOT IN ({placeholders});", connection, transaction);
                        delete.Parameters.AddWithValue("@lockKey", LockRowKey);
                        int index = 0;
                        foreach (string key in normalizedKeys)
                        {
                            delete.Parameters.AddWithValue($"@row{index++}", key);
                        }
                        delete.ExecuteNonQuery();
                    }

                    using var upsert = new SQLiteCommand($@"
INSERT INTO {tableName} (RowKey, RenderOrder)
VALUES (@rowKey, @order)
ON CONFLICT(RowKey) DO UPDATE SET RenderOrder = excluded.RenderOrder;", connection, transaction);
                    var rowKeyParam = upsert.Parameters.Add("@rowKey", DbType.String);
                    var orderParam = upsert.Parameters.Add("@order", DbType.Int32);

                    foreach ((string RowKey, int Order) row in rows)
                    {
                        string normalizedKey = NormalizeRowKey(blockKind, row.RowKey);
                        if (string.IsNullOrWhiteSpace(normalizedKey) || row.Order <= 0)
                        {
                            continue;
                        }

                        rowKeyParam.Value = normalizedKey;
                        orderParam.Value = row.Order;
                        upsert.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist block rows for {blockKind}: {ex.Message}");
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static bool GetBlockLock(DockBlockKind blockKind)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return false;
            }

            try
            {
                EnsureTable(blockKind, connection);

                string tableName = GetTableName(blockKind);
                using var command = new SQLiteCommand($"SELECT IsLocked FROM {tableName} WHERE RowKey = @lockKey LIMIT 1;", connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);
                object result = command.ExecuteScalar();
                return DecodeBool(result, IsLockedByDefault(blockKind));
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to read block lock for {blockKind}: {ex.Message}");
                return false;
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static void SetBlockLock(DockBlockKind blockKind, bool isLocked)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return;
            }

            try
            {
                EnsureTable(blockKind, connection);

                string tableName = GetTableName(blockKind);
                using var command = new SQLiteCommand($"INSERT OR REPLACE INTO {tableName} (RowKey, RenderOrder, IsLocked) VALUES (@lockKey, NULL, @isLocked);", connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);
                command.Parameters.AddWithValue("@isLocked", isLocked ? 1 : 0);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist block lock for {blockKind}: {ex.Message}");
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static string GetTableName(DockBlockKind blockKind)
        {
            return string.Concat(TablePrefix, blockKind);
        }

        public static string CanonicalizeRowKey(DockBlockKind blockKind, string rowKey)
        {
            return NormalizeRowKey(blockKind, rowKey);
        }

        public static Dictionary<string, string> LoadRowData(DockBlockKind blockKind)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                EnsureTable(blockKind, connection);
                string tableName = GetTableName(blockKind);

                string sql = $"SELECT RowKey, {RowDataColumnName} FROM {tableName} WHERE RowKey <> @lockKey;";
                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);

                using SQLiteDataReader reader = command.ExecuteReader();
                Dictionary<string, string> data = new(StringComparer.OrdinalIgnoreCase);

                while (reader.Read())
                {
                    string rowKey = NormalizeRowKey(blockKind, reader["RowKey"]?.ToString());
                    if (string.IsNullOrWhiteSpace(rowKey))
                    {
                        continue;
                    }

                    string rowData = reader[RowDataColumnName]?.ToString();
                    if (!string.IsNullOrWhiteSpace(rowData))
                    {
                        data[rowKey] = rowData;
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load block row data for {blockKind}: {ex.Message}");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static void SetRowData(DockBlockKind blockKind, string rowKey, string rowData)
        {
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                return;
            }

            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return;
            }

            try
            {
                EnsureTable(blockKind, connection);
                string tableName = GetTableName(blockKind);

                using var command = new SQLiteCommand($@"
INSERT INTO {tableName} (RowKey, {RowDataColumnName})
VALUES (@rowKey, @rowData)
ON CONFLICT(RowKey) DO UPDATE SET {RowDataColumnName} = excluded.{RowDataColumnName};", connection);
                command.Parameters.AddWithValue("@rowKey", NormalizeRowKey(blockKind, rowKey));
                command.Parameters.AddWithValue("@rowData", rowData ?? string.Empty);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist block row data for {blockKind}: {ex.Message}");
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        private static void EnsureTable(DockBlockKind blockKind, SQLiteConnection connection = null)
        {
            string tableName = GetTableName(blockKind);
            if (_tableReady.ContainsKey(tableName))
            {
                return;
            }

            bool disposeConnection = false;
            SQLiteConnection targetConnection = connection ?? OpenConnection(null, out disposeConnection);
            if (targetConnection == null)
            {
                return;
            }

            try
            {
            string createSql = $@"
CREATE TABLE IF NOT EXISTS {tableName} (
    RowKey TEXT PRIMARY KEY,
    RenderOrder INTEGER,
    IsLocked INTEGER NOT NULL DEFAULT 0
);";

                using var createCommand = new SQLiteCommand(createSql, targetConnection);
                createCommand.ExecuteNonQuery();

                EnsureRowDataColumn(targetConnection, tableName);
                MigrateLegacyTable(targetConnection, blockKind, tableName);
                MigrateLegacyLockRow(targetConnection, tableName);

                int defaultLockValue = IsLockedByDefault(blockKind) ? 1 : 0;
                using var ensureLockRow = new SQLiteCommand($"INSERT OR IGNORE INTO {tableName} (RowKey, IsLocked) VALUES (@lockKey, @isLocked);", targetConnection);
                ensureLockRow.Parameters.AddWithValue("@lockKey", LockRowKey);
                ensureLockRow.Parameters.AddWithValue("@isLocked", defaultLockValue);
                ensureLockRow.ExecuteNonQuery();

                _tableReady[tableName] = true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to ensure block table {tableName}: {ex.Message}");
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(targetConnection);
                }
            }
        }

        private static SQLiteConnection OpenConnection(SQLiteConnection connectionOverride, out bool shouldDispose)
        {
            if (connectionOverride != null)
            {
                shouldDispose = false;
                return connectionOverride;
            }

            SQLiteConnection connection = DatabaseManager.OpenConnection();
            if (connection != null)
            {
                DatabaseConfig.ConfigureDatabase(connection);
            }

            shouldDispose = connection != null;
            return connection;
        }

        private static void MigrateLegacyTable(SQLiteConnection connection, DockBlockKind blockKind, string newTableName)
        {
            string legacyTableName = string.Concat(LegacyTablePrefix, blockKind);
            if (!TableExists(connection, legacyTableName))
            {
                return;
            }

            // Skip migration if the new table already has data.
            using (var countNew = new SQLiteCommand($"SELECT COUNT(1) FROM {newTableName};", connection))
            {
                object newCount = countNew.ExecuteScalar();
                if (Convert.ToInt32(newCount) > 0)
                {
                    return;
                }
            }

            string migrateSql = $@"
INSERT OR IGNORE INTO {newTableName} (RowKey, RenderOrder, IsLocked)
SELECT RowKey, RenderOrder, IsLocked FROM {legacyTableName};";

            using var migrateCommand = new SQLiteCommand(migrateSql, connection);
            migrateCommand.ExecuteNonQuery();
        }

        private static void MigrateLegacyLockRow(SQLiteConnection connection, string tableName)
        {
            // Copy old lock row value if present, then remove legacy row.
            string migrateSql = $@"
INSERT OR REPLACE INTO {tableName} (RowKey, RenderOrder, IsLocked)
SELECT @newKey, RenderOrder, IsLocked FROM {tableName} WHERE RowKey = @oldKey LIMIT 1;
DELETE FROM {tableName} WHERE RowKey = @oldKey;";

            using var migrateCommand = new SQLiteCommand(migrateSql, connection);
            migrateCommand.Parameters.AddWithValue("@newKey", LockRowKey);
            migrateCommand.Parameters.AddWithValue("@oldKey", LegacyLockRowKey);
            migrateCommand.ExecuteNonQuery();
        }

        private static bool TableExists(SQLiteConnection connection, string tableName)
        {
            using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1;", connection);
            command.Parameters.AddWithValue("@name", tableName);
            object result = command.ExecuteScalar();
            return result != null && result != DBNull.Value;
        }

        private static string NormalizeRowKey(DockBlockKind blockKind, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Equals(LegacyLockRowKey, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(LockRowKey, StringComparison.OrdinalIgnoreCase))
            {
                return LockRowKey;
            }

            if (blockKind == DockBlockKind.Controls &&
                trimmed.Equals("PanelMenu", StringComparison.OrdinalIgnoreCase))
            {
                return "BlockMenu";
            }

            if (blockKind == DockBlockKind.Specs)
            {
                return NormalizeSpecsKey(trimmed);
            }

            return trimmed;
        }

        private static string NormalizeSpecsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string lower = key.Trim();
            if (lower.Equals("target_fps", StringComparison.OrdinalIgnoreCase))
            {
                return "TargetFPS";
            }

            if (lower.Equals("fps", StringComparison.OrdinalIgnoreCase))
            {
                return "FPS";
            }

            if (lower.Equals("frame_time", StringComparison.OrdinalIgnoreCase))
            {
                return "FrameTime";
            }

            if (lower.Equals("window_mode", StringComparison.OrdinalIgnoreCase))
            {
                return "WindowMode";
            }

            if (lower.Equals("fixed_time", StringComparison.OrdinalIgnoreCase))
            {
                return "FixedTime";
            }

            if (lower.Equals("window_size", StringComparison.OrdinalIgnoreCase))
            {
                return "WindowSize";
            }

            if (lower.Equals("surface_format", StringComparison.OrdinalIgnoreCase))
            {
                return "SurfaceFormat";
            }

            if (lower.Equals("depth_format", StringComparison.OrdinalIgnoreCase))
            {
                return "DepthFormat";
            }

            if (lower.Equals("graphics_profile", StringComparison.OrdinalIgnoreCase))
            {
                return "GraphicsProfile";
            }

            if (lower.Equals("backbuffer", StringComparison.OrdinalIgnoreCase))
            {
                return "Backbuffer";
            }

            if (lower.Equals("adapter", StringComparison.OrdinalIgnoreCase))
            {
                return "Adapter";
            }

            if (lower.Equals("cpu_threads", StringComparison.OrdinalIgnoreCase))
            {
                return "CPUThreads";
            }

            if (lower.Equals("process_memory", StringComparison.OrdinalIgnoreCase))
            {
                return "ProcessMemory";
            }

            if (lower.Equals("managed_memory", StringComparison.OrdinalIgnoreCase))
            {
                return "ManagedMemory";
            }

            if (lower.Equals("vsync", StringComparison.OrdinalIgnoreCase))
            {
                return "VSync";
            }

            if (lower.Equals("os", StringComparison.OrdinalIgnoreCase))
            {
                return "OS";
            }

            return key.Trim();
        }

        private static string NormalizeRowKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool IsLockedByDefault(DockBlockKind blockKind)
        {
            return blockKind == DockBlockKind.ColorScheme;
        }

        private static int DecodeInt(object rawValue, int fallback)
        {
            if (rawValue == null || rawValue == DBNull.Value)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(rawValue);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool DecodeBool(object rawValue, bool fallback)
        {
            if (rawValue == null || rawValue == DBNull.Value)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(rawValue) != 0;
            }
            catch
            {
                return fallback;
            }
        }

        private static void EnsureRowDataColumn(SQLiteConnection connection, string tableName)
        {
            if (connection == null || string.IsNullOrWhiteSpace(tableName))
            {
                return;
            }

            if (ColumnExists(connection, tableName, RowDataColumnName))
            {
                return;
            }

            using var command = new SQLiteCommand($"ALTER TABLE {tableName} ADD COLUMN {RowDataColumnName} TEXT;", connection);
            command.ExecuteNonQuery();
        }

        private static bool ColumnExists(SQLiteConnection connection, string tableName, string columnName)
        {
            if (connection == null || string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName))
            {
                return false;
            }

            string pragmaSql = $"PRAGMA table_info({tableName});";
            using var command = new SQLiteCommand(pragmaSql, connection);

            using SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["name"] != null &&
                    string.Equals(reader["name"].ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
