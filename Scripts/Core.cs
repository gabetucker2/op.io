using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

namespace op.io
{
    public class Core : Game
    {
        public GraphicsDeviceManager Graphics { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Microsoft.Xna.Framework.Color BackgroundColor { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }

        public bool VSyncEnabled { get; set; }
        public bool UseFixedTimeStep { get; set; }
        public int TargetFrameRate { get; set; }
        public WindowMode WindowMode { get; set; } = WindowMode.BorderlessFullscreen;

        public List<GameObject> GameObjects { get; set; } = new List<GameObject>();
        public List<GameObject> StaticObjects { get; set; } = new List<GameObject>();
        public ShapeManager ShapesManager { get; set; }
        public PhysicsManager PhysicsManager { get; private set; }

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
            PhysicsManager = new PhysicsManager();
            GameInitializer.Initialize(this);
            ApplyWindowMode();

            Graphics.SynchronizeWithVerticalRetrace = VSyncEnabled;
            IsFixedTimeStep = UseFixedTimeStep;

            int safeFps = Math.Clamp(TargetFrameRate, 1, 1000);
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / safeFps);

            Graphics.ApplyChanges();

            ShapesManager = new ShapeManager(ViewportWidth, ViewportHeight);
            ObjectManager.InitializeObjects(this);

            base.Initialize();

            ApplyWindowIcon();
        }

        private void ApplyWindowIcon()
        {
#if WINDOWS
    string iconPathIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icon.ico");
    string iconPathBmp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icon.bmp");

    if (File.Exists(iconPathIco))
    {
        using (var icon = new Icon(iconPathIco))
        {
            IntPtr hIcon = icon.Handle;
            SendMessage(Window.Handle, WM_SETICON, ICON_SMALL, hIcon);
            SendMessage(Window.Handle, WM_SETICON, ICON_BIG, hIcon);
            Console.WriteLine("Icon.ico applied successfully.");
        }
    }
    else if (File.Exists(iconPathBmp))
    {
        using (var bmp = new Bitmap(iconPathBmp))
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            SendMessage(Window.Handle, WM_SETICON, ICON_SMALL, hBitmap);
            SendMessage(Window.Handle, WM_SETICON, ICON_BIG, hBitmap);
            Console.WriteLine("Icon.bmp applied successfully.");
        }
    }
    else
    {
        Console.WriteLine("No Icon.ico or Icon.bmp found in root directory.");
    }
#endif
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        private const int WM_SETICON = 0x80;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

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
            DebugVisualizer.Initialize(GraphicsDevice);
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
