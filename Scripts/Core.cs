using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
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

        public bool VSyncEnabled { get; set; }
        public bool UseFixedTimeStep { get; set; }
        public int TargetFrameRate { get; set; }
        public WindowMode WindowMode { get; set; } = WindowMode.BorderlessFullscreen;

        public List<GameObject> GameObjects { get; set; }
        public List<GameObject> StaticObjects { get; set; }
        public ShapeManager ShapesManager { get; set; }
        public PhysicsManager PhysicsManager { get; private set; }

        public Core()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Safe defaults until loaded from DB
            VSyncEnabled = false;
            UseFixedTimeStep = false;
            TargetFrameRate = 240;

            Graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 240.0);
        }

        protected override void Initialize()
        {
            DebugManager.InitializeConsoleIfEnabled();

            PhysicsManager = new PhysicsManager();

            GameInitializer.Initialize(this); // Loads Viewport, VSync, etc.

            ApplyWindowMode(); // Apply selected window mode

            Graphics.SynchronizeWithVerticalRetrace = VSyncEnabled;
            IsFixedTimeStep = UseFixedTimeStep;

            int safeFps = Math.Clamp(TargetFrameRate, 1, 1000);
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            Graphics.ApplyChanges();

            ShapesManager = new ShapeManager(ViewportWidth, ViewportHeight);
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
            ShapesManager.LoadContent(GraphicsDevice);

            // Initialize DebugVisualizer here with GraphicsDevice
            DebugVisualizer.Initialize(GraphicsDevice);

            // Print to confirm initialization
            DebugLogger.PrintDebug("DebugVisualizer successfully initialized during LoadContent.");
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
