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
    internal static class BackendBlock
    {
        public const string BlockTitle = "Backend";

        private static readonly string PlaceholderText = TextSpacingHelper.JoinWithWideSpacing("No", "backend", "values", "traced.");

        private static readonly StringBuilder _stringBuilder = new();
        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly List<BackendVariable> _rows = new();
        private static readonly Dictionary<string, int> _customOrder = new(StringComparer.OrdinalIgnoreCase);
        private static bool _orderCacheHydrated;
        private static readonly BlockDragState<BackendVariable> _dragState = new(row => row.Name, row => row.Bounds, (row, isDragging) =>
        {
            row.IsDragging = isDragging;
            return row;
        });
        private static float _lineHeightCache;
        private static Texture2D _pixelTexture;
        private static string _hoveredRowKey;
        private static string _tooltipRowKey;

        public static string GetHoveredRowKey() => _tooltipRowKey;

        public static string GetHoveredRowLabel() => _tooltipRowKey;

        // ── Per-row FunColInterfaces (Name | Value+bool | WrappingMessage) ──
        private static readonly Dictionary<string, FunColInterface> _rowFunCols =
            new(StringComparer.OrdinalIgnoreCase);
        private static FunColInterface _dragGhostFunCol;
        private static FunColInterface _headerFunCol;
        private const int HeaderRowHeight = 16;
        private static bool _headerVisibleLoaded;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Backend);

            var hfc = GetOrEnsureHeaderFunCol();
            if (!_headerVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Backend);
                if (rowData.TryGetValue("FunColHeaderVisible", out string stored))
                    hfc.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                _headerVisibleLoaded = true;
            }

            int headerH = hfc.HeaderVisible ? HeaderRowHeight : 0;
            var listArea = new Rectangle(contentBounds.X, contentBounds.Y + headerH, contentBounds.Width, Math.Max(0, contentBounds.Height - headerH));

            RefreshRows();
            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _lineHeightCache = 0f;
                _scrollPanel.Update(listArea, 0f, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }

            float contentHeight = CalculateContentHeight(boldFont, listArea.Width);
            _scrollPanel.Update(listArea, contentHeight, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            UpdateRowBounds(listBounds, boldFont);

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
            foreach (BackendVariable row in _rows)
            {
                if (row.Bounds != Rectangle.Empty)
                    GetOrCreateRowFunCol(row.Name).Update(row.Bounds, mouseState, dt, suppressHover);
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
            else if (!blockLocked && pointerInsideList &&
                     !string.IsNullOrEmpty(_hoveredRowKey) &&
                     GetOrCreateRowFunCol(_hoveredRowKey).DragHandleClicked)
            {
                // Col 0 (Green): name — drag handle
                _dragState.TryStartDrag(_rows, _hoveredRowKey, mouseState);
            }

            // Update header hover so DrawHeader can show column tooltips + hide toggle
            int headerH2 = hfc.HeaderVisible ? HeaderRowHeight : 0;
            var headerBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, headerH2);
            hfc.ShowHeaderToggle = BlockManager.DockingModeEnabled;
            hfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            hfc.UpdateHeaderHover(headerBounds, mouseState, blockLocked ? (MouseState?)previousMouseState : null);
            if (hfc.HeaderToggleClicked)
                BlockDataStore.SetRowData(DockBlockKind.Backend, "FunColHeaderVisible", hfc.HeaderVisible ? "true" : "false");
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Backend);

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
                _scrollPanel.Draw(spriteBatch, blockLocked);
                return;
            }

            EnsurePixelTexture();

            // ── Scissor clip so rows don't bleed over the header ─────────────
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

            foreach (BackendVariable row in _rows)
            {
                Rectangle rowBounds = row.Bounds;
                if (rowBounds == Rectangle.Empty) continue;
                if (rowBounds.Bottom <= listBounds.Y) continue;
                if (rowBounds.Y >= listBounds.Bottom) break;

                bool isDraggingRow = _dragState.IsDragging &&
                    string.Equals(row.Name, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (isDraggingRow) continue;

                DrawRowBackground(spriteBatch, rowBounds, row.Name);
                DrawRowWithFunCol(spriteBatch, row, rowBounds, boldFont);
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                if (_dragState.DropIndicatorBounds != Rectangle.Empty)
                    FillRect(spriteBatch, _dragState.DropIndicatorBounds, ColorPalette.DropIndicator);

                DrawDraggingRow(spriteBatch, listBounds, boldFont);
            }

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            _scrollPanel.Draw(spriteBatch, blockLocked);

            // Header drawn last — stays on top of scrolled content
            var backendHfc = GetOrEnsureHeaderFunCol();
            int backendHdrH = backendHfc.HeaderVisible ? HeaderRowHeight : 0;
            backendHfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            var headerStrip = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, backendHdrH);
            backendHfc.DrawHeader(spriteBatch, headerStrip, boldFont, _pixelTexture);
        }

        // ── Descriptive change messages ────────────────────────────────────────

        private static string BuildChangeMessage(string variableName, object previousValue, object nextValue)
        {
            string prev = previousValue?.ToString() ?? "null";
            string next = nextValue?.ToString() ?? "null";

            // Boolean — use enable/disable language where we know the variable
            if (nextValue is bool nextBool)
            {
                return variableName switch
                {
                    "DockingMode"       => nextBool ? "Docking mode enabled."  : "Docking mode disabled.",
                    "BlockMenuOpen"     => nextBool ? "Block menu opened."      : "Block menu closed.",
                    "InputBlocked"      => nextBool ? "All input blocked by modal overlay." : "Input unblocked.",
                    "DraggingLayout"    => nextBool ? "Block drag started."     : "Block drag ended.",
                    "CursorOnGameBlock" => nextBool ? "Cursor entered game area." : "Cursor left game area.",
                    "AnyGUIInteracting" => nextBool ? "Cursor pressed on a UI block — gameplay inputs suppressed." : "GUI interaction ended — gameplay inputs restored.",
                    "FreezeGameInputs"  => nextBool ? "Gameplay inputs frozen." : "Gameplay inputs resumed.",
                    _ => nextBool ? $"{variableName} turned ON." : $"{variableName} turned OFF."
                };
            }

            // String — show transition in readable form
            return variableName switch
            {
                "HoveredBlock"       => $"Cursor moved to {next}.",
                "HoveredDragBar"     => next == "None" ? "Left drag bar." : $"Hovering drag bar of {next}.",
                "FocusedBlock"       => next == "None" ? "Keyboard focus cleared." : $"Keyboard focus moved to {next}.",
                "GUIInteractingWith" => next == "None" ? "Interaction ended." : $"Interacting with {next}.",
                _ => $"{prev} → {next}"
            };
        }

        // ── Variable row heights ───────────────────────────────────────────────

        private static float CalculateContentHeight(UIStyle.UIFont font, int listWidth)
        {
            if (_lineHeightCache <= 0f || _rows.Count == 0) return 0f;
            float total = 0f;
            foreach (BackendVariable row in _rows)
                total += CalculateRowHeight(font, row, listWidth);
            return total;
        }

        private static float CalculateRowHeight(UIStyle.UIFont font, BackendVariable row, int listWidth)
        {
            if (_lineHeightCache <= 0f) return _lineHeightCache;

            // Message column is weight 0.65 of total width
            int msgColWidth = (int)(listWidth * 0.65f);
            int lines = GetOrCreateRowFunColWrapping(row.Name)
                .CalculateMessageLines(font, msgColWidth, row.LastChangeMessage ?? string.Empty);
            return Math.Max(1, lines) * _lineHeightCache;
        }

        // ── Order cache ───────────────────────────────────────────────────────

        private static void EnsureOrderCacheHydrated()
        {
            if (_orderCacheHydrated)
            {
                return;
            }

            Dictionary<string, int> storedOrders = BlockDataStore.LoadRowOrders(DockBlockKind.Backend);
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

            IReadOnlyList<GameTracker.GameTrackerVariable> variables = GameTracker.GetTrackedVariables();
            HashSet<string> incoming = new(StringComparer.OrdinalIgnoreCase);
            bool orderChanged = false;
            int nextOrder = GetNextOrderSeed();

            foreach (var variable in variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    continue;
                }

                string storageKey = BlockDataStore.CanonicalizeRowKey(DockBlockKind.Backend, variable.Name);
                incoming.Add(storageKey);
                if (!_customOrder.ContainsKey(storageKey))
                {
                    _customOrder[storageKey] = nextOrder++;
                    orderChanged = true;
                }

                BackendVariable row = FindRow(storageKey);
                bool wasDragging = row?.IsDragging ?? false;
                Rectangle previousBounds = row?.Bounds ?? Rectangle.Empty;

                if (row == null)
                {
                    row = new BackendVariable(storageKey, variable.Value, variable.IsBoolean);
                    _rows.Add(row);
                }

                object previousValue = row.Value;
                row.Value = variable.Value;
                row.IsBoolean = variable.IsBoolean;
                row.RenderOrder = _customOrder[storageKey];

                // A non-empty Detail from GameTracker takes precedence as the persistent message.
                // Otherwise, build a descriptive change message when the value transitions.
                if (!string.IsNullOrEmpty(variable.Detail))
                {
                    row.LastChangeMessage = variable.Detail;
                }
                else if (!Equals(previousValue, variable.Value) && previousValue != null)
                {
                    row.LastChangeMessage = BuildChangeMessage(variable.Name, previousValue, variable.Value);
                }
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

            BlockDataStore.SaveRowOrders(DockBlockKind.Backend, rows);
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

        private static void UpdateRowBounds(Rectangle contentBounds, UIStyle.UIFont font)
        {
            if (_lineHeightCache <= 0f)
            {
                return;
            }

            int listWidth = contentBounds.Width;
            float y = contentBounds.Y - _scrollPanel.ScrollOffset;

            for (int i = 0; i < _rows.Count; i++)
            {
                BackendVariable row = _rows[i];
                float rowH = CalculateRowHeight(font, row, listWidth);
                int rowHeight = (int)MathF.Ceiling(rowH);
                row.Bounds = new Rectangle(contentBounds.X, (int)MathF.Round(y), listWidth, rowHeight);
                _rows[i] = row;
                y += rowH;
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
            bool changed = false;
            for (int i = 0; i < _rows.Count; i++)
            {
                BackendVariable row = _rows[i];
                _customOrder[row.Name] = row.RenderOrder;
                changed = true;
            }

            changed |= NormalizeCustomOrder();

            if (changed)
            {
                PersistRowOrders();
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
                FillRect(spriteBatch, bounds, ColorPalette.RowHover);
            }
        }

        // ── FunCol factory ────────────────────────────────────────────────────

        private static FunColInterface GetOrCreateRowFunCol(string name)
        {
            if (_rowFunCols.TryGetValue(name, out var existing)) return existing;
            var fc = new FunColInterface(
                new float[] { 0.27f, 0.08f, 0.65f },
                new TextLabelFeature("Name", FunColTextAlign.Right),
                new ValueDisplayFeature("Value"),
                new WrappingTextFeature("Message", FunColTextAlign.Left)
            );
            fc.DisableExpansion = true;
            fc.DisableColors = true;
            fc.RowDragEnabled = true;
            fc.SuppressTooltipWarnings = true;
            _rowFunCols[name] = fc;
            return fc;
        }

        /// <summary>Returns the WrappingTextFeature from a row's FunColInterface, for line-count queries.</summary>
        private static RowFunColAccessor GetOrCreateRowFunColWrapping(string name)
        {
            var fc = GetOrCreateRowFunCol(name);
            var wrapping = fc.GetFeature(2) as WrappingTextFeature;
            return new RowFunColAccessor(wrapping);
        }

        private static FunColInterface GetOrEnsureHeaderFunCol()
        {
            if (_headerFunCol != null) return _headerFunCol;
            _headerFunCol = new FunColInterface(
                new float[] { 0.27f, 0.08f, 0.65f },
                new TextLabelFeature("Name", FunColTextAlign.Right)
                    { HeaderTooltipTexts = ["Variable name"] },
                new ValueDisplayFeature("Value")
                    { HeaderTooltipTexts = ["Current value of the variable"] },
                new WrappingTextFeature("Message", FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Last change log message"] }
            );
            _headerFunCol.DisableExpansion = true;
            _headerFunCol.DisableColors = true;
            _headerFunCol.ShowHeaderTooltips = true;
            return _headerFunCol;
        }

        private static void DrawRowWithFunCol(SpriteBatch spriteBatch, BackendVariable row,
            Rectangle rowBounds, UIStyle.UIFont boldFont)
        {
            var funCol = GetOrCreateRowFunCol(row.Name);

            if (funCol.GetFeature(0) is TextLabelFeature nameF)
                nameF.Text = row.Name;

            if (funCol.GetFeature(1) is ValueDisplayFeature valF)
            {
                valF.IsBoolean = row.IsBoolean;
                if (row.IsBoolean && row.Value is bool b)
                    valF.BoolState = b;
                else
                    valF.BoolState = false;
                valF.Text = row.Value?.ToString() ?? "null";
            }

            if (funCol.GetFeature(2) is WrappingTextFeature msgF)
                msgF.Text = row.LastChangeMessage ?? string.Empty;

            funCol.Draw(spriteBatch, rowBounds, boldFont, _pixelTexture);
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds,
            UIStyle.UIFont boldFont)
        {
            if (_lineHeightCache <= 0f) return;
            Rectangle dragBounds = _dragState.GetDragBounds(contentBounds, _lineHeightCache);
            if (dragBounds == Rectangle.Empty) return;

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);

            BackendVariable row = _dragState.DraggingSnapshot;

            if (_dragGhostFunCol == null)
            {
                _dragGhostFunCol = new FunColInterface(
                    new float[] { 0.27f, 0.08f, 0.65f },
                    new TextLabelFeature("Name", FunColTextAlign.Right),
                    new ValueDisplayFeature("Value"),
                    new WrappingTextFeature("Message", FunColTextAlign.Left)
                );
                _dragGhostFunCol.DisableExpansion = true;
                _dragGhostFunCol.DisableColors = true;
                _dragGhostFunCol.SuppressTooltipWarnings = true;
            }

            if (_dragGhostFunCol.GetFeature(0) is TextLabelFeature gn) gn.Text = row.Name;
            if (_dragGhostFunCol.GetFeature(1) is ValueDisplayFeature gv)
            {
                gv.IsBoolean = row.IsBoolean;
                if (row.IsBoolean && row.Value is bool b)
                    gv.BoolState = b;
                else
                    gv.BoolState = false;
                gv.Text = row.Value?.ToString() ?? "null";
            }
            if (_dragGhostFunCol.GetFeature(2) is WrappingTextFeature gm) gm.Text = row.LastChangeMessage ?? string.Empty;

            _dragGhostFunCol.Draw(spriteBatch, dragBounds, boldFont, _pixelTexture);
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

        // ── Small helper to call CalculateLineCount from the wrapping feature ──

        private readonly struct RowFunColAccessor
        {
            private readonly WrappingTextFeature _feature;
            public RowFunColAccessor(WrappingTextFeature feature) => _feature = feature;

            public int CalculateMessageLines(UIStyle.UIFont font, int columnWidth, string text)
            {
                if (_feature == null) return 1;
                _feature.Text = text;
                return _feature.CalculateLineCount(font, columnWidth);
            }
        }

        // ── Data ──────────────────────────────────────────────────────────────

        private sealed class BackendVariable
        {
            public BackendVariable(string name, object value, bool isBoolean)
            {
                Name = name ?? string.Empty;
                Value = value;
                IsBoolean = isBoolean;
                LastChangeMessage = string.Empty;
            }

            public string Name { get; }
            public object Value { get; set; }
            public bool IsBoolean { get; set; }
            public string LastChangeMessage { get; set; }
            public int RenderOrder { get; set; }
            public Rectangle Bounds { get; set; }
            public bool IsDragging { get; set; }
        }
    }
}
