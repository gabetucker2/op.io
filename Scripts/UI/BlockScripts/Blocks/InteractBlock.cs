using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io.UI.BlockScripts.Blocks
{
    /// <summary>
    /// The Interact block displays Dynamic block content triggered by game-world
    /// stimuli such as ZoneBlock proximity. When no Dynamic content is active it
    /// shows a placeholder message.
    /// </summary>
    internal static class InteractBlock
    {
        public const string BlockTitle = "Interact";

        // ── Active Dynamic content ──────────────────────────────────────────────
        private static string _activeDynamicKey;
        /// <summary>Key of the currently displayed Dynamic content (null = none).</summary>
        public static string ActiveDynamicKey => _activeDynamicKey;

        /// <summary>Whether any Dynamic content is currently displayed.</summary>
        public static bool HasActiveContent => !string.IsNullOrEmpty(_activeDynamicKey);

        // ── Player preview camera ───────────────────────────────────────────────
        private static Vector2 _previewCameraOffset = Vector2.Zero;
        private static Vector2? _previewPanAnchor;
        private static Vector2 _previewPanAnchorOffset;
        private static bool _previewDragArmed;
        private const float PreviewSnapRange = 60f;
        private static Rectangle _lastContentBounds;
        private static Texture2D _pixelTexture;

        // ── Barrel extrusion ────────────────────────────────────────────────────
        private const float BarrelGap = 10f;
        private const float BulletSampleGap = 6f;

        // ── Selection state ─────────────────────────────────────────────────────
        private enum SelectedPart { None, Body, Barrel }
        private static SelectedPart _selectedPart = SelectedPart.None;
        private static int _selectedBarrelIndex = -1;

        // ── Attributes panel ────────────────────────────────────────────────────
        private static bool _showHidden;
        private static bool _showHiddenLoaded;
        private const string ShowHiddenRowKey = "ShowHidden";
        private static readonly BlockScrollPanel _attrScrollPanel = new();
        private static MouseState _lastMouseState;

        private const int AttrPadding = 8;
        private const int AttrRowSpacing = 3;
        private const int AttrHeaderSpacing = 6;
        private const int AttrButtonHeight = 28;
        private const int AttrMinPanelWidth = 180;
        private const int AttrSectionSpacing = 6;
        private const int PanelGap = 6;
        private const int CloseButtonSize = 22;
        private const int CloseButtonMargin = 4;
        private const int AttrRowLabelMinWidth = 110;
        private static readonly Color ConnectorLineColor = new(110, 142, 255, 200);
        private const int ConnectorLineThickness = 1;

        // Stored during DrawPlayerPreview so the connector line can be drawn afterward.
        private static Vector2 _selectedPartDrawCenter;
        private static bool _selectedPartDrawCenterValid;

        // ── Attribute row hover (for tooltip system) ────────────────────────
        private static string _hoveredAttrRowKey;
        private static string _hoveredAttrRowLabel;
        public static string GetHoveredRowKey()   => _hoveredAttrRowKey;
        public static string GetHoveredRowLabel() => _hoveredAttrRowLabel;

        // ── Rename state ────────────────────────────────────────────────────
        private static bool _renaming;
        private static readonly System.Text.StringBuilder _renameBuffer = new();
        private static readonly KeyRepeatTracker _renameRepeater = new();
        private const string RenameFocusOwner = "InteractRename";
        private const int RenameButtonWidth = 60;
        private const int ResetButtonWidth = 50;
        private const int HeaderButtonHeight = 20;
        private const int HeaderButtonGap = 4;

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the ZoneBlock detection system to activate a specific Dynamic
        /// content view inside the Interact block.
        /// </summary>
        public static void SetActiveDynamicContent(string dynamicKey)
        {
            if (string.Equals(_activeDynamicKey, dynamicKey, StringComparison.OrdinalIgnoreCase))
                return;

            _activeDynamicKey = dynamicKey;
            _previewCameraOffset = Vector2.Zero;
            _previewPanAnchor = null;
            _previewDragArmed = false;
            ClearSelection();
        }

        /// <summary>Clears the active Dynamic content (player left the zone).</summary>
        public static void ClearActiveDynamicContent()
        {
            _activeDynamicKey = null;
            _previewCameraOffset = Vector2.Zero;
            _previewPanAnchor = null;
            _previewDragArmed = false;
            ClearSelection();
        }

        private static void ClearSelection()
        {
            _selectedPart = SelectedPart.None;
            _selectedBarrelIndex = -1;
            _attrScrollPanel.Reset();
            CancelRename();
        }

        private static void CancelRename()
        {
            _renaming = false;
            _renameBuffer.Clear();
            _renameRepeater.Reset();
            FocusModeManager.SetFocusActive(RenameFocusOwner, false);
        }

        // ── Show/Hide hidden (single per-block, persisted in SQL) ────────────

        private static bool GetShowHidden()
        {
            if (!_showHiddenLoaded)
            {
                _showHiddenLoaded = true;
                var data = BlockDataStore.LoadRowData(DockBlockKind.Interact);
                if (data.TryGetValue(ShowHiddenRowKey, out string stored))
                    _showHidden = string.Equals(stored, "true", StringComparison.OrdinalIgnoreCase);
                else
                    _showHidden = ControlStateManager.GetSwitchState(ControlKeyMigrations.ShowHiddenAttrsKey);
            }
            return _showHidden;
        }

        private static void SetShowHidden(bool value)
        {
            _showHidden = value;
            _showHiddenLoaded = true;
            BlockDataStore.SetRowData(DockBlockKind.Interact, ShowHiddenRowKey, value ? "true" : "false");
        }

        // ── Update ──────────────────────────────────────────────────────────────

        public static void Update(GameTime gameTime, Rectangle contentBounds,
            MouseState mouseState, MouseState previousMouseState,
            KeyboardState keyboardState, KeyboardState previousKeyboardState)
        {
            _lastContentBounds = contentBounds;
            _lastMouseState = mouseState;
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Interact);

            if (blockLocked || !HasActiveContent)
                return;

            if (!string.Equals(_activeDynamicKey, "PlayerPreview", StringComparison.OrdinalIgnoreCase))
                return;

            Agent player = Core.Instance?.Player;

            // Validate barrel selection still in range
            if (_selectedPart == SelectedPart.Barrel &&
                (player == null || _selectedBarrelIndex < 0 || _selectedBarrelIndex >= player.BarrelCount))
                ClearSelection();

            bool panelVisible = _selectedPart != SelectedPart.None;
            ComputeLayout(contentBounds, panelVisible, out Rectangle previewBounds, out Rectangle panelBounds);

            // ── Rename keyboard input ──────────────────────────────────────────
            if (_renaming && !blockLocked)
            {
                double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
                bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
                foreach (Keys key in _renameRepeater.GetKeysWithRepeat(keyboardState, previousKeyboardState, elapsed))
                {
                    bool newPress = !previousKeyboardState.IsKeyDown(key);
                    switch (key)
                    {
                        case Keys.Enter when newPress:
                            CommitRename(player);
                            break;
                        case Keys.Escape when newPress:
                            CancelRename();
                            break;
                        case Keys.Back:
                            if (_renameBuffer.Length > 0)
                                _renameBuffer.Remove(_renameBuffer.Length - 1, 1);
                            break;
                        default:
                            if (TryConvertKey(key, shift, out char ch))
                                _renameBuffer.Append(ch);
                            break;
                    }
                }
                FocusModeManager.SetFocusActive(RenameFocusOwner, true);
            }
            else
            {
                _renameRepeater.Reset();
                FocusModeManager.SetFocusActive(RenameFocusOwner, false);
            }

            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed;

            bool clickHandled = false;

            if (!blockLocked && leftClickReleased && player?.Shape != null)
            {
                // Check close (X) button in attributes panel header
                if (panelVisible && panelBounds.Width > 0)
                {
                    Rectangle closeBounds = GetCloseButtonBounds(panelBounds);
                    if (closeBounds.Contains(mouseState.Position))
                    {
                        ClearSelection();
                        clickHandled = true;
                    }
                }

                // Check rename button
                if (!clickHandled && panelVisible && panelBounds.Width > 0)
                {
                    Rectangle renameBounds = GetRenameButtonBounds(panelBounds);
                    if (renameBounds.Contains(mouseState.Position))
                    {
                        if (_renaming)
                        {
                            CommitRename(player);
                        }
                        else
                        {
                            _renaming = true;
                            _renameBuffer.Clear();
                            string current = GetCurrentPartName(player);
                            _renameBuffer.Append(current);
                        }
                        clickHandled = true;
                    }
                }

                // Check reset button (only visible when renaming)
                if (!clickHandled && _renaming && panelVisible && panelBounds.Width > 0)
                {
                    Rectangle resetBounds = GetResetButtonBounds(panelBounds);
                    if (resetBounds.Contains(mouseState.Position))
                    {
                        ResetNameToDefault(player);
                        CancelRename();
                        clickHandled = true;
                    }
                }

                // Check hidden toggle button in attributes panel
                if (!clickHandled && panelVisible && panelBounds.Width > 0)
                {
                    Rectangle buttonBounds = GetAttrButtonBounds(panelBounds);
                    if (buttonBounds.Contains(mouseState.Position))
                    {
                        SetShowHidden(!GetShowHidden());
                        clickHandled = true;
                    }
                }

                // Hit test preview area for body / barrel selection
                if (!clickHandled && previewBounds != Rectangle.Empty &&
                    previewBounds.Contains(mouseState.Position))
                {
                    Vector2 clickPos = new(mouseState.Position.X, mouseState.Position.Y);
                    Vector2 blockCenter = new(
                        previewBounds.X + previewBounds.Width / 2f,
                        previewBounds.Y + previewBounds.Height / 2f);
                    Vector2 drawCenter = blockCenter - _previewCameraOffset;
                    float playerRadius = Math.Max(player.Shape.Width, player.Shape.Height) / 2f;

                    int hitBarrel = HitTestBarrels(clickPos, drawCenter, playerRadius, player);

                    if (hitBarrel >= 0)
                    {
                        if (_selectedPart == SelectedPart.Barrel && _selectedBarrelIndex == hitBarrel)
                            ClearSelection();
                        else
                        {
                            _selectedPart = SelectedPart.Barrel;
                            _selectedBarrelIndex = hitBarrel;
                            _attrScrollPanel.Reset();
                            CancelRename();
                        }
                        clickHandled = true;
                    }
                    else if (PointInCircle(clickPos, drawCenter, playerRadius))
                    {
                        if (_selectedPart == SelectedPart.Body)
                            ClearSelection();
                        else
                        {
                            _selectedPart = SelectedPart.Body;
                            _selectedBarrelIndex = -1;
                            _attrScrollPanel.Reset();
                            CancelRename();
                        }
                        clickHandled = true;
                    }
                    else
                    {
                        // Clicked empty space in preview — deselect
                        if (_selectedPart != SelectedPart.None)
                            ClearSelection();
                        clickHandled = true;
                    }
                }
            }

            // Recompute layout if selection state changed
            bool nowPanelVisible = _selectedPart != SelectedPart.None;
            if (nowPanelVisible != panelVisible)
                ComputeLayout(contentBounds, nowPanelVisible, out previewBounds, out panelBounds);

            // Update attributes scroll panel
            if (_selectedPart != SelectedPart.None && player != null && panelBounds.Width > 0)
            {
                Rectangle scrollViewport = GetAttrScrollViewport(panelBounds);
                float contentHeight = CalculateAttrContentHeight(player);
                _attrScrollPanel.Update(scrollViewport, contentHeight,
                    BlockManager.GetScrollMouseState(blockLocked, mouseState, previousMouseState),
                    previousMouseState);
            }

            // Camera drag constrained to preview area
            Rectangle dragBounds = previewBounds != Rectangle.Empty ? previewBounds : contentBounds;
            UpdatePreviewCameraDrag(dragBounds, mouseState, previousMouseState);
        }

        // ── Draw ────────────────────────────────────────────────────────────────

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null) return;
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0) return;

            EnsurePixelTexture();

            if (!HasActiveContent)
            {
                DrawPlaceholder(spriteBatch, contentBounds);
                return;
            }

            if (string.Equals(_activeDynamicKey, "PlayerPreview", StringComparison.OrdinalIgnoreCase))
            {
                bool panelVisible = _selectedPart != SelectedPart.None;
                ComputeLayout(contentBounds, panelVisible, out Rectangle previewBounds, out Rectangle panelBounds);

                if (previewBounds != Rectangle.Empty)
                    DrawPlayerPreview(spriteBatch, previewBounds);

                if (panelVisible && panelBounds.Width > 0)
                {
                    DrawAttributesPanel(spriteBatch, panelBounds);

                    // Dogleg connector from selected part to the title in the attributes panel
                    if (_selectedPartDrawCenterValid)
                    {
                        Vector2 from = _selectedPartDrawCenter;
                        float titleY = panelBounds.Y + AttrPadding + (UIStyle.FontHBody.IsAvailable
                            ? UIStyle.FontHBody.LineHeight / 2f : 12f);
                        Vector2 to = new(panelBounds.X, titleY);
                        DrawDogleg(spriteBatch, from, to, ConnectorLineColor, ConnectorLineThickness);
                    }
                }
            }
        }

        // ── Layout ──────────────────────────────────────────────────────────────

        private static void ComputeLayout(Rectangle contentBounds, bool panelVisible,
            out Rectangle previewBounds, out Rectangle panelBounds)
        {
            if (!panelVisible)
            {
                previewBounds = contentBounds;
                panelBounds = Rectangle.Empty;
                return;
            }

            int panelWidth = Math.Max(AttrMinPanelWidth, (int)(contentBounds.Width * 0.45f));
            int previewWidth = contentBounds.Width - panelWidth - PanelGap;

            if (previewWidth < 120)
            {
                previewWidth = 120;
                panelWidth = contentBounds.Width - previewWidth - PanelGap;
            }

            if (panelWidth < 100)
            {
                // Too narrow for both — panel takes everything
                previewBounds = Rectangle.Empty;
                panelBounds = contentBounds;
                return;
            }

            previewBounds = new Rectangle(contentBounds.X, contentBounds.Y,
                previewWidth, contentBounds.Height);
            panelBounds = new Rectangle(contentBounds.X + previewWidth + PanelGap, contentBounds.Y,
                panelWidth, contentBounds.Height);
        }

        private static Rectangle GetAttrButtonBounds(Rectangle panelBounds)
        {
            return new Rectangle(
                panelBounds.X + AttrPadding,
                panelBounds.Bottom - AttrButtonHeight - AttrPadding,
                Math.Max(0, panelBounds.Width - AttrPadding * 2),
                AttrButtonHeight);
        }

        private static Rectangle GetCloseButtonBounds(Rectangle panelBounds)
        {
            return new Rectangle(
                panelBounds.Right - CloseButtonSize - CloseButtonMargin,
                panelBounds.Y + CloseButtonMargin,
                CloseButtonSize,
                CloseButtonSize);
        }

        private static Rectangle GetAttrScrollViewport(Rectangle panelBounds)
        {
            float headerH = GetAttrHeaderHeight();
            return new Rectangle(
                panelBounds.X,
                panelBounds.Y + (int)headerH,
                panelBounds.Width,
                Math.Max(0, panelBounds.Height - (int)headerH - AttrButtonHeight - AttrPadding * 2));
        }

        private static float GetAttrHeaderHeight()
        {
            UIStyle.UIFont heading = UIStyle.FontHBody;
            if (!heading.IsAvailable) return 24f;
            return heading.LineHeight + AttrHeaderSpacing + AttrPadding;
        }

        // ── Hit testing ─────────────────────────────────────────────────────────

        private static bool PointInCircle(Vector2 point, Vector2 center, float radius)
        {
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool PointInRotatedRect(Vector2 point, Vector2 center,
            float length, float width, float angle)
        {
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            float cos = MathF.Cos(-angle);
            float sin = MathF.Sin(-angle);
            float localX = dx * cos - dy * sin;
            float localY = dx * sin + dy * cos;
            return MathF.Abs(localX) <= length / 2f && MathF.Abs(localY) <= width / 2f;
        }

        private static int HitTestBarrels(Vector2 clickPos, Vector2 drawCenter,
            float playerRadius, Agent player)
        {
            int barrelCount = player.BarrelCount;
            if (barrelCount <= 0) return -1;

            float angleStep = barrelCount > 1 ? MathF.Tau / barrelCount : 0f;
            const float previewRotation = -MathHelper.PiOver2;

            for (int i = 0; i < barrelCount; i++)
            {
                var slot = player.Barrels[i];
                if (slot.FullShape == null) continue;

                float barrelAngle = previewRotation + i * angleStep;
                float barrelLength = slot.FullShape.Width;
                float barrelWidth = slot.FullShape.Height;
                float halfLength = barrelLength / 2f;
                Vector2 dir = new(MathF.Cos(barrelAngle), MathF.Sin(barrelAngle));
                Vector2 barrelCenter = drawCenter + dir * (playerRadius + BarrelGap + halfLength);

                if (PointInRotatedRect(clickPos, barrelCenter, barrelLength, barrelWidth, barrelAngle))
                    return i;

                // Also hit-test the sample bullet at the barrel tip
                var attrs = slot.Attrs;
                float bulletMass = attrs.BulletMass >= 0
                    ? attrs.BulletMass : BulletManager.DefaultBulletMass;
                float bulletRadius = BulletManager.ComputeBulletRadius(bulletMass);
                float barrelTipDist = playerRadius + BarrelGap + barrelLength;
                Vector2 bulletCenter = drawCenter
                    + dir * (barrelTipDist + BulletSampleGap + bulletRadius);

                if (PointInCircle(clickPos, bulletCenter, bulletRadius))
                    return i;
            }

            return -1;
        }

        // ── Player preview drawing ──────────────────────────────────────────────

        private static void DrawPlayerPreview(SpriteBatch spriteBatch, Rectangle previewBounds)
        {
            _selectedPartDrawCenterValid = false;

            Agent player = Core.Instance?.Player;
            if (player?.Shape == null) return;

            Vector2 blockCenter = new(
                previewBounds.X + previewBounds.Width / 2f,
                previewBounds.Y + previewBounds.Height / 2f);

            Vector2 drawCenter = blockCenter - _previewCameraOffset;

            // ── Scissor clip ────────────────────────────────────────────────────
            var gd = spriteBatch.GraphicsDevice;
            float uiScale = BlockManager.UIScale;
            Rectangle scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(previewBounds.X * uiScale),
                    (int)(previewBounds.Y * uiScale),
                    (int)(previewBounds.Width * uiScale),
                    (int)(previewBounds.Height * uiScale))
                : previewBounds;
            var viewport = gd.Viewport;
            scissorRect.X = Math.Clamp(scissorRect.X, 0, viewport.Width);
            scissorRect.Y = Math.Clamp(scissorRect.Y, 0, viewport.Height);
            scissorRect.Width = Math.Clamp(scissorRect.Width, 0, viewport.Width - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, viewport.Height - scissorRect.Y);

            spriteBatch.End();
            var scissorState = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            gd.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            // ── Draw barrels extruded from the player circle ────────────────────
            int barrelCount = player.BarrelCount;
            if (barrelCount > 0)
            {
                float angleStep = barrelCount > 1 ? MathF.Tau / barrelCount : 0f;
                float playerRadius = Math.Max(player.Shape.Width, player.Shape.Height) / 2f;
                const float previewRotation = -MathHelper.PiOver2;

                for (int i = 0; i < barrelCount; i++)
                {
                    var slot = player.Barrels[i];
                    if (slot.FullShape == null) continue;
                    float barrelAngle = previewRotation + i * angleStep;

                    float barrelLength = slot.FullShape.Width;
                    float barrelWidth = slot.FullShape.Height;

                    float halfLength = barrelLength / 2f;
                    Vector2 dir = new(MathF.Cos(barrelAngle), MathF.Sin(barrelAngle));
                    Vector2 barrelCenter = drawCenter + dir * (playerRadius + BarrelGap + halfLength);

                    Color barrelColor = i == player.ActiveBarrelIndex
                        ? new Color(180, 220, 255, 220)
                        : new Color(120, 140, 160, 180);

                    DrawRotatedRect(spriteBatch, barrelCenter, barrelLength, barrelWidth,
                        barrelAngle, barrelColor);

                    // ── Sample bullet at the barrel tip ─────────────────────────
                    var attrs = slot.Attrs;
                    float bulletMass = attrs.BulletMass >= 0
                        ? attrs.BulletMass : BulletManager.DefaultBulletMass;
                    float bulletRadius = BulletManager.ComputeBulletRadius(bulletMass);

                    float barrelTipDist = playerRadius + BarrelGap + barrelLength;
                    Vector2 bulletCenter = drawCenter
                        + dir * (barrelTipDist + BulletSampleGap + bulletRadius);

                    Color bulletFill = attrs.BulletFillAlphaRaw >= 0
                        ? attrs.BulletFillColor : BulletManager.DefaultBulletFillColor;
                    Color bulletOutline = attrs.BulletOutlineAlphaRaw >= 0
                        ? attrs.BulletOutlineColor : BulletManager.DefaultBulletOutlineColor;
                    int bulletOutlineW = attrs.BulletOutlineWidth >= 0
                        ? attrs.BulletOutlineWidth : BulletManager.DefaultBulletOutlineWidth;

                    DrawFilledCircle(spriteBatch, bulletCenter, bulletRadius, bulletFill);
                    DrawCircleOutline(spriteBatch, bulletCenter, bulletRadius,
                        bulletOutline, bulletOutlineW);

                    // Track selected barrel center for connector line
                    if (_selectedPart == SelectedPart.Barrel && _selectedBarrelIndex == i)
                    {
                        _selectedPartDrawCenter = barrelCenter;
                        _selectedPartDrawCenterValid = true;
                    }
                }
            }

            // ── Draw the player circle ──────────────────────────────────────────
            float radius = Math.Max(player.Shape.Width, player.Shape.Height) / 2f;

            DrawFilledCircle(spriteBatch, drawCenter, radius, player.FillColor);
            DrawCircleOutline(spriteBatch, drawCenter, radius, player.OutlineColor, player.OutlineWidth);

            // Track selected body center for connector line
            if (_selectedPart == SelectedPart.Body)
            {
                _selectedPartDrawCenter = drawCenter;
                _selectedPartDrawCenterValid = true;
            }

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);
        }

        // ── Attributes panel drawing ────────────────────────────────────────────

        private static void DrawAttributesPanel(SpriteBatch spriteBatch, Rectangle panelBounds)
        {
            Agent player = Core.Instance?.Player;
            if (player == null) return;

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Interact);

            // Reset attribute row hover each frame.
            _hoveredAttrRowKey = null;
            _hoveredAttrRowLabel = null;

            // Panel background
            DrawRect(spriteBatch, panelBounds, ColorPalette.BlockBackground * 0.85f);
            DrawRectOutline(spriteBatch, panelBounds, UIStyle.BlockBorder, 1);

            UIStyle.UIFont heading = UIStyle.FontHBody;
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!heading.IsAvailable || !body.IsAvailable || !tech.IsAvailable) return;

            // ── Header ──────────────────────────────────────────────────────────
            float headerY = panelBounds.Y + AttrPadding;

            if (_renaming)
            {
                // Draw inline rename text field
                int fieldX = panelBounds.X + AttrPadding;
                int fieldW = GetRenameButtonBounds(panelBounds).X - fieldX - HeaderButtonGap;
                int fieldH = (int)heading.LineHeight;
                Rectangle fieldRect = new(fieldX, (int)headerY, Math.Max(20, fieldW), fieldH);
                DrawRect(spriteBatch, fieldRect, new Color(35, 35, 40));
                DrawRectOutline(spriteBatch, fieldRect, UIStyle.AccentColor, 1);

                string displayText = _renameBuffer.ToString();
                body.DrawString(spriteBatch, displayText,
                    new Vector2(fieldRect.X + 4, fieldRect.Y + (fieldRect.Height - body.LineHeight) / 2f),
                    UIStyle.TextColor);
            }
            else
            {
                string title;
                if (_selectedPart == SelectedPart.Body)
                {
                    string bodyName = player.UnitAttributes.Name;
                    title = string.IsNullOrWhiteSpace(bodyName) ? "Body" : bodyName;
                }
                else
                {
                    string barrelName = (_selectedBarrelIndex >= 0 && _selectedBarrelIndex < player.BarrelCount)
                        ? player.Barrels[_selectedBarrelIndex].Name : null;
                    title = string.IsNullOrWhiteSpace(barrelName)
                        ? $"Barrel {_selectedBarrelIndex + 1}"
                        : barrelName;
                }
                heading.DrawString(spriteBatch, title, new Vector2(panelBounds.X + AttrPadding, headerY),
                    UIStyle.TextColor);
            }

            // ── Close (X) button at top-right ──────────────────────────────────
            Rectangle closeBounds = GetCloseButtonBounds(panelBounds);
            bool closeHovered = closeBounds.Contains(_lastMouseState.Position);
            UIButtonRenderer.Draw(spriteBatch, closeBounds, "X",
                UIButtonRenderer.ButtonStyle.Grey, closeHovered, blockLocked);

            // ── Rename button ──────────────────────────────────────────────────
            Rectangle renameBounds = GetRenameButtonBounds(panelBounds);
            bool renameHovered = renameBounds.Contains(_lastMouseState.Position);
            string renameLabel = _renaming ? "Save" : "Rename";
            UIButtonRenderer.Draw(spriteBatch, renameBounds, renameLabel,
                _renaming ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                renameHovered, blockLocked);

            // ── Reset button (visible only while renaming) ──────────────────────
            if (_renaming)
            {
                Rectangle resetBounds = GetResetButtonBounds(panelBounds);
                bool resetHovered = resetBounds.Contains(_lastMouseState.Position);
                UIButtonRenderer.Draw(spriteBatch, resetBounds, "Reset",
                    UIButtonRenderer.ButtonStyle.Grey, resetHovered, blockLocked);
            }

            // ── Show/Hide Hidden button at bottom ───────────────────────────────
            Rectangle buttonBounds = GetAttrButtonBounds(panelBounds);
            bool showHidden = GetShowHidden();
            bool btnHovered = buttonBounds.Contains(_lastMouseState.Position);
            string btnLabel = showHidden ? "Hide Hidden" : "Show Hidden";
            UIButtonRenderer.Draw(spriteBatch, buttonBounds, btnLabel,
                showHidden ? UIButtonRenderer.ButtonStyle.Blue : UIButtonRenderer.ButtonStyle.Grey,
                btnHovered, blockLocked);

            // ── Scrollable content ──────────────────────────────────────────────
            Rectangle scrollViewport = GetAttrScrollViewport(panelBounds);
            if (scrollViewport.Height <= 0) return;

            // Scissor clip for scrollable rows
            var gd = spriteBatch.GraphicsDevice;
            float uiScale = BlockManager.UIScale;
            Rectangle scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(scrollViewport.X * uiScale),
                    (int)(scrollViewport.Y * uiScale),
                    (int)(scrollViewport.Width * uiScale),
                    (int)(scrollViewport.Height * uiScale))
                : scrollViewport;
            var viewport = gd.Viewport;
            scissorRect.X = Math.Clamp(scissorRect.X, 0, viewport.Width);
            scissorRect.Y = Math.Clamp(scissorRect.Y, 0, viewport.Height);
            scissorRect.Width = Math.Clamp(scissorRect.Width, 0, viewport.Width - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, viewport.Height - scissorRect.Y);

            spriteBatch.End();
            var scissorState = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            gd.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            float y = scrollViewport.Y - _attrScrollPanel.ScrollOffset;
            float x = panelBounds.X + AttrPadding;

            var sections = _selectedPart == SelectedPart.Body
                ? GetBodySections(player)
                : GetBarrelSections(player, _selectedBarrelIndex);

            bool firstSection = true;
            foreach (var (sectionTitle, rows) in sections)
            {
                if (!firstSection)
                    y += AttrSectionSpacing;
                firstSection = false;

                // Section header
                if (y + body.LineHeight >= scrollViewport.Y && y <= scrollViewport.Bottom)
                    body.DrawString(spriteBatch, sectionTitle, new Vector2(x, y), UIStyle.AccentColor);
                y += body.LineHeight + AttrRowSpacing;

                foreach (var row in rows)
                {
                    if (row.IsHidden && !showHidden) continue;

                    float rowH = tech.LineHeight + AttrRowSpacing;

                    if (y + rowH >= scrollViewport.Y && y <= scrollViewport.Bottom)
                    {
                        if (row.Kind == Properties.RowKind.Color)
                            DrawAttrColorRow(spriteBatch, tech, x, y, row.Label, row.Color,
                                row.Value, row.IsHidden);
                        else
                            DrawAttrRow(spriteBatch, tech, x, y, row.Label, row.Value, row.IsHidden);

                        // Track hover for tooltip system (uses same keys as PropertiesBlock).
                        if (!blockLocked && row.Kind == Properties.RowKind.Text)
                        {
                            Point mp = _lastMouseState.Position;
                            if (mp.X >= scrollViewport.X && mp.X <= scrollViewport.Right &&
                                mp.Y >= (int)y && mp.Y < (int)(y + rowH))
                            {
                                _hoveredAttrRowKey = row.IsHidden
                                    ? "props_attr:" + row.Label
                                    : "props_row:" + row.Label;
                                _hoveredAttrRowLabel = row.Label;
                            }
                        }
                    }

                    y += rowH;
                }
            }

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            // Scrollbar drawn on top (outside scissor)
            _attrScrollPanel.Draw(spriteBatch, blockLocked);
        }

        private static void DrawAttrRow(SpriteBatch spriteBatch, UIStyle.UIFont font,
            float x, float y, string label, string value, bool isHidden)
        {
            Color labelColor = isHidden ? UIStyle.MutedTextColor * 0.55f : UIStyle.MutedTextColor;
            Color valueColor = isHidden ? UIStyle.TextColor * 0.45f : UIStyle.TextColor;
            font.DrawString(spriteBatch, label, new Vector2(x, y), labelColor);

            float valueX = x + Math.Max(font.MeasureString(label).X + AttrPadding, AttrRowLabelMinWidth);
            font.DrawString(spriteBatch, value, new Vector2(valueX, y), valueColor);
        }

        private static void DrawAttrColorRow(SpriteBatch spriteBatch, UIStyle.UIFont font,
            float x, float y, string label, Color color, string hex, bool isHidden)
        {
            Color labelColor = isHidden ? UIStyle.MutedTextColor * 0.55f : UIStyle.MutedTextColor;
            font.DrawString(spriteBatch, label, new Vector2(x, y), labelColor);

            float labelW = Math.Max(font.MeasureString(label).X + AttrPadding, AttrRowLabelMinWidth);
            int swatchSize = (int)Math.Max(10, font.LineHeight - 6);
            Rectangle swatch = new((int)(x + labelW), (int)y + 2, swatchSize, swatchSize);
            if (_pixelTexture != null)
            {
                spriteBatch.Draw(_pixelTexture, swatch, color);
                DrawRectOutline(spriteBatch, swatch, UIStyle.BlockBorder, 1);
            }

            Color hexColor = isHidden ? UIStyle.TextColor * 0.45f : UIStyle.TextColor;
            font.DrawString(spriteBatch, hex, new Vector2(swatch.Right + 4, y), hexColor);
        }

        private static float CalculateAttrContentHeight(Agent player)
        {
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!body.IsAvailable || !tech.IsAvailable) return 0f;

            bool showHidden = GetShowHidden();
            float height = 0f;

            var sections = _selectedPart == SelectedPart.Body
                ? GetBodySections(player)
                : GetBarrelSections(player, _selectedBarrelIndex);

            bool firstSection = true;
            foreach (var (_, rows) in sections)
            {
                if (!firstSection)
                    height += AttrSectionSpacing;
                firstSection = false;

                height += body.LineHeight + AttrRowSpacing;
                foreach (var row in rows)
                {
                    if (row.IsHidden && !showHidden) continue;
                    height += tech.LineHeight + AttrRowSpacing;
                }
            }

            return height;
        }

        // ── Section / row generation ────────────────────────────────────────────

        private static List<(string title, List<Properties.Row> rows)> GetBodySections(Agent player)
        {
            var sections = new List<(string, List<Properties.Row>)>();

            // ── Body Transform ───────────────────────────────────────────────────
            var transform = new List<Properties.Row>
            {
                new("Position", $"{player.Position.X:0.0}, {player.Position.Y:0.0}"),
                new("Rotation", $"{MathHelper.ToDegrees(player.Rotation):0.0} deg")
            };
            if (player.Shape != null)
                transform.Add(new Properties.Row("Size", $"{player.Shape.Width} x {player.Shape.Height}"));

            string shapeText = player.Shape?.ShapeType ?? "-";
            if (player.Shape is { ShapeType: "Polygon", Sides: > 0 })
                shapeText = $"Polygon ({player.Shape.Sides} sides)";
            transform.Add(new Properties.Row("Shape", shapeText));
            transform.Add(new Properties.Row("Mass", $"{player.BodyAttributes.Mass:0.##}"));
            transform.Add(new Properties.Row("Fill", player.FillColor, ToHex(player.FillColor)));
            transform.Add(new Properties.Row("Outline", player.OutlineColor, ToHex(player.OutlineColor)));
            sections.Add(("Body Transform", transform));

            // ── Body Attributes ──────────────────────────────────────────────────
            Attributes_Body a = player.BodyAttributes;
            float control = a.Control > 0f ? a.Control : 1f;
            float kScale = CollisionResolver.KnockbackMassScale;

            var attrs = new List<Properties.Row>
            {
                new("Mass",              $"{a.Mass:0.##}"),
                new("Max Health",        $"{AttributeDerived.MaxHealth(a.Mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsMaxHealth),
                new("Health Regen",      $"{a.HealthRegen:0.##}"),
                new("Health Armor",      $"{a.HealthArmor:0.##}"),
                new("Dmg Regen Delay",   $"{a.HealthRegenDelay:0.##}"),
                new("Max Shield",        $"{a.MaxShield:0.##}"),
                new("Shield Regen",      $"{a.ShieldRegen:0.##}"),
                new("Shield Armor",      $"{a.ShieldArmor:0.##}"),
                new("Dmg Shield Delay",  $"{a.ShieldRegenDelay:0.##}"),
                new("Body Coll. Damage", $"{a.BodyCollisionDamage:0.##}"),
                new("Body Penetration",  $"{a.BodyPenetration:0.##}"),
                new("Body Knockback",    $"{AttributeDerived.BodyKnockback(a.Mass, kScale):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBodyKnockback),
                new("Coll. Dmg Resist",  $"{a.CollisionDamageResistance:0.##}"),
                new("Bullet Dmg Resist", $"{a.BulletDamageResistance:0.##}"),
                new("Speed",             $"{a.Speed:0.##}"),
                new("Control",           $"{a.Control:0.##}"),
                new("Rotation Speed",    $"{(float)(180.0 / AttributeDerived.RotationDelay(control)):0.##} deg/s",
                    isHidden: true, affectsList: AttributeDerived.AffectsRotationSpeed),
                new("Accel. Speed",      $"{(1f / AttributeDerived.AccelerationDelay(control)):0.##} /s",
                    isHidden: true, affectsList: AttributeDerived.AffectsAccelerationSpeed),
                new("Action Buff",       $"{a.BodyActionBuff:0.##}")
            };
            sections.Add(("Body Attributes", attrs));

            return sections;
        }

        private static List<(string title, List<Properties.Row> rows)> GetBarrelSections(
            Agent player, int barrelIndex)
        {
            var sections = new List<(string, List<Properties.Row>)>();

            if (barrelIndex < 0 || barrelIndex >= player.BarrelCount)
                return sections;

            var slot = player.Barrels[barrelIndex];
            Attributes_Barrel a = slot.Attrs;

            float mass   = a.BulletMass > 0f ? a.BulletMass : BulletManager.DefaultBulletMass;
            float radius = AttributeDerived.BulletRadius(mass, BulletManager.BulletRadiusScalar);
            float drag   = AttributeDerived.BulletDrag(radius, BulletManager.AirResistanceScalar,
                BulletManager.DefaultBulletDragFactor);

            // ── Barrel Attributes ────────────────────────────────────────────────
            float bulletKnockback = AttributeDerived.BulletKnockback(a.BulletPenetration, BulletManager.BulletKnockbackScalar);
            float bulletRecoil = AttributeDerived.BulletRecoil(mass, bulletKnockback, BulletManager.BulletRecoilScalar);

            var attrs = new List<Properties.Row>
            {
                new("Bullet Damage",      $"{a.BulletDamage:0.##}"),
                new("Bullet Penetration", $"{a.BulletPenetration:0.##}"),
                new("Bullet Knockback",   $"{bulletKnockback:0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletKnockback),
                new("Reload Speed",       $"{a.ReloadSpeed:0.##}"),
                new("Bullet Recoil",      $"{bulletRecoil:0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletRecoil),
                new("Bullet Health",      $"{AttributeDerived.BulletHealth(mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletHealth),
                new("Bullet Radius",      $"{radius:0.##} px",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletRadius),
                new("Bullet Mass",        $"{a.BulletMass:0.##}"),
                new("Bullet Speed",       $"{a.BulletSpeed:0.##}"),
                new("Bullet Lifespan",    $"{a.BulletMaxLifespan:0.##}"),
                new("Bullet Drag",        $"{drag:0.####}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletDrag),
                new("Bullet Health Regen",      $"{AttributeDerived.BulletHealthRegen(mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletHealthRegen),
                new("Bullet Dmg Regen Delay",   $"{AttributeDerived.BulletHealthRegenDelay(mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletHealthRegenDelay),
                new("Bullet Health Armor",      $"{AttributeDerived.BulletHealthArmor(mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletHealthArmor),
                new("Bullet Coll. Dmg Resist",  $"{AttributeDerived.BulletCollisionDamageResistance(mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletCollisionDamageResistance),
                new("Bullet Dmg Resist",        $"{AttributeDerived.BulletBarrelDamageResistance(mass):0.##}",
                    isHidden: true, affectsList: AttributeDerived.AffectsBulletDamageResistance),
                new("Bullet Control",           $"{a.BulletControl:0.##}")
            };
            sections.Add(("Barrel Attributes", attrs));

            // ── Barrel Transform ─────────────────────────────────────────────────
            float barrelWidth = AttributeDerived.BarrelWidth(mass, BulletManager.BulletRadiusScalar);
            float bulletSpeed = a.BulletSpeed > 0f ? a.BulletSpeed : BulletManager.DefaultBulletSpeed;
            float barrelHeight = AttributeDerived.BarrelHeight(bulletSpeed, BulletManager.BarrelHeightScalar);

            var transform = new List<Properties.Row>();
            if (slot.FullShape != null)
            {
                transform.Add(new Properties.Row("Fill",
                    slot.FullShape.FillColor, ToHex(slot.FullShape.FillColor)));
                transform.Add(new Properties.Row("Outline",
                    slot.FullShape.OutlineColor, ToHex(slot.FullShape.OutlineColor)));
            }
            transform.Add(new Properties.Row("Barrel Width", $"{barrelWidth:0.##} px",
                isHidden: true, affectsList: AttributeDerived.AffectsBarrelWidth));
            transform.Add(new Properties.Row("Barrel Height", $"{barrelHeight:0.##} px",
                isHidden: true, affectsList: AttributeDerived.AffectsBarrelHeight));
            sections.Add(("Barrel Transform", transform));

            return sections;
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        }

        // ── Camera drag (middle-mouse with snap-to-center) ──────────────────────

        private static void UpdatePreviewCameraDrag(Rectangle bounds,
            MouseState mouseState, MouseState previousMouseState)
        {
            bool middleJustPressed = mouseState.MiddleButton == ButtonState.Pressed
                && previousMouseState.MiddleButton == ButtonState.Released;
            bool middleHeld = mouseState.MiddleButton == ButtonState.Pressed;
            bool cursorInBlock = bounds.Contains(mouseState.Position);

            if (middleJustPressed && cursorInBlock)
            {
                _previewPanAnchor = new Vector2(mouseState.Position.X, mouseState.Position.Y);
                _previewPanAnchorOffset = _previewCameraOffset;
                _previewDragArmed = false;
            }
            else if (middleHeld && _previewPanAnchor.HasValue)
            {
                Vector2 currentPos = new(mouseState.Position.X, mouseState.Position.Y);
                Vector2 delta = currentPos - _previewPanAnchor.Value;
                Vector2 draggedOffset = _previewPanAnchorOffset - delta;

                float dist = draggedOffset.Length();
                if (!_previewDragArmed && dist > PreviewSnapRange)
                    _previewDragArmed = true;

                if (_previewDragArmed && dist <= PreviewSnapRange)
                    draggedOffset = Vector2.Zero;

                _previewCameraOffset = draggedOffset;
            }
            else if (!middleHeld && _previewPanAnchor.HasValue)
            {
                if (_previewDragArmed && _previewCameraOffset.Length() <= PreviewSnapRange)
                    _previewCameraOffset = Vector2.Zero;

                _previewPanAnchor = null;
            }
        }

        // ── Placeholder ─────────────────────────────────────────────────────────

        private static void DrawPlaceholder(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (!FontManager.TryGetBackendFonts(out _, out UIStyle.UIFont regularFont))
                return;

            string text = TextSpacingHelper.JoinWithWideSpacing("No", "active", "zone.");
            regularFont.DrawString(spriteBatch, text,
                new Vector2(contentBounds.X + 8, contentBounds.Y + 8), UIStyle.MutedTextColor);
        }

        // ── Drawing helpers ─────────────────────────────────────────────────────

        private static void EnsurePixelTexture()
        {
            if (_pixelTexture != null || Core.Instance?.GraphicsDevice == null)
                return;
            _pixelTexture = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        private static void DrawFilledCircle(SpriteBatch spriteBatch, Vector2 center,
            float radius, Color color)
        {
            if (_pixelTexture == null) return;

            int r = (int)MathF.Ceiling(radius);
            for (int y = -r; y <= r; y++)
            {
                float halfWidth = MathF.Sqrt(radius * radius - y * y);
                int x0 = (int)(center.X - halfWidth);
                int x1 = (int)(center.X + halfWidth);
                int py = (int)center.Y + y;
                if (x1 > x0)
                    spriteBatch.Draw(_pixelTexture, new Rectangle(x0, py, x1 - x0, 1), color);
            }
        }

        private static void DrawCircleOutline(SpriteBatch spriteBatch, Vector2 center,
            float radius, Color color, int thickness)
        {
            if (_pixelTexture == null || thickness <= 0) return;

            float outerR = radius + thickness / 2f;
            float innerR = Math.Max(0, radius - thickness / 2f);
            int r = (int)MathF.Ceiling(outerR);

            for (int y = -r; y <= r; y++)
            {
                float outerHalf = outerR * outerR - y * y;
                if (outerHalf < 0) continue;
                outerHalf = MathF.Sqrt(outerHalf);

                float innerHalf = innerR * innerR - y * y;
                innerHalf = innerHalf > 0 ? MathF.Sqrt(innerHalf) : 0;

                int py = (int)center.Y + y;

                int lx0 = (int)(center.X - outerHalf);
                int lx1 = (int)(center.X - innerHalf);
                if (lx1 > lx0)
                    spriteBatch.Draw(_pixelTexture, new Rectangle(lx0, py, lx1 - lx0, 1), color);

                int rx0 = (int)(center.X + innerHalf);
                int rx1 = (int)(center.X + outerHalf);
                if (rx1 > rx0)
                    spriteBatch.Draw(_pixelTexture, new Rectangle(rx0, py, rx1 - rx0, 1), color);
            }
        }

        private static void DrawRotatedRect(SpriteBatch spriteBatch, Vector2 center,
            float length, float width, float angle, Color color)
        {
            if (_pixelTexture == null) return;

            var origin = new Vector2(0.5f, 0.5f);
            var destRect = new Rectangle((int)center.X, (int)center.Y, (int)length, (int)width);
            spriteBatch.Draw(_pixelTexture, destRect, null, color, angle, origin,
                SpriteEffects.None, 0f);
        }

        private static void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0) return;
            spriteBatch.Draw(_pixelTexture, bounds, color);
        }

        private static void DrawRectOutline(SpriteBatch spriteBatch, Rectangle bounds,
            Color color, int thickness)
        {
            if (_pixelTexture == null || bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0)
                return;

            thickness = Math.Max(1, thickness);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
        }
        // ── Rename helpers ──────────────────────────────────────────────────

        private static Rectangle GetRenameButtonBounds(Rectangle panelBounds)
        {
            float headerY = panelBounds.Y + AttrPadding;
            float btnY = headerY;
            if (UIStyle.FontHBody.IsAvailable)
                btnY += (UIStyle.FontHBody.LineHeight - HeaderButtonHeight) / 2f;

            int btnX = panelBounds.Right - CloseButtonSize - CloseButtonMargin - HeaderButtonGap - RenameButtonWidth;
            if (_renaming)
                btnX -= HeaderButtonGap + ResetButtonWidth;
            return new Rectangle(btnX, (int)btnY, RenameButtonWidth, HeaderButtonHeight);
        }

        private static Rectangle GetResetButtonBounds(Rectangle panelBounds)
        {
            float headerY = panelBounds.Y + AttrPadding;
            float btnY = headerY;
            if (UIStyle.FontHBody.IsAvailable)
                btnY += (UIStyle.FontHBody.LineHeight - HeaderButtonHeight) / 2f;

            int btnX = panelBounds.Right - CloseButtonSize - CloseButtonMargin - HeaderButtonGap - ResetButtonWidth;
            return new Rectangle(btnX, (int)btnY, ResetButtonWidth, HeaderButtonHeight);
        }

        private static string GetCurrentPartName(Agent player)
        {
            if (_selectedPart == SelectedPart.Body)
                return player.UnitAttributes.Name ?? "Body";
            if (_selectedPart == SelectedPart.Barrel && _selectedBarrelIndex >= 0 && _selectedBarrelIndex < player.BarrelCount)
                return player.Barrels[_selectedBarrelIndex].Name ?? $"Barrel {_selectedBarrelIndex + 1}";
            return string.Empty;
        }

        private static string GetDefaultPartName(Agent player)
        {
            if (_selectedPart == SelectedPart.Body)
                return "Body";
            if (_selectedPart == SelectedPart.Barrel)
                return $"Barrel {_selectedBarrelIndex + 1}";
            return string.Empty;
        }

        private static void CommitRename(Agent player)
        {
            if (player == null) { CancelRename(); return; }
            string newName = _renameBuffer.ToString().Trim();
            if (string.IsNullOrEmpty(newName)) { CancelRename(); return; }

            if (_selectedPart == SelectedPart.Body)
            {
                var ua = player.UnitAttributes;
                ua.Name = newName;
                player.UnitAttributes = ua;
            }
            else if (_selectedPart == SelectedPart.Barrel && _selectedBarrelIndex >= 0 && _selectedBarrelIndex < player.BarrelCount)
            {
                player.Barrels[_selectedBarrelIndex].Name = newName;
            }
            CancelRename();
        }

        private static void ResetNameToDefault(Agent player)
        {
            if (player == null) return;
            if (_selectedPart == SelectedPart.Body)
            {
                var ua = player.UnitAttributes;
                ua.Name = "Body";
                player.UnitAttributes = ua;
            }
            else if (_selectedPart == SelectedPart.Barrel && _selectedBarrelIndex >= 0 && _selectedBarrelIndex < player.BarrelCount)
            {
                player.Barrels[_selectedBarrelIndex].Name = null;
            }
        }

        private static bool TryConvertKey(Keys key, bool shift, out char value)
        {
            value = '\0';
            if (key >= Keys.A && key <= Keys.Z)
            {
                char b = (char)('a' + (key - Keys.A));
                value = shift ? char.ToUpperInvariant(b) : b;
                return true;
            }
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int digit = key - Keys.D0;
                if (shift)
                    value = digit switch { 0 => ')', 1 => '!', 2 => '@', 3 => '#', 4 => '$', 5 => '%', 6 => '^', 7 => '&', 8 => '*', 9 => '(', _ => '\0' };
                else
                    value = (char)('0' + digit);
                return value != '\0';
            }
            value = key switch
            {
                Keys.Space => ' ',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemComma => shift ? '<' : ',',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                _ => '\0'
            };
            return value != '\0';
        }

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to,
            Color color, int thickness)
        {
            if (_pixelTexture == null || thickness <= 0) return;

            Vector2 delta = to - from;
            float length = delta.Length();
            if (length < 1f) return;

            float angle = MathF.Atan2(delta.Y, delta.X);
            var destRect = new Rectangle((int)from.X, (int)from.Y, (int)length, thickness);
            spriteBatch.Draw(_pixelTexture, destRect, null, color, angle,
                new Vector2(0f, 0.5f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draws a diagonal-first dogleg: a steep diagonal segment from <paramref name="from"/>
        /// followed by a horizontal segment into <paramref name="to"/>.
        /// The diagonal consumes the full Y delta with a limited X slope, and the
        /// remaining horizontal distance is drawn as a flat line into the target.
        /// </summary>
        internal static void DrawDogleg(SpriteBatch spriteBatch, Vector2 from, Vector2 to,
            Color color, int thickness, float slopeRatio = DoglegSlopeRatio)
        {
            if (_pixelTexture == null || thickness <= 0) return;

            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float absDx = MathF.Abs(dx);
            float absDy = MathF.Abs(dy);

            if (absDx < 1f && absDy < 1f) return;

            // Nearly horizontal — draw a straight line.
            if (absDy < 2f) { DrawLine(spriteBatch, from, to, color, thickness); return; }

            // The diagonal consumes all Y delta with limited X travel (steep slope).
            // Clamp X travel so there is always a horizontal segment remaining.
            float diagXBudget = absDy * slopeRatio;
            float maxDiagX = MathF.Max(0f, absDx - DoglegMinHorizontal);
            float diagX = MathF.Min(diagXBudget, maxDiagX) * MathF.Sign(dx);

            Vector2 knee = new(from.X + diagX, to.Y);
            DrawLine(spriteBatch, from, knee, color, thickness);
            DrawLine(spriteBatch, knee, to, color, thickness);
        }

        private const float DoglegSlopeRatio = 0.4f;
        private const float DoglegMinHorizontal = 8f;
    }
}
