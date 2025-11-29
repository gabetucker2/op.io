using System;
using System.Collections.Generic;
using System.Linq;
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
        private static string _hoveredKeyAction;
        private static float _lineHeightCache;
        private static string _hoveredTypeKey;
        private static bool _hoveredTypeIndicator;
        private static readonly BlockScrollPanel _scrollPanel = new();
        private static string _pressedRowKey;
        private static Point _pressStartPosition;
        private static bool _rebindOverlayVisible;
        private static string _rebindAction;
        private static string _rebindCurrentInput;
        private static string _rebindPendingDisplay;
        private static string _rebindCurrentCanonical;
        private static string _rebindPendingCanonical;
        private static Rectangle _rebindModalBounds;
        private static Rectangle _rebindConfirmButtonBounds;
        private static Rectangle _rebindUnbindButtonBounds;
        private static bool _rebindCaptured;
        private static bool _suppressNextCapture;
        private static string _rebindConflictWarning;
        private static bool _rebindConfirmHovered;
        private static bool _rebindUnbindHovered;
        private static bool _rebindPendingUnbind;

        private static readonly Color HoverRowColor = new(38, 38, 38, 180);
        private static readonly Color DraggingRowBackground = new(24, 24, 24, 220);
        private static readonly Color DropIndicatorColor = new(110, 142, 255, 90);
        private static readonly Color TypeToggleIdleFill = new(38, 38, 38, 140);
        private static readonly Color TypeToggleHoverFill = new(68, 92, 160, 200);
        private static readonly Color TypeToggleActiveFill = new(68, 92, 160, 230);
        private static readonly Color RebindScrimColor = new(8, 8, 8, 190);
        private static readonly Color WarningColor = new(240, 196, 64);
        private const int TypeTogglePadding = 2;
        private const int TypeIndicatorDiameter = 10;
        private const int ValueHighlightPadding = 2;
        private const int DragStartThreshold = 6;

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
            UpdateKeyValueBounds(boldFont, regularFont);

            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickStarted = leftDown && !leftDownPrev;
            bool leftClickReleased = !leftDown && leftDownPrev;
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
                _hoveredKeyAction = HitTestKeyValue(mouseState.Position);
            }
            else
            {
                _hoveredTypeKey = null;
                _hoveredTypeIndicator = false;
                _hoveredKeyAction = null;
            }

            if (!allowInteraction)
            {
                _pressedRowKey = null;
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
            else
            {
                if (allowInteraction && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
                {
                    _pressedRowKey = _hoveredRowKey;
                    _pressStartPosition = mouseState.Position;
                }

                if (allowInteraction && _pressedRowKey != null && leftDown && !_dragState.IsDragging)
                {
                    if (HasDragExceededThreshold(mouseState.Position))
                    {
                        _dragState.TryStartDrag(_keybindCache, _pressedRowKey, mouseState);
                        _pressedRowKey = null;
                    }
                }

                if (leftClickReleased)
                {
                    if (_dragState.IsDragging)
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
                    else if (allowInteraction &&
                        !string.IsNullOrEmpty(_pressedRowKey) &&
                        string.Equals(_pressedRowKey, _hoveredRowKey, StringComparison.OrdinalIgnoreCase))
                    {
                        BeginRebindFlow(_pressedRowKey);
                    }

                    _pressedRowKey = null;
                }
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
                    DrawRowBackground(spriteBatch, row, rowBounds, panelLocked);
                    DrawRowContents(spriteBatch, row, rowBounds, lineHeight, boldFont, regularFont, listBounds, panelLocked);
                }
            }

            if (!panelLocked && _dragState.IsDragging && _dragState.HasSnapshot)
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
                        KeyValueBounds = Rectangle.Empty,
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

        private static void DrawRowBackground(SpriteBatch spriteBatch, KeybindDisplayRow row, Rectangle bounds, bool panelLocked)
        {
            if (panelLocked || bounds == Rectangle.Empty || _pixelTexture == null)
            {
                return;
            }

            if (ShouldHighlightRow(row, panelLocked))
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
            row.KeyValueBounds = Rectangle.Empty;
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

            // Build a background aligned to the keybind text (after the ":  " prefix) so it visually sits behind the clickable keys.
            Rectangle keyValueBounds = row.KeyValueBounds;
            if (keyValueBounds == Rectangle.Empty && !string.IsNullOrWhiteSpace(row.Input))
            {
                float keyTextX = valueX + regularFont.MeasureString(":  ").X;
                float keyTextWidth = regularFont.MeasureString(row.Input).X;
                keyValueBounds = new Rectangle(
                    (int)MathF.Floor(keyTextX) - ValueHighlightPadding,
                    rowBounds.Y,
                    (int)MathF.Ceiling(keyTextWidth) + (ValueHighlightPadding * 2),
                    rowBounds.Height);
            }

            if (!panelLocked && keyValueBounds != Rectangle.Empty)
            {
                bool keyHovered = string.Equals(_hoveredKeyAction, row.Action, StringComparison.OrdinalIgnoreCase);
                Color fill = keyHovered ? TypeToggleHoverFill : TypeToggleIdleFill;
                FillRect(spriteBatch, keyValueBounds, fill);
            }
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

        private static bool HasDragExceededThreshold(Point position)
        {
            int deltaX = Math.Abs(position.X - _pressStartPosition.X);
            int deltaY = Math.Abs(position.Y - _pressStartPosition.Y);
            return deltaX >= DragStartThreshold || deltaY >= DragStartThreshold;
        }

        private static bool ShouldHighlightRow(KeybindDisplayRow row, bool panelLocked)
        {
            return !panelLocked &&
                !_dragState.IsDragging &&
                !string.IsNullOrWhiteSpace(_hoveredRowKey) &&
                string.Equals(_hoveredRowKey, row.Action, StringComparison.OrdinalIgnoreCase);
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

        private static void UpdateKeyValueBounds(UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            if (_lineHeightCache <= 0f || !boldFont.IsAvailable || !regularFont.IsAvailable)
            {
                return;
            }

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                var row = _keybindCache[i];
                if (row.Bounds == Rectangle.Empty || string.IsNullOrWhiteSpace(row.Input))
                {
                    row.KeyValueBounds = Rectangle.Empty;
                    _keybindCache[i] = row;
                    continue;
                }

                _stringBuilder.Clear();
                _stringBuilder.Append(row.Action);
                _stringBuilder.Append(' ');
                _stringBuilder.Append('[');
                _stringBuilder.Append(row.TypeLabel);
                _stringBuilder.Append(']');
                string header = _stringBuilder.ToString();

                Vector2 headerSize = boldFont.MeasureString(header);
                Vector2 prefixSize = regularFont.MeasureString(":  ");
                Vector2 inputSize = regularFont.MeasureString(row.Input);

                int x = (int)MathF.Floor(row.Bounds.X + headerSize.X + prefixSize.X) - ValueHighlightPadding;
                int y = row.Bounds.Y;
                int height = row.Bounds.Height > 0 ? row.Bounds.Height : (int)MathF.Ceiling(_lineHeightCache);
                int width = (int)MathF.Ceiling(inputSize.X) + (ValueHighlightPadding * 2);

                row.KeyValueBounds = new Rectangle(x, y, width, height);
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

        public static bool IsRebindOverlayOpen() => _rebindOverlayVisible;

        public static void UpdateRebindOverlay(GameTime gameTime, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            if (!_rebindOverlayVisible)
            {
                return;
            }

            EnsureRebindLayout();

            _rebindConfirmHovered = UIButtonRenderer.IsHovered(_rebindConfirmButtonBounds, mouseState.Position);
            _rebindUnbindHovered = UIButtonRenderer.IsHovered(_rebindUnbindButtonBounds, mouseState.Position);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool pointerOnConfirm = _rebindConfirmHovered;
            bool pointerOnUnbind = _rebindUnbindHovered;

            if (!(pointerOnConfirm && leftReleased) && !(pointerOnUnbind && leftReleased))
            {
                if (_suppressNextCapture)
                {
                    if (AnyReleaseDetected(keyboardState, previousKeyboardState, mouseState, previousMouseState))
                    {
                        _suppressNextCapture = false;
                    }
                }
                else if (TryCaptureBinding(keyboardState, previousKeyboardState, mouseState, previousMouseState, out string inputKey, out string displayLabel))
                {
                    _rebindPendingCanonical = inputKey;
                    _rebindPendingDisplay = displayLabel;
                    _rebindCaptured = true;
                    _rebindPendingUnbind = false;
                    EvaluateBindingConflicts(inputKey);
                }
            }

            if (leftReleased && pointerOnUnbind)
            {
                SetPendingUnbind();
            }
            else if (leftReleased && pointerOnConfirm)
            {
                ApplyRebindSelection();
            }
        }

        public static void DrawRebindOverlay(SpriteBatch spriteBatch)
        {
            if (!_rebindOverlayVisible || spriteBatch == null)
            {
                return;
            }

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont bodyFont = UIStyle.FontTech;
            UIStyle.UIFont buttonFont = UIStyle.FontBody;
            if (!headerFont.IsAvailable || !bodyFont.IsAvailable || !buttonFont.IsAvailable)
            {
                return;
            }

            Rectangle viewport = GetViewportBounds();
            if (viewport == Rectangle.Empty)
            {
                return;
            }

            EnsurePixelTexture();
            EnsureRebindLayout();
            if (_rebindModalBounds == Rectangle.Empty || _rebindConfirmButtonBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, viewport, RebindScrimColor);
            FillRect(spriteBatch, _rebindModalBounds, UIStyle.PanelBackground);
            DrawRectOutline(spriteBatch, _rebindModalBounds, UIStyle.PanelBorder, UIStyle.PanelBorderThickness);

            string title = string.IsNullOrWhiteSpace(_rebindAction) ? "Rebind keybind" : $"Rebind {_rebindAction}";
            Vector2 titleSize = headerFont.MeasureString(title);
            Vector2 titlePosition = new(_rebindModalBounds.X + (_rebindModalBounds.Width - titleSize.X) / 2f, _rebindModalBounds.Y + 12);
            headerFont.DrawString(spriteBatch, title, titlePosition, UIStyle.TextColor);

            float lineHeight = MathF.Max(20f, bodyFont.LineHeight);
            int textX = _rebindModalBounds.X + 20;
            float textY = _rebindModalBounds.Y + titleSize.Y + 24f;

            string instruction = "Press and release a new key or mouse button to capture.";
            bodyFont.DrawString(spriteBatch, instruction, new Vector2(textX, textY), UIStyle.TextColor);
            textY += lineHeight + 6f;

            string currentValue = string.IsNullOrWhiteSpace(_rebindCurrentInput) ? "Unbound" : _rebindCurrentInput;
            string currentLabel = $"Current: {currentValue}";
            bodyFont.DrawString(spriteBatch, currentLabel, new Vector2(textX, textY), UIStyle.MutedTextColor);
            textY += lineHeight + 4f;

            string pendingValue = string.IsNullOrWhiteSpace(_rebindPendingDisplay)
                ? (string.IsNullOrWhiteSpace(_rebindCurrentInput) ? "Unbound" : _rebindCurrentInput)
                : _rebindPendingDisplay;
            string pendingLabel = _rebindCaptured ? "New binding" : "Pending";
            string pendingText = $"{pendingLabel}: {pendingValue}";
            Color pendingColor = _rebindCaptured ? UIStyle.AccentColor : UIStyle.TextColor;
            bodyFont.DrawString(spriteBatch, pendingText, new Vector2(textX, textY), pendingColor);
            textY += lineHeight + 8f;

            string footer = "Confirm to save and close.";
            bodyFont.DrawString(spriteBatch, footer, new Vector2(textX, textY), UIStyle.MutedTextColor);

            if (!string.IsNullOrWhiteSpace(_rebindConflictWarning))
            {
                textY += lineHeight + 4f;
                bodyFont.DrawString(spriteBatch, _rebindConflictWarning, new Vector2(textX, textY), WarningColor);
            }

            UIButtonRenderer.Draw(spriteBatch, _rebindUnbindButtonBounds, "Unbind", UIButtonRenderer.ButtonStyle.Grey, _rebindUnbindHovered);
            UIButtonRenderer.Draw(spriteBatch, _rebindConfirmButtonBounds, "Confirm", UIButtonRenderer.ButtonStyle.Blue, _rebindConfirmHovered);
        }

        private static void BeginRebindFlow(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            int index = GetRowIndex(settingKey);
            if (index < 0)
            {
                return;
            }

            KeybindDisplayRow row = _keybindCache[index];
            if (row.ToggleLocked)
            {
                return;
            }

            _dragState.Reset();
            _pressedRowKey = null;

            _rebindOverlayVisible = true;
            _rebindAction = row.Action;
            string currentBindingDisplay = string.IsNullOrWhiteSpace(row.Input) ? string.Empty : row.Input;
            string currentCanonical = ControlKeyData.GetControl(row.Action)?.InputKey;
            if (string.IsNullOrWhiteSpace(currentCanonical))
            {
                currentCanonical = currentBindingDisplay;
            }
            _rebindCurrentCanonical = currentCanonical ?? string.Empty;
            _rebindPendingCanonical = _rebindCurrentCanonical;
            _rebindCurrentInput = string.IsNullOrWhiteSpace(currentBindingDisplay) ? "Unbound" : currentBindingDisplay;
            _rebindPendingDisplay = _rebindCurrentInput;
            _rebindCaptured = false;
            _rebindPendingUnbind = false;
            _suppressNextCapture = true;
            _rebindModalBounds = Rectangle.Empty;
            _rebindConfirmButtonBounds = Rectangle.Empty;
            _rebindUnbindButtonBounds = Rectangle.Empty;
            _rebindConfirmHovered = false;
            _rebindUnbindHovered = false;
            EvaluateBindingConflicts(_rebindCurrentCanonical);
        }

        private static void ApplyRebindSelection()
        {
            if (!_rebindOverlayVisible || string.IsNullOrWhiteSpace(_rebindAction))
            {
                ResetRebindOverlay();
                return;
            }

            bool hasPendingBinding = !string.IsNullOrWhiteSpace(_rebindPendingCanonical);
            if (_rebindPendingUnbind)
            {
                TryApplyUnbind();
                return;
            }

            if (!hasPendingBinding)
            {
                ResetRebindOverlay();
                return;
            }

            string finalBinding = _rebindPendingCanonical?.Trim();
            if (string.IsNullOrWhiteSpace(finalBinding))
            {
                TryApplyUnbind();
                return;
            }

            try
            {
                ControlKeyData.SetInputKey(_rebindAction, finalBinding);
                if (!InputManager.TryUpdateBindingInputKey(_rebindAction, finalBinding, out string displayLabel))
                {
                    DebugLogger.PrintWarning($"Failed to apply new binding for '{_rebindAction}'.");
                    displayLabel = InputManager.GetBindingDisplayLabel(_rebindAction);
                    if (string.IsNullOrWhiteSpace(displayLabel))
                    {
                        displayLabel = finalBinding;
                    }
                }

                int index = GetRowIndex(_rebindAction);
                if (index >= 0)
                {
                    var row = _keybindCache[index];
                    row.Input = string.IsNullOrWhiteSpace(displayLabel) ? finalBinding : displayLabel;
                    _keybindCache[index] = row;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to persist keybind '{_rebindAction}': {ex.Message}");
            }
            finally
            {
                ResetRebindOverlay();
            }
        }

        private static void TryApplyUnbind()
        {
            try
            {
                if (!InputManager.TryUnbind(_rebindAction))
                {
                    DebugLogger.PrintWarning($"Failed to unbind '{_rebindAction}'.");
                }

                int index = GetRowIndex(_rebindAction);
                if (index >= 0)
                {
                    var row = _keybindCache[index];
                    row.Input = "Unbound";
                    _keybindCache[index] = row;
                }

                _rebindCurrentCanonical = string.Empty;
                _rebindPendingCanonical = string.Empty;
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to unbind keybind '{_rebindAction}': {ex.Message}");
            }
            finally
            {
                ResetRebindOverlay();
            }
        }

        private static void ResetRebindOverlay()
        {
            _rebindOverlayVisible = false;
            _rebindAction = null;
            _rebindCurrentInput = null;
            _rebindCurrentCanonical = null;
            _rebindPendingCanonical = null;
            _rebindPendingDisplay = null;
            _rebindCaptured = false;
            _suppressNextCapture = false;
            _rebindModalBounds = Rectangle.Empty;
            _rebindConfirmButtonBounds = Rectangle.Empty;
            _rebindUnbindButtonBounds = Rectangle.Empty;
            _rebindConflictWarning = null;
            _rebindConfirmHovered = false;
            _rebindUnbindHovered = false;
            _rebindPendingUnbind = false;
        }

        private static void EnsureRebindLayout()
        {
            Rectangle viewport = GetViewportBounds();
            if (viewport == Rectangle.Empty)
            {
                _rebindModalBounds = Rectangle.Empty;
                _rebindConfirmButtonBounds = Rectangle.Empty;
                _rebindUnbindButtonBounds = Rectangle.Empty;
                return;
            }

            int width = Math.Clamp((int)(viewport.Width * 0.46f), 360, 640);
            int height = Math.Clamp((int)(viewport.Height * 0.32f), 220, 360);
            int modalX = viewport.X + (viewport.Width - width) / 2;
            int modalY = viewport.Y + (viewport.Height - height) / 2;
            _rebindModalBounds = new Rectangle(modalX, modalY, width, height);

            const int padding = 20;
            const int buttonHeight = 34;
            int buttonWidth = _rebindModalBounds.Width - (padding * 2);
            _rebindUnbindButtonBounds = new Rectangle(_rebindModalBounds.X + padding, _rebindModalBounds.Bottom - padding - (buttonHeight * 2) - 8, buttonWidth, buttonHeight);
            _rebindConfirmButtonBounds = new Rectangle(_rebindModalBounds.X + padding, _rebindModalBounds.Bottom - padding - buttonHeight, buttonWidth, buttonHeight);
        }

        private static Rectangle GetViewportBounds()
        {
            if (Core.Instance?.GraphicsDevice == null)
            {
                return Rectangle.Empty;
            }

            return Core.Instance.GraphicsDevice.Viewport.Bounds;
        }

        private static bool TryCaptureBinding(KeyboardState keyboardState, KeyboardState previousKeyboardState, MouseState mouseState, MouseState previousMouseState, out string inputKey, out string displayLabel)
        {
            inputKey = null;
            displayLabel = null;

            Keys? releasedKey = GetReleasedKey(keyboardState, previousKeyboardState);
            string releasedMouse = GetReleasedMouseButton(mouseState, previousMouseState);

            if (!releasedKey.HasValue && string.IsNullOrWhiteSpace(releasedMouse))
            {
                return false;
            }

            List<string> tokens = new();
            AppendModifierToken(keyboardState, Keys.LeftControl, Keys.RightControl, "Ctrl", tokens);
            AppendModifierToken(keyboardState, Keys.LeftShift, Keys.RightShift, "Shift", tokens);
            AppendModifierToken(keyboardState, Keys.LeftAlt, Keys.RightAlt, "Alt", tokens);

            if (releasedKey.HasValue)
            {
                tokens.Add(NormalizeKeyToken(releasedKey.Value));
            }
            else
            {
                tokens.Add(releasedMouse);
            }

            inputKey = string.Join(" + ", tokens).Trim();
            displayLabel = BuildDisplayLabel(tokens);
            return !string.IsNullOrWhiteSpace(inputKey);
        }

        private static bool AnyReleaseDetected(KeyboardState keyboardState, KeyboardState previousKeyboardState, MouseState mouseState, MouseState previousMouseState)
        {
            return GetReleasedKey(keyboardState, previousKeyboardState).HasValue ||
                !string.IsNullOrWhiteSpace(GetReleasedMouseButton(mouseState, previousMouseState));
        }

        private static Keys? GetReleasedKey(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            Keys[] previouslyPressed = previousKeyboardState.GetPressedKeys();
            if (previouslyPressed == null || previouslyPressed.Length == 0)
            {
                return null;
            }

            foreach (Keys key in previouslyPressed)
            {
                if (key == Keys.None)
                {
                    continue;
                }

                if (!keyboardState.IsKeyDown(key))
                {
                    return key;
                }
            }

            return null;
        }

        private static string HitTestKeyValue(Point position)
        {
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (row.KeyValueBounds != Rectangle.Empty && row.KeyValueBounds.Contains(position))
                {
                    return row.Action;
                }
            }

            return null;
        }

        private static string GetReleasedMouseButton(MouseState mouseState, MouseState previousMouseState)
        {
            if (mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed)
            {
                return "LeftClick";
            }

            if (mouseState.RightButton == ButtonState.Released && previousMouseState.RightButton == ButtonState.Pressed)
            {
                return "RightClick";
            }

            return null;
        }

        private static void AppendModifierToken(KeyboardState keyboardState, Keys primary, Keys secondary, string tokenLabel, List<string> tokens)
        {
            if (tokens == null || string.IsNullOrWhiteSpace(tokenLabel))
            {
                return;
            }

            if ((primary != Keys.None && keyboardState.IsKeyDown(primary)) || (secondary != Keys.None && keyboardState.IsKeyDown(secondary)))
            {
                if (!tokens.Contains(tokenLabel, StringComparer.OrdinalIgnoreCase))
                {
                    tokens.Add(tokenLabel);
                }
            }
        }

        private static string NormalizeKeyToken(Keys key)
        {
            if (key == Keys.LeftControl || key == Keys.RightControl)
            {
                return "Ctrl";
            }

            if (key == Keys.LeftShift || key == Keys.RightShift)
            {
                return "Shift";
            }

            if (key == Keys.LeftAlt || key == Keys.RightAlt)
            {
                return "Alt";
            }

            string token = key.ToString();
            if (token.Length <= 1)
            {
                return token.ToUpperInvariant();
            }

            return char.ToUpperInvariant(token[0]) + token[1..];
        }

        private static string BuildDisplayLabel(IReadOnlyList<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return "Unbound";
            }

            _stringBuilder.Clear();
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (string.Equals(token, "LeftClick", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Left Click";
                }
                else if (string.Equals(token, "RightClick", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Right Click";
                }

                _stringBuilder.Append(token);
                if (i < tokens.Count - 1)
                {
                    _stringBuilder.Append(" + ");
                }
            }

            return _stringBuilder.ToString();
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0 || spriteBatch == null)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(_pixelTexture, top, color);
            spriteBatch.Draw(_pixelTexture, bottom, color);
            spriteBatch.Draw(_pixelTexture, left, color);
            spriteBatch.Draw(_pixelTexture, right, color);
        }

        private static void EvaluateBindingConflicts(string inputKey)
        {
            _rebindConflictWarning = null;

            if (string.IsNullOrWhiteSpace(inputKey) || string.IsNullOrWhiteSpace(_rebindAction))
            {
                return;
            }

            string canonical = inputKey.Trim();
            if (string.IsNullOrWhiteSpace(canonical))
            {
                return;
            }

            IReadOnlyList<string> conflicts = InputManager.GetBindingsForInputKey(canonical, _rebindAction);
            if (conflicts.Count > 0)
            {
                _rebindConflictWarning = "Warning: also bound to " + string.Join(", ", conflicts);
            }
        }

        private static void SetPendingUnbind()
        {
            _rebindPendingDisplay = "Unbound";
            _rebindPendingCanonical = string.Empty;
            _rebindCaptured = true;
            _suppressNextCapture = true;
            _rebindConflictWarning = null;
            _rebindPendingUnbind = true;
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
            public Rectangle KeyValueBounds;
            public bool IsDragging;
            public bool TriggerAutoFire;
            public bool ToggleLocked;
            public bool IsSwitchType => ControlsBlock.IsSwitchType(InputType);
            public bool IsPersistentSwitch => ControlsBlock.IsPersistentSwitch(InputType);
            public bool IsToggleCandidate => !IsPersistentSwitch && InputType != InputType.Trigger && !ToggleLocked;
        }
    }
}
