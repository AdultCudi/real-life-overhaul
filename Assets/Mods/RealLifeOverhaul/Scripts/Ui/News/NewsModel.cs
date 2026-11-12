namespace RealLifeOverhaul.Ui
{
    /// <summary>Which news screen a model belongs to.</summary>
    public enum NewsApp : byte
    {
        Economy = 0,
        People,
        Stand,
        Paper,
        Ending,
        Household,
        Renters
    }

    /// <summary>How the page is shaped. Each one is that screen's own arrangement.</summary>
    public enum NewsShape : byte
    {
        Overview = 0,
        Prices,
        Jobs,
        Districts,
        Markets,
        Finances,
        Insights,
        People,
        Stand,
        Reading,
        Paper,
        Ending,
        Household,
        Renters,
        Notice,
        PaperMarkets,
        PaperPrices
    }

    /// <summary>One piece of advice: a glyph, the sentence, and the link that opens where you act.</summary>
    public struct NewsAdvice
    {
        public UiIcon Icon;
        public string Title;
        public string Body;
        public string Link;
        public int Target;
    }

    /// <summary>
    /// The city's people as a page of the Economy window: a card of headline figures with the links to who pays the most rent and the busiest district,
    /// two donuts, and a table under them (the districts, or the renters of one district). A figure that needs history the save lacks carries its
    /// own words in place of a number.
    /// </summary>
    public sealed class NewsPeople
    {
        public string Title;
        public string HeadLabel;                   // the first row in capitals ("Residents") and its figure
        public string HeadValue;
        public UiInfoRow[] Rows = new UiInfoRow[0];
        public string ChartTitle;
        public string[] ChartLabels = new string[0];
        public string[] ChartValues = new string[0];
        public float[] ChartShares = new float[0];
        public string Chart2Title;
        public string[] Chart2Labels = new string[0];
        public string[] Chart2Values = new string[0];
        public float[] Chart2Shares = new float[0];
        public string TableTitle;
        public UiDataTableModel Table;
    }

    /// <summary>A named fact with a mark of initials: who moved a price this week, what a holding is worth. A headline and a line under it.</summary>
    public struct NewsMove
    {
        public string Mark;
        public UiRole Tint;
        public string Head;
        public string Detail;
    }

    /// <summary>One bar of a ranked list that runs both ways from a centre line: a sector's return over five sessions.</summary>
    public struct NewsBar
    {
        public string Label;
        public string Value;
        public float Share;   // -1 to 1: how far the bar runs from the centre, left for a fall
    }

    /// <summary>
    /// One row of the Newsstand: a paper's mark, what it covers, today's lead headline, its price a week, whether it is
    /// subscribed and the one action that applies (Subscribe, or Stop when subscribed).
    /// </summary>
    public struct NewsTitle
    {
        public UiIcon Icon;            // the Pass has a glyph; a paper has its mark in initials
        public string Mark;
        public UiRole Tint;
        public string Name;
        public string Tagline;
        public string Covers;
        public string LeadLabel;       // "Today's lead"
        public string Lead;            // that paper's lead headline of the day; null for none
        public string EditionLabel;    // "Today's edition"
        public string Edition;         // "On your phone", "Comes at 06:00"
        public UiRole EditionTone;
        public string Price;
        public string PerWeek;         // "a week"
        public string Chip;            // "Subscribed", or null
        public string Renews;          // "Renews day 293", or null
        public string Action;          // "Subscribe" or "Stop"
        public UiButtonKind ActionKind;
        public bool Pass;
    }

    /// <summary>A run of one cycle phase behind the chart, as a share of the window.</summary>
    public struct NewsBand
    {
        public string Label;
        public float Share;
        public UiRole Tint;
    }

    /// <summary>
    /// What one News page shows. Plain words and numbers. The page drawer places them. A sample and the live fill
    /// write the same fields.
    /// </summary>
    public sealed class NewsModel
    {
        public NewsApp App;
        public NewsShape Shape;
        public string Title;
        public UiIcon Icon = UiIcon.Chart;
        public bool Across;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public string Chip;
        public UiRole ChipTone = UiRole.WarnText;
        public string Lead;
        public string Body;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public UiChartModel Chart;
        public NewsBand[] Bands = new NewsBand[0];
        public int[] Marks = new int[0];
        public string RiseLine;
        public NewsAdvice[] Advice = new NewsAdvice[0];
        public string AdviceTitle;
        public UiDataTableModel Table;
        public UiDataTableModel Table2;
        public string TableTitle;                     // the card's label over the table
        public string Table2Title;
        public UiCatalogueModel Catalogue;
        public UiReaderModel Reader;
        public UiEditionModel Edition;
        public UiDecisionModel Decision;
        public NewsPeople People;
        public UiInfoModel Household;                 // a household's rows
        public string PageTitle;                      // the name of a page opened from another, beside its back button
        public string BackLabel;                      // where Back goes from a page opened from another (the page under it); null on a tab
        public string Note;
        public string Note2;
        public string StripNote;                      // the title strip's note at the right: the day, the edition, the price
        public NewsMove[] Moves = new NewsMove[0];    // who moved prices this week, or the holdings; left out when empty
        public string MovesTitle;
        public string MovesLink;                      // the link under a list of moves, or null
        public int MovesTarget;
        public UiChartModel SideChart;                // a second chart beside the main one (the price index by kind)
        public NewsBar[] Bars = new NewsBar[0];
        public string BarsTitle;
        public string BarsNote;
        public string BigValue;                       // the markets page's index level, drawn large above its chart
        public string[] BigChanges = new string[0];
        public bool[] BigGood = new bool[0];
        public UiDataTableModel[] Boards = new UiDataTableModel[0];   // risers, fallers and most traded, side by side
        public string[] BoardTitles = new string[0];
        public NewsTitle[] Titles = new NewsTitle[0];
        public string StandLine;                      // the connection, over the titles
        public UiDataTableModel Shares;               // the Ledger's share prices page
        public UiActionModel[] ReadTitles = new UiActionModel[0];   // the reading page: the papers to pick, the one open as a chip
        public UiActionModel[] ReadDays = new UiActionModel[0];     // ... the days of the open paper's editions
        public UiActionModel ReadPrinted;                           // ... the printed page of the edition open; Label null for none
        public string ReadNote;                                     // ... a quiet line under the chips (a subscription that ended)
        public string EmptyAction;                                  // the label of the button under an empty state that leads to the Titles tab; null for none
        public UiActionModel[] Actions = new UiActionModel[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public string EmptyTitle;
        public string EmptyBody;
        public UiIcon EmptyIcon = UiIcon.Newspaper;
        public INewsSource Source;
    }

    /// <summary>Fills a page when the city changes, and answers a press. The game sets it. A sample does not.</summary>
    public interface INewsSource
    {
        int Generation { get; }

        void Fill(NewsModel model);

        bool Press(NewsModel model, int command, int arg);

        /// <summary>A step back inside the page. True when it had a list or a household open.</summary>
        bool Back();
    }

    /// <summary>Commands past the kit blocks. A link's argument is which row.</summary>
    public static class NewsCommands
    {
        public const int Link = 440;
        public const int Rent = 445;
        public const int Busy = 446;
        public const int Home = 447;
        public const int Back = 449;       // the page's back button: one step to the page it was opened from
        public const int District = 448;   // a district row: added to UiDataTable's own offsets
        public const int Title = 450;      // a paper's action on the Newsstand: its index in Arg
        public const int ReadTitle = 452;  // the reading page: a paper picked to read (its index in Arg)
        public const int ReadDay = 453;    // ... a day of its editions (the day in Arg)
        public const int ReadPrinted = 454;   // ... the edition opened as the printed page
        public const int ToTitles = 455;   // ... the way to the Titles tab, where a paper is taken out
    }
}
