using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class FarmManager
    {
        private ShapesManager _shapesManager;

        public FarmManager()
        {
            _shapesManager = new ShapesManager();
        }

        public void AddFarmShape(Vector2 position, string type, int size, int sides, Color color, Color outlineColor, int outlineWidth, bool enableCollision, bool enablePhysics)
        {
            _shapesManager.AddShape(position, type, size, sides, color, outlineColor, outlineWidth, enableCollision, enablePhysics);
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            _shapesManager.LoadContent(graphicsDevice);
        }

        public void Update(float deltaTime)
        {
            _shapesManager.Update(deltaTime);
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            _shapesManager.Draw(spriteBatch, debugEnabled);
        }

        public List<Shape> GetFarmShapes()
        {
            return _shapesManager.GetShapes();
        }
    }
}
