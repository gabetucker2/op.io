using Microsoft.Xna.Framework;

namespace op.io
{
    /// <summary>
    /// Convenience accessor for palette values so call sites stay succinct.
    /// </summary>
    public static class ColorPalette
    {
        public static Color TransparentWindowKey => ColorScheme.GetColor(ColorRole.TransparentWindowKey);
        public static Color DefaultFallback => ColorScheme.GetColor(ColorRole.DefaultFallback);
        public static Color GameBackground => ColorScheme.GetColor(ColorRole.GameBackground);
        public static Color ScreenBackground => ColorScheme.GetColor(ColorRole.ScreenBackground);
        public static Color BlockBackground => ColorScheme.GetColor(ColorRole.BlockBackground);
        public static Color BlockBorder => ColorScheme.GetColor(ColorRole.BlockBorder);
        public static Color HeaderBackground => ColorScheme.GetColor(ColorRole.HeaderBackground);
        public static Color TextPrimary => ColorScheme.GetColor(ColorRole.TextPrimary);
        public static Color TextMuted => ColorScheme.GetColor(ColorRole.TextMuted);
        public static Color Accent => ColorScheme.GetColor(ColorRole.Accent);
        public static Color AccentSoft => ColorScheme.GetColor(ColorRole.AccentSoft);
        public static Color OverlayBackground => ColorScheme.GetColor(ColorRole.OverlayBackground);
        public static Color DragBarHoverTint => ColorScheme.GetColor(ColorRole.DragBarHoverTint);
        public static Color ResizeBar => ColorScheme.GetColor(ColorRole.ResizeBar);
        public static Color ResizeBarHover => ColorScheme.GetColor(ColorRole.ResizeBarHover);
        public static Color ResizeBarActive => ColorScheme.GetColor(ColorRole.ResizeBarActive);

        public static Color ButtonNeutral => ColorScheme.GetColor(ColorRole.ButtonNeutral);
        public static Color ButtonNeutralHover => ColorScheme.GetColor(ColorRole.ButtonNeutralHover);
        public static Color ButtonPrimary => ColorScheme.GetColor(ColorRole.ButtonPrimary);
        public static Color ButtonPrimaryHover => ColorScheme.GetColor(ColorRole.ButtonPrimaryHover);

        public static Color RowHover => ColorScheme.GetColor(ColorRole.RowHover);
        public static Color RowDragging => ColorScheme.GetColor(ColorRole.RowDragging);
        public static Color DropIndicator => ColorScheme.GetColor(ColorRole.DropIndicator);

        public static Color ToggleIdle => ColorScheme.GetColor(ColorRole.ToggleIdle);
        public static Color ToggleHover => ColorScheme.GetColor(ColorRole.ToggleHover);
        public static Color ToggleActive => ColorScheme.GetColor(ColorRole.ToggleActive);
        public static Color RebindScrim => ColorScheme.GetColor(ColorRole.RebindScrim);
        public static Color Warning => ColorScheme.GetColor(ColorRole.Warning);

        public static Color ScrollTrack => ColorScheme.GetColor(ColorRole.ScrollTrack);
        public static Color ScrollThumb => ColorScheme.GetColor(ColorRole.ScrollThumb);
        public static Color ScrollThumbHover => ColorScheme.GetColor(ColorRole.ScrollThumbHover);

        public static Color IndicatorActive => ColorScheme.GetColor(ColorRole.IndicatorActive);
        public static Color IndicatorInactive => ColorScheme.GetColor(ColorRole.IndicatorInactive);

        public static Color CloseBackground => ColorScheme.GetColor(ColorRole.CloseBackground);
        public static Color CloseHoverBackground => ColorScheme.GetColor(ColorRole.CloseHoverBackground);
        public static Color CloseBorder => ColorScheme.GetColor(ColorRole.CloseBorder);
        public static Color CloseHoverBorder => ColorScheme.GetColor(ColorRole.CloseHoverBorder);
        public static Color CloseOverlayBackground => ColorScheme.GetColor(ColorRole.CloseOverlayBackground);
        public static Color CloseOverlayHoverBackground => ColorScheme.GetColor(ColorRole.CloseOverlayHoverBackground);
        public static Color CloseOverlayBorder => ColorScheme.GetColor(ColorRole.CloseOverlayBorder);

        public static Color LockLockedFill => ColorScheme.GetColor(ColorRole.LockLockedFill);
        public static Color LockLockedHoverFill => ColorScheme.GetColor(ColorRole.LockLockedHoverFill);
        public static Color LockUnlockedFill => ColorScheme.GetColor(ColorRole.LockUnlockedFill);
        public static Color LockUnlockedHoverFill => ColorScheme.GetColor(ColorRole.LockUnlockedHoverFill);
        public static Color CloseGlyph => ColorScheme.GetColor(ColorRole.CloseGlyph);
        public static Color CloseGlyphHover => ColorScheme.GetColor(ColorRole.CloseGlyphHover);
    }
}
