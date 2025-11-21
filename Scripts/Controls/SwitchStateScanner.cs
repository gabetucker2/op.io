using System.Collections.Generic;
using System.Linq;

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
            foreach (string key in InputTypeManager.GetRegisteredSwitchKeys())
            {
                if (!ControlKeyRules.ShouldScannerTrackSwitch(key))
                {
                    continue;
                }

                _switchKeys.Add(key);
            }
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
