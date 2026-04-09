using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol.Features
{
    /// <summary>
    /// A fun-col column feature for displaying cycling values (enum options or boolean toggles).
    ///
    /// For boolean/switch types: set IsBoolean=true and BoolState; renders a colored dot.
    /// For enum/string types:    set IsBoolean=false and CurrentValue; renders centered text.
    ///
    /// Click detection is handled by the parent block (check FunColInterface.HoveredColumn == this column's index).
    /// Set IsLocked=true to prevent visual interaction cues.
    /// </summary>
    public class CyclerFeature : FunctionFieldFeature
    {
        private readonly string _label;

        /// <summary>Text alignment for enum/text display mode. Defaults to Center.</summary>
        public FunColTextAlign TextAlign { get; set; } = FunColTextAlign.Center;

        /// <summary>Current display value for enum/text mode. Updated externally each frame.</summary>
        public string CurrentValue { get; set; } = string.Empty;

        /// <summary>True = render a boolean on/off dot instead of text.</summary>
        public bool IsBoolean { get; set; } = false;

        /// <summary>Boolean on/off state when IsBoolean is true.</summary>
        public bool BoolState { get; set; } = false;

        /// <summary>When true, the cycler is read-only (dot is dim, text is muted).</summary>
        public bool IsLocked { get; set; } = false;

        public override string Label => _label;

        public CyclerFeature(string label = "type")
        {
            _label = label ?? "type";
        }

        public override void Draw(SpriteBatch sb, Rectangle colBounds, Color baseColor,
            UIStyle.UIFont font, Texture2D pixel, bool isExpanded)
        {
            if (colBounds.Width < 4) return;

            if (IsBoolean)
                DrawBoolDot(sb, colBounds, pixel);
            else
                DrawTextValue(sb, colBounds, font, isExpanded);
        }

        private void DrawBoolDot(SpriteBatch sb, Rectangle colBounds, Texture2D pixel)
        {
            if (pixel == null) return;

            const int Pad = 4;
            int dotSize = System.Math.Min(10, System.Math.Min(colBounds.Width - Pad * 2, colBounds.Height - Pad * 2));
            if (dotSize <= 0) return;

            int dx = TextAlign switch
            {
                FunColTextAlign.Left  => colBounds.X + Pad,
                FunColTextAlign.Right => colBounds.Right - dotSize - Pad,
                _                    => colBounds.X + (colBounds.Width - dotSize) / 2,
            };
            int dy = colBounds.Y + (colBounds.Height - dotSize) / 2;

            Color dotColor = BoolState ? new Color(80, 220, 80) : new Color(220, 80, 80);
            if (IsLocked) dotColor *= 0.5f;

            sb.Draw(pixel, new Rectangle(dx, dy, dotSize, dotSize), dotColor);
        }

        private void DrawTextValue(SpriteBatch sb, Rectangle colBounds, UIStyle.UIFont font, bool isExpanded)
        {
            if (!font.IsAvailable) return;

            string val = CurrentValue;
            if (string.IsNullOrEmpty(val)) return;

            const int Pad = 4;
            Vector2 size = font.MeasureString(val);
            float ty = colBounds.Y + (colBounds.Height - size.Y) / 2f;
            float tx = TextAlign switch
            {
                FunColTextAlign.Left  => colBounds.X + Pad,
                FunColTextAlign.Right => colBounds.Right - size.X - Pad,
                _                    => colBounds.X + (colBounds.Width - size.X) / 2f,
            };

            Color textColor = IsLocked ? UIStyle.MutedTextColor : Color.White;
            font.DrawString(sb, val, new Microsoft.Xna.Framework.Vector2(tx, ty), textColor);
        }
    }
}
