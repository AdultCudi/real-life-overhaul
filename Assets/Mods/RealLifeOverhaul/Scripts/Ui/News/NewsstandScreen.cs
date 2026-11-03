namespace RealLifeOverhaul.Ui
{
    /// <summary>Newsstand on the kit. Subscribe and stop use the paper's own charge. Nothing a frame.</summary>
    public sealed class NewsstandScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "newsstand";

        public NewsstandScreen(INewsSource source) : base(ScreenId, NewsApp.Stand, UiIcon.Newspaper, source) { }
    }
}
