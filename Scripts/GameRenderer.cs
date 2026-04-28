using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io.UI.BlockScripts.Blocks;

namespace op.io
{
    public static class GameRenderer
    {
        private static readonly int GridStepWorldUnits = Math.Max(1, (int)MathF.Round(CentifootUnits.CentifootToWorld(1f)));
        private static readonly int GridCoordinateStepWorldUnits = Math.Max(GridStepWorldUnits, (int)MathF.Round(CentifootUnits.CentifootToWorld(5f)));
        private const int GridCoordinateTextStride = 2;
        private const int MaxGridLinesPerAxis = 6000;
        private const int MaxGridCoordinateLabels = 900;
        private static readonly Color GridMinorLineColor = new(128, 128, 128, 34);
        private static readonly Color GridMajorLineColor = new(154, 154, 154, 82);
        private static readonly Color GridCoordinateTextColor = new(210, 210, 210, 174);
        private static readonly Color GridCoordinateTextShadowColor = new(0, 0, 0, 120);
        private static Texture2D _gridPixelTexture;

        public static bool IsDrawing { get; private set; }
        public static void LoadGraphics()
        {
            if (Core.Instance == null)
            {
                DebugLogger.PrintError("LoadGraphics failed: Core.Instance is null.");
                return;
            }

            if (Core.Instance.GraphicsDevice == null)
            {
                DebugLogger.PrintError("LoadGraphics failed: GraphicsDevice is null.");
                return;
            }

            if (Core.Instance.SpriteBatch == null)
            {
                Core.Instance.SpriteBatch = new SpriteBatch(Core.Instance.GraphicsDevice);
                DebugLogger.Print("SpriteBatch initialized successfully.");
            }

            DebugRenderer.Initialize(Core.Instance.GraphicsDevice);
            BlockManager.OnGraphicsReady();
            GameBlockOceanBackground.Initialize(Core.Instance.GraphicsDevice, Core.Instance.Content);
            GameBlockTerrainBackground.Initialize(Core.Instance.GraphicsDevice);
            AmbienceSettings.Initialize();
            GameBlock.Initialize(Core.Instance.Content);
            XPClumpManager.LoadContent(Core.Instance.GraphicsDevice);

            if (Core.Instance.GameObjects == null || Core.Instance.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects to load content for.");
                return;
            }

            HashSet<Shape> loadedShapes = [];

            foreach (GameObject obj in Core.Instance.GameObjects)
            {
                if (obj.Shape != null && !loadedShapes.Contains(obj.Shape))
                {
                    obj.Shape.LoadContent(Core.Instance.GraphicsDevice);
                    loadedShapes.Add(obj.Shape);
                }
            }

            // Load barrel shapes for all barrel slots on each agent
            foreach (GameObject obj in Core.Instance.GameObjects)
            {
                if (obj is Agent agent)
                {
                    foreach (var slot in agent.Barrels)
                        slot.FullShape?.LoadContent(Core.Instance.GraphicsDevice);
                }
            }

            DebugLogger.Print("GameRenderer: Graphics and GameObjects loaded successfully.");
        }

        public static bool PrepareStartupTerrainForWindowReveal()
        {
            if (Core.Instance?.GraphicsDevice == null || Core.Instance.SpriteBatch == null)
            {
                DebugLogger.PrintWarning("Startup terrain preparation skipped: graphics are not ready.");
                return false;
            }

            Matrix camMatrix = BlockManager.GetCameraTransform();
            FogOfWarManager.Prepare(camMatrix);
            Rectangle panelBounds = new(
                0,
                0,
                Core.Instance.GraphicsDevice.Viewport.Width,
                Core.Instance.GraphicsDevice.Viewport.Height);

            bool ready = GameBlockTerrainBackground.PrepareStartupVisibleTerrain(
                Core.Instance.GraphicsDevice,
                panelBounds,
                camMatrix);
            if (!ready)
            {
                DebugLogger.PrintWarning($"Startup terrain preparation did not complete: {GameBlockTerrainBackground.TerrainStartupReadinessSummary}");
            }

            return ready;
        }

        public static void Draw()
        {
            if (IsDrawing)
            {
                DebugLogger.PrintUI("[GameRenderer] [Draw] Re-entrant Draw call detected and blocked.");
                return;
            }

            IsDrawing = true;
            try
            {
                DrawInternal();
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"[GameRenderer] [Draw] Exception in Draw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                try { Core.Instance?.GraphicsDevice?.SetRenderTarget(null); } catch { }
                try { Core.Instance?.SpriteBatch?.End(); } catch { }
            }
            finally
            {
                IsDrawing = false;
            }
        }

        private static void DrawInternal()
        {
            bool usingDockedLayout = BlockManager.BeginDockedFrame(Core.Instance.GraphicsDevice);

            Matrix camMatrix = BlockManager.GetCameraTransform();
            FogOfWarManager.Prepare(camMatrix);
            Core.Instance.GraphicsDevice.Clear(Core.Instance.BackgroundColor);

            Rectangle panelBounds = new(
                0,
                0,
                Core.Instance.GraphicsDevice.Viewport.Width,
                Core.Instance.GraphicsDevice.Viewport.Height);
            GameBlockOceanBackground.Draw(Core.Instance.SpriteBatch, panelBounds, Core.GAMETIME, BlockManager.CameraZoom, camMatrix);
            GameBlockTerrainBackground.Draw(Core.Instance.SpriteBatch, panelBounds, camMatrix);
            if (GameBlockTerrainBackground.TerrainStartupVisibleTerrainReady)
            {
                GameInitializer.RevealStartupWindow();
            }

            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, camMatrix);

            // Draw all shapes once; ShapeManager already iterates registered objects.
            ShapeManager.Instance.DrawShapes(Core.Instance.SpriteBatch);
            DrawWorldGrid(Core.Instance.SpriteBatch, camMatrix);
            Core.Instance.SpriteBatch.End();

            // Draw clumps in their own world-layer pass so they always render over map geometry.
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, camMatrix);
            XPClumpManager.DrawCore(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            // Render debug direction line last to ensure visibility
            if (DebugModeHandler.DEBUGENABLED)
            {
                Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, camMatrix);
                foreach (GameObject gameObject in Core.Instance.GameObjects)
                {
                    DebugRenderer.DrawDebugCircle(Core.Instance.SpriteBatch, gameObject);
                }
                Core.Instance.SpriteBatch.End();
            }

            // Additive pass for hit-flash: adds brightness without washing to pure white
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, null, null, null, null, camMatrix);
            ShapeManager.Instance.DrawFlashes(Core.Instance.SpriteBatch);
            XPClumpManager.DrawGlow(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            // Health bar pass — drawn on top of game world, below damage numbers and UI
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, camMatrix);
            HealthBarManager.Draw(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            // Damage number pass — drawn on top of game world, below UI
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, camMatrix);
            DamageNumberManager.Draw(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            // Fog-of-war overlay: vision circles are cut out of a fully obscuring fog layer.
            FogOfWarManager.DrawOverlay(Core.Instance.SpriteBatch);

            if (usingDockedLayout)
            {
                BlockManager.CompleteDockedFrame(Core.Instance.SpriteBatch);
            }
        }

        public static bool WorldGridRequested =>
            ControlStateManager.ContainsSwitchState(ControlKeyMigrations.GridKey) &&
            ControlStateManager.GetSwitchState(ControlKeyMigrations.GridKey);

        public static bool WorldGridVisible => DebugModeHandler.DEBUGENABLED && WorldGridRequested;

        private static bool IsGridEnabled()
        {
            return WorldGridVisible;
        }

        private static void DrawWorldGrid(SpriteBatch spriteBatch, Matrix cameraMatrix)
        {
            if (spriteBatch == null || !IsGridEnabled())
            {
                return;
            }

            EnsureGridPixelTexture(spriteBatch.GraphicsDevice);
            if (_gridPixelTexture == null || _gridPixelTexture.IsDisposed)
            {
                return;
            }

            if (!TryGetVisibleWorldBounds(cameraMatrix, out float visibleMinX, out float visibleMaxX, out float visibleMinY, out float visibleMaxY))
            {
                return;
            }

            float minX = visibleMinX;
            float maxX = visibleMaxX;
            float minY = visibleMinY;
            float maxY = visibleMaxY;

            if (!GameBlockTerrainBackground.IsActive &&
                TryGetMapWorldBounds(out float mapMinX, out float mapMaxX, out float mapMinY, out float mapMaxY))
            {
                minX = MathF.Max(minX, mapMinX);
                maxX = MathF.Min(maxX, mapMaxX);
                minY = MathF.Max(minY, mapMinY);
                maxY = MathF.Min(maxY, mapMaxY);
            }

            if (maxX <= minX || maxY <= minY)
            {
                return;
            }

            int startX = RoundUpToStep((int)MathF.Floor(minX), GridStepWorldUnits);
            int endX = (int)MathF.Ceiling(maxX);
            int startY = RoundUpToStep((int)MathF.Floor(minY), GridStepWorldUnits);
            int endY = (int)MathF.Ceiling(maxY);

            ClampGridLineRange(ref startX, ref endX);
            ClampGridLineRange(ref startY, ref endY);

            if (endX <= startX || endY <= startY)
            {
                return;
            }

            int gridHeight = Math.Max(1, endY - startY + 1);
            int gridWidth = Math.Max(1, endX - startX + 1);

            for (int x = startX; x <= endX; x += GridStepWorldUnits)
            {
                bool isMajor = x % GridCoordinateStepWorldUnits == 0;
                Color lineColor = isMajor ? GridMajorLineColor : GridMinorLineColor;
                spriteBatch.Draw(_gridPixelTexture, new Rectangle(x, startY, 1, gridHeight), lineColor);
            }

            for (int y = startY; y <= endY; y += GridStepWorldUnits)
            {
                bool isMajor = y % GridCoordinateStepWorldUnits == 0;
                Color lineColor = isMajor ? GridMajorLineColor : GridMinorLineColor;
                spriteBatch.Draw(_gridPixelTexture, new Rectangle(startX, y, gridWidth, 1), lineColor);
            }

            DrawGridCoordinateOverlay(spriteBatch, startX, endX, startY, endY);
        }

        private static void DrawGridCoordinateOverlay(SpriteBatch spriteBatch, int startX, int endX, int startY, int endY)
        {
            int labelStartX = RoundUpToStep(startX, GridCoordinateStepWorldUnits);
            int labelStartY = RoundUpToStep(startY, GridCoordinateStepWorldUnits);
            if (labelStartX > endX || labelStartY > endY)
            {
                return;
            }

            int markerCountX = ((endX - labelStartX) / GridCoordinateStepWorldUnits) + 1;
            int markerCountY = ((endY - labelStartY) / GridCoordinateStepWorldUnits) + 1;
            long markerCount = (long)markerCountX * markerCountY;
            if (markerCount <= 0)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Regular);
            bool canDrawText = font.IsAvailable && font.Font != null;
            float fontScale = canDrawText ? MathF.Max(0.18f, font.Scale * 0.4f) : 0f;
            int labelStride = GridCoordinateTextStride;
            if (canDrawText)
            {
                while (markerCount / ((long)labelStride * labelStride) > MaxGridCoordinateLabels)
                {
                    labelStride++;
                }
            }

            int xIndex = 0;
            for (int x = labelStartX; x <= endX; x += GridCoordinateStepWorldUnits, xIndex++)
            {
                int yIndex = 0;
                for (int y = labelStartY; y <= endY; y += GridCoordinateStepWorldUnits, yIndex++)
                {
                    if (!canDrawText || (xIndex % labelStride != 0) || (yIndex % labelStride != 0))
                    {
                        continue;
                    }

                    int xCentifoot = (int)MathF.Round(CentifootUnits.WorldToCentifoot(x));
                    int yCentifoot = (int)MathF.Round(CentifootUnits.WorldToCentifoot(y));
                    string label = $"{xCentifoot},{yCentifoot}";
                    Vector2 labelPosition = new(x + 0.8f, y + 0.8f);
                    spriteBatch.DrawString(font.Font, label, labelPosition + new Vector2(0.6f, 0.6f), GridCoordinateTextShadowColor, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawString(font.Font, label, labelPosition, GridCoordinateTextColor, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0f);
                }
            }
        }

        private static void EnsureGridPixelTexture(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_gridPixelTexture != null && !_gridPixelTexture.IsDisposed)
            {
                return;
            }

            _gridPixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _gridPixelTexture.SetData([Color.White]);
        }

        public static bool TryGetVisibleWorldBounds(Matrix cameraMatrix, out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = maxX = minY = maxY = 0f;

            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return false;
            }

            Matrix inverse = Matrix.Invert(cameraMatrix);
            Viewport viewport = graphicsDevice.Viewport;
            float right = viewport.X + viewport.Width;
            float bottom = viewport.Y + viewport.Height;

            Vector2 topLeft = Vector2.Transform(new Vector2(viewport.X, viewport.Y), inverse);
            Vector2 topRight = Vector2.Transform(new Vector2(right, viewport.Y), inverse);
            Vector2 bottomLeft = Vector2.Transform(new Vector2(viewport.X, bottom), inverse);
            Vector2 bottomRight = Vector2.Transform(new Vector2(right, bottom), inverse);

            minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
            maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
            minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
            maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

            return !(float.IsNaN(minX) || float.IsNaN(maxX) || float.IsNaN(minY) || float.IsNaN(maxY));
        }

        private static bool TryGetMapWorldBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;

            List<GameObject> staticObjects = Core.Instance?.StaticObjects;
            if (staticObjects == null || staticObjects.Count == 0)
            {
                return false;
            }

            bool found = false;
            foreach (GameObject gameObject in staticObjects)
            {
                if (gameObject == null || gameObject.IsPrototype)
                {
                    continue;
                }

                float radius = MathF.Max(gameObject.BoundingRadius, 1f);
                minX = MathF.Min(minX, gameObject.Position.X - radius);
                maxX = MathF.Max(maxX, gameObject.Position.X + radius);
                minY = MathF.Min(minY, gameObject.Position.Y - radius);
                maxY = MathF.Max(maxY, gameObject.Position.Y + radius);
                found = true;
            }

            if (!found)
            {
                return false;
            }

            const float margin = 16f;
            minX -= margin;
            maxX += margin;
            minY -= margin;
            maxY += margin;
            return true;
        }

        private static int RoundUpToStep(int value, int step)
        {
            if (step <= 0)
            {
                return value;
            }

            int remainder = value % step;
            if (remainder == 0)
            {
                return value;
            }

            if (value >= 0)
            {
                return value + (step - remainder);
            }

            return value - remainder;
        }

        private static void ClampGridLineRange(ref int minValue, ref int maxValue)
        {
            int span = maxValue - minValue;
            if (span <= MaxGridLinesPerAxis)
            {
                return;
            }

            int center = minValue + (span / 2);
            int halfRange = MaxGridLinesPerAxis / 2;
            minValue = center - halfRange;
            maxValue = minValue + MaxGridLinesPerAxis;
        }
    }
}
