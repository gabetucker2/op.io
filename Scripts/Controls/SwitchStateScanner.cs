using System.Collections.Generic;

namespace op.io
{
    public static class SwitchStateScanner
    {
        private static readonly List<string> _switchKeys = new();
        private static bool _initialized;

        public static void Initialize()
        {
            RefreshSwitchKeys();
            _initialized = true;
        }

        public static void RefreshSwitchKeys()
        {
            _switchKeys.Clear();
            _switchKeys.AddRange(InputTypeManager.GetRegisteredSwitchKeys());
        }

        public static void Tick()
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (_switchKeys.Count == 0)
            {
                return;
            }

            foreach (string key in _switchKeys)
            {
                bool liveState = InputManager.IsInputActive(key);
                ControlStateManager.SetSwitchState(key, liveState);
            }
        }
    }
}
