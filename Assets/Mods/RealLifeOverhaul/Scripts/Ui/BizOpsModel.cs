namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of an operations form holds: a heading, a fact, or one of the game's own controls mirrored.</summary>
    public enum BizOpsRowKind : byte
    {
        Heading = 0,   // a section's label in capitals, starting a block
        Info,          // a label and its value
        Switch,        // the game's toggle
        Choice,        // the game's drop-down
        Slider,        // the game's slider, held to step
        Stepper,       // the game's amount: minus, the typed figure, plus
        Text,          // the game's input field
        Button,        // one of the game's buttons on the form
        Meter,         // a graded bar (an average skill, satisfaction), read only
        Note           // a quiet sentence wrapped to the column
    }

    /// <summary>
    /// A line of an operations form (a manager's plan, a machine's recipe, a vehicle slot's driver): its kind, its words, its
    /// value or state, and the number its press carries back to the screen, which presses the game's own widget for it.
    /// </summary>
    public struct BizOpsRow
    {
        public BizOpsRowKind Kind;
        public string Label;
        public string Value;        // a fact's value, a choice's option, a stepper's figure, a field's words, a slider's figure, a button's words
        public string Placeholder;  // an empty field's hint
        public int Grade;           // a meter's 0 to 100
        public float Share;         // a slider's place, 0 to 1
        public bool On;             // a switch's state
        public bool Held;           // a slider held to take Left and Right
        public bool Typing;         // a field typed into now
        public bool CanLess, CanMore;
        public bool Disabled;       // the game holds it now (Going Public's locks, a plan without a manager)
        public bool Right;          // laid in the form's right column
        public UiRole Tone;         // a fact's value's tone; a button's danger
        public UiIcon Icon;         // a button's glyph
        public int Arg;             // what the screen presses for it
    }

    /// <summary>A stop of a logistics route: the place goods go to, its goods' count, and whether its goods are open beneath it.</summary>
    public struct BizOpsStop
    {
        public string Name;         // the place, or "Choose a business"
        public string Line;         // "6 goods", or what is wrong
        public bool Open;
        public bool Warn;           // past the route's limit
        public bool Removable;
        public string[] Goods;      // the goods it takes, when open
        public string[] Amounts;
    }

    /// <summary>
    /// The operations of a place in BizMan, as plain data: the schedule as a board of shifts with the people beside it, a
    /// manager kind as its plans and the chosen one's form and goods, a factory's machines and the chosen one's recipe, a
    /// warehouse's drivers and its stock. The game's side fills it from the game's own widgets after the game and other mods
    /// ran; the samples fill it offline. No colour but a role, no position. Game-free.
    /// </summary>
    public sealed class BizOpsModel
    {
        // The schedule
        public UiBoardModel Board;
        public string DayOpenWords;                     // "Open on Monday"
        public bool DayOpen;
        public bool DayOpenDisabled;                    // a headquarters keeps the game's own days
        public string HoursWords;                       // the day's opening hours in the game's form, or that it is closed
        public string LaneSearch;                       // the lanes' search as typed
        public string LaneSearchHint;
        public string PeopleTitle;                      // "Staff" or "Who works at Register 1 from 10:00"
        public string PeopleLine;                       // what a press does, or why the list is empty
        public bool Picking;                            // the game's picker is open for an hour of a lane
        public UiDataTableModel People;
        public string Progress;                         // the auto fill at work, or null
        public int ProgressShare;                       // 0 to 100
        public string CancelWords;                      // the auto fill's Cancel, in the game's own word

        // A manager kind
        public string[] Tabs = new string[0];           // a kind's own tabs (logistics: warehouses and factories; headhunters: recruiting and replacement)
        public int Tab;
        public string PlansTitle;
        public BizManItem[] Plans = new BizManItem[0];  // each plan: what it covers, who runs it, chosen in the cyan fifth
        public string PlansEmpty, PlansEmptyBody;
        public UiActionModel[] PlanActions = new UiActionModel[0];   // add a plan, move the chosen one up or down
        public string FormTitle;                        // the chosen plan's or machine's name over its form
        public string FormLead;                         // a sentence over the form (the businesses it covers)
        public string FormEmpty, FormEmptyBody;         // no plan chosen yet: what the right side will hold and how
        public BizOpsRow[] Rows = new BizOpsRow[0];
        public string TableTitle;
        public UiDataTableModel Table;
        public string StopsTitle;
        public BizOpsStop[] Stops = new BizOpsStop[0];
        public UiActionModel[] Actions = new UiActionModel[0];       // the form's actions, the main one last

        // A choice's list open over the page: a form row's, a stop's, the people's filter
        public int ListOpen = -1;                       // the row (or BizManPages.OpsStopList + a stop, BizManPages.OpsPeopleList for the people's filter) whose list is open
        public string[] Options = new string[0];
        public int OptionChosen = -1;
        public float OptionOffset;

        public UiCardModel Card;                        // a question the game asks inside the page (the auto fill's speed), drawn over it, or null
    }
}
