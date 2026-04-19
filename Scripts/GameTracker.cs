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
        public static bool DisableToolTips      => ControlStateManager.ContainsSwitchState(ControlKeyMigrations.DisableToolTipsKey) &&
                                                   ControlStateManager.GetSwitchState(ControlKeyMigrations.DisableToolTipsKey);
        public static bool BlockMenuOpen        => BlockManager.IsBlockMenuOpen();
        public static bool InputBlocked         => BlockManager.IsInputBlocked();
        public static bool DraggingLayout       => BlockManager.IsDraggingLayout;
        public static bool SuperimposeLocked    => BlockManager.IsSuperimposeLocked;
        public static bool CursorOnGameBlock    => BlockManager.IsCursorWithinGameBlock();
        public static bool NativeWindowResizeEdges => ScreenManager.NativeWindowResizeEdgesEnabled;
        public static bool CustomDockingResizeEdges => ScreenManager.CustomDockingResizeEdgesEnabled;
        public static bool DraggingWindowResize => BlockManager.IsDraggingWindowResize;
        public static string HoveredBlock       => BlockManager.GetHoveredBlockKind();
        public static string HoveredDragBar     => BlockManager.GetHoveredDragBarKind();
        public static string SuperimposeLockTarget => BlockManager.GetSuperimposeLockTargetKind();
        public static string FocusedBlock       => BlockManager.GetFocusedBlockKind()?.ToString() ?? "None";
        public static bool   AnyGUIInteracting  => BlockManager.IsAnyGuiInteracting;
        public static string GUIInteractingWith => BlockManager.GetInteractingBlockKind();
        public static float DoubleTapSuppressionSeconds => InputTypeManager.DoubleTapSuppressionSeconds;

        /// <summary>
        /// Shows the DockBlockCategory (Standard / Overlay / Dynamic) of the
        /// currently hovered or focused block so developers can inspect block types
        /// at runtime.
        /// </summary>
        public static string BlockType => BlockManager.GetFocusedBlockCategory();
        public static string EnumDisabledOptions => ControlStateManager.GetAllEnumDisabledOptionsSummary();

        // ── Constants (from MathBlock) ────────────────────────────────────────
        // Bullet Physics (DB-driven)
        public static float AirResistanceScalar     => BulletManager.AirResistanceScalar;
        public static float BounceVelocityLoss      => BulletManager.BounceVelocityLoss;
        public static float HitVelocityLoss         => BulletManager.HitVelocityLoss;
        public static float PenetrationSpring       => BulletManager.PenetrationSpringCoeff;
        public static float PenetrationDamping      => BulletManager.PenetrationDamping;
        // Scalars (DB-driven)
        public static float BulletRadiusScalar          => BulletManager.BulletRadiusScalar;
        public static float BarrelHeightScalar          => BulletManager.BarrelHeightScalar;
        public static float BulletKnockbackScalar       => BulletManager.BulletKnockbackScalar;
        public static float BulletRecoilScalar          => BulletManager.BulletRecoilScalar;
        public static float BulletFarmKnockbackScalar   => BulletManager.BulletFarmKnockbackScalar;
        public static float OwnerImmunityDuration       => BulletManager.OwnerImmunityDuration;
        // Bullet Defaults (DB-driven)
        public static float DefaultBulletSpeed      => BulletManager.DefaultBulletSpeed;
        public static float DefaultBulletLifespan   => BulletManager.DefaultBulletLifespan;
        public static float DefaultDragFactor       => BulletManager.DefaultBulletDragFactor;
        public static float DefaultBulletMass       => BulletManager.DefaultBulletMass;
        public static float DefaultBulletDamage     => BulletManager.DefaultBulletDamage;
        public static float DefaultBulletPenHP      => BulletManager.DefaultBulletHealth;
        // Physics (DB-driven)
        public static float PhysicsFrictionRate     => PhysicsManager.FrictionRate;
        // Code-defined constants
        public static string AngularAccelFactor     => "4";
        public static string BarrelSwitchSpeed      => "15 /s";
        public static int    ActiveBodyIndex       => Core.Instance?.Player?.ActiveBodyIndex ?? 0;
        public static string ActiveBodyName        => Core.Instance?.Player is Agent p && p.BodyCount > 0
            ? (p.Bodies[p.ActiveBodyIndex].Name ?? $"Body {p.ActiveBodyIndex + 1}")
            : "None";

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

                // Default bullet settings are static DB config, not runtime state — hide from Backend block.
                if (property.Name.StartsWith("Default", StringComparison.Ordinal))
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
