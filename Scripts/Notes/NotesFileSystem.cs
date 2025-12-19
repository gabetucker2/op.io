using System;
using System.IO;
using System.Text;

namespace op.io
{
    internal static class NotesFileSystem
    {
        private const string DefaultNoteNameInternal = "note";
        private const string DefaultNoteContents = "Write here";

        private static readonly string ProjectRoot = ResolveProjectRoot();

        public static string NotesDirectoryPath { get; } = Path.Combine(ProjectRoot, "UserNotes");
        public static string DefaultNotePath { get; } = Path.Combine(NotesDirectoryPath, $"{DefaultNoteNameInternal}.txt");
        public static string DefaultNoteName => DefaultNoteNameInternal;

        public static void ResetToDefaultNote()
        {
            try
            {
                Directory.CreateDirectory(NotesDirectoryPath);

                foreach (string file in Directory.EnumerateFiles(NotesDirectoryPath))
                {
                    TryDeleteFile(file);
                }

                foreach (string directory in Directory.EnumerateDirectories(NotesDirectoryPath))
                {
                    TryDeleteDirectory(directory);
                }

                File.WriteAllText(DefaultNotePath, DefaultNoteContents, Encoding.UTF8);
                DebugLogger.PrintDatabase($"Reset notes directory to default at '{DefaultNotePath}'.");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to reset notes directory '{NotesDirectoryPath}': {ex.Message}");
            }
        }

        private static string ResolveProjectRoot()
        {
            if (!string.IsNullOrWhiteSpace(DatabaseConfig.ProjectRootPath))
            {
                return DatabaseConfig.ProjectRootPath;
            }

            string fallback = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
            return string.IsNullOrWhiteSpace(fallback) ? AppContext.BaseDirectory : fallback;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to delete note file '{path}': {ex.Message}");
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to delete notes subdirectory '{path}': {ex.Message}");
            }
        }
    }
}
