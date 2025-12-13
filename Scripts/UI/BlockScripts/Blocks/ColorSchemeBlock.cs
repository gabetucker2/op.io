using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

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
        private const int WheelMinSize = 160;
        private const int WheelMaxSize = 260;
        private const int SliderWidth = 14;
        private const int SliderMargin = 10;
        private const int ButtonHeight = 30;
        private const int ButtonWidth = 100;

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
        private static KeyboardState _previousKeyboardState;
        private static Point _lastMousePosition;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            ColorScheme.Initialize();
            EnsureRows();
            EnsureLineHeight();

            float contentHeight = Math.Max(0f, _rows.Count * _lineHeight);
            _scrollPanel.Update(contentBounds, contentHeight, mouseState, previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            UpdateRowBounds(listBounds);

            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickStarted = leftDown && !leftDownPrev;
            bool leftClickReleased = !leftDown && leftDownPrev;
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.ColorScheme);
            bool pointerInsideList = listBounds.Contains(mouseState.Position);

            if (blockLocked && _dragState.IsDragging)
            {
                _dragState.Reset();
            }

            if (_editor.IsActive)
            {
                UpdateEditor(contentBounds, mouseState, previousMouseState, keyboardState, previousKeyboardState);
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
                if (_dragState.TryStartDrag(_rows, _hoveredRowKey, mouseState))
                {
                    // dragging handled by BlockDragState
                }
                else if (!string.IsNullOrWhiteSpace(_hoveredRowKey))
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

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = contentBounds;
            }

            UIStyle.UIFont labelFont = UIStyle.FontBody;
            UIStyle.UIFont valueFont = UIStyle.FontTech;
            UIStyle.UIFont placeholderFont = UIStyle.FontH2;

            if (!labelFont.IsAvailable || !valueFont.IsAvailable)
            {
                return;
            }

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
                return;
            }

            float lineHeight = _lineHeight;

            foreach (ColorRow row in _rows)
            {
                bool isDraggingRow = _dragState.IsDragging && string.Equals(row.Key, _dragState.DraggingKey, StringComparison.OrdinalIgnoreCase);
                if (!isDraggingRow)
                {
                    DrawRow(spriteBatch, row, labelFont, valueFont);
                }
            }

            if (_dragState.IsDragging && _dragState.HasSnapshot)
            {
                if (_dragState.DropIndicatorBounds != Rectangle.Empty)
                {
                    FillRect(spriteBatch, _dragState.DropIndicatorBounds, ColorPalette.DropIndicator);
                }

                DrawDraggingRow(spriteBatch, _dragState.DraggingSnapshot, labelFont, valueFont, listBounds, lineHeight);
            }

            _scrollPanel.Draw(spriteBatch);

            if (_editor.IsActive)
            {
                DrawEditor(spriteBatch, contentBounds);
            }
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
                row.IsDragging = false;
                _rows.Add(row);
            }
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

        private static void DrawRow(SpriteBatch spriteBatch, ColorRow row, UIStyle.UIFont labelFont, UIStyle.UIFont valueFont)
        {
            Rectangle bounds = row.Bounds;
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            bool hovered = string.Equals(_hoveredRowKey, row.Key, StringComparison.OrdinalIgnoreCase);
            Color background = hovered ? ColorPalette.RowHover : UIStyle.BlockBackground;
            FillRect(spriteBatch, bounds, background);

            Rectangle swatch = GetSwatchBounds(bounds);
            FillRect(spriteBatch, swatch, row.Value);
            DrawRectOutline(spriteBatch, swatch, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            float textY = bounds.Y + (bounds.Height - labelFont.LineHeight) / 2f;
            Vector2 labelPos = new(swatch.Right + SwatchPadding, textY);
            labelFont.DrawString(spriteBatch, row.Label, labelPos, UIStyle.TextColor);

            string hex = ColorScheme.ToHex(row.Value);
            Vector2 hexSize = valueFont.MeasureString(hex);
            float hexY = bounds.Y + (bounds.Height - hexSize.Y) / 2f;
            Vector2 hexPos = new(bounds.Right - hexSize.X - SwatchPadding, hexY);
            valueFont.DrawString(spriteBatch, hex, hexPos, UIStyle.MutedTextColor);
        }

        private static void DrawDraggingRow(SpriteBatch spriteBatch, ColorRow snapshot, UIStyle.UIFont labelFont, UIStyle.UIFont valueFont, Rectangle listBounds, float lineHeight)
        {
            Rectangle dragBounds = _dragState.GetDragBounds(listBounds, lineHeight);
            if (dragBounds == Rectangle.Empty)
            {
                return;
            }

            FillRect(spriteBatch, dragBounds, ColorPalette.RowDragging);
            snapshot.Bounds = dragBounds;
            DrawRow(spriteBatch, snapshot, labelFont, valueFont);
        }

        private static Rectangle GetSwatchBounds(Rectangle rowBounds)
        {
            int size = Math.Min(SwatchSize, Math.Max(12, rowBounds.Height - (RowVerticalPadding * 2)));
            int swatchY = rowBounds.Y + (rowBounds.Height - size) / 2;
            return new Rectangle(rowBounds.X + SwatchPadding, swatchY, size, size);
        }

        private static void BeginEdit(string rowKey)
        {
            ColorRow? row = _rows.FirstOrDefault(r => string.Equals(r.Key, rowKey, StringComparison.OrdinalIgnoreCase));
            if (!row.HasValue)
            {
                return;
            }

            ColorRow target = row.Value;
            _editor = new ColorEditorState
            {
                IsActive = true,
                Role = target.Role,
                Label = target.Label,
                WorkingColor = target.Value,
                OriginalColor = target.Value,
                HexBuffer = ColorScheme.ToHex(target.Value),
                HexFocused = false
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
        }

        private static void UpdateEditor(Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            if (!_editor.IsActive)
            {
                return;
            }

            BuildEditorLayout(contentBounds);
            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickStarted = leftDown && !leftDownPrev;
            bool leftClickReleased = !leftDown && leftDownPrev;
            Point pointer = mouseState.Position;

            if (leftClickStarted)
            {
                if (_editor.WheelBounds.Contains(pointer) && TryUpdateWheel(pointer))
                {
                    _editor.DraggingWheel = true;
                }
                else if (_editor.SliderBounds.Contains(pointer))
                {
                    UpdateValueFromPointer(pointer);
                    _editor.DraggingValue = true;
                }
                else if (_editor.HexBounds.Contains(pointer))
                {
                    _editor.HexFocused = true;
                }
                else if (_editor.ApplyBounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: true);
                    return;
                }
                else if (_editor.CancelBounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: false);
                    return;
                }
                else if (!_editor.Bounds.Contains(pointer))
                {
                    CloseEditor(applyChanges: false);
                    return;
                }
                else
                {
                    _editor.HexFocused = false;
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
                _editor.DraggingWheel = false;
                _editor.DraggingValue = false;
            }

            if (_editor.HexFocused)
            {
                HandleHexInput(keyboardState, previousKeyboardState);
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

        private static void DrawEditor(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (!_editor.IsActive)
            {
                return;
            }

            BuildEditorLayout(contentBounds);
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
            DrawRect(spriteBatch, hex, background);
            DrawRectOutline(spriteBatch, hex, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            string text = string.IsNullOrWhiteSpace(_editor.HexBuffer) ? "#RRGGBBAA" : _editor.HexBuffer;
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

        private static void HandleHexInput(KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (previousKeyboardState.IsKeyDown(key))
                {
                    continue;
                }

                if (key == Keys.Back || key == Keys.Delete)
                {
                    TrimHexCharacter();
                    continue;
                }

                if (key == Keys.Enter)
                {
                    ApplyHexBuffer();
                    continue;
                }

                if (key == Keys.Escape)
                {
                    _editor.HexFocused = false;
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
                SetEditorColor(parsed);
            }
        }

        private static void SetEditorColor(Color color)
        {
            _editor.WorkingColor = color;
            _editor.HexBuffer = ColorScheme.ToHex(color);
            _editor.Alpha = color.A;
            ToHsv(color, out _editor.Hue, out _editor.Saturation, out _editor.Value);
        }

        private static void ApplyHsvToEditorColor()
        {
            Color color = FromHsv(_editor.Hue, _editor.Saturation, _editor.Value, _editor.Alpha);
            _editor.WorkingColor = color;
            _editor.HexBuffer = ColorScheme.ToHex(color);
        }

        private static void AppendHexCharacter(char c)
        {
            string buffer = _editor.HexBuffer ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buffer))
            {
                buffer = "#";
            }

            if (!buffer.StartsWith("#", StringComparison.Ordinal))
            {
                buffer = "#" + buffer;
            }

            if (buffer.Length >= 9)
            {
                _editor.HexBuffer = buffer;
                return;
            }

            _editor.HexBuffer = buffer + char.ToUpperInvariant(c);
        }

        private static void TrimHexCharacter()
        {
            string buffer = _editor.HexBuffer ?? string.Empty;
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

        private static void BuildEditorLayout(Rectangle contentBounds)
        {
            int maxOverlayWidth = Math.Max(0, contentBounds.Width - (EditorPadding * 2));
            int maxOverlayHeight = Math.Max(0, contentBounds.Height - (EditorPadding * 2));

            int desiredWidth = Math.Clamp(contentBounds.Width - 20, 320, 540);
            int desiredHeight = Math.Clamp(contentBounds.Height - 20, 280, 440);

            int overlayWidth = Math.Max(0, Math.Min(desiredWidth, maxOverlayWidth));
            int overlayHeight = Math.Max(0, Math.Min(desiredHeight, maxOverlayHeight));

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

            int overlayX = contentBounds.X + (contentBounds.Width - overlayWidth) / 2;
            int overlayY = contentBounds.Y + (contentBounds.Height - overlayHeight) / 2;
            _editor.Bounds = new Rectangle(overlayX, overlayY, overlayWidth, overlayHeight);

            int availableWheel = Math.Min(overlayWidth - 180, overlayHeight - 120);
            int wheelSize = Math.Min(WheelMaxSize, Math.Max(0, availableWheel));
            int wheelX = overlayX + EditorPadding;
            int wheelY = overlayY + 48;
            _editor.WheelBounds = new Rectangle(wheelX, wheelY, wheelSize, wheelSize);
            _editor.SliderBounds = new Rectangle(_editor.WheelBounds.Right + SliderMargin, wheelY, SliderWidth, wheelSize);

            int previewWidth = Math.Max(0, Math.Min(120, overlayWidth - (_editor.SliderBounds.Right - overlayX) - (EditorPadding * 2)));
            _editor.PreviewBounds = previewWidth > 0 ? new Rectangle(_editor.SliderBounds.Right + SliderMargin, wheelY, previewWidth, 36) : Rectangle.Empty;

            int hexY = _editor.WheelBounds.Bottom + EditorPadding;
            int hexWidth = Math.Max(0, overlayWidth - (EditorPadding * 2));
            _editor.HexBounds = new Rectangle(overlayX + EditorPadding, hexY, hexWidth, 30);

            int buttonsY = _editor.Bounds.Bottom - ButtonHeight - EditorPadding;
            _editor.ApplyBounds = new Rectangle(_editor.Bounds.Right - ButtonWidth - EditorPadding, buttonsY, ButtonWidth, ButtonHeight);
            _editor.CancelBounds = new Rectangle(_editor.ApplyBounds.X - ButtonWidth - 12, buttonsY, ButtonWidth, ButtonHeight);
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
            public bool IsDragging;
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
            public bool DraggingWheel;
            public bool DraggingValue;
            public float Hue;
            public float Saturation;
            public float Value;
            public byte Alpha;
            public Rectangle Bounds;
            public Rectangle WheelBounds;
            public Rectangle SliderBounds;
            public Rectangle HexBounds;
            public Rectangle ApplyBounds;
            public Rectangle CancelBounds;
            public Rectangle PreviewBounds;
        }
    }
}
