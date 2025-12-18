using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io.UI.BlockScripts.BlockUtilities
{
    /// <summary>
    /// Represents a group of blocks contained within a panel (the rectangular space with the drag bar and active block).
    /// Each group is rendered through a single layout node.
    /// </summary>
    internal sealed class PanelGroup
    {
        private readonly List<DockBlock> _blocks = new();

        public PanelGroup(string panelId, DockBlock initialBlock)
        {
            PanelId = panelId ?? throw new ArgumentNullException(nameof(panelId));
            if (initialBlock != null)
            {
                AddBlock(initialBlock, makeActive: true);
            }
        }

        public string PanelId { get; }
        public IReadOnlyList<DockBlock> Blocks => _blocks;
        public string ActiveBlockId { get; private set; }
        public bool TabsExpanded { get; set; } = true;
        public bool IsLocked { get; set; } = false;

        public DockBlock ActiveBlock => _blocks.FirstOrDefault(b => string.Equals(b.Id, ActiveBlockId, StringComparison.OrdinalIgnoreCase)) ?? _blocks.FirstOrDefault();

        public bool Contains(string blockId)
        {
            return _blocks.Any(b => string.Equals(b.Id, blockId, StringComparison.OrdinalIgnoreCase));
        }

        public void AddBlock(DockBlock block, bool makeActive = false, int? insertIndex = null)
        {
            if (block == null || Contains(block.Id))
            {
                return;
            }

            if (insertIndex.HasValue)
            {
                int index = Math.Clamp(insertIndex.Value, 0, _blocks.Count);
                _blocks.Insert(index, block);
            }
            else
            {
                _blocks.Add(block);
            }

            if (makeActive || string.IsNullOrWhiteSpace(ActiveBlockId))
            {
                ActiveBlockId = block.Id;
            }
        }

        public bool RemoveBlock(string blockId, out DockBlock removed)
        {
            removed = null;
            if (string.IsNullOrWhiteSpace(blockId))
            {
                return false;
            }

            for (int i = 0; i < _blocks.Count; i++)
            {
                DockBlock candidate = _blocks[i];
                if (!string.Equals(candidate.Id, blockId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _blocks.RemoveAt(i);
                removed = candidate;
                if (string.Equals(ActiveBlockId, blockId, StringComparison.OrdinalIgnoreCase))
                {
                    ActiveBlockId = _blocks.FirstOrDefault()?.Id;
                }

                return true;
            }

            return false;
        }

        public void SetActiveBlock(string blockId)
        {
            if (Contains(blockId))
            {
                ActiveBlockId = blockId;
            }
        }

        public int IndexOf(string blockId)
        {
            return _blocks.FindIndex(b => string.Equals(b.Id, blockId, StringComparison.OrdinalIgnoreCase));
        }

        public void MoveBlock(string blockId, int targetIndex)
        {
            int currentIndex = IndexOf(blockId);
            if (currentIndex < 0)
            {
                return;
            }

            targetIndex = Math.Clamp(targetIndex, 0, _blocks.Count - 1);
            if (targetIndex == currentIndex)
            {
                return;
            }

            DockBlock block = _blocks[currentIndex];
            _blocks.RemoveAt(currentIndex);
            if (targetIndex >= _blocks.Count)
            {
                _blocks.Add(block);
            }
            else
            {
                _blocks.Insert(targetIndex, block);
            }

            if (string.IsNullOrWhiteSpace(ActiveBlockId))
            {
                ActiveBlockId = block.Id;
            }
        }
    }
}
