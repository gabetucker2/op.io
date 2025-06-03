using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class GameRenderer
    {
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

            DebugLogger.Print("GameRenderer: Graphics and GameObjects loaded successfully.");
        }

        public static void Draw()
        {
            Core.Instance.GraphicsDevice.Clear(Core.Instance.BackgroundColor);
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            foreach (GameObject gameObject in Core.Instance.GameObjects)
            {
                ShapeManager.Instance.DrawShapes(Core.Instance.SpriteBatch);
            }

            // Render debug direction line last to ensure visibility
            if (DebugModeHandler.DEBUGENABLED)
            {
                foreach (GameObject gameObject in Core.Instance.GameObjects)
                {
                    DebugRenderer.DrawDebugCircle(Core.Instance.SpriteBatch, gameObject);
                }
            }

            Core.Instance.SpriteBatch.End();
        }
    }
}
