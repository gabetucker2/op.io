namespace op.io
{
    /// <summary>
    /// Tracks the three interaction tiers for every interactable GUI element.
    /// NotHovering  — cursor is not over this element (or element is z-blocked by an overlay).
    /// Hovering     — cursor is over the element but no button is held.
    /// Interacting  — cursor is actively pressing / dragging the element.
    /// </summary>
    public enum GUIInteractionState
    {
        NotHovering = 0,
        Hovering    = 1,
        Interacting = 2,
    }
}
