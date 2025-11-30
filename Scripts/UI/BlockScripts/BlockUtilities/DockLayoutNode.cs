using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public enum DockSplitOrientation
    {
        Horizontal,
        Vertical
    }

    public enum DockEdge
    {
        Top,
        Bottom,
        Left,
        Right,
        Center
    }

    public abstract class DockNode
    {
        public Rectangle Bounds { get; protected set; }
        public abstract bool HasVisibleContent { get; }
        public abstract int GetMinWidth();
        public abstract int GetMinHeight();
        public abstract void Arrange(Rectangle bounds);
    }

    public sealed class PanelNode : DockNode
    {
        public PanelNode(DockPanel panel)
        {
            Panel = panel;
        }

        public DockPanel Panel { get; }
        public override bool HasVisibleContent => Panel != null && Panel.IsVisible;

        public override int GetMinWidth()
        {
            if (Panel == null || !Panel.IsVisible)
            {
                return 0;
            }

            return Math.Max(0, Panel.MinWidth);
        }

        public override int GetMinHeight()
        {
            if (Panel == null || !Panel.IsVisible)
            {
                return 0;
            }

            return Math.Max(0, Panel.MinHeight);
        }

        public override void Arrange(Rectangle bounds)
        {
            Bounds = bounds;
            if (Panel != null)
            {
                Panel.Bounds = bounds;
            }
        }
    }

    public sealed class SplitNode : DockNode
    {
        public SplitNode(DockSplitOrientation orientation)
        {
            Orientation = orientation;
        }

        public DockSplitOrientation Orientation { get; set; }
        public float SplitRatio { get; set; } = 0.5f;
        public DockNode First { get; set; }
        public DockNode Second { get; set; }

        public override bool HasVisibleContent =>
            (First?.HasVisibleContent ?? false) ||
            (Second?.HasVisibleContent ?? false);

        public override int GetMinWidth()
        {
            int firstMin = First?.GetMinWidth() ?? 0;
            int secondMin = Second?.GetMinWidth() ?? 0;
            bool firstVisible = First?.HasVisibleContent ?? false;
            bool secondVisible = Second?.HasVisibleContent ?? false;

            if (!firstVisible && !secondVisible)
            {
                return 0;
            }

            if (!firstVisible)
            {
                return secondMin;
            }

            if (!secondVisible)
            {
                return firstMin;
            }

            return Orientation == DockSplitOrientation.Vertical
                ? firstMin + secondMin
                : Math.Max(firstMin, secondMin);
        }

        public override int GetMinHeight()
        {
            int firstMin = First?.GetMinHeight() ?? 0;
            int secondMin = Second?.GetMinHeight() ?? 0;
            bool firstVisible = First?.HasVisibleContent ?? false;
            bool secondVisible = Second?.HasVisibleContent ?? false;

            if (!firstVisible && !secondVisible)
            {
                return 0;
            }

            if (!firstVisible)
            {
                return secondMin;
            }

            if (!secondVisible)
            {
                return firstMin;
            }

            return Orientation == DockSplitOrientation.Horizontal
                ? firstMin + secondMin
                : Math.Max(firstMin, secondMin);
        }

        public override void Arrange(Rectangle bounds)
        {
            Bounds = bounds;
            bool firstVisible = First?.HasVisibleContent ?? false;
            bool secondVisible = Second?.HasVisibleContent ?? false;

            if (!firstVisible && !secondVisible)
            {
                return;
            }

            if (!firstVisible)
            {
                Second?.Arrange(bounds);
                return;
            }

            if (!secondVisible)
            {
                First?.Arrange(bounds);
                return;
            }

            if (Orientation == DockSplitOrientation.Horizontal)
            {
                int minFirstHeight = Math.Min(bounds.Height, Math.Max(0, First?.GetMinHeight() ?? 0));
                int minSecondHeight = Math.Min(bounds.Height, Math.Max(0, Second?.GetMinHeight() ?? 0));
                int minSplit = Math.Min(bounds.Height, Math.Max(0, minFirstHeight));
                int maxSplit = Math.Max(minSplit, bounds.Height - minSecondHeight);
                int splitHeight = Math.Clamp((int)(bounds.Height * SplitRatio), minSplit, maxSplit);

                Rectangle top = new(bounds.X, bounds.Y, bounds.Width, splitHeight);
                Rectangle bottom = new(bounds.X, bounds.Y + splitHeight, bounds.Width, bounds.Height - splitHeight);
                First?.Arrange(top);
                Second?.Arrange(bottom);
            }
            else
            {
                int minFirstWidth = Math.Min(bounds.Width, Math.Max(0, First?.GetMinWidth() ?? 0));
                int minSecondWidth = Math.Min(bounds.Width, Math.Max(0, Second?.GetMinWidth() ?? 0));
                int minSplit = Math.Min(bounds.Width, Math.Max(0, minFirstWidth));
                int maxSplit = Math.Max(minSplit, bounds.Width - minSecondWidth);
                int splitWidth = Math.Clamp((int)(bounds.Width * SplitRatio), minSplit, maxSplit);

                Rectangle left = new(bounds.X, bounds.Y, splitWidth, bounds.Height);
                Rectangle right = new(bounds.X + splitWidth, bounds.Y, bounds.Width - splitWidth, bounds.Height);
                First?.Arrange(left);
                Second?.Arrange(right);
            }
        }
    }

    public static class DockLayout
    {
        public static DockNode Detach(DockNode node, PanelNode target)
        {
            if (node == null || target == null)
            {
                return node;
            }

            if (node is PanelNode panelNode && panelNode == target)
            {
                return null;
            }

            if (node is SplitNode split)
            {
                split.First = Detach(split.First, target);
                split.Second = Detach(split.Second, target);

                if (split.First == null && split.Second == null)
                {
                    return null;
                }

                if (split.First == null)
                {
                    return split.Second;
                }

                if (split.Second == null)
                {
                    return split.First;
                }
            }

            return node;
        }

        public static DockNode InsertRelative(DockNode node, PanelNode insertNode, PanelNode referenceNode, DockEdge edge)
        {
            if (insertNode == null)
            {
                return node;
            }

            if (referenceNode == null)
            {
                return node ?? insertNode;
            }

            if (node == null)
            {
                return insertNode;
            }

            if (node is PanelNode panelNode && panelNode == referenceNode)
            {
                var orientation = edge is DockEdge.Top or DockEdge.Bottom
                    ? DockSplitOrientation.Horizontal
                    : DockSplitOrientation.Vertical;

                SplitNode split = new(orientation)
                {
                    SplitRatio = 0.5f
                };

                if (edge is DockEdge.Top or DockEdge.Left)
                {
                    split.First = insertNode;
                    split.Second = panelNode;
                }
                else
                {
                    split.First = panelNode;
                    split.Second = insertNode;
                }

                return split;
            }

            if (node is SplitNode parentSplit)
            {
                var updatedFirst = InsertRelative(parentSplit.First, insertNode, referenceNode, edge);
                if (!ReferenceEquals(updatedFirst, parentSplit.First))
                {
                    parentSplit.First = updatedFirst;
                    return node;
                }

                var updatedSecond = InsertRelative(parentSplit.Second, insertNode, referenceNode, edge);
                parentSplit.Second = updatedSecond;
            }

            return node;
        }
    }
}
