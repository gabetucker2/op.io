using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io.UI.Blocks
{
    internal static class BackendBlock
    {
        public const string PanelTitle = "Backend";

        private static readonly StringBuilder _stringBuilder = new();

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            UIStyle.UIFont boldFont = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            UIStyle.UIFont regularFont = UIStyle.FontTech;
            if (!boldFont.IsAvailable || !regularFont.IsAvailable)
            {
                return;
            }

            List<BackendVariable> rows = BuildRows();
            if (rows.Count == 0)
            {
                string placeholder = "No backend values tracked.";
                regularFont.DrawString(spriteBatch, placeholder, new Vector2(contentBounds.X, contentBounds.Y), UIStyle.MutedTextColor);
                return;
            }

            float lineHeight = Math.Max(boldFont.LineHeight, regularFont.LineHeight) + 2f;
            float y = contentBounds.Y;

            foreach (BackendVariable row in rows)
            {
                if (y + lineHeight > contentBounds.Bottom)
                {
                    break;
                }

                Vector2 labelPosition = new(contentBounds.X, y);
                string header = row.Name;
                Vector2 headerSize = boldFont.MeasureString(header);
                boldFont.DrawString(spriteBatch, header, labelPosition, UIStyle.TextColor);

                string valueText = FormatValue(row);
                float valueX = labelPosition.X + headerSize.X;

                _stringBuilder.Clear();
                _stringBuilder.Append(":  ");
                _stringBuilder.Append(valueText);

                regularFont.DrawString(spriteBatch, _stringBuilder.ToString(), new Vector2(valueX, y), UIStyle.TextColor);

                if (row.IsBoolean && row.Value is bool boolValue)
                {
                    BlockIndicatorRenderer.TryDrawBooleanIndicator(spriteBatch, contentBounds, lineHeight, y, boolValue);
                }

                y += lineHeight;
            }
        }

        private static List<BackendVariable> BuildRows()
        {
            List<BackendVariable> rows = new();

            foreach (var variable in GameTracker.GetTrackedVariables())
            {
                rows.Add(new BackendVariable(variable.Name, variable.Value, variable.IsBoolean));
            }

            return rows;
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
