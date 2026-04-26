using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.FunCol;
using op.io.UI.FunCol.Features;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class AmbienceBlock
    {
        public const string BlockTitle = "Ambience";

        private static readonly float[] ColWeights = { 0.14f, 0.24f, 0.20f, 0.42f };
        private const int HeaderRowHeight = 16;
        private const int SwatchPadding = 6;
        private const int SwatchMinSize = 12;
        private const int SwatchPreferredSize = 18;

        private static readonly BlockScrollPanel _scrollPanel = new();
        private static readonly List<AmbienceRow> _rows = new();
        private static readonly Dictionary<string, FunColInterface> _rowFunCols = new(StringComparer.OrdinalIgnoreCase);
        private static FunColInterface _headerFunCol;
        private static Texture2D _pixelTexture;
        private static float _lineHeightCache;
        private static bool _headerVisibleLoaded;
        private static string _hoveredRowKey;
        private static string _tooltipRowKey;
        private static string _tooltipRowLabel;

        public static string GetHoveredRowKey() => _tooltipRowKey;

        public static string GetHoveredRowLabel() => _tooltipRowLabel;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            AmbienceSettings.Initialize();

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Ambience);
            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _scrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
            {
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);
            }

            FunColInterface headerFunCol = GetOrEnsureHeaderFunCol();
            if (!_headerVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Ambience);
                if (rowData.TryGetValue("FunColHeaderVisible", out string stored))
                {
                    headerFunCol.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                }

                if (rowData.TryGetValue("FunColColumnWeights", out string weightStr))
                {
                    ApplyEncodedWeights(headerFunCol, weightStr);
                }

                _headerVisibleLoaded = true;
            }

            RefreshRows();

            int headerHeight = headerFunCol.HeaderVisible ? HeaderRowHeight : 0;
            Rectangle headerStrip = new(contentBounds.X, contentBounds.Y, contentBounds.Width, headerHeight);
            headerFunCol.ShowHeaderToggle = BlockManager.DockingModeEnabled && !blockLocked && contentBounds.Contains(mouseState.Position);
            headerFunCol.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            headerFunCol.UpdateHeaderHover(headerStrip, mouseState, blockLocked ? (MouseState?)previousMouseState : null);

            if (headerFunCol.HeaderToggleClicked)
            {
                BlockDataStore.SetRowData(DockBlockKind.Ambience, "FunColHeaderVisible", headerFunCol.HeaderVisible ? "true" : "false");
            }

            if (headerFunCol.ColumnWeightsChanged)
            {
                string encoded = EncodeWeights(headerFunCol.GetWeights());
                BlockDataStore.SetRowData(DockBlockKind.Ambience, "FunColColumnWeights", encoded);
                PropagateWeightsToRowFunCols(headerFunCol.GetWeights());
            }

            Rectangle listArea = new(
                contentBounds.X,
                contentBounds.Y + headerHeight,
                contentBounds.Width,
                Math.Max(0, contentBounds.Height - headerHeight));

            float contentHeight = CalculateContentHeight(boldFont, listArea.Width);
            _scrollPanel.Update(listArea, contentHeight,
                BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty)
            {
                listBounds = listArea;
            }

            RebuildRowLayouts(listBounds, boldFont);

            float dt = (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? 0f);
            foreach (AmbienceRow row in _rows)
            {
                if (row.Bounds != Rectangle.Empty)
                {
                    SyncRowFunCol(row).Update(row.Bounds, mouseState, dt, blockLocked);
                }
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            bool pointerInsideList = listBounds.Contains(mouseState.Position);
            string hitRow = pointerInsideList ? HitTestRow(mouseState.Position) : null;

            _hoveredRowKey = !blockLocked ? hitRow : null;
            _tooltipRowKey = hitRow;
            _tooltipRowLabel = hitRow != null && TryGetRow(hitRow, out AmbienceRow hoveredRow) ? hoveredRow.Label : null;

            if (!blockLocked && leftClickStarted && TryGetRow(hitRow, out AmbienceRow rowToEdit))
            {
                OpenRowEditor(rowToEdit);
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            AmbienceSettings.Initialize();

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Ambience);
            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out _))
            {
                return;
            }

            EnsurePixelTexture();

            FunColInterface headerFunCol = GetOrEnsureHeaderFunCol();
            int headerHeight = headerFunCol.HeaderVisible ? HeaderRowHeight : 0;
            Rectangle listArea = new(
                contentBounds.X,
                contentBounds.Y + headerHeight,
                contentBounds.Width,
                Math.Max(0, contentBounds.Height - headerHeight));

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
            using var scissorState = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            graphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            foreach (AmbienceRow row in _rows)
            {
                if (row.Bounds == Rectangle.Empty)
                {
                    continue;
                }

                if (row.Bounds.Bottom <= listBounds.Y)
                {
                    continue;
                }

                if (row.Bounds.Y >= listBounds.Bottom)
                {
                    break;
                }

                DrawRow(spriteBatch, row, boldFont, listBounds);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            _scrollPanel.Draw(spriteBatch, blockLocked);

            headerFunCol.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            headerFunCol.DrawHeader(spriteBatch, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, headerHeight), boldFont, _pixelTexture);
        }

        private static void RefreshRows()
        {
            _rows.Clear();
            _rows.Add(new AmbienceRow(
                AmbienceSettings.FogOfWarRowKey,
                "Fog of war",
                AmbienceSettings.FogOfWarColor,
                "Base color of hidden territory."));
            _rows.Add(new AmbienceRow(
                AmbienceSettings.OceanWaterRowKey,
                "Ocean water",
                AmbienceSettings.OceanWaterColor,
                "Base hue driving the ocean water shader."));
            _rows.Add(new AmbienceRow(
                AmbienceSettings.BackgroundWavesRowKey,
                "Background waves",
                AmbienceSettings.BackgroundWavesColor,
                "Highlight color of the ocean's background wave crests."));
            _rows.Add(new AmbienceRow(
                AmbienceSettings.WorldTintRowKey,
                "World tint",
                AmbienceSettings.WorldTintColor,
                "50% gray is neutral. Other colors tint gameplay objects already in the world."));
        }

        private static float CalculateContentHeight(UIStyle.UIFont font, int listWidth)
        {
            if (_lineHeightCache <= 0f || _rows.Count == 0)
            {
                return 0f;
            }

            float total = 0f;
            foreach (AmbienceRow row in _rows)
            {
                total += CalculateRowHeight(font, row, listWidth);
            }

            return total;
        }

        private static float CalculateRowHeight(UIStyle.UIFont font, AmbienceRow row, int listWidth)
        {
            if (_lineHeightCache <= 0f)
            {
                return 0f;
            }

            FunColInterface funCol = SyncRowFunCol(row);
            Rectangle probeBounds = new(0, 0, Math.Max(1, listWidth), (int)MathF.Ceiling(_lineHeightCache));
            Rectangle effectColumnBounds = funCol.GetColumnBounds(3, probeBounds);
            if (funCol.GetFeature(3) is WrappingTextFeature effectFeature)
            {
                int lines = effectFeature.CalculateLineCount(font, effectColumnBounds.Width);
                float wrappedHeight = Math.Max(1, lines) * _lineHeightCache;
                return Math.Max(wrappedHeight, SwatchPreferredSize + (SwatchPadding * 2));
            }

            return Math.Max(_lineHeightCache, SwatchPreferredSize + (SwatchPadding * 2));
        }

        private static void RebuildRowLayouts(Rectangle listBounds, UIStyle.UIFont font)
        {
            if (_lineHeightCache <= 0f)
            {
                return;
            }

            float y = listBounds.Y - _scrollPanel.ScrollOffset;
            for (int i = 0; i < _rows.Count; i++)
            {
                AmbienceRow row = _rows[i];
                int rowHeight = (int)MathF.Ceiling(CalculateRowHeight(font, row, listBounds.Width));
                row.Bounds = new Rectangle(listBounds.X, (int)MathF.Round(y), listBounds.Width, rowHeight);
                _rows[i] = row;
                y += rowHeight;
            }
        }

        private static void DrawRow(SpriteBatch spriteBatch, AmbienceRow row, UIStyle.UIFont font, Rectangle listBounds)
        {
            Rectangle visibleBounds = Rectangle.Intersect(row.Bounds, listBounds);
            if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
            {
                return;
            }

            bool hovered = string.Equals(_hoveredRowKey, row.Key, StringComparison.OrdinalIgnoreCase);
            FillRect(spriteBatch, visibleBounds, hovered ? ColorPalette.RowHover : UIStyle.BlockBackground);

            FunColInterface funCol = SyncRowFunCol(row);
            Rectangle previewColumnBounds = funCol.GetColumnBounds(0, row.Bounds);
            Rectangle swatchBounds = BuildSwatchBounds(previewColumnBounds);
            if (swatchBounds != Rectangle.Empty)
            {
                FillRect(spriteBatch, swatchBounds, row.Color);
                DrawRectOutline(spriteBatch, swatchBounds, UIStyle.BlockBorder, 1);
            }

            funCol.Draw(spriteBatch, row.Bounds, font, _pixelTexture);
        }

        private static Rectangle BuildSwatchBounds(Rectangle previewColumnBounds)
        {
            if (previewColumnBounds.Width <= 0 || previewColumnBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int availableWidth = previewColumnBounds.Width - (SwatchPadding * 2);
            int availableHeight = previewColumnBounds.Height - (SwatchPadding * 2);
            if (availableWidth < SwatchMinSize || availableHeight < SwatchMinSize)
            {
                return Rectangle.Empty;
            }

            int size = Math.Min(SwatchPreferredSize, Math.Min(availableWidth, availableHeight));

            return new Rectangle(
                previewColumnBounds.X + (previewColumnBounds.Width - size) / 2,
                previewColumnBounds.Y + (previewColumnBounds.Height - size) / 2,
                size,
                size);
        }

        private static FunColInterface SyncRowFunCol(AmbienceRow row)
        {
            FunColInterface funCol = GetOrCreateRowFunCol(row.Key);
            if (funCol.GetFeature(0) is TextLabelFeature previewFeature)
            {
                previewFeature.Text = string.Empty;
            }

            if (funCol.GetFeature(1) is TextLabelFeature settingFeature)
            {
                settingFeature.Text = row.Label;
            }

            if (funCol.GetFeature(2) is ValueDisplayFeature hexFeature)
            {
                hexFeature.Text = ColorScheme.ToHex(row.Color, includeAlpha: true);
            }

            if (funCol.GetFeature(3) is WrappingTextFeature effectFeature)
            {
                effectFeature.Text = row.Description;
            }

            return funCol;
        }

        private static FunColInterface GetOrCreateRowFunCol(string rowKey)
        {
            if (_rowFunCols.TryGetValue(rowKey, out FunColInterface existing))
            {
                return existing;
            }

            float[] weights = _headerFunCol?.GetWeights() ?? ColWeights;
            FunColInterface created = new(
                weights,
                new TextLabelFeature("Preview", FunColTextAlign.Center),
                new TextLabelFeature("Setting", FunColTextAlign.Left),
                new ValueDisplayFeature("Hex") { TextAlign = FunColTextAlign.Right },
                new WrappingTextFeature("Effect", FunColTextAlign.Left));

            created.DisableExpansion = true;
            created.DisableColors = true;
            created.SuppressTooltipWarnings = true;
            _rowFunCols[rowKey] = created;
            return created;
        }

        private static FunColInterface GetOrEnsureHeaderFunCol()
        {
            if (_headerFunCol != null)
            {
                return _headerFunCol;
            }

            _headerFunCol = new FunColInterface(
                ColWeights,
                new TextLabelFeature("Preview", FunColTextAlign.Center)
                {
                    HeaderTooltipTexts = ["Live color swatch for the ambience setting."]
                },
                new TextLabelFeature("Setting", FunColTextAlign.Left)
                {
                    HeaderTooltipTexts = ["Ambience setting name. Click any row to edit it live."]
                },
                new ValueDisplayFeature("Hex")
                {
                    TextAlign = FunColTextAlign.Right,
                    HeaderTooltipTexts = ["Current RGBA hex value for the setting."]
                },
                new WrappingTextFeature("Effect", FunColTextAlign.Left)
                {
                    HeaderTooltipTexts = ["What this setting changes in the game viewport."]
                });

            _headerFunCol.DisableExpansion = true;
            _headerFunCol.DisableColors = true;
            _headerFunCol.EnableColumnResize = true;
            _headerFunCol.ShowHeaderTooltips = true;
            return _headerFunCol;
        }

        private static void PropagateWeightsToRowFunCols(float[] weights)
        {
            foreach (FunColInterface funCol in _rowFunCols.Values)
            {
                funCol.SetWeights(weights);
            }
        }

        private static string EncodeWeights(float[] weights)
        {
            System.Text.StringBuilder builder = new();
            for (int i = 0; i < weights.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                builder.Append(weights[i].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void ApplyEncodedWeights(FunColInterface funCol, string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return;
            }

            string[] parts = encoded.Split('|');
            float[] weights = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weights[i]))
                {
                    return;
                }
            }

            funCol.SetWeights(weights);
        }

        private static string HitTestRow(Point pointer)
        {
            foreach (AmbienceRow row in _rows)
            {
                if (row.Bounds.Contains(pointer))
                {
                    return row.Key;
                }
            }

            return null;
        }

        private static bool TryGetRow(string rowKey, out AmbienceRow row)
        {
            foreach (AmbienceRow candidate in _rows)
            {
                if (string.Equals(candidate.Key, rowKey, StringComparison.OrdinalIgnoreCase))
                {
                    row = candidate;
                    return true;
                }
            }

            row = default;
            return false;
        }

        private static void OpenRowEditor(AmbienceRow row)
        {
            Color originalColor = row.Color;
            ColorSchemeBlock.OpenExternalColorEditor(
                nameof(AmbienceBlock),
                row.Label,
                originalColor,
                liveColor =>
                {
                    ApplyRowColor(row.Key, liveColor, persist: false);
                    RefreshRows();
                },
                (finalColor, applyChanges) =>
                {
                    if (applyChanges)
                    {
                        ApplyRowColor(row.Key, finalColor, persist: true);
                    }

                    RefreshRows();
                });
        }

        private static void ApplyRowColor(string rowKey, Color color, bool persist)
        {
            if (string.Equals(rowKey, AmbienceSettings.FogOfWarRowKey, StringComparison.OrdinalIgnoreCase))
            {
                AmbienceSettings.SetFogOfWarColor(color, persist);
                return;
            }

            if (string.Equals(rowKey, AmbienceSettings.OceanWaterRowKey, StringComparison.OrdinalIgnoreCase))
            {
                AmbienceSettings.SetOceanWaterColor(color, persist);
                return;
            }

            if (string.Equals(rowKey, AmbienceSettings.BackgroundWavesRowKey, StringComparison.OrdinalIgnoreCase))
            {
                AmbienceSettings.SetBackgroundWavesColor(color, persist);
                return;
            }

            if (string.Equals(rowKey, AmbienceSettings.WorldTintRowKey, StringComparison.OrdinalIgnoreCase))
            {
                AmbienceSettings.SetWorldTintColor(color, persist);
            }
        }

        private static void EnsurePixelTexture()
        {
            if (_pixelTexture != null && !_pixelTexture.IsDisposed)
            {
                return;
            }

            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return;
            }

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData([Color.White]);
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }

        private struct AmbienceRow
        {
            public AmbienceRow(string key, string label, Color color, string description)
            {
                Key = key;
                Label = label;
                Color = color;
                Description = description;
                Bounds = Rectangle.Empty;
            }

            public string Key;
            public string Label;
            public Color Color;
            public string Description;
            public Rectangle Bounds;
        }
    }
}
