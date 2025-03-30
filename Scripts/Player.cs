using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class Player : GameObject
    {
        public float Speed { get; private set; }
        public static Player player { get; set; }

        private const int _pointerLength = 50;
        private static Texture2D _pointerTexture;

        // Store the graphics device used during LoadContent
        private static GraphicsDevice _graphicsDevice;

        public Player(Vector2 position, int radius, float speed, Color fillColor, Color outlineColor, int outlineWidth)
            : base(
                position,
                0f,
                1f,
                isPlayer: true,
                isDestructible: false,
                isCollidable: true,
                staticPhysics: false,
                shape: new Shape(position, "Circle", radius * 2, radius * 2, 0, fillColor, outlineColor, outlineWidth))
        {
            if (radius <= 0)
                DebugLogger.PrintError($"Initialization failed: radius must be > 0 (received {radius})");

            if (speed <= 0)
                DebugLogger.PrintError($"Initialization failed: speed must be > 0 (received {speed})");

            if (outlineWidth < 0)
                DebugLogger.PrintWarning($"Outline width should not be negative (received {outlineWidth})");

            Speed = speed;
            Position = position;
            DebugLogger.PrintPlayer($"Player created at {position} with radius {radius}, speed {speed}");
        }

        public override void LoadContent(GraphicsDevice graphicsDevice)
        {
            base.LoadContent(graphicsDevice);

            // Store the graphics device for later use in Draw
            _graphicsDevice = graphicsDevice;

            if (_pointerTexture == null)
            {
                _pointerTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pointerTexture.SetData(new[] { Color.White });
            }

            if (_pointerTexture == null)
            {
                DebugLogger.PrintError("Player LoadContent failed: Pointer texture could not be initialized.");
                return;
            }

            DebugLogger.PrintPlayer("Player LoadContent completed.");
        }

        public override void Update(float deltaTime)
        {
            if (deltaTime <= 0)
            {
                DebugLogger.PrintWarning("Skipped update: deltaTime must be positive.");
                return;
            }

            base.Update(deltaTime);

            Rotation = MouseFunctions.GetAngleToMouse(Position);
            Vector2 input = InputManager.GetMoveVector();

            ActionHandler.Move(this, input, Speed * InputManager.SpeedMultiplier(), deltaTime);

            if (Shape != null)
                Shape.Position = Position;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (_graphicsDevice == null)
            {
                DebugLogger.PrintError("Draw failed: GraphicsDevice is null. Ensure LoadContent() is called before Draw().");
                return;
            }

            if (DebugModeHandler.IsDebugEnabled())
            {
                DebugVisualizer.DrawDebugRotationPointer(spriteBatch, _graphicsDevice, Position, Rotation, _pointerLength);
            }
        }
    }
}
