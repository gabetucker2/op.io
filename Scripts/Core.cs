using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class Core : Game
    {
        // Set to true if you are debugging the database debug mode flag, meaning the console does not currently reliably open
        public static bool ForceDebugMode { get; private set; } = false;

        public GraphicsDeviceManager Graphics { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Color BackgroundColor { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }

        public bool VSyncEnabled { get; set; }
        public bool UseFixedTimeStep { get; set; }
        public int TargetFrameRate { get; set; }
        public WindowMode WindowMode { get; set; } = WindowMode.BorderlessFullscreen;

        public List<GameObject> GameObjects { get; set; } = new List<GameObject>();
        public PhysicsManager PhysicsManager { get; set; } = new PhysicsManager();

        public Core()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            VSyncEnabled = false;
            UseFixedTimeStep = false;
            TargetFrameRate = 240;

            Graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 240.0);
        }

        protected override void Initialize()
        {
            DatabaseInitializer.InitializeDatabase();
            DebugManager.InitializeConsoleIfEnabled();

            GameInitializer.Initialize(this);
            ApplyWindowMode();

            Graphics.SynchronizeWithVerticalRetrace = VSyncEnabled;
            IsFixedTimeStep = UseFixedTimeStep;

            int safeFps = Math.Clamp(TargetFrameRate, 1, 1000);
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            Graphics.ApplyChanges();

            ObjectManager.InitializeObjects(this);

            base.Initialize();
        }

        private void ApplyWindowMode()
        {
            switch (WindowMode)
            {
                case WindowMode.BorderedWindowed:
                    Graphics.IsFullScreen = false;
                    Window.IsBorderless = false;
                    break;

                case WindowMode.BorderlessWindowed:
                    Graphics.IsFullScreen = false;
                    Window.IsBorderless = true;
                    break;

                case WindowMode.LegacyFullscreen:
                    Graphics.IsFullScreen = true;
                    Window.IsBorderless = false;
                    break;

                case WindowMode.BorderlessFullscreen:
                    Graphics.IsFullScreen = false;
                    Window.IsBorderless = true;
                    var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    Graphics.PreferredBackBufferWidth = display.Width;
                    Graphics.PreferredBackBufferHeight = display.Height;
                    break;
            }
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