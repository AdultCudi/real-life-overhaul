namespace RealLifeOverhaul.Ui
{
    /// <summary>The city's people as an info view. Nothing a frame.</summary>
    public sealed class PeopleScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "people";

        public PeopleScreen(INewsSource source) : base(ScreenId, NewsApp.People, UiIcon.People, source) { }
    }
}
