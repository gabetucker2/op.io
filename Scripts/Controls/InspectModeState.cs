using System;

namespace op.io
{
    public static class InspectModeState
    {
        public const string InspectModeKey = "InspectMode";

        private static InspectableObjectInfo _hovered;
        private static InspectableObjectInfo _locked;
        private static bool _inspectModeEnabled;

        public static bool InspectModeEnabled => _inspectModeEnabled;
        public static bool IsNonMetaSuppressed => _inspectModeEnabled;

        public static void ApplyInspectModeState(bool enabled)
        {
            _inspectModeEnabled = enabled;
        }

        public static void UpdateHovered(InspectableObjectInfo hovered)
        {
            _hovered = hovered;
        }

        public static void LockHovered()
        {
            if (!_inspectModeEnabled || _hovered == null)
            {
                return;
            }

            _locked = _hovered.Clone();
        }

        public static void ClearLock()
        {
            _locked = null;
        }

        public static InspectableObjectInfo GetActiveTarget()
        {
            InspectableObjectInfo active = _locked ?? _hovered;
            active?.Refresh();
            return active;
        }

        public static InspectableObjectInfo GetLockedTarget()
        {
            _locked?.Refresh();
            return _locked;
        }

        public static void ValidateLockStillValid()
        {
            if (_locked == null)
            {
                return;
            }

            _locked.Refresh();
            if (!_locked.IsValid)
            {
                _locked = null;
            }
        }
    }
}
