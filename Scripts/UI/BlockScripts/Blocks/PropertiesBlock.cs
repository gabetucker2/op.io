using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    internal static class PropertiesBlock
    {
        public const string BlockTitle = "Properties";

        private const int PreviewMaxSize = 220;
        private const int Padding = 10;
        private const int ButtonHeight = 32;
        private const int HeaderSpacing = 6;
        private const int RowSpacing = 4;
        private const int LockButtonPadding = 6;
        private const int SectionSpacing = 8;
        private const int SectionIndent = 12;
        private const int BarRowHeight = 14;
        private const int BarSegmentGap = 2;

        private static Texture2D _pixel;
        private static Texture2D _lockedIcon;
        private static Texture2D _unlockedIcon;
        private static MouseState _lastMouseState;
        private static readonly Dictionary<Shape, Texture2D> PreviewCache = new();
        private static readonly BlockScrollPanel ScrollPanel = new();

        // ── Per-block hidden attribute toggle (persisted in SQL) ──────────────────
        private static bool _showHidden;
        private static bool _showHiddenLoaded;
        private const string ShowHiddenRowKey = "ShowHidden";

        private static bool GetShowHidden(int goId)
        {
            if (!_showHiddenLoaded)
            {
                _showHiddenLoaded = true;
                var data = BlockDataStore.LoadRowData(DockBlockKind.Properties);
                if (data.TryGetValue(ShowHiddenRowKey, out string stored))
                    _showHidden = string.Equals(stored, "true", StringComparison.OrdinalIgnoreCase);
                else
                    _showHidden = ControlStateManager.GetSwitchState(ControlKeyMigrations.ShowHiddenAttrsKey);
            }
            return _showHidden;
        }

        private static void SetShowHidden(int goId, bool value)
        {
            _showHidden = value;
            _showHiddenLoaded = true;
            BlockDataStore.SetRowData(DockBlockKind.Properties, ShowHiddenRowKey, value ? "true" : "false");
        }

        // ── Button hover tooltip ───────────────────────────────────────────────────
        private static string _hoveredButtonKey;
        public static string GetHoveredButtonKey() => _hoveredButtonKey;

        // ── Properties row hover tooltip (all text rows, hidden and non-hidden) ──
        private static string _hoveredPropRowKey;
        private static string _hoveredPropRowLabel;
        public static string GetHoveredPropRowKey()   => _hoveredPropRowKey;
        public static string GetHoveredPropRowLabel() => _hoveredPropRowLabel;

        // Descriptions for all properties text rows (displayed as the first tooltip).
        private static readonly Dictionary<string, string> _propDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Unit
            ["Body Switch Speed"]     = "Speed at which this agent cycles through body configurations",
            ["Barrel Switch Speed"]   = "Speed at which this agent cycles through barrel configurations",
            // Player
            ["Player ID"]             = "The player's unique network identifier",
            // GameObject
            ["Name"]                  = "Display name of this game object",
            ["Type"]                  = "Game object type or category",
            ["ID"]                    = "Unique identifier for this object in the game world",
            // Destructible
            ["Death XP Reward"]       = "XP points awarded to the attacker when this object is destroyed",
            ["Current Health"]        = "Current health relative to maximum health",
            ["Current Shield"]        = "Current shield points relative to maximum shield",
            // Body Transform
            ["Position"]              = "Current world-space coordinates of this object",
            ["Rotation"]              = "Current facing angle in degrees",
            ["Size"]                  = "Width and height of this object's collision bounds",
            ["Shape"]                 = "Geometry type used for the physics body",
            ["Mass"]                  = "Physical mass affecting physics and derived combat stats",
            ["Draw Layer"]            = "Render order layer; higher values draw on top (0=default, 100=bullets, 200=units)",
            ["Offset"]                = "Position offset of the barrel relative to the agent's body center",
            // Body Attributes (non-hidden, Agent)
            ["Health Regen"]          = "HP regenerated per second after the damage delay expires",
            ["Health Armor"]          = "Multiplier that reduces incoming health damage",
            ["Dmg Regen Delay"]       = "Seconds after taking health damage before HP regen resumes",
            ["Max Shield"]            = "Maximum shield point capacity",
            ["Shield Regen"]          = "Shield points regenerated per second after the damage delay expires",
            ["Shield Armor"]          = "Multiplier that reduces incoming shield damage",
            ["Dmg Shield Delay"]      = "Seconds after taking shield damage before shield regen resumes",
            ["Body Coll. Damage"]     = "Damage dealt to other objects on physical contact",
            ["Body Penetration"]      = "Ability to pass through or reduce collision force against barriers",
            ["Coll. Dmg Resist"]      = "Resistance reducing collision-based damage received",
            ["Bullet Dmg Resist"]     = "Resistance reducing bullet damage received",
            ["Speed"]                 = "Base movement speed of this agent",
            ["Control"]               = "Maneuverability stat affecting rotation and acceleration",
            ["Action Buff"]           = "Buff multiplier applied to action-based abilities",
            // Hidden Body Attributes (Agent)
            ["Max Health"]            = "Maximum health capacity of this unit",
            ["Body Knockback"]        = "Force applied to other objects on collision",
            ["Rotation Speed"]        = "Maximum rotation rate in degrees per second",
            ["Accel. Speed"]          = "Acceleration force applied per second",
            // Barrel Attributes (non-hidden)
            ["Bullet Damage"]         = "Base damage dealt by each bullet on hit",
            ["Bullet Penetration"]    = "Ability of bullets to pass through targets",
            ["Reload Speed"]          = "Rate at which this barrel reloads between shots",
            ["Bullet Mass"]           = "Physical mass of each bullet, affecting size, drag, and durability",
            ["Bullet Speed"]          = "Initial velocity of bullets when fired",
            ["Bullet Lifespan"]       = "Maximum time in seconds a bullet can travel before disappearing",
            // Hidden Barrel Attributes
            ["Bullet Knockback"]      = "Effective push force bullets exert on collided targets",
            ["Recoil Mass"]           = "Recoil impulse mass, derived from bullet mass",
            ["Bullet Health"]         = "Durability of each bullet before it is destroyed on impact",
            ["Bullet Radius"]         = "Physical radius of each bullet in pixels",
            ["Bullet Drag"]           = "Air resistance slowing bullets as they travel",
            // Bullet Effectors (body-equivalent stats on bullets)
            ["Bullet Health Regen"]       = "HP regenerated per second on the bullet after damage delay",
            ["Bullet Health Armor"]       = "Multiplier reducing incoming health damage to the bullet",
            ["Bullet Dmg Regen Delay"]    = "Seconds after bullet takes damage before HP regen resumes",
            ["Bullet Max Shield"]         = "Maximum shield capacity on the bullet",
            ["Bullet Shield Regen"]       = "Shield regenerated per second on the bullet after damage delay",
            ["Bullet Shield Armor"]       = "Multiplier reducing incoming shield damage to the bullet",
            ["Bullet Dmg Shield Delay"]   = "Seconds after bullet takes shield damage before shield regen resumes",
            ["Bullet Coll. Dmg Resist"]   = "Resistance reducing collision damage the bullet receives",
            ["Bullet Control"]            = "Controls bullet rotation and acceleration responsiveness",
            // Non-agent Body Attributes
            ["Health Regen Delay"]    = "Seconds after taking damage before HP regeneration resumes",
            ["Shield Regen Delay"]    = "Seconds after taking damage before shield regeneration resumes",
            ["Body Collision Damage"] = "Damage dealt to other objects on physical contact",
            ["Coll. Dmg Resistance"]  = "Resistance reducing collision-based damage received",
            ["Bullet Dmg Resistance"] = "Resistance reducing bullet damage received",
            // Hidden Barrel Transform
            ["Barrel Width"]          = "Narrow dimension of the barrel, matching bullet diameter",
            ["Barrel Height"]         = "Long dimension of the barrel, scaled from bullet speed",
            // Colors (Body Transform / Barrel Transform)
            ["Fill"]                  = "Fill color of this object or barrel",
            ["Outline"]               = "Outline color of this object or barrel",
            // Flags (GameObject)
            ["Flags"]                 = "Tags describing this object's role and physics behavior",
            // Farm Attributes
            ["FloatAmplitude"]        = "Amplitude of the sine-wave float animation",
            ["FloatSpeed"]            = "Direction-reversal frequency in cycles per second",
        };

        // "Derived from" text for hidden (derived) attribute rows (displayed as second tooltip).
        private static readonly Dictionary<string, string> _hiddenAttrDerivedFrom = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Max Health"]      = "Derived from: Mass",
            ["Body Knockback"]  = "Derived from: Mass",
            ["Rotation Speed"]  = "Derived from: Control",
            ["Accel. Speed"]    = "Derived from: Control",
            ["Bullet Knockback"]       = "Derived from: Bullet Penetration",
            ["Recoil Mass"]            = "Derived from: Bullet Mass",
            ["Bullet Health"]          = "Derived from: Bullet Mass",
            ["Bullet Radius"]          = "Derived from: Bullet Mass",
            ["Bullet Drag"]            = "Derived from: Bullet Mass",
            ["Bullet Health Regen"]    = "Derived from: Bullet Mass",
            ["Bullet Dmg Regen Delay"] = "Derived from: Bullet Mass",
            ["Bullet Health Armor"]    = "Derived from: Bullet Mass",
            ["Bullet Coll. Dmg Resist"]= "Derived from: Bullet Mass",
            ["Bullet Dmg Resist"]      = "Derived from: Bullet Mass",
            ["Barrel Width"]           = "Derived from: Bullet Mass",
            ["Barrel Height"]          = "Derived from: Bullet Speed",
        };

        /// <summary>
        /// Returns tooltip entry arrays for all properties text rows.
        /// Hidden rows get two entries (description + "Derived from: X").
        /// Non-hidden rows get one entry (description only).
        /// </summary>
        public static IEnumerable<(string Key, (string Text, string DataType)[] Entries)> GetAllPropRowTooltipEntries()
        {
            foreach (var (label, desc) in _propDescriptions)
            {
                // Always emit a non-hidden tooltip so the description works on normal rows.
                yield return ("props_row:" + label, [(desc, string.Empty)]);

                // If this label is also used as a hidden (derived) attribute, emit a
                // second entry with the "Derived from" line so hidden rows get both.
                if (_hiddenAttrDerivedFrom.TryGetValue(label, out string derivedFrom))
                    yield return ("props_attr:" + label, [(desc, string.Empty), (derivedFrom, string.Empty)]);
            }
        }

        private readonly struct PropertiesLayout
        {
            public PropertiesLayout(Rectangle modeLabel, Rectangle previewBounds, Rectangle detailsBounds, Rectangle lockButtonBounds, Rectangle hiddenToggleBounds, Rectangle infoArea)
            {
                ModeLabel = modeLabel;
                PreviewBounds = previewBounds;
                DetailsBounds = detailsBounds;
                LockButtonBounds = lockButtonBounds;
                HiddenToggleBounds = hiddenToggleBounds;
                InfoArea = infoArea;
            }

            public Rectangle ModeLabel { get; }
            public Rectangle PreviewBounds { get; }
            public Rectangle DetailsBounds { get; }
            public Rectangle LockButtonBounds { get; }
            public Rectangle HiddenToggleBounds { get; }
            public Rectangle InfoArea { get; }
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            _lastMouseState = mouseState;
            InspectableObjectInfo earlyTarget = InspectModeState.GetActiveTarget();
            bool hasHiddenRows = earlyTarget?.Source is Agent;
            PropertiesLayout layout = BuildLayout(contentBounds, ScrollPanel.ScrollOffset, hasHiddenRows);
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed;

            // ── Per-GO hidden-attrs toggle ───────────────────────────────────────
            int activeGoId = earlyTarget?.Id ?? -1;
            bool blockLocked0 = BlockManager.IsBlockLocked(DockBlockKind.Properties);
            if (!blockLocked0 && leftClickReleased
                && layout.HiddenToggleBounds != Rectangle.Empty
                && layout.HiddenToggleBounds.Contains(mouseState.Position)
                && activeGoId >= 0)
            {
                bool current = GetShowHidden(activeGoId);
                SetShowHidden(activeGoId, !current);
            }

            InspectableObjectInfo hovered = null;
            bool cursorInGameBlock = BlockManager.IsCursorWithinGameBlock();
            if (cursorInGameBlock)
            {
                Vector2 gameCursor = MouseFunctions.GetMousePosition();
                hovered = GameObjectInspector.FindHoveredObject(gameCursor);
            }

            InspectModeState.UpdateHovered(hovered, mouseState.Position, allowNullOverride: cursorInGameBlock);
            InspectModeState.ValidateLockStillValid();

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Properties);
            InspectableObjectInfo activeTarget = InspectModeState.GetActiveTarget();
            bool hasActiveTarget = activeTarget != null;
            bool lockButtonHovered = hasActiveTarget && layout.LockButtonBounds.Contains(mouseState.Position);
            bool lockButtonClicked = !blockLocked && lockButtonHovered && leftClickReleased;

            bool inspectClickFired = false;
            if (lockButtonClicked)
            {
                if (InspectModeState.IsTargetLocked(activeTarget))
                {
                    InspectModeState.ClearLock();
                }
                else if (activeTarget != null && activeTarget.IsValid)
                {
                    InspectModeState.LockTarget(activeTarget);
                }
                inspectClickFired = true;
            }
            else if (leftClickReleased && InspectModeState.InspectModeEnabled && cursorInGameBlock)
            {
                if (hovered != null)
                {
                    InspectModeState.LockHovered();
                }
                else
                {
                    InspectModeState.ClearLock();
                }
                inspectClickFired = true;
            }

            if (inspectClickFired && InspectModeState.InspectModeEnabled &&
                ControlStateManager.GetSwitchState(ControlKeyMigrations.AutoTurnInspectModeOffKey))
            {
                InputTypeManager.ForceSwitchBindingState(InspectModeState.InspectModeKey, false);
                ControlStateManager.SetSwitchState(InspectModeState.InspectModeKey, false, "AutoTurnInspectModeOff");
            }

            bool targetLocked = InspectModeState.IsTargetLocked(activeTarget);
            bool toggleHovered = layout.HiddenToggleBounds != Rectangle.Empty && layout.HiddenToggleBounds.Contains(mouseState.Position);
            if (!blockLocked && lockButtonHovered)
                _hoveredButtonKey = targetLocked ? "props_btn:lock:unlock" : "props_btn:lock:lock";
            else if (!blockLocked && toggleHovered)
                _hoveredButtonKey = (activeGoId >= 0 && GetShowHidden(activeGoId)) ? "props_btn:hidden:hide" : "props_btn:hidden:show";
            else
                _hoveredButtonKey = null;

            InspectableObjectInfo scrollTarget = InspectModeState.GetActiveTarget();
            InspectableObjectInfo scrollLocked = InspectModeState.GetLockedTarget();
            float totalContentHeight = CalculateTotalContentHeight(scrollTarget, scrollLocked, contentBounds, scrollTarget?.Source is Agent);
            ScrollPanel.Update(contentBounds, totalContentHeight, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            EnsureResources(spriteBatch.GraphicsDevice);
            float scroll = ScrollPanel.ScrollOffset;
            InspectableObjectInfo drawTarget = InspectModeState.GetActiveTarget();
            PropertiesLayout layout = BuildLayout(contentBounds, scroll, drawTarget?.Source is Agent);
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Properties);

            if (layout.ModeLabel.Bottom > contentBounds.Y && layout.ModeLabel.Y < contentBounds.Bottom)
                DrawModeBadge(spriteBatch, layout.ModeLabel);

            InspectableObjectInfo target = InspectModeState.GetActiveTarget();
            target?.Refresh(); // keep health / shield live every frame
            bool lockHovered = layout.LockButtonBounds.Contains(_lastMouseState.Position);
            bool targetLocked = InspectModeState.IsTargetLocked(target);

            if (target == null || !target.IsValid)
            {
                DrawEmptyState(spriteBatch, layout);
                if (InspectModeState.HasLockedTarget
                    && layout.LockButtonBounds != Rectangle.Empty
                    && layout.LockButtonBounds.Bottom > contentBounds.Y
                    && layout.LockButtonBounds.Y < contentBounds.Bottom)
                {
                    DrawLockToggle(spriteBatch, layout.LockButtonBounds, targetLocked, lockHovered, blockLocked);
                }
                return;
            }

            if (layout.PreviewBounds != Rectangle.Empty
                && layout.PreviewBounds.Bottom > contentBounds.Y
                && layout.PreviewBounds.Y < contentBounds.Bottom)
            {
                DrawPreview(spriteBatch, layout.PreviewBounds, target);
            }

            if (layout.LockButtonBounds != Rectangle.Empty
                && layout.LockButtonBounds.Bottom > contentBounds.Y
                && layout.LockButtonBounds.Y < contentBounds.Bottom)
            {
                DrawLockToggle(spriteBatch, layout.LockButtonBounds, targetLocked, lockHovered, blockLocked);
            }

            if (layout.HiddenToggleBounds != Rectangle.Empty
                && layout.HiddenToggleBounds.Bottom > contentBounds.Y
                && layout.HiddenToggleBounds.Y < contentBounds.Bottom)
            {
                bool toggleHovered = layout.HiddenToggleBounds.Contains(_lastMouseState.Position);
                int drawGoId = target?.Id ?? -1;
                bool showHidden = drawGoId >= 0 && GetShowHidden(drawGoId);
                DrawHiddenToggle(spriteBatch, layout.HiddenToggleBounds, showHidden, toggleHovered, blockLocked);
            }

            Rectangle clipBounds = ScrollPanel.ContentViewportBounds == Rectangle.Empty
                ? contentBounds
                : new Rectangle(contentBounds.X, contentBounds.Y, ScrollPanel.ContentViewportBounds.Width, contentBounds.Height);
            DrawDetails(spriteBatch, clipBounds, target, layout.InfoArea.Y);
            ScrollPanel.Draw(spriteBatch, blockLocked);
        }

        private static PropertiesLayout BuildLayout(Rectangle contentBounds, float scrollOffset = 0f, bool hasHiddenRows = false)
        {
            int scrolledY = contentBounds.Y - (int)MathF.Round(scrollOffset);
            Rectangle modeLabel = new(contentBounds.X, scrolledY, contentBounds.Width, ButtonHeight);

            int contentTop = modeLabel.Bottom + Padding;

            // Preview is left-aligned at the top, above all text
            int previewSize = Math.Min(contentBounds.Width, PreviewMaxSize);
            Rectangle previewBounds = previewSize >= 80
                ? new Rectangle(contentBounds.X, contentTop, previewSize, previewSize)
                : Rectangle.Empty;

            Rectangle lockButtonBounds   = Rectangle.Empty;
            Rectangle hiddenToggleBounds = Rectangle.Empty;
            if (previewBounds != Rectangle.Empty)
            {
                // Lock button: procedural size, top-right corner of preview
                int shortSide = Math.Min(previewBounds.Width, previewBounds.Height);
                int maxSize   = Math.Clamp((int)(shortSide * 0.09f), 10, 24);
                int available = Math.Max(0, previewBounds.Height - LockButtonPadding * 2);
                maxSize       = Math.Min(maxSize, available);

                if (maxSize > 0)
                {
                    int bx    = Math.Max(previewBounds.Right - maxSize - LockButtonPadding, previewBounds.X);
                    int lockY = previewBounds.Y + LockButtonPadding;
                    lockButtonBounds = new Rectangle(bx, lockY, maxSize, maxSize);
                }

                // Hidden-attrs toggle only shown for objects that actually have hidden rows (agents)
                if (hasHiddenRows)
                    hiddenToggleBounds = new Rectangle(contentBounds.X, previewBounds.Bottom + 4, contentBounds.Width, ButtonHeight);
            }

            // Info area starts below the toggle button (if present), otherwise below the preview
            int infoTop = hiddenToggleBounds != Rectangle.Empty
                ? hiddenToggleBounds.Bottom + Padding
                : (previewBounds != Rectangle.Empty ? previewBounds.Bottom + Padding : contentTop);
            int infoHeight = Math.Max(0, contentBounds.Bottom - infoTop);
            Rectangle infoArea = new(contentBounds.X, infoTop, contentBounds.Width, infoHeight);
            Rectangle detailsBounds = infoArea;

            return new PropertiesLayout(modeLabel, previewBounds, detailsBounds, lockButtonBounds, hiddenToggleBounds, infoArea);
        }

        private static float CalculateTotalContentHeight(InspectableObjectInfo target, InspectableObjectInfo locked, Rectangle contentBounds, bool hasHiddenRows = false)
        {
            float height = ButtonHeight + Padding;

            int previewSize = Math.Min(contentBounds.Width, PreviewMaxSize);
            if (previewSize >= 80)
            {
                height += previewSize + 4; // preview + gap
                if (hasHiddenRows)
                    height += ButtonHeight + Padding; // toggle button (only for agents)
                else
                    height += Padding; // just padding below preview
            }

            height += CalculateDetailsContentHeight(target, locked);
            return height;
        }

        private static void EnsureResources(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_pixel == null || _pixel.IsDisposed)
            {
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
        }

        private static void DrawModeBadge(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontTech;
            if (!font.IsAvailable)
            {
                return;
            }

            string binding = InputManager.GetBindingDisplayLabel(InspectModeState.InspectModeKey);
            if (string.IsNullOrWhiteSpace(binding))
            {
                binding = "Shift + I";
            }

            bool active = InspectModeState.InspectModeEnabled;
            string label = active ? $"Inspect mode: ON ({binding})" : $"Inspect mode: OFF ({binding})";
            Color textColor = active ? UIStyle.AccentColor : UIStyle.MutedTextColor;

            Vector2 size = font.MeasureString(label);
            Vector2 position = new(bounds.X, bounds.Y + Math.Max(0, (bounds.Height - size.Y) / 2));
            font.DrawString(spriteBatch, label, position, textColor);
        }

        private static void DrawEmptyState(SpriteBatch spriteBatch, PropertiesLayout layout)
        {
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!body.IsAvailable || !tech.IsAvailable)
            {
                return;
            }

            Rectangle details = layout.DetailsBounds;
            float y = details.Y;
            Vector2 headlineSize = body.MeasureString("No object selected.");
            body.DrawString(spriteBatch, "No object selected.", new Vector2(details.X, y), UIStyle.TextColor);
            y += headlineSize.Y + HeaderSpacing;

            string hoverText = "Hover over an object to preview its properties.";
            tech.DrawString(spriteBatch, hoverText, new Vector2(details.X, y), UIStyle.MutedTextColor);
            y += tech.LineHeight + RowSpacing;

            string lockText = "Click an object with inspect mode on to pin the target.";
            tech.DrawString(spriteBatch, lockText, new Vector2(details.X, y), UIStyle.MutedTextColor);

            if (layout.PreviewBounds != Rectangle.Empty)
            {
                DrawRect(spriteBatch, layout.PreviewBounds, ColorPalette.BlockBackground * 0.6f);
                DrawRectOutline(spriteBatch, layout.PreviewBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
            }
        }

        private static void DrawPreview(SpriteBatch spriteBatch, Rectangle previewBounds, InspectableObjectInfo target)
        {
            if (previewBounds == Rectangle.Empty || spriteBatch.GraphicsDevice == null)
                return;

            DrawRect(spriteBatch, previewBounds, ColorPalette.BlockBackground * 0.9f);

            // Determine which bar rows are visible for this target
            bool hasHealth = target.MaxHealth > 0f;
            bool hasShield = target.MaxShield > 0f;
            bool hasXP     = target.MaxXP    > 0f;
            var allRowGroups = BarConfigManager.GetGroupedByRow();
            var previewBarRows = new List<List<BarConfigManager.BarEntry>>();
            foreach (var group in allRowGroups)
            {
                var filtered = new List<BarConfigManager.BarEntry>();
                foreach (var e in group)
                {
                    if (e.IsHidden) continue;
                    if ((e.Type == BarType.Health && hasHealth)
                     || (e.Type == BarType.Shield && hasShield)
                     || (e.Type == BarType.XP     && hasXP))
                        filtered.Add(e);
                }
                if (filtered.Count > 0) previewBarRows.Add(filtered);
            }

            int numBarRows = previewBarRows.Count;

            // Build shapes list early so we can compute extents before totalBarsH,
            // enabling proportional bar heights that match the worldScale of the drawn shape.
            float parentRotation = target.Source?.Rotation ?? target.Rotation;
            float cos = MathF.Cos(parentRotation);
            float sin = MathF.Sin(parentRotation);
            var shapes = new List<(Shape shape, Vector2 localOffset, float localRotation, float heightScale)>();

            if (target.Shape != null)
            {
                shapes.Add((target.Shape, Vector2.Zero, 0f, 1f));

                if (target.Source is Agent agentSrc && agentSrc.BarrelCount > 0)
                {
                    int N      = agentSrc.BarrelCount;
                    float step = N > 1 ? MathF.Tau / N : 0f;
                    float bodyRadius = Math.Max(target.Shape.Width, target.Shape.Height) / 2f;

                    // Standby barrels first so the active barrel draws on top
                    for (int i = 0; i < N; i++)
                    {
                        if (i == agentSrc.ActiveBarrelIndex) continue;
                        var slot = agentSrc.Barrels[i];
                        if (slot.FullShape == null) continue;
                        float localAngle = i * step - agentSrc.CarouselAngle;
                        float scaledHalfLen = slot.FullShape.Width * slot.CurrentHeightScale / 2f;
                        Vector2 dir = new(MathF.Cos(localAngle), MathF.Sin(localAngle));
                        Vector2 localOff = dir * (bodyRadius + scaledHalfLen);
                        shapes.Add((slot.FullShape, localOff, localAngle, slot.CurrentHeightScale));
                    }

                    // Active barrel last
                    {
                        var active = agentSrc.Barrels[agentSrc.ActiveBarrelIndex];
                        if (active.FullShape != null)
                        {
                            float localAngle = agentSrc.ActiveBarrelIndex * step - agentSrc.CarouselAngle;
                            float scaledHalfLen = active.FullShape.Width * active.CurrentHeightScale / 2f;
                            Vector2 dir = new(MathF.Cos(localAngle), MathF.Sin(localAngle));
                            Vector2 localOff = dir * (bodyRadius + scaledHalfLen);
                            shapes.Add((active.FullShape, localOff, localAngle, active.CurrentHeightScale));
                        }
                    }
                }
                else if (target.Source != null)
                {
                    foreach (GameObject child in target.Source.Children)
                    {
                        if (child?.Shape == null) continue;
                        shapes.Add((child.Shape, child.Position, child.Rotation, 1f));
                    }
                }
            }

            // Compute extents to derive a provisional worldScale for proportional bar sizing
            float extentX = 1f, extentY = 1f;
            if (shapes.Count > 0)
            {
                float minX = float.MaxValue, maxX = float.MinValue,
                      minY = float.MaxValue, maxY = float.MinValue;
                foreach (var (s, off, _, hScale) in shapes)
                {
                    float scaledW = s.Width * hScale;
                    float halfDiag = MathF.Sqrt(scaledW * scaledW + s.Height * s.Height) / 2f;
                    minX = Math.Min(minX, off.X - halfDiag);
                    maxX = Math.Max(maxX, off.X + halfDiag);
                    minY = Math.Min(minY, off.Y - halfDiag);
                    maxY = Math.Max(maxY, off.Y + halfDiag);
                }
                extentX = Math.Max(1f, Math.Max(MathF.Abs(minX), MathF.Abs(maxX)));
                extentY = Math.Max(1f, Math.Max(MathF.Abs(minY), MathF.Abs(maxY)));
            }

            // Provisional scale using full preview bounds — used only to size bars proportionally
            float provisionalScale = Math.Max(0.1f, Math.Min(
                (previewBounds.Width  / 2f - Padding) / extentX,
                (previewBounds.Height / 2f - Padding) / extentY));

            int barH = numBarRows > 0 ? HealthBarManager.BarHeight : 0;
            int barG = numBarRows > 0 ? Math.Max(1, (int)MathF.Round(2f * provisionalScale)) : 0;
            int barP = numBarRows > 0 ? Math.Max(2, (int)MathF.Round(HealthBarManager.OffsetY * provisionalScale)) : 0;

            int totalBarsH = numBarRows > 0
                ? barP * 2 + numBarRows * barH + (numBarRows - 1) * barG
                : 0;

            // Shape draws in the upper portion; bar rows fill the bottom strip
            Rectangle shapeArea = new(
                previewBounds.X,
                previewBounds.Y,
                previewBounds.Width,
                Math.Max(20, previewBounds.Height - totalBarsH));

            float worldScale = 0.1f;
            if (shapes.Count > 0)
            {
                worldScale = Math.Min(
                    (shapeArea.Width  / 2f - Padding) / extentX,
                    (shapeArea.Height / 2f - Padding) / extentY);
                worldScale = Math.Max(0.1f, worldScale);

                Vector2 worldOrigin = new(shapeArea.Center.X, shapeArea.Center.Y);

                // Draw non-body shapes first (barrels/children), then body on top.
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < shapes.Count; i++)
                    {
                        bool isBody = (i == 0);
                        if (isBody == (pass == 0)) continue; // pass 0 = non-body, pass 1 = body

                        var (s, localOff, localRot, hScale) = shapes[i];
                        Texture2D tex = GetPreviewTexture(spriteBatch.GraphicsDevice, s);
                        if (tex == null || tex.IsDisposed) continue;

                        Vector2 rotatedOff = new(
                            localOff.X * cos - localOff.Y * sin,
                            localOff.X * sin + localOff.Y * cos);

                        float scaleX = Math.Max(0.1f, worldScale * s.Width  / tex.Width);
                        float scaleY = Math.Max(0.1f, worldScale * s.Height / tex.Height);
                        Vector2 pos    = worldOrigin + rotatedOff * worldScale;
                        Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
                        spriteBatch.Draw(tex, pos, null, Color.White,
                            parentRotation + localRot, origin,
                            new Vector2(scaleX * hScale, scaleY),
                            SpriteEffects.None, 0f);
                    }
                }
            }

            // Draw bar rows at the bottom of the preview, width proportional to rendered player
            if (numBarRows > 0)
            {
                // Bars span the same horizontal extent as the player shape in this viewport
                float scaledPlayerW = shapes.Count > 0 ? extentX * 2f * worldScale : previewBounds.Width - Padding * 2f;
                int barW = Math.Max(20, Math.Min((int)scaledPlayerW, previewBounds.Width - Padding * 2));
                int barX = previewBounds.X + (previewBounds.Width - barW) / 2;
                int barY = previewBounds.Bottom - barP - numBarRows * barH - (numBarRows - 1) * barG;
                foreach (var rowEntries in previewBarRows)
                {
                    DrawPreviewBarRow(spriteBatch, barX, barY, barW, barH, rowEntries, target);
                    barY += barH + barG;
                }
            }

            DrawRectOutline(spriteBatch, previewBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
        }

        private static void DrawPreviewBarRow(SpriteBatch spriteBatch, int x, int y, int width, int height,
            List<BarConfigManager.BarEntry> entries, InspectableObjectInfo target)
        {
            if (width <= 0 || entries.Count == 0 || _pixel == null) return;

            float totalMax = 0f;
            foreach (var e in entries)
            {
                totalMax += e.Type switch
                {
                    BarType.Health => target.MaxHealth,
                    BarType.Shield => target.MaxShield,
                    BarType.XP     => target.MaxXP,
                    _              => 0f
                };
            }
            if (totalMax <= 0f) return;

            int bxStart = x;
            for (int vi = 0; vi < entries.Count; vi++)
            {
                var   entry    = entries[vi];
                float entryMax = entry.Type switch
                {
                    BarType.Health => target.MaxHealth,
                    BarType.Shield => target.MaxShield,
                    BarType.XP     => target.MaxXP,
                    _              => 0f
                };
                float current = entry.Type switch
                {
                    BarType.Health => target.CurrentHealth,
                    BarType.Shield => target.CurrentShield,
                    BarType.XP     => target.CurrentXP,
                    _              => 0f
                };
                if (entryMax <= 0f) continue;

                bool isLast = vi == entries.Count - 1;
                int bw = isLast ? (x + width - bxStart) : (int)(width * entryMax / totalMax);
                if (bw <= 0) { bxStart += bw; continue; }

                Color fill = entry.Type switch
                {
                    BarType.Health => HealthBarManager.GetHealthFillColor(current, entryMax),
                    BarType.Shield => HealthBarManager.ShieldFillColor,
                    BarType.XP     => HealthBarManager.XPFillColor,
                    _              => Color.Gray
                };

                HealthBarManager.DrawBarPreview(spriteBatch, _pixel, bxStart, y, bw, height, current, entryMax, fill);

                bxStart += bw;
            }
        }

        private static void DrawLockToggle(SpriteBatch spriteBatch, Rectangle bounds, bool isLocked, bool hovered, bool disabled)
        {
            if (bounds == Rectangle.Empty || _pixel == null)
            {
                return;
            }

            bool activeHover = hovered && !disabled;
            Color fill = isLocked
                ? (activeHover ? ColorPalette.LockLockedHoverFill : ColorPalette.LockLockedFill)
                : (activeHover ? ColorPalette.LockUnlockedHoverFill : ColorPalette.LockUnlockedFill);
            Color border = isLocked
                ? (activeHover ? UIStyle.AccentColor : UIStyle.BlockBorder)
                : UIStyle.AccentColor;

            if (disabled)
            {
                fill *= 0.55f;
                border *= 0.55f;
            }

            DrawRect(spriteBatch, bounds, fill);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            Texture2D icon = GetLockIcon(isLocked);
            if (icon != null && !icon.IsDisposed)
            {
                Vector2 center = new(bounds.Center.X, bounds.Center.Y);
                Vector2 origin = new(icon.Width / 2f, icon.Height / 2f);
                int innerPad = Math.Max(2, bounds.Width / 5);
                float scale  = Math.Min(
                    (bounds.Width  - innerPad * 2f) / icon.Width,
                    (bounds.Height - innerPad * 2f) / icon.Height);
                scale = Math.Max(0f, scale);
                Color tint = disabled ? Color.White * 0.65f : Color.White;
                spriteBatch.Draw(icon, center, null, tint, 0f, origin, scale, SpriteEffects.None, 0f);
                return;
            }

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            string glyph = isLocked ? "L" : "U";
            Color glyphColor = disabled ? UIStyle.MutedTextColor : Color.White;
            Vector2 glyphSize = glyphFont.MeasureString(glyph);
            Vector2 glyphPosition = new(
                bounds.X + (bounds.Width - glyphSize.X) / 2f,
                bounds.Y + (bounds.Height - glyphSize.Y) / 2f - 1f);
            glyphFont.DrawString(spriteBatch, glyph, glyphPosition, glyphColor);
        }

        private static void DrawHiddenToggle(SpriteBatch spriteBatch, Rectangle bounds, bool showHidden, bool hovered, bool disabled)
        {
            if (bounds == Rectangle.Empty) return;

            string label = showHidden ? "Hide Hidden Attributes" : "Show Hidden Attributes";
            UIButtonRenderer.Draw(spriteBatch, bounds, label,
                showHidden ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                hovered, disabled);
        }

        private static Texture2D GetLockIcon(bool isLocked)
        {
            EnsureLockIcons();
            return isLocked ? _lockedIcon : _unlockedIcon;
        }

        private static void EnsureLockIcons()
        {
            if (_lockedIcon == null || _lockedIcon.IsDisposed)
            {
                _lockedIcon = BlockIconProvider.GetIcon("Icon_Locked.png");
            }

            if (_unlockedIcon == null || _unlockedIcon.IsDisposed)
            {
                _unlockedIcon = BlockIconProvider.GetIcon("Icon_Unlocked.png");
            }
        }

        private static float CalculateDetailsContentHeight(InspectableObjectInfo target, InspectableObjectInfo locked)
        {
            UIStyle.UIFont heading = UIStyle.FontHBody;
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!heading.IsAvailable || !body.IsAvailable || !tech.IsAvailable || target == null || !target.IsValid)
                return 0f;

            Properties properties = new(target, locked);
            float height = heading.LineHeight + HeaderSpacing;
            bool showHidden = GetShowHidden(target.Id);

            bool firstSection = true;
            foreach (Properties.Section section in properties.Sections)
            {
                if (!firstSection && section.Depth <= 1)
                    height += SectionSpacing;
                firstSection = false;

                height += body.LineHeight + RowSpacing;
                foreach (Properties.Row row in section.Rows)
                {
                    if (row.IsHidden && !showHidden) continue;
                    height += row.Kind == Properties.RowKind.BarGraph || row.Kind == Properties.RowKind.CombinedHealthBar
                        ? BarRowHeight + RowSpacing
                        : row.LineCount * (tech.LineHeight + RowSpacing);
                }
            }

            return height;
        }

        private static void DrawDetails(SpriteBatch spriteBatch, Rectangle clipBounds, InspectableObjectInfo target, float contentStartY)
        {
            // Reset prop-row hover every frame; set below if mouse lands on a text row.
            _hoveredPropRowKey   = null;
            _hoveredPropRowLabel = null;

            UIStyle.UIFont heading = UIStyle.FontHBody;
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!heading.IsAvailable || !body.IsAvailable || !tech.IsAvailable)
                return;

            InspectableObjectInfo locked = InspectModeState.GetLockedTarget();
            Properties properties = new(target, locked);
            bool showHidden = GetShowHidden(target.Id);

            float y = contentStartY;

            Vector2 headingSize = heading.MeasureString(properties.Title);
            if (IsRowVisible(y, headingSize.Y, clipBounds))
            {
                heading.DrawString(spriteBatch, properties.Title, new Vector2(clipBounds.X, y), UIStyle.TextColor);

                string lockedTag = properties.LockedTag;
                if (!string.IsNullOrWhiteSpace(lockedTag))
                {
                    Vector2 tagSize = tech.MeasureString(lockedTag);
                    Vector2 tagPos = new(clipBounds.Right - tagSize.X, y + Math.Max(0, (headingSize.Y - tagSize.Y) / 2));
                    tech.DrawString(spriteBatch, lockedTag, tagPos, UIStyle.AccentColor);
                }
            }

            y += headingSize.Y + HeaderSpacing;

            bool firstSection = true;
            bool stopped = false;
            foreach (Properties.Section section in properties.Sections)
            {
                if (stopped)
                    break;

                if (!firstSection && section.Depth <= 1)
                    y += SectionSpacing;
                firstSection = false;

                if (y > clipBounds.Bottom)
                    break;

                int indent = section.Depth * SectionIndent;

                if (IsRowVisible(y, body.LineHeight, clipBounds))
                    DrawSectionHeader(spriteBatch, body, section.Title, clipBounds.X + indent, y);

                y += body.LineHeight + RowSpacing;

                foreach (Properties.Row row in section.Rows)
                {
                    if (row.IsHidden && !showHidden) continue;
                    if (y > clipBounds.Bottom) { stopped = true; break; }

                    float rowH = row.Kind == Properties.RowKind.BarGraph || row.Kind == Properties.RowKind.CombinedHealthBar
                        ? BarRowHeight + RowSpacing
                        : row.LineCount * (tech.LineHeight + RowSpacing);

                    if (IsRowVisible(y, rowH, clipBounds))
                    {
                        if (row.Kind == Properties.RowKind.BulletList)
                            DrawBulletListRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.Items);
                        else if (row.Kind == Properties.RowKind.Color)
                            DrawColorRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.Color, row.Value);
                        else if (row.Kind == Properties.RowKind.BarGraph)
                            DrawBarRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.CurrentValue, row.MaxValue, row.SegmentCount, clipBounds, row.BarFillColor);
                        else if (row.Kind == Properties.RowKind.CombinedHealthBar)
                            DrawCombinedHealthBarRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.CurrentValue, row.MaxValue, row.CurrentShieldValue, row.MaxShieldValue, row.SegmentCount, clipBounds);
                        else if (row.Kind == Properties.RowKind.Boolean)
                            DrawBoolRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.BoolValue, row.IsHidden);
                        else
                        {
                            DrawRow(spriteBatch, tech, row.Label, row.Value, clipBounds.X + indent, y, row.IsHidden);
                            {
                                Point mp = _lastMouseState.Position;
                                if (mp.X >= clipBounds.X && mp.X <= clipBounds.Right &&
                                    mp.Y >= (int)y && mp.Y < (int)(y + rowH))
                                {
                                    _hoveredPropRowKey   = row.IsHidden
                                        ? "props_attr:" + row.Label
                                        : "props_row:"  + row.Label;
                                    _hoveredPropRowLabel = row.Label;
                                }
                            }
                        }
                    }

                    y += rowH;
                }
            }
        }

        private static void DrawBarRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, float y,
            string label, float current, float max, int segmentCount, Rectangle clipBounds,
            Color? barFillColorOverride = null)
        {
            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            string healthText = $"{(int)MathF.Round(current)} / {(int)MathF.Round(max)}";
            Vector2 textSize  = font.MeasureString(healthText);

            // Bar starts after: label + gap + health text + gap.
            // This pushes valueX right to reserve space, naturally shrinking the bar.
            float valueX     = x + Math.Max(font.MeasureString(label).X + Padding + textSize.X + Padding, 120f);
            float totalWidth = clipBounds.Right - Padding - valueX;
            if (totalWidth < 20f || segmentCount <= 0 || max <= 0f) return;

            font.DrawString(spriteBatch, healthText, new Vector2(valueX - textSize.X - Padding, y), UIStyle.MutedTextColor);

            int n = segmentCount;
            float segW = Math.Max(2f, (totalWidth - BarSegmentGap * (n - 1)) / n);
            float fraction = MathF.Max(0f, MathF.Min(1f, current / max));

            Color fillColor  = barFillColorOverride.HasValue
                ? barFillColorOverride.Value
                : HealthBarManager.GetHealthFillColor(current, max);
            Color emptyColor = HealthBarManager.PropBarEmpty;
            Color corner     = ColorPalette.BlockBackground;

            for (int i = 0; i < n; i++)
            {
                // Partial fill: segment i covers fraction range [i/n, (i+1)/n].
                float segFrac = MathF.Max(0f, MathF.Min(1f, fraction * n - i));

                int sx = (int)(valueX + i * (segW + BarSegmentGap));
                int sy = (int)y;
                int sw = (int)segW;
                int sh = BarRowHeight;

                DrawRect(spriteBatch, new Rectangle(sx, sy, sw, sh), emptyColor);
                if (segFrac > 0f)
                    DrawRect(spriteBatch, new Rectangle(sx, sy, (int)(sw * segFrac), sh), fillColor);

                // Trim 1-pixel corners to approximate rounded look
                if (sw >= 4 && sh >= 4)
                {
                    DrawRect(spriteBatch, new Rectangle(sx,          sy,          1, 1), corner);
                    DrawRect(spriteBatch, new Rectangle(sx + sw - 1, sy,          1, 1), corner);
                    DrawRect(spriteBatch, new Rectangle(sx,          sy + sh - 1, 1, 1), corner);
                    DrawRect(spriteBatch, new Rectangle(sx + sw - 1, sy + sh - 1, 1, 1), corner);
                }
            }
        }

        private static void DrawCombinedHealthBarRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, float y,
            string label, float currentHealth, float maxHealth, float currentShield, float maxShield,
            int segmentCount, Rectangle clipBounds)
        {
            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            string valueText = $"{(int)MathF.Round(currentHealth)}/{(int)MathF.Round(maxHealth)}  {(int)MathF.Round(currentShield)}/{(int)MathF.Round(maxShield)}";
            Vector2 textSize = font.MeasureString(valueText);

            float valueX     = x + Math.Max(font.MeasureString(label).X + Padding + textSize.X + Padding, 120f);
            float totalWidth = clipBounds.Right - Padding - valueX;
            if (totalWidth < 20f || segmentCount <= 0) return;

            font.DrawString(spriteBatch, valueText, new Vector2(valueX - textSize.X - Padding, y), UIStyle.MutedTextColor);

            int   n           = segmentCount;
            float segW        = Math.Max(2f, (totalWidth - BarSegmentGap * (n - 1)) / n);
            float segmentSize = MathF.Max(1f, HealthBarManager.SegmentSize);
            float totalMax    = maxHealth + maxShield;

            float healthRatio     = maxHealth > 0f ? MathF.Max(0f, MathF.Min(1f, currentHealth / maxHealth)) : 0f;
            Color healthFillColor = Color.Lerp(HealthBarManager.PropBarHealthLow, HealthBarManager.PropBarHealthHigh, healthRatio);
            Color shieldFillColor = HealthBarManager.ShieldFillColor;
            Color emptyColor      = HealthBarManager.PropBarEmpty;
            Color corner          = ColorPalette.BlockBackground;

            for (int i = 0; i < n; i++)
            {
                float segStart = i * segmentSize;
                float segWidth = MathF.Min(segmentSize, totalMax - segStart);
                if (segWidth <= 0f) break;

                // Fraction of this segment covered by health, then by shield (continuing after health).
                float hFill    = MathF.Max(0f, MathF.Min(1f, (currentHealth - segStart) / segWidth));
                float sFillEnd = MathF.Max(0f, MathF.Min(1f, (currentHealth + currentShield - segStart) / segWidth));
                float sFill    = sFillEnd - hFill;

                int sx = (int)(valueX + i * (segW + BarSegmentGap));
                int sy = (int)y;
                int sw = (int)segW;
                int sh = BarRowHeight;

                DrawRect(spriteBatch, new Rectangle(sx, sy, sw, sh), emptyColor);
                if (hFill > 0f)
                    DrawRect(spriteBatch, new Rectangle(sx, sy, (int)(sw * hFill), sh), healthFillColor);
                if (sFill > 0f)
                    DrawRect(spriteBatch, new Rectangle(sx + (int)(sw * hFill), sy, (int)(sw * sFill), sh), shieldFillColor);

                if (sw >= 4 && sh >= 4)
                {
                    DrawRect(spriteBatch, new Rectangle(sx,          sy,          1, 1), corner);
                    DrawRect(spriteBatch, new Rectangle(sx + sw - 1, sy,          1, 1), corner);
                    DrawRect(spriteBatch, new Rectangle(sx,          sy + sh - 1, 1, 1), corner);
                    DrawRect(spriteBatch, new Rectangle(sx + sw - 1, sy + sh - 1, 1, 1), corner);
                }
            }
        }

        private static void DrawBulletListRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, float y, string label, string[] items)
        {
            if (items == null || items.Length == 0)
                return;

            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            Vector2 labelSize = font.MeasureString(label);
            float bulletX = x + Math.Max(labelSize.X + Padding, 120f);

            for (int i = 0; i < items.Length; i++)
            {
                float lineY = y + i * (font.LineHeight + RowSpacing);
                font.DrawString(spriteBatch, $"- {items[i]}", new Vector2(bulletX, lineY), UIStyle.TextColor);
            }
        }

        private static bool IsRowVisible(float y, float height, Rectangle bounds)
        {
            return y + height >= bounds.Y && y <= bounds.Bottom;
        }

        private static void DrawSectionHeader(SpriteBatch spriteBatch, UIStyle.UIFont font, string title, float x, float y)
        {
            font.DrawString(spriteBatch, title, new Vector2(x, y), UIStyle.AccentColor);
        }

        private static void DrawRow(SpriteBatch spriteBatch, UIStyle.UIFont font, string label, string value, float x, float y, bool isHidden = false)
        {
            Vector2 labelSize = font.MeasureString(label);
            Color labelColor = isHidden ? UIStyle.MutedTextColor * 0.55f : UIStyle.MutedTextColor;
            Color valueColor = isHidden ? UIStyle.TextColor      * 0.45f : UIStyle.TextColor;
            font.DrawString(spriteBatch, label, new Vector2(x, y), labelColor);

            float valueX = x + Math.Max(labelSize.X + Padding, 120f);
            font.DrawString(spriteBatch, value, new Vector2(valueX, y), valueColor);
        }

        private static void DrawBoolRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, float y, string label, bool boolValue, bool isHidden = false)
        {
            Vector2 labelSize = font.MeasureString(label);
            Color labelColor = isHidden ? UIStyle.MutedTextColor * 0.55f : UIStyle.MutedTextColor;
            font.DrawString(spriteBatch, label, new Vector2(x, y), labelColor);

            float dotX = x + Math.Max(labelSize.X + Padding, 120f);
            int dotSize = Math.Min(10, (int)font.LineHeight - 4);
            if (dotSize <= 0) return;
            int dotY = (int)(y + (font.LineHeight - dotSize) / 2f);

            Color dotColor = boolValue ? new Color(80, 220, 80) : new Color(220, 80, 80);
            DrawRect(spriteBatch, new Rectangle((int)dotX, dotY, dotSize, dotSize), dotColor);
        }

        private static void DrawColorRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, float y, string label, Color color, string hex)
        {
            Vector2 labelSize = font.MeasureString(label);
            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            int swatchSize = (int)Math.Max(12, font.LineHeight - 6);
            Rectangle swatch = new((int)(x + Math.Max(labelSize.X + Padding, 120f)), (int)y + 2, swatchSize, swatchSize);
            DrawRect(spriteBatch, swatch, color);
            DrawRectOutline(spriteBatch, swatch, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            float textX = swatch.Right + Padding;
            font.DrawString(spriteBatch, hex, new Vector2(textX, y), UIStyle.TextColor);
        }

        private static Texture2D GetPreviewTexture(GraphicsDevice graphicsDevice, Shape shape)
        {
            if (graphicsDevice == null || shape == null)
            {
                return null;
            }

            if (PreviewCache.TryGetValue(shape, out Texture2D cached) && cached != null && !cached.IsDisposed)
            {
                return cached;
            }

            Texture2D built = BuildPreviewTexture(graphicsDevice, shape);
            PreviewCache[shape] = built;
            return built;
        }

        private static Texture2D BuildPreviewTexture(GraphicsDevice graphicsDevice, Shape shape)
        {
            int baseWidth = Math.Max(1, shape.Width + (shape.OutlineWidth * 2));
            int baseHeight = Math.Max(1, shape.Height + (shape.OutlineWidth * 2));
            float scale = Math.Min(PreviewMaxSize / (float)baseWidth, PreviewMaxSize / (float)baseHeight);
            if (scale <= 0f)
            {
                scale = 1f;
            }

            int width = Math.Max(8, (int)MathF.Ceiling(baseWidth * scale));
            int height = Math.Max(8, (int)MathF.Ceiling(baseHeight * scale));
            int outline = Math.Max(0, (int)MathF.Ceiling(shape.OutlineWidth * scale));
            Color[] data = new Color[width * height];

            string shapeType = shape.ShapeType ?? "Rectangle";
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = shapeType switch
                    {
                        "Circle" => EvaluateCirclePixel(x, y, width, height, outline, shape.FillColor, shape.OutlineColor),
                        "Polygon" => EvaluatePolygonPixel(x, y, width, height, outline, shape.Sides, shape.FillColor, shape.OutlineColor),
                        _ => EvaluateRectanglePixel(x, y, width, height, outline, shape.FillColor, shape.OutlineColor)
                    };

                    data[y * width + x] = color;
                }
            }

            Texture2D texture = new(graphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private static Color EvaluateRectanglePixel(int x, int y, int width, int height, int outline, Color fill, Color outlineColor)
        {
            if (outline <= 0)
            {
                return fill;
            }

            bool isOutline = x < outline || y < outline || x >= width - outline || y >= height - outline;
            return isOutline ? outlineColor : fill;
        }

        private static Color EvaluateCirclePixel(int x, int y, int width, int height, int outline, Color fill, Color outlineColor)
        {
            Vector2 center = new(width / 2f, height / 2f);
            float dx = x - center.X;
            float dy = y - center.Y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));

            float outer = MathF.Min(width, height) / 2f;
            float inner = Math.Max(0f, outer - outline);

            if (distance <= inner)
            {
                return fill;
            }

            if (distance <= outer)
            {
                return outlineColor;
            }

            return Color.Transparent;
        }

        private static Color EvaluatePolygonPixel(int x, int y, int width, int height, int outline, int sides, Color fill, Color outlineColor)
        {
            if (sides < 3)
            {
                return EvaluateRectanglePixel(x, y, width, height, outline, fill, outlineColor);
            }

            float outerRadius = MathF.Min(width, height) / 2f;
            float innerRadius = Math.Max(0f, outerRadius - outline);

            bool insideFill = RenderPolygonPixel(x, y, width, height, sides, innerRadius);
            if (insideFill)
            {
                return fill;
            }

            bool insideOutline = RenderPolygonPixel(x, y, width, height, sides, outerRadius);
            return insideOutline ? outlineColor : Color.Transparent;
        }

        private static bool RenderPolygonPixel(int x, int y, int textureWidth, int textureHeight, int sides, float radius)
        {
            Vector2 center = new(textureWidth / 2f, textureHeight / 2f);
            Vector2 point = new Vector2(x, y) - center;

            float angle = MathF.Atan2(point.Y, point.X);
            float distance = point.Length();

            float sectorAngle = MathF.Tau / sides;
            float halfSector = sectorAngle / 2f;

            float rotatedAngle = (angle + MathF.Tau) % sectorAngle;
            float cornerDistance = MathF.Cos(halfSector) / MathF.Cos(rotatedAngle - halfSector);
            float maxDistance = radius * cornerDistance;

            return distance <= maxDistance;
        }

        private static void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixel == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixel, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixel == null || bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0)
            {
                return;
            }

            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(_pixel, top, color);
            spriteBatch.Draw(_pixel, bottom, color);
            spriteBatch.Draw(_pixel, left, color);
            spriteBatch.Draw(_pixel, right, color);
        }
    }
}
