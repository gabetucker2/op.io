using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.FunCol;
using op.io.UI.FunCol.Features;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class MathBlock
    {
        public const string BlockTitle = "Math";
        public const int MinWidth  = 300;
        public const int MinHeight = 100;

        // ── Constants section ──────────────────────────────────────────────────
        private static readonly List<ConstEntry> _consts  = BuildConstants();
        private static FunColInterface _constHeaderFunCol;
        private static readonly Dictionary<string, FunColInterface> _constRowFunCols =
            new(StringComparer.OrdinalIgnoreCase);
        private static string _hoveredConstKey;

        private const float ColSymWeight   = 0.13f;
        private const float ColConstWeight = 0.47f;
        private const float ColValWeight   = 0.40f;

        // ── Equations section ──────────────────────────────────────────────────
        private static readonly List<MathEntry> _entries = BuildEntries();
        private static FunColInterface _eqHeaderFunCol;
        private static readonly Dictionary<string, FunColInterface> _eqRowFunCols =
            new(StringComparer.OrdinalIgnoreCase);
        private static string _hoveredEqKey;

        private const float ColNameWeight = 0.32f;
        private const float ColEqWeight   = 0.68f;

        private static readonly BlockScrollPanel _scrollPanel = new();

        // ── Shared ─────────────────────────────────────────────────────────────
        private static Texture2D _pixelTexture;
        private static float _lineHeightCache;
        private const int HeaderRowHeight = 16;
        private const int DividerHeight   = 3;
        private static bool _constHeaderVisibleLoaded;
        private static bool _eqHeaderVisibleLoaded;

        // ── Tooltip support ────────────────────────────────────────────────────

        public static string GetHoveredRowKey() => _hoveredConstKey ?? _hoveredEqKey;

        public static string GetHoveredRowLabel()
        {
            if (!string.IsNullOrEmpty(_hoveredConstKey))
            {
                foreach (ConstEntry e in _consts)
                    if (string.Equals(e.Key, _hoveredConstKey, StringComparison.OrdinalIgnoreCase))
                        return e.Name;
            }
            if (!string.IsNullOrEmpty(_hoveredEqKey))
            {
                foreach (MathEntry e in _entries)
                    if (string.Equals(e.Key, _hoveredEqKey, StringComparison.OrdinalIgnoreCase))
                        return e.Name;
            }
            return null;
        }

        public static IEnumerable<(string Key, string Text)> GetTooltipEntries()
        {
            foreach (ConstEntry e in _consts)
                if (!string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Description))
                    yield return (e.Key, e.Description);
            foreach (MathEntry e in _entries)
                if (!e.IsSection && !string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Description))
                    yield return (e.Key, e.Description);
        }

        // ── Update ─────────────────────────────────────────────────────────────

        public static void Update(GameTime gameTime, Rectangle contentBounds,
            MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Math);

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _scrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            MouseState effectiveMouse = blockLocked ? previousMouseState : mouseState;

            // ── Sticky const header; everything below scrolls ──
            var constHfc = GetOrEnsureConstHeaderFunCol();
            if (!_constHeaderVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Math);
                if (rowData.TryGetValue("ConstHeaderVisible", out string stored))
                    constHfc.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                _constHeaderVisibleLoaded = true;
            }

            var eqHfc = GetOrEnsureEqHeaderFunCol();
            if (!_eqHeaderVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Math);
                if (rowData.TryGetValue("EqHeaderVisible", out string stored))
                    eqHfc.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                _eqHeaderVisibleLoaded = true;
            }

            int constHeaderH = constHfc.HeaderVisible ? HeaderRowHeight : 0;
            int eqHeaderH    = eqHfc.HeaderVisible    ? HeaderRowHeight : 0;

            var constHeaderBounds = new Rectangle(contentBounds.X, contentBounds.Y,
                                        contentBounds.Width, constHeaderH);
            var scrollArea = new Rectangle(contentBounds.X, contentBounds.Y + constHeaderH,
                                 contentBounds.Width, Math.Max(0, contentBounds.Height - constHeaderH));

            float constContentH = _consts.Count * _lineHeightCache;
            float eqContentH    = eqHeaderH + _entries.Count * _lineHeightCache;
            float totalContentH = constContentH + DividerHeight + eqContentH;

            _scrollPanel.Update(scrollArea, totalContentH, effectiveMouse, previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty) listBounds = scrollArea;

            float scrollOffset    = _scrollPanel.ScrollOffset;
            float constRowsStartY = scrollArea.Y - scrollOffset;
            float eqHeaderStartY  = constRowsStartY + constContentH + DividerHeight;
            float eqRowsStartY    = eqHeaderStartY + eqHeaderH;

            UpdateConstBounds(listBounds, constRowsStartY);
            UpdateEqBounds(listBounds, eqRowsStartY);

            foreach (ConstEntry e in _consts)
                if (e.Bounds != Rectangle.Empty)
                    GetOrCreateConstFunCol(e.Key).Update(e.Bounds, mouseState, dt, blockLocked);

            bool inScrollArea = scrollArea.Contains(mouseState.Position);
            _hoveredConstKey = !blockLocked && inScrollArea ? HitTestConst(mouseState.Position) : null;

            foreach (MathEntry e in _entries)
                if (!e.IsSection && e.Bounds != Rectangle.Empty)
                    GetOrCreateEqFunCol(e.Key).Update(e.Bounds, mouseState, dt, blockLocked);

            _hoveredEqKey = !blockLocked && inScrollArea && _hoveredConstKey == null
                ? HitTestEq(mouseState.Position) : null;

            constHfc.ShowHeaderToggle = BlockManager.DockingModeEnabled;
            constHfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            constHfc.UpdateHeaderHover(constHeaderBounds, mouseState,
                blockLocked ? (MouseState?)previousMouseState : null);
            if (constHfc.HeaderToggleClicked)
                BlockDataStore.SetRowData(DockBlockKind.Math, "ConstHeaderVisible", constHfc.HeaderVisible ? "true" : "false");

            var eqHeaderRect = new Rectangle(listBounds.X, (int)eqHeaderStartY,
                                   listBounds.Width, eqHeaderH);
            eqHfc.ShowHeaderToggle = BlockManager.DockingModeEnabled;
            eqHfc.CollapsedToggleBounds = new Rectangle(listBounds.X, (int)eqHeaderStartY, listBounds.Width, HeaderRowHeight);
            eqHfc.UpdateHeaderHover(eqHeaderRect, mouseState,
                blockLocked ? (MouseState?)previousMouseState : null);
            if (eqHfc.HeaderToggleClicked)
                BlockDataStore.SetRowData(DockBlockKind.Math, "EqHeaderVisible", eqHfc.HeaderVisible ? "true" : "false");
        }

        // ── Draw ───────────────────────────────────────────────────────────────

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null) return;

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Math);

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
                return;

            if (_lineHeightCache <= 0f)
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);

            EnsurePixelTexture();

            var mathConstHfc = GetOrEnsureConstHeaderFunCol();
            int mathConstHdrH = mathConstHfc.HeaderVisible ? HeaderRowHeight : 0;
            var constHeaderStrip = new Rectangle(contentBounds.X, contentBounds.Y,
                                       contentBounds.Width, mathConstHdrH);
            var scrollArea = new Rectangle(contentBounds.X, contentBounds.Y + mathConstHdrH,
                                 contentBounds.Width, Math.Max(0, contentBounds.Height - mathConstHdrH));

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty) listBounds = scrollArea;

            float scrollOffset    = _scrollPanel.ScrollOffset;
            float constContentH   = _consts.Count * _lineHeightCache;
            float constRowsStartY = scrollArea.Y - scrollOffset;
            int   dividerY        = (int)(constRowsStartY + constContentH);
            int   eqHeaderStartY  = dividerY + DividerHeight;

            var gd      = spriteBatch.GraphicsDevice;
            float uiScale = BlockManager.UIScale;

            // ── Scissored: const rows, divider, eq header, eq rows ──
            DrawScissored(spriteBatch, gd, uiScale, listBounds, scrollArea, boldFont,
                dividerY, eqHeaderStartY);

            // ── Scrollbar ──
            _scrollPanel.Draw(spriteBatch, blockLocked);

            // ── Sticky const header ──
            mathConstHfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            mathConstHfc.DrawHeader(spriteBatch, constHeaderStrip, boldFont, _pixelTexture);
        }

        // ── Scissored draw helper ───────────────────────────────────────────────

        private static void DrawScissored(SpriteBatch spriteBatch, GraphicsDevice gd,
            float uiScale, Rectangle listBounds, Rectangle scrollArea,
            UIStyle.UIFont boldFont, int dividerY, int eqHeaderStartY)
        {
            Rectangle scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(scrollArea.X      * uiScale),
                    (int)(scrollArea.Y      * uiScale),
                    (int)(scrollArea.Width  * uiScale),
                    (int)(scrollArea.Height * uiScale))
                : scrollArea;
            var viewport = gd.Viewport;
            scissorRect.X      = Math.Clamp(scissorRect.X,      0, viewport.Width);
            scissorRect.Y      = Math.Clamp(scissorRect.Y,      0, viewport.Height);
            scissorRect.Width  = Math.Clamp(scissorRect.Width,  0, viewport.Width  - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, viewport.Height - scissorRect.Y);

            spriteBatch.End();
            var scissorState = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            gd.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            DrawConstRows(spriteBatch, listBounds, boldFont);
            FillRect(spriteBatch, new Rectangle(scrollArea.X, dividerY, scrollArea.Width, DividerHeight),
                new Color(50, 50, 65, 200));
            var mathEqHfc = GetOrEnsureEqHeaderFunCol();
            int mathEqHdrH = mathEqHfc.HeaderVisible ? HeaderRowHeight : 0;
            var eqHeaderRect = new Rectangle(listBounds.X, eqHeaderStartY, listBounds.Width, mathEqHdrH);
            mathEqHfc.CollapsedToggleBounds = new Rectangle(listBounds.X, eqHeaderStartY, listBounds.Width, HeaderRowHeight);
            mathEqHfc.DrawHeader(spriteBatch, eqHeaderRect, boldFont, _pixelTexture);
            DrawEqRows(spriteBatch, listBounds, boldFont);

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);
        }

        private static void DrawConstRows(SpriteBatch spriteBatch, Rectangle listBounds, UIStyle.UIFont font)
        {
            foreach (ConstEntry e in _consts)
            {
                Rectangle b = e.Bounds;
                if (b == Rectangle.Empty) continue;
                if (b.Y >= listBounds.Bottom) break;
                if (b.Bottom <= listBounds.Y) continue;
                if (!string.IsNullOrEmpty(_hoveredConstKey) &&
                    string.Equals(_hoveredConstKey, e.Key, StringComparison.OrdinalIgnoreCase))
                    FillRect(spriteBatch, b, ColorPalette.RowHover);
                FunColInterface fc = GetOrCreateConstFunCol(e.Key);
                if (fc.GetFeature(0) is TextLabelFeature sym)  sym.Text  = e.Symbol ?? string.Empty;
                if (fc.GetFeature(1) is TextLabelFeature name) name.Text = e.Name   ?? string.Empty;
                if (fc.GetFeature(2) is TextLabelFeature val)  val.Text  = e.GetValue();
                fc.Draw(spriteBatch, b, font, _pixelTexture);
            }
        }

        private static void DrawEqRows(SpriteBatch spriteBatch, Rectangle listBounds, UIStyle.UIFont font)
        {
            foreach (MathEntry e in _entries)
            {
                Rectangle b = e.Bounds;
                if (b == Rectangle.Empty) continue;
                if (b.Y >= listBounds.Bottom) break;
                if (b.Bottom <= listBounds.Y) continue;
                if (e.IsSection)
                {
                    DrawSectionRow(spriteBatch, e, b, font);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_hoveredEqKey) &&
                        string.Equals(_hoveredEqKey, e.Key, StringComparison.OrdinalIgnoreCase))
                        FillRect(spriteBatch, b, ColorPalette.RowHover);
                    FunColInterface fc = GetOrCreateEqFunCol(e.Key);
                    if (fc.GetFeature(0) is TextLabelFeature nameF) nameF.Text = e.Name     ?? e.Key;
                    if (fc.GetFeature(1) is TextLabelFeature eqF)   eqF.Text   = e.Equation ?? string.Empty;
                    fc.Draw(spriteBatch, b, font, _pixelTexture);
                }
            }
        }

        // ── Bounds helpers ─────────────────────────────────────────────────────

        private static void UpdateConstBounds(Rectangle listBounds, float startY)
        {
            if (_lineHeightCache <= 0f) return;
            int rowH = (int)MathF.Ceiling(_lineHeightCache);
            float y  = startY;
            for (int i = 0; i < _consts.Count; i++)
            {
                _consts[i].Bounds = new Rectangle(listBounds.X, (int)MathF.Round(y), listBounds.Width, rowH);
                y += _lineHeightCache;
            }
        }

        private static void UpdateEqBounds(Rectangle listBounds, float startY)
        {
            if (_lineHeightCache <= 0f) return;
            int rowH = (int)MathF.Ceiling(_lineHeightCache);
            float y  = startY;
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Bounds = new Rectangle(listBounds.X, (int)MathF.Round(y), listBounds.Width, rowH);
                y += _lineHeightCache;
            }
        }

        private static string HitTestConst(Point position)
        {
            foreach (ConstEntry e in _consts)
                if (e.Bounds.Contains(position)) return e.Key;
            return null;
        }

        private static string HitTestEq(Point position)
        {
            foreach (MathEntry e in _entries)
                if (!e.IsSection && e.Bounds.Contains(position)) return e.Key;
            return null;
        }

        // ── FunCol factories ───────────────────────────────────────────────────

        private static FunColInterface GetOrCreateConstFunCol(string key)
        {
            if (_constRowFunCols.TryGetValue(key, out FunColInterface existing)) return existing;
            var fc = new FunColInterface(
                new float[] { ColSymWeight, ColConstWeight, ColValWeight },
                new TextLabelFeature("Symbol",   FunColTextAlign.Left),
                new TextLabelFeature("Constant", FunColTextAlign.Left),
                new TextLabelFeature("Value",    FunColTextAlign.Left));
            fc.DisableExpansion = true;
            fc.DisableColors    = true;
            fc.SuppressTooltipWarnings = true;
            _constRowFunCols[key] = fc;
            return fc;
        }

        private static FunColInterface GetOrEnsureConstHeaderFunCol()
        {
            if (_constHeaderFunCol != null) return _constHeaderFunCol;
            _constHeaderFunCol = new FunColInterface(
                new float[] { ColSymWeight, ColConstWeight, ColValWeight },
                new TextLabelFeature("Symbol",   FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Mathematical symbol or abbreviation"] },
                new TextLabelFeature("Constant", FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Name of the mathematical constant"] },
                new TextLabelFeature("Value",    FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Computed or defined numeric value"] });
            _constHeaderFunCol.DisableExpansion = true;
            _constHeaderFunCol.DisableColors    = true;
            _constHeaderFunCol.ShowHeaderTooltips = true;
            return _constHeaderFunCol;
        }

        private static FunColInterface GetOrCreateEqFunCol(string key)
        {
            if (_eqRowFunCols.TryGetValue(key, out FunColInterface existing)) return existing;
            var fc = new FunColInterface(
                new float[] { ColNameWeight, ColEqWeight },
                new TextLabelFeature("Name",     FunColTextAlign.Left),
                new TextLabelFeature("Equation", FunColTextAlign.Left));
            fc.DisableExpansion = true;
            fc.DisableColors    = true;
            fc.SuppressTooltipWarnings = true;
            _eqRowFunCols[key] = fc;
            return fc;
        }

        private static FunColInterface GetOrEnsureEqHeaderFunCol()
        {
            if (_eqHeaderFunCol != null) return _eqHeaderFunCol;
            _eqHeaderFunCol = new FunColInterface(
                new float[] { ColNameWeight, ColEqWeight },
                new TextLabelFeature("Name",     FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Name of the equation or formula"] },
                new TextLabelFeature("Equation", FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Mathematical expression or formula"] });
            _eqHeaderFunCol.DisableExpansion = true;
            _eqHeaderFunCol.DisableColors    = true;
            _eqHeaderFunCol.ShowHeaderTooltips = true;
            return _eqHeaderFunCol;
        }

        // ── Section row drawing ────────────────────────────────────────────────

        private static void DrawSectionRow(SpriteBatch spriteBatch, MathEntry entry,
            Rectangle rowBounds, UIStyle.UIFont font)
        {
            FillRect(spriteBatch, rowBounds, new Color(28, 28, 38, 180));
            if (!font.IsAvailable || string.IsNullOrEmpty(entry.Name)) return;
            Vector2 size = font.MeasureString(entry.Name);
            float tx = rowBounds.X + (rowBounds.Width - size.X) / 2f;
            float ty = rowBounds.Y + (rowBounds.Height - size.Y) / 2f;
            font.DrawString(spriteBatch, entry.Name, new Vector2(tx, ty), UIStyle.MutedTextColor);
        }

        // ── Pixel texture ──────────────────────────────────────────────────────

        private static void EnsurePixelTexture()
        {
            if (_pixelTexture != null || Core.Instance?.GraphicsDevice == null) return;
            _pixelTexture = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        private static void FillRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || spriteBatch == null ||
                bounds.Width <= 0 || bounds.Height <= 0) return;
            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        // ── Value formatting ───────────────────────────────────────────────────

        private static string Fmt(float v)
        {
            if (v == MathF.Floor(v)) return ((int)v).ToString();
            return v.ToString("G4");
        }

        private static string SafeVal(Func<float> getter, string suffix = "")
        {
            try { return Fmt(getter()) + suffix; }
            catch { return "?"; }
        }

        // ── Constants data ─────────────────────────────────────────────────────

        private static List<ConstEntry> BuildConstants() => new List<ConstEntry>
        {
            // ── Bullet Physics (DB-driven) ──────────────────────────────────────
            Con("const_airR",  "airR",   "Air Resistance Scalar",   () => SafeVal(() => BulletManager.AirResistanceScalar),
                "Scales all bullet drag. drag = airR * bulletVolume / dragFactor"),
            Con("const_lossB", "loss_b", "Bounce Velocity Loss",    () => SafeVal(() => BulletManager.BounceVelocityLoss),
                "Fraction of bullet speed lost per bounce off static surfaces"),
            Con("const_lossH", "loss_h", "Hit Velocity Loss",       () => SafeVal(() => BulletManager.HitVelocityLoss),
                "Fraction of bullet speed lost per frame while penetrating objects"),
            Con("const_k",     "k",      "Penetration Spring",      () => SafeVal(() => BulletManager.PenetrationSpringCoeff),
                "Spring stiffness pushing bullet out of penetrated bodies; higher = shallower embed"),
            Con("const_c",     "c",      "Penetration Damping",     () => SafeVal(() => BulletManager.PenetrationDamping),
                "Damping coefficient; ~89 = critically damped (no overshoot); higher = overdamped/sticky"),

            // ── Bullet Defaults (DB-driven) ─────────────────────────────────────
            Con("const_v0",    "v0",     "Default Bullet Speed",    () => SafeVal(() => BulletManager.DefaultBulletSpeed,    " px/s"),
                "Fallback bullet speed when the barrel prototype has no value set"),
            Con("const_tmax",  "t_max",  "Default Lifespan",        () => SafeVal(() => BulletManager.DefaultBulletLifespan, " s"),
                "Fallback bullet lifespan in seconds when the barrel has no value set"),
            Con("const_Fd",    "Fd",     "Default Drag Factor",     () => SafeVal(() => BulletManager.DefaultBulletDragFactor),
                "Fallback drag factor denominator in: drag = airR * volume / Fd"),
            Con("const_m0",    "m0",     "Default Bullet Mass",     () => SafeVal(() => BulletManager.DefaultBulletMass),
                "Fallback bullet mass when the barrel prototype has no value set"),
            Con("const_D0",    "D0",     "Default Bullet Damage",   () => SafeVal(() => BulletManager.DefaultBulletDamage),
                "Fallback bullet damage when the barrel prototype has no value set"),
            Con("const_HPb",   "HP_b",   "Default Bullet Pen. HP",  () => SafeVal(() => BulletManager.DefaultBulletHealth),
                "Fallback bullet penetration HP when the barrel prototype has no value set"),

            // ── Code-defined constants ──────────────────────────────────────────
            Con("const_fric",  "fric",   "Physics Friction Rate",   () => "6 /s",
                "Rate at which physics impulse velocity decays per second: vP *= clamp(1 - fric*dt, 0, 1)"),
            Con("const_angA",  "angA",   "Angular Accel Factor",    () => "4",
                "Multiplier on RotationSpeed to compute max angular acceleration: accel = RotSpeed * angA * dt"),
            Con("const_animS", "animS",  "Barrel Switch Speed",     () => "15 /s",
                "Exponential lerp speed for the barrel carousel switch animation"),

            // ── Gameplay defaults (DB-defaults shown for reference) ─────────────
            Con("const_HPr",   "HP_r",   "Player HP Regen",         () => "5 /s",
                "Default player health regeneration rate set in the database"),
            Con("const_HPd",   "HP_d",   "HP Regen Delay",          () => "5 s",
                "Seconds after taking damage before player HP regeneration resumes"),
            Con("const_SHr",   "SH_r",   "Player Shield Regen",     () => "3 /s",
                "Default player shield regeneration rate set in the database"),
            Con("const_SHd",   "SH_d",   "Shield Regen Delay",      () => "3 s",
                "Seconds after taking damage before player shield regeneration resumes"),
            Con("const_FHPr",  "FHP_r",  "Farm HP Regen Rate",      () => "5% /s",
                "Farm objects regenerate health at 5% of MaxHP per second after their delay"),
            Con("const_FHPd",  "FHP_d",  "Farm HP Regen Delay",     () => "6 s",
                "Seconds after taking damage before farm HP regeneration resumes"),
            Con("const_XPmax", "XP_max", "Player Max XP",           () => "100",
                "Default XP required to fill the XP bar (set when the MaxXP column is added)"),
            Con("const_kBdef", "kB_def", "Default Body Knockback",  () => "20",
                "Default BodyKnockback value assigned to new agents in the database"),
        };

        private static ConstEntry Con(string key, string symbol, string name,
            Func<string> value, string description)
            => new ConstEntry { Key = key, Symbol = symbol, Name = name,
                                GetValue = value, Description = description };

        // ── Equations data ─────────────────────────────────────────────────────

        private static List<MathEntry> BuildEntries() => new List<MathEntry>
        {
            // Collision ─────────────────────────────────────────────────────────
            Sec("Collision"),
            Row("math_sep_a",       "Separation A",       "pos += mtv * (mB / totalM)",           "Pushes A out of B; heavier objects move less distance"),
            Row("math_sep_b",       "Separation B",       "pos -= mtv * (mA / totalM)",           "Pushes B out of A; heavier objects move less distance"),
            Row("math_frame_vel",   "Frame Velocity",     "v = dPos / dt",                        "Estimates velocity from position change this frame"),
            Row("math_restitution", "Restitution",        "e = min((kA+kB)/2, 1)",                "Blended bounciness; 0 = inelastic, 1 = fully elastic"),
            Row("math_impulse_j",   "Impulse Magnitude",  "j = (1+e)*vRel.n / (1/mA+1/mB)",      "Impulse needed to reverse relative approach velocity at contact"),
            Row("math_impulse_a",   "Impulse -> A",       "dvA = j * n / mA",                     "Velocity change applied to A from collision impulse along normal"),
            Row("math_impulse_b",   "Impulse -> B",       "dvB = j * n / mB",                     "Velocity change applied to B from collision impulse along normal"),
            Row("math_knock_a",     "Knockback -> A",     "dvA += kSum * n / mA",                 "Extra velocity kick to A from the sum of both objects' knockback stats"),
            Row("math_knock_b",     "Knockback -> B",     "dvB += kSum * n / mB",                 "Extra velocity kick to B from the sum of both objects' knockback stats"),
            Row("math_contact_dmg", "Contact Damage",     "dmg = collisionDmg * dt",              "Continuous damage dealt per frame while two objects are overlapping"),

            // Bullet Collision ──────────────────────────────────────────────────
            Sec("Bullet Collision"),
            Row("math_reflect",     "Bullet Reflect",     "v' = v - 2*(v.n)*n",                   "Mirrors bullet velocity across the surface normal on bounce"),
            Row("math_bounce_f",    "Bounce Fraction",    "f = mObj / (mBullet + mObj)",           "Heavier targets absorb more of the bullet's speed on bounce"),
            Row("math_speed_ret",   "Speed Retained",     "s = 1 - loss * f",                     "Fraction of bullet speed kept after bouncing off a surface"),
            Row("math_spring_a",    "Spring Accel",       "a = k * penetration",                  "Pushes bullet outward, proportional to how deep it has penetrated"),
            Row("math_damp_a",      "Damp Accel",         "a = -c * (v.n)",                       "Opposes bullet's approach velocity to prevent oscillation during penetration"),
            Row("math_pen_step",    "Penetration Step",   "dv = (aSpring + aDamp) * dt",          "Combined spring+damping velocity change applied per frame"),
            Row("math_pen_drain",   "Pen HP Drain",       "dHP = depth * loss * f * dt",          "Depletes bullet penetration HP based on overlap depth and target mass ratio"),

            // Swept Collision ───────────────────────────────────────────────────
            Sec("Swept Collision"),
            Row("math_sweep_a",     "Sweep a",            "a = sweepD . sweepD",                  "Quadratic coefficient a: squared length of bullet's travel vector"),
            Row("math_sweep_b",     "Sweep b",            "b = 2 * (w . sweepD)",                 "Quadratic coefficient b: 2x dot of initial separation and sweep delta"),
            Row("math_sweep_c",     "Sweep c",            "c = w.w - (rA+rB)^2",                  "Quadratic coefficient c: initial distance^2 minus combined radii^2"),
            Row("math_discrim",     "Discriminant",       "D = b^2 - 4*a*c",                      "Positive D means the bullet path intersects the target circle this frame"),
            Row("math_t_enter",     "Entry Time",         "tEnter = (-b - sqrt(D)) / (2*a)",      "Frame fraction at which the bullet first touches the target"),
            Row("math_t_exit",      "Exit Time",          "tExit = (-b + sqrt(D)) / (2*a)",       "Frame fraction at which the bullet fully exits the target"),
            Row("math_eff_resist",  "Eff. Resistance",    "r = clamp(bodyR - bulletP, 0, 1)",     "Effective resistance after subtracting the bullet's armor-pierce stat"),
            Row("math_eff_dmg",     "Effective Damage",   "dmg = base * (1 - r)",                 "Actual damage delivered after resistance reduction"),
            Row("math_depenet",     "Depenetration",      "dpos = overlap * mEnemy / totalM",     "Pushes bullet back proportional to the enemy's mass share"),
            Row("math_vel_drain",   "Velocity Drain",     "dv = (v.n * mEnemy/totalM) * n",       "Slows bullet along normal, scaled to enemy mass fraction"),
            Row("math_bul_bul_j",   "Bullet-Bullet j",    "j = -2*(vRel.n) / (1/mA+1/mB)",       "Elastic impulse between two colliding bullets (e=1, no energy loss)"),

            // Damage & Health ───────────────────────────────────────────────────
            Sec("Damage & Health"),
            Row("math_shield_abs",  "Shield Absorb",      "sdmg = min(shield, dmg)",              "Shield takes damage first, capped at the current shield value"),
            Row("math_hp_dmg",      "Health Damage",      "hdmg = min(hp, dmg - sdmg)",           "Remaining damage after shield spills onto health"),
            Row("math_total_dmg",   "Total Damage",       "total = sdmg + hdmg",                  "Sum of shield damage and health damage dealt in this hit"),

            // Bullet Physics ────────────────────────────────────────────────────
            Sec("Bullet Physics"),
            Row("math_circle_a",    "Circle Cross-Sect.", "A = pi * (w/2)^2",                     "Cross-sectional area of a circular bullet, used for air drag calculation"),
            Row("math_rect_a",      "Rect Cross-Sect.",   "A = w * h",                            "Cross-sectional area of a rectangular bullet for air drag calculation"),
            Row("math_drag_coeff",  "Air Drag Coeff",     "drag = airR * volume / dragFactor",    "How strongly air resists this particular bullet per unit time"),
            Row("math_vel_decay",   "Velocity Decay",     "v' = v * (1 - drag * dt)",             "Applies air resistance each frame to gradually slow the bullet"),
            Row("math_bul_move",    "Bullet Movement",    "dpos = v * dt",                        "Advances bullet position by velocity times elapsed time"),

            // Forces ────────────────────────────────────────────────────────────
            Sec("Forces"),
            Row("math_newton2",     "Newton 2nd Law",     "a = F / m",                            "Acceleration equals applied force divided by object mass (F = ma)"),
            Row("math_force_int",   "Force Integration",  "dpos = a * dt",                        "Position change from acceleration integrated over one frame"),

            // Angular Motion ────────────────────────────────────────────────────
            Sec("Angular Motion"),
            Row("math_farm_float",  "Farm Float",         "w_t = A * sin((t+p) * f * 2pi)",       "Target angular velocity for floating farm objects; sinusoidal oscillation"),
            Row("math_spin_rev",    "Spin Reversal",      "w_t = A * sin((t+p) * 2pi / T)",       "Periodic reversal of spin direction over full period T"),
            Row("math_ang_accel",   "Angular Accel",      "dw = clamp(w_t - w, -a, a)",           "Smoothly accelerates angular velocity toward its target value"),
            Row("math_rot_step",    "Rotation Step",      "theta += w * dt",                      "Advances rotation angle by angular velocity times elapsed time"),
            Row("math_phys_fric",   "Physics Friction",   "vP *= clamp(1 - fric*dt, 0, 1)",       "Friction-like decay of physics impulse velocity (fric = 6 /s)"),
            Row("math_phys_pos",    "Physics Position",   "pos += vPhys * dt",                    "Moves object by accumulated physics impulse velocity each frame"),

            // Child Objects ─────────────────────────────────────────────────────
            Sec("Child Objects"),
            Row("math_child_rx",    "Child Rotate X",     "x' = x*cos(t) - y*sin(t)",             "Rotates child local X offset by parent's current rotation angle"),
            Row("math_child_ry",    "Child Rotate Y",     "y' = x*sin(t) + y*cos(t)",             "Rotates child local Y offset by parent's current rotation angle"),
            Row("math_centroid",    "Centroid",           "c = (parent + sum(ch)) / (n+1)",       "Geometric center shared by a parent object and all its children"),

            // Bounding ──────────────────────────────────────────────────────────
            Sec("Bounding"),
            Row("math_bound_r",     "Bounding Radius",    "r = sqrt(w^2 + h^2) / 2",              "Radius of the smallest circle that fully encloses a w x h rectangle"),

            // Visual Effects ────────────────────────────────────────────────────
            Sec("Visual Effects"),
            Row("math_flash_int",   "Hit Flash Interrupt","flash = (fi + fo - flash) / fi",       "Resets flash timer so an interrupted flash still fades in cleanly"),
            Row("math_flash_alpha", "Flash Fade Alpha",   "alpha = (t - fadeIn) / fadeOut",       "Linear fade-to-transparent progress during the flash fade-out phase"),

            // Barrel ────────────────────────────────────────────────────────────
            Sec("Barrel"),
            Row("math_barrel_w",    "Barrel Width",       "w = max(1, bodyR * 4/5)",              "Default barrel width derived from body radius"),
            Row("math_barrel_l",    "Barrel Length",      "l = bodyR * 2",                        "Default barrel length derived from body radius"),
            Row("math_barrel_ang",  "Barrel Angle Step",  "theta = 2*pi / barrelCount",           "Evenly distributes barrel slots around the full circle"),
            Row("math_carousel_l",  "Carousel Left",      "i = (i - 1 + n) mod n",               "Wraps active barrel index backward using modulo arithmetic"),
            Row("math_carousel_r",  "Carousel Right",     "i = (i + 1) mod n",                   "Wraps active barrel index forward using modulo arithmetic"),
        };

        private static MathEntry Sec(string name) => new MathEntry
        {
            Key = string.Empty, Name = name,
            Equation = string.Empty, Description = string.Empty,
            IsSection = true,
        };

        private static MathEntry Row(string key, string name, string equation, string description)
            => new MathEntry { Key = key, Name = name, Equation = equation, Description = description };

        // ── Models ──────────────────────────────────────────────────────────────

        private sealed class ConstEntry
        {
            public string    Key         { get; init; }
            public string    Symbol      { get; init; }
            public string    Name        { get; init; }
            public Func<string> GetValue { get; init; }
            public string    Description { get; init; }
            public Rectangle Bounds      { get; set; }
        }

        private sealed class MathEntry
        {
            public string    Key         { get; init; }
            public string    Name        { get; init; }
            public string    Equation    { get; init; }
            public string    Description { get; init; }
            public bool      IsSection   { get; init; }
            public Rectangle Bounds      { get; set; }
        }
    }
}
