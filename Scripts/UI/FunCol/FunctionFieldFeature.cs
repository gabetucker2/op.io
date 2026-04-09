using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace op.io.UI.FunCol
{
    /// <summary>
    /// Abstract base for a single column in a FunColInterface.
    ///
    /// Each feature defines what its column displays and how it responds to cursor state.
    /// Instances are reusable across multiple FunColInterface instances.
    ///
    /// Column background color and expansion animation are handled by FunColInterface.
    /// Features only draw their content inside the already-colored column bounds.
    /// </summary>
    public abstract class FunctionFieldFeature
    {
        /// <summary>
        /// Short label shown in the column header when collapsed (hint to the user).
        /// Only shown when ShowLabelWhenCollapsed is true.
        /// </summary>
        public abstract string Label { get; }

        /// <summary>
        /// When true, the collapsed hint label is rendered inside the column
        /// (if it fits the width). Default: false — no text shown when collapsed.
        /// </summary>
        public bool ShowLabelWhenCollapsed { get; set; } = false;

        /// <summary>
        /// When true, ExpandedInstruction is rendered centered in the column
        /// background when the column is expanded. Default: false.
        /// </summary>
        public bool ShowTextWhenExpanded { get; set; } = false;

        /// <summary>
        /// Instruction text drawn in the column when expanded and ShowTextWhenExpanded is true.
        /// Typically a short user-facing hint like "Drag to reorder row".
        /// </summary>
        public string ExpandedInstruction { get; set; } = string.Empty;

        /// <summary>
        /// Optional tooltip texts shown (stacked) when the header column is hovered.
        /// The first non-empty entry is used; falls back to <see cref="Label"/> when the array
        /// is null or empty. Each entry renders as a separate tooltip box below the header.
        /// </summary>
        public string[] HeaderTooltipTexts { get; set; }

        /// <summary>
        /// Called each frame for the feature's column, regardless of hover state.
        /// colBounds: current animated bounds of this column.
        /// isExpanded: true when this column is the hovered/active one.
        /// </summary>
        public virtual void Update(Rectangle colBounds, MouseState mouse, float dt, bool isExpanded) { }

        /// <summary>
        /// Draw this feature's content inside colBounds.
        /// Called only when the column is wide enough to render (cw > 0).
        /// baseColor: the column's canonical color from FunColInterface.ColumnColors.
        /// isExpanded: true when this column is hovered/expanded.
        /// </summary>
        public abstract void Draw(SpriteBatch sb, Rectangle colBounds, Color baseColor,
            UIStyle.UIFont font, Texture2D pixel, bool isExpanded);
    }
}
