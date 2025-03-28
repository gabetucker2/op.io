using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace op.io
{
    public class ShapeManager
    {
        private List<GameObject> _shapes;
        private int _viewportWidth;
        private int _viewportHeight;

        public ShapeManager(int viewportWidth, int viewportHeight)
        {
            if (viewportWidth <= 0)
            {
                DebugManager.PrintError($"Invalid viewport width: {viewportWidth}. Defaulting to 1280.");
                viewportWidth = 1280;
            }

            if (viewportHeight <= 0)
            {
                DebugManager.PrintError($"Invalid viewport height: {viewportHeight}. Defaulting to 720.");
                viewportHeight = 720;
            }

            _shapes = new List<GameObject>();
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;

            LoadShapesFromDatabase();
        }

        /// <summary>
        /// Loads shapes from GameObjects, FarmData, and MapData tables.
        /// </summary>
        public void LoadShapesFromDatabase()
        {
            DebugManager.PrintMeta("Loading shapes from database...");

            try
            {
                List<GameObject> farmShapes = GameObjectLoader.LoadGameObjects("FarmData");
                List<GameObject> mapShapes = GameObjectLoader.LoadGameObjects("MapData");

                AddShapes(farmShapes, "FarmData");
                AddShapes(mapShapes, "MapData");

                DebugManager.PrintInfo($"Total shapes loaded: {_shapes.Count}");
            }
            catch (Exception ex)
            {
                DebugManager.PrintError($"Failed to load shapes from database: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to add shapes from different tables.
        /// </summary>
        private void AddShapes(List<GameObject> shapeObjects, string sourceTable)
        {
            if (shapeObjects.Count == 0)
            {
                DebugManager.PrintWarning($"No shapes were loaded from {sourceTable}.");
                return;
            }

            foreach (var shape in shapeObjects)
            {
                _shapes.Add(shape);
            }

            DebugManager.PrintInfo($"Loaded {shapeObjects.Count} shapes from {sourceTable}.");
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            foreach (var shape in _shapes)
            {
                shape.LoadContent(graphicsDevice);
            }
        }

        /// <summary>
        /// Updates all shapes.
        /// </summary>
        public void Update(float deltaTime)
        {
            foreach (var shape in _shapes)
            {
                shape.Update(deltaTime);
            }
        }

        /// <summary>
        /// Draws all shapes.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            foreach (var shape in _shapes)
            {
                shape.Draw(spriteBatch, debugEnabled);
            }
        }

        /// <summary>
        /// Retrieves the list of shapes.
        /// </summary>
        public List<GameObject> GetShapes()
        {
            return _shapes;
        }
    }
}
