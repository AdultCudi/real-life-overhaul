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

        /// <summary>The sign a change's words carry: 1 for "+" or a figure, -1 for "-" or a minus, 0 for none or no words.</summary>
        public static int Sign(string change)
        {
            for (int i = 0; change != null && i < change.Length; i++)
            {
                char c = change[i];
                if (c == '+')
                {
                    return 1;
                }
                if (c == '-' || c == '\u2212')
                {
                    return -1;
                }
                if (char.IsDigit(c))
                {
                    return 0;
                }
            }
            return 0;
        }

        /// <summary>Whether a change rises: by the sign its words carry, else by what the model says.</summary>
        public static bool Rises(ref UiFigureModel figure)
        {
            int sign = Sign(figure.Change);
            return sign != 0 ? sign > 0 : figure.Rising;
        }
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
        public UiIcon Icon;      // a business's or a place's icon on a tile in its family's colour, in place of a person's face
        public UiRole Tint;
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
        public UiLedgerLine[] Table;   // a table the message carries (the tax office's notice): drawn in the bubble under its words; null for none
        public string TableFoot;       // the words under the table ("Please pay by Day 410"); null for none
    }

    /// <summary>A group or a tab of a sidebar.</summary>
    public struct UiNavModel
    {
        public string Label;
        public string Count;
        public float Hue;
        public bool Selected;
        public bool Loud;        // the count is unread: a badge, not a quiet figure
        public UiIcon Icon;      // the group's icon on its tile in the hue's chart colour, or None for the hue's dot
        public UiRole Tint;
    }

    /// <summary>A figure with its change, for a dashboard: the arrow follows the change's sign, the colour whether it is good.</summary>
    public struct UiFigureModel
    {
        public string Label;
        public string Value;
        public string Change;    // "+4.2%", "-3", or null
        public bool Rising;      // the change goes up: read only when its words carry no sign
        public bool Good;        // the change is good for the player (green), or bad (red): rent rising is bad, jobs rising good
        public float[] Trend;    // a sparkline's values, oldest first, or null
        public int Span;         // places of the row it stands in, 0 or 1 for one: a wide figure draws its line beside the number
    }

    /// <summary>A column of a table.</summary>
    public struct UiColumn
    {
        public string Title;
        public float Width;      // px, on the grid; 0 shares the room left with the other 0s
        public bool Right;       // figures: to the right
        public bool Grow;        // an app window's table: this column takes the room left, and the first keeps to its words
    }

    /// <summary>A table's view model: its columns, its rows of cells, the selected row and how far it has scrolled.</summary>
    public sealed class UiTableModel
    {
        public UiColumn[] Columns = new UiColumn[0];
        public string[][] Rows = new string[0][];
        public int RowCount;
        public int Selected = -1;
        public float Offset;      // px scrolled. UiDraw.Table writes back the offset it took, so a screen keeps it for the next layout
        public int Reveal = -1;   // a row the table scrolls into view as it is laid out, or -1
        public float RowHeight;   // px a row takes, 0 for the dense TableRow. A page with room to spare lets its rows breathe (UiDraw.Breathe)
        public bool Inert;        // a table to read: no row is a target, so none looks pressable or answers a press with the error sound
        public bool[] Leads;      // the rows a press opens, or null for all. A row that leads nowhere is no target
        public string EmptyTitle;
        public string EmptyBody;

        /// <summary>Whether a row is a target: a press on it opens something.</summary>
        public bool RowLeads(int row) => !Inert && (Leads == null || (row >= 0 && row < Leads.Length && Leads[row]));
    }
}
