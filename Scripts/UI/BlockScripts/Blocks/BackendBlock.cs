using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class BackendBlock
    {
        public const string PanelTitle = "Backend";

        private const string PlaceholderWordSeparator = "    ";
        private static readonly string PlaceholderText = string.Join(PlaceholderWordSeparator, new[] { "No", "backend", "values", "tracked." });

        private static readonly StringBuilder _stringBuilder = new();
        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly List<BackendVariable> _rows = new();
        private static readonly Dictionary<string, int> _customOrder = new(StringComparer.OrdinalIgnoreCase);
        private static readonly BlockDragState<BackendVariable> _dragState = new(row => row.Name, row => row.Bounds, (row, isDragging) =>
        {
            row.IsDragging = isDragging;
            return row;
        });
        private static float _lineHeightCache;
        private static Texture2D _pixelTexture;
        private static string _hoveredRowKey;

        private static readonly Color HoverRowColor = new(38, 38, 38, 180);
        private static readonly Color DraggingRowBackground = new(24, 24, 24, 220);
        private static readonly Color DropIndicatorColor = new(110, 142, 255, 90);

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool panelLocked = BlockManager.IsPanelLocked(DockPanelKind.Backend);

            RefreshRows();
            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _lineHeightCache = 0f;
                _scrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }

            float contentHeight = Math.Max(0f, _rows.Count * _lineHeightCache);
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

            if (panelLocked && _dragState.IsDragging)
            {
                _dragState.Reset();
            }

            _hoveredRowKey = !panelLocked && pointerInsideList ? HitTestRow(mouseState.Position) : null;

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
            else if (!panelLocked && pointerInsideList && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
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

            if (_rows.Count == 0)
            {
                regularFont.DrawString(spriteBatch, PlaceholderText, new Vector2(listBounds.X, listBounds.Y), UIStyle.MutedTextColor);
                _scrollPanel.Draw(spriteBatch);
                return;
            }

            EnsurePixelTexture();

            float lineHeight = _lineHeightCache;
            float y = listBounds.Y - _scrollPanel.ScrollOffset;
            int rowHeight = (int)MathF.Ceiling(lineHeight);

            foreach (BackendVariable row in _rows)
            {
                Rectangle rowBounds = new(listBounds.X, (int)MathF.Round(y), listBounds.Width, rowHeight);
                y += lineHeight;

                if (rowBounds.Bottom <= listBounds.Y)
                {
                    continue;
                }

                if (rowBounds.Y >= listBounds.Bottom)
                {
                    break;
                }

                bool isDraggingRow = _dragState.IsDragging && string.Equals(row.Name, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (!isDraggingRow)
                {
                    DrawRowBackground(spriteBatch, rowBounds, row.Name);
                }

                Vector2 labelPosition = new(rowBounds.X, rowBounds.Y);
                string header = row.Name;
                Vector2 headerSize = boldFont.MeasureString(header);
                boldFont.DrawString(spriteBatch, header, labelPosition, UIStyle.TextColor);

                string valueText = FormatValue(row);
                float valueX = labelPosition.X + headerSize.X;

                _stringBuilder.Clear();
                _stringBuilder.Append(":  ");
                _stringBuilder.Append(valueText);

                regularFont.DrawString(spriteBatch, _stringBuilder.ToString(), new Vector2(valueX, rowBounds.Y), UIStyle.TextColor);

                if (row.IsBoolean && row.Value is bool boolValue)
                {
                    BlockIndicatorRenderer.TryDrawBooleanIndicator(spriteBatch, listBounds, lineHeight, rowBounds.Y, boolValue);
                }
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                if (_dragState.DropIndicatorBounds != Rectangle.Empty)
                {
                    FillRect(spriteBatch, _dragState.DropIndicatorBounds, DropIndicatorColor);
                }

                DrawDraggingRow(spriteBatch, listBounds, lineHeight, boldFont, regularFont);
            }

            _scrollPanel.Draw(spriteBatch);
        }

        private static void RefreshRows()
        {
            IReadOnlyList<GameTracker.GameTrackerVariable> variables = GameTracker.GetTrackedVariables();
            HashSet<string> incoming = new(StringComparer.OrdinalIgnoreCase);
            int nextOrder = GetNextOrderSeed();

            foreach (var variable in variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    continue;
                }

                incoming.Add(variable.Name);
                if (!_customOrder.ContainsKey(variable.Name))
                {
                    _customOrder[variable.Name] = nextOrder++;
                }

                BackendVariable row = FindRow(variable.Name);
                bool wasDragging = row?.IsDragging ?? false;
                Rectangle previousBounds = row?.Bounds ?? Rectangle.Empty;

                if (row == null)
                {
                    row = new BackendVariable(variable.Name, variable.Value, variable.IsBoolean);
                    _rows.Add(row);
                }

                row.Value = variable.Value;
                row.IsBoolean = variable.IsBoolean;
                row.RenderOrder = _customOrder[variable.Name];
                row.IsDragging = wasDragging;
                row.Bounds = previousBounds;
            }

            if (!_dragState.IsDragging)
            {
                for (int i = _rows.Count - 1; i >= 0; i--)
                {
                    if (!incoming.Contains(_rows[i].Name))
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
            foreach (BackendVariable row in _rows)
            {
                if (_customOrder.TryGetValue(row.Name, out int order))
                {
                    row.RenderOrder = order;
                }
            }

            _rows.Sort((a, b) => a.RenderOrder.CompareTo(b.RenderOrder));
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
                BackendVariable row = _rows[i];
                row.Bounds = new Rectangle(contentBounds.X, (int)MathF.Round(y), contentBounds.Width, rowHeight);
                _rows[i] = row;
                y += _lineHeightCache;
            }
        }

        private static string HitTestRow(Point position)
        {
            foreach (BackendVariable row in _rows)
            {
                if (row.Bounds.Contains(position))
                {
                    return row.Name;
                }
            }

            return null;
        }

        private static void NormalizeRowOrder()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                BackendVariable row = _rows[i];
                int expectedOrder = i + 1;
                if (row.RenderOrder != expectedOrder)
                {
                    row.RenderOrder = expectedOrder;
                }
            }
        }

        private static void UpdateCustomOrderFromRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                BackendVariable row = _rows[i];
                _customOrder[row.Name] = row.RenderOrder;
            }
        }

        private static void DrawRowBackground(SpriteBatch spriteBatch, Rectangle bounds, string rowKey)
        {
            if (bounds == Rectangle.Empty || _pixelTexture == null)
            {
                return;
            }

            if (!_dragState.IsDragging && string.Equals(_hoveredRowKey, rowKey, StringComparison.OrdinalIgnoreCase))
            {
                FillRect(spriteBatch, bounds, HoverRowColor);
            }
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds, float lineHeight, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            Rectangle dragBounds = _dragState.GetDragBounds(contentBounds, lineHeight);
            if (dragBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dragBounds, DraggingRowBackground);

            BackendVariable row = _dragState.DraggingSnapshot;
            row.Bounds = dragBounds;

            Vector2 labelPosition = new(row.Bounds.X, row.Bounds.Y);
            string header = row.Name;
            Vector2 headerSize = boldFont.MeasureString(header);
            boldFont.DrawString(spriteBatch, header, labelPosition, UIStyle.TextColor);

            string valueText = FormatValue(row);
            float valueX = labelPosition.X + headerSize.X;

            _stringBuilder.Clear();
            _stringBuilder.Append(":  ");
            _stringBuilder.Append(valueText);

            regularFont.DrawString(spriteBatch, _stringBuilder.ToString(), new Vector2(valueX, row.Bounds.Y), UIStyle.TextColor);

            if (row.IsBoolean && row.Value is bool boolValue)
            {
                BlockIndicatorRenderer.TryDrawBooleanIndicator(spriteBatch, contentBounds, lineHeight, row.Bounds.Y, boolValue);
            }
        }

        private static int GetRowIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return -1;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static BackendVariable FindRow(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (BackendVariable row in _rows)
            {
                if (string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase))
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

        private static string FormatValue(BackendVariable variable)
        {
            if (variable.Value == null)
            {
                return "null";
            }

            if (variable.IsBoolean && variable.Value is bool boolValue)
            {
                return boolValue ? "1" : "0";
            }

            return variable.Value.ToString();
        }

        private sealed class BackendVariable
        {
            public BackendVariable(string name, object value, bool isBoolean)
            {
                Name = name ?? string.Empty;
                Value = value;
                IsBoolean = isBoolean;
            }

            public string Name { get; }
            public object Value { get; set; }
            public bool IsBoolean { get; set; }
            public int RenderOrder { get; set; }
            public Rectangle Bounds { get; set; }
            public bool IsDragging { get; set; }
        }
    }
}
