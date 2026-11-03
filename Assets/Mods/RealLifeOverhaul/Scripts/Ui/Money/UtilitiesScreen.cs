namespace RealLifeOverhaul.Ui
{
    /// <summary>Bills and the broadband company, on the kit. Nothing a frame.</summary>
    public sealed class UtilitiesScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "utilities";

        public UtilitiesScreen(IMoneySource source) : base(ScreenId, MoneyApp.Utilities, UiIcon.Globe, source) { }
    }
}
