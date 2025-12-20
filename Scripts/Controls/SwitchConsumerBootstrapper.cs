namespace op.io
{
    public static class SwitchConsumerBootstrapper
    {
        private static bool _dockingModeState;
        private static bool _transparentTabBlockingState;

        public static void RegisterDefaultConsumers()
        {
            SwitchRegistry.ClearConsumers();

            SwitchRegistry.RegisterConsumer("DockingMode", value =>
            {
                bool previous = _dockingModeState;
                _dockingModeState = value;
                BlockManager.DockingModeEnabled = value;
                DockingDiagnostics.RecordConsumerUpdate(
                    "SwitchConsumerBootstrapper.RegisterDefaultConsumers",
                    value,
                    note: $"previous={previous}");
                ApplyWindowClickThrough();
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

            SwitchRegistry.RegisterConsumer("TransparentTabBlocking", value =>
            {
                _transparentTabBlockingState = value;
                ApplyWindowClickThrough();
            });

            SwitchRegistry.RegisterConsumer(InspectModeState.InspectModeKey, value =>
            {
                InspectModeState.ApplyInspectModeState(value);
            });

            SwitchRegistry.RegisterConsumer(ControlKeyMigrations.HoldInputsKey, value =>
            {
                if (value)
                {
                    InputManager.ApplyHoldLatchSnapshot();
                }
                else
                {
                    InputManager.ClearHoldLatch();
                }
            });

            ApplyInitialStates();
        }

        private static void ApplyWindowClickThrough()
        {
            // Keep the window interactive while docking mode is on; honor the transparent blocking switch otherwise.
            bool enableClickThrough = !_dockingModeState && !_transparentTabBlockingState;
            GameInitializer.SetWindowClickThrough(enableClickThrough);
        }

        private static void ApplyInitialStates()
        {
            var snapshot = ControlStateManager.GetCachedSwitchStatesSnapshot();

            if (snapshot.TryGetValue("DockingMode", out bool docking))
            {
                _dockingModeState = docking;
                DockingDiagnostics.RecordConsumerUpdate(
                    "SwitchConsumerBootstrapper.ApplyInitialStates",
                    docking,
                    note: "Hydrated from cached switch state snapshot");
            }
            else
            {
                DockingDiagnostics.RecordConsumerUpdate(
                    "SwitchConsumerBootstrapper.ApplyInitialStates",
                    state: false,
                    note: "DockingMode missing from cached switch state snapshot; defaulting false");
            }

            if (snapshot.TryGetValue("TransparentTabBlocking", out bool transparentBlock))
            {
                _transparentTabBlockingState = transparentBlock;
            }

            foreach (var kvp in snapshot)
            {
                SwitchRegistry.NotifyConsumers(kvp.Key, kvp.Value);
            }
        }
    }
}
