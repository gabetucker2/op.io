using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace op.io
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Color _backgroundColor;
        private int _viewportWidth;
        private int _viewportHeight;
        private Player _player;
        private ShapesManager _shapesManager;
        private PhysicsManager _physicsManager;
        private bool _collisionDestroyShapes;
        private bool _debugEnabled;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this)
                ?? throw new ArgumentNullException(nameof(_graphics), "GraphicsDeviceManager initialization failed.");
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            var config = BaseFunctions.Config();
            if (config == null)
                throw new ArgumentException("Configuration cannot be null or invalid.", nameof(config));

            // Set viewport dimensions and background color
            _viewportWidth = config.RootElement.GetProperty("Viewport").GetProperty("Width").GetInt32();
            _viewportHeight = config.RootElement.GetProperty("Viewport").GetProperty("Height").GetInt32();

            if (_viewportWidth <= 0 || _viewportHeight <= 0)
                throw new ArgumentException("Viewport dimensions must be positive values.");

            _backgroundColor = BaseFunctions.GetColor(config.RootElement.GetProperty("Viewport").GetProperty("BackgroundColor"), Color.HotPink);
            if (_backgroundColor == Color.HotPink)
                throw new ArgumentNullException(nameof(_backgroundColor), "Background color cannot be null.");

            _graphics.PreferredBackBufferWidth = _viewportWidth;
            _graphics.PreferredBackBufferHeight = _viewportHeight;
            _graphics.ApplyChanges();

            // Enable debugging if configured
            _debugEnabled = config.RootElement.GetProperty("Debugging").GetProperty("Enabled").GetBoolean();
            if (_debugEnabled)
            {
                if (!config.RootElement.TryGetProperty("Debugging", out var debugSettings))
                    throw new ArgumentException("Debugging configuration is missing.");
                DebugVisualizer.Initialize(GraphicsDevice, debugSettings);
            }

            // Initialize ShapesManager
            _shapesManager = new ShapesManager();
            var farms = config.RootElement.GetProperty("Farms").EnumerateArray();
            foreach (var farm in farms)
            {
                string shapeType = farm.GetProperty("Type").GetString() ?? "Polygon";
                int sides = farm.GetProperty("NumberOfSides").GetInt32();
                Color fillColor = BaseFunctions.GetColor(farm.GetProperty("Color"), Color.White);
                int count = farm.GetProperty("Count").GetInt32();
                int size = farm.GetProperty("Size").GetInt32();
                int weight = farm.GetProperty("Weight").GetInt32();
                Color outlineColor = BaseFunctions.GetColor(farm.GetProperty("OutlineColor"), Color.Black);
                int outlineWidth = farm.GetProperty("OutlineWidth").GetInt32();

                if (count <= 0)
                    throw new ArgumentException("Farm count must be greater than 0.");

                if (size <= 0)
                    throw new ArgumentException("Farm size must be greater than 0.");

                if (sides < 3)
                    throw new ArgumentException("Farm shapes must have at least 3 sides.");

                for (int i = 0; i < count; i++)
                {
                    int x = Random.Shared.Next(0, _viewportWidth - size);
                    int y = Random.Shared.Next(0, _viewportHeight - size);
                    _shapesManager.AddShape(new Vector2(x, y), shapeType, size, sides, fillColor, outlineColor, outlineWidth, true, false);
                }
            }

            // Initialize player
            _player = new Player(
                config.RootElement.GetProperty("Player").GetProperty("X").GetSingle(),
                config.RootElement.GetProperty("Player").GetProperty("Y").GetSingle(),
                config.RootElement.GetProperty("Player").GetProperty("Radius").GetInt32(),
                config.RootElement.GetProperty("Player").GetProperty("Speed").GetSingle(),
                BaseFunctions.GetColor(config.RootElement.GetProperty("Player").GetProperty("Color"), Color.Cyan),
                config.RootElement.GetProperty("Player").GetProperty("Weight").GetInt32(),
                BaseFunctions.GetColor(config.RootElement.GetProperty("Player").GetProperty("OutlineColor"), Color.DarkBlue),
                config.RootElement.GetProperty("Player").GetProperty("OutlineWidth").GetInt32()
            );

            _physicsManager = new PhysicsManager();
            _collisionDestroyShapes = config.RootElement.GetProperty("CollisionDestroyShapes").GetBoolean();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            if (GraphicsDevice == null)
                throw new ArgumentNullException(nameof(GraphicsDevice), "GraphicsDevice cannot be null during content loading.");

            _spriteBatch = new SpriteBatch(GraphicsDevice)
                ?? throw new ArgumentNullException(nameof(_spriteBatch), "SpriteBatch initialization failed.");

            _player.LoadContent(GraphicsDevice);
            _shapesManager.LoadContent(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (gameTime == null)
                throw new ArgumentNullException(nameof(gameTime), "GameTime cannot be null.");

            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (deltaTime <= 0)
                deltaTime = 0.0001f;

            _player.Update(deltaTime);
            _shapesManager.Update(deltaTime);

            // Resolve collisions
            if (_physicsManager == null)
                throw new InvalidOperationException("PhysicsManager is not initialized.");

            _physicsManager.ResolveCollisions(_shapesManager.GetShapes(), _player, _collisionDestroyShapes);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (gameTime == null)
                throw new ArgumentNullException(nameof(gameTime), "GameTime cannot be null.");

            GraphicsDevice.Clear(_backgroundColor);

            if (_spriteBatch == null)
                throw new InvalidOperationException("SpriteBatch is not initialized.");

            // Configure SpriteBatch for transparency and default sorting
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw the player and shapes
            _player.Draw(_spriteBatch, _debugEnabled);
            _shapesManager.Draw(_spriteBatch, _debugEnabled);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
