using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class FarmManager
    {
        private List<FarmShape> _farmShapes;

        public FarmManager()
        {
            _farmShapes = new List<FarmShape>();
        }

        public void AddFarmShape(FarmShape shape)
        {
            _farmShapes.Add(shape);
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            foreach (var shape in _farmShapes)
            {
                shape.Draw(spriteBatch, debugEnabled);
            }
        }
    }
}
