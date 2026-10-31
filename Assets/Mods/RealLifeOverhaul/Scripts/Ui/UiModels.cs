namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The building blocks of a screen's view model: what each component shows, as plain data with no colour, size or
    /// position of its own (the template and the kit decide those). Here the few sums the models share. Game-free.
    /// </summary>
    public static class UiModels
    {
        public const int Segments = 10;   // a stat bar's segments

        /// <summary>The segments a value of 0 to 100 fills, rounded as the wireframe rounds.</summary>
        public static int Filled(int value) => value <= 0 ? 0 : value >= 100 ? Segments : (value + 5) / 10;

        /// <summary>The shade of a stat's filled segments: full at 70 and over, weaker below, as the wireframe shades them.</summary>
        public static float Shade(int value) => value >= 70 ? 1f : value >= 50 ? 0.72f : 0.5f;
    }

    /// <summary>A list row as a screen's view model gives it: who, the last word, when, and what is new.</summary>
    public struct UiRowModel
    {
        public string Name;
        public string Preview;
        public string Time;
        public int Unread;
        public float Hue;        // the avatar's hue, degrees (UiColour.HueOf a stable key)
        public bool Online;
        public bool Verified;
        public bool Selected;
        public bool Pinned;
        public string Section;   // the capitals over the block the row opens, or null when it continues the block above
    }

    /// <summary>A service or an action with its price, as a detail's side column lists them.</summary>
    public struct UiServiceModel
    {
        public string Title;
        public string Price;     // "Free", a figure, or "Quote"
        public string About;
        public bool Disabled;
    }

    /// <summary>A choice of a desk session: its key, its name, the trade-off and a note.</summary>
    public struct UiChoiceModel
    {
        public UiAction Key;     // the action that picks it, or Count for none
        public string Name;
        public string Trade;
        public string Note;
        public bool Selected;
        public bool Disabled;
    }

    /// <summary>A figure on ten segments (a skill, satisfaction), 0 to 100.</summary>
    public struct UiStatModel
    {
        public string Label;
        public string Figure;
        public int Value;
    }

    /// <summary>A key and its value.</summary>
    public struct UiPairModel
    {
        public string Key;
        public string Value;
    }

    /// <summary>Who sent a message: the other side, the player, or the system's own line.</summary>
    public enum UiFrom : byte
    {
        Them = 0,
        Me,
        System
    }

    /// <summary>One message of a thread.</summary>
    public struct UiMessageModel
    {
        public UiFrom From;
        public string Text;
        public string Time;
        public bool HasChoices;  // the game's own message carries buttons or a form: the kit offers the classic view for it
    }

    /// <summary>A group or a tab of a sidebar.</summary>
    public struct UiNavModel
    {
        public string Label;
        public string Count;
        public float Hue;
        public bool Selected;
        public bool Loud;        // the count is unread: a badge, not a quiet figure
    }

    /// <summary>A figure with its change, for a dashboard.</summary>
    public struct UiFigureModel
    {
        public string Label;
        public string Value;
        public string Change;    // "+4.2%", or null
        public bool Up;          // the change is good news (green up), or bad (red down)
        public float[] Trend;    // a sparkline's values, oldest first, or null
    }

    /// <summary>A column of a table.</summary>
    public struct UiColumn
    {
        public string Title;
        public float Width;      // px, on the grid; 0 shares the room left with the other 0s
        public bool Right;       // figures: to the right
    }

    /// <summary>A table's view model: its columns, its rows of cells, the selected row and how far it has scrolled.</summary>
    public sealed class UiTableModel
    {
        public UiColumn[] Columns = new UiColumn[0];
        public string[][] Rows = new string[0][];
        public int RowCount;
        public int Selected = -1;
        public float Offset;
        public string EmptyTitle;
        public string EmptyBody;
    }
}
