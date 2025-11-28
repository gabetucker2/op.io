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
        private static float _lineHeightCache;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
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

            _scrollPanel.Draw(spriteBatch);
        }

        private static void RefreshRows()
        {
            _rows.Clear();
            foreach (var variable in GameTracker.GetTrackedVariables())
            {
                _rows.Add(new BackendVariable(variable.Name, variable.Value, variable.IsBoolean));
            }
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

        private readonly struct BackendVariable
        {
            public BackendVariable(string name, object value, bool isBoolean)
            {
                Name = name;
                Value = value;
                IsBoolean = isBoolean;
            }

            public string Name { get; }
            public object Value { get; }
            public bool IsBoolean { get; }
        }
    }
}
