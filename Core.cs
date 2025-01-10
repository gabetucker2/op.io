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

        private void InitializeComponents(JsonDocument config)
        {
            _player = FarmManager.InitializePlayer(config, _viewportWidth, _viewportHeight);
            _farmManager = new FarmManager(config, _viewportWidth, _viewportHeight);
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _farmManager.LoadContent(GraphicsDevice, _player);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            _farmManager.Update((float)gameTime.ElapsedGameTime.TotalSeconds, _player);

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
