using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io
{
    public static class BlockManager
    {
        private const int LayoutPadding = 24;
        private const int PanelPadding = 12;
        private const int HeaderHeight = 32;
        private const int MinPanelSize = 120;
        private const float DropEdgeThreshold = 0.28f;
        private const string PanelHotkeyLabel = "Shift + X";
        private const string GamePanelKey = "game";
        private const string BlankPanelKey = "blank";
        private const string SettingsPanelKey = "settings";

        private static readonly Color ScreenBackground = new(18, 18, 18);
        private static readonly Color PanelBackground = new(26, 26, 26);
        private static readonly Color PanelBorder = new(48, 48, 48);
        private static readonly Color HeaderBackground = new(35, 35, 35);
        private static readonly Color TextColor = new(226, 226, 226);
        private static readonly Color MutedTextColor = new(160, 160, 160);
        private static readonly Color AccentColor = new(110, 142, 255);
        private static readonly Color AccentMuted = new(110, 142, 255, 70);
        private static readonly Color OverlayBackground = new(24, 24, 24, 230);

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
        private static SpriteFont _uiFont;
        private static readonly List<KeybindDisplayRow> _keybindCache = [];
        private static bool _keybindCacheLoaded;
        private static KeyboardState _previousKeyboardState;
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
        private static readonly StringBuilder _stringBuilder = new();

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
                _previousKeyboardState = Keyboard.GetState();
                _previousMouseState = Mouse.GetState();
                return;
            }

            EnsurePanels();
            UpdateLayoutCache();
            EnsureSurfaceResources(Core.Instance.GraphicsDevice);

            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            _mousePosition = mouseState.Position;

            bool comboPressed = IsShiftDown(keyboardState) && keyboardState.IsKeyDown(Keys.X);
            bool comboPreviouslyPressed = IsShiftDown(_previousKeyboardState) && _previousKeyboardState.IsKeyDown(Keys.X);

            if (comboPressed && !comboPreviouslyPressed)
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

            if (_overlayMenuVisible)
            {
                UpdateOverlayInteractions(leftClickStarted);
                _draggingPanel = null;
                _dropPreview = null;
            }
            else
            {
                UpdateDragState(leftClickStarted, leftClickReleased);
            }

            _previousKeyboardState = keyboardState;
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
            DrawRect(spriteBatch, viewport, ScreenBackground);

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

            if (_uiFont == null && Core.Instance?.Content != null)
            {
                try
                {
                    _uiFont = Core.Instance.Content.Load<SpriteFont>("Fonts/Minimal");
                }
                catch (ContentLoadException ex)
                {
                    DebugLogger.PrintError($"Failed to load docking UI font: {ex.Message}");
                }
            }
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

                Rectangle headerRect = panel.GetHeaderBounds(HeaderHeight);
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
                if (relativeY <= DropEdgeThreshold)
                {
                    edge = DockEdge.Top;
                }
                else if (relativeY >= 1f - DropEdgeThreshold)
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

            if (_draggingPanel != null)
            {
                Rectangle floating = new(_mousePosition.X - _dragOffset.X, _mousePosition.Y - _dragOffset.Y, _draggingStartBounds.Width, _draggingStartBounds.Height);
                DrawRect(spriteBatch, floating, PanelBackground * 0.75f);
                DrawRectOutline(spriteBatch, floating, AccentColor * 0.8f, 2);
            }

            if (_dropPreview.HasValue)
            {
                DrawRect(spriteBatch, _dropPreview.Value.HighlightBounds, AccentMuted);
                DrawRectOutline(spriteBatch, _dropPreview.Value.HighlightBounds, AccentColor, 2);
            }
        }

        private static void DrawPanelBackground(SpriteBatch spriteBatch, DockPanel panel)
        {
            DrawRect(spriteBatch, panel.Bounds, PanelBackground);
            DrawRectOutline(spriteBatch, panel.Bounds, PanelBorder, 1);
            Rectangle header = panel.GetHeaderBounds(HeaderHeight);
            DrawRect(spriteBatch, header, HeaderBackground);
            if (_uiFont != null)
            {
                spriteBatch.DrawString(_uiFont, panel.Title, new Vector2(header.X + 12, header.Y + 6), TextColor);
            }
        }

        private static void DrawPanelContent(SpriteBatch spriteBatch, DockPanel panel)
        {
            Rectangle contentBounds = panel.GetContentBounds(HeaderHeight, PanelPadding);

            switch (panel.Kind)
            {
                case DockPanelKind.Game:
                    if (_worldRenderTarget != null)
                    {
                        spriteBatch.Draw(_worldRenderTarget, contentBounds, Color.White);
                    }
                    break;
                case DockPanelKind.Blank:
                    DrawBlankPanel(spriteBatch, contentBounds);
                    break;
                case DockPanelKind.Settings:
                    DrawSettingsPanel(spriteBatch, contentBounds);
                    break;
            }
        }

        private static void DrawBlankPanel(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (_uiFont == null)
            {
                return;
            }

            const string label = "Empty block";
            Vector2 size = _uiFont.MeasureString(label);
            Vector2 position = new(contentBounds.X + (contentBounds.Width - size.X) / 2f, contentBounds.Y + (contentBounds.Height - size.Y) / 2f);
            spriteBatch.DrawString(_uiFont, label, position, MutedTextColor);
        }

        private static void DrawSettingsPanel(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (_uiFont == null)
            {
                return;
            }

            EnsureKeybindCache();

            float lineHeight = _uiFont.LineSpacing + 2f;
            float y = contentBounds.Y;
            foreach (KeybindDisplayRow row in _keybindCache)
            {
                if (y + lineHeight > contentBounds.Bottom)
                {
                    break;
                }

                _stringBuilder.Clear();
                _stringBuilder.Append(row.Action);
                _stringBuilder.Append(": ");
                _stringBuilder.Append(row.Input);
                _stringBuilder.Append(" [");
                _stringBuilder.Append(row.Type);
                _stringBuilder.Append(']');

                spriteBatch.DrawString(_uiFont, _stringBuilder, new Vector2(contentBounds.X, y), TextColor);
                y += lineHeight;
            }
        }

        private static void DrawOverlayMenu(SpriteBatch spriteBatch)
        {
            if (!_overlayMenuVisible || _uiFont == null)
            {
                return;
            }

            if (_overlayBounds == Rectangle.Empty)
            {
                BuildOverlayLayout();
            }

            DrawRect(spriteBatch, _overlayBounds, OverlayBackground);
            DrawRectOutline(spriteBatch, _overlayBounds, PanelBorder, 1);

            string header = "Panel visibility";
            Vector2 headerSize = _uiFont.MeasureString(header);
            Vector2 headerPosition = new(_overlayBounds.X + (_overlayBounds.Width - headerSize.X) / 2f, _overlayBounds.Y + 12);
            spriteBatch.DrawString(_uiFont, header, headerPosition, TextColor);

            int rowY = _overlayBounds.Y + 52;
            foreach (OverlayPanelToggle toggle in _overlayToggles)
            {
                string label = toggle.Panel.Title;
                spriteBatch.DrawString(_uiFont, label, new Vector2(_overlayBounds.X + 20, rowY), TextColor);
                string state = toggle.Panel.IsVisible ? "Hide" : "Show";
                DrawButton(spriteBatch, toggle.Bounds, state, toggle.Panel.IsVisible ? AccentColor : PanelBorder);
                rowY += 32;
            }

            DrawButton(spriteBatch, _overlayOpenAllBounds, "Open all", AccentColor);
            DrawButton(spriteBatch, _overlayCloseAllBounds, "Close all", PanelBorder);
        }

        private static void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Color border)
        {
            DrawRect(spriteBatch, bounds, PanelBackground);
            DrawRectOutline(spriteBatch, bounds, border, 1);
            if (_uiFont != null)
            {
                Vector2 textSize = _uiFont.MeasureString(label);
                Vector2 textPosition = new(bounds.X + (bounds.Width - textSize.X) / 2f, bounds.Y + (bounds.Height - textSize.Y) / 2f);
                spriteBatch.DrawString(_uiFont, label, textPosition, TextColor);
            }
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
            if (_uiFont == null)
            {
                return;
            }

            string message = $"Press {PanelHotkeyLabel} to open panels";
            Vector2 size = _uiFont.MeasureString(message);
            Vector2 position = new(viewport.X + (viewport.Width - size.X) / 2f, viewport.Y + (viewport.Height - size.Y) / 2f);
            spriteBatch.DrawString(_uiFont, message, position, MutedTextColor);
        }

        private static Rectangle GetLayoutBounds(Rectangle viewport)
        {
            return new Rectangle(
                viewport.X + LayoutPadding,
                viewport.Y + LayoutPadding,
                Math.Max(0, viewport.Width - (LayoutPadding * 2)),
                Math.Max(0, viewport.Height - (LayoutPadding * 2)));
        }

        private static bool AnyPanelVisible() => _orderedPanels.Any(panel => panel.IsVisible);

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
                return;
            }

            GraphicsDevice graphicsDevice = Core.Instance?.GraphicsDevice;
            if (graphicsDevice == null)
            {
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
            _rootNode?.Arrange(_layoutBounds, MinPanelSize);

            _gameContentBounds = Rectangle.Empty;
            if (TryGetGamePanel(out DockPanel gamePanel) && gamePanel.IsVisible)
            {
                _gameContentBounds = gamePanel.GetContentBounds(HeaderHeight, PanelPadding);
            }

            _layoutDirty = false;
        }

        private static void MarkLayoutDirty()
        {
            _layoutDirty = true;
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

        private static void EnsureKeybindCache()
        {
            if (_keybindCacheLoaded)
            {
                return;
            }

            try
            {
                _keybindCache.Clear();
                var rows = DatabaseQuery.ExecuteQuery("SELECT SettingKey, InputKey, InputType FROM ControlKey ORDER BY SettingKey;");
                foreach (var row in rows)
                {
                    _keybindCache.Add(new KeybindDisplayRow
                    {
                        Action = row.TryGetValue("SettingKey", out object action) ? action?.ToString() ?? "Action" : "Action",
                        Input = row.TryGetValue("InputKey", out object key) ? key?.ToString() ?? "Key" : "Key",
                        Type = row.TryGetValue("InputType", out object type) ? type?.ToString() ?? string.Empty : string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"Failed to load keybinds for settings panel: {ex.Message}");
            }
            finally
            {
                _keybindCacheLoaded = true;
            }
        }

        private static bool IsShiftDown(KeyboardState state) =>
            state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift);

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

        private struct KeybindDisplayRow
        {
            public string Action;
            public string Input;
            public string Type;
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

        private struct DockDropPreview
        {
            public DockPanel TargetPanel;
            public DockEdge Edge;
            public Rectangle HighlightBounds;
        }
    }
}
