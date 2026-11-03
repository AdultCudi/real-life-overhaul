namespace RealLifeOverhaul.Ui
{
    /// <summary>Which of the four money apps a model is, so one page drawer can keep their shapes apart.</summary>
    public enum MoneyApp : byte
    {
        Finance = 0,
        Banks,
        OwnBank,
        Utilities
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
        Plans,          // a short catalogue
        Network         // the company's hero, its customers, its districts
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

    /// <summary>The commands a money page adds past the kit's own blocks.</summary>
    public static class MoneyCommands
    {
        public const int StepLess = 400, StepType = 401, StepMore = 402;
        public const int Choice = 410;
        public const int Follow = 420;
        public const int Export = 430;
        public const int Link = 440;
    }
}
