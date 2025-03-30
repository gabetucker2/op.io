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

            DebugVisualizer.Initialize(Core.Instance.GraphicsDevice);

            if (Core.Instance.GameObjects == null || Core.Instance.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects to load content for.");
                return;
            }

            foreach (var obj in Core.Instance.GameObjects)
            {
                obj.LoadContent(Core.Instance.GraphicsDevice);
            }

            DebugLogger.Print("GameRenderer: Graphics and GameObjects loaded successfully.");
        }

        public static void Draw()
        {
            Core.Instance.GraphicsDevice.Clear(Core.Instance.BackgroundColor);
            Core.Instance.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            foreach (var gameObject in Core.Instance.GameObjects)
            {
                gameObject.Draw(Core.Instance.SpriteBatch);
            }

            // Render debug direction line last to ensure visibility
            if (DebugModeHandler.IsDebugEnabled())
            {
                foreach (var gameObject in Core.Instance.GameObjects)
                {
                    DebugVisualizer.DrawDebugCircle(Core.Instance.SpriteBatch, gameObject);
                }
            }

            Core.Instance.SpriteBatch.End();
        }
    }
}
