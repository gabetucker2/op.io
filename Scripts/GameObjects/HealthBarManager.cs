using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Draws stat bars below destructible objects using the layout defined by
    /// BarConfigManager.
    /// </summary>
    public static class HealthBarManager
    {
        private readonly struct ResolvedBarRender
        {
            public ResolvedBarRender(BarConfigManager.BarEntry entry, float current, float max, float alpha)
            {
                Entry = entry;
                Current = current;
                Max = max;
                Alpha = alpha;
            }

            public BarConfigManager.BarEntry Entry { get; }
            public float Current { get; }
            public float Max { get; }
            public float Alpha { get; }
        }

        private const float ChangeVisibilitySeconds = 1.25f;
        private const float ValueChangeEpsilon = 0.001f;
        private const float DefaultYourBarRevealSeconds = 5f;

        private static readonly Dictionary<int, float> _alphas = new();
        private static readonly HashSet<int> _liveIds = new();
        private static readonly List<int> _toRemove = new();
        private static readonly List<ResolvedBarRender> _visibleInRow = new();
        private static readonly Dictionary<long, float> _barVisibilityAlphas = new();
        private static readonly HashSet<long> _liveBarKeys = new();
        private static readonly List<long> _barKeysToRemove = new();

        private static readonly Dictionary<int, float> _prevHealth = new();
        private static readonly Dictionary<int, float> _prevShield = new();
        private static readonly Dictionary<int, float> _prevXP = new();
        private static readonly Dictionary<int, float> _prevHealthRegenProgress = new();
        private static readonly Dictionary<int, float> _prevShieldRegenProgress = new();

        private static readonly Dictionary<int, float> _healthDmgTime = new();
        private static readonly Dictionary<int, float> _shieldDmgTime = new();
        private static readonly Dictionary<int, float> _healthChangeTime = new();
        private static readonly Dictionary<int, float> _shieldChangeTime = new();
        private static readonly Dictionary<int, float> _xpChangeTime = new();
        private static readonly Dictionary<int, float> _healthRegenChangeTime = new();
        private static readonly Dictionary<int, float> _shieldRegenChangeTime = new();
        private static readonly Dictionary<long, float> _barSpawnTime = new();

        private static Texture2D _pixel;
        private static bool _lastHealthBarsEnabled = true;
        private static float _totalTime;
        private static bool _yourBarRevealActive;
        private static bool _yourBarSwitchMode;
        private static float _yourBarRevealRemainingSeconds;
        private static float _yourBarMaxVisibilityAlpha;

        private static bool HealthBarsEnabled =>
            !ControlStateManager.ContainsSwitchState(ControlKeyMigrations.HealthBarKey) ||
            ControlStateManager.GetSwitchState(ControlKeyMigrations.HealthBarKey);

        public static bool XPBarsEnabled
        {
            get
            {
                return BarConfigManager.IsBarVisibleInAnyGroup(BarType.XP);
            }
        }

        private static (float FadeIn, float Hold, float FadeOut)? _cachedAnim;
        private static (float FadeIn, float Hold, float FadeOut) Anim =>
            _cachedAnim ??= DatabaseFetch.GetAnimSetting("HealthBarAnim", 0.15f, 0f, 0.40f, "BarSettings");

        public static float FadeIn => Anim.FadeIn;
        public static float FadeOut => Anim.FadeOut;

        private static float? _cachedYourBarRevealSeconds;
        public static float YourBarRevealSeconds
        {
            get
            {
                _cachedYourBarRevealSeconds ??= DatabaseFetch.GetSetting<float>(
                    "BarSettings", "Value", "SettingKey", "YourBarRevealSeconds", DefaultYourBarRevealSeconds);
                return MathF.Max(0f, _cachedYourBarRevealSeconds.Value);
            }
        }

        public static bool YourBarRevealActive => _yourBarRevealActive;
        public static bool YourBarControlSwitchMode => _yourBarSwitchMode;
        public static float YourBarRevealRemainingSeconds => _yourBarRevealRemainingSeconds;
        public static float YourBarVisibilityAlpha => _yourBarMaxVisibilityAlpha;
        public static bool YourBarVisible => BarConfigManager.BarsVisible && _yourBarMaxVisibilityAlpha > 0.001f;

        public static void Clear()
        {
            _alphas.Clear();
            _liveIds.Clear();
            _toRemove.Clear();
            _visibleInRow.Clear();
            _barVisibilityAlphas.Clear();
            _liveBarKeys.Clear();
            _barKeysToRemove.Clear();
            _prevHealth.Clear();
            _prevShield.Clear();
            _prevXP.Clear();
            _prevHealthRegenProgress.Clear();
            _prevShieldRegenProgress.Clear();
            _healthDmgTime.Clear();
            _shieldDmgTime.Clear();
            _healthChangeTime.Clear();
            _shieldChangeTime.Clear();
            _xpChangeTime.Clear();
            _healthRegenChangeTime.Clear();
            _shieldRegenChangeTime.Clear();
            _barSpawnTime.Clear();
            _yourBarRevealActive = false;
            _yourBarRevealRemainingSeconds = 0f;
            _yourBarMaxVisibilityAlpha = 0f;
        }

        private static int? _cachedBarHeight;
        public static int BarHeight
        {
            get
            {
                _cachedBarHeight ??= DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", "HealthBarHeight", 4);
                return _cachedBarHeight.Value;
            }
        }

        private static int? _cachedOffsetY;
        public static int OffsetY
        {
            get
            {
                _cachedOffsetY ??= DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", "HealthBarOffsetY", 8);
                return _cachedOffsetY.Value;
            }
        }

        private static int? _cachedSegmentSize;
        public static int SegmentSize
        {
            get
            {
                _cachedSegmentSize ??= DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", "HealthBarSegmentSize", 10);
                return _cachedSegmentSize.Value;
            }
        }

        public static Color HealthFillLow => ColorPalette.HealthBarLow;
        public static Color HealthFillHigh => ColorPalette.HealthBarHigh;
        public static Color BarBackground => ColorPalette.BarBackground;
        public static Color ShieldFillColor => ColorPalette.ShieldBar;
        public static Color XPFillColor => ColorPalette.XPBar;

        public static Color PropBarHealthLow => ColorPalette.HealthBarLow;
        public static Color PropBarHealthHigh => ColorPalette.HealthBarHigh;
        public static Color PropBarEmpty => ColorPalette.BarBackground * 0.82f;

        public static void Update(float dt)
        {
            var gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null)
            {
                return;
            }

            _totalTime += dt;
            UpdateYourBarControl(dt);
            _yourBarMaxVisibilityAlpha = 0f;

            float fadeIn = FadeIn;
            float fadeOut = FadeOut;
            bool healthEnabled = HealthBarsEnabled;
            bool xpEnabled = XPBarsEnabled;
            _liveIds.Clear();
            _liveBarKeys.Clear();

            foreach (GameObject obj in gameObjects)
            {
                if (!obj.IsDestructible || obj.MaxHealth <= 0f || obj.Shape == null)
                {
                    continue;
                }

                int id = obj.ID;
                _liveIds.Add(id);

                if (_prevHealth.TryGetValue(id, out float previousHealth) && obj.CurrentHealth < previousHealth)
                {
                    _healthDmgTime[id] = _totalTime;
                }

                if (_prevShield.TryGetValue(id, out float previousShield) && obj.MaxShield > 0f && obj.CurrentShield < previousShield)
                {
                    _shieldDmgTime[id] = _totalTime;
                }

                TrackValueChange(BarType.Health, _prevHealth, _healthChangeTime, id, obj.CurrentHealth);
                TrackValueChange(BarType.Shield, _prevShield, _shieldChangeTime, id, obj.CurrentShield);
                TrackValueChange(BarType.XP, _prevXP, _xpChangeTime, id, obj.CurrentXP);
                TrackValueChange(BarType.HealthRegen, _prevHealthRegenProgress, _healthRegenChangeTime, id, GetHealthRegenProgressValue(obj));
                TrackValueChange(BarType.ShieldRegen, _prevShieldRegenProgress, _shieldRegenChangeTime, id, GetShieldRegenProgressValue(obj));

                bool damaged = obj.CurrentHealth < obj.MaxHealth ||
                               (obj.MaxShield > 0f && obj.CurrentShield < obj.MaxShield);

                if (!_alphas.ContainsKey(id))
                {
                    _alphas[id] = 0f;
                }

                float alpha = _alphas[id];
                if (damaged)
                {
                    alpha = MathF.Min(1f, alpha + dt / MathF.Max(fadeIn, 0.001f));
                }
                else
                {
                    alpha = MathF.Max(0f, alpha - dt / MathF.Max(fadeOut, 0.001f));
                }

                _alphas[id] = alpha;

                bool isPlayer = obj is Agent agent && agent.IsPlayer;
                IReadOnlyList<BarConfigManager.BarEntry> barEntries = BarConfigManager.GetResolvedEntries(obj);
                foreach (BarConfigManager.BarEntry entry in barEntries)
                {
                    if (entry == null || entry.IsHidden)
                    {
                        continue;
                    }

                    if (!TryGetBarBaseState(obj, entry.Type, healthEnabled, xpEnabled, alpha, isPlayer, out _, out _, out _, out bool externalVisible))
                    {
                        continue;
                    }

                    long barKey = GetBarVisibilityKey(id, entry.Type);
                    _liveBarKeys.Add(barKey);

                    bool relationActive = AreBarVisibilityRelationsActive(obj, entry);
                    bool shouldBeVisible = relationActive && externalVisible;
                    float barAlpha = _barVisibilityAlphas.TryGetValue(barKey, out float existingAlpha)
                        ? existingAlpha
                        : (shouldBeVisible ? 1f : 0f);

                    if (shouldBeVisible)
                    {
                        barAlpha = 1f;
                    }
                    else
                    {
                        float fadeSeconds = MathF.Max(entry.VisibilityFadeOutSeconds, 0f);
                        barAlpha = fadeSeconds <= 0.001f
                            ? 0f
                            : MathF.Max(0f, barAlpha - (dt / fadeSeconds));
                    }

                    _barVisibilityAlphas[barKey] = barAlpha;
                    if (isPlayer && barAlpha > _yourBarMaxVisibilityAlpha)
                    {
                        _yourBarMaxVisibilityAlpha = barAlpha;
                    }
                }
            }

            _toRemove.Clear();
            foreach (int id in _alphas.Keys)
            {
                if (!_liveIds.Contains(id))
                {
                    _toRemove.Add(id);
                }
            }

            foreach (int id in _toRemove)
            {
                _alphas.Remove(id);
                RemoveTrackedState(id);
            }

            _barKeysToRemove.Clear();
            foreach (long barKey in _barVisibilityAlphas.Keys)
            {
                if (!_liveBarKeys.Contains(barKey))
                {
                    _barKeysToRemove.Add(barKey);
                }
            }

            foreach (long barKey in _barKeysToRemove)
            {
                _barVisibilityAlphas.Remove(barKey);
            }
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!BarConfigManager.BarsVisible)
            {
                return;
            }

            bool healthEnabled = HealthBarsEnabled;
            if (healthEnabled != _lastHealthBarsEnabled)
            {
                DebugLogger.PrintDebug($"[HealthBar] enabled: {_lastHealthBarsEnabled} -> {healthEnabled}");
                _lastHealthBarsEnabled = healthEnabled;
            }

            bool xpEnabled = XPBarsEnabled;
            if (!healthEnabled && !xpEnabled)
            {
                return;
            }

            var gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null)
            {
                return;
            }

            EnsurePixel(spriteBatch.GraphicsDevice);
            if (_pixel == null)
            {
                return;
            }

            int segmentSize = SegmentSize;
            Color bgColor = BarBackground;
            Color healthLow = HealthFillLow;
            Color healthHigh = HealthFillHigh;
            Color shieldColor = ShieldFillColor;
            Color xpColor = XPFillColor;

            foreach (GameObject obj in gameObjects)
            {
                if (obj.MaxHealth <= 0f || obj.Shape == null)
                {
                    continue;
                }

                bool hasXP = obj.MaxXP > 0f;
                if (obj.MaxHealth <= 0f && !hasXP)
                {
                    continue;
                }

                bool isPlayer = obj is Agent agent && agent.IsPlayer;
                float healthAlpha = _alphas.TryGetValue(obj.ID, out float alpha) ? alpha : 1f;

                int barWidth = Math.Max(1, obj.Shape.Width);
                float sizeRatio = barWidth / 50f;
                int barHeight = BarHeight;
                int offsetY = Math.Max(1, (int)MathF.Round(OffsetY * sizeRatio));
                int rowGap = Math.Max(1, (int)MathF.Round(2f * sizeRatio));
                int drawX = (int)(obj.Position.X - (barWidth * 0.5f));
                int baseY = (int)(obj.Position.Y + (obj.Shape.Height * 0.5f) + offsetY);
                int currentRowY = baseY;
                List<List<BarConfigManager.BarEntry>> rowGroups = BarConfigManager.GetGroupedByRow(obj);

                foreach (List<BarConfigManager.BarEntry> rowEntries in rowGroups)
                {
                    _visibleInRow.Clear();

                    foreach (BarConfigManager.BarEntry entry in rowEntries)
                    {
                        if (TryResolveBarRender(obj, entry, healthEnabled, xpEnabled, healthAlpha, isPlayer, out ResolvedBarRender resolved))
                        {
                            _visibleInRow.Add(resolved);
                        }
                    }

                    if (_visibleInRow.Count == 0)
                    {
                        continue;
                    }

                    float totalMax = 0f;
                    foreach (ResolvedBarRender resolved in _visibleInRow)
                    {
                        totalMax += Math.Max(0f, resolved.Max);
                    }

                    if (totalMax <= 0f)
                    {
                        totalMax = 1f;
                    }

                    int bxStart = drawX;
                    for (int i = 0; i < _visibleInRow.Count; i++)
                    {
                        ResolvedBarRender resolved = _visibleInRow[i];
                        bool isLast = i == _visibleInRow.Count - 1;
                        int barWidthForEntry = isLast
                            ? (drawX + barWidth - bxStart)
                            : (int)(barWidth * Math.Max(0f, resolved.Max) / totalMax);
                        int segCount = resolved.Entry.SegmentsEnabled ? Math.Max(1, resolved.Entry.SegmentCount) : 0;

                        switch (resolved.Entry.Type)
                        {
                            case BarType.Health:
                                DrawSingleBar(
                                    spriteBatch,
                                    bxStart,
                                    currentRowY,
                                    barWidthForEntry,
                                    barHeight,
                                    resolved.Current,
                                    resolved.Max,
                                    segmentSize,
                                    segCount,
                                    Color.Lerp(healthLow, healthHigh, MathHelper.Clamp(resolved.Current / Math.Max(1f, resolved.Max), 0f, 1f)),
                                    bgColor,
                                    resolved.Alpha,
                                    resolved.Entry.ShowPercent);
                                break;

                            case BarType.Shield:
                                DrawSingleBar(
                                    spriteBatch,
                                    bxStart,
                                    currentRowY,
                                    barWidthForEntry,
                                    barHeight,
                                    resolved.Current,
                                    resolved.Max,
                                    segmentSize,
                                    segCount,
                                    shieldColor,
                                    bgColor,
                                    resolved.Alpha,
                                    resolved.Entry.ShowPercent);
                                break;

                            case BarType.XP:
                                DrawSingleBar(
                                    spriteBatch,
                                    bxStart,
                                    currentRowY,
                                    barWidthForEntry,
                                    barHeight,
                                    resolved.Current,
                                    resolved.Max,
                                    segmentSize,
                                    segCount,
                                    xpColor,
                                    bgColor,
                                    resolved.Alpha,
                                    resolved.Entry.ShowPercent);
                                break;

                            case BarType.HealthRegen:
                                DrawSingleBar(
                                    spriteBatch,
                                    bxStart,
                                    currentRowY,
                                    barWidthForEntry,
                                    barHeight,
                                    resolved.Current,
                                    resolved.Max,
                                    segCount > 1 ? resolved.Max / segCount : 0f,
                                    segCount,
                                    ColorPalette.BarRegenTick,
                                    bgColor,
                                    resolved.Alpha,
                                    resolved.Entry.ShowPercent);
                                break;

                            case BarType.ShieldRegen:
                                DrawSingleBar(
                                    spriteBatch,
                                    bxStart,
                                    currentRowY,
                                    barWidthForEntry,
                                    barHeight,
                                    resolved.Current,
                                    resolved.Max,
                                    segCount > 1 ? resolved.Max / segCount : 0f,
                                    segCount,
                                    ColorPalette.ShieldRegenTick,
                                    bgColor,
                                    resolved.Alpha,
                                    resolved.Entry.ShowPercent);
                                break;
                        }

                        bxStart += barWidthForEntry;
                    }

                    currentRowY += barHeight + rowGap;
                }
            }
        }

        private static bool TryResolveBarRender(
            GameObject obj,
            BarConfigManager.BarEntry entry,
            bool healthEnabled,
            bool xpEnabled,
            float healthAlpha,
            bool isPlayer,
            out ResolvedBarRender resolved)
        {
            resolved = default;
            if (entry == null || entry.IsHidden)
            {
                return false;
            }

            if (!TryGetBarBaseState(obj, entry.Type, healthEnabled, xpEnabled, healthAlpha, isPlayer, out float current, out float max, out float alpha, out bool externalVisible))
            {
                return false;
            }

            bool relationActive = AreBarVisibilityRelationsActive(obj, entry);
            long barKey = GetBarVisibilityKey(obj.ID, entry.Type);
            float visibilityAlpha = _barVisibilityAlphas.TryGetValue(barKey, out float storedAlpha)
                ? storedAlpha
                : (relationActive && externalVisible ? 1f : 0f);
            float finalAlpha = alpha * visibilityAlpha;
            if (finalAlpha <= 0.001f)
            {
                return false;
            }

            resolved = new ResolvedBarRender(entry, current, max, finalAlpha);
            return true;
        }

        private static void UpdateYourBarControl(float dt)
        {
            ControlKeyData.ControlKeyRecord record = ControlKeyData.GetControl(ControlKeyMigrations.YourBarKey);
            if (record == null)
            {
                _yourBarSwitchMode = false;
                _yourBarRevealActive = true;
                _yourBarRevealRemainingSeconds = 0f;
                return;
            }

            _yourBarSwitchMode = IsSwitchInputType(record.InputType);
            bool inputActive = InputManager.IsInputActive(ControlKeyMigrations.YourBarKey);

            if (_yourBarSwitchMode)
            {
                _yourBarRevealActive = inputActive;
                _yourBarRevealRemainingSeconds = 0f;
                return;
            }

            if (inputActive)
            {
                _yourBarRevealRemainingSeconds = YourBarRevealSeconds;
            }
            else if (_yourBarRevealRemainingSeconds > 0f)
            {
                _yourBarRevealRemainingSeconds = MathF.Max(0f, _yourBarRevealRemainingSeconds - MathF.Max(0f, dt));
            }

            _yourBarRevealActive = _yourBarRevealRemainingSeconds > 0f;
        }

        private static bool IsSwitchInputType(string inputTypeLabel)
        {
            return string.Equals(inputTypeLabel, nameof(InputType.SaveSwitch), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputTypeLabel, nameof(InputType.NoSaveSwitch), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputTypeLabel, nameof(InputType.DoubleTapToggle), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(inputTypeLabel, "Switch", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsYourPlayerBar(GameObject obj)
        {
            return obj is Agent agent && agent.IsPlayer;
        }

        private static bool AreBarVisibilityRelationsActive(GameObject obj, BarConfigManager.BarEntry entry)
        {
            bool relationActive = BarConfigManager.AreVisibilityRelationsActive(entry, type => GetBarSourceState(obj, type));
            return IsYourPlayerBar(obj)
                ? relationActive || _yourBarRevealActive
                : relationActive;
        }

        private static bool TryGetBarBaseState(
            GameObject obj,
            BarType barType,
            bool healthEnabled,
            bool xpEnabled,
            float healthAlpha,
            bool isPlayer,
            out float current,
            out float max,
            out float alpha,
            out bool externalVisible)
        {
            current = 0f;
            max = 0f;
            alpha = 1f;
            externalVisible = true;

            switch (barType)
            {
                case BarType.Health:
                    if (!healthEnabled || obj.MaxHealth <= 0f)
                    {
                        return false;
                    }

                    current = obj.CurrentHealth;
                    max = obj.MaxHealth;
                    alpha = isPlayer ? 1f : healthAlpha;
                    externalVisible = isPlayer || healthAlpha > 0f;
                    return true;

                case BarType.Shield:
                    if (!healthEnabled || obj.MaxShield <= 0f)
                    {
                        return false;
                    }

                    current = obj.CurrentShield;
                    max = obj.MaxShield;
                    alpha = isPlayer ? 1f : healthAlpha;
                    externalVisible = isPlayer || healthAlpha > 0f;
                    return true;

                case BarType.XP:
                    if (!xpEnabled || obj.MaxXP <= 0f)
                    {
                        return false;
                    }

                    current = obj.CurrentXP;
                    max = obj.MaxXP;
                    return true;

                case BarType.HealthRegen:
                    if (obj.HealthRegen <= 0f || obj.HealthRegenDelay <= 0f)
                    {
                        return false;
                    }

                    current = GetHealthRegenProgressValue(obj);
                    max = obj.HealthRegenDelay;
                    return true;

                case BarType.ShieldRegen:
                    if (obj.ShieldRegen <= 0f || obj.ShieldRegenDelay <= 0f || obj.MaxShield <= 0f)
                    {
                        return false;
                    }

                    current = GetShieldRegenProgressValue(obj);
                    max = obj.ShieldRegenDelay;
                    return true;

                default:
                    return false;
            }
        }

        private static long GetBarVisibilityKey(int objectId, BarType barType)
        {
            return ((long)objectId << 32) | (uint)(int)barType;
        }

        private static BarConfigManager.BarSourceState GetBarSourceState(GameObject obj, BarType type)
        {
            float current = 0f;
            float max = 0f;
            bool isKnown = false;
            bool changedRecently = false;
            int id = obj.ID;
            bool spawnedRecently = WasSpawnedRecently(id, type);

            switch (type)
            {
                case BarType.Health:
                    current = obj.CurrentHealth;
                    max = obj.MaxHealth;
                    isKnown = _prevHealth.ContainsKey(id);
                    changedRecently = WasChangedRecently(_healthChangeTime, id);
                    break;

                case BarType.Shield:
                    current = obj.CurrentShield;
                    max = obj.MaxShield;
                    isKnown = _prevShield.ContainsKey(id);
                    changedRecently = WasChangedRecently(_shieldChangeTime, id);
                    break;

                case BarType.XP:
                    current = obj.CurrentXP;
                    max = obj.MaxXP;
                    isKnown = _prevXP.ContainsKey(id);
                    changedRecently = WasChangedRecently(_xpChangeTime, id);
                    break;

                case BarType.HealthRegen:
                    current = GetHealthRegenProgressValue(obj);
                    max = obj.HealthRegen > 0f && obj.HealthRegenDelay > 0f ? obj.HealthRegenDelay : 0f;
                    isKnown = _prevHealthRegenProgress.ContainsKey(id);
                    changedRecently = WasChangedRecently(_healthRegenChangeTime, id);
                    break;

                case BarType.ShieldRegen:
                    current = GetShieldRegenProgressValue(obj);
                    max = obj.ShieldRegen > 0f && obj.ShieldRegenDelay > 0f ? obj.ShieldRegenDelay : 0f;
                    isKnown = _prevShieldRegenProgress.ContainsKey(id);
                    changedRecently = WasChangedRecently(_shieldRegenChangeTime, id);
                    break;
            }

            return new BarConfigManager.BarSourceState(current, max, changedRecently, spawnedRecently, isKnown);
        }

        private static bool WasChangedRecently(Dictionary<int, float> changeTimes, int id)
        {
            return changeTimes.TryGetValue(id, out float lastChange) &&
                   _totalTime - lastChange <= ChangeVisibilitySeconds;
        }

        private static bool WasSpawnedRecently(int id, BarType type)
        {
            return _barSpawnTime.TryGetValue(GetBarVisibilityKey(id, type), out float spawnTime) &&
                   _totalTime - spawnTime <= ChangeVisibilitySeconds;
        }

        private static float GetHealthRegenProgressValue(GameObject obj)
        {
            if (obj.HealthRegen <= 0f || obj.HealthRegenDelay <= 0f || obj.CurrentHealth >= obj.MaxHealth)
            {
                return 0f;
            }

            float last = _healthDmgTime.TryGetValue(obj.ID, out float time) ? time : float.NegativeInfinity;
            return MathHelper.Clamp(_totalTime - last, 0f, obj.HealthRegenDelay);
        }

        private static float GetShieldRegenProgressValue(GameObject obj)
        {
            if (obj.ShieldRegen <= 0f || obj.ShieldRegenDelay <= 0f || obj.MaxShield <= 0f || obj.CurrentShield >= obj.MaxShield)
            {
                return 0f;
            }

            float last = _shieldDmgTime.TryGetValue(obj.ID, out float time) ? time : float.NegativeInfinity;
            return MathHelper.Clamp(_totalTime - last, 0f, obj.ShieldRegenDelay);
        }

        private static void TrackValueChange(
            BarType type,
            Dictionary<int, float> previousValues,
            Dictionary<int, float> changeTimes,
            int id,
            float current)
        {
            if (previousValues.TryGetValue(id, out float previous))
            {
                if (MathF.Abs(current - previous) > ValueChangeEpsilon)
                {
                    changeTimes[id] = _totalTime;
                }
            }
            else
            {
                _barSpawnTime[GetBarVisibilityKey(id, type)] = _totalTime;
            }

            previousValues[id] = current;
        }

        private static void RemoveTrackedState(int id)
        {
            _prevHealth.Remove(id);
            _prevShield.Remove(id);
            _prevXP.Remove(id);
            _prevHealthRegenProgress.Remove(id);
            _prevShieldRegenProgress.Remove(id);
            _healthDmgTime.Remove(id);
            _shieldDmgTime.Remove(id);
            _healthChangeTime.Remove(id);
            _shieldChangeTime.Remove(id);
            _xpChangeTime.Remove(id);
            _healthRegenChangeTime.Remove(id);
            _shieldRegenChangeTime.Remove(id);
            foreach (BarType type in Enum.GetValues<BarType>())
            {
                _barSpawnTime.Remove(GetBarVisibilityKey(id, type));
            }
        }

        private static void DrawSingleBar(
            SpriteBatch spriteBatch,
            int x,
            int y,
            int w,
            int h,
            float current,
            float max,
            float segmentPts,
            int segCount,
            Color fillColor,
            Color bgColor,
            float alpha,
            bool showPercent = false)
        {
            if (alpha <= 0f || max <= 0f)
            {
                return;
            }

            spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), bgColor * alpha);

            float ratio = MathHelper.Clamp(current / max, 0f, 1f);
            int fill = (int)(w * ratio);
            if (fill > 0)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, y, fill, h), fillColor * alpha);
            }

            if (segmentPts > 0f && segCount > 1 && max > segmentPts)
            {
                Color tickColor = Color.Black * 0.45f * alpha;
                float pxPerPoint = (float)w / max;
                for (float pt = segmentPts; pt < max - 0.5f; pt += segmentPts)
                {
                    spriteBatch.Draw(_pixel, new Rectangle(x + (int)(pt * pxPerPoint), y, 1, h), tickColor);
                }
            }

            if (showPercent)
            {
                UIStyle.UIFont font = UIStyle.FontTech;
                if (font.IsAvailable)
                {
                    string pct = $"{(int)(ratio * 100f)}%";
                    Vector2 textSize = font.MeasureString(pct);
                    if (textSize.X + 2 <= w && textSize.Y <= h)
                    {
                        float tx = x + (w - textSize.X) / 2f;
                        float ty = y + (h - textSize.Y) / 2f;
                        font.DrawString(spriteBatch, pct, new Vector2(tx, ty), Color.White * alpha);
                    }
                }
            }
        }

        /// <summary>
        /// Computes the health fill color with the same ratio-based lerp used by the in-game bars.
        /// </summary>
        public static Color GetHealthFillColor(float current, float max)
        {
            float ratio = max > 0f ? MathHelper.Clamp(current / max, 0f, 1f) : 0f;
            return Color.Lerp(HealthFillLow, HealthFillHigh, ratio);
        }

        /// <summary>
        /// Draws a bar in the standard in-game style for preview contexts.
        /// </summary>
        public static void DrawBarPreview(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            int x,
            int y,
            int w,
            int h,
            float current,
            float max,
            Color fillColor,
            float segmentPts = -1f,
            int segCount = -1)
        {
            if (pixel == null || max <= 0f || w <= 0 || h <= 0)
            {
                return;
            }

            spriteBatch.Draw(pixel, new Rectangle(x, y, w, h), BarBackground);

            float ratio = MathHelper.Clamp(current / max, 0f, 1f);
            int fill = (int)(w * ratio);
            if (fill > 0)
            {
                spriteBatch.Draw(pixel, new Rectangle(x, y, fill, h), fillColor);
            }

            if (segmentPts < 0f)
            {
                segmentPts = SegmentSize;
                segCount = int.MaxValue;
            }

            if (segmentPts > 0f && segCount > 1 && max > segmentPts)
            {
                Color tick = Color.Black * 0.45f;
                float pxPerPoint = (float)w / max;
                for (float pt = segmentPts; pt < max - 0.5f; pt += segmentPts)
                {
                    spriteBatch.Draw(pixel, new Rectangle(x + (int)(pt * pxPerPoint), y, 1, h), tick);
                }
            }
        }

        private static void EnsurePixel(GraphicsDevice device)
        {
            if (_pixel != null || device == null)
            {
                return;
            }

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
    }
}
