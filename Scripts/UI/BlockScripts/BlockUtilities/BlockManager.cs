using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.Blocks;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io
{
    public static class BlockManager
    {
        private const string GamePanelKey = "game";
        private const string BlankPanelKey = "blank";
        private const string TransparentPanelKey = "transparent";
        private const string ControlsPanelKey = "controls";
        private const string NotesPanelKey = "notes";
        private const string BackendPanelKey = "backend";
        private const string SpecsPanelKey = "specs";
        private const string PanelMenuControlKey = "PanelMenu";
        private const string WideWordSeparator = "    ";
        private const int DragBarButtonPadding = 8;
        private const int DragBarButtonSpacing = 6;
        private const int WindowEdgeSnapDistance = 30;

        private static bool _dockingModeEnabled = true;
        private static bool _panelDefinitionsReady;
        private static bool _renderingDockedFrame;
        private const int CornerSnapDistance = 16;
        private static readonly Dictionary<string, DockPanel> _panels = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PanelNode> _panelNodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DockPanel> _orderedPanels = [];
        private static readonly Dictionary<string, bool> _panelLockStates = new(StringComparer.OrdinalIgnoreCase);
        private static DockNode _rootNode;
        private static Rectangle _cachedViewportBounds;
        private static Rectangle _layoutBounds;
        private static Rectangle _gameContentBounds;
        private static bool _layoutDirty = true;
        private static Viewport _previousViewport;
        private static bool _viewportPushed;
        private static RenderTarget2D _worldRenderTarget;
        private static Texture2D _pixelTexture;
        private static MouseState _previousMouseState;
        private static Point _mousePosition;
        private static DockPanel _draggingPanel;
        private static Rectangle _draggingStartBounds;
        private static Point _dragOffset;
        private static DockDropPreview? _dropPreview;
        private static bool _overlayMenuVisible;
        private static bool _panelMenuSwitchState;
        private static Rectangle _overlayBounds;
        private static Rectangle _overlayDismissBounds;
        private static Rectangle _overlayOpenAllBounds;
        private static Rectangle _overlayCloseAllBounds;
        private static readonly List<ResizeBar> _resizeBars = [];
        private static ResizeBar? _hoveredResizeBar;
        private static ResizeBar? _activeResizeBar;
        private static ResizeBar? _activeResizeBarSnapTarget;
        private static int? _activeResizeBarSnapCoordinate;
        private static readonly List<CornerHandle> _cornerHandles = [];
        private static CornerHandle? _hoveredCornerHandle;
        private static CornerHandle? _activeCornerHandle;
        private static CornerHandle? _activeCornerSnapTarget;
        private static Point? _activeCornerSnapPosition;
        private static Point? _activeCornerSnapAnchor;
        private static bool _activeCornerSnapLockX;
        private static bool _activeCornerSnapLockY;
        private static string _focusedPanelId;
        private static string _hoveredDragBarId;
        private static KeyboardState _previousKeyboardState;
        private static readonly List<PanelMenuEntry> _panelMenuEntries = [];
        private static readonly List<OverlayMenuRow> _overlayRows = [];
        private static PanelMenuEntry _activeNumericEntry;
        private static Dictionary<string, Rectangle> _resizeStartPanelBounds;

        public static bool IsPanelLocked(DockPanelKind panelKind)
        {
            DockPanel panel = _orderedPanels.FirstOrDefault(p => p.Kind == panelKind);
            return IsPanelLocked(panel);
        }

        public static bool DockingModeEnabled
        {
            get => _dockingModeEnabled;
            set
            {
                bool dockingChanged = _dockingModeEnabled != value;
                _dockingModeEnabled = value;
                ScreenManager.ApplyDockingWindowChrome(Core.Instance, _dockingModeEnabled);

                if (!dockingChanged)
                {
                    return;
                }

                if (!_dockingModeEnabled)
                {
                    CollapseInteractions();
                    ClearResizeBars();
                    MarkLayoutDirty();
                }
                else
                {
                    EnsurePanels();
                    MarkLayoutDirty();
                }
            }
        }

        public static void OnGraphicsReady()
        {
            EnsurePanels();
            MarkLayoutDirty();
            EnsureSurfaceResources(Core.Instance?.GraphicsDevice);
        }

        public static void Update(GameTime gameTime)
        {
            if (Core.Instance?.GraphicsDevice == null)
            {
                _previousMouseState = Mouse.GetState();
                return;
            }

            EnsurePanels();
            EnsureFocusedPanelValid();
            UpdateLayoutCache();
            EnsureSurfaceResources(Core.Instance.GraphicsDevice);

            MouseState mouseState = Mouse.GetState();
            KeyboardState keyboardState = Keyboard.GetState();
            _mousePosition = mouseState.Position;
            bool dockingEnabled = DockingModeEnabled;
            bool rebindOverlayOpen = ControlsBlock.IsRebindOverlayOpen();
            if (!dockingEnabled)
            {
                ClearDockingInteractions();
            }

            bool panelMenuState = rebindOverlayOpen ? false : GetPanelMenuState();
            if (panelMenuState != _panelMenuSwitchState)
            {
                _panelMenuSwitchState = panelMenuState;
                _overlayMenuVisible = panelMenuState;
            }

            if (!_overlayMenuVisible && !rebindOverlayOpen)
            {
                ResetOverlayLayout();
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickHeld = mouseState.LeftButton == ButtonState.Pressed;
            bool allowReorder = dockingEnabled;

            if (rebindOverlayOpen)
            {
                ClearDockingInteractions();
            }
            else if (_overlayMenuVisible)
            {
                UpdateOverlayKeyboardInput(keyboardState);
                UpdateOverlayInteractions(leftClickStarted);
                ClearDockingInteractions();
            }
            else if (dockingEnabled)
            {
                bool resizingPanels = allowReorder && (UpdateResizeBarState(leftClickStarted, leftClickHeld, leftClickReleased) ||
                    UpdateCornerResizeState(leftClickStarted, leftClickHeld, leftClickReleased));
                if (!resizingPanels)
                {
                    UpdateDragState(leftClickStarted, leftClickReleased, allowReorder);
                }
                else
                {
                    _draggingPanel = null;
                    _dropPreview = null;
                }
            }
            else
            {
                ClearDockingInteractions();
            }

            UpdateInteractiveBlocks(gameTime, mouseState, _previousMouseState, keyboardState, _previousKeyboardState);
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

            EnsurePanels();
            UpdateLayoutCache();
            EnsureSurfaceResources(graphicsDevice);

            bool readyForRenderTarget =
                _worldRenderTarget != null &&
                TryGetGamePanel(out DockPanel gamePanel) &&
                gamePanel.IsVisible &&
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

            if (!AnyPanelVisible())
            {
                DrawEmptyState(spriteBatch, viewport);
            }
            else
            {
                DrawPanels(spriteBatch);
            }

            DrawOverlayMenu(spriteBatch);
            ControlsBlock.DrawRebindOverlay(spriteBatch);
            spriteBatch.End();

            _renderingDockedFrame = false;
        }

        private static void EnsurePanels()
        {
            EnsurePanelMenuEntries();
            if (_panelDefinitionsReady)
            {
                return;
            }

            _panels.Clear();
            _panelNodes.Clear();
            _orderedPanels.Clear();
            ClearDockingInteractions();

            foreach (PanelMenuEntry entry in _panelMenuEntries)
            {
                if (entry.ControlMode == PanelMenuControlMode.Toggle)
                {
                    DockPanel panel = CreatePanel(entry.IdPrefix, entry.Label, entry.Kind);
                    panel.IsVisible = entry.IsVisible;
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
                        string panelId = BuildPanelId(entry.IdPrefix, i);
                        string title = BuildPanelTitle(entry, i);
                        CreatePanel(panelId, title, entry.Kind);
                    }
                }
            }

            _rootNode = BuildDefaultLayout();

            _panelDefinitionsReady = true;
            MarkLayoutDirty();
        }

        private static void EnsurePanelMenuEntries()
        {
            if (_panelMenuEntries.Count > 0)
            {
                return;
            }

            _panelMenuEntries.Add(new PanelMenuEntry(BlankPanelKey, BlankBlock.PanelTitle, DockPanelKind.Blank, PanelMenuControlMode.Count, 0, 10, 1));
            _panelMenuEntries.Add(new PanelMenuEntry(TransparentPanelKey, TransparentBlock.PanelTitle, DockPanelKind.Transparent, PanelMenuControlMode.Count, 0, 10, 1));
            _panelMenuEntries.Add(new PanelMenuEntry(GamePanelKey, GameBlock.PanelTitle, DockPanelKind.Game, PanelMenuControlMode.Toggle, initialVisible: true));
            _panelMenuEntries.Add(new PanelMenuEntry(ControlsPanelKey, ControlsBlock.PanelTitle, DockPanelKind.Controls, PanelMenuControlMode.Toggle, initialVisible: true));
            _panelMenuEntries.Add(new PanelMenuEntry(NotesPanelKey, NotesBlock.PanelTitle, DockPanelKind.Notes, PanelMenuControlMode.Toggle, initialVisible: true));
            _panelMenuEntries.Add(new PanelMenuEntry(BackendPanelKey, BackendBlock.PanelTitle, DockPanelKind.Backend, PanelMenuControlMode.Toggle, initialVisible: true));
            _panelMenuEntries.Add(new PanelMenuEntry(SpecsPanelKey, SpecsBlock.PanelTitle, DockPanelKind.Specs, PanelMenuControlMode.Toggle, initialVisible: true));
        }

        private static int ClampCount(PanelMenuEntry entry, int value)
        {
            if (entry == null)
            {
                return value;
            }

            return Math.Clamp(value, entry.MinCount, entry.MaxCount);
        }

        private static string BuildPanelId(string prefix, int index)
        {
            if (index <= 0)
            {
                return prefix;
            }

            return string.Concat(prefix, index + 1);
        }

        private static string BuildPanelTitle(PanelMenuEntry entry, int index)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (entry.ControlMode != PanelMenuControlMode.Count || entry.Count <= 1)
            {
                return entry.Label;
            }

            return $"{entry.Label} {index + 1}";
        }

        private static DockNode BuildDefaultLayout()
        {
            List<PanelNode> blankNodes = GetPanelNodesByKind(DockPanelKind.Blank);
            List<PanelNode> transparentNodes = GetPanelNodesByKind(DockPanelKind.Transparent);
            PanelNode gameNode = GetPanelNodesByKind(DockPanelKind.Game).FirstOrDefault();
            PanelNode controlsNode = GetPanelNodesByKind(DockPanelKind.Controls).FirstOrDefault();
            PanelNode notesNode = GetPanelNodesByKind(DockPanelKind.Notes).FirstOrDefault();
            PanelNode backendNode = GetPanelNodesByKind(DockPanelKind.Backend).FirstOrDefault();
            PanelNode specsNode = GetPanelNodesByKind(DockPanelKind.Specs).FirstOrDefault();

            DockNode transparentStack = BuildStack(transparentNodes, DockSplitOrientation.Horizontal);
            DockNode blankStack = BuildStack(blankNodes, DockSplitOrientation.Horizontal);

            float sideRatio = CalculateSplitRatio(transparentNodes.Count, blankNodes.Count, 0.5f);
            DockNode blankAndTransparent = CombineNodes(transparentStack, blankStack, DockSplitOrientation.Vertical, sideRatio);

            DockNode leftColumn = CombineNodes(blankAndTransparent, gameNode, DockSplitOrientation.Horizontal, 0.36f);
            DockNode controlsAndNotes = CombineNodes(controlsNode, notesNode, DockSplitOrientation.Horizontal, 0.5f);
            DockNode backendAndSpecs = CombineNodes(backendNode, specsNode, DockSplitOrientation.Horizontal, 0.58f);
            DockNode rightColumn = CombineNodes(controlsAndNotes, backendAndSpecs, DockSplitOrientation.Horizontal, 0.72f);

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

        private static DockNode BuildStack(IReadOnlyList<PanelNode> nodes, DockSplitOrientation orientation)
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

        private static List<PanelNode> GetPanelNodesByKind(DockPanelKind kind)
        {
            List<PanelNode> nodes = new();
            foreach (DockPanel panel in _orderedPanels)
            {
                if (panel == null || panel.Kind != kind)
                {
                    continue;
                }

                if (_panelNodes.TryGetValue(panel.Id, out PanelNode node))
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        private static DockPanel CreatePanel(string id, string title, DockPanelKind kind)
        {
            DockPanel panel = new(id, title, kind);
            (int minWidth, int minHeight) = GetPanelMinimumSize(kind);
            panel.MinWidth = Math.Max(0, minWidth);
            panel.MinHeight = Math.Max(0, minHeight);
            _panels[id] = panel;
            _orderedPanels.Add(panel);
            _panelNodes[id] = new PanelNode(panel);
            EnsurePanelLockState(panel);
            return panel;
        }

        private static (int MinWidth, int MinHeight) GetPanelMinimumSize(DockPanelKind kind)
        {
            const int defaultMin = 10;
            (int width, int height) = kind switch
            {
                DockPanelKind.Game => (GameBlock.MinWidth, GameBlock.MinHeight),
                DockPanelKind.Transparent => (TransparentBlock.MinWidth, TransparentBlock.MinHeight),
                DockPanelKind.Blank => (BlankBlock.MinWidth, BlankBlock.MinHeight),
                DockPanelKind.Controls => (ControlsBlock.MinWidth, ControlsBlock.MinHeight),
                DockPanelKind.Notes => (NotesBlock.MinWidth, NotesBlock.MinHeight),
                DockPanelKind.Backend => (BackendBlock.MinWidth, BackendBlock.MinHeight),
                DockPanelKind.Specs => (SpecsBlock.MinWidth, SpecsBlock.MinHeight),
                _ => (defaultMin, defaultMin)
            };

            // Ensure the drag bar area can never be occluded.
            int dragBarProtectedHeight = Math.Max(height, UIStyle.DragBarHeight);
            return (width, dragBarProtectedHeight);
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

            bool gamePanelVisible = TryGetGamePanel(out DockPanel gamePanel) && gamePanel.IsVisible;
            int desiredWidth = Math.Max(1, _gameContentBounds.Width);
            int desiredHeight = Math.Max(1, _gameContentBounds.Height);

            if (!gamePanelVisible || desiredWidth <= 1 || desiredHeight <= 1)
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

        private static bool UpdateResizeBarState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (!AnyPanelVisible() || _resizeBars.Count == 0)
            {
                _hoveredResizeBar = null;
                if (!leftClickHeld)
                {
                    _activeResizeBar = null;
                    ClearResizeBarSnap();
                }

                return false;
            }

            if (_activeResizeBar.HasValue)
            {
                if (!leftClickHeld || leftClickReleased)
                {
                    if (_activeResizeBar.HasValue)
                    {
                        DebugLogger.PrintUI($"[ResizeBarEnd] {DescribeResizeBar(_activeResizeBar.Value)}");
                    }
                    LogResizePanelDeltas();
                    _activeResizeBar = null;
                    ClearResizeBarSnap();
                    return false;
                }

                ApplyResizeBarDrag(_activeResizeBar.Value, _mousePosition);
                return true;
            }

            ResizeBar? hovered = HitTestResizeBar(_mousePosition);
            _hoveredResizeBar = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                DebugLogger.PrintUI($"[ResizeBarStart] {DescribeResizeBar(hovered.Value)} Mouse={_mousePosition}");
                CapturePanelBoundsForResize();
                _activeResizeBar = hovered;
                ClearResizeBarSnap();
                ApplyResizeBarDrag(hovered.Value, _mousePosition);
                return true;
            }

            return false;
        }

        private static bool UpdateCornerResizeState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (!AnyPanelVisible() || _cornerHandles.Count == 0)
            {
                _hoveredCornerHandle = null;
                if (!leftClickHeld)
                {
                    _activeCornerHandle = null;
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
                        LogResizePanelDeltas();
                    }
                    _activeCornerHandle = null;
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
                CapturePanelBoundsForResize();
                _activeCornerHandle = hovered;
                ClearCornerSnap();
                ApplyCornerHandleDrag(hovered.Value, _mousePosition);
                return true;
            }

            return false;
        }

        private static void UpdateDragState(bool leftClickStarted, bool leftClickReleased, bool allowReorder)
        {
            _hoveredDragBarId = null;

            if (!AnyPanelVisible())
            {
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
                DockPanel closeHit = HitTestCloseButton(_mousePosition);
                if (closeHit != null)
                {
                    if (PanelHasFocus(closeHit.Id))
                    {
                        ClearPanelFocus();
                    }

                    closeHit.IsVisible = false;
                    MarkLayoutDirty();
                    return;
                }
            }

            DockPanel dragBarHover = HitTestDragBarPanel(_mousePosition, excludeHeaderButtons: true);
            _hoveredDragBarId = dragBarHover?.Id;

            DockPanel dragBarHit = null;
            if (leftClickStarted)
            {
                dragBarHit = HitTestDragBarPanel(_mousePosition);
                if (dragBarHit != null)
                {
                    SetFocusedPanel(dragBarHit);
                }
            }

            if (!allowReorder)
            {
                _draggingPanel = null;
                _dropPreview = null;
                return;
            }

            if (_draggingPanel == null && leftClickStarted)
            {
                DockPanel panel = dragBarHit ?? HitTestDragBarPanel(_mousePosition);
                if (panel != null)
                {
                    _draggingPanel = panel;
                    _draggingStartBounds = panel.Bounds;
                    _dragOffset = new Point(_mousePosition.X - panel.Bounds.X, _mousePosition.Y - panel.Bounds.Y);
                }
            }
            else if (_draggingPanel != null)
            {
                _dropPreview = BuildDropPreview(_mousePosition);

                if (leftClickReleased)
                {
                    if (_dropPreview.HasValue)
                    {
                        ApplyDrop(_dropPreview.Value);
                    }

                    _draggingPanel = null;
                    _dropPreview = null;
                }
            }
        }

        private static bool TryTogglePanelLock(Point position)
        {
            DockPanel lockHit = HitTestLockButton(position);
            if (lockHit == null)
            {
                return false;
            }

            TogglePanelLock(lockHit);
            ClearDockingInteractions();
            return true;
        }

        private static bool IsPanelLocked(DockPanel panel)
        {
            if (panel == null || !IsLockToggleAvailable(panel))
            {
                return false;
            }

            EnsurePanelLockState(panel);
            _panelLockStates.TryGetValue(panel.Id, out bool locked);

            return locked;
        }

        private static void TogglePanelLock(DockPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            bool nextState = !IsPanelLocked(panel);
            _panelLockStates[panel.Id] = nextState;
            BlockDataStore.SetPanelLock(panel.Kind, nextState);
        }

        private static void EnsurePanelLockState(DockPanel panel)
        {
            if (panel == null || !IsLockToggleAvailable(panel) || _panelLockStates.ContainsKey(panel.Id))
            {
                return;
            }

            bool storedLock = BlockDataStore.GetPanelLock(panel.Kind);
            _panelLockStates[panel.Id] = storedLock;
        }

        private static DockPanel HitTestDragBarPanel(Point position, bool excludeHeaderButtons = false)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                Rectangle dragBarRect = panel.GetDragBarBounds(dragBarHeight);
                if (!dragBarRect.Contains(position))
                {
                    continue;
                }

                if (excludeHeaderButtons && IsPointOnHeaderButton(panel, dragBarHeight, position))
                {
                    continue;
                }

                return panel;
            }

            return null;
        }

        private static DockPanel HitTestCloseButton(Point position)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                Rectangle closeBounds = GetCloseButtonBounds(panel, dragBarHeight);
                if (closeBounds != Rectangle.Empty && closeBounds.Contains(position))
                {
                    return panel;
                }
            }

            return null;
        }

        private static DockPanel HitTestLockButton(Point position)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible || !IsLockToggleAvailable(panel))
                {
                    continue;
                }

                Rectangle lockBounds = GetLockButtonBounds(panel, dragBarHeight);
                if (lockBounds != Rectangle.Empty && lockBounds.Contains(position))
                {
                    return panel;
                }
            }

            return null;
        }

        private static bool IsPointOnHeaderButton(DockPanel panel, int dragBarHeight, Point position)
        {
            GetHeaderButtonBounds(panel, dragBarHeight, out Rectangle lockBounds, out Rectangle closeBounds);
            return (lockBounds != Rectangle.Empty && lockBounds.Contains(position)) ||
                (closeBounds != Rectangle.Empty && closeBounds.Contains(position));
        }

        private static Rectangle GetCloseButtonBounds(DockPanel panel, int dragBarHeight)
        {
            GetHeaderButtonBounds(panel, dragBarHeight, out _, out Rectangle closeBounds);
            return closeBounds;
        }

        private static Rectangle GetLockButtonBounds(DockPanel panel, int dragBarHeight)
        {
            GetHeaderButtonBounds(panel, dragBarHeight, out Rectangle lockBounds, out _);
            return lockBounds;
        }

        private static void GetHeaderButtonBounds(DockPanel panel, int dragBarHeight, out Rectangle lockBounds, out Rectangle closeBounds)
        {
            lockBounds = Rectangle.Empty;
            closeBounds = Rectangle.Empty;

            if (panel == null || dragBarHeight <= 0)
            {
                return;
            }

            Rectangle dragBarRect = panel.GetDragBarBounds(dragBarHeight);
            if (dragBarRect.Width <= 0 || dragBarRect.Height <= 0)
            {
                return;
            }

            closeBounds = GetCloseButtonBounds(dragBarRect);

            if (!IsLockToggleAvailable(panel) || closeBounds == Rectangle.Empty)
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

            lockBounds = new Rectangle(x, y, buttonSize, buttonSize);
        }

        private static bool IsLockToggleAvailable(DockPanel panel)
        {
            if (panel == null)
            {
                return false;
            }

            return panel.Kind == DockPanelKind.Controls ||
                panel.Kind == DockPanelKind.Backend ||
                panel.Kind == DockPanelKind.Specs;
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
            int existingCount = CountVisiblePanelsAlongOrientation(_rootNode, orientation, _draggingPanel);
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
            DockDropPreview? preview = BuildViewportSnapPreview(position);
            if (preview.HasValue)
            {
                return preview;
            }

            preview = null;
            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible || panel == _draggingPanel)
                {
                    continue;
                }

                Rectangle bounds = panel.Bounds;
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
                    TargetPanel = panel,
                    Edge = edge,
                    HighlightBounds = highlight
                };
                break;
            }

            return preview;
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

        private static ResizeBar? HitTestResizeBar(Point position)
        {
            // Prefer the deepest (most specific) split when handles overlap so dragging a nested panel
            // doesn't unexpectedly move higher-level columns/rows.
            ResizeBar? best = null;
            int bestDepth = -1;

            foreach (ResizeBar handle in _resizeBars)
            {
                Rectangle hitBounds = handle.Bounds;
                hitBounds.Inflate(2, 2);
                if (hitBounds.Contains(position))
                {
                    if (!best.HasValue || handle.Depth > bestDepth)
                    {
                        best = handle;
                        bestDepth = handle.Depth;
                    }
                }
            }

            return best;
        }

        private static void ApplyResizeBarDrag(ResizeBar handle, Point position)
        {
            if (handle.Node == null)
            {
                return;
            }

            position = GetResizeBarPosition(handle, position);

            Rectangle bounds = handle.Node.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            float previousRatio = handle.Node.SplitRatio;
            float newRatio;
            if (handle.Orientation == DockSplitOrientation.Vertical)
            {
                int relative = position.X - bounds.X;
                newRatio = ClampSplitRatio(handle.Node, relative, bounds.Width);
            }
            else
            {
                int relative = position.Y - bounds.Y;
                newRatio = ClampSplitRatio(handle.Node, relative, bounds.Height);
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
                DebugLogger.PrintUI($"[ResizeBarDrag] Ori={handle.Orientation} Node={DescribeNode(handle.Node)} Prev={previousRatio:F3} New={newRatio:F3} RelativePos={(handle.Orientation == DockSplitOrientation.Vertical ? position.X - bounds.X : position.Y - bounds.Y)} Bounds={bounds} Mouse={position} First={firstDesc} Second={secondDesc}");
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
            ApplyResizeBarDrag(corner.VerticalHandle, snapped);
            ApplyResizeBarDrag(corner.HorizontalHandle, snapped);
        }

        private static Point GetResizeBarPosition(ResizeBar handle, Point position)
        {
            if (!_activeResizeBar.HasValue || !ResizeBarsEqual(handle, _activeResizeBar.Value))
            {
                return position;
            }

            int? snapCoordinate = GetResizeBarSnapCoordinate(handle, position);
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

        private static int? GetResizeBarSnapCoordinate(ResizeBar handle, Point position)
        {
            int axisPosition = handle.Orientation == DockSplitOrientation.Vertical ? position.X : position.Y;
            ResizeBar? candidate = FindResizeBarSnapTarget(handle, axisPosition, CornerSnapDistance);

            if (candidate.HasValue)
            {
                int targetCoordinate = GetResizeBarAxisCenter(candidate.Value);
                _activeResizeBarSnapTarget = candidate.Value;
                _activeResizeBarSnapCoordinate = targetCoordinate;
                return targetCoordinate;
            }

            if (_activeResizeBarSnapCoordinate.HasValue)
            {
                int releaseDistance = GetResizeBarReleaseDistance();
                int distance = Math.Abs(axisPosition - _activeResizeBarSnapCoordinate.Value);
                if (distance <= releaseDistance)
                {
                    return _activeResizeBarSnapCoordinate.Value;
                }

                ClearResizeBarSnap();
            }

            return null;
        }

        private static ResizeBar? FindResizeBarSnapTarget(ResizeBar handle, int axisPosition, int threshold)
        {
            if (_resizeBars.Count <= 1 || threshold <= 0)
            {
                return null;
            }

            int snapDistance = Math.Max(1, threshold);
            int bestDistance = snapDistance;
            ResizeBar? bestHandle = null;

            foreach (ResizeBar other in _resizeBars)
            {
                if (ResizeBarsEqual(other, handle) || other.Orientation != handle.Orientation)
                {
                    continue;
                }

                int otherCoordinate = GetResizeBarAxisCenter(other);
                int distance = Math.Abs(otherCoordinate - axisPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHandle = other;
                }
            }

            return bestHandle;
        }

        private static int GetResizeBarAxisCenter(ResizeBar handle)
        {
            return handle.Orientation == DockSplitOrientation.Vertical
                ? handle.Bounds.Center.X
                : handle.Bounds.Center.Y;
        }

        private static int GetResizeBarReleaseDistance()
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

        private static int CountVisiblePanelsAlongOrientation(DockNode node, DockSplitOrientation orientation, DockPanel excludePanel = null)
        {
            if (node == null || !node.HasVisibleContent)
            {
                return 0;
            }

            if (node is PanelNode panelNode)
            {
                DockPanel panel = panelNode.Panel;
                if (panel == null || !panel.IsVisible || (excludePanel != null && ReferenceEquals(panel, excludePanel)))
                {
                    return 0;
                }

                return 1;
            }

            if (node is SplitNode split)
            {
                int first = CountVisiblePanelsAlongOrientation(split.First, orientation, excludePanel);
                int second = CountVisiblePanelsAlongOrientation(split.Second, orientation, excludePanel);
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

            CornerSnapResult? snapResult = FindCornerSnapTarget(corner, position, CornerSnapDistance);
            if (snapResult.HasValue)
            {
                CornerSnapResult result = snapResult.Value;
                Point lockedPosition = new(
                    result.LockX ? result.SnapPoint.X : position.X,
                    result.LockY ? result.SnapPoint.Y : position.Y);
                Point anchor = new(
                    result.LockX ? result.SnapPoint.X : position.X,
                    result.LockY ? result.SnapPoint.Y : position.Y);

                _activeCornerSnapTarget = result.Target;
                _activeCornerSnapPosition = lockedPosition;
                _activeCornerSnapAnchor = anchor;
                _activeCornerSnapLockX = result.LockX;
                _activeCornerSnapLockY = result.LockY;
                return lockedPosition;
            }

            if (_activeCornerSnapPosition.HasValue && _activeCornerSnapAnchor.HasValue && (_activeCornerSnapLockX || _activeCornerSnapLockY))
            {
                if (IsWithinCornerSnapRange(position))
                {
                    Point snapPoint = _activeCornerSnapPosition.Value;
                    Point clamped = new(
                        _activeCornerSnapLockX ? snapPoint.X : position.X,
                        _activeCornerSnapLockY ? snapPoint.Y : position.Y);
                    _activeCornerSnapPosition = clamped;
                    return clamped;
                }

                ClearCornerSnap();
            }

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

        private static void ApplyDrop(DockDropPreview preview)
        {
            if (_draggingPanel == null)
            {
                return;
            }

            if (preview.IsViewportSnap)
            {
                ApplyViewportSnap(preview.Edge);
                return;
            }

            if (preview.TargetPanel == null)
            {
                return;
            }

            if (_draggingPanel == preview.TargetPanel)
            {
                return;
            }

            if (!_panelNodes.TryGetValue(_draggingPanel.Id, out PanelNode movingNode) ||
                !_panelNodes.TryGetValue(preview.TargetPanel.Id, out PanelNode targetNode))
            {
                return;
            }

            _rootNode = DockLayout.Detach(_rootNode, movingNode);
            _rootNode ??= targetNode;
            _rootNode = DockLayout.InsertRelative(_rootNode, movingNode, targetNode, preview.Edge);
            MarkLayoutDirty();
        }

        private static void ApplyViewportSnap(DockEdge edge)
        {
            if (_draggingPanel == null)
            {
                return;
            }

            if (!_panelNodes.TryGetValue(_draggingPanel.Id, out PanelNode movingNode))
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

            int existingCount = CountVisiblePanelsAlongOrientation(remaining, orientation);
            int total = Math.Max(1, existingCount + 1);
            float newPanelFraction = 1f / total;

            SplitNode split = new(orientation);

            if (edge is DockEdge.Top or DockEdge.Left)
            {
                split.First = movingNode;
                split.Second = remaining;
                split.SplitRatio = newPanelFraction;
            }
            else
            {
                split.First = remaining;
                split.Second = movingNode;
                split.SplitRatio = 1f - newPanelFraction;
            }

            _rootNode = split;
            MarkLayoutDirty();
        }

        private static void DrawPanels(SpriteBatch spriteBatch)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            bool showDockingChrome = DockingModeEnabled;

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                DrawPanelBackground(spriteBatch, panel, dragBarHeight);
                DrawPanelContent(spriteBatch, panel, dragBarHeight);
            }

            if (showDockingChrome)
            {
                DrawResizeBars(spriteBatch);
                DrawCornerHandles(spriteBatch);
            }

            if (_draggingPanel != null)
            {
                Rectangle floating = new(_mousePosition.X - _dragOffset.X, _mousePosition.Y - _dragOffset.Y, _draggingStartBounds.Width, _draggingStartBounds.Height);
                DrawRect(spriteBatch, floating, UIStyle.PanelBackground * 0.75f);
                DrawRectOutline(spriteBatch, floating, UIStyle.AccentColor * 0.8f, UIStyle.DragOutlineThickness);
            }

            if (_dropPreview.HasValue)
            {
                DrawRect(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentMuted);
                DrawRectOutline(spriteBatch, _dropPreview.Value.HighlightBounds, UIStyle.AccentColor, UIStyle.DragOutlineThickness);
            }
        }

        private static void DrawResizeBars(SpriteBatch spriteBatch)
        {
            if (_resizeBars.Count == 0)
            {
                return;
            }

            foreach (ResizeBar handle in _resizeBars)
            {
                Color color = UIStyle.ResizeBarColor;
                bool isActive = _activeResizeBar.HasValue && ReferenceEquals(handle.Node, _activeResizeBar.Value.Node);
                bool isHovered = _hoveredResizeBar.HasValue && ReferenceEquals(handle.Node, _hoveredResizeBar.Value.Node);

                if (!isActive && _activeCornerHandle.HasValue && CornerContainsResizeBar(_activeCornerHandle.Value, handle))
                {
                    isActive = true;
                }

                if (!isHovered && _hoveredCornerHandle.HasValue && CornerContainsResizeBar(_hoveredCornerHandle.Value, handle))
                {
                    isHovered = true;
                }

                if (isActive)
                {
                    color = UIStyle.ResizeBarActiveColor;
                }
                else if (isHovered)
                {
                    color = UIStyle.ResizeBarHoverColor;
                }

                DrawRect(spriteBatch, handle.Bounds, color);
            }
        }

        private static void DrawPanelBackground(SpriteBatch spriteBatch, DockPanel panel, int dragBarHeight)
        {
            bool isTransparentPanel = panel.Kind == DockPanelKind.Transparent;
            DrawRect(spriteBatch, panel.Bounds, isTransparentPanel ? Core.TransparentWindowColor : UIStyle.PanelBackground);
            DrawRectOutline(spriteBatch, panel.Bounds, UIStyle.PanelBorder, UIStyle.PanelBorderThickness);

            if (dragBarHeight <= 0)
            {
                return;
            }

            Rectangle dragBar = panel.GetDragBarBounds(dragBarHeight);
            if (dragBar.Height <= 0)
            {
                return;
            }

            DrawRect(spriteBatch, dragBar, UIStyle.HeaderBackground);
            bool dragBarHovered = string.Equals(_hoveredDragBarId, panel.Id, StringComparison.Ordinal);
            GetHeaderButtonBounds(panel, dragBarHeight, out Rectangle lockButtonBounds, out Rectangle closeButtonBounds);

            if (dragBarHovered)
            {
                DrawRect(spriteBatch, dragBar, UIStyle.DragBarHoverTint);
            }

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            if (headerFont.IsAvailable)
            {
                Vector2 textSize = headerFont.MeasureString(panel.Title);
                float textY = dragBar.Bottom - textSize.Y + 3f;
                Vector2 textPosition = new Vector2(dragBar.X + 12, textY);
                headerFont.DrawString(spriteBatch, panel.Title, textPosition, UIStyle.TextColor);
            }

            if (lockButtonBounds != Rectangle.Empty)
            {
                bool panelLocked = IsPanelLocked(panel);
                bool hovered = lockButtonBounds.Contains(_mousePosition);
                DrawLockToggleButton(spriteBatch, lockButtonBounds, panelLocked, hovered);
            }

            if (closeButtonBounds != Rectangle.Empty)
            {
                bool hovered = closeButtonBounds.Contains(_mousePosition);
                DrawPanelCloseButton(spriteBatch, closeButtonBounds, hovered);
            }
        }

        private static void DrawPanelCloseButton(SpriteBatch spriteBatch, Rectangle bounds, bool hovered)
        {
            Color background = hovered ? new Color(140, 32, 32, 240) : new Color(80, 20, 20, 220);
            Color border = hovered ? new Color(220, 72, 72) : new Color(160, 40, 40);
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.PanelBorderThickness);

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
            Color glyphColor = hovered ? Color.White : Color.OrangeRed;
            glyphFont.DrawString(spriteBatch, glyph, glyphPosition, glyphColor);
        }

        private static void DrawLockToggleButton(SpriteBatch spriteBatch, Rectangle bounds, bool isLocked, bool hovered)
        {
            Color background = isLocked ? new Color(38, 38, 38, hovered ? 240 : 220) : new Color(68, 92, 160, hovered ? 250 : 230);
            Color border = isLocked ? (hovered ? UIStyle.AccentColor : UIStyle.PanelBorder) : UIStyle.AccentColor;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.PanelBorderThickness);

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            string glyph = isLocked ? "L" : "U";
            Vector2 glyphSize = glyphFont.MeasureString(glyph);
            Vector2 glyphPosition = new(
                bounds.X + (bounds.Width - glyphSize.X) / 2f,
                bounds.Y + (bounds.Height - glyphSize.Y) / 2f - 1f);
            Color glyphColor = Color.White;
            glyphFont.DrawString(spriteBatch, glyph, glyphPosition, glyphColor);
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
                Color color = UIStyle.ResizeBarColor;
                Rectangle bounds = corner.Bounds;

                bool isActiveCorner = _activeCornerHandle.HasValue && CornerEquals(corner, _activeCornerHandle.Value);
                if (isActiveCorner)
                {
                    color = UIStyle.ResizeBarActiveColor;

                    if (_activeCornerSnapPosition.HasValue)
                    {
                        bounds = CenterRectangle(bounds, _activeCornerSnapPosition.Value);
                    }
                }
                else if (_hoveredCornerHandle.HasValue && CornerEquals(corner, _hoveredCornerHandle.Value))
                {
                    color = UIStyle.ResizeBarHoverColor;
                }

                DrawRect(spriteBatch, bounds, color);
            }
        }

        private static void DrawPanelContent(SpriteBatch spriteBatch, DockPanel panel, int dragBarHeight)
        {
            Rectangle contentBounds = panel.GetContentBounds(dragBarHeight, UIStyle.PanelPadding);

            switch (panel.Kind)
            {
                case DockPanelKind.Game:
                    GameBlock.Draw(spriteBatch, contentBounds, _worldRenderTarget);
                    break;
                case DockPanelKind.Transparent:
                    TransparentBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Blank:
                    BlankBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Controls:
                    ControlsBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Notes:
                    NotesBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Backend:
                    BackendBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Specs:
                    SpecsBlock.Draw(spriteBatch, contentBounds);
                    break;
            }
        }

        private static void UpdateInteractiveBlocks(GameTime gameTime, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            if (gameTime == null || _orderedPanels.Count == 0)
            {
                return;
            }

            if (ControlsBlock.IsRebindOverlayOpen())
            {
                ControlsBlock.UpdateRebindOverlay(gameTime, mouseState, previousMouseState, keyboardState, previousKeyboardState);
                return;
            }

            int dragBarHeight = GetActiveDragBarHeight();
            foreach (DockPanel panel in _orderedPanels)
            {
                if (panel == null || !panel.IsVisible)
                {
                    continue;
                }

                Rectangle contentBounds = panel.GetContentBounds(dragBarHeight, UIStyle.PanelPadding);
                switch (panel.Kind)
                {
                    case DockPanelKind.Notes:
                        NotesBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockPanelKind.Controls:
                        ControlsBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockPanelKind.Backend:
                        BackendBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                    case DockPanelKind.Specs:
                        SpecsBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
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
            DrawRectOutline(spriteBatch, _overlayBounds, UIStyle.PanelBorder, UIStyle.PanelBorderThickness);

            string header = "Panel visibility";
            Vector2 headerSize = headerFont.MeasureString(header);
            Vector2 headerPosition = new(_overlayBounds.X + (_overlayBounds.Width - headerSize.X) / 2f, _overlayBounds.Y + 12);
            headerFont.DrawString(spriteBatch, header, headerPosition, UIStyle.TextColor);
            DrawOverlayDismissButton(spriteBatch);

            int rowY = _overlayBounds.Y + 52;
            foreach (OverlayMenuRow row in _overlayRows)
            {
                PanelMenuEntry entry = row.Entry;
                if (entry == null)
                {
                    continue;
                }

                bodyFont.DrawString(spriteBatch, entry.Label, new Vector2(_overlayBounds.X + 20, rowY), UIStyle.TextColor);

                if (entry.ControlMode == PanelMenuControlMode.Toggle)
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

            string openAllLabel = JoinWordsWithWideSpacing("Open", "all");
            string closeAllLabel = JoinWordsWithWideSpacing("Close", "all");
            bool openAllHovered = UIButtonRenderer.IsHovered(_overlayOpenAllBounds, _mousePosition);
            bool closeAllHovered = UIButtonRenderer.IsHovered(_overlayCloseAllBounds, _mousePosition);
            UIButtonRenderer.Draw(spriteBatch, _overlayOpenAllBounds, openAllLabel, UIButtonRenderer.ButtonStyle.Blue, openAllHovered);
            UIButtonRenderer.Draw(spriteBatch, _overlayCloseAllBounds, closeAllLabel, UIButtonRenderer.ButtonStyle.Grey, closeAllHovered);
        }

        private static void CollapseInteractions()
        {
            _overlayMenuVisible = false;
            _panelMenuSwitchState = false;
            ClearDockingInteractions();
            ResetOverlayLayout();
        }

        private static void ClearDockingInteractions()
        {
            _draggingPanel = null;
            _dropPreview = null;
            _hoveredDragBarId = null;
            _hoveredResizeBar = null;
            _activeResizeBar = null;
            ClearResizeBarSnap();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
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

        private static void ClearResizeBarSnap()
        {
            _activeResizeBarSnapTarget = null;
            _activeResizeBarSnapCoordinate = null;
        }

        private static void DrawOverlayDismissButton(SpriteBatch spriteBatch)
        {
            if (_overlayDismissBounds == Rectangle.Empty)
            {
                return;
            }

            bool hovered = UIButtonRenderer.IsHovered(_overlayDismissBounds, _mousePosition);
            Color background = new(64, 24, 24, 240);
            Color hoverBackground = new(90, 36, 36, 240);
            Color border = new(150, 40, 40);
            UIButtonRenderer.Draw(
                spriteBatch,
                _overlayDismissBounds,
                "X",
                UIButtonRenderer.ButtonStyle.Grey,
                hovered,
                isDisabled: false,
                textColorOverride: Color.OrangeRed,
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

            Color border = isActive ? UIStyle.AccentColor : UIStyle.PanelBorder;
            DrawRect(spriteBatch, bounds, UIStyle.PanelBackground);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.PanelBorderThickness);

            text ??= string.Empty;
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPosition = new(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f);
            font.DrawString(spriteBatch, text, textPosition, UIStyle.TextColor);
        }

        private static string GetNumericDisplayText(PanelMenuEntry entry)
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
                SetAllPanelsVisibility(true);
                return;
            }

            if (_overlayCloseAllBounds.Contains(_mousePosition))
            {
                SetAllPanelsVisibility(false);
                return;
            }

            foreach (OverlayMenuRow row in _overlayRows)
            {
                PanelMenuEntry entry = row.Entry;
                if (entry == null)
                {
                    continue;
                }

                if (entry.ControlMode == PanelMenuControlMode.Toggle)
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

        private static void RebuildPanelsFromMenuChange()
        {
            _panelDefinitionsReady = false;
            EnsurePanels();
            MarkLayoutDirty();
        }

        private static void ApplyToggleVisibility(PanelMenuEntry entry)
        {
            if (entry == null || entry.ControlMode != PanelMenuControlMode.Toggle)
            {
                return;
            }

            if (_panels.TryGetValue(entry.IdPrefix, out DockPanel panel))
            {
                panel.IsVisible = entry.IsVisible;
            }

            MarkLayoutDirty();
        }

        private static void AdjustNumericEntry(PanelMenuEntry entry, int newValue)
        {
            if (entry == null || entry.ControlMode != PanelMenuControlMode.Count)
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
            RebuildPanelsFromMenuChange();
        }

        private static void SetActiveNumericEntry(PanelMenuEntry entry)
        {
            if (entry == null || entry.ControlMode != PanelMenuControlMode.Count)
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
        }

        private static void ClearActiveNumericEntry()
        {
            if (_activeNumericEntry != null)
            {
                _activeNumericEntry.IsEditing = false;
                _activeNumericEntry.InputBuffer = _activeNumericEntry.Count.ToString();
            }

            _activeNumericEntry = null;
        }

        private static void ClearOverlayEditingState()
        {
            foreach (PanelMenuEntry entry in _panelMenuEntries)
            {
                entry.IsEditing = false;
                if (entry.ControlMode == PanelMenuControlMode.Count)
                {
                    entry.InputBuffer = entry.Count.ToString();
                }
            }

            _activeNumericEntry = null;
        }

        private static void UpdateOverlayKeyboardInput(KeyboardState keyboardState)
        {
            if (!_overlayMenuVisible || _activeNumericEntry == null)
            {
                return;
            }

            PanelMenuEntry entry = _activeNumericEntry;
            bool changed = false;

            if (WasKeyPressed(keyboardState, Keys.Back))
            {
                if (!string.IsNullOrEmpty(entry.InputBuffer))
                {
                    entry.InputBuffer = entry.InputBuffer[..^1];
                }
                else
                {
                    entry.InputBuffer = string.Empty;
                }

                changed = true;
            }

            if (WasKeyPressed(keyboardState, Keys.Delete))
            {
                entry.InputBuffer = string.Empty;
                changed = true;
            }

            if (WasKeyPressed(keyboardState, Keys.Enter))
            {
                ApplyNumericBuffer(entry);
                ClearActiveNumericEntry();
                return;
            }

            if (WasKeyPressed(keyboardState, Keys.Escape))
            {
                entry.InputBuffer = entry.Count.ToString();
                ClearActiveNumericEntry();
                return;
            }

            for (int digit = 0; digit <= 9; digit++)
            {
                Keys mainKey = (Keys)((int)Keys.D0 + digit);
                Keys numpadKey = (Keys)((int)Keys.NumPad0 + digit);
                if (WasKeyPressed(keyboardState, mainKey) || WasKeyPressed(keyboardState, numpadKey))
                {
                    AppendDigit(entry, digit);
                    changed = true;
                }
            }

            if (changed)
            {
                ApplyNumericBuffer(entry);
            }
        }

        private static bool WasKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private static void AppendDigit(PanelMenuEntry entry, int digit)
        {
            if (entry == null || entry.ControlMode != PanelMenuControlMode.Count)
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

        private static void ApplyNumericBuffer(PanelMenuEntry entry)
        {
            if (entry == null || entry.ControlMode != PanelMenuControlMode.Count)
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
                RebuildPanelsFromMenuChange();
            }
        }

        private static void CloseOverlayMenuFromUi()
        {
            _overlayMenuVisible = false;
            bool overrideApplied = InputTypeManager.OverrideSwitchState(PanelMenuControlKey, false);
            if (!overrideApplied && ControlStateManager.ContainsSwitchState(PanelMenuControlKey))
            {
                ControlStateManager.SetSwitchState(PanelMenuControlKey, false);
            }
            _panelMenuSwitchState = false;

            _overlayBounds = Rectangle.Empty;
            _overlayDismissBounds = Rectangle.Empty;
            _overlayOpenAllBounds = Rectangle.Empty;
            _overlayCloseAllBounds = Rectangle.Empty;
            _overlayRows.Clear();
            ClearOverlayEditingState();
        }

        private static bool GetPanelMenuState()
        {
            bool liveState = InputManager.IsInputActive(PanelMenuControlKey);
            if (ControlStateManager.ContainsSwitchState(PanelMenuControlKey))
            {
                bool cachedState = ControlStateManager.GetSwitchState(PanelMenuControlKey);
                return liveState || cachedState;
            }

            return liveState;
        }

        private static void BuildOverlayLayout()
        {
            EnsurePanelMenuEntries();
            Rectangle viewport = Core.Instance?.GraphicsDevice?.Viewport.Bounds ?? new Rectangle(0, 0, 1280, 720);
            int width = 360;
            int height = 120 + (_panelMenuEntries.Count * 32);
            _overlayBounds = new Rectangle(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
            int closeButtonSize = 24;
            _overlayDismissBounds = new Rectangle(_overlayBounds.Right - closeButtonSize - 12, _overlayBounds.Y + 12, closeButtonSize, closeButtonSize);

            _overlayRows.Clear();
            int rowY = _overlayBounds.Y + 52;
            foreach (PanelMenuEntry entry in _panelMenuEntries)
            {
                Rectangle toggleBounds = Rectangle.Empty;
                Rectangle minusBounds = Rectangle.Empty;
                Rectangle inputBounds = Rectangle.Empty;
                Rectangle plusBounds = Rectangle.Empty;

                if (entry.ControlMode == PanelMenuControlMode.Toggle)
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

            string label = GetPanelHotkeyLabel();
            string message = BuildWideSpacedSentence(label);
            Vector2 size = font.MeasureString(message);
            Vector2 position = new(viewport.X + (viewport.Width - size.X) / 2f, viewport.Y + (viewport.Height - size.Y) / 2f);
            font.DrawString(spriteBatch, message, position, UIStyle.MutedTextColor);
        }

        private static string BuildWideSpacedSentence(string label)
        {
            return JoinWordsWithWideSpacing("Press", label, "to", "open", "panels");
        }

        private static Rectangle GetLayoutBounds(Rectangle viewport)
        {
            // No outer padding: panels should touch the window edges.
            return viewport;
        }

        private static int GetActiveDragBarHeight()
        {
            return DockingModeEnabled ? UIStyle.DragBarHeight : 0;
        }

        private static bool AnyPanelVisible() => _orderedPanels.Any(panel => panel.IsVisible);

        public static bool IsPanelMenuOpen() => _overlayMenuVisible;

        public static bool IsInputBlocked() => _overlayMenuVisible || ControlsBlock.IsRebindOverlayOpen();

        public static bool IsCursorWithinGamePanel()
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

        private static string GetPanelHotkeyLabel()
        {
            string label = InputManager.GetBindingDisplayLabel(PanelMenuControlKey);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = "Shift + X";
            }

            return label;
        }

        private static string JoinWordsWithWideSpacing(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> filtered = new(parts.Length);
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    filtered.Add(trimmed);
                }
            }

            return filtered.Count == 0 ? string.Empty : string.Join(WideWordSeparator, filtered);
        }

        private static void SetAllPanelsVisibility(bool value)
        {
            EnsurePanelMenuEntries();
            bool definitionsChanged = false;
            foreach (PanelMenuEntry entry in _panelMenuEntries)
            {
                if (entry.ControlMode == PanelMenuControlMode.Toggle)
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
                RebuildPanelsFromMenuChange();
            }
        }

        private static void UpdateLayoutCache()
        {
            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                ClearResizeBars();
                return;
            }

            Rectangle viewport = graphicsDevice.Viewport.Bounds;
            if (viewport != _cachedViewportBounds)
            {
                _cachedViewportBounds = viewport;
                _layoutDirty = true;
            }

            if (!_layoutDirty)
            {
                if (!DockingModeEnabled)
                {
                    ClearResizeBars();
                }

                return;
            }

            _layoutBounds = GetLayoutBounds(viewport);
            _rootNode?.Arrange(_layoutBounds);

            if (DockingModeEnabled)
            {
                RebuildResizeBars();
            }
            else
            {
                ClearResizeBars();
            }

            _gameContentBounds = Rectangle.Empty;
            int dragBarHeight = GetActiveDragBarHeight();
            if (TryGetGamePanel(out DockPanel gamePanel) && gamePanel.IsVisible)
            {
                _gameContentBounds = gamePanel.GetContentBounds(dragBarHeight, UIStyle.PanelPadding);
            }

            _layoutDirty = false;
        }

        private static void MarkLayoutDirty()
        {
            _layoutDirty = true;
        }

        private static void RebuildResizeBars()
        {
            if (_rootNode == null || !AnyPanelVisible())
            {
                ClearResizeBars();
                return;
            }

            _resizeBars.Clear();
            CollectResizeBars(_rootNode, 0);
            RebuildCornerHandles();

            if (_hoveredResizeBar.HasValue)
            {
                _hoveredResizeBar = FindResizeBarForNode(_hoveredResizeBar.Value.Node);
            }

            if (_activeResizeBar.HasValue)
            {
                _activeResizeBar = FindResizeBarForNode(_activeResizeBar.Value.Node);
            }

            if (_activeResizeBarSnapTarget.HasValue)
            {
                ResizeBar? refreshed = FindResizeBarForNode(_activeResizeBarSnapTarget.Value.Node);
                if (refreshed.HasValue && refreshed.Value.Orientation == _activeResizeBarSnapTarget.Value.Orientation)
                {
                    _activeResizeBarSnapTarget = refreshed;
                    _activeResizeBarSnapCoordinate = GetResizeBarAxisCenter(refreshed.Value);
                }
                else
                {
                    ClearResizeBarSnap();
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

        private static void CollectResizeBars(DockNode node, int depth)
        {
            if (node is not SplitNode split)
            {
                return;
            }

            bool firstVisible = split.First?.HasVisibleContent ?? false;
            bool secondVisible = split.Second?.HasVisibleContent ?? false;

            if (firstVisible && secondVisible)
            {
                Rectangle handleBounds = GetResizeBarBounds(split);
                if (handleBounds.Width > 0 && handleBounds.Height > 0)
                {
                    _resizeBars.Add(new ResizeBar(split, split.Orientation, handleBounds, depth));
                }
            }

            if (split.First != null)
            {
                CollectResizeBars(split.First, depth + 1);
            }

            if (split.Second != null)
            {
                CollectResizeBars(split.Second, depth + 1);
            }
        }

        private static void RebuildCornerHandles()
        {
            _cornerHandles.Clear();
            if (_resizeBars.Count == 0)
            {
                return;
            }

            // Keep corner targets tight to the actual intersection so dragging a nearby bar
            // doesn't accidentally activate a combined corner resize.
            int inflate = 0;

            foreach (ResizeBar vertical in _resizeBars)
            {
                if (vertical.Orientation != DockSplitOrientation.Vertical)
                {
                    continue;
                }

                foreach (ResizeBar horizontal in _resizeBars)
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

        private static Rectangle GetResizeBarBounds(SplitNode split)
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

            int thickness = Math.Max(2, UIStyle.ResizeBarThickness);
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

        private static ResizeBar? FindResizeBarForNode(SplitNode node)
        {
            if (node == null)
            {
                return null;
            }

            foreach (ResizeBar handle in _resizeBars)
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

        private static Point GetCornerIntersection(CornerHandle corner)
        {
            return new Point(
                corner.VerticalHandle.Bounds.Center.X,
                corner.HorizontalHandle.Bounds.Center.Y);
        }

        private static bool CornerContainsResizeBar(CornerHandle corner, ResizeBar handle)
        {
            return ReferenceEquals(corner.VerticalHandle.Node, handle.Node) ||
                   ReferenceEquals(corner.HorizontalHandle.Node, handle.Node);
        }

        private static bool ResizeBarsEqual(ResizeBar a, ResizeBar b)
        {
            return ReferenceEquals(a.Node, b.Node) && a.Orientation == b.Orientation;
        }

        private static void CapturePanelBoundsForResize()
        {
            _resizeStartPanelBounds = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            foreach (DockPanel panel in _orderedPanels)
            {
                if (panel != null)
                {
                    _resizeStartPanelBounds[panel.Id] = panel.Bounds;
                }
            }
        }

        private static void LogResizePanelDeltas()
        {
            if (_resizeStartPanelBounds == null || _resizeStartPanelBounds.Count == 0)
            {
                return;
            }

            List<string> changes = new();
            foreach (DockPanel panel in _orderedPanels)
            {
                if (panel == null)
                {
                    continue;
                }

                Rectangle start = _resizeStartPanelBounds.TryGetValue(panel.Id, out Rectangle value) ? value : Rectangle.Empty;
                Rectangle end = panel.Bounds;
                if (start == end)
                {
                    continue;
                }

                changes.Add($"{panel.Title}: {start} -> {end}");
            }

            if (changes.Count > 0)
            {
                DebugLogger.PrintUI("[ResizeLayoutDelta] " + string.Join(" | ", changes));
            }

            _resizeStartPanelBounds = null;
        }

        private static string DescribeNode(DockNode node)
        {
            if (node is PanelNode panelNode)
            {
                string id = panelNode.Panel?.Id ?? "null";
                Rectangle bounds = panelNode.Panel?.Bounds ?? panelNode.Bounds;
                return $"Panel(id={id}, bounds={bounds})";
            }

            if (node is SplitNode split)
            {
                return $"Split({split.Orientation}, ratio={split.SplitRatio:F3}, bounds={split.Bounds})";
            }

            return node?.GetType()?.Name ?? "null";
        }

        private static string DescribeResizeBar(ResizeBar handle)
        {
            return $"Handle[{handle.Orientation}] Depth={handle.Depth} Bounds={handle.Bounds} Node={DescribeNode(handle.Node)} First={DescribeNode(handle.Node?.First)} Second={DescribeNode(handle.Node?.Second)}";
        }

        private static string DescribeCornerHandle(CornerHandle corner)
        {
            return $"Corner V={DescribeResizeBar(corner.VerticalHandle)} H={DescribeResizeBar(corner.HorizontalHandle)} Bounds={corner.Bounds}";
        }

        private static void ClearResizeBars()
        {
            _resizeBars.Clear();
            _hoveredResizeBar = null;
            _activeResizeBar = null;
            ClearResizeBarSnap();
            _cornerHandles.Clear();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
            ClearCornerSnap();
            _hoveredDragBarId = null;
            _resizeStartPanelBounds = null;
        }

        private static bool TryGetGamePanel(out DockPanel panel)
        {
            if (_panels.TryGetValue(GamePanelKey, out panel))
            {
                return true;
            }

            panel = null;
            return false;
        }

        public static bool TryFocusPanel(DockPanelKind kind)
        {
            if (!TryGetPanelByKind(kind, out DockPanel panel))
            {
                return false;
            }

            SetFocusedPanel(panel);
            return true;
        }

        public static bool PanelHasFocus(DockPanelKind kind)
        {
            if (!TryGetPanelByKind(kind, out DockPanel panel))
            {
                return false;
            }

            return PanelHasFocus(panel?.Id);
        }

        public static bool PanelHasFocus(string panelId)
        {
            if (string.IsNullOrWhiteSpace(panelId) || string.IsNullOrWhiteSpace(_focusedPanelId))
            {
                return false;
            }

            return string.Equals(_focusedPanelId, panelId, StringComparison.OrdinalIgnoreCase);
        }

        public static void ClearPanelFocus()
        {
            _focusedPanelId = null;
        }

        public static DockPanelKind? GetFocusedPanelKind()
        {
            if (string.IsNullOrWhiteSpace(_focusedPanelId))
            {
                return null;
            }

            if (!_panels.TryGetValue(_focusedPanelId, out DockPanel panel))
            {
                return null;
            }

            return panel.Kind;
        }

        private static void SetFocusedPanel(DockPanel panel)
        {
            if (panel == null || !panel.IsVisible)
            {
                return;
            }

            _focusedPanelId = panel.Id;
        }

        private static void EnsureFocusedPanelValid()
        {
            if (string.IsNullOrWhiteSpace(_focusedPanelId))
            {
                return;
            }

            if (!_panels.TryGetValue(_focusedPanelId, out DockPanel panel) || panel == null || !panel.IsVisible)
            {
                _focusedPanelId = null;
            }
        }

        private static bool TryGetPanelByKind(DockPanelKind kind, out DockPanel panel)
        {
            panel = _orderedPanels.FirstOrDefault(p => p.Kind == kind);
            return panel != null;
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

        private enum PanelMenuControlMode
        {
            Toggle,
            Count
        }

        private sealed class PanelMenuEntry
        {
            public PanelMenuEntry(string idPrefix, string label, DockPanelKind kind, PanelMenuControlMode controlMode, int minCount = 0, int maxCount = 10, int initialCount = 0, bool initialVisible = true)
            {
                IdPrefix = idPrefix ?? throw new ArgumentNullException(nameof(idPrefix));
                Label = string.IsNullOrWhiteSpace(label) ? idPrefix : label;
                Kind = kind;
                ControlMode = controlMode;
                MinCount = Math.Max(0, minCount);
                MaxCount = Math.Max(MinCount, maxCount);
                Count = controlMode == PanelMenuControlMode.Count ? Math.Clamp(initialCount, MinCount, MaxCount) : 0;
                IsVisible = controlMode == PanelMenuControlMode.Toggle ? initialVisible : true;
                InputBuffer = Count.ToString();
            }

            public string IdPrefix { get; }
            public string Label { get; }
            public DockPanelKind Kind { get; }
            public PanelMenuControlMode ControlMode { get; }
            public int MinCount { get; }
            public int MaxCount { get; }
            public int Count { get; set; }
            public bool IsVisible { get; set; }
            public bool IsEditing { get; set; }
            public string InputBuffer { get; set; }
        }

        private readonly struct OverlayMenuRow
        {
            public OverlayMenuRow(PanelMenuEntry entry, Rectangle toggleBounds, Rectangle minusBounds, Rectangle inputBounds, Rectangle plusBounds)
            {
                Entry = entry;
                ToggleBounds = toggleBounds;
                MinusBounds = minusBounds;
                InputBounds = inputBounds;
                PlusBounds = plusBounds;
            }

            public PanelMenuEntry Entry { get; }
            public Rectangle ToggleBounds { get; }
            public Rectangle MinusBounds { get; }
            public Rectangle InputBounds { get; }
            public Rectangle PlusBounds { get; }
        }

        private readonly struct ResizeBar
        {
            public ResizeBar(SplitNode node, DockSplitOrientation orientation, Rectangle bounds, int depth)
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
            public CornerHandle(ResizeBar verticalHandle, ResizeBar horizontalHandle, Rectangle bounds)
            {
                VerticalHandle = verticalHandle;
                HorizontalHandle = horizontalHandle;
                Bounds = bounds;
            }

            public ResizeBar VerticalHandle { get; }
            public ResizeBar HorizontalHandle { get; }
            public Rectangle Bounds { get; }
        }

        private struct DockDropPreview
        {
            public DockPanel TargetPanel;
            public DockEdge Edge;
            public Rectangle HighlightBounds;
            public bool IsViewportSnap;
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



