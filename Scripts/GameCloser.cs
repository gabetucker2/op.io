namespace op.io
{
    public static class Exit
    {
        public static void CloseGame()
        {
            PersistCachedSwitchStates();
            Core.Instance.Exit();
        }

        private static void PersistCachedSwitchStates()
        {
            var cachedSwitches = ControlStateManager.GetCachedSwitchStatesSnapshot();

            if (cachedSwitches.Count == 0)
            {
                DebugLogger.PrintDatabase("No cached switch states to persist before closing.");
                return;
            }

            DebugLogger.PrintDatabase("Persisting cached switch states before closing the game.");

            foreach (var kvp in cachedSwitches)
            {
                DatabaseConfig.UpdateSetting(
                    "ControlKey",
                    "SwitchStartState",
                    kvp.Key,
                    TypeConversionFunctions.BoolToInt(kvp.Value));
            }
        }
    }
}
