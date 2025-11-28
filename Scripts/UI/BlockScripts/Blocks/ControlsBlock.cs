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
        private static Texture2D _pixelTexture;
        private static string _hoveredRowKey;
        private static bool _isDraggingRow;
        private static string _draggingRowKey;
        private static KeybindDisplayRow _draggingRowSnapshot;
        private static bool _hasDraggingSnapshot;
        private static float _dragOffsetY;
        private static float _draggedRowY;
        private static int _pendingDropIndex;
        private static string _pendingDropTargetKey;
        private static Rectangle _dropIndicatorBounds;
        private static float _lineHeightCache;
        private static readonly BlockScrollPanel _scrollPanel = new();

        private static readonly Color HoverRowColor = new(38, 38, 38, 180);
        private static readonly Color DraggingRowBackground = new(24, 24, 24, 220);
        private static readonly Color DropIndicatorColor = new(110, 142, 255, 90);

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            if (!TryGetRowFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            EnsureKeybindCache();
            _lineHeightCache = CalculateLineHeight(boldFont, regularFont);
            float contentHeight = _keybindCache.Count * _lineHeightCache;
            _scrollPanel.Update(contentBounds, contentHeight, mouseState, previousMouseState);
            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            UpdateRowBounds(listBounds);

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool pointerInsideList = listBounds.Contains(mouseState.Position);

            _hoveredRowKey = pointerInsideList ? HitTestRow(mouseState.Position) : null;

            if (_isDraggingRow)
            {
                UpdateRowDrag(listBounds, mouseState);
                if (leftClickReleased)
                {
                    CompleteRowDrag();
                }
            }
            else if (pointerInsideList && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
            {
                StartRowDrag(mouseState);
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            if (!TryGetRowFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            EnsureKeybindCache();
            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = CalculateLineHeight(boldFont, regularFont);
            }
            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            EnsurePixelTexture();

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

                bool isDraggingRow = _isDraggingRow && string.Equals(row.Action, _draggingRowKey, StringComparison.OrdinalIgnoreCase);
                if (!isDraggingRow)
                {
                    DrawRowBackground(spriteBatch, row, rowBounds);
                    DrawRowContents(spriteBatch, row, rowBounds, lineHeight, boldFont, regularFont, listBounds);
                }
            }

            if (_isDraggingRow && _hasDraggingSnapshot)
            {
                if (_dropIndicatorBounds != Rectangle.Empty)
                {
                    FillRect(spriteBatch, _dropIndicatorBounds, DropIndicatorColor);
                }

                DrawDraggingRow(spriteBatch, listBounds, lineHeight, boldFont, regularFont);
            }

            _scrollPanel.Draw(spriteBatch);
        }

        private static void EnsureKeybindCache()
        {
            if (_keybindCacheLoaded)
            {
                return;
            }

            ResetDragState();

            try
            {
                ControlKeyMigrations.EnsureApplied();
                _keybindCache.Clear();

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

                    if (!Enum.TryParse(typeLabel, true, out InputType parsedType))
                    {
                        parsedType = InputType.Hold;
                    }

                    string actionLabel = row.TryGetValue("SettingKey", out object action) ? action?.ToString() ?? "Action" : "Action";
                    string inputLabel = row.TryGetValue("InputKey", out object key) ? key?.ToString() ?? "Key" : "Key";
                    int orderValue = row.TryGetValue("ControlOrder", out object orderObj) ? Convert.ToInt32(orderObj) : fallbackOrder;

                    if (ControlKeyRules.RequiresSwitchSemantics(actionLabel))
                    {
                        parsedType = InputType.Switch;
                    }

                    _keybindCache.Add(new KeybindDisplayRow
                    {
                        Action = actionLabel,
                        Input = inputLabel,
                        TypeLabel = parsedType.ToString(),
                        InputType = parsedType,
                        RenderOrder = orderValue > 0 ? orderValue : fallbackOrder,
                        Bounds = Rectangle.Empty,
                        IsDragging = false
                    });

                    fallbackOrder++;
                }

                NormalizeCacheOrder();
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

        private static void StartRowDrag(MouseState mouseState)
        {
            if (string.IsNullOrEmpty(_hoveredRowKey))
            {
                return;
            }

            int index = GetRowIndex(_hoveredRowKey);
            if (index < 0)
            {
                return;
            }

            KeybindDisplayRow row = _keybindCache[index];
            if (row.Bounds == Rectangle.Empty)
            {
                return;
            }

            row.IsDragging = true;
            _keybindCache[index] = row;

            _isDraggingRow = true;
            _hasDraggingSnapshot = true;
            _draggingRowKey = row.Action;
            _draggingRowSnapshot = row;
            _dragOffsetY = mouseState.Y - row.Bounds.Y;
            _draggedRowY = row.Bounds.Y;
            _pendingDropIndex = index;
            _pendingDropTargetKey = row.Action;
            _dropIndicatorBounds = Rectangle.Empty;
        }

        private static void UpdateRowDrag(Rectangle contentBounds, MouseState mouseState)
        {
            if (!_hasDraggingSnapshot)
            {
                return;
            }

            float minTop = contentBounds.Y - MathF.Min(_lineHeightCache * 0.65f, _lineHeightCache);
            float maxTop = Math.Max(contentBounds.Y, contentBounds.Bottom - _lineHeightCache);
            _draggedRowY = MathHelper.Clamp(mouseState.Y - _dragOffsetY, minTop, maxTop);

            float dragCenterY = _draggedRowY + (_lineHeightCache / 2f);
            int dropIndex = 0;
            string dropTarget = null;
            Rectangle indicator = Rectangle.Empty;
            Rectangle lastBounds = Rectangle.Empty;

            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (string.Equals(row.Action, _draggingRowKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Rectangle bounds = row.Bounds;
                if (bounds == Rectangle.Empty)
                {
                    continue;
                }

                float midpoint = bounds.Y + (bounds.Height / 2f);
                if (dragCenterY < midpoint)
                {
                    int indicatorY = Math.Max(contentBounds.Y - 2, bounds.Y - 2);
                    indicator = new Rectangle(contentBounds.X, indicatorY, contentBounds.Width, 4);
                    dropTarget = row.Action;
                    break;
                }

                lastBounds = bounds;
                dropIndex++;
            }

            if (indicator == Rectangle.Empty)
            {
                int indicatorY = lastBounds == Rectangle.Empty ? contentBounds.Y - 2 : lastBounds.Bottom - 2;
                indicator = new Rectangle(contentBounds.X, Math.Max(contentBounds.Y - 2, indicatorY), contentBounds.Width, 4);
            }

            _pendingDropIndex = dropIndex;
            _pendingDropTargetKey = dropTarget;
            _dropIndicatorBounds = indicator;
        }

        private static void CompleteRowDrag()
        {
            if (!_hasDraggingSnapshot)
            {
                ResetDragState();
                return;
            }

            int currentIndex = GetRowIndex(_draggingRowKey);
            if (currentIndex < 0)
            {
                ResetDragState();
                return;
            }

            KeybindDisplayRow row = _keybindCache[currentIndex];
            row.IsDragging = false;
            _keybindCache[currentIndex] = row;

            _keybindCache.RemoveAt(currentIndex);
            int insertIndex = Math.Clamp(_pendingDropIndex, 0, _keybindCache.Count);
            _keybindCache.Insert(insertIndex, row);

            bool orderChanged = insertIndex != currentIndex;
            NormalizeCacheOrder();

            if (orderChanged)
            {
                PersistRowOrder();
            }

            ResetDragState();
        }

        private static void PersistRowOrder()
        {
            List<(string SettingKey, int Order)> updates = new(_keybindCache.Count);
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (string.IsNullOrWhiteSpace(row.Action) || row.RenderOrder <= 0)
                {
                    continue;
                }

                updates.Add((row.Action, row.RenderOrder));
            }

            ControlKeyData.UpdateRenderOrders(updates);
        }

        private static void DrawRowBackground(SpriteBatch spriteBatch, KeybindDisplayRow row, Rectangle bounds)
        {
            if (bounds == Rectangle.Empty || _pixelTexture == null)
            {
                return;
            }

            if (!_isDraggingRow && string.Equals(_hoveredRowKey, row.Action, StringComparison.OrdinalIgnoreCase))
            {
                FillRect(spriteBatch, bounds, HoverRowColor);
            }
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            Rectangle dragBounds = new(contentBounds.X, (int)MathF.Round(_draggedRowY), contentBounds.Width, (int)MathF.Ceiling(lineHeight));
            FillRect(spriteBatch, dragBounds, DraggingRowBackground);

            KeybindDisplayRow row = _draggingRowSnapshot;
            row.Bounds = dragBounds;
            DrawRowContents(spriteBatch, row, dragBounds, lineHeight, boldFont, regularFont, contentBounds);
        }

        private static void DrawRowContents(SpriteBatch spriteBatch, KeybindDisplayRow row, Rectangle rowBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont, Rectangle contentBounds)
        {
            Vector2 labelPosition = new(rowBounds.X, rowBounds.Y);

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

            if (row.IsSwitch)
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

        private static bool TryGetRowFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont)
        {
            boldFont = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            regularFont = UIStyle.FontTech;
            return boldFont.IsAvailable && regularFont.IsAvailable;
        }

        private static float CalculateLineHeight(UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            return Math.Max(boldFont.LineHeight, regularFont.LineHeight) + 2f;
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

        private static void ResetDragState()
        {
            _isDraggingRow = false;
            _hasDraggingSnapshot = false;
            _draggingRowKey = null;
            _pendingDropTargetKey = null;
            _dropIndicatorBounds = Rectangle.Empty;
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

        private struct KeybindDisplayRow
        {
            public string Action;
            public string Input;
            public string TypeLabel;
            public InputType InputType;
            public int RenderOrder;
            public Rectangle Bounds;
            public bool IsDragging;
            public bool IsSwitch => InputType == InputType.Switch;
        }
    }
}
