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
        public const string BlockTitle = "Specs";
        public const int MinWidth = 30;
        public const int MinHeight = 0;

        private static readonly string PlaceholderText = TextSpacingHelper.JoinWithWideSpacing("No", "specs", "available.");

        private static readonly List<SpecRow> _rows = new();
        private static readonly Dictionary<string, int> _customOrder = new(StringComparer.OrdinalIgnoreCase);
        private static bool _orderCacheHydrated;
        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly StringBuilder _stringBuilder = new();
        private static readonly BlockDragState<SpecRow> _dragState = new(row => row.Key, row => row.Bounds, (row, isDragging) =>
        {
            row.IsDragging = isDragging;
            return row;
        });
        private static Texture2D _pixelTexture;
        private static float _lineHeightCache;
        private static string _hoveredRowKey;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Specs);

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

            if (blockLocked && _dragState.IsDragging)
            {
                _dragState.Reset();
            }

            _hoveredRowKey = !blockLocked && pointerInsideList ? HitTestRow(mouseState.Position) : null;

            if (_dragState.IsDragging)
            {
                _dragState.UpdateDrag(_rows, listBounds, _lineHeightCache, mouseState);
                if (leftClickReleased)
                {
                    if (_dragState.TryCompleteDrag(_rows, out bool orderChanged))
                    {
                        NormalizeRowOrder();
                        if (orderChanged)
                        {
                            UpdateCustomOrderFromRows();
                        }
                    }
                }
            }
            else if (!blockLocked && pointerInsideList && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
            {
                _dragState.TryStartDrag(_rows, _hoveredRowKey, mouseState);
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

                bool draggingThisRow = _dragState.IsDragging && string.Equals(row.Key, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (!draggingThisRow)
                {
                    DrawRowBackground(spriteBatch, row, rowBounds);
                    DrawRowContents(spriteBatch, row, rowBounds, lineHeight, boldFont, regularFont, listBounds);
                }
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                if (_dragState.DropIndicatorBounds != Rectangle.Empty)
                {
                    FillRect(spriteBatch, _dragState.DropIndicatorBounds, ColorPalette.DropIndicator);
                }

                DrawDraggingRow(spriteBatch, listBounds, lineHeight, boldFont, regularFont);
            }

            _scrollPanel.Draw(spriteBatch);
        }

        private static void EnsureOrderCacheHydrated()
        {
            if (_orderCacheHydrated)
            {
                return;
            }

            Dictionary<string, int> storedOrders = BlockDataStore.LoadRowOrders(DockBlockKind.Specs);
            if (storedOrders.Count > 0)
            {
                _customOrder.Clear();
                foreach (var entry in storedOrders)
                {
                    _customOrder[entry.Key] = entry.Value;
                }
            }

            _orderCacheHydrated = true;
        }

        private static void RefreshRows()
        {
            EnsureOrderCacheHydrated();

            IReadOnlyList<SystemSpec> specs = SystemSpecsProvider.GetSpecs();
            HashSet<string> incoming = new(StringComparer.OrdinalIgnoreCase);
            bool orderChanged = false;
            int nextOrder = GetNextOrderSeed();

            foreach (SystemSpec spec in specs)
            {
                if (string.IsNullOrWhiteSpace(spec.Key))
                {
                    continue;
                }

                string storageKey = BlockDataStore.CanonicalizeRowKey(DockBlockKind.Specs, spec.Key);
                incoming.Add(storageKey);
                if (!_customOrder.ContainsKey(storageKey))
                {
                    _customOrder[storageKey] = nextOrder++;
                    orderChanged = true;
                }

                SpecRow row = FindRow(storageKey);
                if (row == null)
                {
                    row = new SpecRow(storageKey);
                    _rows.Add(row);
                }

                row.Label = string.IsNullOrWhiteSpace(spec.Label) ? storageKey : spec.Label;
                row.Value = spec.Value ?? string.Empty;
                row.IsBoolean = spec.IsBoolean;
                row.BoolValue = spec.BoolValue;
                row.RenderOrder = _customOrder[storageKey];
            }

            if (!_dragState.IsDragging)
            {
                for (int i = _rows.Count - 1; i >= 0; i--)
                {
                    if (!incoming.Contains(_rows[i].Key))
                    {
                        _rows.RemoveAt(i);
                        orderChanged = true;
                    }
                }
            }

            foreach (string key in new List<string>(_customOrder.Keys))
            {
                if (!incoming.Contains(key))
                {
                    _customOrder.Remove(key);
                    orderChanged = true;
                }
            }

            orderChanged |= NormalizeCustomOrder();
            ApplyOrdersToRows();

            if (orderChanged)
            {
                PersistRowOrders();
            }
        }

        private static bool NormalizeCustomOrder()
        {
            if (_customOrder.Count == 0)
            {
                return false;
            }

            List<KeyValuePair<string, int>> entries = new(_customOrder);
            entries.Sort((a, b) => a.Value.CompareTo(b.Value));
            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                int desiredOrder = i + 1;
                if (_customOrder[entries[i].Key] != desiredOrder)
                {
                    _customOrder[entries[i].Key] = desiredOrder;
                    changed = true;
                }
            }

            return changed;
        }

        private static void PersistRowOrders()
        {
            List<KeyValuePair<string, int>> ordered = new(_customOrder);
            ordered.Sort((a, b) => a.Value.CompareTo(b.Value));

            List<(string RowKey, int Order)> rows = new(ordered.Count);
            foreach (KeyValuePair<string, int> entry in ordered)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0)
                {
                    continue;
                }

                rows.Add((entry.Key, entry.Value));
            }

            BlockDataStore.SaveRowOrders(DockBlockKind.Specs, rows);
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
            bool changed = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                SpecRow row = _rows[i];
                _customOrder[row.Key] = row.RenderOrder;
                changed = true;
            }

            changed |= NormalizeCustomOrder();

            if (changed)
            {
                PersistRowOrders();
            }
        }

        private static void DrawRowBackground(SpriteBatch spriteBatch, SpecRow row, Rectangle bounds)
        {
            if (bounds == Rectangle.Empty || _pixelTexture == null)
            {
                return;
            }

            if (!_dragState.IsDragging && string.Equals(_hoveredRowKey, row.Key, StringComparison.OrdinalIgnoreCase))
            {
                FillRect(spriteBatch, bounds, ColorPalette.RowHover);
            }
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            Rectangle dragBounds = _dragState.GetDragBounds(contentBounds, lineHeight);
            if (dragBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);

            SpecRow row = _dragState.DraggingSnapshot;
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
            int max = 0;
            foreach (int value in _customOrder.Values)
            {
                if (value > max)
                {
                    max = value;
                }
            }

            return Math.Max(1, max + 1);
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
