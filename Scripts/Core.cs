using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
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
        private List<FarmShape> _farmShapes = new List<FarmShape>();
        private PhysicsManager _physicsManager = new PhysicsManager();
        private bool _collisionDestroyShapes = false;
        private bool _debugEnabled;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            var config = BaseFunctions.Config();

            // Set viewport dimensions and background color
            _viewportWidth = config.RootElement.GetProperty("Viewport").GetProperty("Width").GetInt32();
            _viewportHeight = config.RootElement.GetProperty("Viewport").GetProperty("Height").GetInt32();
            _backgroundColor = BaseFunctions.GetColor(config.RootElement.GetProperty("Viewport").GetProperty("BackgroundColor"), Color.Black);

            _graphics.PreferredBackBufferWidth = _viewportWidth;
            _graphics.PreferredBackBufferHeight = _viewportHeight;
            _graphics.ApplyChanges();

            // Enable debugging if configured
            _debugEnabled = config.RootElement.GetProperty("Debugging").GetProperty("Enabled").GetBoolean();
            if (_debugEnabled)
            {
                var debugSettings = config.RootElement.GetProperty("Debugging");
                DebugVisualizer.Initialize(GraphicsDevice, debugSettings);
            }

            // Populate _farmShapes dynamically
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

                for (int i = 0; i < count; i++)
                {
                    int x = Random.Shared.Next(0, _viewportWidth - size);
                    int y = Random.Shared.Next(0, _viewportHeight - size);
                    _farmShapes.Add(new FarmShape(new Vector2(x, y), size, shapeType, sides, fillColor, weight, outlineColor, outlineWidth));
                }
            }

            // Initialize player
            _player = new Player(
                config.RootElement.GetProperty("Player").GetProperty("X").GetSingle(),
                config.RootElement.GetProperty("Player").GetProperty("Y").GetSingle(),
                config.RootElement.GetProperty("Player").GetProperty("Radius").GetInt32(),
                config.RootElement.GetProperty("Player").GetProperty("Speed").GetSingle(),
                BaseFunctions.GetColor(config.RootElement.GetProperty("Player").GetProperty("Color"), Color.Cyan),
                config.RootElement.GetProperty("Player").GetProperty("Weight").GetInt32()
            );

            _collisionDestroyShapes = config.RootElement.GetProperty("CollisionDestroyShapes").GetBoolean();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _player.LoadContent(GraphicsDevice);
            foreach (var farmShape in _farmShapes)
            {
                farmShape.LoadContent(GraphicsDevice);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _player.Update(deltaTime);

            // Resolve collisions
            _physicsManager.ResolveCollisions(_farmShapes, _player, _collisionDestroyShapes);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColor);

            _spriteBatch.Begin();

            _player.Draw(_spriteBatch, _debugEnabled);
            foreach (var farmShape in _farmShapes)
            {
                farmShape.Draw(_spriteBatch, _debugEnabled);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
