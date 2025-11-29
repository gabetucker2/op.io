using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class ControlsBlock
    {
        public const string PanelTitle = "Controls";

        private static readonly List<KeybindDisplayRow> _keybindCache = new();
        private static bool _keybindCacheLoaded;
        private static readonly StringBuilder _stringBuilder = new();
        private static readonly BlockDragState<KeybindDisplayRow> _dragState = new(row => row.Action, row => row.Bounds, (row, isDragging) =>
        {
            row.IsDragging = isDragging;
            return row;
        });
        private static Texture2D _pixelTexture;
        private static string _hoveredRowKey;
        private static float _lineHeightCache;
        private static string _hoveredTypeKey;
        private static bool _hoveredTypeIndicator;
        private static readonly BlockScrollPanel _scrollPanel = new();

        private static readonly Color HoverRowColor = new(38, 38, 38, 180);
        private static readonly Color DraggingRowBackground = new(24, 24, 24, 220);
        private static readonly Color DropIndicatorColor = new(110, 142, 255, 90);
        private static readonly Color TypeToggleIdleFill = new(38, 38, 38, 140);
        private static readonly Color TypeToggleHoverFill = new(68, 92, 160, 200);
        private static readonly Color TypeToggleActiveFill = new(68, 92, 160, 230);
        private const int TypeTogglePadding = 2;
        private const int TypeIndicatorDiameter = 10;

        private static bool IsSwitchType(InputType inputType) =>
            inputType == InputType.SaveSwitch || inputType == InputType.NoSaveSwitch;

        private static bool IsPersistentSwitch(InputType inputType) => inputType == InputType.SaveSwitch;

        private static InputType ParseInputTypeLabel(string typeLabel)
        {
            if (string.Equals(typeLabel, "Switch", StringComparison.OrdinalIgnoreCase))
            {
                return InputType.SaveSwitch;
            }

            return Enum.TryParse(typeLabel, true, out InputType parsed) ? parsed : InputType.Hold;
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool panelLocked = BlockManager.IsPanelLocked(DockPanelKind.Controls);

            if (!FontManager.TryGetControlsFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            EnsureKeybindCache();
            _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            float contentHeight = _keybindCache.Count * _lineHeightCache;
            _scrollPanel.Update(contentBounds, contentHeight, mouseState, previousMouseState);
            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            UpdateRowBounds(listBounds);
            UpdateTypeToggleBounds(boldFont);

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool pointerInsideList = listBounds.Contains(mouseState.Position);

            if (panelLocked && _dragState.IsDragging)
            {
                _dragState.Reset();
            }

            bool allowInteraction = !panelLocked && pointerInsideList;
            _hoveredRowKey = allowInteraction ? HitTestRow(mouseState.Position) : null;
            if (allowInteraction)
            {
                _hoveredTypeKey = HitTestTypeToggle(mouseState.Position, listBounds, out bool indicatorArea);
                _hoveredTypeIndicator = indicatorArea;
            }
            else
            {
                _hoveredTypeKey = null;
                _hoveredTypeIndicator = false;
            }

            if (_dragState.IsDragging)
            {
                _dragState.UpdateDrag(_keybindCache, listBounds, _lineHeightCache, mouseState);
                if (leftClickReleased)
                {
                    if (_dragState.TryCompleteDrag(_keybindCache, out bool orderChanged))
                    {
                        NormalizeCacheOrder();
                        if (orderChanged)
                        {
                            PersistRowOrder();
                        }
                    }
                }
            }
            else if (allowInteraction && leftClickStarted && TryToggleInputType(_hoveredTypeKey, boldFont, _hoveredTypeIndicator))
            {
                // Interaction consumed by type toggle.
            }
            else if (!panelLocked && pointerInsideList && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
            {
                _dragState.TryStartDrag(_keybindCache, _hoveredRowKey, mouseState);
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            if (!FontManager.TryGetControlsFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            bool panelLocked = BlockManager.IsPanelLocked(DockPanelKind.Controls);

            EnsureKeybindCache();
            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }
            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            EnsurePixelTexture();
            UpdateTypeToggleBounds(boldFont);

            float lineHeight = _lineHeightCache;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                Rectangle rowBounds = row.Bounds;
                if (rowBounds == Rectangle.Empty)
                {
                    continue;
                }

                if (rowBounds.Y >= listBounds.Bottom)
                {
                    break;
                }

                if (rowBounds.Bottom <= listBounds.Y)
                {
                    continue;
                }

                bool isDraggingRow = _dragState.IsDragging && string.Equals(row.Action, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (!isDraggingRow)
                {
                    DrawRowBackground(spriteBatch, row, rowBounds);
                    DrawRowContents(spriteBatch, row, rowBounds, lineHeight, boldFont, regularFont, listBounds, panelLocked);
                }
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                if (_dragState.DropIndicatorBounds != Rectangle.Empty)
                {
                    FillRect(spriteBatch, _dragState.DropIndicatorBounds, DropIndicatorColor);
                }

                DrawDraggingRow(spriteBatch, listBounds, lineHeight, boldFont, regularFont, panelLocked);
            }

            _scrollPanel.Draw(spriteBatch);
        }

        private static void EnsureKeybindCache()
        {
            if (_keybindCacheLoaded)
            {
                return;
            }

            _dragState.Reset();

            try
            {
                ControlKeyMigrations.EnsureApplied();
                _keybindCache.Clear();

                Dictionary<string, int> storedOrders = BlockDataStore.LoadRowOrders(DockPanelKind.Controls);
                Dictionary<string, string> storedRowData = BlockDataStore.LoadRowData(DockPanelKind.Controls);

                const string sql = "SELECT SettingKey, InputKey, InputType, COALESCE(RenderOrder, 0) AS ControlOrder FROM ControlKey ORDER BY ControlOrder ASC, SettingKey;";
                var rows = DatabaseQuery.ExecuteQuery(sql);
                int fallbackOrder = 1;

                foreach (var row in rows)
                {
                    string typeLabel = row.TryGetValue("InputType", out object type) ? type?.ToString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(typeLabel))
                    {
                        typeLabel = "Unknown";
                    }

                    InputType parsedType = ParseInputTypeLabel(typeLabel);

                    string actionLabel = row.TryGetValue("SettingKey", out object action) ? action?.ToString() ?? "Action" : "Action";
                    string inputLabel = row.TryGetValue("InputKey", out object key) ? key?.ToString() ?? "Key" : "Key";
                    int orderValue = row.TryGetValue("ControlOrder", out object orderObj) ? Convert.ToInt32(orderObj) : fallbackOrder;
                    int resolvedOrder = storedOrders.TryGetValue(actionLabel, out int storedOrder) ? storedOrder : orderValue;

                    bool triggerAuto = false;
                    string canonicalKey = BlockDataStore.CanonicalizeRowKey(DockPanelKind.Controls, actionLabel);
                    if (storedRowData.TryGetValue(canonicalKey, out string storedData) &&
                        TryParseRowData(storedData, out InputType storedType, out bool storedTriggerAuto) &&
                        !IsPersistentSwitch(storedType))
                    {
                        parsedType = storedType;
                        triggerAuto = storedTriggerAuto;
                    }

                    if (ControlKeyRules.RequiresSwitchSemantics(actionLabel))
                    {
                        parsedType = InputType.SaveSwitch;
                        triggerAuto = false;
                    }

                    bool toggleLocked = InputManager.IsTypeLocked(actionLabel);

                    string parsedTypeLabel = parsedType.ToString();
                    if (!string.Equals(typeLabel, parsedTypeLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        ControlKeyData.SetInputType(actionLabel, parsedTypeLabel);
                    }

                    InputManager.UpdateBindingInputType(actionLabel, parsedType, triggerAuto);
                    BlockDataStore.SetRowData(DockPanelKind.Controls, actionLabel, SerializeRowData(parsedType, triggerAuto));

                    _keybindCache.Add(new KeybindDisplayRow
                    {
                        Action = actionLabel,
                        Input = inputLabel,
                        TypeLabel = parsedTypeLabel,
                        InputType = parsedType,
                        RenderOrder = resolvedOrder > 0 ? resolvedOrder : fallbackOrder,
                        Bounds = Rectangle.Empty,
                        IsDragging = false,
                        TypeToggleBounds = Rectangle.Empty,
                        TriggerAutoFire = triggerAuto,
                        ToggleLocked = toggleLocked
                    });

                    fallbackOrder++;
                }

                _keybindCache.Sort((a, b) => a.RenderOrder.CompareTo(b.RenderOrder));
                NormalizeCacheOrder();
                PersistRowOrder();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load keybinds for controls panel: {ex.Message}");
            }
            finally
            {
                _keybindCacheLoaded = true;
            }
        }

        private static void PersistRowOrder()
        {
            List<(string RowKey, int Order)> blockOrders = new(_keybindCache.Count);
            List<(string SettingKey, int Order)> updates = new(_keybindCache.Count);
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (string.IsNullOrWhiteSpace(row.Action) || row.RenderOrder <= 0)
                {
                    continue;
                }

                blockOrders.Add((row.Action, row.RenderOrder));
                updates.Add((row.Action, row.RenderOrder));
            }

            BlockDataStore.SaveRowOrders(DockPanelKind.Controls, blockOrders);
            ControlKeyData.UpdateRenderOrders(updates);
        }

        private static void DrawRowBackground(SpriteBatch spriteBatch, KeybindDisplayRow row, Rectangle bounds)
        {
            if (bounds == Rectangle.Empty || _pixelTexture == null)
            {
                return;
            }

            if (!_dragState.IsDragging && string.Equals(_hoveredRowKey, row.Action, StringComparison.OrdinalIgnoreCase))
            {
                FillRect(spriteBatch, bounds, HoverRowColor);
            }
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont, bool panelLocked)
        {
            Rectangle dragBounds = _dragState.GetDragBounds(contentBounds, lineHeight);
            if (dragBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dragBounds, DraggingRowBackground);

            KeybindDisplayRow row = _dragState.DraggingSnapshot;
            row.Bounds = dragBounds;
            row.TypeToggleBounds = Rectangle.Empty;
            DrawRowContents(spriteBatch, row, dragBounds, lineHeight, boldFont, regularFont, contentBounds, panelLocked);
        }

        private static void DrawRowContents(SpriteBatch spriteBatch, KeybindDisplayRow row, Rectangle rowBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont, Rectangle contentBounds, bool panelLocked)
        {
            Vector2 labelPosition = new(rowBounds.X, rowBounds.Y);

            bool showToggle = !panelLocked && row.IsToggleCandidate && row.TypeToggleBounds != Rectangle.Empty;
            if (showToggle)
            {
                bool hovered = string.Equals(_hoveredTypeKey, row.Action, StringComparison.OrdinalIgnoreCase);
                Color fill = row.InputType == InputType.Trigger && row.TriggerAutoFire ? TypeToggleActiveFill : TypeToggleIdleFill;
                if (hovered)
                {
                    fill = TypeToggleHoverFill;
                }

                FillRect(spriteBatch, row.TypeToggleBounds, fill);
            }

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
            regularFont.DrawString(spriteBatch, value, new Vector2(valueX, rowBounds.Y), UIStyle.TextColor);

            if (IsSwitchType(row.InputType))
            {
                bool state = GetSwitchState(row.Action);
                BlockIndicatorRenderer.TryDrawBooleanIndicator(spriteBatch, contentBounds, lineHeight, rowBounds.Y, state);
            }
        }

        private static void UpdateRowBounds(Rectangle contentBounds)
        {
            if (_lineHeightCache <= 0f)
            {
                return;
            }

            int rowHeight = (int)MathF.Ceiling(_lineHeightCache);
            float y = contentBounds.Y - _scrollPanel.ScrollOffset;

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                var row = _keybindCache[i];
                row.Bounds = new Rectangle(contentBounds.X, (int)MathF.Round(y), contentBounds.Width, rowHeight);
                _keybindCache[i] = row;
                y += _lineHeightCache;
            }
        }

        private static void UpdateTypeToggleBounds(UIStyle.UIFont boldFont)
        {
            if (_lineHeightCache <= 0f || !boldFont.IsAvailable)
            {
                return;
            }

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                var row = _keybindCache[i];
                if (row.Bounds == Rectangle.Empty || IsPersistentSwitch(row.InputType) || row.InputType == InputType.Trigger || row.ToggleLocked)
                {
                    row.TypeToggleBounds = Rectangle.Empty;
                    _keybindCache[i] = row;
                    continue;
                }

                string actionPrefix = string.Concat(row.Action ?? string.Empty, " ");
                string typeLabel = string.Concat("[", row.TypeLabel ?? string.Empty, "]");
                Vector2 actionSize = boldFont.MeasureString(actionPrefix);
                Vector2 typeSize = boldFont.MeasureString(typeLabel);

                int x = (int)MathF.Round(row.Bounds.X + actionSize.X) - TypeTogglePadding;
                int y = row.Bounds.Y;
                int height = row.Bounds.Height > 0 ? row.Bounds.Height : (int)MathF.Ceiling(_lineHeightCache);
                int width = (int)MathF.Ceiling(typeSize.X) + (TypeTogglePadding * 2);

                row.TypeToggleBounds = new Rectangle(x, y, width, height);
                _keybindCache[i] = row;
            }
        }

        private static string HitTestRow(Point position)
        {
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (row.Bounds.Contains(position))
                {
                    return row.Action;
                }
            }

            return null;
        }

        private static string HitTestTypeToggle(Point position, Rectangle contentBounds, out bool indicatorArea)
        {
            indicatorArea = false;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (IsPersistentSwitch(row.InputType) || row.TypeToggleBounds == Rectangle.Empty)
                {
                    continue;
                }

                if (row.TypeToggleBounds.Contains(position))
                {
                    return row.Action;
                }

                if (row.InputType == InputType.Trigger && _lineHeightCache > 0f && row.IsToggleCandidate)
                {
                    int indicatorX = Math.Max(contentBounds.X, contentBounds.Right - TypeIndicatorDiameter - 4);
                    int indicatorY = (int)(row.Bounds.Y + ((_lineHeightCache - TypeIndicatorDiameter) / 2f));
                    Rectangle indicatorBounds = new(indicatorX, indicatorY, TypeIndicatorDiameter, TypeIndicatorDiameter);
                    if (indicatorBounds.Contains(position))
                    {
                        indicatorArea = true;
                        return row.Action;
                    }
                }
            }

            return null;
        }

        private static void NormalizeCacheOrder()
        {
            for (int i = 0; i < _keybindCache.Count; i++)
            {
                var row = _keybindCache[i];
                int expectedOrder = i + 1;
                if (row.RenderOrder != expectedOrder)
                {
                    row.RenderOrder = expectedOrder;
                    _keybindCache[i] = row;
                }
            }
        }

        private static int GetRowIndex(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return -1;
            }

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                if (string.Equals(_keybindCache[i].Action, settingKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryToggleInputType(string settingKey, UIStyle.UIFont boldFont, bool clickedIndicator)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            int index = GetRowIndex(settingKey);
            if (index < 0)
            {
                return false;
            }

            var row = _keybindCache[index];
            if (IsPersistentSwitch(row.InputType) || row.InputType == InputType.Trigger)
            {
                return false;
            }

            if (row.ToggleLocked)
            {
                return false;
            }

            InputType nextType = row.InputType;
            bool triggerAuto = row.TriggerAutoFire;

            if (row.InputType == InputType.Hold)
            {
                nextType = InputType.NoSaveSwitch;
                triggerAuto = false;
            }
            else if (row.InputType == InputType.NoSaveSwitch)
            {
                nextType = InputType.Hold;
                triggerAuto = false;
            }

            row.InputType = nextType;
            row.TypeLabel = nextType.ToString();
            row.TriggerAutoFire = triggerAuto;
            _keybindCache[index] = row;

            string serialized = SerializeRowData(nextType, triggerAuto);
            BlockDataStore.SetRowData(DockPanelKind.Controls, row.Action, serialized);
            ControlKeyData.SetInputType(row.Action, nextType.ToString());
            InputManager.UpdateBindingInputType(row.Action, nextType, triggerAuto);

            if (boldFont.IsAvailable)
            {
                UpdateTypeToggleBounds(boldFont);
            }

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

        private static void EnsurePixelTexture()
        {
            if (_pixelTexture != null || Core.Instance?.GraphicsDevice == null)
            {
                return;
            }

            _pixelTexture = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || spriteBatch == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private static string SerializeRowData(InputType inputType, bool triggerAuto)
        {
            return $"{inputType}|{(triggerAuto ? 1 : 0)}";
        }

        private static bool TryParseRowData(string data, out InputType inputType, out bool triggerAuto)
        {
            inputType = InputType.Hold;
            triggerAuto = false;

            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            string[] parts = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            inputType = ParseInputTypeLabel(parts[0]);

            if (IsSwitchType(inputType) || inputType == InputType.Hold)
            {
                triggerAuto = false;
                return true;
            }

            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedAuto))
            {
                triggerAuto = parsedAuto != 0;
            }

            return true;
        }

        private struct KeybindDisplayRow
        {
            public string Action;
            public string Input;
            public string TypeLabel;
            public InputType InputType;
            public int RenderOrder;
            public Rectangle Bounds;
            public Rectangle TypeToggleBounds;
            public bool IsDragging;
            public bool TriggerAutoFire;
            public bool ToggleLocked;
            public bool IsSwitchType => ControlsBlock.IsSwitchType(InputType);
            public bool IsPersistentSwitch => ControlsBlock.IsPersistentSwitch(InputType);
            public bool IsToggleCandidate => !IsPersistentSwitch && InputType != InputType.Trigger && !ToggleLocked;
        }
    }
}
