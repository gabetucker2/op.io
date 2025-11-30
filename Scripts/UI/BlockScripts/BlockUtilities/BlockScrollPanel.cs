using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal sealed class BlockScrollPanel
    {
        private const int ScrollbarWidth = 7;
        private const int ScrollbarPadding = 4;
        private const int ThumbMinHeight = 24;
        private const float ScrollPixelsPerNotch = 60f;
        private const int TrackCornerRadius = 5;
        private const int ThumbCornerRadius = 4;

        private float _scrollOffset;
        private float _maxOffset;
        private bool _scrollbarVisible;
        private Rectangle _viewportBounds;
        private Rectangle _contentViewportBounds;
        private Rectangle _trackBounds;
        private Rectangle _thumbBounds;
        private bool _draggingThumb;
        private int _thumbDragOffset;
        private Point _lastMousePosition;
        private Texture2D _pixelTexture;

        private static readonly Color TrackColor = new(24, 24, 24, 220);
        private static readonly Color ThumbColor = new(95, 95, 95, 255);
        private static readonly Color ThumbHoverColor = new(136, 136, 136, 255);
        private static readonly Dictionary<int, Texture2D> _cornerTextures = new();

        public float ScrollOffset => _scrollOffset;
        public bool IsScrollbarVisible => _scrollbarVisible;
        public Rectangle ContentViewportBounds => _contentViewportBounds == Rectangle.Empty ? _viewportBounds : _contentViewportBounds;

        public void Reset()
        {
            _scrollOffset = 0f;
            _maxOffset = 0f;
            _draggingThumb = false;
            _scrollbarVisible = false;
            _trackBounds = Rectangle.Empty;
            _thumbBounds = Rectangle.Empty;
            _contentViewportBounds = Rectangle.Empty;
        }

        public void Update(Rectangle viewportBounds, float contentHeight, MouseState mouseState, MouseState previousMouseState)
        {
            _viewportBounds = viewportBounds;
            _contentViewportBounds = viewportBounds;

            float clampedContentHeight = Math.Max(0f, contentHeight);
            _scrollbarVisible = clampedContentHeight > viewportBounds.Height + 0.5f && viewportBounds.Height > 0;

            if (_scrollbarVisible)
            {
                int reserve = ScrollbarWidth + ScrollbarPadding;
                _contentViewportBounds.Width = Math.Max(0, _contentViewportBounds.Width - reserve);
                _trackBounds = new Rectangle(viewportBounds.Right - ScrollbarWidth, viewportBounds.Y, ScrollbarWidth, viewportBounds.Height);
            }
            else
            {
                _trackBounds = Rectangle.Empty;
                _thumbBounds = Rectangle.Empty;
                _draggingThumb = false;
            }

            float viewportHeight = Math.Max(0, viewportBounds.Height);
            _maxOffset = Math.Max(0f, clampedContentHeight - viewportHeight);
            _scrollOffset = MathHelper.Clamp(_scrollOffset, 0f, _maxOffset);

            UpdateThumbBounds(viewportHeight, clampedContentHeight);
            HandleInput(mouseState, previousMouseState);
            UpdateThumbBounds(viewportHeight, clampedContentHeight);

            _lastMousePosition = mouseState.Position;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_scrollbarVisible || spriteBatch == null || _trackBounds.Width <= 0 || _trackBounds.Height <= 0)
            {
                return;
            }

            EnsurePixelTexture(spriteBatch.GraphicsDevice);
            FillRoundedRect(spriteBatch, _trackBounds, TrackColor, TrackCornerRadius);

            if (_thumbBounds.Width > 0 && _thumbBounds.Height > 0)
            {
                bool hovered = _thumbBounds.Contains(_lastMousePosition);
                Color thumbColor = hovered || _draggingThumb ? ThumbHoverColor : ThumbColor;
                FillRoundedRect(spriteBatch, _thumbBounds, thumbColor, ThumbCornerRadius);
            }
        }

        private void HandleInput(MouseState mouseState, MouseState previousMouseState)
        {
            if (!_scrollbarVisible)
            {
                return;
            }

            bool pointerOverScrollableRegion = _contentViewportBounds.Contains(mouseState.Position) || _trackBounds.Contains(mouseState.Position);
            int wheelDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (wheelDelta != 0 && pointerOverScrollableRegion)
            {
                float delta = (wheelDelta / 120f) * ScrollPixelsPerNotch;
                ScrollBy(-delta);
            }

            bool leftDown = mouseState.LeftButton == ButtonState.Pressed;
            bool leftDownPrev = previousMouseState.LeftButton == ButtonState.Pressed;

            if (_draggingThumb)
            {
                if (!leftDown)
                {
                    _draggingThumb = false;
                    return;
                }

                DragThumb(mouseState.Y);
                return;
            }

            if (!leftDown && leftDownPrev)
            {
                _draggingThumb = false;
                return;
            }

            if (!leftDown || leftDownPrev)
            {
                return;
            }

            if (!_trackBounds.Contains(mouseState.Position))
            {
                return;
            }

            if (_thumbBounds.Contains(mouseState.Position))
            {
                _draggingThumb = true;
                _thumbDragOffset = mouseState.Y - _thumbBounds.Y;
            }
            else
            {
                JumpToPosition(mouseState.Y);
            }
        }

        private void ScrollBy(float delta)
        {
            if (_maxOffset <= 0f)
            {
                _scrollOffset = 0f;
                return;
            }

            _scrollOffset = MathHelper.Clamp(_scrollOffset + delta, 0f, _maxOffset);
        }

        private void DragThumb(int mouseY)
        {
            if (_trackBounds.Height <= 0 || _thumbBounds.Height <= 0)
            {
                return;
            }

            int maxThumbTop = Math.Max(_trackBounds.Y, _trackBounds.Bottom - _thumbBounds.Height);
            int travelRange = Math.Max(1, maxThumbTop - _trackBounds.Y);
            int clampedY = Math.Clamp(mouseY - _thumbDragOffset, _trackBounds.Y, maxThumbTop);
            float normalized = (clampedY - _trackBounds.Y) / (float)travelRange;
            _scrollOffset = _maxOffset <= 0f ? 0f : normalized * _maxOffset;
        }

        private void JumpToPosition(int mouseY)
        {
            if (_trackBounds.Height <= 0 || _thumbBounds.Height <= 0)
            {
                return;
            }

            int halfThumb = _thumbBounds.Height / 2;
            int maxThumbTop = Math.Max(_trackBounds.Y, _trackBounds.Bottom - _thumbBounds.Height);
            int travelRange = Math.Max(1, maxThumbTop - _trackBounds.Y);
            int targetY = Math.Clamp(mouseY - halfThumb, _trackBounds.Y, maxThumbTop);
            float normalized = (targetY - _trackBounds.Y) / (float)travelRange;
            _scrollOffset = _maxOffset <= 0f ? 0f : normalized * _maxOffset;
        }

        private void UpdateThumbBounds(float viewportHeight, float contentHeight)
        {
            if (!_scrollbarVisible || _trackBounds.Height <= 0)
            {
                _thumbBounds = Rectangle.Empty;
                return;
            }

            float ratio = contentHeight <= 0f || viewportHeight <= 0f ? 1f : MathHelper.Clamp(viewportHeight / contentHeight, 0.05f, 1f);
            int rawHeight = (int)MathF.Round(_trackBounds.Height * ratio);
            int maxThumbHeight = Math.Max(0, _trackBounds.Height);
            int minThumbHeight = Math.Min(ThumbMinHeight, maxThumbHeight);
            int thumbHeight = Math.Clamp(rawHeight, minThumbHeight, maxThumbHeight);

            float travelRange = Math.Max(0, _trackBounds.Height - thumbHeight);
            float normalized = _maxOffset <= 0f ? 0f : MathHelper.Clamp(_scrollOffset / _maxOffset, 0f, 1f);
            int thumbY = (int)MathF.Round(_trackBounds.Y + (travelRange * normalized));
            int maxThumbTop = Math.Max(_trackBounds.Y, _trackBounds.Bottom - thumbHeight);
            thumbY = Math.Clamp(thumbY, _trackBounds.Y, maxThumbTop);

            _thumbBounds = new Rectangle(_trackBounds.X, thumbY, _trackBounds.Width, thumbHeight);
        }

        private void EnsurePixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture != null && !_pixelTexture.IsDisposed)
            {
                return;
            }

            if (graphicsDevice == null)
            {
                return;
            }

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        private void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private void FillRoundedRect(SpriteBatch spriteBatch, Rectangle bounds, Color color, int radius)
        {
            if (spriteBatch == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            EnsurePixelTexture(spriteBatch.GraphicsDevice);
            radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
            if (radius <= 1)
            {
                FillRect(spriteBatch, bounds, color);
                return;
            }

            int diameter = radius * 2;
            Rectangle center = new(bounds.X + radius, bounds.Y + radius, Math.Max(0, bounds.Width - diameter), Math.Max(0, bounds.Height - diameter));
            Rectangle top = new(bounds.X + radius, bounds.Y, Math.Max(0, bounds.Width - diameter), radius);
            Rectangle bottom = new(bounds.X + radius, bounds.Bottom - radius, Math.Max(0, bounds.Width - diameter), radius);
            Rectangle left = new(bounds.X, bounds.Y + radius, radius, Math.Max(0, bounds.Height - diameter));
            Rectangle right = new(bounds.Right - radius, bounds.Y + radius, radius, Math.Max(0, bounds.Height - diameter));

            FillRect(spriteBatch, center, color);
            FillRect(spriteBatch, top, color);
            FillRect(spriteBatch, bottom, color);
            FillRect(spriteBatch, left, color);
            FillRect(spriteBatch, right, color);

            Texture2D cornerTexture = EnsureCornerTexture(radius, spriteBatch.GraphicsDevice);
            if (cornerTexture == null)
            {
                return;
            }

            Rectangle srcTopLeft = new(0, 0, radius, radius);
            Rectangle srcTopRight = new(radius, 0, radius, radius);
            Rectangle srcBottomLeft = new(0, radius, radius, radius);
            Rectangle srcBottomRight = new(radius, radius, radius, radius);

            spriteBatch.Draw(cornerTexture, new Rectangle(bounds.X, bounds.Y, radius, radius), srcTopLeft, color);
            spriteBatch.Draw(cornerTexture, new Rectangle(bounds.Right - radius, bounds.Y, radius, radius), srcTopRight, color);
            spriteBatch.Draw(cornerTexture, new Rectangle(bounds.X, bounds.Bottom - radius, radius, radius), srcBottomLeft, color);
            spriteBatch.Draw(cornerTexture, new Rectangle(bounds.Right - radius, bounds.Bottom - radius, radius, radius), srcBottomRight, color);
        }

        private static Texture2D EnsureCornerTexture(int radius, GraphicsDevice graphicsDevice)
        {
            if (radius <= 0 || graphicsDevice == null)
            {
                return null;
            }

            if (_cornerTextures.TryGetValue(radius, out Texture2D cached) && cached != null && !cached.IsDisposed)
            {
                return cached;
            }

            int diameter = radius * 2;
            Color[] data = new Color[diameter * diameter];
            float r = radius;
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    bool inside = (dx * dx + dy * dy) <= r * r;
                    data[(y * diameter) + x] = inside ? Color.White : Color.Transparent;
                }
            }

            Texture2D texture = new(graphicsDevice, diameter, diameter);
            texture.SetData(data);
            _cornerTextures[radius] = texture;
            return texture;
        }
    }
}
