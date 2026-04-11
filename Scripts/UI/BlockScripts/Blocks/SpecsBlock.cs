using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.FunCol;
using op.io.UI.FunCol.Features;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class SpecsBlock
    {
        public const string BlockTitle = "Specs";

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
        private static string _tooltipRowKey;

        public static string GetHoveredRowKey() => _tooltipRowKey;

        public static string GetHoveredRowLabel()
        {
            if (string.IsNullOrEmpty(_tooltipRowKey)) return null;
            SpecRow row = FindRow(_tooltipRowKey);
            return row?.Label ?? _tooltipRowKey;
        }

        // ── Per-row FunColInterfaces (Label | Value+bool) ──
        private static readonly Dictionary<string, FunColInterface> _rowFunCols =
            new(StringComparer.OrdinalIgnoreCase);
        private static FunColInterface _dragGhostFunCol;
        private static FunColInterface _headerFunCol;
        private const int HeaderRowHeight = 16;
        private static bool _headerVisibleLoaded;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Specs);

            var specsHfc = GetOrEnsureHeaderFunCol();
            if (!_headerVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Specs);
                if (rowData.TryGetValue("FunColHeaderVisible", out string stored))
                    specsHfc.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                _headerVisibleLoaded = true;
            }

            int headerH = specsHfc.HeaderVisible ? HeaderRowHeight : 0;
            var listArea = new Rectangle(contentBounds.X, contentBounds.Y + headerH, contentBounds.Width, Math.Max(0, contentBounds.Height - headerH));

            RefreshRows();

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _scrollPanel.Update(listArea, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }

            float contentHeight = _rows.Count * _lineHeightCache;
            _scrollPanel.Update(listArea, contentHeight, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            UpdateRowBounds(listBounds);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool pointerInsideList = listBounds.Contains(mouseState.Position);

            if (blockLocked && _dragState.IsDragging)
            {
                _dragState.Reset();
            }

            // Update per-row FunColInterfaces
            bool suppressHover = _dragState.IsDragging || blockLocked;
            foreach (SpecRow row in _rows)
            {
                if (row.Bounds != Rectangle.Empty)
                    GetOrCreateRowFunCol(row.Key).Update(row.Bounds, mouseState, dt, suppressHover);
            }

            string hitRow = pointerInsideList ? HitTestRow(mouseState.Position) : null;
            _hoveredRowKey = !blockLocked ? hitRow : null;
            _tooltipRowKey = hitRow;

            // Determine hovered column for the hovered row
            int hoveredCol = -1;
            if (!string.IsNullOrEmpty(_hoveredRowKey) &&
                _rowFunCols.TryGetValue(_hoveredRowKey, out var hovFunCol))
            {
                hoveredCol = hovFunCol.HoveredColumn;
            }

            if (_dragState.IsDragging)
            {
                _dragState.UpdateDrag(_rows, listBounds, _lineHeightCache, mouseState);
                if (leftClickReleased)
                {
                    if (_dragState.TryCompleteDrag(_rows, out bool orderChanged))
                    {
                        NormalizeRowOrder();
                        if (orderChanged)
                            UpdateCustomOrderFromRows();
                    }
                }
            }
            else if (!blockLocked && pointerInsideList && leftClickStarted &&
                     !string.IsNullOrEmpty(_hoveredRowKey) && hoveredCol == 0)
            {
                // Col 0 (Green): label — also drag zone
                _dragState.TryStartDrag(_rows, _hoveredRowKey, mouseState);
            }

            // Update header hover so DrawHeader can show column tooltips + hide toggle
            int specsHdrH = specsHfc.HeaderVisible ? HeaderRowHeight : 0;
            var headerBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, specsHdrH);
            specsHfc.ShowHeaderToggle = BlockManager.DockingModeEnabled && !blockLocked && contentBounds.Contains(mouseState.Position);
            specsHfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            specsHfc.UpdateHeaderHover(headerBounds, mouseState, blockLocked ? (MouseState?)previousMouseState : null);
            if (specsHfc.HeaderToggleClicked)
                BlockDataStore.SetRowData(DockBlockKind.Specs, "FunColHeaderVisible", specsHfc.HeaderVisible ? "true" : "false");
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Specs);

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
                _scrollPanel.Draw(spriteBatch, blockLocked);
                var specsHfcDraw = GetOrEnsureHeaderFunCol();
                int specsHdrHDraw = specsHfcDraw.HeaderVisible ? HeaderRowHeight : 0;
                specsHfcDraw.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
                GetOrEnsureHeaderFunCol().DrawHeader(spriteBatch, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, specsHdrHDraw), boldFont, _pixelTexture);
                return;
            }

            float lineHeight = _lineHeightCache;

            // Clip row drawing to the scroll viewport so rows don't bleed over the header.
            var gd = spriteBatch.GraphicsDevice;
            float uiScale = BlockManager.UIScale;
            Rectangle scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(listBounds.X      * uiScale),
                    (int)(listBounds.Y      * uiScale),
                    (int)(listBounds.Width  * uiScale),
                    (int)(listBounds.Height * uiScale))
                : listBounds;
            var viewport = gd.Viewport;
            scissorRect.X      = Math.Clamp(scissorRect.X,      0, viewport.Width);
            scissorRect.Y      = Math.Clamp(scissorRect.Y,      0, viewport.Height);
            scissorRect.Width  = Math.Clamp(scissorRect.Width,  0, viewport.Width  - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, viewport.Height - scissorRect.Y);

            spriteBatch.End();
            var scissorState = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            gd.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            foreach (SpecRow row in _rows)
            {
                Rectangle rowBounds = row.Bounds;
                if (rowBounds == Rectangle.Empty) continue;
                if (rowBounds.Y >= listBounds.Bottom) break;
                if (rowBounds.Bottom <= listBounds.Y) continue;

                bool draggingThisRow = _dragState.IsDragging &&
                    string.Equals(row.Key, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (draggingThisRow) continue;

                DrawRowBackground(spriteBatch, row, rowBounds);
                DrawRowWithFunCol(spriteBatch, row, rowBounds, boldFont);
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                if (_dragState.DropIndicatorBounds != Rectangle.Empty)
                    FillRect(spriteBatch, _dragState.DropIndicatorBounds, ColorPalette.DropIndicator);

                DrawDraggingRow(spriteBatch, listBounds, lineHeight, boldFont);
            }

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            _scrollPanel.Draw(spriteBatch, blockLocked);

            // Draw header strip last so it always renders on top of scrolled content
            var specsHfcMain = GetOrEnsureHeaderFunCol();
            int specsHdrHMain = specsHfcMain.HeaderVisible ? HeaderRowHeight : 0;
            specsHfcMain.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            specsHfcMain.DrawHeader(spriteBatch, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, specsHdrHMain), boldFont, _pixelTexture);
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

        private static FunColInterface GetOrCreateRowFunCol(string key)
        {
            if (_rowFunCols.TryGetValue(key, out var existing)) return existing;
            var fc = new FunColInterface(
                new float[] { 0.42f, 0.58f },
                new TextLabelFeature("Label", FunColTextAlign.Right),
                new ValueDisplayFeature("Value")
            );
            fc.DisableExpansion = true;
            fc.DisableColors = true;
            fc.SuppressTooltipWarnings = true;
            _rowFunCols[key] = fc;
            return fc;
        }

        private static FunColInterface GetOrEnsureHeaderFunCol()
        {
            if (_headerFunCol != null) return _headerFunCol;
            _headerFunCol = new FunColInterface(
                new float[] { 0.42f, 0.58f },
                new TextLabelFeature("Label", FunColTextAlign.Right)
                    { HeaderTooltipTexts = ["Metric or system property label"] },
                new ValueDisplayFeature("Value")
                    { HeaderTooltipTexts = ["Current measured value"] }
            );
            _headerFunCol.DisableExpansion = true;
            _headerFunCol.DisableColors = true;
            _headerFunCol.ShowHeaderTooltips = true;
            return _headerFunCol;
        }

        private static void DrawRowWithFunCol(SpriteBatch spriteBatch, SpecRow row,
            Rectangle rowBounds, UIStyle.UIFont boldFont)
        {
            var funCol = GetOrCreateRowFunCol(row.Key);

            if (funCol.GetFeature(0) is TextLabelFeature labelF)
                labelF.Text = row.Label ?? row.Key;

            if (funCol.GetFeature(1) is ValueDisplayFeature valF)
            {
                valF.IsBoolean = row.IsBoolean;
                valF.BoolState = row.BoolValue;
                valF.Text      = row.Value ?? string.Empty;
            }

            funCol.Draw(spriteBatch, rowBounds, boldFont, _pixelTexture);
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds,
            float lineHeight, UIStyle.UIFont boldFont)
        {
            Rectangle dragBounds = _dragState.GetDragBounds(contentBounds, lineHeight);
            if (dragBounds == Rectangle.Empty) return;

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);

            SpecRow row = _dragState.DraggingSnapshot;

            if (_dragGhostFunCol == null)
            {
                _dragGhostFunCol = new FunColInterface(
                    new float[] { 0.42f, 0.58f },
                    new TextLabelFeature("Label", FunColTextAlign.Right),
                    new ValueDisplayFeature("Value")
                );
                _dragGhostFunCol.DisableExpansion = true;
                _dragGhostFunCol.DisableColors = true;
                _dragGhostFunCol.SuppressTooltipWarnings = true;
            }

            if (_dragGhostFunCol.GetFeature(0) is TextLabelFeature gn) gn.Text = row.Label ?? row.Key;
            if (_dragGhostFunCol.GetFeature(1) is ValueDisplayFeature gv)
            {
                gv.IsBoolean = row.IsBoolean;
                gv.BoolState = row.BoolValue;
                gv.Text      = row.Value ?? string.Empty;
            }

            _dragGhostFunCol.Draw(spriteBatch, dragBounds, boldFont, _pixelTexture);
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
