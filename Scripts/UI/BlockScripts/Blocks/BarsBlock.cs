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
        private const int SegmentPanelH   = 22;
        private const int RelationDropdownW = 144;
        private const int RelationDropdownH = 26;
        private const int RelationClearBtnMinW = 30;
        private const int RelationClearBtnMaxW = 46;
        private const int RelationClearBtnH = 18;
        private const float RelationArrowThickness = 3f;
        private const float RelationArrowHeadLength = 12f;
        private const float RelationArrowHeadAngle = 0.52f;
        private const float PreviewChangeVisibleSeconds = 1.25f;
        private const float PreviewChangeEpsilon = 0.001f;

        // ── Session state ────────────────────────────────────────────────────
        private static List<BarConfigManager.BarEntry> _snapshot;
        private static List<BarConfigManager.BarEntry> _local;
        private static readonly Dictionary<string, List<BarConfigManager.BarEntry>> _snapshotByGroup = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<BarConfigManager.BarEntry>> _localByGroup = new(StringComparer.OrdinalIgnoreCase);
        private static bool _sessionStarted;
        private static bool _snapshotBarsVisible;
        private static string _selectedGroupKey = BarConfigManager.AllDestructiblesGroupKey;
        private static readonly UIDropdown _groupDropdown = new();
        private static string _tooltipRowKey;
        private static string _tooltipRowLabel;
        private static GameObject _previewSource;

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
        private static readonly Dictionary<BarType, float> _simPrevBarValues = new();
        private static readonly Dictionary<BarType, float> _simLastChangeTimes = new();

        // ── Shared FunColInterface (covers entire list area) ─────────────────
        private static FunColInterface _listFunCol;
        private static Rectangle _cachedListBounds = Rectangle.Empty;

        // FunCol column indices
        private const int ColDragRow  = 0;  // Green  — drag to reorder whole row
        private const int ColDragBar  = 1;  // Blue   — drag individual bar to merge/split
        private const int ColSegments = 2;  // Red    — configure segment count/visibility

        // ── Segment editing state ────────────────────────────────────────────
        private const int ColRelations = 3; // Orange - configure bar visibility relations

        private static string _editingSegBarType;
        private static string _segInputBuffer = "";

        private struct RelationDragState
        {
            public bool Active;
            public BarOverlayTarget DependentTarget;
            public BarOverlayTarget HoverTarget;
            public Point Cursor;
        }

        private struct RelationDropdownState
        {
            public bool Active;
            public BarOverlayTarget DependentTarget;
            public BarOverlayTarget SourceTarget;
        }

        private readonly struct RelationRemoveButton
        {
            public RelationRemoveButton(Rectangle bounds, BarType sourceType, BarConfigManager.BarRelationName relationName)
            {
                Bounds = bounds;
                SourceType = sourceType;
                RelationName = relationName;
            }

            public Rectangle Bounds { get; }
            public BarType SourceType { get; }
            public BarConfigManager.BarRelationName RelationName { get; }
        }

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
        private static KeyboardState _prevKeyboardState;
        private static Point _lastMousePosition;
        private static RelationDragState _relationDrag;
        private static RelationDropdownState _relationDropdownState;
        private static readonly UIDropdown _relationDropdown = new();
        private static readonly List<RelationRemoveButton> _relationRemoveButtons = new();

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
        private static readonly List<(BarType Type, int BarRow, Rectangle Rect)> _clearRelationBtnRects = new();
        private struct BarOverlayTarget
        {
            public bool Active;
            public int BarRow;
            public BarType BarType;
            public Rectangle RowRect;
            public Rectangle EntryRect;
        }
        private static BarOverlayTarget _segmentTarget;
        private static Rectangle _segmentOverlayRect;
        private static BarOverlayTarget _relationTarget;
        private static Rectangle _relationOverlayRect;

        // ── Section label rects (for section-level drag detection) ────────────
        private static Rectangle _showSectionRect;
        private static Rectangle _hideSectionRect;

        // ── Preview animation ────────────────────────────────────────────────
        private static float _previewRotation;
        private static float _previewAimAngle;

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public static string GetHoveredRowKey() => _tooltipRowKey;
        public static string GetHoveredRowLabel() => _tooltipRowLabel;

        public static bool TryGetTooltipEntries(string rowKey, out (string Text, string DataType)[] entries)
        {
            entries = null;
            if (string.IsNullOrWhiteSpace(rowKey) ||
                !TryParseRelationTooltipKey(rowKey, out string groupKey, out BarType barType))
            {
                return false;
            }

            if (!_localByGroup.TryGetValue(groupKey, out List<BarConfigManager.BarEntry> localEntries))
            {
                localEntries = BarConfigManager.CloneGroup(groupKey);
            }

            BarConfigManager.BarEntry entry = localEntries.FirstOrDefault(candidate => candidate.Type == barType);
            if (entry == null)
            {
                return false;
            }

            if (!entry.HasVisibilityRelations)
            {
                entries = [("No linked bars. This bar will never show.", string.Empty)];
                return true;
            }

            entries = entry.VisibilityRelations
                .Where(relation => relation != null)
                .Select(relation => (BarConfigManager.DescribeVisibilityRelation(entry.Type, relation), string.Empty))
                .ToArray();
            return entries.Length > 0;
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds,
            MouseState mouseState, MouseState previousMouseState)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            KeyboardState keyboardState = Keyboard.GetState();
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
            UpdateSimChangeTracking();

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
            _listFunCol.Update(_cachedListBounds, mouseState, dt, suppressHover: isDragging || blockLocked || BlockManager.IsCursorInAnyDragBar);

            if (blockLocked)
            {
                _segmentTarget = default;
                _segmentOverlayRect = Rectangle.Empty;
                _relationTarget = default;
                _relationOverlayRect = Rectangle.Empty;
                ResetRelationUi();
            }
            else
            {
                UpdateOverlayTargets(mouseState.Position);
            }

            UpdateGroupDropdown(mouseState, previousMouseState, keyboardState, blockLocked, contentBounds);

            if (!blockLocked)
            {
                HandleTopBarClick(mouseState, previousMouseState, contentBounds);
                HandleSegmentInput(mouseState, previousMouseState);
                bool relationUiBlocking = HandleRelationInput(mouseState, previousMouseState, keyboardState, _prevKeyboardState);
                if (!relationUiBlocking)
                {
                    HandleSplitButtonClick(mouseState, previousMouseState);
                    HandleDrag(mouseState, previousMouseState, shown, hidden);
                    HandleButtons(mouseState, previousMouseState, contentBounds, shown, hidden);
                }
            }

            float listContentH = CalculateTotalHeight(shown, hidden);
            _scroll.Update(listViewport, listContentH, BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState), previousMouseState);

            _prevMouse = mouseState;
            _prevKeyboardState = keyboardState;
            _lastMousePosition = mouseState.Position;
            UpdateRelationTooltipState();
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
            _clearRelationBtnRects.Clear();
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
            if (_segmentTarget.Active)
                DrawSegmentSettingsOverlay(sb);

            DrawRelationInteraction(sb);

            DrawDropIndicators(sb, shown, hidden);

            if (_drag.Active)
                DrawDragTooltip(sb, BuildDragTooltipText(shown, hidden), _lastMousePosition);

            // ── Fixed buttons at bottom ───────────────────────────────────────
            int buttonsY = contentBounds.Bottom - ButtonHeight - Padding;
            DrawButtons(sb, x, buttonsY, w, blockLocked);

            _scroll.Draw(sb, blockLocked);
            DrawRelationDropdown(sb);
            _groupDropdown.DrawOptionsOverlay(sb);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Session management
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureSession()
        {
            if (_sessionStarted) return;

            _snapshotByGroup.Clear();
            _localByGroup.Clear();
            foreach (BarConfigManager.BarConfigGroupDefinition group in BarConfigManager.GetGroupDefinitions())
            {
                List<BarConfigManager.BarEntry> snapshot = BarConfigManager.CloneGroup(group.Key);
                _snapshotByGroup[group.Key] = snapshot;
                _localByGroup[group.Key] = CloneBarEntries(snapshot);
            }

            _selectedGroupKey    = BarConfigManager.GetDefaultGroupKey();
            SyncSelectedGroupBuffers();
            EnsureGroupDropdownOptions();
            _snapshotBarsVisible = BarConfigManager.BarsVisible;
            ReseedPreviewSimulation();

            _simPrevBarValues.Clear();
            _simLastChangeTimes.Clear();
            _segmentTarget = default;
            _segmentOverlayRect = Rectangle.Empty;
            _tooltipRowKey = null;
            _tooltipRowLabel = null;
            ResetRelationUi();
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
                },
                new DragHandleFeature("Link Bars")
                {
                    ShowTextWhenExpanded = true,
                    ExpandedInstruction  = "Drag controlled bar to source bar"
                }
            );
            _listFunCol.SuppressTooltipWarnings = true;
            _listFunCol.LockHoveredColumnUntilExit = true;
            _listFunCol.HoveredColumnFillsFieldWhileLocked = true;
        }

        private static Agent FindPlayer()
        {
            var objs = Core.Instance?.GameObjects;
            if (objs == null) return null;
            foreach (var obj in objs)
                if (obj is Agent a && a.IsPlayer) return a;
            return null;
        }

        private static void EnsureGroupDropdownOptions()
        {
            IEnumerable<UIDropdown.Option> options = BarConfigManager.GetGroupDefinitions()
                .OrderBy(group => group.RenderOrder)
                .Select(group => new UIDropdown.Option(group.Key, group.Label));
            _groupDropdown.SetOptions(options, _selectedGroupKey);
        }

        private static void SyncSelectedGroupBuffers()
        {
            _selectedGroupKey = BarConfigManager.NormalizeGroupKey(_selectedGroupKey);
            if (!_snapshotByGroup.TryGetValue(_selectedGroupKey, out _))
            {
                _snapshotByGroup[_selectedGroupKey] = BarConfigManager.CloneGroup(_selectedGroupKey);
            }

            if (!_localByGroup.TryGetValue(_selectedGroupKey, out _))
            {
                _localByGroup[_selectedGroupKey] = CloneBarEntries(_snapshotByGroup[_selectedGroupKey]);
            }

            _snapshot = _snapshotByGroup[_selectedGroupKey];
            _local = _localByGroup[_selectedGroupKey];
        }

        private static void UpdateGroupDropdown(MouseState mouse, MouseState prev, KeyboardState keyboardState, bool blockLocked, Rectangle contentBounds)
        {
            EnsureGroupDropdownOptions();
            GetTopBarLayout(contentBounds, out Rectangle groupRect, out _, out _);
            _groupDropdown.Bounds = groupRect;

            if (_groupDropdown.Update(mouse, prev, keyboardState, _prevKeyboardState, out string selectedGroupId, isDisabled: blockLocked))
            {
                SelectGroup(selectedGroupId);
            }
        }

        private static void SelectGroup(string groupKey)
        {
            string normalizedGroupKey = BarConfigManager.NormalizeGroupKey(groupKey);
            if (string.Equals(_selectedGroupKey, normalizedGroupKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedGroupKey = normalizedGroupKey;
            SyncSelectedGroupBuffers();
            EnsureGroupDropdownOptions();
            _groupDropdown.Close();
            _editingSegBarType = null;
            _segInputBuffer = string.Empty;
            _segmentTarget = default;
            _segmentOverlayRect = Rectangle.Empty;
            ResetRelationUi();
            ReseedPreviewSimulation();
        }

        private static void ReseedPreviewSimulation()
        {
            _previewSource = FindPreviewSource(_selectedGroupKey);
            GameObject source = _previewSource ?? FindPlayer();

            if (source != null)
            {
                _simMaxHealth        = Math.Max(1f, source.MaxHealth);
                _simMaxShield        = Math.Max(0f, source.MaxShield);
                _simMaxXP            = Math.Max(0f, source.MaxXP);
                _simHealth           = MathHelper.Clamp(source.CurrentHealth, 0f, _simMaxHealth);
                _simShield           = MathHelper.Clamp(source.CurrentShield, 0f, _simMaxShield);
                _simXP               = MathHelper.Clamp(source.CurrentXP, 0f, Math.Max(0f, _simMaxXP));
                _simHealthRegen      = source.HealthRegen;
                _simShieldRegen      = source.ShieldRegen;
                _simHealthRegenDelay = source.HealthRegenDelay;
                _simShieldRegenDelay = source.ShieldRegenDelay;

                if (source is Agent agent)
                {
                    _simHealthRegen      = agent.BodyAttributes.HealthRegen;
                    _simShieldRegen      = agent.BodyAttributes.ShieldRegen;
                    _simHealthRegenDelay = agent.BodyAttributes.HealthRegenDelay;
                    _simShieldRegenDelay = agent.BodyAttributes.ShieldRegenDelay;
                }
            }
            else
            {
                _simMaxHealth = 100f;
                _simMaxShield = 10f;
                _simMaxXP = 100f;
                _simHealth = _simMaxHealth;
                _simShield = _simMaxShield;
                _simXP = 0f;
                _simHealthRegen = 5f;
                _simShieldRegen = 3f;
                _simHealthRegenDelay = 5f;
                _simShieldRegenDelay = 3f;
            }

            _simLastHealthDmgTime = float.NegativeInfinity;
            _simLastShieldDmgTime = float.NegativeInfinity;
            _simPrevBarValues.Clear();
            _simLastChangeTimes.Clear();
        }

        private static GameObject FindPreviewSource(string groupKey)
        {
            var objects = Core.Instance?.GameObjects;
            if (objects == null)
            {
                return null;
            }

            Agent player = FindPlayer();
            if (player?.Shape != null &&
                BarConfigManager.DoesObjectMatchGroup(player, groupKey, includeDescendants: true))
            {
                return player;
            }

            foreach (GameObject obj in objects)
            {
                if (obj?.Shape == null || !obj.IsDestructible)
                {
                    continue;
                }

                if (BarConfigManager.DoesObjectMatchGroup(obj, groupKey, includeDescendants: true))
                {
                    return obj;
                }
            }

            return objects.FirstOrDefault(obj => obj?.Shape != null && obj.IsDestructible);
        }

        private static bool IsGroupDirty(string groupKey)
        {
            string normalizedGroupKey = BarConfigManager.NormalizeGroupKey(groupKey);
            return _snapshotByGroup.TryGetValue(normalizedGroupKey, out List<BarConfigManager.BarEntry> snapshot) &&
                _localByGroup.TryGetValue(normalizedGroupKey, out List<BarConfigManager.BarEntry> local) &&
                !BarEntryListsEqual(snapshot, local);
        }

        private static void RefreshCleanDescendantGroups(string changedGroupKey)
        {
            string normalizedChangedGroupKey = BarConfigManager.NormalizeGroupKey(changedGroupKey);
            foreach (BarConfigManager.BarConfigGroupDefinition group in BarConfigManager.GetGroupDefinitions())
            {
                if (string.Equals(group.Key, normalizedChangedGroupKey, StringComparison.OrdinalIgnoreCase) ||
                    !IsDescendantGroup(group.Key, normalizedChangedGroupKey) ||
                    IsGroupDirty(group.Key))
                {
                    continue;
                }

                List<BarConfigManager.BarEntry> snapshot = BarConfigManager.CloneGroup(group.Key);
                _snapshotByGroup[group.Key] = snapshot;
                _localByGroup[group.Key] = CloneBarEntries(snapshot);
            }

            SyncSelectedGroupBuffers();
        }

        private static bool IsDescendantGroup(string groupKey, string ancestorGroupKey)
        {
            string current = BarConfigManager.NormalizeGroupKey(groupKey);
            string target = BarConfigManager.NormalizeGroupKey(ancestorGroupKey);
            if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Dictionary<string, string> parents = BarConfigManager.GetGroupDefinitions()
                .ToDictionary(group => group.Key, group => group.ParentKey, StringComparer.OrdinalIgnoreCase);

            while (!string.IsNullOrWhiteSpace(current) && parents.TryGetValue(current, out string parentKey))
            {
                if (string.IsNullOrWhiteSpace(parentKey))
                {
                    return false;
                }

                if (string.Equals(parentKey, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = parentKey;
            }

            return false;
        }

        private static List<BarConfigManager.BarEntry> CloneBarEntries(IEnumerable<BarConfigManager.BarEntry> entries)
        {
            return entries?.Select(entry => entry?.Clone()).Where(entry => entry != null).ToList() ?? new();
        }

        private static bool BarEntryListsEqual(IReadOnlyList<BarConfigManager.BarEntry> left, IReadOnlyList<BarConfigManager.BarEntry> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                BarConfigManager.BarEntry a = left[i];
                BarConfigManager.BarEntry b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }

                    continue;
                }

                if (a.Type != b.Type ||
                    a.BarRow != b.BarRow ||
                    a.PositionInRow != b.PositionInRow ||
                    a.SegmentCount != b.SegmentCount ||
                    a.SegmentsEnabled != b.SegmentsEnabled ||
                    a.IsHidden != b.IsHidden ||
                    a.ShowPercent != b.ShowPercent ||
                    MathF.Abs(a.VisibilityFadeOutSeconds - b.VisibilityFadeOutSeconds) > 0.0001f ||
                    BarConfigManager.EncodeVisibilityRelations(a.VisibilityRelations) != BarConfigManager.EncodeVisibilityRelations(b.VisibilityRelations))
                {
                    return false;
                }
            }

            return true;
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

        private static void GetTopBarLayout(Rectangle contentBounds, out Rectangle groupRect, out Rectangle toggleRect, out Rectangle simRect)
        {
            int x = contentBounds.X + Padding;
            int y = contentBounds.Y + PreviewHeight + Padding;
            int w = contentBounds.Width - Padding * 2;
            int h = TopBarH - 4;
            int gap = 6;
            int minGroupWidth = Math.Min(140, Math.Max(90, w / 2));
            int maxGroupWidth = Math.Max(minGroupWidth, w - 180);
            int groupW = Math.Clamp((int)MathF.Round(w * 0.38f), minGroupWidth, maxGroupWidth);
            int remainingW = Math.Max(80, w - groupW - gap * 2);
            int toggleW = Math.Clamp((int)MathF.Round(remainingW * 0.36f), 90, Math.Max(90, remainingW / 2));
            int simW = Math.Max(80, w - groupW - toggleW - gap * 2);

            groupRect = new Rectangle(x, y + 2, groupW, h);
            toggleRect = new Rectangle(groupRect.Right + gap, y + 2, toggleW, h);
            simRect = new Rectangle(toggleRect.Right + gap, y + 2, simW, h);
        }

        private static void DrawTopBtnRow(SpriteBatch sb, int x, int y, int w, bool blockLocked)
        {
            bool barsOn  = BarConfigManager.BarsVisible;
            Point mouse  = _lastMousePosition;
            int h = TopBarH - 4;
            int gap = 6;
            int minGroupWidth = Math.Min(140, Math.Max(90, w / 2));
            int maxGroupWidth = Math.Max(minGroupWidth, w - 180);
            int groupW = Math.Clamp((int)MathF.Round(w * 0.38f), minGroupWidth, maxGroupWidth);
            int remainingW = Math.Max(80, w - groupW - gap * 2);
            int toggleW = Math.Clamp((int)MathF.Round(remainingW * 0.36f), 90, Math.Max(90, remainingW / 2));
            int simW = Math.Max(80, w - groupW - toggleW - gap * 2);

            Rectangle groupRect = new(x, y + 2, groupW, h);
            Rectangle toggleRect = new(groupRect.Right + gap, y + 2, toggleW, h);
            Rectangle simRect = new(toggleRect.Right + gap, y + 2, simW, h);

            _groupDropdown.Bounds = groupRect;
            _groupDropdown.Draw(sb, drawOptions: false, isDisabled: blockLocked);

            bool toggleHov = !blockLocked && UIButtonRenderer.IsHovered(toggleRect, mouse);
            UIButtonRenderer.Draw(sb, toggleRect,
                barsOn ? "Bars ON" : "Bars OFF",
                barsOn ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                toggleHov,
                isDisabled: blockLocked,
                fillOverride:       barsOn ? ColorPalette.IndicatorActive * 0.75f  : ColorPalette.IndicatorInactive * 0.68f,
                hoverFillOverride:  barsOn ? ColorPalette.IndicatorActive * 0.95f  : ColorPalette.IndicatorInactive * 0.88f);

            bool simHov = !blockLocked && UIButtonRenderer.IsHovered(simRect, mouse);
            UIButtonRenderer.Draw(sb, simRect,
                "Simulate Damage",
                UIButtonRenderer.ButtonStyle.Grey,
                simHov,
                isDisabled: blockLocked,
                fillOverride:      ColorPalette.ButtonPrimary,
                hoverFillOverride: ColorPalette.ButtonPrimaryHover);
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
            int previewRadius = _previewSource?.Shape != null
                ? Math.Max(_previewSource.Shape.Width, _previewSource.Shape.Height) / 2
                : PreviewPlayerR;

            DrawPreviewSource(sb, cx, cy);

            // Bars below dummy player — width matches player diameter for proportionality
            int barW = Math.Min(previewBounds.Width - Padding * 4, Math.Max(previewRadius * 2, PreviewPlayerR * 2));
            int barX = cx - barW / 2;
            int barY = cy + previewRadius + BarPreviewGap;

            var groups = GroupByRow(_local);
            foreach (var rowEntries in groups)
            {
                var visible = rowEntries.Where(ShouldPreviewEntryRender).ToList();
                if (visible.Count == 0) continue;

                // Compute per-entry max values for proportional width allocation
                float totalMax = 0f;
                foreach (var entry in visible)
                {
                    if (!TryGetSimBarValue(entry.Type, out _, out float entryMax))
                    {
                        continue;
                    }

                    totalMax += Math.Max(0f, entryMax);
                }
                if (totalMax <= 0f) totalMax = Math.Max(1f, visible.Count);

                int bx = barX;
                for (int vi = 0; vi < visible.Count; vi++)
                {
                    var entry = visible[vi];
                    if (!TryGetSimBarValue(entry.Type, out _, out float entryMax))
                    {
                        continue;
                    }

                    bool isLast = vi == visible.Count - 1;
                    int bw = isLast ? (barX + barW - bx) : (int)(barW * Math.Max(0f, entryMax) / totalMax);
                    int barH = HealthBarManager.BarHeight;
                    if (bw > 0) DrawPreviewBar(sb, entry, bx, barY, bw, barH);
                    bx += bw;
                }
                barY += HealthBarManager.BarHeight + BarPreviewGap;
            }
        }

        private static void DrawPreviewSource(SpriteBatch sb, int cx, int cy)
        {
            if (_previewSource is Agent player && player.Shape != null)
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
            else if (_previewSource?.Shape != null)
            {
                _previewSource.Shape.DrawAt(sb, new Vector2(cx, cy), _previewRotation);
            }
            else
            {
                // Fallback: filled circle + orientation line
                DrawCircleApprox(sb, cx, cy, PreviewPlayerR, ColorPalette.ShieldBar * 0.78f, ColorPalette.ShieldBar * 0.55f, 3);
                float rot = _previewAimAngle;
                int ex = cx + (int)(MathF.Cos(rot) * (PreviewPlayerR + 8));
                int ey = cy + (int)(MathF.Sin(rot) * (PreviewPlayerR + 8));
                // Draw orientation line using thin rect
                int lx = Math.Min(cx, ex), ly = Math.Min(cy, ey);
                int lw = Math.Max(1, Math.Abs(ex - cx));
                int lh = Math.Max(1, Math.Abs(ey - cy));
                DrawRect(sb, new Rectangle(lx, ly, lw, lh), ColorPalette.TextPrimary * 0.78f);
            }
        }

        private static void DrawPreviewBar(SpriteBatch sb, BarConfigManager.BarEntry entry,
            int x, int y, int w, int h)
        {
            float current, max;
            float previewSegmentPts = 0f;
            int previewSegmentCount = entry.SegmentsEnabled ? Math.Max(1, entry.SegmentCount) : 0;
            switch (entry.Type)
            {
                case BarType.Health:
                    current = _simHealth;
                    max = _simMaxHealth;
                    previewSegmentPts = entry.SegmentsEnabled ? HealthBarManager.SegmentSize : 0f;
                    break;
                case BarType.Shield:
                    current = _simShield;
                    max = _simMaxShield;
                    previewSegmentPts = entry.SegmentsEnabled ? HealthBarManager.SegmentSize : 0f;
                    break;
                case BarType.XP:
                    current = _simXP;
                    max = _simMaxXP;
                    previewSegmentPts = entry.SegmentsEnabled ? HealthBarManager.SegmentSize : 0f;
                    break;
                case BarType.HealthRegen:
                {
                    if (_simHealthRegenDelay <= 0f || _simHealthRegen <= 0f) return;
                    float elapsed = MathHelper.Clamp(_simTime - _simLastHealthDmgTime, 0f, _simHealthRegenDelay);
                    current = elapsed; max = _simHealthRegenDelay;
                    previewSegmentPts = entry.SegmentsEnabled && previewSegmentCount > 1
                        ? max / previewSegmentCount
                        : 0f;
                    break;
                }
                case BarType.ShieldRegen:
                {
                    if (_simShieldRegenDelay <= 0f || _simShieldRegen <= 0f) return;
                    float elapsed = MathHelper.Clamp(_simTime - _simLastShieldDmgTime, 0f, _simShieldRegenDelay);
                    current = elapsed; max = _simShieldRegenDelay;
                    previewSegmentPts = entry.SegmentsEnabled && previewSegmentCount > 1
                        ? max / previewSegmentCount
                        : 0f;
                    break;
                }
                default: return;
            }
            if (max <= 0f) return;

            float ratio = MathHelper.Clamp(current / max, 0f, 1f);
            Color fill = entry.Type == BarType.Health
                ? Color.Lerp(HealthBarManager.HealthFillLow, HealthBarManager.HealthFillHigh, ratio)
                : GetBarColor(entry.Type);
            HealthBarManager.DrawBarPreview(sb, _pixel, x, y, w, h, current, max, fill, previewSegmentPts, previewSegmentCount);

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
            string[] shortNames = { "Drag Row", "Drag Bar", "Bar Settings", "Link Bars" };
            string[] instrNames =
            {
                "Drag to reorder row",
                "Drag bar to merge",
                "Hover bar for settings",
                "Drag bar -> source bar"
            };
            string[] instrFallbacks =
            {
                "Reorder rows",
                "Drag to merge",
                "Hover to edit",
                "Drag to link"
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
                    if (expanded)
                    {
                        Vector2 primarySize = font.MeasureString(text);
                        if (primarySize.X + 4 > colBounds.Width)
                        {
                            text = instrFallbacks[i];
                        }
                    }

                    Vector2 ts = font.MeasureString(text);
                    if (ts.X + 4 <= colBounds.Width)
                    {
                        float tx = colBounds.X + (colBounds.Width  - ts.X) / 2f;
                        float ty = y            + (ColHeaderH        - ts.Y) / 2f;
                        font.DrawString(sb, text, new Vector2(tx, ty), tc);
                    }
                    else
                    {
                        string abbrev;
                        if (expanded)
                        {
                            abbrev = i == 0 ? "Reorder" : i == 1 ? "Merge" : i == 2 ? "Edit" : "Link";
                        }
                        else
                        {
                            abbrev = i == 0 ? "Row" : i == 1 ? "Drag" : i == 2 ? "Set" : "Link";
                        }
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
                else if ((hovCol == ColSegments || hovCol == ColRelations) && eb.Contains(_lastMousePosition))
                    DrawHoverHighlight(sb, eb);

                // Split button: appears per-bar when Drag Bar is active and row has multiple bars
                if (hovCol == ColDragBar && rowEntries.Count > 1)
                {
                    int sbX = ex + ew - SplitBtnSize - 3;
                    int sbY = y + (RowHeight - SplitBtnSize) / 2;
                    var splitRect = new Rectangle(sbX, sbY, SplitBtnSize, SplitBtnSize);
                    _splitBtnRects.Add((entry.Type, barRow, splitRect));
                    Color splitBg = splitRect.Contains(_lastMousePosition)
                        ? ColorPalette.ButtonPrimaryHover : ColorPalette.ButtonPrimary;
                    DrawInlineActionButton(sb, splitRect, "<", splitBg, FunColInterface.GetColumnColor(ColDragBar), Color.White * 0.9f);
                }

                if (hovCol == ColRelations)
                {
                    Rectangle clearRect = GetRelationClearButtonRect(eb);
                    _clearRelationBtnRects.Add((entry.Type, barRow, clearRect));
                    bool hasRelations = entry.HasVisibilityRelations;
                    bool hoveredClear = clearRect.Contains(_lastMousePosition);
                    Color clearFill = hasRelations
                        ? (hoveredClear ? ColorPalette.Warning : ColorPalette.ButtonNeutral)
                        : ColorPalette.BlockBackground * 0.7f;
                    Color clearOutline = hasRelations
                        ? FunColInterface.GetColumnColor(ColRelations)
                        : UIStyle.BlockBorder;
                    Color clearText = hasRelations
                        ? Color.White * 0.92f
                        : UIStyle.MutedTextColor;
                    string clearLabel = clearRect.Width >= 38 ? "Clear" : "Clr";
                    DrawInlineActionButton(sb, clearRect, clearLabel, clearFill, clearOutline, clearText);
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
            if (!font.IsAvailable || _pixel == null || !_segmentTarget.Active) return;
            if (!TryGetLocalEntry(_segmentTarget.BarRow, _segmentTarget.BarType, out BarConfigManager.BarEntry entry)) return;

            Rectangle panel = _segmentOverlayRect == Rectangle.Empty ? GetSegmentOverlayRect(_segmentTarget) : _segmentOverlayRect;
            if (panel == Rectangle.Empty) return;

            DrawRect(sb, panel, ColorPalette.OverlayBackground);
            DrawOutline(sb, panel, FunColInterface.GetColumnColor(ColSegments), 1);

            string key = entry.Type.ToString();
            int fx = panel.X + 4;
            int fy = panel.Y + (panel.Height - (int)font.LineHeight) / 2;

            int checkSize = 12;
            var checkRect = new Rectangle(fx, panel.Y + (panel.Height - checkSize) / 2, checkSize, checkSize);
            DrawRect(sb, checkRect, entry.SegmentsEnabled ? ColorPalette.IndicatorActive : ColorPalette.ToggleIdle);
            DrawOutline(sb, checkRect, UIStyle.BlockBorder, 1);
            fx += checkSize + 4;

            font.DrawString(sb, "Segs", new Vector2(fx, fy), UIStyle.MutedTextColor);
            fx += (int)font.MeasureString("Segs").X + 4;

            if (entry.SegmentsEnabled)
            {
                int arrowY = panel.Y + (panel.Height - ArrowBtnSize) / 2;

                DrawRect(sb, new Rectangle(fx, arrowY, ArrowBtnSize, ArrowBtnSize), ColorPalette.ButtonNeutral);
                font.DrawString(sb, "-", new Vector2(fx + ArrowBtnSize / 2f - 3, arrowY + 1), UIStyle.TextColor);
                fx += ArrowBtnSize + 2;

                string val = _editingSegBarType == key ? _segInputBuffer : entry.SegmentCount.ToString();
                var inputRect = new Rectangle(fx, arrowY, SegInputWidth, ArrowBtnSize);
                DrawRect(sb, inputRect, ColorPalette.ChatInputField);
                DrawOutline(sb, inputRect, _editingSegBarType == key ? UIStyle.AccentColor : UIStyle.BlockBorder, 1);
                font.DrawString(sb, val,
                    new Vector2(fx + 4, arrowY + (ArrowBtnSize - font.LineHeight) / 2f), UIStyle.TextColor);
                fx += SegInputWidth + 2;

                DrawRect(sb, new Rectangle(fx, arrowY, ArrowBtnSize, ArrowBtnSize), ColorPalette.ButtonNeutral);
                font.DrawString(sb, "+", new Vector2(fx + ArrowBtnSize / 2f - 3, arrowY + 1), UIStyle.TextColor);
                fx += ArrowBtnSize + 2;
            }

            fx += 6;
            int pctCheckSize = 12;
            var pctCheckRect = new Rectangle(fx, panel.Y + (panel.Height - pctCheckSize) / 2, pctCheckSize, pctCheckSize);
            DrawRect(sb, pctCheckRect, entry.ShowPercent ? ColorPalette.IndicatorActive : ColorPalette.ToggleIdle);
            DrawOutline(sb, pctCheckRect, UIStyle.BlockBorder, 1);
            fx += pctCheckSize + 4;
            font.DrawString(sb, "% Text", new Vector2(fx, fy), UIStyle.MutedTextColor);
        }

        private static void DrawRelationInteraction(SpriteBatch sb)
        {
            _relationRemoveButtons.Clear();

            if (_pixel == null)
            {
                return;
            }

            if (_relationDrag.Active)
            {
                if (_relationDrag.HoverTarget.Active)
                {
                    DrawRelationConnection(sb, _relationDrag.DependentTarget, _relationDrag.HoverTarget, FunColInterface.GetColumnColor(ColRelations));
                }
                else
                {
                    DrawRelationArrow(
                        sb,
                        GetEntryCenter(_relationDrag.DependentTarget.EntryRect),
                        _relationDrag.Cursor.ToVector2(),
                        FunColInterface.GetColumnColor(ColRelations));
                }
                return;
            }

            if (_relationDropdownState.Active)
            {
                DrawRelationConnection(sb, _relationDropdownState.DependentTarget, _relationDropdownState.SourceTarget, FunColInterface.GetColumnColor(ColRelations));
                return;
            }

            if (!_relationTarget.Active ||
                !TryGetLocalEntry(_relationTarget.BarRow, _relationTarget.BarType, out BarConfigManager.BarEntry entry))
            {
                return;
            }

            DrawOutline(sb, _relationTarget.EntryRect, FunColInterface.GetColumnColor(ColRelations) * 0.90f, 2);
        }

        private static void DrawRelationDropdown(SpriteBatch sb)
        {
            if (!_relationDropdownState.Active)
            {
                return;
            }

            _relationDropdown.Draw(sb, drawOptions: false);
            _relationDropdown.DrawOptionsOverlay(sb);
        }

        private static void DrawRelationOverlay(
            SpriteBatch sb,
            BarOverlayTarget target,
            BarConfigManager.BarEntry entry,
            Rectangle panel)
        {
            _ = sb;
            _ = target;
            _ = entry;
            _ = panel;
        }

        private static void UpdateRelationTooltipState()
        {
            _tooltipRowKey = null;
            _tooltipRowLabel = null;

            if (_relationDrag.Active ||
                _relationDropdownState.Active ||
                !_relationTarget.Active ||
                (_listFunCol?.HoveredColumn ?? -1) != ColRelations ||
                !TryGetLocalEntry(_relationTarget.BarRow, _relationTarget.BarType, out BarConfigManager.BarEntry entry))
            {
                return;
            }

            _tooltipRowKey = BuildRelationTooltipKey(_selectedGroupKey, entry.Type);
            _tooltipRowLabel = $"{BarConfigManager.GetGroupLabel(_selectedGroupKey)} {BarConfigManager.GetBarShortLabel(entry.Type)}";
        }

        private static string BuildRelationTooltipKey(string groupKey, BarType barType)
        {
            return $"bars_relation:{BarConfigManager.NormalizeGroupKey(groupKey)}:{barType}";
        }

        private static bool TryParseRelationTooltipKey(string rowKey, out string groupKey, out BarType barType)
        {
            groupKey = null;
            barType = default;
            if (string.IsNullOrWhiteSpace(rowKey) || !rowKey.StartsWith("bars_relation:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = rowKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !Enum.TryParse(parts[2], true, out barType))
            {
                return false;
            }

            groupKey = BarConfigManager.NormalizeGroupKey(parts[1]);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: buttons
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawButtons(SpriteBatch sb, int x, int y, int w, bool blockLocked)
        {
            string[] labels = { "Save", "Apply", "Discard" };
            Color[]  fills  =
            {
                ColorPalette.IndicatorActive * 0.7f,
                ColorPalette.Warning * 0.5f,
                ColorPalette.IndicatorInactive * 0.73f
            };
            Color[] hoverFills =
            {
                ColorPalette.IndicatorActive * 0.9f,
                ColorPalette.Warning * 0.65f,
                ColorPalette.IndicatorInactive * 0.93f
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
            if (!UIStyle.AreTooltipsEnabled) return;

            UIStyle.UIFont font = UIStyle.FontTech;
            if (!font.IsAvailable || _pixel == null || string.IsNullOrEmpty(text)) return;
            Vector2 ts  = font.MeasureString(text);
            int ox = mousePos.X + 14;
            int oy = mousePos.Y - (int)(ts.Y / 2f);
            var bg = new Rectangle(ox - 4, oy - 2, (int)ts.X + 8, (int)ts.Y + 4);
            DrawRect(sb, bg, ColorPalette.OverlayBackground);
            DrawOutline(sb, bg, UIStyle.AccentColor, 1);
            font.DrawString(sb, text, new Vector2(ox, oy), Color.White);
        }

        private static void DrawInlineActionButton(SpriteBatch sb, Rectangle rect, string label, Color fill, Color outline, Color textColor)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            DrawRect(sb, rect, fill);
            DrawOutline(sb, rect, outline, 1);

            UIStyle.UIFont font = UIStyle.FontTech;
            if (!font.IsAvailable || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            Vector2 size = font.MeasureString(label);
            float tx = rect.X + (rect.Width - size.X) / 2f;
            float ty = rect.Y + (rect.Height - size.Y) / 2f;
            font.DrawString(sb, label, new Vector2(tx, ty), textColor);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drawing: drop indicators
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawRelationConnection(SpriteBatch sb, BarOverlayTarget dependentTarget, BarOverlayTarget sourceTarget, Color color)
        {
            if (!dependentTarget.Active || !sourceTarget.Active)
            {
                return;
            }

            DrawOutline(sb, dependentTarget.EntryRect, color * 0.90f, 2);
            DrawOutline(sb, sourceTarget.EntryRect, GetBarColor(sourceTarget.BarType) * 0.90f, 2);
            DrawRelationArrow(sb, GetEntryCenter(dependentTarget.EntryRect), GetEntryCenter(sourceTarget.EntryRect), color);
        }

        private static Vector2 GetEntryCenter(Rectangle rect)
        {
            return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
        }

        private static void DrawRelationArrow(SpriteBatch sb, Vector2 start, Vector2 end, Color color)
        {
            Vector2 delta = end - start;
            if (delta.LengthSquared() < 1f)
            {
                return;
            }

            Vector2 direction = Vector2.Normalize(delta);
            Vector2 arrowTip = end - (direction * 6f);
            Vector2 arrowBase = start + (direction * 10f);
            if ((arrowTip - arrowBase).LengthSquared() < 1f)
            {
                return;
            }

            DrawLine(sb, arrowBase, arrowTip, color, RelationArrowThickness);

            float angle = MathF.Atan2(direction.Y, direction.X);
            Vector2 left = arrowTip - new Vector2(MathF.Cos(angle - RelationArrowHeadAngle), MathF.Sin(angle - RelationArrowHeadAngle)) * RelationArrowHeadLength;
            Vector2 right = arrowTip - new Vector2(MathF.Cos(angle + RelationArrowHeadAngle), MathF.Sin(angle + RelationArrowHeadAngle)) * RelationArrowHeadLength;
            DrawLine(sb, arrowTip, left, color, RelationArrowThickness);
            DrawLine(sb, arrowTip, right, color, RelationArrowThickness);
        }

        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness)
        {
            if (_pixel == null)
            {
                return;
            }

            Vector2 delta = end - start;
            float length = delta.Length();
            if (length <= 0.001f)
            {
                return;
            }

            float angle = MathF.Atan2(delta.Y, delta.X);
            sb.Draw(
                _pixel,
                start,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(length, MathF.Max(1f, thickness)),
                SpriteEffects.None,
                0f);
        }

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

            GetTopBarLayout(contentBounds, out _, out Rectangle toggleRect, out Rectangle simRect);
            if (toggleRect.Contains(mouse.Position))
            {
                BarConfigManager.BarsVisible = !BarConfigManager.BarsVisible;
                return;
            }

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

        private static void HandleSegmentInput(MouseState mouse, MouseState prev)
        {
            bool clicked = mouse.LeftButton == ButtonState.Released &&
                           prev.LeftButton  == ButtonState.Pressed;
            bool overlayFocused = _segmentTarget.Active && _segmentOverlayRect.Contains(mouse.Position);
            bool allowOverlay = _listFunCol?.HoveredColumn == ColSegments || overlayFocused;

            if (!allowOverlay)
            {
                if (clicked && _editingSegBarType != null)
                {
                    CommitSegInput();
                    _editingSegBarType = null;
                }
                return;
            }

            if (!clicked || !_segmentTarget.Active || _segmentOverlayRect == Rectangle.Empty) return;
            if (!TryGetLocalEntry(_segmentTarget.BarRow, _segmentTarget.BarType, out BarConfigManager.BarEntry entry)) return;

            string key = entry.Type.ToString();
            Rectangle panel = _segmentOverlayRect;

            // Segments-enabled checkbox
            int fx = panel.X + 4;
            int checkSize = 12;
            var checkRect = new Rectangle(fx, panel.Y + (panel.Height - checkSize) / 2, checkSize, checkSize);
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
                int arrowY = panel.Y + (panel.Height - ArrowBtnSize) / 2;

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
            var pctCheckRect = new Rectangle(fx, panel.Y + (panel.Height - pctCheckSize) / 2, pctCheckSize, pctCheckSize);
            if (pctCheckRect.Contains(mouse.Position))
            {
                entry.ShowPercent = !entry.ShowPercent;
                return;
            }

            // Click elsewhere — commit edit
            if (_editingSegBarType != null) { CommitSegInput(); _editingSegBarType = null; }
        }

        private static bool HandleRelationInput(
            MouseState mouse,
            MouseState prev,
            KeyboardState keyboardState,
            KeyboardState previousKeyboardState)
        {
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed &&
                               prev.LeftButton == ButtonState.Released;
            bool leftReleased = mouse.LeftButton == ButtonState.Released &&
                                prev.LeftButton == ButtonState.Pressed;

            if (leftReleased && TryHandleRelationClearClick(mouse.Position))
            {
                return true;
            }

            if (_relationDropdownState.Active)
            {
                bool pointerOverDropdown = _relationDropdown.IsPointerOverDropdown(mouse.Position);
                bool selectionChanged = _relationDropdown.Update(
                    mouse,
                    prev,
                    keyboardState,
                    previousKeyboardState,
                    out string selectionChangedId);

                if (selectionChanged)
                {
                    ApplyPendingRelationSelection(selectionChangedId);
                    ResetRelationUi();
                    return true;
                }

                bool clickedOutsideDropdown = leftPressed && !pointerOverDropdown;
                bool escapePressed = keyboardState.IsKeyDown(Keys.Escape) && previousKeyboardState.IsKeyUp(Keys.Escape);
                if (clickedOutsideDropdown || escapePressed)
                {
                    ResetRelationUi();
                }

                return true;
            }

            if (_relationDrag.Active)
            {
                _relationDrag.Cursor = mouse.Position;
                _relationDrag.HoverTarget = TryGetBarEntryAtPoint(mouse.Position, out BarOverlayTarget hoverTarget)
                    ? hoverTarget
                    : default;

                if (leftReleased)
                {
                    if (_relationDrag.HoverTarget.Active)
                    {
                        OpenRelationDropdown(_relationDrag.DependentTarget, _relationDrag.HoverTarget, mouse.Position);
                    }
                    else
                    {
                        ResetRelationUi();
                    }
                }

                return true;
            }

            if ((_listFunCol?.HoveredColumn ?? -1) != ColRelations)
            {
                return false;
            }

            if (leftPressed && TryGetRelationClearButtonAtPoint(mouse.Position, out _, out _))
            {
                return true;
            }

            if (!leftPressed)
            {
                return false;
            }

            if (!TryGetBarEntryAtPoint(mouse.Position, out BarOverlayTarget dependentTarget))
            {
                return false;
            }

            _relationDrag = new RelationDragState
            {
                Active = true,
                DependentTarget = dependentTarget,
                HoverTarget = dependentTarget,
                Cursor = mouse.Position
            };
            return true;
        }

        private static bool TryHandleRelationClearClick(Point pointer)
        {
            if (!TryGetRelationClearButtonAtPoint(pointer, out BarType barType, out int barRow) ||
                !TryGetLocalEntry(barRow, barType, out BarConfigManager.BarEntry entry))
            {
                return false;
            }

            if (!entry.HasVisibilityRelations)
            {
                return false;
            }

            BarConfigManager.ClearVisibilityRelations(entry);
            return true;
        }

        private static void CommitSegInput()
        {
            if (string.IsNullOrWhiteSpace(_segInputBuffer)) return;
            if (!int.TryParse(_segInputBuffer.Trim(), out int val)) return;
            val = Math.Max(1, val);
            var entry = _local.FirstOrDefault(e => e.Type.ToString() == _editingSegBarType);
            if (entry != null) entry.SegmentCount = val;
        }

        private static void OpenRelationDropdown(BarOverlayTarget dependentTarget, BarOverlayTarget sourceTarget, Point pointer)
        {
            _relationDrag = default;

            if (!TryGetLocalEntry(dependentTarget.BarRow, dependentTarget.BarType, out BarConfigManager.BarEntry entry))
            {
                ResetRelationUi();
                return;
            }

            List<UIDropdown.Option> options = GetRelationDropdownOptions(dependentTarget, sourceTarget).ToList();
            BarConfigManager.BarRelation existingRelation = entry.VisibilityRelations?
                .FirstOrDefault(r => r != null && r.SourceType == sourceTarget.BarType && options.Any(o => o.Id == GetRelationDropdownId(r.RelationName)));
            string selectedId = existingRelation != null
                ? GetRelationDropdownId(existingRelation.RelationName)
                : GetRelationDropdownId(BarConfigManager.BarRelationName.BelowFull);

            _relationDropdown.Bounds = GetRelationDropdownBounds(pointer, options.Count);
            _relationDropdown.SetOptions(options, selectedId);
            _relationDropdown.Open();

            _relationDropdownState = new RelationDropdownState
            {
                Active = true,
                DependentTarget = dependentTarget,
                SourceTarget = sourceTarget
            };
        }

        private static IEnumerable<UIDropdown.Option> GetRelationDropdownOptions(
            BarOverlayTarget dependentTarget,
            BarOverlayTarget sourceTarget)
        {
            yield return new UIDropdown.Option(
                GetRelationDropdownId(BarConfigManager.BarRelationName.BelowFull),
                BarConfigManager.GetRelationLabel(BarConfigManager.BarRelationName.BelowFull));
            yield return new UIDropdown.Option(
                GetRelationDropdownId(BarConfigManager.BarRelationName.Empty),
                BarConfigManager.GetRelationLabel(BarConfigManager.BarRelationName.Empty));
            yield return new UIDropdown.Option(
                GetRelationDropdownId(BarConfigManager.BarRelationName.Change),
                BarConfigManager.GetRelationLabel(BarConfigManager.BarRelationName.Change));

            if (dependentTarget.BarType == sourceTarget.BarType)
            {
                yield return new UIDropdown.Option(
                    GetRelationDropdownId(BarConfigManager.BarRelationName.Always),
                    BarConfigManager.GetRelationLabel(BarConfigManager.BarRelationName.Always));
            }
        }

        private static Rectangle GetRelationDropdownBounds(Point pointer, int optionCount)
        {
            int x = pointer.X + 10;
            int y = pointer.Y - (RelationDropdownH / 2);

            if (_cachedListBounds != Rectangle.Empty)
            {
                int optionHeight = Math.Max(24, (int)MathF.Ceiling(UIStyle.FontBody.IsAvailable ? UIStyle.FontBody.LineHeight + 8f : 24f));
                int listHeight = optionHeight * Math.Max(1, optionCount);
                int maxX = Math.Max(_cachedListBounds.X, _cachedListBounds.Right - RelationDropdownW);
                int maxY = Math.Max(_cachedListBounds.Y, _cachedListBounds.Bottom - RelationDropdownH - 4 - listHeight);
                x = Math.Clamp(x, _cachedListBounds.X, maxX);
                y = Math.Clamp(y, _cachedListBounds.Y, maxY);
            }

            return new Rectangle(x, y, RelationDropdownW, RelationDropdownH);
        }

        private static void ApplyPendingRelationSelection(string selectionChangedId)
        {
            if (!_relationDropdownState.Active ||
                string.IsNullOrWhiteSpace(selectionChangedId) ||
                !TryGetLocalEntry(_relationDropdownState.DependentTarget.BarRow, _relationDropdownState.DependentTarget.BarType, out BarConfigManager.BarEntry entry))
            {
                return;
            }

            BarConfigManager.AddVisibilityRelation(
                entry,
                _relationDropdownState.SourceTarget.BarType,
                GetRelationFromDropdownId(selectionChangedId));
        }

        private static string GetRelationDropdownId(BarConfigManager.BarRelationName relationName) => relationName switch
        {
            BarConfigManager.BarRelationName.BelowFull => "not_full",
            BarConfigManager.BarRelationName.Empty => "empty",
            BarConfigManager.BarRelationName.Change => "changed",
            BarConfigManager.BarRelationName.Always => "always",
            _ => relationName.ToString()
        };

        private static BarConfigManager.BarRelationName GetRelationFromDropdownId(string relationId) => relationId switch
        {
            "not_full" => BarConfigManager.BarRelationName.BelowFull,
            "empty" => BarConfigManager.BarRelationName.Empty,
            "changed" => BarConfigManager.BarRelationName.Change,
            "always" => BarConfigManager.BarRelationName.Always,
            _ => BarConfigManager.BarRelationName.BelowFull
        };

        private static void ResetRelationUi()
        {
            _relationDrag = default;
            _relationDropdownState = default;
            _relationDropdown.Close();
            _relationDropdown.Bounds = Rectangle.Empty;
            _relationRemoveButtons.Clear();
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

        private static void UpdateSimChangeTracking()
        {
            TrackSimBarChange(BarType.Health, _simHealth);
            TrackSimBarChange(BarType.Shield, _simShield);
            TrackSimBarChange(BarType.XP, _simXP);
            TrackSimBarChange(BarType.HealthRegen, GetHealthRegenPreviewValue());
            TrackSimBarChange(BarType.ShieldRegen, GetShieldRegenPreviewValue());
        }

        private static void TrackSimBarChange(BarType type, float value)
        {
            if (!_simPrevBarValues.TryGetValue(type, out float previousValue) ||
                MathF.Abs(previousValue - value) > PreviewChangeEpsilon)
            {
                _simLastChangeTimes[type] = _simTime;
            }

            _simPrevBarValues[type] = value;
        }

        private static bool TryGetSimBarValue(BarType type, out float current, out float max)
        {
            current = 0f;
            max = 0f;

            switch (type)
            {
                case BarType.Health:
                    current = _simHealth;
                    max = _simMaxHealth;
                    return max > 0f;

                case BarType.Shield:
                    current = _simShield;
                    max = _simMaxShield;
                    return max > 0f;

                case BarType.XP:
                    current = _simXP;
                    max = _simMaxXP;
                    return max > 0f;

                case BarType.HealthRegen:
                    if (_simHealthRegen <= 0f || _simHealthRegenDelay <= 0f)
                    {
                        return false;
                    }

                    current = GetHealthRegenPreviewValue();
                    max = _simHealthRegenDelay;
                    return max > 0f;

                case BarType.ShieldRegen:
                    if (_simShieldRegen <= 0f || _simShieldRegenDelay <= 0f)
                    {
                        return false;
                    }

                    current = GetShieldRegenPreviewValue();
                    max = _simShieldRegenDelay;
                    return max > 0f;

                default:
                    return false;
            }
        }

        private static float GetHealthRegenPreviewValue()
        {
            if (_simHealthRegenDelay <= 0f)
            {
                return 0f;
            }

            return MathHelper.Clamp(_simTime - _simLastHealthDmgTime, 0f, _simHealthRegenDelay);
        }

        private static float GetShieldRegenPreviewValue()
        {
            if (_simShieldRegenDelay <= 0f)
            {
                return 0f;
            }

            return MathHelper.Clamp(_simTime - _simLastShieldDmgTime, 0f, _simShieldRegenDelay);
        }

        private static BarConfigManager.BarSourceState GetSimBarSourceState(BarType type)
        {
            bool isKnown = _simPrevBarValues.ContainsKey(type);
            bool changedRecently = _simLastChangeTimes.TryGetValue(type, out float changeTime) &&
                                   _simTime - changeTime <= PreviewChangeVisibleSeconds;

            if (!TryGetSimBarValue(type, out float current, out float max))
            {
                return new BarConfigManager.BarSourceState(0f, 0f, changedRecently, isKnown);
            }

            return new BarConfigManager.BarSourceState(current, max, changedRecently, isKnown);
        }

        private static bool ShouldPreviewEntryRender(BarConfigManager.BarEntry entry)
        {
            return entry != null &&
                   !entry.IsHidden &&
                   TryGetSimBarValue(entry.Type, out _, out _) &&
                   BarConfigManager.AreVisibilityRelationsActive(entry, GetSimBarSourceState);
        }

        private static void UpdateOverlayTargets(Point mouse)
        {
            _segmentTarget = TryResolveOverlayTarget(mouse, ColSegments, _segmentTarget, _segmentOverlayRect, out BarOverlayTarget segmentTarget)
                ? segmentTarget
                : default;
            _segmentOverlayRect = _segmentTarget.Active ? GetSegmentOverlayRect(_segmentTarget) : Rectangle.Empty;

            if (_relationDrag.Active || _relationDropdownState.Active)
            {
                _relationTarget = default;
                _relationOverlayRect = Rectangle.Empty;
                return;
            }

            _relationTarget = _listFunCol?.HoveredColumn == ColRelations &&
                              TryGetBarEntryAtPoint(mouse, out BarOverlayTarget relationTarget)
                ? relationTarget
                : default;
            _relationOverlayRect = Rectangle.Empty;
        }

        private static bool TryResolveOverlayTarget(
            Point mouse,
            int overlayColumn,
            BarOverlayTarget existingTarget,
            Rectangle existingOverlayRect,
            out BarOverlayTarget target)
        {
            target = default;

            if (_listFunCol?.HoveredColumn == overlayColumn && TryGetBarEntryAtPoint(mouse, out target))
            {
                return true;
            }

            if (existingTarget.Active && existingOverlayRect.Contains(mouse))
            {
                target = existingTarget;
                return true;
            }

            return false;
        }

        private static bool TryGetBarEntryAtPoint(Point point, out BarOverlayTarget target)
        {
            foreach (BarRowBounds row in _rowBounds)
            {
                foreach ((BarType type, Rectangle bounds) in row.Entries)
                {
                    if (!bounds.Contains(point))
                    {
                        continue;
                    }

                    target = new BarOverlayTarget
                    {
                        Active = true,
                        BarRow = row.BarRow,
                        BarType = type,
                        RowRect = row.RowRect,
                        EntryRect = bounds
                    };
                    return true;
                }
            }

            target = default;
            return false;
        }

        private static Rectangle GetSegmentOverlayRect(BarOverlayTarget target)
            => GetOverlayRect(target.RowRect, SegmentPanelH);

        private static Rectangle GetRelationOverlayRect(BarOverlayTarget target)
        {
            _ = target;
            return Rectangle.Empty;
        }

        private static int GetRelationOverlayHeight(BarConfigManager.BarEntry entry)
        {
            _ = entry;
            return 0;
        }

        private static bool TryGetRelationRemoveButtonRect(Rectangle panel, int relationIndex, out Rectangle buttonRect)
        {
            _ = panel;
            _ = relationIndex;
            buttonRect = Rectangle.Empty;
            return false;
        }

        private static Rectangle GetRelationClearButtonRect(Rectangle entryRect)
        {
            if (entryRect == Rectangle.Empty)
            {
                return Rectangle.Empty;
            }

            int availableWidth = Math.Max(18, entryRect.Width - BorderAccentW - 8);
            int buttonWidth = Math.Max(RelationClearBtnMinW, entryRect.Width / 3);
            buttonWidth = Math.Min(buttonWidth, Math.Min(RelationClearBtnMaxW, availableWidth));
            buttonWidth = Math.Max(18, buttonWidth);

            int x = Math.Max(entryRect.X + BorderAccentW + 2, entryRect.Right - buttonWidth - 4);
            int y = entryRect.Y + ((entryRect.Height - RelationClearBtnH) / 2);
            return new Rectangle(x, y, buttonWidth, RelationClearBtnH);
        }

        private static bool TryGetRelationClearButtonAtPoint(Point point, out BarType barType, out int barRow)
        {
            foreach ((BarType type, int row, Rectangle rect) in _clearRelationBtnRects)
            {
                if (!rect.Contains(point))
                {
                    continue;
                }

                barType = type;
                barRow = row;
                return true;
            }

            barType = default;
            barRow = default;
            return false;
        }

        private static Rectangle GetOverlayRect(Rectangle rowRect, int overlayHeight)
        {
            if (rowRect == Rectangle.Empty || overlayHeight <= 0)
            {
                return Rectangle.Empty;
            }

            int y = rowRect.Bottom;
            if (_cachedListBounds != Rectangle.Empty && y + overlayHeight > _cachedListBounds.Bottom)
            {
                y = Math.Max(_cachedListBounds.Y, rowRect.Y - overlayHeight);
            }

            return new Rectangle(rowRect.X, y, rowRect.Width, overlayHeight);
        }

        private static bool TryGetLocalEntry(int barRow, BarType type, out BarConfigManager.BarEntry entry)
        {
            entry = _local.FirstOrDefault(e => e.BarRow == barRow && e.Type == type);
            return entry != null;
        }

        private static void DoApply()
        {
            ResetRelationUi();
            BarConfigManager.ApplyGroup(_selectedGroupKey, _local);
            RefreshCleanDescendantGroups(_selectedGroupKey);
        }

        private static void DoSave()
        {
            ResetRelationUi();
            BarConfigManager.Save(_selectedGroupKey, _local);
            _snapshot            = CloneBarEntries(_local);
            _snapshotByGroup[_selectedGroupKey] = _snapshot;
            _local               = CloneBarEntries(_local);
            _localByGroup[_selectedGroupKey] = _local;
            _snapshotBarsVisible = BarConfigManager.BarsVisible;
            RefreshCleanDescendantGroups(_selectedGroupKey);
        }

        private static void DoDiscard()
        {
            ResetRelationUi();
            _local = CloneBarEntries(_snapshot);
            _localByGroup[_selectedGroupKey] = _local;
            BarConfigManager.BarsVisible = _snapshotBarsVisible;
            BarConfigManager.ApplyGroup(_selectedGroupKey, _local);
            RefreshCleanDescendantGroups(_selectedGroupKey);
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
            BarType.HealthRegen => ColorPalette.BarRegenTick,
            BarType.ShieldRegen => ColorPalette.ShieldRegenTick,
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
