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
        private const string PropertiesBlockKey = "properties";
        private const string ColorSchemeBlockKey = "colors";
        private const string ControlsBlockKey = "controls";
        private const string NotesBlockKey = "notes";
        private const string ControlSetupsBlockKey = "controlsetups";
        private const string DockingSetupsBlockKey = "dockingsetups";
        private const string BackendBlockKey = "backend";
        private const string SpecsBlockKey = "specs";
        private const string DebugLogsBlockKey = "debuglogs";
        private const string BarsBlockKey = "bars";
        private const string ChatBlockKey = "chat";
        private const string PerformanceBlockKey = "performance";

        private const string InteractBlockKey = "interact";
        private const string BlockMenuControlKey = "BlockMenu";
private const string DockingSetupActiveRowKey = "__ActiveSetup";
        private const string OverlayInputFocusOwner = "BlockManager.OverlayNumeric";
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
        private const int OpacitySliderMaxTrackWidth = 500;
        private const int OpacitySliderLabelSpacing = 8;
        private const float SuperimposeZoneFraction = 0.4f;
        private const int OpacityRowHeight = 22;
        private const int SuperimposeZoneMaxSide = 500;

        private static Color GroupBarBackground => ColorPalette.TabBarBackground;
        private static Color TabInactiveBackground => ColorPalette.TabInactive;
        private static Color TabHoverBackground => ColorPalette.TabHover;
        private static Color TabActiveBackground => ColorPalette.TabActive;
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
        private static readonly Dictionary<string, (string Text, string DataType)[]> _blockTooltips = new(StringComparer.OrdinalIgnoreCase);
        private static bool _tooltipsLoaded;
        private static string _tooltipHoveredRowKey;
        private static string _tooltipHoveredRowLabel;
        private static double _tooltipHoverElapsed;
        private const double TooltipDelaySeconds = 0.6d;
        private const int TooltipMaxWidth = 230;
        private const int TooltipPadding = 8;
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
        private static MouseState _previousVirtualMouseState;
        private static Point _mousePosition;

        // ── Camera pan (middle-mouse drag) ────────────────────────────────────────
        public static Vector2 CameraOffset { get; set; } = Vector2.Zero;
        private static Vector2? _cameraPanAnchorRaw;
        private static Vector2 _cameraPanAnchorOffset;
        // Persistent offset from the player-center baseline used in Locked mode.
        private static Vector2 _lockedCameraOffset = Vector2.Zero;
        // Camera zoom (1.0 = default, >1 = zoomed in / closer, <1 = zoomed out / farther)
        private static float _cameraZoom = 1f;
        public static float CameraZoom => _cameraZoom;
        // When releasing middle-mouse drag, if the camera is within this distance of
        // the player-centered position it snaps back automatically (loaded from DB).
        private static float _cameraSnapRange = 75f;
        // True once the drag has moved outside snap range; snap preview only activates after this.
        private static bool _cameraDragArmed = false;
        private static float _uiScale = 1f;
        public static float UIScale => _uiScale;
        public static Matrix CurrentUITransform => _uiScale > 0f ? Matrix.CreateScale(_uiScale, _uiScale, 1f) : Matrix.Identity;
        private static Rectangle _actualGameContentBounds;
        private const float ReferenceUIHeight = 1080f;
        private static DockBlock _draggingBlock;
        private static PanelGroup _draggingPanel;
        private static Rectangle _draggingStartBounds;
        private static Point _dragOffset;
        private static DockDropPreview? _dropPreview;
        private static bool _superimposeLocked;
        private static DockBlock _superimposeLockedTarget;
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
        private static Point? _activeCornerSnapPosition;
        private static Point? _activeCornerSnapAnchor;
        private static bool _activeCornerSnapLockX;
        private static bool _activeCornerSnapLockY;
        private static readonly List<OverlayResizeEdge> _overlayResizeEdges = [];
        private static OverlayResizeEdge? _hoveredOverlayResizeEdge;
        private static OverlayResizeEdge? _activeOverlayResizeEdge;
        private static readonly List<OverlayCornerHandle> _overlayCornerHandles = [];
        private static OverlayCornerHandle? _hoveredOverlayCornerHandle;
        private static OverlayCornerHandle? _activeOverlayCornerHandle;
        private static Rectangle? _overlayDragPreviewBounds;
        private static DockBlock _overlayDropTargetBlock;
        private static readonly HashSet<string> _parentSuppressedOverlays = new(StringComparer.OrdinalIgnoreCase);
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

        // Per-block GUI interaction states — keyed by block ID, same pattern as _blockLockStates.
        private static readonly Dictionary<string, GUIInteractionState> _blockInteractionStates = new(StringComparer.OrdinalIgnoreCase);
        // Priority-queue left-click ownership: true while the UI has claimed the current press.
        private static bool _uiOwnsLeftClick;
        private static string _pressedTabBlockId;
        private static string _pressedTabPanelId;
        private static Point _tabPressPosition;
        private static bool _draggingFromTab;
        private static bool _opacitySliderDragging;
        private static string _opacitySliderDraggingId;
        private static Rectangle _opacitySliderTrackBounds;
        private static string _opacityExpandedId;

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

        /// <summary>
        /// Returns true while the BlockManager is actively consuming mouse input (dragging a slider,
        /// resize edge, corner handle, block, etc.). Use this in GameUpdater to suppress player
        /// movement and rotation while interacting with UI controls.
        /// </summary>
        public static bool IsConsumingMouseInput() =>
            _opacitySliderDragging ||
            _activeResizeEdge.HasValue ||
            _activeCornerHandle.HasValue ||
            _activeOverlayResizeEdge.HasValue ||
            _activeOverlayCornerHandle.HasValue ||
            _draggingBlock != null;

        /// <summary>
        /// Must be called each frame BEFORE player movement is computed (i.e., before
        /// InputManager.GetMoveVector). Evaluates the left-click priority queue using the
        /// current mouse state so that <see cref="IsAnyGuiInteracting"/> is always current-frame
        /// accurate rather than one frame behind.
        ///
        /// Priority rules for LeftClick:
        ///   1. If the UI already owned the press (sticky while button held) → UI keeps it.
        ///   2. If a new press lands on an enabled interactive UI element → UI claims it.
        ///   3. Otherwise (disabled element, empty area, no block) → game input may use it.
        /// </summary>
        public static void PreUpdateInteractionStates()
        {
            MouseState current = Mouse.GetState();
            bool leftHeld = current.LeftButton == ButtonState.Pressed;

            if (!leftHeld)
            {
                _uiOwnsLeftClick = false;
                return;
            }

            // Sticky: once the UI claimed this press, keep ownership until button released.
            if (_uiOwnsLeftClick)
            {
                return;
            }

            // New press — determine whether an enabled UI element is at the cursor.
            bool leftJustPressed = _previousMouseState.LeftButton == ButtonState.Released;
            if (!leftJustPressed)
            {
                return;
            }

            Point pos = current.Position;
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !block.IsVisible || block.Kind == DockBlockKind.Game)
                {
                    continue;
                }

                if (!block.Bounds.Contains(pos))
                {
                    continue;
                }

                // Cursor is over this block. Claim ownership only if there is an enabled element here.
                _uiOwnsLeftClick = IsBlockInteractableAt(block, pos);
                return;
            }

            _uiOwnsLeftClick = false;
        }

        /// <summary>
        /// Returns whether the block has an enabled interactive element at <paramref name="pos"/>.
        /// Locked blocks (when not in BlockMode) are never interactable — clicks pass through to game input.
        /// Blocks with disabled buttons return false at those button positions so the left-click
        /// can fall through to game input.
        /// </summary>
        private static bool IsBlockInteractableAt(DockBlock block, Point pos)
        {
            if (IsBlockLocked(block) || IsPanelLocked(block))
                return false;

            return block.Kind switch
            {
                DockBlockKind.ControlSetups => ControlSetupsBlock.IsInteractableAt(pos),
                DockBlockKind.DockingSetups => DockingSetupsBlock.IsInteractableAt(pos),
                _ => true
            };
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
                MouseState ms = Mouse.GetState();
                _previousMouseState = ms;
                _previousVirtualMouseState = ms;
                return;
            }

            EnsureBlocks();
            EnsureFocusedBlockValid();
            UpdateLayoutCache();
            EnsureSurfaceResources(Core.Instance.GraphicsDevice);
            double elapsedSeconds = Math.Max(gameTime?.ElapsedGameTime.TotalSeconds ?? 0d, 0d);

            MouseState mouseState = Mouse.GetState();
            float invScale = _uiScale > 0f ? 1f / _uiScale : 1f;
            MouseState virtualMouseState = ScaleMouseState(mouseState, invScale);
            KeyboardState keyboardState = Keyboard.GetState();
            _mousePosition = virtualMouseState.Position;
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

            // ── Camera pan (middle-mouse drag) ────────────────────────────────────
            bool middleJustPressed = mouseState.MiddleButton == ButtonState.Pressed && _previousMouseState.MiddleButton == ButtonState.Released;
            bool middleHeld = mouseState.MiddleButton == ButtonState.Pressed;
            if (middleJustPressed && IsCursorWithinGameBlock())
            {
                _cameraPanAnchorRaw    = ToGameSpaceRaw(mouseState.Position);
                _cameraPanAnchorOffset = CameraOffset;
                _cameraDragArmed       = false;
            }
            else if (middleHeld && _cameraPanAnchorRaw.HasValue)
            {
                Vector2 rawDelta = ToGameSpaceRaw(mouseState.Position) - _cameraPanAnchorRaw.Value;
                Vector2 draggedOffset = _cameraPanAnchorOffset - rawDelta;

                // Snap preview: only after the drag has left the snap zone at least once.
                if (_cameraSnapRange > 0f && Core.Instance?.Player != null && _worldRenderTarget != null)
                {
                    var renderCenter = new Vector2(_worldRenderTarget.Width / 2f, _worldRenderTarget.Height / 2f);
                    Vector2 playerCentered = Core.Instance.Player.Position - renderCenter;
                    float distFromPlayer;
                    string camMode = GetCameraMode();

                    if (string.Equals(camMode, "Locked", StringComparison.OrdinalIgnoreCase))
                        distFromPlayer = (draggedOffset - playerCentered).Length();
                    else if (string.Equals(camMode, "Free", StringComparison.OrdinalIgnoreCase))
                        distFromPlayer = (draggedOffset - playerCentered).Length();
                    else
                        distFromPlayer = float.MaxValue; // Scout always snaps on release; no preview needed.

                    // Arm once the drag moves outside the snap zone.
                    if (!_cameraDragArmed && distFromPlayer > _cameraSnapRange)
                        _cameraDragArmed = true;

                    // Preview snap only when armed and back inside the zone.
                    if (_cameraDragArmed && distFromPlayer <= _cameraSnapRange)
                        draggedOffset = playerCentered;
                }

                CameraOffset = draggedOffset;
            }
            else if (!middleHeld)
            {
                if (_cameraPanAnchorRaw.HasValue)
                {
                    string camMode = GetCameraMode();
                    bool holdInputs = ControlStateManager.GetSwitchState(ControlKeyMigrations.HoldInputsKey);

                    if (string.Equals(camMode, "Scout", StringComparison.OrdinalIgnoreCase) || holdInputs)
                    {
                        // Scout (or HoldInputs): snap back to player center by clearing the locked offset.
                        _lockedCameraOffset = Vector2.Zero;
                    }
                    else if (string.Equals(camMode, "Locked", StringComparison.OrdinalIgnoreCase)
                             && Core.Instance?.Player != null && _worldRenderTarget != null)
                    {
                        // Locked: keep current viewport position relative to player.
                        var renderCenter = new Vector2(_worldRenderTarget.Width / 2f, _worldRenderTarget.Height / 2f);
                        _lockedCameraOffset = CameraOffset - (Core.Instance.Player.Position - renderCenter);
                        // Snap back to player if armed and within configurable range.
                        if (_cameraDragArmed && _lockedCameraOffset.Length() <= _cameraSnapRange)
                            _lockedCameraOffset = Vector2.Zero;
                    }
                    else if (string.Equals(camMode, "Free", StringComparison.OrdinalIgnoreCase)
                             && Core.Instance?.Player != null && _worldRenderTarget != null)
                    {
                        // Free: snap to player if armed and close enough.
                        var renderCenter = new Vector2(_worldRenderTarget.Width / 2f, _worldRenderTarget.Height / 2f);
                        Vector2 playerCentered = Core.Instance.Player.Position - renderCenter;
                        if (_cameraDragArmed && (CameraOffset - playerCentered).Length() <= _cameraSnapRange)
                            CameraOffset = playerCentered;
                    }
                }
                _cameraPanAnchorRaw = null;
            }

            // ── Camera follow (Scout / Locked modes) ─────────────────────────────
            if (!_cameraPanAnchorRaw.HasValue && _worldRenderTarget != null && Core.Instance?.Player != null)
            {
                string camMode = GetCameraMode();
                if (string.Equals(camMode, "Scout", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(camMode, "Locked", StringComparison.OrdinalIgnoreCase))
                {
                    var renderCenter = new Vector2(_worldRenderTarget.Width / 2f, _worldRenderTarget.Height / 2f);
                    CameraOffset = Core.Instance.Player.Position - renderCenter + _lockedCameraOffset;
                }
            }

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
                    // Overlay resize runs after panel resize so panel handles take priority when they overlap.
                    bool resizingBlocks = !tabInteracted && allowReorder && (UpdateCornerResizeState(leftClickStarted, leftClickHeld, leftClickReleased) ||
                        UpdateResizeEdgeState(leftClickStarted, leftClickHeld, leftClickReleased) ||
                        UpdateOverlayResizeState(leftClickStarted, leftClickHeld, leftClickReleased));
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
                    // Overlays are always interactive (drag, resize, lock, close, opacity)
                    // even outside docking mode. Standard block chrome is cleared.
                    bool overlayResizing = !tabInteracted && UpdateOverlayResizeState(leftClickStarted, leftClickHeld, leftClickReleased);
                    if (!overlayResizing && !tabInteracted)
                    {
                        UpdateDragState(leftClickStarted, leftClickReleased, allowReorder: false);
                    }
                    else
                    {
                        _draggingBlock = null;
                        _dropPreview = null;
                    }

                    // Clear standard-block-only docking state that doesn't apply outside docking mode.
                    _hoveredResizeEdge = null;
                    _activeResizeEdge = null;
                    _hoveredCornerHandle = null;
                    _activeCornerHandle = null;
                    _activeCornerLinkedHandle = null;
                }
            }
            else
            {
                ClearDockingDragState();
            }

            UpdateInteractiveBlocks(gameTime, virtualMouseState, _previousVirtualMouseState, keyboardState, _previousKeyboardState);
            UpdateTooltipHoverState(elapsedSeconds, leftClickHeld);
            ApplyPendingDockingSetup();
            UpdateTransparentBlockClickThrough();
            _previousMouseState = mouseState;
            _previousVirtualMouseState = virtualMouseState;
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
                _actualGameContentBounds.Width > 0 &&
                _actualGameContentBounds.Height > 0;

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
                if (block == null || !block.IsVisible)
                {
                    continue;
                }

                bool isFullyTransparent =
                    BlockHasOpacitySlider(block) && block.BackgroundOpacity <= 0f && !block.IsOverlay;

                if (isFullyTransparent && block.Bounds.Contains(_mousePosition))
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

            Matrix uiTransform = _uiScale > 0f ? Matrix.CreateScale(_uiScale, _uiScale, 1f) : Matrix.Identity;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, uiTransform);
            DrawRect(spriteBatch, _layoutBounds, Core.TransparentWindowColor);

            if (!AnyBlockVisible())
            {
                DrawEmptyState(spriteBatch, _layoutBounds);
            }
            else
            {
                DrawBlocks(spriteBatch);
            }

            ColorSchemeBlock.DrawOverlay(spriteBatch, _layoutBounds);
            DrawOverlayMenu(spriteBatch);
            ControlsBlock.DrawRebindOverlay(spriteBatch);
            DrawTooltip(spriteBatch);
            spriteBatch.End();

            _renderingDockedFrame = false;
        }

        private static void EnsureBlocks()
        {
            EnsureBlockMenuEntries();

            if (!_tooltipsLoaded)
            {
                var loaded = DatabaseFetch.LoadBlockTooltips();
                foreach (var pair in loaded)
                    _blockTooltips[pair.Key] = [pair.Value];
                // Button tooltips for PropertiesBlock
                _blockTooltips["props_btn:lock:lock"]     = [("Click to lock the current inspect target", string.Empty)];
                _blockTooltips["props_btn:lock:unlock"]   = [("Click to unlock the inspect target", string.Empty)];
                _blockTooltips["props_btn:hidden:show"]   = [("Reveal hidden attributes in the details panel", string.Empty)];
                _blockTooltips["props_btn:hidden:hide"]   = [("Collapse hidden attributes in the details panel", string.Empty)];

                // All property row tooltips: non-hidden rows get 1 entry, hidden rows get 2
                foreach (var (key, entries) in PropertiesBlock.GetAllPropRowTooltipEntries())
                    _blockTooltips[key] = entries;

                _tooltipsLoaded = true;
            }

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

            // Specs is a normal tab inside the Backend panel group (no longer a game overlay).

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

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !block.IsOverlay || string.IsNullOrEmpty(block.OverlayParentId))
                {
                    continue;
                }

                setup.Overlays.Add(new DockingSetupOverlay
                {
                    BlockId = block.Id,
                    ParentId = block.OverlayParentId,
                    X = block.OverlayRelX,
                    Y = block.OverlayRelY,
                    W = block.OverlayRelWidth,
                    H = block.OverlayRelHeight
                });
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !BlockHasOpacitySlider(block))
                {
                    continue;
                }

                float opacity = MathHelper.Clamp(block.BackgroundOpacity, 0f, 1f);
                if (Math.Abs(opacity - 1.0f) > 0.001f)
                {
                    setup.BlockOpacities[block.Id] = opacity;
                }
            }

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
            MergeControlSetupsIntoControlsGroup();
            MergeBackendAndSpecs();

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
            ApplyDockingSetupOverlays(setup.Overlays);

            // Specs is now a regular tab in the Backend panel; no overlay migration needed.

            ApplyDockingSetupOpacities(setup.BlockOpacities);

            _blockDefinitionsReady = true;
            MarkLayoutDirty();
            return true;
        }

        private static void ApplyDockingSetupOverlays(IEnumerable<DockingSetupOverlay> overlays)
        {
            if (overlays == null)
            {
                return;
            }

            foreach (DockingSetupOverlay overlay in overlays)
            {
                if (overlay == null || string.IsNullOrEmpty(overlay.BlockId) || string.IsNullOrEmpty(overlay.ParentId))
                {
                    continue;
                }

                if (!_blocks.TryGetValue(overlay.BlockId, out DockBlock block))
                {
                    continue;
                }

                if (_blockNodes.TryGetValue(block.Id, out BlockNode blockNode))
                {
                    _rootNode = DockLayout.Detach(_rootNode, blockNode);
                    _blockNodes.Remove(block.Id);
                }

                PanelGroup group = GetPanelGroupForBlock(block);
                if (group != null)
                {
                    group.RemoveBlock(block.Id, out _);
                    _blockToPanel.Remove(block.Id);
                    if (group.Blocks.Count == 0)
                    {
                        RemovePanelGroup(group);
                    }
                    else if (string.Equals(group.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        DockBlock next = group.Blocks.FirstOrDefault();
                        if (next != null)
                        {
                            SetPanelActiveBlock(group, next);
                        }
                    }
                }

                block.IsOverlay = true;
                block.OverlayParentId = overlay.ParentId;
                block.OverlayRelX = overlay.X;
                block.OverlayRelY = overlay.Y;
                block.OverlayRelWidth = Math.Max(0.05f, overlay.W);
                block.OverlayRelHeight = Math.Max(0.05f, overlay.H);
                block.IsVisible = true;

                // Overlay blocks are detached from the layout tree and stripped from any
                // panel group by the code above.  Without their own panel group the
                // RedrawBlockHeader path finds group == null, skips the tab bar entirely,
                // and the per-block lock button is never drawn or hit-testable.
                // Re-attach the block to a fresh single-block panel group keyed on its own
                // ID so the tab bar (and lock toggle) appear at the overlay's position.
                if (!_panelGroups.ContainsKey(block.Id))
                {
                    PanelGroup overlayGroup = new(block.Id, block);
                    _panelGroups[block.Id] = overlayGroup;
                    _blockToPanel[block.Id] = block.Id;
                    if (!_orderedPanelIds.Contains(block.Id, StringComparer.OrdinalIgnoreCase))
                        _orderedPanelIds.Add(block.Id);
                }
            }
        }

        private static void ApplyDockingSetupOpacities(IDictionary<string, float> opacities)
        {
            if (opacities == null)
            {
                return;
            }

            foreach ((string blockId, float opacity) in opacities)
            {
                if (_blocks.TryGetValue(blockId, out DockBlock block))
                {
                    block.BackgroundOpacity = MathHelper.Clamp(opacity, 0f, 1f);
                }
            }
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
            _blockMenuEntries.Add(new BlockMenuEntry(GameBlockKey, GameBlock.BlockTitle, DockBlockKind.Game, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(PropertiesBlockKey, PropertiesBlock.BlockTitle, DockBlockKind.Properties, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ColorSchemeBlockKey, ColorSchemeBlock.BlockTitle, DockBlockKind.ColorScheme, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ControlsBlockKey, ControlsBlock.BlockTitle, DockBlockKind.Controls, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(NotesBlockKey, NotesBlock.BlockTitle, DockBlockKind.Notes, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ControlSetupsBlockKey, ControlSetupsBlock.BlockTitle, DockBlockKind.ControlSetups, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(DockingSetupsBlockKey, DockingSetupsBlock.BlockTitle, DockBlockKind.DockingSetups, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(BackendBlockKey, BackendBlock.BlockTitle, DockBlockKind.Backend, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(DebugLogsBlockKey, DebugLogsBlock.BlockTitle, DockBlockKind.DebugLogs, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(SpecsBlockKey, SpecsBlock.BlockTitle, DockBlockKind.Specs, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(BarsBlockKey, BarsBlock.BlockTitle, DockBlockKind.Bars, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ChatBlockKey, ChatBlock.BlockTitle, DockBlockKind.Chat, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(PerformanceBlockKey, PerformanceBlock.BlockTitle, DockBlockKind.Performance, BlockMenuControlMode.Toggle, initialVisible: true));

            _blockMenuEntries.Add(new BlockMenuEntry(InteractBlockKey, InteractBlock.BlockTitle, DockBlockKind.Interact, BlockMenuControlMode.Toggle, initialVisible: true));
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
            MergeControlSetupsIntoControlsGroup();

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

            // Merge Chat and Math into the Backend panel group so they appear as tabs, not separate columns.
            if (_blocks.TryGetValue(BackendBlockKey, out DockBlock backendBlock) && backendBlock != null)
            {
                PanelGroup backendGroup = GetPanelGroupForBlock(backendBlock);
                if (backendGroup != null)
                {
                    MergeBlockIntoGroup(backendGroup, ChatBlockKey);

                    MergeBlockIntoGroup(backendGroup, SpecsBlockKey);
                }
            }

            // Merge Bars into the bottom-right panel (Backend group).
            if (_blocks.TryGetValue(BackendBlockKey, out DockBlock backendBlockForBars) && backendBlockForBars != null)
            {
                PanelGroup barsTarget = GetPanelGroupForBlock(backendBlockForBars);
                if (barsTarget != null)
                    MergeBlockIntoGroup(barsTarget, BarsBlockKey);
            }

            // Merge Interact into the Controls panel group so it appears as the default active tab.
            MergeInteractIntoControlsGroup();
        }

        private static void MergeInteractIntoControlsGroup()
        {
            if (!_blocks.TryGetValue(ControlsBlockKey, out DockBlock controlsBlock) ||
                controlsBlock == null)
            {
                return;
            }

            PanelGroup controlsGroup = GetPanelGroupForBlock(controlsBlock);
            if (controlsGroup == null)
                return;

            MergeBlockIntoGroup(controlsGroup, InteractBlockKey);

            // Make Interact the active tab in this panel group.
            controlsGroup.SetActiveBlock(InteractBlockKey);
        }

        private static void MergeControlSetupsIntoControlsGroup()
        {
            if (!_blocks.TryGetValue(ControlsBlockKey, out DockBlock controlsBlock) ||
                controlsBlock == null)
            {
                return;
            }

            PanelGroup controlsGroup = GetPanelGroupForBlock(controlsBlock);
            if (controlsGroup == null)
            {
                return;
            }

            bool mergedGroupLocked = controlsGroup.IsLocked;

            if (_blocks.TryGetValue(ControlSetupsBlockKey, out DockBlock controlSetupsBlock) &&
                controlSetupsBlock != null)
            {
                PanelGroup controlSetupsGroup = GetPanelGroupForBlock(controlSetupsBlock);
                if (controlSetupsGroup != null && !ReferenceEquals(controlsGroup, controlSetupsGroup))
                {
                    mergedGroupLocked |= controlSetupsGroup.IsLocked;
                }
            }

            MergeBlockIntoGroup(controlsGroup, ControlSetupsBlockKey);
            controlsGroup.IsLocked = mergedGroupLocked;
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
                backendBlock == null)
            {
                return;
            }

            PanelGroup backendGroup = GetPanelGroupForBlock(backendBlock);
            if (backendGroup == null)
            {
                return;
            }

            bool backendLocked = backendGroup.IsLocked;

            if (_blocks.TryGetValue(SpecsBlockKey, out DockBlock specsBlock) && specsBlock != null)
            {
                PanelGroup specsGroup = GetPanelGroupForBlock(specsBlock);
                if (specsGroup != null && !ReferenceEquals(backendGroup, specsGroup))
                {
                    backendLocked |= specsGroup.IsLocked;
                    MergeBlockIntoGroup(backendGroup, specsBlock.Id);
                }
            }

            if (_blocks.TryGetValue(DebugLogsBlockKey, out DockBlock debugLogsBlock) && debugLogsBlock != null)
            {
                PanelGroup debugLogsGroup = GetPanelGroupForBlock(debugLogsBlock);
                if (debugLogsGroup != null && !ReferenceEquals(backendGroup, debugLogsGroup))
                {
                    backendLocked |= debugLogsGroup.IsLocked;
                }

                MergeBlockIntoGroup(backendGroup, debugLogsBlock.Id);
            }

            if (_blocks.TryGetValue(ChatBlockKey, out DockBlock chatBlock) && chatBlock != null)
            {
                PanelGroup chatGroup = GetPanelGroupForBlock(chatBlock);
                if (chatGroup != null && !ReferenceEquals(backendGroup, chatGroup))
                {
                    backendLocked |= chatGroup.IsLocked;
                }

                MergeBlockIntoGroup(backendGroup, chatBlock.Id);
            }

            if (_blocks.TryGetValue(PerformanceBlockKey, out DockBlock performanceBlock) && performanceBlock != null)
            {
                PanelGroup performanceGroup = GetPanelGroupForBlock(performanceBlock);
                if (performanceGroup != null && !ReferenceEquals(backendGroup, performanceGroup))
                {
                    backendLocked |= performanceGroup.IsLocked;
                }

                MergeBlockIntoGroup(backendGroup, performanceBlock.Id);
            }

            backendGroup.IsLocked = backendLocked;
        }

        private static DockNode BuildDefaultLayout()
        {
            List<BlockNode> blankNodes = GetBlockNodesByKind(DockBlockKind.Blank);
            BlockNode gameNode = GetBlockNodesByKind(DockBlockKind.Game).FirstOrDefault();
            BlockNode propertiesNode = GetBlockNodesByKind(DockBlockKind.Properties).FirstOrDefault();
            BlockNode colorNode = GetBlockNodesByKind(DockBlockKind.ColorScheme).FirstOrDefault();
            BlockNode controlsNode = GetBlockNodesByKind(DockBlockKind.Controls).FirstOrDefault();
            BlockNode controlSetupsNode = GetBlockNodesByKind(DockBlockKind.ControlSetups).FirstOrDefault();
            BlockNode notesNode = GetBlockNodesByKind(DockBlockKind.Notes).FirstOrDefault();
            BlockNode backendNode = GetBlockNodesByKind(DockBlockKind.Backend).FirstOrDefault();
            // specsNode, chatNode, and mathNode are merged into the backend panel group as tabs

            DockNode blankStack = BuildStack(blankNodes, DockSplitOrientation.Horizontal);

            DockNode leftColumn = CombineNodes(blankStack, gameNode, DockSplitOrientation.Horizontal, 0.36f);
            DockNode controlsAndConfigs = CombineNodes(controlsNode, controlSetupsNode, DockSplitOrientation.Horizontal, 0.52f);
            DockNode controlsAndNotes = CombineNodes(controlsAndConfigs, notesNode, DockSplitOrientation.Horizontal, 0.64f);
            // Specs, Chat, and Math are merged into Backend's panel group as tabs
            DockNode paletteBackend = CombineNodes(colorNode, backendNode, DockSplitOrientation.Horizontal, 0.42f);
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

            GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds, out Rectangle mergeBounds);
            int buttonStart = dragBar.Right - DragBarButtonPadding;
            if (closeBounds != Rectangle.Empty)
            {
                buttonStart = Math.Min(buttonStart, closeBounds.X);
            }

            if (panelLockBounds != Rectangle.Empty)
            {
                buttonStart = Math.Min(buttonStart, panelLockBounds.X);
            }

            if (mergeBounds != Rectangle.Empty)
            {
                buttonStart = Math.Min(buttonStart, mergeBounds.X);
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
            // Opacity row is drawn as an overlay on the content area — it no longer
            // pushes content down, so we do NOT add OpacityRowHeight here.
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

                // Overlays always show drag bars regardless of docking mode.
                int dbh = active.IsOverlay ? UIStyle.DragBarHeight : dragBarHeight;
                PanelGroupBarLayout layout = BuildGroupBarLayout(active, group, dbh);
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
                int count = tabWidths.Count;
                int share = remainingWidth / count;
                int leftover = remainingWidth % count;
                for (int i = 0; i < count; i++)
                {
                    tabWidths[i] += share + (i < leftover ? 1 : 0);
                }
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
            (int minWidth, int minHeight) = GetBlockMinimumSize();
            block.MinWidth = minWidth;
            block.MinHeight = minHeight;
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

        private static (int MinWidth, int MinHeight) GetBlockMinimumSize()
        {
            // All blocks share the same minimum: wide enough for MinBlockSize,
            // tall enough for the drag bar so we can detect when it is being pushed.
            int headerHeight = Math.Max(UIStyle.DragBarHeight, GroupBarHeight);
            return (UIStyle.MinBlockSize, headerHeight);
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
            int desiredWidth = Math.Max(1, _actualGameContentBounds.Width);
            int desiredHeight = Math.Max(1, _actualGameContentBounds.Height);

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

            // Suppress resize edge hover and activation while a drag bar drag is in progress.
            if (_draggingBlock != null)
            {
                _hoveredResizeEdge = null;
                return false;
            }

            ResizeEdge? hovered = HitTestResizeEdge(_mousePosition);
            _hoveredResizeEdge = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                // If the click is also on a drag bar grab region, yield to the drag bar
                // so vertically stacked blocks can be displaced by dragging upward.
                DockBlock dragBarHit = HitTestDragBarBlock(_mousePosition, excludeDragBarButtons: true, requireGrabRegion: true);
                if (dragBarHit != null)
                {
                    _hoveredResizeEdge = null;
                    return false;
                }

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

            // Suppress corner handle hover and activation while a drag bar drag is in progress.
            if (_draggingBlock != null)
            {
                _hoveredCornerHandle = null;
                return false;
            }

            CornerHandle? hovered = HitTestCornerHandle(_mousePosition);
            _hoveredCornerHandle = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                // If the click is also on a drag bar grab region, yield to the drag bar
                // so vertically stacked blocks can be displaced by dragging upward.
                DockBlock dragBarHit = HitTestDragBarBlock(_mousePosition, excludeDragBarButtons: true, requireGrabRegion: true);
                if (dragBarHit != null)
                {
                    _hoveredCornerHandle = null;
                    return false;
                }

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

            // Overlay drag bars sit visually on top of panel group bars — give them input priority.
            // Exception: if the cursor is on an overlay's own group bar (tab strip), let tab
            // interactions proceed so the overlay's own tabs/lock/close buttons still respond.
            int activeDbh = GetActiveDragBarHeight();
            if (leftClickStarted && IsMouseOnAnyOverlayDragBar(activeDbh)
                && !IsMouseCoveredByOverlayGroupBar(activeDbh))
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

                // Overlay blocks sit on top visually — don't let an underlying non-overlay
                // panel's tab region steal input when the cursor is inside an overlay block.
                if (!active.IsOverlay && IsMouseInsideAnyOverlayBlock())
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
                        bool tabSwitchRequiresBlockMode = ControlStateManager.GetSwitchState(ControlKeyMigrations.TabSwitchRequiresBlockModeKey);
                        if (!tabSwitchRequiresBlockMode || DockingModeEnabled)
                        {
                            ActivatePanelTab(pressedGroup, _pressedTabBlockId);
                        }
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

            // Handle opacity slider drag (takes priority over everything)
            if (_opacitySliderDragging)
            {
                UpdateOpacitySliderDrag();
                // Keep the expansion visible while dragging
                _opacityExpandedId = _opacitySliderDraggingId;
                if (leftClickReleased)
                {
                    _opacitySliderDragging = false;
                    _opacitySliderDraggingId = null;
                    _opacitySliderTrackBounds = Rectangle.Empty;
                }

                return;
            }

            if (leftClickStarted)
            {
                DockBlock sliderBlock = HitTestOpacitySlider(_mousePosition, out Rectangle sliderTrack);
                if (sliderBlock != null)
                {
                    _opacitySliderDragging = true;
                    _opacitySliderDraggingId = sliderBlock.Id;
                    _opacitySliderTrackBounds = sliderTrack;
                    UpdateOpacitySliderDrag();
                    return;
                }
            }

            if (leftClickStarted && TryOverlayMerge(_mousePosition))
            {
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

            // Extended opacity row zone: keep slider visible while cursor is in drag bar OR the opacity row below it.
            // Only expand when the panel is unlocked.
            if (_hoveredDragBarId != null &&
                _blocks.TryGetValue(_hoveredDragBarId, out DockBlock hoverBlockForOpacity) &&
                BlockHasOpacitySlider(hoverBlockForOpacity) &&
                !IsPanelLocked(GetPanelGroupForBlock(hoverBlockForOpacity)) &&
                !IsBlockLocked(hoverBlockForOpacity))
            {
                _opacityExpandedId = _hoveredDragBarId;
            }
            else if (_hoveredDragBarId == null && _opacityExpandedId != null)
            {
                if (_blocks.TryGetValue(_opacityExpandedId, out DockBlock expandedBlock) &&
                    expandedBlock.IsVisible && BlockHasOpacitySlider(expandedBlock))
                {
                    int expDbh = GetActiveDragBarHeight();
                    PanelGroup expGroup = GetPanelGroupForBlock(expandedBlock);
                    Rectangle expDragBar = expandedBlock.GetDragBarBounds(expDbh);
                    int expGroupBarH = GetGroupBarHeight(expGroup);
                    int expHeaderH = Math.Max(expDbh, expGroupBarH);
                    int expRowY = expandedBlock.Bounds.Y + expHeaderH;
                    bool expRowFits = expRowY + OpacityRowHeight <= expandedBlock.Bounds.Bottom;
                    Rectangle expOpRow = expRowFits
                        ? new Rectangle(expandedBlock.Bounds.X, expRowY, expandedBlock.Bounds.Width, OpacityRowHeight)
                        : Rectangle.Empty;
                    Rectangle extZone = expOpRow != Rectangle.Empty && expDragBar != Rectangle.Empty
                        ? Rectangle.Union(expDragBar, expOpRow)
                        : (expOpRow != Rectangle.Empty ? expOpRow : expDragBar);
                    if (extZone != Rectangle.Empty && extZone.Contains(_mousePosition))
                        _hoveredDragBarId = _opacityExpandedId;
                    else
                        _opacityExpandedId = null;
                }
                else
                {
                    _opacityExpandedId = null;
                }
            }
            else if (_hoveredDragBarId != null)
            {
                _opacityExpandedId = null;
            }

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
                // Overlay blocks are always draggable even outside docking mode.
                // Only clear drag state for non-overlay blocks.
                if (_draggingBlock != null && !_draggingBlock.IsOverlay)
                {
                    _draggingBlock = null;
                    _dropPreview = null;
                }

                // Still allow initiating overlay drags.
                if (_draggingBlock == null && leftClickStarted && dragBarHit != null && dragBarHit.IsOverlay)
                {
                    _draggingFromTab = false;
                    ClearPressedTabState();
                    _draggingBlock = dragBarHit;
                    _draggingPanel = GetPanelGroupForBlock(dragBarHit);
                    _draggingStartBounds = dragBarHit.Bounds;
                    _dragOffset = new Point(_mousePosition.X - dragBarHit.Bounds.X, _mousePosition.Y - dragBarHit.Bounds.Y);
                }

                if (_draggingBlock == null || !_draggingBlock.IsOverlay)
                {
                    _dropPreview = null;
                    return;
                }
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
                    DebugLogger.PrintUI($"[DragBarDragStart] Block={block.Id} ({block.Kind}) Bounds={block.Bounds} Mouse={_mousePosition}");
                }
            }
            else if (_draggingBlock != null)
            {
                if (_draggingBlock.IsOverlay)
                {
                    UpdateOverlayBlockDrag();
                    _dropPreview = null;

                    if (leftClickReleased)
                    {
                        if (_overlayDragPreviewBounds.HasValue)
                        {
                            _draggingBlock.Bounds = _overlayDragPreviewBounds.Value;
                            if (_overlayDropTargetBlock != null)
                            {
                                Rectangle newParentBounds = _overlayDropTargetBlock.Bounds;
                                _draggingBlock.OverlayParentId = _overlayDropTargetBlock.Id;
                                if (newParentBounds.Width > 0 && newParentBounds.Height > 0)
                                {
                                    _draggingBlock.OverlayRelX = (_overlayDragPreviewBounds.Value.X - newParentBounds.X) / (float)newParentBounds.Width;
                                    _draggingBlock.OverlayRelY = (_overlayDragPreviewBounds.Value.Y - newParentBounds.Y) / (float)newParentBounds.Height;
                                }
                            }
                        }
                        _overlayDragPreviewBounds = null;
                        _overlayDropTargetBlock = null;
                        MarkLayoutDirty(); // refreshes overlay resize edge positions
                        _draggingBlock = null;
                        _draggingPanel = null;
                        _dropPreview = null;
                        _superimposeLocked = false;
                        _superimposeLockedTarget = null;
                    }
                }
                else
                {
                    _dropPreview = BuildDropPreview(_mousePosition);

                    if (leftClickReleased)
                    {
                        // If locked into superimpose mode but cursor left the parent panel (_dropPreview is null),
                        // cancel the drag without applying any drop.
                        if (!_superimposeLocked || _dropPreview.HasValue)
                        {
                            if (_dropPreview.HasValue)
                            {
                                ApplyDrop(_dropPreview.Value);
                            }
                        }

                        _draggingBlock = null;
                        _draggingPanel = null;
                        _draggingFromTab = false;
                        _dropPreview = null;
                        _superimposeLocked = false;
                        _superimposeLockedTarget = null;
                    }
                }
            }
        }

        private static void UpdateOverlayBlockDrag()
        {
            if (_draggingBlock == null || !_draggingBlock.IsOverlay)
                return;

            if (!_blocks.TryGetValue(_draggingBlock.OverlayParentId ?? string.Empty, out DockBlock currentParent) || !currentParent.IsVisible)
            {
                _draggingBlock.IsOverlay = false;
                _draggingBlock.OverlayParentId = null;
                _overlayDropTargetBlock = null;
                return;
            }

            int blockW = _draggingBlock.Bounds.Width;
            int blockH = _draggingBlock.Bounds.Height;
            const int OverlayEdgeSnapDistance = 10;

            int newX = _mousePosition.X - _dragOffset.X;
            int newY = _mousePosition.Y - _dragOffset.Y;

            // Check if the cursor is hovering a different visible non-overlay block as a potential new parent.
            DockBlock dropTarget = null;
            foreach (DockBlock candidate in _orderedBlocks)
            {
                if (candidate == _draggingBlock || candidate.IsOverlay || !candidate.IsVisible) continue;
                if (string.Equals(candidate.Id, currentParent.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (candidate.Bounds.Contains(_mousePosition))
                {
                    dropTarget = candidate;
                    break;
                }
            }
            _overlayDropTargetBlock = dropTarget;

            // Compute snap position within the relevant parent (drop target or current parent).
            Rectangle snapBounds = dropTarget != null ? dropTarget.Bounds : currentParent.Bounds;

            int clampedX = Math.Clamp(newX, snapBounds.X, Math.Max(snapBounds.X, snapBounds.Right  - blockW));
            int clampedY = Math.Clamp(newY, snapBounds.Y, Math.Max(snapBounds.Y, snapBounds.Bottom - blockH));

            if (clampedX - snapBounds.X < OverlayEdgeSnapDistance)
                clampedX = snapBounds.X;
            else if (snapBounds.Right - (clampedX + blockW) < OverlayEdgeSnapDistance)
                clampedX = snapBounds.Right - blockW;

            if (clampedY - snapBounds.Y < OverlayEdgeSnapDistance)
                clampedY = snapBounds.Y;
            else if (snapBounds.Bottom - (clampedY + blockH) < OverlayEdgeSnapDistance)
                clampedY = snapBounds.Bottom - blockH;

            // Only update relative coords for the current parent while dragging;
            // the new parent assignment happens on drop.
            if (dropTarget == null && currentParent.Bounds.Width > 0 && currentParent.Bounds.Height > 0)
            {
                _draggingBlock.OverlayRelX = (clampedX - currentParent.Bounds.X) / (float)currentParent.Bounds.Width;
                _draggingBlock.OverlayRelY = (clampedY - currentParent.Bounds.Y) / (float)currentParent.Bounds.Height;
            }

            _overlayDragPreviewBounds = new Rectangle(clampedX, clampedY, blockW, blockH);
        }

        private static bool TryTogglePanelLock(Point position)
        {
            DockBlock lockHit = HitTestPanelLockButton(position);
            if (lockHit == null)
            {
                return false;
            }

            PanelGroup group = GetPanelGroupForBlock(lockHit);
            if (group != null)
                TogglePanelLock(group);
            else
                ToggleBlockLock(lockHit);
            ClearDockingInteractions();
            return true;
        }

        /// <summary>
        /// Hit-tests the merge button and, if clicked, merges the overlay block (or
        /// all tabs of the overlay panel) into the parent panel group.
        /// </summary>
        private static bool TryOverlayMerge(Point position)
        {
            int standardDbh = GetActiveDragBarHeight();

            // Only check overlay blocks.
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay) continue;

                int dbh = UIStyle.DragBarHeight;
                Rectangle mergeBounds = GetMergeButtonBounds(block, dbh);
                if (mergeBounds == Rectangle.Empty || !mergeBounds.Contains(position)) continue;

                // Find the parent panel group to merge into.
                if (string.IsNullOrEmpty(block.OverlayParentId)) return false;
                if (!_blocks.TryGetValue(block.OverlayParentId, out DockBlock parent)) return false;
                PanelGroup parentGroup = GetPanelGroupForBlock(parent);
                if (parentGroup == null) return false;

                PanelGroup overlayGroup = GetPanelGroupForBlock(block);
                if (overlayGroup == null) return false;

                // Collect all blocks from the overlay panel.
                List<DockBlock> blocksToMerge = overlayGroup.Blocks.ToList();

                // Remove each block from the overlay group and add to the parent group.
                foreach (DockBlock overlayBlock in blocksToMerge)
                {
                    overlayGroup.RemoveBlock(overlayBlock.Id, out _);
                    _blockToPanel.Remove(overlayBlock.Id);

                    overlayBlock.IsOverlay = false;
                    overlayBlock.OverlayParentId = null;
                    overlayBlock.IsVisible = false;

                    parentGroup.AddBlock(overlayBlock);
                    MapBlockToPanel(overlayBlock, parentGroup);
                }

                // Clean up the now-empty overlay panel group.
                if (overlayGroup.Blocks.Count == 0)
                {
                    RemovePanelGroup(overlayGroup);
                }

                // Activate the first merged block in the parent panel.
                if (blocksToMerge.Count > 0)
                {
                    DockBlock firstMerged = blocksToMerge[0];
                    SetPanelActiveBlock(parentGroup, firstMerged);
                    firstMerged.IsVisible = true;
                }

                RebuildGroupBarLayoutCache(GetActiveDragBarHeight());
                MarkLayoutDirty();
                return true;
            }

            return false;
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

            // Per-block (tab) locks always apply — they govern block content interaction
            // regardless of whether docking mode is active.
            if (IsBlockLockEnabled(block))
            {
                return true;
            }

            // Panel group locks only restrict block content outside docking mode.
            // In docking mode, panel locks govern layout management (drag bars, group bars),
            // not block content.
            if (!DockingModeEnabled)
            {
                PanelGroup group = GetPanelGroupForBlock(block);
                if (group != null && group.IsLocked)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the mouse state a block should pass to its scroll panel.
        /// When the block is locked but docking mode is active, the returned state
        /// preserves scroll-wheel and position data (so the scroll panel can detect
        /// hover and compute wheel delta) while suppressing all button input.
        /// </summary>
        internal static MouseState GetScrollMouseState(bool blockLocked, MouseState mouseState, MouseState previousMouseState)
        {
            if (!blockLocked)
                return mouseState;
            if (!DockingModeEnabled)
                return previousMouseState;
            // Docking mode + locked: allow scroll wheel and left-click (for scrollbar dragging),
            // suppress all other buttons.
            return new MouseState(
                mouseState.X, mouseState.Y,
                mouseState.ScrollWheelValue,
                mouseState.LeftButton, ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released);
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

            // BlockMode temporarily makes all locked panels accessible as if unlocked.
            if (DockingModeEnabled)
            {
                return false;
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
            int standardDbh = GetActiveDragBarHeight();

            // Two passes: overlay blocks first so they intercept hits in regions they cover.
            for (int pass = 0; pass < 2; pass++)
            {
                bool overlayPass = pass == 0;
                foreach (DockBlock block in _orderedBlocks)
                {
                    if (!block.IsVisible || block.IsOverlay != overlayPass)
                    {
                        continue;
                    }

                    int dbh = block.IsOverlay ? UIStyle.DragBarHeight : standardDbh;
                    if (dbh <= 0) continue;

                    Rectangle dragBarRect = block.GetDragBarBounds(dbh);
                    if (!dragBarRect.Contains(position))
                    {
                        continue;
                    }

                    if (excludeDragBarButtons && IsPointOnDragBarButton(block, dbh, position))
                    {
                        continue;
                    }

                    if (requireGrabRegion)
                    {
                        PanelGroup group = GetPanelGroupForBlock(block);
                        Rectangle grabBounds = GetDragBarGrabBounds(block, group, dbh);
                        if (grabBounds == Rectangle.Empty || !grabBounds.Contains(position))
                        {
                            continue;
                        }
                    }

                    return block;
                }
            }

            return null;
        }

        private static DockBlock HitTestCloseButton(Point position)
        {
            int standardDbh = GetActiveDragBarHeight();

            for (int pass = 0; pass < 2; pass++)
            {
                bool overlayPass = pass == 0;
                foreach (DockBlock block in _orderedBlocks)
                {
                    if (!block.IsVisible || block.IsOverlay != overlayPass)
                    {
                        continue;
                    }

                    int dbh = block.IsOverlay ? UIStyle.DragBarHeight : standardDbh;
                    if (dbh <= 0) continue;

                    Rectangle closeBounds = GetCloseButtonBounds(block, dbh);
                    if (closeBounds != Rectangle.Empty && closeBounds.Contains(position))
                    {
                        return block;
                    }
                }
            }

            return null;
        }

        private static DockBlock HitTestPanelLockButton(Point position)
        {
            int standardDbh = GetActiveDragBarHeight();

            for (int pass = 0; pass < 2; pass++)
            {
                bool overlayPass = pass == 0;
                foreach (DockBlock block in _orderedBlocks)
                {
                    if (!block.IsVisible || block.IsOverlay != overlayPass)
                    {
                        continue;
                    }

                    int dbh = block.IsOverlay ? UIStyle.DragBarHeight : standardDbh;
                    if (dbh <= 0) continue;

                    Rectangle panelLockBounds = GetPanelLockButtonBounds(block, dbh);
                    if (panelLockBounds != Rectangle.Empty && panelLockBounds.Contains(position))
                    {
                        return block;
                    }
                }
            }

            return null;
        }

        private static bool BlockHasOpacitySlider(DockBlock block) =>
            block != null;

        // Returns the bounds of the opacity row that appears below the header bar when hovered.
        private static Rectangle GetOpacityRowBounds(DockBlock block, PanelGroup group, int dragBarHeight)
        {
            if (!BlockHasOpacitySlider(block) ||
                (!string.Equals(_hoveredDragBarId, block.Id, StringComparison.Ordinal) &&
                 !string.Equals(_opacityExpandedId, block.Id, StringComparison.Ordinal)))
            {
                return Rectangle.Empty;
            }

            // Only expand when neither the panel nor the block is locked
            if ((group != null && IsPanelLocked(group)) || IsBlockLocked(block))
            {
                return Rectangle.Empty;
            }

            int groupBarH = GetGroupBarHeight(group);
            int headerHeight = Math.Max(dragBarHeight, groupBarH);
            int rowY = block.Bounds.Y + headerHeight;
            if (rowY + OpacityRowHeight > block.Bounds.Bottom)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(block.Bounds.X, rowY, block.Bounds.Width, OpacityRowHeight);
        }

        private static void GetOpacitySliderBounds(DockBlock block, int dragBarHeight, out Rectangle trackBounds, out Rectangle labelBounds)
        {
            trackBounds = Rectangle.Empty;
            labelBounds = Rectangle.Empty;

            if (!BlockHasOpacitySlider(block) || dragBarHeight <= 0)
            {
                return;
            }

            PanelGroup group = GetPanelGroupForBlock(block);
            Rectangle row = GetOpacityRowBounds(block, group, dragBarHeight);
            if (row == Rectangle.Empty)
            {
                return;
            }

            UIStyle.UIFont font = UIStyle.FontBody;
            int labelH = font.IsAvailable ? (int)font.LineHeight : 14;
            // Size the label column on the widest possible string so the track doesn't shift while dragging.
            // "Global Transparency: 100%" is the longest possible label.
            int labelW = font.IsAvailable ? (int)MathF.Ceiling(font.MeasureString("Global Transparency: 100%").X) : 120;

            int available = row.Width - DragBarButtonPadding * 2;
            int trackW = Math.Min(OpacitySliderMaxTrackWidth, Math.Max(0, available - labelW - OpacitySliderLabelSpacing));
            if (trackW < 20)
            {
                return;
            }

            int totalW = labelW + OpacitySliderLabelSpacing + trackW;
            // Center the assembly when the row is wider than the total content
            int startX = row.X + DragBarButtonPadding + Math.Max(0, (available - totalW) / 2);

            int labelY = row.Y + (row.Height - labelH) / 2;
            labelBounds = new Rectangle(startX, labelY, labelW, labelH);

            const int trackH = 3;
            int trackY = row.Y + (row.Height - trackH) / 2;
            trackBounds = new Rectangle(startX + labelW + OpacitySliderLabelSpacing, trackY, trackW, trackH);
        }

        private static void DrawOpacitySlider(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight)
        {
            GetOpacitySliderBounds(block, dragBarHeight, out Rectangle track, out Rectangle label);
            if (track == Rectangle.Empty)
            {
                return;
            }

            float opacity = MathHelper.Clamp(block.BackgroundOpacity, 0f, 1f);
            // The slider is expressed as transparency (0% = fully opaque, 100% = fully transparent)
            float transparency = 1f - opacity;

            // Label — grey "Global/Local Transparency: X%" to the left of the track
            UIStyle.UIFont font = UIStyle.FontBody;
            if (font.IsAvailable && label != Rectangle.Empty)
            {
                string transparencyKind = block.IsOverlay ? "Local" : "Global";
                string labelText = $"{transparencyKind} Transparency: {(int)Math.Round(transparency * 100f)}%";
                font.DrawString(spriteBatch, labelText, new Vector2(label.X, label.Y), UIStyle.MutedTextColor);
            }

            // Track background — thin dark rail
            DrawRect(spriteBatch, track, ColorPalette.SliderTrack);

            // Track fill grows left→right as transparency increases (right = 100% transparent)
            int fillW = (int)(track.Width * transparency);
            if (fillW > 0)
            {
                DrawRect(spriteBatch, new Rectangle(track.X, track.Y, fillW, track.Height), ColorPalette.SliderFill);
            }

            // Thumb handle — slightly taller white rectangle at fill end
            const int thumbW = 3;
            const int thumbH = 10;
            int thumbX = track.X + fillW - thumbW / 2;
            thumbX = Math.Clamp(thumbX, track.X, track.Right - thumbW);
            int thumbY = track.Y - (thumbH - track.Height) / 2;
            DrawRect(spriteBatch, new Rectangle(thumbX, thumbY, thumbW, thumbH), Color.White);
        }

        private static DockBlock HitTestOpacitySlider(Point position, out Rectangle trackBounds)
        {
            trackBounds = Rectangle.Empty;
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0 || _opacityExpandedId == null)
            {
                return null;
            }

            // Only allow slider interaction when the opacity row is actually visible (the drag bar was
            // hovered in a prior frame and _opacityExpandedId was set). This prevents clicking the
            // invisible slider through the collapsed panel.
            if (!_blocks.TryGetValue(_opacityExpandedId, out DockBlock expandedBlock) || !expandedBlock.IsVisible ||
                !BlockHasOpacitySlider(expandedBlock))
            {
                return null;
            }

            string savedHoveredId = _hoveredDragBarId;
            _hoveredDragBarId = _opacityExpandedId;

            GetOpacitySliderBounds(expandedBlock, dragBarHeight, out Rectangle track, out _);

            if (track != Rectangle.Empty)
            {
                PanelGroup group = GetPanelGroupForBlock(expandedBlock);
                Rectangle row = GetOpacityRowBounds(expandedBlock, group, dragBarHeight);
                Rectangle hitArea = row != Rectangle.Empty ? row : track;

                if (hitArea.Contains(position))
                {
                    _hoveredDragBarId = savedHoveredId;
                    trackBounds = track;
                    return expandedBlock;
                }
            }

            _hoveredDragBarId = savedHoveredId;
            return null;
        }

        private static void UpdateOpacitySliderDrag()
        {
            if (!_opacitySliderDragging || _opacitySliderTrackBounds == Rectangle.Empty)
            {
                return;
            }

            if (!_blocks.TryGetValue(_opacitySliderDraggingId ?? string.Empty, out DockBlock block))
            {
                return;
            }

            float relX = (_mousePosition.X - _opacitySliderTrackBounds.X) / (float)Math.Max(1, _opacitySliderTrackBounds.Width);
            // relX represents transparency (0=left=opaque, 1=right=fully transparent).
            // Snap to multiples of 5% (steps of 0.05) then convert to opacity.
            float snappedTransparency = MathF.Round(MathHelper.Clamp(relX, 0f, 1f) * 20f) / 20f;
            block.BackgroundOpacity = 1f - snappedTransparency;
        }

        private static bool IsPointOnDragBarButton(DockBlock block, int dragBarHeight, Point position)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds, out Rectangle mergeBounds);
            return (panelLockBounds != Rectangle.Empty && panelLockBounds.Contains(position)) ||
                (closeBounds != Rectangle.Empty && closeBounds.Contains(position)) ||
                (mergeBounds != Rectangle.Empty && mergeBounds.Contains(position));
        }

        private static Rectangle GetCloseButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out _, out Rectangle closeBounds, out _);
            return closeBounds;
        }

        private static Rectangle GetPanelLockButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockBounds, out _, out _);
            return panelLockBounds;
        }

        private static Rectangle GetMergeButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out _, out _, out Rectangle mergeBounds);
            return mergeBounds;
        }

        private static void GetDragBarButtonBounds(DockBlock block, int dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds)
        {
            GetDragBarButtonBounds(block, dragBarHeight, out panelLockBounds, out closeBounds, out _);
        }

        private static void GetDragBarButtonBounds(DockBlock block, int dragBarHeight, out Rectangle panelLockBounds, out Rectangle closeBounds, out Rectangle mergeButtonBounds)
        {
            panelLockBounds = Rectangle.Empty;
            closeBounds = Rectangle.Empty;
            mergeButtonBounds = Rectangle.Empty;

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
            int minimumX = dragBarRect.X + DragBarButtonPadding;

            int lockX = closeBounds.X - DragBarButtonSpacing - buttonSize;
            int y = closeBounds.Y;
            if (lockX < minimumX)
            {
                return;
            }

            panelLockBounds = new Rectangle(lockX, y, buttonSize, buttonSize);

            // Merge button only for overlay blocks — to the left of the lock button.
            if (block.IsOverlay)
            {
                int mergeX = lockX - DragBarButtonSpacing - buttonSize;
                if (mergeX >= minimumX)
                {
                    mergeButtonBounds = new Rectangle(mergeX, y, buttonSize, buttonSize);
                }
            }
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

        /// <summary>
        /// Returns true when <paramref name="a"/> and <paramref name="b"/> share an edge
        /// along the given split orientation.  Horizontal orientation means stacked
        /// vertically (shared top/bottom edge); Vertical means side-by-side (shared
        private static Rectangle ComputeSuperimposeZone(Rectangle bounds)
        {
            int shortSide = Math.Min(bounds.Width, bounds.Height);
            int zoneSide = Math.Min((int)(shortSide * SuperimposeZoneFraction), SuperimposeZoneMaxSide);
            int zoneX = bounds.X + (bounds.Width - zoneSide) / 2;
            int zoneY = bounds.Y + (bounds.Height - zoneSide) / 2;
            return new Rectangle(zoneX, zoneY, zoneSide, zoneSide);
        }

        private static DockDropPreview? BuildSuperimposePreview(DockBlock block, Rectangle bounds, Rectangle superimposeZone, Point position)
        {
            int minW = _draggingBlock?.MinWidth ?? 60;
            int minH = _draggingBlock?.MinHeight ?? 40;
            // Cap preview size at 40% of parent so it doesn't look like it takes over the whole panel
            int maxW = Math.Max(minW, (int)(bounds.Width * 0.4f));
            int maxH = Math.Max(minH, (int)(bounds.Height * 0.4f));
            int projW = Math.Clamp(_draggingStartBounds.Width > 0 ? _draggingStartBounds.Width : minW, minW, maxW);
            int projH = Math.Clamp(_draggingStartBounds.Height > 0 ? _draggingStartBounds.Height : minH, minH, maxH);
            // Position preview centered on the mouse cursor, clamped to parent bounds
            int maxDropX = Math.Max(bounds.X, bounds.Right - projW);
            int maxDropY = Math.Max(bounds.Y, bounds.Bottom - projH);
            int projX = Math.Clamp(position.X - projW / 2, bounds.X, maxDropX);
            int projY = Math.Clamp(position.Y - projH / 2, bounds.Y, maxDropY);
            return new DockDropPreview
            {
                TargetBlock = block,
                IsOverlayDrop = true,
                OverlayDropPosition = position,
                HighlightBounds = new Rectangle(projX, projY, projW, projH)
            };
        }

        private static DockDropPreview? BuildDropPreview(Point position)
        {
            // Once the user enters a superimpose zone, lock preview to that parent panel until drag ends.
            if (_superimposeLocked && _superimposeLockedTarget != null && _superimposeLockedTarget.IsVisible)
            {
                Rectangle parentBounds = _superimposeLockedTarget.Bounds;
                if (parentBounds.Contains(position))
                {
                    Rectangle zone = ComputeSuperimposeZone(parentBounds);
                    if (zone.Contains(position))
                    {
                        return BuildSuperimposePreview(_superimposeLockedTarget, parentBounds, zone, position);
                    }

                    // Cursor left the superimpose zone but is still inside the panel —
                    // unlock so edge-based drops (Top/Bottom/Left/Right) can be detected.
                    _superimposeLocked = false;
                    _superimposeLockedTarget = null;
                }
                else
                {
                    // Cursor left the parent panel; return null so releasing outside cancels the drag.
                    _superimposeLocked = false;
                    _superimposeLockedTarget = null;
                    return null;
                }
            }

            if (_draggingFromTab)
            {
                DockDropPreview? tabPreview = BuildTabDropPreview(position);
                if (tabPreview.HasValue)
                {
                    return tabPreview;
                }

                // Tab drags that leave the tab strip fall through to the same
                // adjacency / edge / superimpose logic used by drag-bar drags,
                // so dragging a tab upward can displace the block above.
            }

            // Single pass: find the block under the cursor. Check superimpose zone
            // first, then use 4-hemisphere edge detection (whichever axis has the
            // larger displacement from center wins). Adjacent and non-adjacent blocks
            // use the same logic so all four edges are always available.
            DockDropPreview? preview = null;

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || block.IsOverlay || block == _draggingBlock)
                    continue;

                Rectangle bounds = block.Bounds;
                if (!bounds.Contains(position))
                    continue;

                // Center superimpose zone — always checked regardless of adjacency.
                Rectangle superimposeZone = ComputeSuperimposeZone(bounds);
                if (superimposeZone.Contains(position))
                {
                    _superimposeLocked = true;
                    _superimposeLockedTarget = block;
                    preview = BuildSuperimposePreview(block, bounds, superimposeZone, position);
                    break;
                }

                // 4-hemisphere edge detection: the axis with the larger offset from
                // center determines whether Top/Bottom or Left/Right is chosen.
                float relativeX = (position.X - bounds.X) / (float)Math.Max(1, bounds.Width);
                float relativeY = (position.Y - bounds.Y) / (float)Math.Max(1, bounds.Height);
                float distX = Math.Abs(relativeX - 0.5f);
                float distY = Math.Abs(relativeY - 0.5f);

                DockEdge edge;
                if (distY >= distX)
                    edge = relativeY <= 0.5f ? DockEdge.Top : DockEdge.Bottom;
                else
                    edge = relativeX <= 0.5f ? DockEdge.Left : DockEdge.Right;

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
                DebugLogger.PrintUI($"[BuildDropPreview] Result: Target={block.Id} Edge={edge} relY={relativeY:F3} relX={relativeX:F3}");
                break;
            }

            // Viewport snap: only when cursor is not over any block.
            if (!preview.HasValue)
            {
                preview = BuildViewportSnapPreview(position);
            }

            if (!preview.HasValue)
            {
                DebugLogger.PrintUI($"[BuildDropPreview] NoPreview dragging={_draggingBlock?.Id} fromTab={_draggingFromTab} startBounds={_draggingStartBounds} mouse={position}");
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

            position = GetResizeEdgePosition(handle, position);
            ApplyResizeEdgeDragInternal(handle, position);
        }

        private static void ApplyResizeEdgeDragInternal(ResizeEdge handle, Point position)
        {
            Rectangle bounds = handle.Node.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            float previousRatio = handle.Node.SplitRatio;
            float newRatio;

            int axisPositionRaw = handle.Orientation == DockSplitOrientation.Vertical ? position.X - bounds.X : position.Y - bounds.Y;

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

                MarkLayoutDirty();
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

        /// <summary>
        /// After DetachDraggingTabFromGroup, the dragging block may have been fully
        /// removed from _blockNodes / _panelGroups (single-block panel case).
        /// This helper re-creates the node and panel infrastructure so subsequent
        /// layout operations (Detach / InsertRelative) can find the block.
        /// </summary>
        private static void EnsureDraggingBlockHasNode()
        {
            if (_draggingBlock == null)
                return;

            string id = _draggingBlock.Id;

            if (!_blockNodes.TryGetValue(id, out BlockNode node))
            {
                node = new BlockNode(_draggingBlock);
                _blockNodes[id] = node;
            }

            if (GetPanelGroupForBlock(_draggingBlock) == null)
            {
                _panelGroups[id] = new PanelGroup(id, _draggingBlock);
                _panelNodes[id] = node;
                _blockToPanel[id] = id;
                if (!_orderedPanelIds.Contains(id))
                    _orderedPanelIds.Add(id);
            }
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
                EnsureDraggingBlockHasNode();
                ApplyViewportSnap(preview.Edge);
                return;
            }

            if (preview.IsOverlayDrop)
            {
                ApplyOverlayDrop(preview);
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
            EnsureDraggingBlockHasNode();

            if (!_blockNodes.TryGetValue(_draggingBlock.Id, out BlockNode movingNode) ||
                !_blockNodes.TryGetValue(preview.TargetBlock.Id, out BlockNode targetNode))
            {
                return;
            }

            DebugLogger.PrintUI($"[ApplyDrop] Moving={_draggingBlock.Id} Target={preview.TargetBlock.Id} Edge={preview.Edge}");
            _rootNode = DockLayout.Detach(_rootNode, movingNode);
            _rootNode ??= targetNode;
            _rootNode = DockLayout.InsertRelative(_rootNode, movingNode, targetNode, preview.Edge);
            MarkLayoutDirty();
        }

        private static void ApplyOverlayDrop(DockDropPreview preview)
        {
            if (_draggingBlock == null || preview.TargetBlock == null)
            {
                return;
            }

            DockBlock block = _draggingBlock;
            DockBlock parent = preview.TargetBlock;
            if (block == parent)
            {
                return;
            }

            DetachDraggingTabFromGroup();

            // Remove from layout tree
            if (_blockNodes.TryGetValue(block.Id, out BlockNode blockNode))
            {
                _rootNode = DockLayout.Detach(_rootNode, blockNode);
                _blockNodes.Remove(block.Id);
            }

            // Remove from panel group
            PanelGroup group = GetPanelGroupForBlock(block);
            if (group != null)
            {
                group.RemoveBlock(block.Id, out _);
                _blockToPanel.Remove(block.Id);
                if (group.Blocks.Count == 0)
                {
                    RemovePanelGroup(group);
                }
                else if (string.Equals(group.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
                {
                    DockBlock next = group.Blocks.FirstOrDefault();
                    if (next != null)
                    {
                        SetPanelActiveBlock(group, next);
                    }
                }
            }

            Rectangle parentBounds = parent.Bounds;
            if (parentBounds.Width <= 0 || parentBounds.Height <= 0)
            {
                return;
            }

            int projW = Math.Clamp(block.Bounds.Width > 0 ? block.Bounds.Width : 60, block.MinWidth, Math.Max(block.MinWidth, (int)(parentBounds.Width * 0.4f)));
            int projH = Math.Clamp(block.Bounds.Height > 0 ? block.Bounds.Height : 40, block.MinHeight, Math.Max(block.MinHeight, (int)(parentBounds.Height * 0.4f)));

            float centerX = (preview.OverlayDropPosition.X - parentBounds.X) - projW / 2f;
            float centerY = (preview.OverlayDropPosition.Y - parentBounds.Y) - projH / 2f;

            block.OverlayRelX = MathHelper.Clamp(centerX / parentBounds.Width, 0f, Math.Max(0f, 1f - (float)projW / parentBounds.Width));
            block.OverlayRelY = MathHelper.Clamp(centerY / parentBounds.Height, 0f, Math.Max(0f, 1f - (float)projH / parentBounds.Height));
            block.OverlayRelWidth = MathHelper.Clamp((float)projW / parentBounds.Width, 0.05f, 1f);
            block.OverlayRelHeight = MathHelper.Clamp((float)projH / parentBounds.Height, 0.05f, 1f);
            block.IsOverlay = true;
            block.OverlayParentId = parent.Id;
            block.IsVisible = true;

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

                // If the target is an overlay panel, inherit overlay properties on the newly-added block.
                DockBlock existingOverlayMember = targetGroup.Blocks.FirstOrDefault(
                    b => b.IsOverlay && !ReferenceEquals(b, _draggingBlock));
                if (existingOverlayMember != null && !_draggingBlock.IsOverlay)
                {
                    if (_blockNodes.TryGetValue(_draggingBlock.Id, out BlockNode mergeNode))
                    {
                        _rootNode = DockLayout.Detach(_rootNode, mergeNode);
                        _blockNodes.Remove(_draggingBlock.Id);
                    }
                    _draggingBlock.IsOverlay = true;
                    _draggingBlock.OverlayParentId = existingOverlayMember.OverlayParentId;
                    _draggingBlock.OverlayRelX = existingOverlayMember.OverlayRelX;
                    _draggingBlock.OverlayRelY = existingOverlayMember.OverlayRelY;
                    _draggingBlock.OverlayRelWidth = existingOverlayMember.OverlayRelWidth;
                    _draggingBlock.OverlayRelHeight = existingOverlayMember.OverlayRelHeight;
                }
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
                if (!block.IsVisible || block.IsOverlay)
                {
                    continue;
                }

                DrawBlockBackground(spriteBatch, block, dragBarHeight);
                DrawBlockContent(spriteBatch, block, dragBarHeight);
            }

            bool mouseBlockedByOverlay = IsMouseCoveredByOverlayGroupBar(dragBarHeight);
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || block.IsOverlay)
                {
                    continue;
                }

                RedrawBlockHeader(spriteBatch, block, dragBarHeight, mouseBlockedByOverlay);
            }

            // Overlay drag/resize chrome is always drawn; standard block chrome only in docking mode.
            DrawOverlayResizeChrome(spriteBatch);

            if (_draggingBlock != null && _draggingBlock.IsOverlay)
            {
                // Subtle parent context — shows the constrained movement region.
                if (!string.IsNullOrEmpty(_draggingBlock.OverlayParentId) &&
                    _blocks.TryGetValue(_draggingBlock.OverlayParentId, out DockBlock overlayParent) &&
                    overlayParent.IsVisible)
                {
                    DrawRect(spriteBatch, overlayParent.Bounds, UIStyle.AccentMuted * 0.08f);
                    DrawRectOutline(spriteBatch, overlayParent.Bounds, UIStyle.AccentColor * 0.35f, 1);
                }

                // Drop-target highlight: brighter accent outline on the block the overlay would re-parent to.
                if (_overlayDropTargetBlock != null && _overlayDropTargetBlock.IsVisible)
                {
                    DrawRect(spriteBatch, _overlayDropTargetBlock.Bounds, UIStyle.AccentColor * 0.12f);
                    DrawRectOutline(spriteBatch, _overlayDropTargetBlock.Bounds, UIStyle.AccentColor * 0.85f, 2);
                }

                // Ghost preview: precise outline showing where the panel will land on release.
                if (_overlayDragPreviewBounds.HasValue)
                {
                    Rectangle ghost = _overlayDragPreviewBounds.Value;
                    DrawRect(spriteBatch, ghost, UIStyle.BlockBackground * 0.75f);
                    int previewBarH = Math.Min(UIStyle.DragBarHeight, ghost.Height);
                    DrawRect(spriteBatch, new Rectangle(ghost.X, ghost.Y, ghost.Width, previewBarH), UIStyle.DragBarBackground * 0.95f);
                    // Draw resize edges translated to the ghost position.
                    Point edgeOffset = new(ghost.X - _draggingStartBounds.X, ghost.Y - _draggingStartBounds.Y);
                    foreach (OverlayResizeEdge edge in _overlayResizeEdges)
                    {
                        if (!ReferenceEquals(edge.Overlay, _draggingBlock)) continue;
                        DrawRect(spriteBatch, new Rectangle(edge.Bounds.X + edgeOffset.X, edge.Bounds.Y + edgeOffset.Y, edge.Bounds.Width, edge.Bounds.Height), UIStyle.ResizeEdgeActiveColor);
                    }
                    foreach (OverlayCornerHandle corner in _overlayCornerHandles)
                    {
                        if (!ReferenceEquals(corner.VerticalEdge.Overlay, _draggingBlock)) continue;
                        DrawRect(spriteBatch, new Rectangle(corner.Bounds.X + edgeOffset.X, corner.Bounds.Y + edgeOffset.Y, corner.Bounds.Width, corner.Bounds.Height), UIStyle.ResizeEdgeActiveColor);
                    }
                    DrawRectOutline(spriteBatch, ghost, UIStyle.AccentColor * 0.8f, UIStyle.DragOutlineThickness);
                }
            }

            if (showDockingChrome)
            {
                DrawResizeEdges(spriteBatch);
                DrawCornerHandles(spriteBatch);

                if (_draggingBlock != null && !_draggingBlock.IsOverlay)
                {
                    foreach (DockBlock block in _orderedBlocks)
                    {
                        if (!block.IsVisible || block.IsOverlay || block == _draggingBlock)
                        {
                            continue;
                        }

                        DrawSuperimposeZone(spriteBatch, block);
                    }
                }
            }

            // Suppress standard floating preview for overlay drags — the overlay ghost preview above
            // already shows the destination, so an unconstrained mouse-position ghost would be confusing.
            if (_draggingBlock != null && !_draggingBlock.IsOverlay &&
                !(_dropPreview.HasValue && _dropPreview.Value.IsOverlayDrop))
            {
                DrawFloatingBlockPreview(spriteBatch, dragBarHeight);
            }

            if (_dropPreview.HasValue)
            {
                if (_dropPreview.Value.IsOverlayDrop)
                {
                    DrawRect(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.BlockBackground * 0.75f);
                    DrawRectOutline(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentColor, UIStyle.DragOutlineThickness);
                }
                else
                {
                    DrawRect(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentMuted);
                    DrawRectOutline(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentColor, UIStyle.DragOutlineThickness);
                }
            }

            DrawOverlayBlocks(spriteBatch, dragBarHeight);
        }

        private static void DrawSuperimposeZone(SpriteBatch spriteBatch, DockBlock block)
        {
            Rectangle zone = ComputeSuperimposeZone(block.Bounds);
            if (zone.Width < 20 || zone.Height < 20)
            {
                return;
            }

            bool isHit = _dropPreview.HasValue && _dropPreview.Value.IsOverlayDrop &&
                         _dropPreview.Value.TargetBlock == block;

            Color fill = isHit ? UIStyle.AccentMuted * 0.5f : UIStyle.AccentMuted * 0.2f;
            Color border = isHit ? UIStyle.AccentColor : UIStyle.AccentColor * 0.5f;
            DrawRect(spriteBatch, zone, fill);
            DrawRectOutline(spriteBatch, zone, border, 1);

            UIStyle.UIFont font = UIStyle.FontBody;
            if (font.IsAvailable)
            {
                const string label = "Superimpose";
                Vector2 size = font.MeasureString(label);
                if (size.X <= zone.Width - 4 && size.Y <= zone.Height - 4)
                {
                    Vector2 pos = new(zone.X + (zone.Width - size.X) / 2f, zone.Y + (zone.Height - size.Y) / 2f);
                    font.DrawString(spriteBatch, label, pos, border);
                }
            }
        }

        private static void DrawOverlayBlocks(SpriteBatch spriteBatch, int dragBarHeight)
        {
            // Overlays always show drag bars regardless of docking mode.
            int overlayDbh = UIStyle.DragBarHeight;

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay)
                {
                    continue;
                }

                DrawBlockBackground(spriteBatch, block, overlayDbh);
                DrawBlockContent(spriteBatch, block, overlayDbh);
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay)
                {
                    continue;
                }

                RedrawBlockHeader(spriteBatch, block, overlayDbh);
            }
        }

        private static void DrawResizeEdges(SpriteBatch spriteBatch)
        {
            foreach (ResizeEdge handle in _resizeEdges)
            {
                Color color = UIStyle.ResizeEdgeColor;
                bool isActive = _activeResizeEdge.HasValue && ReferenceEquals(handle.Node, _activeResizeEdge.Value.Node);
                bool isHovered = _hoveredResizeEdge.HasValue && ReferenceEquals(handle.Node, _hoveredResizeEdge.Value.Node);

                if (!isActive && _activeCornerHandle.HasValue && CornerContainsResizeEdge(_activeCornerHandle.Value, handle))
                    isActive = true;
                if (!isHovered && _hoveredCornerHandle.HasValue && CornerContainsResizeEdge(_hoveredCornerHandle.Value, handle))
                    isHovered = true;

                if (isActive)
                    color = UIStyle.ResizeEdgeActiveColor;
                else if (isHovered)
                    color = UIStyle.ResizeEdgeHoverColor;

                DrawRect(spriteBatch, handle.Bounds, color);
            }
            // Overlay resize edges/corners drawn by DrawOverlayResizeChrome (always-on path).
        }

        /// <summary>
        /// Draws overlay resize edges and corner handles. Called from both docking
        /// and non-docking draw paths so overlays are always resizable.
        /// </summary>
        private static void DrawOverlayResizeChrome(SpriteBatch spriteBatch)
        {
            foreach (OverlayResizeEdge edge in _overlayResizeEdges)
            {
                Color color = UIStyle.ResizeEdgeColor;

                bool isActive = (_activeOverlayResizeEdge.HasValue && ReferenceEquals(_activeOverlayResizeEdge.Value.Overlay, edge.Overlay) && _activeOverlayResizeEdge.Value.Side == edge.Side)
                    || (_activeOverlayCornerHandle.HasValue &&
                        (ReferenceEquals(_activeOverlayCornerHandle.Value.VerticalEdge.Overlay, edge.Overlay) &&
                         (_activeOverlayCornerHandle.Value.VerticalEdge.Side == edge.Side || _activeOverlayCornerHandle.Value.HorizontalEdge.Side == edge.Side)));

                bool isHovered = !isActive &&
                    ((_hoveredOverlayResizeEdge.HasValue && ReferenceEquals(_hoveredOverlayResizeEdge.Value.Overlay, edge.Overlay) && _hoveredOverlayResizeEdge.Value.Side == edge.Side)
                    || (_hoveredOverlayCornerHandle.HasValue &&
                        (ReferenceEquals(_hoveredOverlayCornerHandle.Value.VerticalEdge.Overlay, edge.Overlay) &&
                         (_hoveredOverlayCornerHandle.Value.VerticalEdge.Side == edge.Side || _hoveredOverlayCornerHandle.Value.HorizontalEdge.Side == edge.Side))));

                if (isActive)
                    color = UIStyle.ResizeEdgeActiveColor;
                else if (isHovered)
                    color = UIStyle.ResizeEdgeHoverColor;

                DrawRect(spriteBatch, edge.Bounds, color);
            }

            foreach (OverlayCornerHandle corner in _overlayCornerHandles)
            {
                Color color = UIStyle.ResizeEdgeColor;

                bool isActive = _activeOverlayCornerHandle.HasValue &&
                    ReferenceEquals(_activeOverlayCornerHandle.Value.VerticalEdge.Overlay, corner.VerticalEdge.Overlay) &&
                    _activeOverlayCornerHandle.Value.VerticalEdge.Side == corner.VerticalEdge.Side &&
                    _activeOverlayCornerHandle.Value.HorizontalEdge.Side == corner.HorizontalEdge.Side;

                bool isHovered = !isActive && _hoveredOverlayCornerHandle.HasValue &&
                    ReferenceEquals(_hoveredOverlayCornerHandle.Value.VerticalEdge.Overlay, corner.VerticalEdge.Overlay) &&
                    _hoveredOverlayCornerHandle.Value.VerticalEdge.Side == corner.VerticalEdge.Side &&
                    _hoveredOverlayCornerHandle.Value.HorizontalEdge.Side == corner.HorizontalEdge.Side;

                if (isActive)
                    color = UIStyle.ResizeEdgeActiveColor;
                else if (isHovered)
                    color = UIStyle.ResizeEdgeHoverColor;

                DrawRect(spriteBatch, corner.Bounds, color);
            }
        }

        private static void DrawBlockBackground(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight)
        {
            PanelGroup group = GetPanelGroupForBlock(block);

            // Determine background color based on opacity
            Color bgColor;
            if (block.IsOverlay)
            {
                // Overlay blocks blend into the parent; never punch a chroma-key hole through the window.
                if (BlockHasOpacitySlider(block) && block.BackgroundOpacity <= 0f)
                    return; // fully transparent — skip drawing background so parent content shows through
                bgColor = BlockHasOpacitySlider(block) && block.BackgroundOpacity < 1f
                    ? UIStyle.BlockBackground * block.BackgroundOpacity
                    : UIStyle.BlockBackground;
            }
            else if (BlockHasOpacitySlider(block) && block.BackgroundOpacity <= 0f)
            {
                // Fully transparent blank block: punch through to OS window background
                DrawRect(spriteBatch, block.Bounds, Core.TransparentWindowColor);
                DrawRectOutline(spriteBatch, block.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
                return;
            }
            else if (BlockHasOpacitySlider(block) && block.BackgroundOpacity < 1f)
            {
                // Partial global transparency: the window uses a color-key and does NOT support
                // per-pixel alpha, so we simulate partial transparency with horizontal stripe dithering.
                // Each 1-px row is either the key color (transparent to OS) or the opaque background,
                // distributed evenly via a Bresenham error-term so the density matches the opacity.
                DrawGlobalTransparentDithered(spriteBatch, block.Bounds, block.BackgroundOpacity);
                DrawRectOutline(spriteBatch, block.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
                return;
            }
            else
            {
                bgColor = UIStyle.BlockBackground;
            }

            DrawRect(spriteBatch, block.Bounds, bgColor);
            DrawRectOutline(spriteBatch, block.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
        }

        // Simulates partial global (window) transparency using 1-px horizontal stripe dithering.
        // The window uses LWA_COLORKEY so only exact key-color pixels are transparent.
        // Rows are distributed with a Bresenham error term so the opaque fraction equals `opacity`.
        private static void DrawGlobalTransparentDithered(SpriteBatch spriteBatch, Rectangle bounds, float opacity)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // Transparency fraction (0 = fully opaque, 1 = fully transparent)
            float transparency = 1f - MathHelper.Clamp(opacity, 0f, 1f);

            int totalRows = bounds.Height;
            // Number of transparent rows = round(transparency * totalRows)
            int transparentRows = (int)MathF.Round(transparency * totalRows);
            int opaqueRows = totalRows - transparentRows;

            // Bresenham-distribute transparent rows among all rows
            // We emit a transparent row each time the error threshold is crossed.
            int err = totalRows / 2;
            for (int i = 0; i < totalRows; i++)
            {
                int y = bounds.Y + i;
                err -= transparentRows;
                Color rowColor;
                if (err < 0)
                {
                    err += totalRows;
                    rowColor = Core.TransparentWindowColor;
                }
                else
                {
                    rowColor = UIStyle.BlockBackground;
                }
                DrawRect(spriteBatch, new Rectangle(bounds.X, y, bounds.Width, 1), rowColor);
            }
        }

        private static void RedrawBlockHeader(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight, bool mouseBlockedByOverlay = false)
        {
            PanelGroup group = GetPanelGroupForBlock(block);

            Rectangle groupBar = GetGroupBarBounds(block, group, dragBarHeight);
            bool showTabs = groupBar != Rectangle.Empty && (group?.Blocks.Count ?? 0) > 0;

            if (dragBarHeight > 0)
            {
                Rectangle dragBar = block.GetDragBarBounds(dragBarHeight);
                if (dragBar.Height > 0)
                {
                    DrawRect(spriteBatch, dragBar, UIStyle.DragBarBackground);
                    bool dragBarHovered = string.Equals(_hoveredDragBarId, block.Id, StringComparison.Ordinal);
                    GetDragBarButtonBounds(block, dragBarHeight, out Rectangle panelLockButtonBounds, out Rectangle closeButtonBounds, out Rectangle mergeButtonBounds);
                    Rectangle dragGrabBounds = GetDragBarGrabBounds(block, group, dragBarHeight);

                    if (dragBarHovered && dragGrabBounds != Rectangle.Empty)
                    {
                        DrawRect(spriteBatch, dragGrabBounds, UIStyle.DragBarHoverTint);
                        DrawDragBarGrabHint(spriteBatch, dragGrabBounds);
                    }

                    // Merge/separate button for overlay blocks — left of the lock button.
                    if (mergeButtonBounds != Rectangle.Empty && block.IsOverlay)
                    {
                        bool hovered = mergeButtonBounds.Contains(_mousePosition);
                        bool isSingleTab = group == null || group.Blocks.Count <= 1;
                        DrawOverlayMergeButton(spriteBatch, mergeButtonBounds, isSingleTab, hovered);
                    }

                    if (panelLockButtonBounds != Rectangle.Empty)
                    {
                        // Use the stored lock state for display so the icon always reflects what is
                        // actually locked, even when BlockMode temporarily bypasses lock restrictions.
                        bool panelLocked = group != null ? (group.IsLocked && !_panelInteractionLockActive) : IsBlockLockEnabled(block);
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
                bool tabSwitchRequiresBlockMode = ControlStateManager.GetSwitchState(ControlKeyMigrations.TabSwitchRequiresBlockModeKey);
                bool canInteractWithTabs = !tabSwitchRequiresBlockMode || DockingModeEnabled;
                bool allowHover = canInteractWithTabs && (block.IsOverlay || !mouseBlockedByOverlay);
                DrawGroupBar(spriteBatch, layout, group, block, allowHover);
            }

            // Draw opacity slider row below the header bar, only when drag bar is hovered.
            // Uses the same background + hover tint as the drag bar so the two areas read as one unit.
            if (BlockHasOpacitySlider(block))
            {
                Rectangle row = GetOpacityRowBounds(block, group, dragBarHeight);
                if (row != Rectangle.Empty)
                {
                    DrawRect(spriteBatch, row, UIStyle.DragBarBackground);
                    bool dragBarHovered = string.Equals(_hoveredDragBarId, block.Id, StringComparison.Ordinal);
                    if (dragBarHovered)
                    {
                        DrawRect(spriteBatch, row, UIStyle.DragBarHoverTint);
                    }
                    DrawOpacitySlider(spriteBatch, block, dragBarHeight);
                }
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
                int inset = Math.Max(3, bounds.Width / 5);
                Rectangle iconBounds = new(bounds.X + inset, bounds.Y + inset,
                    Math.Max(2, bounds.Width - inset * 2), Math.Max(2, bounds.Height - inset * 2));
                DrawCenteredIcon(spriteBatch, icon, iconBounds, Color.White);
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
                int inset = Math.Max(3, bounds.Width / 5);
                Rectangle iconBounds = new(bounds.X + inset, bounds.Y + inset,
                    Math.Max(2, bounds.Width - inset * 2), Math.Max(2, bounds.Height - inset * 2));
                DrawCenteredIcon(spriteBatch, icon, iconBounds, Color.White);
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

        /// <summary>
        /// Draws the overlay merge button. For single-tab overlays the glyph is a
        /// down-arrow (merge this tab into parent). For multi-tab overlay panels
        /// the glyph shows a double-arrow (merge all tabs).
        /// </summary>
        private static void DrawOverlayMergeButton(SpriteBatch spriteBatch, Rectangle bounds, bool isSingleTab, bool hovered)
        {
            Color accent = UIStyle.AccentColor;
            float strength = hovered ? 0.7f : 0.4f;
            Color background = accent * strength;
            Color border = hovered ? accent : accent * 0.8f;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable) return;

            // Down arrow = merge single tab into parent; double down = merge all tabs
            string glyph = isSingleTab ? "\u2193" : "\u21CA";
            DrawCenteredGlyph(spriteBatch, glyphFont, glyph, bounds, Color.White, -1f);
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

        private static bool IsMouseOnAnyOverlayDragBar(int dragBarHeight)
        {
            // Overlays always have drag bars regardless of docking mode.
            int overlayDbh = UIStyle.DragBarHeight;
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay) continue;
                Rectangle dragBar = block.GetDragBarBounds(overlayDbh);
                if (dragBar != Rectangle.Empty && dragBar.Contains(_mousePosition))
                    return true;
            }
            return false;
        }

        private static bool IsMouseInsideAnyOverlayBlock()
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay) continue;
                if (block.Bounds.Contains(_mousePosition)) return true;
            }
            return false;
        }

        private static bool IsMouseCoveredByOverlayGroupBar(int dragBarHeight)
        {
            // Overlays always have drag bars so use their dedicated height.
            int overlayDbh = UIStyle.DragBarHeight;
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay)
                {
                    continue;
                }

                PanelGroup group = GetPanelGroupForBlock(block);
                if (group == null)
                {
                    continue;
                }

                Rectangle groupBar = GetGroupBarBounds(block, group, overlayDbh);
                if (groupBar != Rectangle.Empty && groupBar.Contains(_mousePosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawGroupBar(SpriteBatch spriteBatch, PanelGroupBarLayout layout, PanelGroup group, DockBlock activeBlock, bool allowHover = true)
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
            // Use stored lock state for display so icons/dimming reflect actual lock state
            // even when BlockMode is bypassing the lock for interaction purposes.
            bool locked = IsBlockLockEnabled(activeBlock);
            bool panelLocked = IsPanelLocked(group);

            foreach (TabHitRegion tab in layout.Tabs)
            {
                if (!_blocks.TryGetValue(tab.BlockId, out DockBlock tabBlock))
                {
                    continue;
                }

                bool isActive = string.Equals(group.ActiveBlockId, tab.BlockId, StringComparison.OrdinalIgnoreCase);
                bool isHovered = allowHover && tab.Bounds.Contains(_mousePosition);
                bool closeHovered = allowHover && tab.CloseBounds.Contains(_mousePosition);
                bool ungroupHovered = allowHover && !panelLocked && tab.UngroupBounds.Contains(_mousePosition);
                bool lockHovered = allowHover && !panelLocked && tab.LockBounds.Contains(_mousePosition);
                bool tabLocked = IsBlockLockEnabled(tabBlock);

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

                if (tabLocked)
                {
                    background *= 0.65f;
                    textColor = UIStyle.MutedTextColor;
                }

                DrawRect(spriteBatch, tab.Bounds, background);
                DrawRectOutline(spriteBatch, tab.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

                if (isHovered)
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
            foreach (CornerHandle corner in _cornerHandles)
            {
                Color color = UIStyle.ResizeEdgeColor;
                Rectangle bounds = corner.Bounds;

                bool isActiveCorner = _activeCornerHandle.HasValue && CornerEquals(corner, _activeCornerHandle.Value);
                if (isActiveCorner)
                {
                    color = UIStyle.ResizeEdgeActiveColor;
                    if (_activeCornerSnapPosition.HasValue)
                        bounds = CenterRectangle(bounds, _activeCornerSnapPosition.Value);
                }
                else if (_hoveredCornerHandle.HasValue && CornerEquals(corner, _hoveredCornerHandle.Value))
                {
                    color = UIStyle.ResizeEdgeHoverColor;
                }

                DrawRect(spriteBatch, bounds, color);
            }
            // Overlay corner handles are drawn by DrawOverlayResizeChrome.
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
                case DockBlockKind.ControlSetups:
                    ControlSetupsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.DockingSetups:
                    DockingSetupsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Backend:
                    BackendBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.DebugLogs:
                    DebugLogsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Specs:
                    SpecsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Bars:
                    BarsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Chat:
                    ChatBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Performance:
                    PerformanceBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Interact:
                    InteractBlock.Draw(spriteBatch, contentBounds);
                    break;
            }
        }

        private static void UpdateTooltipHoverState(double elapsedSeconds, bool mouseButtonHeld)
        {
            if (mouseButtonHeld || _draggingBlock != null || _draggingPanel != null ||
                _activeResizeEdge != null || _activeCornerHandle != null)
            {
                _tooltipHoveredRowKey = null;
                _tooltipHoveredRowLabel = null;
                _tooltipHoverElapsed = 0d;
                return;
            }

            string rowKey =
                ControlsBlock.GetHoveredRowKey()
                ?? BackendBlock.GetHoveredRowKey()
                ?? SpecsBlock.GetHoveredRowKey()
                ?? ColorSchemeBlock.GetHoveredRowKey()
                ?? PropertiesBlock.GetHoveredButtonKey()
                ?? PropertiesBlock.GetHoveredPropRowKey()
                ?? InteractBlock.GetHoveredRowKey()
                ?? ControlSetupsBlock.GetHoveredRowKey()
                ?? DockingSetupsBlock.GetHoveredRowKey()
                ?? NotesBlock.GetHoveredRowKey()
                ?? GetHoveredDragBarButtonTooltipKey();

            string rowLabel = rowKey != null
                ? (ControlsBlock.GetHoveredRowLabel()
                    ?? BackendBlock.GetHoveredRowLabel()
                    ?? SpecsBlock.GetHoveredRowLabel()
                    ?? ColorSchemeBlock.GetHoveredRowLabel()
                    ?? PropertiesBlock.GetHoveredPropRowLabel()
                    ?? InteractBlock.GetHoveredRowLabel())
                : null;

            if (!string.Equals(rowKey, _tooltipHoveredRowKey, StringComparison.OrdinalIgnoreCase))
            {
                _tooltipHoveredRowKey = rowKey;
                _tooltipHoveredRowLabel = rowLabel;
                _tooltipHoverElapsed = 0d;
            }
            else if (rowKey != null)
            {
                _tooltipHoverElapsed += elapsedSeconds;
            }
        }

        /// <summary>
        /// Returns a tooltip key if the mouse is hovering over a drag bar button
        /// (Close, Lock, or Merge). Returns null otherwise.
        /// </summary>
        private static string GetHoveredDragBarButtonTooltipKey()
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible) continue;
                int dbh = GetDragBarHeightForBlock(block);
                if (dbh <= 0) continue;

                GetDragBarButtonBounds(block, dbh, out Rectangle lockBounds, out Rectangle closeBounds, out Rectangle mergeBounds);

                if (closeBounds != Rectangle.Empty && closeBounds.Contains(_mousePosition))
                    return "Btn_Close";
                if (lockBounds != Rectangle.Empty && lockBounds.Contains(_mousePosition))
                    return "Btn_Lock";
                if (mergeBounds != Rectangle.Empty && mergeBounds.Contains(_mousePosition))
                    return "Btn_OverlayMerge";
            }
            return null;
        }

        private const string BulletPrefix = "\u2022 "; // "• "

        private static void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_tooltipHoverElapsed < TooltipDelaySeconds || string.IsNullOrEmpty(_tooltipHoveredRowKey))
                return;

            if (!_blockTooltips.TryGetValue(_tooltipHoveredRowKey, out var tooltipEntries)
                || tooltipEntries == null || tooltipEntries.Length == 0)
                return;

            UIStyle.UIFont font       = UIStyle.FontTech;
            if (!font.IsAvailable) return;
            UIStyle.UIFont boldFont   = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Bold);
            UIStyle.UIFont italicFont = UIStyle.GetFontVariant(UIStyle.FontFamilyKey.Xenon, UIStyle.FontVariant.Italic);

            // Label and DataType only apply to the first tooltip box.
            bool hasLabel    = boldFont.IsAvailable && !string.IsNullOrEmpty(_tooltipHoveredRowLabel);
            bool hasDataType = italicFont.IsAvailable && !string.IsNullOrEmpty(tooltipEntries[0].DataType);

            float lineHeight = font.LineHeight;
            float boldH      = boldFont.IsAvailable ? boldFont.LineHeight : lineHeight;
            int   innerWidth = TooltipMaxWidth - TooltipPadding * 2;

            const float LabelBodyGap = 4f;
            const float DataTypeGap  = 8f;
            const int   BoxGap       = 4;

            // ── Pre-compute each tooltip box ───────────────────────────────────────
            var boxes = new (List<string> Lines, int BoxW, int BoxH)[tooltipEntries.Length];
            for (int bi = 0; bi < tooltipEntries.Length; bi++)
            {
                bool isFirst = bi == 0;
                bool showLbl = isFirst && hasLabel;
                bool showDt  = isFirst && hasDataType;
                List<string> lines = WrapTooltipLines(font, tooltipEntries[bi].Text, innerWidth);

                float maxW = 0f;
                if (showLbl)
                {
                    float lw = boldFont.MeasureString(_tooltipHoveredRowLabel).X;
                    if (showDt) lw += DataTypeGap + italicFont.MeasureString(tooltipEntries[0].DataType).X;
                    if (lw > maxW) maxW = lw;
                }
                foreach (string ln in lines) { float w = font.MeasureString(ln).X; if (w > maxW) maxW = w; }

                float hdrH = showLbl ? boldH + LabelBodyGap : 0f;
                int   bW   = Math.Min((int)maxW, innerWidth) + TooltipPadding * 2;
                int   bH   = (int)(hdrH + lines.Count * lineHeight) + TooltipPadding * 2;
                boxes[bi]  = (lines, bW, bH);
            }

            // ── Check there is something to show ──────────────────────────────────
            bool anyContent = hasLabel;
            if (!anyContent)
                foreach (var box in boxes)
                    if (box.Lines.Count > 0) { anyContent = true; break; }
            if (!anyContent) return;

            // ── Total dimensions ───────────────────────────────────────────────────
            int totalH = 0, maxW2 = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].Lines.Count == 0 && !(i == 0 && hasLabel)) continue;
                totalH += boxes[i].BoxH + BoxGap;
                if (boxes[i].BoxW > maxW2) maxW2 = boxes[i].BoxW;
            }
            if (totalH == 0) return;
            totalH -= BoxGap; // remove trailing gap

            // ── Position ───────────────────────────────────────────────────────────
            int tipX = _mousePosition.X + 14;
            int tipY = _mousePosition.Y + 18;
            if (tipX + maxW2  > _layoutBounds.Right)  tipX = _mousePosition.X - maxW2  - 4;
            if (tipY + totalH > _layoutBounds.Bottom) tipY = _mousePosition.Y - totalH - 4;
            tipX = Math.Max(_layoutBounds.X, tipX);
            tipY = Math.Max(_layoutBounds.Y, tipY);

            // ── Draw each tooltip box ──────────────────────────────────────────────
            var bulletColor = ColorPalette.TooltipMuted;
            int curY = tipY;
            for (int bi = 0; bi < boxes.Length; bi++)
            {
                bool isFirst = bi == 0;
                bool showLbl = isFirst && hasLabel;
                bool showDt  = isFirst && hasDataType;
                (List<string> lines, int bW, int bH) = boxes[bi];
                if (lines.Count == 0 && !showLbl) continue;

                Rectangle bg = new(tipX, curY, bW, bH);
                DrawRect(spriteBatch, bg, ColorPalette.TooltipBackground);
                DrawRectOutline(spriteBatch, bg, ColorPalette.TooltipBorder, UIStyle.BlockBorderThickness);

                float ty = curY + TooltipPadding;
                if (showLbl)
                {
                    boldFont.DrawString(spriteBatch, _tooltipHoveredRowLabel, new Vector2(tipX + TooltipPadding, ty), ColorPalette.TooltipText);
                    if (showDt)
                    {
                        float lw  = boldFont.MeasureString(_tooltipHoveredRowLabel).X;
                        float dtx = tipX + TooltipPadding + lw + DataTypeGap;
                        float dty = ty + (boldH - italicFont.LineHeight) * 0.5f;
                        italicFont.DrawString(spriteBatch, tooltipEntries[0].DataType, new Vector2(dtx, dty), ColorPalette.TooltipMuted);
                    }
                    ty += boldH + LabelBodyGap;
                }

                for (int li = 0; li < lines.Count; li++)
                {
                    string line = lines[li];
                    Color lc = line.StartsWith(BulletPrefix, StringComparison.Ordinal) ? bulletColor : ColorPalette.TooltipText;
                    font.DrawString(spriteBatch, line, new Vector2(tipX + TooltipPadding, ty + li * lineHeight), lc);
                }
                curY += bH + BoxGap;
            }
        }

        /// <summary>
        /// Splits <paramref name="text"/> on '\n' to respect explicit line breaks, then word-wraps
        /// each segment to fit <paramref name="maxWidth"/>. Lines that start with a bullet prefix
        /// are kept intact so the bullet character aligns correctly.
        /// </summary>
        private static List<string> WrapTooltipLines(UIStyle.UIFont font, string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || !font.IsAvailable || maxWidth <= 0)
            {
                return lines;
            }

            string[] segments = text.Split('\n');
            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                WrapSingleSegment(font, segment, maxWidth, lines);
            }

            return lines;
        }

        private static void WrapSingleSegment(UIStyle.UIFont font, string segment, int maxWidth, List<string> lines)
        {
            string[] words = segment.Split(' ');
            var current = new System.Text.StringBuilder();

            foreach (string word in words)
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                }
                else
                {
                    string candidate = current + " " + word;
                    if (font.MeasureString(candidate).X > maxWidth)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        // Continuation lines from a bullet segment do NOT get the bullet prefix re-added;
                        // they just continue with indented spacing from the natural word-wrap break.
                        current.Append(word);
                    }
                    else
                    {
                        current.Append(' ');
                        current.Append(word);
                    }
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
            }
        }

        // Keep old name as a thin wrapper so any other internal callers still compile.
        private static List<string> WrapTooltipText(UIStyle.UIFont font, string text, int maxWidth)
            => WrapTooltipLines(font, text, maxWidth);

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
                    // Clear any stale Interacting state so an invisible block can't permanently
                    // hold IsAnyGuiInteracting true (e.g. after navigating away from Bars block).
                    if (block != null)
                        _blockInteractionStates[block.Id] = GUIInteractionState.NotHovering;
                    continue;
                }

                Rectangle contentBounds = GetPanelContentBounds(block, GetDragBarHeightForBlock(block));

                // Suppress mouse input for blocks whose content area is covered by an overlay block.
                MouseState effectiveMouse = mouseState;
                MouseState effectivePrevMouse = previousMouseState;
                bool suppressedByOverlay = false;
                if (!block.IsOverlay)
                {
                    foreach (DockBlock overlay in _orderedBlocks)
                    {
                        if (!overlay.IsVisible || !overlay.IsOverlay || ReferenceEquals(overlay, block))
                            continue;
                        if (overlay.Bounds.Intersects(block.Bounds) && overlay.Bounds.Contains(mouseState.Position))
                        {
                            effectiveMouse = new MouseState(-9999, -9999, previousMouseState.ScrollWheelValue,
                                ButtonState.Released, ButtonState.Released, ButtonState.Released,
                                ButtonState.Released, ButtonState.Released);
                            effectivePrevMouse = new MouseState(-9999, -9999, previousMouseState.ScrollWheelValue,
                                ButtonState.Released, ButtonState.Released, ButtonState.Released,
                                ButtonState.Released, ButtonState.Released);
                            suppressedByOverlay = true;
                            break;
                        }
                    }
                }

                // Update per-block GUIInteractionState.
                GUIInteractionState guiState;
                if (suppressedByOverlay)
                {
                    guiState = GUIInteractionState.NotHovering;
                }
                else if (block.Kind == DockBlockKind.Game)
                {
                    // The game viewport is never a "GUI interaction" — clicking there is gameplay,
                    // not a UI click. Marking it Interacting would erroneously freeze game inputs.
                    // Use effectiveMouse so an overlay block sitting on top of the game block
                    // prevents the game from receiving hover/input in that region.
                    bool cursorIn = block.Bounds.Contains(effectiveMouse.Position);
                    guiState = cursorIn ? GUIInteractionState.Hovering : GUIInteractionState.NotHovering;
                }
                else
                {
                    bool cursorIn = block.Bounds.Contains(mouseState.Position);
                    bool effectivelyLocked = IsBlockLocked(block) || IsPanelLocked(block);
                    if (cursorIn && mouseState.LeftButton == ButtonState.Pressed && !effectivelyLocked)
                        guiState = GUIInteractionState.Interacting;
                    else if (cursorIn)
                        guiState = GUIInteractionState.Hovering;
                    else
                        guiState = GUIInteractionState.NotHovering;
                }
                _blockInteractionStates[block.Id] = guiState;

                switch (block.Kind)
                {
                    case DockBlockKind.Blank:
                        UpdateBlankBlockHoverState(block, elapsedSeconds);
                        break;
                    case DockBlockKind.Properties:
                        PropertiesBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.Notes:
                        NotesBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.ColorScheme:
                        ColorSchemeBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse, keyboardState, previousKeyboardState);
                        break;
                case DockBlockKind.Controls:
                    ControlsBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                    break;
                case DockBlockKind.ControlSetups:
                    ControlSetupsBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                    break;
                case DockBlockKind.Backend:
                    BackendBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                    break;
                    case DockBlockKind.DebugLogs:
                        DebugLogsBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.Specs:
                        SpecsBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.DockingSetups:
                        DockingSetupsBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.Bars:
                        BarsBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.Chat:
                        ChatBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.Performance:
                        PerformanceBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse);
                        break;
                    case DockBlockKind.Interact:
                        InteractBlock.Update(gameTime, contentBounds, effectiveMouse, effectivePrevMouse, keyboardState, previousKeyboardState);
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
            _overlayDragPreviewBounds = null;
            _overlayDropTargetBlock = null;
            _draggingBlock = null;
            _draggingPanel = null;
            _draggingFromTab = false;
            _dropPreview = null;
            _superimposeLocked = false;
            _superimposeLockedTarget = null;
            _hoveredDragBarId = null;
            _hoveredResizeEdge = null;
            _activeResizeEdge = null;
            ClearResizeEdgeSnap();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
            _activeCornerLinkedHandle = null;
            ClearCornerSnap();
            _opacitySliderDragging = false;
            _opacitySliderDraggingId = null;
            _opacitySliderTrackBounds = Rectangle.Empty;
            _opacityExpandedId = null;
            _hoveredOverlayResizeEdge = null;
            _activeOverlayResizeEdge = null;
            _hoveredOverlayCornerHandle = null;
            _activeOverlayCornerHandle = null;
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
                if (block != null && block.IsVisible && !block.IsOverlay)
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
            FocusModeManager.SetFocusActive(OverlayInputFocusOwner, true);
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
            FocusModeManager.SetFocusActive(OverlayInputFocusOwner, false);
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
            FocusModeManager.SetFocusActive(OverlayInputFocusOwner, false);
        }

        private static void UpdateOverlayKeyboardInput(KeyboardState keyboardState, double elapsedSeconds)
        {
            if (!_overlayMenuVisible || _activeNumericEntry == null)
            {
                FocusModeManager.SetFocusActive(OverlayInputFocusOwner, false);
                OverlayInputRepeater.Reset();
                return;
            }

            FocusModeManager.SetFocusActive(OverlayInputFocusOwner, true);
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

        public static Rectangle GetVirtualViewport()
        {
            if (_layoutBounds.Width > 0 && _layoutBounds.Height > 0) return _layoutBounds;
            return Core.Instance?.GraphicsDevice?.Viewport.Bounds ?? Rectangle.Empty;
        }

        private static MouseState ScaleMouseState(MouseState state, float scale)
        {
            return new MouseState(
                (int)MathF.Round(state.X * scale),
                (int)MathF.Round(state.Y * scale),
                state.ScrollWheelValue,
                state.LeftButton,
                state.MiddleButton,
                state.RightButton,
                state.XButton1,
                state.XButton2);
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
            // Use a fixed reference height so panel content stays consistent regardless of window
            // size; layout only adapts when the aspect ratio changes.
            if (_uiScale <= 0f) return viewport;
            int virtualWidth = (int)MathF.Round(viewport.Width / _uiScale);
            return new Rectangle(0, 0, virtualWidth, (int)ReferenceUIHeight);
        }

        private static int GetActiveDragBarHeight()
        {
            return DockingModeEnabled ? UIStyle.DragBarHeight : 0;
        }

        /// <summary>
        /// Overlay blocks always show drag bars so they can be moved/resized outside
        /// docking mode. Standard blocks only show drag bars during docking mode.
        /// </summary>
        private static int GetDragBarHeightForBlock(DockBlock block)
        {
            return block.IsOverlay ? UIStyle.DragBarHeight : GetActiveDragBarHeight();
        }

        private static bool AnyBlockVisible() => _orderedBlocks.Any(block => block.IsVisible);

        public static bool IsBlockMenuOpen() => _overlayMenuVisible;

        public static bool IsInputBlocked() => _overlayMenuVisible || ControlsBlock.IsRebindOverlayOpen() || ColorSchemeBlock.IsEditorOpen;

        /// <summary>
        /// Returns true when scrollwheel game inputs (camera zoom, etc.) should be
        /// suppressed: when the cursor is over a visible, unlocked non-Game block,
        /// or when docking mode is active and the cursor is not over the Game block.
        /// </summary>
        public static bool ShouldSuppressScrollWheel()
        {
            Point pos = _mousePosition;

            if (_dockingModeEnabled)
            {
                // Allow scroll through to game actions only when cursor is over the Game block
                foreach (DockBlock block in _orderedBlocks)
                {
                    if (block == null || !block.IsVisible) continue;
                    if (!block.Bounds.Contains(pos)) continue;
                    return block.Kind != DockBlockKind.Game;
                }
                return true; // cursor not over any block
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !block.IsVisible || block.Kind == DockBlockKind.Game)
                    continue;
                if (!block.Bounds.Contains(pos))
                    continue;
                // Mouse is over this block — suppress scroll if the block is unlocked
                if (!IsBlockLocked(block))
                    return true;
                break;
            }
            return false;
        }

        public static bool IsDraggingLayout =>
            _activeResizeEdge.HasValue || _activeCornerHandle.HasValue;

        private static bool ShouldLockPanelInteractions() => false;

        public static string GetHoveredBlockKind()
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay) continue;
                if (block.Bounds.Contains(_mousePosition)) return block.Kind.ToString();
            }
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || block.IsOverlay) continue;
                if (block.Bounds.Contains(_mousePosition)) return block.Kind.ToString();
            }
            return "None";
        }

        public static string GetHoveredDragBarKind()
        {
            if (string.IsNullOrEmpty(_hoveredDragBarId)) return "None";
            return _blocks.TryGetValue(_hoveredDragBarId, out DockBlock b) ? b.Kind.ToString() : "None";
        }

        public static bool IsDragBarHovered => !string.IsNullOrEmpty(_hoveredDragBarId);

        public static bool IsCursorInAnyDragBar
        {
            get
            {
                foreach (DockBlock block in _orderedBlocks)
                {
                    if (!block.IsVisible) continue;
                    int dbh = GetDragBarHeightForBlock(block);
                    if (dbh <= 0) continue;
                    if (block.GetDragBarBounds(dbh).Contains(_mousePosition))
                        return true;
                }
                return false;
            }
        }

        public static bool IsAnyGuiInteracting
        {
            get
            {
                // Primary: current-frame accurate ownership set by PreUpdateInteractionStates().
                if (_uiOwnsLeftClick) return true;
                // Fallback: legacy drag/resize states tracked by the main Update loop.
                foreach (GUIInteractionState s in _blockInteractionStates.Values)
                    if (s == GUIInteractionState.Interacting) return true;
                return false;
            }
        }

        public static GUIInteractionState GetBlockInteractionState(DockBlockKind kind)
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block.Kind == kind && _blockInteractionStates.TryGetValue(block.Id, out GUIInteractionState s))
                    return s;
            }
            return GUIInteractionState.NotHovering;
        }

        public static string GetInteractingBlockKind()
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (_blockInteractionStates.TryGetValue(block.Id, out GUIInteractionState s) && s == GUIInteractionState.Interacting)
                    return block.Kind.ToString();
            }
            return "None";
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
            if (!bounds.Contains(cursor)) return false;

            // Cursor is within game block bounds — but if an overlay covers it, the game is not the active target.
            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay) continue;
                if (block.Bounds.Contains(cursor)) return false;
            }
            return true;
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

        private static void UpdateOverlayBlockBounds()
        {
            foreach (DockBlock block in _orderedBlocks)
            {
                if (block == null || !block.IsOverlay || string.IsNullOrEmpty(block.OverlayParentId))
                {
                    continue;
                }

                if (!_blocks.TryGetValue(block.OverlayParentId, out DockBlock parent) || !parent.IsVisible)
                {
                    // Remember that we suppressed this overlay due to an inactive parent
                    // so we can restore it when the parent becomes visible again.
                    if (block.IsVisible)
                        _parentSuppressedOverlays.Add(block.Id);
                    block.IsVisible = false;
                    continue;
                }

                // Parent is visible — restore overlay if we were the ones who hid it.
                if (_parentSuppressedOverlays.Remove(block.Id))
                    block.IsVisible = true;

                Rectangle pb = parent.Bounds;
                if (pb.Width <= 0 || pb.Height <= 0)
                {
                    continue;
                }

                int w = Math.Max(block.MinWidth, (int)(pb.Width * block.OverlayRelWidth));
                int h = Math.Max(block.MinHeight, (int)(pb.Height * block.OverlayRelHeight));
                int x = pb.X + (int)(pb.Width * block.OverlayRelX);
                int y = pb.Y + (int)(pb.Height * block.OverlayRelY);

                x = Math.Clamp(x, pb.X, Math.Max(pb.X, pb.Right - w));
                y = Math.Clamp(y, pb.Y, Math.Max(pb.Y, pb.Bottom - h));

                block.Bounds = new Rectangle(x, y, w, h);
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
            float newScale = viewport.Height > 0 ? viewport.Height / ReferenceUIHeight : 1f;
            if (Math.Abs(_uiScale - newScale) > 0.0001f)
            {
                _uiScale = newScale;
                _layoutDirty = true;
            }

            if (viewport != _cachedViewportBounds)
            {
                _cachedViewportBounds = viewport;
                _layoutDirty = true;
            }

            EnsureVisibleBlocksAttachedToLayout();
            UpdateOverlayBlockBounds();

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
                // Clear standard block resize edges but keep overlay resize handles
                // so overlays remain resizable outside docking mode.
                _resizeEdges.Clear();
                _hoveredResizeEdge = null;
                _activeResizeEdge = null;
                ClearResizeEdgeSnap();
                _cornerHandles.Clear();
                _hoveredCornerHandle = null;
                _activeCornerHandle = null;
                _activeCornerLinkedHandle = null;
                ClearCornerSnap();
                CollectOverlayResizeHandles();
            }

            _gameContentBounds = Rectangle.Empty;
            _actualGameContentBounds = Rectangle.Empty;
            int dragBarHeight = GetActiveDragBarHeight();
            if (TryGetGameBlock(out DockBlock gameBlock) && gameBlock.IsVisible)
            {
                PanelGroup gamePanel = GetPanelGroupForBlock(gameBlock);
                int groupBarHeight = GetGroupBarHeight(gamePanel);
                _gameContentBounds = gameBlock.GetContentBounds(dragBarHeight, UIStyle.BlockPadding, groupBarHeight);
                _actualGameContentBounds = _uiScale > 0f
                    ? new Rectangle(
                        (int)(_gameContentBounds.X * _uiScale),
                        (int)(_gameContentBounds.Y * _uiScale),
                        (int)MathF.Ceiling(_gameContentBounds.Width * _uiScale),
                        (int)MathF.Ceiling(_gameContentBounds.Height * _uiScale))
                    : _gameContentBounds;
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
                if (block == null || block.IsOverlay)
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
            // Overlay blocks are visible but intentionally absent from the layout tree
            HashSet<string> intendedVisibleIds = new(_orderedBlocks.Where(block => block != null && block.IsVisible && !block.IsOverlay).Select(block => block.Id), StringComparer.OrdinalIgnoreCase);

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
            CollectOverlayResizeHandles();

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

        // ----- Overlay resize edge collection & logic -----

        private const float OverlayEdgeSnapTolerance = 0.001f;

        private static void CollectOverlayResizeHandles()
        {
            _overlayResizeEdges.Clear();
            _overlayCornerHandles.Clear();

            int t = Math.Max(2, UIStyle.ResizeEdgeThickness);

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !block.IsOverlay || string.IsNullOrEmpty(block.OverlayParentId))
                    continue;

                if (!_blocks.TryGetValue(block.OverlayParentId, out DockBlock parent) || !parent.IsVisible)
                    continue;

                PanelGroup group = GetPanelGroupForBlock(block);
                if (group != null && IsPanelLocked(group))
                    continue;

                Rectangle b = block.Bounds;
                bool snappedLeft   = block.OverlayRelX <= OverlayEdgeSnapTolerance;
                bool snappedTop    = block.OverlayRelY <= OverlayEdgeSnapTolerance;
                bool snappedRight  = (block.OverlayRelX + block.OverlayRelWidth)  >= 1f - OverlayEdgeSnapTolerance;
                bool snappedBottom = (block.OverlayRelY + block.OverlayRelHeight) >= 1f - OverlayEdgeSnapTolerance;

                if (!snappedLeft)
                    _overlayResizeEdges.Add(new OverlayResizeEdge(block, parent, DockSplitOrientation.Vertical,   new Rectangle(b.X, b.Y, t, b.Height), OverlayEdgeSide.Left));
                if (!snappedRight)
                    _overlayResizeEdges.Add(new OverlayResizeEdge(block, parent, DockSplitOrientation.Vertical,   new Rectangle(b.Right - t, b.Y, t, b.Height), OverlayEdgeSide.Right));
                if (!snappedTop)
                    _overlayResizeEdges.Add(new OverlayResizeEdge(block, parent, DockSplitOrientation.Horizontal, new Rectangle(b.X, b.Y, b.Width, t), OverlayEdgeSide.Top));
                if (!snappedBottom)
                    _overlayResizeEdges.Add(new OverlayResizeEdge(block, parent, DockSplitOrientation.Horizontal, new Rectangle(b.X, b.Bottom - t, b.Width, t), OverlayEdgeSide.Bottom));
            }

            // Build corner handles where vertical and horizontal edges of the SAME overlay block intersect
            foreach (OverlayResizeEdge ve in _overlayResizeEdges)
            {
                if (ve.Orientation != DockSplitOrientation.Vertical) continue;
                foreach (OverlayResizeEdge he in _overlayResizeEdges)
                {
                    if (he.Orientation != DockSplitOrientation.Horizontal) continue;
                    if (!ReferenceEquals(ve.Overlay, he.Overlay)) continue;

                    Rectangle overlap = Rectangle.Intersect(ve.Bounds, he.Bounds);
                    if (overlap.Width > 0 && overlap.Height > 0)
                    {
                        _overlayCornerHandles.Add(new OverlayCornerHandle(ve, he, overlap));
                    }
                }
            }
        }

        private static OverlayResizeEdge? HitTestOverlayResizeEdge(Point position)
        {
            // Prioritise corners: if the cursor is inside a corner, don't fire a lone edge
            foreach (OverlayCornerHandle corner in _overlayCornerHandles)
            {
                if (corner.Bounds.Contains(position))
                    return null;
            }

            OverlayResizeEdge? best = null;
            int bestDist = int.MaxValue;

            foreach (OverlayResizeEdge edge in _overlayResizeEdges)
            {
                if (!edge.Bounds.Contains(position)) continue;
                int axisDist = edge.Orientation == DockSplitOrientation.Vertical
                    ? Math.Abs(position.X - (edge.Bounds.X + edge.Bounds.Width / 2))
                    : Math.Abs(position.Y - (edge.Bounds.Y + edge.Bounds.Height / 2));
                if (axisDist < bestDist)
                {
                    bestDist = axisDist;
                    best = edge;
                }
            }
            return best;
        }

        private static OverlayCornerHandle? HitTestOverlayCornerHandle(Point position)
        {
            foreach (OverlayCornerHandle corner in _overlayCornerHandles)
            {
                if (corner.Bounds.Contains(position))
                    return corner;
            }
            return null;
        }

        private static void ApplyOverlayEdgeDrag(OverlayResizeEdge edge, Point mousePos)
        {
            DockBlock block = edge.Overlay;
            DockBlock parent = edge.Parent;
            Rectangle pb = parent.Bounds;
            if (pb.Width <= 0 || pb.Height <= 0) return;

            int minW = block.MinWidth;
            int minH = block.MinHeight;

            switch (edge.Side)
            {
                case OverlayEdgeSide.Left:
                {
                    int maxX = pb.X + (int)(pb.Width * (block.OverlayRelX + block.OverlayRelWidth)) - minW;
                    int newX = Math.Clamp(mousePos.X, pb.X, maxX);
                    float newRelX = (float)(newX - pb.X) / pb.Width;
                    float newRelW = block.OverlayRelX + block.OverlayRelWidth - newRelX;
                    block.OverlayRelX = MathHelper.Clamp(newRelX, 0f, 1f);
                    block.OverlayRelWidth = MathHelper.Clamp(newRelW, (float)minW / pb.Width, 1f - block.OverlayRelX);
                    break;
                }
                case OverlayEdgeSide.Right:
                {
                    int minX = pb.X + (int)(pb.Width * block.OverlayRelX) + minW;
                    int newRight = Math.Clamp(mousePos.X, minX, pb.Right);
                    float newRelW = (float)(newRight - (pb.X + (int)(pb.Width * block.OverlayRelX))) / pb.Width;
                    block.OverlayRelWidth = MathHelper.Clamp(newRelW, (float)minW / pb.Width, 1f - block.OverlayRelX);
                    break;
                }
                case OverlayEdgeSide.Top:
                {
                    int maxY = pb.Y + (int)(pb.Height * (block.OverlayRelY + block.OverlayRelHeight)) - minH;
                    int newY = Math.Clamp(mousePos.Y, pb.Y, maxY);
                    float newRelY = (float)(newY - pb.Y) / pb.Height;
                    float newRelH = block.OverlayRelY + block.OverlayRelHeight - newRelY;
                    block.OverlayRelY = MathHelper.Clamp(newRelY, 0f, 1f);
                    block.OverlayRelHeight = MathHelper.Clamp(newRelH, (float)minH / pb.Height, 1f - block.OverlayRelY);
                    break;
                }
                case OverlayEdgeSide.Bottom:
                {
                    int minY = pb.Y + (int)(pb.Height * block.OverlayRelY) + minH;
                    int newBottom = Math.Clamp(mousePos.Y, minY, pb.Bottom);
                    float newRelH = (float)(newBottom - (pb.Y + (int)(pb.Height * block.OverlayRelY))) / pb.Height;
                    block.OverlayRelHeight = MathHelper.Clamp(newRelH, (float)minH / pb.Height, 1f - block.OverlayRelY);
                    break;
                }
            }
        }

        private static bool UpdateOverlayResizeState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (_activeOverlayCornerHandle.HasValue)
            {
                if (leftClickReleased)
                {
                    _activeOverlayCornerHandle = null;
                    CollectOverlayResizeHandles();
                    return false;
                }
                if (leftClickHeld)
                {
                    ApplyOverlayEdgeDrag(_activeOverlayCornerHandle.Value.VerticalEdge, _mousePosition);
                    ApplyOverlayEdgeDrag(_activeOverlayCornerHandle.Value.HorizontalEdge, _mousePosition);
                    return true;
                }
            }

            if (_activeOverlayResizeEdge.HasValue)
            {
                if (leftClickReleased)
                {
                    _activeOverlayResizeEdge = null;
                    CollectOverlayResizeHandles();
                    return false;
                }
                if (leftClickHeld)
                {
                    ApplyOverlayEdgeDrag(_activeOverlayResizeEdge.Value, _mousePosition);
                    return true;
                }
            }

            // Suppress overlay resize hover and activation while a drag bar drag is in progress.
            if (_draggingBlock != null)
            {
                _hoveredOverlayCornerHandle = null;
                _hoveredOverlayResizeEdge = null;
                return false;
            }

            OverlayCornerHandle? hoveredCorner = HitTestOverlayCornerHandle(_mousePosition);
            _hoveredOverlayCornerHandle = hoveredCorner;

            OverlayResizeEdge? hoveredEdge = hoveredCorner.HasValue ? null : HitTestOverlayResizeEdge(_mousePosition);
            _hoveredOverlayResizeEdge = hoveredEdge;

            if (leftClickStarted)
            {
                // If the click is also on a drag bar grab region, yield to the drag bar
                // so vertically stacked blocks can be displaced by dragging upward.
                DockBlock dragBarHit = HitTestDragBarBlock(_mousePosition, excludeDragBarButtons: true, requireGrabRegion: true);

                if (hoveredCorner.HasValue)
                {
                    if (dragBarHit != null) return false;
                    _activeOverlayCornerHandle = hoveredCorner;
                    ApplyOverlayEdgeDrag(hoveredCorner.Value.VerticalEdge, _mousePosition);
                    ApplyOverlayEdgeDrag(hoveredCorner.Value.HorizontalEdge, _mousePosition);
                    return true;
                }
                if (hoveredEdge.HasValue)
                {
                    if (dragBarHit != null) return false;
                    _activeOverlayResizeEdge = hoveredEdge;
                    ApplyOverlayEdgeDrag(hoveredEdge.Value, _mousePosition);
                    return true;
                }
            }

            return false;
        }

        // ----- End overlay resize edge logic -----

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
            _overlayResizeEdges.Clear();
            _hoveredOverlayResizeEdge = null;
            _activeOverlayResizeEdge = null;
            _overlayCornerHandles.Clear();
            _hoveredOverlayCornerHandle = null;
            _activeOverlayCornerHandle = null;
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

        /// <summary>
        /// Returns the DockBlockCategory name of the currently focused block,
        /// or "None" when nothing is focused.
        /// </summary>
        public static string GetFocusedBlockCategory()
        {
            if (string.IsNullOrWhiteSpace(_focusedBlockId))
                return "None";
            if (!_blocks.TryGetValue(_focusedBlockId, out DockBlock block))
                return "None";
            return block.Category.ToString();
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

        /// <summary>Maps a raw window position to game-world space, accounting for camera offset.</summary>
        public static Vector2 ToGameSpace(Point windowPosition) => ToGameSpaceRaw(windowPosition) + CameraOffset;

        /// <summary>Maps a raw window position to game-world space WITHOUT camera offset (used for pan anchor).</summary>
        private static Vector2 ToGameSpaceRaw(Point windowPosition)
        {
            float x = windowPosition.X;
            float y = windowPosition.Y;

            if (_worldRenderTarget == null || _actualGameContentBounds.Width <= 0 || _actualGameContentBounds.Height <= 0)
                return new Vector2(x, y);

            float relativeX = (x - _actualGameContentBounds.X) / _actualGameContentBounds.Width;
            float relativeY = (y - _actualGameContentBounds.Y) / _actualGameContentBounds.Height;
            float rawX = relativeX * _worldRenderTarget.Width;
            float rawY = relativeY * _worldRenderTarget.Height;

            // Reverse the zoom: screen center is the zoom pivot.
            float cx = _worldRenderTarget.Width / 2f;
            float cy = _worldRenderTarget.Height / 2f;
            rawX = cx + (rawX - cx) / _cameraZoom;
            rawY = cy + (rawY - cy) / _cameraZoom;

            return new Vector2(rawX, rawY);
        }

        /// <summary>Returns the camera transform matrix (translation + zoom) for the current view.</summary>
        public static Matrix GetCameraTransform()
        {
            // Zoom around the center of the render target so the player stays centered.
            float cx = _worldRenderTarget != null ? _worldRenderTarget.Width / 2f : 0f;
            float cy = _worldRenderTarget != null ? _worldRenderTarget.Height / 2f : 0f;

            return Matrix.CreateTranslation(-CameraOffset.X, -CameraOffset.Y, 0f)
                 * Matrix.CreateTranslation(-cx, -cy, 0f)
                 * Matrix.CreateScale(_cameraZoom, _cameraZoom, 1f)
                 * Matrix.CreateTranslation(cx, cy, 0f);
        }

        /// <summary>
        /// Adjusts the camera zoom by one step. Positive delta zooms in (closer),
        /// negative zooms out (farther). Clamped to the ScrollMinDistance / ScrollMaxDistance
        /// control settings which represent the visible radius in world units.
        /// </summary>
        public static void ApplyCameraZoom(int steps)
        {
            if (_worldRenderTarget == null) return;

            float minDist = ControlStateManager.GetFloat(ControlKeyMigrations.ScrollMinDistanceKey, 200f);
            float maxDist = ControlStateManager.GetFloat(ControlKeyMigrations.ScrollMaxDistanceKey, 2000f);

            // Convert distance limits to zoom factors. The "distance" represents how much
            // of the world is visible — half the render target width at zoom 1.0.
            float halfWidth = _worldRenderTarget.Width / 2f;
            float maxZoom = halfWidth / MathHelper.Max(minDist, 1f);
            float minZoom = halfWidth / MathHelper.Max(maxDist, 1f);

            // Each step multiplies/divides by a fixed factor for smooth feel.
            const float zoomFactor = 1.1f;
            float newZoom = _cameraZoom;
            if (steps > 0)
                for (int i = 0; i < steps; i++) newZoom *= zoomFactor;
            else
                for (int i = 0; i < -steps; i++) newZoom /= zoomFactor;

            _cameraZoom = MathHelper.Clamp(newZoom, minZoom, maxZoom);
        }

        // ── Camera mode helpers ───────────────────────────────────────────────

        private static string GetCameraMode() =>
            ControlStateManager.ContainsEnumState(ControlKeyMigrations.CameraLockModeKey)
                ? ControlStateManager.GetEnumValue(ControlKeyMigrations.CameraLockModeKey)
                : "Locked";

        /// <summary>
        /// Snaps the camera to center on the player (used by Shift+Space in Free mode).
        /// Does nothing if the player or render target is unavailable.
        /// </summary>
        public static void SnapCameraToPlayer()
        {
            if (Core.Instance?.Player == null || _worldRenderTarget == null) return;
            var renderCenter = new Vector2(_worldRenderTarget.Width / 2f, _worldRenderTarget.Height / 2f);
            CameraOffset = Core.Instance.Player.Position - renderCenter;
        }

        /// <summary>
        /// Resets the Locked-mode camera offset so the next follow-update centers on the player.
        /// Used by Shift+Space in Locked mode.
        /// </summary>
        public static void ResetLockedCameraOffset()
        {
            _lockedCameraOffset = Vector2.Zero;
        }

        public static void LoadCameraSnapRange()
        {
            float val = DatabaseFetch.GetValue<float>("GeneralSettings", "Value", "SettingKey", "CameraSnapRange");
            if (val > 0f) _cameraSnapRange = val;
        }

        private static Rectangle GetCurrentGameContentBounds()
        {
            if (_actualGameContentBounds.Width > 0 && _actualGameContentBounds.Height > 0)
            {
                return _actualGameContentBounds;
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
                _actualGameContentBounds.Width <= 0 ||
                _actualGameContentBounds.Height <= 0)
            {
                return false;
            }

            // Subtract camera offset, then apply zoom around the render-target center.
            float renderX = gamePosition.X - CameraOffset.X;
            float renderY = gamePosition.Y - CameraOffset.Y;
            float cx = _worldRenderTarget.Width / 2f;
            float cy = _worldRenderTarget.Height / 2f;
            renderX = cx + (renderX - cx) * _cameraZoom;
            renderY = cy + (renderY - cy) * _cameraZoom;
            float normalizedX = renderX / _worldRenderTarget.Width;
            float normalizedY = renderY / _worldRenderTarget.Height;

            normalizedX = MathHelper.Clamp(normalizedX, 0f, 1f);
            normalizedY = MathHelper.Clamp(normalizedY, 0f, 1f);

            float projectedX = _actualGameContentBounds.X + (normalizedX * _actualGameContentBounds.Width);
            float projectedY = _actualGameContentBounds.Y + (normalizedY * _actualGameContentBounds.Height);

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
            public bool IsOverlayDrop;
            public Point OverlayDropPosition;
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

        private enum OverlayEdgeSide { Left, Right, Top, Bottom }

        private readonly struct OverlayResizeEdge
        {
            public OverlayResizeEdge(DockBlock overlay, DockBlock parent, DockSplitOrientation orientation, Rectangle bounds, OverlayEdgeSide side)
            {
                Overlay = overlay;
                Parent = parent;
                Orientation = orientation;
                Bounds = bounds;
                Side = side;
            }

            public DockBlock Overlay { get; }
            public DockBlock Parent { get; }
            public DockSplitOrientation Orientation { get; }
            public Rectangle Bounds { get; }
            public OverlayEdgeSide Side { get; }
        }

        private readonly struct OverlayCornerHandle
        {
            public OverlayCornerHandle(OverlayResizeEdge verticalEdge, OverlayResizeEdge horizontalEdge, Rectangle bounds)
            {
                VerticalEdge = verticalEdge;
                HorizontalEdge = horizontalEdge;
                Bounds = bounds;
            }

            public OverlayResizeEdge VerticalEdge { get; }
            public OverlayResizeEdge HorizontalEdge { get; }
            public Rectangle Bounds { get; }
        }
    }
}
