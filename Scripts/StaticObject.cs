using System;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class StaticObject
    {
        // Properties
        public Vector2 Position { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        private Color _color;
        private Color _outlineColor;
        private int _outlineWidth;
        private Texture2D _texture;
        public bool IsCollidable { get; private set; } // Indicates if the object is collidable
        public bool IsDestructible { get; private set; } // Indicates if the object is destructible

        // New property: BoundingRadius
        public float BoundingRadius => MathF.Sqrt((Width * Width) + (Height * Height)) / 2;

        // Updated Constructor
        public StaticObject(
            Vector2 position,
            int width,
            int height,
            Color color,
            Color outlineColor,
            int outlineWidth,
            bool isCollidable,
            bool isDestructible
        )
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be greater than 0.", nameof(width));

            Position = position;
            Width = width;
            Height = height;
            _color = color;
            _outlineColor = outlineColor;
            _outlineWidth = outlineWidth;
            IsCollidable = isCollidable;
            IsDestructible = isDestructible;
        }

        // Load texture for rendering
        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _texture = new Texture2D(graphicsDevice, Width, Height);
            Color[] data = new Color[Width * Height];
            for (int i = 0; i < data.Length; i++)
                data[i] = _color;
            _texture.SetData(data);
        }

        // Draw the object
        public void Draw(SpriteBatch spriteBatch)
        {
            Rectangle bounds = new Rectangle((int)(Position.X - Width / 2), (int)(Position.Y - Height / 2), Width, Height);
            spriteBatch.Draw(_texture, bounds, Color.White);
        }

        // Create a StaticObject from JSON
        public static StaticObject FromJson(JsonElement config)
        {
            return new StaticObject(
                new Vector2(
                    config.GetProperty("Position").GetProperty("X").GetSingle(),
                    config.GetProperty("Position").GetProperty("Y").GetSingle()
                ),
                config.GetProperty("Width").GetInt32(),
                config.GetProperty("Height").GetInt32(),
                BaseFunctions.GetColor(config.GetProperty("Color")),
                BaseFunctions.GetColor(config.GetProperty("OutlineColor")),
                config.GetProperty("OutlineWidth").GetInt32(),
                config.GetProperty("IsCollidable").GetBoolean(),
                config.GetProperty("IsDestructible").GetBoolean()
            );
        }
    }
}
