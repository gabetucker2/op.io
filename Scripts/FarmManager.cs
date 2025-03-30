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
            DebugLogger.PrintObject("Initializing FarmManager...");

            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                DebugLogger.PrintError($"Invalid viewport dimensions: {viewportWidth}x{viewportHeight}. Must be greater than 0.");
                viewportWidth = Math.Max(1, viewportWidth);
                viewportHeight = Math.Max(1, viewportHeight);
            }

            _farmObjects = new List<GameObject>();
            _random = new Random(); // Optional: seed for repeatable tests
            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;

            LoadFarmObjectsFromDatabase();
        }

        private void LoadFarmObjectsFromDatabase()
        {
            _farmObjects.Clear();

            DebugLogger.PrintObject("Loading farm objects from database...");
            var prototypes = GameObjectLoader.LoadGameObjects("FarmData");

            if (prototypes.Count == 0)
            {
                DebugLogger.PrintWarning("No farm objects found in database.");
                return;
            }

            DebugLogger.PrintObject($"Loaded {prototypes.Count} prototype entries from FarmData.");

            foreach (var prototype in prototypes)
            {
                int count = Math.Max(1, prototype.Count); // fallback to 1

                DebugLogger.PrintObject($"Generating {count} instances of type {prototype.Shape?.Type ?? "Unknown"}");

                for (int i = 0; i < count; i++)
                {
                    var clone = CloneWithRandomViewportPosition(prototype);
                    _farmObjects.Add(clone);
                    DebugLogger.PrintObject($"Spawned instance {i + 1}/{count} at {clone.Position} with rotation {clone.Rotation:F2} radians");
                }
            }

            DebugLogger.PrintObject($"Final farm object count: {_farmObjects.Count}");
        }

        /// <summary>
        /// Creates a randomized copy of a GameObject within the screen bounds and assigns a random rotation.
        /// </summary>
        private GameObject CloneWithRandomViewportPosition(GameObject source)
        {
            int maxX = Math.Max(1, _viewportWidth - source.Shape.Width);
            int maxY = Math.Max(1, _viewportHeight - source.Shape.Height);

            Vector2 position = new Vector2(
                _random.Next(0, maxX),
                _random.Next(0, maxY)
            );

            float rotation = (float)(_random.NextDouble() * MathF.Tau); // Random rotation between 0 and 2π

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
                Rotation = rotation, // Inject stochastic rotation here
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
                farmObject.Draw(spriteBatch);
            }
        }

        public List<GameObject> GetFarmShapes() => _farmObjects;
    }
}
