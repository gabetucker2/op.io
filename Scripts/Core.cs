using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace op.io
{
    public class Core : Game
    {
        // Manual settings

        // do not use `ForceDebugMode` unless `DebugMode` in `InitDB_StartData_Settlings.sql` isn't working.
        public static bool ForceDebugMode { get; set; } = false;
        public static bool RestartDB { get; set; } = true;

        public static Color TransparentWindowColor = new(255, 105, 180);
        public static Color DefaultColor = new(255, 105, 179);

        // Auto settings
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

        private Agent _player { get; set; }
        public Agent Player
        {
            get
            {
                if (_player == null)
                {
                    DebugLogger.PrintError("Player is null. Ensure that the player is initialized before accessing it.");
                    return null;
                }
                else
                {
                    return _player;
                }
            }
            set
            {
                if (value == null)
                {
                    DebugLogger.PrintWarning("Setting Player to null.");
                }
                _player = value;
            }
        }

        public static float GAMETIME { get; set; } = 0f;

        private static float _deltaTime = 0f;
        public static float DELTATIME {
            get
            {
                return _deltaTime;
            }
            set
            {
                if (value < 0.0001f)
                {
                    _deltaTime = 0.0001f;
                }
                else
                {
                    _deltaTime = value;
                }
            }
        }

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

        protected override void Draw(GameTime gameTime)
        {
            GameRenderer.Draw();
            base.Draw(gameTime);
        }

        protected override void Update(GameTime gameTime)
        {
            GameUpdater.Update(gameTime);
            base.Update(gameTime);
        }
    }
}
