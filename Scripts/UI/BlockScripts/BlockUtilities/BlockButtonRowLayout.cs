using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    /// <summary>
    /// Utility for laying out uniformly sized button rows inside blocks so sizing stays consistent.
    /// </summary>
    internal static class BlockButtonRowLayout
    {
        public const int DefaultButtonHeight = 28;
        public const int DefaultButtonWidth = 28;
        public const int DefaultButtonSpacing = 8;
        public const int MinButtonWidth = 22;

        internal enum Alignment
        {
            Left,
            Right
        }

        public static IReadOnlyList<Rectangle> BuildUniformRow(
            Rectangle rowBounds,
            int buttonCount,
            int? buttonWidth = null,
            int? buttonHeight = null,
            int? spacing = null,
            Alignment alignment = Alignment.Left,
            int horizontalPadding = 0)
        {
            if (buttonCount <= 0)
            {
                return Array.Empty<Rectangle>();
            }

            int resolvedWidth = Math.Max(MinButtonWidth, buttonWidth ?? DefaultButtonWidth);
            int[] widths = new int[buttonCount];
            for (int i = 0; i < buttonCount; i++)
            {
                widths[i] = resolvedWidth;
            }

            return BuildRow(rowBounds, widths, buttonHeight, spacing, alignment, horizontalPadding);
        }

        public static IReadOnlyList<Rectangle> BuildRow(
            Rectangle rowBounds,
            IReadOnlyList<int> buttonWidths,
            int? buttonHeight = null,
            int? spacing = null,
            Alignment alignment = Alignment.Left,
            int horizontalPadding = 0)
        {
            if (rowBounds.Width <= 0 || rowBounds.Height <= 0 || buttonWidths == null || buttonWidths.Count == 0)
            {
                return Array.Empty<Rectangle>();
            }

            int resolvedHeight = Math.Min(rowBounds.Height, buttonHeight ?? DefaultButtonHeight);
            resolvedHeight = Math.Max(1, resolvedHeight);

            int resolvedSpacing = Math.Max(0, spacing ?? DefaultButtonSpacing);
            int padding = Math.Max(0, horizontalPadding);
            int availableWidth = Math.Max(0, rowBounds.Width - (padding * 2));

            int[] widths = new int[buttonWidths.Count];
            for (int i = 0; i < buttonWidths.Count; i++)
            {
                widths[i] = Math.Max(MinButtonWidth, buttonWidths[i]);
            }

            int totalSpacing = resolvedSpacing * Math.Max(0, widths.Length - 1);
            int widthBudget = Math.Max(0, availableWidth - totalSpacing);

            if (widthBudget < widths.Length * MinButtonWidth && widths.Length > 1)
            {
                resolvedSpacing = Math.Max(0, (availableWidth - (widths.Length * MinButtonWidth)) / (widths.Length - 1));
                totalSpacing = resolvedSpacing * Math.Max(0, widths.Length - 1);
                widthBudget = Math.Max(0, availableWidth - totalSpacing);
            }

            int maxWidth = widths.Length > 0 && widthBudget > 0
                ? widthBudget / widths.Length
                : widths.Length > 0 ? MinButtonWidth : 0;

            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = Math.Min(widths[i], Math.Max(MinButtonWidth, maxWidth));
            }

            int totalWidthUsed = widths.Sum() + resolvedSpacing * Math.Max(0, widths.Length - 1);
            if (totalWidthUsed > availableWidth && widths.Length > 1)
            {
                resolvedSpacing = Math.Max(0, resolvedSpacing - (totalWidthUsed - availableWidth) / (widths.Length - 1));
                totalWidthUsed = widths.Sum() + resolvedSpacing * Math.Max(0, widths.Length - 1);
            }

            int y = rowBounds.Y + Math.Max(0, (rowBounds.Height - resolvedHeight) / 2);
            int startX = alignment == Alignment.Left
                ? rowBounds.X + padding
                : rowBounds.Right - padding - totalWidthUsed;
            startX = Math.Max(rowBounds.X, startX);

            Rectangle[] results = new Rectangle[widths.Length];
            int x = startX;
            for (int i = 0; i < widths.Length; i++)
            {
                results[i] = new Rectangle(x, y, widths[i], resolvedHeight);
                x += widths[i] + resolvedSpacing;
            }

            return results;
        }
    }
}
