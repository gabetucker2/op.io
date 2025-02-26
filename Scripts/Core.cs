using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace op.io
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Color _backgroundColor;
        private int _viewportWidth;
        private int _viewportHeight;
        private List<GameObject> _gameObjects;
        private List<GameObject> _staticObjects;
        private ShapesManager _shapesManager;
        private PhysicsManager _physicsManager;
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
            try
            {
                _gameObjects = new List<GameObject>();
                _staticObjects = new List<GameObject>();
                _physicsManager = new PhysicsManager();

                // Load general configuration
                try
                {
                    var generalConfig = BaseFunctions.LoadJson("General.json");
                    _backgroundColor = BaseFunctions.GetColor(generalConfig.RootElement.GetProperty("BackgroundColor"));
                    _viewportWidth = generalConfig.RootElement.GetProperty("ViewportWidth").GetInt32();
                    _viewportHeight = generalConfig.RootElement.GetProperty("ViewportHeight").GetInt32();
                    bool isFullscreen = generalConfig.RootElement.GetProperty("Fullscreen").GetBoolean();
                    bool vSyncEnabled = generalConfig.RootElement.GetProperty("VSync").GetBoolean();
                    int targetFrameRate = generalConfig.RootElement.GetProperty("TargetFrameRate").GetInt32();

                    // Apply settings to the graphics device
                    _graphics.PreferredBackBufferWidth = _viewportWidth;
                    _graphics.PreferredBackBufferHeight = _viewportHeight;
                    _graphics.IsFullScreen = isFullscreen;
                    _graphics.SynchronizeWithVerticalRetrace = vSyncEnabled;
                    TargetElapsedTime = TimeSpan.FromSeconds(1.0 / targetFrameRate);
                    _graphics.ApplyChanges();
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException("General.json file not found. Ensure it exists in the Data folder.", ex);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Error parsing General.json. Check JSON format.", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unexpected error loading general settings.", ex);
                }

                // Initialize shapes manager
                try
                {
                    _shapesManager = new ShapesManager(_viewportWidth, _viewportHeight);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to initialize ShapesManager.", ex);
                }

                // Initialize Player
                try
                {
                    var playerConfig = BaseFunctions.LoadJson("Player.json");
                    InitializePlayer(playerConfig);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException("Player.json file not found. Ensure it exists in the Data folder.", ex);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Error parsing Player.json. Check JSON format.", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unexpected error initializing the player.", ex);
                }

                // Initialize Map
                try
                {
                    var mapConfig = BaseFunctions.LoadJson("Map.json");
                    InitializeMap(mapConfig);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException("Map.json file not found. Ensure it exists in the Data folder.", ex);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Error parsing Map.json. Check JSON format.", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unexpected error initializing the map.", ex);
                }

                // Initialize Farms
                try
                {
                    var farmConfig = BaseFunctions.LoadJson("Farm.json");
                    InitializeFarms(farmConfig);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException("Farm.json file not found. Ensure it exists in the Data folder.", ex);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Error parsing Farm.json. Check JSON format.", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unexpected error initializing farms.", ex);
                }

                // Add static objects to a separate list
                try
                {
                    foreach (var obj in _gameObjects.Where(go => !go.IsPlayer && !go.IsDestructible))
                    {
                        _staticObjects.Add(obj);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unexpected error while filtering and adding static objects.", ex);
                }

                // Initialize Debugging
                try
                {
                    var debugConfig = BaseFunctions.LoadJson("Debug.json");
                    InitializeDebugging(debugConfig);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidOperationException("Debug.json file not found. Ensure it exists in the Data folder.", ex);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException("Error parsing Debug.json. Check JSON format.", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unexpected error initializing debugging settings.", ex);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Critical error initializing the game: {ex.Message}", ex);
            }

            base.Initialize();
        }

        private void InitializePlayer(JsonDocument playerConfig)
        {
            try
            {
                var root = playerConfig.RootElement;

                if (!root.TryGetProperty("X", out var xProperty) || !root.TryGetProperty("Y", out var yProperty))
                    throw new KeyNotFoundException("Player.json is missing required 'X' or 'Y' properties.");

                if (!root.TryGetProperty("Radius", out var radiusProperty))
                    throw new KeyNotFoundException("Player.json is missing the required 'Radius' property.");

                var player = new GameObject(
                    new Vector2(xProperty.GetSingle(), yProperty.GetSingle()),
                    0f,
                    1f,
                    radiusProperty.GetInt32(),
                    isPlayer: true,
                    isDestructible: false,
                    isCollidable: true
                );

                // Ensure only one player exists
                if (_gameObjects.Any(go => go.IsPlayer))
                    throw new InvalidOperationException("Multiple GameObjects with IsPlayer=true detected.");

                _gameObjects.Add(player);
            }
            catch (KeyNotFoundException ex)
            {
                throw new InvalidOperationException("Player.json is missing a required key.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unexpected error initializing the player.", ex);
            }
        }

        private void InitializeMap(JsonDocument mapConfig)
        {
            var staticObjectsArray = mapConfig.RootElement.GetProperty("StaticObjects").EnumerateArray();
            foreach (var staticObjectConfig in staticObjectsArray)
            {
                var staticObject = new GameObject(
                    new Vector2(
                        staticObjectConfig.GetProperty("Position").GetProperty("X").GetSingle(),
                        staticObjectConfig.GetProperty("Position").GetProperty("Y").GetSingle()
                    ),
                    0f,
                    1f,
                    MathF.Sqrt(
                        MathF.Pow(staticObjectConfig.GetProperty("Width").GetInt32(), 2) +
                        MathF.Pow(staticObjectConfig.GetProperty("Height").GetInt32(), 2)
                    ) / 2,
                    isPlayer: false,
                    isDestructible: false,
                    isCollidable: true
                );

                _gameObjects.Add(staticObject);
            }
        }

        private void InitializeFarms(JsonDocument farmConfig)
        {
            var farmManager = new FarmManager();
            foreach (var farm in farmConfig.RootElement.GetProperty("Farms").EnumerateArray())
            {
                farmManager.AddFarmShape(
                    new Vector2(
                        farm.GetProperty("Position").GetProperty("X").GetSingle(),
                        farm.GetProperty("Position").GetProperty("Y").GetSingle()
                    ),
                    farm.GetProperty("Type").GetString(),
                    farm.GetProperty("Width").GetInt32(),
                    farm.GetProperty("Height").GetInt32(),
                    farm.GetProperty("Sides").GetInt32(),
                    BaseFunctions.GetColor(farm.GetProperty("FillColor")),
                    BaseFunctions.GetColor(farm.GetProperty("OutlineColor")),
                    farm.GetProperty("OutlineWidth").GetInt32(),
                    farm.GetProperty("IsCollidable").GetBoolean(),
                    farm.GetProperty("IsDestructible").GetBoolean()
                );
            }

            _gameObjects.AddRange(farmManager.GetFarmShapes());
        }

        private void InitializeDebugging(JsonDocument debugConfig)
        {
            var debugRoot = debugConfig.RootElement;
            _debugEnabled = debugRoot.GetProperty("Enabled").GetBoolean();
            if (_debugEnabled)
            {
                DebugVisualizer.Initialize(GraphicsDevice, debugRoot.GetProperty("DebugCircle"));
            }
        }

        protected override void LoadContent()
        {
            if (GraphicsDevice == null)
                throw new ArgumentNullException(nameof(GraphicsDevice), "GraphicsDevice cannot be null during content loading.");

            _spriteBatch = new SpriteBatch(GraphicsDevice)
                ?? throw new ArgumentNullException(nameof(_spriteBatch), "SpriteBatch initialization failed.");

            foreach (var gameObject in _gameObjects)
            {
                gameObject.LoadContent(GraphicsDevice);
            }

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

            foreach (var gameObject in _gameObjects)
            {
                gameObject.Update(deltaTime);
            }

            _shapesManager.Update(deltaTime);

            if (_physicsManager == null)
                throw new InvalidOperationException("PhysicsManager is not initialized.");

            var player = _gameObjects.FirstOrDefault(go => go.IsPlayer);
            if (player == null)
                throw new InvalidOperationException("No GameObject with IsPlayer=true exists.");

            // Ensure _staticObjects is populated before calling ResolveCollisions
            _physicsManager.ResolveCollisions(
                _gameObjects,
                _staticObjects, // Static objects now passed correctly
                player,
                destroyOnCollision: false
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

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            foreach (var gameObject in _gameObjects)
            {
                gameObject.Draw(_spriteBatch, _debugEnabled);
            }

            _shapesManager.Draw(_spriteBatch, _debugEnabled);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
