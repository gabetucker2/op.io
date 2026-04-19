using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.FunCol;
using op.io.UI.FunCol.Features;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class ControlsBlock
    {
        public const string BlockTitle = "Controls";

        private static readonly List<KeybindDisplayRow> _keybindCache = new();
        private static bool _keybindCacheLoaded;
        private static bool _headerVisibleLoaded;
        private static readonly StringBuilder _stringBuilder = new();
        private const bool DefaultFunColCategoryModeEnabled = false;
        private static readonly bool _funColCategoryModeEnabled = IsFunColCategoryModeEnabled(DockBlockKind.Controls);
        private static readonly BlockDragState<KeybindDisplayRow> _rowDragState = new(row => row.Action, row => row.Bounds, (row, isDragging) =>
        {
            row.IsDragging = isDragging;
            return row;
        });
        private static readonly BlockDragState<CategoryLayoutRow> _categoryDragState = new(row => row.CategoryKey, row => row.Bounds, (row, isDragging) =>
        {
            row.IsDragging = isDragging;
            return row;
        });
        private static readonly List<CategoryLayoutRow> _categoryLayoutRows = new();
        private static readonly List<CategoryLayoutRow> _categoryDragRows = new();
        private static readonly List<KeybindDisplayRow> _rowDragCategoryRows = new();
        private static readonly HashSet<string> _collapsedCategoryKeys = new(StringComparer.OrdinalIgnoreCase);
        private static Rectangle _activeRowDragBounds;
        private static string _activeRowDragCategoryKey;
        private static Texture2D _pixelTexture;
        private static string _hoveredRowKey;
        private static string _hoveredCategoryKey;
        private static string _hoveredCategoryToggleKey;
        private static string _tooltipRowKey;
        private static string _tooltipRowLabel;

        public static string GetHoveredRowKey() => _tooltipRowKey;

        public static string GetHoveredRowLabel() => _tooltipRowLabel ?? _tooltipRowKey;
        private static float _lineHeightCache;
        private static readonly BlockScrollPanel _scrollPanel = new();
        private static string _pressedDragRowKey;   // row where col 3 (drag handle) was pressed
        private static string _pressedCategoryDragKey;
        private static string _pressedRebindRowKey; // row where col 1 (keybind) was pressed
        private static Point _pressStartPosition;

        // ── Per-row FunColInterfaces (Green=name, Blue=key, Red=type, Orange=value) ──
        private static readonly Dictionary<string, FunColInterface> _rowFunCols =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, UIDropdown> _enumDropdowns =
            new(StringComparer.OrdinalIgnoreCase);
        // Static drag-ghost funCol reused when rendering the dragging row ghost
        private static FunColInterface _dragGhostFunCol;
        private static string _openEnumDropdownKey;
        private static bool _rebindOverlayVisible;
        private static string _rebindAction;
        private static string _rebindCurrentInput;
        private static string _rebindPendingDisplay;
        private static string _rebindCurrentCanonical;
        private static string _rebindPendingCanonical;
        private static Rectangle _rebindModalBounds;
        private static Rectangle _rebindConfirmButtonBounds;
        private static Rectangle _rebindUnbindButtonBounds;
        private static Rectangle _rebindCancelButtonBounds;
        private static bool _rebindCaptured;
        private static bool _suppressNextCapture;
        private static string _rebindConflictWarning;
        private static bool _rebindConfirmHovered;
        private static bool _rebindUnbindHovered;
        private static bool _rebindCancelHovered;
        private static bool _rebindPendingUnbind;

        private const int TypeTogglePadding = 2;
        private const int TypeIndicatorDiameter = 10;
        private const int ValueHighlightPadding = 2;
        private const int DragStartThreshold = 6;
        private const int ControlsHeaderHeight = 16;
        private const int CategorySectionGap = 4;
        private const int CategoryLabelLeftPad = 5;
        private const int CategoryLabelTopPad = 1;
        private const int CategoryToggleBtnSize = 10;
        private const int CategoryToggleBtnPad = 3;
        private const string EnumOptionTooltipKeyEnabled = "Dropdown_ControlEnumOptionEnabled";
        private const string EnumOptionTooltipKeyDisabled = "Dropdown_ControlEnumOptionDisabled";

        // ── Float widget constants ────────────────────────────────────────────
        private const int FloatArrowW  = 16;
        private const int FloatInputW  = 48;
        private const int FloatWidgetH = 16;
        private const float FloatStep  = 0.1f;
        private const float FloatMin   = 0.01f;

        // ── Float editing state ───────────────────────────────────────────────
        private static string _editingFloatKey;
        private static string _floatInputBuffer = string.Empty;
        private static readonly KeyRepeatTracker _floatInputTracker = new();
        private static KeyboardState _prevFloatKeyboard;
        private static KeyboardState _previousKeyboardState;

        internal static void InvalidateCache()
        {
            _keybindCacheLoaded = false;
            _keybindCache.Clear();
            _rowFunCols.Clear();
            _enumDropdowns.Clear();
            _categoryLayoutRows.Clear();
            _rowDragCategoryRows.Clear();
            _openEnumDropdownKey = null;
            _dragGhostFunCol = null;
            _activeRowDragBounds = Rectangle.Empty;
            _activeRowDragCategoryKey = null;
            _hoveredCategoryKey = null;
            _hoveredCategoryToggleKey = null;
            _pressedCategoryDragKey = null;
            _collapsedCategoryKeys.Clear();
            _rowDragState.Reset();
            _categoryDragState.Reset();
        }

        private static bool IsSwitchType(InputType inputType) =>
            inputType == InputType.SaveSwitch ||
            inputType == InputType.NoSaveSwitch ||
            inputType == InputType.DoubleTapToggle;

        private static bool IsEnumType(InputType inputType) =>
            inputType == InputType.SaveEnum || inputType == InputType.NoSaveEnum;

        private static bool IsFloatType(InputType inputType) =>
            inputType == InputType.Float;

        private static bool IsPersistentSwitch(InputType inputType) => inputType == InputType.SaveSwitch;

        private static InputType ParseInputTypeLabel(string typeLabel)
        {
            if (string.Equals(typeLabel, "Switch", StringComparison.OrdinalIgnoreCase))
            {
                return InputType.SaveSwitch;
            }

            return Enum.TryParse(typeLabel, true, out InputType parsed) ? parsed : InputType.Hold;
        }

        private static bool IsFunColCategoryModeEnabled(DockBlockKind blockKind)
        {
            return DefaultFunColCategoryModeEnabled || blockKind == DockBlockKind.Controls;
        }

        private static bool IsAnyDragActive => _rowDragState.IsDragging || _categoryDragState.IsDragging;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Controls);
            KeyboardState keyboardState = Keyboard.GetState();

            if (!FontManager.TryGetControlsFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            EnsureKeybindCache();

            var headerFunColEarly = GetOrEnsureHeaderFunCol();
            if (!_headerVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Controls);
                if (rowData.TryGetValue("FunColHeaderVisible", out string stored))
                    headerFunColEarly.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                if (rowData.TryGetValue("FunColColumnWeights", out string weightStr))
                    ApplyEncodedWeights(headerFunColEarly, weightStr);
                _headerVisibleLoaded = true;
            }

            _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            float contentHeight = GetListContentHeight();
            int headerH = headerFunColEarly.HeaderVisible ? ControlsHeaderHeight : 0;
            var listArea = new Rectangle(contentBounds.X, contentBounds.Y + headerH, contentBounds.Width, Math.Max(0, contentBounds.Height - headerH));
            _scrollPanel.Update(listArea, contentHeight, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);
            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = listArea;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateRowBounds(listBounds);

            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickStarted = leftDown && !leftDownPrev;
            bool leftClickReleased = !leftDown && leftDownPrev;
            bool pointerInsideList = listBounds.Contains(mouseState.Position);

            if (blockLocked && IsAnyDragActive)
            {
                _rowDragState.Reset();
                _categoryDragState.Reset();
                _rowDragCategoryRows.Clear();
                _activeRowDragBounds = Rectangle.Empty;
                _activeRowDragCategoryKey = null;
                _hoveredCategoryToggleKey = null;
            }

            // Update per-row FunColInterfaces (suppress hover animation during drag)
            bool suppressHover = IsAnyDragActive;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (row.Bounds == Rectangle.Empty) continue;
                GetOrCreateRowFunCol(row.Action)
                    .Update(row.Bounds, mouseState, dt, suppressHover || blockLocked);
            }

            UpdateEnumDropdownBounds();
            SyncEnumDropdowns();
            bool enumDropdownStateChanged = UpdateEnumDropdownInteractions(mouseState, previousMouseState, keyboardState, blockLocked);
            if (enumDropdownStateChanged)
            {
                SyncEnumDropdowns();
            }

            bool hasEnumDropdownTooltip = TryGetHoveredEnumDropdownTooltip(mouseState.Position, out string enumDropdownTooltipKey, out string enumDropdownTooltipLabel);
            bool allowInteraction = !blockLocked && (pointerInsideList || hasEnumDropdownTooltip);
            string hitRow = pointerInsideList ? HitTestRow(mouseState.Position) : null;
            string hitCategory = pointerInsideList ? HitTestCategoryHeader(mouseState.Position) : null;
            string hitCategoryToggle = pointerInsideList ? HitTestCategoryToggle(mouseState.Position, blockLocked, pointerInsideList) : null;
            _hoveredRowKey = allowInteraction ? hitRow : null;
            _hoveredCategoryToggleKey = allowInteraction && string.IsNullOrWhiteSpace(hitRow) ? hitCategoryToggle : null;
            _hoveredCategoryKey = allowInteraction && string.IsNullOrWhiteSpace(hitRow) && string.IsNullOrWhiteSpace(_hoveredCategoryToggleKey) ? hitCategory : null;
            _tooltipRowKey = hasEnumDropdownTooltip ? enumDropdownTooltipKey : hitRow;
            _tooltipRowLabel = hasEnumDropdownTooltip ? enumDropdownTooltipLabel : hitRow;

            if (!allowInteraction)
            {
                _pressedDragRowKey   = null;
                _pressedCategoryDragKey = null;
                _pressedRebindRowKey = null;
                _hoveredCategoryToggleKey = null;
            }

            // Determine which column is hovered on the hovered row
            int hoveredCol = -1;
            if (!string.IsNullOrEmpty(_hoveredRowKey) &&
                _rowFunCols.TryGetValue(_hoveredRowKey, out var hovFunCol))
            {
                hoveredCol = hovFunCol.HoveredColumn;
            }

            if (_categoryDragState.IsDragging)
            {
                UpdateCategoryDrag(listBounds, mouseState);
                if (leftClickReleased)
                {
                    if (TryCompleteCategoryDrag())
                    {
                        NormalizeCacheOrder();
                        PersistRowOrder();
                    }

                    _pressedDragRowKey = null;
                    _pressedCategoryDragKey = null;
                    _pressedRebindRowKey = null;
                }
            }
            else if (_rowDragState.IsDragging)
            {
                UpdateRowDrag(mouseState);
                if (leftClickReleased)
                {
                    if (TryCompleteRowDrag())
                    {
                        NormalizeCacheOrder();
                        PersistRowOrder();
                    }

                    _pressedDragRowKey   = null;
                    _pressedCategoryDragKey = null;
                    _pressedRebindRowKey = null;
                }
            }
            else
            {
                if (allowInteraction && leftClickStarted && !string.IsNullOrWhiteSpace(_hoveredCategoryToggleKey))
                {
                    ToggleCategoryCollapsed(_hoveredCategoryToggleKey);
                    _pressedDragRowKey = null;
                    _pressedCategoryDragKey = null;
                    _pressedRebindRowKey = null;
                    CloseAllEnumDropdowns();
                    _openEnumDropdownKey = null;
                }
                else if (allowInteraction && leftClickStarted && !string.IsNullOrEmpty(_hoveredRowKey))
                {
                    if (hoveredCol == 2)
                    {
                        // Col 2 (Red): type label — cycle input type (Hold ↔ Switch) only, skip enums
                        int rowIdx2 = GetRowIndex(_hoveredRowKey);
                        if (rowIdx2 >= 0 && !IsEnumType(_keybindCache[rowIdx2].InputType))
                            TryToggleInputType(_hoveredRowKey, boldFont, clickedIndicator: false);
                    }
                    else if (hoveredCol == 3)
                    {
                        // Col 3 (Orange): value — toggle switch state (enum rows use dropdown selection)
                        int rowIdx3 = GetRowIndex(_hoveredRowKey);
                        if (rowIdx3 >= 0)
                        {
                            var vRow = _keybindCache[rowIdx3];
                            if (IsSwitchType(vRow.InputType) || IsPersistentSwitch(vRow.InputType))
                                TryToggleInputType(_hoveredRowKey, boldFont, clickedIndicator: true);
                        }
                    }
                    else if (GetOrCreateRowFunCol(_hoveredRowKey).DragHandleClicked)
                    {
                        // Col 0 (Green): action name — drag handle
                        _pressedDragRowKey = _hoveredRowKey;
                        _pressStartPosition = mouseState.Position;
                    }
                    else if (hoveredCol == 1)
                    {
                        // Col 1 (Blue): keybind — press tracked for rebind on release
                        // Skip rebind tracking for Float rows (they have no key to rebind)
                        int floatCheckIdx = GetRowIndex(_hoveredRowKey);
                        if (floatCheckIdx < 0 || !IsFloatType(_keybindCache[floatCheckIdx].InputType))
                            _pressedRebindRowKey = _hoveredRowKey;
                    }
                }
                else if (allowInteraction && leftClickStarted && _funColCategoryModeEnabled && !string.IsNullOrWhiteSpace(_hoveredCategoryKey))
                {
                    _pressedCategoryDragKey = _hoveredCategoryKey;
                    _pressStartPosition = mouseState.Position;
                }

                // Drag threshold check
                if (_pressedDragRowKey != null && leftDown)
                {
                    if (HasDragExceededThreshold(mouseState.Position))
                    {
                        TryStartRowDrag(_pressedDragRowKey, mouseState);
                        _pressedDragRowKey = null;
                    }
                }

                if (_pressedCategoryDragKey != null && leftDown)
                {
                    if (HasDragExceededThreshold(mouseState.Position))
                    {
                        TryStartCategoryDrag(_pressedCategoryDragKey, mouseState);
                        _pressedCategoryDragKey = null;
                    }
                }

                if (leftClickReleased)
                {
                    bool handledByFloat = false;
                    if (!string.IsNullOrEmpty(_hoveredRowKey) && hoveredCol == 3)
                    {
                        int floatIdx = GetRowIndex(_hoveredRowKey);
                        if (floatIdx >= 0 && IsFloatType(_keybindCache[floatIdx].InputType))
                        {
                            HandleFloatWidgetClick(mouseState.Position, _keybindCache[floatIdx]);
                            handledByFloat = true;
                        }
                    }

                    // Commit float edit when clicking away from the float row's col 3
                    if (!handledByFloat && _editingFloatKey != null)
                    {
                        CommitFloatInput();
                        _editingFloatKey = null;
                    }

                    if (!handledByFloat &&
                        !string.IsNullOrEmpty(_pressedRebindRowKey) &&
                        string.Equals(_pressedRebindRowKey, _hoveredRowKey, StringComparison.OrdinalIgnoreCase) &&
                        hoveredCol == 1)
                    {
                        BeginRebindFlow(_pressedRebindRowKey);
                    }

                    _pressedDragRowKey   = null;
                    _pressedCategoryDragKey = null;
                    _pressedRebindRowKey = null;
                }
            }

            // Float textbox keyboard input
            if (_editingFloatKey != null && !blockLocked)
            {
                double elapsedSec = gameTime.ElapsedGameTime.TotalSeconds;
                foreach (Keys k in _floatInputTracker.GetKeysWithRepeat(keyboardState, _prevFloatKeyboard, elapsedSec))
                {
                    if (k == Keys.Enter)
                    {
                        CommitFloatInput();
                        _editingFloatKey = null;
                        break;
                    }
                    if (k == Keys.Escape)
                    {
                        _editingFloatKey = null;
                        _floatInputBuffer = string.Empty;
                        break;
                    }
                    if (k == Keys.Back)
                    {
                        if (_floatInputBuffer.Length > 0)
                            _floatInputBuffer = _floatInputBuffer[..^1];
                        continue;
                    }
                    char? c = FloatKeyToChar(k, keyboardState, _floatInputBuffer);
                    if (c.HasValue && _floatInputBuffer.Length < 8)
                        _floatInputBuffer += c.Value;
                }
                _prevFloatKeyboard = keyboardState;
            }

            // Update header hover so DrawHeader can show column tooltips + hide toggle
            int headerH2 = headerFunColEarly.HeaderVisible ? ControlsHeaderHeight : 0;
            var headerBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, headerH2);
            var headerFunCol = headerFunColEarly;
            headerFunCol.ShowHeaderToggle = BlockManager.DockingModeEnabled && !blockLocked && contentBounds.Contains(mouseState.Position);
            headerFunCol.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, ControlsHeaderHeight);
            headerFunCol.UpdateHeaderHover(headerBounds, mouseState, blockLocked ? (MouseState?)previousMouseState : null);
            if (headerFunCol.HeaderToggleClicked)
                BlockDataStore.SetRowData(DockBlockKind.Controls, "FunColHeaderVisible", headerFunCol.HeaderVisible ? "true" : "false");
            if (headerFunCol.ColumnWeightsChanged)
            {
                string encoded = EncodeWeights(headerFunCol.GetWeights());
                BlockDataStore.SetRowData(DockBlockKind.Controls, "FunColColumnWeights", encoded);
                PropagateWeightsToRowFunCols(headerFunCol.GetWeights());
            }

            _previousKeyboardState = keyboardState;
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

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Controls);

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
            Point pointerPosition = Mouse.GetState().Position;
            bool pointerInsideList = listBounds.Contains(pointerPosition);

            EnsurePixelTexture();

            float lineHeight = _lineHeightCache;

            // Clip row drawing to the scroll viewport so partially-scrolled rows don't bleed outside.
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

            if (_funColCategoryModeEnabled)
            {
                foreach (CategoryLayoutRow category in _categoryLayoutRows)
                {
                    Rectangle headerBounds = category.HeaderBounds;
                    if (headerBounds == Rectangle.Empty) continue;
                    if (headerBounds.Y >= listBounds.Bottom) break;
                    if (headerBounds.Bottom <= listBounds.Y) continue;

                    bool isDraggingCategory = _categoryDragState.IsDragging &&
                        string.Equals(category.CategoryKey, _categoryDragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                    if (!isDraggingCategory)
                    {
                        DrawCategoryHeader(spriteBatch, category, blockLocked, boldFont, pointerInsideList);
                    }
                }
            }

            foreach (KeybindDisplayRow row in _keybindCache)
            {
                Rectangle rowBounds = row.Bounds;
                if (rowBounds == Rectangle.Empty) continue;
                if (rowBounds.Y >= listBounds.Bottom) break;
                if (rowBounds.Bottom <= listBounds.Y) continue;

                bool isDraggingRow = _rowDragState.IsDragging &&
                    string.Equals(row.Action, _rowDragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                bool isDraggingCategoryRow = _categoryDragState.IsDragging &&
                    string.Equals(row.CategoryKey, _categoryDragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (!isDraggingRow && !isDraggingCategoryRow)
                {
                    DrawRowBackground(spriteBatch, row, rowBounds, blockLocked);
                    DrawRowWithFunCol(spriteBatch, row, rowBounds, boldFont);
                    if (IsFloatType(row.InputType))
                        DrawFloatWidget(spriteBatch, row, rowBounds, boldFont, blockLocked);
                    if (IsEnumType(row.InputType))
                        DrawEnumDropdown(spriteBatch, row, blockLocked);
                }
            }

            if (!blockLocked && _rowDragState.IsDragging && _rowDragState.HasSnapshot)
            {
                if (_rowDragState.DropIndicatorBounds != Rectangle.Empty)
                    FillRect(spriteBatch, _rowDragState.DropIndicatorBounds, ColorPalette.DropIndicator);

                Rectangle dragBounds = _activeRowDragBounds == Rectangle.Empty ? listBounds : _activeRowDragBounds;
                DrawDraggingRow(spriteBatch, dragBounds, lineHeight, boldFont);
            }
            else if (!blockLocked && _categoryDragState.IsDragging && _categoryDragState.HasSnapshot)
            {
                if (_categoryDragState.DropIndicatorBounds != Rectangle.Empty)
                    FillRect(spriteBatch, _categoryDragState.DropIndicatorBounds, ColorPalette.DropIndicator);

                DrawDraggingCategory(spriteBatch, listBounds, boldFont, blockLocked);
            }

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            _scrollPanel.Draw(spriteBatch, blockLocked);

            // Draw header strip last so it always renders on top of scrolled content
            var hfc = GetOrEnsureHeaderFunCol();
            int hdrH = hfc.HeaderVisible ? ControlsHeaderHeight : 0;
            hfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, ControlsHeaderHeight);
            var headerStrip = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, hdrH);
            hfc.DrawHeader(spriteBatch, headerStrip, boldFont, _pixelTexture);
            DrawEnumDropdownOptionOverlays(spriteBatch, blockLocked);
        }

        private static void EnsureKeybindCache()
        {
            if (_keybindCacheLoaded)
            {
                return;
            }

            _rowDragState.Reset();
            _categoryDragState.Reset();
            _rowDragCategoryRows.Clear();
            _activeRowDragBounds = Rectangle.Empty;
            _activeRowDragCategoryKey = null;

            try
            {
                ControlKeyMigrations.EnsureApplied();
                _keybindCache.Clear();

                Dictionary<string, string> storedRowData = BlockDataStore.LoadRowData(DockBlockKind.Controls);

                const string sql = @"
SELECT SettingKey,
       InputKey,
       InputType,
       COALESCE(RenderOrder, 0) AS ControlOrder,
       COALESCE(RenderCategory, '') AS RenderCategory,
       COALESCE(RenderCategoryOrder, 0) AS RenderCategoryOrder
FROM ControlKey
ORDER BY RenderCategoryOrder ASC, RenderCategory ASC, ControlOrder ASC, SettingKey ASC;";
                var rows = DatabaseQuery.ExecuteQuery(sql);
                int fallbackOrder = 0;

                foreach (var row in rows)
                {
                    string typeLabel = row.TryGetValue("InputType", out object type) ? type?.ToString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(typeLabel))
                    {
                        typeLabel = "Unknown";
                    }

                    InputType parsedType = ParseInputTypeLabel(typeLabel);

                    string actionLabel = row.TryGetValue("SettingKey", out object action) ? action?.ToString() ?? "Action" : "Action";
                    string rawInputKey = row.TryGetValue("InputKey", out object key) ? key?.ToString() ?? "" : "";
                    string inputLabel = IsFloatType(parsedType)
                        ? ControlStateManager.GetFloat(actionLabel, 1.0f).ToString("F2")
                        : (string.IsNullOrWhiteSpace(rawInputKey) ? "[UNBOUND]" : rawInputKey);
                    int orderValue = row.TryGetValue("ControlOrder", out object orderObj) ? Convert.ToInt32(orderObj) : fallbackOrder;
                    string rawCategory = row.TryGetValue("RenderCategory", out object categoryObj) ? categoryObj?.ToString() ?? string.Empty : string.Empty;
                    int categoryOrder = row.TryGetValue("RenderCategoryOrder", out object categoryOrderObj) ? Convert.ToInt32(categoryOrderObj) : 0;
                    string resolvedCategory = ControlRowCategoryCatalog.NormalizeCategoryKey(rawCategory, actionLabel);
                    if (categoryOrder < 0)
                    {
                        categoryOrder = ControlRowCategoryCatalog.GetDefaultCategoryOrder(resolvedCategory);
                    }

                    bool triggerAuto = false;
                    string canonicalKey = BlockDataStore.CanonicalizeRowKey(DockBlockKind.Controls, actionLabel);
                    if (!IsFloatType(parsedType) &&
                        storedRowData.TryGetValue(canonicalKey, out string storedData) &&
                        TryParseRowData(storedData, out InputType storedType, out bool storedTriggerAuto) &&
                        !IsPersistentSwitch(storedType) &&
                        !IsEnumType(parsedType))
                    {
                        parsedType = storedType;
                        triggerAuto = storedTriggerAuto;
                    }

                    if (ControlKeyRules.RequiresSwitchSemantics(actionLabel))
                    {
                        parsedType = InputType.SaveSwitch;
                        triggerAuto = false;
                    }

                    bool toggleLocked = InputManager.IsTypeLocked(actionLabel) || IsFloatType(parsedType);

                    string parsedTypeLabel = parsedType.ToString();
                    if (!string.Equals(typeLabel, parsedTypeLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        ControlKeyData.SetInputType(actionLabel, parsedTypeLabel);
                    }

                    InputManager.UpdateBindingInputType(actionLabel, parsedType, triggerAuto);
                    BlockDataStore.SetRowData(DockBlockKind.Controls, actionLabel, SerializeRowData(parsedType, triggerAuto));

                    _keybindCache.Add(new KeybindDisplayRow
                    {
                        Action = actionLabel,
                        Input = inputLabel,
                        TypeLabel = parsedTypeLabel,
                        InputType = parsedType,
                        RenderOrder = Math.Max(0, orderValue),
                        CategoryKey = resolvedCategory,
                        CategoryOrder = categoryOrder,
                        Bounds = Rectangle.Empty,
                        IsDragging = false,
                        TypeToggleBounds = Rectangle.Empty,
                        KeyValueBounds = Rectangle.Empty,
                        EnumValueBounds = Rectangle.Empty,
                        TriggerAutoFire = triggerAuto,
                        ToggleLocked = toggleLocked
                    });

                    fallbackOrder++;
                }

                _keybindCache.Sort(CompareRowsByCategoryThenOrder);
                NormalizeCacheOrder();
                PersistRowOrder();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load keybinds for controls block: {ex.Message}");
            }
            finally
            {
                _keybindCacheLoaded = true;
            }
        }

        private static void PersistRowOrder()
        {
            var updates = new List<ControlKeyData.RenderOrderUpdate>(_keybindCache.Count);
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (string.IsNullOrWhiteSpace(row.Action) || row.RenderOrder < 0)
                {
                    continue;
                }

                string categoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                int categoryOrder = row.CategoryOrder >= 0
                    ? row.CategoryOrder
                    : ControlRowCategoryCatalog.GetDefaultCategoryOrder(categoryKey);
                updates.Add(new ControlKeyData.RenderOrderUpdate(row.Action, row.RenderOrder, categoryKey, categoryOrder));
            }

            ControlKeyData.UpdateRenderOrders(updates);
        }

        private static void DrawRowBackground(SpriteBatch spriteBatch, KeybindDisplayRow row, Rectangle bounds, bool blockLocked)
        {
            if (blockLocked || bounds == Rectangle.Empty || _pixelTexture == null) return;
            if (ShouldHighlightRow(row, blockLocked))
                FillRect(spriteBatch, bounds, ColorPalette.RowHover);
        }

        /// <summary>Draws a row using its per-row FunColInterface (4 columns).</summary>
        private static void DrawRowWithFunCol(SpriteBatch spriteBatch, KeybindDisplayRow row,
            Rectangle rowBounds, UIStyle.UIFont boldFont)
        {
            var funCol = GetOrCreateRowFunCol(row.Action);

            // Update feature texts each frame before drawing
            if (funCol.GetFeature(0) is TextLabelFeature nameF)
                nameF.Text = row.Action;

            if (funCol.GetFeature(1) is TextLabelFeature keyF)
            {
                // Float rows: leave text empty — the widget draws its own content on top
                keyF.Text = IsFloatType(row.InputType)
                    ? string.Empty
                    : (string.IsNullOrWhiteSpace(row.Input) ? "[UNBOUND]" : row.Input);
            }

            if (funCol.GetFeature(2) is TextLabelFeature typeF)
                typeF.Text = row.TypeLabel;

            if (funCol.GetFeature(3) is CyclerFeature valueF)
            {
                valueF.IsLocked = row.ToggleLocked;
                if (IsSwitchType(row.InputType) || IsPersistentSwitch(row.InputType))
                {
                    valueF.IsBoolean    = true;
                    valueF.BoolState    = GetSwitchState(row.Action);
                    valueF.CurrentValue = string.Empty;
                }
                else if (IsEnumType(row.InputType))
                {
                    valueF.IsBoolean    = false;
                    valueF.CurrentValue = ControlStateManager.GetEnumValue(row.Action) ?? string.Empty;
                }
                else
                {
                    valueF.IsBoolean    = false;
                    valueF.CurrentValue = string.Empty;
                }
            }

            funCol.Draw(spriteBatch, rowBounds, boldFont, _pixelTexture);
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, Rectangle contentBounds,
            float lineHeight, UIStyle.UIFont boldFont)
        {
            Rectangle dragBounds = _rowDragState.GetDragBounds(contentBounds, lineHeight);
            if (dragBounds == Rectangle.Empty) return;

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);

            KeybindDisplayRow row = _rowDragState.DraggingSnapshot;
            // Reuse or create a ghost funCol with equal widths (no hover animation)
            if (_dragGhostFunCol == null)
            {
                _dragGhostFunCol = new FunColInterface(
                    new float[] { 0.35f, 0.25f, 0.22f, 0.18f },
                    new TextLabelFeature("Action", FunColTextAlign.Right),
                    new TextLabelFeature("Key",    FunColTextAlign.Left),
                    new TextLabelFeature("Type",   FunColTextAlign.Left),
                    new CyclerFeature("Value") { TextAlign = FunColTextAlign.Center }
                );
                _dragGhostFunCol.DisableExpansion = true;
                _dragGhostFunCol.DisableColors = true;
                _dragGhostFunCol.SuppressTooltipWarnings = true;
            }

            if (_dragGhostFunCol.GetFeature(0) is TextLabelFeature gn) gn.Text = row.Action;
            if (_dragGhostFunCol.GetFeature(1) is TextLabelFeature gk) gk.Text = row.Input;
            if (_dragGhostFunCol.GetFeature(2) is TextLabelFeature gt) gt.Text = row.TypeLabel;
            if (_dragGhostFunCol.GetFeature(3) is CyclerFeature gv)
            {
                gv.IsLocked = row.ToggleLocked;
                if (IsSwitchType(row.InputType) || IsPersistentSwitch(row.InputType))
                {
                    gv.IsBoolean = true; gv.BoolState = GetSwitchState(row.Action);
                }
                else if (IsEnumType(row.InputType))
                {
                    gv.IsBoolean = false;
                    gv.CurrentValue = ControlStateManager.GetEnumValue(row.Action) ?? string.Empty;
                }
                else
                {
                    gv.IsBoolean = false;
                    gv.CurrentValue = string.Empty;
                }
            }

            _dragGhostFunCol.Draw(spriteBatch, dragBounds, boldFont, _pixelTexture);
        }

        private static bool TryGetCategoryLayout(string categoryKey, out CategoryLayoutRow layout)
        {
            for (int i = 0; i < _categoryLayoutRows.Count; i++)
            {
                if (string.Equals(_categoryLayoutRows[i].CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))
                {
                    layout = _categoryLayoutRows[i];
                    return true;
                }
            }

            layout = default;
            return false;
        }

        private static bool TryStartRowDrag(string rowKey, MouseState mouseState)
        {
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                return false;
            }

            int rowIndex = GetRowIndex(rowKey);
            if (rowIndex < 0)
            {
                return false;
            }

            string categoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(_keybindCache[rowIndex].CategoryKey, _keybindCache[rowIndex].Action);
            if (!TryGetCategoryLayout(categoryKey, out CategoryLayoutRow categoryLayout) || categoryLayout.RowsBounds == Rectangle.Empty)
            {
                return false;
            }

            _rowDragCategoryRows.Clear();
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (string.Equals(row.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))
                {
                    _rowDragCategoryRows.Add(row);
                }
            }

            if (_rowDragCategoryRows.Count == 0)
            {
                return false;
            }

            _activeRowDragCategoryKey = categoryKey;
            _activeRowDragBounds = categoryLayout.RowsBounds;
            _categoryDragState.Reset();
            _categoryDragRows.Clear();
            return _rowDragState.TryStartDrag(_rowDragCategoryRows, rowKey, mouseState);
        }

        private static void UpdateRowDrag(MouseState mouseState)
        {
            if (!_rowDragState.IsDragging || _rowDragCategoryRows.Count == 0)
            {
                return;
            }

            Rectangle dragBounds = _activeRowDragBounds == Rectangle.Empty
                ? _scrollPanel.ContentViewportBounds
                : _activeRowDragBounds;
            _rowDragState.UpdateDrag(_rowDragCategoryRows, dragBounds, _lineHeightCache, mouseState);
        }

        private static bool TryCompleteRowDrag()
        {
            if (_rowDragCategoryRows.Count == 0)
            {
                _rowDragState.Reset();
                _activeRowDragCategoryKey = null;
                _activeRowDragBounds = Rectangle.Empty;
                return false;
            }

            bool completed = _rowDragState.TryCompleteDrag(_rowDragCategoryRows, out bool orderChanged);
            if (!completed)
            {
                _rowDragCategoryRows.Clear();
                _activeRowDragCategoryKey = null;
                _activeRowDragBounds = Rectangle.Empty;
                return false;
            }

            if (orderChanged && !string.IsNullOrWhiteSpace(_activeRowDragCategoryKey))
            {
                for (int i = 0; i < _rowDragCategoryRows.Count; i++)
                {
                    string actionKey = _rowDragCategoryRows[i].Action;
                    int index = GetRowIndex(actionKey);
                    if (index < 0)
                    {
                        continue;
                    }

                    KeybindDisplayRow row = _keybindCache[index];
                    row.RenderOrder = i;
                    row.CategoryKey = _activeRowDragCategoryKey;
                    _keybindCache[index] = row;
                }
            }

            _rowDragCategoryRows.Clear();
            _activeRowDragCategoryKey = null;
            _activeRowDragBounds = Rectangle.Empty;
            return orderChanged;
        }

        private static bool TryStartCategoryDrag(string categoryKey, MouseState mouseState)
        {
            if (!_funColCategoryModeEnabled || string.IsNullOrWhiteSpace(categoryKey) || _categoryLayoutRows.Count == 0)
            {
                return false;
            }

            _categoryDragRows.Clear();
            _categoryDragRows.AddRange(_categoryLayoutRows);
            _rowDragState.Reset();
            _rowDragCategoryRows.Clear();
            _activeRowDragCategoryKey = null;
            _activeRowDragBounds = Rectangle.Empty;
            return _categoryDragState.TryStartDrag(_categoryDragRows, categoryKey, mouseState);
        }

        private static void UpdateCategoryDrag(Rectangle contentBounds, MouseState mouseState)
        {
            if (!_categoryDragState.IsDragging || _categoryDragRows.Count == 0)
            {
                return;
            }

            int dragHeight = Math.Max(1, _categoryDragState.DraggingSnapshot.Bounds.Height);
            _categoryDragState.UpdateDrag(_categoryDragRows, contentBounds, dragHeight, mouseState);
        }

        private static bool TryCompleteCategoryDrag()
        {
            if (_categoryDragRows.Count == 0)
            {
                _categoryDragState.Reset();
                return false;
            }

            bool completed = _categoryDragState.TryCompleteDrag(_categoryDragRows, out bool orderChanged);
            if (!completed)
            {
                _categoryDragRows.Clear();
                return false;
            }

            if (orderChanged)
            {
                for (int categoryOrder = 0; categoryOrder < _categoryDragRows.Count; categoryOrder++)
                {
                    string categoryKey = _categoryDragRows[categoryOrder].CategoryKey;
                    for (int i = 0; i < _keybindCache.Count; i++)
                    {
                        KeybindDisplayRow row = _keybindCache[i];
                        if (string.Equals(row.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))
                        {
                            row.CategoryOrder = categoryOrder;
                            _keybindCache[i] = row;
                        }
                    }
                }
            }

            _categoryDragRows.Clear();
            return orderChanged;
        }

        private static void DrawCategoryHeader(
            SpriteBatch spriteBatch,
            CategoryLayoutRow category,
            bool blockLocked,
            UIStyle.UIFont boldFont,
            bool pointerInsideList)
        {
            if (spriteBatch == null || _pixelTexture == null || category.HeaderBounds == Rectangle.Empty)
            {
                return;
            }

            bool categoryCollapsed = IsCategoryCollapsed(category.CategoryKey);
            bool hovered = !blockLocked &&
                !IsAnyDragActive &&
                !string.IsNullOrWhiteSpace(_hoveredCategoryKey) &&
                string.Equals(_hoveredCategoryKey, category.CategoryKey, StringComparison.OrdinalIgnoreCase);

            Color fill = hovered ? ColorPalette.RowHover * 0.85f : (UIStyle.BlockBackground * 0.72f);
            FillRect(spriteBatch, category.HeaderBounds, fill);
            DrawRectOutline(spriteBatch, category.HeaderBounds, UIStyle.BlockBorder * 0.85f, 1);

            if (boldFont.IsAvailable)
            {
                string label = string.IsNullOrWhiteSpace(category.Label)
                    ? category.CategoryKey
                    : category.Label;
                int labelX = category.HeaderBounds.X + CategoryLabelLeftPad + CategoryToggleBtnSize + CategoryToggleBtnPad + 2;
                boldFont.DrawString(
                    spriteBatch,
                    label,
                    new Vector2(labelX, category.HeaderBounds.Y + CategoryLabelTopPad),
                    UIStyle.TextColor);
            }

            bool showToggle = ShouldShowCategoryToggle(category.CategoryKey, blockLocked, pointerInsideList, hoveredHeader: category.HeaderBounds.Contains(Mouse.GetState().Position));
            if (!showToggle)
            {
                return;
            }

            Rectangle toggleBounds = GetCategoryToggleButtonRect(category.HeaderBounds);
            bool toggleHovered = !blockLocked && !IsAnyDragActive && toggleBounds.Contains(Mouse.GetState().Position);
            Color toggleFill = categoryCollapsed
                ? (toggleHovered ? ColorPalette.IndicatorInactive * 0.9f : ColorPalette.IndicatorInactive * 0.67f)
                : (toggleHovered ? ColorPalette.IndicatorActive * 0.9f : ColorPalette.IndicatorActive * 0.67f);
            FillRect(spriteBatch, toggleBounds, toggleFill);
            DrawRectOutline(spriteBatch, toggleBounds, ColorPalette.TextMuted * 0.55f, 1);
        }

        private static Rectangle GetCategoryToggleButtonRect(Rectangle headerBounds)
        {
            int by = headerBounds.Y + (headerBounds.Height - CategoryToggleBtnSize) / 2;
            return new Rectangle(headerBounds.X + CategoryToggleBtnPad, by, CategoryToggleBtnSize, CategoryToggleBtnSize);
        }

        private static bool ShouldShowCategoryToggle(string categoryKey, bool blockLocked, bool pointerInsideList, bool hoveredHeader)
        {
            if (!_funColCategoryModeEnabled || blockLocked || IsAnyDragActive || !BlockManager.DockingModeEnabled)
            {
                return false;
            }

            if (IsCategoryCollapsed(categoryKey))
            {
                return pointerInsideList;
            }

            return hoveredHeader;
        }

        private static void DrawDraggingCategory(SpriteBatch spriteBatch, Rectangle contentBounds, UIStyle.UIFont boldFont, bool blockLocked)
        {
            if (!_categoryDragState.HasSnapshot)
            {
                return;
            }

            CategoryLayoutRow draggingCategory = _categoryDragState.DraggingSnapshot;
            int dragHeight = Math.Max(1, draggingCategory.Bounds.Height);
            Rectangle dragBounds = _categoryDragState.GetDragBounds(contentBounds, dragHeight);
            if (dragBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);

            int headerHeight = Math.Max(1, draggingCategory.HeaderBounds.Height);
            Rectangle headerBounds = new Rectangle(dragBounds.X, dragBounds.Y, dragBounds.Width, headerHeight);
            DrawCategoryHeader(
                spriteBatch,
                new CategoryLayoutRow
                {
                    CategoryKey = draggingCategory.CategoryKey,
                    Label = draggingCategory.Label,
                    Bounds = dragBounds,
                    HeaderBounds = headerBounds,
                    RowsBounds = Rectangle.Empty,
                    IsCollapsed = false,
                    IsDragging = true
                },
                blockLocked,
                boldFont,
                pointerInsideList: false);

            float y = headerBounds.Bottom;
            int rowHeight = (int)MathF.Ceiling(_lineHeightCache);
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (!string.Equals(row.CategoryKey, draggingCategory.CategoryKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Rectangle rowBounds = new Rectangle(dragBounds.X, (int)MathF.Round(y), dragBounds.Width, rowHeight);
                DrawRowWithFunCol(spriteBatch, row, rowBounds, boldFont);
                y += _lineHeightCache;
            }
        }

        private static float GetListContentHeight()
        {
            if (_lineHeightCache <= 0f)
            {
                return 0f;
            }

            if (!_funColCategoryModeEnabled)
            {
                return _keybindCache.Count * _lineHeightCache;
            }

            int categoryCount = GetUsedCategoryCount();
            if (categoryCount <= 0)
            {
                return _keybindCache.Count * _lineHeightCache;
            }

            float totalRowsHeight = 0f;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                string categoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                if (!IsCategoryCollapsed(categoryKey))
                {
                    totalRowsHeight += _lineHeightCache;
                }
            }

            float headerHeight = GetCategoryHeaderHeight();
            float total = totalRowsHeight + (categoryCount * headerHeight);
            if (categoryCount > 1)
            {
                total += (categoryCount - 1) * CategorySectionGap;
            }

            return total;
        }

        private static float GetCategoryHeaderHeight()
        {
            return MathF.Max(12f, _lineHeightCache * 0.9f);
        }

        private static int GetUsedCategoryCount()
        {
            if (_keybindCache.Count == 0)
            {
                return 0;
            }

            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                string categoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                if (!string.IsNullOrWhiteSpace(categoryKey))
                {
                    categories.Add(categoryKey);
                }
            }

            return categories.Count;
        }

        private static void UpdateRowBounds(Rectangle contentBounds)
        {
            _categoryLayoutRows.Clear();

            if (_lineHeightCache <= 0f)
            {
                return;
            }

            int rowHeight = (int)MathF.Ceiling(_lineHeightCache);
            float y = contentBounds.Y - _scrollPanel.ScrollOffset;

            if (!_funColCategoryModeEnabled)
            {
                for (int i = 0; i < _keybindCache.Count; i++)
                {
                    KeybindDisplayRow row = _keybindCache[i];
                    row.Bounds = new Rectangle(contentBounds.X, (int)MathF.Round(y), contentBounds.Width, rowHeight);
                    row.CategoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                    row.CategoryOrder = 0;
                    _keybindCache[i] = row;
                    y += _lineHeightCache;
                }

                return;
            }

            var rowsByCategory = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var categorySortHints = new Dictionary<string, (int CategoryOrder, int FirstIndex)>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                KeybindDisplayRow row = _keybindCache[i];
                string categoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                int categoryOrder = row.CategoryOrder >= 0
                    ? row.CategoryOrder
                    : ControlRowCategoryCatalog.GetDefaultCategoryOrder(categoryKey);
                row.CategoryKey = categoryKey;
                row.CategoryOrder = categoryOrder;
                _keybindCache[i] = row;

                if (!rowsByCategory.TryGetValue(categoryKey, out List<int> categoryRows))
                {
                    categoryRows = new List<int>();
                    rowsByCategory[categoryKey] = categoryRows;
                    categorySortHints[categoryKey] = (categoryOrder, i);
                }

                categoryRows.Add(i);
            }

            List<string> orderedCategories = rowsByCategory.Keys.ToList();
            orderedCategories.Sort((a, b) =>
            {
                (int orderA, int indexA) = categorySortHints[a];
                (int orderB, int indexB) = categorySortHints[b];
                int cmp = orderA.CompareTo(orderB);
                if (cmp != 0) return cmp;

                cmp = indexA.CompareTo(indexB);
                if (cmp != 0) return cmp;

                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            _collapsedCategoryKeys.RemoveWhere(key => !orderedCategories.Contains(key, StringComparer.OrdinalIgnoreCase));

            int headerHeightPx = (int)MathF.Ceiling(GetCategoryHeaderHeight());
            for (int categoryIndex = 0; categoryIndex < orderedCategories.Count; categoryIndex++)
            {
                string categoryKey = orderedCategories[categoryIndex];
                List<int> categoryRows = rowsByCategory[categoryKey];
                bool categoryCollapsed = IsCategoryCollapsed(categoryKey);

                Rectangle headerBounds = new(
                    contentBounds.X,
                    (int)MathF.Round(y),
                    contentBounds.Width,
                    headerHeightPx);

                y += GetCategoryHeaderHeight();
                int rowStartY = (int)MathF.Round(y);

                if (categoryCollapsed)
                {
                    foreach (int rowIndex in categoryRows)
                    {
                        KeybindDisplayRow row = _keybindCache[rowIndex];
                        row.Bounds = Rectangle.Empty;
                        _keybindCache[rowIndex] = row;
                    }
                }
                else
                {
                    foreach (int rowIndex in categoryRows)
                    {
                        KeybindDisplayRow row = _keybindCache[rowIndex];
                        row.Bounds = new Rectangle(contentBounds.X, (int)MathF.Round(y), contentBounds.Width, rowHeight);
                        _keybindCache[rowIndex] = row;
                        y += _lineHeightCache;
                    }
                }

                int rowBottomY = (int)MathF.Round(y);
                Rectangle rowBounds = !categoryCollapsed && categoryRows.Count > 0
                    ? new Rectangle(contentBounds.X, rowStartY, contentBounds.Width, Math.Max(rowHeight, rowBottomY - rowStartY))
                    : Rectangle.Empty;
                Rectangle categoryBounds = new Rectangle(
                    contentBounds.X,
                    headerBounds.Y,
                    contentBounds.Width,
                    categoryCollapsed
                        ? headerBounds.Height
                        : Math.Max(headerBounds.Height, rowBottomY - headerBounds.Y));

                _categoryLayoutRows.Add(new CategoryLayoutRow
                {
                    CategoryKey = categoryKey,
                    Label = ControlRowCategoryCatalog.GetCategoryLabel(categoryKey),
                    Bounds = categoryBounds,
                    HeaderBounds = headerBounds,
                    RowsBounds = rowBounds,
                    IsCollapsed = categoryCollapsed,
                    IsDragging = false
                });

                if (categoryIndex < orderedCategories.Count - 1)
                {
                    y += CategorySectionGap;
                }
            }
        }

        private static bool HasDragExceededThreshold(Point position)
        {
            int deltaX = Math.Abs(position.X - _pressStartPosition.X);
            int deltaY = Math.Abs(position.Y - _pressStartPosition.Y);
            return deltaX >= DragStartThreshold || deltaY >= DragStartThreshold;
        }

        private static bool IsCategoryCollapsed(string categoryKey)
        {
            if (!_funColCategoryModeEnabled || string.IsNullOrWhiteSpace(categoryKey))
            {
                return false;
            }

            return _collapsedCategoryKeys.Contains(categoryKey);
        }

        private static void ToggleCategoryCollapsed(string categoryKey)
        {
            if (!_funColCategoryModeEnabled || string.IsNullOrWhiteSpace(categoryKey))
            {
                return;
            }

            if (_collapsedCategoryKeys.Contains(categoryKey))
            {
                _collapsedCategoryKeys.Remove(categoryKey);
            }
            else
            {
                _collapsedCategoryKeys.Add(categoryKey);
            }
        }

        private static bool ShouldHighlightRow(KeybindDisplayRow row, bool blockLocked)
        {
            return !blockLocked &&
                !IsAnyDragActive &&
                !string.IsNullOrWhiteSpace(_hoveredRowKey) &&
                string.Equals(_hoveredRowKey, row.Action, StringComparison.OrdinalIgnoreCase);
        }

        private static FunColInterface GetOrCreateRowFunCol(string action)
        {
            if (_rowFunCols.TryGetValue(action, out var existing)) return existing;
            float[] weights = _headerFunCol?.GetWeights() ?? new float[] { 0.35f, 0.25f, 0.22f, 0.18f };
            var fc = new FunColInterface(
                weights,
                new TextLabelFeature("Action", FunColTextAlign.Right),
                new TextLabelFeature("Key",    FunColTextAlign.Left),
                new TextLabelFeature("Type",   FunColTextAlign.Left),
                new CyclerFeature("Value") { TextAlign = FunColTextAlign.Center }
            );
            fc.DisableExpansion = true;
            fc.DisableColors = true;
            fc.RowDragEnabled = true;
            fc.SuppressTooltipWarnings = true;
            _rowFunCols[action] = fc;
            return fc;
        }

        private static UIDropdown GetOrCreateEnumDropdown(string settingKey)
        {
            if (_enumDropdowns.TryGetValue(settingKey, out UIDropdown existing))
            {
                return existing;
            }

            var dropdown = new UIDropdown
            {
                ShowOptionDisableToggles = true,
                PreventSelectingDisabledOptions = true
            };
            _enumDropdowns[settingKey] = dropdown;
            return dropdown;
        }

        private static void SyncEnumDropdowns()
        {
            HashSet<string> enumKeys = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (!IsEnumType(row.InputType))
                {
                    continue;
                }

                enumKeys.Add(row.Action);
                UIDropdown dropdown = GetOrCreateEnumDropdown(row.Action);
                dropdown.Bounds = row.EnumValueBounds;

                string[] options = ControlStateManager.GetEnumOptions(row.Action);
                IEnumerable<UIDropdown.Option> dropdownOptions = options.Select(option =>
                {
                    bool isDisabled = ControlStateManager.IsEnumOptionDisabled(row.Action, option);
                    string tooltipKey = isDisabled ? EnumOptionTooltipKeyDisabled : EnumOptionTooltipKeyEnabled;
                    return new UIDropdown.Option(option, option, isDisabled, tooltipKey, option);
                });
                string selected = ControlStateManager.GetEnumValue(row.Action);
                dropdown.SetOptions(dropdownOptions, selected);
            }

            List<string> staleKeys = new();
            foreach (string existingKey in _enumDropdowns.Keys)
            {
                if (!enumKeys.Contains(existingKey))
                {
                    staleKeys.Add(existingKey);
                }
            }

            foreach (string stale in staleKeys)
            {
                _enumDropdowns.Remove(stale);
            }

            if (!string.IsNullOrWhiteSpace(_openEnumDropdownKey) && !_enumDropdowns.ContainsKey(_openEnumDropdownKey))
            {
                _openEnumDropdownKey = null;
            }
        }

        private static void UpdateEnumDropdownBounds()
        {
            for (int i = 0; i < _keybindCache.Count; i++)
            {
                KeybindDisplayRow row = _keybindCache[i];
                if (!IsEnumType(row.InputType) || row.Bounds == Rectangle.Empty || !_rowFunCols.TryGetValue(row.Action, out FunColInterface rowFunCol))
                {
                    row.EnumValueBounds = Rectangle.Empty;
                    _keybindCache[i] = row;
                    continue;
                }

                Rectangle valueBounds = rowFunCol.GetColumnBounds(3, row.Bounds);
                if (valueBounds == Rectangle.Empty)
                {
                    row.EnumValueBounds = Rectangle.Empty;
                    _keybindCache[i] = row;
                    continue;
                }

                const int horizontalPad = 3;
                const int verticalPad = 2;
                int width = Math.Max(0, valueBounds.Width - (horizontalPad * 2));
                int height = Math.Max(0, valueBounds.Height - (verticalPad * 2));
                row.EnumValueBounds = width <= 0 || height <= 0
                    ? Rectangle.Empty
                    : new Rectangle(valueBounds.X + horizontalPad, valueBounds.Y + verticalPad, width, height);
                _keybindCache[i] = row;
            }
        }

        private static bool UpdateEnumDropdownInteractions(
            MouseState mouseState,
            MouseState previousMouseState,
            KeyboardState keyboardState,
            bool blockLocked)
        {
            if (blockLocked)
            {
                CloseAllEnumDropdowns();
                _openEnumDropdownKey = null;
                return false;
            }

            bool enumStateChanged = false;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (!IsEnumType(row.InputType) || !_enumDropdowns.TryGetValue(row.Action, out UIDropdown dropdown))
                {
                    continue;
                }

                bool isDisabled = blockLocked || IsAnyDragActive || row.ToggleLocked || row.EnumValueBounds == Rectangle.Empty;
                dropdown.Update(
                    mouseState,
                    previousMouseState,
                    keyboardState,
                    _previousKeyboardState,
                    out string selectedId,
                    out string toggledId,
                    out bool toggledDisabledState,
                    isDisabled);

                if (!string.IsNullOrWhiteSpace(toggledId))
                {
                    if (ControlStateManager.TrySetEnumOptionDisabled(row.Action, toggledId, toggledDisabledState, "ControlsBlock.EnumOptionDisableToggle"))
                    {
                        enumStateChanged = true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    if (ControlStateManager.TrySetEnumValue(row.Action, selectedId, "ControlsBlock.EnumDropdownSelection"))
                    {
                        enumStateChanged = true;
                    }
                }

                if (dropdown.IsOpen)
                {
                    if (!string.Equals(_openEnumDropdownKey, row.Action, StringComparison.OrdinalIgnoreCase))
                    {
                        CloseAllEnumDropdowns(row.Action);
                    }

                    _openEnumDropdownKey = row.Action;
                }
                else if (string.Equals(_openEnumDropdownKey, row.Action, StringComparison.OrdinalIgnoreCase))
                {
                    _openEnumDropdownKey = null;
                }
            }

            if (!string.IsNullOrWhiteSpace(_openEnumDropdownKey) &&
                (!_enumDropdowns.TryGetValue(_openEnumDropdownKey, out UIDropdown openDropdown) || !openDropdown.IsOpen))
            {
                _openEnumDropdownKey = null;
            }

            return enumStateChanged;
        }

        private static void CloseAllEnumDropdowns(string exemptSettingKey = null)
        {
            foreach (KeyValuePair<string, UIDropdown> entry in _enumDropdowns)
            {
                if (!string.IsNullOrWhiteSpace(exemptSettingKey) &&
                    string.Equals(entry.Key, exemptSettingKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry.Value.Close();
            }
        }

        private static bool TryGetHoveredEnumDropdownTooltip(Point pointer, out string tooltipKey, out string tooltipLabel)
        {
            tooltipKey = null;
            tooltipLabel = null;

            if (string.IsNullOrWhiteSpace(_openEnumDropdownKey) ||
                !_enumDropdowns.TryGetValue(_openEnumDropdownKey, out UIDropdown dropdown) ||
                !dropdown.IsOpen ||
                !dropdown.IsPointerOverDropdown(pointer))
            {
                return false;
            }

            tooltipKey = dropdown.GetHoveredOptionTooltipKey();
            tooltipLabel = dropdown.GetHoveredOptionTooltipLabel();
            if (!string.IsNullOrWhiteSpace(tooltipKey))
            {
                return true;
            }

            if (dropdown.TryGetHoveredOption(out UIDropdown.Option hoveredOption))
            {
                tooltipKey = hoveredOption.IsDisabled ? EnumOptionTooltipKeyDisabled : EnumOptionTooltipKeyEnabled;
                return true;
            }

            tooltipKey = EnumOptionTooltipKeyEnabled;
            return true;
        }

        private static void DrawEnumDropdown(SpriteBatch spriteBatch, KeybindDisplayRow row, bool blockLocked)
        {
            if (!IsEnumType(row.InputType) ||
                !_enumDropdowns.TryGetValue(row.Action, out UIDropdown dropdown) ||
                row.EnumValueBounds == Rectangle.Empty)
            {
                return;
            }

            dropdown.Bounds = row.EnumValueBounds;
            bool isDisabled = blockLocked || IsAnyDragActive || row.ToggleLocked;
            if (isDisabled)
            {
                dropdown.Close();
            }

            dropdown.Draw(spriteBatch, drawOptions: false, isDisabled: isDisabled);
        }

        private static void DrawEnumDropdownOptionOverlays(SpriteBatch spriteBatch, bool blockLocked)
        {
            if (spriteBatch == null || blockLocked || IsAnyDragActive)
            {
                return;
            }

            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (!IsEnumType(row.InputType) || row.ToggleLocked || row.EnumValueBounds == Rectangle.Empty)
                {
                    continue;
                }

                if (_enumDropdowns.TryGetValue(row.Action, out UIDropdown dropdown))
                {
                    dropdown.DrawOptionsOverlay(spriteBatch);
                }
            }
        }

        private static FunColInterface _headerFunCol;
        private static FunColInterface GetOrEnsureHeaderFunCol()
        {
            if (_headerFunCol != null) return _headerFunCol;
            _headerFunCol = new FunColInterface(
                new float[] { 0.35f, 0.25f, 0.22f, 0.18f },
                new TextLabelFeature("Action", FunColTextAlign.Right)
                    { HeaderTooltipTexts = ["Name of the game action"] },
                new TextLabelFeature("Key",    FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Keybinding assigned to this action"] },
                new TextLabelFeature("Type",   FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Input type: Hold, Switch, Trigger, or Float"] },
                new CyclerFeature("Value") { TextAlign = FunColTextAlign.Center,
                    HeaderTooltipTexts = ["Current state or value"] }
            );
            _headerFunCol.DisableExpansion = true;
            _headerFunCol.DisableColors = true;
            _headerFunCol.ShowHeaderTooltips = true;
            _headerFunCol.EnableColumnResize = true;
            return _headerFunCol;
        }

        private static void PropagateWeightsToRowFunCols(float[] weights)
        {
            foreach (var fc in _rowFunCols.Values)
                fc.SetWeights(weights);
            _dragGhostFunCol?.SetWeights(weights);
        }

        private static string EncodeWeights(float[] weights)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < weights.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(weights[i].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static void ApplyEncodedWeights(FunColInterface fc, string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return;
            string[] parts = encoded.Split('|');
            float[] weights = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out weights[i]))
                    return;
            }
            fc.SetWeights(weights);
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
                int indicatorStart = row.Bounds.Right - TypeIndicatorDiameter - 4;
                if (x + width > indicatorStart - 4)
                {
                    width = Math.Max(0, indicatorStart - 4 - x);
                }

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

        private static string HitTestCategoryHeader(Point position)
        {
            if (!_funColCategoryModeEnabled)
            {
                return null;
            }

            foreach (CategoryLayoutRow category in _categoryLayoutRows)
            {
                if (category.HeaderBounds != Rectangle.Empty && category.HeaderBounds.Contains(position))
                {
                    return category.CategoryKey;
                }
            }

            return null;
        }

        private static string HitTestCategoryToggle(Point position, bool blockLocked, bool pointerInsideList)
        {
            if (!_funColCategoryModeEnabled)
            {
                return null;
            }

            foreach (CategoryLayoutRow category in _categoryLayoutRows)
            {
                if (category.HeaderBounds == Rectangle.Empty)
                {
                    continue;
                }

                bool hoveredHeader = category.HeaderBounds.Contains(position);
                if (!ShouldShowCategoryToggle(category.CategoryKey, blockLocked, pointerInsideList, hoveredHeader))
                {
                    continue;
                }

                Rectangle toggleBounds = GetCategoryToggleButtonRect(category.HeaderBounds);
                if (toggleBounds.Contains(position))
                {
                    return category.CategoryKey;
                }
            }

            return null;
        }

        private static string HitTestTypeToggle(Point position, Rectangle contentBounds, out bool indicatorArea)
        {
            indicatorArea = false;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                // Check type toggle label (not for persistent switches or empty bounds)
                if (!IsPersistentSwitch(row.InputType) && row.TypeToggleBounds != Rectangle.Empty)
                {
                    if (row.TypeToggleBounds.Contains(position))
                        return row.Action;
                }

                // Check indicator dot for switches and Trigger autofire toggle
                if (_lineHeightCache > 0f && row.Bounds != Rectangle.Empty)
                {
                    bool checkIndicator = IsSwitchType(row.InputType) ||
                                         (row.InputType == InputType.Trigger && row.IsToggleCandidate);
                    if (checkIndicator)
                    {
                        int indicatorX = Math.Max(contentBounds.X, contentBounds.Right - TypeIndicatorDiameter - 4);
                        int indicatorH = row.Bounds.Height > 0 ? row.Bounds.Height : (int)MathF.Ceiling(_lineHeightCache);
                        Rectangle indicatorBounds = new(indicatorX, row.Bounds.Y, contentBounds.Right - indicatorX, indicatorH);
                        if (indicatorBounds.Contains(position))
                        {
                            indicatorArea = true;
                            return row.Action;
                        }
                    }
                }
            }

            return null;
        }

        private static void NormalizeCacheOrder()
        {
            if (_keybindCache.Count == 0)
            {
                return;
            }

            if (!_funColCategoryModeEnabled)
            {
                for (int i = 0; i < _keybindCache.Count; i++)
                {
                    KeybindDisplayRow row = _keybindCache[i];
                    row.CategoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                    row.CategoryOrder = 0;
                    row.RenderOrder = i;
                    _keybindCache[i] = row;
                }

                return;
            }

            var rowsByCategory = new Dictionary<string, List<KeybindDisplayRow>>(StringComparer.OrdinalIgnoreCase);
            var categorySortHints = new Dictionary<string, (int CategoryOrder, int FirstIndex)>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                KeybindDisplayRow row = _keybindCache[i];
                string categoryKey = ControlRowCategoryCatalog.NormalizeCategoryKey(row.CategoryKey, row.Action);
                int categoryOrder = row.CategoryOrder >= 0
                    ? row.CategoryOrder
                    : ControlRowCategoryCatalog.GetDefaultCategoryOrder(categoryKey);

                row.CategoryKey = categoryKey;
                row.CategoryOrder = categoryOrder;

                if (!rowsByCategory.TryGetValue(categoryKey, out List<KeybindDisplayRow> rows))
                {
                    rows = new List<KeybindDisplayRow>();
                    rowsByCategory[categoryKey] = rows;
                    categorySortHints[categoryKey] = (categoryOrder, i);
                }

                rows.Add(row);
            }

            List<string> orderedCategories = rowsByCategory.Keys.ToList();
            orderedCategories.Sort((a, b) =>
            {
                (int orderA, int firstIndexA) = categorySortHints[a];
                (int orderB, int firstIndexB) = categorySortHints[b];
                int cmp = orderA.CompareTo(orderB);
                if (cmp != 0) return cmp;

                cmp = firstIndexA.CompareTo(firstIndexB);
                if (cmp != 0) return cmp;

                cmp = ControlRowCategoryCatalog.GetDefaultCategoryOrder(a).CompareTo(ControlRowCategoryCatalog.GetDefaultCategoryOrder(b));
                if (cmp != 0) return cmp;

                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            var normalizedRows = new List<KeybindDisplayRow>(_keybindCache.Count);
            for (int categoryOrder = 0; categoryOrder < orderedCategories.Count; categoryOrder++)
            {
                string categoryKey = orderedCategories[categoryOrder];
                List<KeybindDisplayRow> categoryRows = rowsByCategory[categoryKey];
                categoryRows.Sort((a, b) =>
                {
                    int cmp = a.RenderOrder.CompareTo(b.RenderOrder);
                    if (cmp != 0) return cmp;

                    return string.Compare(a.Action, b.Action, StringComparison.OrdinalIgnoreCase);
                });

                for (int rowOrder = 0; rowOrder < categoryRows.Count; rowOrder++)
                {
                    KeybindDisplayRow row = categoryRows[rowOrder];
                    row.CategoryKey = categoryKey;
                    row.CategoryOrder = categoryOrder;
                    row.RenderOrder = rowOrder;
                    normalizedRows.Add(row);
                }
            }

            _keybindCache.Clear();
            _keybindCache.AddRange(normalizedRows);
        }

        private static int CompareRowsByCategoryThenOrder(KeybindDisplayRow a, KeybindDisplayRow b)
        {
            string categoryA = ControlRowCategoryCatalog.NormalizeCategoryKey(a.CategoryKey, a.Action);
            string categoryB = ControlRowCategoryCatalog.NormalizeCategoryKey(b.CategoryKey, b.Action);
            int categoryOrderA = a.CategoryOrder >= 0 ? a.CategoryOrder : ControlRowCategoryCatalog.GetDefaultCategoryOrder(categoryA);
            int categoryOrderB = b.CategoryOrder >= 0 ? b.CategoryOrder : ControlRowCategoryCatalog.GetDefaultCategoryOrder(categoryB);

            int cmp = categoryOrderA.CompareTo(categoryOrderB);
            if (cmp != 0) return cmp;

            cmp = string.Compare(categoryA, categoryB, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;

            cmp = a.RenderOrder.CompareTo(b.RenderOrder);
            if (cmp != 0) return cmp;

            return string.Compare(a.Action, b.Action, StringComparison.OrdinalIgnoreCase);
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
            if (IsPersistentSwitch(row.InputType))
            {
                if (clickedIndicator && ControlStateManager.ContainsSwitchState(settingKey))
                {
                    bool newState = !ControlStateManager.GetSwitchState(settingKey);
                    if (!InputTypeManager.OverrideSwitchState(settingKey, newState))
                        ControlStateManager.SetSwitchState(settingKey, newState, "ControlsBlock.TryToggleInputType");
                    return true;
                }
                return false;
            }

            if (row.InputType == InputType.Trigger)
            {
                return false;
            }

            if (row.ToggleLocked)
            {
                return false;
            }

            if (IsSwitchType(row.InputType) && clickedIndicator)
            {
                if (ControlStateManager.ContainsSwitchState(settingKey))
                {
                    bool newState = !ControlStateManager.GetSwitchState(settingKey);
                    if (!InputTypeManager.OverrideSwitchState(settingKey, newState))
                        ControlStateManager.SetSwitchState(settingKey, newState, "ControlsBlock.TryToggleInputType");
                    return true;
                }
                return false;
            }

            if (IsEnumType(row.InputType))
            {
                ControlStateManager.CycleEnum(row.Action);
                return true;
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
                nextType = InputType.DoubleTapToggle;
                triggerAuto = false;
            }
            else if (row.InputType == InputType.DoubleTapToggle)
            {
                nextType = InputType.Hold;
                triggerAuto = false;
            }

            row.InputType = nextType;
            row.TypeLabel = nextType.ToString();
            row.TriggerAutoFire = triggerAuto;
            _keybindCache[index] = row;

            string serialized = SerializeRowData(nextType, triggerAuto);
            BlockDataStore.SetRowData(DockBlockKind.Controls, row.Action, serialized);
            ControlKeyData.SetInputType(row.Action, nextType.ToString());
            InputManager.UpdateBindingInputType(row.Action, nextType, triggerAuto);

            return true;
        }

        private static bool GetSwitchState(string settingKey)
        {
            bool liveState = InputManager.IsInputActive(settingKey);
            if (ControlStateManager.ContainsSwitchState(settingKey))
            {
                return liveState || ControlStateManager.GetSwitchState(settingKey);
            }

            return liveState;
        }

        // ── Float widget helpers ──────────────────────────────────────────────

        private static void DrawFloatWidget(SpriteBatch sb, KeybindDisplayRow row, Rectangle rowBounds, UIStyle.UIFont font, bool blockLocked = false)
        {
            if (_pixelTexture == null) return;
            var funCol = GetOrCreateRowFunCol(row.Action);
            Rectangle col3 = funCol.GetColumnBounds(3, rowBounds);
            if (col3.Width < FloatArrowW * 2 + FloatInputW + 8) return;

            float lockedAlpha = blockLocked ? 0.4f : 1.0f;
            Color btnBg = ColorPalette.ButtonNeutral * lockedAlpha;
            Color inputBg = ColorPalette.ChatInputField * lockedAlpha;
            Color border = UIStyle.BlockBorder * lockedAlpha;
            Color text = UIStyle.TextColor * lockedAlpha;

            int arrowY = col3.Y + (col3.Height - FloatWidgetH) / 2;
            int fx = col3.X + 3;

            // [-] button
            var downRect = new Rectangle(fx, arrowY, FloatArrowW, FloatWidgetH);
            FillRect(sb, downRect, btnBg);
            DrawRectOutline(sb, downRect, border, 1);
            if (font.IsAvailable)
                font.DrawString(sb, "-", new Vector2(downRect.X + FloatArrowW / 2f - 3f, arrowY + 1f), text);
            fx += FloatArrowW + 2;

            // Textbox
            string displayVal = (!blockLocked && string.Equals(_editingFloatKey, row.Action, StringComparison.OrdinalIgnoreCase))
                ? _floatInputBuffer
                : ControlStateManager.GetFloat(row.Action, 1.0f).ToString("F2");
            var inputRect = new Rectangle(fx, arrowY, FloatInputW, FloatWidgetH);
            FillRect(sb, inputRect, inputBg);
            Color activeBorder = (!blockLocked && string.Equals(_editingFloatKey, row.Action, StringComparison.OrdinalIgnoreCase))
                ? UIStyle.AccentColor : border;
            DrawRectOutline(sb, inputRect, activeBorder, 1);
            if (font.IsAvailable)
                font.DrawString(sb, displayVal,
                    new Vector2(fx + 3f, arrowY + (FloatWidgetH - font.LineHeight) / 2f), text);
            fx += FloatInputW + 2;

            // [+] button
            var upRect = new Rectangle(fx, arrowY, FloatArrowW, FloatWidgetH);
            FillRect(sb, upRect, btnBg);
            DrawRectOutline(sb, upRect, border, 1);
            if (font.IsAvailable)
                font.DrawString(sb, "+", new Vector2(upRect.X + FloatArrowW / 2f - 3f, arrowY + 1f), text);
        }

        private static void HandleFloatWidgetClick(Point mousePos, KeybindDisplayRow row)
        {
            if (row.Bounds == Rectangle.Empty) return;
            var funCol = GetOrCreateRowFunCol(row.Action);
            Rectangle col3 = funCol.GetColumnBounds(3, row.Bounds);
            if (col3.Width < FloatArrowW * 2 + FloatInputW + 8) return;

            int arrowY = col3.Y + (col3.Height - FloatWidgetH) / 2;
            int fx = col3.X + 3;

            var downRect  = new Rectangle(fx, arrowY, FloatArrowW, FloatWidgetH);
            fx += FloatArrowW + 2;
            var inputRect = new Rectangle(fx, arrowY, FloatInputW, FloatWidgetH);
            fx += FloatInputW + 2;
            var upRect    = new Rectangle(fx, arrowY, FloatArrowW, FloatWidgetH);

            if (downRect.Contains(mousePos))
            {
                float cur = ControlStateManager.GetFloat(row.Action, 1.0f);
                ControlStateManager.SetFloat(row.Action, MathF.Max(FloatMin, MathF.Round(cur - FloatStep, 2)));
                RefreshFloatCacheValue(row.Action);
            }
            else if (upRect.Contains(mousePos))
            {
                float cur = ControlStateManager.GetFloat(row.Action, 1.0f);
                ControlStateManager.SetFloat(row.Action, MathF.Round(cur + FloatStep, 2));
                RefreshFloatCacheValue(row.Action);
            }
            else if (inputRect.Contains(mousePos))
            {
                if (string.Equals(_editingFloatKey, row.Action, StringComparison.OrdinalIgnoreCase))
                {
                    CommitFloatInput();
                    _editingFloatKey = null;
                }
                else
                {
                    _editingFloatKey = row.Action;
                    _floatInputBuffer = ControlStateManager.GetFloat(row.Action, 1.0f).ToString("F2");
                }
            }
        }

        private static void CommitFloatInput()
        {
            if (string.IsNullOrWhiteSpace(_editingFloatKey) || string.IsNullOrWhiteSpace(_floatInputBuffer)) return;
            if (!float.TryParse(_floatInputBuffer.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float val)) return;
            val = MathF.Max(FloatMin, MathF.Round(val, 2));
            ControlStateManager.SetFloat(_editingFloatKey, val);
            RefreshFloatCacheValue(_editingFloatKey);
        }

        private static void RefreshFloatCacheValue(string settingKey)
        {
            int idx = GetRowIndex(settingKey);
            if (idx < 0) return;
            var row = _keybindCache[idx];
            row.Input = ControlStateManager.GetFloat(settingKey, 1.0f).ToString("F2");
            _keybindCache[idx] = row;
        }

        private static char? FloatKeyToChar(Keys key, KeyboardState ks, string current)
        {
            return key switch
            {
                Keys.D0 or Keys.NumPad0 => '0',
                Keys.D1 or Keys.NumPad1 => '1',
                Keys.D2 or Keys.NumPad2 => '2',
                Keys.D3 or Keys.NumPad3 => '3',
                Keys.D4 or Keys.NumPad4 => '4',
                Keys.D5 or Keys.NumPad5 => '5',
                Keys.D6 or Keys.NumPad6 => '6',
                Keys.D7 or Keys.NumPad7 => '7',
                Keys.D8 or Keys.NumPad8 => '8',
                Keys.D9 or Keys.NumPad9 => '9',
                Keys.OemPeriod or Keys.Decimal when !current.Contains('.') => '.',
                _ => null
            };
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

        private static string TruncateWithEllipsis(string text, float maxWidth, UIStyle.UIFont font)
        {
            if (maxWidth <= 0f || string.IsNullOrEmpty(text))
                return string.Empty;

            if (font.MeasureString(text).X <= maxWidth)
                return text;

            const string ellipsis = "...";
            float ellipsisWidth = font.MeasureString(ellipsis).X;

            if (ellipsisWidth >= maxWidth)
                return string.Empty;

            int lo = 0, hi = text.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (font.MeasureString(text[..mid]).X + ellipsisWidth <= maxWidth)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return lo == 0 ? string.Empty : text[..lo] + ellipsis;
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
            _rebindCancelHovered = UIButtonRenderer.IsHovered(_rebindCancelButtonBounds, mouseState.Position);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            bool pointerOnConfirm = _rebindConfirmHovered;
            bool pointerOnUnbind = _rebindUnbindHovered;
            bool pointerOnCancel = _rebindCancelHovered;

            if (leftReleased && pointerOnCancel)
            {
                ResetRebindOverlay();
                return;
            }

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

            FillRect(spriteBatch, viewport, ColorPalette.RebindScrim);
            FillRect(spriteBatch, _rebindModalBounds, UIStyle.BlockBackground);
            DrawRectOutline(spriteBatch, _rebindModalBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            string title = string.IsNullOrWhiteSpace(_rebindAction) ? "Rebind keybind" : $"Rebind {_rebindAction}";
            Vector2 titleSize = headerFont.MeasureString(title);
            Vector2 titlePosition = new(_rebindModalBounds.X + (_rebindModalBounds.Width - titleSize.X) / 2f, _rebindModalBounds.Y + 12);
            headerFont.DrawString(spriteBatch, title, titlePosition, UIStyle.TextColor);

            if (_rebindCancelButtonBounds != Rectangle.Empty)
            {
                UIButtonRenderer.Draw(
                    spriteBatch,
                    _rebindCancelButtonBounds,
                    "X",
                    UIButtonRenderer.ButtonStyle.Grey,
                    _rebindCancelHovered,
                    isDisabled: false,
                    textColorOverride: _rebindCancelHovered ? ColorPalette.CloseGlyphHover : ColorPalette.CloseGlyph,
                    fillOverride: ColorPalette.CloseOverlayBackground,
                    hoverFillOverride: ColorPalette.CloseOverlayHoverBackground,
                    borderOverride: ColorPalette.CloseOverlayBorder);
            }

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
                bodyFont.DrawString(spriteBatch, _rebindConflictWarning, new Vector2(textX, textY), ColorPalette.Warning);
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

            _rowDragState.Reset();
            _categoryDragState.Reset();
            _pressedDragRowKey   = null;
            _pressedCategoryDragKey = null;
            _pressedRebindRowKey = null;
            _rowDragCategoryRows.Clear();
            _categoryDragRows.Clear();
            _activeRowDragBounds = Rectangle.Empty;
            _activeRowDragCategoryKey = null;
            CloseAllEnumDropdowns();
            _openEnumDropdownKey = null;

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
            _rebindCancelButtonBounds = Rectangle.Empty;
            _rebindConfirmHovered = false;
            _rebindUnbindHovered = false;
            _rebindCancelHovered = false;
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
            _rebindCancelButtonBounds = Rectangle.Empty;
            _rebindConflictWarning = null;
            _rebindConfirmHovered = false;
            _rebindUnbindHovered = false;
            _rebindCancelHovered = false;
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
            const int cancelSize = 24;
            _rebindCancelButtonBounds = new Rectangle(_rebindModalBounds.Right - cancelSize - 10, _rebindModalBounds.Y + 10, cancelSize, cancelSize);
        }

        private static Rectangle GetViewportBounds()
        {
            return BlockManager.GetVirtualViewport();
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

        private static string HitTestEnumValue(Point position)
        {
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (row.EnumValueBounds != Rectangle.Empty && row.EnumValueBounds.Contains(position))
                    return row.Action;
            }

            return null;
        }

        private static void UpdateEnumValueBounds(UIStyle.UIFont regularFont)
        {
            if (_lineHeightCache <= 0f || !regularFont.IsAvailable)
                return;

            for (int i = 0; i < _keybindCache.Count; i++)
            {
                var row = _keybindCache[i];
                if (!IsEnumType(row.InputType) || row.Bounds == Rectangle.Empty)
                {
                    row.EnumValueBounds = Rectangle.Empty;
                    _keybindCache[i] = row;
                    continue;
                }

                string enumValue = ControlStateManager.GetEnumValue(row.Action);
                if (string.IsNullOrEmpty(enumValue))
                {
                    row.EnumValueBounds = Rectangle.Empty;
                    _keybindCache[i] = row;
                    continue;
                }

                Vector2 enumTextSize = regularFont.MeasureString(enumValue);
                int x = (int)MathF.Floor(row.Bounds.Right - enumTextSize.X - 4) - ValueHighlightPadding;
                int y = row.Bounds.Y;
                int width = (int)MathF.Ceiling(enumTextSize.X) + (ValueHighlightPadding * 2);
                int height = row.Bounds.Height > 0 ? row.Bounds.Height : (int)MathF.Ceiling(_lineHeightCache);

                row.EnumValueBounds = new Rectangle(x, y, width, height);
                _keybindCache[i] = row;
            }
        }

        private static string GetReleasedMouseButton(MouseState mouseState, MouseState previousMouseState)
        {
            int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (scrollDelta > 0)
            {
                return "ScrollUp";
            }

            if (scrollDelta < 0)
            {
                return "ScrollDown";
            }

            if (mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed)
            {
                return "LeftClick";
            }

            if (mouseState.RightButton == ButtonState.Released && previousMouseState.RightButton == ButtonState.Pressed)
            {
                return "RightClick";
            }

            if (mouseState.MiddleButton == ButtonState.Released && previousMouseState.MiddleButton == ButtonState.Pressed)
            {
                return "MiddleClick";
            }

            if (mouseState.XButton1 == ButtonState.Released && previousMouseState.XButton1 == ButtonState.Pressed)
            {
                return "Mouse4";
            }

            if (mouseState.XButton2 == ButtonState.Released && previousMouseState.XButton2 == ButtonState.Pressed)
            {
                return "Mouse5";
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
                else if (string.Equals(token, "MiddleClick", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Middle Click";
                }
                else if (string.Equals(token, "Mouse4", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Mouse 4";
                }
                else if (string.Equals(token, "Mouse5", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Mouse 5";
                }
                else if (string.Equals(token, "ScrollUp", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Scroll Up";
                }
                else if (string.Equals(token, "ScrollDown", StringComparison.OrdinalIgnoreCase))
                {
                    token = "Scroll Down";
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
                _rebindConflictWarning = "Duplicate binding: also bound to " + string.Join(", ", conflicts);
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

        private struct CategoryLayoutRow
        {
            public string CategoryKey;
            public string Label;
            public Rectangle Bounds;
            public Rectangle HeaderBounds;
            public Rectangle RowsBounds;
            public bool IsCollapsed;
            public bool IsDragging;
        }

        private struct KeybindDisplayRow
        {
            public string Action;
            public string Input;
            public string TypeLabel;
            public InputType InputType;
            public int RenderOrder;
            public string CategoryKey;
            public int CategoryOrder;
            public Rectangle Bounds;
            public Rectangle TypeToggleBounds;
            public Rectangle KeyValueBounds;
            public Rectangle EnumValueBounds;
            public bool IsDragging;
            public bool TriggerAutoFire;
            public bool ToggleLocked;
            public bool IsSwitchType => ControlsBlock.IsSwitchType(InputType);
            public bool IsPersistentSwitch => ControlsBlock.IsPersistentSwitch(InputType);
            public bool IsToggleCandidate => !IsPersistentSwitch && InputType != InputType.Trigger && !ToggleLocked;
        }
    }
}
