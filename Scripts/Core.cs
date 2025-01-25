using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

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

            try
            {
                // Viewport configuration
                var viewport = config.RootElement.GetProperty("Viewport");
                _viewportWidth = viewport.GetProperty("Width").GetInt32();
                _viewportHeight = viewport.GetProperty("Height").GetInt32();
                if (_viewportWidth <= 0 || _viewportHeight <= 0)
                    throw new ArgumentException("Viewport dimensions must be positive values.");
                _backgroundColor = BaseFunctions.GetColor(viewport.GetProperty("BackgroundColor"), Color.Black);

                _graphics.PreferredBackBufferWidth = _viewportWidth;
                _graphics.PreferredBackBufferHeight = _viewportHeight;
                _graphics.ApplyChanges();

                // Debugging configuration
                _debugEnabled = config.RootElement.TryGetProperty("Debugging", out var debugSettings) &&
                                debugSettings.GetProperty("Enabled").GetBoolean();

                if (_debugEnabled)
                    DebugVisualizer.Initialize(GraphicsDevice, debugSettings);

                // ShapesManager configuration
                _shapesManager = new ShapesManager();
                if (config.RootElement.TryGetProperty("Farms", out var farmsArray))
                {
                    foreach (var farm in farmsArray.EnumerateArray())
                    {
                        string shapeType = farm.GetProperty("Type").GetString() ?? "Polygon";
                        int count = farm.GetProperty("Count").GetInt32();
                        if (count <= 0)
                            throw new ArgumentException("Shape count must be greater than 0.");

                        int width = 0, height = 0, sides = 0;
                        int radius = 0;

                        // Configure shape-specific properties
                        switch (shapeType)
                        {
                            case "Circle":
                                if (!farm.TryGetProperty("Radius", out var radiusProperty))
                                    throw new ArgumentException("Circle must have a valid radius specified.");
                                radius = radiusProperty.GetInt32();
                                if (radius <= 0)
                                    throw new ArgumentException("Radius must be greater than 0.");
                                width = height = radius * 2;
                                break;

                            case "Polygon":
                                if (!farm.TryGetProperty("NumberOfSides", out var sidesProperty))
                                    throw new ArgumentException("Polygon must have a valid number of sides specified.");
                                sides = sidesProperty.GetInt32();
                                if (sides < 3)
                                    throw new ArgumentException("Polygon must have at least 3 sides.");
                                if (!farm.TryGetProperty("Size", out var sizeProperty))
                                    throw new ArgumentException("Polygon must have a valid size specified.");
                                width = height = sizeProperty.GetInt32();
                                break;

                            case "Rectangle":
                                if (!farm.TryGetProperty("Width", out var widthProperty) || !farm.TryGetProperty("Height", out var heightProperty))
                                    throw new ArgumentException("Rectangle must have valid width and height specified.");
                                width = widthProperty.GetInt32();
                                height = heightProperty.GetInt32();
                                break;

                            default:
                                throw new ArgumentException($"Unsupported shape type: {shapeType}");
                        }

                        // Common properties
                        Color fillColor = BaseFunctions.GetColor(farm.GetProperty("Color"), Color.White);
                        Color outlineColor = BaseFunctions.GetColor(farm.GetProperty("OutlineColor"), Color.Black);
                        int outlineWidth = farm.TryGetProperty("OutlineWidth", out var outlineWidthProperty)
                            ? outlineWidthProperty.GetInt32()
                            : 0;
                        int weight = farm.GetProperty("Weight").GetInt32();

                        for (int i = 0; i < count; i++)
                        {
                            int x = Random.Shared.Next(0, _viewportWidth - width);
                            int y = Random.Shared.Next(0, _viewportHeight - height);

                            _shapesManager.AddShape(
                                new Vector2(x, y),
                                shapeType,
                                width,
                                height,
                                sides,
                                fillColor,
                                outlineColor,
                                outlineWidth,
                                true,   // Enable collision
                                false   // Enable physics
                            );
                        }
                    }
                }

                // Player configuration
                if (config.RootElement.TryGetProperty("Player", out var playerConfig))
                {
                    _player = new Player(
                        playerConfig.GetProperty("X").GetSingle(),
                        playerConfig.GetProperty("Y").GetSingle(),
                        playerConfig.GetProperty("Radius").GetInt32(),
                        playerConfig.GetProperty("Speed").GetSingle(),
                        BaseFunctions.GetColor(playerConfig.GetProperty("Color"), Color.Cyan),
                        playerConfig.GetProperty("Weight").GetInt32(),
                        BaseFunctions.GetColor(playerConfig.GetProperty("OutlineColor"), Color.DarkBlue),
                        playerConfig.GetProperty("OutlineWidth").GetInt32()
                    );
                }
                else
                {
                    throw new ArgumentException("Player configuration is missing or invalid.");
                }

                // Initialize PhysicsManager
                _physicsManager = new PhysicsManager();
                _collisionDestroyShapes = config.RootElement.TryGetProperty("CollisionDestroyShapes", out var destroyShapesProperty) &&
                                          destroyShapesProperty.GetBoolean();
            }
            catch (KeyNotFoundException ex)
            {
                throw new InvalidOperationException($"A required configuration key is missing: {ex.Message}", ex);
            }

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
