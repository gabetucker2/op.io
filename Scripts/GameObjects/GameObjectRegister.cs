using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    public static class GameObjectRegister
    {
        private static readonly List<GameObject> _gameObjects = new List<GameObject>();

        // Registers a GameObject to the list if it's not already registered
        public static void RegisterGameObject(GameObject gameObject)
        {
            if (!_gameObjects.Contains(gameObject))
            {
                _gameObjects.Add(gameObject);
                // DebugLogger.PrintGO($"Registered GameObject ID={gameObject.ID}, Shape={gameObject.Shape?.ShapeType ?? "NULL"}");
            }
            else
            {
                DebugLogger.PrintWarning($"Attempted to register duplicate GameObject ID={gameObject.ID}");
            }
        }

        // Unregisters a GameObject from the list
        public static void UnregisterGameObject(GameObject gameObject)
        {
            if (_gameObjects.Remove(gameObject))
            {
                 DebugLogger.PrintGO($"Unregistered GameObject ID={gameObject.ID}");
            }
            else
            {
                DebugLogger.PrintWarning($"Attempted to unregister GameObject ID={gameObject.ID} but it wasn't in the manager.");
            }
        }

        // Retrieves a list of all registered GameObjects
        public static List<GameObject> GetRegisteredGameObjects()
        {
            return _gameObjects;
        }

        // Clears all registered GameObjects
        public static void ClearAllGameObjects()
        {
            _gameObjects.Clear();
        }

        // Draw all registered GameObjects
        public static void DrawRegisteredGameObjects(SpriteBatch spriteBatch)
        {
            foreach (GameObject gameObject in _gameObjects)
            {
                if (gameObject.Shape == null)
                {
                    DebugLogger.PrintWarning($"GameObject ID={gameObject.ID} has no Shape — skipping draw.");
                    continue;
                }

                if (gameObject.Shape.IsPrototype)
                {
                    DebugLogger.PrintDebug($"Skipping draw for prototype shape ID={gameObject.ID}.");
                    continue;
                }

                gameObject.Shape.Draw(spriteBatch, gameObject);

                if (DebugModeHandler.DEBUGENABLED && gameObject == Core.Instance.Player)
                {
                    // DebugRenderer.DrawRotationPointer(spriteBatch, (Agent)gameObject);
                }
            }
        }
    }
}
