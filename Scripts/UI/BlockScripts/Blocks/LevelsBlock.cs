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

        private const int HeaderHeight = 34;
        private const int ButtonHeight = 22;
        private const int ButtonGap = 6;
        private const int RowPadding = 8;
        private const int RowGap = 6;
        private const int RowMinHeight = 74;

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
            return new Rectangle(
                contentBounds.X,
                contentBounds.Y,
                contentBounds.Width,
                Math.Min(HeaderHeight, Math.Max(0, contentBounds.Height)));
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
            int textRoom = Math.Max(0, listWidth - 130);
            int descriptionLines = textRoom < 220 ? 2 : 1;
            int configLines = textRoom < 260 ? 2 : 1;
            int textHeight = (int)MathF.Ceiling((descriptionLines + configLines + 2) * Math.Max(_lineHeight, 16f));
            return Math.Max(RowMinHeight, textHeight + RowPadding * 2);
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
                int buttonWidth = Math.Min(86, Math.Max(58, rowBounds.Width / 4));
                Rectangle buttonBounds = new(
                    rowBounds.Right - RowPadding - buttonWidth,
                    rowBounds.Y + RowPadding,
                    buttonWidth,
                    ButtonHeight);
                _rowLayouts.Add(new LevelRowLayout(level, rowBounds, buttonBounds));
                y += rowHeight + RowGap;
            }
        }

        private static void RegisterHeaderButtons(Rectangle headerBounds)
        {
            int buttonWidth = Math.Min(80, Math.Max(54, (headerBounds.Width - RowPadding * 2 - ButtonGap * 2) / 3));
            int y = headerBounds.Y + Math.Max(0, (headerBounds.Height - ButtonHeight) / 2);
            int right = headerBounds.Right - RowPadding;

            Rectangle next = new(right - buttonWidth, y, buttonWidth, ButtonHeight);
            Rectangle reload = new(next.X - ButtonGap - buttonWidth, y, buttonWidth, ButtonHeight);
            Rectangle previous = new(reload.X - ButtonGap - buttonWidth, y, buttonWidth, ButtonHeight);

            _buttonBoundsByKey[PreviousTooltipKey] = previous;
            _buttonBoundsByKey[ReloadTooltipKey] = reload;
            _buttonBoundsByKey[NextTooltipKey] = next;
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

        private static void DrawHeader(SpriteBatch spriteBatch, Rectangle bounds, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            FillRect(spriteBatch, bounds, UIStyle.BlockBackground * 0.72f);

            GameLevelDefinition active = GameLevelManager.ActiveLevel;
            string title = $"Active: {active.DisplayName}";
            Vector2 titlePosition = new(bounds.X + RowPadding, bounds.Y + 3);
            boldFont.DrawString(spriteBatch, title, titlePosition, UIStyle.TextColor);

            string summary = GameLevelManager.ActiveLevelLoadoutSummary;
            Vector2 summaryPosition = new(bounds.X + RowPadding, bounds.Y + 18);
            regularFont.DrawString(spriteBatch, TruncateToWidth(summary, regularFont, Math.Max(40, bounds.Width - RowPadding * 2 - 270)), summaryPosition, UIStyle.MutedTextColor);

            Point pointer = Mouse.GetState().Position;
            DrawHeaderButton(spriteBatch, PreviousTooltipKey, "Prev", pointer);
            DrawHeaderButton(spriteBatch, ReloadTooltipKey, "Reload", pointer);
            DrawHeaderButton(spriteBatch, NextTooltipKey, "Next", pointer);
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
                isDisabled: BlockManager.IsBlockLocked(DockBlockKind.Levels));
        }

        private static void DrawLevelRow(SpriteBatch spriteBatch, LevelRowLayout row, UIStyle.UIFont boldFont, UIStyle.UIFont regularFont)
        {
            bool active = ReferenceEquals(row.Level, GameLevelManager.ActiveLevel);
            Point pointer = Mouse.GetState().Position;
            Color fill = active ? ColorPalette.ButtonPrimary * 0.22f : UIStyle.BlockBackground * 0.58f;
            Color border = active ? UIStyle.AccentColor : UIStyle.BlockBorder * 0.75f;

            FillRect(spriteBatch, row.Bounds, fill);
            DrawRectOutline(spriteBatch, row.Bounds, border, 1);

            int textRight = Math.Max(row.Bounds.X + 40, row.ButtonBounds.X - RowPadding);
            int textWidth = Math.Max(0, textRight - row.Bounds.X - RowPadding);
            Vector2 titlePosition = new(row.Bounds.X + RowPadding, row.Bounds.Y + RowPadding);
            boldFont.DrawString(spriteBatch, row.Level.DisplayName, titlePosition, active ? UIStyle.AccentColor : UIStyle.TextColor);

            string description = TruncateToWidth(row.Level.Description, regularFont, textWidth);
            Vector2 descriptionPosition = new(row.Bounds.X + RowPadding, row.Bounds.Y + RowPadding + _lineHeight);
            regularFont.DrawString(spriteBatch, description, descriptionPosition, UIStyle.TextColor);

            string loadout = TruncateToWidth(GameLevelManager.BuildLevelLoadoutSummary(row.Level), regularFont, textWidth);
            Vector2 loadoutPosition = new(row.Bounds.X + RowPadding, row.Bounds.Y + RowPadding + (_lineHeight * 2f));
            regularFont.DrawString(spriteBatch, loadout, loadoutPosition, UIStyle.MutedTextColor);

            string config = $"Terrain: {row.Level.TerrainConfigurationKey} | Ocean: {row.Level.OceanZoneConfigurationKey}";
            Vector2 configPosition = new(row.Bounds.X + RowPadding, row.Bounds.Y + RowPadding + (_lineHeight * 3f));
            regularFont.DrawString(spriteBatch, TruncateToWidth(config, regularFont, textWidth), configPosition, UIStyle.MutedTextColor);

            UIButtonRenderer.Draw(
                spriteBatch,
                row.ButtonBounds,
                active ? "Reload" : "Load",
                active ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                UIButtonRenderer.IsHovered(row.ButtonBounds, pointer),
                isDisabled: BlockManager.IsBlockLocked(DockBlockKind.Levels));
        }

        private static string GetLevelTooltipKey(GameLevelDefinition level)
        {
            if (level?.Kind == GameLevelKind.Natural)
            {
                return NaturalTooltipKey;
            }

            return ManualTooltipKey;
        }

        private static string TruncateToWidth(string text, UIStyle.UIFont font, int maxWidth)
        {
            text ??= string.Empty;
            if (maxWidth <= 0 || !font.IsAvailable)
            {
                return string.Empty;
            }

            if (font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string suffix = "...";
            int length = text.Length;
            while (length > 0)
            {
                string candidate = text[..length].TrimEnd() + suffix;
                if (font.MeasureString(candidate).X <= maxWidth)
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
            public LevelRowLayout(GameLevelDefinition level, Rectangle bounds, Rectangle buttonBounds)
            {
                Level = level;
                Bounds = bounds;
                ButtonBounds = buttonBounds;
            }

            public GameLevelDefinition Level { get; }
            public Rectangle Bounds { get; }
            public Rectangle ButtonBounds { get; }
        }
    }
}
