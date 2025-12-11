using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class InputManager
    {
        private const string BlockMenuKey = ControlKeyMigrations.BlockMenuKey;
        private const string AllowGameInputFreezeKey = "AllowGameInputFreeze";

        // Dictionary to store control key mappings (keyboard and mouse, including combos)
        private static readonly Dictionary<string, ControlBinding> _controlBindings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> _cachedSpeedMultipliers = [];
        private static readonly Dictionary<string, bool> _triggerOverrides = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _latchedHoldControls = new(StringComparer.OrdinalIgnoreCase);
        private static bool _holdLatchEnabled;
        private static float _holdLatchRotation;
        internal static bool IsHoldLatchActive => _holdLatchEnabled;
        private static bool _isControlKeyLoaded = false;
        private static readonly Dictionary<string, Keys[]> _modifierAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Shift"] = new[] { Keys.LeftShift, Keys.RightShift },
            ["Ctrl"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Control"] = new[] { Keys.LeftControl, Keys.RightControl },
            ["Alt"] = new[] { Keys.LeftAlt, Keys.RightAlt }
        };

        private static bool IsSwitchType(InputType inputType) =>
            inputType == InputType.SaveSwitch || inputType == InputType.NoSaveSwitch;

        private static InputType ParseInputType(string inputTypeLabel)
        {
            if (string.IsNullOrWhiteSpace(inputTypeLabel))
            {
                return InputType.Hold;
            }

            if (string.Equals(inputTypeLabel, "Switch", StringComparison.OrdinalIgnoreCase))
            {
                return InputType.SaveSwitch;
            }

            return Enum.TryParse(inputTypeLabel, true, out InputType parsed) ? parsed : InputType.Hold;
        }

        // This will be set during initialization to avoid unnecessary database calls.
        static InputManager()
        {
            LoadControlKey();
        }

        private static void LoadControlKey()
        {
            if (_isControlKeyLoaded) return;

            ControlKeyMigrations.EnsureApplied();

            var controls = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType, MetaControl, LockMode FROM ControlKey;");
            foreach (var control in controls)
            {
                string settingKey = control["SettingKey"].ToString();
                string inputKey = control["InputKey"].ToString();
                InputType inputType = ParseInputType(control["InputType"]?.ToString());
                bool isMetaControl = false;
                bool lockMode = false;
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

                if (control.TryGetValue("LockMode", out object lockValue) && lockValue != null && lockValue != DBNull.Value)
                {
                    try
                    {
                        lockMode = Convert.ToInt32(lockValue) != 0;
                    }
                    catch
                    {
                        lockMode = false;
                    }
                }

                if (ControlKeyRules.RequiresSwitchSemantics(settingKey) && !IsSwitchType(inputType))
                {
                    inputType = InputType.SaveSwitch;
                }

                if (TryCreateBinding(settingKey, inputKey, inputType, isMetaControl, lockMode, out ControlBinding binding))
                {
                    _controlBindings[settingKey] = binding;
                }
                else
                {
                    DebugLogger.PrintError($"Failed to parse input key '{inputKey}' for '{settingKey}'.");
                }
            }

            if (!_controlBindings.ContainsKey(BlockMenuKey) &&
                TryCreateBinding(BlockMenuKey, "Shift + X", InputType.SaveSwitch, true, false, out ControlBinding defaultBinding))
            {
                _controlBindings[BlockMenuKey] = defaultBinding;
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

        public static int GetBindingTokenCount(string settingKey)
        {
            if (_controlBindings.TryGetValue(settingKey, out ControlBinding binding))
            {
                return binding.TokenCount;
            }

            return 0;
        }

        public static bool IsTypeLocked(string settingKey)
        {
            return _controlBindings.TryGetValue(settingKey, out ControlBinding binding) && binding.LockMode;
        }

        internal static void ApplyHoldLatchSnapshot()
        {
            _holdLatchEnabled = true;
            _latchedHoldControls.Clear();
            _holdLatchRotation = Core.Instance?.Player?.Rotation ?? 0f;

            foreach (var kvp in _controlBindings)
            {
                ControlBinding binding = kvp.Value;
                if (!CanLatchBinding(binding))
                {
                    continue;
                }

                if (!ShouldAllowBinding(binding.IsMetaControl))
                {
                    continue;
                }

                if (binding.AreHoldTokensHeld())
                {
                    _latchedHoldControls.Add(binding.SettingKey);
                }
            }
        }

        internal static void ClearHoldLatch()
        {
            _holdLatchEnabled = false;
            _latchedHoldControls.Clear();
        }

        internal static bool TryGetHoldLatchRotation(out float rotation)
        {
            rotation = _holdLatchRotation;
            return _holdLatchEnabled;
        }

        private static bool CanLatchBinding(ControlBinding binding)
        {
            return binding != null &&
                !binding.IsMetaControl &&
                binding.InputType == InputType.Hold &&
                binding.TokenCount > 0;
        }

        private static bool IsLatchedHold(string settingKey, InputType inputType)
        {
            return _holdLatchEnabled &&
                inputType == InputType.Hold &&
                _latchedHoldControls.Contains(settingKey);
        }

        public static void UpdateBindingInputType(string settingKey, InputType newType, bool triggerOverride)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            if (!_controlBindings.TryGetValue(settingKey, out ControlBinding binding))
            {
                return;
            }

            if (ControlKeyRules.RequiresSwitchSemantics(settingKey))
            {
                newType = InputType.SaveSwitch;
            }

            ControlBinding updated = new(binding.SettingKey, newType, binding.Tokens, binding.DisplayLabel, binding.IsMetaControl, binding.LockMode);
            _controlBindings[settingKey] = updated;
            if (IsSwitchType(newType))
            {
                ControlKeyData.EnsureSwitchStartState(settingKey, 0);
                InputTypeManager.EnsureSwitchRegistration(settingKey);
                SwitchStateScanner.RefreshSwitchKeys();
            }
            SetTriggerOverride(settingKey, triggerOverride && newType == InputType.Trigger);
        }

        public static bool TryUpdateBindingInputKey(string settingKey, string newInputKey, out string displayLabel)
        {
            displayLabel = string.Empty;

            if (string.IsNullOrWhiteSpace(settingKey) || string.IsNullOrWhiteSpace(newInputKey))
            {
                return false;
            }

            if (!_controlBindings.TryGetValue(settingKey, out ControlBinding binding))
            {
                ControlKeyData.ControlKeyRecord record = ControlKeyData.GetControl(settingKey);
                if (record == null)
                {
                    return false;
                }

                InputType inferredType = ParseInputType(record.InputType);
                if (ControlKeyRules.RequiresSwitchSemantics(settingKey))
                {
                    inferredType = InputType.SaveSwitch;
                }

                if (record.LockMode)
                {
                    return false;
                }

                if (!TryCreateBinding(settingKey, newInputKey, inferredType, record.MetaControl, record.LockMode, out ControlBinding created))
                {
                    return false;
                }

                binding = created;
            }
            else if (binding.LockMode)
            {
                return false;
            }

            InputType targetType = binding.InputType;
            if (ControlKeyRules.RequiresSwitchSemantics(settingKey))
            {
                targetType = InputType.SaveSwitch;
            }

            if (!TryCreateBinding(settingKey, newInputKey, targetType, binding.IsMetaControl, binding.LockMode, out ControlBinding updated))
            {
                return false;
            }

            _controlBindings[settingKey] = updated;
            displayLabel = updated.DisplayLabel;
            InputTypeManager.UpdateInputKeyMapping(settingKey, newInputKey);
            if (IsSwitchType(targetType))
            {
                InputTypeManager.EnsureSwitchRegistration(settingKey);
                SwitchStateScanner.RefreshSwitchKeys();
            }

            return true;
        }

        public static bool TryUnbind(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            if (!_controlBindings.TryGetValue(settingKey, out ControlBinding binding))
            {
                return false;
            }

            if (binding.LockMode)
            {
                return false;
            }

            ControlKeyData.SetInputKey(settingKey, string.Empty);
            InputTypeManager.ClearInputKeyMapping(settingKey);
            _controlBindings.Remove(settingKey);
            SwitchStateScanner.RefreshSwitchKeys();
            return true;
        }

        public static IReadOnlyList<string> GetBindingsForInputKey(string inputKey, string excludeSetting = null)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return Array.Empty<string>();
            }

            List<string> keys = InputTypeManager.GetSettingKeysForInputKey(inputKey).ToList();
            if (!string.IsNullOrWhiteSpace(excludeSetting))
            {
                keys.RemoveAll(k => string.Equals(k, excludeSetting, StringComparison.OrdinalIgnoreCase));
            }

            List<string> normalizedInput = NormalizeInputTokens(inputKey);
            if (normalizedInput.Count == 0)
            {
                return keys;
            }

            foreach (var kvp in _controlBindings)
            {
                if (!string.IsNullOrWhiteSpace(excludeSetting) && string.Equals(kvp.Key, excludeSetting, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (keys.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (BindingMatchesInputTokens(kvp.Value, normalizedInput))
                {
                    keys.Add(kvp.Key);
                }
            }

            return keys;
        }

        private static List<string> NormalizeInputTokens(string inputKey)
        {
            List<string> tokens = new();
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return tokens;
            }

            string[] rawTokens = inputKey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in rawTokens)
            {
                string normalized = NormalizeInputToken(raw);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    tokens.Add(normalized);
                }
            }

            tokens.Sort(StringComparer.OrdinalIgnoreCase);
            return tokens;
        }

        private static bool BindingMatchesInputTokens(ControlBinding binding, IReadOnlyList<string> normalizedInputTokens)
        {
            if (binding?.Tokens == null || normalizedInputTokens == null || normalizedInputTokens.Count == 0)
            {
                return false;
            }

            List<string> bindingTokens = new();
            foreach (InputBindingToken token in binding.Tokens)
            {
                string normalized = token.IsMouse
                    ? NormalizeInputToken(token.MouseButton)
                    : NormalizeKeyToken(token.Keys);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    bindingTokens.Add(normalized);
                }
            }

            if (bindingTokens.Count != normalizedInputTokens.Count)
            {
                return false;
            }

            bindingTokens.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < bindingTokens.Count; i++)
            {
                if (!string.Equals(bindingTokens[i], normalizedInputTokens[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeInputToken(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return string.Empty;
            }

            string trimmed = rawToken.Trim();
            string mouse = CanonicalizeMouseToken(trimmed);
            if (!string.IsNullOrEmpty(mouse))
            {
                return mouse;
            }

            string collapsed = trimmed.Replace(" ", string.Empty);

            if (collapsed.Equals("LeftControl", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("RightControl", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            {
                return "Ctrl";
            }

            if (collapsed.Equals("LeftShift", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("RightShift", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                return "Shift";
            }

            if (collapsed.Equals("LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("RightAlt", StringComparison.OrdinalIgnoreCase) ||
                collapsed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                return "Alt";
            }

            return NormalizeLabel(trimmed);
        }

        private static string CanonicalizeMouseToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            string collapsed = token.Replace(" ", string.Empty);
            string lower = collapsed.ToLowerInvariant();

            return lower switch
            {
                "leftclick" => "LeftClick",
                "rightclick" => "RightClick",
                "middleclick" or "middlemouse" or "mouse3" or "mmb" or "wheelclick" or "scrollclick" or "scrollpress" => "MiddleClick",
                "mouse4" or "mb4" or "m4" or "xbutton1" or "button4" => "Mouse4",
                "mouse5" or "mb5" or "m5" or "xbutton2" or "button5" => "Mouse5",
                "scrollup" or "wheelup" or "scrollwheelup" => "ScrollUp",
                "scrolldown" or "wheeldown" or "scrollwheeldown" => "ScrollDown",
                _ => string.Empty
            };
        }

        private static string NormalizeKeyToken(IReadOnlyList<Keys> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return string.Empty;
            }

            foreach (Keys key in keys)
            {
                switch (key)
                {
                    case Keys.LeftControl:
                    case Keys.RightControl:
                        return "Ctrl";
                    case Keys.LeftShift:
                    case Keys.RightShift:
                        return "Shift";
                    case Keys.LeftAlt:
                    case Keys.RightAlt:
                        return "Alt";
                    default:
                        string label = NormalizeLabel(key.ToString());
                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            return label;
                        }
                        break;
                }
            }

            return string.Empty;
        }

        private static bool TryCreateBinding(string settingKey, string inputKey, InputType inputType, bool isMetaControl, bool lockMode, out ControlBinding binding)
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
            binding = new ControlBinding(settingKey, inputType, tokens, displayLabel, isMetaControl, lockMode);
            return true;
        }

        private static bool TryCreateMouseToken(string token, out InputBindingToken bindingToken)
        {
            bindingToken = default;

            string canonical = CanonicalizeMouseToken(token);
            if (string.IsNullOrWhiteSpace(canonical))
            {
                return false;
            }

            bindingToken = InputBindingToken.CreateMouse(canonical);
            return true;
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
            public ControlBinding(string settingKey, InputType inputType, IReadOnlyList<InputBindingToken> tokens, string displayLabel, bool isMetaControl, bool lockMode)
            {
                SettingKey = settingKey;
                InputType = inputType;
                Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
                DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? string.Join(" + ", tokens) : displayLabel;
                IsMetaControl = isMetaControl;
                LockMode = lockMode;
            }

            public string SettingKey { get; }
            public InputType InputType { get; }
            public IReadOnlyList<InputBindingToken> Tokens { get; }
            public string DisplayLabel { get; }
            public bool IsMetaControl { get; }
            public bool LockMode { get; }
            public int TokenCount => Tokens?.Count ?? 0;

            public bool IsActive()
            {
                if (Tokens == null || Tokens.Count == 0)
                {
                    return false;
                }

                if (IsLatchedHold(SettingKey, InputType))
                {
                    // Keep global freeze bookkeeping in sync even though the latch bypasses suppression checks.
                    _ = ShouldAllowBinding(IsMetaControl);
                    return true;
                }

                if (_holdLatchEnabled && !IsMetaControl && InputType == InputType.Hold)
                {
                    // When the hold latch is active, block any fresh non-meta hold bindings from activating.
                    _ = ShouldAllowBinding(IsMetaControl);
                    return false;
                }

                // LockMode only prevents rebinding in the UI; the binding should still be usable.
                if (InputType == InputType.Trigger && IsTriggerOverrideActive(SettingKey))
                {
                    return true;
                }

                bool anyTokenHeld = Tokens.Any(t => t.IsHeld());

                if (!ShouldAllowBinding(IsMetaControl))
                {
                    // Even if binding is suppressed (e.g., non-meta when inputs are frozen), prevent chord keys from toggling solo switches.
                    if (IsSwitchType(InputType) && Tokens.Count > 1 && anyTokenHeld)
                    {
                        InputTypeManager.ConsumeKeys(GetAllKeys(Tokens));
                        DebugLogger.PrintDebug($"[INPUT] Suppressing combo '{DisplayLabel}' while binding disallowed; consumed keys.");
                    }

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
                        if (!IsSwitchType(InputType))
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
                    InputType.SaveSwitch or InputType.NoSaveSwitch => EvaluateSwitchState(primary, modifiersHeld, hasModifiers),
                    _ => false
                };
            }

            public bool AreHoldTokensHeld()
            {
                if (InputType != InputType.Hold || Tokens == null || Tokens.Count == 0)
                {
                    return false;
                }

                int modifierCount = Math.Max(0, Tokens.Count - 1);

                for (int i = 0; i < modifierCount; i++)
                {
                    if (!Tokens[i].IsHeld())
                    {
                        return false;
                    }
                }

                return Tokens[^1].IsHeld();
            }

            private bool EvaluateSwitchState(InputBindingToken primary, bool modifiersHeld, bool hasModifiers)
            {
                if (hasModifiers)
                {
                    bool allTokensHeld = Tokens.All(t => t.IsHeld());
                    return InputTypeManager.EvaluateComboSwitch(SettingKey, allTokensHeld, GetAllKeys(Tokens));
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

            private static IEnumerable<Keys> GetAllKeys(IReadOnlyList<InputBindingToken> tokens)
            {
                if (tokens == null)
                {
                    yield break;
                }

                foreach (InputBindingToken token in tokens)
                {
                    if (token.Keys == null)
                    {
                        continue;
                    }

                    foreach (Keys key in token.Keys)
                    {
                        yield return key;
                    }
                }
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
            if (BlockManager.IsInputBlocked())
            {
                GameTracker.FreezeGameInputs = true;
                return true;
            }

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

            bool shouldFreeze = !BlockManager.IsCursorWithinGameBlock();
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

        public static void SetTriggerOverride(string settingKey, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return;
            }

            if (!isActive)
            {
                _triggerOverrides.Remove(settingKey);
                return;
            }

            _triggerOverrides[settingKey] = true;
        }

        private static bool IsTriggerOverrideActive(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            return _triggerOverrides.TryGetValue(settingKey, out bool state) && state;
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

                if (mouseButton.Equals("MiddleClick", StringComparison.OrdinalIgnoreCase))
                {
                    return "Middle Click";
                }

                if (mouseButton.Equals("Mouse4", StringComparison.OrdinalIgnoreCase))
                {
                    return "Mouse 4";
                }

                if (mouseButton.Equals("Mouse5", StringComparison.OrdinalIgnoreCase))
                {
                    return "Mouse 5";
                }

                if (mouseButton.Equals("ScrollUp", StringComparison.OrdinalIgnoreCase))
                {
                    return "Scroll Up";
                }

                if (mouseButton.Equals("ScrollDown", StringComparison.OrdinalIgnoreCase))
                {
                    return "Scroll Down";
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
