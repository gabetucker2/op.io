using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.Scripts;
using System.Text.Json;

namespace op_io
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Color _backgroundColor;
        private int _viewportWidth;
        private int _viewportHeight;

        private Player _player;
        private FarmManager _farmManager;
        private PhysicsManager _physicsManager;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Load configuration from JSON
            var config = BaseFunctions.Config();

            InitializeViewport(config);
            InitializeComponents(config);
        }

        private void InitializeViewport(JsonDocument config)
        {
            _viewportWidth = BaseFunctions.GetJSON<int>(config, "Viewport", "Width", 600);
            _viewportHeight = BaseFunctions.GetJSON<int>(config, "Viewport", "Height", 600);
            _backgroundColor = BaseFunctions.GetColor(config, "Viewport", "BackgroundColor", Color.CornflowerBlue);

            _graphics.PreferredBackBufferWidth = _viewportWidth;
            _graphics.PreferredBackBufferHeight = _viewportHeight;
            _graphics.ApplyChanges();
        }

        public static Player InitializePlayer(JsonDocument config, int viewportWidth, int viewportHeight)
        {
            float playerX = config.RootElement.GetProperty("Player").GetProperty("X").GetSingle();
            float playerY = config.RootElement.GetProperty("Player").GetProperty("Y").GetSingle();
            int playerRadius = config.RootElement.GetProperty("Player").GetProperty("Radius").GetInt32();
            float playerSpeed = config.RootElement.GetProperty("Player").GetProperty("Speed").GetSingle();
            int playerWeight = config.RootElement.GetProperty("Player").GetProperty("Weight").GetInt32();
            var colorProperty = config.RootElement.GetProperty("Player").GetProperty("Color");
            Color playerColor = BaseFunctions.GetColor(colorProperty, Color.Red);

            return new Player(playerX, playerY, playerRadius, playerSpeed, playerColor, viewportWidth, viewportHeight, playerWeight);
        }

        private void InitializeComponents(JsonDocument config)
        {
            _player = InitializePlayer(config, _viewportWidth, _viewportHeight);
            _farmManager = new FarmManager(config, _viewportWidth, _viewportHeight);
            _physicsManager = new PhysicsManager();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _farmManager.LoadContent(GraphicsDevice);
            _player.LoadContent(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Get input from InputManager
            Vector2 input = InputManager.MoveVector();

            // Apply player movement using PhysicsManager
            _physicsManager.ApplyPlayerInput(_player, input, deltaTime);

            // Update farm manager and resolve collisions
            _farmManager.Update(deltaTime, _player);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColor);

            _spriteBatch.Begin();
            _farmManager.Draw(_spriteBatch, _player);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
