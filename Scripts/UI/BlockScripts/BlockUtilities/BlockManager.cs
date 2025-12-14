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
        private const string GameBlockKey = "game";
        private const string BlankBlockKey = "blank";
        private const string TransparentBlockKey = "transparent";
        private const string ColorSchemeBlockKey = "colors";
        private const string ControlsBlockKey = "controls";
        private const string NotesBlockKey = "notes";
        private const string BackendBlockKey = "backend";
        private const string SpecsBlockKey = "specs";
        private const string BlockMenuControlKey = "BlockMenu";
        private const int DragBarButtonPadding = 8;
        private const int DragBarButtonSpacing = 6;
        private const int WindowEdgeSnapDistance = 30;

        private static bool _dockingModeEnabled = true;
        private static bool _blockDefinitionsReady;
        private static bool _renderingDockedFrame;
        private const int CornerSnapDistance = 16;
        private static readonly Dictionary<string, DockBlock> _blocks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BlockNode> _blockNodes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DockBlock> _orderedBlocks = [];
        private static readonly Dictionary<string, bool> _blockLockStates = new(StringComparer.OrdinalIgnoreCase);
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
        private static DockBlock _draggingBlock;
        private static Rectangle _draggingStartBounds;
        private static Point _dragOffset;
        private static DockDropPreview? _dropPreview;
        private static bool _overlayMenuVisible;
        private static bool _blockMenuSwitchState;
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
        private static CornerHandle? _activeCornerLinkedHandle;
        private static CornerHandle? _activeCornerSnapTarget;
        private static bool _isPropagatingResize;
        private static Point? _activeCornerSnapPosition;
        private static Point? _activeCornerSnapAnchor;
        private static bool _activeCornerSnapLockX;
        private static bool _activeCornerSnapLockY;
        private static string _focusedBlockId;
        private static string _hoveredDragBarId;
        private static KeyboardState _previousKeyboardState;
        private static readonly List<BlockMenuEntry> _blockMenuEntries = [];
        private static readonly List<OverlayMenuRow> _overlayRows = [];
        private static BlockMenuEntry _activeNumericEntry;
        private static Dictionary<string, Rectangle> _resizeStartBlockBounds;

        public static bool IsBlockLocked(DockBlockKind blockKind)
        {
            DockBlock block = _orderedBlocks.FirstOrDefault(p => p.Kind == blockKind);
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
                    EnsureBlocks();
                    MarkLayoutDirty();
                }
            }
        }

        public static void OnGraphicsReady()
        {
            EnsureBlocks();
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

            EnsureBlocks();
            EnsureFocusedBlockValid();
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

            bool blockMenuState = rebindOverlayOpen ? false : GetBlockMenuState();
            if (blockMenuState != _blockMenuSwitchState)
            {
                _blockMenuSwitchState = blockMenuState;
                _overlayMenuVisible = blockMenuState;
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
                // Evaluate corners first so clicks on intersections start a dual-axis drag instead of being swallowed by a single edge.
                bool resizingBlocks = allowReorder && (UpdateCornerResizeState(leftClickStarted, leftClickHeld, leftClickReleased) ||
                    UpdateResizeBarState(leftClickStarted, leftClickHeld, leftClickReleased));
                if (!resizingBlocks)
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

            _blocks.Clear();
            _blockNodes.Clear();
            _orderedBlocks.Clear();
            ClearDockingInteractions();

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

            _rootNode = BuildDefaultLayout();

            _blockDefinitionsReady = true;
            MarkLayoutDirty();
        }

        private static void EnsureBlockMenuEntries()
        {
            if (_blockMenuEntries.Count > 0)
            {
                return;
            }

            _blockMenuEntries.Add(new BlockMenuEntry(BlankBlockKey, BlankBlock.BlockTitle, DockBlockKind.Blank, BlockMenuControlMode.Count, 0, 10, 1));
            _blockMenuEntries.Add(new BlockMenuEntry(TransparentBlockKey, TransparentBlock.BlockTitle, DockBlockKind.Transparent, BlockMenuControlMode.Count, 0, 10, 1));
            _blockMenuEntries.Add(new BlockMenuEntry(GameBlockKey, GameBlock.BlockTitle, DockBlockKind.Game, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ColorSchemeBlockKey, ColorSchemeBlock.BlockTitle, DockBlockKind.ColorScheme, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(ControlsBlockKey, ControlsBlock.BlockTitle, DockBlockKind.Controls, BlockMenuControlMode.Toggle, initialVisible: true));
            _blockMenuEntries.Add(new BlockMenuEntry(NotesBlockKey, NotesBlock.BlockTitle, DockBlockKind.Notes, BlockMenuControlMode.Toggle, initialVisible: true));
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

        private static DockNode BuildDefaultLayout()
        {
            List<BlockNode> blankNodes = GetBlockNodesByKind(DockBlockKind.Blank);
            List<BlockNode> transparentNodes = GetBlockNodesByKind(DockBlockKind.Transparent);
            BlockNode gameNode = GetBlockNodesByKind(DockBlockKind.Game).FirstOrDefault();
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
            DockNode rightColumn = CombineNodes(controlsAndNotes, paletteBackend, DockSplitOrientation.Horizontal, 0.68f);

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

        private static DockBlock CreateBlock(string id, string title, DockBlockKind kind)
        {
            DockBlock block = new(id, title, kind);
            (int minWidth, int minHeight) = GetBlockMinimumSize(kind);
            block.MinWidth = Math.Max(0, minWidth);
            block.MinHeight = Math.Max(0, minHeight);
            _blocks[id] = block;
            _orderedBlocks.Add(block);
            _blockNodes[id] = new BlockNode(block);
            EnsureBlockLockState(block);
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
                DockBlockKind.ColorScheme => (ColorSchemeBlock.MinWidth, ColorSchemeBlock.MinHeight),
                DockBlockKind.Controls => (ControlsBlock.MinWidth, ControlsBlock.MinHeight),
                DockBlockKind.Notes => (NotesBlock.MinWidth, NotesBlock.MinHeight),
                DockBlockKind.Backend => (BackendBlock.MinWidth, BackendBlock.MinHeight),
                DockBlockKind.Specs => (SpecsBlock.MinWidth, SpecsBlock.MinHeight),
                _ => (defaultMin, defaultMin)
            };

            // Keep at least the drag bar height so we can detect when a header is being pushed;
            // overflow gets propagated to neighboring resize edges instead of hard-clamping.
            int clampedWidth = Math.Max(width, UIStyle.MinBlockSize);
            int clampedHeight = Math.Max(height, UIStyle.DragBarHeight);
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

        private static bool UpdateResizeBarState(bool leftClickStarted, bool leftClickHeld, bool leftClickReleased)
        {
            if (!AnyBlockVisible() || _resizeBars.Count == 0)
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
                    LogResizeBlockDeltas();
                    _activeResizeBar = null;
                    ClearResizeBarSnap();
                    return false;
                }

                ApplyResizeBarDrag(_activeResizeBar.Value, _mousePosition);

                // If we're snapped to another handle, move that handle in lockstep so both edges nudge together.
                if (_activeResizeBarSnapTarget.HasValue && _activeResizeBarSnapCoordinate.HasValue)
                {
                    ResizeBar snapTarget = _activeResizeBarSnapTarget.Value;
                    Point snappedPosition = snapTarget.Orientation == DockSplitOrientation.Vertical
                        ? new Point(_activeResizeBarSnapCoordinate.Value, _mousePosition.Y)
                        : new Point(_mousePosition.X, _activeResizeBarSnapCoordinate.Value);
                    ApplyResizeBarDrag(snapTarget, snappedPosition);
                }

                return true;
            }

            ResizeBar? hovered = HitTestResizeBar(_mousePosition);
            _hoveredResizeBar = hovered;

            if (hovered.HasValue && leftClickStarted)
            {
                DebugLogger.PrintUI($"[ResizeBarStart] {DescribeResizeBar(hovered.Value)} Mouse={_mousePosition}");
                CaptureBlockBoundsForResize();
                _activeResizeBar = hovered;
                ClearResizeBarSnap();
                ApplyResizeBarDrag(hovered.Value, _mousePosition);
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

        private static void UpdateDragState(bool leftClickStarted, bool leftClickReleased, bool allowReorder)
        {
            _hoveredDragBarId = null;

            if (!AnyBlockVisible())
            {
                _draggingBlock = null;
                _dropPreview = null;
                return;
            }

            if (leftClickStarted && TryToggleBlockLock(_mousePosition))
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

            DockBlock dragBarHover = HitTestDragBarBlock(_mousePosition, excludeHeaderButtons: true);
            _hoveredDragBarId = dragBarHover?.Id;

            DockBlock dragBarHit = null;
            if (leftClickStarted)
            {
                dragBarHit = HitTestDragBarBlock(_mousePosition);
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
                DockBlock block = dragBarHit ?? HitTestDragBarBlock(_mousePosition);
                if (block != null)
                {
                    _draggingBlock = block;
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
                    _dropPreview = null;
                }
            }
        }

        private static bool TryToggleBlockLock(Point position)
        {
            DockBlock lockHit = HitTestLockButton(position);
            if (lockHit == null)
            {
                return false;
            }

            ToggleBlockLock(lockHit);
            ClearDockingInteractions();
            return true;
        }

        private static void HandleBlockClose(DockBlock block)
        {
            if (block == null)
            {
                return;
            }

            block.IsVisible = false;

            if (TryDecrementCountedBlock(block))
            {
                return;
            }

            MarkLayoutDirty();
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

            if (_blockNodes.TryGetValue(block.Id, out BlockNode node))
            {
                _rootNode = DockLayout.Detach(_rootNode, node);
                _blockNodes.Remove(block.Id);
            }

            _blocks.Remove(block.Id);
            _orderedBlocks.Remove(block);
            _blockLockStates.Remove(block.Id);
            if (string.Equals(_hoveredDragBarId, block.Id, StringComparison.OrdinalIgnoreCase))
            {
                _hoveredDragBarId = null;
            }
            MarkLayoutDirty();
        }

        private static bool IsBlockLocked(DockBlock block)
        {
            if (block == null || !IsLockToggleAvailable(block))
            {
                return false;
            }

            EnsureBlockLockState(block);
            _blockLockStates.TryGetValue(block.Id, out bool locked);

            return locked;
        }

        private static void ToggleBlockLock(DockBlock block)
        {
            if (block == null)
            {
                return;
            }

            bool nextState = !IsBlockLocked(block);
            _blockLockStates[block.Id] = nextState;
            BlockDataStore.SetBlockLock(block.Kind, nextState);
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

        private static DockBlock HitTestDragBarBlock(Point position, bool excludeHeaderButtons = false)
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

                if (excludeHeaderButtons && IsPointOnHeaderButton(block, dragBarHeight, position))
                {
                    continue;
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

        private static DockBlock HitTestLockButton(Point position)
        {
            int dragBarHeight = GetActiveDragBarHeight();
            if (dragBarHeight <= 0)
            {
                return null;
            }

            foreach (DockBlock block in _orderedBlocks)
            {
                if (!block.IsVisible || !IsLockToggleAvailable(block))
                {
                    continue;
                }

                Rectangle lockBounds = GetLockButtonBounds(block, dragBarHeight);
                if (lockBounds != Rectangle.Empty && lockBounds.Contains(position))
                {
                    return block;
                }
            }

            return null;
        }

        private static bool IsPointOnHeaderButton(DockBlock block, int dragBarHeight, Point position)
        {
            GetHeaderButtonBounds(block, dragBarHeight, out Rectangle lockBounds, out Rectangle closeBounds);
            return (lockBounds != Rectangle.Empty && lockBounds.Contains(position)) ||
                (closeBounds != Rectangle.Empty && closeBounds.Contains(position));
        }

        private static Rectangle GetCloseButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetHeaderButtonBounds(block, dragBarHeight, out _, out Rectangle closeBounds);
            return closeBounds;
        }

        private static Rectangle GetLockButtonBounds(DockBlock block, int dragBarHeight)
        {
            GetHeaderButtonBounds(block, dragBarHeight, out Rectangle lockBounds, out _);
            return lockBounds;
        }

        private static void GetHeaderButtonBounds(DockBlock block, int dragBarHeight, out Rectangle lockBounds, out Rectangle closeBounds)
        {
            lockBounds = Rectangle.Empty;
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

            if (!IsLockToggleAvailable(block) || closeBounds == Rectangle.Empty)
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

        private static bool IsLockToggleAvailable(DockBlock block)
        {
            if (block == null)
            {
                return false;
            }

            return block.Kind == DockBlockKind.Controls ||
                block.Kind == DockBlockKind.ColorScheme ||
                block.Kind == DockBlockKind.Backend ||
                block.Kind == DockBlockKind.Specs;
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
            // When handles overlap (e.g., nested splits near each other), pick the one whose center
            // is closest to the cursor along the resize axis, breaking ties by depth so nested layouts
            // still feel precise without stealing drags meant for a parent boundary.
            ResizeBar? best = null;
            int bestDepth = -1;
            int bestDistance = int.MaxValue;

            foreach (ResizeBar handle in _resizeBars)
            {
                Rectangle hitBounds = handle.Bounds;
                hitBounds.Inflate(2, 2);
                if (!hitBounds.Contains(position))
                {
                    continue;
                }

                int axisCenter = GetResizeBarAxisCenter(handle);
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

        private static void ApplyResizeBarDrag(ResizeBar handle, Point position)
        {
            if (handle.Node == null)
            {
                return;
            }

            // Avoid propagating loops when we nudge neighboring handles.
            if (_isPropagatingResize)
            {
                position = GetResizeBarPosition(handle, position);
                ApplyResizeBarDragInternal(handle, position, allowPropagation: false);
                return;
            }

            position = GetResizeBarPosition(handle, position);
            ApplyResizeBarDragInternal(handle, position, allowPropagation: true);
        }

        private static void ApplyResizeBarDragInternal(ResizeBar handle, Point position, bool allowPropagation)
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
            ResizeBar? snapPartner = _activeResizeBarSnapTarget.HasValue && _activeResizeBarSnapTarget.Value.Orientation == handle.Orientation
                ? _activeResizeBarSnapTarget
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
                DebugLogger.PrintUI($"[ResizeBarDrag] Ori={handle.Orientation} Node={DescribeNode(handle.Node)} Prev={previousRatio:F3} New={newRatio:F3} RelativePos={(handle.Orientation == DockSplitOrientation.Vertical ? position.X - bounds.X : position.Y - bounds.Y)} Bounds={bounds} Mouse={position} First={firstDesc} Second={secondDesc}");
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
                    NudgeNearestResizeBar(handle, overflow, negativeDirection: true, snapPartner);
                }
                else if (axisPositionRaw > maxClamp)
                {
                    int overflow = axisPositionRaw - maxClamp;
                    NudgeNearestResizeBar(handle, overflow, negativeDirection: false, snapPartner);
                }
            }
        }

        private static void NudgeNearestResizeBar(ResizeBar source, int amount, bool negativeDirection, ResizeBar? preferred = null)
        {
            if (amount <= 0)
            {
                return;
            }

            ResizeBar? best = preferred;
            int bestDistance = int.MaxValue;

            foreach (ResizeBar other in _resizeBars)
            {
                if (ResizeBarsEqual(other, source) || other.Orientation != source.Orientation)
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

            ResizeBar target = best.Value;
            int signedAmount = negativeDirection ? -amount : amount;
            Point targetPosition = target.Orientation == DockSplitOrientation.Vertical
                ? new Point(target.Bounds.Center.X + signedAmount, target.Bounds.Center.Y)
                : new Point(target.Bounds.Center.X, target.Bounds.Center.Y + signedAmount);

            _isPropagatingResize = true;
            try
            {
                ApplyResizeBarDragInternal(target, targetPosition, allowPropagation: false);
            }
            finally
            {
                _isPropagatingResize = false;
            }
        }

        private static void ApplyCornerHandleDrag(CornerHandle corner, Point position)
        {
            Point snapped = GetCornerDragPosition(corner, position);
            ApplyResizeBarDrag(corner.VerticalHandle, snapped);
            ApplyResizeBarDrag(corner.HorizontalHandle, snapped);

            // If two corners started the drag already snapped together, move the paired corner in lockstep.
            if (_activeCornerHandle.HasValue && CornerEquals(corner, _activeCornerHandle.Value) && _activeCornerLinkedHandle.HasValue)
            {
                ApplyResizeBarDrag(_activeCornerLinkedHandle.Value.VerticalHandle, snapped);
                ApplyResizeBarDrag(_activeCornerLinkedHandle.Value.HorizontalHandle, snapped);
            }
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

        private static void ApplyDrop(DockDropPreview preview)
        {
            if (_draggingBlock == null)
            {
                return;
            }

            if (preview.IsViewportSnap)
            {
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
                DrawResizeBars(spriteBatch);
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

        private static void DrawBlockBackground(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight)
        {
            bool isTransparentBlock = block.Kind == DockBlockKind.Transparent;
            DrawRect(spriteBatch, block.Bounds, isTransparentBlock ? Core.TransparentWindowColor : UIStyle.BlockBackground);
            DrawRectOutline(spriteBatch, block.Bounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            if (dragBarHeight <= 0)
            {
                return;
            }

            Rectangle dragBar = block.GetDragBarBounds(dragBarHeight);
            if (dragBar.Height <= 0)
            {
                return;
            }

            DrawRect(spriteBatch, dragBar, UIStyle.HeaderBackground);
            bool dragBarHovered = string.Equals(_hoveredDragBarId, block.Id, StringComparison.Ordinal);
            GetHeaderButtonBounds(block, dragBarHeight, out Rectangle lockButtonBounds, out Rectangle closeButtonBounds);

            if (dragBarHovered)
            {
                DrawRect(spriteBatch, dragBar, UIStyle.DragBarHoverTint);
            }

            UIStyle.UIFont headerFont = UIStyle.FontH2;
            if (headerFont.IsAvailable)
            {
                Vector2 textSize = headerFont.MeasureString(block.Title);
                float textY = dragBar.Bottom - textSize.Y + 3f;
                Vector2 textPosition = new Vector2(dragBar.X + 12, textY);
                headerFont.DrawString(spriteBatch, block.Title, textPosition, UIStyle.TextColor);
            }

            if (lockButtonBounds != Rectangle.Empty)
            {
                bool blockLocked = IsBlockLocked(block);
                bool hovered = lockButtonBounds.Contains(_mousePosition);
                DrawLockToggleButton(spriteBatch, lockButtonBounds, blockLocked, hovered);
            }

            if (closeButtonBounds != Rectangle.Empty)
            {
                bool hovered = closeButtonBounds.Contains(_mousePosition);
                DrawBlockCloseButton(spriteBatch, closeButtonBounds, hovered);
            }
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

        private static void DrawLockToggleButton(SpriteBatch spriteBatch, Rectangle bounds, bool isLocked, bool hovered)
        {
            Color background = isLocked
                ? (hovered ? ColorPalette.LockLockedHoverFill : ColorPalette.LockLockedFill)
                : (hovered ? ColorPalette.LockUnlockedHoverFill : ColorPalette.LockUnlockedFill);
            Color border = isLocked ? (hovered ? UIStyle.AccentColor : UIStyle.BlockBorder) : UIStyle.AccentColor;
            DrawRect(spriteBatch, bounds, background);
            DrawRectOutline(spriteBatch, bounds, border, UIStyle.BlockBorderThickness);

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
                int headerHeight = Math.Min(dragBarHeight, floating.Height);
                Rectangle header = new(floating.X, floating.Y, floating.Width, headerHeight);
                DrawRect(spriteBatch, header, UIStyle.HeaderBackground * 0.95f);
                DrawRectOutline(spriteBatch, header, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
            }

            DrawRectOutline(spriteBatch, floating, UIStyle.AccentColor * 0.8f, UIStyle.DragOutlineThickness);
        }

        private static void DrawBlockContent(SpriteBatch spriteBatch, DockBlock block, int dragBarHeight)
        {
            Rectangle contentBounds = block.GetContentBounds(dragBarHeight, UIStyle.BlockPadding);

            switch (block.Kind)
            {
                case DockBlockKind.Game:
                    GameBlock.Draw(spriteBatch, contentBounds, _worldRenderTarget);
                    break;
                case DockBlockKind.Transparent:
                    TransparentBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Blank:
                    BlankBlock.Draw(spriteBatch, contentBounds);
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
                case DockBlockKind.Backend:
                    BackendBlock.Draw(spriteBatch, contentBounds);
                    break;
                case DockBlockKind.Specs:
                    SpecsBlock.Draw(spriteBatch, contentBounds);
                    break;
            }
        }

        private static void UpdateInteractiveBlocks(GameTime gameTime, MouseState mouseState, MouseState previousMouseState, KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            if (gameTime == null || _orderedBlocks.Count == 0)
            {
                return;
            }

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

                Rectangle contentBounds = block.GetContentBounds(dragBarHeight, UIStyle.BlockPadding);
                switch (block.Kind)
                {
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
            _draggingBlock = null;
            _dropPreview = null;
            _hoveredDragBarId = null;
            _hoveredResizeBar = null;
            _activeResizeBar = null;
            ClearResizeBarSnap();
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
            }

            MarkLayoutDirty();
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
            foreach (BlockMenuEntry entry in _blockMenuEntries)
            {
                entry.IsEditing = false;
                if (entry.ControlMode == BlockMenuControlMode.Count)
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

            BlockMenuEntry entry = _activeNumericEntry;
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
                RebuildBlocksFromMenuChange();
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
            if (TryGetGameBlock(out DockBlock gameBlock) && gameBlock.IsVisible)
            {
                _gameContentBounds = gameBlock.GetContentBounds(dragBarHeight, UIStyle.BlockPadding);
            }

            _layoutDirty = false;
        }

        private static void MarkLayoutDirty()
        {
            _layoutDirty = true;
        }

        private static void RebuildResizeBars()
        {
            if (_rootNode == null || !AnyBlockVisible())
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

        private static bool CornerContainsResizeBar(CornerHandle corner, ResizeBar handle)
        {
            return ReferenceEquals(corner.VerticalHandle.Node, handle.Node) ||
                   ReferenceEquals(corner.HorizontalHandle.Node, handle.Node);
        }

        private static bool ResizeBarsEqual(ResizeBar a, ResizeBar b)
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
            block = _orderedBlocks.FirstOrDefault(p => p.Kind == kind);
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
            public DockBlock TargetBlock;
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
