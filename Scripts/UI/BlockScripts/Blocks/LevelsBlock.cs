using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class LevelsBlock
    {
        public const string BlockTitle = "Levels";

        private const int HeaderWideHeight = 42;
        private const int HeaderCompactHeight = 58;
        private const int HeaderCompactWidth = 330;
        private const int ButtonHeight = 20;
        private const int ButtonGap = 4;
        private const int RowPadding = 8;
        private const int CompactRowPadding = 6;
        private const int RowGap = 5;
        private const int RowMinHeight = 64;
        private const int CompactRowMinHeight = 88;
        private const int RowButtonMinWidth = 48;
        private const int RowButtonMaxWidth = 70;
        private const int RowSideButtonReserve = 86;
        private const int RowStackedButtonWidth = 64;
        private const int RowStackedThreshold = 275;
        private const float HeaderTitleTextScale = 0.78f;
        private const float HeaderSummaryTextScale = 0.66f;
        private const float RowTitleTextScale = 0.78f;
        private const float RowBodyTextScale = 0.66f;
        private const float HeaderButtonTextScale = 0.66f;
        private const float RowButtonTextScale = 0.68f;

        public const string PreviousTooltipKey = "GameLevelPrevious";
        public const string NextTooltipKey = "GameLevelNext";
        public const string ReloadTooltipKey = "GameLevelReload";
        public const string ManualTooltipKey = "GameLevelManual";
        public const string NaturalTooltipKey = "GameLevelNatural";

        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly List<LevelRowLayout> _rowLayouts = new();
        private static readonly Dictionary<string, Rectangle> _buttonBoundsByKey = new(StringComparer.OrdinalIgnoreCase);
        private static Texture2D _pixelTexture;
        private static string _tooltipRowKey;
        private static string _tooltipRowLabel;
        private static float _lineHeight;

        public static string GetHoveredRowKey() => _tooltipRowKey;
        public static string GetHoveredRowLabel() => _tooltipRowLabel;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Levels);
            _tooltipRowKey = null;
            _tooltipRowLabel = null;
            _buttonBoundsByKey.Clear();

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _scrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeight <= 0f)
            {
                _lineHeight = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }

            Rectangle headerBounds = ResolveHeaderBounds(contentBounds);
            Rectangle listArea = new(
                contentBounds.X,
                headerBounds.Bottom + RowGap,
                contentBounds.Width,
                Math.Max(0, contentBounds.Bottom - headerBounds.Bottom - RowGap));

            float contentHeight = CalculateContentHeight(GameLevelManager.Levels.Count, listArea.Width);
            _scrollPanel.Update(listArea, contentHeight,
                BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = listArea;
            }

            RebuildRowLayouts(listBounds, GameLevelManager.Levels, listArea.Width);
            RegisterHeaderButtons(headerBounds);
            RegisterRowButtons(_rowLayouts);

            Point pointer = mouseState.Position;
            ResolveTooltip(pointer);

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed &&
                previousMouseState.LeftButton == ButtonState.Released;
            if (blockLocked || !leftClickStarted)
            {
                return;
            }

            if (_buttonBoundsByKey.TryGetValue(PreviousTooltipKey, out Rectangle prevBounds) &&
                prevBounds.Contains(pointer))
            {
                GameLevelManager.TryLoadLevel(GameLevelManager.GetNextLevel(-1));
                return;
            }

            if (_buttonBoundsByKey.TryGetValue(NextTooltipKey, out Rectangle nextBounds) &&
                nextBounds.Contains(pointer))
            {
                GameLevelManager.TryLoadLevel(GameLevelManager.GetNextLevel(1));
                return;
            }

            if (_buttonBoundsByKey.TryGetValue(ReloadTooltipKey, out Rectangle reloadBounds) &&
                reloadBounds.Contains(pointer))
            {
                GameLevelManager.TryLoadLevel(GameLevelManager.ActiveLevel, forceReload: true);
                return;
            }

            foreach (LevelRowLayout row in _rowLayouts)
            {
                if (row.ButtonBounds.Contains(pointer))
                {
                    bool active = ReferenceEquals(row.Level, GameLevelManager.ActiveLevel);
                    GameLevelManager.TryLoadLevel(row.Level, forceReload: active);
                    return;
                }
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            EnsurePixelTexture(spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice);
            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                return;
            }

            Rectangle headerBounds = ResolveHeaderBounds(contentBounds);
            DrawHeader(spriteBatch, headerBounds, boldFont, regularFont);

            Rectangle listArea = new(
                contentBounds.X,
                headerBounds.Bottom + RowGap,
                contentBounds.Width,
                Math.Max(0, contentBounds.Bottom - headerBounds.Bottom - RowGap));
            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = listArea;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice;
            float uiScale = BlockManager.UIScale;
            Rectangle scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(listBounds.X * uiScale),
                    (int)(listBounds.Y * uiScale),
                    (int)(listBounds.Width * uiScale),
                    (int)(listBounds.Height * uiScale))
                : listBounds;

            Viewport viewport = graphicsDevice.Viewport;
            scissorRect.X = Math.Clamp(scissorRect.X, 0, viewport.Width);
            scissorRect.Y = Math.Clamp(scissorRect.Y, 0, viewport.Height);
            scissorRect.Width = Math.Clamp(scissorRect.Width, 0, viewport.Width - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, viewport.Height - scissorRect.Y);

            spriteBatch.End();
            using RasterizerState scissorState = new() { CullMode = CullMode.None, ScissorTestEnable = true };
            graphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            foreach (LevelRowLayout row in _rowLayouts)
            {
                if (row.Bounds.Bottom <= listBounds.Y)
                {
                    continue;
                }

                if (row.Bounds.Y >= listBounds.Bottom)
                {
                    break;
                }

                DrawLevelRow(spriteBatch, row, boldFont, regularFont);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            _scrollPanel.Draw(spriteBatch, BlockManager.IsBlockLocked(DockBlockKind.Levels));
        }

        private static Rectangle ResolveHeaderBounds(Rectangle contentBounds)
        {
            int preferredHeight = contentBounds.Width < HeaderCompactWidth
                ? HeaderCompactHeight
                : HeaderWideHeight;
            return new Rectangle(
                contentBounds.X,
                contentBounds.Y,
                contentBounds.Width,
                Math.Min(preferredHeight, Math.Max(0, contentBounds.Height)));
        }

        private static float CalculateContentHeight(int rowCount, int listWidth)
        {
            if (rowCount <= 0)
            {
                return 0f;
            }

            return rowCount * CalculateRowHeight(listWidth) + Math.Max(0, rowCount - 1) * RowGap;
        }

        private static int CalculateRowHeight(int listWidth)
        {
            bool stacked = ShouldStackRowButton(listWidth);
            int padding = ResolveRowPadding(listWidth);
            int visibleTextLines = listWidth < 210 ? 3 : 4;
            float lineHeight = ResolveRowLineHeight();
            int textHeight = (int)MathF.Ceiling(visibleTextLines * lineHeight);
            if (stacked)
            {
                return Math.Max(CompactRowMinHeight, textHeight + ButtonHeight + (padding * 3));
            }

            return Math.Max(RowMinHeight, textHeight + (padding * 2));
        }

        private static void RebuildRowLayouts(Rectangle listBounds, IReadOnlyList<GameLevelDefinition> levels, int listWidth)
        {
            _rowLayouts.Clear();
            if (levels == null || levels.Count == 0)
            {
                return;
            }

            int rowHeight = CalculateRowHeight(listWidth);
            int y = (int)MathF.Round(listBounds.Y - _scrollPanel.ScrollOffset);
            foreach (GameLevelDefinition level in levels)
            {
                Rectangle rowBounds = new(listBounds.X, y, listBounds.Width, rowHeight);
                int padding = ResolveRowPadding(rowBounds.Width);
                bool stacked = ShouldStackRowButton(rowBounds.Width);
                int buttonWidth = stacked
                    ? Math.Min(RowStackedButtonWidth, Math.Max(RowButtonMinWidth, rowBounds.Width - padding * 2))
                    : Math.Min(RowButtonMaxWidth, Math.Max(RowButtonMinWidth, rowBounds.Width / 4));
                Rectangle buttonBounds = stacked
                    ? new Rectangle(
                        rowBounds.Right - padding - buttonWidth,
                        rowBounds.Bottom - padding - ButtonHeight,
                        buttonWidth,
                        ButtonHeight)
                    : new Rectangle(
                        rowBounds.Right - padding - buttonWidth,
                        rowBounds.Y + padding,
                        buttonWidth,
                        ButtonHeight);
                int textRight = stacked
                    ? rowBounds.Right - padding
                    : Math.Max(rowBounds.X + padding, buttonBounds.X - padding);
                Rectangle textBounds = new(
                    rowBounds.X + padding,
                    rowBounds.Y + padding,
                    Math.Max(0, textRight - rowBounds.X - padding),
                    Math.Max(0, (stacked ? buttonBounds.Y : rowBounds.Bottom - padding) - rowBounds.Y - padding));
                _rowLayouts.Add(new LevelRowLayout(level, rowBounds, textBounds, buttonBounds, stacked));
                y += rowHeight + RowGap;
            }
        }

        private static void RegisterHeaderButtons(Rectangle headerBounds)
        {
            HeaderLayout layout = BuildHeaderLayout(headerBounds);
            _buttonBoundsByKey[PreviousTooltipKey] = layout.PreviousButtonBounds;
            _buttonBoundsByKey[ReloadTooltipKey] = layout.ReloadButtonBounds;
            _buttonBoundsByKey[NextTooltipKey] = layout.NextButtonBounds;
        }

        private static void RegisterRowButtons(IEnumerable<LevelRowLayout> rows)
        {
            foreach (LevelRowLayout row in rows)
            {
                _buttonBoundsByKey[GetLevelTooltipKey(row.Level)] = row.ButtonBounds;
            }
        }

        private static void ResolveTooltip(Point pointer)
        {
            foreach (KeyValuePair<string, Rectangle> pair in _buttonBoundsByKey)
            {
                if (!pair.Value.Contains(pointer))
                {
                    continue;
                }

                _tooltipRowKey = pair.Key;
                _tooltipRowLabel = pair.Key switch
                {
                    PreviousTooltipKey => "Previous level",
                    NextTooltipKey => "Next level",
                    ReloadTooltipKey => "Reload level",
                    ManualTooltipKey => "Manual level",
                    NaturalTooltipKey => "Natural level",
                    _ => "Level"
                };
                return;
            }

            foreach (LevelRowLayout row in _rowLayouts)
            {
                if (row.Bounds.Contains(pointer))
                {
                    _tooltipRowKey = GetLevelTooltipKey(row.Level);
                    _tooltipRowLabel = $"{row.Level.DisplayName} level";
                    return;
                }
            }
        }

        private static HeaderLayout BuildHeaderLayout(Rectangle bounds)
        {
            int padding = ResolveHeaderPadding(bounds.Width);
            bool compact = bounds.Width < HeaderCompactWidth;
            int buttonHeight = Math.Min(ButtonHeight, Math.Max(1, bounds.Height - padding * 2));
            int buttonY = compact
                ? bounds.Bottom - padding - buttonHeight
                : bounds.Y + Math.Max(0, (bounds.Height - buttonHeight) / 2);
            Rectangle buttonRowBounds = new(
                bounds.X + padding,
                buttonY,
                Math.Max(0, bounds.Width - padding * 2),
                buttonHeight);
            int reloadWidth = compact ? 36 : 58;
            int sideWidth = compact ? 28 : 38;
            IReadOnlyList<Rectangle> buttons = BlockButtonRowLayout.BuildRow(
                buttonRowBounds,
                [sideWidth, reloadWidth, sideWidth],
                buttonHeight,
                ButtonGap,
                BlockButtonRowLayout.Alignment.Right);
            Rectangle previous = buttons.Count > 0 ? buttons[0] : Rectangle.Empty;
            Rectangle reload = buttons.Count > 1 ? buttons[1] : Rectangle.Empty;
            Rectangle next = buttons.Count > 2 ? buttons[2] : Rectangle.Empty;

            int textRight = compact || previous == Rectangle.Empty
                ? bounds.Right - padding
                : Math.Max(bounds.X + padding, previous.X - ButtonGap);
            Rectangle activeTextBounds = new(
                bounds.X + padding,
                bounds.Y + 3,
                Math.Max(0, textRight - bounds.X - padding),
                16);
            int summaryBottom = compact && previous != Rectangle.Empty
                ? Math.Max(bounds.Y + 18, previous.Y - 2)
                : bounds.Bottom - 3;
            Rectangle summaryTextBounds = new(
                bounds.X + padding,
                bounds.Y + 20,
                Math.Max(0, textRight - bounds.X - padding),
                Math.Max(0, summaryBottom - (bounds.Y + 20)));

            return new HeaderLayout(activeTextBounds, summaryTextBounds, previous, reload, next);
        }

        private static int ResolveHeaderPadding(int width) => width < HeaderCompactWidth ? CompactRowPadding : RowPadding;

        private static int ResolveRowPadding(int width) => width < RowStackedThreshold ? CompactRowPadding : RowPadding;

        private static bool ShouldStackRowButton(int width) => width < RowStackedThreshold;

        private static float ResolveRowLineHeight() => MathF.Max(12f, MathF.Ceiling(Math.Max(_lineHeight, 16f) * RowBodyTextScale));

        private static string GetHeaderButtonLabel(string key, Rectangle bounds)
        {
            if (bounds.Width <= 52)
            {
                return key switch
                {
                    PreviousTooltipKey => "<",
                    NextTooltipKey => ">",
                    _ => "R"
                };
            }

            return key switch
            {
                PreviousTooltipKey => "<",
                NextTooltipKey => ">",
                _ => "Reload"
            };
        }

        private static Vector2 MeasureScaledString(UIStyle.UIFont font, string text, float textScale)
        {
            return font.IsAvailable && !string.IsNullOrEmpty(text)
                ? font.MeasureString(text) * Math.Clamp(textScale, 0.45f, 1.25f)
                : Vector2.Zero;
        }

        private static void DrawScaledString(
            SpriteBatch spriteBatch,
            UIStyle.UIFont font,
            string text,
            Vector2 position,
            Color color,
            float textScale)
        {
            if (spriteBatch == null || !font.IsAvailable || string.IsNullOrEmpty(text))
            {
                return;
            }

            float resolvedTextScale = Math.Clamp(textScale, 0.45f, 1.25f);
            if (MathF.Abs(resolvedTextScale - 1f) <= 0.001f)
            {
                font.DrawString(spriteBatch, text, position, color);
                return;
            }

            spriteBatch.DrawString(
                font.Font,
                text,
                position,
                color,
                0f,
                Vector2.Zero,
                font.Scale * resolvedTextScale,
                SpriteEffects.None,
                0f);
        }

        private static void DrawHeader(SpriteBatch spriteBatch, Rectangle bounds, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            FillRect(spriteBatch, bounds, UIStyle.BlockBackground * 0.72f);

            HeaderLayout layout = BuildHeaderLayout(bounds);
            GameLevelDefinition active = GameLevelManager.ActiveLevel;
            string title = $"Active: {active.DisplayName}";
            string titleText = TruncateToWidth(title, boldFont, layout.ActiveTextBounds.Width, HeaderTitleTextScale);
            Vector2 titlePosition = new(layout.ActiveTextBounds.X, layout.ActiveTextBounds.Y);
            DrawScaledString(spriteBatch, boldFont, titleText, titlePosition, UIStyle.TextColor, HeaderTitleTextScale);

            string summary = GameLevelManager.ActiveLevelLoadoutSummary;
            string summaryText = TruncateToWidth(summary, regularFont, layout.SummaryTextBounds.Width, HeaderSummaryTextScale);
            Vector2 summaryPosition = new(layout.SummaryTextBounds.X, layout.SummaryTextBounds.Y);
            DrawScaledString(spriteBatch, regularFont, summaryText, summaryPosition, UIStyle.MutedTextColor, HeaderSummaryTextScale);

            Point pointer = Mouse.GetState().Position;
            DrawHeaderButton(spriteBatch, PreviousTooltipKey, GetHeaderButtonLabel(PreviousTooltipKey, layout.PreviousButtonBounds), pointer);
            DrawHeaderButton(spriteBatch, ReloadTooltipKey, GetHeaderButtonLabel(ReloadTooltipKey, layout.ReloadButtonBounds), pointer);
            DrawHeaderButton(spriteBatch, NextTooltipKey, GetHeaderButtonLabel(NextTooltipKey, layout.NextButtonBounds), pointer);
        }

        private static void DrawHeaderButton(SpriteBatch spriteBatch, string key, string label, Point pointer)
        {
            if (!_buttonBoundsByKey.TryGetValue(key, out Rectangle bounds))
            {
                return;
            }

            UIButtonRenderer.Draw(
                spriteBatch,
                bounds,
                label,
                UIButtonRenderer.ButtonStyle.Grey,
                UIButtonRenderer.IsHovered(bounds, pointer),
                isDisabled: BlockManager.IsBlockLocked(DockBlockKind.Levels),
                textScale: HeaderButtonTextScale);
        }

        private static void DrawLevelRow(SpriteBatch spriteBatch, LevelRowLayout row, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            bool active = ReferenceEquals(row.Level, GameLevelManager.ActiveLevel);
            Point pointer = Mouse.GetState().Position;
            Color fill = active ? ColorPalette.ButtonPrimary * 0.22f : UIStyle.BlockBackground * 0.58f;
            Color border = active ? UIStyle.AccentColor : UIStyle.BlockBorder * 0.75f;

            FillRect(spriteBatch, row.Bounds, fill);
            DrawRectOutline(spriteBatch, row.Bounds, border, 1);

            int textWidth = Math.Max(0, row.TextBounds.Width);
            float lineHeight = ResolveRowLineHeight();
            Vector2 titlePosition = new(row.TextBounds.X, row.TextBounds.Y);
            DrawScaledString(
                spriteBatch,
                boldFont,
                TruncateToWidth(row.Level.DisplayName, boldFont, textWidth, RowTitleTextScale),
                titlePosition,
                active ? UIStyle.AccentColor : UIStyle.TextColor,
                RowTitleTextScale);

            string description = TruncateToWidth(row.Level.Description, regularFont, textWidth, RowBodyTextScale);
            Vector2 descriptionPosition = new(row.TextBounds.X, row.TextBounds.Y + lineHeight);
            DrawScaledString(spriteBatch, regularFont, description, descriptionPosition, UIStyle.TextColor, RowBodyTextScale);

            string loadout = TruncateToWidth(GameLevelManager.BuildLevelLoadoutSummary(row.Level), regularFont, textWidth, RowBodyTextScale);
            Vector2 loadoutPosition = new(row.TextBounds.X, row.TextBounds.Y + (lineHeight * 2f));
            DrawScaledString(spriteBatch, regularFont, loadout, loadoutPosition, UIStyle.MutedTextColor, RowBodyTextScale);

            string config = $"Terrain: {row.Level.TerrainConfigurationKey} | Ocean: {row.Level.OceanBiomeConfigurationKey}";
            if (row.TextBounds.Height >= lineHeight * 3.5f)
            {
                Vector2 configPosition = new(row.TextBounds.X, row.TextBounds.Y + (lineHeight * 3f));
                DrawScaledString(
                    spriteBatch,
                    regularFont,
                    TruncateToWidth(config, regularFont, textWidth, RowBodyTextScale),
                    configPosition,
                    UIStyle.MutedTextColor,
                    RowBodyTextScale);
            }

            UIButtonRenderer.Draw(
                spriteBatch,
                row.ButtonBounds,
                active ? "Reload" : "Load",
                active ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                UIButtonRenderer.IsHovered(row.ButtonBounds, pointer),
                isDisabled: BlockManager.IsBlockLocked(DockBlockKind.Levels),
                textScale: RowButtonTextScale);
        }

        private static string GetLevelTooltipKey(GameLevelDefinition level)
        {
            if (level?.Kind == GameLevelKind.Natural)
            {
                return NaturalTooltipKey;
            }

            return ManualTooltipKey;
        }

        private static string TruncateToWidth(string text, UIStyle.UIFont font, int maxWidth, float textScale = 1f)
        {
            text ??= string.Empty;
            if (maxWidth <= 0 || !font.IsAvailable)
            {
                return string.Empty;
            }

            if (MeasureScaledString(font, text, textScale).X <= maxWidth)
            {
                return text;
            }

            const string suffix = "...";
            int length = text.Length;
            while (length > 0)
            {
                string candidate = text[..length].TrimEnd() + suffix;
                if (MeasureScaledString(font, candidate, textScale).X <= maxWidth)
                {
                    return candidate;
                }

                length--;
            }

            return suffix;
        }

        private static void EnsurePixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture != null || graphicsDevice == null)
            {
                return;
            }

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData([Color.White]);
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || spriteBatch == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixelTexture == null || spriteBatch == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }

        private readonly struct LevelRowLayout
        {
            public LevelRowLayout(GameLevelDefinition level, Rectangle bounds, Rectangle textBounds, Rectangle buttonBounds, bool stackedButton)
            {
                Level = level;
                Bounds = bounds;
                TextBounds = textBounds;
                ButtonBounds = buttonBounds;
                StackedButton = stackedButton;
            }

            public GameLevelDefinition Level { get; }
            public Rectangle Bounds { get; }
            public Rectangle TextBounds { get; }
            public Rectangle ButtonBounds { get; }
            public bool StackedButton { get; }
        }

        private readonly struct HeaderLayout
        {
            public HeaderLayout(
                Rectangle activeTextBounds,
                Rectangle summaryTextBounds,
                Rectangle previousButtonBounds,
                Rectangle reloadButtonBounds,
                Rectangle nextButtonBounds)
            {
                ActiveTextBounds = activeTextBounds;
                SummaryTextBounds = summaryTextBounds;
                PreviousButtonBounds = previousButtonBounds;
                ReloadButtonBounds = reloadButtonBounds;
                NextButtonBounds = nextButtonBounds;
            }

            public Rectangle ActiveTextBounds { get; }
            public Rectangle SummaryTextBounds { get; }
            public Rectangle PreviousButtonBounds { get; }
            public Rectangle ReloadButtonBounds { get; }
            public Rectangle NextButtonBounds { get; }
        }
    }
}
