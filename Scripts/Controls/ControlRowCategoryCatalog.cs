using System;
using System.Collections.Generic;

namespace op.io
{
    internal readonly struct ControlRowCategoryDefinition
    {
        public string Key { get; }
        public string Label { get; }
        public int DefaultOrder { get; }

        public ControlRowCategoryDefinition(string key, string label, int defaultOrder)
        {
            Key = key ?? string.Empty;
            Label = label ?? key ?? string.Empty;
            DefaultOrder = Math.Max(0, defaultOrder);
        }
    }

    internal static class ControlRowCategoryCatalog
    {
        public const string MovementCategoryKey = "Movement";
        public const string CombatCategoryKey = "Combat";
        public const string CameraCategoryKey = "Camera";
        public const string InterfaceCategoryKey = "Interface";
        public const string BlockControlsCategoryKey = "BlockControls";
        public const string SystemCategoryKey = "System";
        public const string MiscCategoryKey = "Misc";

        private static readonly ControlRowCategoryDefinition[] _orderedCategories =
        [
            new(MovementCategoryKey, "Movement", 0),
            new(CombatCategoryKey, "Combat", 1),
            new(CameraCategoryKey, "Camera", 2),
            new(InterfaceCategoryKey, "Interface", 3),
            new(BlockControlsCategoryKey, "Block controls", 4),
            new(SystemCategoryKey, "System", 5),
            new(MiscCategoryKey, "Misc", 6),
        ];

        private static readonly Dictionary<string, ControlRowCategoryDefinition> _categoriesByKey = BuildCategoryLookup();
        private static readonly Dictionary<string, string> _categoriesBySettingKey = BuildSettingLookup();

        public static IReadOnlyList<ControlRowCategoryDefinition> OrderedCategories => _orderedCategories;

        public static string NormalizeCategoryKey(string categoryKey, string settingKey = null)
        {
            if (!string.IsNullOrWhiteSpace(categoryKey))
            {
                string trimmed = categoryKey.Trim();
                if (_categoriesByKey.TryGetValue(trimmed, out ControlRowCategoryDefinition category))
                {
                    return category.Key;
                }

                foreach (ControlRowCategoryDefinition definition in _orderedCategories)
                {
                    if (string.Equals(definition.Label, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return definition.Key;
                    }
                }
            }

            return GetDefaultCategoryKey(settingKey);
        }

        public static string GetDefaultCategoryKey(string settingKey)
        {
            if (string.Equals(settingKey, ControlKeyMigrations.BlockMenuKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settingKey, ControlKeyMigrations.LegacyPanelMenuKey, StringComparison.OrdinalIgnoreCase) ||
                ControlKeyRules.IsGeneratedBlockVisibilityControlKey(settingKey))
            {
                return BlockControlsCategoryKey;
            }

            if (!string.IsNullOrWhiteSpace(settingKey) &&
                _categoriesBySettingKey.TryGetValue(settingKey.Trim(), out string category))
            {
                return category;
            }

            return MiscCategoryKey;
        }

        public static int GetDefaultCategoryOrder(string categoryKey)
        {
            string normalized = NormalizeCategoryKey(categoryKey);
            return _categoriesByKey.TryGetValue(normalized, out ControlRowCategoryDefinition category)
                ? category.DefaultOrder
                : _categoriesByKey[MiscCategoryKey].DefaultOrder;
        }

        public static string GetCategoryLabel(string categoryKey)
        {
            string normalized = NormalizeCategoryKey(categoryKey);
            return _categoriesByKey.TryGetValue(normalized, out ControlRowCategoryDefinition category)
                ? category.Label
                : normalized;
        }

        private static Dictionary<string, ControlRowCategoryDefinition> BuildCategoryLookup()
        {
            var lookup = new Dictionary<string, ControlRowCategoryDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (ControlRowCategoryDefinition category in _orderedCategories)
            {
                lookup[category.Key] = category;
            }

            return lookup;
        }

        private static Dictionary<string, string> BuildSettingLookup()
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Movement
                ["MoveUp"] = MovementCategoryKey,
                ["MoveDown"] = MovementCategoryKey,
                ["MoveLeft"] = MovementCategoryKey,
                ["MoveRight"] = MovementCategoryKey,
                ["MoveTowardsCursor"] = MovementCategoryKey,
                ["MoveAwayFromCursor"] = MovementCategoryKey,
                ["Sprint"] = MovementCategoryKey,
                ["Crouch"] = MovementCategoryKey,
                ["ReturnCursorToPlayer"] = MovementCategoryKey,

                // Combat
                ["Fire"] = CombatCategoryKey,
                ["BarrelLeft"] = CombatCategoryKey,
                ["BarrelRight"] = CombatCategoryKey,
                [ControlKeyMigrations.BodyLeftKey] = CombatCategoryKey,
                [ControlKeyMigrations.BodyRightKey] = CombatCategoryKey,
                [ControlKeyMigrations.CombatTextKey] = CombatCategoryKey,
                [ControlKeyMigrations.RespawnKey] = CombatCategoryKey,

                // Camera
                [ControlKeyMigrations.CameraLockModeKey] = CameraCategoryKey,
                [ControlKeyMigrations.CameraSnapToPlayerKey] = CameraCategoryKey,
                [ControlKeyMigrations.ScrollInKey] = CameraCategoryKey,
                [ControlKeyMigrations.ScrollOutKey] = CameraCategoryKey,
                [ControlKeyMigrations.ScrollMinDistanceKey] = CameraCategoryKey,
                [ControlKeyMigrations.ScrollMaxDistanceKey] = CameraCategoryKey,
                [ControlKeyMigrations.ScrollIncrementKey] = CameraCategoryKey,
                [ControlKeyMigrations.CtrlBufferKey] = CameraCategoryKey,

                // Interface
                [ControlKeyMigrations.BlockMenuKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.LegacyPanelMenuKey] = InterfaceCategoryKey,
                ["DockingMode"] = InterfaceCategoryKey,
                ["TransparentTabBlocking"] = InterfaceCategoryKey,
                [InspectModeState.InspectModeKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.AutoTurnInspectModeOffKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.TabSwitchRequiresBlockModeKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.DisableToolTipsKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.ShowHiddenAttrsKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.GridKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.OceanZoneDebugKey] = InterfaceCategoryKey,
                [ControlKeyMigrations.YourBarKey] = InterfaceCategoryKey,

                // System
                ["Exit"] = SystemCategoryKey,
                ["DebugMode"] = SystemCategoryKey,
                [ControlKeyMigrations.AllowGameInputFreezeKey] = SystemCategoryKey,
                [ControlKeyMigrations.HoldInputsKey] = SystemCategoryKey,
                [ControlKeyMigrations.PreviousConfigurationKey] = SystemCategoryKey,
                [ControlKeyMigrations.NextConfigurationKey] = SystemCategoryKey,
            };

            return lookup;
        }
    }
}
