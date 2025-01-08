using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

        private Circle _circle;
        private SquareManager _squareManager;

        private JsonDocument _config;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Load configuration from JSON
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Config.json");
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Config file not found at path: {configPath}");

            string json = File.ReadAllText(configPath);
            _config = JsonDocument.Parse(json);

            _viewportWidth = GetProperty<int>(_config, "Viewport", "Width", 800);
            _viewportHeight = GetProperty<int>(_config, "Viewport", "Height", 600);
            _backgroundColor = new Color(
                GetProperty<int>(_config, "Viewport", "BackgroundColorR", 0),
                GetProperty<int>(_config, "Viewport", "BackgroundColorG", 0),
                GetProperty<int>(_config, "Viewport", "BackgroundColorB", 0),
                GetProperty<int>(_config, "Viewport", "BackgroundColorA", 255)
            );

            // Apply viewport size
            _graphics.PreferredBackBufferWidth = _viewportWidth;
            _graphics.PreferredBackBufferHeight = _viewportHeight;
            _graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            // Circle settings
            float circleX = GetProperty<float>(_config, "Circle", "X", _viewportWidth / 2f);
            float circleY = GetProperty<float>(_config, "Circle", "Y", _viewportHeight / 2f);
            int circleRadius = GetProperty<int>(_config, "Circle", "Radius", 20);
            float circleSpeed = GetProperty<float>(_config, "Circle", "Speed", 200f);
            Color circleColor = new Color(
                GetProperty<int>(_config, "Circle", "ColorR", 255),
                GetProperty<int>(_config, "Circle", "ColorG", 0),
                GetProperty<int>(_config, "Circle", "ColorB", 0),
                GetProperty<int>(_config, "Circle", "ColorA", 255)
            );

            _circle = new Circle(circleX, circleY, circleRadius, circleSpeed, circleColor, _viewportWidth, _viewportHeight);

            // SquareManager settings
            int squareCount = GetProperty<int>(_config, "Square", "InitialCount", 10);
            Color squareColor = new Color(
                GetProperty<int>(_config, "Square", "ColorR", 0),
                GetProperty<int>(_config, "Square", "ColorG", 255),
                GetProperty<int>(_config, "Square", "ColorB", 0),
                GetProperty<int>(_config, "Square", "ColorA", 255)
            );

            _squareManager = new SquareManager(squareCount, _viewportWidth, _viewportHeight, squareColor);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _circle.LoadContent(GraphicsDevice);
            _squareManager.LoadContent(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            _circle.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            _squareManager.CheckCollisions(_circle);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_backgroundColor);

            _spriteBatch.Begin();
            _circle.Draw(_spriteBatch);
            _squareManager.Draw(_spriteBatch);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private T GetProperty<T>(JsonDocument doc, string section, string key, T defaultValue = default)
        {
            try
            {
                var element = doc.RootElement.GetProperty(section).GetProperty(key);
                return (T)Convert.ChangeType(element.GetRawText(), typeof(T));
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }
    }
}
