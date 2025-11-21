using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        private const string AllowGameInputFreezeKey = "AllowGameInputFreeze";

        // Dictionary to store control key mappings (keyboard and mouse, including combos)
        private static readonly Dictionary<string, ControlBinding> _controlBindings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _cachedSpeedMultipliers = [];
        private static bool _isControlKeyLoaded = false;
        private static readonly Dictionary<string, Keys[]> _modifierAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Shift"] = new[] { Keys.LeftShift, Keys.RightShift },
            ["Ctrl"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Control"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Alt"] = new[] { Keys.LeftAlt, Keys.RightAlt }
        };

        // This will be set during initialization to avoid unnecessary database calls.
        static InputManager()
        {
            LoadControlKey();
        }

        private static void LoadControlKey()
        {
            if (_isControlKeyLoaded) return;

            ControlKeyMigrations.EnsureApplied();

            var controls = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType, MetaControl FROM ControlKey;");
            foreach (var control in controls)
            {
                string settingKey = control["SettingKey"].ToString();
                string inputKey = control["InputKey"].ToString();
                InputType inputType = Enum.Parse<InputType>(control["InputType"].ToString());
                bool isMetaControl = false;
                if (control.TryGetValue("MetaControl", out object metaValue) && metaValue != null && metaValue != DBNull.Value)
                {
                    try
                    {
                        isMetaControl = Convert.ToInt32(metaValue) != 0;
                    }
                    catch
                    {
                        isMetaControl = false;
                    }
                }

                if (ControlKeyRules.RequiresSwitchSemantics(settingKey) && inputType != InputType.Switch)
                {
                    inputType = InputType.Switch;
                }

                if (TryCreateBinding(settingKey, inputKey, inputType, isMetaControl, out ControlBinding binding))
                {
                    _controlBindings[settingKey] = binding;
                }
                else
                {
                    DebugLogger.PrintError($"Failed to parse input key '{inputKey}' for '{settingKey}'.");
                }
            }

            if (!_controlBindings.ContainsKey("PanelMenu") &&
                TryCreateBinding("PanelMenu", "Shift + X", InputType.Switch, true, out ControlBinding defaultBinding))
            {
                _controlBindings["PanelMenu"] = defaultBinding;
            }

            _isControlKeyLoaded = true;
        }

        // Retrieve speed multiplier values from the cache or the database
        private static float GetCachedMultiplier(string settingKey, string databaseKey)
        {
            if (_cachedSpeedMultipliers.ContainsKey(settingKey))
            {
                return _cachedSpeedMultipliers[settingKey];
            }

            float multiplier = DatabaseFetch.GetValue<float>("ControlSettings", "Value", "SettingKey", databaseKey);
            _cachedSpeedMultipliers[settingKey] = multiplier;
            return multiplier;
        }

        // Calculate the movement vector based on input keys
        public static Vector2 GetMoveVector()
        {
            KeyboardState state = Keyboard.GetState();
            Vector2 direction = Vector2.Zero;

            if (!(IsInputActive("MoveTowardsCursor") && IsInputActive("MoveAwayFromCursor")) && (IsInputActive("MoveTowardsCursor") || IsInputActive("MoveAwayFromCursor")))
            {
                float rotation = Core.Instance.Player.Rotation;
                if (IsInputActive("MoveTowardsCursor"))
                {
                    direction.X += MathF.Cos(rotation);
                    direction.Y += MathF.Sin(rotation);
                }
                if (IsInputActive("MoveAwayFromCursor"))
                {
                    direction.X -= MathF.Cos(rotation);
                    direction.Y -= MathF.Sin(rotation);
                }
            }
            else
            {
                if (IsInputActive("MoveUp")) direction.Y -= 1;
                if (IsInputActive("MoveDown")) direction.Y += 1;
                if (IsInputActive("MoveLeft")) direction.X -= 1;
                if (IsInputActive("MoveRight")) direction.X += 1;
            }

            if (direction.LengthSquared() > 0)
                direction.Normalize();

            return direction;
        }

        // Speed multiplier considering sprint and crouch inputs
        public static float SpeedMultiplier()
        {
            float multiplier = 1f;

            if (IsInputActive("Sprint"))
            {
                multiplier *= GetCachedMultiplier("Sprint", "SprintSpeedMultiplier");
            }
            else if (IsInputActive("Crouch"))
            {
                multiplier *= GetCachedMultiplier("Crouch", "CrouchSpeedMultiplier");
            }

            return multiplier;
        }

        // Check if a specific input action is active
        public static bool IsInputActive(string settingKey)
        {
            return _controlBindings.TryGetValue(settingKey, out ControlBinding binding) && binding.IsActive();
        }

        public static string GetBindingDisplayLabel(string settingKey)
        {
            if (_controlBindings.TryGetValue(settingKey, out ControlBinding binding))
            {
                return binding.DisplayLabel;
            }

            return string.Empty;
        }

        private static bool TryCreateBinding(string settingKey, string inputKey, InputType inputType, bool isMetaControl, out ControlBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(inputKey))
            {
                return false;
            }

            string[] rawTokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            List<InputBindingToken> tokens = new();

            foreach (string rawToken in rawTokens)
            {
                string token = rawToken.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (TryCreateMouseToken(token, out InputBindingToken mouseToken))
                {
                    tokens.Add(mouseToken);
                    continue;
                }

                if (TryCreateKeyToken(token, out InputBindingToken keyToken))
                {
                    tokens.Add(keyToken);
                    continue;
                }

                DebugLogger.PrintError($"Unsupported input token '{token}' for control '{settingKey}'.");
                return false;
            }

            if (tokens.Count == 0)
            {
                DebugLogger.PrintError($"Control '{settingKey}' has no valid tokens.");
                return false;
            }

            string displayLabel = string.Join(" + ", tokens.Select(t => t.DisplayName));
            binding = new ControlBinding(settingKey, inputType, tokens, displayLabel, isMetaControl);
            return true;
        }

        private static bool TryCreateMouseToken(string token, out InputBindingToken bindingToken)
        {
            bindingToken = default;

            if (string.Equals(token, "LeftClick", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "RightClick", StringComparison.OrdinalIgnoreCase))
            {
                bindingToken = InputBindingToken.CreateMouse(token);
                return true;
            }

            return false;
        }

        private static bool TryCreateKeyToken(string token, out InputBindingToken bindingToken)
        {
            bindingToken = default;

            if (_modifierAliases.TryGetValue(token, out Keys[] aliasKeys))
            {
                bindingToken = InputBindingToken.CreateKeyboard(aliasKeys, NormalizeLabel(token));
                return true;
            }

            if (Enum.TryParse(token, true, out Keys parsedKey))
            {
                bindingToken = InputBindingToken.CreateKeyboard(new[] { parsedKey }, NormalizeLabel(token));
                return true;
            }

            return false;
        }

        private static string NormalizeLabel(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            token = token.Trim();
            if (token.Length <= 1)
            {
                return token.ToUpperInvariant();
            }

            return char.ToUpperInvariant(token[0]) + token[1..];
        }

        private sealed class ControlBinding
        {
            public ControlBinding(string settingKey, InputType inputType, IReadOnlyList<InputBindingToken> tokens, string displayLabel, bool isMetaControl)
            {
                SettingKey = settingKey;
                InputType = inputType;
                Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
                DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? string.Join(" + ", tokens) : displayLabel;
                IsMetaControl = isMetaControl;
            }

            public string SettingKey { get; }
            public InputType InputType { get; }
            public IReadOnlyList<InputBindingToken> Tokens { get; }
            public string DisplayLabel { get; }
            public bool IsMetaControl { get; }

            public bool IsActive()
            {
                if (Tokens == null || Tokens.Count == 0)
                {
                    return false;
                }

                if (!ShouldAllowBinding(IsMetaControl))
                {
                    return false;
                }

            bool modifiersHeld = true;
            bool hasModifiers = Tokens.Count > 1;
            int modifierCount = Math.Max(0, Tokens.Count - 1);


            for (int i = 0; i < modifierCount; i++)
            {
                if (!Tokens[i].IsHeld())
                    {
                        modifiersHeld = false;
                        if (InputType != InputType.Switch)
                        {
                            return false;
                        }
                        break;
                    }
                }

                InputBindingToken primary = Tokens[^1];

                return InputType switch
                {
                    InputType.Hold => modifiersHeld && primary.IsHeld(),
                    InputType.Trigger => modifiersHeld && primary.IsTriggered(),
                    InputType.Switch => EvaluateSwitchState(primary, modifiersHeld, hasModifiers),
                    _ => false
                };
            }

            private bool EvaluateSwitchState(InputBindingToken primary, bool modifiersHeld, bool hasModifiers)
            {
                // If this binding expects modifiers and they aren't held, don't toggle or peek; just return cached state.
                if (hasModifiers && !modifiersHeld)
                {
                    if (ControlStateManager.ContainsSwitchState(SettingKey))
                    {
                        return ControlStateManager.GetSwitchState(SettingKey);
                    }

                    return false;
                }

                if (modifiersHeld)
                {
                    return primary.IsSwitch();
                }

                bool peekState = primary.PeekSwitchState();
                if (peekState)
                {
                    return true;
                }

                if (ControlStateManager.ContainsSwitchState(SettingKey))
                {
                    return ControlStateManager.GetSwitchState(SettingKey);
                }

                return false;
            }

        }

        private static bool ShouldAllowBinding(bool isMetaControl)
        {
            if (isMetaControl)
            {
                return true;
            }

            return !ShouldSuppressNonMetaControls();
        }

        private static bool ShouldSuppressNonMetaControls()
        {
            if (Core.Instance == null)
            {
                GameTracker.FreezeGameInputs = false;
                return false;
            }

            if (!IsGameInputFreezeAllowed())
            {
                GameTracker.FreezeGameInputs = false;
                return false;
            }

            bool shouldFreeze = !BlockManager.IsCursorWithinGamePanel();
            GameTracker.FreezeGameInputs = shouldFreeze;
            return shouldFreeze;
        }

        private static bool IsGameInputFreezeAllowed()
        {
            if (ControlStateManager.ContainsSwitchState(AllowGameInputFreezeKey))
            {
                return ControlStateManager.GetSwitchState(AllowGameInputFreezeKey);
            }

            return true;
        }

        private readonly struct InputBindingToken
        {
            private InputBindingToken(IReadOnlyList<Keys> keys, string mouseButton, string displayName)
            {
                Keys = keys;
                MouseButton = mouseButton;
                DisplayName = displayName;
            }

            public IReadOnlyList<Keys> Keys { get; }
            public string MouseButton { get; }
            public string DisplayName { get; }
            public bool IsMouse => !string.IsNullOrWhiteSpace(MouseButton);

            public static InputBindingToken CreateKeyboard(IReadOnlyList<Keys> keys, string label) =>
                new(keys ?? throw new ArgumentNullException(nameof(keys)), null, label);

            public static InputBindingToken CreateMouse(string mouseButton) =>
                new(null, mouseButton, NormalizeMouseLabel(mouseButton));

            public bool IsHeld()
            {
                if (IsMouse)
                {
                    return InputTypeManager.IsMouseButtonHeld(MouseButton);
                }

                foreach (Keys key in Keys)
                {
                    if (InputTypeManager.IsKeyHeld(key))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool IsTriggered()
            {
                if (IsMouse)
                {
                    return InputTypeManager.IsMouseButtonTriggered(MouseButton);
                }

                foreach (Keys key in Keys)
                {
                    if (InputTypeManager.IsKeyTriggered(key))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool IsSwitch()
            {
                if (IsMouse)
                {
                    return InputTypeManager.IsMouseButtonSwitch(MouseButton);
                }

                foreach (Keys key in Keys)
                {
                    if (InputTypeManager.IsKeySwitch(key))
                    {
                        return true;
                    }
                }

                return false;
            }

            public override string ToString() => DisplayName ?? MouseButton ?? string.Empty;

            private static string NormalizeMouseLabel(string mouseButton)
            {
                if (string.IsNullOrWhiteSpace(mouseButton))
                {
                    return string.Empty;
                }

                if (mouseButton.Equals("LeftClick", StringComparison.OrdinalIgnoreCase))
                {
                    return "Left Click";
                }

                if (mouseButton.Equals("RightClick", StringComparison.OrdinalIgnoreCase))
                {
                    return "Right Click";
                }

                return mouseButton;
            }

            public bool PeekSwitchState()
            {
                if (IsMouse)
                {
                    return InputTypeManager.PeekMouseSwitchState(MouseButton);
                }

                foreach (Keys key in Keys)
                {
                    if (InputTypeManager.PeekKeySwitchState(key))
                    {
                        return true;
                    }
                }

                return false;
            }

        }

    }
}
