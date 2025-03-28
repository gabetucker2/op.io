using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace op.io
{
    public class Core : Game
    {
        public GraphicsDeviceManager Graphics { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Color BackgroundColor { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }
        public List<GameObject> GameObjects { get; set; }
        public List<GameObject> StaticObjects { get; set; }
        public ShapeManager ShapesManager { get; set; }
        public PhysicsManager PhysicsManager { get; private set; }

        public Core()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            DebugManager.InitializeConsoleIfEnabled();

            PhysicsManager = new PhysicsManager();

            GameInitializer.Initialize(this);
            ShapesManager = new ShapeManager(ViewportWidth, ViewportHeight);
            ObjectManager.InitializeObjects(this);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            ShapesManager.LoadContent(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            GameUpdater.Update(this, gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GameRenderer.Draw(this, gameTime);
            base.Draw(gameTime);
        }
    }
}
