using System;
using System.Data.SQLite;
using System.IO;

namespace op.io
{
    public static class DatabaseConfig
    {
        // Existing logic for project and database directory setup
        private static readonly string ProjectRoot = AppContext.BaseDirectory
            .Split(new[] { "\\bin\\" }, StringSplitOptions.None)[0];

        private static readonly string DatabaseDirectory = Path.Combine(ProjectRoot, "Data");
        private static readonly string DatabaseFileName = "op.io.db";

        public static string DatabaseFilePath => Path.Combine(DatabaseDirectory, DatabaseFileName);
        public static string ConnectionString => $"Data Source={DatabaseFilePath};Version=3;";

        // Method to read a setting from the DebugSettings table
        public static T GetSetting<T>(string tableName, string columnName, string settingKey, T defaultValue)
        {
            try
            {
                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    // Remove the reference to `Group` in the WHERE clause
                    string query = $"SELECT {columnName} FROM {tableName} WHERE Setting = @settingKey;";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@settingKey", settingKey);

                        var result = command.ExecuteScalar();

                        // If result is DBNull or null, throw an exception
                        if (result == DBNull.Value || result == null)
                        {
                            throw new InvalidOperationException(
                                $"No result found for {columnName} in {tableName} where Setting = '{settingKey}'. Returning default value: {defaultValue}"
                            );
                        }

                        // Cast the result to the expected type
                        return (T)Convert.ChangeType(result, typeof(T));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve setting '{settingKey}' from {tableName}: {ex.Message}", ex);
            }
        }

        // Method to update a setting in the DebugSettings table
        public static void UpdateSetting(string tableName, string columnName, string settingKey, string group, int newValue)
        {
            try
            {
                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    string query = $"UPDATE {tableName} SET {columnName} = @newValue WHERE Setting = @settingKey AND Group = @group;";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@newValue", newValue);
                        command.Parameters.AddWithValue("@settingKey", settingKey);
                        command.Parameters.AddWithValue("@group", group);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            DebugLogger.PrintMeta($"Successfully updated '{settingKey}' in {tableName}.");
                        }
                        else
                        {
                            DebugLogger.PrintWarning($"No rows updated for '{settingKey}' in {tableName}. Check the provided parameters.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to update setting '{settingKey}' in {tableName}: {ex.Message}");
            }
        }

        public static int LoadDebugSettings()
        {
            return GetSetting<int>("DebugSettings", "Enabled", "General", 0);
        }

        public static void ToggleDebugMode(int newState)
        {
            UpdateSetting("DebugSettings", "Enabled", "Setting", "General", newState);
        }

    }
}
