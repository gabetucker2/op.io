using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.Blocks
{
    internal static class SettingsBlock
    {
        private static readonly List<KeybindDisplayRow> _keybindCache = new();
        private static bool _keybindCacheLoaded;
        private static readonly StringBuilder _stringBuilder = new();

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            UIStyle.UIFont boldFont = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            UIStyle.UIFont regularFont = UIStyle.FontTech;
            if (!boldFont.IsAvailable || !regularFont.IsAvailable)
            {
                return;
            }

            EnsureKeybindCache();

            float lineHeight = Math.Max(boldFont.LineHeight, regularFont.LineHeight) + 2f;
            float y = contentBounds.Y;

            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (y + lineHeight > contentBounds.Bottom)
                {
                    break;
                }

                Vector2 labelPosition = new(contentBounds.X, y);
                Vector2 labelSize = boldFont.MeasureString(row.Action);
                boldFont.DrawString(spriteBatch, row.Action, labelPosition, UIStyle.TextColor);

                float colonX = labelPosition.X + labelSize.X;
                string colon = ":";
                Vector2 colonSize = regularFont.MeasureString(colon);
                regularFont.DrawString(spriteBatch, colon, new Vector2(colonX, y), UIStyle.TextColor);

                string value = $" {row.Input} [{row.Type}]";
                float valueX = colonX + colonSize.X + 4f;
                regularFont.DrawString(spriteBatch, value, new Vector2(valueX, y), UIStyle.TextColor);
                y += lineHeight;
            }
        }

        private static void EnsureKeybindCache()
        {
            if (_keybindCacheLoaded)
            {
                return;
            }

            try
            {
                _keybindCache.Clear();
                var rows = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlKey ORDER BY SettingKey;");
                foreach (var row in rows)
                {
                    _keybindCache.Add(new KeybindDisplayRow
                    {
                        Action = row.TryGetValue("SettingKey", out object action) ? action?.ToString() ?? "Action" : "Action",
                        Input = row.TryGetValue("InputKey", out object key) ? key?.ToString() ?? "Key" : "Key",
                        Type = row.TryGetValue("InputType", out object type) ? type?.ToString() ?? string.Empty : string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load keybinds for settings panel: {ex.Message}");
            }
            finally
            {
                _keybindCacheLoaded = true;
            }
        }

        private struct KeybindDisplayRow
        {
            public string Action;
            public string Input;
            public string Type;
        }
    }
}
