using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.FunCol;
using op.io.UI.FunCol.Features;

namespace op.io.UI.BlockScripts.Blocks
{
    /// <summary>
    /// "Bars" block — configure health, shield, and XP bar layout.
    ///
    /// Layout (top to bottom):
    ///   [Top row]    — global Bars on/off toggle + Simulate Damage button
    ///   [Preview]    — player body + barrels rendered at preview position; bars with current values
    ///   [Col header] — column names aligned with the shared FunCol ("Drag Row | Drag Bar | Bar Settings")
    ///   [SHOW list]  — bar rows shown in-game; drag-to-reorder
    ///   [HIDE list]  — bar rows hidden from in-game rendering
    ///   [Buttons]    — Save | Apply | Discard
    ///
    /// One shared FunColInterface spans the full list area (col header + SHOW + HIDE rows).
    /// Its colored background is drawn first; bar row content is drawn on top.
    /// </summary>
    internal static class BarsBlock
    {
        public const string BlockTitle = "Bars";

        // ── Layout constants ─────────────────────────────────────────────────
        private const int Padding         = 8;
        private const int TopBarH         = 26;   // on/off + simulate damage row
        private const int PreviewHeight   = 120;
        private const int PreviewPlayerR  = 26;
        private const int ColHeaderH      = 20;   // column name header row
        private const int SectionLabelH   = 16;   // "SHOW" / "HIDE" section label
        private const int RowHeight       = 28;   // each bar entry row
        private const int ButtonHeight    = 26;
        private const int ButtonSpacing   = 6;
        private const int SegInputWidth   = 48;
        private const int ArrowBtnSize    = 18;
        private const int BarPreviewGap   = 2;
        private const int BorderAccentW   = 3;    // left color border on each bar row entry
        private const int SplitBtnSize    = 12;   // split-out button for Drag Bar mode

        // ── Session state ────────────────────────────────────────────────────
        private static List<BarConfigManager.BarEntry> _snapshot;
        private static List<BarConfigManager.BarEntry> _local;
        private static bool _sessionStarted;
        private static bool _snapshotBarsVisible;

        // ── Simulated stat values ────────────────────────────────────────────
        private static float _simHealth;
        private static float _simShield;
        private static float _simXP;
        private static float _simMaxHealth        = 100f;
        private static float _simMaxShield        = 10f;
        private static float _simMaxXP            = 100f;
        private static float _simLastHealthDmgTime = float.NegativeInfinity;
        private static float _simLastShieldDmgTime = float.NegativeInfinity;
        private static float _simHealthRegen      = 5f;
        private static float _simShieldRegen      = 3f;
        private static float _simHealthRegenDelay = 5f;
        private static float _simShieldRegenDelay = 3f;
        private static float _simTime;

        // ── Shared FunColInterface (covers entire list area) ─────────────────
        private static FunColInterface _listFunCol;
        private static Rectangle _cachedListBounds = Rectangle.Empty;

        // FunCol column indices
        private const int ColDragRow  = 0;  // Green  — drag to reorder whole row
        private const int ColDragBar  = 1;  // Blue   — drag individual bar to merge/split
        private const int ColSegments = 2;  // Red    — configure segment count/visibility

        // ── Segment editing state ────────────────────────────────────────────
        private static string _editingSegBarType;
        private static string _segInputBuffer = "";

        // ── Drag state ───────────────────────────────────────────────────────
        private struct DragState
        {
            public bool     Active;
            public BarType? DraggedBar;    // null = dragging whole row
            public int      DraggedRow;    // BarRow value of source
            public Point    DragStart;
            public int      DropBeforeIdx; // index in combined row list to insert before (-1 = end)
            public bool     DropIntoHide;  // drop target is inside the HIDE section
            public int      DropIntoRow;   // BarRow value to merge into (-1 = none, only for bar drag)
        }
        private static DragState _drag;
        private static MouseState _prevMouse;
        private static Point _lastMousePosition;

        // ── Cached resources ─────────────────────────────────────────────────
        private static Texture2D _pixel;

        // ── Scroll ───────────────────────────────────────────────────────────
        private static readonly BlockScrollPanel _scroll = new();

        // ── Row bounds for hit-testing ───────────────────────────────────────
        private struct BarRowBounds
        {
            public int      BarRow;
            public bool     IsHidden;
            public Rectangle RowRect;
            public List<(BarType Type, Rectangle Bounds)> Entries;
        }
        private static readonly List<BarRowBounds> _rowBounds = new();
        private static readonly List<(BarType Type, int BarRow, Rectangle Rect)> _splitBtnRects = new();

        // ── Section label rects (for section-level drag detection) ────────────
        private static Rectangle _showSectionRect;
        private static Rectangle _hideSectionRect;

        // ── Preview animation ────────────────────────────────────────────────
        private static float _previewRotation;
        private static float _previewAimAngle;

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public static void Update(GameTime gameTime, Rectangle contentBounds,
            MouseState mouseState, MouseState previousMouseState)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _simTime += dt;
            _previewRotation += dt * 0.4f;

            // Compute preview-player center (mirrors DrawPreview layout) and aim at cursor.
            int prevX  = contentBounds.X + Padding;
            int prevW  = contentBounds.Width - Padding * 2;
            int prevCx = prevX + prevW / 2;
            int prevCy = (contentBounds.Y + TopBarH + Padding) + PreviewHeight / 2 - PreviewPlayerR - 10;
            _previewAimAngle = MathF.Atan2(mouseState.Y - prevCy, mouseState.X - prevCx);

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Bars);

            EnsureSession();
            EnsureListFunCol();
            UpdateSimRegen(dt);

            var groups  = GroupByRow(_local);
            var shown   = groups.Where(g => !g[0].IsHidden).ToList();
            var hidden  = groups.Where(g =>  g[0].IsHidden).ToList();

            int x = contentBounds.X + Padding;
            int w = contentBounds.Width - Padding * 2;

            // Fixed header height (preview + button row + column header)
            int fixedHeaderH = PreviewHeight + Padding + TopBarH + Padding + ColHeaderH;
            int fixedFooterH = ButtonHeight + Padding + Padding;
            int listViewportY = contentBounds.Y + fixedHeaderH;
            int listViewportH = Math.Max(0, contentBounds.Height - fixedHeaderH - fixedFooterH);
            var listViewport  = new Rectangle(x, listViewportY, w, listViewportH);

            // FunCol hover detection spans visible list area (column header already drawn above)
            _cachedListBounds = listViewport;

            bool isDragging = _drag.Active;
            _listFunCol.Update(_cachedListBounds, mouseState, dt, suppressHover: isDragging || blockLocked || !BlockManager.DockingModeEnabled);

            if (!blockLocked)
            {
                HandleTopBarClick(mouseState, previousMouseState, contentBounds);
                HandleSegmentInput(mouseState, previousMouseState, shown, hidden);
                HandleSplitButtonClick(mouseState, previousMouseState);
                HandleDrag(mouseState, previousMouseState, shown, hidden);
                HandleButtons(mouseState, previousMouseState, contentBounds, shown, hidden);
            }

            float listContentH = CalculateTotalHeight(shown, hidden);
            _scroll.Update(listViewport, listContentH, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);

            _prevMouse = mouseState;
            _lastMousePosition = mouseState.Position;
        }

        public static void Draw(SpriteBatch sb, Rectangle contentBounds)
        {
            if (sb == null) return;
            EnsurePixel(sb.GraphicsDevice);
            EnsureSession();
            EnsureListFunCol();

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Bars);

            int x = contentBounds.X + Padding;
            int w = contentBounds.Width - Padding * 2;

            // ── Fixed elements (not scrolled) ─────────────────────────────────
            int fy = contentBounds.Y;
            DrawPreview(sb, new Rectangle(x, fy, w, PreviewHeight));
            fy += PreviewHeight + Padding;

            DrawTopBtnRow(sb, x, fy, w, blockLocked);
            fy += TopBarH + Padding;

            DrawColumnHeader(sb, x, fy, w);
            fy += ColHeaderH;

            // fy is now the top edge of the scrollable list area
            int listAreaBottom = contentBounds.Bottom - ButtonHeight - Padding - Padding;
            int listVisibleH   = Math.Max(0, listAreaBottom - fy);

            // ── FunCol background over visible list area ──────────────────────
            var listVisibleRect = new Rectangle(x, fy, w, listVisibleH);
            _listFunCol.DrawBackground(sb, listVisibleRect, _pixel);

            // ── Scrolled list items ───────────────────────────────────────────
            _rowBounds.Clear();
            _splitBtnRects.Clear();
            var groups = GroupByRow(_local);
            var shown  = groups.Where(g => !g[0].IsHidden).ToList();
            var hidden = groups.Where(g =>  g[0].IsHidden).ToList();

            float scroll = _scroll.ScrollOffset;
            int sy = fy - (int)scroll;

            _showSectionRect = new Rectangle(x, sy, w, SectionLabelH);
            if (sy + SectionLabelH > fy && sy < listAreaBottom)
                DrawSectionLabel(sb, "SHOW", x, sy, w);
            sy += SectionLabelH;

            foreach (var rowEntries in shown)
            {
                int ri = _rowBounds.Count;
                bool vis = sy + RowHeight > fy && sy < listAreaBottom;
                DrawBarRow(sb, rowEntries, x, sy, w, isHideSection: false, rowIndex: ri, draw: vis);
                sy += RowHeight;
            }

            _hideSectionRect = new Rectangle(x, sy, w, SectionLabelH);
            if (sy + SectionLabelH > fy && sy < listAreaBottom)
                DrawSectionLabel(sb, "HIDE", x, sy, w);
            sy += SectionLabelH;

            foreach (var rowEntries in hidden)
            {
                int ri = _rowBounds.Count;
                bool vis = sy + RowHeight > fy && sy < listAreaBottom;
                DrawBarRow(sb, rowEntries, x, sy, w, isHideSection: true, rowIndex: ri, draw: vis);
                sy += RowHeight;
            }

            // ── Overlays ──────────────────────────────────────────────────────
            if (_listFunCol.HoveredColumn == ColSegments)
                DrawSegmentSettingsOverlay(sb);

            DrawDropIndicators(sb, shown, hidden);

            if (_drag.Active)
                DrawDragTooltip(sb, BuildDragTooltipText(shown, hidden), _lastMousePosition);

            // ── Fixed buttons at bottom ───────────────────────────────────────
            int buttonsY = contentBounds.Bottom - ButtonHeight - Padding;
            DrawButtons(sb, x, buttonsY, w, blockLocked);

            _scroll.Draw(sb, blockLocked);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Session management
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureSession()
        {
            if (_sessionStarted) return;

            _snapshot            = BarConfigManager.CloneGlobal();
            _local               = BarConfigManager.CloneGlobal();
            _snapshotBarsVisible = BarConfigManager.BarsVisible;

            var player = FindPlayer();
            if (player != null)
            {
                _simMaxHealth        = Math.Max(1f, player.MaxHealth);
                _simMaxShield        = player.MaxShield;
                _simMaxXP            = Math.Max(0f, player.MaxXP);
                _simHealth           = player.CurrentHealth;
                _simShield           = player.CurrentShield;
                _simXP               = player.CurrentXP;
                _simHealthRegen      = player.BodyAttributes.HealthRegen;
                _simShieldRegen      = player.BodyAttributes.ShieldRegen;
                _simHealthRegenDelay = player.BodyAttributes.HealthRegenDelay;
                _simShieldRegenDelay = player.BodyAttributes.ShieldRegenDelay;
            }
            else
            {
                _simHealth = _simMaxHealth;
                _simShield = _simMaxShield;
                _simXP     = 0f;
            }

            _sessionStarted = true;
        }

        private static void EnsureListFunCol()
        {
            if (_listFunCol != null) return;
            _listFunCol = new FunColInterface(
                new DragHandleFeature("Drag Row")
                {
                    ShowTextWhenExpanded = true,
                    ExpandedInstruction  = "Drag to reorder bar row"
                },
                new DragHandleFeature("Drag Bar")
                {
                    ShowTextWhenExpanded = true,
                    ExpandedInstruction  = "Drag to merge / split bars"
                },
                new DragHandleFeature("Bar Settings")
                {
                    ShowTextWhenExpanded = true,
                    ExpandedInstruction  = "Hover to edit bar settings"
                }
            );
            _listFunCol.SuppressTooltipWarnings = true;
        }

        private static Agent FindPlayer()
        {
            var objs = Core.Instance?.GameObjects;
            if (objs == null) return null;
            foreach (var obj in objs)
                if (obj is Agent a && a.IsPlayer) return a;
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Simulated regen
        // ─────────────────────────────────────────────────────────────────────

        private static void UpdateSimRegen(float dt)
        {
            if (_simHealth < _simMaxHealth && _simHealthRegen > 0f &&
                _simTime - _simLastHealthDmgTime >= _simHealthRegenDelay)
                _simHealth = Math.Min(_simMaxHealth, _simHealth + _simHealthRegen * dt);

            if (_simShield < _simMaxShield && _simShieldRegen > 0f &&
                _simTime - _simLastShieldDmgTime >= _simShieldRegenDelay)
                _simShield = Math.Min(_simMaxShield, _simShield + _simShieldRegen * dt);
        }

        private static void SimulateDamage()
        {
            float dmg         = _simMaxHealth * 0.1f;
            float shieldAbsorb = Math.Min(_simShield, dmg);
            if (shieldAbsorb > 0f)
            {
                _simShield -= shieldAbsorb;
                _simLastShieldDmgTime = _simTime;
                dmg -= shieldAbsorb;
            }
            float healthAbsorb = Math.Min(_simHealth, dmg);
            if (healthAbsorb > 0f)
            {
                _simHealth -= healthAbsorb;
                _simLastHealthDmgTime = _simTime;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: top bar
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawTopBtnRow(SpriteBatch sb, int x, int y, int w, bool blockLocked)
        {
            bool barsOn  = BarConfigManager.BarsVisible;
            Point mouse  = _lastMousePosition;
            int btnH     = TopBarH - 4;
            int gap      = 6;
            int btnW     = (w - gap) / 2;

            // Bars ON/OFF button (left half)
            var toggleRect = new Rectangle(x, y + 2, btnW, btnH);
            bool toggleHov = !blockLocked && UIButtonRenderer.IsHovered(toggleRect, mouse);
            UIButtonRenderer.Draw(sb, toggleRect,
                barsOn ? "Bars ON" : "Bars OFF",
                barsOn ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                toggleHov,
                isDisabled: blockLocked,
                fillOverride:       barsOn ? new Color(50, 155, 75)  : new Color(130, 45, 45),
                hoverFillOverride:  barsOn ? new Color(65, 195, 95)  : new Color(170, 60, 60));

            // Simulate Damage button (right half)
            var simRect = new Rectangle(x + btnW + gap, y + 2, w - btnW - gap, btnH);
            bool simHov = !blockLocked && UIButtonRenderer.IsHovered(simRect, mouse);
            UIButtonRenderer.Draw(sb, simRect,
                "Simulate Damage",
                UIButtonRenderer.ButtonStyle.Grey,
                simHov,
                isDisabled: blockLocked,
                fillOverride:      new Color(55, 95, 160),
                hoverFillOverride: new Color(75, 125, 210));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: preview
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawPreview(SpriteBatch sb, Rectangle previewBounds)
        {
            DrawRect(sb, previewBounds, ColorPalette.BlockBackground * 0.8f);
            DrawOutline(sb, previewBounds, UIStyle.BlockBorder, 1);

            int cx = previewBounds.X + previewBounds.Width  / 2;
            int cy = previewBounds.Y + previewBounds.Height / 2;

            // Draw player using actual Shape if available, else fallback circle
            DrawPreviewPlayer(sb, cx, cy);

            // Bars below dummy player — width matches player diameter for proportionality
            int barW = Math.Min(previewBounds.Width - Padding * 4, PreviewPlayerR * 2);
            int barX = cx - barW / 2;
            int barY = cy + PreviewPlayerR + BarPreviewGap;

            var groups = GroupByRow(_local);
            foreach (var rowEntries in groups)
            {
                var visible = rowEntries.Where(e => !e.IsHidden).ToList();
                if (visible.Count == 0) continue;

                // Compute per-entry max values for proportional width allocation
                float totalMax = 0f;
                foreach (var entry in visible)
                {
                    float entryMax = entry.Type switch
                    {
                        BarType.Health      => _simMaxHealth,
                        BarType.Shield      => _simMaxShield,
                        BarType.XP          => _simMaxXP,
                        BarType.HealthRegen => _simHealthRegenDelay,
                        BarType.ShieldRegen => _simShieldRegenDelay,
                        _                   => 0f
                    };
                    totalMax += Math.Max(0f, entryMax);
                }
                if (totalMax <= 0f) totalMax = Math.Max(1f, visible.Count);

                int bx = barX;
                for (int vi = 0; vi < visible.Count; vi++)
                {
                    var entry = visible[vi];
                    float entryMax = entry.Type switch
                    {
                        BarType.Health      => _simMaxHealth,
                        BarType.Shield      => _simMaxShield,
                        BarType.XP          => _simMaxXP,
                        BarType.HealthRegen => _simHealthRegenDelay,
                        BarType.ShieldRegen => _simShieldRegenDelay,
                        _                   => 0f
                    };
                    bool isLast = vi == visible.Count - 1;
                    int bw = isLast ? (barX + barW - bx) : (int)(barW * Math.Max(0f, entryMax) / totalMax);
                    int barH = HealthBarManager.BarHeight;
                    if (bw > 0) DrawPreviewBar(sb, entry, bx, barY, bw, barH);
                    bx += bw;
                }
                barY += HealthBarManager.BarHeight + BarPreviewGap;
            }
        }

        private static void DrawPreviewPlayer(SpriteBatch sb, int cx, int cy)
        {
            var player = FindPlayer();
            if (player?.Shape != null)
            {
                var previewPos = new Vector2(cx, cy);
                float rot = _previewAimAngle;

                // Barrels first (behind body) — matches ShapeManager positioning
                if (player.BarrelCount > 0)
                {
                    int N = player.BarrelCount;
                    float step = N > 1 ? MathF.Tau / N : 0f;
                    float bodyRadius = Math.Max(player.Shape.Width, player.Shape.Height) / 2f;

                    // standby barrels
                    for (int i = 0; i < N; i++)
                    {
                        if (i == player.ActiveBarrelIndex) continue;
                        var slot = player.Barrels[i];
                        if (slot.FullShape == null) continue;
                        float angle  = rot + i * step - player.CarouselAngle;
                        float scaledHalfL = slot.FullShape.Width * slot.CurrentHeightScale / 2f;
                        Vector2 dir  = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        Vector2 off  = dir * (bodyRadius + scaledHalfL);
                        slot.FullShape.DrawAt(sb, previewPos + off, angle,
                            new Vector2(slot.CurrentHeightScale, 1f));
                    }
                    // active barrel (drawn last so it appears on top)
                    var active = player.Barrels[player.ActiveBarrelIndex];
                    if (active.FullShape != null)
                    {
                        float angle = rot + player.ActiveBarrelIndex * step - player.CarouselAngle;
                        float scaledHalfL = active.FullShape.Width * active.CurrentHeightScale / 2f;
                        Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        Vector2 off = dir * (bodyRadius + scaledHalfL);
                        active.FullShape.DrawAt(sb, previewPos + off, angle,
                            new Vector2(active.CurrentHeightScale, 1f));
                    }
                }
                player.Shape.DrawAt(sb, previewPos, rot);
            }
            else
            {
                // Fallback: filled circle + orientation line
                DrawCircleApprox(sb, cx, cy, PreviewPlayerR, new Color(0, 220, 220, 200), new Color(0, 140, 140), 3);
                float rot = _previewAimAngle;
                int ex = cx + (int)(MathF.Cos(rot) * (PreviewPlayerR + 8));
                int ey = cy + (int)(MathF.Sin(rot) * (PreviewPlayerR + 8));
                // Draw orientation line using thin rect
                int lx = Math.Min(cx, ex), ly = Math.Min(cy, ey);
                int lw = Math.Max(1, Math.Abs(ex - cx));
                int lh = Math.Max(1, Math.Abs(ey - cy));
                DrawRect(sb, new Rectangle(lx, ly, lw, lh), new Color(200, 200, 200, 200));
            }
        }

        private static void DrawPreviewBar(SpriteBatch sb, BarConfigManager.BarEntry entry,
            int x, int y, int w, int h)
        {
            float current, max;
            switch (entry.Type)
            {
                case BarType.Health: current = _simHealth; max = _simMaxHealth; break;
                case BarType.Shield: current = _simShield; max = _simMaxShield; break;
                case BarType.XP:    current = _simXP;    max = _simMaxXP;    break;
                case BarType.HealthRegen:
                {
                    if (_simHealthRegenDelay <= 0f || _simHealthRegen <= 0f) return;
                    float elapsed = MathHelper.Clamp(_simTime - _simLastHealthDmgTime, 0f, _simHealthRegenDelay);
                    current = elapsed; max = _simHealthRegenDelay;
                    break;
                }
                case BarType.ShieldRegen:
                {
                    if (_simShieldRegenDelay <= 0f || _simShieldRegen <= 0f) return;
                    float elapsed = MathHelper.Clamp(_simTime - _simLastShieldDmgTime, 0f, _simShieldRegenDelay);
                    current = elapsed; max = _simShieldRegenDelay;
                    break;
                }
                default: return;
            }
            if (max <= 0f) return;

            float ratio = MathHelper.Clamp(current / max, 0f, 1f);
            Color fill = entry.Type == BarType.Health
                ? Color.Lerp(HealthBarManager.HealthFillLow, HealthBarManager.HealthFillHigh, ratio)
                : GetBarColor(entry.Type);
            HealthBarManager.DrawBarPreview(sb, _pixel, x, y, w, h, current, max, fill);

            // Value label centered in bar
            UIStyle.UIFont font = UIStyle.FontTech;
            if (font.IsAvailable && h >= 10)
            {
                string valText = entry.Type switch
                {
                    BarType.Health      => $"Health: {(int)current}",
                    BarType.Shield      => $"Shield: {(int)current}",
                    BarType.XP          => $"XP: {(int)current}",
                    BarType.HealthRegen => $"HR: {current:F1}s",
                    BarType.ShieldRegen => $"SR: {current:F1}s",
                    _                   => ""
                };
                if (!string.IsNullOrEmpty(valText))
                {
                    Vector2 ts = font.MeasureString(valText);
                    if (ts.X + 4 <= w && ts.Y <= h)
                    {
                        float tx = x + (w - ts.X) / 2f;
                        float ty = y + (h - ts.Y) / 2f;
                        font.DrawString(sb, valText, new Vector2(tx, ty), Color.White * 0.9f);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: column header
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawColumnHeader(SpriteBatch sb, int x, int y, int w)
        {
            var headerRect = new Rectangle(x, y, w, ColHeaderH);
            DrawRect(sb, headerRect, ColorPalette.BlockBackground * 0.7f);
            DrawOutline(sb, headerRect, UIStyle.BlockBorder, 1);

            UIStyle.UIFont font = UIStyle.FontTech;
            string[] shortNames = { "Drag Row", "Drag Bar", "Bar Settings" };
            string[] instrNames =
            {
                "Drag to reorder row",
                "Drag to merge / split bars",
                "Hover row → bar settings"
            };

            for (int i = 0; i < shortNames.Length; i++)
            {
                var colBounds = _listFunCol.GetColumnBounds(i, new Rectangle(x, y, w, ColHeaderH));
                if (colBounds.Width < 4) continue;

                bool expanded = _listFunCol.HoveredColumn == i;
                string text   = expanded ? instrNames[i] : shortNames[i];
                Color  col    = FunColInterface.GetColumnColor(i);
                Color  tc     = expanded ? Color.White : col * 0.80f;

                if (font.IsAvailable)
                {
                    Vector2 ts = font.MeasureString(text);
                    if (ts.X + 4 <= colBounds.Width)
                    {
                        float tx = colBounds.X + (colBounds.Width  - ts.X) / 2f;
                        float ty = y            + (ColHeaderH        - ts.Y) / 2f;
                        font.DrawString(sb, text, new Vector2(tx, ty), tc);
                    }
                    else if (!expanded)
                    {
                        // Try abbrev fallback
                        string abbrev = i == 0 ? "Row" : i == 1 ? "Drag" : "Set";
                        Vector2 ts2 = font.MeasureString(abbrev);
                        if (ts2.X + 4 <= colBounds.Width)
                        {
                            float tx = colBounds.X + (colBounds.Width  - ts2.X) / 2f;
                            float ty = y            + (ColHeaderH        - ts2.Y) / 2f;
                            font.DrawString(sb, abbrev, new Vector2(tx, ty), tc);
                        }
                    }
                }

                // Subtle column divider
                if (i < shortNames.Length - 1)
                    DrawRect(sb, new Rectangle(colBounds.Right - 1, y + 2, 1, ColHeaderH - 4),
                             UIStyle.BlockBorder);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: section labels + bar rows
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawSectionLabel(SpriteBatch sb, string label, int x, int y, int w)
        {
            var rect = new Rectangle(x, y, w, SectionLabelH);
            DrawRect(sb, rect, ColorPalette.BlockBackground * 0.5f);
            DrawOutline(sb, rect, UIStyle.BlockBorder, 1);

            UIStyle.UIFont font = UIStyle.FontTech;
            if (font.IsAvailable)
            {
                Vector2 ts = font.MeasureString(label);
                font.DrawString(sb, label,
                    new Vector2(x + 6, y + (SectionLabelH - ts.Y) / 2f),
                    UIStyle.MutedTextColor);
            }
        }

        private static void DrawBarRow(SpriteBatch sb,
            List<BarConfigManager.BarEntry> rowEntries,
            int x, int y, int w, bool isHideSection, int rowIndex, bool draw = true)
        {
            if (rowEntries.Count == 0) return;
            int barRow = rowEntries[0].BarRow;

            var rowRec = new Rectangle(x, y, w, RowHeight);
            var rbd = new BarRowBounds
            {
                BarRow   = barRow,
                IsHidden = isHideSection,
                RowRect  = rowRec,
                Entries  = new List<(BarType, Rectangle)>()
            };

            // Pre-populate entry bounds for hit-testing even when not drawing
            int entryW = w / Math.Max(1, rowEntries.Count);
            int ex     = x;
            for (int ei = 0; ei < rowEntries.Count; ei++)
            {
                int ew = (ei == rowEntries.Count - 1) ? (x + w - ex) : entryW;
                rbd.Entries.Add((rowEntries[ei].Type, new Rectangle(ex, y, ew, RowHeight)));
                ex += ew;
            }
            _rowBounds.Add(rbd);

            if (!draw) return;  // bounds registered; visual draw skipped (row is outside viewport)

            // Row background
            Color rowBg = isHideSection
                ? ColorPalette.BlockBackground * 0.30f
                : ColorPalette.BlockBackground * 0.20f;
            DrawRect(sb, rowRec, rowBg);

            ex = x;
            for (int ei = 0; ei < rowEntries.Count; ei++)
            {
                var entry = rowEntries[ei];
                int ew    = (ei == rowEntries.Count - 1) ? (x + w - ex) : entryW;
                var eb    = new Rectangle(ex, y, ew, RowHeight);

                Color barColor = GetBarColor(entry.Type);
                DrawRect(sb, new Rectangle(ex, y, BorderAccentW, RowHeight), barColor);

                UIStyle.UIFont font = UIStyle.FontTech;
                if (font.IsAvailable)
                {
                    string label = entry.Type.ToString();
                    float  ly    = y + (RowHeight - font.LineHeight) / 2f;
                    font.DrawString(sb, label, new Vector2(ex + BorderAccentW + 5, ly), UIStyle.TextColor);
                }

                int hovCol = _listFunCol?.HoveredColumn ?? -1;
                if ((hovCol == ColDragRow || hovCol == ColDragBar) && rowRec.Contains(_lastMousePosition))
                    DrawHoverHighlight(sb, eb);

                // Split button: appears per-bar when Drag Bar is active and row has multiple bars
                if (hovCol == ColDragBar && rowEntries.Count > 1)
                {
                    int sbX = ex + ew - SplitBtnSize - 3;
                    int sbY = y + (RowHeight - SplitBtnSize) / 2;
                    var splitRect = new Rectangle(sbX, sbY, SplitBtnSize, SplitBtnSize);
                    _splitBtnRects.Add((entry.Type, barRow, splitRect));
                    Color splitBg = splitRect.Contains(_lastMousePosition)
                        ? new Color(80, 130, 200) : new Color(40, 65, 110);
                    DrawRect(sb, splitRect, splitBg);
                    DrawOutline(sb, splitRect, FunColInterface.GetColumnColor(ColDragBar), 1);
                    UIStyle.UIFont sfont = UIStyle.FontTech;
                    if (sfont.IsAvailable)
                    {
                        const string splitGlyph = "<";
                        Vector2 gs = sfont.MeasureString(splitGlyph);
                        sfont.DrawString(sb, splitGlyph,
                            new Vector2(sbX + (SplitBtnSize - gs.X) / 2f, sbY + (SplitBtnSize - gs.Y) / 2f),
                            Color.White * 0.9f);
                    }
                }

                ex += ew;
            }

            bool isDragBar = _drag.Active && _drag.DraggedBar.HasValue && _drag.DropIntoRow == barRow;
            if (isDragBar) DrawDropIntoRowHighlight(sb, rowRec);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: segment settings overlay
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawSegmentSettingsOverlay(SpriteBatch sb)
        {
            UIStyle.UIFont font = UIStyle.FontTech;
            if (!font.IsAvailable || _pixel == null) return;

            Point mouse = _lastMousePosition;
            var hovRow  = _rowBounds.FirstOrDefault(r => r.RowRect.Contains(mouse));
            if (hovRow.RowRect == Rectangle.Empty) return;

            var entry = _local.FirstOrDefault(e => e.BarRow == hovRow.BarRow);
            if (entry == null) return;

            // Draw overlay below (or above) the row
            const int panelH = 22;
            int panelY = hovRow.RowRect.Bottom;
            int panelX = hovRow.RowRect.X;
            int panelW = hovRow.RowRect.Width;

            var panel = new Rectangle(panelX, panelY, panelW, panelH);
            DrawRect(sb, panel, new Color(30, 30, 36, 230));
            DrawOutline(sb, panel, FunColInterface.GetColumnColor(ColSegments), 1);

            string key = entry.Type.ToString();
            int fx = panelX + 4;
            int fy = panelY + (panelH - (int)font.LineHeight) / 2;

            // Segments-enabled checkbox
            int checkSize = 12;
            var checkRect = new Rectangle(fx, panelY + (panelH - checkSize) / 2, checkSize, checkSize);
            DrawRect(sb, checkRect, entry.SegmentsEnabled ? new Color(80, 180, 80) : new Color(55, 55, 55));
            DrawOutline(sb, checkRect, UIStyle.BlockBorder, 1);
            fx += checkSize + 4;

            font.DrawString(sb, "Segs", new Vector2(fx, fy), UIStyle.MutedTextColor);
            fx += (int)font.MeasureString("Segs").X + 4;

            if (entry.SegmentsEnabled)
            {
                int arrowY = panelY + (panelH - ArrowBtnSize) / 2;

                DrawRect(sb, new Rectangle(fx, arrowY, ArrowBtnSize, ArrowBtnSize), new Color(55, 55, 60));
                font.DrawString(sb, "-", new Vector2(fx + ArrowBtnSize / 2f - 3, arrowY + 1), UIStyle.TextColor);
                fx += ArrowBtnSize + 2;

                string val      = _editingSegBarType == key ? _segInputBuffer : entry.SegmentCount.ToString();
                var inputRect   = new Rectangle(fx, arrowY, SegInputWidth, ArrowBtnSize);
                DrawRect(sb, inputRect, new Color(28, 28, 32));
                DrawOutline(sb, inputRect, _editingSegBarType == key ? UIStyle.AccentColor : UIStyle.BlockBorder, 1);
                font.DrawString(sb, val,
                    new Vector2(fx + 4, arrowY + (ArrowBtnSize - font.LineHeight) / 2f), UIStyle.TextColor);
                fx += SegInputWidth + 2;

                DrawRect(sb, new Rectangle(fx, arrowY, ArrowBtnSize, ArrowBtnSize), new Color(55, 55, 60));
                font.DrawString(sb, "+", new Vector2(fx + ArrowBtnSize / 2f - 3, arrowY + 1), UIStyle.TextColor);
                fx += ArrowBtnSize + 2;
            }

            // % Text toggle
            fx += 6;
            int pctCheckSize = 12;
            var pctCheckRect = new Rectangle(fx, panelY + (panelH - pctCheckSize) / 2, pctCheckSize, pctCheckSize);
            DrawRect(sb, pctCheckRect, entry.ShowPercent ? new Color(80, 180, 80) : new Color(55, 55, 55));
            DrawOutline(sb, pctCheckRect, UIStyle.BlockBorder, 1);
            fx += pctCheckSize + 4;
            font.DrawString(sb, "% Text", new Vector2(fx, fy), UIStyle.MutedTextColor);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: buttons
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawButtons(SpriteBatch sb, int x, int y, int w, bool blockLocked)
        {
            string[] labels = { "Save", "Apply", "Discard" };
            Color[]  fills  =
            {
                new Color(50,  140, 80),
                new Color(100, 100, 50),
                new Color(140, 60,  60)
            };
            Color[] hoverFills =
            {
                new Color(65,  180, 100),
                new Color(130, 130, 70),
                new Color(180, 80,  80)
            };

            int totalGap = ButtonSpacing * (labels.Length - 1);
            int btnW     = (w - totalGap) / labels.Length;
            int bx       = x;
            Point mouse  = _lastMousePosition;

            for (int i = 0; i < labels.Length; i++)
            {
                int tw      = (i == labels.Length - 1) ? (x + w - bx) : btnW;
                var btnRect = new Rectangle(bx, y, tw, ButtonHeight);
                bool hov    = !blockLocked && UIButtonRenderer.IsHovered(btnRect, mouse);
                UIButtonRenderer.Draw(sb, btnRect, labels[i],
                    UIButtonRenderer.ButtonStyle.Grey,
                    hov,
                    isDisabled:       blockLocked,
                    fillOverride:      fills[i],
                    hoverFillOverride: hoverFills[i]);
                bx += tw + ButtonSpacing;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Centralized visual helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Horizontal drop indicator line with small end-cap diamonds.</summary>
        private static void DrawDropIndicatorLine(SpriteBatch sb, int x, int y, int w)
        {
            if (_pixel == null) return;
            DrawRect(sb, new Rectangle(x, y - 1, w, 2), UIStyle.AccentColor);
            DrawRect(sb, new Rectangle(x,         y - 3, 5, 6), UIStyle.AccentColor);
            DrawRect(sb, new Rectangle(x + w - 5, y - 3, 5, 6), UIStyle.AccentColor);
        }

        /// <summary>Highlight border around a target row to indicate "drop here to merge".</summary>
        private static void DrawDropIntoRowHighlight(SpriteBatch sb, Rectangle rowBounds)
        {
            if (_pixel == null) return;
            DrawRect(sb, rowBounds, UIStyle.AccentColor * 0.20f);
            DrawOutline(sb, rowBounds, UIStyle.AccentColor, 2);
        }

        /// <summary>Subtle hover highlight overlay.</summary>
        private static void DrawHoverHighlight(SpriteBatch sb, Rectangle bounds)
        {
            if (_pixel == null) return;
            DrawRect(sb, bounds, Color.White * 0.06f);
        }

        /// <summary>Floating tooltip near the cursor during drag.</summary>
        private static void DrawDragTooltip(SpriteBatch sb, string text, Point mousePos)
        {
            UIStyle.UIFont font = UIStyle.FontTech;
            if (!font.IsAvailable || _pixel == null || string.IsNullOrEmpty(text)) return;
            Vector2 ts  = font.MeasureString(text);
            int ox = mousePos.X + 14;
            int oy = mousePos.Y - (int)(ts.Y / 2f);
            var bg = new Rectangle(ox - 4, oy - 2, (int)ts.X + 8, (int)ts.Y + 4);
            DrawRect(sb, bg, new Color(18, 18, 22, 215));
            DrawOutline(sb, bg, UIStyle.AccentColor, 1);
            font.DrawString(sb, text, new Vector2(ox, oy), Color.White);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: drop indicators
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawDropIndicators(SpriteBatch sb,
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            if (!_drag.Active || _drag.DraggedBar.HasValue) return; // only for row drags

            int dropIdx = _drag.DropBeforeIdx;
            if (dropIdx < 0) return;

            var allRows = _rowBounds;
            int shownCount = allRows.Count(rb => !rb.IsHidden);

            int lineY;
            if (dropIdx == 0 && allRows.Count > 0)
                lineY = allRows[0].RowRect.Y;
            else if (dropIdx == shownCount && shownCount > 0 && !_drag.DropIntoHide)
                // Between SHOW and HIDE sections — anchor at bottom of last shown row
                lineY = allRows[shownCount - 1].RowRect.Bottom;
            else if (dropIdx < allRows.Count)
                lineY = allRows[dropIdx].RowRect.Y;
            else
                lineY = allRows.Count > 0 ? allRows[^1].RowRect.Bottom : _cachedListBounds.Y;

            if (_cachedListBounds.Width > 0)
                DrawDropIndicatorLine(sb, _cachedListBounds.X, lineY, _cachedListBounds.Width);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Input handling
        // ─────────────────────────────────────────────────────────────────────

        private static void HandleTopBarClick(MouseState mouse, MouseState prev, Rectangle contentBounds)
        {
            bool clicked = mouse.LeftButton == ButtonState.Released &&
                           prev.LeftButton  == ButtonState.Pressed;
            if (!clicked) return;

            int x = contentBounds.X + Padding;
            int w = contentBounds.Width - Padding * 2;
            int y = contentBounds.Y + PreviewHeight + Padding;  // button row is below preview

            int btnH = TopBarH - 4;
            int gap  = 6;
            int btnW = (w - gap) / 2;

            var toggleRect = new Rectangle(x, y + 2, btnW, btnH);
            if (toggleRect.Contains(mouse.Position))
            {
                BarConfigManager.BarsVisible = !BarConfigManager.BarsVisible;
                return;
            }

            var simRect = new Rectangle(x + btnW + gap, y + 2, w - btnW - gap, btnH);
            if (simRect.Contains(mouse.Position))
                SimulateDamage();
        }

        private static void HandleButtons(MouseState mouse, MouseState prev, Rectangle contentBounds,
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            bool clicked = mouse.LeftButton == ButtonState.Released &&
                           prev.LeftButton  == ButtonState.Pressed;
            if (!clicked) return;

            int x = contentBounds.X + Padding;
            int w = contentBounds.Width - Padding * 2;
            int y = contentBounds.Bottom - ButtonHeight - Padding;  // fixed — buttons never scroll

            string[] labels   = { "Save", "Apply", "Discard" };
            int      totalGap = ButtonSpacing * (labels.Length - 1);
            int      btnW     = (w - totalGap) / labels.Length;
            int      bx       = x;

            for (int i = 0; i < labels.Length; i++)
            {
                int tw      = (i == labels.Length - 1) ? (x + w - bx) : btnW;
                var btnRect = new Rectangle(bx, y, tw, ButtonHeight);
                if (btnRect.Contains(mouse.Position))
                {
                    switch (i)
                    {
                        case 0: DoSave();    break;
                        case 1: DoApply();   break;
                        case 2: DoDiscard(); break;
                    }
                    return;
                }
                bx += tw + ButtonSpacing;
            }
        }

        private static void HandleSegmentInput(MouseState mouse, MouseState prev,
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            if (_listFunCol?.HoveredColumn != ColSegments) return;

            bool clicked = mouse.LeftButton == ButtonState.Released &&
                           prev.LeftButton  == ButtonState.Pressed;
            if (!clicked) return;

            // Find the row the mouse is in
            var hovRow = _rowBounds.FirstOrDefault(r => r.RowRect.Contains(mouse.Position));
            if (hovRow.RowRect == Rectangle.Empty) return;

            var entry = _local.FirstOrDefault(e => e.BarRow == hovRow.BarRow);
            if (entry == null) return;

            string key = entry.Type.ToString();
            const int panelH = 22;
            int panelY = hovRow.RowRect.Bottom;
            int panelX = hovRow.RowRect.X;
            int panelW = hovRow.RowRect.Width;

            // Segments-enabled checkbox
            int fx = panelX + 4;
            int checkSize = 12;
            var checkRect = new Rectangle(fx, panelY + (panelH - checkSize) / 2, checkSize, checkSize);
            if (checkRect.Contains(mouse.Position))
            {
                entry.SegmentsEnabled = !entry.SegmentsEnabled;
                return;
            }
            fx += checkSize + 4;

            UIStyle.UIFont font = UIStyle.FontTech;
            if (!font.IsAvailable) return;
            fx += (int)font.MeasureString("Segs").X + 4;

            if (entry.SegmentsEnabled)
            {
                int arrowY = panelY + (panelH - ArrowBtnSize) / 2;

                var downRect = new Rectangle(fx, arrowY, ArrowBtnSize, ArrowBtnSize);
                if (downRect.Contains(mouse.Position)) { entry.SegmentCount = Math.Max(1, entry.SegmentCount - 1); return; }
                fx += ArrowBtnSize + 2;

                var inputRect = new Rectangle(fx, arrowY, SegInputWidth, ArrowBtnSize);
                if (inputRect.Contains(mouse.Position))
                {
                    _editingSegBarType = _editingSegBarType == key ? null : key;
                    _segInputBuffer    = entry.SegmentCount.ToString();
                    return;
                }
                fx += SegInputWidth + 2;

                var upRect = new Rectangle(fx, arrowY, ArrowBtnSize, ArrowBtnSize);
                if (upRect.Contains(mouse.Position)) { entry.SegmentCount++; return; }
                fx += ArrowBtnSize + 2;
            }

            // % Text checkbox (always reachable regardless of SegmentsEnabled)
            fx += 6;
            int pctCheckSize = 12;
            var pctCheckRect = new Rectangle(fx, panelY + (panelH - pctCheckSize) / 2, pctCheckSize, pctCheckSize);
            if (pctCheckRect.Contains(mouse.Position))
            {
                entry.ShowPercent = !entry.ShowPercent;
                return;
            }

            // Click elsewhere — commit edit
            if (_editingSegBarType != null) { CommitSegInput(); _editingSegBarType = null; }
        }

        private static void CommitSegInput()
        {
            if (string.IsNullOrWhiteSpace(_segInputBuffer)) return;
            if (!int.TryParse(_segInputBuffer.Trim(), out int val)) return;
            val = Math.Max(1, val);
            var entry = _local.FirstOrDefault(e => e.Type.ToString() == _editingSegBarType);
            if (entry != null) entry.SegmentCount = val;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drag handling
        // ─────────────────────────────────────────────────────────────────────

        private static void HandleSplitButtonClick(MouseState mouse, MouseState prev)
        {
            if (_listFunCol?.HoveredColumn != ColDragBar) return;
            bool clicked = mouse.LeftButton == ButtonState.Released &&
                           prev.LeftButton  == ButtonState.Pressed;
            if (!clicked) return;

            foreach (var (barType, barRow, rect) in _splitBtnRects)
            {
                if (!rect.Contains(mouse.Position)) continue;

                // Split this bar out into its own new row, inserted after its current row
                var entry = _local.FirstOrDefault(e => e.Type == barType && e.BarRow == barRow);
                if (entry == null) break;

                // Only split if the row has more than one bar
                if (_local.Count(e => e.BarRow == barRow) <= 1) break;

                // Insert the new row right after the current barRow
                // Shift all BarRows > barRow up by 1 to make space
                foreach (var e in _local.Where(e => e.BarRow > barRow))
                    e.BarRow++;

                entry.BarRow        = barRow + 1;
                entry.PositionInRow = 0;
                NormalizeLocalLayout();
                break;
            }
        }

        private static void HandleDrag(MouseState mouse, MouseState prev,
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            bool pressing  = mouse.LeftButton == ButtonState.Pressed;
            bool releasing = mouse.LeftButton == ButtonState.Released &&
                             prev.LeftButton  == ButtonState.Pressed;

            if (!pressing && !releasing) { _drag = default; return; }

            int hovCol = _listFunCol?.HoveredColumn ?? -1;

            if (pressing && !_drag.Active && hovCol == ColDragRow)
            {
                // Green field only — drag to reorder whole rows
                foreach (var rowData in _rowBounds)
                {
                    if (!rowData.RowRect.Contains(mouse.Position)) continue;
                    _drag = new DragState
                    {
                        Active      = true,
                        DraggedBar  = null,
                        DraggedRow  = rowData.BarRow,
                        DragStart   = mouse.Position,
                        DropIntoRow = -1
                    };
                    return;
                }
            }
            else if (pressing && !_drag.Active && hovCol == ColDragBar)
            {
                // Blue field only — drag individual bar to merge or split
                foreach (var rowData in _rowBounds)
                {
                    if (!rowData.RowRect.Contains(mouse.Position)) continue;
                    foreach (var (bt, bounds) in rowData.Entries)
                    {
                        if (!bounds.Contains(mouse.Position)) continue;
                        _drag = new DragState
                        {
                            Active      = true,
                            DraggedBar  = bt,
                            DraggedRow  = rowData.BarRow,
                            DragStart   = mouse.Position,
                            DropIntoRow = -1
                        };
                        return;
                    }
                }
            }

            if (_drag.Active)
            {
                // Determine drop position
                _drag.DropBeforeIdx = -1;
                _drag.DropIntoRow   = -1;
                _drag.DropIntoHide  = _hideSectionRect.Contains(mouse.Position) ||
                                      mouse.Position.Y >= _hideSectionRect.Y;

                if (_drag.DraggedBar.HasValue)
                {
                    // For bar drag: check if hovering an existing row (merge target)
                    foreach (var rd in _rowBounds)
                    {
                        if (rd.RowRect.Contains(mouse.Position) &&
                            rd.BarRow != _drag.DraggedRow)
                        {
                            _drag.DropIntoRow = rd.BarRow;
                            break;
                        }
                    }
                }
                else
                {
                    // For row drag: find insert-before index within the relevant section
                    int shownRowCount = shown.Count;
                    for (int ri = 0; ri < _rowBounds.Count; ri++)
                    {
                        var rd = _rowBounds[ri];
                        if (mouse.Position.Y < rd.RowRect.Y + rd.RowRect.Height / 2)
                        {
                            _drag.DropBeforeIdx = ri;
                            break;
                        }
                    }
                    // Cap to section boundary: show section [0..shownRowCount], hide section [shownRowCount..Count]
                    if (_drag.DropBeforeIdx < 0)
                        _drag.DropBeforeIdx = _drag.DropIntoHide ? _rowBounds.Count : shownRowCount;
                    else if (!_drag.DropIntoHide)
                        _drag.DropBeforeIdx = Math.Min(_drag.DropBeforeIdx, shownRowCount);
                    else
                        _drag.DropBeforeIdx = Math.Max(_drag.DropBeforeIdx, shownRowCount);
                }

                if (releasing) CommitDrop(shown, hidden);
            }
        }

        private static void CommitDrop(
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            if (!_drag.Active) return;

            if (_drag.DraggedBar.HasValue)
            {
                // Drag Bar: only merge into an existing row — no new-row creation
                if (_drag.DropIntoRow < 0)
                {
                    // No valid merge target — cancel
                    _drag = default;
                    return;
                }

                var entry = _local.FirstOrDefault(e => e.Type == _drag.DraggedBar.Value);
                if (entry != null)
                {
                    // Inherit visibility from the target row
                    var targetRb = _rowBounds.FirstOrDefault(rb => rb.BarRow == _drag.DropIntoRow);
                    if (targetRb.RowRect != Rectangle.Empty)
                        entry.IsHidden = targetRb.IsHidden;

                    entry.BarRow        = _drag.DropIntoRow;
                    entry.PositionInRow = _local.Count(e => e.BarRow == _drag.DropIntoRow);
                    NormalizeLocalLayout();
                }
            }
            else
            {
                // Whole row drag: reorder within and/or between Show/Hide sections
                int srcBarRow = _drag.DraggedRow;
                var srcGroup  = _rowBounds.FirstOrDefault(rb => rb.BarRow == srcBarRow);
                if (srcGroup.RowRect == Rectangle.Empty) { _drag = default; return; }

                // Toggle hidden state based on drop section
                foreach (var e in _local.Where(e => e.BarRow == srcBarRow))
                    e.IsHidden = _drag.DropIntoHide;

                var sectionGroups = _drag.DropIntoHide ? hidden : shown;
                int srcIdxInSection = sectionGroups.FindIndex(g => g.Count > 0 && g[0].BarRow == srcBarRow);

                // Compute section-local drop index
                int sectionDropIdx = _drag.DropIntoHide
                    ? Math.Max(0, _drag.DropBeforeIdx - shown.Count)
                    : _drag.DropBeforeIdx;

                var newSection = sectionGroups.Where(g => g[0].BarRow != srcBarRow).ToList();

                // After removing src, indices after srcIdxInSection shift by -1
                int insertAt;
                if (srcIdxInSection >= 0 && sectionDropIdx > srcIdxInSection)
                    insertAt = Math.Clamp(sectionDropIdx - 1, 0, newSection.Count);
                else
                    insertAt = Math.Clamp(sectionDropIdx, 0, newSection.Count);

                if (srcIdxInSection >= 0)
                    newSection.Insert(Math.Clamp(insertAt, 0, newSection.Count),
                        sectionGroups[srcIdxInSection]);

                // Re-assign BarRow for all rows preserving section ordering
                var otherSection = _drag.DropIntoHide ? shown : hidden;
                var combined = _drag.DropIntoHide
                    ? otherSection.Concat(newSection).ToList()
                    : newSection.Concat(otherSection).ToList();

                for (int ri = 0; ri < combined.Count; ri++)
                    foreach (var e in combined[ri])
                        e.BarRow = ri;
            }

            _drag = default;
        }

        private static void NormalizeLocalLayout()
        {
            var groups = GroupByRow(_local);
            for (int ri = 0; ri < groups.Count; ri++)
            {
                var g = groups[ri];
                for (int ei = 0; ei < g.Count; ei++)
                {
                    g[ei].BarRow        = ri;
                    g[ei].PositionInRow = ei;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Save / Apply / Discard
        // ─────────────────────────────────────────────────────────────────────

        private static void DoApply()
        {
            BarConfigManager.ApplyGlobal(_local);
        }

        private static void DoSave()
        {
            BarConfigManager.Save(_local);
            _snapshot            = _local.Select(e => e.Clone()).ToList();
            _snapshotBarsVisible = BarConfigManager.BarsVisible;
        }

        private static void DoDiscard()
        {
            _local = _snapshot.Select(e => e.Clone()).ToList();
            BarConfigManager.BarsVisible = _snapshotBarsVisible;
            BarConfigManager.ApplyGlobal(_local);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static List<List<BarConfigManager.BarEntry>> GroupByRow(
            List<BarConfigManager.BarEntry> entries)
        {
            var rowIndices = entries.Select(e => e.BarRow).Distinct().OrderBy(r => r).ToList();
            return rowIndices
                .Select(r => entries.Where(e => e.BarRow == r).OrderBy(e => e.PositionInRow).ToList())
                .ToList();
        }

        private static string BuildDragTooltipText(
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            if (_drag.DraggedBar.HasValue)
                return _drag.DropIntoRow >= 0 ? "Merge into row" : "";
            return "Reorder bar row";
        }

        private static Color GetBarColor(BarType type) => type switch
        {
            BarType.Health      => Color.Lerp(HealthBarManager.HealthFillLow, HealthBarManager.HealthFillHigh, 0.7f),
            BarType.Shield      => HealthBarManager.ShieldFillColor,
            BarType.XP          => HealthBarManager.XPFillColor,
            BarType.HealthRegen => new Color(220, 200, 80),
            BarType.ShieldRegen => new Color(80, 220, 200),
            _                   => Color.Gray
        };

        private static float CalculateTotalHeight(
            List<List<BarConfigManager.BarEntry>> shown,
            List<List<BarConfigManager.BarEntry>> hidden)
        {
            // Only the scrollable list content — fixed elements (TopBar, Preview, ColHeader, Buttons)
            // are outside the scroll viewport and do not contribute to scroll height.
            return SectionLabelH + shown.Count  * RowHeight
                 + SectionLabelH + hidden.Count * RowHeight;
        }

        // ── Pixel drawing helpers ────────────────────────────────────────────

        private static void DrawRect(SpriteBatch sb, Rectangle r, Color c)
        {
            if (_pixel == null || r.Width <= 0 || r.Height <= 0) return;
            sb.Draw(_pixel, r, c);
        }

        private static void DrawOutline(SpriteBatch sb, Rectangle r, Color c, int t)
        {
            if (_pixel == null) return;
            sb.Draw(_pixel, new Rectangle(r.X,          r.Y,          r.Width, t), c);
            sb.Draw(_pixel, new Rectangle(r.X,          r.Bottom - t, r.Width, t), c);
            sb.Draw(_pixel, new Rectangle(r.X,          r.Y,          t, r.Height), c);
            sb.Draw(_pixel, new Rectangle(r.Right  - t, r.Y,          t, r.Height), c);
        }

        /// <summary>Rough circle approximation using horizontal scan slices.</summary>
        private static void DrawCircleApprox(SpriteBatch sb, int cx, int cy, int r,
            Color fill, Color outline, int outlineT)
        {
            if (_pixel == null) return;
            for (int dy = -r; dy <= r; dy++)
            {
                int hw = (int)MathF.Sqrt(MathF.Max(0, r * r - dy * dy));
                sb.Draw(_pixel, new Rectangle(cx - hw, cy + dy, hw * 2, 1), fill);
            }
            // Approximate outline using a slightly larger circle in a different color
            for (int dy = -(r + outlineT); dy <= r + outlineT; dy++)
            {
                int hwOuter = (int)MathF.Sqrt(MathF.Max(0, (r + outlineT) * (r + outlineT) - dy * dy));
                int hwInner = (int)MathF.Sqrt(MathF.Max(0, (r - outlineT) * (r - outlineT) - dy * dy));
                if (hwOuter > hwInner)
                {
                    sb.Draw(_pixel, new Rectangle(cx - hwOuter, cy + dy, hwOuter - hwInner, 1), outline);
                    sb.Draw(_pixel, new Rectangle(cx + hwInner, cy + dy, hwOuter - hwInner, 1), outline);
                }
            }
        }

        private static void EnsurePixel(GraphicsDevice device)
        {
            if (_pixel != null || device == null) return;
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
    }
}
