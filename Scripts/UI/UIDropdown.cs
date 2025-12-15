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
        internal readonly record struct Option(string Id, string Label);

        private readonly List<Option> _options = new();
        private Texture2D _pixel;
        private bool _isOpen;
        private int _hoveredIndex = -1;

        public Rectangle Bounds { get; set; }
        public string SelectedId { get; private set; }
        public int MaxVisibleOptions { get; set; } = 6;

        public bool HasOptions => _options.Count > 0;
        public bool IsOpen => _isOpen;

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
                    _options.Add(new Option(option.Id, label));
                }
            }

            if (_options.Count == 0)
            {
                SelectedId = null;
                _isOpen = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                Option match = _options.FirstOrDefault(o => string.Equals(o.Id, selectedId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match.Id))
                {
                    SelectedId = match.Id;
                    return;
                }
            }

            SelectedId = _options[0].Id;
        }

        public bool Update(MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState, out string selectionChangedId, bool isDisabled = false)
        {
            selectionChangedId = null;

            if (Bounds == Rectangle.Empty || !HasOptions || isDisabled)
            {
                _isOpen = false;
                return false;
            }

            bool leftClick = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool leftRelease = mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed;
            Point pointer = mouseState.Position;

            if (_isOpen)
            {
                Rectangle listBounds = GetListBounds();
                _hoveredIndex = HitTestOption(pointer, listBounds);

                if (leftRelease && _hoveredIndex >= 0)
                {
                    SelectedId = _options[_hoveredIndex].Id;
                    selectionChangedId = SelectedId;
                    _isOpen = false;
                    return true;
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
                if (leftRelease && Bounds.Contains(pointer))
                {
                    _isOpen = true;
                }
            }

            return selectionChangedId != null;
        }

        public void Draw(SpriteBatch spriteBatch, bool drawOptions = true)
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
            Color textColor = UIStyle.TextColor;

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
            return string.IsNullOrWhiteSpace(match.Label) ? (match.Id ?? string.Empty) : match.Label;
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
                bool hovered = _hoveredIndex == i;
                if (hovered)
                {
                    FillRect(spriteBatch, optionBounds, ColorPalette.RowHover);
                }

                string optionLabel = string.IsNullOrWhiteSpace(_options[i].Label) ? _options[i].Id : _options[i].Label;
                Vector2 size = font.MeasureString(optionLabel);
                Vector2 pos = new(optionBounds.X + 10, optionBounds.Y + (optionHeight - size.Y) / 2f);
                font.DrawString(spriteBatch, optionLabel, pos, UIStyle.TextColor);
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
