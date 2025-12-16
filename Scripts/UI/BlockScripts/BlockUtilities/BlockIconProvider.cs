using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using op.io;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal static class BlockIconProvider
    {
        private const string IconFolder = "Images\\Icons";
        private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.OrdinalIgnoreCase);

        public static Texture2D GetIcon(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            if (Cache.TryGetValue(fileName, out Texture2D cached))
            {
                if (cached != null && !cached.IsDisposed)
                {
                    return cached;
                }

                Cache.Remove(fileName);
            }

            GraphicsDevice device = Core.Instance?.GraphicsDevice;
            if (device == null)
            {
                return null;
            }

            string baseDir = AppContext.BaseDirectory ?? string.Empty;
            string iconPath = Path.Combine(baseDir, IconFolder, fileName);
            if (!File.Exists(iconPath))
            {
                string projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(projectRoot))
                {
                    string fallback = Path.Combine(projectRoot, IconFolder, fileName);
                    if (File.Exists(fallback))
                    {
                        iconPath = fallback;
                    }
                }
            }

            if (!File.Exists(iconPath))
            {
                DebugLogger.PrintWarning($"Icon '{fileName}' not found in '{IconFolder}'.");
                Cache[fileName] = null;
                return null;
            }

            try
            {
                using FileStream stream = File.OpenRead(iconPath);
                Texture2D texture = Texture2D.FromStream(device, stream);
                Cache[fileName] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load icon '{fileName}': {ex.Message}");
                Cache[fileName] = null;
                return null;
            }
        }
    }
}
