using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op_io.Scripts;

namespace op_io
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Circle _circle;
        private SquareManager _squareManager;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
            // Do not initialize _circle and _squareManager here to avoid issues with GraphicsDevice.Viewport.
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Initialize _circle and _squareManager here, where GraphicsDevice is fully initialized
            int viewportWidth = GraphicsDevice.Viewport.Width;
            int viewportHeight = GraphicsDevice.Viewport.Height;

            _circle = new Circle(viewportWidth / 2, viewportHeight / 2, 20, 200f, Color.Blue, viewportWidth, viewportHeight);
            _squareManager = new SquareManager(10, viewportWidth, viewportHeight);

            // Load content for both objects
            _circle.LoadContent(GraphicsDevice);
            _squareManager.LoadContent(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _circle.Update(deltaTime);
            _squareManager.CheckCollisions(_circle);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin();
            _circle.Draw(_spriteBatch);
            _squareManager.Draw(_spriteBatch);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
