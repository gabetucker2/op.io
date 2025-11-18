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
        private const string SettingsPanelKey = "settings";
        private const string PanelMenuControlKey = "PanelMenu";

        private static bool _dockingModeEnabled = true;
        private static bool _panelDefinitionsReady;
        private static bool _renderingDockedFrame;
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
        private static Rectangle _overlayBounds;
        private static Rectangle _overlayOpenAllBounds;
        private static Rectangle _overlayCloseAllBounds;
        private static readonly List<OverlayPanelToggle> _overlayToggles = [];
        private static readonly List<SplitHandle> _splitHandles = [];
        private static SplitHandle? _hoveredSplitHandle;
        private static SplitHandle? _activeSplitHandle;
        private static readonly List<CornerHandle> _cornerHandles = [];
        private static CornerHandle? _hoveredCornerHandle;
        private static CornerHandle? _activeCornerHandle;

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
                    _overlayMenuVisible = false;
                    _draggingPanel = null;
                    _dropPreview = null;
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
            if (!DockingModeEnabled || Core.Instance?.GraphicsDevice == null)
            {
                _previousMouseState = Mouse.GetState();
                return;
            }

            EnsurePanels();
            UpdateLayoutCache();
            EnsureSurfaceResources(Core.Instance.GraphicsDevice);

            MouseState mouseState = Mouse.GetState();
            _mousePosition = mouseState.Position;

            if (InputManager.IsInputActive(PanelMenuControlKey))
            {
                _overlayMenuVisible = !_overlayMenuVisible;
            }

            if (!_overlayMenuVisible)
            {
                _overlayBounds = Rectangle.Empty;
                _overlayToggles.Clear();
            }

            bool leftClickStarted = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool leftClickHeld = mouseState.LeftButton == ButtonState.Pressed;

            if (_overlayMenuVisible)
            {
                UpdateOverlayInteractions(leftClickStarted);
                _draggingPanel = null;
                _dropPreview = null;
                _hoveredSplitHandle = null;
                _activeSplitHandle = null;
                _hoveredCornerHandle = null;
                _activeCornerHandle = null;
            }
            else
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

            _previousMouseState = mouseState;
        }

        public static bool BeginDockedFrame(GraphicsDevice graphicsDevice)
        {
            if (!DockingModeEnabled)
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
            if (!DockingModeEnabled || Core.Instance?.GraphicsDevice == null)
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
            DockPanel settings = CreatePanel(SettingsPanelKey, "Keybind Settings", DockPanelKind.Settings);

            PanelNode blankNode = _panelNodes[blank.Id];
            PanelNode gameNode = _panelNodes[game.Id];
            PanelNode settingsNode = _panelNodes[settings.Id];

            SplitNode leftColumn = new(DockSplitOrientation.Horizontal)
            {
                SplitRatio = 0.36f,
                First = blankNode,
                Second = gameNode
            };

            _rootNode = new SplitNode(DockSplitOrientation.Vertical)
            {
                SplitRatio = 0.67f,
                First = leftColumn,
                Second = settingsNode
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
                }

                return false;
            }

            if (_activeSplitHandle.HasValue)
            {
                if (!leftClickHeld || leftClickReleased)
                {
                    _activeSplitHandle = null;
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
                }

                return false;
            }

            if (_activeCornerHandle.HasValue)
            {
                if (!leftClickHeld || leftClickReleased)
                {
                    _activeCornerHandle = null;
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

            if (_draggingPanel == null && leftClickStarted)
            {
                DockPanel panel = HitTestHeader(_mousePosition);
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
            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                Rectangle headerRect = panel.GetHeaderBounds(UIStyle.HeaderHeight);
                if (headerRect.Contains(position))
                {
                    return panel;
                }
            }

            return null;
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
            ApplySplitHandleDrag(corner.VerticalHandle, position);
            ApplySplitHandleDrag(corner.HorizontalHandle, position);
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
            foreach (DockPanel panel in _orderedPanels)
            {
                if (!panel.IsVisible)
                {
                    continue;
                }

                DrawPanelBackground(spriteBatch, panel);
                DrawPanelContent(spriteBatch, panel);
            }

            DrawSplitHandles(spriteBatch);
            DrawCornerHandles(spriteBatch);

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

        private static void DrawPanelBackground(SpriteBatch spriteBatch, DockPanel panel)
        {
            DrawRect(spriteBatch, panel.Bounds, UIStyle.PanelBackground);
            DrawRectOutline(spriteBatch, panel.Bounds, UIStyle.PanelBorder, UIStyle.PanelBorderThickness);
            Rectangle header = panel.GetHeaderBounds(UIStyle.HeaderHeight);
            DrawRect(spriteBatch, header, UIStyle.HeaderBackground);

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            if (headerFont.IsAvailable)
            {
                Vector2 textSize = headerFont.MeasureString(panel.Title);
                float textY = header.Y + (header.Height - textSize.Y) / 2f;
                Vector2 textPosition = new(header.X + 12, textY);
                headerFont.DrawString(spriteBatch, panel.Title, textPosition, UIStyle.TextColor);
            }
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
                if (_activeCornerHandle.HasValue && corner.Equals(_activeCornerHandle.Value))
                {
                    color = UIStyle.SplitHandleActiveColor;
                }
                else if (_hoveredCornerHandle.HasValue && corner.Equals(_hoveredCornerHandle.Value))
                {
                    color = UIStyle.SplitHandleHoverColor;
                }

                DrawRect(spriteBatch, corner.Bounds, color);
            }
        }

        private static void DrawPanelContent(SpriteBatch spriteBatch, DockPanel panel)
        {
            Rectangle contentBounds = panel.GetContentBounds(UIStyle.HeaderHeight, UIStyle.PanelPadding);

            switch (panel.Kind)
            {
                case DockPanelKind.Game:
                    GameBlock.Draw(spriteBatch, contentBounds, _worldRenderTarget);
                    break;
                case DockPanelKind.Blank:
                    BlankBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Settings:
                    SettingsBlock.Draw(spriteBatch, contentBounds);
                    break;
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

        private static void UpdateOverlayInteractions(bool leftClickStarted)
        {
            BuildOverlayLayout();

            if (!leftClickStarted)
            {
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

        private static void BuildOverlayLayout()
        {
            Rectangle viewport = Core.Instance?.GraphicsDevice?.Viewport.Bounds ?? new Rectangle(0, 0, 1280, 720);
            int width = 360;
            int height = 120 + (_orderedPanels.Count * 32);
            _overlayBounds = new Rectangle(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);

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
            UIStyle.UIFont font = UIStyle.FontH3;
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

        private static bool AnyPanelVisible() => _orderedPanels.Any(panel => panel.IsVisible);

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
            if (!DockingModeEnabled)
            {
                ClearSplitHandles();
                return;
            }

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
                return;
            }

            _layoutBounds = GetLayoutBounds(viewport);
            _rootNode?.Arrange(_layoutBounds, UIStyle.MinPanelSize);
            RebuildSplitHandles();

            _gameContentBounds = Rectangle.Empty;
            if (TryGetGamePanel(out DockPanel gamePanel) && gamePanel.IsVisible)
            {
                _gameContentBounds = gamePanel.GetContentBounds(UIStyle.HeaderHeight, UIStyle.PanelPadding);
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

            if (_hoveredCornerHandle.HasValue)
            {
                _hoveredCornerHandle = FindCornerHandle(_hoveredCornerHandle.Value);
            }

            if (_activeCornerHandle.HasValue)
            {
                _activeCornerHandle = FindCornerHandle(_activeCornerHandle.Value);
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
                if (ReferenceEquals(handle.VerticalHandle.Node, corner.VerticalHandle.Node) &&
                    ReferenceEquals(handle.HorizontalHandle.Node, corner.HorizontalHandle.Node))
                {
                    return handle;
                }
            }

            return null;
        }

        private static bool CornerContainsHandle(CornerHandle corner, SplitHandle handle)
        {
            return ReferenceEquals(corner.VerticalHandle.Node, handle.Node) ||
                   ReferenceEquals(corner.HorizontalHandle.Node, handle.Node);
        }

        private static void ClearSplitHandles()
        {
            _splitHandles.Clear();
            _hoveredSplitHandle = null;
            _activeSplitHandle = null;
            _cornerHandles.Clear();
            _hoveredCornerHandle = null;
            _activeCornerHandle = null;
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

        public static Vector2 ToGameSpace(Point windowPosition)
        {
            int x = Math.Max(0, windowPosition.X);
            int y = Math.Max(0, windowPosition.Y);

            if (!DockingModeEnabled || _worldRenderTarget == null || _gameContentBounds.Width <= 0 || _gameContentBounds.Height <= 0)
            {
                return new Vector2(x, y);
            }

            float relativeX = (x - _gameContentBounds.X) / (float)_gameContentBounds.Width;
            float relativeY = (y - _gameContentBounds.Y) / (float)_gameContentBounds.Height;

            relativeX = MathHelper.Clamp(relativeX, 0f, 1f);
            relativeY = MathHelper.Clamp(relativeY, 0f, 1f);

            return new Vector2(relativeX * _worldRenderTarget.Width, relativeY * _worldRenderTarget.Height);
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
    }
}
