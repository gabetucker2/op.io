using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class ColorSchemeBlock
    {
        public const string BlockTitle = "Color Scheme";
        public const int MinWidth = 40;
        public const int MinHeight = 0;

        private const int SwatchSize = 18;
        private const int SwatchPadding = 8;
        private const int RowVerticalPadding = 6;
        private const int EditorPadding = 14;
        private const int WheelMinSize = 180;
        private const int WheelMaxSize = 320;
        private const int WheelMinSizeCompact = 64;
        private const int WheelToPreviewGap = 14;
        private const int PreviewHeight = 36;
        private const int PreviewMinHeight = 20;
        private const int PreviewToHexGap = 10;
        private const int PreviewLabelAllowance = 14;
        private const int HexMinHeight = 18;
        private const int SliderWidth = 14;
        private const int SliderMargin = 10;
        private const int ButtonHeight = 30;
        private const int ButtonWidth = 100;
        private const int HexInputHeight = 30;
        private const int LockIndicatorSize = 12;
        private const int LockIndicatorSpacing = 6;
        private const int EditorMinWidth = 320;
        private const int EditorMaxWidth = 560;
        private const int EditorMinHeight = 360;
        private const int EditorMaxHeight = 620;
        private const int SchemeToolbarHeight = 44;
        private const int SchemeButtonHeight = 28;
        private const int SchemeButtonWidth = SchemeButtonHeight;
        private const int SchemeDropdownHeight = 30;
        private const int SchemeControlSpacing = 10;
        private const int SchemePromptWidth = 360;
        private const int SchemePromptHeight = 180;
        private const int SchemePromptPadding = 16;

        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly List<ColorRow> _rows = new();
        private static readonly BlockDragState<ColorRow> _dragState = new(row => row.Key, row => row.Bounds, (row, dragging) =>
        {
            row.IsDragging = dragging;
            return row;
        });

        private static Texture2D _pixel;
        private static Texture2D _colorWheelTexture;
        private static float _lineHeight;
        private static string _hoveredRowKey;
        private static ColorEditorState _editor;
        private static readonly UIDropdown _schemeDropdown = new();
        private static Rectangle _schemeToolbarBounds;
        private static Rectangle _saveSchemeBounds;
        private static Rectangle _newSchemeBounds;
        private static Rectangle _deleteSchemeBounds;
        private static Texture2D _saveIcon;
        private static Texture2D _newIcon;
        private static Texture2D _deleteIcon;
        private static string _selectedSchemeName = ColorScheme.DefaultSchemeName;
        private static bool _schemeListDirty = true;
        private static SchemePromptState _schemePrompt;
        private static readonly KeyRepeatTracker HexInputRepeater = new();
        private static readonly KeyRepeatTracker SchemePromptRepeater = new();
        private static KeyboardState _previousKeyboardState;
        private static Point _lastMousePosition;
        private static Rectangle _lastContentBounds;

        public static bool IsEditorOpen => _editor.IsActive;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            double elapsedSeconds = Math.Max(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d, 0d);
            ColorScheme.Initialize();
            EnsureSchemeOptions();
            EnsureRows();
            EnsureLineHeight();
            _lastContentBounds = contentBounds;
            UpdateToolbarLayout(contentBounds);

            Rectangle listContentBounds = GetListContentBounds(contentBounds);
            float contentHeight = Math.Max(0f, _rows.Count * _lineHeight);
            _scrollPanel.Update(listContentBounds, contentHeight, mouseState, previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = listContentBounds;
            }

            UpdateRowBounds(listBounds);
            UIStyle.UIFont valueFont = UIStyle.FontTech;
            UpdateHexBounds(valueFont);

            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickStarted = leftDown && !leftDownPrev;
            bool leftClickReleased = !leftDown && leftDownPrev;
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.ColorScheme);
            bool pointerInsideList = listBounds.Contains(mouseState.Position);

            if (_schemePrompt.IsOpen)
            {
                UpdateSchemePrompt(mouseState, previousMouseState, keyboardState, previousKeyboardState, elapsedSeconds);
                _lastMousePosition = mouseState.Position;
                _previousKeyboardState = keyboardState;
                return;
            }

            if (blockLocked && _dragState.IsDragging)
            {
                _dragState.Reset();
            }

            if (blockLocked && _editor.IsActive)
            {
                CloseEditor(applyChanges: false);
            }

            bool dropdownChanged = _schemeDropdown.Update(mouseState, previousMouseState, keyboardState, previousKeyboardState, out string newlySelected, isDisabled: blockLocked);
            if (dropdownChanged && !string.IsNullOrWhiteSpace(newlySelected))
            {
                _selectedSchemeName = newlySelected;
                if (TryLoadSelectedScheme())
                {
                    EnsureRows();
                    _schemeListDirty = true;
                }
            }

            if (!blockLocked && leftClickReleased)
            {
                if (_saveSchemeBounds.Contains(mouseState.Position))
                {
                    if (TrySaveSelectedScheme())
                    {
                        _schemeListDirty = true;
                    }
                }
                else if (_newSchemeBounds.Contains(mouseState.Position))
                {
                    CloseEditor(applyChanges: false);
                    OpenSchemePrompt();
                    _lastMousePosition = mouseState.Position;
                    _previousKeyboardState = keyboardState;
                    return;
                }
                else if (_deleteSchemeBounds.Contains(mouseState.Position))
                {
                    if (TryDeleteSelectedScheme())
                    {
                        EnsureRows();
                        _schemeListDirty = true;
                    }
                }
            }

            if (_editor.IsActive)
            {
                UpdateEditor(contentBounds, mouseState, previousMouseState, keyboardState, previousKeyboardState, elapsedSeconds);
                _lastMousePosition = mouseState.Position;
                _previousKeyboardState = keyboardState;
                return;
            }

            _hoveredRowKey = !blockLocked && pointerInsideList ? HitTestRow(mouseState.Position) : null;

            if (_dragState.IsDragging)
            {
                _dragState.UpdateDrag(_rows, listBounds, _lineHeight, mouseState);
                if (leftClickReleased)
                {
                    if (_dragState.TryCompleteDrag(_rows, out bool orderChanged) && orderChanged)
                    {
                        PersistRowOrder();
                    }
                }
            }
            else if (!blockLocked && pointerInsideList && leftClickStarted)
            {
                bool hasRow = TryGetRow(_hoveredRowKey, out ColorRow hoveredRow);
                bool lockedRow = hasRow && hoveredRow.IsLocked;
                bool clickedHex = hasRow && !lockedRow && hoveredRow.HexBounds != Rectangle.Empty && hoveredRow.HexBounds.Contains(mouseState.Position);
                bool clickedSwatch = hasRow &&
                    !lockedRow &&
                    hoveredRow.Bounds != Rectangle.Empty &&
                    GetSwatchBounds(hoveredRow.Bounds).Contains(mouseState.Position);

                if (clickedHex)
                {
                    BeginEdit(_hoveredRowKey, focusHexInput: true);
                }
                else if (clickedSwatch)
                {
                    BeginEdit(_hoveredRowKey);
                }
                else if (_dragState.TryStartDrag(_rows, _hoveredRowKey, mouseState))
                {
                    // dragging handled by BlockDragState
                }
                else if (!string.IsNullOrWhiteSpace(_hoveredRowKey) && !lockedRow)
                {
                    BeginEdit(_hoveredRowKey);
                }
            }

            _lastMousePosition = mouseState.Position;
            _previousKeyboardState = keyboardState;
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            EnsureRows();
            EnsurePixel(spriteBatch);
            EnsureLineHeight();
            EnsureSchemeIcons();
            _lastContentBounds = contentBounds;
            UpdateToolbarLayout(contentBounds);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = GetListContentBounds(contentBounds);
            }

            UIStyle.UIFont labelFont = UIStyle.FontBody;
            UIStyle.UIFont valueFont = UIStyle.FontTech;
            UIStyle.UIFont placeholderFont = UIStyle.FontH2;
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.ColorScheme);

            if (!labelFont.IsAvailable || !valueFont.IsAvailable)
            {
                return;
            }

            DrawSchemeToolbar(spriteBatch, blockLocked);

            if (_rows.Count == 0)
            {
                string placeholder = TextSpacingHelper.JoinWithWideSpacing("No", "colors", "defined.");
                Vector2 size = placeholderFont.IsAvailable ? placeholderFont.MeasureString(placeholder) : Vector2.Zero;
                Vector2 pos = new(listBounds.X, listBounds.Y);
                if (size != Vector2.Zero)
                {
                    pos = new Vector2(listBounds.X + (listBounds.Width - size.X) / 2f, listBounds.Y + (listBounds.Height - size.Y) / 2f);
                }
                (placeholderFont.IsAvailable ? placeholderFont : labelFont).DrawString(spriteBatch, placeholder, pos, UIStyle.MutedTextColor);
                _scrollPanel.Draw(spriteBatch);
                _schemeDropdown.DrawOptionsOverlay(spriteBatch);
                return;
            }

            float lineHeight = _lineHeight;

            foreach (ColorRow row in _rows)
            {
                bool isDraggingRow = _dragState.IsDragging && string.Equals(row.Key, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (!isDraggingRow)
                {
                    DrawRow(spriteBatch, row, labelFont, valueFont, listBounds);
                }
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                Rectangle dropIndicator = Rectangle.Intersect(_dragState.DropIndicatorBounds, listBounds);
                if (dropIndicator.Width > 0 && dropIndicator.Height > 0)
                {
                    FillRect(spriteBatch, dropIndicator, ColorPalette.DropIndicator);
                }

                DrawDraggingRow(spriteBatch, _dragState.DraggingSnapshot, labelFont, valueFont, listBounds, lineHeight);
            }

            _scrollPanel.Draw(spriteBatch);
            _schemeDropdown.DrawOptionsOverlay(spriteBatch);
        }

        public static void DrawOverlay(SpriteBatch spriteBatch, Rectangle layoutBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            Rectangle overlaySpace = layoutBounds != Rectangle.Empty ? layoutBounds : _lastContentBounds;
            if (_schemePrompt.IsOpen)
            {
                DrawSchemePrompt(spriteBatch, overlaySpace);
                return;
            }

            if (!_editor.IsActive)
            {
                return;
            }

            overlaySpace = GetEditorViewport(overlaySpace);
            DrawEditor(spriteBatch, overlaySpace);
        }

        private static void EnsureRows()
        {
            Dictionary<string, ColorRow> existing = _rows.ToDictionary(r => r.Key, r => r, StringComparer.OrdinalIgnoreCase);

            _rows.Clear();
            foreach (ColorOption option in ColorScheme.GetOrderedOptions())
            {
                ColorRow row = existing.TryGetValue(option.Key, out ColorRow cached) ? cached : default;
                row.Role = option.Role;
                row.Key = option.Key;
                row.Label = option.Label;
                row.Category = option.Category;
                row.Value = option.Value;
                row.IsLocked = option.IsLocked;
                row.IsDragging = false;
                row.HexBounds = Rectangle.Empty;
                _rows.Add(row);
            }
        }

        private static void EnsureSchemeOptions()
        {
            if (!_schemeListDirty && _schemeDropdown.HasOptions)
            {
                return;
            }

            IReadOnlyList<string> schemes = ColorScheme.GetAvailableSchemeNames();
            IEnumerable<UIDropdown.Option> options = schemes.Select(name => new UIDropdown.Option(name, name));
            string desired = !string.IsNullOrWhiteSpace(_selectedSchemeName) ? _selectedSchemeName : ColorScheme.ActiveSchemeName;
            _schemeDropdown.SetOptions(options, desired);
            _selectedSchemeName = _schemeDropdown.HasOptions ? (_schemeDropdown.SelectedId ?? ColorScheme.ActiveSchemeName) : ColorScheme.ActiveSchemeName;
            _schemeListDirty = false;
        }

        private static void UpdateToolbarLayout(Rectangle contentBounds)
        {
            _schemeToolbarBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, SchemeToolbarHeight);

            int buttonY = _schemeToolbarBounds.Y + (_schemeToolbarBounds.Height - SchemeButtonHeight) / 2;
            int x = _schemeToolbarBounds.Right - SchemeButtonWidth;
            _deleteSchemeBounds = new Rectangle(x, buttonY, SchemeButtonWidth, SchemeButtonHeight);
            x -= SchemeControlSpacing + SchemeButtonWidth;
            _newSchemeBounds = new Rectangle(x, buttonY, SchemeButtonWidth, SchemeButtonHeight);
            x -= SchemeControlSpacing + SchemeButtonWidth;
            _saveSchemeBounds = new Rectangle(Math.Max(_schemeToolbarBounds.X, x), buttonY, SchemeButtonWidth, SchemeButtonHeight);

            int availableDropdown = _saveSchemeBounds.X - SchemeControlSpacing - _schemeToolbarBounds.X;
            int dropdownWidth = Math.Max(0, availableDropdown);
            int dropdownHeight = SchemeDropdownHeight;
            int dropdownY = _schemeToolbarBounds.Y + (_schemeToolbarBounds.Height - dropdownHeight) / 2;
            _schemeDropdown.Bounds = dropdownWidth > 0
                ? new Rectangle(_schemeToolbarBounds.X, dropdownY, dropdownWidth, dropdownHeight)
                : Rectangle.Empty;
        }

        private static Rectangle GetListContentBounds(Rectangle contentBounds)
        {
            int listY = contentBounds.Y + SchemeToolbarHeight;
            int listHeight = Math.Max(0, contentBounds.Height - SchemeToolbarHeight);
            return new Rectangle(contentBounds.X, listY, contentBounds.Width, listHeight);
        }

        private static bool TrySaveSelectedScheme()
        {
            string target = string.IsNullOrWhiteSpace(_selectedSchemeName) ? ColorScheme.ActiveSchemeName : _selectedSchemeName;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            bool saved = ColorScheme.SaveCurrentScheme(target, makeActive: true);
            if (saved)
            {
                _selectedSchemeName = ColorScheme.ActiveSchemeName;
            }

            return saved;
        }

        private static bool TryLoadSelectedScheme()
        {
            string target = string.IsNullOrWhiteSpace(_selectedSchemeName) ? ColorScheme.ActiveSchemeName : _selectedSchemeName;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            bool loaded = ColorScheme.TryLoadScheme(target);
            if (loaded)
            {
                _selectedSchemeName = ColorScheme.ActiveSchemeName;
            }

            return loaded;
        }

        private static bool TryDeleteSelectedScheme()
        {
            string target = string.IsNullOrWhiteSpace(_selectedSchemeName) ? ColorScheme.ActiveSchemeName : _selectedSchemeName;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            bool deleted = ColorScheme.DeleteScheme(target);
            if (!deleted)
            {
                return false;
            }

            IReadOnlyList<string> schemes = ColorScheme.GetAvailableSchemeNames();
            string next = schemes.FirstOrDefault(name => !string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(next))
            {
                next = schemes.FirstOrDefault();
            }

            _selectedSchemeName = next;
            if (!string.IsNullOrWhiteSpace(_selectedSchemeName))
            {
                ColorScheme.TryLoadScheme(_selectedSchemeName);
            }

            return true;
        }

        private static void EnsureLineHeight()
        {
            UIStyle.UIFont labelFont = UIStyle.FontBody;
            UIStyle.UIFont valueFont = UIStyle.FontTech;
            if (!labelFont.IsAvailable && !valueFont.IsAvailable)
            {
                _lineHeight = 0f;
                return;
            }

            float labelHeight = labelFont.IsAvailable ? labelFont.LineHeight : 0f;
            float valueHeight = valueFont.IsAvailable ? valueFont.LineHeight : 0f;
            _lineHeight = MathF.Ceiling(Math.Max(labelHeight, valueHeight) + RowVerticalPadding);
        }

        private static void EnsureSchemeIcons()
        {
            _saveIcon = EnsureIcon(_saveIcon, "Icon_Save.png");
            _newIcon = EnsureIcon(_newIcon, "Icon_New.png");
            _deleteIcon = EnsureIcon(_deleteIcon, "Icon_Delete.png");
        }

        private static void UpdateRowBounds(Rectangle listBounds)
        {
            if (_lineHeight <= 0f || listBounds.Height <= 0)
            {
                return;
            }

            float y = listBounds.Y - _scrollPanel.ScrollOffset;
            int rowHeight = (int)MathF.Ceiling(_lineHeight);
            for (int i = 0; i < _rows.Count; i++)
            {
                ColorRow row = _rows[i];
                row.Bounds = new Rectangle(listBounds.X, (int)MathF.Round(y), listBounds.Width, rowHeight);
                _rows[i] = row;
                y += _lineHeight;
            }
        }

        private static string HitTestRow(Point pointer)
        {
            foreach (ColorRow row in _rows)
            {
                if (row.Bounds.Contains(pointer))
                {
                    return row.Key;
                }
            }

            return null;
        }

        private static void DrawSchemeToolbar(SpriteBatch spriteBatch, bool blockLocked)
        {
            if (spriteBatch == null || _schemeToolbarBounds == Rectangle.Empty)
            {
                return;
            }

            EnsureSchemeIcons();
            FillRect(spriteBatch, _schemeToolbarBounds, UIStyle.BlockBackground * 1.05f);
            DrawRectOutline(spriteBatch, _schemeToolbarBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            _schemeDropdown.Draw(spriteBatch, drawOptions: false);

            bool saveHovered = !blockLocked && UIButtonRenderer.IsHovered(_saveSchemeBounds, _lastMousePosition);
            bool newHovered = !blockLocked && UIButtonRenderer.IsHovered(_newSchemeBounds, _lastMousePosition);
            bool deleteHovered = !blockLocked && UIButtonRenderer.IsHovered(_deleteSchemeBounds, _lastMousePosition);

            DrawSchemeButton(spriteBatch, _saveSchemeBounds, _saveIcon, "Save", saveHovered, blockLocked);
            DrawSchemeButton(spriteBatch, _newSchemeBounds, _newIcon, "New", newHovered, blockLocked);
            DrawSchemeButton(spriteBatch, _deleteSchemeBounds, _deleteIcon, "Delete", deleteHovered, blockLocked);
        }

        private static void DrawSchemeButton(SpriteBatch spriteBatch, Rectangle bounds, Texture2D icon, string fallbackLabel, bool isHovered, bool isDisabled)
        {
            if (icon != null && !icon.IsDisposed)
            {
                UIButtonRenderer.DrawIcon(spriteBatch, bounds, icon, UIButtonRenderer.ButtonStyle.Grey, isHovered, isDisabled);
            }
            else
            {
                UIButtonRenderer.Draw(spriteBatch, bounds, fallbackLabel, UIButtonRenderer.ButtonStyle.Grey, isHovered, isDisabled);
            }
        }

        private static void DrawRow(SpriteBatch spriteBatch, ColorRow row, UIStyle.UIFont labelFont, UIStyle.UIFont valueFont, Rectangle viewport)
        {
            Rectangle bounds = row.Bounds;
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            Rectangle visibleBounds = Rectangle.Intersect(bounds, viewport);
            if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
            {
                return;
            }

            bool hovered = string.Equals(_hoveredRowKey, row.Key, StringComparison.OrdinalIgnoreCase);
            Color background = hovered ? ColorPalette.RowHover : UIStyle.BlockBackground;
            FillRect(spriteBatch, visibleBounds, background);

            Rectangle swatch = GetSwatchBounds(visibleBounds);
            swatch = Rectangle.Intersect(swatch, viewport);
            if (swatch.Width > 0 && swatch.Height > 0)
            {
                FillRect(spriteBatch, swatch, row.Value);
                DrawRectOutline(spriteBatch, swatch, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
            }

            int swatchRight = swatch.Width > 0 && swatch.Height > 0 ? swatch.Right : visibleBounds.X;
            float textY = visibleBounds.Y + (visibleBounds.Height - labelFont.LineHeight) / 2f;
            float labelX = swatchRight + SwatchPadding;
            if (row.IsLocked)
            {
                Rectangle lockBounds = GetLockIndicatorBounds(visibleBounds, labelX);
                DrawLockIndicator(spriteBatch, lockBounds);
                labelX += lockBounds.Width + LockIndicatorSpacing;
            }

            Vector2 labelPos = new(labelX, textY);
            labelFont.DrawString(spriteBatch, row.Label, labelPos, UIStyle.TextColor);

            string hex = ColorScheme.ToHex(row.Value, includeAlpha: false);
            Vector2 hexSize = valueFont.MeasureString(hex);
            float hexY = visibleBounds.Y + (visibleBounds.Height - hexSize.Y) / 2f;
            Rectangle hexBounds = row.HexBounds != Rectangle.Empty ? row.HexBounds : new Rectangle(
                (int)MathF.Floor(visibleBounds.Right - hexSize.X - SwatchPadding),
                (int)MathF.Floor(hexY),
                (int)MathF.Ceiling(hexSize.X),
                (int)MathF.Ceiling(hexSize.Y));

            bool isHexFocused = _editor.IsActive && _editor.HexFocused && _editor.Role == row.Role;
            if (isHexFocused)
            {
                Rectangle padded = hexBounds;
                padded.Inflate(4, 2);
                FillRect(spriteBatch, padded, ColorPalette.DropIndicator);
            }

            Vector2 hexPos = new(hexBounds.X, hexBounds.Y);
            Color hexColor = (_editor.IsActive && _editor.Role == row.Role) ? Color.White : UIStyle.MutedTextColor;
            valueFont.DrawString(spriteBatch, hex, hexPos, hexColor);
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, ColorRow snapshot, UIStyle.UIFont labelFont, UIStyle.UIFont valueFont, Rectangle listBounds, float lineHeight)
        {
            Rectangle dragBounds = Rectangle.Intersect(_dragState.GetDragBounds(listBounds, lineHeight), listBounds);
            if (dragBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);
            snapshot.Bounds = dragBounds;
            DrawRow(spriteBatch, snapshot, labelFont, valueFont, listBounds);
        }

        private static Rectangle GetSwatchBounds(Rectangle rowBounds)
        {
            int size = Math.Min(SwatchSize, Math.Max(12, rowBounds.Height - (RowVerticalPadding * 2)));
            int swatchY = rowBounds.Y + (rowBounds.Height - size) / 2;
            return new Rectangle(rowBounds.X + SwatchPadding, swatchY, size, size);
        }

        private static void UpdateHexBounds(UIStyle.UIFont valueFont)
        {
            if (!valueFont.IsAvailable)
            {
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                ColorRow row = _rows[i];
                if (row.Bounds == Rectangle.Empty)
                {
                    continue;
                }

                Rectangle hexBounds = ComputeHexBounds(row, valueFont);
                row.HexBounds = hexBounds;
                _rows[i] = row;
            }
        }

        private static Rectangle ComputeHexBounds(ColorRow row, UIStyle.UIFont valueFont)
        {
            if (!valueFont.IsAvailable || row.Bounds == Rectangle.Empty)
            {
                return Rectangle.Empty;
            }

            string hex = ColorScheme.ToHex(row.Value, includeAlpha: false);
            Vector2 hexSize = valueFont.MeasureString(hex);
            float hexY = row.Bounds.Y + (row.Bounds.Height - hexSize.Y) / 2f;
            return new Rectangle(
                (int)MathF.Floor(row.Bounds.Right - hexSize.X - SwatchPadding),
                (int)MathF.Floor(hexY),
                (int)MathF.Ceiling(hexSize.X),
                (int)MathF.Ceiling(hexSize.Y));
        }

        private static Rectangle GetLockIndicatorBounds(Rectangle rowBounds, float startX)
        {
            int targetSize = Math.Min(LockIndicatorSize, Math.Max(8, rowBounds.Height - (RowVerticalPadding * 2)));
            int indicatorY = rowBounds.Y + (rowBounds.Height - targetSize) / 2;
            return new Rectangle((int)MathF.Round(startX), indicatorY, targetSize, targetSize);
        }

        private static void DrawLockIndicator(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_pixel == null || spriteBatch == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            FillRect(spriteBatch, bounds, ColorPalette.LockLockedFill);
            DrawRectOutline(spriteBatch, bounds, UIStyle.BlockBorder, 1);

            UIStyle.UIFont techFont = UIStyle.FontTech;
            if (techFont.IsAvailable)
            {
                const string glyph = "L";
                Vector2 size = techFont.MeasureString(glyph);
                Vector2 pos = new(bounds.X + (bounds.Width - size.X) / 2f, bounds.Y + (bounds.Height - size.Y) / 2f);
                techFont.DrawString(spriteBatch, glyph, pos, UIStyle.TextColor);
            }
        }

        private static void BeginEdit(string rowKey, bool focusHexInput = false)
        {
            if (!TryGetRow(rowKey, out ColorRow target))
            {
                return;
            }

            if (target.IsLocked)
            {
                return;
            }

            _editor = new ColorEditorState
            {
                IsActive = true,
                Role = target.Role,
                Label = target.Label,
                WorkingColor = target.Value,
                OriginalColor = target.Value,
                HexBuffer = ColorScheme.ToHex(target.Value, includeAlpha: false),
                HexFocused = focusHexInput,
                HexFreshFocus = focusHexInput,
                ReadyForOutsideClose = false
            };

            SetEditorColor(target.Value);
        }

        private static void CloseEditor(bool applyChanges)
        {
            if (_editor.IsActive && applyChanges)
            {
                ColorScheme.TryUpdateColor(_editor.Role, _editor.WorkingColor);
                EnsureRows();
            }

            _editor = default;
            HexInputRepeater.Reset();
        }

        private static void UpdateEditor(Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState, double elapsedSeconds)
        {
            if (!_editor.IsActive)
            {
                return;
            }

            Rectangle overlayBounds = GetEditorViewport(contentBounds);
            BuildEditorLayout(overlayBounds);
            if (_editor.HexBounds == Rectangle.Empty)
            {
                _editor.HexFocused = false;
            }
            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickStarted = leftDown && !leftDownPrev;
            bool leftClickReleased = !leftDown && leftDownPrev;
            Point pointer = mouseState.Position;

            if (!leftDown)
            {
                _editor.ReadyForOutsideClose = true;
            }

            if (leftClickStarted)
            {
                if (_editor.ApplyBounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: true);
                    return;
                }

                if (_editor.CancelBounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: false);
                    return;
                }

                if (!_editor.Bounds.Contains(pointer) && _editor.ReadyForOutsideClose)
                {
                    CloseEditor(applyChanges: false);
                    return;
                }

                if (_editor.WheelBounds.Contains(pointer) && TryUpdateWheel(pointer))
                {
                    _editor.HexFocused = false;
                    _editor.DraggingWheel = true;
                }
                else if (_editor.SliderBounds.Contains(pointer))
                {
                    _editor.HexFocused = false;
                    UpdateValueFromPointer(pointer);
                    _editor.DraggingValue = true;
                }
                else if (_editor.HexBounds != Rectangle.Empty && _editor.HexBounds.Contains(pointer))
                {
                    bool wasFocused = _editor.HexFocused;
                    _editor.HexFocused = true;
                    _editor.HexFreshFocus = !wasFocused;
                }
                else
                {
                    _editor.HexFocused = false;
                    _editor.HexFreshFocus = false;
                }
            }

            if (_editor.DraggingWheel && leftDown)
            {
                TryUpdateWheel(pointer);
            }

            if (_editor.DraggingValue && leftDown)
            {
                UpdateValueFromPointer(pointer);
            }

            if (leftClickReleased)
            {
                _editor.ReadyForOutsideClose = true;

                if (_editor.ApplyBounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: true);
                    return;
                }

                if (_editor.CancelBounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: false);
                    return;
                }

                _editor.DraggingWheel = false;
                _editor.DraggingValue = false;
            }

            if (_editor.HexFocused)
            {
                HandleHexInput(keyboardState, previousKeyboardState, elapsedSeconds);
            }
            else
            {
                HexInputRepeater.Reset();
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Enter))
            {
                CloseEditor(applyChanges: true);
                return;
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Escape))
            {
                CloseEditor(applyChanges: false);
            }
        }

        private static void DrawEditor(SpriteBatch spriteBatch, Rectangle overlayBounds)
        {
            if (!_editor.IsActive)
            {
                return;
            }

            BuildEditorLayout(overlayBounds);
            EnsurePixel(spriteBatch);
            EnsureColorWheelTexture(spriteBatch.GraphicsDevice, _editor.WheelBounds.Width);

            DrawRect(spriteBatch, _editor.Bounds, ColorPalette.OverlayBackground);
            DrawRectOutline(spriteBatch, _editor.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont techFont = UIStyle.FontTech;

            string title = $"Edit {_editor.Label}";
            if (headerFont.IsAvailable)
            {
                Vector2 titleSize = headerFont.MeasureString(title);
                Vector2 titlePos = new(_editor.Bounds.X + (_editor.Bounds.Width - titleSize.X) / 2f, _editor.Bounds.Y + EditorPadding);
                headerFont.DrawString(spriteBatch, title, titlePos, UIStyle.TextColor);
            }

            if (_colorWheelTexture != null)
            {
                spriteBatch.Draw(_colorWheelTexture, _editor.WheelBounds, Color.White);
                DrawWheelIndicator(spriteBatch);
            }

            DrawValueSlider(spriteBatch);
            DrawPreview(spriteBatch, techFont);
            DrawHexInput(spriteBatch, techFont);
            DrawEditorButtons(spriteBatch);
        }

        private static void DrawPreview(SpriteBatch spriteBatch, UIStyle.UIFont techFont)
        {
            Rectangle preview = _editor.PreviewBounds;
            if (preview == Rectangle.Empty)
            {
                return;
            }

            int halfWidth = preview.Width / 2;
            Rectangle originalRect = new(preview.X, preview.Y, halfWidth - 2, preview.Height);
            Rectangle updatedRect = new(preview.X + halfWidth + 2, preview.Y, preview.Width - halfWidth - 2, preview.Height);

            DrawRect(spriteBatch, originalRect, _editor.OriginalColor);
            DrawRectOutline(spriteBatch, originalRect, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            DrawRect(spriteBatch, updatedRect, _editor.WorkingColor);
            DrawRectOutline(spriteBatch, updatedRect, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            if (techFont.IsAvailable)
            {
                Vector2 origLabelSize = techFont.MeasureString("Was");
                Vector2 newLabelSize = techFont.MeasureString("New");
                techFont.DrawString(spriteBatch, "Was", new Vector2(originalRect.X + (originalRect.Width - origLabelSize.X) / 2f, originalRect.Bottom + 4), UIStyle.MutedTextColor);
                techFont.DrawString(spriteBatch, "New", new Vector2(updatedRect.X + (updatedRect.Width - newLabelSize.X) / 2f, updatedRect.Bottom + 4), UIStyle.MutedTextColor);
            }
        }

        private static void DrawHexInput(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            Rectangle hex = _editor.HexBounds;
            if (hex == Rectangle.Empty || !font.IsAvailable)
            {
                return;
            }

            Color background = _editor.HexFocused ? ColorPalette.BlockBackground * 1.2f : ColorPalette.BlockBackground;
            Color border = _editor.HexFocused ? UIStyle.AccentColor : UIStyle.BlockBorder;
            DrawRect(spriteBatch, hex, background);
            DrawRectOutline(spriteBatch, hex, border, UIStyle.BlockBorderThickness);

            string text = string.IsNullOrWhiteSpace(_editor.HexBuffer) ? "#RRGGBB" : _editor.HexBuffer;
            Color textColor = string.IsNullOrWhiteSpace(_editor.HexBuffer) ? UIStyle.MutedTextColor : UIStyle.TextColor;
            Vector2 size = font.MeasureString(text);
            Vector2 pos = new(hex.X + 8, hex.Y + (hex.Height - size.Y) / 2f);
            font.DrawString(spriteBatch, text, pos, textColor);
        }

        private static void DrawEditorButtons(SpriteBatch spriteBatch)
        {
            UIButtonRenderer.Draw(spriteBatch, _editor.ApplyBounds, "Apply", UIButtonRenderer.ButtonStyle.Blue, UIButtonRenderer.IsHovered(_editor.ApplyBounds, _lastMousePosition));
            UIButtonRenderer.Draw(spriteBatch, _editor.CancelBounds, "Cancel", UIButtonRenderer.ButtonStyle.Grey, UIButtonRenderer.IsHovered(_editor.CancelBounds, _lastMousePosition));
        }

        private static void DrawSchemePrompt(SpriteBatch spriteBatch, Rectangle overlaySpace)
        {
            if (!_schemePrompt.IsOpen || spriteBatch == null)
            {
                return;
            }

            EnsurePixel(spriteBatch);
            Rectangle viewport = GetEditorViewport(overlaySpace);
            BuildSchemePromptLayout(viewport);

            FillRect(spriteBatch, viewport, ColorPalette.RebindScrim);

            Rectangle dialog = _schemePrompt.Bounds;
            if (dialog == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dialog, UIStyle.BlockBackground);
            DrawRectOutline(spriteBatch, dialog, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            UIStyle.UIFont inputFont = UIStyle.FontTech;
            if (!headerFont.IsAvailable || !bodyFont.IsAvailable || !inputFont.IsAvailable)
            {
                return;
            }

            string title = "Save color scheme";
            Vector2 titleSize = headerFont.MeasureString(title);
            Vector2 titlePos = new(dialog.X + (dialog.Width - titleSize.X) / 2f, dialog.Y + SchemePromptPadding);
            headerFont.DrawString(spriteBatch, title, titlePos, UIStyle.TextColor);

            string helper = "Name this scheme to store current colors.";
            Vector2 helperPos = new(dialog.X + SchemePromptPadding, titlePos.Y + titleSize.Y + 6f);
            bodyFont.DrawString(spriteBatch, helper, helperPos, UIStyle.MutedTextColor);

            Rectangle input = _schemePrompt.InputBounds;
            FillRect(spriteBatch, input, UIStyle.BlockBackground * 1.1f);
            DrawRectOutline(spriteBatch, input, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            string text = string.IsNullOrWhiteSpace(_schemePrompt.Buffer) ? "Scheme name" : _schemePrompt.Buffer;
            Color textColor = string.IsNullOrWhiteSpace(_schemePrompt.Buffer) ? UIStyle.MutedTextColor : UIStyle.TextColor;
            Vector2 textSize = TextSpacingHelper.MeasureWithWideSpaces(inputFont, text);
            Vector2 textPos = new(input.X + 8, input.Y + (input.Height - textSize.Y) / 2f);
            TextSpacingHelper.DrawWithWideSpaces(inputFont, spriteBatch, text, textPos, textColor);

            bool saveHovered = UIButtonRenderer.IsHovered(_schemePrompt.ConfirmBounds, _lastMousePosition);
            bool cancelHovered = UIButtonRenderer.IsHovered(_schemePrompt.CancelBounds, _lastMousePosition);
            bool disableSave = string.IsNullOrWhiteSpace(_schemePrompt.Buffer);
            UIButtonRenderer.Draw(spriteBatch, _schemePrompt.ConfirmBounds, "Save", UIButtonRenderer.ButtonStyle.Blue, saveHovered, disableSave);
            UIButtonRenderer.Draw(spriteBatch, _schemePrompt.CancelBounds, "Cancel", UIButtonRenderer.ButtonStyle.Grey, cancelHovered);
        }

        private static void DrawWheelIndicator(SpriteBatch spriteBatch)
        {
            Rectangle wheel = _editor.WheelBounds;
            Vector2 center = new(wheel.X + wheel.Width / 2f, wheel.Y + wheel.Height / 2f);
            float radius = wheel.Width / 2f;
            float angle = _editor.Hue * MathHelper.TwoPi;
            float distance = radius * _editor.Saturation;
            Vector2 point = new(center.X + MathF.Cos(angle) * distance, center.Y + MathF.Sin(angle) * distance);

            Rectangle indicator = new((int)MathF.Round(point.X) - 3, (int)MathF.Round(point.Y) - 3, 6, 6);
            DrawRect(spriteBatch, indicator, ColorPalette.BlockBackground);
            DrawRectOutline(spriteBatch, indicator, UIStyle.AccentColor, UIStyle.BlockBorderThickness);
        }

        private static void DrawValueSlider(SpriteBatch spriteBatch)
        {
            Rectangle slider = _editor.SliderBounds;
            if (slider == Rectangle.Empty || _pixel == null)
            {
                return;
            }

            for (int i = 0; i < slider.Height; i++)
            {
                float t = 1f - (i / (float)Math.Max(1, slider.Height - 1));
                Color c = FromHsv(_editor.Hue, _editor.Saturation, t, 255);
                Rectangle line = new(slider.X, slider.Y + i, slider.Width, 1);
                spriteBatch.Draw(_pixel, line, c);
            }

            int indicatorY = slider.Y + (int)MathF.Round((1f - _editor.Value) * slider.Height);
            Rectangle indicator = new(slider.X - 2, indicatorY - 1, slider.Width + 4, 3);
            DrawRect(spriteBatch, indicator, UIStyle.BlockBorder);
        }

        private static bool TryUpdateWheel(Point pointer)
        {
            Rectangle wheel = _editor.WheelBounds;
            Vector2 center = new(wheel.X + wheel.Width / 2f, wheel.Y + wheel.Height / 2f);
            float radius = wheel.Width / 2f;
            float dx = pointer.X - center.X;
            float dy = pointer.Y - center.Y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance > radius)
            {
                return false;
            }

            float angle = MathF.Atan2(dy, dx);
            if (angle < 0f)
            {
                angle += MathHelper.TwoPi;
            }

            _editor.Hue = MathHelper.Clamp(angle / MathHelper.TwoPi, 0f, 1f);
            _editor.Saturation = MathHelper.Clamp(distance / radius, 0f, 1f);
            ApplyHsvToEditorColor();
            return true;
        }

        private static void UpdateValueFromPointer(Point pointer)
        {
            Rectangle slider = _editor.SliderBounds;
            if (slider.Height <= 0)
            {
                return;
            }

            float relative = 1f - ((pointer.Y - slider.Y) / (float)slider.Height);
            _editor.Value = MathHelper.Clamp(relative, 0f, 1f);
            ApplyHsvToEditorColor();
        }

        private static void HandleHexInput(KeyboardState keyboardState, KeyboardState previousKeyboardState, double elapsedSeconds)
        {
            foreach (Keys key in HexInputRepeater.GetKeysWithRepeat(keyboardState, previousKeyboardState, elapsedSeconds))
            {
                bool newPress = !previousKeyboardState.IsKeyDown(key);

                if (key == Keys.Back || key == Keys.Delete)
                {
                    TrimHexCharacter();
                    continue;
                }

                if (key == Keys.Enter)
                {
                    if (newPress)
                    {
                        ApplyHexBuffer();
                    }
                    continue;
                }

                if (key == Keys.Escape)
                {
                    if (newPress)
                    {
                        _editor.HexFocused = false;
                    }
                    continue;
                }

                if (TryMapHexKey(key, out char hexChar))
                {
                    AppendHexCharacter(hexChar);
                }
            }
        }

        private static void ApplyHexBuffer()
        {
            if (ColorScheme.TryParseHex(_editor.HexBuffer, out Color parsed))
            {
                string trimmed = (_editor.HexBuffer ?? string.Empty).Trim();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    trimmed = trimmed[1..];
                }

                byte alpha = trimmed.Length == 8 ? parsed.A : _editor.Alpha;
                Color merged = new(parsed.R, parsed.G, parsed.B, alpha);
                SetEditorColor(merged);
            }
        }

        private static void SetEditorColor(Color color)
        {
            _editor.WorkingColor = color;
            _editor.HexBuffer = ColorScheme.ToHex(color, includeAlpha: false);
            _editor.Alpha = color.A;
            ToHsv(color, out _editor.Hue, out _editor.Saturation, out _editor.Value);
        }

        private static void ApplyHsvToEditorColor()
        {
            Color color = FromHsv(_editor.Hue, _editor.Saturation, _editor.Value, _editor.Alpha);
            _editor.WorkingColor = color;
            _editor.HexBuffer = ColorScheme.ToHex(color, includeAlpha: false);
        }

        private static void AppendHexCharacter(char c)
        {
            string buffer = _editor.HexBuffer ?? string.Empty;
            if (_editor.HexFreshFocus)
            {
                buffer = "#";
                _editor.HexFreshFocus = false;
            }

            if (string.IsNullOrWhiteSpace(buffer))
            {
                buffer = "#";
            }

            if (!buffer.StartsWith("#", StringComparison.Ordinal))
            {
                buffer = "#" + buffer;
            }

            if (buffer.Length >= 7)
            {
                _editor.HexBuffer = buffer;
                return;
            }

            _editor.HexBuffer = buffer + char.ToUpperInvariant(c);
        }

        private static void TrimHexCharacter()
        {
            string buffer = _editor.HexBuffer ?? string.Empty;
            _editor.HexFreshFocus = false;
            if (string.IsNullOrEmpty(buffer))
            {
                return;
            }

            buffer = buffer.TrimEnd();
            if (buffer.Length > 1)
            {
                buffer = buffer[..^1];
            }
            else
            {
                buffer = "#";
            }

            _editor.HexBuffer = buffer;
        }

        private static bool TryMapHexKey(Keys key, out char hexChar)
        {
            hexChar = default;

            if (key is >= Keys.D0 and <= Keys.D9)
            {
                hexChar = (char)('0' + (key - Keys.D0));
                return true;
            }

            if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
            {
                hexChar = (char)('0' + (key - Keys.NumPad0));
                return true;
            }

            if (key is >= Keys.A and <= Keys.F)
            {
                hexChar = (char)('A' + (key - Keys.A));
                return true;
            }

            return false;
        }

        private static bool WasKeyPressed(KeyboardState current, KeyboardState previous, Keys key)
        {
            return current.IsKeyDown(key) && !previous.IsKeyDown(key);
        }

        private static void OpenSchemePrompt()
        {
            _schemeDropdown.Close();
            SchemePromptRepeater.Reset();
            _schemePrompt = new SchemePromptState
            {
                IsOpen = true,
                Buffer = string.Empty
            };
            BuildSchemePromptLayout(_lastContentBounds);
        }

        private static void CloseSchemePrompt()
        {
            _schemePrompt = default;
            SchemePromptRepeater.Reset();
        }

        private static void BuildSchemePromptLayout(Rectangle overlaySpace)
        {
            Rectangle viewport = GetEditorViewport(overlaySpace);
            int width = Math.Min(SchemePromptWidth, Math.Max(120, viewport.Width - (SchemePromptPadding * 2)));
            int height = Math.Min(SchemePromptHeight, Math.Max(120, viewport.Height - (SchemePromptPadding * 2)));

            int x = viewport.X + (viewport.Width - width) / 2;
            int y = viewport.Y + (viewport.Height - height) / 2;
            _schemePrompt.Bounds = new Rectangle(x, y, width, height);

            int inputWidth = width - (SchemePromptPadding * 2);
            int inputHeight = HexInputHeight;
            int inputY = y + SchemePromptPadding + 44;
            _schemePrompt.InputBounds = new Rectangle(x + SchemePromptPadding, inputY, inputWidth, inputHeight);

            int buttonsY = _schemePrompt.InputBounds.Bottom + SchemePromptPadding;
            int buttonWidth = Math.Max(40, (inputWidth - SchemeControlSpacing) / 2);
            _schemePrompt.ConfirmBounds = new Rectangle(x + SchemePromptPadding, buttonsY, buttonWidth, SchemeButtonHeight);
            _schemePrompt.CancelBounds = new Rectangle(_schemePrompt.ConfirmBounds.Right + SchemeControlSpacing, buttonsY, buttonWidth, SchemeButtonHeight);
        }

        private static void UpdateSchemePrompt(MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState, double elapsedSeconds)
        {
            if (!_schemePrompt.IsOpen)
            {
                SchemePromptRepeater.Reset();
                return;
            }

            BuildSchemePromptLayout(_lastContentBounds);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;

            if (leftReleased)
            {
                if (_schemePrompt.ConfirmBounds.Contains(mouseState.Position))
                {
                    CommitSchemePrompt();
                    return;
                }

                if (_schemePrompt.CancelBounds.Contains(mouseState.Position))
                {
                    CloseSchemePrompt();
                    return;
                }
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Enter))
            {
                CommitSchemePrompt();
                return;
            }

            if (WasKeyPressed(keyboardState, previousKeyboardState, Keys.Escape))
            {
                CloseSchemePrompt();
                return;
            }

            HandleSchemeNameInput(keyboardState, previousKeyboardState, elapsedSeconds);
        }

        private static void HandleSchemeNameInput(KeyboardState current, KeyboardState previous, double elapsedSeconds)
        {
            bool shift = current.IsKeyDown(Keys.LeftShift) || current.IsKeyDown(Keys.RightShift);

            foreach (Keys key in SchemePromptRepeater.GetKeysWithRepeat(current, previous, elapsedSeconds))
            {
                if (key == Keys.Back)
                {
                    if (!string.IsNullOrEmpty(_schemePrompt.Buffer))
                    {
                        _schemePrompt.Buffer = _schemePrompt.Buffer[..^1];
                    }
                }
                else if (TryConvertToSchemeChar(key, shift, out char value))
                {
                    _schemePrompt.Buffer += value;
                }
            }
        }

        private static bool TryConvertToSchemeChar(Keys key, bool shift, out char value)
        {
            value = default;

            if (key is >= Keys.A and <= Keys.Z)
            {
                char baseChar = (char)('a' + (key - Keys.A));
                value = shift ? char.ToUpperInvariant(baseChar) : baseChar;
                return true;
            }

            if (key is >= Keys.D0 and <= Keys.D9)
            {
                value = (char)('0' + (key - Keys.D0));
                return true;
            }

            if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
            {
                value = (char)('0' + (key - Keys.NumPad0));
                return true;
            }

            if (key == Keys.Space)
            {
                value = ' ';
                return true;
            }

            if (key == Keys.OemMinus || key == Keys.Subtract)
            {
                value = shift ? '_' : '-';
                return true;
            }

            if (key == Keys.OemPeriod)
            {
                value = '.';
                return true;
            }

            return false;
        }

        private static void CommitSchemePrompt()
        {
            string name = _schemePrompt.Buffer?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (ColorScheme.SaveCurrentScheme(name, makeActive: true))
            {
                _selectedSchemeName = ColorScheme.ActiveSchemeName;
                _schemeListDirty = true;
            }

            CloseSchemePrompt();
        }

        private static void BuildEditorLayout(Rectangle availableBounds)
        {
            int desiredWidth = Math.Clamp(availableBounds.Width - 40, EditorMinWidth, EditorMaxWidth);
            int desiredHeight = Math.Clamp(availableBounds.Height - 80, EditorMinHeight, EditorMaxHeight);

            int overlayWidth = Math.Clamp(desiredWidth, EditorMinWidth, EditorMaxWidth);
            int overlayHeight = Math.Clamp(desiredHeight, EditorMinHeight, EditorMaxHeight);

            if (overlayWidth <= 0 || overlayHeight <= 0)
            {
                _editor.Bounds = Rectangle.Empty;
                _editor.WheelBounds = Rectangle.Empty;
                _editor.SliderBounds = Rectangle.Empty;
                _editor.PreviewBounds = Rectangle.Empty;
                _editor.HexBounds = Rectangle.Empty;
                _editor.ApplyBounds = Rectangle.Empty;
                _editor.CancelBounds = Rectangle.Empty;
                return;
            }

            int overlayX = availableBounds.X + (availableBounds.Width - overlayWidth) / 2;
            int overlayY = availableBounds.Y + (availableBounds.Height - overlayHeight) / 2;
            _editor.Bounds = new Rectangle(overlayX, overlayY, overlayWidth, overlayHeight);

            int wheelTopOffset = 48;
            int contentWidth = Math.Max(0, overlayWidth - (EditorPadding * 2));
            int wheelWidthSpace = Math.Max(0, contentWidth - (SliderWidth + SliderMargin));

            int buttonsY = _editor.Bounds.Bottom - ButtonHeight - EditorPadding;
            int availableHeight = Math.Max(0, buttonsY - wheelTopOffset);
            int minimumReservedBelowWheel = WheelToPreviewGap + PreviewMinHeight + PreviewLabelAllowance + PreviewToHexGap + HexMinHeight;
            int wheelHeightBudget = Math.Max(WheelMinSizeCompact, availableHeight - minimumReservedBelowWheel);

            int wheelSize = Math.Min(WheelMaxSize, Math.Min(wheelWidthSpace, wheelHeightBudget));
            if (wheelSize <= 0 && wheelWidthSpace > 0)
            {
                wheelSize = Math.Min(WheelMinSizeCompact, wheelWidthSpace);
            }
            else if (wheelSize < WheelMinSize && wheelWidthSpace >= WheelMinSize && wheelHeightBudget >= WheelMinSize)
            {
                wheelSize = Math.Min(WheelMaxSize, WheelMinSize);
            }

            int wheelX = overlayX + EditorPadding;
            int wheelY = overlayY + wheelTopOffset;
            _editor.WheelBounds = new Rectangle(wheelX, wheelY, wheelSize, wheelSize);
            _editor.SliderBounds = new Rectangle(_editor.WheelBounds.Right + SliderMargin, wheelY, SliderWidth, wheelSize);

            int previewY = _editor.WheelBounds.Bottom + WheelToPreviewGap;
            int previewWidth = contentWidth;
            int previewHeightBudget = Math.Max(0, availableHeight - wheelSize - PreviewLabelAllowance - PreviewToHexGap - HexMinHeight - WheelToPreviewGap);
            int previewHeight = Math.Min(PreviewHeight, Math.Max(PreviewMinHeight, previewHeightBudget));
            previewHeight = Math.Max(0, Math.Min(previewHeight, previewHeightBudget));
            _editor.PreviewBounds = previewWidth > 0 && previewHeight > 0 ? new Rectangle(overlayX + EditorPadding, previewY, previewWidth, previewHeight) : Rectangle.Empty;

            int hexY = previewY + previewHeight + PreviewLabelAllowance + PreviewToHexGap;
            int hexWidth = contentWidth;
            int maxHexHeight = Math.Max(0, buttonsY - hexY);
            int hexHeight = Math.Min(HexInputHeight, Math.Max(HexMinHeight, maxHexHeight));
            hexHeight = Math.Max(0, Math.Min(hexHeight, maxHexHeight));
            _editor.HexBounds = hexHeight > 0 ? new Rectangle(overlayX + EditorPadding, hexY, hexWidth, hexHeight) : Rectangle.Empty;

            _editor.ApplyBounds = new Rectangle(_editor.Bounds.Right - ButtonWidth - EditorPadding, buttonsY, ButtonWidth, ButtonHeight);
            _editor.CancelBounds = new Rectangle(_editor.ApplyBounds.X - ButtonWidth - 12, buttonsY, ButtonWidth, ButtonHeight);
        }

        private static Rectangle GetEditorViewport(Rectangle fallbackBounds)
        {
            GraphicsDevice device = Core.Instance?.GraphicsDevice;
            if (device != null)
            {
                Rectangle viewport = device.Viewport.Bounds;
                if (viewport.Width > 0 && viewport.Height > 0)
                {
                    return viewport;
                }
            }

            return fallbackBounds;
        }

        private static void EnsurePixel(SpriteBatch spriteBatch)
        {
            if (_pixel != null || spriteBatch?.GraphicsDevice == null)
            {
                return;
            }

            _pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        private static void EnsureColorWheelTexture(GraphicsDevice device, int targetSize)
        {
            if (_colorWheelTexture != null || device == null)
            {
                return;
            }

            int size = Math.Clamp(targetSize, WheelMinSize, WheelMaxSize);
            Color[] data = new Color[size * size];
            float radius = (size - 1) / 2f;
            Vector2 center = new(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.X;
                    float dy = y - center.Y;
                    float distance = MathF.Sqrt((dx * dx) + (dy * dy));
                    int idx = (y * size) + x;
                    if (distance > radius)
                    {
                        data[idx] = Color.Transparent;
                        continue;
                    }

                    float hue = MathF.Atan2(dy, dx);
                    if (hue < 0f)
                    {
                        hue += MathHelper.TwoPi;
                    }

                    float saturation = MathHelper.Clamp(distance / radius, 0f, 1f);
                    data[idx] = FromHsv(hue / MathHelper.TwoPi, saturation, 1f, 255);
                }
            }

            _colorWheelTexture = new Texture2D(device, size, size);
            _colorWheelTexture.SetData(data);
        }

        private static Texture2D EnsureIcon(Texture2D icon, string fileName)
        {
            if (icon != null && !icon.IsDisposed)
            {
                return icon;
            }

            return BlockIconProvider.GetIcon(fileName);
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixel == null || spriteBatch == null)
            {
                return;
            }

            spriteBatch.Draw(_pixel, bounds, color);
        }

        private static void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            FillRect(spriteBatch, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixel == null || spriteBatch == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(_pixel, top, color);
            spriteBatch.Draw(_pixel, bottom, color);
            spriteBatch.Draw(_pixel, left, color);
            spriteBatch.Draw(_pixel, right, color);
        }

        private static void PersistRowOrder()
        {
            List<ColorRole> order = _rows.Select(r => r.Role).ToList();
            ColorScheme.UpdateOrder(order);
        }

        private static bool TryGetRow(string rowKey, out ColorRow row)
        {
            row = default;
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                return false;
            }

            foreach (ColorRow candidate in _rows)
            {
                if (string.Equals(candidate.Key, rowKey, StringComparison.OrdinalIgnoreCase))
                {
                    row = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void ToHsv(Color color, out float h, out float s, out float v)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;

            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float delta = max - min;

            h = 0f;
            if (delta > 0f)
            {
                if (max == r)
                {
                    h = ((g - b) / delta) % 6f;
                }
                else if (max == g)
                {
                    h = ((b - r) / delta) + 2f;
                }
                else
                {
                    h = ((r - g) / delta) + 4f;
                }

                h /= 6f;
                if (h < 0f)
                {
                    h += 1f;
                }
            }

            v = max;
            s = max <= 0f ? 0f : delta / max;
        }

        private static Color FromHsv(float h, float s, float v, byte a)
        {
            h = MathHelper.Clamp(h, 0f, 1f);
            s = MathHelper.Clamp(s, 0f, 1f);
            v = MathHelper.Clamp(v, 0f, 1f);

            float c = v * s;
            float x = c * (1f - MathF.Abs((h * 6f % 2f) - 1f));
            float m = v - c;

            float r, g, b;
            if (h < 1f / 6f)
            {
                r = c; g = x; b = 0f;
            }
            else if (h < 2f / 6f)
            {
                r = x; g = c; b = 0f;
            }
            else if (h < 3f / 6f)
            {
                r = 0f; g = c; b = x;
            }
            else if (h < 4f / 6f)
            {
                r = 0f; g = x; b = c;
            }
            else if (h < 5f / 6f)
            {
                r = x; g = 0f; b = c;
            }
            else
            {
                r = c; g = 0f; b = x;
            }

            byte R = (byte)Math.Clamp((r + m) * 255f, 0f, 255f);
            byte G = (byte)Math.Clamp((g + m) * 255f, 0f, 255f);
            byte B = (byte)Math.Clamp((b + m) * 255f, 0f, 255f);

            return new Color(R, G, B, a);
        }

        private struct ColorRow
        {
            public ColorRole Role;
            public string Key;
            public string Label;
            public string Category;
            public Color Value;
            public Rectangle Bounds;
            public Rectangle HexBounds;
            public bool IsDragging;
            public bool IsLocked;
        }

        private struct ColorEditorState
        {
            public bool IsActive;
            public ColorRole Role;
            public string Label;
            public Color WorkingColor;
            public Color OriginalColor;
            public string HexBuffer;
            public bool HexFocused;
            public bool HexFreshFocus;
            public bool DraggingWheel;
            public bool DraggingValue;
            public float Hue;
            public float Saturation;
            public float Value;
            public byte Alpha;
            public bool ReadyForOutsideClose;
            public Rectangle Bounds;
            public Rectangle WheelBounds;
            public Rectangle SliderBounds;
            public Rectangle HexBounds;
            public Rectangle ApplyBounds;
            public Rectangle CancelBounds;
            public Rectangle PreviewBounds;
        }

        private struct SchemePromptState
        {
            public bool IsOpen;
            public string Buffer;
            public Rectangle Bounds;
            public Rectangle InputBounds;
            public Rectangle ConfirmBounds;
            public Rectangle CancelBounds;
        }
    }
}
