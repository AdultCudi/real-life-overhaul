namespace RealLifeOverhaul.Ui
{
    /// <summary>The City Ledger edition on the kit, opened by picking up the paper. Nothing a frame.</summary>
    public sealed class LedgerScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "ledger";

        public LedgerScreen(INewsSource source) : base(ScreenId, NewsApp.Paper, UiIcon.Newspaper, source) { }
    }
}
