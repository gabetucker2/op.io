using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class MapBlock
    {
        public const string BlockTitle = "Map";

        private const string ModeRowKey = "Mode";
        private const string ChunkModeEncoded = "chunk";
        private const string GlobalModeEncoded = "global";
        private const int Padding = 8;
        private const int ToggleButtonWidth = 28;
        private const int ToggleButtonHeight = 20;
        private const int ToggleButtonGap = 2;
        private const int ToggleButtonInset = 6;
        private const int MaxTextureAxis = 176;
        private const int MinTextureAxis = 24;
        private const float BoundsEpsilon = 0.5f;
        private const string MapTextureWorkKey = "MapTexture";
        private const int GlobalMaxTextureAxis = 96;
        private const int ChunkMaxTextureAxis = 144;
        private const float MinimumWorldUnitsPerTexel = 1f;
        private const float GlobalPanCacheScale = 2.5f;
        private const float GlobalPanCacheSnapFraction = 0.5f;
        private const float ScaleBarTargetWidthFraction = 0.28f;
        private const int ScaleBarMinPixelWidth = 34;
        private const int ScaleBarMaxPixelWidth = 120;
        private const int ScaleBarInset = 8;
        private const int ScaleBarTickHeight = 5;
        private const float ScaleBarLabelScale = 0.58f;

        private enum MapMode
        {
            Chunk,
            Global
        }

        private const MapMode DefaultMode = MapMode.Global;

        private static MapMode _mode = DefaultMode;
        private static bool _modeLoaded;
        private static Rectangle _chunkButtonBounds;
        private static Rectangle _globalButtonBounds;
        private static Point _lastMousePosition;
        private static Texture2D _pixelTexture;
        private static Texture2D _mapTexture;
        private static RasterizerState _mapScissorRasterizerState;
        private static Color[] _mapPixels = Array.Empty<Color>();
        private static int _mapTextureWidth;
        private static int _mapTextureHeight;
        private static MapMode _lastRenderedMode = (MapMode)(-1);
        private static float _lastMinX;
        private static float _lastMaxX;
        private static float _lastMinY;
        private static float _lastMaxY;
        private static bool _lastBoundsValid;
        private static int _lastSampleCount;
        private static double _lastRebuildMilliseconds;
        private static string _lastBoundsSummary = "none";
        private static string _lastTextureResolution = "none";
        private static bool _lastRenderedColorsValid;
        private static uint _lastRenderedTerrainColorPacked;
        private static uint _lastRenderedEmptyColorPacked;
        private static uint _lastRenderedMissingChunkColorPacked;
        private static bool _hasQueuedTextureBuild;
        private static int _queuedTextureWidth;
        private static int _queuedTextureHeight;
        private static float _queuedMinX;
        private static float _queuedMaxX;
        private static float _queuedMinY;
        private static float _queuedMaxY;
        private static MapMode _queuedMode = (MapMode)(-1);
        private static long _queuedTextureRequestId;
        private static long _lastAppliedTextureRequestId;
        private static bool _hasDeferredTextureBuild;
        private static int _deferredTextureWidth;
        private static int _deferredTextureHeight;
        private static int _deferredLodCellPixels;
        private static float _deferredMinX;
        private static float _deferredMaxX;
        private static float _deferredMinY;
        private static float _deferredMaxY;
        private static MapMode _deferredMode = (MapMode)(-1);
        private static int _deferredTextureRebuildCount;
        private static int _queuedLodCellPixels;
        private static int _lastLodCellPixels;
        private static float _lastWorldUnitsPerTexel;
        private static string _lastLodSummary = "none";

        public static string CurrentModeName => _mode == MapMode.Global ? "Global" : "Chunk";
        public static int LastSampleCount => _lastSampleCount;
        public static double LastRebuildMilliseconds => _lastRebuildMilliseconds;
        public static string LastBoundsSummary => _lastBoundsSummary;
        public static string LastTextureResolution => _lastTextureResolution;
        public static string LastLodSummary => _lastLodSummary;
        public static int LastLodCellPixels => _lastLodCellPixels;
        public static float LastWorldUnitsPerTexel => _lastWorldUnitsPerTexel;
        public static string LoadStatus => BlockAsyncLoadManager.GetBlockStatus(DockBlockKind.Map);
        public static long QueuedTextureRequestId => _queuedTextureRequestId;
        public static long LastAppliedTextureRequestId => _lastAppliedTextureRequestId;
        public static bool HasDeferredTextureBuild => _hasDeferredTextureBuild;
        public static int DeferredTextureRebuildCount => _deferredTextureRebuildCount;

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            LoadModeIfNeeded();
            _lastMousePosition = mouseState.Position;

            UpdateToggleButtonBounds(GetMapArea(contentBounds));

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Map);
            bool clicked = !blockLocked &&
                mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed;
            if (!clicked)
            {
                return;
            }

            if (_chunkButtonBounds.Contains(mouseState.Position))
            {
                SetMode(MapMode.Chunk);
            }
            else if (_globalButtonBounds.Contains(mouseState.Position))
            {
                SetMode(MapMode.Global);
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            LoadModeIfNeeded();
            EnsurePixelTexture(spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice);
            if (_pixelTexture == null)
            {
                return;
            }

            Rectangle mapArea = GetMapArea(contentBounds);
            UpdateToggleButtonBounds(mapArea);

            if (mapArea.Width <= 2 || mapArea.Height <= 2)
            {
                return;
            }

            if (!GameBlockTerrainBackground.TryGetMapWorldBounds(
                    _mode == MapMode.Chunk,
                    out float minX,
                    out float maxX,
                    out float minY,
                    out float maxY))
            {
                FillRect(spriteBatch, mapArea, ColorPalette.BlockBackground * 0.8f);
                DrawModeToggle(spriteBatch);
                return;
            }

            if (_mode == MapMode.Global)
            {
                CenterGlobalBoundsOnPlayer(mapArea, ref minX, ref maxX, ref minY, ref maxY);
            }

            Rectangle drawBounds = FitWorldBoundsToArea(mapArea, minX, maxX, minY, maxY);
            if (drawBounds.Width <= 0 || drawBounds.Height <= 0)
            {
                DrawModeToggle(spriteBatch);
                return;
            }

            MaybeRebuildMapTexture(spriteBatch.GraphicsDevice, drawBounds, minX, maxX, minY, maxY);

            FillRect(spriteBatch, mapArea, ColorPalette.BlockBackground * 0.78f);
            DrawCachedMapTexture(spriteBatch, drawBounds, minX, maxX, minY, maxY);

            DrawRectOutline(spriteBatch, drawBounds, UIStyle.BlockBorder, 1);
            DrawVisionCircle(spriteBatch, drawBounds, minX, maxX, minY, maxY);
            DrawPlayerMarker(spriteBatch, drawBounds, minX, maxX, minY, maxY);
            DrawScaleReference(spriteBatch, drawBounds, minX, maxX);
            DrawModeToggle(spriteBatch);
        }

        private static void LoadModeIfNeeded()
        {
            if (_modeLoaded)
            {
                return;
            }

            _modeLoaded = true;
            var data = BlockDataStore.LoadRowData(DockBlockKind.Map);
            if (data.TryGetValue(ModeRowKey, out string encoded))
            {
                _mode = DecodeMode(encoded);
                return;
            }

            _mode = DefaultMode;
            BlockDataStore.SetRowData(DockBlockKind.Map, ModeRowKey, EncodeMode(_mode));
        }

        private static void SetMode(MapMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            _lastRenderedMode = (MapMode)(-1);
            _hasQueuedTextureBuild = false;
            _hasDeferredTextureBuild = false;
            BlockDataStore.SetRowData(DockBlockKind.Map, ModeRowKey, EncodeMode(_mode));
        }

        private static string EncodeMode(MapMode mode) => mode == MapMode.Global ? GlobalModeEncoded : ChunkModeEncoded;

        private static MapMode DecodeMode(string encoded)
        {
            if (string.Equals(encoded, GlobalModeEncoded, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(encoded, "1", StringComparison.OrdinalIgnoreCase))
            {
                return MapMode.Global;
            }

            return MapMode.Chunk;
        }

        private static Rectangle GetMapArea(Rectangle contentBounds)
        {
            return new Rectangle(
                contentBounds.X + Padding,
                contentBounds.Y + Padding,
                Math.Max(0, contentBounds.Width - (Padding * 2)),
                Math.Max(0, contentBounds.Height - (Padding * 2)));
        }

        private static void UpdateToggleButtonBounds(Rectangle mapArea)
        {
            if (mapArea.Width <= 0 || mapArea.Height <= 0)
            {
                _chunkButtonBounds = Rectangle.Empty;
                _globalButtonBounds = Rectangle.Empty;
                return;
            }

            int totalHeight = (ToggleButtonHeight * 2) + ToggleButtonGap;
            int x = mapArea.Right - ToggleButtonWidth - ToggleButtonInset;
            int y = mapArea.Y + Math.Max(ToggleButtonInset, (mapArea.Height - totalHeight) / 2);
            if (y + totalHeight > mapArea.Bottom - ToggleButtonInset)
            {
                y = Math.Max(mapArea.Y + ToggleButtonInset, mapArea.Bottom - ToggleButtonInset - totalHeight);
            }

            _chunkButtonBounds = new Rectangle(x, y, ToggleButtonWidth, ToggleButtonHeight);
            _globalButtonBounds = new Rectangle(x, _chunkButtonBounds.Bottom + ToggleButtonGap, ToggleButtonWidth, ToggleButtonHeight);
        }

        private static void DrawModeToggle(SpriteBatch spriteBatch)
        {
            DrawModeButton(spriteBatch, _chunkButtonBounds, "C", _mode == MapMode.Chunk);
            DrawModeButton(spriteBatch, _globalButtonBounds, "G", _mode == MapMode.Global);
        }

        private static void DrawModeButton(SpriteBatch spriteBatch, Rectangle bounds, string label, bool selected)
        {
            bool locked = BlockManager.IsBlockLocked(DockBlockKind.Map);
            bool hovered = !locked && UIButtonRenderer.IsHovered(bounds, _lastMousePosition);
            UIButtonRenderer.Draw(
                spriteBatch,
                bounds,
                label,
                selected ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                hovered,
                locked);
        }

        private static Rectangle FitWorldBoundsToArea(Rectangle area, float minX, float maxX, float minY, float maxY)
        {
            float worldWidth = MathF.Max(1f, maxX - minX);
            float worldHeight = MathF.Max(1f, maxY - minY);
            float scale = MathF.Min(area.Width / worldWidth, area.Height / worldHeight);
            if (!float.IsFinite(scale) || scale <= 0f)
            {
                return Rectangle.Empty;
            }

            int width = Math.Max(1, (int)MathF.Floor(worldWidth * scale));
            int height = Math.Max(1, (int)MathF.Floor(worldHeight * scale));
            return new Rectangle(
                area.X + ((area.Width - width) / 2),
                area.Y + ((area.Height - height) / 2),
                width,
                height);
        }

        private static void CenterGlobalBoundsOnPlayer(Rectangle mapArea, ref float minX, ref float maxX, ref float minY, ref float maxY)
        {
            Vector2 center = Core.Instance?.PlayerOrNull?.Position ?? new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || mapArea.Width <= 0 || mapArea.Height <= 0)
            {
                return;
            }

            float viewWidth = MathF.Max(1f, maxX - minX);
            float viewHeight = MathF.Max(1f, maxY - minY);
            float areaAspect = mapArea.Width / (float)Math.Max(1, mapArea.Height);
            float viewAspect = viewWidth / viewHeight;
            if (areaAspect > viewAspect)
            {
                viewWidth = viewHeight * areaAspect;
            }
            else if (areaAspect < viewAspect)
            {
                viewHeight = viewWidth / MathF.Max(0.001f, areaAspect);
            }

            minX = center.X - (viewWidth * 0.5f);
            maxX = center.X + (viewWidth * 0.5f);
            minY = center.Y - (viewHeight * 0.5f);
            maxY = center.Y + (viewHeight * 0.5f);
        }

        private static void MaybeRebuildMapTexture(
            GraphicsDevice graphicsDevice,
            Rectangle drawBounds,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            TryPromoteCompletedMapTexture(graphicsDevice);

            MapTextureRenderPlan renderPlan = ResolveMapTextureRenderPlan(drawBounds, minX, maxX, minY, maxY, _mode);
            int desiredWidth = renderPlan.TextureWidth;
            int desiredHeight = renderPlan.TextureHeight;
            bool boundsChanged = !_lastBoundsValid ||
                MathF.Abs(renderPlan.SampleMinX - _lastMinX) > renderPlan.BoundsEpsilon ||
                MathF.Abs(renderPlan.SampleMaxX - _lastMaxX) > renderPlan.BoundsEpsilon ||
                MathF.Abs(renderPlan.SampleMinY - _lastMinY) > renderPlan.BoundsEpsilon ||
                MathF.Abs(renderPlan.SampleMaxY - _lastMaxY) > renderPlan.BoundsEpsilon ||
                _lastLodCellPixels != renderPlan.CellPixels;
            bool textureChanged = _mapTexture == null ||
                _mapTexture.IsDisposed ||
                desiredWidth != _mapTextureWidth ||
                desiredHeight != _mapTextureHeight;
            Color terrainColor = AmbienceSettings.TerrainColor;
            Color emptyColor = ColorPalette.BlockBackground;
            Color missingChunkColor = ColorPalette.BlockBorder * 0.65f;
            bool colorsChanged = !_lastRenderedColorsValid ||
                _lastRenderedTerrainColorPacked != terrainColor.PackedValue ||
                _lastRenderedEmptyColorPacked != emptyColor.PackedValue ||
                _lastRenderedMissingChunkColorPacked != missingChunkColor.PackedValue;
            bool modeChanged = _lastRenderedMode != _mode;
            bool globalViewCoveredByCache =
                _mode == MapMode.Global &&
                !textureChanged &&
                !colorsChanged &&
                !modeChanged &&
                _lastLodCellPixels == renderPlan.CellPixels &&
                IsVisibleWorldBoundsInsideLastTexture(minX, maxX, minY, maxY, BoundsEpsilon);

            if (globalViewCoveredByCache)
            {
                _hasDeferredTextureBuild = false;
                return;
            }

            if (!boundsChanged && !textureChanged && !colorsChanged && !modeChanged)
            {
                _hasDeferredTextureBuild = false;
                return;
            }

            if (BlockAsyncLoadManager.IsBlockLoading(DockBlockKind.Map))
            {
                if (_hasQueuedTextureBuild &&
                    _queuedTextureWidth == desiredWidth &&
                    _queuedTextureHeight == desiredHeight &&
                    _queuedLodCellPixels == renderPlan.CellPixels &&
                    _queuedMode == _mode &&
                    MathF.Abs(_queuedMinX - renderPlan.SampleMinX) <= renderPlan.BoundsEpsilon &&
                    MathF.Abs(_queuedMaxX - renderPlan.SampleMaxX) <= renderPlan.BoundsEpsilon &&
                    MathF.Abs(_queuedMinY - renderPlan.SampleMinY) <= renderPlan.BoundsEpsilon &&
                    MathF.Abs(_queuedMaxY - renderPlan.SampleMaxY) <= renderPlan.BoundsEpsilon)
                {
                    return;
                }

                RememberDeferredTextureBuild(renderPlan);
                return;
            }

            MapTextureBuildInput input = new()
            {
                Width = desiredWidth,
                Height = desiredHeight,
                MinX = renderPlan.SampleMinX,
                MaxX = renderPlan.SampleMaxX,
                MinY = renderPlan.SampleMinY,
                MaxY = renderPlan.SampleMaxY,
                Mode = _mode,
                TerrainColor = terrainColor,
                EmptyColor = emptyColor,
                MissingChunkColor = missingChunkColor,
                LodCellPixels = renderPlan.CellPixels,
                WorldUnitsPerTexel = renderPlan.WorldUnitsPerTexel,
                LodSummary = renderPlan.Summary
            };

            _queuedTextureWidth = desiredWidth;
            _queuedTextureHeight = desiredHeight;
            _queuedLodCellPixels = renderPlan.CellPixels;
            _queuedMinX = renderPlan.SampleMinX;
            _queuedMaxX = renderPlan.SampleMaxX;
            _queuedMinY = renderPlan.SampleMinY;
            _queuedMaxY = renderPlan.SampleMaxY;
            _queuedMode = _mode;
            _hasQueuedTextureBuild = true;
            _hasDeferredTextureBuild = false;
            _queuedTextureRequestId = BlockAsyncLoadManager.QueueLatest(
                DockBlockKind.Map,
                MapTextureWorkKey,
                "building map terrain and ocean-biome texture",
                token => BuildMapTexturePixels(input, token));
            _lastBoundsSummary = $"{CentifootUnits.FormatDistance(renderPlan.SampleMinX)}, {CentifootUnits.FormatDistance(renderPlan.SampleMinY)} -> {CentifootUnits.FormatDistance(renderPlan.SampleMaxX)}, {CentifootUnits.FormatDistance(renderPlan.SampleMaxY)}";
            _lastTextureResolution = $"{desiredWidth}x{desiredHeight} queued {renderPlan.Summary}";
            _lastLodSummary = renderPlan.Summary;
        }

        private static void RememberDeferredTextureBuild(MapTextureRenderPlan renderPlan)
        {
            if (_hasDeferredTextureBuild &&
                _deferredTextureWidth == renderPlan.TextureWidth &&
                _deferredTextureHeight == renderPlan.TextureHeight &&
                _deferredLodCellPixels == renderPlan.CellPixels &&
                _deferredMode == _mode &&
                MathF.Abs(_deferredMinX - renderPlan.SampleMinX) <= renderPlan.BoundsEpsilon &&
                MathF.Abs(_deferredMaxX - renderPlan.SampleMaxX) <= renderPlan.BoundsEpsilon &&
                MathF.Abs(_deferredMinY - renderPlan.SampleMinY) <= renderPlan.BoundsEpsilon &&
                MathF.Abs(_deferredMaxY - renderPlan.SampleMaxY) <= renderPlan.BoundsEpsilon)
            {
                return;
            }

            _hasDeferredTextureBuild = true;
            _deferredTextureWidth = renderPlan.TextureWidth;
            _deferredTextureHeight = renderPlan.TextureHeight;
            _deferredLodCellPixels = renderPlan.CellPixels;
            _deferredMinX = renderPlan.SampleMinX;
            _deferredMaxX = renderPlan.SampleMaxX;
            _deferredMinY = renderPlan.SampleMinY;
            _deferredMaxY = renderPlan.SampleMaxY;
            _deferredMode = _mode;
            _deferredTextureRebuildCount++;
            _lastBoundsSummary = "map rebuild deferred until active texture build completes";
            _lastTextureResolution = $"{renderPlan.TextureWidth}x{renderPlan.TextureHeight} deferred {renderPlan.Summary}";
            _lastLodSummary = renderPlan.Summary;
        }

        private static MapTextureRenderPlan ResolveMapTextureRenderPlan(
            Rectangle drawBounds,
            float minX,
            float maxX,
            float minY,
            float maxY,
            MapMode mode)
        {
            float visibleWorldWidth = MathF.Max(1f, maxX - minX);
            float visibleWorldHeight = MathF.Max(1f, maxY - minY);
            float worldUnitsPerDisplayPixel = MathF.Max(
                visibleWorldWidth / Math.Max(1, drawBounds.Width),
                visibleWorldHeight / Math.Max(1, drawBounds.Height));
            if (!float.IsFinite(worldUnitsPerDisplayPixel) || worldUnitsPerDisplayPixel <= 0f)
            {
                worldUnitsPerDisplayPixel = 1f;
            }

            int cellPixels = ResolveLodCellPixels(worldUnitsPerDisplayPixel, mode);
            int maxAxis = mode == MapMode.Global ? GlobalMaxTextureAxis : ChunkMaxTextureAxis;
            int textureWidth = Math.Clamp((int)MathF.Ceiling(drawBounds.Width / (float)cellPixels), MinTextureAxis, Math.Min(MaxTextureAxis, maxAxis));
            int textureHeight = Math.Clamp((int)MathF.Ceiling(drawBounds.Height / (float)cellPixels), MinTextureAxis, Math.Min(MaxTextureAxis, maxAxis));

            float sampleMinX = minX;
            float sampleMaxX = maxX;
            float sampleMinY = minY;
            float sampleMaxY = maxY;
            if (mode == MapMode.Global)
            {
                float centerX = (minX + maxX) * 0.5f;
                float centerY = (minY + maxY) * 0.5f;
                float sampleWorldWidth = MathF.Max(visibleWorldWidth, visibleWorldWidth * GlobalPanCacheScale);
                float sampleWorldHeight = MathF.Max(visibleWorldHeight, visibleWorldHeight * GlobalPanCacheScale);
                float paddingX = MathF.Max(0f, (sampleWorldWidth - visibleWorldWidth) * 0.5f);
                float paddingY = MathF.Max(0f, (sampleWorldHeight - visibleWorldHeight) * 0.5f);
                float snapStepX = MathF.Max(MinimumWorldUnitsPerTexel, paddingX * GlobalPanCacheSnapFraction);
                float snapStepY = MathF.Max(MinimumWorldUnitsPerTexel, paddingY * GlobalPanCacheSnapFraction);

                sampleMinX = SnapDown(centerX - (sampleWorldWidth * 0.5f), snapStepX);
                sampleMaxX = sampleMinX + sampleWorldWidth;
                sampleMinY = SnapDown(centerY - (sampleWorldHeight * 0.5f), snapStepY);
                sampleMaxY = sampleMinY + sampleWorldHeight;
                if (sampleMinX > minX)
                {
                    sampleMinX = minX;
                }

                if (sampleMaxX < maxX)
                {
                    sampleMaxX = maxX;
                }

                if (sampleMinY > minY)
                {
                    sampleMinY = minY;
                }

                if (sampleMaxY < maxY)
                {
                    sampleMaxY = maxY;
                }
            }

            float texelWorldX = MathF.Max(MinimumWorldUnitsPerTexel, (sampleMaxX - sampleMinX) / Math.Max(1, textureWidth));
            float texelWorldY = MathF.Max(MinimumWorldUnitsPerTexel, (sampleMaxY - sampleMinY) / Math.Max(1, textureHeight));
            sampleMinX = SnapDown(sampleMinX, texelWorldX);
            sampleMaxX = SnapUp(sampleMaxX, texelWorldX);
            sampleMinY = SnapDown(sampleMinY, texelWorldY);
            sampleMaxY = SnapUp(sampleMaxY, texelWorldY);
            float boundsEpsilon = MathF.Max(BoundsEpsilon, MathF.Min(texelWorldX, texelWorldY) * 0.45f);
            float worldUnitsPerTexel = MathF.Max(texelWorldX, texelWorldY);
            string summary = mode == MapMode.Global
                ? $"lod={cellPixels}px/texel world={CentifootUnits.FormatDistance(worldUnitsPerTexel)} cache={GlobalPanCacheScale:0.#}x"
                : $"lod={cellPixels}px/texel world={CentifootUnits.FormatDistance(worldUnitsPerTexel)}";

            return new MapTextureRenderPlan(
                textureWidth,
                textureHeight,
                cellPixels,
                sampleMinX,
                sampleMaxX,
                sampleMinY,
                sampleMaxY,
                boundsEpsilon,
                worldUnitsPerTexel,
                summary);
        }

        private static int ResolveLodCellPixels(float worldUnitsPerDisplayPixel, MapMode mode)
        {
            int cellPixels = worldUnitsPerDisplayPixel switch
            {
                <= 4f => 1,
                <= 12f => 2,
                <= 32f => 3,
                <= 72f => 4,
                <= 160f => 6,
                <= 360f => 8,
                <= 800f => 12,
                _ => 16
            };

            if (mode == MapMode.Global)
            {
                cellPixels = Math.Max(3, cellPixels);
            }

            return Math.Clamp(cellPixels, 1, 16);
        }

        private static float SnapDown(float value, float step)
        {
            if (!float.IsFinite(value) || !float.IsFinite(step) || step <= 0f)
            {
                return value;
            }

            return MathF.Floor(value / step) * step;
        }

        private static float SnapUp(float value, float step)
        {
            if (!float.IsFinite(value) || !float.IsFinite(step) || step <= 0f)
            {
                return value;
            }

            return MathF.Ceiling(value / step) * step;
        }

        private static bool IsVisibleWorldBoundsInsideLastTexture(
            float minX,
            float maxX,
            float minY,
            float maxY,
            float tolerance)
        {
            if (!_lastBoundsValid ||
                _mapTexture == null ||
                _mapTexture.IsDisposed ||
                _lastRenderedMode != _mode)
            {
                return false;
            }

            float effectiveTolerance = MathF.Max(BoundsEpsilon, tolerance);
            return minX >= _lastMinX - effectiveTolerance &&
                maxX <= _lastMaxX + effectiveTolerance &&
                minY >= _lastMinY - effectiveTolerance &&
                maxY <= _lastMaxY + effectiveTolerance;
        }

        private static void TryPromoteCompletedMapTexture(GraphicsDevice graphicsDevice)
        {
            if (!BlockAsyncLoadManager.TryTakeCompleted(
                    DockBlockKind.Map,
                    MapTextureWorkKey,
                    out MapTextureBuildResult result,
                    out Exception error,
                    out long requestId))
            {
                return;
            }

            if ((_hasQueuedTextureBuild && requestId != _queuedTextureRequestId) ||
                (result != null && result.Mode != _mode))
            {
                return;
            }

            _hasQueuedTextureBuild = false;

            if (error != null)
            {
                _lastBoundsSummary = $"map build error: {error.GetType().Name}";
                DebugLogger.PrintWarning($"Map block texture build failed: {error.Message}");
                return;
            }

            if (result == null ||
                result.Pixels == null ||
                result.Pixels.Length != result.Width * result.Height ||
                result.Width <= 0 ||
                result.Height <= 0)
            {
                _lastBoundsSummary = "map build returned no pixels";
                return;
            }

            if (_mapTexture == null ||
                _mapTexture.IsDisposed ||
                _mapTextureWidth != result.Width ||
                _mapTextureHeight != result.Height)
            {
                _mapTexture?.Dispose();
                _mapTexture = new Texture2D(graphicsDevice, result.Width, result.Height, false, SurfaceFormat.Color);
                _mapTextureWidth = result.Width;
                _mapTextureHeight = result.Height;
            }

            _mapPixels = result.Pixels;
            _mapTexture.SetData(_mapPixels);

            _lastRenderedMode = result.Mode;
            _lastMinX = result.MinX;
            _lastMaxX = result.MaxX;
            _lastMinY = result.MinY;
            _lastMaxY = result.MaxY;
            _lastBoundsValid = true;
            _lastSampleCount = result.SampleCount;
            _lastRebuildMilliseconds = result.BuildMilliseconds;
            _lastLodCellPixels = result.LodCellPixels;
            _lastWorldUnitsPerTexel = result.WorldUnitsPerTexel;
            _lastLodSummary = result.LodSummary;
            _lastRenderedColorsValid = true;
            _lastRenderedTerrainColorPacked = result.TerrainColor.PackedValue;
            _lastRenderedEmptyColorPacked = result.EmptyColor.PackedValue;
            _lastRenderedMissingChunkColorPacked = result.MissingChunkColor.PackedValue;
            _lastBoundsSummary = $"{CentifootUnits.FormatDistance(result.MinX)}, {CentifootUnits.FormatDistance(result.MinY)} -> {CentifootUnits.FormatDistance(result.MaxX)}, {CentifootUnits.FormatDistance(result.MaxY)}";
            _lastTextureResolution = $"{result.Width}x{result.Height} {result.LodSummary}";
            _lastAppliedTextureRequestId = requestId;
        }

        private static MapTextureBuildResult BuildMapTexturePixels(MapTextureBuildInput input, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool chunkMode = input.Mode == MapMode.Chunk;
            float stepX = (input.MaxX - input.MinX) / Math.Max(1, input.Width);
            float stepY = (input.MaxY - input.MinY) / Math.Max(1, input.Height);
            Color[] pixels = new Color[input.Width * input.Height];
            int sampleCount = 0;
            using GameBlockTerrainBackground.MapSamplingContext mapSamplingContext =
                GameBlockTerrainBackground.CreateMapSamplingContext();

            for (int y = 0; y < input.Height; y++)
            {
                token.ThrowIfCancellationRequested();
                float worldY = input.MinY + ((y + 0.5f) * stepY);
                for (int x = 0; x < input.Width; x++)
                {
                    float worldX = input.MinX + ((x + 0.5f) * stepX);
                    int index = (y * input.Width) + x;
                    if (GameBlockTerrainBackground.TrySampleMapAtWorldPosition(
                            new Vector2(worldX, worldY),
                            chunkMode,
                            out bool isTerrain,
                            out OceanBiomeType oceanBiome,
                            out bool chunkLoaded,
                            backgroundSafe: true,
                            approximationWorldUnits: input.WorldUnitsPerTexel,
                            samplingContext: mapSamplingContext))
                    {
                        pixels[index] = isTerrain
                            ? input.TerrainColor
                            : ResolveOceanColor(oceanBiome);
                    }
                    else
                    {
                        pixels[index] = chunkLoaded ? input.EmptyColor : input.MissingChunkColor;
                    }

                    sampleCount++;
                }
            }

            stopwatch.Stop();
            return new MapTextureBuildResult
            {
                Pixels = pixels,
                Width = input.Width,
                Height = input.Height,
                SampleCount = sampleCount,
                BuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                MinX = input.MinX,
                MaxX = input.MaxX,
                MinY = input.MinY,
                MaxY = input.MaxY,
                Mode = input.Mode,
                TerrainColor = input.TerrainColor,
                EmptyColor = input.EmptyColor,
                MissingChunkColor = input.MissingChunkColor,
                LodCellPixels = input.LodCellPixels,
                WorldUnitsPerTexel = input.WorldUnitsPerTexel,
                LodSummary = input.LodSummary
            };
        }

        private static Color ResolveOceanColor(OceanBiomeType oceanBiome)
        {
            return oceanBiome switch
            {
                OceanBiomeType.Shallow => new Color(62, 172, 207, 255),
                OceanBiomeType.Sunlit => new Color(22, 121, 190, 255),
                OceanBiomeType.Twilight => new Color(13, 78, 145, 255),
                OceanBiomeType.Midnight => new Color(8, 45, 102, 255),
                OceanBiomeType.Abyss => new Color(4, 18, 53, 255),
                _ => new Color(22, 121, 190, 255)
            };
        }

        private static void DrawCachedMapTexture(
            SpriteBatch spriteBatch,
            Rectangle drawBounds,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (_mapTexture == null ||
                _mapTexture.IsDisposed ||
                !_lastBoundsValid ||
                _mapTextureWidth <= 0 ||
                _mapTextureHeight <= 0)
            {
                return;
            }

            if (_lastRenderedMode != MapMode.Global)
            {
                spriteBatch.Draw(_mapTexture, drawBounds, Color.White);
                return;
            }

            GraphicsDevice graphicsDevice = spriteBatch.GraphicsDevice ?? Core.Instance?.GraphicsDevice;
            if (graphicsDevice != null &&
                TryBuildMapScissorRectangle(drawBounds, graphicsDevice.Viewport, out Rectangle scissorRect))
            {
                DrawGlobalMapTextureWithScissor(spriteBatch, graphicsDevice, scissorRect, drawBounds, minX, maxX, minY, maxY);
                return;
            }

            DrawGlobalMapTextureSourceWindow(spriteBatch, drawBounds, minX, maxX, minY, maxY);
        }

        private static void DrawGlobalMapTextureWithScissor(
            SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice,
            Rectangle scissorRect,
            Rectangle drawBounds,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            Rectangle previousScissor = graphicsDevice.ScissorRectangle;
            spriteBatch.End();
            graphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                GetMapScissorRasterizerState(),
                null,
                BlockManager.CurrentUITransform);

            try
            {
                DrawGlobalMapTextureProjected(spriteBatch, drawBounds, minX, maxX, minY, maxY);
            }
            finally
            {
                spriteBatch.End();
                graphicsDevice.ScissorRectangle = previousScissor;
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone,
                    null,
                    BlockManager.CurrentUITransform);
            }
        }

        private static void DrawGlobalMapTextureProjected(
            SpriteBatch spriteBatch,
            Rectangle drawBounds,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            float visibleWidth = MathF.Max(1f, maxX - minX);
            float visibleHeight = MathF.Max(1f, maxY - minY);
            float cachedWidth = MathF.Max(1f, _lastMaxX - _lastMinX);
            float cachedHeight = MathF.Max(1f, _lastMaxY - _lastMinY);
            float pixelsPerWorldX = drawBounds.Width / visibleWidth;
            float pixelsPerWorldY = drawBounds.Height / visibleHeight;
            float destinationX = drawBounds.X + ((_lastMinX - minX) * pixelsPerWorldX);
            float destinationY = drawBounds.Y + ((_lastMinY - minY) * pixelsPerWorldY);
            float destinationWidth = cachedWidth * pixelsPerWorldX;
            float destinationHeight = cachedHeight * pixelsPerWorldY;
            if (!float.IsFinite(destinationX) ||
                !float.IsFinite(destinationY) ||
                !float.IsFinite(destinationWidth) ||
                !float.IsFinite(destinationHeight) ||
                destinationWidth <= 0f ||
                destinationHeight <= 0f)
            {
                DrawGlobalMapTextureSourceWindow(spriteBatch, drawBounds, minX, maxX, minY, maxY);
                return;
            }

            spriteBatch.Draw(
                _mapTexture,
                new Vector2(destinationX, destinationY),
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(destinationWidth / _mapTextureWidth, destinationHeight / _mapTextureHeight),
                SpriteEffects.None,
                0f);
        }

        private static void DrawGlobalMapTextureSourceWindow(
            SpriteBatch spriteBatch,
            Rectangle drawBounds,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (_mapTexture == null ||
                _mapTexture.IsDisposed ||
                _mapTextureWidth <= 0 ||
                _mapTextureHeight <= 0)
            {
                return;
            }

            float cachedWidth = MathF.Max(1f, _lastMaxX - _lastMinX);
            float cachedHeight = MathF.Max(1f, _lastMaxY - _lastMinY);
            float sourceLeft = ((minX - _lastMinX) / cachedWidth) * _mapTextureWidth;
            float sourceTop = ((minY - _lastMinY) / cachedHeight) * _mapTextureHeight;
            float sourceRight = ((maxX - _lastMinX) / cachedWidth) * _mapTextureWidth;
            float sourceBottom = ((maxY - _lastMinY) / cachedHeight) * _mapTextureHeight;
            if (!float.IsFinite(sourceLeft) ||
                !float.IsFinite(sourceTop) ||
                !float.IsFinite(sourceRight) ||
                !float.IsFinite(sourceBottom))
            {
                spriteBatch.Draw(_mapTexture, drawBounds, Color.White);
                return;
            }

            int sourceX = Math.Clamp((int)MathF.Floor(sourceLeft), 0, Math.Max(0, _mapTextureWidth - 1));
            int sourceY = Math.Clamp((int)MathF.Floor(sourceTop), 0, Math.Max(0, _mapTextureHeight - 1));
            int sourceMaxX = Math.Clamp((int)MathF.Ceiling(sourceRight), sourceX + 1, _mapTextureWidth);
            int sourceMaxY = Math.Clamp((int)MathF.Ceiling(sourceBottom), sourceY + 1, _mapTextureHeight);
            Rectangle sourceBounds = new(sourceX, sourceY, sourceMaxX - sourceX, sourceMaxY - sourceY);
            spriteBatch.Draw(_mapTexture, drawBounds, sourceBounds, Color.White);
        }

        private static bool TryBuildMapScissorRectangle(Rectangle drawBounds, Viewport viewport, out Rectangle scissorRect)
        {
            scissorRect = Rectangle.Empty;
            if (drawBounds.Width <= 0 || drawBounds.Height <= 0)
            {
                return false;
            }

            float uiScale = BlockManager.UIScale;
            scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(drawBounds.X * uiScale),
                    (int)(drawBounds.Y * uiScale),
                    (int)MathF.Ceiling(drawBounds.Width * uiScale),
                    (int)MathF.Ceiling(drawBounds.Height * uiScale))
                : drawBounds;

            scissorRect.X = Math.Clamp(scissorRect.X, 0, viewport.Width);
            scissorRect.Y = Math.Clamp(scissorRect.Y, 0, viewport.Height);
            scissorRect.Width = Math.Clamp(scissorRect.Width, 0, viewport.Width - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, viewport.Height - scissorRect.Y);
            if (BlockManager.IsContentClipActive)
            {
                scissorRect = Rectangle.Intersect(scissorRect, BlockManager.CurrentContentClipRectangle);
            }

            return scissorRect.Width > 0 && scissorRect.Height > 0;
        }

        private static RasterizerState GetMapScissorRasterizerState()
        {
            return _mapScissorRasterizerState ??= new RasterizerState
            {
                CullMode = CullMode.None,
                ScissorTestEnable = true
            };
        }

        private static void DrawVisionCircle(SpriteBatch spriteBatch, Rectangle mapBounds, float minX, float maxX, float minY, float maxY)
        {
            if (!GameBlockTerrainBackground.TryGetPlayerVisionCircle(out Vector2 center, out float radius))
            {
                return;
            }

            Vector2 screenCenter = WorldToMap(center, mapBounds, minX, maxX, minY, maxY);
            float pixelsPerWorldX = mapBounds.Width / MathF.Max(1f, maxX - minX);
            float pixelsPerWorldY = mapBounds.Height / MathF.Max(1f, maxY - minY);
            float pixelRadius = radius * MathF.Min(pixelsPerWorldX, pixelsPerWorldY);
            if (!float.IsFinite(pixelRadius) || pixelRadius <= 1f)
            {
                return;
            }

            int segments = Math.Clamp((int)(pixelRadius * 0.7f), 24, 96);
            Vector2 previous = screenCenter + new Vector2(pixelRadius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = MathF.Tau * i / segments;
                Vector2 next = screenCenter + new Vector2(MathF.Cos(angle) * pixelRadius, MathF.Sin(angle) * pixelRadius);
                DrawLine(spriteBatch, previous, next, Color.White * 0.78f, 2f);
                previous = next;
            }
        }

        private static void DrawPlayerMarker(SpriteBatch spriteBatch, Rectangle mapBounds, float minX, float maxX, float minY, float maxY)
        {
            Vector2 playerPosition = Core.Instance?.PlayerOrNull?.Position ?? Vector2.Zero;
            if (!float.IsFinite(playerPosition.X) || !float.IsFinite(playerPosition.Y))
            {
                return;
            }

            Vector2 marker = WorldToMap(playerPosition, mapBounds, minX, maxX, minY, maxY);
            Rectangle markerRect = new((int)MathF.Round(marker.X) - 2, (int)MathF.Round(marker.Y) - 2, 5, 5);
            FillRect(spriteBatch, markerRect, Color.White);
        }

        private static void DrawScaleReference(SpriteBatch spriteBatch, Rectangle mapBounds, float minX, float maxX)
        {
            if (spriteBatch == null ||
                _pixelTexture == null ||
                mapBounds.Width < ScaleBarMinPixelWidth + (ScaleBarInset * 2) ||
                mapBounds.Height < 34)
            {
                return;
            }

            float worldWidth = MathF.Max(1f, maxX - minX);
            float worldUnitsPerPixel = worldWidth / Math.Max(1f, mapBounds.Width);
            if (!float.IsFinite(worldUnitsPerPixel) || worldUnitsPerPixel <= 0f)
            {
                return;
            }

            float targetPixels = Math.Clamp(
                mapBounds.Width * ScaleBarTargetWidthFraction,
                ScaleBarMinPixelWidth,
                Math.Min(ScaleBarMaxPixelWidth, mapBounds.Width - (ScaleBarInset * 2)));
            float targetCentifoot = CentifootUnits.WorldToCentifoot(targetPixels * worldUnitsPerPixel);
            float niceCentifoot = ResolveNiceScaleCentifoot(targetCentifoot);
            float scaleWorldUnits = CentifootUnits.CentifootToWorld(niceCentifoot);
            float scalePixels = scaleWorldUnits / worldUnitsPerPixel;
            if (!float.IsFinite(scalePixels) || scalePixels < 8f)
            {
                return;
            }

            float maxPixels = Math.Max(8f, mapBounds.Width - (ScaleBarInset * 2));
            while (scalePixels > maxPixels && niceCentifoot > 0.001f)
            {
                niceCentifoot *= 0.5f;
                scaleWorldUnits = CentifootUnits.CentifootToWorld(niceCentifoot);
                scalePixels = scaleWorldUnits / worldUnitsPerPixel;
            }

            int lineWidth = Math.Clamp((int)MathF.Round(scalePixels), 8, Math.Max(8, (int)MathF.Floor(maxPixels)));
            UIStyle.UIFont font = UIStyle.FontTech;
            string label = $"{CentifootUnits.FormatNumber(niceCentifoot, ResolveScaleLabelFormat(niceCentifoot))} {CentifootUnits.UnitAbbreviation}";
            Vector2 labelSize = MeasureScaleLabel(font, label, ScaleBarLabelScale);
            int backingWidth = Math.Max(lineWidth + 10, (int)MathF.Ceiling(labelSize.X) + 10);
            int backingHeight = Math.Max(18, ScaleBarTickHeight + 8 + (int)MathF.Ceiling(labelSize.Y));
            int backingX = mapBounds.X + ScaleBarInset;
            int backingY = mapBounds.Bottom - ScaleBarInset - backingHeight;
            if (backingY < mapBounds.Y + ScaleBarInset)
            {
                backingY = mapBounds.Y + ScaleBarInset;
            }

            Rectangle backing = new(
                backingX,
                backingY,
                Math.Min(backingWidth, Math.Max(1, mapBounds.Right - ScaleBarInset - backingX)),
                backingHeight);
            if (backing.Width <= 0 || backing.Height <= 0)
            {
                return;
            }

            FillRect(spriteBatch, backing, Color.Black * 0.42f);
            int lineX = backing.X + 5;
            int lineY = backing.Bottom - 8;
            int clampedLineWidth = Math.Min(lineWidth, Math.Max(1, backing.Right - 5 - lineX));
            Color lineColor = Color.White * 0.9f;
            FillRect(spriteBatch, new Rectangle(lineX, lineY, clampedLineWidth, 2), lineColor);
            FillRect(spriteBatch, new Rectangle(lineX, lineY - ScaleBarTickHeight + 1, 2, ScaleBarTickHeight + 1), lineColor);
            FillRect(spriteBatch, new Rectangle(lineX + clampedLineWidth - 2, lineY - ScaleBarTickHeight + 1, 2, ScaleBarTickHeight + 1), lineColor);

            if (!font.IsAvailable || font.Font == null || string.IsNullOrEmpty(label))
            {
                return;
            }

            float labelScale = ResolveScaleLabelDrawScale(font, label, backing.Width - 8, ScaleBarLabelScale);
            Vector2 scaledLabelSize = MeasureScaleLabel(font, label, labelScale);
            Vector2 labelPosition = new(
                backing.X + ((backing.Width - scaledLabelSize.X) * 0.5f),
                backing.Y + 3f);
            spriteBatch.DrawString(
                font.Font,
                label,
                labelPosition + new Vector2(1f, 1f),
                Color.Black * 0.65f,
                0f,
                Vector2.Zero,
                font.Scale * labelScale,
                SpriteEffects.None,
                0f);
            spriteBatch.DrawString(
                font.Font,
                label,
                labelPosition,
                Color.White * 0.92f,
                0f,
                Vector2.Zero,
                font.Scale * labelScale,
                SpriteEffects.None,
                0f);
        }

        private static float ResolveNiceScaleCentifoot(float targetCentifoot)
        {
            if (!float.IsFinite(targetCentifoot) || targetCentifoot <= 0f)
            {
                return 1f;
            }

            float exponent = MathF.Pow(10f, MathF.Floor(MathF.Log10(targetCentifoot)));
            float normalized = targetCentifoot / exponent;
            float nice = normalized switch
            {
                < 1.5f => 1f,
                < 3.5f => 2f,
                < 7.5f => 5f,
                _ => 10f
            };

            return MathF.Max(0.001f, nice * exponent);
        }

        private static string ResolveScaleLabelFormat(float centifoot)
        {
            return centifoot >= 10f ? "0" : centifoot >= 1f ? "0.#" : "0.##";
        }

        private static Vector2 MeasureScaleLabel(UIStyle.UIFont font, string label, float labelScale)
        {
            if (!font.IsAvailable || font.Font == null || string.IsNullOrEmpty(label))
            {
                return Vector2.Zero;
            }

            return font.Font.MeasureString(label) * font.Scale * MathF.Max(0.05f, labelScale);
        }

        private static float ResolveScaleLabelDrawScale(UIStyle.UIFont font, string label, float availableWidth, float preferredScale)
        {
            if (!font.IsAvailable || font.Font == null || string.IsNullOrEmpty(label) || availableWidth <= 0f)
            {
                return preferredScale;
            }

            Vector2 preferredSize = MeasureScaleLabel(font, label, preferredScale);
            if (preferredSize.X <= availableWidth)
            {
                return preferredScale;
            }

            float baseWidth = MathF.Max(1f, font.Font.MeasureString(label).X * font.Scale);
            return MathHelper.Clamp(availableWidth / baseWidth, 0.32f, preferredScale);
        }

        private static Vector2 WorldToMap(Vector2 world, Rectangle mapBounds, float minX, float maxX, float minY, float maxY)
        {
            float u = (world.X - minX) / MathF.Max(1f, maxX - minX);
            float v = (world.Y - minY) / MathF.Max(1f, maxY - minY);
            return new Vector2(
                mapBounds.X + (u * mapBounds.Width),
                mapBounds.Y + (v * mapBounds.Height));
        }

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length <= 0.1f || _pixelTexture == null)
            {
                return;
            }

            float angle = MathF.Atan2(delta.Y, delta.X);
            spriteBatch.Draw(
                _pixelTexture,
                start,
                null,
                color,
                angle,
                new Vector2(0f, 0.5f),
                new Vector2(length, MathF.Max(1f, thickness)),
                SpriteEffects.None,
                0f);
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
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            thickness = Math.Max(1, thickness);
            FillRect(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            FillRect(spriteBatch, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            FillRect(spriteBatch, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            FillRect(spriteBatch, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }

        private static void EnsurePixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture != null || graphicsDevice == null)
            {
                return;
            }

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        private sealed class MapTextureBuildInput
        {
            public int Width;
            public int Height;
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
            public MapMode Mode;
            public Color TerrainColor;
            public Color EmptyColor;
            public Color MissingChunkColor;
            public int LodCellPixels;
            public float WorldUnitsPerTexel;
            public string LodSummary;
        }

        private sealed class MapTextureBuildResult
        {
            public Color[] Pixels;
            public int Width;
            public int Height;
            public int SampleCount;
            public double BuildMilliseconds;
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
            public MapMode Mode;
            public Color TerrainColor;
            public Color EmptyColor;
            public Color MissingChunkColor;
            public int LodCellPixels;
            public float WorldUnitsPerTexel;
            public string LodSummary;
        }

        private readonly struct MapTextureRenderPlan
        {
            public MapTextureRenderPlan(
                int textureWidth,
                int textureHeight,
                int cellPixels,
                float sampleMinX,
                float sampleMaxX,
                float sampleMinY,
                float sampleMaxY,
                float boundsEpsilon,
                float worldUnitsPerTexel,
                string summary)
            {
                TextureWidth = Math.Max(1, textureWidth);
                TextureHeight = Math.Max(1, textureHeight);
                CellPixels = Math.Max(1, cellPixels);
                SampleMinX = sampleMinX;
                SampleMaxX = sampleMaxX;
                SampleMinY = sampleMinY;
                SampleMaxY = sampleMaxY;
                BoundsEpsilon = MathF.Max(0.001f, boundsEpsilon);
                WorldUnitsPerTexel = MathF.Max(0f, worldUnitsPerTexel);
                Summary = string.IsNullOrWhiteSpace(summary) ? "lod=unknown" : summary;
            }

            public int TextureWidth { get; }
            public int TextureHeight { get; }
            public int CellPixels { get; }
            public float SampleMinX { get; }
            public float SampleMaxX { get; }
            public float SampleMinY { get; }
            public float SampleMaxY { get; }
            public float BoundsEpsilon { get; }
            public float WorldUnitsPerTexel { get; }
            public string Summary { get; }
        }
    }
}
