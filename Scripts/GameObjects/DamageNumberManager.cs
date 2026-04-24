using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace op.io
{
    /// <summary>
    /// Manages stacked combat-text numbers above damaged objects.
    ///
    /// Stacking rules:
    ///   • Each (object, source) pair has exactly one active entry — the "routine" for that source.
    ///   • A barrel, a collision agent, or any other source each get their own independent entry.
    ///   • Hits with the same integer-ceiling damage increment the hit counter (x2, x3…).
    ///   • Hits with a different damage value replace the entry (count resets to 1).
    ///   • A new entry can only be created once the previous one fully expires — while
    ///     an entry is alive (even if fading) all new hits from the same source stack onto it.
    ///
    /// Non-overlap:
    ///   • Entries for the same object are placed in distinct horizontal lanes so they never
    ///     occlude each other, even after the drift position resets.
    ///   • Lane 0 = centre, lane 1 = −LaneWidth, lane 2 = +LaneWidth, etc.
    ///   • Drift is purely vertical; the horizontal lane offset never changes.
    ///
    /// Lifetime:
    ///   • TimeSinceLastHit starts at -FadeIn and advances each frame.
    ///   • While TimeSinceLastHit &lt; 0: fade-in phase (alpha 0→1).
    ///   • While TimeSinceLastHit in [0, FadeOut): fade-out phase (alpha 1→0).
    ///   • Any hit resets TimeSinceLastHit to 0, holding alpha at 1 indefinitely.
    ///   • Entry is removed when TimeSinceLastHit >= FadeOut.
    ///
    /// Position:
    ///   • BasePosition is updated by every Notify call, so the text follows the object
    ///     while it is alive. DriftOffset accumulates upward independently.
    ///   • When vertical drift exceeds MaxDriftDistance the offset resets to (laneX, 0).
    ///
    /// Scale bounce:
    ///   • Each hit that increments the counter triggers a 0.05 s scale-up followed by
    ///     a 0.1 s scale-down animation, restarting from the current scale if interrupted.
    /// </summary>
    public static class DamageNumberManager
    {
        // ── Stack entry ───────────────────────────────────────────────────────

        private struct StackEntry
        {
            public int     ObjectId;
            public int     SourceId;            // which damage source owns this routine
            public int     LaneIndex;           // horizontal lane index for non-overlap
            public float   Damage;              // per-hit damage (ceiling int used for matching)
            public float   TotalDamage;         // cumulative sum of all damage merged into this entry
            public int     HitCount;            // 1 = "1", 2 = "1 x2", etc.
            public float   Age;                 // seconds since spawned (used for fade-in scale)
            public float   TimeSinceLastHit;    // -FadeIn at spawn; reset to 0 on each hit; removed at FadeOut
            public Vector2 BasePosition;        // world position updated by Notify
            public Vector2 DriftOffset;         // (laneX, accumulated vertical drift)
            public Vector2 DriftVelocity;       // purely vertical drift — horizontal lane set at spawn
            public float   BounceTimer;         // 0 = no bounce; counts up through BounceIn+BounceOut
            public float   BounceFromScale;     // scale at the moment the bounce was triggered
            public Color   TextColor;           // render color (set at spawn; alpha multiplied at draw time)
        }

        private struct PendingEntry
        {
            public int     SourceId;
            public Vector2 Position;
            public float   Damage;
            public Color   Color;
            public bool    IsNewHit; // true if any caller this frame flagged a new contact
        }

        private static Color DefaultTextColor => ColorPalette.DamageNumber;

        private static readonly List<StackEntry>                  _active  = new();
        private static readonly Dictionary<(int, int), PendingEntry> _pending = new();

        // ── Bounce animation constants ────────────────────────────────────────
        private const float BounceIn   = 0.05f;
        private const float BounceOut  = 0.10f;
        private const float BouncePeak = 1.5f;

        // ── Drift constants ───────────────────────────────────────────────────
        // When vertical drift exceeds this the text snaps back to its lane origin.
        private const float MaxDriftDistance = 80f;
        // Horizontal spacing (pixels) between per-source lanes for the same target.
        private const float LaneWidth = 44f;

        // ── Cached SQL settings ───────────────────────────────────────────────

        private static (float FadeIn, float Hold, float FadeOut)? _cachedAnim;
        private static (float FadeIn, float Hold, float FadeOut) Anim =>
            _cachedAnim ??= DatabaseFetch.GetAnimSetting("DamageNumAnim", 0.12f, 0.8f, 0.8f);
        public static float FadeIn  => Anim.FadeIn;
        public static float Hold    => Anim.Hold;
        public static float FadeOut => Anim.FadeOut;

        private static float? _cachedDriftSpeed;
        public static float DriftSpeed
        {
            get
            {
                _cachedDriftSpeed ??= DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "DamageNumDriftSpeed", 28f);
                return _cachedDriftSpeed.Value;
            }
        }

        private static float? _cachedScaleStart;
        public static float ScaleStart
        {
            get
            {
                _cachedScaleStart ??= DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "DamageNumScaleStart", 0.5f);
                return _cachedScaleStart.Value;
            }
        }

        private static float? _cachedScalePeak;
        public static float ScalePeak
        {
            get
            {
                _cachedScalePeak ??= DatabaseFetch.GetSetting<float>("FXSettings", "Value", "SettingKey", "DamageNumScalePeak", 1.4f);
                return _cachedScalePeak.Value;
            }
        }

        // Returns the current CombatText mode: "None", "Limited", or "All".
        // Falls back to "Limited" if the enum is not yet registered.
        private static string CombatTextMode =>
            ControlStateManager.ContainsEnumState("CombatText")
                ? ControlStateManager.GetEnumValue("CombatText")
                : "Limited";

        // ── Lane layout ───────────────────────────────────────────────────────
        // Lane 0 = 0, Lane 1 = -LaneWidth, Lane 2 = +LaneWidth, Lane 3 = -2*LaneWidth, …
        private static float LaneBaseX(int laneIndex)
        {
            int   half = (laneIndex + 1) / 2;
            float sign = laneIndex % 2 == 0 ? 1f : -1f;
            return sign * half * LaneWidth;
        }

        // ── Damage notification ───────────────────────────────────────────────
        // Called from any damage site; accumulates per (object, source) per frame.
        // sourceId identifies the agent or system dealing the damage (e.g. bullet.OwnerID).

        public static void Notify(int objectId, Vector2 position, float damage, int sourceId = 0, Color color = default, bool isNewHit = false, bool isBulletDamage = false)
        {
            if (damage <= 0f) return;
            string mode = CombatTextMode;
            if (mode == "None") return;
            if (mode == "Limited" && isBulletDamage) return;
            var key = (objectId, sourceId);
            if (_pending.TryGetValue(key, out var existing))
                _pending[key] = new PendingEntry { SourceId = sourceId, Position = position, Damage = existing.Damage + damage, Color = existing.Color.A > 0 ? existing.Color : color, IsNewHit = existing.IsNewHit || isNewHit };
            else
                _pending[key] = new PendingEntry { SourceId = sourceId, Position = position, Damage = damage, Color = color, IsNewHit = isNewHit };
        }

        // ── Flush ─────────────────────────────────────────────────────────────
        // Called once per frame after all physics.  Merges pending damage into per-source entries.

        public static void Flush()
        {
            if (_pending.Count == 0) return;

            float fadeIn = FadeIn;

            foreach (var kvp in _pending)
            {
                int     objId    = kvp.Key.Item1;
                int     srcId    = kvp.Key.Item2;
                Vector2 pos      = kvp.Value.Position;
                float   damage   = kvp.Value.Damage;
                bool    isNewHit = kvp.Value.IsNewHit;

                // Find existing active entry for this (object, source) pair.
                int existingIdx = -1;
                for (int i = 0; i < _active.Count; i++)
                {
                    if (_active[i].ObjectId == objId && _active[i].SourceId == srcId)
                    { existingIdx = i; break; }
                }

                Color textColor = kvp.Value.Color.A > 0 ? kvp.Value.Color : DefaultTextColor;

                if (existingIdx < 0)
                {
                    // Assign the lowest lane index not currently used by any entry for this object.
                    int  laneIndex = 0;
                    bool laneInUse;
                    do
                    {
                        laneInUse = false;
                        for (int i = 0; i < _active.Count; i++)
                        {
                            if (_active[i].ObjectId == objId && _active[i].LaneIndex == laneIndex)
                            { laneInUse = true; laneIndex++; break; }
                        }
                    } while (laneInUse);

                    float laneX = LaneBaseX(laneIndex);

                    _active.Add(new StackEntry
                    {
                        ObjectId         = objId,
                        SourceId         = srcId,
                        LaneIndex        = laneIndex,
                        Damage           = damage,
                        TotalDamage      = damage,
                        HitCount         = 1,
                        Age              = 0f,
                        TimeSinceLastHit = -fadeIn,
                        BasePosition     = pos,
                        DriftOffset      = new Vector2(laneX, 0f),
                        DriftVelocity    = new Vector2(0f, -DriftSpeed),
                        BounceTimer      = 0f,
                        BounceFromScale  = 1f,
                        TextColor        = textColor,
                    });
                }
                else
                {
                    var e = _active[existingIdx];
                    e.BasePosition     = pos;
                    e.Damage           = damage;
                    e.TotalDamage     += damage;
                    e.TimeSinceLastHit = 0f;

                    // Increment hit counter if the caller flagged a new contact event,
                    // or if the accumulated per-frame damage is large enough to be a real hit.
                    if (isNewHit || damage >= 0.5f)
                    {
                        e.HitCount++;
                        e.BounceFromScale = ComputeBounceMultiplier(e);
                        e.BounceTimer     = 0f;
                    }

                    _active[existingIdx] = e;
                }
            }

            _pending.Clear();
        }

        // ── Update ────────────────────────────────────────────────────────────

        public static void Update(float dt)
        {
            float hold    = Hold;
            float fadeOut = FadeOut;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                e.Age              += dt;
                e.TimeSinceLastHit += dt;
                e.DriftOffset      += e.DriftVelocity * dt;
                if (e.BounceTimer < BounceIn + BounceOut)
                    e.BounceTimer = MathF.Min(e.BounceTimer + dt, BounceIn + BounceOut);

                // Reset vertical drift when the text has risen far enough, keeping the lane X.
                if (e.DriftOffset.Y <= -MaxDriftDistance)
                    e.DriftOffset = new Vector2(LaneBaseX(e.LaneIndex), 0f);

                if (e.TimeSinceLastHit >= hold + fadeOut)
                    _active.RemoveAt(i);
                else
                    _active[i] = e;
            }
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (CombatTextMode == "None") return;
            if (_active.Count == 0) return;

            SpriteFont font = UIStyle.FontH2.Font;
            if (font == null) return;

            float fadeIn     = FadeIn;
            float hold       = Hold;
            float fadeOut    = FadeOut;
            float scaleStart = ScaleStart;
            float scalePeak  = ScalePeak;
            float baseScale  = UIStyle.FontH2.Scale;

            foreach (var e in _active)
            {
                float alpha = ComputeAlpha(e, fadeIn, hold, fadeOut);
                if (alpha <= 0f) continue;

                float finalScale = ComputeBaseScale(e, fadeIn, scalePeak, scaleStart) *
                                   ComputeBounceMultiplier(e) *
                                   baseScale;

                int displayedTotal = e.TotalDamage < 1f ? 0 : (int)MathF.Round(e.TotalDamage);
                string text = e.HitCount > 1 ? $"{displayedTotal} x{e.HitCount}" : displayedTotal.ToString();

                Vector2 drawPos = e.BasePosition + e.DriftOffset;
                Vector2 origin  = font.MeasureString(text) * 0.5f;
                Color   color   = e.TextColor * alpha;

                spriteBatch.DrawString(font, text, drawPos, color, 0f, origin, finalScale, SpriteEffects.None, 0f);
            }
        }

        // ── Animation helpers ─────────────────────────────────────────────────

        private static float ComputeAlpha(in StackEntry e, float fadeIn, float hold, float fadeOut)
        {
            // Fade in: TimeSinceLastHit starts at -FadeIn and climbs to 0.
            if (e.TimeSinceLastHit < 0f)
                return fadeIn > 0f ? 1f + e.TimeSinceLastHit / fadeIn : 1f;

            // Hold: full opacity until hold duration elapses.  A hit resets TimeSinceLastHit to 0.
            if (e.TimeSinceLastHit < hold)
                return 1f;

            // Fade out: hold → hold+FadeOut.
            float t = e.TimeSinceLastHit - hold;
            return fadeOut > 0f ? MathHelper.Clamp(1f - t / fadeOut, 0f, 1f) : 0f;
        }

        private static float ComputeBaseScale(in StackEntry e)
        {
            return ComputeBaseScale(e, FadeIn, ScalePeak, ScaleStart);
        }

        private static float ComputeBaseScale(in StackEntry e, float fadeIn, float scalePeak, float scaleStart)
        {
            if (e.Age < fadeIn)
            {
                float t = fadeIn > 0f ? e.Age / fadeIn : 1f;
                return MathHelper.Lerp(scaleStart, scalePeak, t);
            }
            return scalePeak;
        }

        private static float ComputeBounceMultiplier(in StackEntry e)
        {
            if (e.BounceTimer <= 0f) return 1f;

            // Diminish peak with hit count via natural log so rapid stacking never blows up:
            //   HitCount=1 → peak=1.5,  HitCount=2 → ~1.38,  HitCount=10 → ~1.20
            float effectivePeak = 1f + (BouncePeak - 1f) / MathF.Log(e.HitCount + MathF.E - 1f);

            if (e.BounceTimer <= BounceIn)
            {
                float t = BounceIn > 0f ? e.BounceTimer / BounceIn : 1f;
                return MathHelper.Lerp(e.BounceFromScale, effectivePeak, t);
            }
            else
            {
                float t = BounceOut > 0f ? (e.BounceTimer - BounceIn) / BounceOut : 1f;
                return MathHelper.Lerp(effectivePeak, 1f, t);
            }
        }
    }
}
