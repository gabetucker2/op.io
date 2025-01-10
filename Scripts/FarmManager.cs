using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using op.io.Scripts;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace op_io
{
    public class FarmManager
    {
        public List<FarmShape> _farmShapes;
        private PhysicsManager _physicsManager;
        private bool _collisionDestroyShapes;

        public FarmManager(JsonDocument config, int screenWidth, int screenHeight)
        {
            _farmShapes = new List<FarmShape>();
            _physicsManager = new PhysicsManager();
            _collisionDestroyShapes = config.RootElement.GetProperty("CollisionDestroyShapes").GetBoolean();

            var farms = config.RootElement.GetProperty("Farms").EnumerateArray();
            foreach (var farm in farms)
            {
                string shapeType = farm.GetProperty("Type").GetString() ?? "Polygon";
                int sides = farm.GetProperty("NumberOfSides").GetInt32();
                var colorProperty = farm.GetProperty("Color");
                Color color = BaseFunctions.GetColor(colorProperty, Color.White);
                int count = farm.GetProperty("Count").GetInt32();
                int size = farm.GetProperty("Size").GetInt32();
                int weight = farm.GetProperty("Weight").GetInt32();

                for (int i = 0; i < count; i++)
                {
                    int x = Random.Shared.Next(0, screenWidth - size);
                    int y = Random.Shared.Next(0, screenHeight - size);
                    _farmShapes.Add(new FarmShape(new Vector2(x, y), size, shapeType, sides, color, weight));
                }
            }
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            foreach (var farmShape in _farmShapes)
            {
                farmShape.LoadContent(graphicsDevice);
            }
        }

        public void Update(float deltaTime, Player player)
        {
            _physicsManager.ResolveCollisions(_farmShapes, player, _collisionDestroyShapes);
        }

        public void Draw(SpriteBatch spriteBatch, Player player)
        {
            foreach (var farmShape in _farmShapes)
            {
                farmShape.Draw(spriteBatch);
            }

            player?.Draw(spriteBatch);
        }

        public class FarmShape
        {
            public Vector2 Position;
            public int Size;
            public int Weight;
            private string _shapeType;
            private int _sides;
            private Color _color;
            private Texture2D _texture;

            public FarmShape(Vector2 position, int size, string shapeType, int sides, Color color, int weight)
            {
                Position = position;
                Size = size;
                _shapeType = shapeType;
                _sides = sides;
                _color = color;
                Weight = weight;
            }

            public void LoadContent(GraphicsDevice graphicsDevice)
            {
                _texture = new Texture2D(graphicsDevice, Size, Size);
                Color[] data = new Color[Size * Size];

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        if (IsPointInsidePolygon(x, y, Size / 2, Size / 2, _sides, Size / 2))
                        {
                            data[y * Size + x] = _color;
                        }
                        else
                        {
                            data[y * Size + x] = Color.Transparent;
                        }
                    }
                }
                _texture.SetData(data);
            }

            public void Draw(SpriteBatch spriteBatch)
            {
                spriteBatch.Draw(_texture, Position, Color.White);
            }

            private bool IsPointInsidePolygon(int x, int y, int centerX, int centerY, int sides, float radius)
            {
                double angleIncrement = 2 * Math.PI / sides;
                double startingAngle = (sides % 2 == 0) ? -Math.PI / 2 + angleIncrement / 2 : -Math.PI / 2;
                var points = new List<Vector2>();

                for (int i = 0; i < sides; i++)
                {
                    double angle = startingAngle + i * angleIncrement;
                    points.Add(new Vector2(
                        centerX + (float)(radius * Math.Cos(angle)),
                        centerY + (float)(radius * Math.Sin(angle))
                    ));
                }

                int intersections = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    var p1 = points[i];
                    var p2 = points[(i + 1) % points.Count];

                    if ((p1.Y > y) != (p2.Y > y) &&
                        x < (p2.X - p1.X) * (y - p1.Y) / (p2.Y - p1.Y) + p1.X)
                    {
                        intersections++;
                    }
                }

                return intersections % 2 != 0;
            }
        }
    }
}
