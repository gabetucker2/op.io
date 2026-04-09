using System;
using System.Collections.Generic;
using System.Reflection;

namespace op.io
{
    public static class GameTracker
    {
        public static bool FreezeGameInputs { get; internal set; }

        /// <summary>
        /// Explains why FreezeGameInputs is currently true. Set by InputManager
        /// whenever it decides to suppress non-meta controls. Shown as a detail
        /// message in the Backend block so the cause is always visible.
        /// </summary>
        public static string FreezeGameInputsReason { get; internal set; } = string.Empty;

        // UI state — read live from BlockManager
        public static bool DockingMode          => BlockManager.DockingModeEnabled;
        public static bool BlockMenuOpen        => BlockManager.IsBlockMenuOpen();
        public static bool InputBlocked         => BlockManager.IsInputBlocked();
        public static bool DraggingLayout       => BlockManager.IsDraggingLayout;
        public static bool CursorOnGameBlock    => BlockManager.IsCursorWithinGameBlock();
        public static string HoveredBlock       => BlockManager.GetHoveredBlockKind();
        public static string HoveredDragBar     => BlockManager.GetHoveredDragBarKind();
        public static string FocusedBlock       => BlockManager.GetFocusedBlockKind()?.ToString() ?? "None";
        public static bool   AnyGUIInteracting  => BlockManager.IsAnyGuiInteracting;
        public static string GUIInteractingWith => BlockManager.GetInteractingBlockKind();

        public static IReadOnlyList<GameTrackerVariable> GetTrackedVariables()
        {
            List<GameTrackerVariable> variables = new();
            Type trackerType = typeof(GameTracker);

            foreach (PropertyInfo property in trackerType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (!property.CanRead) continue;

                // Skip FreezeGameInputsReason — it's surfaced as the Detail of FreezeGameInputs instead.
                if (string.Equals(property.Name, nameof(FreezeGameInputsReason), StringComparison.OrdinalIgnoreCase))
                    continue;

                object value;
                try { value = property.GetValue(null); }
                catch { continue; }

                string detail = string.Equals(property.Name, nameof(FreezeGameInputs), StringComparison.OrdinalIgnoreCase)
                    ? FreezeGameInputsReason
                    : string.Empty;

                variables.Add(new GameTrackerVariable(property.Name, value, detail));
            }

            foreach (FieldInfo field in trackerType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object value = field.GetValue(null);
                variables.Add(new GameTrackerVariable(field.Name, value));
            }

            variables.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            return variables;
        }

        public readonly struct GameTrackerVariable
        {
            public GameTrackerVariable(string name, object value, string detail = null)
            {
                Name   = name;
                Value  = value;
                Detail = detail ?? string.Empty;
            }

            public string Name      { get; }
            public object Value     { get; }
            /// <summary>Optional detail/reason message shown in the Backend message column.</summary>
            public string Detail    { get; }
            public bool   IsBoolean => Value is bool;
        }
    }
}
