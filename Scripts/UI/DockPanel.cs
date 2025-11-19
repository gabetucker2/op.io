using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public enum DockPanelKind
    {
        Game,
        Blank,
        Controls,
        Notes,
        Backend
    }

    public sealed class DockPanel
    {
        public DockPanel(string id, string title, DockPanelKind kind)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = string.IsNullOrWhiteSpace(title) ? id : title;
            Kind = kind;
            IsVisible = true;
        }

        public string Id { get; }
        public string Title { get; }
        public DockPanelKind Kind { get; }
        public bool IsVisible { get; set; } = true;
        public Rectangle Bounds { get; set; }

        public Rectangle GetDragBarBounds(int dragBarHeight)
        {
            return new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Math.Max(0, Math.Min(dragBarHeight, Bounds.Height)));
        }

        public Rectangle GetContentBounds(int headerHeight, int padding)
        {
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
