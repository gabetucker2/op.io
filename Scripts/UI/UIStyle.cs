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
        public const int PanelPadding = 12;
        public const int HeaderHeight = 32;
        public const int MinPanelSize = 120;
        public const float DropEdgeThreshold = 0.28f;
        public const int SplitHandleThickness = 8;
        public const int PanelBorderThickness = 1;
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

        public static readonly Color ScreenBackground = new(18, 18, 18);
        public static readonly Color PanelBackground = new(26, 26, 26);
        public static readonly Color PanelBorder = new(48, 48, 48);
        public static readonly Color HeaderBackground = new(35, 35, 35);
        public static readonly Color TextColor = new(226, 226, 226);
        public static readonly Color MutedTextColor = new(160, 160, 160);
        public static readonly Color AccentColor = new(110, 142, 255);
        public static readonly Color AccentMuted = new(110, 142, 255, 70);
        public static readonly Color OverlayBackground = new(24, 24, 24, 230);
        public static readonly Color SplitHandleColor = new(58, 58, 58, 210);
        public static readonly Color SplitHandleHoverColor = new(110, 142, 255, 150);
        public static readonly Color SplitHandleActiveColor = new(110, 142, 255, 220);

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

            SpriteFont fallback = _fontMont ?? _fontBebas ?? _fontMonospaceXenon ?? _fontMonospaceNeon;

            FontH1 = CreateFontStyle(_fontBebas, FontH1Size);
            FontH2 = CreateFontStyle(_fontMont, FontH2Size);
            FontHBody = CreateFontStyle(_fontMonospaceNeonBold, FontHBodySize);
            FontHTech = CreateFontStyle(_fontMonospaceXenonBold, FontHTechSize);
            FontBody = CreateFontStyle(_fontMonospaceNeon, FontBodySize);
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

            _variantFonts[(family, variant)] = CreateFontStyle(resolved, FontTechSize);
        }

        private static UIFont CreateFontStyle(SpriteFont font, float desiredSize)
        {
            if (font == null)
            {
                return default;
            }

            float scale = CalculateScale(font, desiredSize);
            return new UIFont(font, scale);
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

        public readonly struct UIFont
        {
            public UIFont(SpriteFont font, float scale)
            {
                Font = font;
                Scale = scale > 0f ? scale : 1f;
            }

            public SpriteFont Font { get; }
            public float Scale { get; }
            public bool IsAvailable => Font != null;
            public float LineHeight => (Font?.LineSpacing ?? 0f) * Scale;

            public Vector2 MeasureString(string text)
            {
                if (!IsAvailable || string.IsNullOrEmpty(text))
                {
                    return Vector2.Zero;
                }

                return Font.MeasureString(text) * Scale;
            }

            public Vector2 MeasureString(StringBuilder builder)
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

                spriteBatch.DrawString(Font, text, position, color, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }

            public void DrawString(SpriteBatch spriteBatch, StringBuilder builder, Vector2 position, Color color)
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
