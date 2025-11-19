using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.Blocks;

namespace op.io
{
    public static class BlockManager
    {
        private const string GamePanelKey = "game";
        private const string BlankPanelKey = "blank";
        private const string ControlsPanelKey = "controls";
        private const string NotesPanelKey = "notes";
        private const string BackendPanelKey = "backend";
        private const string PanelMenuControlKey = "PanelMenu";

        private static bool _dockingModeEnabled = true;
        private static bool _panelDefinitionsReady;
        private static bool _renderingDockedFrame;
        private const int CornerSnapDistance = 16;
        private static readonly Dictionary<string, DockPanel> _panels = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PanelNode> _panelNodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DockPanel> _orderedPanels = [];
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
        private static readonly List<OverlayPanelToggle> _overlayToggles = [];
        private static readonly List<SplitHandle> _splitHandles = [];
        private static SplitHandle? _hoveredSplitHandle;
        private static SplitHandle? _activeSplitHandle;
        private static SplitHandle? _activeSplitSnapTarget;
        private static int? _activeSplitSnapCoordinate;
        private static readonly List<CornerHandle> _cornerHandles = [];
        private static CornerHandle? _hoveredCornerHandle;
        private static CornerHandle? _activeCornerHandle;
        private static CornerHandle? _activeCornerSnapTarget;
        private static Point? _activeCornerSnapPosition;
        private static Point? _activeCornerSnapAnchor;
        private static bool _activeCornerSnapLockX;
        private static bool _activeCornerSnapLockY;
        private static string _focusedPanelId;

        public static bool DockingModeEnabled
        {
            get => _dockingModeEnabled;
            set
            {
                if (_dockingModeEnabled == value)
                {
                    return;
                }

                _dockingModeEnabled = value;
                if (!_dockingModeEnabled)
                {
                    CollapseInteractions();
                    ClearSplitHandles();
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
            _mousePosition = mouseState.Position;
            bool dockingEnabled = DockingModeEnabled;
            if (!dockingEnabled)
            {
                ClearDockingInteractions();
            }

            bool panelMenuState = GetPanelMenuState();
            if (panelMenuState != _panelMenuSwitchState)
            {
                _panelMenuSwitchState = panelMenuState;
                _overlayMenuVisible = panelMenuState;
            }

            if (!_overlayMenuVisible)
            {
                ResetOverlayLayout();
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickHeld = mouseState.LeftButton == ButtonState.Pressed;

            if (_overlayMenuVisible)
            {
                UpdateOverlayInteractions(leftClickStarted);
                ClearDockingInteractions();
            }
            else if (dockingEnabled)
            {
                bool resizingPanels = UpdateCornerResizeState(leftClickStarted, leftClickHeld, leftClickReleased) ||
                    UpdateSplitResizeState(leftClickStarted, leftClickHeld, leftClickReleased);
                if (!resizingPanels)
                {
                    UpdateDragState(leftClickStarted, leftClickReleased);
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

            UpdateInteractiveBlocks(gameTime, mouseState, _previousMouseState);
            _previousMouseState = mouseState;
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
            DrawRect(spriteBatch, viewport, UIStyle.ScreenBackground);

            if (!AnyPanelVisible())
            {
                DrawEmptyState(spriteBatch, viewport);
            }
            else
            {
                DrawPanels(spriteBatch);
            }

            DrawOverlayMenu(spriteBatch);
            spriteBatch.End();

            _renderingDockedFrame = false;
        }

        private static void EnsurePanels()
        {
            if (_panelDefinitionsReady)
            {
                return;
            }

            _panels.Clear();
            _panelNodes.Clear();
            _orderedPanels.Clear();

            DockPanel blank = CreatePanel(BlankPanelKey, "Blank Panel", DockPanelKind.Blank);
            DockPanel game = CreatePanel(GamePanelKey, "Game", DockPanelKind.Game);
            DockPanel controls = CreatePanel(ControlsPanelKey, "Controls", DockPanelKind.Controls);
            DockPanel notes = CreatePanel(NotesPanelKey, "Notes", DockPanelKind.Notes);
            DockPanel backend = CreatePanel(BackendPanelKey, "Backend", DockPanelKind.Backend);

            PanelNode blankNode = _panelNodes[blank.Id];
            PanelNode gameNode = _panelNodes[game.Id];
            PanelNode controlsNode = _panelNodes[controls.Id];
            PanelNode notesNode = _panelNodes[notes.Id];
            PanelNode backendNode = _panelNodes[backend.Id];

            SplitNode leftColumn = new(DockSplitOrientation.Horizontal)
            {
                SplitRatio = 0.36f,
                First = blankNode,
                Second = gameNode
            };

            SplitNode controlsAndNotes = new(DockSplitOrientation.Horizontal)
            {
                SplitRatio = 0.5f,
                First = controlsNode,
                Second = notesNode
            };

            SplitNode rightColumn = new(DockSplitOrientation.Horizontal)
            {
                SplitRatio = 0.72f,
                First = controlsAndNotes,
                Second = backendNode
            };

            _rootNode = new SplitNode(DockSplitOrientation.Vertical)
            {
                SplitRatio = 0.67f,
                First = leftColumn,
                Second = rightColumn
            };

            _panelDefinitionsReady = true;
            MarkLayoutDirty();
        }

        private static DockPanel CreatePanel(string id, string title, DockPanelKind kind)
        {
            DockPanel panel = new(id, title, kind);
            _panels[id] = panel;
            _orderedPanels.Add(panel);
            _panelNodes[id] = new PanelNode(panel);
            return panel;
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

        private static bool UpdateSplitResizeState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (!AnyPanelVisible() || _splitHandles.Count == 0)
            {
                _hoveredSplitHandle = null;
                if (!leftClickHeld)
                {
                    _activeSplitHandle = null;
                    ClearSplitSnap();
                }

                return false;
            }

            if (_activeSplitHandle.HasValue)
            {
                if (!leftClickHeld || leftClickReleased)
                {
                    _activeSplitHandle = null;
                    ClearSplitSnap();
                    return false;
                }

                ApplySplitHandleDrag(_activeSplitHandle.Value, _mousePosition);
                return true;
            }

            SplitHandle? hovered = HitTestSplitHandle(_mousePosition);
            _hoveredSplitHandle = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                _activeSplitHandle = hovered;
                ClearSplitSnap();
                ApplySplitHandleDrag(hovered.Value, _mousePosition);
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
                _activeCornerHandle = hovered;
                ClearCornerSnap();
                ApplyCornerHandleDrag(hovered.Value, _mousePosition);
                return true;
            }

            return false;
        }

        private static void UpdateDragState(bool leftClickStarted, bool leftClickReleased)
        {
            if (!AnyPanelVisible())
            {
                _draggingPanel = null;
                _dropPreview = null;
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

            DockPanel headerHit = null;
            if (leftClickStarted)
            {
                headerHit = HitTestHeader(_mousePosition);
                if (headerHit != null)
                {
                    SetFocusedPanel(headerHit);
                }
            }

            if (_draggingPanel == null && leftClickStarted)
            {
                DockPanel panel = headerHit ?? HitTestHeader(_mousePosition);
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

        private static DockPanel HitTestHeader(Point position)
        {
            int headerHeight = GetActiveHeaderHeight();
            if (headerHeight <= 0)
            {
                return null;
            }

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                Rectangle headerRect = panel.GetHeaderBounds(headerHeight);
                if (headerRect.Contains(position))
                {
                    return panel;
                }
            }

            return null;
        }

        private static DockPanel HitTestCloseButton(Point position)
        {
            int headerHeight = GetActiveHeaderHeight();
            if (headerHeight <= 0)
            {
                return null;
            }

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                Rectangle closeBounds = GetCloseButtonBounds(panel, headerHeight);
                if (closeBounds != Rectangle.Empty && closeBounds.Contains(position))
                {
                    return panel;
                }
            }

            return null;
        }

        private static Rectangle GetCloseButtonBounds(DockPanel panel, int headerHeight)
        {
            Rectangle headerRect = panel.GetHeaderBounds(headerHeight);
            return GetCloseButtonBounds(headerRect);
        }

        private static Rectangle GetCloseButtonBounds(Rectangle headerRect)
        {
            if (headerRect.Width <= 0 || headerRect.Height <= 0)
            {
                return Rectangle.Empty;
            }

            const int horizontalPadding = 8;
            int buttonSize = Math.Clamp(headerRect.Height - 10, 14, 24);
            if (buttonSize <= 0 || headerRect.Width <= (horizontalPadding * 2) + buttonSize)
            {
                return Rectangle.Empty;
            }

            int x = headerRect.Right - horizontalPadding - buttonSize;
            int y = headerRect.Y + (headerRect.Height - buttonSize) / 2;
            return new Rectangle(x, y, buttonSize, buttonSize);
        }

        private static DockDropPreview? BuildDropPreview(Point position)
        {
            DockDropPreview? preview = null;
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
                hitBounds.Inflate(2, 2);
                if (hitBounds.Contains(position))
                {
                    return corner;
                }
            }

            return null;
        }

        private static SplitHandle? HitTestSplitHandle(Point position)
        {
            foreach (SplitHandle handle in _splitHandles)
            {
                Rectangle hitBounds = handle.Bounds;
                hitBounds.Inflate(2, 2);
                if (hitBounds.Contains(position))
                {
                    return handle;
                }
            }

            return null;
        }

        private static void ApplySplitHandleDrag(SplitHandle handle, Point position)
        {
            if (handle.Node == null)
            {
                return;
            }

            position = GetSplitDragPosition(handle, position);

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
                newRatio = ClampSplitRatio(relative, bounds.Width);
            }
            else
            {
                int relative = position.Y - bounds.Y;
                newRatio = ClampSplitRatio(relative, bounds.Height);
            }

            if (float.IsNaN(newRatio) || float.IsInfinity(newRatio))
            {
                return;
            }

            newRatio = MathHelper.Clamp(newRatio, 0.001f, 0.999f);
            if (Math.Abs(newRatio - previousRatio) > 0.0001f)
            {
                handle.Node.SplitRatio = newRatio;
                MarkLayoutDirty();
            }
        }

        private static void ApplyCornerHandleDrag(CornerHandle corner, Point position)
        {
            Point snapped = GetCornerDragPosition(corner, position);
            ApplySplitHandleDrag(corner.VerticalHandle, snapped);
            ApplySplitHandleDrag(corner.HorizontalHandle, snapped);
        }

        private static Point GetSplitDragPosition(SplitHandle handle, Point position)
        {
            if (!_activeSplitHandle.HasValue || !SplitHandlesEqual(handle, _activeSplitHandle.Value))
            {
                return position;
            }

            int? snapCoordinate = GetSplitSnapCoordinate(handle, position);
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

        private static int? GetSplitSnapCoordinate(SplitHandle handle, Point position)
        {
            int axisPosition = handle.Orientation == DockSplitOrientation.Vertical ? position.X : position.Y;
            SplitHandle? candidate = FindSplitSnapTarget(handle, axisPosition, CornerSnapDistance);

            if (candidate.HasValue)
            {
                int targetCoordinate = GetSplitHandleAxisCenter(candidate.Value);
                _activeSplitSnapTarget = candidate.Value;
                _activeSplitSnapCoordinate = targetCoordinate;
                return targetCoordinate;
            }

            if (_activeSplitSnapCoordinate.HasValue)
            {
                int releaseDistance = GetSplitReleaseDistance();
                int distance = Math.Abs(axisPosition - _activeSplitSnapCoordinate.Value);
                if (distance <= releaseDistance)
                {
                    return _activeSplitSnapCoordinate.Value;
                }

                ClearSplitSnap();
            }

            return null;
        }

        private static SplitHandle? FindSplitSnapTarget(SplitHandle handle, int axisPosition, int threshold)
        {
            if (_splitHandles.Count <= 1 || threshold <= 0)
            {
                return null;
            }

            int snapDistance = Math.Max(1, threshold);
            int bestDistance = snapDistance;
            SplitHandle? bestHandle = null;

            foreach (SplitHandle other in _splitHandles)
            {
                if (SplitHandlesEqual(other, handle) || other.Orientation != handle.Orientation)
                {
                    continue;
                }

                int otherCoordinate = GetSplitHandleAxisCenter(other);
                int distance = Math.Abs(otherCoordinate - axisPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestHandle = other;
                }
            }

            return bestHandle;
        }

        private static int GetSplitHandleAxisCenter(SplitHandle handle)
        {
            return handle.Orientation == DockSplitOrientation.Vertical
                ? handle.Bounds.Center.X
                : handle.Bounds.Center.Y;
        }

        private static int GetSplitReleaseDistance()
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

        private static float ClampSplitRatio(int relativePosition, int spanLength)
        {
            if (spanLength <= 0)
            {
                return 0.5f;
            }

            int minClamp = Math.Min(UIStyle.MinPanelSize, spanLength / 2);
            int maxClamp = Math.Max(minClamp, spanLength - minClamp);
            int clamped = Math.Clamp(relativePosition, minClamp, maxClamp);
            return clamped / (float)Math.Max(1, spanLength);
        }

        private static void ApplyDrop(DockDropPreview preview)
        {
            if (_draggingPanel == null || preview.TargetPanel == null)
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

        private static void DrawPanels(SpriteBatch spriteBatch)
        {
            int headerHeight = GetActiveHeaderHeight();
            bool showDockingChrome = DockingModeEnabled;

            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                DrawPanelBackground(spriteBatch, panel, headerHeight);
                DrawPanelContent(spriteBatch, panel, headerHeight);
            }

            if (showDockingChrome)
            {
                DrawSplitHandles(spriteBatch);
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

        private static void DrawSplitHandles(SpriteBatch spriteBatch)
        {
            if (_splitHandles.Count == 0)
            {
                return;
            }

            foreach (SplitHandle handle in _splitHandles)
            {
                Color color = UIStyle.SplitHandleColor;
                bool isActive = _activeSplitHandle.HasValue && ReferenceEquals(handle.Node, _activeSplitHandle.Value.Node);
                bool isHovered = _hoveredSplitHandle.HasValue && ReferenceEquals(handle.Node, _hoveredSplitHandle.Value.Node);

                if (!isActive && _activeCornerHandle.HasValue && CornerContainsHandle(_activeCornerHandle.Value, handle))
                {
                    isActive = true;
                }

                if (!isHovered && _hoveredCornerHandle.HasValue && CornerContainsHandle(_hoveredCornerHandle.Value, handle))
                {
                    isHovered = true;
                }

                if (isActive)
                {
                    color = UIStyle.SplitHandleActiveColor;
                }
                else if (isHovered)
                {
                    color = UIStyle.SplitHandleHoverColor;
                }

                DrawRect(spriteBatch, handle.Bounds, color);
            }
        }

        private static void DrawPanelBackground(SpriteBatch spriteBatch, DockPanel panel, int headerHeight)
        {
            DrawRect(spriteBatch, panel.Bounds, UIStyle.PanelBackground);
            DrawRectOutline(spriteBatch, panel.Bounds, UIStyle.PanelBorder, UIStyle.PanelBorderThickness);

            if (headerHeight <= 0)
            {
                return;
            }

            Rectangle header = panel.GetHeaderBounds(headerHeight);
            if (header.Height <= 0)
            {
                return;
            }

            DrawRect(spriteBatch, header, UIStyle.HeaderBackground);
            Rectangle closeButtonBounds = GetCloseButtonBounds(header);

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            if (headerFont.IsAvailable)
            {
                Vector2 textSize = headerFont.MeasureString(panel.Title);
                float textY = header.Bottom - textSize.Y + 3f;
                Vector2 textPosition = new Vector2(header.X + 12, textY);
                headerFont.DrawString(spriteBatch, panel.Title, textPosition, UIStyle.TextColor);
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
                Color color = UIStyle.SplitHandleColor;
                Rectangle bounds = corner.Bounds;

                bool isActiveCorner = _activeCornerHandle.HasValue && CornerEquals(corner, _activeCornerHandle.Value);
                if (isActiveCorner)
                {
                    color = UIStyle.SplitHandleActiveColor;

                    if (_activeCornerSnapPosition.HasValue)
                    {
                        bounds = CenterRectangle(bounds, _activeCornerSnapPosition.Value);
                    }
                }
                else if (_hoveredCornerHandle.HasValue && CornerEquals(corner, _hoveredCornerHandle.Value))
                {
                    color = UIStyle.SplitHandleHoverColor;
                }

                DrawRect(spriteBatch, bounds, color);
            }
        }

        private static void DrawPanelContent(SpriteBatch spriteBatch, DockPanel panel, int headerHeight)
        {
            Rectangle contentBounds = panel.GetContentBounds(headerHeight, UIStyle.PanelPadding);

            switch (panel.Kind)
            {
                case DockPanelKind.Game:
                    GameBlock.Draw(spriteBatch, contentBounds, _worldRenderTarget);
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
            }
        }

        private static void UpdateInteractiveBlocks(GameTime gameTime, MouseState mouseState, MouseState previousMouseState)
        {
            if (gameTime == null || _orderedPanels.Count == 0)
            {
                return;
            }

            int headerHeight = GetActiveHeaderHeight();
            foreach (DockPanel panel in _orderedPanels)
            {
                if (panel == null || !panel.IsVisible)
                {
                    continue;
                }

                Rectangle contentBounds = panel.GetContentBounds(headerHeight, UIStyle.PanelPadding);
                switch (panel.Kind)
                {
                    case DockPanelKind.Notes:
                        NotesBlock.Update(gameTime, contentBounds, mouseState, previousMouseState);
                        break;
                }
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
            foreach (OverlayPanelToggle toggle in _overlayToggles)
            {
                string label = toggle.Panel.Title;
                bodyFont.DrawString(spriteBatch, label, new Vector2(_overlayBounds.X + 20, rowY), UIStyle.TextColor);
                string state = toggle.Panel.IsVisible ? "Hide" : "Show";
                DrawButton(spriteBatch, toggle.Bounds, state, toggle.Panel.IsVisible ? UIStyle.AccentColor : UIStyle.PanelBorder);
                rowY += 32;
            }

            DrawButton(spriteBatch, _overlayOpenAllBounds, "Open all", UIStyle.AccentColor);
            DrawButton(spriteBatch, _overlayCloseAllBounds, "Close all", UIStyle.PanelBorder);
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
            _hoveredSplitHandle = null;
            _activeSplitHandle = null;
            ClearSplitSnap();
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
            _overlayToggles.Clear();
        }

        private static void ClearCornerSnap()
        {
            _activeCornerSnapTarget = null;
            _activeCornerSnapPosition = null;
            _activeCornerSnapAnchor = null;
            _activeCornerSnapLockX = false;
            _activeCornerSnapLockY = false;
        }

        private static void ClearSplitSnap()
        {
            _activeSplitSnapTarget = null;
            _activeSplitSnapCoordinate = null;
        }

        private static void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Color border)
        {
            UIStyle.UIFont buttonFont = UIStyle.FontBody;
            if (!buttonFont.IsAvailable)
            {
                return;
            }

            DrawRect(spriteBatch, bounds, UIStyle.PanelBackground);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.PanelBorderThickness);

            Vector2 textSize = buttonFont.MeasureString(label);
            Vector2 textPosition = new(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f);
            buttonFont.DrawString(spriteBatch, label, textPosition, UIStyle.TextColor);
        }

        private static void DrawOverlayDismissButton(SpriteBatch spriteBatch)
        {
            if (_overlayDismissBounds == Rectangle.Empty)
            {
                return;
            }

            Color background = new(64, 24, 24, 240);
            Color border = new(150, 40, 40);
            DrawRect(spriteBatch, _overlayDismissBounds, background);
            DrawRectOutline(spriteBatch, _overlayDismissBounds, border, UIStyle.PanelBorderThickness);

            UIStyle.UIFont glyphFont = UIStyle.FontBody;
            if (!glyphFont.IsAvailable)
            {
                return;
            }

            const string glyph = "X";
            Vector2 glyphSize = glyphFont.MeasureString(glyph);
            Vector2 glyphPosition = new(
                _overlayDismissBounds.X + (_overlayDismissBounds.Width - glyphSize.X) / 2f,
                _overlayDismissBounds.Y + (_overlayDismissBounds.Height - glyphSize.Y) / 2f - 1f);
            glyphFont.DrawString(spriteBatch, glyph, glyphPosition, Color.OrangeRed);
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

            foreach (OverlayPanelToggle toggle in _overlayToggles)
            {
                if (toggle.Bounds.Contains(_mousePosition))
                {
                    toggle.Panel.IsVisible = !toggle.Panel.IsVisible;
                    MarkLayoutDirty();
                    return;
                }
            }

            if (_overlayOpenAllBounds.Contains(_mousePosition))
            {
                SetAllPanelsVisibility(true);
            }
            else if (_overlayCloseAllBounds.Contains(_mousePosition))
            {
                SetAllPanelsVisibility(false);
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
            _overlayToggles.Clear();
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
            Rectangle viewport = Core.Instance?.GraphicsDevice?.Viewport.Bounds ?? new Rectangle(0, 0, 1280, 720);
            int width = 360;
            int height = 120 + (_orderedPanels.Count * 32);
            _overlayBounds = new Rectangle(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
            int closeButtonSize = 24;
            _overlayDismissBounds = new Rectangle(_overlayBounds.Right - closeButtonSize - 12, _overlayBounds.Y + 12, closeButtonSize, closeButtonSize);

            _overlayToggles.Clear();
            int rowY = _overlayBounds.Y + 52;
            foreach (DockPanel panel in _orderedPanels)
            {
                Rectangle toggleBounds = new(_overlayBounds.Right - 96, rowY - 4, 76, 28);
                _overlayToggles.Add(new OverlayPanelToggle(panel, toggleBounds));
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
            string message = $"Press {label} to open panels";
            Vector2 size = font.MeasureString(message);
            Vector2 position = new(viewport.X + (viewport.Width - size.X) / 2f, viewport.Y + (viewport.Height - size.Y) / 2f);
            font.DrawString(spriteBatch, message, position, UIStyle.MutedTextColor);
        }

        private static Rectangle GetLayoutBounds(Rectangle viewport)
        {
            return new Rectangle(
                viewport.X + UIStyle.LayoutPadding,
                viewport.Y + UIStyle.LayoutPadding,
                Math.Max(0, viewport.Width - (UIStyle.LayoutPadding * 2)),
                Math.Max(0, viewport.Height - (UIStyle.LayoutPadding * 2)));
        }

        private static int GetActiveHeaderHeight()
        {
            return DockingModeEnabled ? UIStyle.HeaderHeight : 0;
        }

        private static bool AnyPanelVisible() => _orderedPanels.Any(panel => panel.IsVisible);

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

        private static void SetAllPanelsVisibility(bool value)
        {
            foreach (DockPanel panel in _orderedPanels)
            {
                panel.IsVisible = value;
            }

            MarkLayoutDirty();
        }

        private static void UpdateLayoutCache()
        {
            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
                ClearSplitHandles();
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
                    ClearSplitHandles();
                }

                return;
            }

            _layoutBounds = GetLayoutBounds(viewport);
            _rootNode?.Arrange(_layoutBounds, UIStyle.MinPanelSize);

            if (DockingModeEnabled)
            {
                RebuildSplitHandles();
            }
            else
            {
                ClearSplitHandles();
            }

            _gameContentBounds = Rectangle.Empty;
            int headerHeight = GetActiveHeaderHeight();
            if (TryGetGamePanel(out DockPanel gamePanel) && gamePanel.IsVisible)
            {
                _gameContentBounds = gamePanel.GetContentBounds(headerHeight, UIStyle.PanelPadding);
            }

            _layoutDirty = false;
        }

        private static void MarkLayoutDirty()
        {
            _layoutDirty = true;
        }

        private static void RebuildSplitHandles()
        {
            if (_rootNode == null || !AnyPanelVisible())
            {
                ClearSplitHandles();
                return;
            }

            _splitHandles.Clear();
            CollectSplitHandles(_rootNode);
            RebuildCornerHandles();

            if (_hoveredSplitHandle.HasValue)
            {
                _hoveredSplitHandle = FindHandleForNode(_hoveredSplitHandle.Value.Node);
            }

            if (_activeSplitHandle.HasValue)
            {
                _activeSplitHandle = FindHandleForNode(_activeSplitHandle.Value.Node);
            }

            if (_activeSplitSnapTarget.HasValue)
            {
                SplitHandle? refreshed = FindHandleForNode(_activeSplitSnapTarget.Value.Node);
                if (refreshed.HasValue && refreshed.Value.Orientation == _activeSplitSnapTarget.Value.Orientation)
                {
                    _activeSplitSnapTarget = refreshed;
                    _activeSplitSnapCoordinate = GetSplitHandleAxisCenter(refreshed.Value);
                }
                else
                {
                    ClearSplitSnap();
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

        private static void CollectSplitHandles(DockNode node)
        {
            if (node is not SplitNode split)
            {
                return;
            }

            bool firstVisible = split.First?.HasVisibleContent ?? false;
            bool secondVisible = split.Second?.HasVisibleContent ?? false;

            if (firstVisible && secondVisible)
            {
                Rectangle handleBounds = GetSplitHandleBounds(split);
                if (handleBounds.Width > 0 && handleBounds.Height > 0)
                {
                    _splitHandles.Add(new SplitHandle(split, split.Orientation, handleBounds));
                }
            }

            if (split.First != null)
            {
                CollectSplitHandles(split.First);
            }

            if (split.Second != null)
            {
                CollectSplitHandles(split.Second);
            }
        }

        private static void RebuildCornerHandles()
        {
            _cornerHandles.Clear();
            if (_splitHandles.Count == 0)
            {
                return;
            }

            int inflate = Math.Max(2, UIStyle.SplitHandleThickness / 2);

            foreach (SplitHandle vertical in _splitHandles)
            {
                if (vertical.Orientation != DockSplitOrientation.Vertical)
                {
                    continue;
                }

                foreach (SplitHandle horizontal in _splitHandles)
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

        private static Rectangle GetSplitHandleBounds(SplitNode split)
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

            int thickness = Math.Max(2, UIStyle.SplitHandleThickness);
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

        private static SplitHandle? FindHandleForNode(SplitNode node)
        {
            if (node == null)
            {
                return null;
            }

            foreach (SplitHandle handle in _splitHandles)
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

        private static bool CornerContainsHandle(CornerHandle corner, SplitHandle handle)
        {
            return ReferenceEquals(corner.VerticalHandle.Node, handle.Node) ||
                   ReferenceEquals(corner.HorizontalHandle.Node, handle.Node);
        }

        private static bool SplitHandlesEqual(SplitHandle a, SplitHandle b)
        {
            return ReferenceEquals(a.Node, b.Node) && a.Orientation == b.Orientation;
        }

        private static void ClearSplitHandles()
        {
            _splitHandles.Clear();
            _hoveredSplitHandle = null;
            _activeSplitHandle = null;
            ClearSplitSnap();
            _cornerHandles.Clear();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
            ClearCornerSnap();
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

        private readonly struct OverlayPanelToggle
        {
            public OverlayPanelToggle(DockPanel panel, Rectangle bounds)
            {
                Panel = panel;
                Bounds = bounds;
            }

            public DockPanel Panel { get; }
            public Rectangle Bounds { get; }
        }

        private readonly struct SplitHandle
        {
            public SplitHandle(SplitNode node, DockSplitOrientation orientation, Rectangle bounds)
            {
                Node = node;
                Orientation = orientation;
                Bounds = bounds;
            }

            public SplitNode Node { get; }
            public DockSplitOrientation Orientation { get; }
            public Rectangle Bounds { get; }
        }

        private readonly struct CornerHandle
        {
            public CornerHandle(SplitHandle verticalHandle, SplitHandle horizontalHandle, Rectangle bounds)
            {
                VerticalHandle = verticalHandle;
                HorizontalHandle = horizontalHandle;
                Bounds = bounds;
            }

            public SplitHandle VerticalHandle { get; }
            public SplitHandle HorizontalHandle { get; }
            public Rectangle Bounds { get; }
        }

        private struct DockDropPreview
        {
            public DockPanel TargetPanel;
            public DockEdge Edge;
            public Rectangle HighlightBounds;
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
