using System;

namespace op.io
{
    /// <summary>
    /// Centralizes UI font retrieval and line-height calculations to keep UI blocks lean.
    /// </summary>
    public static class FontManager
    {
        private const float DefaultRowPadding = 2f;

        public static bool TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont)
        {
            boldFont = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            UIStyle.UIFont bodyFont = UIStyle.FontBody;
            UIStyle.UIFont techFont = UIStyle.FontTech;
            regularFont = bodyFont.IsAvailable ? bodyFont : techFont;
            return boldFont.IsAvailable && regularFont.IsAvailable;
        }

        public static bool TryGetControlsFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont)
        {
            boldFont = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            UIStyle.UIFont techFont = UIStyle.FontTech;
            regularFont = techFont.IsAvailable ? techFont : boldFont;
            return boldFont.IsAvailable && regularFont.IsAvailable;
        }

        public static float CalculateRowLineHeight(UIStyle.UIFont primary, UIStyle.UIFont secondary, float padding = DefaultRowPadding)
        {
            float primaryHeight = primary.IsAvailable ? primary.LineHeight : 0f;
            float secondaryHeight = secondary.IsAvailable ? secondary.LineHeight : 0f;
            return Math.Max(primaryHeight, secondaryHeight) + padding;
        }
    }
}
