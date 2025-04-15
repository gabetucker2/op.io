using System;
using System.IO;
using System.Data.SQLite;

namespace op.io
{
    public static class SQLScriptExecutor
    {
        public static bool RunSQLScript(SQLiteConnection connection, string scriptPath, SQLiteTransaction transaction = null)
        {
            if (!File.Exists(scriptPath))
            {
                DebugLogger.PrintError($"SQL script not found: {scriptPath}");
                return false;
            }

            string script = File.ReadAllText(scriptPath);
            if (string.IsNullOrWhiteSpace(script))
            {
                DebugLogger.PrintError($"SQL script is empty: {scriptPath}");
                return false;
            }

            try
            {
                if (transaction == null)
                {
                    // If no transaction is provided, create one for this script execution
                    using var localTransaction = connection.BeginTransaction();
                    using var command = new SQLiteCommand(script, connection, localTransaction);
                    command.ExecuteNonQuery();
                    localTransaction.Commit();
                }
                else
                {
                    // Execute within the provided transaction
                    using (var command = new SQLiteCommand(script, connection, transaction))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                DebugLogger.PrintDatabase($"Executed successfully: {Path.GetFileName(scriptPath)}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed executing {Path.GetFileName(scriptPath)}: {ex.Message}");

                if (transaction != null)
                {
                    DebugLogger.PrintWarning($"Rolling back transaction due to error in {Path.GetFileName(scriptPath)}.");
                    transaction.Rollback();
                }

                return false;
            }
        }
    }
}
