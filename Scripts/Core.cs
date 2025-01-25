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
        private List<StaticObject> _staticObjects;
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
            {
                throw new ArgumentException("Configuration cannot be null or invalid.", nameof(config));
            }

            try
            {
                // Viewport configuration
                if (!config.RootElement.TryGetProperty("Viewport", out var viewport))
                    throw new KeyNotFoundException("Missing required 'Viewport' configuration.");

                _viewportWidth = viewport.GetProperty("Width").GetInt32();
                _viewportHeight = viewport.GetProperty("Height").GetInt32();

                if (_viewportWidth <= 0 || _viewportHeight <= 0)
                    throw new ArgumentException("Viewport dimensions must be positive integers.", nameof(viewport));

                _backgroundColor = BaseFunctions.GetColor(viewport.GetProperty("BackgroundColor"), Color.Black);

                _graphics.PreferredBackBufferWidth = _viewportWidth;
                _graphics.PreferredBackBufferHeight = _viewportHeight;
                _graphics.ApplyChanges();

                // Debugging configuration
                _debugEnabled = config.RootElement.TryGetProperty("Debugging", out var debugSettings) &&
                                debugSettings.GetProperty("Enabled").GetBoolean();

                if (_debugEnabled)
                {
                    DebugVisualizer.Initialize(GraphicsDevice, debugSettings);
                }

                // ShapeManager configuration
                _shapesManager = new ShapesManager();

                // Farms configuration
                if (config.RootElement.TryGetProperty("Farms", out var farmsArray))
                {
                    foreach (var farm in farmsArray.EnumerateArray())
                    {
                        if (!farm.TryGetProperty("Type", out var typeElement) || string.IsNullOrEmpty(typeElement.GetString()))
                        {
                            throw new KeyNotFoundException("A 'Type' key is missing or invalid in the Farms configuration.");
                        }

                        string shapeType = typeElement.GetString();
                        int width = 0, height = 0, sides = 0;

                        if (shapeType == "Circle")
                        {
                            if (!farm.TryGetProperty("Radius", out var radiusElement))
                                throw new KeyNotFoundException("A 'Radius' key is missing for a Circle in the Farms configuration.");
                            int radius = radiusElement.GetInt32();
                            width = height = radius * 2;
                        }
                        else if (shapeType == "Rectangle")
                        {
                            if (!farm.TryGetProperty("Width", out var widthElement) || !farm.TryGetProperty("Height", out var heightElement))
                                throw new KeyNotFoundException("A 'Width' or 'Height' key is missing for a Rectangle in the Farms configuration.");
                            width = widthElement.GetInt32();
                            height = heightElement.GetInt32();
                        }
                        else if (shapeType == "Polygon")
                        {
                            if (!farm.TryGetProperty("NumberOfSides", out var sidesElement))
                                throw new KeyNotFoundException("A 'NumberOfSides' key is missing for a Polygon in the Farms configuration.");
                            sides = sidesElement.GetInt32();
                            if (sides < 3)
                                throw new ArgumentException("Polygons must have at least 3 sides.");
                            if (!farm.TryGetProperty("Size", out var sizeElement))
                                throw new KeyNotFoundException("A 'Size' key is missing for a Polygon in the Farms configuration.");
                            width = height = sizeElement.GetInt32();
                        }
                        else
                        {
                            throw new ArgumentException($"Unsupported shape type: {shapeType}");
                        }

                        if (!farm.TryGetProperty("Count", out var countElement))
                            throw new KeyNotFoundException("A 'Count' key is missing in the Farms configuration.");
                        int count = countElement.GetInt32();

                        Color color = BaseFunctions.GetColor(farm.GetProperty("Color"), Color.White);
                        Color outlineColor = BaseFunctions.GetColor(farm.GetProperty("OutlineColor"), Color.Black);
                        int outlineWidth = farm.GetProperty("OutlineWidth").GetInt32();

                        for (int i = 0; i < count; i++)
                        {
                            int x = Random.Shared.Next(0, _viewportWidth - width);
                            int y = Random.Shared.Next(0, _viewportHeight - height);
                            _shapesManager.AddShape(new Vector2(x, y), shapeType, width, height, sides, color, outlineColor, outlineWidth, true, false);
                        }
                    }
                }
                else
                {
                    throw new KeyNotFoundException("Missing 'Farms' configuration.");
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
                    throw new KeyNotFoundException("Missing 'Player' configuration.");
                }

                // StaticObjects configuration
                _staticObjects = new List<StaticObject>();

                if (config.RootElement.TryGetProperty("StaticObjects", out var staticObjectsArray))
                {
                    foreach (var staticObject in staticObjectsArray.EnumerateArray())
                    {
                        string type = staticObject.GetProperty("Type").GetString();
                        int width = staticObject.GetProperty("Width").GetInt32();
                        int height = staticObject.GetProperty("Height").GetInt32();
                        Vector2 position = new Vector2(
                            staticObject.GetProperty("Position").GetProperty("X").GetSingle(),
                            staticObject.GetProperty("Position").GetProperty("Y").GetSingle()
                        );
                        Color color = BaseFunctions.GetColor(staticObject.GetProperty("Color"), Color.White);
                        Color outlineColor = BaseFunctions.GetColor(staticObject.GetProperty("OutlineColor"), Color.Black);
                        int outlineWidth = staticObject.GetProperty("OutlineWidth").GetInt32();

                        _staticObjects.Add(new StaticObject(position, width, height, color, outlineColor, outlineWidth));
                    }
                }

                // Initialize PhysicsManager
                _physicsManager = new PhysicsManager();
                _collisionDestroyShapes = config.RootElement.TryGetProperty("CollisionDestroyShapes", out var destroyShapesProperty) &&
                                          destroyShapesProperty.GetBoolean();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An error occurred during initialization: {ex.Message}", ex);
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

            foreach (var staticObject in _staticObjects)
            {
                staticObject.LoadContent(GraphicsDevice);
            }
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

            _physicsManager.ResolveCollisions(
                _shapesManager.GetShapes(),
                _staticObjects, // Pass the list of static objects
                _player,
                _collisionDestroyShapes // Pass the destroyOnCollision flag
            );

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

            // Draw static objects
            foreach (var staticObject in _staticObjects)
            {
                staticObject.Draw(_spriteBatch);
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
