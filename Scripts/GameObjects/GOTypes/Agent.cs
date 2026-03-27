using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public class Agent : GameObject
    {
        // Agent-specific properties
        public float TriggerCooldown { get; set; }
        public float SwitchCooldown { get; set; }

        private int GetMode(int _mode, string modeSettingName)
        {
            if (_mode == -1)
            {
                if (ControlStateManager.ContainsSwitchState(modeSettingName)) // If it exists as a switch state
                {
                    _crouchMode = TypeConversionFunctions.BoolToInt(ControlStateManager.GetSwitchState(modeSettingName));
                }
                else // Else it's probably Hold, just default it to 0
                {
                    _crouchMode = 0;
                }
            }

            return _mode;
        }

        private int _crouchMode = -1; // -1 = Not initialized, 0 = False, 1 = True
        public bool IsCrouching
        {
            get
            {
                return TypeConversionFunctions.IntToBool(GetMode(_crouchMode, "Crouch"));
            }
            set
            {
                _crouchMode = TypeConversionFunctions.BoolToInt(value);
            }
        }

        private int _sprintMode = -1; // -1 = Not initialized, 0 = False, 1 = True
        public bool IsSprinting
        {
            get
            {
                return TypeConversionFunctions.IntToBool(GetMode(_sprintMode, "Sprint"));
            }
            set
            {
                _sprintMode = TypeConversionFunctions.BoolToInt(value);
            }
        }

        public bool IsPlayer { get; private set; }
        public long PlayerID { get; set; }
        public Attributes_Barrel BarrelAttributes { get; set; }
        public Attributes_Body BodyAttributes { get; set; }
        public Attributes_Unit UnitAttributes { get; set; }
        public Shape BarrelShape { get; private set; }
        public GameObject BarrelObject { get; private set; }

        private float _baseSpeed;

        // Static variables for cached cooldown values
        private static float? cachedTriggerCooldown = null;
        private static float? cachedSwitchCooldown = null;

        public float BaseSpeed
        {
            get => _baseSpeed;
            set
            {
                _baseSpeed = value;
                if (value < 0)
                {
                    DebugLogger.PrintWarning($"BaseSpeed updated to negative value: {value}");
                }
            }
        }

        // Calculate the effective speed based on crouching/sprinting
        public float Speed
        {
            get
            {
                return BaseSpeed * InputManager.SpeedMultiplier(); // Use InputManager to handle the multiplier
            }
        }

        // Constructor that initializes the Agent by calling the base GameObject constructor
        public Agent(
            int id,
            string name,
            string type,
            Vector2 position,
            float rotation,
            float mass,
            bool isDestructible,
            bool isCollidable,
            bool staticPhysics,
            Shape shape,
            float baseSpeed,
            bool isPlayer,
            Color fillColor,
            Color outlineColor,
            int outlineWidth,
            Attributes_Barrel barrelAttributes = default,
            Attributes_Body bodyAttributes = default
        )
            : base(id, name, type, position, rotation, mass, isDestructible, isCollidable, staticPhysics, shape, fillColor, outlineColor, outlineWidth)
        {
            TriggerCooldown = 0;
            SwitchCooldown = 0;
            IsCrouching = false;
            IsSprinting = false;
            IsPlayer = isPlayer;
            BaseSpeed = baseSpeed;
            BarrelAttributes = barrelAttributes;
            BodyAttributes = bodyAttributes;

            // Build barrel shape: height = bodyRadius*2/5, length = 4*bodyRadius
            int bodyRadius = shape.Width / 2;
            int bw = Math.Max(1, bodyRadius * 4 / 5);
            int bl = bodyRadius * 2;
            BarrelShape = new Shape("Rectangle", bl, bw, 0,
                new Color(192, 192, 192),
                new Color(64, 64, 64),
                1)
            {
                SkipHover = true
            };

            // Register barrel as a child for parent/child tracking.
            // Position stores the relative offset from this agent center (forward = +X at rotation 0),
            // matching the halfLength offset used in ShapeManager.DrawShapes.
            // isPrototype=true prevents registration in GameObjectRegister, so ShapeManager
            // won't draw it through the standard path — the existing barrel draw logic still handles it.
            float halfBarrelLength = bl / 2f;
            BarrelObject = new GameObject(
                id, "Barrel", "Barrel",
                new Vector2(halfBarrelLength, 0f), 0f,
                0f, false, false, true,
                BarrelShape,
                new Color(192, 192, 192), new Color(64, 64, 64), 1,
                isPrototype: true);
            AddChild(BarrelObject);

            // Log agent creation without loading cooldowns
            DebugLogger.PrintPlayer($"Agent created with TriggerCooldown: {TriggerCooldown}, SwitchCooldown: {SwitchCooldown}");
        }

        // Load TriggerCooldown value from the database or cache it
        public void LoadTriggerCooldown()
        {
            if (!cachedTriggerCooldown.HasValue)
            {
                TriggerCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "TriggerCooldown");
                if (TriggerCooldown == 0)
                {
                    DebugLogger.PrintError("TriggerCooldown is 0 after loading from the database.");
                }
                DebugLogger.PrintPlayer($"TriggerCooldown loaded: {TriggerCooldown}");
                cachedTriggerCooldown = TriggerCooldown;
            }
            else
            {
                TriggerCooldown = cachedTriggerCooldown.Value;
                DebugLogger.PrintPlayer($"Loaded from cache: TriggerCooldown: {TriggerCooldown}");
            }
        }

        // Load SwitchCooldown value from the database or cache it
        public void LoadSwitchCooldown()
        {
            if (!cachedSwitchCooldown.HasValue)
            {
                SwitchCooldown = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", "SwitchCooldown");
                if (SwitchCooldown == 0)
                {
                    DebugLogger.PrintError("SwitchCooldown is 0 after loading from the database.");
                }
                DebugLogger.PrintPlayer($"SwitchCooldown loaded: {SwitchCooldown}");
                cachedSwitchCooldown = SwitchCooldown;
            }
            else
            {
                SwitchCooldown = cachedSwitchCooldown.Value;
                DebugLogger.PrintPlayer($"Loaded from cache: SwitchCooldown: {SwitchCooldown}");
            }
        }

        // Update method to manage cooldowns
        public override void Update()
        {
            // Decrease cooldowns only if greater than 0
            if (TriggerCooldown > 0)
            {
                TriggerCooldown -= Core.DELTATIME;
            }

            if (SwitchCooldown > 0)
            {
                SwitchCooldown -= Core.DELTATIME;
            }

            // Log cooldowns after update to confirm they are being handled
            //DebugLogger.PrintPlayer($"TriggerCooldown: {TriggerCooldown}, SwitchCooldown: {SwitchCooldown}");
        }
    }
}
