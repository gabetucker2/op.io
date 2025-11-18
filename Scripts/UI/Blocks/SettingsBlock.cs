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
        private static Texture2D _indicatorTexture;
        private const int IndicatorDiameter = 10;
        private static readonly Color SwitchActiveColor = new(72, 201, 115);
        private static readonly Color SwitchInactiveColor = new(192, 57, 43);

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

                _stringBuilder.Clear();
                _stringBuilder.Append(row.Action);
                _stringBuilder.Append(' ');
                _stringBuilder.Append('[');
                _stringBuilder.Append(row.TypeLabel);
                _stringBuilder.Append(']');
                string header = _stringBuilder.ToString();
                Vector2 headerSize = boldFont.MeasureString(header);
                boldFont.DrawString(spriteBatch, header, labelPosition, UIStyle.TextColor);

                _stringBuilder.Clear();
                _stringBuilder.Append(":  ");
                _stringBuilder.Append(row.Input);
                string value = _stringBuilder.ToString();
                float valueX = labelPosition.X + headerSize.X;
                regularFont.DrawString(spriteBatch, value, new Vector2(valueX, y), UIStyle.TextColor);

                if (row.IsSwitch && EnsureIndicatorTexture(spriteBatch))
                {
                    int indicatorX = Math.Max(contentBounds.X, contentBounds.Right - IndicatorDiameter - 4);
                    int indicatorY = (int)(y + ((lineHeight - IndicatorDiameter) / 2f));
                    Rectangle indicatorBounds = new(indicatorX, indicatorY, IndicatorDiameter, IndicatorDiameter);
                    Color stateColor = GetSwitchState(row.Action) ? SwitchActiveColor : SwitchInactiveColor;
                    spriteBatch.Draw(_indicatorTexture, indicatorBounds, stateColor);
                }

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
                ControlKeyMigrations.EnsureApplied();
                _keybindCache.Clear();
                var rows = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlKey ORDER BY SettingKey;");
                foreach (var row in rows)
                {
                    string typeLabel = row.TryGetValue("InputType", out object type) ? type?.ToString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(typeLabel))
                    {
                        typeLabel = "Unknown";
                    }

                    if (!Enum.TryParse(typeLabel, true, out InputType parsedType))
                    {
                        parsedType = InputType.Hold;
                    }

                    string actionLabel = row.TryGetValue("SettingKey", out object action) ? action?.ToString() ?? "Action" : "Action";
                    string inputLabel = row.TryGetValue("InputKey", out object key) ? key?.ToString() ?? "Key" : "Key";

                    if (ControlKeyRules.RequiresSwitchSemantics(actionLabel))
                    {
                        parsedType = InputType.Switch;
                    }

                    _keybindCache.Add(new KeybindDisplayRow
                    {
                        Action = actionLabel,
                        Input = inputLabel,
                        TypeLabel = parsedType.ToString(),
                        InputType = parsedType
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

        private static bool EnsureIndicatorTexture(SpriteBatch spriteBatch)
        {
            if (_indicatorTexture != null && !_indicatorTexture.IsDisposed)
            {
                return true;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return false;
            }

            int diameter = IndicatorDiameter;
            int pixels = diameter * diameter;
            Color[] data = new Color[pixels];
            float radius = (diameter - 1) / 2f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float distanceSquared = (dx * dx) + (dy * dy);
                    data[(y * diameter) + x] = distanceSquared <= radius * radius ? Color.White : Color.Transparent;
                }
            }

            _indicatorTexture = new Texture2D(graphicsDevice, diameter, diameter);
            _indicatorTexture.SetData(data);
            return true;
        }

        private static bool GetSwitchState(string settingKey)
        {
            if (ControlStateManager.ContainsSwitchState(settingKey))
            {
                return ControlStateManager.GetSwitchState(settingKey);
            }

            return InputManager.IsInputActive(settingKey);
        }

        private struct KeybindDisplayRow
        {
            public string Action;
            public string Input;
            public string TypeLabel;
            public InputType InputType;
            public bool IsSwitch => InputType == InputType.Switch;
        }
    }
}
