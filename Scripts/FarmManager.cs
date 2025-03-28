using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace op.io
{
    public class FarmManager
    {
        private List<GameObject> _farmObjects;
        private Random _random;
        private int _viewportWidth;
        private int _viewportHeight;

        public FarmManager(int viewportWidth, int viewportHeight)
        {
            if (viewportWidth <= 0 || viewportHeight <= 0)
                throw new ArgumentException("Viewport dimensions must be greater than 0.");

            _farmObjects = new List<GameObject>();
            _random = new Random(); // Optional: seed for repeatable tests
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;

            LoadFarmObjectsFromDatabase();
        }

        private void LoadFarmObjectsFromDatabase()
        {
            DebugManager.PrintDebug("Loading farm objects from database...");
            var prototypes = GameObjectLoader.LoadGameObjects("FarmData");

            if (prototypes.Count == 0)
            {
                DebugManager.PrintWarning("No farm objects found in database.");
                return;
            }

            DebugManager.PrintMeta($"Loaded {prototypes.Count} prototype entries from FarmData.");

            foreach (var prototype in prototypes)
            {
                int count = Math.Max(1, prototype.Count); // fallback to 1

                DebugManager.PrintDebug($"Generating {count} instances of type {prototype.Shape?.Type ?? "Unknown"}");

                for (int i = 0; i < count; i++)
                {
                    var clone = CloneWithRandomViewportPosition(prototype);
                    _farmObjects.Add(clone);
                    DebugManager.PrintDebug($"Spawned instance {i + 1}/{count} at {clone.Position}");
                }
            }

            DebugManager.PrintMeta($"Final farm object count: {_farmObjects.Count}");
        }

        /// <summary>
        /// Creates a randomized copy of a GameObject within the screen bounds.
        /// </summary>
        private GameObject CloneWithRandomViewportPosition(GameObject source)
        {
            int maxX = Math.Max(1, _viewportWidth - source.Shape.Width);
            int maxY = Math.Max(1, _viewportHeight - source.Shape.Height);

            Vector2 position = new Vector2(
                _random.Next(0, maxX),
                _random.Next(0, maxY)
            );

            var clonedShape = new Shape(
                position,
                source.Shape.Type,
                source.Shape.Width,
                source.Shape.Height,
                source.Shape.Sides,
                source.Shape.FillColor,
                source.Shape.OutlineColor,
                source.Shape.OutlineWidth
            );

            return new GameObject
            {
                Position = position,
                Mass = source.Mass,
                IsPlayer = source.IsPlayer,
                IsDestructible = source.IsDestructible,
                IsCollidable = source.IsCollidable,
                Shape = clonedShape,
                Count = 1
            };
        }

        public void LoadContent(GraphicsDevice graphicsDevice)
        {
            foreach (var farmObject in _farmObjects)
            {
                farmObject.LoadContent(graphicsDevice);
            }
        }

        public void Update(float deltaTime)
        {
            foreach (var farmObject in _farmObjects)
            {
                farmObject.Update(deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool debugEnabled)
        {
            foreach (var farmObject in _farmObjects)
            {
                farmObject.Draw(spriteBatch, debugEnabled);
            }
        }

        public List<GameObject> GetFarmShapes() => _farmObjects;
    }
}
