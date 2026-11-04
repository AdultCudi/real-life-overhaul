namespace RealLifeOverhaul.Ui
{
    /// <summary>The drawing tools of the Stocks chart, in the order of its rail. The number is the tool in hand; 0 is the pointer.</summary>
    public enum UiChartTool : byte
    {
        Pointer = 0,
        Trend,
        Level,
        Fibonacci,
        Ray,
        Rectangle,
        Measure,
        Note,
        Snap,
        Count
    }

    /// <summary>
    /// The rail's tools as one table: each has its own icon and the words of its tip, so no two tools share a picture and none
    /// is left without a name. Game-free. Cost: nothing.
    /// </summary>
    public static class UiChartTools
    {
        public const int Count = (int)UiChartTool.Count;

        /// <summary>The icon of each tool.</summary>
        public static readonly UiIcon[] Icons =
        {
            UiIcon.ToolCursor, UiIcon.ToolTrend, UiIcon.ToolLevel, UiIcon.ToolFib, UiIcon.ToolRay, UiIcon.ToolRect, UiIcon.ToolMeasure, UiIcon.ToolText, UiIcon.ToolSnap
        };

        public static readonly string[] Keys =
        {
            "reallife_stk_tool_pointer", "reallife_stk_tool_trend", "reallife_stk_tool_level", "reallife_stk_tool_fib", "reallife_stk_tool_ray",
            "reallife_stk_tool_rect", "reallife_stk_tool_measure", "reallife_stk_tool_note", "reallife_stk_tool_snap"
        };

        public static readonly string[] English =
        {
            "Pointer", "Trend line", "Horizontal line", "Fibonacci retracement", "Ray", "Rectangle", "Measure", "Note", "Snap line"
        };

        /// <summary>How many clicks a tool takes to finish a drawing: 1 for a line or a note, 2 for the rest, 0 for the pointer.</summary>
        public static int Clicks(int tool)
        {
            switch ((UiChartTool)tool)
            {
                case UiChartTool.Pointer: return 0;
                case UiChartTool.Level:
                case UiChartTool.Note:
                case UiChartTool.Snap: return 1;
                default: return 2;
            }
        }
    }
}
