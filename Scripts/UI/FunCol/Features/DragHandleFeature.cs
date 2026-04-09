using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol.Features
{
    /// <summary>
    /// A fun-col column feature that renders a drag-reorder grip (three horizontal lines).
    /// The parent block is responsible for initiating drag via BlockDragState when this
    /// column is hovered and the mouse is pressed.
    /// </summary>
    public class DragHandleFeature : FunctionFieldFeature
    {
        private readonly string _label;
        public override string Label => _label;

        public DragHandleFeature(string label = "drag")
        {
            _label = label ?? "drag";
        }

        public override void Draw(SpriteBatch sb, Rectangle colBounds, Color baseColor,
            UIStyle.UIFont font, Texture2D pixel, bool isExpanded)
        {
            if (pixel == null || colBounds.Width < 8) return;

            const int lines   = 3;
            const int lineH   = 2;
            const int spacing = 3;

            int totalH = lineH * lines + spacing * (lines - 1);
            int startY = colBounds.Y + (colBounds.Height - totalH) / 2;
            int lineW  = System.Math.Min(colBounds.Width - 8, 16);
            if (lineW <= 0) return;
            int lineX  = colBounds.X + (colBounds.Width - lineW) / 2;

            Color gripColor = isExpanded ? Color.White : Color.White * 0.55f;
            for (int i = 0; i < lines; i++)
                sb.Draw(pixel,
                    new Rectangle(lineX, startY + i * (lineH + spacing), lineW, lineH),
                    gripColor);
        }
    }
}
