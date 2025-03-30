using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class Core : Game
    {
        public static bool ForceDebugMode { get; private set; } = false;

        public GraphicsDeviceManager Graphics { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Color BackgroundColor { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }

        public bool VSyncEnabled { get; set; }
        public bool UseFixedTimeStep { get; set; }
        public int TargetFrameRate { get; set; }
        public WindowMode WindowMode { get; set; }

        public List<GameObject> GameObjects { get; set; } = new List<GameObject>();
        public List<GameObject> StaticObjects { get; set; } = new List<GameObject>();
        public PhysicsManager PhysicsManager { get; private set; }

        public Core()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            PhysicsManager = new PhysicsManager();

            VSyncEnabled = false;
            UseFixedTimeStep = false;
            TargetFrameRate = 240;
            WindowMode = WindowMode.BorderedWindowed;

            Graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 240.0);
        }

        protected override void Initialize() // Necessary for MonoGame's backend to properly init
        {
            GameInitializer.Initialize(this);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            DebugVisualizer.Initialize(GraphicsDevice);

            foreach (var obj in GameObjects)
                obj.LoadContent(GraphicsDevice);
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
