using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

        private static Texture2D _pixel;
        private static MouseState _lastMouseState;
        private static readonly Dictionary<Shape, Texture2D> PreviewCache = new();

        private readonly struct PropertiesLayout
        {
            public PropertiesLayout(Rectangle inspectButton, Rectangle modeLabel, Rectangle previewBounds, Rectangle detailsBounds)
            {
                InspectButton = inspectButton;
                ModeLabel = modeLabel;
                PreviewBounds = previewBounds;
                DetailsBounds = detailsBounds;
            }

            public Rectangle InspectButton { get; }
            public Rectangle ModeLabel { get; }
            public Rectangle PreviewBounds { get; }
            public Rectangle DetailsBounds { get; }
        }

        public static void Update(GameTime gameTime, Rectangle contentBounds, MouseState mouseState, MouseState previousMouseState)
        {
            _lastMouseState = mouseState;
            PropertiesLayout layout = BuildLayout(contentBounds);
            bool leftClickReleased = mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Pressed;

            InspectableObjectInfo hovered = null;
            if (BlockManager.IsCursorWithinGameBlock())
            {
                Vector2 gameCursor = MouseFunctions.GetMousePosition();
                hovered = GameObjectInspector.FindHoveredObject(gameCursor);
            }

            InspectModeState.UpdateHovered(hovered);
            InspectModeState.ValidateLockStillValid();

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Properties);
            if (!blockLocked && leftClickReleased && layout.InspectButton.Contains(mouseState.Position))
            {
                InspectModeState.ClearLock();
            }
            else if (!blockLocked && leftClickReleased && InspectModeState.InspectModeEnabled && hovered != null)
            {
                InspectModeState.LockHovered();
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null)
            {
                return;
            }

            EnsureResources(spriteBatch.GraphicsDevice);
            PropertiesLayout layout = BuildLayout(contentBounds);
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Properties);
            bool buttonHovered = layout.InspectButton.Contains(_lastMouseState.Position);

            UIButtonRenderer.Draw(
                spriteBatch,
                layout.InspectButton,
                "Inspect nothing",
                UIButtonRenderer.ButtonStyle.Grey,
                buttonHovered,
                blockLocked);

            DrawModeBadge(spriteBatch, layout.ModeLabel);

            InspectableObjectInfo target = InspectModeState.GetActiveTarget();
            if (target == null || !target.IsValid)
            {
                DrawEmptyState(spriteBatch, layout);
                return;
            }

            DrawPreview(spriteBatch, layout.PreviewBounds, target);
            DrawDetails(spriteBatch, layout.DetailsBounds, target);
        }

        private static PropertiesLayout BuildLayout(Rectangle contentBounds)
        {
            int buttonWidth = Math.Clamp(contentBounds.Width / 3, 140, 240);
            Rectangle inspectButton = new(contentBounds.X, contentBounds.Y, Math.Min(buttonWidth, contentBounds.Width), ButtonHeight);

            int modeWidth = Math.Max(0, contentBounds.Width - inspectButton.Width - Padding);
            Rectangle modeLabel = new(inspectButton.Right + Padding, inspectButton.Y, modeWidth, ButtonHeight);

            int infoTop = inspectButton.Bottom + Padding;
            int infoHeight = Math.Max(0, contentBounds.Bottom - infoTop);
            Rectangle infoArea = new(contentBounds.X, infoTop, contentBounds.Width, infoHeight);

            int previewSize = Math.Min(infoArea.Height, Math.Min(infoArea.Width / 2, PreviewMaxSize));
            Rectangle previewBounds = previewSize >= 80
                ? new Rectangle(infoArea.X, infoArea.Y, previewSize, previewSize)
                : Rectangle.Empty;

            int detailsX = previewBounds == Rectangle.Empty ? infoArea.X : previewBounds.Right + Padding;
            int detailsWidth = previewBounds == Rectangle.Empty
                ? infoArea.Width
                : Math.Max(0, infoArea.Width - previewBounds.Width - Padding);
            Rectangle detailsBounds = new(detailsX, infoArea.Y, detailsWidth, infoArea.Height);

            return new PropertiesLayout(inspectButton, modeLabel, previewBounds, detailsBounds);
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

            string lockText = "Enable inspect mode to click-lock a target.";
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
            Texture2D preview = GetPreviewTexture(spriteBatch.GraphicsDevice, target.Shape);
            if (preview != null && !preview.IsDisposed)
            {
                Vector2 origin = new(preview.Width / 2f, preview.Height / 2f);
                Vector2 center = new(previewBounds.Center.X, previewBounds.Center.Y);
                float scale = Math.Min(
                    (previewBounds.Width - Padding * 2) / (float)preview.Width,
                    (previewBounds.Height - Padding * 2) / (float)preview.Height);
                scale = Math.Max(0.1f, scale);

                spriteBatch.Draw(preview, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            DrawRectOutline(spriteBatch, previewBounds, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);
        }

        private static void DrawDetails(SpriteBatch spriteBatch, Rectangle bounds, InspectableObjectInfo target)
        {
            UIStyle.UIFont heading = UIStyle.FontHBody;
            UIStyle.UIFont body = UIStyle.FontBody;
            UIStyle.UIFont tech = UIStyle.FontTech;
            if (!heading.IsAvailable || !body.IsAvailable || !tech.IsAvailable)
            {
                return;
            }

            float y = bounds.Y;
            Vector2 headingSize = heading.MeasureString(target.DisplayName);
            heading.DrawString(spriteBatch, target.DisplayName, new Vector2(bounds.X, y), UIStyle.TextColor);

            InspectableObjectInfo locked = InspectModeState.GetLockedTarget();
            if (locked != null && ReferenceEquals(locked.Source, target.Source))
            {
                string lockedTag = "LOCKED";
                Vector2 tagSize = tech.MeasureString(lockedTag);
                Vector2 tagPos = new(bounds.Right - tagSize.X, y + Math.Max(0, (headingSize.Y - tagSize.Y) / 2));
                tech.DrawString(spriteBatch, lockedTag, tagPos, UIStyle.AccentColor);
            }

            y += headingSize.Y + HeaderSpacing;

            DrawRow(spriteBatch, tech, "Type", $"{target.Type} (ID {target.Id})", bounds.X, ref y);

            string flags = BuildFlags(target);
            DrawRow(spriteBatch, tech, "Flags", flags, bounds.X, ref y);

            DrawRow(spriteBatch, tech, "Position", $"{target.Position.X:0.0}, {target.Position.Y:0.0}", bounds.X, ref y);

            float rotationDeg = MathHelper.ToDegrees(target.Rotation);
            DrawRow(spriteBatch, tech, "Rotation", $"{rotationDeg:0.0} deg", bounds.X, ref y);

            string sizeText = $"{target.Width} x {target.Height}";
            if (target.Shape != null && target.Shape.ShapeType == "Polygon" && target.Sides > 0)
            {
                sizeText = $"{sizeText} ({target.Sides}-sided polygon)";
            }
            else if (target.Shape != null)
            {
                sizeText = $"{sizeText} ({target.Shape.ShapeType})";
            }
            DrawRow(spriteBatch, tech, "Size", sizeText, bounds.X, ref y);

            DrawRow(spriteBatch, tech, "Mass", $"{target.Mass:0.##}", bounds.X, ref y);

            DrawColorRow(spriteBatch, tech, bounds.X, ref y, "Fill", target.FillColor);
            DrawColorRow(spriteBatch, tech, bounds.X, ref y, "Outline", target.OutlineColor);
        }

        private static string BuildFlags(InspectableObjectInfo target)
        {
            List<string> parts = new();
            if (target.IsPlayer)
            {
                parts.Add("Player");
            }

            parts.Add(target.StaticPhysics ? "Static" : "Dynamic");
            if (target.IsCollidable)
            {
                parts.Add("Collidable");
            }
            else
            {
                parts.Add("Non-collidable");
            }

            if (target.IsDestructible)
            {
                parts.Add("Destructible");
            }
            else
            {
                parts.Add("Indestructible");
            }

            return string.Join(" | ", parts);
        }

        private static void DrawRow(SpriteBatch spriteBatch, UIStyle.UIFont font, string label, string value, float x, ref float y)
        {
            Vector2 labelSize = font.MeasureString(label);
            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            float valueX = x + Math.Max(labelSize.X + Padding, 120f);
            font.DrawString(spriteBatch, value, new Vector2(valueX, y), UIStyle.TextColor);

            y += font.LineHeight + RowSpacing;
        }

        private static void DrawColorRow(SpriteBatch spriteBatch, UIStyle.UIFont font, float x, ref float y, string label, Color color)
        {
            Vector2 labelSize = font.MeasureString(label);
            font.DrawString(spriteBatch, label, new Vector2(x, y), UIStyle.MutedTextColor);

            int swatchSize = (int)Math.Max(12, font.LineHeight - 6);
            Rectangle swatch = new((int)(x + Math.Max(labelSize.X + Padding, 120f)), (int)y + 2, swatchSize, swatchSize);
            DrawRect(spriteBatch, swatch, color);
            DrawRectOutline(spriteBatch, swatch, UIStyle.BlockBorder, UIStyle.BlockBorderThickness);

            string hex = ToHex(color);
            float textX = swatch.Right + Padding;
            font.DrawString(spriteBatch, hex, new Vector2(textX, y), UIStyle.TextColor);

            y += font.LineHeight + RowSpacing;
        }

        private static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
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
