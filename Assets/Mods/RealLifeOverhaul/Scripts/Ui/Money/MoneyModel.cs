namespace RealLifeOverhaul.Ui
{
    /// <summary>Which of the three money apps a model is, so one page drawer can keep their shapes apart. Utilities has a face of its own (UtilitiesScreen).</summary>
    public enum MoneyApp : byte
    {
        Finance = 0,
        Banks,
        OwnBank
    }

    /// <summary>How a page is shaped. Each app uses a few of these, never one dashboard for all of them.</summary>
    public enum MoneyShape : byte
    {
        Overview = 0,   // a borrower or a bank at a glance, a donut, then the lines
        Offer,          // one loan: the hero, the amount, what it costs
        Flow,           // a statement, with a chart of the month over it
        League,         // the rich list and the chosen rival
        Factors,        // graded reasons, a sentence each
        Apply,          // a form in two columns and the headroom
        Home,           // a business's hero, its month, then its table
        Table,          // one big table
        Fund,           // the fund's line, its holdings, its largest accounts
        Counter         // a bank's counter: the bank picker, a head, then what the tab shows on the left and its cards of facts and buttons on the right
    }

    /// <summary>
    /// What one money page shows. Plain words and numbers: the page drawer decides where they stand. A sample and the
    /// live fill write the same fields.
    /// </summary>
    public sealed class MoneyModel
    {
        public MoneyApp App;
        public MoneyShape Shape;
        public string Title;
        public UiIcon Icon = UiIcon.Wallet;
        public bool Across;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public UiProfileModel Profile;
        public UiActionModel[] Actions = new UiActionModel[0];
        public UiHeroModel Hero;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public string DonutTitle;
        public float[] Donut = new float[0];
        public string[] DonutLabels = new string[0];
        public string[] DonutValues = new string[0];
        public UiStatementModel Statement;
        public UiChartModel Chart;
        public UiDataTableModel Table;
        public UiCatalogueModel Catalogue;
        public string Amount;
        public bool CanLess, CanMore;
        public string Note;
        public string Note2;
        public string[] Lines = new string[0];
        public string[] LineBody = new string[0];
        public bool[] LineOn = new bool[0];
        public int[] Marks = new int[0];          // a factor's grade, 0 to 100, when the page is graded bars
        public int Meter = -1;
        public string MeterLabel;
        public string[] Choices = new string[0];
        public string[] ChoiceBody = new string[0];
        public UiPrompt[] Prompts = new UiPrompt[0];

        // The counter shape (Banks, Your bank): the choices, the cards beside the page, a second table under the first.
        public string[] PickNames = new string[0];          // the banks, as a strip under the head; the chosen one lit
        public int PickAt;
        public string Headline;                             // one line over the page
        public UtilitiesCard[] Cards = new UtilitiesCard[0];   // the facts and buttons of what the page is about, in a column at the right
        public MoneyStep[][] CardSteps = new MoneyStep[0][];   // beside each card: what each of its steps does
        public int[] CardSwitchActs = new int[0];              // beside each card: the act its switch does (0 for none)
        public string[] RowIds = new string[0];            // the first table's rows, as the view keeps a pick
        public string Table2Title;
        public UiDataTableModel Table2;
        public string[] RowIds2 = new string[0];
        public string[] PeriodNames = new string[0];       // a statement's periods; the one shown is the view's
        public string Typing;                               // the digits being typed into a step's field, or null
        public short TableList = -1, TableClip = -1, Table2List = -1, Table2Clip = -1, StatementList = -1, StatementClip = -1;   // written back by the layout: where each scrolls
        public float[] PickEdges = new float[0];            // written back: the right edge of each bank of the strip

        /// <summary>What the player has chosen on the page: it belongs to the host and a fill never clears it.</summary>
        public MoneyView View = new MoneyView();

        /// <summary>
        /// Clears what a page shows before it is filled again: a tab that does not set a part must not show the one before's
        /// (a statement, a chart, a table, a button left from another tab). The app, the icon and the tab are the host's and stay.
        /// </summary>
        public void Reset()
        {
            Shape = MoneyShape.Overview;
            Title = null;
            Across = false;
            Nav = new UiNavEntry[0];
            Profile = null;
            Actions = new UiActionModel[0];
            Hero = null;
            Figures = new UiFigureModel[0];
            DonutTitle = null;
            Donut = new float[0];
            DonutLabels = new string[0];
            DonutValues = new string[0];
            Statement = null;
            Chart = null;
            Table = null;
            Catalogue = null;
            Amount = null;
            CanLess = CanMore = false;
            Note = null;
            Note2 = null;
            Lines = new string[0];
            LineBody = new string[0];
            LineOn = new bool[0];
            Marks = new int[0];
            Meter = -1;
            MeterLabel = null;
            Choices = new string[0];
            ChoiceBody = new string[0];
            Prompts = new UiPrompt[0];
            PickNames = new string[0];
            PickAt = 0;
            Headline = null;
            Cards = new UtilitiesCard[0];
            CardSteps = new MoneyStep[0][];
            CardSwitchActs = new int[0];
            RowIds = new string[0];
            Table2Title = null;
            Table2 = null;
            RowIds2 = new string[0];
            PeriodNames = new string[0];
            EmptyTitle = null;
            EmptyBody = null;
        }

        /// <summary>The live fill, set by the game. Samples leave it unset and draw the model as it stands.</summary>
        public IMoneySource Source;
        public string EmptyTitle;
        public string EmptyBody;
        public UiIcon EmptyIcon = UiIcon.Wallet;
    }

    /// <summary>Fills a money page from the save when it changes, and answers a press. The game sets it; a sample does not.</summary>
    public interface IMoneySource
    {
        int Generation { get; }

        void Fill(MoneyModel model);

        bool Press(MoneyModel model, int command, int arg);
    }

    /// <summary>A source that carries the buttons of a bank's page to the feature that owns each: the screen says what, the source does it in the game.</summary>
    public interface IMoneyActs
    {
        /// <summary>One act of <see cref="MoneyAct"/>: the id of the thing (a debt, a segment, a branch) and the value (a target step, an
        /// amount, 1 or 0 for a switch). True when it did something.</summary>
        bool Do(MoneyAct act, MoneyView view, int id, int value);
    }

    /// <summary>
    /// Everything a bank page's buttons do. The view-only ones (a bank picked, a step of an amount) never reach the game; the rest go
    /// to the director or the desk the old app used. Saved nowhere: a number the checks and the wiring table name.
    /// </summary>
    public enum MoneyAct : byte
    {
        None = 0,
        // Banks
        Pay = 1, PayOff, PayOverdue, Plan, CannotPay, DailyPayment, Draw, PayMinimum, Close, OpenOverdraft, OpenCard, ApplyLoan, ApplyBusiness, ApproveFinance, CancelApproval, Consolidate,
        // Your bank
        Found = 30, PutIn, TakeOut, Sell, SaverRate, LoanRate, Lending, Strategy, FundLimit, PayMode, PayShare, PayFixed, DividendEvery, DividendShare, MoveUp
    }

    /// <summary>What a step of a card does: a view's field set to its next or previous value, or an act of the game with the value it asks for.</summary>
    public struct MoneyStep
    {
        public MoneyAct Act;        // None: only the view's field changes
        public int Field;           // the view's field (MoneyView.Set) while Act is None; for a typed step too
        public int Id;              // the thing the act is for (a segment, a debt)
        public int Less, More;      // the values a minus and a plus take the figure to
        public bool CanLess, CanMore;
        public bool Typed;         // its figure is a field the player types into (digits)
        public int Low, High;      // the range a typed figure is kept to
    }

    /// <summary>The player's choices on a bank page (the host's: a fill reads them, a press writes them).</summary>
    public sealed class MoneyView
    {
        public const int FBank = 0, FProduct = 1, FAmount = 2, FDeposit = 3, FTerm = 4, FShop = 5, FPay = 6, FPeriod = 7, FDraw = 8, FPut = 9, FCount = 10;

        public const int DefaultPeriod = 1;   // the statement opens on the week (CashFlow.Week)

        public readonly int[] F = new int[FCount];
        public string Row;

        public MoneyView()
        {
            F[FPeriod] = DefaultPeriod;
        }          // the row picked on the list, as the fill names it ("debt:12", "s|1")
        public int Bank { get => F[FBank]; set => F[FBank] = value; }

        public void Set(int field, int value)
        {
            if (field >= 0 && field < FCount)
            {
                F[field] = value;
            }
        }

        /// <summary>A choice that starts the page over: the row, the amounts and the steps.</summary>
        public void Reset(bool keepBank)
        {
            int bank = F[FBank];
            for (int i = 0; i < F.Length; i++)
            {
                F[i] = 0;
            }
            F[FBank] = keepBank ? bank : 0;
            F[FPeriod] = DefaultPeriod;
            Row = null;
        }
    }

    /// <summary>The commands a money page adds past the kit's own blocks.</summary>
    public static class MoneyCommands
    {
        public const int StepLess = 400, StepType = 401, StepMore = 402;
        public const int Choice = 410;
        public const int Follow = 420;
        public const int Export = 430;
        public const int Link = 440;

        // The counter shape's own block, 900 to 959 (nobody else's: Finance uses 800 to 832, Utilities 800 to 815, and the kit's blocks are under 700):
        // 900 a bank picked, 901 a card's button (arg the act), 902 a tab link (arg the tab), 903 a statement period, 904 back out of a sub page,
        // 905 a step's typed figure (arg card x 8 + step), 910 to 919 a card's minus, 920 to 929 its plus, 930 to 939 its switch (k the card).
        public const int PickBank = 900, CardAct = 901, Go = 902, PeriodNext = 903, BackOut = 904, TypeField = 905;
        public const int CardLess = 910, CardMore = 920, CardSwitch = 930, CardsMost = 10;
    }
}
