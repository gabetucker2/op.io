using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.Scripts;
using System;
using System.IO;
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
        private SquareManager _squareManager;

        private JsonDocument _config;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Load configuration from JSON
            _config = BaseFunctions.Config();

            _viewportWidth = BaseFunctions.GetJSON(_config, "Viewport", "Width", 600);
            _viewportHeight = BaseFunctions.GetJSON(_config, "Viewport", "Height", 600);
            _backgroundColor = BaseFunctions.GetColor(_config, "Viewport", "BackgroundColor", Color.CornflowerBlue);

            // Apply viewport size
            _graphics.PreferredBackBufferWidth = _viewportWidth;
            _graphics.PreferredBackBufferHeight = _viewportHeight;
            _graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            // Circle settings
            float circleX = BaseFunctions.GetJSON(_config, "Circle", "X", _viewportWidth / 2f);
            float circleY = BaseFunctions.GetJSON(_config, "Circle", "Y", _viewportHeight / 2f);
            int circleRadius = BaseFunctions.GetJSON(_config, "Circle", "Radius", 20);
            float circleSpeed = BaseFunctions.GetJSON(_config, "Circle", "Speed", 200f);
            Color circleColor = BaseFunctions.GetColor(_config, "Circle", "Color", Color.Red);

            _player = new Player(new GameObject(), circleX, circleY, circleRadius, circleSpeed, circleColor, _viewportWidth, _viewportHeight);

            // SquareManager settings
            int squareCount = BaseFunctions.GetJSON(_config, "Square", "InitialCount", 10);
            Color squareColor = BaseFunctions.GetColor(_config, "Square", "Color", Color.Green);

            _squareManager = new SquareManager(squareCount, _viewportWidth, _viewportHeight, squareColor);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _player.LoadContent(GraphicsDevice);
            _squareManager.LoadContent(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            _player.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            _squareManager.CheckCollisions(_player);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColor);

            _spriteBatch.Begin();
            _player.Draw(_spriteBatch);
            _squareManager.Draw(_spriteBatch);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
