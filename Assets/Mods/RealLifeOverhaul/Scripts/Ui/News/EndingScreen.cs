namespace RealLifeOverhaul.Ui
{
    /// <summary>The permadeath ending as a decision card. Nothing a frame.</summary>
    public sealed class EndingScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "ending";

        public EndingScreen(INewsSource source) : base(ScreenId, NewsApp.Ending, UiIcon.Newspaper, source) { }
    }
}
