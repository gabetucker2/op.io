using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Draws a health bar horizontally centered below each destructible game object
    /// whenever its current health is below maximum.
    ///
    /// Fade logic:
    ///   • Alpha climbs from 0 → 1 over FadeIn seconds when health drops below max.
    ///   • Alpha holds at 1 while the object remains damaged.
    ///   • Alpha falls from 1 → 0 over FadeOut seconds once health returns to max.
    ///   • Entry is removed when alpha reaches 0.
    ///
    /// Toggled by the "HealthBar" SaveSwitch (Shift + H).
    /// </summary>
    public static class HealthBarManager
    {
        // objectId → current alpha [0, 1]
        private static readonly Dictionary<int, float> _alphas = new();
        private static Texture2D _pixel;
        private static bool _lastHealthBarsEnabled = true;

        private static bool HealthBarsEnabled =>
            !ControlStateManager.ContainsSwitchState(ControlKeyMigrations.HealthBarKey) ||
             ControlStateManager.GetSwitchState(ControlKeyMigrations.HealthBarKey);

        // ── Cached SQL settings ───────────────────────────────────────────────

        private static (float FadeIn, float Hold, float FadeOut)? _cachedAnim;
        private static (float FadeIn, float Hold, float FadeOut) Anim =>
            _cachedAnim ??= DatabaseFetch.GetAnimSetting("HealthBarAnim", 0.15f, 0f, 0.40f);
        public static float FadeIn  => Anim.FadeIn;
        public static float FadeOut => Anim.FadeOut;

        private static int? _cachedBarHeight;
        public static int BarHeight
        {
            get
            {
                _cachedBarHeight ??= DatabaseFetch.GetSetting<int>("PhysicsSettings", "Value", "SettingKey", "HealthBarHeight", 4);
                return _cachedBarHeight.Value;
            }
        }

        private static int? _cachedOffsetY;
        public static int OffsetY
        {
            get
            {
                _cachedOffsetY ??= DatabaseFetch.GetSetting<int>("PhysicsSettings", "Value", "SettingKey", "HealthBarOffsetY", 8);
                return _cachedOffsetY.Value;
            }
        }

        // ── Update ────────────────────────────────────────────────────────────

        public static void Update(float dt)
        {
            var gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null) return;

            float fadeIn  = FadeIn;
            float fadeOut = FadeOut;
            var liveIds   = new HashSet<int>();

            foreach (var obj in gameObjects)
            {
                if (!obj.IsDestructible || obj.MaxHealth <= 0f || obj.Shape == null) continue;

                liveIds.Add(obj.ID);
                bool damaged = obj.CurrentHealth < obj.MaxHealth;

                // Always ensure an entry exists for every tracked object so health bars
                // respond to damage immediately regardless of input freeze state.
                if (!_alphas.ContainsKey(obj.ID))
                    _alphas[obj.ID] = 0f;

                float alpha = _alphas[obj.ID];

                if (damaged)
                    alpha = MathF.Min(1f, alpha + dt / MathF.Max(fadeIn, 0.001f));
                else
                    alpha = MathF.Max(0f, alpha - dt / MathF.Max(fadeOut, 0.001f));

                _alphas[obj.ID] = alpha;
            }

            // Prune stale entries (object was removed from the scene).
            var toRemove = new List<int>();
            foreach (int id in _alphas.Keys)
            {
                if (!liveIds.Contains(id)) toRemove.Add(id);
            }
            foreach (int id in toRemove) _alphas.Remove(id);
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        public static void Draw(SpriteBatch spriteBatch)
        {
            bool enabled = HealthBarsEnabled;
            if (enabled != _lastHealthBarsEnabled)
            {
                DebugLogger.PrintDebug($"[HealthBar] HealthBarsEnabled changed: {_lastHealthBarsEnabled} → {enabled} (FreezeGameInputs={GameTracker.FreezeGameInputs})");
                _lastHealthBarsEnabled = enabled;
            }
            if (!enabled) return;

            var gameObjects = Core.Instance?.GameObjects;
            if (gameObjects == null) return;

            EnsurePixel(spriteBatch.GraphicsDevice);
            if (_pixel == null) return;

            int barHeight = BarHeight;
            int offsetY   = OffsetY;

            foreach (var obj in gameObjects)
            {
                if (!_alphas.TryGetValue(obj.ID, out float alpha)) continue;
                if (alpha <= 0f || obj.MaxHealth <= 0f || obj.Shape == null) continue;

                float ratio    = MathHelper.Clamp(obj.CurrentHealth / obj.MaxHealth, 0f, 1f);
                int   barWidth = Math.Max(1, obj.Shape.Width);
                int   drawX    = (int)(obj.Position.X - barWidth * 0.5f);
                int   drawY    = (int)(obj.Position.Y + obj.Shape.Height * 0.5f + offsetY);

                // Background track (dark gray).
                spriteBatch.Draw(_pixel,
                    new Rectangle(drawX, drawY, barWidth, barHeight),
                    Color.DarkGray * alpha);

                // Health fill (red → green based on ratio).
                int fillWidth = (int)(barWidth * ratio);
                if (fillWidth > 0)
                {
                    Color fillColor = Color.Lerp(new Color(220, 50, 50), new Color(60, 200, 60), ratio) * alpha;
                    spriteBatch.Draw(_pixel,
                        new Rectangle(drawX, drawY, fillWidth, barHeight),
                        fillColor);
                }
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
