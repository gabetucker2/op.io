using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace op.io
{
    public static class GameInitializer
    {
        public static void Initialize(Core game)
        {
            DatabaseInitializer.InitializeDatabase();
            LoadGeneralSettings(game);

            DebugManager.PrintMeta("Game initialized");

            game.ShapesManager = new ShapeManager(game.ViewportWidth, game.ViewportHeight);
        }

        private static void LoadGeneralSettings(Core game)
        {
            DebugManager.PrintMeta("Loading general settings...");

            try
            {
                int r = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_R", 30);
                int g = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_G", 30);
                int b = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_B", 30);
                int a = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "BackgroundColor_A", 255);

                game.BackgroundColor = new Color(r, g, b, a);

                game.ViewportWidth = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportWidth", 1280);
                game.ViewportHeight = BaseFunctions.GetValue<int>("GeneralSettings", "Value", "SettingKey", "ViewportHeight", 720);

                game.Graphics.PreferredBackBufferWidth = game.ViewportWidth;
                game.Graphics.PreferredBackBufferHeight = game.ViewportHeight;
                game.Graphics.ApplyChanges();

                DebugManager.PrintMeta($"Loaded general settings: BackgroundColor={game.BackgroundColor}, Viewport={game.ViewportWidth}x{game.ViewportHeight}");
            }
            catch (Exception ex)
            {
                DebugManager.PrintError($"Failed to load general settings: {ex.Message}");
                game.BackgroundColor = Color.CornflowerBlue;
                game.ViewportWidth = 1280;
                game.ViewportHeight = 720;
            }
        }
    }
}
