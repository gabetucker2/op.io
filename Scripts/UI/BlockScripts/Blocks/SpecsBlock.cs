using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class SpecsBlock
    {
        public const string PanelTitle = "Specs";

        private const string PlaceholderWordSeparator = "    ";
        private static readonly string PlaceholderText = string.Join(PlaceholderWordSeparator, new[] { "No", "specs", "available." });

        private static readonly List<SpecRow> _rows = new();
        private static readonly Dictionary<string, int> _customOrder = new(StringComparer.OrdinalIgnoreCase);
        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly StringBuilder _stringBuilder = new();
        private static Texture2D _pixelTexture;
        private static float _lineHeightCache;
        private static string _hoveredRowKey;
        private static bool _isDraggingRow;
        private static string _draggingRowKey;
        private static SpecRow _draggingRowSnapshot;
        private static bool _hasDraggingSnapshot;
        private static float _dragOffsetY;
        private static float _draggedRowY;
        private static int _pendingDropIndex;
        private static Rectangle _dropIndicatorBounds;

        private static readonly Color HoverRowColor = new(38, 38, 38, 180);
        private static readonly Color DraggingRowBackground = new(24, 24, 24, 220);
        private static readonly Color DropIndicatorColor = new(110, 142, 255, 90);

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool panelLocked = BlockManager.IsPanelLocked(DockPanelKind.Specs);

            RefreshRows();

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _scrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }

            float contentHeight = _rows.Count * _lineHeightCache;
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

            if (panelLocked && _isDraggingRow)
            {
                ResetDragState();
            }

            _hoveredRowKey = !panelLocked && pointerInsideList ? HitTestRow(mouseState.Position) : null;

            if (_isDraggingRow)
            {
                UpdateRowDrag(listBounds, mouseState);
                if (leftClickReleased)
                {
                    CompleteRowDrag();
                }
            }
            else if (!panelLocked && pointerInsideList && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
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

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            if (_rows.Count == 0)
            {
                RefreshRows();
            }

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

            if (_rows.Count == 0)
            {
                regularFont.DrawString(spriteBatch, PlaceholderText, new Vector2(listBounds.X, listBounds.Y), UIStyle.MutedTextColor);
                _scrollPanel.Draw(spriteBatch);
                return;
            }

            float lineHeight = _lineHeightCache;
            foreach (SpecRow row in _rows)
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

                bool draggingThisRow = _isDraggingRow && string.Equals(row.Key, _draggingRowKey, StringComparison.OrdinalIgnoreCase);
                if (!draggingThisRow)
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

        private static void RefreshRows()
        {
            IReadOnlyList<SystemSpec> specs = SystemSpecsProvider.GetSpecs();
            HashSet<string> incoming = new(StringComparer.OrdinalIgnoreCase);
            int nextOrder = GetNextOrderSeed();

            foreach (SystemSpec spec in specs)
            {
                if (string.IsNullOrWhiteSpace(spec.Key))
                {
                    continue;
                }

                incoming.Add(spec.Key);
                if (!_customOrder.ContainsKey(spec.Key))
                {
                    _customOrder[spec.Key] = nextOrder++;
                }

                SpecRow row = FindRow(spec.Key);
                if (row == null)
                {
                    row = new SpecRow(spec.Key);
                    _rows.Add(row);
                }

                row.Label = string.IsNullOrWhiteSpace(spec.Label) ? spec.Key : spec.Label;
                row.Value = spec.Value ?? string.Empty;
                row.IsBoolean = spec.IsBoolean;
                row.BoolValue = spec.BoolValue;
                row.RenderOrder = _customOrder[spec.Key];
            }

            if (!_isDraggingRow)
            {
                for (int i = _rows.Count - 1; i >= 0; i--)
                {
                    if (!incoming.Contains(_rows[i].Key))
                    {
                        _rows.RemoveAt(i);
                    }
                }
            }

            NormalizeCustomOrder();
            ApplyOrdersToRows();
        }

        private static void NormalizeCustomOrder()
        {
            if (_customOrder.Count == 0)
            {
                return;
            }

            List<KeyValuePair<string, int>> entries = new(_customOrder);
            entries.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < entries.Count; i++)
            {
                _customOrder[entries[i].Key] = i + 1;
            }
        }

        private static void ApplyOrdersToRows()
        {
            foreach (SpecRow row in _rows)
            {
                if (_customOrder.TryGetValue(row.Key, out int order))
                {
                    row.RenderOrder = order;
                }
            }

            _rows.Sort((left, right) => left.RenderOrder.CompareTo(right.RenderOrder));
        }

        private static void UpdateRowBounds(Rectangle contentBounds)
        {
            if (_lineHeightCache <= 0f)
            {
                return;
            }

            int rowHeight = (int)MathF.Ceiling(_lineHeightCache);
            float y = contentBounds.Y - _scrollPanel.ScrollOffset;

            for (int i = 0; i < _rows.Count; i++)
            {
                SpecRow row = _rows[i];
                row.Bounds = new Rectangle(contentBounds.X, (int)MathF.Round(y), contentBounds.Width, rowHeight);
                _rows[i] = row;
                y += _lineHeightCache;
            }
        }

        private static string HitTestRow(Point position)
        {
            foreach (SpecRow row in _rows)
            {
                if (row.Bounds.Contains(position))
                {
                    return row.Key;
                }
            }

            return null;
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

            SpecRow row = _rows[index];
            if (row.Bounds == Rectangle.Empty)
            {
                return;
            }

            row.IsDragging = true;
            _rows[index] = row;

            _isDraggingRow = true;
            _hasDraggingSnapshot = true;
            _draggingRowKey = row.Key;
            _draggingRowSnapshot = row;
            _dragOffsetY = mouseState.Y - row.Bounds.Y;
            _draggedRowY = row.Bounds.Y;
            _pendingDropIndex = index;
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
            Rectangle indicator = Rectangle.Empty;
            Rectangle lastBounds = Rectangle.Empty;

            foreach (SpecRow row in _rows)
            {
                if (string.Equals(row.Key, _draggingRowKey, StringComparison.OrdinalIgnoreCase))
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

            SpecRow row = _rows[currentIndex];
            row.IsDragging = false;
            _rows[currentIndex] = row;

            _rows.RemoveAt(currentIndex);
            int insertIndex = Math.Clamp(_pendingDropIndex, 0, _rows.Count);
            _rows.Insert(insertIndex, row);

            bool orderChanged = insertIndex != currentIndex;
            NormalizeRowOrder();
            if (orderChanged)
            {
                UpdateCustomOrderFromRows();
            }

            ResetDragState();
        }

        private static void NormalizeRowOrder()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                SpecRow row = _rows[i];
                int expectedOrder = i + 1;
                if (row.RenderOrder != expectedOrder)
                {
                    row.RenderOrder = expectedOrder;
                    _rows[i] = row;
                }
            }
        }

        private static void UpdateCustomOrderFromRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                SpecRow row = _rows[i];
                _customOrder[row.Key] = row.RenderOrder;
            }
        }

        private static void DrawRowBackground(SpriteBatch spriteBatch, SpecRow row, Rectangle bounds)
        {
            if (bounds == Rectangle.Empty || _pixelTexture == null)
            {
                return;
            }

            if (!_isDraggingRow && string.Equals(_hoveredRowKey, row.Key, StringComparison.OrdinalIgnoreCase))
            {
                FillRect(spriteBatch, bounds, HoverRowColor);
            }
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            Rectangle dragBounds = new(contentBounds.X, (int)MathF.Round(_draggedRowY), contentBounds.Width, (int)MathF.Ceiling(lineHeight));
            FillRect(spriteBatch, dragBounds, DraggingRowBackground);

            SpecRow row = _draggingRowSnapshot;
            row.Bounds = dragBounds;
            DrawRowContents(spriteBatch, row, dragBounds, lineHeight, boldFont, regularFont, contentBounds);
        }

        private static void DrawRowContents(SpriteBatch spriteBatch, SpecRow row, Rectangle rowBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont, Rectangle contentBounds)
        {
            Vector2 labelPosition = new(rowBounds.X, rowBounds.Y);
            string header = row.Label ?? row.Key;
            Vector2 headerSize = boldFont.MeasureString(header);
            boldFont.DrawString(spriteBatch, header, labelPosition, UIStyle.TextColor);

            _stringBuilder.Clear();
            _stringBuilder.Append(":  ");
            _stringBuilder.Append(row.Value ?? string.Empty);
            string value = _stringBuilder.ToString();
            float valueX = labelPosition.X + headerSize.X;
            regularFont.DrawString(spriteBatch, value, new Vector2(valueX, rowBounds.Y), UIStyle.TextColor);

            if (row.IsBoolean)
            {
                BlockIndicatorRenderer.TryDrawBooleanIndicator(spriteBatch, contentBounds, lineHeight, rowBounds.Y, row.BoolValue);
            }
        }

        private static int GetRowIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return -1;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static SpecRow FindRow(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            foreach (SpecRow row in _rows)
            {
                if (string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }

            return null;
        }

        private static int GetNextOrderSeed()
        {
            return Math.Max(1, _customOrder.Count + 1);
        }

        private static void ResetDragState()
        {
            _isDraggingRow = false;
            _hasDraggingSnapshot = false;
            _draggingRowKey = null;
            _dropIndicatorBounds = Rectangle.Empty;
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

        private sealed class SpecRow
        {
            public SpecRow(string key)
            {
                Key = key ?? string.Empty;
            }

            public string Key { get; }
            public string Label { get; set; }
            public string Value { get; set; }
            public bool IsBoolean { get; set; }
            public bool BoolValue { get; set; }
            public int RenderOrder { get; set; }
            public Rectangle Bounds { get; set; }
            public bool IsDragging { get; set; }
        }
    }
}
