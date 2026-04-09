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

    public sealed class BlockNode : DockNode
    {
        public BlockNode(DockBlock block)
        {
            Block = block;
        }

        public DockBlock Block { get; private set; }
        public override bool HasVisibleContent => Block != null && Block.IsVisible;

        public override int GetMinWidth()
        {
            if (Block == null || !Block.IsVisible)
            {
                return 0;
            }

            return Math.Max(0, Block.MinWidth);
        }

        public override int GetMinHeight()
        {
            if (Block == null || !Block.IsVisible)
            {
                return 0;
            }

            return Math.Max(0, Block.MinHeight);
        }

        public override void Arrange(Rectangle bounds)
        {
            Bounds = bounds;
            if (Block != null)
            {
                Block.Bounds = bounds;
            }
        }

        public void SetBlock(DockBlock block)
        {
            Block = block;
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
        public int? PreferredFirstSpan { get; set; }
        public int? PreferredSecondSpan { get; set; }

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
            Rectangle previousBounds = Bounds;
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

                AdjustPreferredSpansForParentChange(previousBounds, bounds, minFirstHeight, minSecondHeight);

                int splitHeight;
                if (PreferredFirstSpan.HasValue)
                {
                    splitHeight = Math.Clamp(PreferredFirstSpan.Value, minSplit, maxSplit);
                    float derivedRatio = bounds.Height > 0 ? splitHeight / (float)bounds.Height : SplitRatio;
                    SplitRatio = MathHelper.Clamp(derivedRatio, 0.001f, 0.999f);
                }
                else
                {
                    splitHeight = Math.Clamp((int)MathF.Round(bounds.Height * SplitRatio), minSplit, maxSplit);
                }

                Rectangle top = new(bounds.X, bounds.Y, bounds.Width, splitHeight);
                Rectangle bottom = new(bounds.X, bounds.Y + splitHeight, bounds.Width, bounds.Height - splitHeight);
                First?.Arrange(top);
                Second?.Arrange(bottom);

                PreferredFirstSpan = top.Height;
                PreferredSecondSpan = bottom.Height;
            }
            else
            {
                int minFirstWidth = Math.Min(bounds.Width, Math.Max(0, First?.GetMinWidth() ?? 0));
                int minSecondWidth = Math.Min(bounds.Width, Math.Max(0, Second?.GetMinWidth() ?? 0));
                int minSplit = Math.Min(bounds.Width, Math.Max(0, minFirstWidth));
                int maxSplit = Math.Max(minSplit, bounds.Width - minSecondWidth);

                AdjustPreferredSpansForParentChange(previousBounds, bounds, minFirstWidth, minSecondWidth);

                int splitWidth;
                if (PreferredFirstSpan.HasValue)
                {
                    splitWidth = Math.Clamp(PreferredFirstSpan.Value, minSplit, maxSplit);
                    float derivedRatio = bounds.Width > 0 ? splitWidth / (float)bounds.Width : SplitRatio;
                    SplitRatio = MathHelper.Clamp(derivedRatio, 0.001f, 0.999f);
                }
                else
                {
                    splitWidth = Math.Clamp((int)MathF.Round(bounds.Width * SplitRatio), minSplit, maxSplit);
                }

                Rectangle left = new(bounds.X, bounds.Y, splitWidth, bounds.Height);
                Rectangle right = new(bounds.X + splitWidth, bounds.Y, bounds.Width - splitWidth, bounds.Height);
                First?.Arrange(left);
                Second?.Arrange(right);

                PreferredFirstSpan = left.Width;
                PreferredSecondSpan = right.Width;
            }
        }

        private void AdjustPreferredSpansForParentChange(Rectangle previousBounds, Rectangle newBounds, int minFirstSpan, int minSecondSpan)
        {
            if (!PreferredFirstSpan.HasValue || !PreferredSecondSpan.HasValue)
            {
                return;
            }

            if (previousBounds == Rectangle.Empty)
            {
                return;
            }

            int oldTotal = Orientation == DockSplitOrientation.Horizontal ? previousBounds.Height : previousBounds.Width;
            int newTotal = Orientation == DockSplitOrientation.Horizontal ? newBounds.Height      : newBounds.Width;

            if (oldTotal <= 0 || oldTotal == newTotal)
            {
                return;
            }

            int minFirst  = Math.Clamp(minFirstSpan,  0, newTotal);
            int minSecond = Math.Clamp(minSecondSpan, 0, newTotal);
            int maxFirst  = Math.Max(minFirst, newTotal - minSecond);

            // Preserve the absolute pixel span of the first child; the second child
            // absorbs the delta. This gives "push" behaviour: resizing a parent does
            // not force all descendants to scale proportionally.
            int first  = Math.Clamp(PreferredFirstSpan.Value, minFirst, maxFirst);
            int second = Math.Max(minSecond, newTotal - first);

            PreferredFirstSpan  = first;
            PreferredSecondSpan = second;
        }
    }

    public static class DockLayout
    {
        public static DockNode Detach(DockNode node, BlockNode target)
        {
            if (node == null || target == null)
            {
                return node;
            }

            if (node is BlockNode blockNode && blockNode == target)
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

        public static DockNode InsertRelative(DockNode node, BlockNode insertNode, BlockNode referenceNode, DockEdge edge)
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

            if (node is BlockNode blockNode && blockNode == referenceNode)
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
                    split.Second = blockNode;
                }
                else
                {
                    split.First = blockNode;
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
