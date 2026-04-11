using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public enum DockBlockKind
    {
        Game,
        Blank,
        Properties,
        ColorScheme,
        Controls,
        Notes,
        ControlSetups,
        DockingSetups,
        Backend,
        Specs,
        DebugLogs,
        Bars,
        Chat,
        Performance,
        Interact
    }

    /// <summary>
    /// Categorises how a block is hosted and displayed.
    /// Standard blocks live in normal panels, Overlay blocks superimpose on
    /// other blocks, and Dynamic blocks appear inside the Interact block
    /// when triggered by a game-world stimulus (e.g. ZoneBlock proximity).
    /// </summary>
    public enum DockBlockCategory
    {
        Standard,
        Overlay,
        Dynamic
    }

    public sealed class DockBlock
    {
        public DockBlock(string id, string title, DockBlockKind kind)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = string.IsNullOrWhiteSpace(title) ? id : title;
            Kind = kind;
            IsVisible = true;
        }

        public string Id { get; }
        public string Title { get; }
        public DockBlockKind Kind { get; }
        public DockBlockCategory Category { get; set; } = DockBlockCategory.Standard;
        public bool IsVisible { get; set; } = true;
        public Rectangle Bounds { get; set; }
        public int MinWidth { get; set; } = 10;
        public int MinHeight { get; set; } = 10;
        public float BackgroundOpacity { get; set; } = 1.0f;
        public bool IsOverlay { get; set; }
        public string OverlayParentId { get; set; }
        public float OverlayRelX { get; set; }
        public float OverlayRelY { get; set; }
        public float OverlayRelWidth { get; set; } = 0.4f;
        public float OverlayRelHeight { get; set; } = 0.4f;

        public Rectangle GetDragBarBounds(int dragBarHeight)
        {
            return new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Math.Max(0, Math.Min(dragBarHeight, Bounds.Height)));
        }

        public Rectangle GetGroupBarBounds(int dragBarHeight, int groupBarHeight)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0 || dragBarHeight <= 0 || groupBarHeight <= 0)
            {
                return Rectangle.Empty;
            }

            int headerHeight = Math.Min(dragBarHeight, Bounds.Height);
            int height = Math.Max(0, Math.Min(groupBarHeight, headerHeight));
            if (height <= 0)
            {
                return Rectangle.Empty;
            }

            int y = Bounds.Y + Math.Max(0, (headerHeight - height) / 2);
            return new Rectangle(Bounds.X, y, Bounds.Width, height);
        }

        public Rectangle GetContentBounds(int dragBarHeight, int padding, int groupBarHeight = 0)
        {
            int headerHeight = Math.Max(dragBarHeight, groupBarHeight);
            int contentY = Bounds.Y + headerHeight + padding;
            int contentHeight = Bounds.Height - headerHeight - (padding * 2);
            if (contentHeight < 0)
            {
                contentHeight = 0;
            }

            int contentX = Bounds.X + padding;
            int contentWidth = Bounds.Width - (padding * 2);
            if (contentWidth < 0)
            {
                contentWidth = 0;
            }

            return new Rectangle(contentX, contentY, contentWidth, contentHeight);
        }
    }
}
