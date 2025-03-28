using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public class FarmManager
    {
        private List<GameObject> _farmObjects;

        public FarmManager()
        {
            _farmObjects = new List<GameObject>();
            LoadFarmObjectsFromDatabase();
        }

        /// <summary>
        /// Loads all farm objects from the database.
        /// </summary>
        private void LoadFarmObjectsFromDatabase()
        {
            DebugManager.DebugPrint("Loading farm objects from database...");
            _farmObjects = GameObjectLoader.LoadGameObjects("FarmData");

            if (_farmObjects.Count == 0)
            {
                DebugManager.DebugPrint("[WARNING] No farm objects found in database.");
            }
            else
            {
                DebugManager.DebugPrint($"Successfully loaded {_farmObjects.Count} farm objects from database.");
            }
        }

        /// <summary>
        /// Loads textures for farm objects.
        /// </summary>
        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            foreach (var farmObject in _farmObjects)
            {
                farmObject.LoadContent(graphicsDevice);
            }
        }

        /// <summary>
        /// Updates all farm objects.
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (var farmObject in _farmObjects)
            {
                farmObject.Update(deltaTime);
            }
        }

        /// <summary>
        /// Draws all farm objects.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            foreach (var farmObject in _farmObjects)
            {
                farmObject.Draw(spriteBatch, debugEnabled);
            }
        }

        /// <summary>
        /// Retrieves the list of farm objects.
        /// </summary>
        public List<GameObject> GetFarmShapes()
        {
            return _farmObjects;
        }
    }
}
