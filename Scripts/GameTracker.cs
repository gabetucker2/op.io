using System;
using System.Collections.Generic;
using System.Reflection;

namespace op.io
{
    public static class GameTracker
    {
        private const string AllowGameInputFreezeKey = "AllowGameInputFreeze";
        private static readonly Dictionary<string, Func<bool>> _variableDependencies = new(StringComparer.OrdinalIgnoreCase)
        {
            ["FreezeGameInputs"] = IsAllowGameInputFreezeEnabled
        };

        public static bool FreezeGameInputs { get; internal set; }

        public static IReadOnlyList<GameTrackerVariable> GetTrackedVariables()
        {
            List<GameTrackerVariable> variables = new();
            Type trackerType = typeof(GameTracker);

            foreach (PropertyInfo property in trackerType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (!property.CanRead)
                {
                    continue;
                }

                if (!ShouldIncludeVariable(property.Name))
                {
                    continue;
                }

                object value;
                try
                {
                    value = property.GetValue(null);
                }
                catch
                {
                    continue;
                }

                variables.Add(new GameTrackerVariable(property.Name, value));
            }

            foreach (FieldInfo field in trackerType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!ShouldIncludeVariable(field.Name))
                {
                    continue;
                }

                object value = field.GetValue(null);
                variables.Add(new GameTrackerVariable(field.Name, value));
            }

            variables.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            return variables;
        }

        private static bool ShouldIncludeVariable(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return true;
            }

            if (_variableDependencies.TryGetValue(variableName, out Func<bool> dependency))
            {
                return dependency();
            }

            return true;
        }

        private static bool IsAllowGameInputFreezeEnabled()
        {
            if (!ControlStateManager.ContainsSwitchState(AllowGameInputFreezeKey))
            {
                return true;
            }

            return ControlStateManager.GetSwitchState(AllowGameInputFreezeKey);
        }

        public readonly struct GameTrackerVariable
        {
            public GameTrackerVariable(string name, object value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public object Value { get; }
            public bool IsBoolean => Value is bool;
        }
    }
}
