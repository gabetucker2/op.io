using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class UIStyle
    {
        public const int LayoutPadding = 24;
        public const int BlockPadding = 12;
        public const int DragBarHeight = 32;
        public const int MinBlockSize = 10;
        public const float DropEdgeThreshold = 0.28f;
        public const int ResizeEdgeThickness = 8;
        public const int BlockBorderThickness = 1;
        public const int DragOutlineThickness = 2;

        private const string BebasFontAsset = "Fonts/Bebas";
        private const string MontHeavyFontAsset = "Fonts/MontHeavy";
        private const string MonaspaceXenonFontAsset = "Fonts/MonaspaceXenon";
        private const string MonaspaceXenonBoldFontAsset = "Fonts/MonaspaceXenonBold";
        private const string MonaspaceXenonItalicFontAsset = "Fonts/MonaspaceXenonItalic";
        private const string MonaspaceNeonFontAsset = "Fonts/MonaspaceNeon";
        private const string MonaspaceNeonBoldFontAsset = "Fonts/MonaspaceNeonBold";
        private const string MonaspaceNeonItalicFontAsset = "Fonts/MonaspaceNeonItalic";

        private const float FontH1Size = 42f;
        private const float FontH2Size = 25f;
        private const float FontHBodySize = 24f;
        private const float FontHTechSize = 20f;
        private const float FontBodySize = 20f;
        private const float FontTechSize = 18f;
        private const float BodyMinLineHeight = 16f;
        private const float MinimumGlyphSpacing = 1f;

        private static bool _fontsLoaded;

        public enum FontFamilyKey
        {
            Xenon,
            Neon
        }

        public enum FontVariant
        {
            Regular,
            Bold,
            Italic
        }

        public static UIFont FontH1 { get; private set; }
        public static UIFont FontH2 { get; private set; }
        public static UIFont FontHBody { get; private set; }
        public static UIFont FontHTech { get; private set; }
        public static UIFont FontBody { get; private set; }
        public static UIFont FontTech { get; private set; }

        private static SpriteFont _fontBebas;
        private static SpriteFont _fontMont;
        private static SpriteFont _fontMonospaceXenon;
        private static SpriteFont _fontMonospaceXenonBold;
        private static SpriteFont _fontMonospaceXenonItalic;
        private static SpriteFont _fontMonospaceNeon;
        private static SpriteFont _fontMonospaceNeonBold;
        private static SpriteFont _fontMonospaceNeonItalic;
        private static readonly Dictionary<(FontFamilyKey, FontVariant), UIFont> _variantFonts = new();

        public static Color ScreenBackground => ColorPalette.ScreenBackground;
        public static Color BlockBackground => ColorPalette.BlockBackground;
        public static Color BlockBorder => ColorPalette.BlockBorder;
        public static Color DragBarBackground => ColorPalette.DragBarBackground;
        public static Color TextColor => ColorPalette.TextPrimary;
        public static Color MutedTextColor => ColorPalette.TextMuted;
        public static Color AccentColor => ColorPalette.Accent;
        public static Color AccentMuted => ColorPalette.AccentSoft;
        public static Color OverlayBackground => ColorPalette.OverlayBackground;
        public static Color ResizeEdgeColor => ColorPalette.ResizeEdge;
        public static Color ResizeEdgeHoverColor => ColorPalette.ResizeEdgeHover;
        public static Color ResizeEdgeActiveColor => ColorPalette.ResizeEdgeActive;
        public static Color DragBarHoverTint => ColorPalette.DragBarHoverTint;

        public static void EnsureFontsLoaded(ContentManager content)
        {
            if (_fontsLoaded || content == null)
            {
                return;
            }

            try
            {
                _fontBebas = content.Load<SpriteFont>(BebasFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Bebas font asset '{BebasFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMont = content.Load<SpriteFont>(MontHeavyFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Mont Heavy font asset '{MontHeavyFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMonospaceXenon = content.Load<SpriteFont>(MonaspaceXenonFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Monaspace Xenon font asset '{MonaspaceXenonFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMonospaceXenonBold = content.Load<SpriteFont>(MonaspaceXenonBoldFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Monaspace Xenon bold font asset '{MonaspaceXenonBoldFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMonospaceXenonItalic = content.Load<SpriteFont>(MonaspaceXenonItalicFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Monaspace Xenon italic font asset '{MonaspaceXenonItalicFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMonospaceNeon = content.Load<SpriteFont>(MonaspaceNeonFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Monaspace Neon font asset '{MonaspaceNeonFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMonospaceNeonBold = content.Load<SpriteFont>(MonaspaceNeonBoldFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Monaspace Neon bold font asset '{MonaspaceNeonBoldFontAsset}': {ex.Message}");
            }

            try
            {
                _fontMonospaceNeonItalic = content.Load<SpriteFont>(MonaspaceNeonItalicFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Monaspace Neon italic font asset '{MonaspaceNeonItalicFontAsset}': {ex.Message}");
            }

            ApplyMinimumSpacing(_fontBebas, _fontMont, _fontMonospaceXenon, _fontMonospaceXenonBold, _fontMonospaceXenonItalic,
                _fontMonospaceNeon, _fontMonospaceNeonBold, _fontMonospaceNeonItalic);

            SpriteFont fallback = _fontMont ?? _fontBebas ?? _fontMonospaceXenon ?? _fontMonospaceNeon;

            FontH1 = CreateFontStyle(_fontBebas, FontH1Size);
            FontH2 = CreateFontStyle(_fontMont, FontH2Size);
            FontHBody = CreateFontStyle(_fontMonospaceNeonBold, FontHBodySize, BodyMinLineHeight);
            FontHTech = CreateFontStyle(_fontMonospaceXenonBold, FontHTechSize);
            FontBody = CreateFontStyle(_fontMonospaceNeon, FontBodySize, BodyMinLineHeight);
            FontTech = CreateFontStyle(_fontMonospaceXenon, FontTechSize);
            RegisterFontVariants(_fontMonospaceXenon, fallback);

            _fontsLoaded = true;
        }

        public static UIFont GetFontVariant(FontFamilyKey family, FontVariant variant = FontVariant.Regular)
        {
            if (_variantFonts.TryGetValue((family, variant), out UIFont font) && font.IsAvailable)
            {
                return font;
            }

            return FontTech;
        }

        private static void RegisterFontVariants(SpriteFont techBase, SpriteFont fallback)
        {
            _variantFonts.Clear();

            RegisterVariant(FontFamilyKey.Xenon, FontVariant.Regular, _fontMonospaceXenon, techBase, fallback);
            RegisterVariant(FontFamilyKey.Xenon, FontVariant.Bold, _fontMonospaceXenonBold, techBase, fallback);
            RegisterVariant(FontFamilyKey.Xenon, FontVariant.Italic, _fontMonospaceXenonItalic, techBase, fallback);

            RegisterVariant(FontFamilyKey.Neon, FontVariant.Regular, _fontMonospaceNeon, techBase, fallback);
            RegisterVariant(FontFamilyKey.Neon, FontVariant.Bold, _fontMonospaceNeonBold, techBase, fallback);
            RegisterVariant(FontFamilyKey.Neon, FontVariant.Italic, _fontMonospaceNeonItalic, techBase, fallback);
        }

        private static void RegisterVariant(FontFamilyKey family, FontVariant variant, SpriteFont spriteFont, SpriteFont techBase, SpriteFont fallback)
        {
            SpriteFont resolved = spriteFont;

            if (resolved == null)
            {
                resolved = family switch
                {
                    FontFamilyKey.Xenon => _fontMonospaceXenon,
                    FontFamilyKey.Neon => _fontMonospaceNeon,
                    _ => null
                };
            }

            resolved ??= techBase;
            resolved ??= fallback;

            float minLineHeight = family == FontFamilyKey.Neon ? BodyMinLineHeight : 0f;
            _variantFonts[(family, variant)] = CreateFontStyle(resolved, FontTechSize, minLineHeight);
        }

        private static UIFont CreateFontStyle(SpriteFont font, float desiredSize, float minLineHeight = 0f)
        {
            if (font == null)
            {
                return default;
            }

            float scale = CalculateScale(font, desiredSize);
            return new UIFont(font, scale, minLineHeight);
        }

        private static float CalculateScale(SpriteFont font, float desiredSize)
        {
            if (font == null || desiredSize <= 0f)
            {
                return 1f;
            }

            float lineHeight = Math.Max(1f, font.LineSpacing);
            return desiredSize / lineHeight;
        }

        private static void ApplyMinimumSpacing(params SpriteFont[] fonts)
        {
            if (fonts == null)
            {
                return;
            }

            foreach (SpriteFont font in fonts)
            {
                if (font == null)
                {
                    continue;
                }

                font.Spacing = Math.Max(font.Spacing, MinimumGlyphSpacing);
            }
        }

        public readonly struct UIFont
        {
            private readonly float _minLineHeight;

            public UIFont(SpriteFont font, float scale, float minLineHeight = 0f)
            {
                Font = font;
                Scale = scale > 0f ? scale : 1f;
                _minLineHeight = Math.Max(0f, minLineHeight);
            }

            public SpriteFont Font { get; }
            public float Scale { get; }
            public bool IsAvailable => Font != null;
            public float MinLineHeight => _minLineHeight;
            public float LineHeight
            {
                get
                {
                    if (!IsAvailable)
                    {
                        return 0f;
                    }

                    return Math.Max(Font.LineSpacing * Scale, _minLineHeight);
                }
            }

            public Vector2 MeasureString(string text)
            {
                if (!IsAvailable || string.IsNullOrEmpty(text))
                {
                    return Vector2.Zero;
                }

                return UITextRenderer.Measure(this, text);
            }

            public Vector2 MeasureString(StringBuilder builder)
            {
                if (!IsAvailable || builder == null || builder.Length == 0)
                {
                    return Vector2.Zero;
                }

                return UITextRenderer.Measure(this, builder);
            }

            internal Vector2 MeasureRawString(string text)
            {
                if (!IsAvailable || string.IsNullOrEmpty(text))
                {
                    return Vector2.Zero;
                }

                return Font.MeasureString(text) * Scale;
            }

            internal Vector2 MeasureRawString(StringBuilder builder)
            {
                if (!IsAvailable || builder == null || builder.Length == 0)
                {
                    return Vector2.Zero;
                }

                return Font.MeasureString(builder) * Scale;
            }

            public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
            {
                if (!IsAvailable || spriteBatch == null || string.IsNullOrEmpty(text))
                {
                    return;
                }

                UITextRenderer.Draw(this, spriteBatch, text, position, color);
            }

            public void DrawString(SpriteBatch spriteBatch, StringBuilder builder, Vector2 position, Color color)
            {
                if (!IsAvailable || spriteBatch == null || builder == null || builder.Length == 0)
                {
                    return;
                }

                UITextRenderer.Draw(this, spriteBatch, builder, position, color);
            }

            internal void DrawRawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
            {
                if (!IsAvailable || spriteBatch == null || string.IsNullOrEmpty(text))
                {
                    return;
                }

                spriteBatch.DrawString(Font, text, position, color, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }

            internal void DrawRawString(SpriteBatch spriteBatch, StringBuilder builder, Vector2 position, Color color)
            {
                if (!IsAvailable || spriteBatch == null || builder == null || builder.Length == 0)
                {
                    return;
                }

                spriteBatch.DrawString(Font, builder, position, color, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }
        }
    }
}
