using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using op.io.UI.BlockScripts.BlockUtilities;
using op.io.UI.FunCol;
using op.io.UI.FunCol.Features;

namespace op.io.UI.BlockScripts.Blocks
{
    /// <summary>
    /// Live FunCol spreadsheet of per-function CPU time and managed-heap allocation.
    /// Columns: Function | Cur Mem | Avg Mem | Avg ms
    /// Sorted most-to-least by Avg Mem by default; click any header column to re-sort.
    /// Rows have full expansion animation (DisableExpansion = false).
    /// Lives in the same panel group as Debug Logs.
    /// </summary>
    internal static class PerformanceBlock
    {
        public const string BlockTitle = "Performance";
        public const int MinWidth  = 290;
        public const int MinHeight = 0;

        // Column layout: Function | Cur Mem | Avg Mem | Avg ms
        private static readonly float[] ColWeights = { 0.52f, 0.18f, 0.18f, 0.12f };
        private const int HeaderRowHeight = 16;

        // Per-row FunColInterfaces, keyed by function name
        private static readonly Dictionary<string, FunColInterface> _rowFunCols =
            new(StringComparer.OrdinalIgnoreCase);

        // Header FunCol — EnableColumnSort = true, drives sort state
        private static FunColInterface _headerFunCol;

        private static readonly BlockScrollPanel _scrollPanel = new();
        private static float _lineHeightCache;
        private static Texture2D _pixelTexture;
        private static bool _headerVisibleLoaded;

        // Snapshot refreshed each Update (already sorted before storing)
        private static ProfileEntry[] _entries = Array.Empty<ProfileEntry>();
        private static readonly List<RowLayout> _rowLayouts = new();

        private readonly struct RowLayout
        {
            public RowLayout(string key, Rectangle bounds) { Key = key; Bounds = bounds; }
            public string    Key    { get; }
            public Rectangle Bounds { get; }
        }

        // ── Update ────────────────────────────────────────────────────────────

        public static void Update(GameTime gameTime, Rectangle contentBounds,
            MouseState mouseState, MouseState previousMouseState)
        {
            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Performance);

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out UIStyle.UIFont regularFont))
            {
                _scrollPanel.Update(contentBounds, 0f, mouseState, previousMouseState);
                return;
            }

            if (_lineHeightCache <= 0f)
                _lineHeightCache = FontManager.CalculateRowLineHeight(boldFont, regularFont);

            // Header hover + sort click detection
            FunColInterface hfc = GetOrEnsureHeaderFunCol();

            // One-time load of header visibility from database
            if (!_headerVisibleLoaded)
            {
                Dictionary<string, string> rowData = BlockDataStore.LoadRowData(DockBlockKind.Performance);
                if (rowData.TryGetValue("FunColHeaderVisible", out string stored))
                    hfc.HeaderVisible = !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase);
                _headerVisibleLoaded = true;
            }

            int perfHeaderH = hfc.HeaderVisible ? HeaderRowHeight : 0;
            var headerStrip = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, perfHeaderH);
            hfc.ShowHeaderToggle = BlockManager.DockingModeEnabled;
            hfc.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            hfc.UpdateHeaderHover(headerStrip, mouseState, blockLocked ? (MouseState?)previousMouseState : null);

            // Persist header visibility when toggled
            if (hfc.HeaderToggleClicked)
                BlockDataStore.SetRowData(DockBlockKind.Performance, "FunColHeaderVisible", hfc.HeaderVisible ? "true" : "false");

            // Refresh + sort entries
            _entries = FrameProfiler.GetEntries();
            ApplySortFromHeader(GetOrEnsureHeaderFunCol());

            // Scroll panel
            var listArea = new Rectangle(
                contentBounds.X,
                contentBounds.Y + perfHeaderH,
                contentBounds.Width,
                Math.Max(0, contentBounds.Height - perfHeaderH));

            float contentHeight = _entries.Length * _lineHeightCache;
            _scrollPanel.Update(listArea, contentHeight,
                blockLocked ? previousMouseState : mouseState, previousMouseState);

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty) listBounds = listArea;

            RebuildRowLayouts(listBounds);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (RowLayout rl in _rowLayouts)
            {
                if (rl.Bounds != Rectangle.Empty)
                    GetOrCreateRowFunCol(rl.Key).Update(rl.Bounds, mouseState, dt, blockLocked);
            }
        }

        // ── Draw ─────────────────────────────────────────────────────────────

        public static void Draw(SpriteBatch spriteBatch, Rectangle contentBounds)
        {
            if (spriteBatch == null) return;

            bool blockLocked = BlockManager.IsBlockLocked(DockBlockKind.Performance);

            if (!FontManager.TryGetBackendFonts(out UIStyle.UIFont boldFont, out _))
                return;

            EnsurePixelTexture();

            var perfHfcDraw = GetOrEnsureHeaderFunCol();
            int perfHdrHDraw = perfHfcDraw.HeaderVisible ? HeaderRowHeight : 0;
            var listArea = new Rectangle(
                contentBounds.X,
                contentBounds.Y + perfHdrHDraw,
                contentBounds.Width,
                Math.Max(0, contentBounds.Height - perfHdrHDraw));

            Rectangle listBounds = _scrollPanel.ContentViewportBounds;
            if (listBounds == Rectangle.Empty) listBounds = listArea;

            // Scissor clip so rows don't bleed over the header
            var gd = spriteBatch.GraphicsDevice;
            float uiScale = BlockManager.UIScale;
            Rectangle scissorRect = uiScale > 0f && uiScale != 1f
                ? new Rectangle(
                    (int)(listBounds.X      * uiScale),
                    (int)(listBounds.Y      * uiScale),
                    (int)(listBounds.Width  * uiScale),
                    (int)(listBounds.Height * uiScale))
                : listBounds;
            var vp = gd.Viewport;
            scissorRect.X      = Math.Clamp(scissorRect.X,      0, vp.Width);
            scissorRect.Y      = Math.Clamp(scissorRect.Y,      0, vp.Height);
            scissorRect.Width  = Math.Clamp(scissorRect.Width,  0, vp.Width  - scissorRect.X);
            scissorRect.Height = Math.Clamp(scissorRect.Height, 0, vp.Height - scissorRect.Y);

            spriteBatch.End();
            var scissorState = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            gd.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, scissorState, null, BlockManager.CurrentUITransform);

            if (_entries.Length == 0)
            {
                boldFont.DrawString(spriteBatch, "No profiler data yet.",
                    new Vector2(listBounds.X + 4, listBounds.Y + 4), UIStyle.MutedTextColor);
            }
            else
            {
                foreach (RowLayout rl in _rowLayouts)
                {
                    if (rl.Bounds == Rectangle.Empty) continue;
                    if (rl.Bounds.Y  >= listBounds.Bottom) break;
                    if (rl.Bounds.Bottom <= listBounds.Y)  continue;
                    DrawRow(spriteBatch, rl, boldFont);
                }
            }

            spriteBatch.End();
            scissorState.Dispose();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, BlockManager.CurrentUITransform);

            _scrollPanel.Draw(spriteBatch, blockLocked);

            // Header drawn last — stays on top of scrolled rows
            perfHfcDraw.CollapsedToggleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, HeaderRowHeight);
            perfHfcDraw.DrawHeader(spriteBatch, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, perfHdrHDraw), boldFont, _pixelTexture);
        }

        // ── Sorting ───────────────────────────────────────────────────────────

        private static void ApplySortFromHeader(FunColInterface hfc)
        {
            if (_entries.Length <= 1) return;
            int col   = hfc.SortColumn;
            bool desc = hfc.SortDescending;

            Comparison<ProfileEntry> cmp = col switch
            {
                0 => (a, b) => CompareStrings(a.FunctionName,    b.FunctionName,    desc),
                1 => (a, b) => CompareLong(a.CurrentAllocBytes,  b.CurrentAllocBytes, desc),
                2 => (a, b) => CompareLong(a.AvgAllocBytes,      b.AvgAllocBytes,   desc),
                3 => (a, b) => CompareDouble(a.AvgMs,            b.AvgMs,           desc),
                _ => (a, b) => CompareLong(a.AvgAllocBytes,      b.AvgAllocBytes,   true) // default: avg mem desc
            };
            Array.Sort(_entries, cmp);
        }

        private static int CompareDouble(double a, double b, bool desc) => desc ? b.CompareTo(a) : a.CompareTo(b);
        private static int CompareLong(long a,    long b,   bool desc) => desc ? b.CompareTo(a) : a.CompareTo(b);
        private static int CompareStrings(string a, string b, bool desc)
        {
            int r = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return desc ? -r : r;
        }

        // ── Row layout ────────────────────────────────────────────────────────

        private static void RebuildRowLayouts(Rectangle listBounds)
        {
            _rowLayouts.Clear();
            if (_lineHeightCache <= 0f || _entries.Length == 0) return;

            int rowH = (int)MathF.Ceiling(_lineHeightCache);
            float y  = listBounds.Y - _scrollPanel.ScrollOffset;
            foreach (ProfileEntry entry in _entries)
            {
                _rowLayouts.Add(new RowLayout(
                    entry.FunctionName,
                    new Rectangle(listBounds.X, (int)MathF.Round(y), listBounds.Width, rowH)));
                y += _lineHeightCache;
            }
        }

        // ── FunCol helpers ────────────────────────────────────────────────────

        private static FunColInterface GetOrCreateRowFunCol(string key)
        {
            if (_rowFunCols.TryGetValue(key, out var existing)) return existing;
            var fc = new FunColInterface(
                ColWeights,
                new TextLabelFeature("Function",      FunColTextAlign.Left),
                new ValueDisplayFeature("Cur Mem")    { TextAlign = FunColTextAlign.Right },
                new ValueDisplayFeature("Avg Mem")    { TextAlign = FunColTextAlign.Right },
                new ValueDisplayFeature("Avg ms")     { TextAlign = FunColTextAlign.Right }
            );
            fc.DisableExpansion = true;
            fc.DisableColors    = true;
            fc.SuppressTooltipWarnings = true;
            _rowFunCols[key] = fc;
            return fc;
        }

        private static FunColInterface GetOrEnsureHeaderFunCol()
        {
            if (_headerFunCol != null) return _headerFunCol;
            _headerFunCol = new FunColInterface(
                ColWeights,
                new TextLabelFeature("Function",    FunColTextAlign.Left)
                    { HeaderTooltipTexts = ["Profiled function or system name"] },
                new ValueDisplayFeature("Cur Mem")  { TextAlign = FunColTextAlign.Right,
                    HeaderTooltipTexts = ["Managed heap allocated by this function in the current frame"] },
                new ValueDisplayFeature("Avg Mem")  { TextAlign = FunColTextAlign.Right,
                    HeaderTooltipTexts = ["Rolling average managed-heap allocation per frame"] },
                new ValueDisplayFeature("Avg ms")   { TextAlign = FunColTextAlign.Right,
                    HeaderTooltipTexts = ["Rolling 60-frame average CPU time (ms)"] }
            );
            _headerFunCol.DisableExpansion    = true;
            _headerFunCol.DisableColors       = true;
            _headerFunCol.EnableColumnSort    = true;
            _headerFunCol.ShowHeaderTooltips  = true;
            return _headerFunCol;
        }

        // ── Row drawing ───────────────────────────────────────────────────────

        private static void DrawRow(SpriteBatch spriteBatch, RowLayout rl, UIStyle.UIFont font)
        {
            ProfileEntry entry = default;
            bool found = false;
            foreach (ProfileEntry e in _entries)
            {
                if (string.Equals(e.FunctionName, rl.Key, StringComparison.OrdinalIgnoreCase))
                { entry = e; found = true; break; }
            }
            if (!found) return;

            FunColInterface fc = GetOrCreateRowFunCol(rl.Key);

            if (fc.GetFeature(0) is TextLabelFeature fnF)       fnF.Text      = entry.FunctionName;
            if (fc.GetFeature(1) is ValueDisplayFeature curF)   curF.Text     = FormatBytes(entry.CurrentAllocBytes);
            if (fc.GetFeature(2) is ValueDisplayFeature avgMemF) avgMemF.Text = FormatBytes(entry.AvgAllocBytes);
            if (fc.GetFeature(3) is ValueDisplayFeature avgF)   avgF.Text     = entry.AvgMs.ToString("0.00");

            fc.Draw(spriteBatch, rl.Bounds, font, _pixelTexture);
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)          return "0";
            if (bytes < 1024)        return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
            return $"{bytes / (1024.0 * 1024.0):0.00} MB";
        }

        private static void EnsurePixelTexture()
        {
            if (_pixelTexture != null || Core.Instance?.GraphicsDevice == null) return;
            _pixelTexture = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }
    }
}
