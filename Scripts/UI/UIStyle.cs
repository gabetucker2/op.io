using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class UIStyle
    {
        public const string PanelHotkeyLabel = "Shift + X";
        public const int LayoutPadding = 24;
        public const int PanelPadding = 12;
        public const int HeaderHeight = 32;
        public const int MinPanelSize = 120;
        public const float DropEdgeThreshold = 0.28f;
        public const int SplitHandleThickness = 8;
        public const int PanelBorderThickness = 1;
        public const int DragOutlineThickness = 2;

        private const string BebasFontAsset = "Fonts/Bebas";
        private const string FuturaFontAsset = "Fonts/Futura";

        private static bool _fontsLoaded;

        public static SpriteFont FontH1 { get; private set; }
        public static SpriteFont FontH2 { get; private set; }
        public static SpriteFont FontH3 { get; private set; }
        public static SpriteFont FontH4 { get; private set; }
        public static SpriteFont FontBody { get; private set; }
        public static SpriteFont FontBebas { get; private set; }
        public static SpriteFont FontFutura { get; private set; }

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
                FontBebas = content.Load<SpriteFont>(BebasFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Bebas font asset '{BebasFontAsset}': {ex.Message}");
            }

            try
            {
                FontFutura = content.Load<SpriteFont>(FuturaFontAsset);
            }
            catch (ContentLoadException ex)
            {
                DebugLogger.PrintError($"Failed to load Futura font asset '{FuturaFontAsset}': {ex.Message}");
            }

            FontH1 = FontBebas;
            FontH2 = FontFutura;
            FontH3 = FontFutura;
            FontH4 = FontFutura;
            FontBody = FontFutura;

            _fontsLoaded = true;
        }
    }
}
