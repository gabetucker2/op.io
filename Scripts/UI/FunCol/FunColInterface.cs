using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol
{
    /// <summary>
    /// A row-level interactive widget with N columns.
    ///
    /// Default mode: columns expand to fill the field when hovered (one column fills, others shrink).
    /// DisableExpansion=true: columns stay at their configured weight widths; no animation on hover.
    /// DisableColors=true: column backgrounds are transparent (no color tinting).
    ///
    /// Column color order: Green (0), Blue (1), Red (2), Orange (3), Purple (4).
    /// </summary>
    public class FunColInterface
    {
        // ── Column color palette ──────────────────────────────────────────────
        public static readonly Color[] ColumnColors =
        {
            new Color(40, 120, 55),   // 0 Green
            new Color(40,  85, 155),  // 1 Blue
            new Color(155, 40,  40),  // 2 Red
            new Color(155,  90, 28),  // 3 Orange
            new Color(105, 40, 155),  // 4 Purple
        };

        // ── Config ────────────────────────────────────────────────────────────
        /// <summary>
        /// When true, columns stay at their configured weight widths — no hover animation.
        /// Default is <c>true</c>. Set to <c>false</c> to opt in to expansion animation.
        /// </summary>
        public bool DisableExpansion { get; set; } = true;

        /// <summary>When true, no colored backgrounds are drawn for columns.</summary>
        public bool DisableColors { get; set; } = false;

        /// <summary>
        /// When true, clicking a column during UpdateHeaderHover() records a sort change.
        /// Read <see cref="SortColumn"/> and <see cref="SortDescending"/> each frame to apply sorting.
        /// Disabled by default — opt in per-instance (e.g. a header FunCol in PerformanceBlock).
        /// </summary>
        public bool EnableColumnSort { get; set; } = false;

        /// <summary>The column index currently used for sorting. -1 = none chosen yet.</summary>
        public int SortColumn { get; private set; } = -1;

        /// <summary>True = sort largest-to-smallest; false = smallest-to-largest.</summary>
        public bool SortDescending { get; private set; } = true;

        /// <summary>
        /// When true, the first column acts as a drag-handle zone.
        /// <see cref="DragHandleClicked"/> is set to <c>true</c> for one frame when col-0 is left-click-started.
        /// Only works when <see cref="DisableExpansion"/> is also true.
        /// </summary>
        public bool RowDragEnabled { get; set; } = false;

        /// <summary>True for exactly one frame when the drag-handle (col 0) was just clicked.</summary>
        public bool DragHandleClicked { get; private set; }

        /// <summary>
        /// When true, suppresses the one-time developer warning that fires when any feature in
        /// this FunColInterface is missing a <see cref="FunctionFieldFeature.HeaderTooltipTexts"/>.
        /// Set to <c>true</c> on row/ghost FunCols where header tooltips are intentionally absent.
        /// Default is <c>false</c> (warning enabled).
        /// </summary>
        public bool SuppressTooltipWarnings { get; set; } = false;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly FunctionFieldFeature[] _features;
        private readonly float[] _weights;   // target widths (normalized, sum=1)
        private readonly float[] _widths;    // animated current widths
        private int _hoveredColumn = -1;
        private bool _isInField;
        private bool _prevLeftDown;
        private int _headerHoveredColumn = -1; // updated by UpdateHeaderHover
        private bool _tooltipWarningsChecked;

        private const float AnimSpeed    = 8f;
        private const int   LabelPad    = 4;
        private const int   ToggleBtnSize = 10;
        private const int   ToggleBtnPad  = 3;

        // ── Column resize ─────────────────────────────────────────────────────
        /// <summary>
        /// When true, column dividers in the header can be dragged to resize adjacent columns.
        /// Only active when <see cref="HeaderVisible"/> is true and the block is unlocked
        /// (controlled by the block passing the appropriate clickMouse to UpdateHeaderHover).
        /// </summary>
        public bool EnableColumnResize { get; set; } = false;

        /// <summary>True for one or more frames while column weights are being changed by a resize drag.</summary>
        public bool ColumnWeightsChanged { get; private set; }

        /// <summary>Index of the left column whose right divider is currently hovered (-1 = none).</summary>
        public int HoveredDivider { get; private set; } = -1;

        /// <summary>True while the user is actively dragging a column divider.</summary>
        public bool IsResizeDragging => _resizeDragging;

        private bool _resizeDragging;
        private int _resizeDragIndex = -1;
        private int _resizeDragStartX;
        private float[] _resizeDragStartWeights;
        private int _resizeDragHeaderWidth;
        private const int ResizeGrabWidth = 4;
        private const float MinColumnWeight = 0.05f;

        // ── Header toggle ─────────────────────────────────────────────────────
        /// <summary>When true, a small toggle button appears at the left of the header on hover.</summary>
        public bool ShowHeaderToggle { get; set; } = false;

        /// <summary>
        /// When true, hovering a column in the header row shows a tooltip for that column.
        /// Disabled by default — opt in per-instance (e.g. PerformanceBlock header).
        /// </summary>
        public bool ShowHeaderTooltips { get; set; } = false;

        /// <summary>Whether the header column labels are currently visible. Default is true.</summary>
        public bool HeaderVisible { get; set; } = true;

        /// <summary>True for exactly one frame when the header toggle button was clicked.</summary>
        public bool HeaderToggleClicked { get; private set; }

        /// <summary>
        /// When set and <see cref="HeaderVisible"/> is false, the toggle button is drawn here
        /// (always visible, no hover required) so the user can expand the header again.
        /// Set each frame to the bounds of the first visible content row below the header.
        /// </summary>
        public Rectangle? CollapsedToggleBounds { get; set; }

        private bool _toggleButtonHovered;
        private bool _headerIsHovered;

        // ── Public API ────────────────────────────────────────────────────────
        public int HoveredColumn => _hoveredColumn;
        public bool IsInField => _isInField;
        public int ColumnCount => _features.Length;

        public FunctionFieldFeature GetFeature(int index)
            => (index >= 0 && index < _features.Length) ? _features[index] : null;

        public static Color GetColumnColor(int index)
            => (index >= 0 && index < ColumnColors.Length) ? ColumnColors[index] : Color.Gray;

        // ── Constructors ──────────────────────────────────────────────────────

        /// <summary>Equal-weight columns.</summary>
        public FunColInterface(params FunctionFieldFeature[] features)
            : this(null, features) { }

        /// <summary>Custom column weights (normalized). Null → equal weights.</summary>
        public FunColInterface(float[] columnWeights, params FunctionFieldFeature[] features)
        {
            _features = features ?? Array.Empty<FunctionFieldFeature>();
            int n = _features.Length;
            _weights = new float[n];
            _widths  = new float[n];

            // Initialize weights
            if (columnWeights != null && columnWeights.Length == n)
            {
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += Math.Max(0f, columnWeights[i]);
                for (int i = 0; i < n; i++) _weights[i] = sum > 0f ? Math.Max(0f, columnWeights[i]) / sum : (n > 0 ? 1f / n : 1f);
            }
            else
            {
                float def = n > 0 ? 1f / n : 1f;
                for (int i = 0; i < n; i++) _weights[i] = def;
            }

            // Initialize current widths to target weights
            for (int i = 0; i < n; i++) _widths[i] = _weights[i];
        }

        // ── Weight access (for persistence) ───────────────────────────────────

        /// <summary>Returns a copy of the current normalized column weights.</summary>
        public float[] GetWeights()
        {
            float[] copy = new float[_weights.Length];
            Array.Copy(_weights, copy, _weights.Length);
            return copy;
        }

        /// <summary>Replaces column weights (re-normalized). Snaps animated widths immediately.</summary>
        public void SetWeights(float[] weights)
        {
            if (weights == null || weights.Length != _weights.Length) return;
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += Math.Max(0f, weights[i]);
            if (sum <= 0.001f) return;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = Math.Max(0f, weights[i]) / sum;
                _widths[i] = _weights[i];
            }
        }

        // ── Update ────────────────────────────────────────────────────────────
        public void Update(Rectangle fieldBounds, MouseState mouse, float dt, bool suppressHover = false)
        {
            CheckTooltipWarnings();
            int n = _features.Length;
            if (n == 0) return;

            if (suppressHover || DisableExpansion)
            {
                _isInField     = !suppressHover && fieldBounds.Contains(mouse.Position);
                _hoveredColumn = _isInField ? GetColumnAtX(fieldBounds, mouse.Position.X) : -1;
                if (suppressHover) { _isInField = false; _hoveredColumn = -1; }
            }
            else
            {
                _isInField = fieldBounds.Contains(mouse.Position);
                if (_isInField && fieldBounds.Width > 0)
                    _hoveredColumn = GetColumnAtX(fieldBounds, mouse.Position.X);
                else
                    _hoveredColumn = -1;
            }

            // Animate widths toward targets
            if (DisableExpansion)
            {
                // Snap directly to configured weights, no animation
                for (int i = 0; i < n; i++) _widths[i] = _weights[i];
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    float target = _hoveredColumn == i ? 1f
                                 : _hoveredColumn == -1 ? _weights[i]
                                 : 0f;
                    _widths[i] = MathHelper.Lerp(_widths[i], target,
                        MathHelper.Clamp(AnimSpeed * dt, 0f, 1f));
                }

                // Normalize
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += _widths[i];
                if (sum > 0.001f)
                    for (int i = 0; i < n; i++) _widths[i] /= sum;
            }

            // Forward update to each feature
            int x = fieldBounds.X;
            for (int i = 0; i < n; i++)
            {
                int cw = (i == n - 1) ? (fieldBounds.Right - x) : (int)(fieldBounds.Width * _widths[i]);
                var colBounds = new Rectangle(x, fieldBounds.Y, Math.Max(0, cw), fieldBounds.Height);
                _features[i].Update(colBounds, mouse, dt, _hoveredColumn == i);
                x += cw;
            }

            // Row drag-handle click detection (opt-in via RowDragEnabled, only when DisableExpansion=true)
            bool currentLeft = mouse.LeftButton == ButtonState.Pressed;
            if (RowDragEnabled && DisableExpansion && !suppressHover && _isInField)
            {
                bool clickStart = currentLeft && !_prevLeftDown && _hoveredColumn == 0;
                DragHandleClicked = clickStart;
            }
            else
            {
                DragHandleClicked = false;
            }
            _prevLeftDown = currentLeft;
        }

        // ── Header hover (lightweight, no feature forwarding) ─────────────────
        /// <summary>
        /// Updates the header hover column used by <see cref="DrawHeader"/> for tooltips
        /// and (when <see cref="EnableColumnSort"/> is true) column-sort click detection.
        /// Call this once per frame from the block's Update method, passing the header strip bounds.
        /// <paramref name="mouse"/> is always used for hover position; pass the actual current
        /// MouseState even when the block is locked (clicks are ignored, but hover still shows).
        /// <paramref name="clickMouse"/> is the mouse used for click/sort interaction — pass
        /// previousMouseState when the block is locked to suppress clicks.
        /// </summary>
        public void UpdateHeaderHover(Rectangle headerBounds, MouseState mouse,
            MouseState? clickMouse = null)
        {
            MouseState cm = clickMouse ?? mouse;

            bool leftDown   = cm.LeftButton == ButtonState.Pressed;
            bool clickStart = leftDown && !_prevLeftDown;

            HeaderToggleClicked  = false;
            ColumnWeightsChanged = false;
            _toggleButtonHovered = false;
            _headerIsHovered     = headerBounds.Contains(mouse.Position);

            // ── Active column-resize drag ─────────────────────────────────────
            if (_resizeDragging && EnableColumnResize)
            {
                if (!leftDown)
                {
                    // Drag released — finalize
                    _resizeDragging = false;
                    _resizeDragIndex = -1;
                    ColumnWeightsChanged = true;
                }
                else if (_resizeDragHeaderWidth > 0 && _resizeDragStartWeights != null)
                {
                    float deltaWeight = (mouse.Position.X - _resizeDragStartX) / (float)_resizeDragHeaderWidth;
                    int li = _resizeDragIndex;
                    int ri = _resizeDragIndex + 1;
                    float leftW  = _resizeDragStartWeights[li] + deltaWeight;
                    float rightW = _resizeDragStartWeights[ri] - deltaWeight;

                    float combined = _resizeDragStartWeights[li] + _resizeDragStartWeights[ri];
                    if (leftW < MinColumnWeight)  { leftW = MinColumnWeight;  rightW = combined - MinColumnWeight; }
                    if (rightW < MinColumnWeight) { rightW = MinColumnWeight; leftW  = combined - MinColumnWeight; }

                    _weights[li] = leftW;
                    _weights[ri] = rightW;
                    for (int i = 0; i < _widths.Length; i++) _widths[i] = _weights[i];
                    ColumnWeightsChanged = true;
                }

                HoveredDivider = _resizeDragIndex;
                _prevLeftDown = leftDown;
                return; // consume all input during resize
            }

            // ── Toggle button ─────────────────────────────────────────────────
            if (ShowHeaderToggle && !HeaderVisible && CollapsedToggleBounds.HasValue)
            {
                // Header is collapsed: toggle button lives at top-left of the first visible row.
                Rectangle toggleRect = GetToggleButtonRect(CollapsedToggleBounds.Value);
                _toggleButtonHovered = toggleRect.Contains(mouse.Position);
                if (_toggleButtonHovered && clickStart)
                {
                    HeaderToggleClicked = true;
                    HeaderVisible       = true;
                }
            }
            else if (ShowHeaderToggle && _headerIsHovered)
            {
                Rectangle toggleRect = GetToggleButtonRect(headerBounds);
                _toggleButtonHovered = toggleRect.Contains(mouse.Position);
                if (_toggleButtonHovered && clickStart)
                {
                    HeaderToggleClicked = true;
                    HeaderVisible       = false;
                }
            }

            bool consumed = _toggleButtonHovered && clickStart;

            // ── Column resize start detection ─────────────────────────────────
            HoveredDivider = -1;
            if (EnableColumnResize && HeaderVisible && _headerIsHovered && !consumed)
            {
                HoveredDivider = GetDividerAtX(headerBounds, mouse.Position.X);
                if (HoveredDivider >= 0 && clickStart)
                {
                    _resizeDragging = true;
                    _resizeDragIndex = HoveredDivider;
                    _resizeDragStartX = mouse.Position.X;
                    _resizeDragStartWeights = (float[])_weights.Clone();
                    _resizeDragHeaderWidth = headerBounds.Width;
                    consumed = true;
                }
            }

            _headerHoveredColumn = (!consumed && _headerIsHovered)
                ? GetColumnAtX(headerBounds, mouse.Position.X)
                : -1;

            if (EnableColumnSort && clickStart && !consumed && _headerHoveredColumn >= 0)
            {
                if (_headerHoveredColumn == SortColumn)
                    SortDescending = !SortDescending;
                else
                {
                    SortColumn     = _headerHoveredColumn;
                    SortDescending = true;
                }
            }

            _prevLeftDown = leftDown;
        }

        // ── Draw ──────────────────────────────────────────────────────────────
        public void Draw(SpriteBatch sb, Rectangle fieldBounds, UIStyle.UIFont font, Texture2D pixel)
        {
            if (pixel == null) return;
            int n = _features.Length;
            if (n == 0) return;

            int x = fieldBounds.X;
            for (int i = 0; i < n; i++)
            {
                int cw = (i == n - 1) ? (fieldBounds.Right - x) : (int)(fieldBounds.Width * _widths[i]);
                if (cw <= 0) { x += cw; continue; }

                var colBounds  = new Rectangle(x, fieldBounds.Y, cw, fieldBounds.Height);
                bool isExpanded = !DisableExpansion && _hoveredColumn == i;
                Color baseColor = i < ColumnColors.Length ? ColumnColors[i] : Color.Gray;

                if (!DisableColors)
                {
                    Color fill   = isExpanded ? baseColor * 0.55f : baseColor * 0.18f;
                    Color border = isExpanded ? baseColor * 0.70f : baseColor * 0.14f;
                    sb.Draw(pixel, colBounds, fill);
                    DrawOutline(sb, pixel, colBounds, border, 1);
                }

                _features[i].Draw(sb, colBounds, baseColor, font, pixel, isExpanded);

                if (!DisableExpansion && isExpanded && _features[i].ShowTextWhenExpanded &&
                    !string.IsNullOrEmpty(_features[i].ExpandedInstruction) && font.IsAvailable)
                {
                    string instr = _features[i].ExpandedInstruction;
                    Vector2 ts = font.MeasureString(instr);
                    if (ts.X + LabelPad * 2 <= cw)
                    {
                        float tx = colBounds.X + (colBounds.Width  - ts.X) / 2f;
                        float ty = colBounds.Y + (colBounds.Height - ts.Y) / 2f;
                        font.DrawString(sb, instr, new Vector2(tx, ty), Color.White * 0.55f);
                    }
                }

                x += cw;
            }
        }

        /// <summary>Draws only the animated column background colors (no feature content).</summary>
        public void DrawBackground(SpriteBatch sb, Rectangle fieldBounds, Texture2D pixel)
        {
            if (pixel == null || DisableColors) return;
            int n = _features.Length;
            if (n == 0) return;

            int x = fieldBounds.X;
            for (int i = 0; i < n; i++)
            {
                int cw = (i == n - 1) ? (fieldBounds.Right - x) : (int)(fieldBounds.Width * _widths[i]);
                if (cw <= 0) { x += cw; continue; }

                var colBounds = new Rectangle(x, fieldBounds.Y, cw, fieldBounds.Height);
                bool isExpanded = !DisableExpansion && _hoveredColumn == i;
                Color baseColor = i < ColumnColors.Length ? ColumnColors[i] : Color.Gray;

                Color fill = isExpanded ? baseColor * 0.30f : baseColor * 0.10f;
                sb.Draw(pixel, colBounds, fill);
                x += cw;
            }
        }

        /// <summary>
        /// Draws a header row showing each column's label above the given bounds.
        /// Call this once per block, passing the header strip rectangle.
        /// </summary>
        public void DrawHeader(SpriteBatch sb, Rectangle headerBounds, UIStyle.UIFont font, Texture2D pixel)
        {
            if (pixel == null) return;
            int n = _features.Length;
            if (n == 0) return;

            // Background
            if (!DisableColors)
                sb.Draw(pixel, headerBounds, ColorPalette.OverlayBackground);

            // Header is collapsed: draw nothing for the strip, but always show the toggle button
            // at the first visible row position (CollapsedToggleBounds) so the user can expand it.
            if (!HeaderVisible)
            {
                if (ShowHeaderToggle && CollapsedToggleBounds.HasValue)
                    DrawToggleButton(sb, pixel, CollapsedToggleBounds.Value);
                return;
            }

            int x = headerBounds.X;
            for (int i = 0; i < n; i++)
            {
                int cw = (i == n - 1) ? (headerBounds.Right - x) : (int)(headerBounds.Width * _widths[i]);
                if (cw <= 0) { x += cw; continue; }

                var colBounds = new Rectangle(x, headerBounds.Y, cw, headerBounds.Height);
                Color baseColor = i < ColumnColors.Length ? ColumnColors[i] : Color.Gray;

                if (!DisableColors)
                    sb.Draw(pixel, colBounds, baseColor * 0.12f);

                if (font.IsAvailable)
                {
                    string label = _features[i].Label ?? string.Empty;
                    if (EnableColumnSort && i == SortColumn)
                        label += SortDescending ? " ↓" : " ↑";
                    if (!string.IsNullOrEmpty(label))
                    {
                        Vector2 ts = font.MeasureString(label);
                        if (ts.X + 4 <= cw)
                        {
                            float tx = colBounds.X + (colBounds.Width  - ts.X) / 2f;
                            float ty = colBounds.Y + (colBounds.Height - ts.Y) / 2f;
                            Color tc = DisableColors ? UIStyle.MutedTextColor : baseColor * 0.85f;
                            if (EnableColumnSort && i == SortColumn) tc = Color.White * 0.95f;
                            font.DrawString(sb, label, new Vector2(tx, ty), tc);
                        }
                    }
                }

                // Column divider (highlight when resize-hovered or actively dragging)
                if (i < n - 1 && cw > 0)
                {
                    bool divActive = EnableColumnResize && (HoveredDivider == i || (_resizeDragging && _resizeDragIndex == i));
                    if (divActive)
                    {
                        int divW = 3;
                        int divX = colBounds.Right - divW / 2 - 1;
                        sb.Draw(pixel, new Rectangle(divX, headerBounds.Y, divW, headerBounds.Height), Color.White * 0.70f);
                    }
                    else
                    {
                        sb.Draw(pixel, new Rectangle(colBounds.Right - 1, headerBounds.Y, 1, headerBounds.Height), UIStyle.BlockBorder);
                    }
                }

                x += cw;
            }

            // Bottom border
            if (pixel != null)
                sb.Draw(pixel, new Rectangle(headerBounds.X, headerBounds.Bottom - 1, headerBounds.Width, 1), UIStyle.BlockBorder);

            // Tooltip for hovered column (opt-in via ShowHeaderTooltips)
            if (ShowHeaderTooltips && _headerHoveredColumn >= 0 && _headerHoveredColumn < n &&
                font.IsAvailable && pixel != null)
            {
                FunctionFieldFeature feat = _features[_headerHoveredColumn];
                string[] tipTexts = feat.HeaderTooltipTexts;
                if (tipTexts == null || tipTexts.Length == 0)
                    tipTexts = [feat.Label];

                const int TipPadH = 5;
                const int TipPadV = 3;
                const int TipMaxW = 220;
                const int TipGap  = 4;
                int   innerW    = TipMaxW - TipPadH * 2;
                float tipLineH  = font.LineHeight;

                // Compute column x for centering (shared across all tooltip boxes)
                int cx = headerBounds.X;
                for (int k = 0; k < _headerHoveredColumn; k++)
                    cx += (k == n - 1) ? (headerBounds.Right - cx) : (int)(headerBounds.Width * _widths[k]);
                int colW = (_headerHoveredColumn == n - 1)
                    ? (headerBounds.Right - cx)
                    : (int)(headerBounds.Width * _widths[_headerHoveredColumn]);

                Color tipBg = ColorPalette.OverlayBackground;
                Color tipFg = DisableColors ? UIStyle.MutedTextColor
                    : FunColInterface.GetColumnColor(_headerHoveredColumn) * 0.90f;

                int currentTipY = headerBounds.Bottom + 2;
                foreach (string tipText in tipTexts)
                {
                    if (string.IsNullOrEmpty(tipText)) continue;

                    var tipLines = WrapHeaderTooltip(font, tipText, innerW);
                    if (tipLines.Count == 0) tipLines.Add(tipText);

                    float maxTipLineW = 0f;
                    foreach (string tl in tipLines)
                    {
                        float w = font.MeasureString(tl).X;
                        if (w > maxTipLineW) maxTipLineW = w;
                    }

                    int tipW = (int)maxTipLineW + TipPadH * 2;
                    int tipH = (int)(tipLines.Count * tipLineH) + TipPadV * 2;
                    int tipX = cx + (colW - tipW) / 2;

                    sb.Draw(pixel, new Rectangle(tipX, currentTipY, tipW, tipH), tipBg);
                    sb.Draw(pixel, new Rectangle(tipX, currentTipY, tipW, 1), UIStyle.BlockBorder);
                    sb.Draw(pixel, new Rectangle(tipX, currentTipY + tipH - 1, tipW, 1), UIStyle.BlockBorder);
                    sb.Draw(pixel, new Rectangle(tipX, currentTipY, 1, tipH), UIStyle.BlockBorder);
                    sb.Draw(pixel, new Rectangle(tipX + tipW - 1, currentTipY, 1, tipH), UIStyle.BlockBorder);
                    for (int li = 0; li < tipLines.Count; li++)
                        font.DrawString(sb, tipLines[li],
                            new Vector2(tipX + TipPadH, currentTipY + TipPadV + li * tipLineH), tipFg);

                    currentTipY += tipH + TipGap;
                }
            }

            // Toggle button drawn on top of everything (only when hovered + enabled)
            if (ShowHeaderToggle && _headerIsHovered)
                DrawToggleButton(sb, pixel, headerBounds);
        }

        private void CheckTooltipWarnings()
        {
            if (_tooltipWarningsChecked || SuppressTooltipWarnings) return;
            _tooltipWarningsChecked = true;
            for (int i = 0; i < _features.Length; i++)
            {
                var tooltipTexts = _features[i]?.HeaderTooltipTexts;
                if (tooltipTexts == null || tooltipTexts.Length == 0 || string.IsNullOrEmpty(tooltipTexts[0]))
                    DebugLogger.PrintWarning(
                        $"FunColInterface: col {i} ('{_features[i]?.Label ?? "?"}') has no HeaderTooltipTexts. " +
                        $"Set SuppressTooltipWarnings=true to silence this per-instance.");
            }
        }

        public Rectangle GetColumnBounds(int index, Rectangle fieldBounds)
        {
            int n = _features.Length;
            if (index < 0 || index >= n) return Rectangle.Empty;
            int x = fieldBounds.X;
            for (int i = 0; i < n; i++)
            {
                int cw = (i == n - 1) ? (fieldBounds.Right - x) : (int)(fieldBounds.Width * _widths[i]);
                cw = Math.Max(0, cw);
                if (i == index) return new Rectangle(x, fieldBounds.Y, cw, fieldBounds.Height);
                x += cw;
            }
            return Rectangle.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Rectangle GetToggleButtonRect(Rectangle headerBounds)
        {
            int by = headerBounds.Y + (headerBounds.Height - ToggleBtnSize) / 2;
            return new Rectangle(headerBounds.X + ToggleBtnPad, by, ToggleBtnSize, ToggleBtnSize);
        }

        private void DrawToggleButton(SpriteBatch sb, Texture2D pixel, Rectangle headerBounds)
        {
            Rectangle r = GetToggleButtonRect(headerBounds);
            Color fill = HeaderVisible
                ? (_toggleButtonHovered ? ColorPalette.IndicatorActive * 0.9f : ColorPalette.IndicatorActive * 0.67f)
                : (_toggleButtonHovered ? ColorPalette.IndicatorInactive * 0.9f : ColorPalette.IndicatorInactive * 0.67f);
            sb.Draw(pixel, r, fill);
            DrawOutline(sb, pixel, r, ColorPalette.TextMuted * 0.55f, 1);
        }

        private static List<string> WrapHeaderTooltip(UIStyle.UIFont font, string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || !font.IsAvailable || maxWidth <= 0)
                return lines;
            string[] words = text.Split(' ');
            var current = new System.Text.StringBuilder();
            foreach (string word in words)
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                }
                else
                {
                    string candidate = current + " " + word;
                    if (font.MeasureString(candidate).X > maxWidth)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else
                    {
                        current.Append(' ');
                        current.Append(word);
                    }
                }
            }
            if (current.Length > 0) lines.Add(current.ToString());
            return lines;
        }

        private int GetColumnAtX(Rectangle fieldBounds, int mouseX)
        {
            int n = _features.Length;
            if (n == 0 || fieldBounds.Width <= 0) return -1;
            // Use current widths for hover detection
            int x = fieldBounds.X;
            for (int i = 0; i < n - 1; i++)
            {
                int cw = (int)(fieldBounds.Width * _widths[i]);
                if (mouseX < x + cw) return i;
                x += cw;
            }
            return n - 1;
        }

        /// <summary>Returns the index of the left column whose right-edge divider is near mouseX, or -1.</summary>
        private int GetDividerAtX(Rectangle bounds, int mouseX)
        {
            int n = _features.Length;
            if (n <= 1 || bounds.Width <= 0) return -1;
            int x = bounds.X;
            for (int i = 0; i < n - 1; i++)
            {
                int cw = (int)(bounds.Width * _widths[i]);
                int dividerX = x + cw;
                if (Math.Abs(mouseX - dividerX) <= ResizeGrabWidth)
                    return i;
                x += cw;
            }
            return -1;
        }

        private static void DrawOutline(SpriteBatch sb, Texture2D pixel, Rectangle r, Color c, int t)
        {
            if (t <= 0) return;
            sb.Draw(pixel, new Rectangle(r.X,          r.Y,              r.Width, t), c);
            sb.Draw(pixel, new Rectangle(r.X,          r.Bottom - t,     r.Width, t), c);
            sb.Draw(pixel, new Rectangle(r.X,          r.Y,              t, r.Height), c);
            sb.Draw(pixel, new Rectangle(r.Right - t,  r.Y,              t, r.Height), c);
        }
    }
}
