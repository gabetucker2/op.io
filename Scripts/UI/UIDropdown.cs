using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    /// <summary>
    /// Lightweight dropdown control that can be reused by blocks.
    /// Keeps state internally so callers only need to set bounds, options,
    /// and ask for updates/draws.
    /// </summary>
    internal sealed class UIDropdown
    {
        internal readonly record struct Option(
            string Id,
            string Label,
            bool IsDisabled = false,
            string TooltipKey = null,
            string TooltipLabel = null);

        private readonly List<Option> _options = new();
        private Texture2D _pixel;
        private bool _isOpen;
        private int _hoveredIndex = -1;
        private int _hoveredToggleIndex = -1;
        private const int ToggleCheckboxHorizontalPadding = 8;
        private const int ToggleCheckboxSize = 12;
        private const int ToggleCheckboxVerticalPadding = 0;
        private const int ToggleCheckboxLabelGap = 8;

        public Rectangle Bounds { get; set; }
        public string SelectedId { get; private set; }
        public int MaxVisibleOptions { get; set; } = 6;
        public bool ShowOptionDisableToggles { get; set; } = false;
        public bool PreventSelectingDisabledOptions { get; set; } = true;

        public bool HasOptions => _options.Count > 0;
        public bool IsOpen => _isOpen;

        public bool IsPointerOverDropdown(Point pointer)
        {
            if (Bounds.Contains(pointer))
            {
                return true;
            }

            if (_isOpen)
            {
                Rectangle listBounds = GetListBounds();
                if (listBounds.Contains(pointer))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetHoveredOption(out Option option)
        {
            option = default;
            if (!_isOpen)
            {
                return false;
            }

            int index = _hoveredIndex >= 0 ? _hoveredIndex : _hoveredToggleIndex;
            if (index < 0 || index >= _options.Count || index >= Math.Max(1, MaxVisibleOptions))
            {
                return false;
            }

            option = _options[index];
            return !string.IsNullOrWhiteSpace(option.Id);
        }

        public string GetHoveredOptionTooltipKey()
        {
            if (!TryGetHoveredOption(out Option option))
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(option.TooltipKey) ? null : option.TooltipKey;
        }

        public string GetHoveredOptionTooltipLabel()
        {
            if (!TryGetHoveredOption(out Option option))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(option.TooltipLabel))
            {
                return option.TooltipLabel;
            }

            return string.IsNullOrWhiteSpace(option.Label) ? option.Id : option.Label;
        }

        public void SetOptions(IEnumerable<Option> options, string selectedId = null)
        {
            _options.Clear();
            if (options != null)
            {
                foreach (Option option in options)
                {
                    if (string.IsNullOrWhiteSpace(option.Id))
                    {
                        continue;
                    }

                    string label = string.IsNullOrWhiteSpace(option.Label) ? option.Id : option.Label;
                    string tooltipLabel = string.IsNullOrWhiteSpace(option.TooltipLabel) ? label : option.TooltipLabel;
                    _options.Add(new Option(option.Id, label, option.IsDisabled, option.TooltipKey, tooltipLabel));
                }
            }

            if (_options.Count == 0)
            {
                SelectedId = null;
                _isOpen = false;
                _hoveredIndex = -1;
                _hoveredToggleIndex = -1;
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                Option match = _options.FirstOrDefault(o => string.Equals(o.Id, selectedId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match.Id) && (!PreventSelectingDisabledOptions || !match.IsDisabled))
                {
                    SelectedId = match.Id;
                    return;
                }
            }

            Option selected = _options.FirstOrDefault(option => !option.IsDisabled || !PreventSelectingDisabledOptions);
            SelectedId = !string.IsNullOrWhiteSpace(selected.Id)
                ? selected.Id
                : _options[0].Id;
        }

        public bool Update(MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState, out string selectionChangedId, bool isDisabled = false)
        {
            _ = Update(
                mouseState,
                previousMouseState,
                keyboardState,
                previousKeyboardState,
                out selectionChangedId,
                out _,
                out _,
                isDisabled);

            return selectionChangedId != null;
        }

        public bool Update(
            MouseState mouseState,
            MouseState previousMouseState,
            KeyboardState keyboardState,
            KeyboardState previousKeyboardState,
            out string selectionChangedId,
            out string toggleChangedId,
            out bool toggledDisabledState,
            bool isDisabled = false)
        {
            selectionChangedId = null;
            toggleChangedId = null;
            toggledDisabledState = false;

            if (Bounds == Rectangle.Empty || !HasOptions || isDisabled)
            {
                _isOpen = false;
                _hoveredIndex = -1;
                _hoveredToggleIndex = -1;
                return false;
            }

            bool leftClick = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftRelease = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            Point pointer = mouseState.Position;

            if (_isOpen)
            {
                Rectangle listBounds = GetListBounds();
                _hoveredIndex = HitTestOption(pointer, listBounds);
                _hoveredToggleIndex = HitTestToggle(pointer, listBounds);

                if (leftRelease && _hoveredToggleIndex >= 0)
                {
                    Option toggled = _options[_hoveredToggleIndex];
                    if (!string.IsNullOrWhiteSpace(toggled.Id))
                    {
                        toggleChangedId = toggled.Id;
                        toggledDisabledState = !toggled.IsDisabled;
                        return true;
                    }
                }

                if (leftRelease && _hoveredIndex >= 0)
                {
                    Option selected = _options[_hoveredIndex];
                    bool canSelect = !PreventSelectingDisabledOptions || !selected.IsDisabled;
                    if (canSelect)
                    {
                        SelectedId = selected.Id;
                        selectionChangedId = SelectedId;
                    }

                    // Any non-toggle option click closes the dropdown, even when the
                    // option cannot be selected (for example disabled options).
                    _isOpen = false;
                    return canSelect;
                }

                if (leftClick && !listBounds.Contains(pointer) && !Bounds.Contains(pointer))
                {
                    _isOpen = false;
                }

                if (WasEscapePressed(keyboardState, previousKeyboardState))
                {
                    _isOpen = false;
                }
            }
            else
            {
                _hoveredIndex = -1;
                _hoveredToggleIndex = -1;
                if (leftRelease && Bounds.Contains(pointer))
                {
                    _isOpen = true;
                }
            }

            return selectionChangedId != null || toggleChangedId != null;
        }

        public void Draw(SpriteBatch spriteBatch, bool drawOptions = true, bool isDisabled = false)
        {
            if (spriteBatch == null || Bounds == Rectangle.Empty || !HasOptions)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontBody;
            if (!font.IsAvailable)
            {
                return;
            }

            EnsurePixel(spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice);
            Color background = UIStyle.BlockBackground;
            Color border = UIStyle.BlockBorder;
            Color textColor = isDisabled ? UIStyle.MutedTextColor : UIStyle.TextColor;

            FillRect(spriteBatch, Bounds, background);
            DrawRectOutline(spriteBatch, Bounds, border, UIStyle.BlockBorderThickness);

            string label = GetSelectedLabel();
            Vector2 textSize = font.MeasureString(label);
            Vector2 textPos = new(Bounds.X + 10, Bounds.Y + (Bounds.Height - textSize.Y) / 2f);
            font.DrawString(spriteBatch, label, textPos, textColor);

            DrawCaret(spriteBatch, font);

            if (_isOpen && drawOptions)
            {
                DrawOptions(spriteBatch, font);
            }
        }

        public void DrawOptionsOverlay(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null || Bounds == Rectangle.Empty || !HasOptions || !_isOpen)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontBody;
            if (!font.IsAvailable)
            {
                return;
            }

            EnsurePixel(spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice);
            DrawOptions(spriteBatch, font);
        }

        public void Close()
        {
            _isOpen = false;
        }

        private string GetSelectedLabel()
        {
            Option match = _options.FirstOrDefault(o => string.Equals(o.Id, SelectedId, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(match.Id))
            {
                return string.Empty;
            }

            string label = string.IsNullOrWhiteSpace(match.Label) ? match.Id : match.Label;
            return match.IsDisabled ? $"{label} (Disabled)" : label;
        }

        private void DrawOptions(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            Rectangle listBounds = GetListBounds();
            if (listBounds.Height <= 0)
            {
                return;
            }

            FillRect(spriteBatch, listBounds, UIStyle.BlockBackground * 1.05f);
            DrawRectOutline(spriteBatch, listBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            int optionHeight = GetOptionHeight(font);
            int count = Math.Min(_options.Count, Math.Max(1, MaxVisibleOptions));
            int y = listBounds.Y;

            for (int i = 0; i < count; i++)
            {
                Rectangle optionBounds = new(listBounds.X, y, listBounds.Width, optionHeight);
                bool hovered = _hoveredIndex == i || _hoveredToggleIndex == i;
                if (hovered)
                {
                    FillRect(spriteBatch, optionBounds, ColorPalette.RowHover);
                }

                Option option = _options[i];
                string optionLabel = string.IsNullOrWhiteSpace(option.Label) ? option.Id : option.Label;
                Vector2 size = font.MeasureString(optionLabel);
                Rectangle labelBounds = GetOptionLabelBounds(optionBounds);
                Vector2 pos = new(labelBounds.X + 2, optionBounds.Y + (optionHeight - size.Y) / 2f);
                Color optionColor = option.IsDisabled ? UIStyle.MutedTextColor : UIStyle.TextColor;
                font.DrawString(spriteBatch, optionLabel, pos, optionColor);

                if (option.IsDisabled)
                {
                    int strikeStartX = (int)MathF.Round(pos.X);
                    int strikeY = optionBounds.Y + (optionHeight / 2);
                    int strikeWidth = Math.Max(0, Math.Min((int)MathF.Ceiling(size.X), Math.Max(0, labelBounds.Width - 4)));
                    if (strikeWidth > 0)
                    {
                        FillRect(spriteBatch, new Rectangle(strikeStartX, strikeY, strikeWidth, 1), UIStyle.MutedTextColor * 0.95f);
                    }
                }

                if (ShowOptionDisableToggles)
                {
                    Rectangle toggleBounds = GetOptionToggleBounds(optionBounds);
                    if (toggleBounds != Rectangle.Empty)
                    {
                        bool toggleHovered = _hoveredToggleIndex == i;
                        DrawDisableCheckbox(spriteBatch, font, toggleBounds, option.IsDisabled, toggleHovered);
                    }
                }

                y += optionHeight;
            }
        }

        private Rectangle GetListBounds()
        {
            int optionHeight = GetOptionHeight(UIStyle.FontBody);
            int visibleCount = Math.Min(_options.Count, Math.Max(1, MaxVisibleOptions));
            int height = optionHeight * visibleCount;
            return new Rectangle(Bounds.X, Bounds.Bottom + 4, Bounds.Width, height);
        }

        private int GetOptionHeight(UIStyle.UIFont font)
        {
            if (!font.IsAvailable)
            {
                return 24;
            }

            return (int)MathF.Max(24f, font.LineHeight + 8f);
        }

        private int HitTestOption(Point pointer, Rectangle listBounds)
        {
            if (!listBounds.Contains(pointer))
            {
                return -1;
            }

            int optionHeight = GetOptionHeight(UIStyle.FontBody);
            int index = (pointer.Y - listBounds.Y) / optionHeight;
            return index >= 0 && index < _options.Count && index < MaxVisibleOptions ? index : -1;
        }

        private int HitTestToggle(Point pointer, Rectangle listBounds)
        {
            if (!ShowOptionDisableToggles || !listBounds.Contains(pointer))
            {
                return -1;
            }

            int optionHeight = GetOptionHeight(UIStyle.FontBody);
            int index = (pointer.Y - listBounds.Y) / optionHeight;
            if (index < 0 || index >= _options.Count || index >= MaxVisibleOptions)
            {
                return -1;
            }

            Rectangle optionBounds = new(
                listBounds.X,
                listBounds.Y + (index * optionHeight),
                listBounds.Width,
                optionHeight);
            Rectangle toggleBounds = GetOptionToggleBounds(optionBounds);
            return toggleBounds.Contains(pointer) ? index : -1;
        }

        private Rectangle GetOptionLabelBounds(Rectangle optionBounds)
        {
            if (!ShowOptionDisableToggles)
            {
                return optionBounds;
            }

            Rectangle toggleBounds = GetOptionToggleBounds(optionBounds);
            if (toggleBounds == Rectangle.Empty)
            {
                return optionBounds;
            }

            int width = Math.Max(0, toggleBounds.X - optionBounds.X - ToggleCheckboxLabelGap);
            return new Rectangle(optionBounds.X, optionBounds.Y, width, optionBounds.Height);
        }

        private Rectangle GetOptionToggleBounds(Rectangle optionBounds)
        {
            if (!ShowOptionDisableToggles || optionBounds == Rectangle.Empty)
            {
                return Rectangle.Empty;
            }

            int width = ToggleCheckboxSize;
            int height = ToggleCheckboxSize;
            int x = optionBounds.Right - width - ToggleCheckboxHorizontalPadding;
            int y = optionBounds.Y + ((optionBounds.Height - height) / 2) + ToggleCheckboxVerticalPadding;

            if (height <= 0 || x <= optionBounds.X)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(x, y, width, height);
        }

        private void DrawCaret(SpriteBatch spriteBatch, UIStyle.UIFont font)
        {
            if (_pixel == null)
            {
                return;
            }

            string caretGlyph = "v";
            Vector2 size = font.MeasureString(caretGlyph);
            Vector2 pos = new(Bounds.Right - size.X - 10, Bounds.Y + (Bounds.Height - size.Y) / 2f);
            font.DrawString(spriteBatch, caretGlyph, pos, UIStyle.MutedTextColor);
        }

        private void DrawDisableCheckbox(SpriteBatch spriteBatch, UIStyle.UIFont font, Rectangle bounds, bool isDisabled, bool hovered)
        {
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            Color fill = hovered ? ColorPalette.RowHover : UIStyle.BlockBackground;
            Color border = hovered ? UIStyle.AccentColor : UIStyle.BlockBorder;
            FillRect(spriteBatch, bounds, fill);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            if (!isDisabled || !font.IsAvailable)
            {
                return;
            }

            const string glyph = "X";
            Vector2 size = font.MeasureString(glyph);
            Vector2 pos = new(
                bounds.X + (bounds.Width - size.X) / 2f,
                bounds.Y + (bounds.Height - size.Y) / 2f - 1f);
            font.DrawString(spriteBatch, glyph, pos, UIStyle.MutedTextColor);
        }

        private void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixel == null || spriteBatch == null)
            {
                return;
            }

            spriteBatch.Draw(_pixel, bounds, color);
        }

        private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixel == null || bounds.Width <= 0 || bounds.Height <= 0 || spriteBatch == null)
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

        private bool WasEscapePressed(KeyboardState current, KeyboardState previous) =>
            current.IsKeyDown(Keys.Escape) && previous.IsKeyUp(Keys.Escape);

        private void EnsurePixel(GraphicsDevice device)
        {
            if (_pixel != null || device == null)
            {
                return;
            }

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
    }
}
