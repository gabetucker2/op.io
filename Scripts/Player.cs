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

        public Player(Vector2 position, int radius, float speed, Color fillColor, Color outlineColor, int outlineWidth)
            : base(
                position,
                0f,
                1f,
                isPlayer: true,
                isDestructible: false,
                isCollidable: true,
                shape: new Shape(position, "Circle", radius * 2, radius * 2, 0, fillColor, outlineColor, outlineWidth))
        {
            if (radius <= 0)
            {
                DebugManager.PrintError($"Player initialization failed: radius must be > 0 (received {radius})");
                return;
            }

            if (speed <= 0)
            {
                DebugManager.PrintError($"Player initialization failed: speed must be > 0 (received {speed})");
                return;
            }

            if (outlineWidth < 0)
            {
                DebugManager.PrintWarning($"Outline width should not be negative (received {outlineWidth})");
            }

            Speed = speed;
            DebugManager.PrintDebug($"Player created at {position} with radius {radius}, speed {speed}");
        }

        public override void LoadContent(GraphicsDevice graphicsDevice)
        {
            base.LoadContent(graphicsDevice);
            DebugManager.PrintDebug("Player LoadContent completed.");
        }

        public override void Update(float deltaTime)
        {
            if (deltaTime <= 0)
            {
                DebugManager.PrintWarning($"Skipped Player update: deltaTime must be positive (received {deltaTime})");
                return;
            }

            DebugManager.PrintDebug($"[Player] Update started. deltaTime: {deltaTime}");

            base.Update(deltaTime);

            _rotation = InputManager.GetAngleToMouse(Position);
            DebugManager.PrintDebug($"[Player] Rotation updated to {_rotation} radians");

            Vector2 input = InputManager.MoveVector();
            DebugManager.PrintDebug($"[Player] Input vector: {input}");

            ActionHandler.Move(this, input, Speed, deltaTime);
            DebugManager.PrintDebug($"[Player] Called ActionHandler.Move with input={input}, speed={Speed}, deltaTime={deltaTime}");

            if (Shape != null)
            {
                Shape.Position = Position;
                DebugManager.PrintDebug($"[Player] Shape position synced to {Position}");
            }
        }

        public override void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            base.Draw(spriteBatch, debugEnabled);

            if (debugEnabled)
            {
                DrawRotationPointer(spriteBatch);
            }
        }

        private void DrawRotationPointer(SpriteBatch spriteBatch)
        {
            if (spriteBatch == null)
            {
                DebugManager.PrintError("DrawRotationPointer failed: SpriteBatch is null.");
                return;
            }

            DebugManager.PrintDebug("[Player] Drawing rotation pointer...");

            Texture2D lineTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            lineTexture.SetData([Color.Red]);

            Vector2 endpoint = Position + new Vector2(
                MathF.Cos(_rotation) * _pointerLength,
                MathF.Sin(_rotation) * _pointerLength
            );

            float distance = Vector2.Distance(Position, endpoint);
            float angle = MathF.Atan2(endpoint.Y - Position.Y, endpoint.X - Position.X);

            spriteBatch.Draw(
                lineTexture,
                Position,
                null,
                Color.Red,
                angle,
                Vector2.Zero,
                new Vector2(distance, 1),
                SpriteEffects.None,
                0f
            );

            DebugManager.PrintDebug($"[Player] Rotation pointer drawn from {Position} to {endpoint} (angle: {angle}, distance: {distance})");
        }

    }
}
