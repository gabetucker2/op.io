using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace op.io
{
    public class StaticObjectManager
    {
        private List<StaticObject> _staticObjects;

        public StaticObjectManager()
        {
            _staticObjects = new List<StaticObject>();
        }

        public void AddStaticObject(Vector2 position, int width, int height, Color color, Color outlineColor, int outlineWidth)
        {
            _staticObjects.Add(new StaticObject(position, width, height, color, outlineColor, outlineWidth));
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            foreach (var staticObject in _staticObjects)
            {
                staticObject.LoadContent(graphicsDevice);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            foreach (var staticObject in _staticObjects)
            {
                staticObject.Draw(spriteBatch);
            }
        }

        public List<StaticObject> GetStaticObjects()
        {
            return _staticObjects;
        }
    }
}
