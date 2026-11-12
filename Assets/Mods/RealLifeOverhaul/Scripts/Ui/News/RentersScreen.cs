namespace RealLifeOverhaul.Ui
{
    /// <summary>Who pays the most rent, as a ranked table of real names. Nothing a frame.</summary>
    public sealed class RentersScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "renters";

        public RentersScreen(INewsSource source) : base(ScreenId, NewsApp.Renters, UiIcon.Home, source) { }
    }
}
