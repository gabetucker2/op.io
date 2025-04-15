using Microsoft.Xna.Framework;
using System;

namespace op.io
{
    public class ShapeVertexGenerator
    {
        private string _shapeType;
        private int _width;
        private int _height;
        private int _sides;

        public ShapeVertexGenerator(string shapeType, int width, int height, int sides)
        {
            _shapeType = shapeType;
            _width = width;
            _height = height;
            _sides = sides;
        }

        public Vector2[] GetVertices()
        {
            switch (_shapeType)
            {
                case "Polygon":
                    return GeneratePolygonVertices();
                case "Rectangle":
                    return GenerateRectangleVertices();
                case "Circle":
                    return GenerateCircleVertices();
                default:
                    throw new InvalidOperationException($"Unsupported shape type: {_shapeType}");
            }
        }

        private Vector2[] GeneratePolygonVertices()
        {
            Vector2[] vertices = new Vector2[_sides];
            float angleIncrement = MathF.Tau / _sides;

            for (int i = 0; i < _sides; i++)
            {
                float angle = angleIncrement * i;
                float x = (_width / 2f) * MathF.Cos(angle);
                float y = (_height / 2f) * MathF.Sin(angle);
                vertices[i] = new Vector2(x, y);
            }
            return vertices;
        }

        private Vector2[] GenerateRectangleVertices()
        {
            float hw = _width / 2f;
            float hh = _height / 2f;
            return new Vector2[] {
                new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh)
            };
        }

        private Vector2[] GenerateCircleVertices()
        {
            int circleSides = 16;
            Vector2[] vertices = new Vector2[circleSides];
            float angleIncrement = MathF.Tau / circleSides;

            for (int i = 0; i < circleSides; i++)
            {
                float angle = angleIncrement * i;
                vertices[i] = new Vector2(
                    (_width / 2f) * MathF.Cos(angle),
                    (_height / 2f) * MathF.Sin(angle)
                );
            }
            return vertices;
        }
    }
}
