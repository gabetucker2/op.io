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
        public const int MinWidth = 280;
        public const int MinHeight = 240;

        private const int PreviewMaxSize = 220;
        private const int Padding = 10;
        private const int ButtonHeight = 32;
        private const int HeaderSpacing = 6;
        private const int RowSpacing = 4;
        private const int LockButtonSize = 44;
        private const int LockButtonPadding = 8;
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

        private readonly struct PropertiesLayout
        {
            public PropertiesLayout(Rectangle modeLabel, Rectangle previewBounds, Rectangle detailsBounds, Rectangle lockButtonBounds, Rectangle infoArea)
            {
                ModeLabel = modeLabel;
                PreviewBounds = previewBounds;
                DetailsBounds = detailsBounds;
                LockButtonBounds = lockButtonBounds;
                InfoArea = infoArea;
            }

            public Rectangle ModeLabel { get; }
            public Rectangle PreviewBounds { get; }
            public Rectangle DetailsBounds { get; }
            public Rectangle LockButtonBounds { get; }
            public Rectangle InfoArea { get; }
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            _lastMouseState = mouseState;
            PropertiesLayout layout = BuildLayout(contentBounds, ScrollPanel.ScrollOffset);
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed;

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
            }
            else if (!blockLocked && leftClickReleased && InspectModeState.InspectModeEnabled && cursorInGameBlock)
            {
                if (hovered != null)
                {
                    InspectModeState.LockHovered();
                }
                else
                {
                    InspectModeState.ClearLock();
                }
            }

            InspectableObjectInfo scrollTarget = InspectModeState.GetActiveTarget();
            InspectableObjectInfo scrollLocked = InspectModeState.GetLockedTarget();
            float totalContentHeight = CalculateTotalContentHeight(scrollTarget, scrollLocked, contentBounds);
            ScrollPanel.Update(contentBounds, totalContentHeight, mouseState, previousMouseState);
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            EnsureResources(spriteBatch.GraphicsDevice);
            float scroll = ScrollPanel.ScrollOffset;
            PropertiesLayout layout = BuildLayout(contentBounds, scroll);
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

            Rectangle clipBounds = ScrollPanel.ContentViewportBounds == Rectangle.Empty
                ? contentBounds
                : new Rectangle(contentBounds.X, contentBounds.Y, ScrollPanel.ContentViewportBounds.Width, contentBounds.Height);
            DrawDetails(spriteBatch, clipBounds, target, layout.InfoArea.Y);
            ScrollPanel.Draw(spriteBatch);
        }

        private static PropertiesLayout BuildLayout(Rectangle contentBounds, float scrollOffset = 0f)
        {
            int scrolledY = contentBounds.Y - (int)MathF.Round(scrollOffset);
            Rectangle modeLabel = new(contentBounds.X, scrolledY, contentBounds.Width, ButtonHeight);

            int contentTop = modeLabel.Bottom + Padding;

            // Preview is left-aligned at the top, above all text
            int previewSize = Math.Min(contentBounds.Width, PreviewMaxSize);
            Rectangle previewBounds = previewSize >= 80
                ? new Rectangle(contentBounds.X, contentTop, previewSize, previewSize)
                : Rectangle.Empty;

            // Info area and details are below the preview; bottom clips to the visible viewport
            int infoTop = previewBounds == Rectangle.Empty ? contentTop : previewBounds.Bottom + Padding;
            int infoHeight = Math.Max(0, contentBounds.Bottom - infoTop);
            Rectangle infoArea = new(contentBounds.X, infoTop, contentBounds.Width, infoHeight);
            Rectangle detailsBounds = infoArea;

            Rectangle lockButtonBounds = Rectangle.Empty;
            if (previewBounds != Rectangle.Empty)
            {
                int maxSize = Math.Min(LockButtonSize, Math.Min(previewBounds.Width, previewBounds.Height));
                int availableHeight = Math.Max(0, previewBounds.Height - (LockButtonPadding * 2));
                maxSize = Math.Min(maxSize, availableHeight);

                if (maxSize > 0)
                {
                    int x = Math.Max(previewBounds.Right - maxSize - LockButtonPadding, previewBounds.X);
                    int y = previewBounds.Y + LockButtonPadding;
                    lockButtonBounds = new Rectangle(x, y, maxSize, maxSize);
                }
            }

            return new PropertiesLayout(modeLabel, previewBounds, detailsBounds, lockButtonBounds, infoArea);
        }

        private static float CalculateTotalContentHeight(InspectableObjectInfo target, InspectableObjectInfo locked, Rectangle contentBounds)
        {
            float height = ButtonHeight + Padding;

            int previewSize = Math.Min(contentBounds.Width, PreviewMaxSize);
            if (previewSize >= 80)
                height += previewSize + Padding;

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
            {
                return;
            }

            DrawRect(spriteBatch, previewBounds, ColorPalette.BlockBackground * 0.9f);

            if (target.Shape != null)
            {
                float parentRotation = target.Source?.Rotation ?? target.Rotation;
                float cos = MathF.Cos(parentRotation);
                float sin = MathF.Sin(parentRotation);

                // Store shapes with their local-frame offsets (unrotated) for stable bbox/zoom,
                // and per-shape local rotation relative to parent.
                var shapes = new List<(Shape shape, Vector2 localOffset, float localRotation)>
                {
                    (target.Shape, Vector2.Zero, 0f)
                };
                if (target.Source != null)
                {
                    foreach (GameObject child in target.Source.Children)
                    {
                        if (child?.Shape == null) continue;
                        shapes.Add((child.Shape, child.Position, child.Rotation));
                    }
                }

                // Compute bbox using local (unrotated) offsets for a stable scale.
                // Extent is measured symmetrically from the parent center (local origin) so that
                // the parent body always maps to the center of the preview — child offsets cannot
                // push the parent body off-center.
                float minX = float.MaxValue, maxX = float.MinValue,
                      minY = float.MaxValue, maxY = float.MinValue;
                foreach (var (s, off, _) in shapes)
                {
                    float halfDiag = MathF.Sqrt(s.Width * s.Width + s.Height * s.Height) / 2f;
                    minX = Math.Min(minX, off.X - halfDiag);
                    maxX = Math.Max(maxX, off.X + halfDiag);
                    minY = Math.Min(minY, off.Y - halfDiag);
                    maxY = Math.Max(maxY, off.Y + halfDiag);
                }

                // Symmetric extents from the parent center ensure equal padding on both sides.
                float extentX = Math.Max(1f, Math.Max(Math.Abs(minX), Math.Abs(maxX)));
                float extentY = Math.Max(1f, Math.Max(Math.Abs(minY), Math.Abs(maxY)));

                float worldScale = Math.Min(
                    (previewBounds.Width / 2f - Padding) / extentX,
                    (previewBounds.Height / 2f - Padding) / extentY);
                worldScale = Math.Max(0.1f, worldScale);

                // Parent body center (local origin) always maps to the preview center.
                Vector2 worldOrigin = new(previewBounds.Center.X, previewBounds.Center.Y);

                // Draw children first (behind body), then body on top.
                // Apply parentRotation to each shape's offset and rotation for rendering.
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < shapes.Count; i++)
                    {
                        bool isBody = (i == 0);
                        if (isBody == (pass == 0)) continue; // pass 0 = children, pass 1 = body

                        var (s, localOff, localRot) = shapes[i];
                        Texture2D tex = GetPreviewTexture(spriteBatch.GraphicsDevice, s);
                        if (tex == null || tex.IsDisposed) continue;

                        Vector2 rotatedOff = new(
                            localOff.X * cos - localOff.Y * sin,
                            localOff.X * sin + localOff.Y * cos);

                        float texScale = worldScale * s.Width / (float)tex.Width;
                        texScale = Math.Max(0.1f, texScale);
                        Vector2 pos = worldOrigin + rotatedOff * worldScale;
                        Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
                        spriteBatch.Draw(tex, pos, null, Color.White, parentRotation + localRot, origin, texScale, SpriteEffects.None, 0f);
                    }
                }
            }

            DrawRectOutline(spriteBatch, previewBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
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
                float scale = Math.Min((bounds.Width - 8f) / (float)icon.Width, (bounds.Height - 8f) / (float)icon.Height);
                scale = Math.Max(0.1f, scale);
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

            bool firstSection = true;
            foreach (Properties.Section section in properties.Sections)
            {
                if (!firstSection && section.Depth <= 1)
                    height += SectionSpacing;
                firstSection = false;

                height += body.LineHeight + RowSpacing;
                foreach (Properties.Row row in section.Rows)
                    height += row.Kind == Properties.RowKind.BarGraph
                        ? BarRowHeight + RowSpacing
                        : row.LineCount * (tech.LineHeight + RowSpacing);
            }

            return height;
        }

        private static void DrawDetails(SpriteBatch spriteBatch, Rectangle clipBounds, InspectableObjectInfo target, float contentStartY)
        {
            UIStyle.UIFont heading = UIStyle.FontHBody;
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!heading.IsAvailable || !body.IsAvailable || !tech.IsAvailable)
                return;

            InspectableObjectInfo locked = InspectModeState.GetLockedTarget();
            Properties properties = new(target, locked);

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
                    if (y > clipBounds.Bottom) { stopped = true; break; }

                    float rowH = row.Kind == Properties.RowKind.BarGraph
                        ? BarRowHeight + RowSpacing
                        : row.LineCount * (tech.LineHeight + RowSpacing);

                    if (IsRowVisible(y, rowH, clipBounds))
                    {
                        if (row.Kind == Properties.RowKind.BulletList)
                            DrawBulletListRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.Items);
                        else if (row.Kind == Properties.RowKind.Color)
                            DrawColorRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.Color, row.Value);
                        else if (row.Kind == Properties.RowKind.BarGraph)
                            DrawBarRow(spriteBatch, tech, clipBounds.X + indent, y, row.Label, row.CurrentValue, row.MaxValue, row.SegmentCount, clipBounds);
                        else
                            DrawRow(spriteBatch, tech, row.Label, row.Value, clipBounds.X + indent, y);
                    }

                    y += rowH;
                }
            }
        }

        private static void DrawBarRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, float y,
            string label, float current, float max, int segmentCount, Rectangle clipBounds)
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
            int filled = (int)MathF.Round(fraction * n);

            Color fillColor  = Color.Lerp(new Color(200, 50, 50), new Color(50, 200, 80), fraction);
            Color emptyColor = new(35, 35, 35, 210);
            Color corner     = ColorPalette.BlockBackground;

            for (int i = 0; i < n; i++)
            {
                int sx  = (int)(valueX + i * (segW + BarSegmentGap));
                int sy  = (int)y;
                int sw  = (int)segW;
                int sh  = BarRowHeight;
                Color c = i < filled ? fillColor : emptyColor;

                DrawRect(spriteBatch, new Rectangle(sx, sy, sw, sh), c);

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

        private static void DrawRow(SpriteBatch spriteBatch, UIStyle.UIFont font, string label, string value, float x, float y)
        {
            Vector2 labelSize = font.MeasureString(label);
            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            float valueX = x + Math.Max(labelSize.X + Padding, 120f);
            font.DrawString(spriteBatch, value, new Vector2(valueX, y), UIStyle.TextColor);
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
