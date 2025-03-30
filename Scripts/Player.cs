using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class Player : GameObject
    {
        public float Speed { get; private set; }
        private float _rotation;
        private const int _pointerLength = 50;
        private static Texture2D _pointerTexture;

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
            DebugLogger.PrintPlayer($"Player created at {position} with radius {radius}, speed {speed}");
        }

        public override void LoadContent(GraphicsDevice graphicsDevice)
        {
            base.LoadContent(graphicsDevice);

            if (_pointerTexture == null)
            {
                _pointerTexture = new Texture2D(graphicsDevice, 1, 1);
                _pointerTexture.SetData(new[] { Color.Red });
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

            _rotation = InputManager.GetAngleToMouse(Position);
            Vector2 input = InputManager.MoveVector();

            ActionHandler.Move(this, input, Speed, deltaTime);

            if (Shape != null)
                Shape.Position = Position;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            if (DebugModeHandler.IsDebugEnabled())
                DebugVisualizer.DrawDebugRotationPointer(spriteBatch, Position, _rotation, _pointerLength, _pointerTexture);
        }
        
    }
}
