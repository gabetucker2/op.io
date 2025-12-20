using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.Blocks;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io
{
    public static class BlockManager
    {
        private const string GameBlockKey = "game";
        private const string BlankBlockKey = "blank";
        private const string TransparentBlockKey = "transparent";
        private const string PropertiesBlockKey = "properties";
        private const string ColorSchemeBlockKey = "colors";
        private const string ControlsBlockKey = "controls";
        private const string NotesBlockKey = "notes";
        private const string DockingSetupsBlockKey = "dockingsetups";
        private const string BackendBlockKey = "backend";
        private const string SpecsBlockKey = "specs";
        private const string BlockMenuControlKey = "BlockMenu";
        private const string AllowGameInputFreezeKey = "AllowGameInputFreeze";
        private const string DockingSetupActiveRowKey = "__ActiveSetup";
        private const int DragBarButtonPadding = 8;
        private const int DragBarButtonSpacing = 6;
        private const int WindowEdgeSnapDistance = 30;
        private const int GroupBarHeight = 26;
        private const int TabMinWidth = 72;
        private const int TabHorizontalPadding = 12;
        private const int TabSpacing = 4;
        private const int TabVerticalPadding = 3;
        private const int TabCloseSize = 12;
        private const int TabLockSize = 18;
        private const int TabUngroupSize = 18;
        private const int TabClosePadding = 8;
        private const int GroupBarDragGap = 24;
        private const int TabDragStartThreshold = 6;
        private const string LockedIconFile = "Icon_Locked.png";
        private const string UnlockedIconFile = "Icon_Unlocked.png";
        private const double BlankBlockFadeInDurationSeconds = 0.35d;
        private const double BlankBlockFadeOutDurationSeconds = 0.22d;
        private const double BlankBlockInterruptedFadeOutScale = 0.6d;
        private const double BlankBlockMinimumFadeDurationSeconds = 0.015d;

        private static readonly Color GroupBarBackground = new(28, 28, 30);
        private static readonly Color TabInactiveBackground = new(40, 40, 42);
        private static readonly Color TabHoverBackground = new(50, 50, 54);
        private static readonly Color TabActiveBackground = new(60, 60, 64);
        private static readonly Color TabCloseHoverTint = new Color(255, 255, 255, 22);

        private static bool _dockingModeEnabled = false;
        private static bool _blockDefinitionsReady;
        private static bool _renderingDockedFrame;
        private const int CornerSnapDistance = 16;
        private static DockingSetupDefinition _pendingDockingSetup;
        private static readonly Dictionary<string, DockBlock> _blocks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BlockNode> _blockNodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PanelGroup> _panelGroups = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BlockNode> _panelNodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _blockToPanel = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BlankBlockHoverState> _blankBlockHoverStates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DockBlock> _orderedBlocks = [];
        private static readonly List<string> _orderedPanelIds = new();
        private static readonly Dictionary<string, bool> _blockLockStates = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _lastNonDockingActiveByPanel = new(StringComparer.OrdinalIgnoreCase);
        private static DockNode _rootNode;
        private static Rectangle _cachedViewportBounds;
        private static Rectangle _layoutBounds;
        private static Rectangle _gameContentBounds;
        private static bool _layoutDirty = true;
        private static Viewport _previousViewport;
        private static bool _viewportPushed;
        private static RenderTarget2D _worldRenderTarget;
        private static Texture2D _pixelTexture;
        private static Texture2D _lockedIcon;
        private static Texture2D _unlockedIcon;
        private static MouseState _previousMouseState;
        private static Point _mousePosition;
        private static DockBlock _draggingBlock;
        private static PanelGroup _draggingPanel;
        private static Rectangle _draggingStartBounds;
        private static Point _dragOffset;
        private static DockDropPreview? _dropPreview;
        private static bool _overlayMenuVisible;
        private static bool _blockMenuSwitchState;
        private static bool _allowTransparentBlockClickThrough;
        private static bool _panelInteractionLockActive;
        private static bool _isWindowClickThroughActive;
        private static Rectangle _overlayBounds;
        private static Rectangle _overlayDismissBounds;
        private static Rectangle _overlayOpenAllBounds;
        private static Rectangle _overlayCloseAllBounds;
        private static readonly List<ResizeEdge> _resizeEdges = [];
        private static ResizeEdge? _hoveredResizeEdge;
        private static ResizeEdge? _activeResizeEdge;
        private static ResizeEdge? _activeResizeEdgeSnapTarget;
        private static int? _activeResizeEdgeSnapCoordinate;
        private static readonly List<CornerHandle> _cornerHandles = [];
        private static CornerHandle? _hoveredCornerHandle;
        private static CornerHandle? _activeCornerHandle;
        private static CornerHandle? _activeCornerLinkedHandle;
        private static CornerHandle? _activeCornerSnapTarget;
        private static bool _isPropagatingResize;
        private static Point? _activeCornerSnapPosition;
        private static Point? _activeCornerSnapAnchor;
        private static bool _activeCornerSnapLockX;
        private static bool _activeCornerSnapLockY;
        private static bool _blockMenuDirty;
        private static string _focusedBlockId;
        private static string _hoveredDragBarId;
        private static KeyboardState _previousKeyboardState;
        private static readonly List<BlockMenuEntry> _blockMenuEntries = [];
        private static readonly List<OverlayMenuRow> _overlayRows = [];
        private static BlockMenuEntry _activeNumericEntry;
        private static readonly KeyRepeatTracker OverlayInputRepeater = new();
        private static Dictionary<string, Rectangle> _resizeStartBlockBounds;
        private static readonly Dictionary<string, PanelGroupBarLayout> _groupBarLayoutCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _loggedLayoutIssues = new(StringComparer.OrdinalIgnoreCase);
        private static string _pressedTabBlockId;
        private static string _pressedTabPanelId;
        private static Point _tabPressPosition;
        private static bool _draggingFromTab;
        private static readonly JsonSerializerOptions DockingSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static bool IsBlockLocked(DockBlockKind blockKind)
        {
            DockBlock block = _orderedBlocks.FirstOrDefault(p => p.Kind == blockKind && p.IsVisible) ??
                _orderedBlocks.FirstOrDefault(p => p.Kind == blockKind);
            return IsBlockLocked(block);
        }

        public static bool DockingModeEnabled
        {
            get => _dockingModeEnabled;
            set
            {
                bool dockingChanged = _dockingModeEnabled != value;
                _dockingModeEnabled = value;
                ScreenManager.ApplyDockingWindowChrome(Core.Instance, _dockingModeEnabled);
                DockingDiagnostics.RecordBlockToggle(
                    "BlockManager.DockingModeEnabled",
                    _dockingModeEnabled,
                    note: $"changed={dockingChanged}");

                if (!dockingChanged)
                {
                    return;
                }

                if (!_dockingModeEnabled)
                {
                    CollapseInteractions();
                    ClearResizeEdges();
                    MarkLayoutDirty();
                }
                else
                {
                    EnsureBlocks();
                    MarkLayoutDirty();
                }

                UpdateTransparentBlockClickThrough();
            }
        }

        public static void OnGraphicsReady()
        {
            EnsureBlocks();
            MarkLayoutDirty();
            EnsureSurfaceResources(Core.Instance?.GraphicsDevice);
        }

        public static void SetTransparentBlockClickThroughAllowed(bool allowClickThrough)
        {
            _allowTransparentBlockClickThrough = allowClickThrough;
            UpdateTransparentBlockClickThrough();
        }

        public static void Update(GameTime gameTime)
        {
            if (Core.Instance?.GraphicsDevice == null)
            {
                _previousMouseState = Mouse.GetState();
                return;
            }

            EnsureBlocks();
            EnsureFocusedBlockValid();
            UpdateLayoutCache();
            EnsureSurfaceResources(Core.Instance.GraphicsDevice);
            double elapsedSeconds = Math.Max(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d, 0d);

            MouseState mouseState = Mouse.GetState();
            KeyboardState keyboardState = Keyboard.GetState();
            _mousePosition = mouseState.Position;
            bool dockingEnabled = DockingModeEnabled;
            bool rebindOverlayOpen = ControlsBlock.IsRebindOverlayOpen();
            bool panelInteractionsLocked = ShouldLockPanelInteractions();
            bool lockStateChanged = panelInteractionsLocked != _panelInteractionLockActive;
            _panelInteractionLockActive = panelInteractionsLocked;
            if (_panelInteractionLockActive)
            {
                if (lockStateChanged)
                {
                    ClearDockingInteractions();
                }

                _overlayMenuVisible = false;
            }

            bool blockMenuState = rebindOverlayOpen ? false : GetBlockMenuState();
            _blockMenuSwitchState = blockMenuState;
            _overlayMenuVisible = !_panelInteractionLockActive && blockMenuState;

            if (!_overlayMenuVisible && !rebindOverlayOpen)
            {
                ResetOverlayLayout();
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickHeld = mouseState.LeftButton == ButtonState.Pressed;
            bool allowReorder = dockingEnabled;
            RebuildGroupBarLayoutCache(GetActiveDragBarHeight());

            if (rebindOverlayOpen && !_panelInteractionLockActive)
            {
                ClearDockingInteractions();
            }
            else if (_overlayMenuVisible)
            {
                UpdateOverlayKeyboardInput(keyboardState, elapsedSeconds);
                UpdateOverlayInteractions(leftClickStarted);
                ClearDockingInteractions();
            }
            else if (!_panelInteractionLockActive)
            {
                bool tabInteracted = UpdateTabInteractions(leftClickStarted, leftClickHeld, leftClickReleased, allowReorder);
                if (dockingEnabled)
                {
                    // Evaluate corners first so clicks on intersections start a dual-axis drag instead of being swallowed by a single edge.
                    bool resizingBlocks = !tabInteracted && allowReorder && (UpdateCornerResizeState(leftClickStarted, leftClickHeld, leftClickReleased) ||
                        UpdateResizeEdgeState(leftClickStarted, leftClickHeld, leftClickReleased));
                    if (!resizingBlocks && !tabInteracted)
                    {
                        UpdateDragState(leftClickStarted, leftClickReleased, allowReorder);
                    }
                    else
                    {
                        _draggingBlock = null;
                        _dropPreview = null;
                    }
                }
                else
                {
                    ClearDockingDragState();
                }
            }
            else
            {
                ClearDockingDragState();
            }

            UpdateInteractiveBlocks(gameTime, mouseState, _previousMouseState, keyboardState, _previousKeyboardState);
            ApplyPendingDockingSetup();
            UpdateTransparentBlockClickThrough();
            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        public static bool BeginDockedFrame(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                _renderingDockedFrame = false;
                return false;
            }

            EnsureBlocks();
            UpdateLayoutCache();
            EnsureSurfaceResources(graphicsDevice);

            bool readyForRenderTarget =
                _worldRenderTarget != null &&
                TryGetGameBlock(out DockBlock gameBlock) &&
                gameBlock.IsVisible &&
                _gameContentBounds.Width > 0 &&
                _gameContentBounds.Height > 0;

            if (readyForRenderTarget)
            {
                _previousViewport = graphicsDevice.Viewport;
                graphicsDevice.SetRenderTarget(_worldRenderTarget);
                graphicsDevice.Viewport = new Viewport(0, 0, _worldRenderTarget.Width, _worldRenderTarget.Height);
                _viewportPushed = true;
                _renderingDockedFrame = true;
            }
            else
            {
                _viewportPushed = false;
                _renderingDockedFrame = false;
            }

            return true;
        }

        private static void UpdateTransparentBlockClickThrough()
        {
            bool shouldAllowClickThrough =
                _allowTransparentBlockClickThrough &&
                !_dockingModeEnabled &&
                IsPointerOverTransparentBlock();

            if (shouldAllowClickThrough == _isWindowClickThroughActive)
            {
                return;
            }

            _isWindowClickThroughActive = shouldAllowClickThrough;
            GameInitializer.SetWindowClickThrough(shouldAllowClickThrough);
        }

        private static bool IsPointerOverTransparentBlock()
        {
            if (_orderedBlocks.Count == 0)
            {
                return false;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !block.IsVisible || block.Kind != DockBlockKind.Transparent)
                {
                    continue;
                }

                if (block.Bounds.Contains(_mousePosition))
                {
                    return true;
                }
            }

            return false;
        }

        public static void CompleteDockedFrame(SpriteBatch spriteBatch)
        {
            if (Core.Instance?.GraphicsDevice == null)
            {
                return;
            }

            if (_renderingDockedFrame)
            {
                Core.Instance.GraphicsDevice.SetRenderTarget(null);
                if (_viewportPushed)
                {
                    Core.Instance.GraphicsDevice.Viewport = _previousViewport;
                    _viewportPushed = false;
                }
            }

            UpdateLayoutCache();
            EnsureSurfaceResources(Core.Instance.GraphicsDevice);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            Rectangle viewport = Core.Instance.GraphicsDevice.Viewport.Bounds;
            DrawRect(spriteBatch, viewport, Core.TransparentWindowColor);

            if (!AnyBlockVisible())
            {
                DrawEmptyState(spriteBatch, viewport);
            }
            else
            {
                DrawBlocks(spriteBatch);
            }

            ColorSchemeBlock.DrawOverlay(spriteBatch, _layoutBounds);
            DrawOverlayMenu(spriteBatch);
            ControlsBlock.DrawRebindOverlay(spriteBatch);
            spriteBatch.End();

            _renderingDockedFrame = false;
        }

        private static void EnsureBlocks()
        {
            EnsureBlockMenuEntries();
            if (_blockDefinitionsReady)
            {
                return;
            }

            DockingSetupDefinition setup = LoadDockingSetupFromDatabase();
            if (setup != null)
            {
                if (_blockMenuDirty)
                {
                    ApplyBlockMenuOverrides(setup);
                }

                if (ApplyDockingSetupDefinition(setup))
                {
                    _blockMenuDirty = false;
                    return;
                }
            }

            ResetBlockState();
            CreateBlocksFromMenuEntries();
            GroupCountedBlocks();
            GroupDefaultPanels();
            _rootNode = BuildDefaultLayout();

            _blockDefinitionsReady = true;
            _blockMenuDirty = false;
            MarkLayoutDirty();
        }

        private static void ResetBlockState()
        {
            _blocks.Clear();
            _blockNodes.Clear();
            _panelGroups.Clear();
            _panelNodes.Clear();
            _blockToPanel.Clear();
            _blankBlockHoverStates.Clear();
            _orderedBlocks.Clear();
            _orderedPanelIds.Clear();
            _blockLockStates.Clear();
            _lastNonDockingActiveByPanel.Clear();
            ClearDockingInteractions();
        }

        private static void CreateBlocksFromMenuEntries()
        {
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                if (entry.ControlMode == BlockMenuControlMode.Toggle)
                {
                    DockBlock block = CreateBlock(entry.IdPrefix, entry.Label, entry.Kind);
                    block.IsVisible = entry.IsVisible;
                }
                else
                {
                    entry.Count = ClampCount(entry, entry.Count);
                    entry.InputBuffer = entry.Count.ToString();
                    if (entry.Count == 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < entry.Count; i++)
                    {
                        string blockId = BuildBlockId(entry.IdPrefix, i);
                        string title = BuildBlockTitle(entry, i);
                        CreateBlock(blockId, title, entry.Kind);
                    }
                }
            }
        }

        internal static string CaptureDockingSetup()
        {
            DockingSetupDefinition setup = BuildDockingSetupDefinition();
            return SerializeDockingSetup(setup);
        }

        internal static bool TryApplyDockingSetup(string payload)
        {
            DockingSetupDefinition setup = DeserializeDockingSetup(payload);
            if (setup == null)
            {
                return false;
            }

            ApplyDockingSetupDefinition(setup);
            return true;
        }

        internal static bool QueueDockingSetupApply(string payload)
        {
            DockingSetupDefinition setup = DeserializeDockingSetup(payload);
            if (setup == null)
            {
                return false;
            }

            _pendingDockingSetup = setup;
            return true;
        }

        private static void ApplyPendingDockingSetup()
        {
            if (_pendingDockingSetup == null)
            {
                return;
            }

            DockingSetupDefinition setup = _pendingDockingSetup;
            _pendingDockingSetup = null;
            ApplyDockingSetupDefinition(setup);
        }

        private static DockingSetupDefinition BuildDockingSetupDefinition()
        {
            EnsureBlockMenuEntries();
            List<DockingSetupPanelGroup> panelGroups = BuildPanelGroupDefinitions();
            DockingSetupDefinition setup = new()
            {
                Version = 3,
                Panels = panelGroups,
                GroupBars = panelGroups.ToList()
            };

            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                DockingSetupMenuEntry menuEntry = new()
                {
                    Kind = entry.Kind.ToString(),
                    Mode = entry.ControlMode.ToString(),
                    Count = entry.ControlMode == BlockMenuControlMode.Count ? entry.Count : 0,
                    Visible = entry.ControlMode == BlockMenuControlMode.Toggle && entry.IsVisible
                };

                setup.Menu.Add(menuEntry);
            }

            setup.Layout = BuildLayoutDefinition(_rootNode);
            CaptureDockingSetupLocks(setup);
            return setup;
        }

        private static List<DockingSetupPanelGroup> BuildPanelGroupDefinitions()
        {
            List<DockingSetupPanelGroup> panelGroups = new();

            foreach (PanelGroup group in EnumeratePanelGroupsInOrder())
            {
                if (group?.Blocks == null || group.Blocks.Count == 0)
                {
                    continue;
                }

                string activeId = ResolveActiveBlockId(group);
                DockingSetupPanelGroup panelGroup = new()
                {
                    Id = group.PanelId,
                    Active = activeId
                };

                foreach (DockBlock block in group.Blocks)
                {
                    if (block == null || string.IsNullOrWhiteSpace(block.Id))
                    {
                        continue;
                    }

                    panelGroup.Blocks.Add(block.Id);
                }

                if (panelGroup.Blocks.Count == 0)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(panelGroup.Active))
                {
                    panelGroup.Active = panelGroup.Blocks[0];
                }

                panelGroups.Add(panelGroup);
            }

            return panelGroups;
        }

        private static string ResolveActiveBlockId(PanelGroup group)
        {
            if (group == null)
            {
                return null;
            }

            string activeId = group.ActiveBlockId;
            if (!string.IsNullOrWhiteSpace(activeId) &&
                _blocks.TryGetValue(activeId, out DockBlock activeBlock) &&
                activeBlock != null &&
                group.Blocks.Any(b => string.Equals(b.Id, activeId, StringComparison.OrdinalIgnoreCase)))
            {
                return activeId;
            }

            string remembered = GetLastNonDockingActive(group);
            if (!string.IsNullOrWhiteSpace(remembered))
            {
                return remembered;
            }

            DockBlock firstNonDocking = group.Blocks.FirstOrDefault(b => b != null && b.Kind != DockBlockKind.DockingSetups);
            if (firstNonDocking != null)
            {
                return firstNonDocking.Id;
            }

            if (!string.IsNullOrWhiteSpace(activeId))
            {
                return activeId;
            }

            return group.ActiveBlock?.Id ?? group.Blocks.FirstOrDefault()?.Id;
        }

        private static string GetLastNonDockingActive(PanelGroup group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.PanelId))
            {
                return null;
            }

            if (_lastNonDockingActiveByPanel.TryGetValue(group.PanelId, out string stored) &&
                _blocks.TryGetValue(stored, out DockBlock storedBlock) &&
                storedBlock != null &&
                storedBlock.Kind != DockBlockKind.DockingSetups &&
                group.Blocks.Any(b => string.Equals(b.Id, stored, StringComparison.OrdinalIgnoreCase)))
            {
                return stored;
            }

            return null;
        }

        private static IEnumerable<PanelGroup> EnumeratePanelGroupsInOrder()
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (string panelId in _orderedPanelIds)
            {
                if (!_panelGroups.TryGetValue(panelId, out PanelGroup group) || group == null || !seen.Add(group.PanelId))
                {
                    continue;
                }

                yield return group;
            }

            foreach (PanelGroup group in _panelGroups.Values)
            {
                if (group == null || !seen.Add(group.PanelId))
                {
                    continue;
                }

                yield return group;
            }
        }

        private static IEnumerable<DockingSetupPanelGroup> GetPanelGroupsFromDefinition(DockingSetupDefinition setup)
        {
            if (setup == null)
            {
                return Enumerable.Empty<DockingSetupPanelGroup>();
            }

            if (setup.GroupBars != null && setup.GroupBars.Count > 0)
            {
                return setup.GroupBars;
            }

            return setup.Panels ?? Enumerable.Empty<DockingSetupPanelGroup>();
        }

        private static HashSet<string> GetBlocksDefinedInSetup(DockingSetupDefinition setup)
        {
            HashSet<string> defined = new(StringComparer.OrdinalIgnoreCase);
            foreach (DockingSetupPanelGroup group in GetPanelGroupsFromDefinition(setup))
            {
                if (group == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(group.Active))
                {
                    defined.Add(group.Active);
                }

                if (group.Blocks == null)
                {
                    continue;
                }

                foreach (string blockId in group.Blocks)
                {
                    if (!string.IsNullOrWhiteSpace(blockId))
                    {
                        defined.Add(blockId);
                    }
                }
            }

            return defined;
        }

        private static void CaptureDockingSetupLocks(DockingSetupDefinition setup)
        {
            if (setup == null)
            {
                return;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || string.IsNullOrWhiteSpace(block.Id))
                {
                    continue;
                }

                setup.BlockLocks[block.Id] = IsBlockLockEnabled(block);
            }

            foreach (PanelGroup group in _panelGroups.Values)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.PanelId))
                {
                    continue;
                }

                setup.PanelLocks[group.PanelId] = group.IsLocked;
            }
        }

        private static DockingSetupLayoutNode BuildLayoutDefinition(DockNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is BlockNode blockNode)
            {
                string panelId = null;
                if (blockNode.Block != null && _blockToPanel.TryGetValue(blockNode.Block.Id, out string mappedId))
                {
                    panelId = mappedId;
                }
                else
                {
                    panelId = blockNode.Block?.Id;
                }

                if (string.IsNullOrWhiteSpace(panelId))
                {
                    return null;
                }

                return new DockingSetupLayoutNode
                {
                    Type = "Panel",
                    PanelId = panelId
                };
            }

            if (node is SplitNode split)
            {
                DockingSetupLayoutNode first = BuildLayoutDefinition(split.First);
                DockingSetupLayoutNode second = BuildLayoutDefinition(split.Second);
                if (first == null)
                {
                    return second;
                }

                if (second == null)
                {
                    return first;
                }

                float ratio = MathHelper.Clamp(split.SplitRatio, 0.001f, 0.999f);

                return new DockingSetupLayoutNode
                {
                    Type = "Split",
                    Orientation = split.Orientation.ToString(),
                    Ratio = ratio,
                    First = first,
                    Second = second
                };
            }

            return null;
        }

        private static void ApplyBlockMenuOverrides(DockingSetupDefinition setup)
        {
            if (setup == null)
            {
                return;
            }

            setup.Menu ??= new List<DockingSetupMenuEntry>();

            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                DockingSetupMenuEntry stored = setup.Menu.FirstOrDefault(menuEntry =>
                    menuEntry != null &&
                    Enum.TryParse(menuEntry.Kind, true, out DockBlockKind kind) &&
                    kind == entry.Kind);

                if (stored == null)
                {
                    stored = new DockingSetupMenuEntry
                    {
                        Kind = entry.Kind.ToString(),
                        Mode = entry.ControlMode.ToString()
                    };
                    setup.Menu.Add(stored);
                }

                if (entry.ControlMode == BlockMenuControlMode.Count)
                {
                    stored.Count = entry.Count;
                }
                else
                {
                    stored.Visible = entry.IsVisible;
                }
            }
        }

        private static bool ApplyDockingSetupDefinition(DockingSetupDefinition setup)
        {
            if (setup == null)
            {
                return false;
            }

            EnsureBlockMenuEntries();
            ResetBlockState();
            ApplyDockingSetupMenuState(setup.Menu);
            CreateBlocksFromMenuEntries();
            HashSet<string> definedBlocks = GetBlocksDefinedInSetup(setup);
            GroupCountedBlocks(definedBlocks);
            ApplyDockingSetupPanelGroups(GetPanelGroupsFromDefinition(setup));
            ApplyDockingSetupLocks(setup);

            HashSet<string> visiblePanels = new(StringComparer.OrdinalIgnoreCase);
            _rootNode = BuildLayoutFromDefinition(setup.Layout, visiblePanels);
            if (_rootNode == null)
            {
                DebugLogger.PrintError("[DockLayout] Failed to build layout from saved definition; rebuilding from available panels.");
                _rootNode = BuildLayoutFromPanels(visiblePanels);
            }

            HidePanelsNotInLayout(visiblePanels);
            ApplyToggleVisibilityOverrides();
            PruneInactiveBlockNodes();

            _blockDefinitionsReady = true;
            MarkLayoutDirty();
            return true;
        }

        private static void ApplyDockingSetupMenuState(IEnumerable<DockingSetupMenuEntry> menuEntries)
        {
            if (menuEntries == null)
            {
                return;
            }

            foreach (DockingSetupMenuEntry stored in menuEntries)
            {
                if (stored == null || string.IsNullOrWhiteSpace(stored.Kind))
                {
                    continue;
                }

                if (!Enum.TryParse(stored.Kind, true, out DockBlockKind kind))
                {
                    continue;
                }

                BlockMenuEntry entry = _blockMenuEntries.FirstOrDefault(e => e.Kind == kind);
                if (entry == null)
                {
                    continue;
                }

                if (entry.ControlMode == BlockMenuControlMode.Count)
                {
                    entry.Count = ClampCount(entry, stored.Count);
                    entry.InputBuffer = entry.Count.ToString();
                }
                else
                {
                    entry.IsVisible = stored.Visible;
                }
            }
        }

        private static void ApplyDockingSetupPanelGroups(IEnumerable<DockingSetupPanelGroup> groups)
        {
            if (groups == null)
            {
                return;
            }

            foreach (DockingSetupPanelGroup group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                List<string> orderedBlocks = group.Blocks?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList() ?? new List<string>();

                string declaredActiveId = !string.IsNullOrWhiteSpace(group.Active) ? group.Active : group.Id;
                string activeId = ResolveDefinitionActiveBlockId(group.Id, declaredActiveId, orderedBlocks);
                if (string.IsNullOrWhiteSpace(activeId))
                {
                    continue;
                }

                if (!orderedBlocks.Any(id => string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase)))
                {
                    orderedBlocks.Insert(0, activeId);
                }

                if (!_blocks.TryGetValue(activeId, out DockBlock activeBlock))
                {
                    continue;
                }

                PanelGroup targetGroup = GetPanelGroupForBlock(activeBlock);
                if (targetGroup == null)
                {
                    continue;
                }

                for (int i = 0; i < orderedBlocks.Count; i++)
                {
                    string blockId = orderedBlocks[i];
                    if (!_blocks.TryGetValue(blockId, out DockBlock block) || block == null)
                    {
                        continue;
                    }

                    PanelGroup sourceGroup = GetPanelGroupForBlock(block);
                    if (sourceGroup != null && !ReferenceEquals(sourceGroup, targetGroup))
                    {
                        sourceGroup.RemoveBlock(block.Id, out _);
                        _blockToPanel.Remove(block.Id);

                        if (sourceGroup.Blocks.Count == 0)
                        {
                            RemovePanelGroup(sourceGroup);
                        }
                        else if (string.Equals(sourceGroup.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            DockBlock next = sourceGroup.ActiveBlock ?? sourceGroup.Blocks.FirstOrDefault();
                            if (next != null)
                            {
                                SetPanelActiveBlock(sourceGroup, next);
                                MapBlockToPanel(next, sourceGroup);
                            }
                        }
                    }
                    else if (ReferenceEquals(sourceGroup, targetGroup))
                    {
                        targetGroup.RemoveBlock(block.Id, out _);
                        _blockToPanel.Remove(block.Id);
                    }

                    targetGroup.AddBlock(block, makeActive: false, insertIndex: i);
                    MapBlockToPanel(block, targetGroup);

                    if (!string.Equals(block.Id, activeId, StringComparison.OrdinalIgnoreCase))
                    {
                        block.IsVisible = false;
                        _blockNodes.Remove(block.Id);
                    }
                }

                if (_blocks.TryGetValue(activeId, out DockBlock active))
                {
                    SetPanelActiveBlock(targetGroup, active);
                    MapBlockToPanel(active, targetGroup);
                }
            }
        }

        private static string ResolveDefinitionActiveBlockId(string panelId, string declaredActiveId, List<string> orderedBlocks)
        {
            if (string.IsNullOrWhiteSpace(declaredActiveId))
            {
                return null;
            }

            bool declaredIsDocking = string.Equals(declaredActiveId, DockingSetupsBlockKey, StringComparison.OrdinalIgnoreCase);
            if (orderedBlocks.Any(id => string.Equals(id, declaredActiveId, StringComparison.OrdinalIgnoreCase)) &&
                _blocks.TryGetValue(declaredActiveId, out DockBlock declaredBlock) &&
                declaredBlock != null)
            {
                return declaredActiveId;
            }

            if (!declaredIsDocking)
            {
                return declaredActiveId;
            }

            if (!string.IsNullOrWhiteSpace(panelId) &&
                _lastNonDockingActiveByPanel.TryGetValue(panelId, out string rememberedId) &&
                orderedBlocks.Any(id => string.Equals(id, rememberedId, StringComparison.OrdinalIgnoreCase)) &&
                _blocks.TryGetValue(rememberedId, out DockBlock rememberedBlock) &&
                rememberedBlock != null &&
                rememberedBlock.Kind != DockBlockKind.DockingSetups)
            {
                return rememberedId;
            }

            foreach (string id in orderedBlocks)
            {
                if (_blocks.TryGetValue(id, out DockBlock block) && block != null && block.Kind != DockBlockKind.DockingSetups)
                {
                    return id;
                }
            }

            return declaredActiveId;
        }

        private static void ApplyDockingSetupLocks(DockingSetupDefinition setup)
        {
            if (setup == null)
            {
                return;
            }

            if (setup.BlockLocks != null)
            {
                foreach (KeyValuePair<string, bool> pair in setup.BlockLocks)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    if (_blocks.TryGetValue(pair.Key, out DockBlock block) && block != null)
                    {
                        SetBlockLock(block, pair.Value);
                    }
                }
            }

            if (setup.PanelLocks != null)
            {
                foreach (KeyValuePair<string, bool> pair in setup.PanelLocks)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    if (_panelGroups.TryGetValue(pair.Key, out PanelGroup group) && group != null)
                    {
                        group.IsLocked = pair.Value;
                    }
                }
            }
        }

        private static DockNode BuildLayoutFromDefinition(DockingSetupLayoutNode node, HashSet<string> visiblePanels)
        {
            if (node == null)
            {
                return null;
            }

            if (string.Equals(node.Type, "Panel", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(node.PanelId))
                {
                    DebugLogger.PrintError("[DockLayout] Encountered panel node with no id in layout definition.");
                    return null;
                }

                DockNode panelNode = GetPanelNodeById(node.PanelId);
                if (panelNode == null)
                {
                    DebugLogger.PrintError($"[DockLayout] Panel '{node.PanelId}' from layout definition could not be resolved. Falling back to rebuild.");
                    return null;
                }

                visiblePanels?.Add(node.PanelId);
                if (panelNode is BlockNode blockNode &&
                    blockNode.Block != null &&
                    _blockToPanel.TryGetValue(blockNode.Block.Id, out string resolvedPanelId) &&
                    !string.IsNullOrWhiteSpace(resolvedPanelId))
                {
                    visiblePanels?.Add(resolvedPanelId);
                }
                return panelNode;
            }

            if (string.Equals(node.Type, "Split", StringComparison.OrdinalIgnoreCase))
            {
                DockNode first = BuildLayoutFromDefinition(node.First, visiblePanels);
                DockNode second = BuildLayoutFromDefinition(node.Second, visiblePanels);
                if (first == null)
                {
                    return second;
                }

                if (second == null)
                {
                    return first;
                }

                DockSplitOrientation orientation = DockSplitOrientation.Vertical;
                if (!string.IsNullOrWhiteSpace(node.Orientation) &&
                    Enum.TryParse(node.Orientation, true, out DockSplitOrientation parsed))
                {
                    orientation = parsed;
                }

                float ratio = MathHelper.Clamp(node.Ratio <= 0f ? 0.5f : node.Ratio, 0.001f, 0.999f);

                return new SplitNode(orientation)
                {
                    SplitRatio = ratio,
                    First = first,
                    Second = second
                };
            }

            DebugLogger.PrintError($"[DockLayout] Unrecognized layout node type '{node.Type ?? "<null>"}'.");
            return null;
        }

        private static DockNode BuildLayoutFromPanels(HashSet<string> visiblePanels)
        {
            List<BlockNode> nodes = new();
            foreach (string panelId in _orderedPanelIds)
            {
                DockNode node = GetPanelNodeById(panelId);
                if (node is BlockNode blockNode)
                {
                    nodes.Add(blockNode);
                    visiblePanels?.Add(panelId);
                }
            }

            return BuildStack(nodes, DockSplitOrientation.Horizontal);
        }

        private static DockNode GetPanelNodeById(string panelId)
        {
            if (string.IsNullOrWhiteSpace(panelId))
            {
                return null;
            }

            if (_panelGroups.TryGetValue(panelId, out PanelGroup group))
            {
                return GetPanelNode(group);
            }

            if (_blocks.TryGetValue(panelId, out DockBlock block))
            {
                PanelGroup fallbackGroup = GetPanelGroupForBlock(block);
                return GetPanelNode(fallbackGroup);
            }

            return null;
        }

        private static void HidePanelsNotInLayout(HashSet<string> visiblePanels)
        {
            if (visiblePanels == null)
            {
                return;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null)
                {
                    continue;
                }

                if (!_blockToPanel.TryGetValue(block.Id, out string panelId) || string.IsNullOrWhiteSpace(panelId))
                {
                    continue;
                }

                if (visiblePanels.Contains(panelId))
                {
                    continue;
                }

                BlockMenuEntry entry = _blockMenuEntries.FirstOrDefault(e =>
                    e != null && string.Equals(e.IdPrefix, panelId, StringComparison.OrdinalIgnoreCase));

                bool shouldRemainVisible = false;
                if (entry != null)
                {
                    if (entry.ControlMode == BlockMenuControlMode.Toggle)
                    {
                        shouldRemainVisible = entry.IsVisible;
                    }
                    else if (entry.ControlMode == BlockMenuControlMode.Count)
                    {
                        shouldRemainVisible = entry.Count > 0;
                    }
                }

                if (shouldRemainVisible)
                {
                    block.IsVisible = true;
                    visiblePanels.Add(panelId);
                    EnsureBlockAttachedToLayout(block);
                }
                else
                {
                    block.IsVisible = false;
                }
            }
        }

        private static void ApplyToggleVisibilityOverrides()
        {
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                if (entry == null || entry.ControlMode != BlockMenuControlMode.Toggle || entry.IsVisible)
                {
                    continue;
                }

                if (_blocks.TryGetValue(entry.IdPrefix, out DockBlock block) && block != null)
                {
                    block.IsVisible = false;
                }
            }
        }

        private static void PruneInactiveBlockNodes()
        {
            if (_blockNodes.Count == 0)
            {
                return;
            }

            List<string> removeIds = new();
            foreach (string blockId in _blockNodes.Keys)
            {
                if (string.IsNullOrWhiteSpace(blockId))
                {
                    continue;
                }

                if (!_blocks.TryGetValue(blockId, out DockBlock block))
                {
                    removeIds.Add(blockId);
                    continue;
                }

                PanelGroup group = GetPanelGroupForBlock(block);
                if (group == null || !string.Equals(group.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
                {
                    removeIds.Add(blockId);
                }
            }

            foreach (string id in removeIds)
            {
                _blockNodes.Remove(id);
            }
        }

        private static DockingSetupDefinition LoadDockingSetupFromDatabase()
        {
            Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.DockingSetups);
            if (data == null || data.Count == 0)
            {
                return null;
            }

            string activeName = null;
            if (data.TryGetValue(DockingSetupActiveRowKey, out string storedName))
            {
                activeName = storedName?.Trim();
            }

            string payload = null;
            if (!string.IsNullOrWhiteSpace(activeName) && data.TryGetValue(activeName, out string storedPayload))
            {
                payload = storedPayload;
            }
            else
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key.StartsWith("__", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    activeName = pair.Key;
                    payload = pair.Value;
                    break;
                }
            }

            return DeserializeDockingSetup(payload);
        }

        private static DockingSetupDefinition DeserializeDockingSetup(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<DockingSetupDefinition>(payload, DockingSerializerOptions);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to parse docking setup: {ex.Message}");
                return null;
            }
        }

        private static string SerializeDockingSetup(DockingSetupDefinition setup)
        {
            if (setup == null)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Serialize(setup, DockingSerializerOptions);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"Failed to serialize docking setup: {ex.Message}");
                return null;
            }
        }

        private static void EnsureBlockMenuEntries()
        {
            if (_blockMenuEntries.Count > 0)
            {
                return;
            }

            _blockMenuEntries.Add(new BlockMenuEntry(BlankBlockKey, BlankBlock.BlockTitle, DockBlockKind.Blank, BlockMenuControlMode.Count, 0, 10, 0));
            _blockMenuEntries.Add(new BlockMenuEntry(TransparentBlockKey, TransparentBlock.BlockTitle, DockBlockKind.Transparent, BlockMenuControlMode.Count, 0, 10, 0));
            _blockMenuEntries.Add(new BlockMenuEntry(GameBlockKey, GameBlock.BlockTitle, DockBlockKind.Game, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(PropertiesBlockKey, PropertiesBlock.BlockTitle, DockBlockKind.Properties, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ColorSchemeBlockKey, ColorSchemeBlock.BlockTitle, DockBlockKind.ColorScheme, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ControlsBlockKey, ControlsBlock.BlockTitle, DockBlockKind.Controls, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(NotesBlockKey, NotesBlock.BlockTitle, DockBlockKind.Notes, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(DockingSetupsBlockKey, DockingSetupsBlock.BlockTitle, DockBlockKind.DockingSetups, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(BackendBlockKey, BackendBlock.BlockTitle, DockBlockKind.Backend, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(SpecsBlockKey, SpecsBlock.BlockTitle, DockBlockKind.Specs, BlockMenuControlMode.Toggle, initialVisible: true));
        }

        private static int ClampCount(BlockMenuEntry entry, int value)
        {
            if (entry == null)
            {
                return value;
            }

            return Math.Clamp(value, entry.MinCount, entry.MaxCount);
        }

        private static string BuildBlockId(string prefix, int index)
        {
            if (index <= 0)
            {
                return prefix;
            }

            return string.Concat(prefix, index + 1);
        }

        private static string GetUniquePanelId(string seed)
        {
            string baseId = string.IsNullOrWhiteSpace(seed) ? "panel" : seed.Trim();
            string candidate = baseId;
            int suffix = 2;

            while (_panelGroups.ContainsKey(candidate))
            {
                candidate = $"{baseId}-{suffix}";
                suffix++;
            }

            return candidate;
        }

        private static string BuildBlockTitle(BlockMenuEntry entry, int index)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (entry.ControlMode != BlockMenuControlMode.Count || entry.Count <= 1)
            {
                return entry.Label;
            }

            return $"{entry.Label} {index + 1}";
        }

        private static void GroupCountedBlocks(HashSet<string> preservedBlockIds = null)
        {
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                if (entry == null || entry.ControlMode != BlockMenuControlMode.Count)
                {
                    continue;
                }

                List<DockBlock> blocks = _orderedBlocks
                    .Where(block =>
                        block != null &&
                        block.Kind == entry.Kind &&
                        block.Id.StartsWith(entry.IdPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (blocks.Count <= 1)
                {
                    continue;
                }

                DockBlock anchor = blocks.FirstOrDefault(block => string.Equals(block.Id, entry.IdPrefix, StringComparison.OrdinalIgnoreCase)) ?? blocks[0];
                PanelGroup targetGroup = GetPanelGroupForBlock(anchor);
                if (targetGroup == null)
                {
                    continue;
                }

                foreach (DockBlock block in blocks)
                {
                    if (ReferenceEquals(block, anchor))
                    {
                        continue;
                    }

                    if (preservedBlockIds != null && preservedBlockIds.Contains(block.Id))
                    {
                        continue;
                    }

                    PanelGroup sourceGroup = GetPanelGroupForBlock(block);
                    if (sourceGroup != null && ReferenceEquals(sourceGroup, targetGroup))
                    {
                        continue;
                    }

                    MergeBlockIntoGroup(targetGroup, block.Id);
                }

                DockBlock active = targetGroup.ActiveBlock ?? anchor;
                if (active != null)
                {
                    SetPanelActiveBlock(targetGroup, active);
                }
            }
        }

        private static void GroupDefaultPanels()
        {
            PanelGroup colorGroup = null;
            if (_blocks.TryGetValue(ColorSchemeBlockKey, out DockBlock colorBlock))
            {
                colorGroup = GetPanelGroupForBlock(colorBlock);
            }

            if (colorGroup != null)
            {
                MergeBlockIntoGroup(colorGroup, NotesBlockKey);
                MergeBlockIntoGroup(colorGroup, DockingSetupsBlockKey);
            }

            MergeBackendAndSpecs();
        }

        private static void MergeBlockIntoGroup(PanelGroup targetGroup, string blockId)
        {
            if (targetGroup == null || string.IsNullOrWhiteSpace(blockId))
            {
                return;
            }

            if (!_blocks.TryGetValue(blockId, out DockBlock block))
            {
                return;
            }

            PanelGroup sourceGroup = GetPanelGroupForBlock(block);
            if (sourceGroup == null || ReferenceEquals(sourceGroup, targetGroup))
            {
                return;
            }

            sourceGroup.RemoveBlock(block.Id, out _);
            if (sourceGroup.Blocks.Count == 0)
            {
                RemovePanelGroup(sourceGroup);
            }
            else
            {
                DockBlock next = sourceGroup.ActiveBlock ?? sourceGroup.Blocks.FirstOrDefault();
                if (next != null)
                {
                    SetPanelActiveBlock(sourceGroup, next);
                    MapBlockToPanel(next, sourceGroup);
                }
            }

            targetGroup.AddBlock(block, makeActive: false);
            MapBlockToPanel(block, targetGroup);
            block.IsVisible = false;
            _blockNodes.Remove(block.Id);
        }

        private static void MergeBackendAndSpecs()
        {
            if (!_blocks.TryGetValue(BackendBlockKey, out DockBlock backendBlock) ||
                !_blocks.TryGetValue(SpecsBlockKey, out DockBlock specsBlock))
            {
                return;
            }

            PanelGroup backendGroup = GetPanelGroupForBlock(backendBlock);
            PanelGroup specsGroup = GetPanelGroupForBlock(specsBlock);
            if (backendGroup == null || specsGroup == null || ReferenceEquals(backendGroup, specsGroup))
            {
                return;
            }

            MergeBlockIntoGroup(backendGroup, specsBlock.Id);
        }

        private static DockNode BuildDefaultLayout()
        {
            List<BlockNode> blankNodes = GetBlockNodesByKind(DockBlockKind.Blank);
            List<BlockNode> transparentNodes = GetBlockNodesByKind(DockBlockKind.Transparent);
            BlockNode gameNode = GetBlockNodesByKind(DockBlockKind.Game).FirstOrDefault();
            BlockNode propertiesNode = GetBlockNodesByKind(DockBlockKind.Properties).FirstOrDefault();
            BlockNode colorNode = GetBlockNodesByKind(DockBlockKind.ColorScheme).FirstOrDefault();
            BlockNode controlsNode = GetBlockNodesByKind(DockBlockKind.Controls).FirstOrDefault();
            BlockNode notesNode = GetBlockNodesByKind(DockBlockKind.Notes).FirstOrDefault();
            BlockNode backendNode = GetBlockNodesByKind(DockBlockKind.Backend).FirstOrDefault();
            BlockNode specsNode = GetBlockNodesByKind(DockBlockKind.Specs).FirstOrDefault();

            DockNode transparentStack = BuildStack(transparentNodes, DockSplitOrientation.Horizontal);
            DockNode blankStack = BuildStack(blankNodes, DockSplitOrientation.Horizontal);

            float sideRatio = CalculateSplitRatio(transparentNodes.Count, blankNodes.Count, 0.5f);
            DockNode blankAndTransparent = CombineNodes(transparentStack, blankStack, DockSplitOrientation.Vertical, sideRatio);

            DockNode leftColumn = CombineNodes(blankAndTransparent, gameNode, DockSplitOrientation.Horizontal, 0.36f);
            DockNode controlsAndNotes = CombineNodes(controlsNode, notesNode, DockSplitOrientation.Horizontal, 0.5f);
            DockNode backendAndSpecs = CombineNodes(backendNode, specsNode, DockSplitOrientation.Horizontal, 0.58f);
            DockNode paletteBackend = CombineNodes(colorNode, backendAndSpecs, DockSplitOrientation.Horizontal, 0.42f);
            DockNode propertiesAndControls = CombineNodes(propertiesNode, controlsAndNotes, DockSplitOrientation.Horizontal, 0.26f);
            DockNode rightColumn = CombineNodes(propertiesAndControls, paletteBackend, DockSplitOrientation.Horizontal, 0.7f);

            return CombineNodes(leftColumn, rightColumn, DockSplitOrientation.Vertical, 0.67f);
        }

        private static float CalculateSplitRatio(int firstCount, int secondCount, float defaultRatio)
        {
            int total = Math.Max(1, firstCount + secondCount);
            if (firstCount <= 0 || secondCount <= 0)
            {
                return defaultRatio;
            }

            float ratio = firstCount / (float)total;
            return MathHelper.Clamp(ratio, 0.1f, 0.9f);
        }

        private static DockNode BuildStack(IReadOnlyList<BlockNode> nodes, DockSplitOrientation orientation)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return null;
            }

            DockNode current = nodes[0];
            for (int i = 1; i < nodes.Count; i++)
            {
                float ratio = i / (float)(i + 1);
                current = new SplitNode(orientation)
                {
                    SplitRatio = ratio,
                    First = current,
                    Second = nodes[i]
                };
            }

            return current;
        }

        private static DockNode CombineNodes(DockNode first, DockNode second, DockSplitOrientation orientation, float splitRatio)
        {
            if (first == null)
            {
                return second;
            }

            if (second == null)
            {
                return first;
            }

            return new SplitNode(orientation)
            {
                SplitRatio = splitRatio,
                First = first,
                Second = second
            };
        }

        private static List<BlockNode> GetBlockNodesByKind(DockBlockKind kind)
        {
            List<BlockNode> nodes = new();
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || block.Kind != kind)
                {
                    continue;
                }

                if (_blockNodes.TryGetValue(block.Id, out BlockNode node))
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        private static PanelGroup GetPanelGroupForBlock(DockBlock block)
        {
            if (block == null)
            {
                return null;
            }

            if (_blockToPanel.TryGetValue(block.Id, out string panelId) &&
                _panelGroups.TryGetValue(panelId, out PanelGroup group))
            {
                return group;
            }

            foreach (PanelGroup existing in _panelGroups.Values)
            {
                if (existing.Contains(block.Id))
                {
                    _blockToPanel[block.Id] = existing.PanelId;
                    return existing;
                }
            }

            // Lazily recreate a panel container so hidden blocks can be restored.
            string newPanelId = block.Id;
            PanelGroup fallback = new(newPanelId, block);
            _panelGroups[newPanelId] = fallback;
            _blockToPanel[block.Id] = newPanelId;

            if (!_panelNodes.TryGetValue(newPanelId, out BlockNode node))
            {
                if (!_blockNodes.TryGetValue(block.Id, out node))
                {
                    node = new BlockNode(block);
                    _blockNodes[block.Id] = node;
                }

                _panelNodes[newPanelId] = node;
            }

            if (!_orderedPanelIds.Contains(newPanelId, StringComparer.OrdinalIgnoreCase))
            {
                _orderedPanelIds.Add(newPanelId);
            }

            return fallback;
        }

        private static BlockNode GetPanelNode(PanelGroup group)
        {
            if (group == null)
            {
                return null;
            }

            if (_panelNodes.TryGetValue(group.PanelId, out BlockNode node))
            {
                return node;
            }

            DockBlock active = group.ActiveBlock;
            if (active != null && _blockNodes.TryGetValue(active.Id, out node))
            {
                _panelNodes[group.PanelId] = node;
                return node;
            }

            return null;
        }

        private static bool SetPanelActiveBlock(PanelGroup group, DockBlock newActiveBlock)
        {
            if (group == null || newActiveBlock == null)
            {
                return false;
            }

            BlockNode node = GetPanelNode(group);
            if (node == null)
            {
                return false;
            }

            DockBlock previous = group.ActiveBlock;
            if (previous != null)
            {
                _blockNodes.Remove(previous.Id);
                previous.IsVisible = false;
            }

            group.SetActiveBlock(newActiveBlock.Id);
            newActiveBlock.IsVisible = true;
            node.SetBlock(newActiveBlock);
            _blockNodes[newActiveBlock.Id] = node;
            RememberLastNonDockingActive(group, newActiveBlock);
            return true;
        }

        private static void RememberLastNonDockingActive(PanelGroup group, DockBlock block)
        {
            if (group == null || block == null || string.IsNullOrWhiteSpace(group.PanelId))
            {
                return;
            }

            if (block.Kind == DockBlockKind.DockingSetups)
            {
                return;
            }

            _lastNonDockingActiveByPanel[group.PanelId] = block.Id;
        }

        private static void MapBlockToPanel(DockBlock block, PanelGroup group)
        {
            if (block == null || group == null)
            {
                return;
            }

            _blockToPanel[block.Id] = group.PanelId;
        }

        private static int GetGroupBarHeight(PanelGroup group)
        {
            if (group == null)
            {
                return 0;
            }

            if (DockingModeEnabled)
            {
                return Math.Min(GroupBarHeight, UIStyle.DragBarHeight);
            }

            return GroupBarHeight;
        }

        private static Rectangle GetGroupBarBounds(DockBlock block, PanelGroup group, int dragBarHeight)
        {
            if (block == null || group == null)
            {
                return Rectangle.Empty;
            }

            int groupBarHeight = GetGroupBarHeight(group);
            if (groupBarHeight <= 0)
            {
                return Rectangle.Empty;
            }

            int headerHeight = Math.Max(dragBarHeight, groupBarHeight);
            Rectangle headerRect = new(block.Bounds.X, block.Bounds.Y, block.Bounds.Width, Math.Min(headerHeight, block.Bounds.Height));
            if (headerRect.Width <= 0 || headerRect.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int buttonStart = headerRect.Right - DragBarButtonPadding;
            if (dragBarHeight > 0)
            {
                GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds);
                if (closeBounds != Rectangle.Empty)
                {
                    buttonStart = Math.Min(buttonStart, closeBounds.X);
                }

                if (panelLockBounds != Rectangle.Empty)
                {
                    buttonStart = Math.Min(buttonStart, panelLockBounds.X);
                }
            }

            int width = Math.Max(0, Math.Min(headerRect.Width, buttonStart - DragBarButtonSpacing - GroupBarDragGap - headerRect.X));
            int height = Math.Max(0, Math.Min(groupBarHeight, headerRect.Height));
            if (width <= 0 || height <= 0)
            {
                return Rectangle.Empty;
            }

            int y = headerRect.Y + Math.Max(0, (headerRect.Height - height) / 2);
            return new Rectangle(headerRect.X, y, width, height);
        }

        private static Rectangle GetDragBarGrabBounds(DockBlock block, PanelGroup group, int dragBarHeight)
        {
            if (block == null || dragBarHeight <= 0)
            {
                return Rectangle.Empty;
            }

            Rectangle dragBar = block.GetDragBarBounds(dragBarHeight);
            if (dragBar.Width <= 0 || dragBar.Height <= 0)
            {
                return Rectangle.Empty;
            }

            GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds);
            int buttonStart = dragBar.Right - DragBarButtonPadding;
            if (closeBounds != Rectangle.Empty)
            {
                buttonStart = Math.Min(buttonStart, closeBounds.X);
            }

            if (panelLockBounds != Rectangle.Empty)
            {
                buttonStart = Math.Min(buttonStart, panelLockBounds.X);
            }

            Rectangle groupBar = GetGroupBarBounds(block, group, dragBarHeight);
            bool includeTabs = group != null && IsPanelLocked(group);
            int left = includeTabs || groupBar == Rectangle.Empty ? dragBar.X + DragBarButtonPadding : groupBar.Right;
            int right = buttonStart - DragBarButtonSpacing;
            int width = Math.Max(0, right - left);
            if (width <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(left, dragBar.Y, width, dragBar.Height);
        }

        private static Rectangle GetPanelContentBounds(DockBlock block, int dragBarHeight)
        {
            PanelGroup group = GetPanelGroupForBlock(block);
            int groupBarHeight = GetGroupBarHeight(group);
            return block.GetContentBounds(dragBarHeight, UIStyle.BlockPadding, groupBarHeight);
        }

        private static void ActivatePanelTab(PanelGroup group, string blockId)
        {
            if (group == null || string.IsNullOrWhiteSpace(blockId))
            {
                return;
            }

            DockBlock target = group.Blocks.FirstOrDefault(b => string.Equals(b.Id, blockId, StringComparison.OrdinalIgnoreCase));
            if (target == null || string.Equals(group.ActiveBlockId, blockId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DockBlock previous = group.ActiveBlock;
            bool hadFocus = previous != null && BlockHasFocus(previous.Id);

            if (previous != null)
            {
                previous.IsVisible = false;
            }

            if (SetPanelActiveBlock(group, target))
            {
                MapBlockToPanel(target, group);
                if (hadFocus)
                {
                    SetFocusedBlock(target);
                }

                MarkLayoutDirty();
            }
        }

        private static void RemovePanelGroup(PanelGroup group)
        {
            if (group == null)
            {
                return;
            }

            foreach (DockBlock block in group.Blocks.ToList())
            {
                _blockToPanel.Remove(block.Id);
                _blockNodes.Remove(block.Id);
            }

            if (_panelNodes.TryGetValue(group.PanelId, out BlockNode node))
            {
                _rootNode = DockLayout.Detach(_rootNode, node);
                _panelNodes.Remove(group.PanelId);
            }

            _lastNonDockingActiveByPanel.Remove(group.PanelId);
            _panelGroups.Remove(group.PanelId);
            _orderedPanelIds.Remove(group.PanelId);
        }

        private static void RebuildGroupBarLayoutCache(int dragBarHeight)
        {
            _groupBarLayoutCache.Clear();
            foreach (PanelGroup group in _panelGroups.Values)
            {
                DockBlock active = group?.ActiveBlock;
                if (active == null || !active.IsVisible)
                {
                    continue;
                }

                PanelGroupBarLayout layout = BuildGroupBarLayout(active, group, dragBarHeight);
                if (layout != null)
                {
                    _groupBarLayoutCache[group.PanelId] = layout;
                }
            }
        }

        private static PanelGroupBarLayout BuildGroupBarLayout(DockBlock block, PanelGroup group, int dragBarHeight)
        {
            if (block == null || group == null)
            {
                return null;
            }

            Rectangle groupBarBounds = GetGroupBarBounds(block, group, dragBarHeight);
            PanelGroupBarLayout layout = new(group.PanelId, groupBarBounds);
            if (groupBarBounds == Rectangle.Empty || group.Blocks.Count == 0)
            {
                return layout;
            }

            static int CalculateIconMinWidth(bool showCloseButtons, bool showLockButtons, bool showUngroupButtons)
            {
                int width = TabHorizontalPadding * 2;
                bool hasIcon = false;

                if (showCloseButtons)
                {
                    width += TabCloseSize;
                    hasIcon = true;
                }

                if (showLockButtons)
                {
                    if (hasIcon)
                    {
                        width += TabClosePadding;
                    }

                    width += TabLockSize;
                    hasIcon = true;
                }

                if (showUngroupButtons)
                {
                    if (hasIcon)
                    {
                        width += TabClosePadding;
                    }

                    width += TabUngroupSize;
                }

                return width;
            }

            static void TopUpWidths(IList<int> widths, int targetWidth, ref int remainingWidth)
            {
                if (widths == null || widths.Count == 0 || targetWidth <= 0 || remainingWidth <= 0)
                {
                    return;
                }

                bool progressed = true;
                while (remainingWidth > 0 && progressed)
                {
                    progressed = false;
                    for (int i = 0; i < widths.Count && remainingWidth > 0; i++)
                    {
                        if (widths[i] >= targetWidth)
                        {
                            continue;
                        }

                        int needed = targetWidth - widths[i];
                        int share = Math.Max(1, remainingWidth / Math.Max(1, widths.Count - i));
                        int grant = Math.Min(needed, Math.Min(share, remainingWidth));
                        if (grant <= 0)
                        {
                            continue;
                        }

                        widths[i] += grant;
                        remainingWidth -= grant;
                        progressed = true;
                    }
                }
            }

            static void TopUpWidthsToTargets(IList<int> widths, IReadOnlyList<int> targets, ref int remainingWidth)
            {
                if (widths == null || targets == null || widths.Count == 0 || targets.Count == 0 || remainingWidth <= 0)
                {
                    return;
                }

                int count = Math.Min(widths.Count, targets.Count);
                bool progressed = true;
                while (remainingWidth > 0 && progressed)
                {
                    progressed = false;
                    for (int i = 0; i < count && remainingWidth > 0; i++)
                    {
                        int target = targets[i];
                        if (widths[i] >= target)
                        {
                            continue;
                        }

                        int needed = target - widths[i];
                        int share = Math.Max(1, remainingWidth / Math.Max(1, count - i));
                        int grant = Math.Min(needed, Math.Min(share, remainingWidth));
                        if (grant <= 0)
                        {
                            continue;
                        }

                        widths[i] += grant;
                        remainingWidth -= grant;
                        progressed = true;
                    }
                }
            }

            UIStyle.UIFont tabFont = UIStyle.FontTech;
            bool panelLocked = IsPanelLocked(group);
            bool showLockButtons = DockingModeEnabled && !panelLocked;
            bool showCloseButtons = DockingModeEnabled && !panelLocked;
            bool showUngroupButtons = showLockButtons && group.Blocks.Count > 1;
            int tabCount = group.Blocks.Count;
            int availableWidth = Math.Max(0, groupBarBounds.Width - (TabSpacing * Math.Max(0, tabCount - 1)) - (TabHorizontalPadding * 2));
            int iconMinWidth = CalculateIconMinWidth(showCloseButtons, showLockButtons, showUngroupButtons);
            int comfortableMinWidth = Math.Max(iconMinWidth, TabMinWidth);
            int baseWidth = tabCount > 0 ? (int)Math.Floor(Math.Min(iconMinWidth, availableWidth / (double)tabCount)) : 0;
            baseWidth = Math.Max(0, baseWidth);
            int remainingWidth = Math.Max(0, availableWidth - (baseWidth * tabCount));

            List<int> tabWidths = new(tabCount);
            List<int> textWidths = new(tabCount);
            foreach (DockBlock tabBlock in group.Blocks)
            {
                tabWidths.Add(baseWidth);
                int measuredText = tabFont.IsAvailable ? (int)Math.Ceiling(tabFont.MeasureString(tabBlock.Title).X) : 0;
                textWidths.Add(Math.Max(0, measuredText));
            }

            TopUpWidths(tabWidths, iconMinWidth, ref remainingWidth);
            if (comfortableMinWidth > iconMinWidth)
            {
                TopUpWidths(tabWidths, comfortableMinWidth, ref remainingWidth);
            }

            List<int> textTargets = new(tabCount);
            for (int i = 0; i < tabCount; i++)
            {
                int target = comfortableMinWidth + textWidths[i];
                target = Math.Min(target, availableWidth);
                target = Math.Max(tabWidths[i], target);
                textTargets.Add(target);
            }

            TopUpWidthsToTargets(tabWidths, textTargets, ref remainingWidth);

            if (remainingWidth > 0 && tabWidths.Count > 0)
            {
                tabWidths[^1] += remainingWidth;
                remainingWidth = 0;
            }

            int x = groupBarBounds.X + TabHorizontalPadding;
            int height = Math.Max(0, groupBarBounds.Height - (TabVerticalPadding * 2));
            for (int i = 0; i < group.Blocks.Count && i < tabWidths.Count; i++)
            {
                DockBlock tabBlock = group.Blocks[i];
                int targetWidth = tabWidths[i];
                targetWidth = Math.Min(targetWidth, Math.Max(0, groupBarBounds.Right - x));
                if (targetWidth <= 0)
                {
                    break;
                }

                Rectangle tabBounds = new(x, groupBarBounds.Y + TabVerticalPadding, targetWidth, Math.Max(0, height));
                Rectangle closeBounds = showCloseButtons ? GetTabCloseBounds(tabBounds) : Rectangle.Empty;
                Rectangle lockBounds = showLockButtons ? GetTabLockBounds(tabBounds, closeBounds) : Rectangle.Empty;
                Rectangle ungroupBounds = showUngroupButtons ? GetTabUngroupBounds(tabBounds, lockBounds, closeBounds) : Rectangle.Empty;
                layout.Tabs.Add(new TabHitRegion(tabBlock.Id, tabBounds, ungroupBounds, lockBounds, closeBounds));
                x += targetWidth + TabSpacing;
                if (x >= groupBarBounds.Right)
                {
                    break;
                }
            }

            layout.Tabs.Sort((a, b) => a.Bounds.X.CompareTo(b.Bounds.X));
            return layout;
        }

        private static Rectangle GetTabCloseBounds(Rectangle tabBounds)
        {
            if (tabBounds.Width <= 0 || tabBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int closeSize = Math.Min(TabCloseSize, Math.Max(8, tabBounds.Height - (TabVerticalPadding * 2)));
            int closeX = Math.Max(tabBounds.X + TabHorizontalPadding, tabBounds.Right - TabHorizontalPadding - closeSize);
            int closeY = tabBounds.Y + (tabBounds.Height - closeSize) / 2;
            return new Rectangle(closeX, closeY, closeSize, closeSize);
        }

        private static Rectangle GetTabLockBounds(Rectangle tabBounds, Rectangle closeBounds)
        {
            if (tabBounds.Width <= 0 || tabBounds.Height <= 0 || closeBounds == Rectangle.Empty)
            {
                return Rectangle.Empty;
            }

            int lockSize = Math.Min(TabLockSize, Math.Max(8, tabBounds.Height));
            int lockX = closeBounds.X - TabClosePadding - lockSize;
            int minX = tabBounds.X + TabHorizontalPadding;
            if (lockX < minX)
            {
                return Rectangle.Empty;
            }

            int lockY = tabBounds.Y + (tabBounds.Height - lockSize) / 2;
            return new Rectangle(lockX, lockY, lockSize, lockSize);
        }

        private static Rectangle GetTabUngroupBounds(Rectangle tabBounds, Rectangle lockBounds, Rectangle closeBounds)
        {
            if (tabBounds.Width <= 0 || tabBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            Rectangle anchor = lockBounds == Rectangle.Empty ? closeBounds : lockBounds;
            if (anchor == Rectangle.Empty)
            {
                return Rectangle.Empty;
            }

            int ungroupSize = Math.Min(TabUngroupSize, Math.Max(8, tabBounds.Height));
            int ungroupX = anchor.X - TabClosePadding - ungroupSize;
            int minX = tabBounds.X + TabHorizontalPadding;
            if (ungroupX < minX)
            {
                return Rectangle.Empty;
            }

            int ungroupY = tabBounds.Y + (tabBounds.Height - ungroupSize) / 2;
            return new Rectangle(ungroupX, ungroupY, ungroupSize, ungroupSize);
        }

        private static PanelGroupBarLayout GetGroupBarLayoutForGroup(PanelGroup group, DockBlock block, int dragBarHeight)
        {
            if (group == null || block == null)
            {
                return null;
            }

            if (_groupBarLayoutCache.TryGetValue(group.PanelId, out PanelGroupBarLayout cached))
            {
                return cached;
            }

            PanelGroupBarLayout layout = BuildGroupBarLayout(block, group, dragBarHeight);
            if (layout != null)
            {
                _groupBarLayoutCache[group.PanelId] = layout;
            }

            return layout;
        }

        private static DockBlock CreateBlock(string id, string title, DockBlockKind kind)
        {
            DockBlock block = new(id, title, kind);
            (int minWidth, int minHeight) = GetBlockMinimumSize(kind);
            block.MinWidth = Math.Max(0, minWidth);
            block.MinHeight = Math.Max(0, minHeight);
            _blocks[id] = block;
            _orderedBlocks.Add(block);
            BlockNode node = new(block);
            _blockNodes[id] = node;
            _panelGroups[id] = new PanelGroup(id, block);
            _panelNodes[id] = node;
            _blockToPanel[id] = id;
            _orderedPanelIds.Add(id);
            EnsureBlockLockState(block);
            RememberLastNonDockingActive(_panelGroups[id], block);
            return block;
        }

        private static (int MinWidth, int MinHeight) GetBlockMinimumSize(DockBlockKind kind)
        {
            const int defaultMin = 10;
            (int width, int height) = kind switch
            {
                DockBlockKind.Game => (GameBlock.MinWidth, GameBlock.MinHeight),
                DockBlockKind.Transparent => (TransparentBlock.MinWidth, TransparentBlock.MinHeight),
                DockBlockKind.Blank => (BlankBlock.MinWidth, BlankBlock.MinHeight),
                DockBlockKind.Properties => (PropertiesBlock.MinWidth, PropertiesBlock.MinHeight),
                DockBlockKind.ColorScheme => (ColorSchemeBlock.MinWidth, ColorSchemeBlock.MinHeight),
                DockBlockKind.Controls => (ControlsBlock.MinWidth, ControlsBlock.MinHeight),
                DockBlockKind.Notes => (NotesBlock.MinWidth, NotesBlock.MinHeight),
                DockBlockKind.DockingSetups => (DockingSetupsBlock.MinWidth, DockingSetupsBlock.MinHeight),
                DockBlockKind.Backend => (BackendBlock.MinWidth, BackendBlock.MinHeight),
                DockBlockKind.Specs => (SpecsBlock.MinWidth, SpecsBlock.MinHeight),
                _ => (defaultMin, defaultMin)
            };

            // Keep at least the drag bar height so we can detect when a drag bar is being pushed;
            // overflow gets propagated to neighboring resize edges instead of hard-clamping.
            int clampedWidth = Math.Max(width, UIStyle.MinBlockSize);
            int headerHeight = Math.Max(UIStyle.DragBarHeight, GroupBarHeight);
            int clampedHeight = Math.Max(height, headerHeight);
            return (clampedWidth, clampedHeight);
        }

        private static void EnsureSurfaceResources(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_pixelTexture == null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            EnsureLockIcons();

            bool gameBlockVisible = TryGetGameBlock(out DockBlock gameBlock) && gameBlock.IsVisible;
            int desiredWidth = Math.Max(1, _gameContentBounds.Width);
            int desiredHeight = Math.Max(1, _gameContentBounds.Height);

            if (!gameBlockVisible || desiredWidth <= 1 || desiredHeight <= 1)
            {
                desiredWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
                desiredHeight = graphicsDevice.PresentationParameters.BackBufferHeight;

                if (desiredWidth <= 0 || desiredHeight <= 0)
                {
                    desiredWidth = Math.Max(1, Core.Instance?.ViewportWidth ?? 1280);
                    desiredHeight = Math.Max(1, Core.Instance?.ViewportHeight ?? 720);
                }
            }

            if (_worldRenderTarget == null || _worldRenderTarget.Width != desiredWidth || _worldRenderTarget.Height != desiredHeight)
            {
                _worldRenderTarget?.Dispose();
                _worldRenderTarget = new RenderTarget2D(graphicsDevice, desiredWidth, desiredHeight, false, SurfaceFormat.Color, DepthFormat.None);
            }

            UIStyle.EnsureFontsLoaded(Core.Instance?.Content);
        }

        private static bool UpdateResizeEdgeState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (!AnyBlockVisible() || _resizeEdges.Count == 0)
            {
                _hoveredResizeEdge = null;
                if (!leftClickHeld)
                {
                    _activeResizeEdge = null;
                    ClearResizeEdgeSnap();
                }

                return false;
            }

            if (_activeResizeEdge.HasValue)
            {
                if (!leftClickHeld || leftClickReleased)
                {
                    if (_activeResizeEdge.HasValue)
                    {
                        DebugLogger.PrintUI($"[ResizeEdgeEnd] {DescribeResizeEdge(_activeResizeEdge.Value)}");
                    }
                    LogResizeBlockDeltas();
                    _activeResizeEdge = null;
                    ClearResizeEdgeSnap();
                    return false;
                }

                ApplyResizeEdgeDrag(_activeResizeEdge.Value, _mousePosition);

                // If we're snapped to another handle, move that handle in lockstep so both edges nudge together.
                if (_activeResizeEdgeSnapTarget.HasValue && _activeResizeEdgeSnapCoordinate.HasValue)
                {
                    ResizeEdge snapTarget = _activeResizeEdgeSnapTarget.Value;
                    Point snappedPosition = snapTarget.Orientation == DockSplitOrientation.Vertical
                        ? new Point(_activeResizeEdgeSnapCoordinate.Value, _mousePosition.Y)
                        : new Point(_mousePosition.X, _activeResizeEdgeSnapCoordinate.Value);
                    ApplyResizeEdgeDrag(snapTarget, snappedPosition);
                }

                return true;
            }

            ResizeEdge? hovered = HitTestResizeEdge(_mousePosition);
            _hoveredResizeEdge = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                DebugLogger.PrintUI($"[ResizeEdgeStart] {DescribeResizeEdge(hovered.Value)} Mouse={_mousePosition}");
                CaptureBlockBoundsForResize();
                _activeResizeEdge = hovered;
                ClearResizeEdgeSnap();
                ApplyResizeEdgeDrag(hovered.Value, _mousePosition);
                return true;
            }

            return false;
        }

        private static bool UpdateCornerResizeState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (!AnyBlockVisible() || _cornerHandles.Count == 0)
            {
                _hoveredCornerHandle = null;
                if (!leftClickHeld)
                {
                    _activeCornerHandle = null;
                    _activeCornerLinkedHandle = null;
                    ClearCornerSnap();
                }

                return false;
            }

            if (_activeCornerHandle.HasValue)
            {
                if (!leftClickHeld || leftClickReleased)
                {
                    if (_activeCornerHandle.HasValue)
                    {
                        DebugLogger.PrintUI($"[CornerEnd] {DescribeCornerHandle(_activeCornerHandle.Value)}");
                        LogResizeBlockDeltas();
                    }
                    _activeCornerHandle = null;
                    _activeCornerLinkedHandle = null;
                    ClearCornerSnap();
                    return false;
                }

                ApplyCornerHandleDrag(_activeCornerHandle.Value, _mousePosition);
                return true;
            }

            CornerHandle? hovered = HitTestCornerHandle(_mousePosition);
            _hoveredCornerHandle = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                DebugLogger.PrintUI($"[CornerStart] {DescribeCornerHandle(hovered.Value)} Mouse={_mousePosition}");
                CaptureBlockBoundsForResize();
                _activeCornerHandle = hovered;
                _activeCornerLinkedHandle = FindAlignedCorner(hovered.Value);
                ClearCornerSnap();
                ApplyCornerHandleDrag(hovered.Value, _mousePosition);
                return true;
            }

            return false;
        }

        private static bool UpdateTabInteractions(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased, bool allowReorder)
        {
            if (!leftClickStarted && !leftClickReleased && string.IsNullOrWhiteSpace(_pressedTabBlockId))
            {
                return false;
            }

            bool allowLockToggle = DockingModeEnabled;
            foreach (PanelGroupBarLayout layout in _groupBarLayoutCache.Values)
            {
                if (layout == null || layout.Tabs.Count == 0)
                {
                    continue;
                }

                if (!_panelGroups.TryGetValue(layout.PanelId, out PanelGroup group))
                {
                    continue;
                }

                DockBlock active = group.ActiveBlock;
                if (active == null || !active.IsVisible || GetGroupBarHeight(group) <= 0)
                {
                    continue;
                }

                bool inTabRegion = layout.GroupBarBounds != Rectangle.Empty && layout.GroupBarBounds.Contains(_mousePosition);
                if (!inTabRegion)
                {
                    foreach (TabHitRegion tab in layout.Tabs)
                    {
                        if (tab.Bounds.Contains(_mousePosition))
                        {
                            inTabRegion = true;
                            break;
                        }
                    }
                }

                if (!inTabRegion)
                {
                    continue;
                }

                bool panelLocked = DockingModeEnabled && IsPanelLocked(group);

                if (leftClickStarted)
                {
                    if (!panelLocked && allowLockToggle)
                    {
                        foreach (TabHitRegion tab in layout.Tabs)
                        {
                            if (tab.UngroupBounds != Rectangle.Empty &&
                                tab.UngroupBounds.Contains(_mousePosition) &&
                                _blocks.TryGetValue(tab.BlockId, out DockBlock tabBlock) &&
                                TryUngroupTab(group, tabBlock))
                            {
                                ClearPressedTabState();
                                return true;
                            }
                        }

                        foreach (TabHitRegion tab in layout.Tabs)
                        {
                            if (tab.LockBounds != Rectangle.Empty &&
                                tab.LockBounds.Contains(_mousePosition) &&
                                _blocks.TryGetValue(tab.BlockId, out DockBlock tabBlock))
                            {
                                ToggleBlockLock(tabBlock);
                                ClearPressedTabState();
                                return true;
                            }
                        }
                    }

                    if (!panelLocked)
                    {
                        foreach (TabHitRegion tab in layout.Tabs)
                        {
                            if (tab.CloseBounds != Rectangle.Empty && tab.CloseBounds.Contains(_mousePosition) && _blocks.TryGetValue(tab.BlockId, out DockBlock tabBlock))
                            {
                                if (BlockHasFocus(tabBlock.Id))
                                {
                                    ClearBlockFocus();
                                }

                                HandleBlockClose(tabBlock);
                                ClearPressedTabState();
                                return true;
                            }
                        }
                    }

                    string targetTab = null;
                    foreach (TabHitRegion tab in layout.Tabs)
                    {
                        if (tab.Bounds.Contains(_mousePosition))
                        {
                            targetTab = tab.BlockId;
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(targetTab))
                    {
                        _pressedTabBlockId = targetTab;
                        _pressedTabPanelId = layout.PanelId;
                        _tabPressPosition = _mousePosition;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_pressedTabBlockId))
            {
                bool panelLocked = false;
                PanelGroup pressedGroup = null;
                if (_panelGroups.TryGetValue(_pressedTabPanelId, out PanelGroup resolvedGroup))
                {
                    pressedGroup = resolvedGroup;
                    panelLocked = DockingModeEnabled && IsPanelLocked(resolvedGroup);
                }

                if (!panelLocked && leftClickHeld && !leftClickReleased && allowReorder)
                {
                    int deltaX = Math.Abs(_mousePosition.X - _tabPressPosition.X);
                    int deltaY = Math.Abs(_mousePosition.Y - _tabPressPosition.Y);
                    if (deltaX >= TabDragStartThreshold || deltaY >= TabDragStartThreshold)
                    {
                        if (TryBeginTabDrag(_pressedTabPanelId, _pressedTabBlockId))
                        {
                            ClearPressedTabState();
                            return false;
                        }

                        ClearPressedTabState();
                        return true;
                    }
                }

                if (leftClickReleased)
                {
                    bool releasedOnPressedTab = false;
                    if (_groupBarLayoutCache.TryGetValue(_pressedTabPanelId, out PanelGroupBarLayout releaseLayout))
                    {
                        foreach (TabHitRegion tab in releaseLayout.Tabs)
                        {
                            if (string.Equals(tab.BlockId, _pressedTabBlockId, StringComparison.OrdinalIgnoreCase) &&
                                tab.Bounds.Contains(_mousePosition))
                            {
                                releasedOnPressedTab = true;
                                break;
                            }
                        }
                    }

                    if (releasedOnPressedTab && pressedGroup != null)
                    {
                        ActivatePanelTab(pressedGroup, _pressedTabBlockId);
                    }

                    ClearPressedTabState();
                    return true;
                }

                return true;
            }

            return false;
        }

        private static void ClearPressedTabState()
        {
            _pressedTabBlockId = null;
            _pressedTabPanelId = null;
            _tabPressPosition = Point.Zero;
        }

        private static bool TryBeginTabDrag(string panelId, string blockId)
        {
            if (string.IsNullOrWhiteSpace(panelId) || string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            if (!_panelGroups.TryGetValue(panelId, out PanelGroup group) || !_blocks.TryGetValue(blockId, out DockBlock block))
            {
                return false;
            }

            _draggingBlock = block;
            _draggingPanel = group;
            _draggingFromTab = true;
            SetFocusedBlock(block);

            BlockNode panelNode = GetPanelNode(group);
            if (!_blockNodes.TryGetValue(block.Id, out BlockNode movingNode) || ReferenceEquals(movingNode, panelNode))
            {
                movingNode = new BlockNode(block);
                _blockNodes[block.Id] = movingNode;
            }

            Rectangle startBounds = GetDragOriginBounds(group, block);
            _draggingStartBounds = startBounds;
            _dragOffset = new Point(_mousePosition.X - startBounds.X, _mousePosition.Y - startBounds.Y);
            return true;
        }

        private static bool TryUngroupTab(PanelGroup sourceGroup, DockBlock block)
        {
            if (!DockingModeEnabled || sourceGroup == null || block == null || sourceGroup.Blocks.Count <= 1)
            {
                return false;
            }

            BlockNode sourceNode = GetPanelNode(sourceGroup);
            if (sourceNode == null)
            {
                return false;
            }

            if (_rootNode == null || !LayoutContainsNode(_rootNode, sourceNode))
            {
                EnsureBlockAttachedToLayout(block);
                if (_rootNode == null || !LayoutContainsNode(_rootNode, sourceNode))
                {
                    return false;
                }
            }

            Rectangle bounds = sourceNode.Block?.Bounds ?? _layoutBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = _layoutBounds;
            }

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = new Rectangle(0, 0, UIStyle.MinBlockSize, UIStyle.MinBlockSize);
            }

            DockSplitOrientation orientation = bounds.Height > bounds.Width
                ? DockSplitOrientation.Vertical
                : DockSplitOrientation.Horizontal;

            bool hadFocus = BlockHasFocus(block.Id);

            if (!sourceGroup.RemoveBlock(block.Id, out _))
            {
                return false;
            }

            _blockToPanel.Remove(block.Id);
            _blockNodes.Remove(block.Id);

            DockBlock newSourceActive = sourceGroup.ActiveBlock ?? sourceGroup.Blocks.FirstOrDefault();
            if (newSourceActive != null)
            {
                SetPanelActiveBlock(sourceGroup, newSourceActive);
                MapBlockToPanel(newSourceActive, sourceGroup);
            }

            string newPanelId = GetUniquePanelId(block.Id);
            PanelGroup newGroup = new(newPanelId, block);
            block.IsVisible = true;
            _panelGroups[newPanelId] = newGroup;
            _blockToPanel[block.Id] = newPanelId;

            BlockNode newPanelNode = new(block);
            _blockNodes[block.Id] = newPanelNode;
            _panelNodes[newPanelId] = newPanelNode;
            if (!_orderedPanelIds.Contains(newPanelId, StringComparer.OrdinalIgnoreCase))
            {
                _orderedPanelIds.Add(newPanelId);
            }

            RememberLastNonDockingActive(newGroup, block);

            SplitNode split = new(orientation)
            {
                SplitRatio = 0.5f
            };

            if (orientation == DockSplitOrientation.Vertical)
            {
                int halfWidth = Math.Max(0, bounds.Width / 2);
                split.PreferredFirstSpan = halfWidth;
                split.PreferredSecondSpan = Math.Max(0, bounds.Width - halfWidth);
            }
            else
            {
                int halfHeight = Math.Max(0, bounds.Height / 2);
                split.PreferredFirstSpan = halfHeight;
                split.PreferredSecondSpan = Math.Max(0, bounds.Height - halfHeight);
            }

            split.First = sourceNode;
            split.Second = newPanelNode;

            _rootNode = ReplaceNode(_rootNode, sourceNode, split) ?? split;

            if (hadFocus)
            {
                SetFocusedBlock(block);
            }

            MarkLayoutDirty();
            RebuildGroupBarLayoutCache(GetActiveDragBarHeight());
            return true;
        }

        private static Rectangle GetDragOriginBounds(PanelGroup group, DockBlock block)
        {
            if (block != null && block.Bounds.Width > 0 && block.Bounds.Height > 0)
            {
                return block.Bounds;
            }

            DockBlock active = group?.ActiveBlock;
            if (active != null && active.Bounds.Width > 0 && active.Bounds.Height > 0)
            {
                return active.Bounds;
            }

            int width = Math.Max(block?.MinWidth ?? UIStyle.MinBlockSize, UIStyle.MinBlockSize);
            int height = Math.Max(block?.MinHeight ?? UIStyle.MinBlockSize, UIStyle.MinBlockSize);
            return new Rectangle(_mousePosition.X - (width / 2), _mousePosition.Y - (height / 2), width, height);
        }

        private static void UpdateDragState(bool leftClickStarted, bool leftClickReleased, bool allowReorder)
        {
            _hoveredDragBarId = null;

            if (!AnyBlockVisible())
            {
                _draggingBlock = null;
                _draggingPanel = null;
                _dropPreview = null;
                return;
            }

            if (leftClickStarted && TryTogglePanelLock(_mousePosition))
            {
                return;
            }

            if (leftClickStarted)
            {
                DockBlock closeHit = HitTestCloseButton(_mousePosition);
                if (closeHit != null)
                {
                    if (BlockHasFocus(closeHit.Id))
                    {
                        ClearBlockFocus();
                    }

                    HandleBlockClose(closeHit);
                    return;
                }
            }

            DockBlock dragBarHover = _draggingFromTab
                ? null
                : HitTestDragBarBlock(_mousePosition, excludeDragBarButtons: true, requireGrabRegion: true);
            _hoveredDragBarId = dragBarHover?.Id;

            DockBlock dragBarHit = null;
            if (leftClickStarted)
            {
                dragBarHit = HitTestDragBarBlock(_mousePosition, excludeDragBarButtons: true, requireGrabRegion: true);
                if (dragBarHit != null)
                {
                    SetFocusedBlock(dragBarHit);
                }
            }

            if (!allowReorder)
            {
                _draggingBlock = null;
                _dropPreview = null;
                return;
            }

            if (_draggingBlock == null && leftClickStarted)
            {
                DockBlock block = dragBarHit ?? HitTestDragBarBlock(_mousePosition, excludeDragBarButtons: true, requireGrabRegion: true);
                if (block != null)
                {
                    _draggingFromTab = false;
                    ClearPressedTabState();
                    _draggingBlock = block;
                    _draggingPanel = GetPanelGroupForBlock(block);
                    _draggingStartBounds = block.Bounds;
                    _dragOffset = new Point(_mousePosition.X - block.Bounds.X, _mousePosition.Y - block.Bounds.Y);
                }
            }
            else if (_draggingBlock != null)
            {
                _dropPreview = BuildDropPreview(_mousePosition);

                if (leftClickReleased)
                {
                    if (_dropPreview.HasValue)
                    {
                        ApplyDrop(_dropPreview.Value);
                    }

                    _draggingBlock = null;
                    _draggingPanel = null;
                    _draggingFromTab = false;
                    _dropPreview = null;
                }
            }
        }

        private static bool TryTogglePanelLock(Point position)
        {
            DockBlock lockHit = HitTestPanelLockButton(position);
            if (lockHit == null)
            {
                return false;
            }

            PanelGroup group = GetPanelGroupForBlock(lockHit);
            TogglePanelLock(group);
            ClearDockingInteractions();
            return true;
        }

        private static void HandleBlockClose(DockBlock block)
        {
            if (block == null)
            {
                return;
            }

            PanelGroup group = GetPanelGroupForBlock(block);
            bool hadFocus = BlockHasFocus(block.Id);

            if (group != null && group.Blocks.Count > 1)
            {
                if (TryDecrementCountedBlock(block))
                {
                    return;
                }

                group.RemoveBlock(block.Id, out _);
                _blockToPanel.Remove(block.Id);
                block.IsVisible = false;

                DockBlock next = group.ActiveBlock ?? group.Blocks.FirstOrDefault();
                if (next != null)
                {
                    SetPanelActiveBlock(group, next);
                    MapBlockToPanel(next, group);
                    if (hadFocus)
                    {
                        SetFocusedBlock(next);
                    }
                }

                RebuildGroupBarLayoutCache(GetActiveDragBarHeight());
                MarkLayoutDirty();
                return;
            }

            block.IsVisible = false;

            if (TryDecrementCountedBlock(block))
            {
                return;
            }

            MarkLayoutDirty();
            RebuildGroupBarLayoutCache(GetActiveDragBarHeight());
        }

        private static bool TryDecrementCountedBlock(DockBlock block)
        {
            EnsureBlockMenuEntries();
            BlockMenuEntry entry = _blockMenuEntries.FirstOrDefault(e => e.Kind == block.Kind && e.ControlMode == BlockMenuControlMode.Count);
            if (entry == null)
            {
                return false;
            }

            int remainingBlocks = _orderedBlocks.Count(p => p != null && p.Kind == block.Kind && !ReferenceEquals(p, block));
            entry.Count = ClampCount(entry, remainingBlocks);
            entry.InputBuffer = entry.Count.ToString();

            RemoveBlock(block);
            return true;
        }

        private static void RemoveBlock(DockBlock block)
        {
            if (block == null)
            {
                return;
            }

            PanelGroup group = GetPanelGroupForBlock(block);
            bool panelRemoved = false;
            if (group != null)
            {
                group.RemoveBlock(block.Id, out _);
                _blockToPanel.Remove(block.Id);

                if (group.Blocks.Count == 0)
                {
                    RemovePanelGroup(group);
                    panelRemoved = true;
                }
                else if (string.Equals(group.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
                {
                    DockBlock next = group.ActiveBlock ?? group.Blocks.FirstOrDefault();
                    if (next != null)
                    {
                        SetPanelActiveBlock(group, next);
                    }
                }
            }

            bool detachNode = group == null;
            if (_blockNodes.TryGetValue(block.Id, out BlockNode node))
            {
                _blockNodes.Remove(block.Id);
                if (detachNode && !panelRemoved)
                {
                    _rootNode = DockLayout.Detach(_rootNode, node);
                }
            }

            _blocks.Remove(block.Id);
            _orderedBlocks.Remove(block);
            _blockLockStates.Remove(block.Id);
            _blankBlockHoverStates.Remove(block.Id);
            if (string.Equals(_hoveredDragBarId, block.Id, StringComparison.OrdinalIgnoreCase))
            {
                _hoveredDragBarId = null;
            }
            MarkLayoutDirty();
        }

        private static bool IsBlockLocked(DockBlock block)
        {
            if (block == null)
            {
                return false;
            }

            if (_panelInteractionLockActive)
            {
                return true;
            }

            if (!IsLockToggleAvailable(block))
            {
                return false;
            }

            return IsBlockLockEnabled(block);
        }

        private static bool IsBlockLockEnabled(DockBlock block)
        {
            if (block == null || !IsLockToggleAvailable(block))
            {
                return false;
            }

            EnsureBlockLockState(block);
            _blockLockStates.TryGetValue(block.Id, out bool locked);

            return locked;
        }

        private static bool IsPanelLocked(PanelGroup group)
        {
            if (_panelInteractionLockActive)
            {
                return true;
            }

            return group != null && group.IsLocked;
        }

        private static bool IsPanelLocked(DockBlock block)
        {
            if (block == null)
            {
                return false;
            }

            PanelGroup group = GetPanelGroupForBlock(block);
            return IsPanelLocked(group);
        }

        private static void ToggleBlockLock(DockBlock block)
        {
            if (block == null)
            {
                return;
            }

            SetBlockLock(block, !IsBlockLockEnabled(block));
        }

        private static void SetBlockLock(DockBlock block, bool isLocked)
        {
            if (block == null || !IsLockToggleAvailable(block))
            {
                return;
            }

            _blockLockStates[block.Id] = isLocked;
            BlockDataStore.SetBlockLock(block.Kind, isLocked);
        }

        private static void TogglePanelLock(PanelGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.IsLocked = !group.IsLocked;
        }

        private static void EnsureBlockLockState(DockBlock block)
        {
            if (block == null || !IsLockToggleAvailable(block) || _blockLockStates.ContainsKey(block.Id))
            {
                return;
            }

            bool storedLock = BlockDataStore.GetBlockLock(block.Kind);
            _blockLockStates[block.Id] = storedLock;
        }

        private static DockBlock HitTestDragBarBlock(Point position, bool excludeDragBarButtons = false, bool requireGrabRegion = false)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible)
                {
                    continue;
                }

                Rectangle dragBarRect = block.GetDragBarBounds(dragBarHeight);
                if (!dragBarRect.Contains(position))
                {
                    continue;
                }

                if (excludeDragBarButtons && IsPointOnDragBarButton(block, dragBarHeight, position))
                {
                    continue;
                }

                if (requireGrabRegion)
                {
                    PanelGroup group = GetPanelGroupForBlock(block);
                    Rectangle grabBounds = GetDragBarGrabBounds(block, group, dragBarHeight);
                    if (grabBounds == Rectangle.Empty || !grabBounds.Contains(position))
                    {
                        continue;
                    }
                }

                return block;
            }

            return null;
        }

        private static DockBlock HitTestCloseButton(Point position)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible)
                {
                    continue;
                }

                Rectangle closeBounds = GetCloseButtonBounds(block, dragBarHeight);
                if (closeBounds != Rectangle.Empty && closeBounds.Contains(position))
                {
                    return block;
                }
            }

            return null;
        }

        private static DockBlock HitTestPanelLockButton(Point position)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible)
                {
                    continue;
                }

                Rectangle panelLockBounds = GetPanelLockButtonBounds(block, dragBarHeight);
                if (panelLockBounds != Rectangle.Empty && panelLockBounds.Contains(position))
                {
                    return block;
                }
            }

            return null;
        }

        private static bool IsPointOnDragBarButton(DockBlock block, int dragBarHeight, Point position)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds);
            return (panelLockBounds != Rectangle.Empty && panelLockBounds.Contains(position)) ||
                (closeBounds != Rectangle.Empty && closeBounds.Contains(position));
        }

        private static Rectangle GetCloseButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out _, out Rectangle closeBounds);
            return closeBounds;
        }

        private static Rectangle GetPanelLockButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out _);
            return panelLockBounds;
        }

        private static void GetDragBarButtonBounds(DockBlock block, int dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds)
        {
            panelLockBounds = Rectangle.Empty;
            closeBounds = Rectangle.Empty;

            if (block == null || dragBarHeight <= 0)
            {
                return;
            }

            Rectangle dragBarRect = block.GetDragBarBounds(dragBarHeight);
            if (dragBarRect.Width <= 0 || dragBarRect.Height <= 0)
            {
                return;
            }

            closeBounds = GetCloseButtonBounds(dragBarRect);

            if (closeBounds == Rectangle.Empty)
            {
                return;
            }

            int buttonSize = closeBounds.Width;
            int x = closeBounds.X - DragBarButtonSpacing - buttonSize;
            int y = closeBounds.Y;
            int minimumX = dragBarRect.X + DragBarButtonPadding;
            if (x < minimumX)
            {
                return;
            }

            panelLockBounds = new Rectangle(x, y, buttonSize, buttonSize);
        }

        private static bool IsLockToggleAvailable(DockBlock block)
        {
            if (block == null)
            {
                return false;
            }

            return true;
        }

        private static Rectangle GetCloseButtonBounds(Rectangle dragBarRect)
        {
            if (dragBarRect.Width <= 0 || dragBarRect.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int buttonSize = Math.Clamp(dragBarRect.Height - 10, 14, 24);
            if (buttonSize <= 0 || dragBarRect.Width <= (DragBarButtonPadding * 2) + buttonSize)
            {
                return Rectangle.Empty;
            }

            int x = dragBarRect.Right - DragBarButtonPadding - buttonSize;
            int y = dragBarRect.Y + (dragBarRect.Height - buttonSize) / 2;
            return new Rectangle(x, y, buttonSize, buttonSize);
        }

        private static DockDropPreview? BuildViewportSnapPreview(Point position)
        {
            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                return null;
            }

            Rectangle viewport = graphicsDevice.Viewport.Bounds;
            Rectangle layout = GetLayoutBounds(viewport);
            if (layout.Width <= 0 || layout.Height <= 0)
            {
                return null;
            }

            Rectangle expanded = layout;
            expanded.Inflate(WindowEdgeSnapDistance, WindowEdgeSnapDistance);
            if (!expanded.Contains(position))
            {
                return null;
            }

            int bestDistance = WindowEdgeSnapDistance + 1;
            DockEdge? bestEdge = null;

            DockSplitOrientation? bestOrientation = null;

            void Consider(DockEdge edge, int distance)
            {
                if (distance < 0 || distance > WindowEdgeSnapDistance || distance >= bestDistance)
                {
                    return;
                }

                bestDistance = distance;
                bestEdge = edge;
                bestOrientation = edge is DockEdge.Top or DockEdge.Bottom
                    ? DockSplitOrientation.Horizontal
                    : DockSplitOrientation.Vertical;
            }

            Consider(DockEdge.Left, Math.Abs(position.X - layout.X));
            Consider(DockEdge.Right, Math.Abs(layout.Right - position.X));
            Consider(DockEdge.Top, Math.Abs(position.Y - layout.Y));
            Consider(DockEdge.Bottom, Math.Abs(layout.Bottom - position.Y));

            if (!bestEdge.HasValue)
            {
                return null;
            }

            DockSplitOrientation orientation = bestOrientation ?? DockSplitOrientation.Vertical;
            int existingCount = CountVisibleBlocksAlongOrientation(_rootNode, orientation, _draggingBlock);
            int total = Math.Max(1, existingCount + 1);
            float newFraction = 1f / total;

            int targetWidth = orientation == DockSplitOrientation.Vertical
                ? Math.Max(1, (int)Math.Round(layout.Width * newFraction))
                : layout.Width;

            int targetHeight = orientation == DockSplitOrientation.Horizontal
                ? Math.Max(1, (int)Math.Round(layout.Height * newFraction))
                : layout.Height;

            Rectangle highlight = bestEdge.Value switch
            {
                DockEdge.Left => new Rectangle(layout.X, layout.Y, targetWidth, layout.Height),
                DockEdge.Right => new Rectangle(layout.Right - targetWidth, layout.Y, targetWidth, layout.Height),
                DockEdge.Top => new Rectangle(layout.X, layout.Y, layout.Width, targetHeight),
                DockEdge.Bottom => new Rectangle(layout.X, layout.Bottom - targetHeight, layout.Width, targetHeight),
                _ => layout
            };

            return new DockDropPreview
            {
                Edge = bestEdge.Value,
                HighlightBounds = highlight,
                IsViewportSnap = true
            };
        }

        private static DockDropPreview? BuildDropPreview(Point position)
        {
            if (_draggingFromTab)
            {
                return BuildTabDropPreview(position);
            }

            DockDropPreview? preview = BuildViewportSnapPreview(position);
            if (preview.HasValue)
            {
                return preview;
            }

            preview = null;
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || block == _draggingBlock)
                {
                    continue;
                }

                Rectangle bounds = block.Bounds;
                if (!bounds.Contains(position))
                {
                    continue;
                }

                float relativeX = (position.X - bounds.X) / (float)Math.Max(1, bounds.Width);
                float relativeY = (position.Y - bounds.Y) / (float)Math.Max(1, bounds.Height);

                DockEdge edge;
                if (relativeY <= UIStyle.DropEdgeThreshold)
                {
                    edge = DockEdge.Top;
                }
                else if (relativeY >= 1f - UIStyle.DropEdgeThreshold)
                {
                    edge = DockEdge.Bottom;
                }
                else if (relativeX <= 0.5f)
                {
                    edge = DockEdge.Left;
                }
                else
                {
                    edge = DockEdge.Right;
                }

                Rectangle highlight = edge switch
                {
                    DockEdge.Top => new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2),
                    DockEdge.Bottom => new Rectangle(bounds.X, bounds.Y + bounds.Height / 2, bounds.Width, bounds.Height / 2),
                    DockEdge.Left => new Rectangle(bounds.X, bounds.Y, bounds.Width / 2, bounds.Height),
                    DockEdge.Right => new Rectangle(bounds.X + bounds.Width / 2, bounds.Y, bounds.Width / 2, bounds.Height),
                    _ => bounds
                };

                preview = new DockDropPreview
                {
                    TargetBlock = block,
                    Edge = edge,
                    HighlightBounds = highlight
                };
                break;
            }

            return preview;
        }

        private static DockDropPreview? BuildTabDropPreview(Point position)
        {
            if (_draggingBlock == null)
            {
                return null;
            }

            int dragBarHeight = GetActiveDragBarHeight();

            foreach (PanelGroupBarLayout layout in _groupBarLayoutCache.Values)
            {
                if (layout == null)
                {
                    continue;
                }

                if (!_panelGroups.TryGetValue(layout.PanelId, out PanelGroup targetGroup))
                {
                    continue;
                }

                DockBlock active = targetGroup.ActiveBlock;
                if (active == null || IsPanelLocked(targetGroup))
                {
                    continue;
                }

                Rectangle groupBarBounds = layout.GroupBarBounds;
                Rectangle dragBar = dragBarHeight > 0 ? active.GetDragBarBounds(dragBarHeight) : Rectangle.Empty;
                Rectangle hitBounds = groupBarBounds != Rectangle.Empty ? groupBarBounds : dragBar;
                if (hitBounds == Rectangle.Empty || !hitBounds.Contains(position))
                {
                    continue;
                }

                PanelGroupBarLayout layoutToUse = layout;
                if (layoutToUse.Tabs.Count == 0)
                {
                    PanelGroupBarLayout rebuilt = BuildGroupBarLayout(active, targetGroup, dragBarHeight);
                    if (rebuilt != null)
                    {
                        layoutToUse = rebuilt;
                        _groupBarLayoutCache[targetGroup.PanelId] = rebuilt;
                        groupBarBounds = rebuilt.GroupBarBounds;
                    }
                }

                int insertIndex = CalculateTabInsertIndex(layoutToUse.Tabs, position.X);
                Rectangle highlight = GetTabInsertHighlight(layoutToUse, groupBarBounds, dragBar, insertIndex);

                return new DockDropPreview
                {
                    TargetBlock = active,
                    TargetPanelId = targetGroup.PanelId,
                    HighlightBounds = highlight,
                    TabInsertIndex = insertIndex,
                    IsTabDrop = true
                };
            }

            return null;
        }

        private static int CalculateTabInsertIndex(IReadOnlyList<TabHitRegion> tabs, int mouseX)
        {
            if (tabs == null || tabs.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                TabHitRegion tab = tabs[i];
                if (mouseX <= tab.Bounds.Center.X)
                {
                    return i;
                }
            }

            return tabs.Count;
        }

        private static Rectangle GetTabInsertHighlight(PanelGroupBarLayout layout, Rectangle groupBarBounds, Rectangle dragBarBounds, int insertIndex)
        {
            Rectangle barBounds = groupBarBounds;
            if (barBounds == Rectangle.Empty && layout != null && layout.Tabs.Count > 0)
            {
                TabHitRegion first = layout.Tabs.First();
                TabHitRegion last = layout.Tabs.Last();
                barBounds = Rectangle.Union(first.Bounds, last.Bounds);
            }

            if (barBounds == Rectangle.Empty)
            {
                barBounds = dragBarBounds;
            }

            if (barBounds == Rectangle.Empty || layout == null || layout.Tabs.Count == 0)
            {
                return barBounds;
            }

            int indicatorX;
            if (insertIndex <= 0)
            {
                indicatorX = layout.Tabs.First().Bounds.X - (TabSpacing / 2);
            }
            else if (insertIndex >= layout.Tabs.Count)
            {
                indicatorX = layout.Tabs.Last().Bounds.Right + (TabSpacing / 2);
            }
            else
            {
                Rectangle previous = layout.Tabs[insertIndex - 1].Bounds;
                Rectangle next = layout.Tabs[insertIndex].Bounds;
                indicatorX = previous.Right + ((next.X - previous.Right) / 2);
            }

            int lineWidth = Math.Max(3, UIStyle.DragOutlineThickness + 1);
            int halfWidth = lineWidth / 2;
            int minX = barBounds.X + halfWidth;
            int maxX = barBounds.Right - halfWidth;
            if (minX > maxX)
            {
                minX = barBounds.X;
                maxX = barBounds.Right;
            }

            indicatorX = Math.Clamp(indicatorX, minX, maxX);
            int height = Math.Max(0, barBounds.Height - 4);
            int y = barBounds.Y + 2;
            return new Rectangle(indicatorX - halfWidth, y, lineWidth, height);
        }

        private static CornerHandle? HitTestCornerHandle(Point position)
        {
            foreach (CornerHandle corner in _cornerHandles)
            {
                Rectangle hitBounds = corner.Bounds;
                if (hitBounds.Contains(position))
                {
                    return corner;
                }
            }

            return null;
        }

        private static ResizeEdge? HitTestResizeEdge(Point position)
        {
            // When handles overlap (e.g., nested splits near each other), pick the one whose center
            // is closest to the cursor along the resize axis, breaking ties by depth so nested layouts
            // still feel precise without stealing drags meant for a parent boundary.
            ResizeEdge? best = null;
            int bestDepth = -1;
            int bestDistance = int.MaxValue;

            foreach (ResizeEdge handle in _resizeEdges)
            {
                Rectangle hitBounds = handle.Bounds;
                hitBounds.Inflate(2, 2);
                if (!hitBounds.Contains(position))
                {
                    continue;
                }

                int axisCenter = GetResizeEdgeAxisCenter(handle);
                int axisDistance = handle.Orientation == DockSplitOrientation.Vertical
                    ? Math.Abs(position.X - axisCenter)
                    : Math.Abs(position.Y - axisCenter);

                if (!best.HasValue ||
                    axisDistance < bestDistance ||
                    (axisDistance == bestDistance && handle.Depth > bestDepth))
                {
                    best = handle;
                    bestDepth = handle.Depth;
                    bestDistance = axisDistance;
                }
            }

            return best;
        }

        private static void ApplyResizeEdgeDrag(ResizeEdge handle, Point position)
        {
            if (handle.Node == null)
            {
                return;
            }

            // Avoid propagating loops when we nudge neighboring handles.
            if (_isPropagatingResize)
            {
                position = GetResizeEdgePosition(handle, position);
                ApplyResizeEdgeDragInternal(handle, position, allowPropagation: false);
                return;
            }

            position = GetResizeEdgePosition(handle, position);
            ApplyResizeEdgeDragInternal(handle, position, allowPropagation: true);
        }

        private static void ApplyResizeEdgeDragInternal(ResizeEdge handle, Point position, bool allowPropagation)
        {
            Rectangle bounds = handle.Node.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            float previousRatio = handle.Node.SplitRatio;
            float newRatio;

            int span = handle.Orientation == DockSplitOrientation.Vertical ? bounds.Width : bounds.Height;
            int axisPositionRaw = handle.Orientation == DockSplitOrientation.Vertical ? position.X - bounds.X : position.Y - bounds.Y;
            int minFirst = handle.Node.Orientation == DockSplitOrientation.Vertical
                ? handle.Node.First?.GetMinWidth() ?? 0
                : handle.Node.First?.GetMinHeight() ?? 0;
            int minSecond = handle.Node.Orientation == DockSplitOrientation.Vertical
                ? handle.Node.Second?.GetMinWidth() ?? 0
                : handle.Node.Second?.GetMinHeight() ?? 0;
            ResizeEdge? snapPartner = _activeResizeEdgeSnapTarget.HasValue && _activeResizeEdgeSnapTarget.Value.Orientation == handle.Orientation
                ? _activeResizeEdgeSnapTarget
                : null;

            if (handle.Orientation == DockSplitOrientation.Vertical)
            {
                newRatio = ClampSplitRatio(handle.Node, axisPositionRaw, bounds.Width);
            }
            else
            {
                newRatio = ClampSplitRatio(handle.Node, axisPositionRaw, bounds.Height);
            }

            if (float.IsNaN(newRatio) || float.IsInfinity(newRatio))
            {
                return;
            }

            newRatio = MathHelper.Clamp(newRatio, 0.001f, 0.999f);
            if (Math.Abs(newRatio - previousRatio) > 0.0001f)
            {
                string firstDesc = DescribeNode(handle.Node.First);
                string secondDesc = DescribeNode(handle.Node.Second);
                DebugLogger.PrintUI($"[ResizeEdgeDrag] Ori={handle.Orientation} Node={DescribeNode(handle.Node)} Prev={previousRatio:F3} New={newRatio:F3} RelativePos={(handle.Orientation == DockSplitOrientation.Vertical ? position.X - bounds.X : position.Y - bounds.Y)} Bounds={bounds} Mouse={position} First={firstDesc} Second={secondDesc}");
                handle.Node.SplitRatio = newRatio;

                int totalSpan = handle.Orientation == DockSplitOrientation.Vertical ? bounds.Width : bounds.Height;
                int newFirstSpan = Math.Max(0, Math.Min(totalSpan, (int)MathF.Round(totalSpan * newRatio)));
                int newSecondSpan = Math.Max(0, totalSpan - newFirstSpan);
                handle.Node.PreferredFirstSpan = newFirstSpan;
                handle.Node.PreferredSecondSpan = newSecondSpan;
                handle.Node.IsUserSized = true;

                MarkLayoutDirty();
            }

            if (allowPropagation)
            {
                int minClamp = Math.Min(span, Math.Max(0, minFirst));
                int maxClamp = Math.Max(minClamp, span - Math.Max(0, minSecond));

                if (axisPositionRaw < minClamp)
                {
                    int overflow = minClamp - axisPositionRaw;
                    NudgeNearestResizeEdge(handle, overflow, negativeDirection: true, snapPartner);
                }
                else if (axisPositionRaw > maxClamp)
                {
                    int overflow = axisPositionRaw - maxClamp;
                    NudgeNearestResizeEdge(handle, overflow, negativeDirection: false, snapPartner);
                }
            }
        }

        private static void NudgeNearestResizeEdge(ResizeEdge source, int amount, bool negativeDirection, ResizeEdge? preferred = null)
        {
            if (amount <= 0)
            {
                return;
            }

            ResizeEdge? best = preferred;
            int bestDistance = int.MaxValue;

            foreach (ResizeEdge other in _resizeEdges)
            {
                if (ResizeEdgesEqual(other, source) || other.Orientation != source.Orientation)
                {
                    continue;
                }

                Rectangle a = source.Bounds;
                Rectangle b = other.Bounds;

                bool overlaps = source.Orientation == DockSplitOrientation.Vertical
                    ? a.Y < b.Bottom && a.Bottom > b.Y
                    : a.X < b.Right && a.Right > b.X;

                if (!overlaps)
                {
                    continue;
                }

                int delta = source.Orientation == DockSplitOrientation.Vertical
                    ? b.Center.X - a.Center.X
                    : b.Center.Y - a.Center.Y;

                if (negativeDirection && delta >= 0)
                {
                    continue;
                }

                if (!negativeDirection && delta <= 0)
                {
                    continue;
                }

                int distance = Math.Abs(delta);
                if (!best.HasValue || distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }

            if (!best.HasValue)
            {
                return;
            }

            ResizeEdge target = best.Value;
            int signedAmount = negativeDirection ? -amount : amount;
            Point targetPosition = target.Orientation == DockSplitOrientation.Vertical
                ? new Point(target.Bounds.Center.X + signedAmount, target.Bounds.Center.Y)
                : new Point(target.Bounds.Center.X, target.Bounds.Center.Y + signedAmount);

            _isPropagatingResize = true;
            try
            {
                ApplyResizeEdgeDragInternal(target, targetPosition, allowPropagation: false);
            }
            finally
            {
                _isPropagatingResize = false;
            }
        }

        private static void ApplyCornerHandleDrag(CornerHandle corner, Point position)
        {
            Point snapped = GetCornerDragPosition(corner, position);
            ApplyResizeEdgeDrag(corner.VerticalHandle, snapped);
            ApplyResizeEdgeDrag(corner.HorizontalHandle, snapped);

            // If two corners started the drag already snapped together, move the paired corner in lockstep.
            if (_activeCornerHandle.HasValue && CornerEquals(corner, _activeCornerHandle.Value) && _activeCornerLinkedHandle.HasValue)
            {
                ApplyResizeEdgeDrag(_activeCornerLinkedHandle.Value.VerticalHandle, snapped);
                ApplyResizeEdgeDrag(_activeCornerLinkedHandle.Value.HorizontalHandle, snapped);
            }
        }

        private static Point GetResizeEdgePosition(ResizeEdge handle, Point position)
        {
            if (!_activeResizeEdge.HasValue || !ResizeEdgesEqual(handle, _activeResizeEdge.Value))
            {
                return position;
            }

            int? snapCoordinate = GetResizeEdgeSnapCoordinate(handle, position);
            if (!snapCoordinate.HasValue)
            {
                return position;
            }

            if (handle.Orientation == DockSplitOrientation.Vertical)
            {
                return new Point(snapCoordinate.Value, position.Y);
            }

            return new Point(position.X, snapCoordinate.Value);
        }

        private static int? GetResizeEdgeSnapCoordinate(ResizeEdge handle, Point position)
        {
            int axisPosition = handle.Orientation == DockSplitOrientation.Vertical ? position.X : position.Y;
            ResizeEdge? candidate = FindResizeEdgeSnapTarget(handle, axisPosition, CornerSnapDistance);

            if (candidate.HasValue)
            {
                int targetCoordinate = GetResizeEdgeAxisCenter(candidate.Value);
                _activeResizeEdgeSnapTarget = candidate.Value;
                _activeResizeEdgeSnapCoordinate = targetCoordinate;
                return targetCoordinate;
            }

            if (_activeResizeEdgeSnapCoordinate.HasValue)
            {
                int releaseDistance = GetResizeEdgeReleaseDistance();
                int distance = Math.Abs(axisPosition - _activeResizeEdgeSnapCoordinate.Value);
                if (distance <= releaseDistance)
                {
                    return _activeResizeEdgeSnapCoordinate.Value;
                }

                ClearResizeEdgeSnap();
            }

            return null;
        }

        private static ResizeEdge? FindResizeEdgeSnapTarget(ResizeEdge handle, int axisPosition, int threshold)
        {
            if (_resizeEdges.Count <= 1 || threshold <= 0)
            {
                return null;
            }

            int snapDistance = Math.Max(1, threshold);
            int bestDistance = snapDistance;
            ResizeEdge? bestHandle = null;

            foreach (ResizeEdge other in _resizeEdges)
            {
                if (ResizeEdgesEqual(other, handle) || other.Orientation != handle.Orientation)
                {
                    continue;
                }

                int otherCoordinate = GetResizeEdgeAxisCenter(other);
                int distance = Math.Abs(otherCoordinate - axisPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHandle = other;
                }
            }

            return bestHandle;
        }

        private static int GetResizeEdgeAxisCenter(ResizeEdge handle)
        {
            return handle.Orientation == DockSplitOrientation.Vertical
                ? handle.Bounds.Center.X
                : handle.Bounds.Center.Y;
        }

        private static int GetResizeEdgeReleaseDistance()
        {
            int baseDistance = Math.Max(CornerSnapDistance * 2, CornerSnapDistance + 12);
            return Math.Max(baseDistance, 1);
        }

        private static CornerSnapResult? FindCornerSnapTarget(CornerHandle corner, Point position, int threshold)
        {
            if (_cornerHandles.Count <= 1 || threshold <= 0)
            {
                return null;
            }

            int snapDistance = Math.Max(1, threshold);
            int snapDistanceSq = snapDistance * snapDistance;
            int bestDistanceSq = snapDistanceSq;
            CornerSnapResult? bestResult = null;

            foreach (CornerHandle other in _cornerHandles)
            {
                if (CornerEquals(other, corner))
                {
                    continue;
                }

                Point otherIntersection = GetCornerIntersection(other);
                int distanceSq = DistanceSquared(position, otherIntersection);
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bool shareVertical = ReferenceEquals(other.VerticalHandle.Node, corner.VerticalHandle.Node);
                bool shareHorizontal = ReferenceEquals(other.HorizontalHandle.Node, corner.HorizontalHandle.Node);

                bool lockX;
                bool lockY;
                if (shareVertical && shareHorizontal)
                {
                    continue;
                }
                else if (shareVertical)
                {
                    lockX = false;
                    lockY = true;
                }
                else if (shareHorizontal)
                {
                    lockX = true;
                    lockY = false;
                }
                else
                {
                    lockX = true;
                    lockY = true;
                }

                if (!lockX && !lockY)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                bestResult = new CornerSnapResult(other, otherIntersection, lockX, lockY);
            }

            return bestResult;
        }

        private static int GetCornerReleaseDistanceSquared()
        {
            int releaseDistance = Math.Max(CornerSnapDistance * 2, CornerSnapDistance + 12);
            releaseDistance = Math.Max(releaseDistance, 1);
            return releaseDistance * releaseDistance;
        }

        private static bool IsWithinCornerSnapRange(Point position)
        {
            if (!_activeCornerSnapAnchor.HasValue)
            {
                return false;
            }

            int thresholdSq = GetCornerReleaseDistanceSquared();
            Point anchor = _activeCornerSnapAnchor.Value;
            int distanceSq = 0;

            if (_activeCornerSnapLockX)
            {
                int dx = position.X - anchor.X;
                distanceSq += dx * dx;
            }

            if (_activeCornerSnapLockY)
            {
                int dy = position.Y - anchor.Y;
                distanceSq += dy * dy;
            }

            return distanceSq <= thresholdSq;
        }

        private static int DistanceSquared(Point a, Point b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
        }

        private static int CountVisibleBlocksAlongOrientation(DockNode node, DockSplitOrientation orientation, DockBlock excludeBlock = null)
        {
            if (node == null || !node.HasVisibleContent)
            {
                return 0;
            }

            if (node is BlockNode blockNode)
            {
                DockBlock block = blockNode.Block;
                if (block == null || !block.IsVisible || (excludeBlock != null && ReferenceEquals(block, excludeBlock)))
                {
                    return 0;
                }

                return 1;
            }

            if (node is SplitNode split)
            {
                int first = CountVisibleBlocksAlongOrientation(split.First, orientation, excludeBlock);
                int second = CountVisibleBlocksAlongOrientation(split.Second, orientation, excludeBlock);
                if (split.Orientation == orientation)
                {
                    return first + second;
                }

                return Math.Max(first, second);
            }

            return 0;
        }

        private static Point GetCornerDragPosition(CornerHandle corner, Point position)
        {
            if (!_activeCornerHandle.HasValue || !CornerEquals(_activeCornerHandle.Value, corner))
            {
                ClearCornerSnap();
                return position;
            }

            // Allow free corner dragging; do not snap to nearby intersections to avoid grid-like movement.
            ClearCornerSnap();
            return position;
        }

        private static float ClampSplitRatio(SplitNode node, int relativePosition, int spanLength)
        {
            if (node == null || spanLength <= 0)
            {
                return 0.5f;
            }

            int minFirst = node.Orientation == DockSplitOrientation.Vertical
                ? node.First?.GetMinWidth() ?? 0
                : node.First?.GetMinHeight() ?? 0;
            int minSecond = node.Orientation == DockSplitOrientation.Vertical
                ? node.Second?.GetMinWidth() ?? 0
                : node.Second?.GetMinHeight() ?? 0;

            minFirst = Math.Clamp(minFirst, 0, spanLength);
            minSecond = Math.Clamp(minSecond, 0, spanLength);

            int minClamp = Math.Min(spanLength, Math.Max(0, minFirst));
            int maxClamp = Math.Max(minClamp, spanLength - minSecond);
            int clamped = Math.Clamp(relativePosition, minClamp, maxClamp);
            return clamped / (float)Math.Max(1, spanLength);
        }

        private static void DetachDraggingTabFromGroup()
        {
            if (!_draggingFromTab || _draggingBlock == null)
            {
                return;
            }

            PanelGroup sourceGroup = _draggingPanel ?? GetPanelGroupForBlock(_draggingBlock);
            if (sourceGroup == null)
            {
                _draggingFromTab = false;
                return;
            }

            bool hadFocus = BlockHasFocus(_draggingBlock.Id);

            sourceGroup.RemoveBlock(_draggingBlock.Id, out _);
            _blockToPanel.Remove(_draggingBlock.Id);

            if (sourceGroup.Blocks.Count == 0)
            {
                RemovePanelGroup(sourceGroup);
            }
            else
            {
                DockBlock next = sourceGroup.ActiveBlock ?? sourceGroup.Blocks.FirstOrDefault();
                if (next != null)
                {
                    SetPanelActiveBlock(sourceGroup, next);
                    MapBlockToPanel(next, sourceGroup);
                    if (hadFocus)
                    {
                        SetFocusedBlock(next);
                    }
                }
            }

            PanelGroup newGroup = GetPanelGroupForBlock(_draggingBlock);
            SetPanelActiveBlock(newGroup, _draggingBlock);
            MapBlockToPanel(_draggingBlock, newGroup);
            _draggingPanel = newGroup;
            _draggingFromTab = false;
            MarkLayoutDirty();
            RebuildGroupBarLayoutCache(GetActiveDragBarHeight());
        }

        private static void ApplyDrop(DockDropPreview preview)
        {
            if (_draggingBlock == null)
            {
                return;
            }

            if (preview.IsTabDrop)
            {
                ApplyTabDrop(preview);
                return;
            }

            if (preview.IsViewportSnap)
            {
                DetachDraggingTabFromGroup();
                ApplyViewportSnap(preview.Edge);
                return;
            }

            if (preview.TargetBlock == null)
            {
                return;
            }

            if (_draggingBlock == preview.TargetBlock)
            {
                return;
            }

            DetachDraggingTabFromGroup();

            if (!_blockNodes.TryGetValue(_draggingBlock.Id, out BlockNode movingNode) ||
                !_blockNodes.TryGetValue(preview.TargetBlock.Id, out BlockNode targetNode))
            {
                return;
            }

            _rootNode = DockLayout.Detach(_rootNode, movingNode);
            _rootNode ??= targetNode;
            _rootNode = DockLayout.InsertRelative(_rootNode, movingNode, targetNode, preview.Edge);
            MarkLayoutDirty();
        }

        private static void ApplyTabDrop(DockDropPreview preview)
        {
            if (_draggingBlock == null || string.IsNullOrWhiteSpace(preview.TargetPanelId))
            {
                return;
            }

            if (!_panelGroups.TryGetValue(preview.TargetPanelId, out PanelGroup targetGroup))
            {
                return;
            }

            DockBlock targetActive = targetGroup.ActiveBlock;
            if (targetActive == null || IsPanelLocked(targetGroup))
            {
                return;
            }

            PanelGroup sourceGroup = _draggingPanel ?? GetPanelGroupForBlock(_draggingBlock);
            if (sourceGroup == null)
            {
                return;
            }

            int insertIndex = Math.Clamp(preview.TabInsertIndex, 0, Math.Max(0, targetGroup.Blocks.Count));

            if (!ReferenceEquals(sourceGroup, targetGroup))
            {
                sourceGroup.RemoveBlock(_draggingBlock.Id, out _);
                if (sourceGroup.Blocks.Count > 0)
                {
                    DockBlock newSourceActive = sourceGroup.ActiveBlock ?? sourceGroup.Blocks.FirstOrDefault();
                    if (newSourceActive != null)
                    {
                        SetPanelActiveBlock(sourceGroup, newSourceActive);
                        MapBlockToPanel(newSourceActive, sourceGroup);
                    }
                }
                targetGroup.AddBlock(_draggingBlock, makeActive: true, insertIndex: insertIndex);
                MapBlockToPanel(_draggingBlock, targetGroup);
            }
            else
            {
                sourceGroup.RemoveBlock(_draggingBlock.Id, out _);
                targetGroup.AddBlock(_draggingBlock, makeActive: true, insertIndex: insertIndex);
            }

            SetPanelActiveBlock(targetGroup, _draggingBlock);

            if (!ReferenceEquals(sourceGroup, targetGroup))
            {
                if (sourceGroup.Blocks.Count == 0)
                {
                    RemovePanelGroup(sourceGroup);
                }
                else
                {
                    DockBlock next = sourceGroup.ActiveBlock ?? sourceGroup.Blocks.FirstOrDefault();
                    if (next != null)
                    {
                        SetPanelActiveBlock(sourceGroup, next);
                    }
                }
            }

            _draggingFromTab = false;
            MarkLayoutDirty();
            RebuildGroupBarLayoutCache(GetActiveDragBarHeight());
        }

        private static void ApplyViewportSnap(DockEdge edge)
        {
            if (_draggingBlock == null)
            {
                return;
            }

            if (!_blockNodes.TryGetValue(_draggingBlock.Id, out BlockNode movingNode))
            {
                return;
            }

            _rootNode = DockLayout.Detach(_rootNode, movingNode);
            DockNode remaining = _rootNode;
            if (remaining == null)
            {
                _rootNode = movingNode;
                MarkLayoutDirty();
                return;
            }

            DockSplitOrientation orientation = edge is DockEdge.Top or DockEdge.Bottom
                ? DockSplitOrientation.Horizontal
                : DockSplitOrientation.Vertical;

            int existingCount = CountVisibleBlocksAlongOrientation(remaining, orientation);
            int total = Math.Max(1, existingCount + 1);
            float newBlockFraction = 1f / total;

            SplitNode split = new(orientation);

            if (edge is DockEdge.Top or DockEdge.Left)
            {
                split.First = movingNode;
                split.Second = remaining;
                split.SplitRatio = newBlockFraction;
            }
            else
            {
                split.First = remaining;
                split.Second = movingNode;
                split.SplitRatio = 1f - newBlockFraction;
            }

            _rootNode = split;
            MarkLayoutDirty();
        }

        private static void DrawBlocks(SpriteBatch spriteBatch)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            bool showDockingChrome = DockingModeEnabled;

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible)
                {
                    continue;
                }

                DrawBlockBackground(spriteBatch, block, dragBarHeight);
                DrawBlockContent(spriteBatch, block, dragBarHeight);
            }

            if (showDockingChrome)
            {
                DrawResizeEdges(spriteBatch);
                DrawCornerHandles(spriteBatch);
            }

            if (_draggingBlock != null)
            {
                DrawFloatingBlockPreview(spriteBatch, dragBarHeight);
            }

            if (_dropPreview.HasValue)
            {
                DrawRect(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentMuted);
                DrawRectOutline(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentColor, UIStyle.DragOutlineThickness);
            }
        }

        private static void DrawResizeEdges(SpriteBatch spriteBatch)
        {
            if (_resizeEdges.Count == 0)
            {
                return;
            }

            foreach (ResizeEdge handle in _resizeEdges)
            {
                Color color = UIStyle.ResizeEdgeColor;
                bool isActive = _activeResizeEdge.HasValue && ReferenceEquals(handle.Node, _activeResizeEdge.Value.Node);
                bool isHovered = _hoveredResizeEdge.HasValue && ReferenceEquals(handle.Node, _hoveredResizeEdge.Value.Node);

                if (!isActive && _activeCornerHandle.HasValue && CornerContainsResizeEdge(_activeCornerHandle.Value, handle))
                {
                    isActive = true;
                }

                if (!isHovered && _hoveredCornerHandle.HasValue && CornerContainsResizeEdge(_hoveredCornerHandle.Value, handle))
                {
                    isHovered = true;
                }

                if (isActive)
                {
                    color = UIStyle.ResizeEdgeActiveColor;
                }
                else if (isHovered)
                {
                    color = UIStyle.ResizeEdgeHoverColor;
                }

                DrawRect(spriteBatch, handle.Bounds, color);
            }
        }

        private static void DrawBlockBackground(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight)
        {
            PanelGroup group = GetPanelGroupForBlock(block);
            bool isTransparentBlock = block.Kind == DockBlockKind.Transparent;
            DrawRect(spriteBatch, block.Bounds, isTransparentBlock ? Core.TransparentWindowColor : UIStyle.BlockBackground);
            DrawRectOutline(spriteBatch, block.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            Rectangle groupBar = GetGroupBarBounds(block, group, dragBarHeight);
            bool showTabs = groupBar != Rectangle.Empty && (group?.Blocks.Count ?? 0) > 0;

            if (dragBarHeight > 0)
            {
                Rectangle dragBar = block.GetDragBarBounds(dragBarHeight);
                if (dragBar.Height > 0)
                {
                    DrawRect(spriteBatch, dragBar, UIStyle.DragBarBackground);
                    bool dragBarHovered = string.Equals(_hoveredDragBarId, block.Id, StringComparison.Ordinal);
                    GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockButtonBounds, out Rectangle closeButtonBounds);
                    Rectangle dragGrabBounds = GetDragBarGrabBounds(block, group, dragBarHeight);

                    if (dragBarHovered && dragGrabBounds != Rectangle.Empty)
                    {
                        DrawRect(spriteBatch, dragGrabBounds, UIStyle.DragBarHoverTint);
                        DrawDragBarGrabHint(spriteBatch, dragGrabBounds);
                    }

                    if (panelLockButtonBounds != Rectangle.Empty)
                    {
                        bool panelLocked = IsPanelLocked(group);
                        bool hovered = panelLockButtonBounds.Contains(_mousePosition);
                        DrawPanelLockToggleButton(spriteBatch, panelLockButtonBounds, panelLocked, hovered);
                    }

                    if (closeButtonBounds != Rectangle.Empty)
                    {
                        bool hovered = closeButtonBounds.Contains(_mousePosition);
                        DrawBlockCloseButton(spriteBatch, closeButtonBounds, hovered);
                    }
                }
            }

            if (showTabs)
            {
                PanelGroupBarLayout layout = GetGroupBarLayoutForGroup(group, block, dragBarHeight);
                DrawGroupBar(spriteBatch, layout, group, block);
            }
        }

        private static void DrawDragBarGrabHint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            int lineWidth = Math.Min(12, Math.Max(2, bounds.Width - 4));
            int lineHeight = 2;
            int spacing = Math.Max(2, bounds.Height / 6);
            int centerX = bounds.X + (bounds.Width / 2);
            int startX = centerX - (lineWidth / 2);
            int centerY = bounds.Y + (bounds.Height / 2);

            Color hintColor = UIStyle.AccentColor * 0.8f;
            DrawRect(spriteBatch, new Rectangle(startX, centerY - spacing - lineHeight, lineWidth, lineHeight), hintColor);
            DrawRect(spriteBatch, new Rectangle(startX, centerY + spacing, lineWidth, lineHeight), hintColor);
        }

        private static void DrawBlockCloseButton(SpriteBatch spriteBatch, Rectangle bounds, bool hovered)
        {
            Color background = hovered ? ColorPalette.CloseHoverBackground : ColorPalette.CloseBackground;
            Color border = hovered ? ColorPalette.CloseHoverBorder : ColorPalette.CloseBorder;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            const string glyph = "X";
            Vector2 glyphSize = glyphFont.MeasureString(glyph);
            Vector2 glyphPosition = new(
                bounds.X + (bounds.Width - glyphSize.X) / 2f,
                bounds.Y + (bounds.Height - glyphSize.Y) / 2f - 1f);
            Color glyphColor = hovered ? ColorPalette.CloseGlyphHover : ColorPalette.CloseGlyph;
            glyphFont.DrawString(spriteBatch, glyph, glyphPosition, glyphColor);
        }

        private static void EnsureLockIcons()
        {
            _lockedIcon = EnsureIcon(_lockedIcon, LockedIconFile);
            _unlockedIcon = EnsureIcon(_unlockedIcon, UnlockedIconFile);
        }

        private static Texture2D EnsureIcon(Texture2D icon, string fileName)
        {
            if (icon != null && !icon.IsDisposed)
            {
                return icon;
            }

            return BlockIconProvider.GetIcon(fileName);
        }

        private static void DrawLockToggleButton(SpriteBatch spriteBatch, Rectangle bounds, bool isLocked, bool hovered)
        {
            Color background = isLocked
                ? (hovered ? ColorPalette.LockLockedHoverFill : ColorPalette.LockLockedFill)
                : (hovered ? ColorPalette.LockUnlockedHoverFill : ColorPalette.LockUnlockedFill);
            Color border = isLocked ? (hovered ? UIStyle.AccentColor : UIStyle.BlockBorder) : UIStyle.AccentColor;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            EnsureLockIcons();
            Texture2D icon = isLocked ? _lockedIcon : _unlockedIcon;
            if (icon != null && !icon.IsDisposed)
            {
                DrawCenteredIcon(spriteBatch, icon, bounds, Color.White);
                return;
            }

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            string glyph = isLocked ? "L" : "U";
            Color glyphColor = Color.White;
            DrawCenteredGlyph(spriteBatch, glyphFont, glyph, bounds, glyphColor, -1f);
        }

        private static void DrawUngroupButton(SpriteBatch spriteBatch, Rectangle bounds, bool hovered)
        {
            Color background = hovered ? ColorPalette.ButtonPrimaryHover : ColorPalette.ButtonPrimary;
            Color border = hovered ? UIStyle.AccentColor : UIStyle.BlockBorder;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            DrawCenteredGlyph(spriteBatch, glyphFont, "U", bounds, UIStyle.TextColor, -1f);
        }

        private static void DrawPanelLockToggleButton(SpriteBatch spriteBatch, Rectangle bounds, bool isLocked, bool hovered)
        {
            Color accent = ColorPalette.Warning;
            float strength = isLocked ? 0.65f : 0.35f;
            if (hovered)
            {
                strength = Math.Min(1f, strength + 0.15f);
            }

            Color background = accent * strength;
            Color border = hovered ? accent : accent * 0.9f;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            EnsureLockIcons();
            Texture2D icon = isLocked ? _lockedIcon : _unlockedIcon;
            if (icon != null && !icon.IsDisposed)
            {
                DrawCenteredIcon(spriteBatch, icon, bounds, Color.White);
                return;
            }

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            string glyph = isLocked ? "L" : "U";
            Color glyphColor = Color.White;
            DrawCenteredGlyph(spriteBatch, glyphFont, glyph, bounds, glyphColor, -1f);
        }

        private static void DrawCenteredGlyph(SpriteBatch spriteBatch, UIStyle.UIFont font, string glyph, Rectangle bounds, Color color, float verticalOffset)
        {
            if (!font.IsAvailable || spriteBatch == null || string.IsNullOrEmpty(glyph) || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            Vector2 glyphSize = font.MeasureString(glyph);
            float scale = 1f;
            if (glyphSize.X > bounds.Width || glyphSize.Y > bounds.Height)
            {
                float scaleX = bounds.Width / glyphSize.X;
                float scaleY = bounds.Height / glyphSize.Y;
                scale = Math.Min(scaleX, scaleY);
            }

            Vector2 scaledSize = glyphSize * scale;
            Vector2 glyphPosition = new(
                bounds.X + (bounds.Width - scaledSize.X) / 2f,
                bounds.Y + (bounds.Height - scaledSize.Y) / 2f + (verticalOffset * scale));
            spriteBatch.DrawString(font.Font, glyph, glyphPosition, color, 0f, Vector2.Zero, font.Scale * scale, SpriteEffects.None, 0f);
        }

        private static void DrawCenteredIcon(SpriteBatch spriteBatch, Texture2D icon, Rectangle bounds, Color color)
        {
            if (spriteBatch == null || icon == null || icon.IsDisposed || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            int padding = Math.Max(2, Math.Min(bounds.Width, bounds.Height) / 6);
            int availableWidth = Math.Max(0, bounds.Width - (padding * 2));
            int availableHeight = Math.Max(0, bounds.Height - (padding * 2));
            if (availableWidth <= 0 || availableHeight <= 0 || icon.Width <= 0 || icon.Height <= 0)
            {
                return;
            }

            float scale = Math.Min(availableWidth / (float)icon.Width, availableHeight / (float)icon.Height);
            scale = Math.Min(scale, 2f);
            if (scale <= 0f)
            {
                return;
            }

            Vector2 size = new(icon.Width * scale, icon.Height * scale);
            Vector2 position = new(bounds.X + (bounds.Width - size.X) / 2f, bounds.Y + (bounds.Height - size.Y) / 2f);
            spriteBatch.Draw(icon, position, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static void DrawGroupBar(SpriteBatch spriteBatch, PanelGroupBarLayout layout, PanelGroup group, DockBlock activeBlock)
        {
            if (layout == null || group == null || activeBlock == null || layout.Tabs.Count == 0)
            {
                return;
            }

            Rectangle groupBarBounds = layout.GroupBarBounds;
            if (groupBarBounds != Rectangle.Empty)
            {
                DrawRect(spriteBatch, groupBarBounds, GroupBarBackground);
                DrawRectOutline(spriteBatch, groupBarBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
            }

            UIStyle.UIFont tabFont = UIStyle.FontTech;
            bool locked = IsBlockLocked(activeBlock);
            bool panelLocked = IsPanelLocked(group);

            foreach (TabHitRegion tab in layout.Tabs)
            {
                if (!_blocks.TryGetValue(tab.BlockId, out DockBlock tabBlock))
                {
                    continue;
                }

                bool isActive = string.Equals(group.ActiveBlockId, tab.BlockId, StringComparison.OrdinalIgnoreCase);
                bool isHovered = tab.Bounds.Contains(_mousePosition);
                bool closeHovered = tab.CloseBounds.Contains(_mousePosition);
                bool ungroupHovered = !panelLocked && tab.UngroupBounds.Contains(_mousePosition);
                bool lockHovered = !panelLocked && tab.LockBounds.Contains(_mousePosition);
                bool tabLocked = IsBlockLocked(tabBlock);

                Color background = TabInactiveBackground;
                if (isActive)
                {
                    background = TabActiveBackground;
                }
                else if (isHovered)
                {
                    background = TabHoverBackground;
                }

                Color textColor = isActive ? UIStyle.TextColor : UIStyle.MutedTextColor;

                if (locked)
                {
                    background *= 0.65f;
                    textColor = UIStyle.MutedTextColor;
                }

                DrawRect(spriteBatch, tab.Bounds, background);
                DrawRectOutline(spriteBatch, tab.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

                if (isHovered && !locked)
                {
                    int hintWidth = Math.Max(0, tab.Bounds.Width - 12);
                    if (hintWidth > 0)
                    {
                        Rectangle dragHint = new(tab.Bounds.X + 6, tab.Bounds.Y + 2, hintWidth, 2);
                        DrawRect(spriteBatch, dragHint, UIStyle.AccentColor * 0.85f);
                    }
                }

                if (isActive)
                {
                    Rectangle underline = new(tab.Bounds.X, tab.Bounds.Bottom - 2, tab.Bounds.Width, 2);
                    DrawRect(spriteBatch, underline, UIStyle.AccentColor);
                }

                if (tabFont.IsAvailable)
                {
                    float textStart = tab.Bounds.X + TabHorizontalPadding;
                    float availableWidth = tab.Bounds.Width - (TabHorizontalPadding * 2);
                    int iconStart = int.MaxValue;
                    if (tab.UngroupBounds != Rectangle.Empty)
                    {
                        iconStart = Math.Min(iconStart, tab.UngroupBounds.X);
                    }
                    if (tab.LockBounds != Rectangle.Empty)
                    {
                        iconStart = Math.Min(iconStart, tab.LockBounds.X);
                    }
                    if (tab.CloseBounds != Rectangle.Empty)
                    {
                        iconStart = Math.Min(iconStart, tab.CloseBounds.X);
                    }

                    if (iconStart != int.MaxValue)
                    {
                        availableWidth = Math.Max(0f, iconStart - TabClosePadding - textStart);
                    }
                    string label = FitTabLabel(tabFont, tabBlock.Title, availableWidth);
                    if (!string.IsNullOrEmpty(label))
                    {
                        Vector2 textSize = tabFont.MeasureString(label);
                        Vector2 textPosition = new(textStart, tab.Bounds.Y + (tab.Bounds.Height - textSize.Y) / 2f);
                        tabFont.DrawString(spriteBatch, label, textPosition, textColor);
                    }

                    if (tab.UngroupBounds != Rectangle.Empty)
                    {
                        DrawUngroupButton(spriteBatch, tab.UngroupBounds, ungroupHovered);
                    }

                    if (tab.LockBounds != Rectangle.Empty)
                    {
                        DrawLockToggleButton(spriteBatch, tab.LockBounds, tabLocked, lockHovered);
                    }

                    if (tab.CloseBounds != Rectangle.Empty)
                    {
                        if (closeHovered)
                        {
                            DrawRect(spriteBatch, tab.CloseBounds, TabCloseHoverTint);
                        }

                        string closeGlyph = "x";
                        Vector2 closeSize = tabFont.MeasureString(closeGlyph);
                        Vector2 closePosition = new(
                            tab.CloseBounds.X + (tab.CloseBounds.Width - closeSize.X) / 2f,
                            tab.CloseBounds.Y + (tab.CloseBounds.Height - closeSize.Y) / 2f);
                        Color closeColor = locked ? UIStyle.MutedTextColor : (closeHovered ? UIStyle.TextColor : UIStyle.MutedTextColor);
                        tabFont.DrawString(spriteBatch, closeGlyph, closePosition, closeColor);
                    }
                }
            }
        }

        private static string FitTabLabel(UIStyle.UIFont font, string text, float maxWidth)
        {
            if (!font.IsAvailable || string.IsNullOrWhiteSpace(text) || maxWidth <= 0f)
            {
                return string.Empty;
            }

            if (font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            float ellipsisWidth = font.MeasureString(ellipsis).X;
            if (ellipsisWidth > maxWidth)
            {
                return string.Empty;
            }

            string trimmed = text;
            while (trimmed.Length > 0 && font.MeasureString(trimmed).X + ellipsisWidth > maxWidth)
            {
                trimmed = trimmed[..^1];
            }

            return string.Concat(trimmed, ellipsis);
        }

        private static Rectangle CenterRectangle(Rectangle reference, Point center)
        {
            int width = reference.Width;
            int height = reference.Height;
            int x = center.X - (width / 2);
            int y = center.Y - (height / 2);
            return new Rectangle(x, y, width, height);
        }

        private static void DrawCornerHandles(SpriteBatch spriteBatch)
        {
            if (_cornerHandles.Count == 0)
            {
                return;
            }

            foreach (CornerHandle corner in _cornerHandles)
            {
                Color color = UIStyle.ResizeEdgeColor;
                Rectangle bounds = corner.Bounds;

                bool isActiveCorner = _activeCornerHandle.HasValue && CornerEquals(corner, _activeCornerHandle.Value);
                if (isActiveCorner)
                {
                    color = UIStyle.ResizeEdgeActiveColor;

                    if (_activeCornerSnapPosition.HasValue)
                    {
                        bounds = CenterRectangle(bounds, _activeCornerSnapPosition.Value);
                    }
                }
                else if (_hoveredCornerHandle.HasValue && CornerEquals(corner, _hoveredCornerHandle.Value))
                {
                    color = UIStyle.ResizeEdgeHoverColor;
                }

                DrawRect(spriteBatch, bounds, color);
            }
        }

        private static void DrawFloatingBlockPreview(SpriteBatch spriteBatch, int dragBarHeight)
        {
            Rectangle floating = new(_mousePosition.X - _dragOffset.X, _mousePosition.Y - _dragOffset.Y, _draggingStartBounds.Width, _draggingStartBounds.Height);
            if (floating.Width <= 0 || floating.Height <= 0)
            {
                return;
            }

            DrawRect(spriteBatch, floating, UIStyle.BlockBackground * 0.75f);

            if (dragBarHeight > 0)
            {
                int dragBarPreviewHeight = Math.Min(dragBarHeight, floating.Height);
                Rectangle dragBarPreview = new(floating.X, floating.Y, floating.Width, dragBarPreviewHeight);
                DrawRect(spriteBatch, dragBarPreview, UIStyle.DragBarBackground * 0.95f);
                DrawRectOutline(spriteBatch, dragBarPreview, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
            }

            DrawRectOutline(spriteBatch, floating, UIStyle.AccentColor * 0.8f, UIStyle.DragOutlineThickness);
        }

        private static void DrawBlockContent(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight)
        {
            Rectangle contentBounds = GetPanelContentBounds(block, dragBarHeight);

            switch (block.Kind)
            {
                case DockBlockKind.Game:
                    GameBlock.Draw(spriteBatch, contentBounds, _worldRenderTarget);
                    break;
                case DockBlockKind.Transparent:
                    TransparentBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Blank:
                    BlankBlock.Draw(spriteBatch, contentBounds, GetBlankBlockLabelOpacity(block));
                    break;
                case DockBlockKind.Properties:
                    PropertiesBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.ColorScheme:
                    ColorSchemeBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Controls:
                    ControlsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Notes:
                    NotesBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.DockingSetups:
                    DockingSetupsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Backend:
                    BackendBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Specs:
                    SpecsBlock.Draw(spriteBatch, contentBounds);
                    break;
            }
        }

        private static void UpdateBlankBlockHoverState(DockBlock block, double elapsedSeconds)
        {
            BlankBlockHoverState state = EnsureBlankBlockHoverState(block);
            if (state == null)
            {
                return;
            }

            bool hovering = block.Bounds.Contains(_mousePosition);
            bool shouldFadeOut = !hovering && (state.IsHovering || state.Animation.TargetOpacity > 0f);

            if (hovering && !state.IsHovering)
            {
                state.IsHovering = true;
                state.Animation.Begin(state.Animation.CurrentOpacity, 1f, BlankBlockFadeInDurationSeconds, true);
            }
            else if (shouldFadeOut)
            {
                state.IsHovering = false;
                double fadeOutDuration = GetBlankBlockFadeOutDuration(state);
                state.Animation.Begin(state.Animation.CurrentOpacity, 0f, fadeOutDuration, false);
            }

            state.Animation.Update(elapsedSeconds);
        }

        private static double GetBlankBlockFadeOutDuration(BlankBlockHoverState state)
        {
            if (state == null)
            {
                return BlankBlockFadeOutDurationSeconds;
            }

            bool interruptedFadeIn = state.Animation.IsFadeIn &&
                state.Animation.DurationSeconds > 0d &&
                state.Animation.ElapsedSeconds < state.Animation.DurationSeconds;

            if (interruptedFadeIn)
            {
                double interruptedDuration = state.Animation.ElapsedSeconds * BlankBlockInterruptedFadeOutScale;
                return Math.Max(BlankBlockMinimumFadeDurationSeconds, interruptedDuration);
            }

            return BlankBlockFadeOutDurationSeconds;
        }

        private static BlankBlockHoverState EnsureBlankBlockHoverState(DockBlock block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.Id))
            {
                return null;
            }

            if (!_blankBlockHoverStates.TryGetValue(block.Id, out BlankBlockHoverState state))
            {
                state = new BlankBlockHoverState();
                state.Animation.Begin(0f, 0f, 0d, false);
                _blankBlockHoverStates[block.Id] = state;
            }

            return state;
        }

        private static float GetBlankBlockLabelOpacity(DockBlock block)
        {
            if (block == null)
            {
                return 0f;
            }

            if (_blankBlockHoverStates.TryGetValue(block.Id, out BlankBlockHoverState state))
            {
                return MathHelper.Clamp(state.Animation.CurrentOpacity, 0f, 1f);
            }

            return 0f;
        }

        private static void UpdateInteractiveBlocks(GameTime gameTime, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            if (gameTime == null || _orderedBlocks.Count == 0)
            {
                return;
            }

            double elapsedSeconds = Math.Max(gameTime.ElapsedGameTime.TotalSeconds, 0d);

            if (ControlsBlock.IsRebindOverlayOpen())
            {
                ControlsBlock.UpdateRebindOverlay(gameTime, mouseState, previousMouseState, keyboardState, previousKeyboardState);
                return;
            }

            int dragBarHeight = GetActiveDragBarHeight();
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !block.IsVisible)
                {
                    continue;
                }

                Rectangle contentBounds = GetPanelContentBounds(block, dragBarHeight);
                switch (block.Kind)
                {
                    case DockBlockKind.Blank:
                        UpdateBlankBlockHoverState(block, elapsedSeconds);
                        break;
                    case DockBlockKind.Properties:
                        PropertiesBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockBlockKind.Notes:
                        NotesBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockBlockKind.ColorScheme:
                        ColorSchemeBlock.Update(gameTime, contentBounds, mouseState, previousMouseState, keyboardState, previousKeyboardState);
                        break;
                    case DockBlockKind.Controls:
                        ControlsBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockBlockKind.Backend:
                        BackendBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockBlockKind.Specs:
                        SpecsBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockBlockKind.DockingSetups:
                        DockingSetupsBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                }
            }

            if (ControlsBlock.IsRebindOverlayOpen())
            {
                ControlsBlock.UpdateRebindOverlay(gameTime, mouseState, previousMouseState, keyboardState, previousKeyboardState);
            }
        }

        private static void DrawOverlayMenu(SpriteBatch spriteBatch)
        {
            UIStyle.UIFont headerFont = UIStyle.FontH1;
            UIStyle.UIFont bodyFont = UIStyle.FontTech;

            if (!_overlayMenuVisible || !headerFont.IsAvailable || !bodyFont.IsAvailable)
            {
                return;
            }

            if (_overlayBounds == Rectangle.Empty)
            {
                BuildOverlayLayout();
            }

            DrawRect(spriteBatch, _overlayBounds, UIStyle.OverlayBackground);
            DrawRectOutline(spriteBatch, _overlayBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            string header = "Block visibility";
            Vector2 headerSize = headerFont.MeasureString(header);
            Vector2 headerPosition = new(_overlayBounds.X + (_overlayBounds.Width - headerSize.X) / 2f, _overlayBounds.Y + 12);
            headerFont.DrawString(spriteBatch, header, headerPosition, UIStyle.TextColor);
            DrawOverlayDismissButton(spriteBatch);

            int rowY = _overlayBounds.Y + 52;
            foreach (OverlayMenuRow row in _overlayRows)
            {
                BlockMenuEntry entry = row.Entry;
                if (entry == null)
                {
                    continue;
                }

                bodyFont.DrawString(spriteBatch, entry.Label, new Vector2(_overlayBounds.X + 20, rowY), UIStyle.TextColor);

                if (entry.ControlMode == BlockMenuControlMode.Toggle)
                {
                    string state = entry.IsVisible ? "Hide" : "Show";
                    bool hovered = UIButtonRenderer.IsHovered(row.ToggleBounds, _mousePosition);
                    UIButtonRenderer.ButtonStyle style = entry.IsVisible ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey;
                    UIButtonRenderer.Draw(spriteBatch, row.ToggleBounds, state, style, hovered);
                }
                else
                {
                    bool atMin = entry.Count <= entry.MinCount;
                    bool atMax = entry.Count >= entry.MaxCount;
                    DrawStepperButton(spriteBatch, row.MinusBounds, "-", atMin);
                    DrawNumberField(spriteBatch, row.InputBounds, GetNumericDisplayText(entry), entry.IsEditing);
                    DrawStepperButton(spriteBatch, row.PlusBounds, "+", atMax);
                }

                rowY += 32;
            }

            string openAllLabel = TextSpacingHelper.JoinWithWideSpacing("Open", "all");
            string closeAllLabel = TextSpacingHelper.JoinWithWideSpacing("Close", "all");
            bool openAllHovered = UIButtonRenderer.IsHovered(_overlayOpenAllBounds, _mousePosition);
            bool closeAllHovered = UIButtonRenderer.IsHovered(_overlayCloseAllBounds, _mousePosition);
            UIButtonRenderer.Draw(spriteBatch, _overlayOpenAllBounds, openAllLabel, UIButtonRenderer.ButtonStyle.Blue, openAllHovered);
            UIButtonRenderer.Draw(spriteBatch, _overlayCloseAllBounds, closeAllLabel, UIButtonRenderer.ButtonStyle.Grey, closeAllHovered);
        }

        private static void CollapseInteractions()
        {
            _overlayMenuVisible = false;
            _blockMenuSwitchState = false;
            ClearDockingInteractions();
            ResetOverlayLayout();
        }

        private static void ClearDockingInteractions()
        {
            ClearDockingDragState();
            ClearPressedTabState();
        }

        private static void ClearDockingDragState()
        {
            _draggingBlock = null;
            _draggingPanel = null;
            _draggingFromTab = false;
            _dropPreview = null;
            _hoveredDragBarId = null;
            _hoveredResizeEdge = null;
            _activeResizeEdge = null;
            ClearResizeEdgeSnap();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
            _activeCornerLinkedHandle = null;
            ClearCornerSnap();
        }

        private static void ResetOverlayLayout()
        {
            _overlayBounds = Rectangle.Empty;
            _overlayDismissBounds = Rectangle.Empty;
            _overlayOpenAllBounds = Rectangle.Empty;
            _overlayCloseAllBounds = Rectangle.Empty;
            _overlayRows.Clear();
            ClearOverlayEditingState();
        }

        private static void ClearCornerSnap()
        {
            _activeCornerSnapTarget = null;
            _activeCornerSnapPosition = null;
            _activeCornerSnapAnchor = null;
            _activeCornerSnapLockX = false;
            _activeCornerSnapLockY = false;
        }

        private static void ClearResizeEdgeSnap()
        {
            _activeResizeEdgeSnapTarget = null;
            _activeResizeEdgeSnapCoordinate = null;
        }

        private static void DrawOverlayDismissButton(SpriteBatch spriteBatch)
        {
            if (_overlayDismissBounds == Rectangle.Empty)
            {
                return;
            }

            bool hovered = UIButtonRenderer.IsHovered(_overlayDismissBounds, _mousePosition);
            Color background = ColorPalette.CloseOverlayBackground;
            Color hoverBackground = ColorPalette.CloseOverlayHoverBackground;
            Color border = ColorPalette.CloseOverlayBorder;
            UIButtonRenderer.Draw(
                spriteBatch,
                _overlayDismissBounds,
                "X",
                UIButtonRenderer.ButtonStyle.Grey,
                hovered,
                isDisabled: false,
                textColorOverride: hovered ? ColorPalette.CloseGlyphHover : ColorPalette.CloseGlyph,
                fillOverride: background,
                hoverFillOverride: hoverBackground,
                borderOverride: border);
        }

        private static void DrawStepperButton(SpriteBatch spriteBatch, Rectangle bounds, string label, bool disabled)
        {
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            bool hovered = UIButtonRenderer.IsHovered(bounds, _mousePosition);
            UIButtonRenderer.Draw(spriteBatch, bounds, label, UIButtonRenderer.ButtonStyle.Grey, hovered, disabled);
        }

        private static void DrawNumberField(SpriteBatch spriteBatch, Rectangle bounds, string text, bool isActive)
        {
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontBody;
            if (!font.IsAvailable)
            {
                return;
            }

            Color border = isActive ? UIStyle.AccentColor : UIStyle.BlockBorder;
            DrawRect(spriteBatch, bounds, UIStyle.BlockBackground);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            text ??= string.Empty;
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPosition = new(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f);
            font.DrawString(spriteBatch, text, textPosition, UIStyle.TextColor);
        }

        private static string GetNumericDisplayText(BlockMenuEntry entry)
        {
            if (entry == null)
            {
                return "0";
            }

            if (string.IsNullOrWhiteSpace(entry.InputBuffer))
            {
                return entry.Count.ToString();
            }

            return entry.InputBuffer;
        }

        private static void UpdateOverlayInteractions(bool leftClickStarted)
        {
            BuildOverlayLayout();

            if (!leftClickStarted)
            {
                return;
            }

            if (_overlayDismissBounds.Contains(_mousePosition))
            {
                CloseOverlayMenuFromUi();
                return;
            }

            if (_overlayOpenAllBounds.Contains(_mousePosition))
            {
                SetAllBlocksVisibility(true);
                return;
            }

            if (_overlayCloseAllBounds.Contains(_mousePosition))
            {
                SetAllBlocksVisibility(false);
                return;
            }

            foreach (OverlayMenuRow row in _overlayRows)
            {
                BlockMenuEntry entry = row.Entry;
                if (entry == null)
                {
                    continue;
                }

                if (entry.ControlMode == BlockMenuControlMode.Toggle)
                {
                    if (row.ToggleBounds.Contains(_mousePosition))
                    {
                        entry.IsVisible = !entry.IsVisible;
                        ApplyToggleVisibility(entry);
                        ResetOverlayLayout();
                        return;
                    }
                }
                else
                {
                    if (row.MinusBounds.Contains(_mousePosition))
                    {
                        SetActiveNumericEntry(entry);
                        AdjustNumericEntry(entry, entry.Count - 1);
                        return;
                    }

                    if (row.PlusBounds.Contains(_mousePosition))
                    {
                        SetActiveNumericEntry(entry);
                        AdjustNumericEntry(entry, entry.Count + 1);
                        return;
                    }

                    if (row.InputBounds.Contains(_mousePosition))
                    {
                        SetActiveNumericEntry(entry);
                        return;
                    }
                }
            }

            ClearActiveNumericEntry();
        }

        private static void RebuildBlocksFromMenuChange()
        {
            _blockDefinitionsReady = false;
            EnsureBlocks();
            MarkLayoutDirty();
        }

        private static void ApplyToggleVisibility(BlockMenuEntry entry)
        {
            if (entry == null || entry.ControlMode != BlockMenuControlMode.Toggle)
            {
                return;
            }

            if (_blocks.TryGetValue(entry.IdPrefix, out DockBlock block))
            {
                block.IsVisible = entry.IsVisible;
                if (entry.IsVisible)
                {
                    EnsureBlockAttachedToLayout(block);
                }
            }

            MarkLayoutDirty();
        }

        private static void EnsureBlockAttachedToLayout(DockBlock block)
        {
            if (block == null)
            {
                return;
            }

            PanelGroup group = GetPanelGroupForBlock(block);
            if (group == null)
            {
                return;
            }

            if (!string.Equals(group.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
            {
                SetPanelActiveBlock(group, block);
            }

            BlockNode panelNode = GetPanelNode(group);
            if (panelNode == null || LayoutContainsNode(_rootNode, panelNode))
            {
                return;
            }

            AttachPanelToLayout(panelNode);
        }

        private static void EnsureVisibleBlocksAttachedToLayout()
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block != null && block.IsVisible)
                {
                    EnsureBlockAttachedToLayout(block);
                }
            }
        }

        private static void AttachPanelToLayout(BlockNode panelNode)
        {
            if (panelNode == null)
            {
                return;
            }

            if (_rootNode == null)
            {
                _rootNode = panelNode;
                MarkLayoutDirty();
                return;
            }

            BlockNode reference = FindFirstVisibleBlockNode(_rootNode) ?? FindFirstBlockNode(_rootNode);
            if (reference == null || ReferenceEquals(reference, panelNode))
            {
                _rootNode = panelNode;
                MarkLayoutDirty();
                return;
            }

            _rootNode = DockLayout.InsertRelative(_rootNode, panelNode, reference, DockEdge.Right);
            MarkLayoutDirty();
        }

        private static bool LayoutContainsNode(DockNode root, DockNode target)
        {
            if (root == null || target == null)
            {
                return false;
            }

            if (ReferenceEquals(root, target))
            {
                return true;
            }

            if (root is SplitNode split)
            {
                return LayoutContainsNode(split.First, target) || LayoutContainsNode(split.Second, target);
            }

            return false;
        }

        private static DockNode ReplaceNode(DockNode root, BlockNode target, DockNode replacement)
        {
            if (root == null || target == null)
            {
                return root;
            }

            if (ReferenceEquals(root, target))
            {
                return replacement;
            }

            if (root is SplitNode split)
            {
                split.First = ReplaceNode(split.First, target, replacement);
                split.Second = ReplaceNode(split.Second, target, replacement);
            }

            return root;
        }

        private static BlockNode FindFirstVisibleBlockNode(DockNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is BlockNode blockNode)
            {
                DockBlock block = blockNode.Block;
                if (block != null && block.IsVisible)
                {
                    return blockNode;
                }
            }

            if (node is SplitNode split)
            {
                return FindFirstVisibleBlockNode(split.First) ?? FindFirstVisibleBlockNode(split.Second);
            }

            return null;
        }

        private static BlockNode FindFirstBlockNode(DockNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is BlockNode blockNode)
            {
                return blockNode;
            }

            if (node is SplitNode split)
            {
                return FindFirstBlockNode(split.First) ?? FindFirstBlockNode(split.Second);
            }

            return null;
        }

        private static void AdjustNumericEntry(BlockMenuEntry entry, int newValue)
        {
            if (entry == null || entry.ControlMode != BlockMenuControlMode.Count)
            {
                return;
            }

            int clamped = ClampCount(entry, newValue);
            if (entry.Count == clamped)
            {
                entry.InputBuffer = entry.Count.ToString();
                return;
            }

            entry.Count = clamped;
            entry.InputBuffer = entry.Count.ToString();
            _blockMenuDirty = true;
            RebuildBlocksFromMenuChange();
        }

        private static void SetActiveNumericEntry(BlockMenuEntry entry)
        {
            if (entry == null || entry.ControlMode != BlockMenuControlMode.Count)
            {
                return;
            }

            if (!ReferenceEquals(_activeNumericEntry, entry))
            {
                ClearOverlayEditingState();
                _activeNumericEntry = entry;
            }

            entry.IsEditing = true;
            entry.InputBuffer = string.IsNullOrWhiteSpace(entry.InputBuffer) ? entry.Count.ToString() : entry.InputBuffer;
            OverlayInputRepeater.Reset();
        }

        private static void ClearActiveNumericEntry()
        {
            if (_activeNumericEntry != null)
            {
                _activeNumericEntry.IsEditing = false;
                _activeNumericEntry.InputBuffer = _activeNumericEntry.Count.ToString();
            }

            _activeNumericEntry = null;
            OverlayInputRepeater.Reset();
        }

        private static void ClearOverlayEditingState()
        {
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                entry.IsEditing = false;
                if (entry.ControlMode == BlockMenuControlMode.Count)
                {
                    entry.InputBuffer = entry.Count.ToString();
                }
            }

            _activeNumericEntry = null;
            OverlayInputRepeater.Reset();
        }

        private static void UpdateOverlayKeyboardInput(KeyboardState keyboardState, double elapsedSeconds)
        {
            if (!_overlayMenuVisible || _activeNumericEntry == null)
            {
                OverlayInputRepeater.Reset();
                return;
            }

            BlockMenuEntry entry = _activeNumericEntry;
            bool changed = false;

            foreach (Keys key in OverlayInputRepeater.GetKeysWithRepeat(keyboardState, _previousKeyboardState, elapsedSeconds))
            {
                switch (key)
                {
                    case Keys.Back:
                        if (!string.IsNullOrEmpty(entry.InputBuffer))
                        {
                            entry.InputBuffer = entry.InputBuffer[..^1];
                        }
                        else
                        {
                            entry.InputBuffer = string.Empty;
                        }

                        changed = true;
                        break;
                    case Keys.Delete:
                        entry.InputBuffer = string.Empty;
                        changed = true;
                        break;
                    case Keys.Enter:
                        ApplyNumericBuffer(entry);
                        ClearActiveNumericEntry();
                        OverlayInputRepeater.Reset();
                        return;
                    case Keys.Escape:
                        entry.InputBuffer = entry.Count.ToString();
                        ClearActiveNumericEntry();
                        OverlayInputRepeater.Reset();
                        return;
                    default:
                        if ((key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.NumPad0 && key <= Keys.NumPad9))
                        {
                            int digit = key >= Keys.D0 && key <= Keys.D9 ? key - Keys.D0 : key - Keys.NumPad0;
                            AppendDigit(entry, digit);
                            changed = true;
                        }
                        break;
                }
            }

            if (changed)
            {
                ApplyNumericBuffer(entry);
            }
        }

        private static void AppendDigit(BlockMenuEntry entry, int digit)
        {
            if (entry == null || entry.ControlMode != BlockMenuControlMode.Count)
            {
                return;
            }

            entry.InputBuffer ??= string.Empty;
            if (entry.InputBuffer.Length >= 2)
            {
                return;
            }

            entry.InputBuffer += digit.ToString();
        }

        private static void ApplyNumericBuffer(BlockMenuEntry entry)
        {
            if (entry == null || entry.ControlMode != BlockMenuControlMode.Count)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.InputBuffer))
            {
                return;
            }

            if (!int.TryParse(entry.InputBuffer, out int parsed))
            {
                return;
            }

            int clamped = ClampCount(entry, parsed);
            if (clamped != parsed)
            {
                entry.InputBuffer = clamped.ToString();
            }

            if (entry.Count != clamped)
            {
                entry.Count = clamped;
                _blockMenuDirty = true;
                RebuildBlocksFromMenuChange();
            }
        }

        private static void CloseOverlayMenuFromUi()
        {
            _overlayMenuVisible = false;
            bool overrideApplied = InputTypeManager.OverrideSwitchState(BlockMenuControlKey, false);
            if (!overrideApplied && ControlStateManager.ContainsSwitchState(BlockMenuControlKey))
            {
                ControlStateManager.SetSwitchState(BlockMenuControlKey, false);
            }
            _blockMenuSwitchState = false;

            _overlayBounds = Rectangle.Empty;
            _overlayDismissBounds = Rectangle.Empty;
            _overlayOpenAllBounds = Rectangle.Empty;
            _overlayCloseAllBounds = Rectangle.Empty;
            _overlayRows.Clear();
            ClearOverlayEditingState();
        }

        private static bool GetBlockMenuState()
        {
            bool liveState = InputManager.IsInputActive(BlockMenuControlKey);
            if (ControlStateManager.ContainsSwitchState(BlockMenuControlKey))
            {
                bool cachedState = ControlStateManager.GetSwitchState(BlockMenuControlKey);
                return liveState || cachedState;
            }

            return liveState;
        }

        private static void BuildOverlayLayout()
        {
            EnsureBlockMenuEntries();
            Rectangle viewport = Core.Instance?.GraphicsDevice?.Viewport.Bounds ?? new Rectangle(0, 0, 1280, 720);
            int width = 360;
            int height = 120 + (_blockMenuEntries.Count * 32);
            _overlayBounds = new Rectangle(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
            int closeButtonSize = 24;
            _overlayDismissBounds = new Rectangle(_overlayBounds.Right - closeButtonSize - 12, _overlayBounds.Y + 12, closeButtonSize, closeButtonSize);

            _overlayRows.Clear();
            int rowY = _overlayBounds.Y + 52;
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                Rectangle toggleBounds = Rectangle.Empty;
                Rectangle minusBounds = Rectangle.Empty;
                Rectangle inputBounds = Rectangle.Empty;
                Rectangle plusBounds = Rectangle.Empty;

                if (entry.ControlMode == BlockMenuControlMode.Toggle)
                {
                    toggleBounds = new Rectangle(_overlayBounds.Right - 96, rowY - 4, 76, 28);
                }
                else
                {
                    const int stepWidth = 28;
                    const int inputWidth = 52;
                    int controlHeight = 28;
                    int totalWidth = (stepWidth * 2) + inputWidth;
                    int startX = _overlayBounds.Right - 20 - totalWidth;
                    int controlY = rowY - 4;

                    minusBounds = new Rectangle(startX, controlY, stepWidth, controlHeight);
                    inputBounds = new Rectangle(minusBounds.Right, controlY, inputWidth, controlHeight);
                    plusBounds = new Rectangle(inputBounds.Right, controlY, stepWidth, controlHeight);
                }

                _overlayRows.Add(new OverlayMenuRow(entry, toggleBounds, minusBounds, inputBounds, plusBounds));
                rowY += 32;
            }

            int buttonWidth = (width - 60) / 2;
            _overlayOpenAllBounds = new Rectangle(_overlayBounds.X + 20, _overlayBounds.Bottom - 44, buttonWidth, 32);
            _overlayCloseAllBounds = new Rectangle(_overlayOpenAllBounds.Right + 20, _overlayBounds.Bottom - 44, buttonWidth, 32);
        }

        private static void DrawEmptyState(SpriteBatch spriteBatch, Rectangle viewport)
        {
            UIStyle.UIFont font = UIStyle.FontHBody;
            if (!font.IsAvailable)
            {
                return;
            }

            string label = GetBlockHotkeyLabel();
            string message = BuildWideSpacedSentence(label);
            Vector2 size = font.MeasureString(message);
            Vector2 position = new(viewport.X + (viewport.Width - size.X) / 2f, viewport.Y + (viewport.Height - size.Y) / 2f);
            font.DrawString(spriteBatch, message, position, UIStyle.MutedTextColor);
        }

        private static string BuildWideSpacedSentence(string label)
        {
            return TextSpacingHelper.JoinWithWideSpacing("Press", label, "to", "open", "blocks");
        }

        private static Rectangle GetLayoutBounds(Rectangle viewport)
        {
            // No outer padding: blocks should touch the window edges.
            return viewport;
        }

        private static int GetActiveDragBarHeight()
        {
            return DockingModeEnabled ? UIStyle.DragBarHeight : 0;
        }

        private static bool AnyBlockVisible() => _orderedBlocks.Any(block => block.IsVisible);

        public static bool IsBlockMenuOpen() => _overlayMenuVisible;

        public static bool IsInputBlocked() => _overlayMenuVisible || ControlsBlock.IsRebindOverlayOpen() || ColorSchemeBlock.IsEditorOpen;

        private static bool ShouldLockPanelInteractions()
        {
            if (!ControlStateManager.ContainsSwitchState(AllowGameInputFreezeKey))
            {
                return false;
            }

            return !ControlStateManager.GetSwitchState(AllowGameInputFreezeKey);
        }

        public static bool IsCursorWithinGameBlock()
        {
            if (Core.Instance == null)
            {
                return true;
            }

            if (!Core.Instance.IsActive)
            {
                return false;
            }

            Rectangle bounds = GetCurrentGameContentBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                GraphicsDevice graphicsDevice = Core.Instance.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return true;
                }

                bounds = graphicsDevice.Viewport.Bounds;
            }

            Point cursor = Mouse.GetState().Position;
            return bounds.Contains(cursor);
        }

        private static string GetBlockHotkeyLabel()
        {
            string label = InputManager.GetBindingDisplayLabel(BlockMenuControlKey);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = "Shift + X";
            }

            return label;
        }

        private static void SetAllBlocksVisibility(bool value)
        {
            EnsureBlockMenuEntries();
            bool definitionsChanged = false;
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                if (entry.ControlMode == BlockMenuControlMode.Toggle)
                {
                    entry.IsVisible = value;
                    ApplyToggleVisibility(entry);
                }
                else
                {
                    int target = value
                        ? Math.Max(entry.Count == 0 ? 1 : entry.Count, Math.Max(entry.MinCount, 1))
                        : entry.MinCount;
                    target = ClampCount(entry, target);

                    if (entry.Count != target)
                    {
                        entry.Count = target;
                        entry.InputBuffer = entry.Count.ToString();
                        definitionsChanged = true;
                    }
                }
            }

            if (definitionsChanged)
            {
                _blockMenuDirty = true;
                RebuildBlocksFromMenuChange();
            }
        }

        private static void UpdateLayoutCache()
        {
            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                ClearResizeEdges();
                return;
            }

            Rectangle viewport = graphicsDevice.Viewport.Bounds;
            if (viewport != _cachedViewportBounds)
            {
                _cachedViewportBounds = viewport;
                _layoutDirty = true;
            }

            EnsureVisibleBlocksAttachedToLayout();

            if (!_layoutDirty)
            {
                if (!DockingModeEnabled)
                {
                    ClearResizeEdges();
                }

                return;
            }

            _layoutBounds = GetLayoutBounds(viewport);
            _rootNode?.Arrange(_layoutBounds);
            LogLayoutIntegrityIssues();

            if (DockingModeEnabled)
            {
                RebuildResizeEdges();
            }
            else
            {
                ClearResizeEdges();
            }

            _gameContentBounds = Rectangle.Empty;
            int dragBarHeight = GetActiveDragBarHeight();
            if (TryGetGameBlock(out DockBlock gameBlock) && gameBlock.IsVisible)
            {
                PanelGroup gamePanel = GetPanelGroupForBlock(gameBlock);
                int groupBarHeight = GetGroupBarHeight(gamePanel);
                _gameContentBounds = gameBlock.GetContentBounds(dragBarHeight, UIStyle.BlockPadding, groupBarHeight);
            }

            _layoutDirty = false;
        }

        private static void LogLayoutIntegrityIssues()
        {
            bool anyVisibleBlocks = AnyBlockVisible();
            if (_rootNode == null)
            {
                if (anyVisibleBlocks)
                {
                    LogLayoutIssue("root-missing", $"[DockLayout] Layout root missing while {CountVisibleBlocks()} block(s) are marked visible. LayoutBounds={_layoutBounds}");
                }
                else
                {
                    ClearLayoutIssue("root-missing");
                }

                return;
            }

            ClearLayoutIssue("root-missing");

            HashSet<string> visibleInLayout = new(StringComparer.OrdinalIgnoreCase);
            CollectVisibleBlocks(_rootNode, visibleInLayout);

            string visibilityMismatch = ValidateRenderedBlocksAgainstHiddenState(visibleInLayout);
            if (string.IsNullOrEmpty(visibilityMismatch))
            {
                ClearLayoutIssue("visibility-mismatch");
            }
            else
            {
                LogLayoutIssue("visibility-mismatch", visibilityMismatch);
            }

            if (anyVisibleBlocks && visibleInLayout.Count == 0)
            {
                LogLayoutIssue("layout-empty", $"[DockLayout] Layout arrange produced zero visible nodes. Root={DescribeNode(_rootNode)} LayoutBounds={_layoutBounds}");
            }
            else
            {
                ClearLayoutIssue("layout-empty");
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null)
                {
                    continue;
                }

                string missingKey = $"missing:{block.Id}";
                string zeroKey = $"zero:{block.Id}";

                if (!block.IsVisible)
                {
                    ClearLayoutIssue(missingKey);
                    ClearLayoutIssue(zeroKey);
                    continue;
                }

                bool inLayout = visibleInLayout.Contains(block.Id);
                if (!inLayout)
                {
                    LogLayoutIssue(missingKey, $"[DockLayout] Visible block '{block.Id}' is not attached to the layout tree. Panel={DescribePanelForBlock(block)} Bounds={block.Bounds} LayoutBounds={_layoutBounds}");
                    ClearLayoutIssue(zeroKey);
                    continue;
                }

                ClearLayoutIssue(missingKey);

                if (block.Bounds.Width <= 0 || block.Bounds.Height <= 0)
                {
                    LogLayoutIssue(zeroKey, $"[DockLayout] Visible block '{block.Id}' arranged with zero-area bounds {block.Bounds}. Panel={DescribePanelForBlock(block)} LayoutBounds={_layoutBounds}");
                }
                else
                {
                    ClearLayoutIssue(zeroKey);
                }
            }
        }

        private static string ValidateRenderedBlocksAgainstHiddenState(ISet<string> visibleInLayout)
        {
            if (_orderedBlocks.Count == 0)
            {
                return null;
            }

            HashSet<string> layoutVisibleIds = visibleInLayout != null
                ? new HashSet<string>(visibleInLayout, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> intendedVisibleIds = new(_orderedBlocks.Where(block => block != null && block.IsVisible).Select(block => block.Id), StringComparer.OrdinalIgnoreCase);

            List<string> missingFromLayout = intendedVisibleIds.Where(id => !layoutVisibleIds.Contains(id)).ToList();
            List<string> unexpectedlyVisible = layoutVisibleIds.Where(id => !intendedVisibleIds.Contains(id)).ToList();
            List<string> hiddenWithBounds = _orderedBlocks
                .Where(block => block != null && !block.IsVisible && block.Bounds != Rectangle.Empty)
                .Select(block => $"{block.Id} bounds={block.Bounds}")
                .ToList();

            bool mismatch = layoutVisibleIds.Count != intendedVisibleIds.Count ||
                missingFromLayout.Count > 0 ||
                unexpectedlyVisible.Count > 0;

            if (!mismatch)
            {
                return null;
            }

            List<string> debugLines = new()
            {
                $"[DockLayout] Visibility summary: renderedInLayout={layoutVisibleIds.Count}, blocksMarkedVisible={intendedVisibleIds.Count}"
            };

            if (missingFromLayout.Count > 0)
            {
                debugLines.Add($"Marked visible but missing from layout: {string.Join(", ", missingFromLayout)}");
            }

            if (unexpectedlyVisible.Count > 0)
            {
                debugLines.Add($"Rendered in layout despite hidden flag: {string.Join(", ", unexpectedlyVisible)}");
            }

            if (hiddenWithBounds.Count > 0)
            {
                debugLines.Add($"Hidden blocks still have bounds assigned: {string.Join("; ", hiddenWithBounds)}");
            }

            debugLines.Add("Block visibility breakdown:");
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null)
                {
                    continue;
                }

                bool layoutVisible = layoutVisibleIds.Contains(block.Id);
                bool nodeInLayout = _blockNodes.TryGetValue(block.Id, out BlockNode node) && LayoutContainsNode(_rootNode, node);
                string panelId = _blockToPanel.TryGetValue(block.Id, out string mappedPanel) ? mappedPanel : "unmapped";
                string bounds = block.Bounds == Rectangle.Empty ? "empty" : block.Bounds.ToString();

                debugLines.Add($" - {block.Id} ({block.Kind}): isVisible={block.IsVisible}, layoutVisible={layoutVisible}, nodeInLayout={nodeInLayout}, panel={panelId}, bounds={bounds}");
            }

            foreach (string line in debugLines)
            {
                DebugLogger.PrintDebug(line);
            }

            return $"[DockLayout] Rendered block set does not match block hidden flags (renderedInLayout={layoutVisibleIds.Count}, markedVisible={intendedVisibleIds.Count}).";
        }

        private static void CollectVisibleBlocks(DockNode node, HashSet<string> visibleIds)
        {
            if (node == null || visibleIds == null)
            {
                return;
            }

            if (node is BlockNode blockNode)
            {
                DockBlock block = blockNode.Block;
                if (block != null && block.IsVisible)
                {
                    visibleIds.Add(block.Id);
                }

                return;
            }

            if (node is SplitNode split)
            {
                CollectVisibleBlocks(split.First, visibleIds);
                CollectVisibleBlocks(split.Second, visibleIds);
            }
        }

        private static void LogLayoutIssue(string key, string message)
        {
            if (_loggedLayoutIssues.Add(key))
            {
                DebugLogger.PrintError(message);
            }
        }

        private static void ClearLayoutIssue(string key)
        {
            _loggedLayoutIssues.Remove(key);
        }

        private static int CountVisibleBlocks()
        {
            int count = 0;
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block != null && block.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private static string DescribePanelForBlock(DockBlock block)
        {
            if (block == null)
            {
                return "null";
            }

            if (_blockToPanel.TryGetValue(block.Id, out string panelId) && !string.IsNullOrWhiteSpace(panelId))
            {
                return panelId;
            }

            return "unmapped";
        }

        private static void MarkLayoutDirty()
        {
            _layoutDirty = true;
        }

        private static void RebuildResizeEdges()
        {
            if (_rootNode == null || !AnyBlockVisible())
            {
                ClearResizeEdges();
                return;
            }

            _resizeEdges.Clear();
            CollectResizeEdges(_rootNode, 0);
            RebuildCornerHandles();

            if (_hoveredResizeEdge.HasValue)
            {
                _hoveredResizeEdge = FindResizeEdgeForNode(_hoveredResizeEdge.Value.Node);
            }

            if (_activeResizeEdge.HasValue)
            {
                _activeResizeEdge = FindResizeEdgeForNode(_activeResizeEdge.Value.Node);
            }

            if (_activeResizeEdgeSnapTarget.HasValue)
            {
                ResizeEdge? refreshed = FindResizeEdgeForNode(_activeResizeEdgeSnapTarget.Value.Node);
                if (refreshed.HasValue && refreshed.Value.Orientation == _activeResizeEdgeSnapTarget.Value.Orientation)
                {
                    _activeResizeEdgeSnapTarget = refreshed;
                    _activeResizeEdgeSnapCoordinate = GetResizeEdgeAxisCenter(refreshed.Value);
                }
                else
                {
                    ClearResizeEdgeSnap();
                }
            }

            if (_hoveredCornerHandle.HasValue)
            {
                _hoveredCornerHandle = FindCornerHandle(_hoveredCornerHandle.Value);
            }

            if (_activeCornerHandle.HasValue)
            {
                _activeCornerHandle = FindCornerHandle(_activeCornerHandle.Value);
            }

            if (_activeCornerLinkedHandle.HasValue)
            {
                _activeCornerLinkedHandle = FindCornerHandle(_activeCornerLinkedHandle.Value);
            }

            if (_activeCornerSnapTarget.HasValue)
            {
                CornerHandle? refreshed = FindCornerHandle(_activeCornerSnapTarget.Value);
                if (refreshed.HasValue)
                {
                    _activeCornerSnapTarget = refreshed;
                    Point snapPoint = GetCornerIntersection(refreshed.Value);
                    if (_activeCornerSnapPosition.HasValue)
                    {
                        Point stored = _activeCornerSnapPosition.Value;
                        if (_activeCornerSnapLockX)
                        {
                            stored.X = snapPoint.X;
                        }

                        if (_activeCornerSnapLockY)
                        {
                            stored.Y = snapPoint.Y;
                        }

                        _activeCornerSnapPosition = stored;
                    }

                    if (_activeCornerSnapAnchor.HasValue)
                    {
                        Point anchor = _activeCornerSnapAnchor.Value;
                        if (_activeCornerSnapLockX)
                        {
                            anchor.X = snapPoint.X;
                        }

                        if (_activeCornerSnapLockY)
                        {
                            anchor.Y = snapPoint.Y;
                        }

                        _activeCornerSnapAnchor = anchor;
                    }
                }
                else
                {
                    ClearCornerSnap();
                }
            }
        }

        private static void CollectResizeEdges(DockNode node, int depth)
        {
            if (node is not SplitNode split)
            {
                return;
            }

            bool firstVisible = split.First?.HasVisibleContent ?? false;
            bool secondVisible = split.Second?.HasVisibleContent ?? false;

            if (firstVisible && secondVisible)
            {
                Rectangle handleBounds = GetResizeEdgeBounds(split);
                if (handleBounds.Width > 0 && handleBounds.Height > 0)
                {
                    _resizeEdges.Add(new ResizeEdge(split, split.Orientation, handleBounds, depth));
                }
            }

            if (split.First != null)
            {
                CollectResizeEdges(split.First, depth + 1);
            }

            if (split.Second != null)
            {
                CollectResizeEdges(split.Second, depth + 1);
            }
        }

        private static void RebuildCornerHandles()
        {
            _cornerHandles.Clear();
            if (_resizeEdges.Count == 0)
            {
                return;
            }

            // Keep corner targets tight to the actual intersection so dragging a nearby edge
            // doesn't accidentally activate a combined corner resize.
            int inflate = 0;

            foreach (ResizeEdge vertical in _resizeEdges)
            {
                if (vertical.Orientation != DockSplitOrientation.Vertical)
                {
                    continue;
                }

                foreach (ResizeEdge horizontal in _resizeEdges)
                {
                    if (horizontal.Orientation != DockSplitOrientation.Horizontal)
                    {
                        continue;
                    }

                    Rectangle overlap = Rectangle.Intersect(vertical.Bounds, horizontal.Bounds);
                    if (overlap.Width <= 0 || overlap.Height <= 0)
                    {
                        continue;
                    }

                    Rectangle bounds = overlap;
                    bounds.Inflate(inflate, inflate);
                    _cornerHandles.Add(new CornerHandle(vertical, horizontal, bounds));
                }
            }
        }

        private static Rectangle GetResizeEdgeBounds(SplitNode split)
        {
            if (split == null)
            {
                return Rectangle.Empty;
            }

            Rectangle bounds = split.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0 || split.First == null || split.Second == null)
            {
                return Rectangle.Empty;
            }

            int thickness = Math.Max(2, UIStyle.ResizeEdgeThickness);
            if (split.Orientation == DockSplitOrientation.Vertical)
            {
                int centerX = split.First.Bounds.Right;
                int minX = bounds.X;
                int maxX = Math.Max(bounds.X, bounds.Right - thickness);
                int x = Math.Clamp(centerX - thickness / 2, minX, maxX);
                return new Rectangle(x, bounds.Y, thickness, bounds.Height);
            }
            else
            {
                int centerY = split.First.Bounds.Bottom;
                int minY = bounds.Y;
                int maxY = Math.Max(bounds.Y, bounds.Bottom - thickness);
                int y = Math.Clamp(centerY - thickness / 2, minY, maxY);
                return new Rectangle(bounds.X, y, bounds.Width, thickness);
            }
        }

        private static ResizeEdge? FindResizeEdgeForNode(SplitNode node)
        {
            if (node == null)
            {
                return null;
            }

            foreach (ResizeEdge handle in _resizeEdges)
            {
                if (ReferenceEquals(handle.Node, node))
                {
                    return handle;
                }
            }

            return null;
        }

        private static CornerHandle? FindCornerHandle(CornerHandle corner)
        {
            foreach (CornerHandle handle in _cornerHandles)
            {
                if (CornerEquals(handle, corner))
                {
                    return handle;
                }
            }

            return null;
        }

        private static bool CornerEquals(CornerHandle a, CornerHandle b)
        {
            return ReferenceEquals(a.VerticalHandle.Node, b.VerticalHandle.Node) &&
                   ReferenceEquals(a.HorizontalHandle.Node, b.HorizontalHandle.Node);
        }

        private static CornerHandle? FindAlignedCorner(CornerHandle corner)
        {
            Point intersection = GetCornerIntersection(corner);
            foreach (CornerHandle other in _cornerHandles)
            {
                if (CornerEquals(other, corner))
                {
                    continue;
                }

                Point otherIntersection = GetCornerIntersection(other);
                if (DistanceSquared(intersection, otherIntersection) == 0)
                {
                    return other;
                }
            }

            return null;
        }

        private static Point GetCornerIntersection(CornerHandle corner)
        {
            return new Point(
                corner.VerticalHandle.Bounds.Center.X,
                corner.HorizontalHandle.Bounds.Center.Y);
        }

        private static bool CornerContainsResizeEdge(CornerHandle corner, ResizeEdge handle)
        {
            return ReferenceEquals(corner.VerticalHandle.Node, handle.Node) ||
                   ReferenceEquals(corner.HorizontalHandle.Node, handle.Node);
        }

        private static bool ResizeEdgesEqual(ResizeEdge a, ResizeEdge b)
        {
            return ReferenceEquals(a.Node, b.Node) && a.Orientation == b.Orientation;
        }

        private static void CaptureBlockBoundsForResize()
        {
            _resizeStartBlockBounds = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block != null)
                {
                    _resizeStartBlockBounds[block.Id] = block.Bounds;
                }
            }
        }

        private static void LogResizeBlockDeltas()
        {
            if (_resizeStartBlockBounds == null || _resizeStartBlockBounds.Count == 0)
            {
                return;
            }

            List<string> changes = new();
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null)
                {
                    continue;
                }

                Rectangle start = _resizeStartBlockBounds.TryGetValue(block.Id, out Rectangle value) ? value : Rectangle.Empty;
                Rectangle end = block.Bounds;
                if (start == end)
                {
                    continue;
                }

                changes.Add($"{block.Title}: {start} -> {end}");
            }

            if (changes.Count > 0)
            {
                DebugLogger.PrintUI("[ResizeLayoutDelta] " + string.Join(" | ", changes));
            }

            _resizeStartBlockBounds = null;
        }

        private static string DescribeNode(DockNode node)
        {
            if (node is BlockNode blockNode)
            {
                string id = blockNode.Block?.Id ?? "null";
                Rectangle bounds = blockNode.Block?.Bounds ?? blockNode.Bounds;
                return $"Block(id={id}, bounds={bounds})";
            }

            if (node is SplitNode split)
            {
                return $"Split({split.Orientation}, ratio={split.SplitRatio:F3}, bounds={split.Bounds})";
            }

            return node?.GetType()?.Name ?? "null";
        }

        private static string DescribeResizeEdge(ResizeEdge edge)
        {
            return $"Edge[{edge.Orientation}] Depth={edge.Depth} Bounds={edge.Bounds} Node={DescribeNode(edge.Node)} First={DescribeNode(edge.Node?.First)} Second={DescribeNode(edge.Node?.Second)}";
        }

        private static string DescribeCornerHandle(CornerHandle corner)
        {
            return $"Corner V={DescribeResizeEdge(corner.VerticalHandle)} H={DescribeResizeEdge(corner.HorizontalHandle)} Bounds={corner.Bounds}";
        }

        private static void ClearResizeEdges()
        {
            _resizeEdges.Clear();
            _hoveredResizeEdge = null;
            _activeResizeEdge = null;
            ClearResizeEdgeSnap();
            _cornerHandles.Clear();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
            _activeCornerLinkedHandle = null;
            ClearCornerSnap();
            _hoveredDragBarId = null;
            _resizeStartBlockBounds = null;
        }

        private static bool TryGetGameBlock(out DockBlock block)
        {
            if (_blocks.TryGetValue(GameBlockKey, out block))
            {
                return true;
            }

            block = null;
            return false;
        }

        public static bool TryFocusBlock(DockBlockKind kind)
        {
            if (!TryGetBlockByKind(kind, out DockBlock block))
            {
                return false;
            }

            SetFocusedBlock(block);
            return true;
        }

        public static bool BlockHasFocus(DockBlockKind kind)
        {
            if (!TryGetBlockByKind(kind, out DockBlock block))
            {
                return false;
            }

            return BlockHasFocus(block?.Id);
        }

        public static bool BlockHasFocus(string blockId)
        {
            if (string.IsNullOrWhiteSpace(blockId) || string.IsNullOrWhiteSpace(_focusedBlockId))
            {
                return false;
            }

            return string.Equals(_focusedBlockId, blockId, StringComparison.OrdinalIgnoreCase);
        }

        public static void ClearBlockFocus()
        {
            _focusedBlockId = null;
        }

        public static DockBlockKind? GetFocusedBlockKind()
        {
            if (string.IsNullOrWhiteSpace(_focusedBlockId))
            {
                return null;
            }

            if (!_blocks.TryGetValue(_focusedBlockId, out DockBlock block))
            {
                return null;
            }

            return block.Kind;
        }

        private static void SetFocusedBlock(DockBlock block)
        {
            if (block == null || !block.IsVisible)
            {
                return;
            }

            _focusedBlockId = block.Id;
        }

        private static void EnsureFocusedBlockValid()
        {
            if (string.IsNullOrWhiteSpace(_focusedBlockId))
            {
                return;
            }

            if (!_blocks.TryGetValue(_focusedBlockId, out DockBlock block) || block == null || !block.IsVisible)
            {
                _focusedBlockId = null;
            }
        }

        private static bool TryGetBlockByKind(DockBlockKind kind, out DockBlock block)
        {
            block = _orderedBlocks.FirstOrDefault(p => p.Kind == kind && p.IsVisible) ??
                _orderedBlocks.FirstOrDefault(p => p.Kind == kind);
            return block != null;
        }

        public static Vector2 ToGameSpace(Point windowPosition)
        {
            float x = windowPosition.X;
            float y = windowPosition.Y;

            if (_worldRenderTarget == null || _gameContentBounds.Width <= 0 || _gameContentBounds.Height <= 0)
            {
                return new Vector2(x, y);
            }

            float relativeX = (x - _gameContentBounds.X) / _gameContentBounds.Width;
            float relativeY = (y - _gameContentBounds.Y) / _gameContentBounds.Height;

            return new Vector2(relativeX * _worldRenderTarget.Width, relativeY * _worldRenderTarget.Height);
        }

        private static Rectangle GetCurrentGameContentBounds()
        {
            if (_gameContentBounds.Width > 0 && _gameContentBounds.Height > 0)
            {
                return _gameContentBounds;
            }

            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            return graphicsDevice?.Viewport.Bounds ?? Rectangle.Empty;
        }

        public static bool TryProjectGameToWindow(Vector2 gamePosition, out Vector2 windowPosition)
        {
            windowPosition = gamePosition;

            if (_worldRenderTarget == null ||
                _worldRenderTarget.Width <= 0 ||
                _worldRenderTarget.Height <= 0 ||
                _gameContentBounds.Width <= 0 ||
                _gameContentBounds.Height <= 0)
            {
                return false;
            }

            float normalizedX = gamePosition.X / _worldRenderTarget.Width;
            float normalizedY = gamePosition.Y / _worldRenderTarget.Height;

            normalizedX = MathHelper.Clamp(normalizedX, 0f, 1f);
            normalizedY = MathHelper.Clamp(normalizedY, 0f, 1f);

            float projectedX = _gameContentBounds.X + (normalizedX * _gameContentBounds.Width);
            float projectedY = _gameContentBounds.Y + (normalizedY * _gameContentBounds.Height);

            windowPosition = new Vector2(projectedX, projectedY);
            return true;
        }

        private static void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds, Color color, int thickness)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            Rectangle top = new(bounds.X, bounds.Y, bounds.Width, thickness);
            Rectangle bottom = new(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness);
            Rectangle left = new(bounds.X, bounds.Y, thickness, bounds.Height);
            Rectangle right = new(bounds.Right - thickness, bounds.Y, thickness, bounds.Height);

            spriteBatch.Draw(_pixelTexture, top, color);
            spriteBatch.Draw(_pixelTexture, bottom, color);
            spriteBatch.Draw(_pixelTexture, left, color);
            spriteBatch.Draw(_pixelTexture, right, color);
        }

        private sealed class PanelGroupBarLayout
        {
            public PanelGroupBarLayout(string panelId, Rectangle groupBarBounds)
            {
                PanelId = panelId;
                GroupBarBounds = groupBarBounds;
                Tabs = new List<TabHitRegion>();
            }

            public string PanelId { get; }
            public Rectangle GroupBarBounds { get; }
            public List<TabHitRegion> Tabs { get; }
        }

        private readonly struct TabHitRegion
        {
            public TabHitRegion(string blockId, Rectangle bounds, Rectangle ungroupBounds, Rectangle lockBounds, Rectangle closeBounds)
            {
                BlockId = blockId;
                Bounds = bounds;
                UngroupBounds = ungroupBounds;
                LockBounds = lockBounds;
                CloseBounds = closeBounds;
            }

            public string BlockId { get; }
            public Rectangle Bounds { get; }
            public Rectangle UngroupBounds { get; }
            public Rectangle LockBounds { get; }
            public Rectangle CloseBounds { get; }
        }

        private sealed class BlankBlockHoverState
        {
            public bool IsHovering;
            public OpacityAnimation Animation;
        }

        private enum BlockMenuControlMode
        {
            Toggle,
            Count
        }

        private sealed class BlockMenuEntry
        {
            public BlockMenuEntry(string idPrefix, string label, DockBlockKind kind, BlockMenuControlMode controlMode, int minCount = 0, int maxCount = 10, int initialCount = 0, bool initialVisible = true)
            {
                IdPrefix = idPrefix ?? throw new ArgumentNullException(nameof(idPrefix));
                Label = string.IsNullOrWhiteSpace(label) ? idPrefix : label;
                Kind = kind;
                ControlMode = controlMode;
                MinCount = Math.Max(0, minCount);
                MaxCount = Math.Max(MinCount, maxCount);
                Count = controlMode == BlockMenuControlMode.Count ? Math.Clamp(initialCount, MinCount, MaxCount) : 0;
                IsVisible = controlMode == BlockMenuControlMode.Toggle ? initialVisible : true;
                InputBuffer = Count.ToString();
            }

            public string IdPrefix { get; }
            public string Label { get; }
            public DockBlockKind Kind { get; }
            public BlockMenuControlMode ControlMode { get; }
            public int MinCount { get; }
            public int MaxCount { get; }
            public int Count { get; set; }
            public bool IsVisible { get; set; }
            public bool IsEditing { get; set; }
            public string InputBuffer { get; set; }
        }

        private readonly struct OverlayMenuRow
        {
            public OverlayMenuRow(BlockMenuEntry entry, Rectangle toggleBounds, Rectangle minusBounds, Rectangle inputBounds, Rectangle plusBounds)
            {
                Entry = entry;
                ToggleBounds = toggleBounds;
                MinusBounds = minusBounds;
                InputBounds = inputBounds;
                PlusBounds = plusBounds;
            }

            public BlockMenuEntry Entry { get; }
            public Rectangle ToggleBounds { get; }
            public Rectangle MinusBounds { get; }
            public Rectangle InputBounds { get; }
            public Rectangle PlusBounds { get; }
        }

        private readonly struct ResizeEdge
        {
            public ResizeEdge(SplitNode node, DockSplitOrientation orientation, Rectangle bounds, int depth)
            {
                Node = node;
                Orientation = orientation;
                Bounds = bounds;
                Depth = depth;
            }

            public SplitNode Node { get; }
            public DockSplitOrientation Orientation { get; }
            public Rectangle Bounds { get; }
            public int Depth { get; }
        }

        private readonly struct CornerHandle
        {
            public CornerHandle(ResizeEdge verticalHandle, ResizeEdge horizontalHandle, Rectangle bounds)
            {
                VerticalHandle = verticalHandle;
                HorizontalHandle = horizontalHandle;
                Bounds = bounds;
            }

            public ResizeEdge VerticalHandle { get; }
            public ResizeEdge HorizontalHandle { get; }
            public Rectangle Bounds { get; }
        }

        private struct DockDropPreview
        {
            public DockBlock TargetBlock;
            public DockEdge Edge;
            public Rectangle HighlightBounds;
            public bool IsViewportSnap;
            public bool IsTabDrop;
            public int TabInsertIndex;
            public string TargetPanelId;
        }

        private readonly struct CornerSnapResult
        {
            public CornerSnapResult(CornerHandle target, Point snapPoint, bool lockX, bool lockY)
            {
                Target = target;
                SnapPoint = snapPoint;
                LockX = lockX;
                LockY = lockY;
            }

            public CornerHandle Target { get; }
            public Point SnapPoint { get; }
            public bool LockX { get; }
            public bool LockY { get; }
        }
    }
}
