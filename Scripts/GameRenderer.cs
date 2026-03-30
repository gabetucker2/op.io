using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class GameRenderer
    {
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

            Core.Instance.GraphicsDevice.Clear(Core.Instance.BackgroundColor);
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw all shapes once; ShapeManager already iterates registered objects.
            ShapeManager.Instance.DrawShapes(Core.Instance.SpriteBatch);

            // Render debug direction line last to ensure visibility
            if (DebugModeHandler.DEBUGENABLED)
            {
                foreach (GameObject gameObject in Core.Instance.GameObjects)
                {
                    DebugRenderer.DrawDebugCircle(Core.Instance.SpriteBatch, gameObject);
                }
            }

            Core.Instance.SpriteBatch.End();

            // Additive pass for hit-flash: adds brightness without washing to pure white
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
            ShapeManager.Instance.DrawFlashes(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            // Health bar pass — drawn on top of game world, below damage numbers and UI
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            HealthBarManager.Draw(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            // Damage number pass — drawn on top of game world, below UI
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            DamageNumberManager.Draw(Core.Instance.SpriteBatch);
            Core.Instance.SpriteBatch.End();

            if (usingDockedLayout)
            {
                BlockManager.CompleteDockedFrame(Core.Instance.SpriteBatch);
            }
        }
    }
}
