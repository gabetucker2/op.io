﻿using System;
using System.Collections.Generic;

namespace op.io
{
    public static class ObjectManager
    {
        public static void InitializeObjects(Core game)
        {
            game.GameObjects = new List<GameObject>();
            game.StaticObjects = new List<GameObject>();

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
            DebugManager.PrintMeta("Initializing Player...");

            try
            {
                int playerId = BaseFunctions.GetValue<int>("Players", "ID", "Name", "Player1", 0);
                GameObject player = GameObjectLoader.LoadGameObject("Players", playerId);

                if (player == null)
                {
                    DebugManager.PrintError("Failed to initialize Player. Check database configuration.");
                    return;
                }

                game.GameObjects.Add(player);
                DebugManager.PrintMeta($"Player initialized at: {player.Position}, Shape: {player.Shape?.Type ?? "None"}");
            }
            catch (Exception ex)
            {
                DebugManager.PrintError($"Exception in InitializePlayer: {ex.Message}");
            }
        }

        private static void InitializeMap(Core game)
        {
            DebugManager.PrintMeta("Initializing Map...");
            game.StaticObjects = GameObjectLoader.LoadGameObjects("MapData");

            if (game.StaticObjects.Count == 0)
            {
                DebugManager.PrintWarning("No static objects were loaded. Check database configuration.");
            }

            game.GameObjects.AddRange(game.StaticObjects);
            DebugManager.PrintMeta($"Map initialized with {game.StaticObjects.Count} static objects.");
        }

        private static void InitializeFarms(Core game)
        {
            DebugManager.PrintMeta("Initializing Farms...");
            var farmManager = new FarmManager(game.ViewportWidth, game.ViewportHeight);
            var farms = farmManager.GetFarmShapes();
            game.GameObjects.AddRange(farms);
            DebugManager.PrintMeta($"Farms initialized with {farms.Count} objects.");
        }
    }
}
