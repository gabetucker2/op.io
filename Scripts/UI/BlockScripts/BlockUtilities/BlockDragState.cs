using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    internal sealed class BlockDragState<TRow>
    {
        private readonly Func<TRow, string> _keySelector;
        private readonly Func<TRow, Rectangle> _boundsSelector;
        private readonly Func<TRow, bool, TRow> _applyDraggingFlag;

        private bool _isDragging;
        private bool _hasSnapshot;
        private string _draggingKey;
        private TRow _draggingSnapshot;
        private float _dragOffsetY;
        private float _draggedRowY;
        private int _pendingDropIndex;
        private Rectangle _dropIndicatorBounds;

        public BlockDragState(Func<TRow, string> keySelector, Func<TRow, Rectangle> boundsSelector, Func<TRow, bool, TRow> applyDraggingFlag)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _boundsSelector = boundsSelector ?? throw new ArgumentNullException(nameof(boundsSelector));
            _applyDraggingFlag = applyDraggingFlag ?? throw new ArgumentNullException(nameof(applyDraggingFlag));
        }

        public bool IsDragging => _isDragging;
        public bool HasSnapshot => _hasSnapshot;
        public string DraggingKey => _draggingKey;
        public TRow DraggingSnapshot => _draggingSnapshot;
        public float DraggedRowY => _draggedRowY;
        public Rectangle DropIndicatorBounds => _dropIndicatorBounds;

        public bool TryStartDrag(IList<TRow> rows, string hoveredKey, MouseState mouseState)
        {
            if (_isDragging || rows == null || string.IsNullOrWhiteSpace(hoveredKey))
            {
                return false;
            }

            int index = GetRowIndex(rows, hoveredKey);
            if (index < 0)
            {
                return false;
            }

            TRow row = rows[index];
            Rectangle bounds = _boundsSelector(row);
            if (bounds == Rectangle.Empty)
            {
                return false;
            }

            row = _applyDraggingFlag(row, true);
            rows[index] = row;

            _isDragging = true;
            _hasSnapshot = true;
            _draggingKey = _keySelector(row);
            _draggingSnapshot = row;
            _dragOffsetY = mouseState.Y - bounds.Y;
            _draggedRowY = bounds.Y;
            _pendingDropIndex = index;
            _dropIndicatorBounds = Rectangle.Empty;

            return true;
        }

        public void UpdateDrag(IList<TRow> rows, Rectangle contentBounds, float lineHeight, MouseState mouseState)
        {
            if (!_hasSnapshot || rows == null || lineHeight <= 0f)
            {
                return;
            }

            float minTop = contentBounds.Y - MathF.Min(lineHeight * 0.65f, lineHeight);
            float maxTop = Math.Max(contentBounds.Y, contentBounds.Bottom - lineHeight);
            _draggedRowY = MathHelper.Clamp(mouseState.Y - _dragOffsetY, minTop, maxTop);

            float dragCenterY = _draggedRowY + (lineHeight / 2f);
            int dropIndex = 0;
            Rectangle indicator = Rectangle.Empty;
            Rectangle lastBounds = Rectangle.Empty;

            foreach (TRow row in rows)
            {
                string rowKey = _keySelector(row);
                if (string.Equals(rowKey, _draggingKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Rectangle bounds = _boundsSelector(row);
                if (bounds == Rectangle.Empty)
                {
                    continue;
                }

                float midpoint = bounds.Y + (bounds.Height / 2f);
                if (dragCenterY < midpoint)
                {
                    int indicatorY = Math.Max(contentBounds.Y - 2, bounds.Y - 2);
                    indicator = new Rectangle(contentBounds.X, indicatorY, contentBounds.Width, 4);
                    break;
                }

                lastBounds = bounds;
                dropIndex++;
            }

            if (indicator == Rectangle.Empty)
            {
                int indicatorY = lastBounds == Rectangle.Empty ? contentBounds.Y - 2 : lastBounds.Bottom - 2;
                indicator = new Rectangle(contentBounds.X, Math.Max(contentBounds.Y - 2, indicatorY), contentBounds.Width, 4);
            }

            _pendingDropIndex = dropIndex;
            _dropIndicatorBounds = indicator;
        }

        public bool TryCompleteDrag(IList<TRow> rows, out bool orderChanged)
        {
            orderChanged = false;

            if (!_hasSnapshot || rows == null)
            {
                Reset();
                return false;
            }

            int currentIndex = GetRowIndex(rows, _draggingKey);
            if (currentIndex < 0)
            {
                Reset();
                return false;
            }

            TRow row = rows[currentIndex];
            row = _applyDraggingFlag(row, false);
            rows[currentIndex] = row;

            rows.RemoveAt(currentIndex);
            int insertIndex = Math.Clamp(_pendingDropIndex, 0, rows.Count);
            rows.Insert(insertIndex, row);

            orderChanged = insertIndex != currentIndex;
            Reset();
            return true;
        }

        public Rectangle GetDragBounds(Rectangle contentBounds, float lineHeight)
        {
            if (!_hasSnapshot || lineHeight <= 0f)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(contentBounds.X, (int)MathF.Round(_draggedRowY), contentBounds.Width, (int)MathF.Ceiling(lineHeight));
        }

        public void Reset()
        {
            _isDragging = false;
            _hasSnapshot = false;
            _draggingKey = null;
            _draggingSnapshot = default;
            _dropIndicatorBounds = Rectangle.Empty;
            _draggedRowY = 0f;
            _dragOffsetY = 0f;
            _pendingDropIndex = 0;
        }

        private int GetRowIndex(IList<TRow> rows, string key)
        {
            if (rows == null || string.IsNullOrWhiteSpace(key))
            {
                return -1;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(_keySelector(rows[i]), key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
