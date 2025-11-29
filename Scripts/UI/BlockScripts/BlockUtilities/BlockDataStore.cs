using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    /// <summary>
    /// Centralized helper for block-level persistence. Handles table creation,
    /// lock states, and drag/drop order storage so individual blocks stay lean.
    /// </summary>
    internal static class BlockDataStore
    {
        private const string LockRowKey = "__block_lock__";
        private const string TablePrefix = "Block_";

        private static readonly Dictionary<string, bool> _tableReady = new(StringComparer.OrdinalIgnoreCase);

        public static void ResetCache()
        {
            _tableReady.Clear();
        }

        public static void EnsureTables(SQLiteConnection connection, params DockPanelKind[] panelKinds)
        {
            if (panelKinds == null || panelKinds.Length == 0)
            {
                return;
            }

            foreach (DockPanelKind kind in panelKinds)
            {
                EnsureTable(kind, connection);
            }
        }

        public static Dictionary<string, int> LoadRowOrders(DockPanelKind panelKind)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                EnsureTable(panelKind, connection);

                string tableName = GetTableName(panelKind);
                string sql = $"SELECT RowKey, RenderOrder FROM {tableName} WHERE RowKey <> @lockKey ORDER BY RenderOrder ASC, RowKey ASC;";

                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);

                using SQLiteDataReader reader = command.ExecuteReader();
                Dictionary<string, int> orders = new(StringComparer.OrdinalIgnoreCase);

                while (reader.Read())
                {
                    string rowKey = NormalizeRowKey(reader["RowKey"]?.ToString());
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
                DebugLogger.PrintError($"Failed to load block rows for {panelKind}: {ex.Message}");
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

        public static void SaveRowOrders(DockPanelKind panelKind, IReadOnlyCollection<(string RowKey, int Order)> rows)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return;
            }

            try
            {
                EnsureTable(panelKind, connection);
                string tableName = GetTableName(panelKind);

                using SQLiteTransaction transaction = connection.BeginTransaction();

                using var clearCommand = new SQLiteCommand($"DELETE FROM {tableName} WHERE RowKey <> @lockKey;", connection, transaction);
                clearCommand.Parameters.AddWithValue("@lockKey", LockRowKey);
                clearCommand.ExecuteNonQuery();

                if (rows != null && rows.Count > 0)
                {
                    using var insert = new SQLiteCommand($"INSERT OR REPLACE INTO {tableName} (RowKey, RenderOrder, IsLocked) VALUES (@rowKey, @order, 0);", connection, transaction);
                    var rowKeyParam = insert.Parameters.Add("@rowKey", DbType.String);
                    var orderParam = insert.Parameters.Add("@order", DbType.Int32);

                    foreach ((string RowKey, int Order) row in rows)
                    {
                        string normalizedKey = NormalizeRowKey(row.RowKey);
                        if (string.IsNullOrWhiteSpace(normalizedKey) || row.Order <= 0)
                        {
                            continue;
                        }

                        rowKeyParam.Value = normalizedKey;
                        orderParam.Value = row.Order;
                        insert.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist block rows for {panelKind}: {ex.Message}");
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static bool GetPanelLock(DockPanelKind panelKind)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return false;
            }

            try
            {
                EnsureTable(panelKind, connection);

                string tableName = GetTableName(panelKind);
                using var command = new SQLiteCommand($"SELECT IsLocked FROM {tableName} WHERE RowKey = @lockKey LIMIT 1;", connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);
                object result = command.ExecuteScalar();
                return DecodeBool(result, false);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to read block lock for {panelKind}: {ex.Message}");
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

        public static void SetPanelLock(DockPanelKind panelKind, bool isLocked)
        {
            SQLiteConnection connection = OpenConnection(null, out bool disposeConnection);
            if (connection == null)
            {
                return;
            }

            try
            {
                EnsureTable(panelKind, connection);

                string tableName = GetTableName(panelKind);
                using var command = new SQLiteCommand($"INSERT OR REPLACE INTO {tableName} (RowKey, RenderOrder, IsLocked) VALUES (@lockKey, 0, @isLocked);", connection);
                command.Parameters.AddWithValue("@lockKey", LockRowKey);
                command.Parameters.AddWithValue("@isLocked", isLocked ? 1 : 0);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist block lock for {panelKind}: {ex.Message}");
            }
            finally
            {
                if (disposeConnection)
                {
                    DatabaseManager.CloseConnection(connection);
                }
            }
        }

        public static string GetTableName(DockPanelKind panelKind)
        {
            return string.Concat(TablePrefix, panelKind);
        }

        private static void EnsureTable(DockPanelKind panelKind, SQLiteConnection connection = null)
        {
            string tableName = GetTableName(panelKind);
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
    RenderOrder INTEGER NOT NULL DEFAULT 0,
    IsLocked INTEGER NOT NULL DEFAULT 0
);";

                using var createCommand = new SQLiteCommand(createSql, targetConnection);
                createCommand.ExecuteNonQuery();

                using var ensureLockRow = new SQLiteCommand($"INSERT OR IGNORE INTO {tableName} (RowKey, RenderOrder, IsLocked) VALUES (@lockKey, 0, 0);", targetConnection);
                ensureLockRow.Parameters.AddWithValue("@lockKey", LockRowKey);
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

        private static string NormalizeRowKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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
    }
}
