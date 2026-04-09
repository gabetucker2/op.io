using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Draws stat bars (health, shield, XP) horizontally centered below each
    /// destructible game object, using the layout defined by BarConfigManager.
    ///
    /// Bar layout: bars in the same BarRow render side-by-side. Rows stack
    /// vertically below the object (row 0 is closest to the object).
    ///
    /// Fade logic (health/shield bars):
    ///   • Alpha climbs 0→1 over FadeIn seconds when health or shield drops below max.
    ///   • Alpha holds at 1 while damaged.
    ///   • Alpha falls 1→0 over FadeOut seconds once both are at maximum.
    ///
    /// XP bars are always visible when XPBarsEnabled is true; they do not fade.
    ///
    /// Toggled by "HealthBar" SaveSwitch (Shift+H) and "XPBar" SaveSwitch (Shift+E).
    /// </summary>
    public static class HealthBarManager
    {
        // objectId → current fade alpha [0, 1] for health/shield bars
        private static readonly Dictionary<int, float> _alphas = new();
        private static Texture2D _pixel;
        private static bool _lastHealthBarsEnabled = true;

        // Reusable collections — cleared and repopulated each frame to avoid heap allocations.
        private static readonly HashSet<int>                      _liveIds      = new();
        private static readonly List<int>                         _toRemove     = new();
        private static readonly List<BarConfigManager.BarEntry>   _visibleInRow = new();

        // ── Regen timer tracking ─────────────────────────────────────────────
        // Accumulated game time used to compute time-since-damage for regen bars.
        private static float _totalTime;
        private static readonly Dictionary<int, float> _prevHealth     = new();
        private static readonly Dictionary<int, float> _prevShield     = new();
        private static readonly Dictionary<int, float> _healthDmgTime  = new();
        private static readonly Dictionary<int, float> _shieldDmgTime  = new();

        // ── Toggle switches ──────────────────────────────────────────────────

        private static bool HealthBarsEnabled =>
            !ControlStateManager.ContainsSwitchState(ControlKeyMigrations.HealthBarKey) ||
             ControlStateManager.GetSwitchState(ControlKeyMigrations.HealthBarKey);

        public static bool XPBarsEnabled
        {
            get
            {
                foreach (BarConfigManager.BarEntry e in BarConfigManager.Global)
                    if (e.Type == BarType.XP && !e.IsHidden) return true;
                return false;
            }
        }

        // ── Cached SQL settings ──────────────────────────────────────────────

        private static (float FadeIn, float Hold, float FadeOut)? _cachedAnim;
        private static (float FadeIn, float Hold, float FadeOut) Anim =>
            _cachedAnim ??= DatabaseFetch.GetAnimSetting("HealthBarAnim", 0.15f, 0f, 0.40f, "BarSettings");
        public static float FadeIn  => Anim.FadeIn;
        public static float FadeOut => Anim.FadeOut;

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

        // ── Bar fill colors ──────────────────────────────────────────────────

        private static Color? _cachedHealthFillLow;
        public static Color HealthFillLow =>
            _cachedHealthFillLow ??= LoadColor("HealthBarFillLow", new Color(220, 50, 50));

        private static Color? _cachedHealthFillHigh;
        public static Color HealthFillHigh =>
            _cachedHealthFillHigh ??= LoadColor("HealthBarFillHigh", new Color(60, 200, 60));

        private static Color? _cachedBg;
        public static Color BarBackground =>
            _cachedBg ??= LoadColor("HealthBarBg", new Color(64, 64, 64));

        private static Color? _cachedShieldFill;
        public static Color ShieldFillColor =>
            _cachedShieldFill ??= LoadColor("ShieldBarFill", new Color(0, 180, 255));

        private static Color? _cachedXPFill;
        public static Color XPFillColor =>
            _cachedXPFill ??= LoadColor("XPBarFill", new Color(50, 220, 80));

        // ── Properties block bar colors ──────────────────────────────────────

        private static Color? _cachedPropHealthLow;
        public static Color PropBarHealthLow =>
            _cachedPropHealthLow ??= LoadColor("PropBarHealthLow", new Color(200, 50, 50));

        private static Color? _cachedPropHealthHigh;
        public static Color PropBarHealthHigh =>
            _cachedPropHealthHigh ??= LoadColor("PropBarHealthHigh", new Color(50, 200, 80));

        private static Color? _cachedPropEmpty;
        public static Color PropBarEmpty =>
            _cachedPropEmpty ??= LoadColor("PropBarEmpty", new Color(35, 35, 35, 210));

        private static Color LoadColor(string prefix, Color fallback)
        {
            int r = DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", prefix + "R", fallback.R);
            int g = DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", prefix + "G", fallback.G);
            int b = DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", prefix + "B", fallback.B);
            int a = DatabaseFetch.GetSetting<int>("BarSettings", "Value", "SettingKey", prefix + "A", fallback.A);
            return new Color(r, g, b, a);
        }

        // ── Update ───────────────────────────────────────────────────────────

        public static void Update(float dt)
        {
            var gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null) return;

            _totalTime += dt;

            float fadeIn  = FadeIn;
            float fadeOut = FadeOut;
            _liveIds.Clear();

            foreach (var obj in gameObjects)
            {
                if (!obj.IsDestructible || obj.MaxHealth <= 0f || obj.Shape == null) continue;

                int id = obj.ID;
                _liveIds.Add(id);

                // Detect health/shield drops to record last-damage time for regen bars
                if (_prevHealth.TryGetValue(id, out float ph) && obj.CurrentHealth < ph)
                    _healthDmgTime[id] = _totalTime;
                _prevHealth[id] = obj.CurrentHealth;

                if (obj.MaxShield > 0f)
                {
                    if (_prevShield.TryGetValue(id, out float ps) && obj.CurrentShield < ps)
                        _shieldDmgTime[id] = _totalTime;
                    _prevShield[id] = obj.CurrentShield;
                }

                bool damaged = obj.CurrentHealth < obj.MaxHealth ||
                               (obj.MaxShield > 0f && obj.CurrentShield < obj.MaxShield);

                if (!_alphas.ContainsKey(id)) _alphas[id] = 0f;

                float alpha = _alphas[id];
                if (damaged)
                    alpha = MathF.Min(1f, alpha + dt / MathF.Max(fadeIn, 0.001f));
                else
                    alpha = MathF.Max(0f, alpha - dt / MathF.Max(fadeOut, 0.001f));

                _alphas[id] = alpha;
            }

            _toRemove.Clear();
            foreach (int id in _alphas.Keys)
                if (!_liveIds.Contains(id)) _toRemove.Add(id);
            foreach (int id in _toRemove)
            {
                _alphas.Remove(id);
                _prevHealth.Remove(id);   _healthDmgTime.Remove(id);
                _prevShield.Remove(id);   _shieldDmgTime.Remove(id);
            }
        }

        // ── Draw ─────────────────────────────────────────────────────────────

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!BarConfigManager.BarsVisible) return;

            bool healthEnabled = HealthBarsEnabled;
            if (healthEnabled != _lastHealthBarsEnabled)
            {
                DebugLogger.PrintDebug($"[HealthBar] enabled: {_lastHealthBarsEnabled} → {healthEnabled}");
                _lastHealthBarsEnabled = healthEnabled;
            }

            bool xpEnabled = XPBarsEnabled;
            if (!healthEnabled && !xpEnabled) return;

            var gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null) return;

            EnsurePixel(spriteBatch.GraphicsDevice);
            if (_pixel == null) return;

            int segmentSize = SegmentSize;

            Color bgColor     = BarBackground;
            Color healthLow   = HealthFillLow;
            Color healthHigh  = HealthFillHigh;
            Color shieldColor = ShieldFillColor;
            Color xpColor     = XPFillColor;

            // Read bar row layout once per frame
            var rowGroups = BarConfigManager.GetGroupedByRow();

            foreach (var obj in gameObjects)
            {
                if (obj.MaxHealth <= 0f || obj.Shape == null) continue;

                bool hasHealthShield = obj.MaxHealth > 0f;
                bool hasXP          = obj.MaxXP > 0f;

                if (!hasHealthShield && !hasXP) continue;

                // Determine fade alpha for health/shield
                float hAlpha = 1f;
                if (hasHealthShield && _alphas.TryGetValue(obj.ID, out float a))
                    hAlpha = a;

                int barWidth = Math.Max(1, obj.Shape.Width);
                float sizeRatio = barWidth / 50f;
                int barHeight = BarHeight;
                int offsetY   = Math.Max(1, (int)MathF.Round(OffsetY * sizeRatio));
                int rowGap    = Math.Max(1, (int)MathF.Round(2f      * sizeRatio));
                int drawX    = (int)(obj.Position.X - barWidth * 0.5f);
                int baseY    = (int)(obj.Position.Y + obj.Shape.Height * 0.5f + offsetY);

                int currentRowY = baseY;

                foreach (var rowEntries in rowGroups)
                {
                    bool rowHasContent = false;
                    int rowMaxHeight   = barHeight;

                    // Regen data — read from GameObject properties (set for both Agents and farm GOs)
                    float hRegenEarly      = obj.HealthRegen;
                    float hRegenDelayEarly = obj.HealthRegenDelay;
                    float sRegenEarly      = obj.ShieldRegen;
                    float sRegenDelayEarly = obj.ShieldRegenDelay;

                    // Determine if this row has any visible bars for this object
                    foreach (var entry in rowEntries)
                    {
                        if (entry.IsHidden) continue;
                        switch (entry.Type)
                        {
                            case BarType.Health:
                                if (healthEnabled && hAlpha > 0f) { rowHasContent = true; } break;
                            case BarType.Shield:
                                if (healthEnabled && hAlpha > 0f && obj.MaxShield > 0f) { rowHasContent = true; } break;
                            case BarType.XP:
                                if (xpEnabled && hasXP) { rowHasContent = true; } break;
                            case BarType.HealthRegen:
                                if (hRegenEarly > 0f && hRegenDelayEarly > 0f && obj.CurrentHealth < obj.MaxHealth) { rowHasContent = true; } break;
                            case BarType.ShieldRegen:
                                if (sRegenEarly > 0f && sRegenDelayEarly > 0f && obj.MaxShield > 0f && obj.CurrentShield < obj.MaxShield) { rowHasContent = true; } break;
                        }
                        if (rowHasContent) break;
                    }
                    if (!rowHasContent) { continue; }

                    float hRegen      = obj.HealthRegen;
                    float hRegenDelay = obj.HealthRegenDelay;
                    float sRegen      = obj.ShieldRegen;
                    float sRegenDelay = obj.ShieldRegenDelay;

                    // Build the list of bars that have real data for this object
                    _visibleInRow.Clear();
                    foreach (var entry in rowEntries)
                    {
                        if (entry.IsHidden) continue;
                        switch (entry.Type)
                        {
                            case BarType.Health:
                                if (healthEnabled && hAlpha > 0f) _visibleInRow.Add(entry);
                                break;
                            case BarType.Shield:
                                if (healthEnabled && hAlpha > 0f && obj.MaxShield > 0f) _visibleInRow.Add(entry);
                                break;
                            case BarType.XP:
                                if (xpEnabled && hasXP) _visibleInRow.Add(entry);
                                break;
                            case BarType.HealthRegen:
                                if (hRegen > 0f && hRegenDelay > 0f && obj.CurrentHealth < obj.MaxHealth) _visibleInRow.Add(entry);
                                break;
                            case BarType.ShieldRegen:
                                if (sRegen > 0f && sRegenDelay > 0f && obj.MaxShield > 0f && obj.CurrentShield < obj.MaxShield) _visibleInRow.Add(entry);
                                break;
                        }
                    }

                    if (_visibleInRow.Count == 0) { currentRowY += rowMaxHeight + rowGap; continue; }

                    // Compute each bar's max value for proportional width allocation
                    float totalMax = 0f;
                    foreach (var entry in _visibleInRow)
                    {
                        totalMax += entry.Type switch
                        {
                            BarType.Health      => obj.MaxHealth,
                            BarType.Shield      => obj.MaxShield,
                            BarType.XP          => obj.MaxXP,
                            BarType.HealthRegen => hRegenDelay,
                            BarType.ShieldRegen => sRegenDelay,
                            _                   => 1f
                        };
                    }
                    if (totalMax <= 0f) totalMax = 1f;

                    int bxStart = drawX;
                    for (int vi = 0; vi < _visibleInRow.Count; vi++)
                    {
                        var   entry    = _visibleInRow[vi];
                        float entryMax = entry.Type switch
                        {
                            BarType.Health      => obj.MaxHealth,
                            BarType.Shield      => obj.MaxShield,
                            BarType.XP          => obj.MaxXP,
                            BarType.HealthRegen => hRegenDelay,
                            BarType.ShieldRegen => sRegenDelay,
                            _                   => 1f
                        };
                        bool isLast = vi == _visibleInRow.Count - 1;
                        int bx = bxStart;
                        int bw = isLast ? (drawX + barWidth - bx) : (int)(barWidth * entryMax / totalMax);
                        int segCount = entry.SegmentsEnabled ? Math.Max(1, entry.SegmentCount) : 0;

                        switch (entry.Type)
                        {
                            case BarType.Health:
                                DrawSingleBar(spriteBatch, bx, currentRowY, bw, barHeight,
                                    obj.CurrentHealth, obj.MaxHealth, segmentSize, segCount,
                                    Color.Lerp(healthLow, healthHigh,
                                        MathHelper.Clamp(obj.CurrentHealth / Math.Max(1f, obj.MaxHealth), 0f, 1f)),
                                    bgColor, hAlpha, entry.ShowPercent);
                                break;
                            case BarType.Shield:
                                DrawSingleBar(spriteBatch, bx, currentRowY, bw, barHeight,
                                    obj.CurrentShield, obj.MaxShield, segmentSize, segCount,
                                    shieldColor, bgColor, hAlpha, entry.ShowPercent);
                                break;
                            case BarType.XP:
                                DrawSingleBar(spriteBatch, bx, currentRowY, bw, barHeight,
                                    obj.CurrentXP, obj.MaxXP, segmentSize, segCount,
                                    xpColor, bgColor, 1f, entry.ShowPercent);
                                break;
                            case BarType.HealthRegen:
                            {
                                float last    = _healthDmgTime.TryGetValue(obj.ID, out float t) ? t : float.NegativeInfinity;
                                float elapsed = MathHelper.Clamp(_totalTime - last, 0f, hRegenDelay);
                                float segPts  = segCount > 1 ? hRegenDelay / segCount : 0f;
                                DrawSingleBar(spriteBatch, bx, currentRowY, bw, barHeight,
                                    elapsed, hRegenDelay, segPts, segCount,
                                    new Color(220, 200, 75), bgColor, 1f, entry.ShowPercent);
                                break;
                            }
                            case BarType.ShieldRegen:
                            {
                                float last    = _shieldDmgTime.TryGetValue(obj.ID, out float t) ? t : float.NegativeInfinity;
                                float elapsed = MathHelper.Clamp(_totalTime - last, 0f, sRegenDelay);
                                float segPts  = segCount > 1 ? sRegenDelay / segCount : 0f;
                                DrawSingleBar(spriteBatch, bx, currentRowY, bw, barHeight,
                                    elapsed, sRegenDelay, segPts, segCount,
                                    new Color(75, 220, 200), bgColor, 1f, entry.ShowPercent);
                                break;
                            }
                        }
                        bxStart += bw;
                    }

                    currentRowY += rowMaxHeight + rowGap;
                }
            }
        }

        private static void DrawSingleBar(SpriteBatch sb, int x, int y, int w, int h,
            float current, float max, float segmentPts, int segCount,
            Color fillColor, Color bgColor, float alpha, bool showPercent = false)
        {
            if (alpha <= 0f || max <= 0f) return;

            sb.Draw(_pixel, new Rectangle(x, y, w, h), bgColor * alpha);

            float ratio = MathHelper.Clamp(current / max, 0f, 1f);
            int fill    = (int)(w * ratio);
            if (fill > 0)
                sb.Draw(_pixel, new Rectangle(x, y, fill, h), fillColor * alpha);

            // Segment tick marks
            if (segmentPts > 0 && segCount > 1 && max > segmentPts)
            {
                Color tickColor  = Color.Black * 0.45f * alpha;
                float pxPerPoint = (float)w / max;
                for (float pt = segmentPts; pt < max - 0.5f; pt += segmentPts)
                    sb.Draw(_pixel, new Rectangle(x + (int)(pt * pxPerPoint), y, 1, h), tickColor);
            }

            // Percent text overlay
            if (showPercent)
            {
                UIStyle.UIFont font = UIStyle.FontTech;
                if (font.IsAvailable)
                {
                    string pct = $"{(int)(ratio * 100f)}%";
                    Vector2 ts = font.MeasureString(pct);
                    if (ts.X + 2 <= w && ts.Y <= h)
                    {
                        float tx = x + (w - ts.X) / 2f;
                        float ty = y + (h - ts.Y) / 2f;
                        font.DrawString(sb, pct, new Vector2(tx, ty), Color.White * alpha);
                    }
                }
            }
        }

        // ── Shared preview rendering ─────────────────────────────────────────

        /// <summary>
        /// Computes the health fill color with the same dynamic ratio-based lerp used by the in-game bars.
        /// </summary>
        public static Color GetHealthFillColor(float current, float max)
        {
            float ratio = max > 0f ? MathHelper.Clamp(current / max, 0f, 1f) : 0f;
            return Color.Lerp(HealthFillLow, HealthFillHigh, ratio);
        }

        /// <summary>
        /// Draws a bar in the standard in-game style (background, fill, value-based tick marks).
        /// Call from any preview context to guarantee visual consistency with in-game bars.
        /// </summary>
        public static void DrawBarPreview(SpriteBatch sb, Texture2D pixel, int x, int y, int w, int h,
            float current, float max, Color fillColor)
        {
            if (pixel == null || max <= 0f || w <= 0 || h <= 0) return;

            sb.Draw(pixel, new Rectangle(x, y, w, h), BarBackground);

            float ratio = MathHelper.Clamp(current / max, 0f, 1f);
            int fill = (int)(w * ratio);
            if (fill > 0)
                sb.Draw(pixel, new Rectangle(x, y, fill, h), fillColor);

            // Segment ticks — same value-based positioning as DrawSingleBar
            float segPts = SegmentSize;
            if (segPts > 0f && max > segPts)
            {
                Color tick = Color.Black * 0.45f;
                float pxPerPoint = (float)w / max;
                for (float pt = segPts; pt < max - 0.5f; pt += segPts)
                    sb.Draw(pixel, new Rectangle(x + (int)(pt * pxPerPoint), y, 1, h), tick);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void EnsurePixel(GraphicsDevice device)
        {
            if (_pixel != null || device == null) return;
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
    }
}
