using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a block of a page is. A page of the Brands, Ventures and Web shop apps is a head and two columns of these.</summary>
    public enum VentBlockKind : byte
    {
        Section,   // capitals over the rows that follow
        Row,       // a label and its value
        Note,      // a quiet sentence, wrapped, with the info glyph or the warning's
        Check,     // a requirement or a promise: kept, pending or broken
        Figures,   // labelled numbers side by side, a spark where there is a history
        Chart,     // a line chart of a history
        Table,     // the kit's big table
        Items,     // short rows: a glyph or a face, a title, a line under it, a chip and an amount
        Switch,    // a setting that is on or off
        Stepper,   // an amount to change
        Actions,   // buttons side by side
        Meter,     // a graded bar with its label
        Steps,     // a ring of months or weeks as segments, the reached ones filled
        Link,      // a cyan link that opens or locates something
        Choice     // a setting with a few values, pressed to go to the next
    }

    /// <summary>A short row of a page: a glyph or a face, a title and a line under it, a chip and an amount. A press runs <see cref="Run"/>.</summary>
    public struct VentItem
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Title, Sub, Chip, Amount;
        public UiRole ChipTone, AmountInk;
        public int Run;          // an index into the model's runs, or -1 for a row that only says something
        public string Face;      // a name to paint a portrait of in the glyph's place, or null
        public float Hue;
        public UiRole Ring;
    }

    /// <summary>One block of a column, by kind; only the fields of its kind are read.</summary>
    public sealed class VentBlock
    {
        public VentBlockKind Kind;
        public string Text, Value;
        public UiRole Tone = UiRole.Text;
        public bool Met, Pending;                 // a check: kept, or still to come (neither: broken)
        public UiFigureModel[] Figs = new UiFigureModel[0];
        public UiChartModel Chart;
        public float ChartHeight = 168f;
        public UiDataTableModel Table;
        public int Base = UiDataTable.First;      // the table's commands start here; a page with two tables gives the second another base
        public string[] RowKeys = new string[0];  // by row of the table or the items: what the page picks when it is pressed
        public int[] RowRuns = new int[0];        // by row of the table: the run of the row's own action button, or -1
        public VentItem[] Items = new VentItem[0];
        public int Run = -1, Run2 = -1;           // a switch, a link or a choice: its run; a stepper's minus (Run) and plus (Run2)
        public bool On, CanLess = true, CanMore = true, Disabled;
        public string Hint;                       // a stepper's typed figure hint, a switch's word
        public UiActionModel[] Actions = new UiActionModel[0];
        public int Grade;                         // a meter's value, 0 to 100
        public int Count, Reached;                // a steps ring: its segments and how many are reached
        public int Rows = 6;                      // a table's room in rows when it is the last of its column
        public string EmptyTitle, EmptyBody;      // an items block with no rows
        public UiIcon Icon = UiIcon.None;
        public short List = -1, Clip = -1;        // written by the face: the table's list and scroll region, for the wheel and the pad

        public static VentBlock Section(string text) => new VentBlock { Kind = VentBlockKind.Section, Text = text };
        public static VentBlock Row(string label, string value, UiRole ink = UiRole.Text) => new VentBlock { Kind = VentBlockKind.Row, Text = label, Value = value, Tone = ink };
        public static VentBlock Note(string text, UiRole tone = UiRole.None) => new VentBlock { Kind = VentBlockKind.Note, Text = text, Tone = tone };
        public static VentBlock Check(string text, bool met, bool pending = false) => new VentBlock { Kind = VentBlockKind.Check, Text = text, Met = met, Pending = pending };
        public static VentBlock Link(string text, int run) => new VentBlock { Kind = VentBlockKind.Link, Text = text, Run = run };
        public static VentBlock Meter(string label, int grade, string figure = null) => new VentBlock { Kind = VentBlockKind.Meter, Text = label, Grade = grade, Value = figure };
        public static VentBlock Steps(string label, int count, int reached) => new VentBlock { Kind = VentBlockKind.Steps, Text = label, Count = count, Reached = reached };
        public static VentBlock Switch(string label, bool on, int run, bool disabled = false, string hint = null) =>
            new VentBlock { Kind = VentBlockKind.Switch, Text = label, On = on, Run = run, Disabled = disabled, Hint = hint };
        public static VentBlock Stepper(string label, string value, int less, bool canLess, bool canMore, string hint = null) =>
            new VentBlock { Kind = VentBlockKind.Stepper, Text = label, Value = value, Run = less, Run2 = less + 1, CanLess = canLess, CanMore = canMore, Hint = hint };
        public static VentBlock Choice(string label, string value, int run, bool disabled = false) =>
            new VentBlock { Kind = VentBlockKind.Choice, Text = label, Value = value, Run = run, Disabled = disabled };
        public static VentBlock Buttons(params UiActionModel[] actions) => new VentBlock { Kind = VentBlockKind.Actions, Actions = actions };
        public static VentBlock Figures(params UiFigureModel[] figs) => new VentBlock { Kind = VentBlockKind.Figures, Figs = figs };
    }

    /// <summary>What a Brands, Ventures or Web shop page is.</summary>
    public enum VentShape : byte
    {
        Off = 0,    // the app cannot run: what it is and how to switch it on
        Page        // the window with its head and its two columns
    }

    /// <summary>
    /// The page of one of the three apps in plain data: the window's tabs, a head (a profile or a hero with figures), the buttons at its right, and a left and a right column
    /// of blocks. The live source and the samples write it with the same builders, so a preview shows what the game shows.
    /// </summary>
    public sealed class VentModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Grid;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public VentShape Shape = VentShape.Page;
        public string OffTitle, OffBody;
        public UiProfileModel Profile;
        public UiHeroModel Hero;
        public UiFigureModel[] HeroFigures = new UiFigureModel[0];
        public string Next;                                          // a line of what to do, when the page has no profile
        public UiActionModel[] HeadActions = new UiActionModel[0];   // at the head's right, the primary last; Arg is a run, or VentFace.BackArg for the way back
        public VentBlock[] Left = new VentBlock[0];
        public VentBlock[] Right = new VentBlock[0];
        public Func<bool>[] Runs = new Func<bool>[0];
        public bool Sub;                                             // a page under a tab (Back steps out of it first)
        public string Empty;                                         // a sentence in the middle of a page with no blocks
        public string[] Prompts = new string[0];                     // the bar's words for Move, Select and Back, from the page's text source
        public Func<int, string> TypedText;                          // by stepper (the argument of its minus), the figure as it is typed: what the field starts with
        public Func<int, string, bool> Typed;                        // by stepper, the typed words taken; false when they are no figure

        /// <summary>The first table or items block with the given command base, for the screen's handlers.</summary>
        public VentBlock TableOf(int command)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                VentBlock[] side = pass == 0 ? Left : Right;
                for (int i = 0; i < side.Length; i++)
                {
                    VentBlock b = side[i];
                    if ((b.Kind == VentBlockKind.Table && command >= b.Base && command < b.Base + 10) || (b.Kind == VentBlockKind.Table && (command == b.Base + UiDataTable.Act
                        || command == b.Base + UiDataTable.Less || command == b.Base + UiDataTable.More || command == b.Base + UiDataTable.Type)))
                    {
                        return b;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>A growing list of runs: a page adds a press and keeps its index, so a target is drawn for the run it will fire.</summary>
    public sealed class VentRuns
    {
        private readonly System.Collections.Generic.List<Func<bool>> _list = new System.Collections.Generic.List<Func<bool>>(16);

        public int Add(Func<bool> run)
        {
            _list.Add(run);
            return _list.Count - 1;
        }

        public Func<bool>[] ToArray() => _list.ToArray();
    }
}
