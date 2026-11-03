namespace RealLifeOverhaul.Ui
{
    /// <summary>One household as an info panel. Nothing a frame.</summary>
    public sealed class HouseholdScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "household";

        public HouseholdScreen(INewsSource source) : base(ScreenId, NewsApp.Household, UiIcon.Home, source) { }
    }
}
