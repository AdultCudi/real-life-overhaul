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
        Notice
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
        public UiCatalogueModel Catalogue;
        public UiReaderModel Reader;
        public UiEditionModel Edition;
        public UiDecisionModel Decision;
        public UiInfoViewModel People;
        public UiInfoModel Household;
        public string Note;
        public string Note2;
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
    }

    /// <summary>Commands past the kit blocks. A link's argument is which row.</summary>
    public static class NewsCommands
    {
        public const int Link = 440;
        public const int Rent = 445;
        public const int Busy = 446;
        public const int Home = 447;
    }
}
