using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class GameRenderer
    {
        public static void LoadGraphics()
        {
            if (Core.InstanceCore == null)
            {
                DebugLogger.PrintError("LoadGraphics failed: Core.InstanceCore is null.");
                return;
            }

            if (Core.InstanceCore.GraphicsDevice == null)
            {
                DebugLogger.PrintError("LoadGraphics failed: GraphicsDevice is null.");
                return;
            }

            if (Core.InstanceCore.SpriteBatch == null)
            {
                Core.InstanceCore.SpriteBatch = new SpriteBatch(Core.InstanceCore.GraphicsDevice);
                DebugLogger.Print("SpriteBatch initialized successfully.");
            }

            DebugVisualizer.Initialize(Core.InstanceCore.GraphicsDevice);

            if (Core.InstanceCore.GameObjects == null || Core.InstanceCore.GameObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No GameObjects to load content for.");
                return;
            }

            foreach (var obj in Core.InstanceCore.GameObjects)
            {
                obj.LoadContent(Core.InstanceCore.GraphicsDevice);
            }

            DebugLogger.Print("GameRenderer: Graphics and GameObjects loaded successfully.");
        }

        public static void Draw()
        {
            Core.InstanceCore.GraphicsDevice.Clear(Core.InstanceCore.BackgroundColor);
            Core.InstanceCore.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            foreach (var gameObject in Core.InstanceCore.GameObjects)
            {
                gameObject.Draw(Core.InstanceCore.SpriteBatch);
            }

            // Render debug direction line last to ensure visibility
            if (DebugModeHandler.IsDebugEnabled())
            {
                foreach (var gameObject in Core.InstanceCore.GameObjects)
                {
                    DebugVisualizer.DrawDebugCircle(Core.InstanceCore.SpriteBatch, gameObject);
                }
            }

            Core.InstanceCore.SpriteBatch.End();
        }
    }
}
