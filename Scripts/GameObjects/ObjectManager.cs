using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io
{
    public static class ObjectManager
    {
        public static void InitializeObjects(Core game)
        {
            game.GameObjects = new List<GameObject>();

            InitializePlayer(game);
            InitializeMap(game);
            InitializeFarms(game);

            foreach (var obj in game.GameObjects)
            {
                obj.LoadContent(game.GraphicsDevice);
            }
        }

        private static void InitializePlayer(Core game)
        {
            DebugLogger.PrintObject("Initializing Player...");

            try
            {
                int playerId = BaseFunctions.GetValue<int>("Players", "ID", "Name", "Player1");
                GameObject player = GameObjectLoader.LoadGameObject("Players", playerId);

                if (player == null)
                {
                    DebugLogger.PrintError("Failed to initialize Player. Check database configuration.");
                    return;
                }

                game.GameObjects.Add(player);
                DebugLogger.PrintObject($"Player initialized at: {player.Position}, Shape: {player.Shape?.Type ?? "None"}");
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Exception in InitializePlayer: {ex.Message}");
            }
        }

        private static void InitializeMap(Core game)
        {
            DebugLogger.PrintObject("Initializing Map...");

            var mapObjects = GameObjectLoader.LoadGameObjects("MapData");

            if (mapObjects.Count == 0)
            {
                DebugLogger.PrintWarning("No map objects were loaded. Check database configuration.");
                return;
            }

            foreach (var obj in mapObjects)
            {
                game.GameObjects.Add(obj);
            }

            DebugLogger.PrintObject($"Map initialized with {game.GameObjects.Count} GameObjects.");
        }

        private static void InitializeFarms(Core game)
        {
            DebugLogger.PrintObject("Initializing Farms...");
            var farmManager = new FarmManager(game.ViewportWidth, game.ViewportHeight);
            var farms = farmManager.GetFarmShapes();
            game.GameObjects.AddRange(farms);
            DebugLogger.PrintObject($"Farms initialized with {farms.Count} objects.");
        }
    }
}