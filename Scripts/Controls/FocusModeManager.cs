using System;
using System.Collections.Generic;

namespace op.io
{
    /// <summary>
    /// Tracks whether any text input owns focus mode; when active, gameplay keybinds should be suppressed.
    /// </summary>
    public static class FocusModeManager
    {
        private const string DefaultOwner = "default";
        private static readonly HashSet<string> _owners = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsFocusModeActive => _owners.Count > 0;

        public static void SetFocusActive(string ownerId, bool isActive)
        {
            if (isActive)
            {
                Enable(ownerId);
            }
            else
            {
                Disable(ownerId);
            }
        }

        public static void Enable(string ownerId)
        {
            _owners.Add(NormalizeOwner(ownerId));
        }

        public static void Disable(string ownerId)
        {
            _owners.Remove(NormalizeOwner(ownerId));
        }

        public static void ClearAll()
        {
            _owners.Clear();
        }

        private static string NormalizeOwner(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? DefaultOwner : ownerId.Trim();
        }
    }
}
