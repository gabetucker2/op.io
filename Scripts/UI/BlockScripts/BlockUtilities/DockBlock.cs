using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public enum DockBlockKind
    {
        Game,
        Transparent,
        Blank,
        Properties,
        ColorScheme,
        Controls,
        Notes,
        ControlSetups,
        DockingSetups,
        Backend,
        Specs,
        DebugLogs
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
        public bool IsVisible { get; set; } = true;
        public Rectangle Bounds { get; set; }
        public int MinWidth { get; set; } = 10;
        public int MinHeight { get; set; } = 10;

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
