using System;
using System.IO;
using System.Data.SQLite;

namespace op.io
{
    public static class SQLScriptExecutor
    {
        public static bool RunSQLScript(SQLiteConnection connection, string scriptPath)
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
                using (var transaction = connection.BeginTransaction())
                using (var command = new SQLiteCommand(script, connection, transaction))
                {
                    command.ExecuteNonQuery();
                    transaction.Commit();
                }
                DebugLogger.PrintDatabase($"Executed successfully: {Path.GetFileName(scriptPath)}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed executing {Path.GetFileName(scriptPath)}: {ex.Message}");
                return false;
            }
        }
    }
}
