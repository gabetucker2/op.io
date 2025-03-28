using System;
using System.IO;

namespace op.io
{
    public static class DatabaseConfig
    {
        private static readonly string DatabaseDirectory = Path.Combine("Data");
        private static readonly string DatabaseFileName = "game.sqlite";

        public static string DatabaseFilePath => Path.Combine(DatabaseDirectory, DatabaseFileName);
        public static string ConnectionString => $"Data Source={DatabaseFilePath};Version=3;";
    }
}
