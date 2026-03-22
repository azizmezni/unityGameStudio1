using UnityEngine.UIElements;

namespace ClaudeCodeGameStudios.Utilities
{
    /// <summary>
    /// Helper for UI Toolkit style properties that don't have shorthand setters.
    /// </summary>
    public static class StyleHelper
    {
        public static void SetBorderRadius(IStyle style, float radius)
        {
            style.borderTopLeftRadius = radius;
            style.borderTopRightRadius = radius;
            style.borderBottomLeftRadius = radius;
            style.borderBottomRightRadius = radius;
        }
    }
}
