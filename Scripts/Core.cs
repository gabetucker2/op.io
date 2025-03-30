using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace op.io
{
    public class Core : Game
    {
        public static bool ForceDebugMode { get; set; } = true;

        public static Core Instance { get; set; }

        public GraphicsDeviceManager Graphics { get; set; }
        public SpriteBatch SpriteBatch { get; set; }
        public Color BackgroundColor { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }

        public bool VSyncEnabled { get; set; }
        public bool UseFixedTimeStep { get; set; }
        public int TargetFrameRate { get; set; }
        public WindowMode WindowMode { get; set; }

        public List<GameObject> GameObjects { get; set; } = [];
        public List<GameObject> StaticObjects { get; set; } = [];
        public PhysicsManager PhysicsManager { get; set; }

        public static float gameTime { get; set; } = 0f;
        public static float deltaTime { get; set; } = 0.00001f;

        public Core()
        {
            Instance = this;
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            GameInitializer.Initialize();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            GameRenderer.LoadGraphics();
        }

        protected override void Update(GameTime gameTime)
        {
            GameUpdater.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GameRenderer.Draw();
            base.Draw(gameTime);
        }
    }
}
