using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InspectModeState
    {
        public const string InspectModeKey = "InspectMode";

        private static InspectableObjectInfo _hovered;
        private static InspectableObjectInfo _locked;
        private static bool _inspectModeEnabled;
        private static bool _suppressHoverUntilPointerMoves;
        private static Point _lastCursorPosition;

        public static bool InspectModeEnabled => _inspectModeEnabled;
        public static bool IsNonMetaSuppressed => _inspectModeEnabled;
        public static bool HasLockedTarget => _locked != null;

        public static void ApplyInspectModeState(bool enabled)
        {
            if (_inspectModeEnabled == enabled)
            {
                return;
            }

            _inspectModeEnabled = enabled;

            if (!_inspectModeEnabled)
            {
                _hovered = null;
                _suppressHoverUntilPointerMoves = true;
                _lastCursorPosition = Mouse.GetState().Position;
            }
            else
            {
                _suppressHoverUntilPointerMoves = false;
            }
        }

        public static void UpdateHovered(InspectableObjectInfo hovered, Point cursorPosition, bool allowNullOverride = true)
        {
            if (_suppressHoverUntilPointerMoves)
            {
                if (cursorPosition == _lastCursorPosition)
                {
                    if (allowNullOverride)
                    {
                        _hovered = null;
                    }
                    return;
                }

                _suppressHoverUntilPointerMoves = false;
            }

            _lastCursorPosition = cursorPosition;

            if (!allowNullOverride && hovered == null && _hovered != null)
            {
                return;
            }

            _hovered = hovered;
        }

        public static void LockHovered()
        {
            if (!_inspectModeEnabled || _hovered == null || !_hovered.IsValid)
            {
                return;
            }

            LockTarget(_hovered);
        }

        public static void LockTarget(InspectableObjectInfo target)
        {
            if (target == null || !target.IsValid)
            {
                return;
            }

            _locked = target.Clone();
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
        }

        public static bool IsTargetLocked(InspectableObjectInfo target)
        {
            if (target == null || _locked == null)
            {
                return false;
            }

            if (_locked.Source != null && target.Source != null)
            {
                return ReferenceEquals(_locked.Source, target.Source);
            }

            return _locked.Id == target.Id && target.Id != 0;
        }
    }
}
