using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One line of a card: a label with its value, or a sentence on its own.</summary>
    public struct UtilitiesLine
    {
        public string Label;            // null for a sentence
        public string Value;            // the sentence itself when there is no label
        public UiRole Ink;              // the value's ink, Text for the usual
        public bool Check;              // a sentence with a tick or a warning before it
        public bool Met;
    }

    /// <summary>An amount the player steps up and down on a card: its name, its figure and what the figure means.</summary>
    public struct UtilitiesStep
    {
        public string Label;
        public string Value;
        public string Hint;
        public bool CanLess, CanMore;
        public int TypeCommand;     // 0 for a figure that is only stepped; else the command of the field the player types the figure into (a bank page's amounts)
        public int TypeArg;
    }

    /// <summary>A panel of facts and what to do about them: the chosen account, the chosen plan, the company's settings.</summary>
    public sealed class UtilitiesCard
    {
        public string Title;
        public bool Heading;            // the title is a section's label in capitals, not a name
        public string Sub;              // a line under the title
        public string Chip;
        public UiRole ChipTone = UiRole.Ok;
        public UtilitiesLine[] Lines = new UtilitiesLine[0];
        public UtilitiesStep[] Steps = new UtilitiesStep[0];
        public string SwitchLabel;      // null for none
        public bool SwitchOn;
        public bool SwitchDisabled;
        public UiActionModel[] Actions = new UiActionModel[0];
    }

    /// <summary>A district's row on the network page.</summary>
    public struct UtilitiesDistrictRow
    {
        public string Name;
        public int Hue;                 // which of the chart hues marks it, 0 to 7
        public string Homes;
        public string Customers;        // null while it has none
        public int TakeUp;              // 0 to 100, or -1 for no bar
        public string TakeUpWords;      // "55%", or "3 days left", "$186,000 to dig"
        public string State;
        public UiRole StateTone;
        public string Action;           // "Dig", "Extend", or null for none
        public int ActionDistrict;
        public bool Bar;                // the take-up is a bar with its figure; else words
    }

    /// <summary>A plan or a provider on the network page's side: a row of a name, a line under it and a price.</summary>
    public struct UtilitiesSideRow
    {
        public string Name;
        public string Sub;
        public string Price;
        public string Per;
        public UiIcon Icon;
    }

    /// <summary>The broadband company's page: its hero, its districts, its plans, its rivals and its customers.</summary>
    public sealed class UtilitiesNetworkModel
    {
        public int State;               // 0 not founded, 1 running, 2 sold
        public string Name;
        public string Line;             // a sentence under the name (the pitch, the sale)
        public string[] Chips = new string[0];
        public UiRole[] ChipTones = new UiRole[0];
        public string[] FigureLabels = new string[0];
        public string[] FigureValues = new string[0];
        public UiRole[] FigureInks = new UiRole[0];
        public UiActionModel[] Actions = new UiActionModel[0];
        public string DistrictsTitle;
        public string DistrictsLink;    // "Dig in a new district", or null
        public string[] Heads = new string[0];
        public UtilitiesDistrictRow[] Districts = new UtilitiesDistrictRow[0];
        public string TotalName, TotalHomes, TotalCustomers, TotalTakeUp;
        public string PlansTitle;
        public string PlansLink;        // "Set prices", or null
        public UtilitiesSideRow[] Plans = new UtilitiesSideRow[0];
        public string ProvidersTitle;
        public UtilitiesSideRow[] Providers = new UtilitiesSideRow[0];
        public UiChartModel Chart;
        public UtilitiesCard Gets;      // what founding the company gets, while it is not founded
        public string EmptyTitle, EmptyBody;   // a sold company's left side
    }

    /// <summary>What is over the page: a question, the places to dig in, or the company's settings.</summary>
    public enum UtilitiesSheetKind : byte
    {
        None = 0,
        Question,       // a title, a body, Cancel and the answer
        Dig,            // the districts not started, each with its cost
        Settings        // prices, maintenance and marketing, and the sale
    }

    /// <summary>The sheet over the page: its title and what it holds (a question's words and answers, the places to dig in, or the settings).</summary>
    public sealed class UtilitiesSheetModel
    {
        public UtilitiesSheetKind Kind;
        public string Title;
        public string Body;
        public UiActionModel[] Answers = new UiActionModel[0];
        public UtilitiesCard Card;                  // the settings
        public UtilitiesDistrictRow[] Districts = new UtilitiesDistrictRow[0];   // the places to dig in
        public string Close;                        // the close button's words
    }

    /// <summary>What the Utilities screen shows: a tab's page, and a sheet over it. Plain words and figures; the layout decides where they stand.</summary>
    public sealed class UtilitiesFaceModel
    {
        public string Title;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public string OffTitle, OffBody;            // the utilities are off, or no save is loaded
        public string Headline;                     // the accounts' or the places' one line
        public UiDataTableModel Accounts;
        public UtilitiesCard AccountCard;
        public UiDataTableModel Places;
        public string PlansTitle;
        public UiDataTableModel Plans;
        public UtilitiesCard BuyCard;
        public UtilitiesNetworkModel Network;
        public UtilitiesSheetModel Sheet;

        // Written back by the layout: the clip and the list of each table, so a wheel and a focus reach the one they belong to.
        public short AccountsClip = -1, PlacesClip = -1, PlansClip = -1;
        public short AccountsList = -1, PlacesList = -1, PlansList = -1;
    }
}
