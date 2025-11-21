namespace op.io
{
    public static class SwitchConsumerBootstrapper
    {
        public static void RegisterDefaultConsumers()
        {
            SwitchRegistry.ClearConsumers();

            SwitchRegistry.RegisterConsumer("DockingMode", value =>
            {
                BlockManager.DockingModeEnabled = value;
            });

            SwitchRegistry.RegisterConsumer("AllowGameInputFreeze", value =>
            {
                if (!value)
                {
                    GameTracker.FreezeGameInputs = false;
                }
            });

            SwitchRegistry.RegisterConsumer("DebugMode", value =>
            {
                DebugModeHandler.ApplySwitchState(value);
            });

            SwitchRegistry.RegisterConsumer("Crouch", value =>
            {
                if (Core.Instance.Player is Agent agent)
                {
                    agent.IsCrouching = value;
                }
            });

            SwitchRegistry.RegisterConsumer("TransparentTabBlocking", value =>
            {
                GameInitializer.SetWindowClickThrough(value);
            });

            ApplyInitialStates();
        }

        private static void ApplyInitialStates()
        {
            var snapshot = ControlStateManager.GetCachedSwitchStatesSnapshot();
            foreach (var kvp in snapshot)
            {
                SwitchRegistry.NotifyConsumers(kvp.Key, kvp.Value);
            }
        }
    }
}
