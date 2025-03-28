using System;
using System.IO;

namespace op.io
{
    public static class DatabaseConfig
    {
        private static readonly string ProjectRoot = AppContext.BaseDirectory
            .Split(["\\bin\\"], StringSplitOptions.None)[0];

        private static readonly string DatabaseDirectory = Path.Combine(ProjectRoot, "Data");
        private static readonly string DatabaseFileName = "op.io.db";

        public static string DatabaseFilePath => Path.Combine(DatabaseDirectory, DatabaseFileName);
        public static string ConnectionString => $"Data Source={DatabaseFilePath};Version=3;";
    }

}
